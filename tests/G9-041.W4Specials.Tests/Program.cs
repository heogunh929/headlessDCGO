using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// PRIM-W4 specials/framework (G9-041): ReplaceTopSecurity (behavior), RevealLibrary (informational no-op),
// and the WhenMoving timing (EffectTiming.OnMove -> "OnMove" trigger name registerable).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("ReplaceTopSecurity -> top security to hand + self face-up security", ReplaceTopSecurity),
    ("RevealLibrary -> no state mutation (informational)", RevealLibrary),
    ("WhenMoving -> EffectTiming.OnMove maps to the 'OnMove' trigger name", WhenMovingTiming),
    ("CanNotBeAttackedSelf -> EvaluateBeAttacked restricted", CanNotBeAttacked),
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

async Task ReplaceTopSecurity()
{
    EngineContext context = Context();
    var s1 = await Place(context, P1, "SEC1", ChoiceZone.Security); // top (index 0)
    var s2 = await Place(context, P1, "SEC2", ChoiceZone.Security);
    var self = await Place(context, P1, "SELF", ChoiceZone.Hand);
    var sink = Sink(context);
    new ReplaceBottomSecurityWithFaceUpEffect(new CardSource(context, self, P1), "replace top", top: true).Apply(sink);
    await sink.FlushAsync();
    AssertTrue(Zone(context, P1, ChoiceZone.Hand).Contains(s1), "top security card went to hand");
    AssertTrue(Zone(context, P1, ChoiceZone.Security).Contains(self), "self placed into security");
}

async Task RevealLibrary()
{
    EngineContext context = Context();
    var self = await Place(context, P1, "SELF", ChoiceZone.BattleArea);
    var sink = Sink(context);
    new InformationalRevealEffect(new CardSource(context, self, P1), 3, "reveal 3").Apply(sink);
    await sink.FlushAsync();
    AssertTrue(Zone(context, P1, ChoiceZone.BattleArea).Contains(self), "no state change (self still in play)");
}

Task WhenMovingTiming()
{
    AssertTrue(EffectTimings.ToTriggerName(EffectTiming.OnMove) == "OnMove", "EffectTiming.OnMove -> 'OnMove'");
    return Task.CompletedTask;
}

async Task CanNotBeAttacked()
{
    EngineContext context = Context();
    var id = await Place(context, P1, "SELF", ChoiceZone.BattleArea);
    AssertTrue(!ContinuousRestrictionGate.EvaluateBeAttacked(context, id).IsRestricted, "not restricted before grant");
    context.EffectRegistry.Register(CardEffectFactory.CanNotBeAttackedSelfStaticEffect(false, new CardSource(context, id, P1), null).ToBinding($"cba:{id.Value}"));
    AssertTrue(ContinuousRestrictionGate.EvaluateBeAttacked(context, id).IsRestricted, "cannot be attacked after grant");
}

// --- Helpers -------------------------------------------------------------

MatchStateMutationSink Sink(EngineContext context) => new(
    context.CardInstanceRepository, context.LogSink, context.ZoneMover, context.MemoryController, context.EffectRegistry, context.GameEventQueue);

IReadOnlyList<HeadlessEntityId> Zone(EngineContext context, HeadlessPlayerId owner, ChoiceZone zone) =>
    context.ZoneMover is IZoneStateReader r ? r.GetCards(owner, zone) : Array.Empty<HeadlessEntityId>();

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 941);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    return context;
}

async Task<HeadlessEntityId> Place(EngineContext context, HeadlessPlayerId owner, string tag, ChoiceZone zone)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, defId.Value, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:{zone}:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = false }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone));
    return id;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
