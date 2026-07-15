using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// PRIM-W3 (G9-034): Tamer memory-gain effects via the new TriggeredGainMemoryEffect (AddMemory + owner-turn +
// optional condition). Gain1 gains 1 only if the opponent has a Digimon; Gain2 gains 2.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Gain1: opponent has a Digimon -> +1 (0 -> 1)", () => Gain1(opponentDigimon: true, expected: 1)),
    ("Gain1: opponent has NO Digimon -> no change (0)", () => Gain1(opponentDigimon: false, expected: 0)),
    ("Gain2: always +2 (0 -> 2)", Gain2),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failures.Add(test.Name); Console.Error.WriteLine($"FAIL {test.Name}: {ex.Message}"); }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

async Task Gain1(bool opponentDigimon, int expected)
{
    EngineContext context = Context();
    context.MemoryController.Set(0);
    var tamer = await Place(context, P1, "TAMER", "Tamer", ChoiceZone.BattleArea);
    if (opponentDigimon)
    {
        await Place(context, P2, "FOE", "Digimon", ChoiceZone.BattleArea);
    }

    await Resolve(context, CardEffectFactory.Gain1MemoryTamerOpponentDigimonEffect(new CardSource(context, tamer, P1)));
    AssertEqual(expected, context.MemoryController.Current.Current, $"opponentDigimon={opponentDigimon} -> {expected}");
}

async Task Gain2()
{
    EngineContext context = Context();
    context.MemoryController.Set(0);
    // (#9) Gain2MemoryOptionDelayEffect is now the AS-IS [Main] <Delay>: trash this own battle-area permanent,
    // then gain 2 ONLY if trashed. The permanent is on the battle area, so the self-trash succeeds and +2 is
    // gained. (Full deferred/gated behavior is covered by FAILa-09.)
    var opt = await Place(context, P1, "OPT", "Digimon", ChoiceZone.BattleArea);
    await ((TrashSelfThenGainMemoryDelayEffect)CardEffectFactory.Gain2MemoryOptionDelayEffect(new CardSource(context, opt, P1))).ResolveAsync(CancellationToken.None);
    AssertEqual(2, context.MemoryController.Current.Current, "Gain2 -> trashed self and gained +2");
}

// --- Helpers -------------------------------------------------------------

// (R3-C2b-2 ledger §5.6 close) The Tamer memory-gain factories are now AS-IS 1:1 new-model ActivateClass (no
// ToBinding). Drive them the way the live window does: under an ambient match scope (GManager.instance / AddMemory),
// gate on CanTrigger (CanUse: on battle area + owner turn) + CanActivate (opponent-digimon + CanAddMemory), then
// Activate (the AddMemory coroutine) — keeping the memory-outcome assertions.
async Task Resolve(EngineContext context, ICardEffect effect)
{
    using var scope = AmbientMatchContext.Enter(context);
    var ht = new System.Collections.Hashtable();
    if (effect is ActivateICardEffect ae && effect.CanTrigger(ht) && effect.CanActivate(ht))
    {
        await ae.Activate(ht);
    }
}

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 934);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    context.TurnController.SetPhase(HeadlessPhase.Main);   // past Setup -> DoneStartGame true (window gate)
    return context;
}

async Task<HeadlessEntityId> Place(EngineContext context, HeadlessPlayerId owner, string tag, string cardType, ChoiceZone zone)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, defId.Value, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 4 }, CardType: cardType));
    var id = new HeadlessEntityId($"{owner.Value}:{zone}:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["isSuspended"] = false }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone));
    return id;
}

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
