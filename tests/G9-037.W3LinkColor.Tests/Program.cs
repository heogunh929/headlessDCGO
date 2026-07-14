using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// PRIM-W3 (G9-037): the final W3 primitives.
//  - MindLink: keyword grant (HasKeyword live; tamer-as-Digimon consumer latent).
//  - ChangeSelfLinkMax / GrantedReduceLinkCost: continuous link modifiers registered under the modifier
//    keys (queryable; link subsystem consumer latent).
//  - UseRequirements: continuous ignore-color flag read by DigivolveAction (behavior-live).
// Registration + carried intent are asserted via the continuous-effect query the consumers read.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("MindLink -> HasKeyword(MindLink) live after grant", MindLink),
    ("ChangeSelfLinkMax +1 -> effective link max raised (ResolveLinkedMax)", LinkMax),
    ("GrantedReduceLinkCost 2 -> effective link cost lowered (ResolveLinkCost)", LinkCost),
    ("UseRequirements -> ignore-color active (read by the digivolve color gate)", UseReq),
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

async Task MindLink()
{
    EngineContext context = Context();
    var id = await Place(context, P1, "TAMER");
    AssertTrue(!ContinuousKeywordGate.HasKeyword(context, id, ContinuousKeywordGate.MindLink), "absent before grant");
    var mindLinkEffect = CardEffectFactory.MindLinkSelfEffect(false, new CardSource(context, id, P1), null);
    if (!LegacyBindingBridge.TryToBinding(mindLinkEffect, $"ml:{id.Value}", out var mindLinkBinding) || mindLinkBinding is null)
        throw new InvalidOperationException($"{mindLinkEffect.GetType().Name} has no ToBinding bridge.");
    context.EffectRegistry.Register(mindLinkBinding);
    AssertTrue(ContinuousKeywordGate.HasKeyword(context, id, ContinuousKeywordGate.MindLink), "MindLink live after grant");
}

// SEAM (RD-P6B-16/17): ChangeLinkMaxClass / ChangeLinkCostClass / IgnoreColorConditionClass are new-model
// kind-classes (IChangeLinkMaxEffect.GetLinkMax / IChangeLinkCostEffect.GetCost / IIgnoreColorConditionEffect.
// IgnoreColorCondition — no ToBinding). Their REAL consumers now UNION the AS-IS live interface scan:
// LinkHelpers.ResolveLinkedMax (AS-IS Permanent.LinkedMax) / ResolveLinkCost (AS-IS CardSource.GetChangedLinkCost)
// and DigivolveAction's color-ignore check (AS-IS CardSource.MatchColorRequirement). Observe through those
// consumers (the earlier raw-registry check was the wrong layer — a new-model grant registers no binding).
// Each grant is attached to the card's controller via the standard CEntity_Effect seam.

async Task LinkMax()
{
    EngineContext context = Context();
    var id = await Place(context, P1, "SELF");
    var card = new CardSource(context, id, P1);
    ICardEffect eff = CardEffectFactory.ChangeSelfLinkMaxStaticEffect(1, false, card, null);
    card.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(eff);
    AssertTrue(LinkHelpers.ResolveLinkedMax(context, id) == LinkHelpers.DefaultLinkedMax + 1, "effective link max raised by +1");
}

async Task LinkCost()
{
    EngineContext context = Context();
    var id = await Place(context, P1, "SELF");
    var card = new CardSource(context, id, P1);
    ICardEffect eff = CardEffectFactory.GrantedReduceLinkCostClass(card, 2, cardSourceCondition: _ => true, permanentCondition: _ => true, rootCondition: _ => true);
    card.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(eff);
    AssertTrue(LinkHelpers.ResolveLinkCost(context, id, 3) == 1, "effective link cost 3 - 2 == 1");
}

async Task UseReq()
{
    EngineContext context = Context();
    var id = await Place(context, P1, "SELF");
    var card = new CardSource(context, id, P1);
    // UseRequirements' CanUse requires the owner control a Digimon/Tamer matching cardCondition — the placed
    // SELF Digimon satisfies `_ => true`, so ignore-color is active. IgnoreColorCondition(this) == (this == card),
    // so it waives THIS card's colour requirement (the digivolve color gate reads exactly this member).
    ICardEffect eff = CardEffectFactory.UseRequirements(card, cardCondition: _ => true);
    card.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(eff);
    using var _scope = AmbientMatchContext.Enter(context);
    AssertTrue(card.IgnoreColorConditionActive(), "ignore-color requirement active for this card");
}

// --- Helpers -------------------------------------------------------------

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 937);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    // (RD-P6B-16/17 seam) live phase so ICardEffect.CanUse's DoneStartGame gate passes for the new-model scans.
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
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = false }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }

// Minimal AS-IS-shaped CEntity_Effect: the seam every ported card definition class uses to surface its printed
// effect list to CardSource.EffectList → CEntity_Effect.CardEffects.
sealed class TestCardEntityEffect : CEntity_Effect
{
    private readonly ICardEffect _effect;
    public TestCardEntityEffect(ICardEffect effect) { _effect = effect; }
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource cardSource) => new() { _effect };
}
