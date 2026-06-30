using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

// G9-008 (EX8-3): a card that returns a self-static <Vortex> at EffectTiming.OnEndTurn (the original
// EX8_074 "Vortex" region keys it there) now registers the keyword at enter-play, because OnEndTurn was
// added to CardEffectRegistrar.AllTimings. The live end-of-turn trigger GR-006 built
// (EndOfTurnEffectAttack) then sees it and offers the effect-driven attack.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("OnEndTurn is a registered AllTimings dispatch point", OnEndTurnInAllTimings),
    ("Entering play registers the card's OnEndTurn <Vortex> as a live keyword", RegistersVortexOnEnter),
    ("The registered Vortex opens the GR-006 end-of-turn attack window", OpensEndOfTurnWindow),
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

Task OnEndTurnInAllTimings()
{
    AssertTrue(CardEffectRegistrar.AllTimings.Contains(EffectTiming.OnEndTurn),
        "EffectTiming.OnEndTurn is in CardEffectRegistrar.AllTimings (registered at enter-play)");
    return Task.CompletedTask;
}

async Task RegistersVortexOnEnter()
{
    EngineContext context = Context();
    var vortex = await PlaceFixtureDigimon(context, P1, "TfxVortex", suspended: false);

    // Before registration, the keyword is not present (no false positive).
    AssertTrue(!ContinuousKeywordGate.HasKeyword(context, vortex, ContinuousKeywordGate.Vortex),
        "Vortex not present before the card registers");

    bool registered = CardEffectRegistrar.RegisterCard(context, vortex, P1);
    AssertTrue(registered, "the fixture card registered effects on enter-play");
    AssertTrue(ContinuousKeywordGate.HasKeyword(context, vortex, ContinuousKeywordGate.Vortex),
        "Vortex is live after enter-play registration (via the OnEndTurn timing)");
}

async Task OpensEndOfTurnWindow()
{
    EngineContext context = Context();
    var vortex = await PlaceFixtureDigimon(context, P1, "TfxVortex", suspended: false);
    await PlaceFixtureDigimon(context, P2, "FOE", suspended: true);
    CardEffectRegistrar.RegisterCard(context, vortex, P1);

    AssertTrue(EndOfTurnEffectAttack.TryOpen(context, P1),
        "the card-registered Vortex opens the end-of-turn effect-driven attack window");
    AssertEqual(ChoiceType.EffectAttack, context.ChoiceController.PendingRequest!.Type, "an effect-attack choice is pending");
}

// --- Helpers -------------------------------------------------------------

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 71);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    return context;
}

async Task<HeadlessEntityId> PlaceFixtureDigimon(EngineContext context, HeadlessPlayerId owner, string cardNumber, bool suspended)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId(cardNumber);
    cards.Upsert(new CardRecord(defId, cardNumber, cardNumber,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{cardNumber}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = suspended }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
