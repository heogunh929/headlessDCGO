// PRIM-P0-flow Build Order 3 (batch A): select-follow-up primitives. Verifies (1) the resolver runs a returned
// effect LIST in order — so an unconditional chain "select+suspend THEN draw" already works (no new compound
// mechanism needed for unconditional tails), and (2) the new SelectAndReturnToDeckEffect / SelectAndPutSecurityEffect
// factories (over the already-dispatched PutLibrary*/PutSecurity* modes). Fixture: TfxSelectFollowUp.
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
HeadlessEntityId Src = new("1:battle:SRC");

var tests = new (string Name, Func<Task> Body)[]
{
    ("unconditional chain: a returned [select+suspend, draw] list runs BOTH steps in order", SequenceRunsBothSteps),
    ("SelectAndReturnToDeckEffect: the selected opponent Digimon goes to the deck", ReturnToDeck),
    ("SelectAndPutSecurityEffect: the selected opponent Digimon goes to security", PutSecurity),
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

async Task SequenceRunsBothSteps()
{
    (EngineContext context, HeadlessEntityId foe) = await Setup("seq");
    int handBefore = HandCount(context, P1);
    Answer(context, ChoiceResult.Select(foe));   // the suspend target

    await ActivatedEffectResolver.ResolveAsync(context, Src, P1, EffectTiming.OptionSkill);

    AssertTrue(IsSuspended(context, foe), "step 1 ran: opponent Digimon suspended");
    AssertEqual(handBefore + 1, HandCount(context, P1), "step 2 ran in sequence: drew 1");
}

async Task ReturnToDeck()
{
    (EngineContext context, HeadlessEntityId foe) = await Setup("deck");
    Answer(context, ChoiceResult.Select(foe));

    await ActivatedEffectResolver.ResolveAsync(context, Src, P1, EffectTiming.OptionSkill);

    AssertTrue(InZone(context, P2, ChoiceZone.Library, foe), "opponent Digimon returned to the deck");
    AssertTrue(!InZone(context, P2, ChoiceZone.BattleArea, foe), "left the battle area");
}

async Task PutSecurity()
{
    (EngineContext context, HeadlessEntityId foe) = await Setup("sec");
    Answer(context, ChoiceResult.Select(foe));

    await ActivatedEffectResolver.ResolveAsync(context, Src, P1, EffectTiming.OptionSkill);

    AssertTrue(InZone(context, P2, ChoiceZone.Security, foe), "opponent Digimon placed into security");
    AssertTrue(!InZone(context, P2, ChoiceZone.BattleArea, foe), "left the battle area");
}

// --- Harness -------------------------------------------------------------

void Answer(EngineContext context, ChoiceResult result) =>
    ((ScriptedChoiceProvider)context.ChoiceProvider).Enqueue(result);

async Task<(EngineContext Context, HeadlessEntityId Foe)> Setup(string followMode)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 401);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    var cards = (CardDatabase)context.CardRepository;

    var defId = new HeadlessEntityId("TfxSelectFollowUp");
    cards.Upsert(new CardRecord(defId, "TfxSelectFollowUp", "FollowSrc", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(Src, defId, P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["followMode"] = followMode, ["isSuspended"] = false }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, Src, ChoiceZone.None, ChoiceZone.BattleArea));

    // An opponent Digimon to target.
    var foeDef = new HeadlessEntityId("DEF:FOE");
    cards.Upsert(new CardRecord(foeDef, "FOE", "Foe", new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 4 }, CardType: "Digimon"));
    var foe = new HeadlessEntityId("2:battle:FOE");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(foe, foeDef, P2,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["isSuspended"] = false }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, foe, ChoiceZone.None, ChoiceZone.BattleArea));

    // Give P1 a small library to draw from (for the "seq" draw step).
    for (int i = 1; i <= 4; i++)
    {
        var lib = new HeadlessEntityId($"1:lib:{i:D2}");
        cards.Upsert(new CardRecord(new HeadlessEntityId($"DEF:LIB{i}"), $"LIB{i}", $"Lib{i}", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
        context.CardInstanceRepository.Upsert(new CardInstanceRecord(lib, new HeadlessEntityId($"DEF:LIB{i}"), P1, Metadata: new Dictionary<string, object?>()));
        await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, lib, ChoiceZone.None, ChoiceZone.Library));
    }

    return (context, foe);
}

int HandCount(EngineContext context, HeadlessPlayerId player) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(player, ChoiceZone.Hand).Count;

bool InZone(EngineContext context, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(player, zone).Contains(cardId);

bool IsSuspended(EngineContext context, HeadlessEntityId cardId) =>
    context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? r) && r is not null &&
    r.Metadata.TryGetValue("isSuspended", out object? raw) && raw is bool b && b;

static void AssertTrue(bool value, string label) { if (!value) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"{label}: expected '{expected}', actual '{actual}'.");
}
