// PRIM-P0 B.O.5: PlayOptionCardEffect — play an Option card from a zone as a nested effect (AS-IS PlayOptionCards).
// The selected Option is trashed and its [Main] (OptionSkill) resolves through the same activation cycle. Fixtures:
// TfxPlayOption (plays an Option from hand) + TfxOptionDraw (the Option: [Main] draw 2).
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
HeadlessEntityId Src = new("1:exec:SRC");
HeadlessEntityId Option = new("1:hand:OPT");

var tests = new (string Name, Func<Task> Body)[]
{
    ("playing the Option trashes it and resolves its [Main] (draw 2)", PlaysOptionAndResolvesMain),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failures.Add(test.Name); Console.Error.WriteLine($"FAIL {test.Name}\n{ex.GetType().Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task PlaysOptionAndResolvesMain()
{
    EngineContext context = await Setup();
    Answer(context, ChoiceResult.Select(Option));   // pick the Option to play

    await ActivatedEffectResolver.ResolveAsync(context, Src, P1, EffectTiming.OptionSkill);

    AssertTrue(InZone(context, P1, ChoiceZone.Trash, Option), "the played Option was trashed");
    AssertTrue(!InZone(context, P1, ChoiceZone.Hand, Option), "the Option left the hand");
    // The Option's [Main] drew 2 -> P1 hand now holds the 2 drawn cards (the Option is gone).
    AssertEqual(2, HandCount(context, P1), "the Option's [Main] draw-2 resolved");
}

// --- Harness -------------------------------------------------------------

void Answer(EngineContext context, ChoiceResult result) =>
    ((ScriptedChoiceProvider)context.ChoiceProvider).Enqueue(result);

async Task<EngineContext> Setup()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 33);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    var cards = (CardDatabase)context.CardRepository;

    // The effect source (in Execution).
    var srcDef = new HeadlessEntityId("TfxPlayOption");
    cards.Upsert(new CardRecord(srcDef, "TfxPlayOption", "PlaySrc", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(Src, srcDef, P1, Metadata: new Dictionary<string, object?>()));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, Src, ChoiceZone.None, ChoiceZone.Execution));

    // The Option to play (in hand).
    var optDef = new HeadlessEntityId("TfxOptionDraw");
    cards.Upsert(new CardRecord(optDef, "TfxOptionDraw", "Opt", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Option"));
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(Option, optDef, P1, Metadata: new Dictionary<string, object?>()));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, Option, ChoiceZone.None, ChoiceZone.Hand));

    // P1 library to draw from.
    for (int i = 1; i <= 5; i++)
    {
        var lib = new HeadlessEntityId($"1:lib:{i:D2}");
        cards.Upsert(new CardRecord(new HeadlessEntityId($"DEF:LIB{i}"), $"LIB{i}", $"Lib{i}", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
        context.CardInstanceRepository.Upsert(new CardInstanceRecord(lib, new HeadlessEntityId($"DEF:LIB{i}"), P1, Metadata: new Dictionary<string, object?>()));
        await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, lib, ChoiceZone.None, ChoiceZone.Library));
    }

    return context;
}

int HandCount(EngineContext context, HeadlessPlayerId player) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(player, ChoiceZone.Hand).Count;

bool InZone(EngineContext context, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId card) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(player, zone).Contains(card);

static void AssertTrue(bool value, string label) { if (!value) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"{label}: expected '{expected}', actual '{actual}'.");
}
