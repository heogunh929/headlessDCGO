using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G6-001: a card's ported effects are auto-registered when it ENTERS PLAY (via PlayCardAction), with no
// manual CardEffectRegistrar call, and auto-removed when it LEAVES PLAY (deletion). The card->effect
// dispatch is reflection-based (class name == card number), so it grows as cards are ported.

HeadlessPlayerId P1 = new(1);
HeadlessEntityId Card = new("p1:hand:ST7_10");

var tests = new (string Name, Func<Task> Body)[]
{
    ("Dispatch discovers ported card classes by number; unknown -> miss", () => Pure(DispatchReflection)),
    ("Playing ST7_10 auto-activates its effects (no manual register)", PlayActivatesEffects),
    ("Deleting ST7_10 removes its effects from the registry", DeleteRemovesEffects),
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

static Task Pure(Action body) { body(); return Task.CompletedTask; }

// --- Tests ---------------------------------------------------------------

void DispatchReflection()
{
    AssertTrue(CardEffectDispatch.Count > 0, "dispatch found at least one ported card class");
    AssertTrue(CardEffectDispatch.TryCreate("ST7_10", out CEntity_Effect? a) && a is not null, "ST7_10 resolved");
    AssertTrue(CardEffectDispatch.TryCreate("ST1_03", out CEntity_Effect? b) && b is not null, "ST1_03 resolved");
    AssertFalse(CardEffectDispatch.TryCreate("NOPE_999", out _), "unknown card number is a miss");
}

async Task PlayActivatesEffects()
{
    EngineContext context = await PlayST7_10();
    AssertEqual(2, ContinuousModifierGate.ResolveSecurityAttack(context, Battle(), baseSecurityAttack: 1), "SA +1 active after play");
    // (P6 STAGE B) The flip retires keyword EffectBindings (a ported <Pierce> is a new-model ActivateClass that
    // registers NO binding — stage A stopped registering activated effects). The flip-correct query for keyword
    // presence is the interface scan ContinuousKeywordGate.HasKeyword (new-model HasPierce), which verifies the
    // SAME behaviour the old GetKeywordEffects("Piercing") binding-count check did.
    AssertTrue(ContinuousKeywordGate.HasKeyword(context, Battle(), "Piercing"), "Piercing active after play");
}

async Task DeleteRemovesEffects()
{
    EngineContext context = await PlayST7_10();
    HeadlessEntityId onField = Battle();

    var sink = new MatchStateMutationSink(context.CardInstanceRepository, log: null, context.ZoneMover, memory: null, context.EffectRegistry);
    sink.Apply(new EffectMutation(
        MatchStateMutationSink.DeleteKind,
        new HeadlessEntityId("src"),
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = onField.Value }));
    await sink.FlushAsync();

    AssertEqual(1, ContinuousModifierGate.ResolveSecurityAttack(context, onField, baseSecurityAttack: 1), "SA buff gone after delete");
    // (P6 STAGE B) new-model interface-scan equivalent of the old GetKeywordEffects binding-count==0 check.
    AssertTrue(!ContinuousKeywordGate.HasKeyword(context, onField, "Piercing"), "Piercing gone after delete");
}

// --- Helpers -------------------------------------------------------------

// The played card keeps the same instance id; it just moves Hand -> BattleArea.
HeadlessEntityId Battle() => Card;

async Task<EngineContext> PlayST7_10()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 601);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId("ST7_10"), "ST7_10", "MetalGreymon", new Dictionary<string, object?>(), CardType: "Digimon", PlayCost: 3));
    // (P6 STAGE B) a live game (past Setup) so AS-IS CanUse/CanTrigger's DoneStartGame gate lets the ported
    // continuous effects apply — the new-model interface scan honours that precondition, as every other
    // continuous member in the suite does; the old binding gate ignored it. A real board play IS in Main.
    context.TurnController.Initialize(new[] { P1, new HeadlessPlayerId(2) }, P1);
    context.TurnController.SetPhase(HeadlessPhase.Main);
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(Card, new HeadlessEntityId("ST7_10"), P1));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, Card, ChoiceZone.None, ChoiceZone.Hand));
    context.MemoryController.Set(5);

    ActionProcessResult result = await new PlayCardAction()
        .ProcessAsync(HeadlessActionFactory.PlayCard(P1, Card, 3), context);
    AssertTrue(result.IsSuccess, $"play succeeded ({result.Message})");
    AssertTrue(((IZoneStateReader)context.ZoneMover).GetCards(P1, ChoiceZone.BattleArea).Contains(Card), "card on battle area");
    return context;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertFalse(bool v, string label) { if (v) throw new InvalidOperationException($"{label}: expected false."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
