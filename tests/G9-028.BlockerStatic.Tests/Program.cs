using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// PRIM-W2 (G9-028): CardEffectFactory.BlockerStaticEffect — a PLAYER-SCOPE Blocker grant ("your Digimon gain
// <Blocker>"). Modeled via the new ContinuousPlayerScopeKeywordEffect; ContinuousKeywordGate.HasKeyword
// (the seam BlockTiming consults) resolves it for any of the scoped player's cards.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("BlockerStaticEffect (owner scope) -> the owner's OTHER Digimon gains Blocker", GrantsOwnerScope),
    ("... but the opponent's Digimon does NOT gain Blocker", NotOpponent),
    ("A false condition -> no grant", ConditionGates),
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

async Task GrantsOwnerScope()
{
    EngineContext context = Context();
    var source = await PlaceDigimon(context, P1, "SRC");
    var ally = await PlaceDigimon(context, P1, "ALLY");

    AssertTrue(!ContinuousKeywordGate.HasKeyword(context, ally, ContinuousKeywordGate.Blocker), "ally has no Blocker before grant");
    Register(context, source, condition: null);
    AssertTrue(ContinuousKeywordGate.HasKeyword(context, ally, ContinuousKeywordGate.Blocker), "the owner's ally now has Blocker (player-scope)");
}

async Task NotOpponent()
{
    EngineContext context = Context();
    var source = await PlaceDigimon(context, P1, "SRC");
    var foe = await PlaceDigimon(context, P2, "FOE");

    Register(context, source, condition: null);
    AssertTrue(!ContinuousKeywordGate.HasKeyword(context, foe, ContinuousKeywordGate.Blocker), "the opponent's Digimon does NOT gain Blocker");
}

async Task ConditionGates()
{
    EngineContext context = Context();
    var source = await PlaceDigimon(context, P1, "SRC");
    var ally = await PlaceDigimon(context, P1, "ALLY");

    Register(context, source, condition: () => false);
    AssertTrue(!ContinuousKeywordGate.HasKeyword(context, ally, ContinuousKeywordGate.Blocker), "false condition -> no Blocker grant");
}

// --- Helpers -------------------------------------------------------------

void Register(EngineContext context, HeadlessEntityId source, Func<bool>? condition) =>
    context.EffectRegistry.Register(
        CardEffectFactory.BlockerStaticEffect(null, false, new CardSource(context, source, P1), condition).ToBinding($"blk:{source.Value}"));

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 928);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    return context;
}

async Task<HeadlessEntityId> PlaceDigimon(EngineContext context, HeadlessPlayerId owner, string tag)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(defId, defId.Value, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = false }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
