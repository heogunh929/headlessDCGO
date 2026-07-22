using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
// (R4 S3b) the mirror Script/MainPhaseAction classes share the AS-IS names — pin the Runtime one.
using AttackPermanentAction = HeadlessDCGO.Engine.Headless.Runtime.AttackPermanentAction;

// G6-005: the attack-declaration windows are now emitted by AttackPermanentAction. ST1_06's
// "[When Attacking] lose 2 memory" (registered under OnAllyAttack) fires when it declares an attack in a
// live match — drained through the same collector/scheduler the game loop uses.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
HeadlessEntityId Attacker = new("p1:battle:ST1_06");

var tests = new (string Name, Func<Task> Body)[]
{
    ("Declaring an attack with ST1_06 emits OnAllyAttack and loses 2 memory", AttackFiresTrigger),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex)
    {
        failures.Add(test.Name);
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}");
    }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

async Task AttackFiresTrigger()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 605);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    context.TurnController.SetPhase(HeadlessPhase.Main);

    CardDatabase cards = (CardDatabase)context.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId("ST1_06"), "ST1_06", "Tyrannomon", new Dictionary<string, object?>(), CardType: "Digimon"));
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(Attacker, new HeadlessEntityId("ST1_06"), P1));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, Attacker, ChoiceZone.None, ChoiceZone.BattleArea));

    // The attacker entered play -> auto-register its effects (incl. the OnAllyAttack memory trigger).
    AssertTrue(CardEffectRegistrar.RegisterCard(context, Attacker, P1), "ST1_06 effects registered");
    context.MemoryController.Set(3);

    LegalAction attack = HeadlessActionFactory.DeclareAttack(P1, Attacker, P2, targetId: null, isDirectAttack: true);
    ActionProcessResult result = new AttackPermanentAction().Process(attack, context);
    AssertTrue(result.IsSuccess, $"attack declared ({result.Message})");

    await DrainEvents(context);

    AssertEqual(1, context.MemoryController.Current.Current, "memory 3 - 2 = 1 (OnAllyAttack fired)");
}

// --- Helpers -------------------------------------------------------------

// (G6-005) AttackPermanentAction.Process → AttackDeclarationCommons.Declare → AttackProcess.Attack STACKS the
// [On Attack] "[When Attacking]" ally-attack window (AS-IS AttackProcess.cs:197-199 StackSkillInfos(OnAllyAttack))
// onto the mirror AutoProcessing trigger stack — it does NOT resolve it (AS-IS StackSkillInfos only stacks; the
// game loop's AutoProcessCheck drains it). Drive the SAME game-loop scheduler the pump uses so ST1_06's stacked
// OnAllyAttack ActivateClass (card.Owner.AddMemory(-2)) resolves. Under the ambient match scope like every other
// AutoProcessCheck driver (the stacked skills read live state through GManager.instance).
async Task DrainEvents(EngineContext context)
{
    context.GameEventQueue.DrainPending();
    await context.EffectScheduler.ResolveAllAsync();
    using (HeadlessDCGO.Engine.Headless.Bridge.AmbientMatchContext.Enter(context))
    {
        await HeadlessDCGO.Engine.Assets.Scripts.Script.AutoProcessing.For(context).AutoProcessCheck();
    }
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
