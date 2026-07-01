using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// PRIM-W3 (G9-035): the three gate-addition restrictions. CantUnsuspend / CanNotBeBlocked verified via the
// ContinuousRestrictionGate Evaluate methods (the same seams the unsuspend step / block gate consult);
// CanNotBeDestroyedBySkill verified END-TO-END by applying an effect Delete through the mutation sink and
// asserting the card survives (and, without the grant, is deleted).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("CantUnsuspendStaticEffect -> EvaluateUnsuspend restricted", CantUnsuspend),
    ("CanNotBeBlockedStaticSelfEffect -> EvaluateBeBlocked restricted", CanNotBeBlocked),
    ("CanNotBeDestroyedBySkill: effect Delete is prevented (card survives)", DeleteBySkillPrevented),
    ("No grant: effect Delete deletes the card (control)", DeleteControl),
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

async Task CantUnsuspend()
{
    EngineContext context = Context();
    var id = await Place(context, P1, "SELF");
    AssertTrue(!ContinuousRestrictionGate.EvaluateUnsuspend(context, id).IsRestricted, "not restricted before grant");
    context.EffectRegistry.Register(CardEffectFactory.CantUnsuspendStaticEffect(false, new CardSource(context, id, P1), null).ToBinding($"cu:{id.Value}"));
    AssertTrue(ContinuousRestrictionGate.EvaluateUnsuspend(context, id).IsRestricted, "does not unsuspend after grant");
}

async Task CanNotBeBlocked()
{
    EngineContext context = Context();
    var id = await Place(context, P1, "SELF");
    AssertTrue(!ContinuousRestrictionGate.EvaluateBeBlocked(context, id).IsRestricted, "not restricted before grant");
    context.EffectRegistry.Register(CardEffectFactory.CanNotBeBlockedStaticSelfEffect(false, new CardSource(context, id, P1), null).ToBinding($"cbb:{id.Value}"));
    AssertTrue(ContinuousRestrictionGate.EvaluateBeBlocked(context, id).IsRestricted, "unblockable after grant");
}

async Task DeleteBySkillPrevented()
{
    EngineContext context = Context();
    var id = await Place(context, P1, "SELF");
    context.EffectRegistry.Register(CardEffectFactory.CanNotBeDestroyedBySkillStaticEffect(false, new CardSource(context, id, P1), null).ToBinding($"cds:{id.Value}"));
    await ApplyDelete(context, id);
    AssertTrue(InBattleArea(context, P1, id), "card survives effect deletion");
}

async Task DeleteControl()
{
    EngineContext context = Context();
    var id = await Place(context, P1, "SELF");
    await ApplyDelete(context, id);
    AssertTrue(!InBattleArea(context, P1, id), "card is deleted without the grant");
}

// --- Helpers -------------------------------------------------------------

async Task ApplyDelete(EngineContext context, HeadlessEntityId targetId)
{
    var sink = new MatchStateMutationSink(
        context.CardInstanceRepository, context.LogSink, context.ZoneMover, context.MemoryController, context.EffectRegistry, context.GameEventQueue);
    sink.Apply(new EffectMutation(MatchStateMutationSink.DeleteKind, new HeadlessEntityId("deleter"),
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["targetEntityId"] = targetId.Value }));
    await sink.FlushAsync();
}

bool InBattleArea(EngineContext context, HeadlessPlayerId owner, HeadlessEntityId id) =>
    context.ZoneMover is IZoneStateReader reader && reader.GetCards(owner, ChoiceZone.BattleArea).Contains(id);

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 935);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    return context;
}

async Task<HeadlessEntityId> Place(EngineContext context, HeadlessPlayerId owner, string tag)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, defId.Value, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = true }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
