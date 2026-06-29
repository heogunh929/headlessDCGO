using HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST1.Red;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

// Phase 1 — ST1 Red activated wave: Option [Main] select-and-delete effects.
//   ST1_16: delete 1 of the opponent's Digimon
//   ST1_15: delete up to 2 of the opponent's Digimon with 4000 DP or less
// Resolved imperatively (build candidate request -> scripted answer -> apply Delete), the pattern the
// SelectPermanentEffect tests use, since the interactive Option activation flow is not yet wired.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
HeadlessPlayerId[] Both = { P1, P2 };

var tests = new (string Name, Func<Task> Body)[]
{
    ("ST1_16: [Main] deletes the chosen opponent Digimon, leaves the rest", ST1_16_Delete),
    ("ST1_15: [Main] only offers opponent Digimon with DP <= 4000", ST1_15_Candidates),
    ("ST1_15: [Main] deletes up to 2 chosen low-DP Digimon", ST1_15_Delete),
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

async Task ST1_16_Delete()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 16);
    var b1 = new HeadlessEntityId("p2:battle:B1");
    var b2 = new HeadlessEntityId("p2:battle:B2");
    await Place(context, P2, b1, dp: 3000);
    await Place(context, P2, b2, dp: 3000);

    var effect = (ActivatedSelectEffect)Main(new ST1_16(), context);
    ChoiceRequest request = effect.BuildRequest(Both);
    AssertEqual(2, request.Candidates.Count, "both opponent Digimon are candidates");

    var sink = Sink(context);
    var provider = new ScriptedChoiceProvider();
    provider.Enqueue(ChoiceResult.Select(b1));
    ChoiceResult result = await provider.ChooseAsync(request);
    effect.Apply(sink, result.SelectedIds);
    await sink.FlushAsync();

    AssertTrue(InZone(context, P2, ChoiceZone.Trash, b1), "B1 trashed");
    AssertTrue(InZone(context, P2, ChoiceZone.BattleArea, b2), "B2 untouched");
}

async Task ST1_15_Candidates()
{
    (EngineContext context, _, _, _) = await ThreeOpponents();
    var effect = (ActivatedSelectEffect)Main(new ST1_15(), context);
    ChoiceRequest request = effect.BuildRequest(Both);
    AssertEqual(2, request.Candidates.Count, "only the two <=4000 DP Digimon are candidates");
    AssertEqual(2, request.MaxCount, "up to 2");
    AssertEqual(1, request.MinCount, "canEndNotMax -> min 1");
}

async Task ST1_15_Delete()
{
    (EngineContext context, HeadlessEntityId low1, HeadlessEntityId high, HeadlessEntityId low2) = await ThreeOpponents();
    var effect = (ActivatedSelectEffect)Main(new ST1_15(), context);
    ChoiceRequest request = effect.BuildRequest(Both);

    var sink = Sink(context);
    var provider = new ScriptedChoiceProvider();
    provider.Enqueue(ChoiceResult.Select(low1, low2));
    ChoiceResult result = await provider.ChooseAsync(request);
    effect.Apply(sink, result.SelectedIds);
    await sink.FlushAsync();

    AssertTrue(InZone(context, P2, ChoiceZone.Trash, low1), "low1 trashed");
    AssertTrue(InZone(context, P2, ChoiceZone.Trash, low2), "low2 trashed");
    AssertTrue(InZone(context, P2, ChoiceZone.BattleArea, high), "5000 DP Digimon untouched");
}

// --- Helpers -------------------------------------------------------------

// Returns context + (low 3000, high 5000, low 4000).
async Task<(EngineContext, HeadlessEntityId, HeadlessEntityId, HeadlessEntityId)> ThreeOpponents()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 15);
    var low1 = new HeadlessEntityId("p2:battle:LOW1");
    var high = new HeadlessEntityId("p2:battle:HIGH");
    var low2 = new HeadlessEntityId("p2:battle:LOW2");
    await Place(context, P2, low1, dp: 3000);
    await Place(context, P2, high, dp: 5000);
    await Place(context, P2, low2, dp: 4000);
    return (context, low1, high, low2);
}

// The Option card's [Main] effect for a fresh P1-controlled source.
ICardEffect Main(CEntity_Effect card, EngineContext context)
{
    var source = new CardSource(context, new HeadlessEntityId("p1:trash:OPT"), P1);
    return card.CardEffects(EffectTiming.OptionSkill, source).Single();
}

async Task Place(EngineContext context, HeadlessPlayerId owner, HeadlessEntityId id, int dp)
{
    CardDatabase cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{id.Value}");
    cards.Upsert(new CardRecord(defId, defId.Value, id.Value, new Dictionary<string, object?>(), CardType: "Digimon"));
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp };
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner, Metadata: meta));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
}

MatchStateMutationSink Sink(EngineContext context) =>
    new(context.CardInstanceRepository, log: null, context.ZoneMover, memory: null, context.EffectRegistry);

bool InZone(EngineContext context, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(player, zone).Contains(cardId);

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
