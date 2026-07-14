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
    AssertTrue(new Permanent(context, id).CanUnsuspend, "not restricted before grant");
    // (P7 stage-B SEAM) CanNotUnsuspendClass is a new-model kind-class with no ToBinding/EffectRegistry
    // bridge — the AS-IS-faithful path is the LIVE cEntity_EffectController.GetCardEffects scan
    // NewModelContinuousScan/ContinuousRestrictionGate now performs. Attach the built effect to the card's
    // controller via the same seam every ported card definition class uses.
    var cs = new CardSource(context, id, P1);
    ICardEffect built = CardEffectFactory.CantUnsuspendStaticEffect(
        permanentCondition: null, isInheritedEffect: false, card: cs, condition: null, effectName: $"cu:{id.Value}");
    cs.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(built);
    AssertTrue(!new Permanent(context, id).CanUnsuspend, "does not unsuspend after grant");
}

async Task CanNotBeBlocked()
{
    EngineContext context = Context();
    var id = await Place(context, P1, "SELF");
    // (P7 test-fix) a candidate BLOCKER must exist to evaluate the joint predicate against (AS-IS
    // Permanent.CanBlock(AttackingPermanent) is always invoked on a REAL candidate blocker permanent — there
    // is no "unblockable by anyone in the abstract" query without one).
    var blocker = await Place(context, P2, "BLOCKER");
    AssertTrue(!ContinuousRestrictionGate.EvaluateBeBlocked(context, id, blocker).IsRestricted, "not restricted before grant");
    // (P7 stage-B SEAM) see CantUnsuspend above.
    var cs = new CardSource(context, id, P1);
    ICardEffect built = CardEffectFactory.CanNotBeBlockedStaticSelfEffect(
        defenderCondition: null, isInheritedEffect: false, card: cs, condition: null, effectName: $"cbb:{id.Value}");
    cs.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(built);
    AssertTrue(ContinuousRestrictionGate.EvaluateBeBlocked(context, id, blocker).IsRestricted, "unblockable after grant");
}

async Task DeleteBySkillPrevented()
{
    EngineContext context = Context();
    var id = await Place(context, P1, "SELF");
    // (P7 RD-P6B-12 resolved SEAM) CanNotBeDestroyedBySkillClass is a new-model kind-class with no
    // ToBinding/EffectRegistry bridge — the AS-IS-faithful path is the LIVE cEntity_EffectController scan
    // NewModelContinuousScan.HasCanNotBeDestroyedBySkill / MatchStateMutationSink.IsRestrictedFromCause now
    // performs. Attach the built effect to the card's controller via the same seam every ported card
    // definition class uses.
    var cs = new CardSource(context, id, P1);
    ICardEffect built = CardEffectFactory.CanNotBeDestroyedBySkillStaticEffect(
        permanentCondition: null, cardEffectCondition: null, isInheritedEffect: false, card: cs, condition: null, effectName: $"cds:{id.Value}");
    cs.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(built);
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
    // (P7 test-fix) pass context: so IsDeletionPreventedByContinuous/IsRestrictedFromCause can consult the
    // new-model interface scan (RD-P6B-10/12) — without it, _context is null and only the registry-only
    // fallback (unconditional restrictions from bindings) runs.
    var sink = new MatchStateMutationSink(
        context.CardInstanceRepository, context.LogSink, context.ZoneMover, context.MemoryController, context.EffectRegistry, context.GameEventQueue, context: context);
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
    context.TurnController.SetPhase(HeadlessPhase.Main);
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

sealed class TestCardEntityEffect : CEntity_Effect
{
    private readonly ICardEffect _effect;
    public TestCardEntityEffect(ICardEffect effect) { _effect = effect; }
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource cardSource) => new() { _effect };
}
