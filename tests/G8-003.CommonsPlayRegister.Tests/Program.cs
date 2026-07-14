using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using Commons = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.CardEffectCommons;
using CardSource = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.CardSource;
using CardEffectRegistrar = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.CardEffectRegistrar;

// G8-003: a card played onto the field by the CardEffectCommons IMPERATIVE helper path
// (CardEffectCommons.PlayPermanentCards -> NewSink, NOT the scheduler sink) auto-registers its ported
// continuous/trigger effects — the effect-play registration gap that left G1/G3/G9-played cards inert
// (bt2_bt3_primitive_dev_status.md §4b). Every context-bearing MatchStateMutationSink now defaults its
// enter-play hook to context.RegisterEnteredCardEffects, mirroring AS-IS PlayCardClass.PlayCard(). Also
// asserts enter-play registration is IDEMPOTENT: a card re-entering play (played again) does not throw a
// duplicate-binding error and does not double its continuous effect.

HeadlessPlayerId P1 = new(1);
HeadlessEntityId Played = new("p1:trash:ST7_10");
HeadlessEntityId Source = new("p1:battle:SRC");

var tests = new (string Name, Func<Task> Body)[]
{
    ("PlayPermanentCards (NewSink) from trash auto-registers the played card's effects", CommonsPlayRegisters),
    ("Re-entering play via PlayPermanentCards is idempotent (no duplicate throw, SA not doubled)", ReEntryIsIdempotent),
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

async Task CommonsPlayRegisters()
{
    EngineContext context = NewContext();
    var played = new CardSource(context, Played, P1);
    var source = new CardSource(context, Source, P1);

    await Commons.PlayPermanentCards(
        new[] { played }, source, payCost: false, isTapped: false, root: ChoiceZone.Trash, activateETB: true);

    AssertTrue(((IZoneStateReader)context.ZoneMover).GetCards(P1, ChoiceZone.BattleArea).Contains(Played),
        "ST7_10 played onto the field via PlayPermanentCards");
    AssertEqual(2, new HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.Permanent(context, Played).Strike,
        "SA +1 auto-active after the commons effect-play");
    AssertTrue(context.EffectRegistry.GetKeywordEffects("Piercing").Count >= 1,
        "Piercing auto-registered after the commons effect-play");
}

async Task ReEntryIsIdempotent()
{
    EngineContext context = NewContext();
    var played = new CardSource(context, Played, P1);
    var source = new CardSource(context, Source, P1);

    // First stint on the field (registers ST7_10's effects).
    await Commons.PlayPermanentCards(
        new[] { played }, source, payCost: false, isTapped: false, root: ChoiceZone.Trash, activateETB: true);
    // Leave play back to the trash. Nothing unregisters the stale self-bindings (AS-IS has no registry).
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, Played, ChoiceZone.BattleArea, ChoiceZone.Trash));

    // Re-enter play: RegisterCard must clear the stale bindings first (idempotent) — no duplicate-binding throw.
    await Commons.PlayPermanentCards(
        new[] { played }, source, payCost: false, isTapped: false, root: ChoiceZone.Trash, activateETB: true);

    AssertTrue(((IZoneStateReader)context.ZoneMover).GetCards(P1, ChoiceZone.BattleArea).Contains(Played),
        "ST7_10 re-played onto the field");
    AssertEqual(2, new HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.Permanent(context, Played).Strike,
        "SA +1 applied exactly once after re-entry (not doubled by a stale duplicate binding)");
}

EngineContext NewContext()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 803);
    var cards = (CardDatabase)context.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId("ST7_10"), "ST7_10", "MetalGreymon", new Dictionary<string, object?>(), CardType: "Digimon"));
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(Played, new HeadlessEntityId("ST7_10"), P1));
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(Source, new HeadlessEntityId("ST7_10"), P1));
    context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, Played, ChoiceZone.None, ChoiceZone.Trash)).GetAwaiter().GetResult();
    context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, Source, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
    return context;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
