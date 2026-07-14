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
    ("ChangeSelfLinkMax +1 -> linkedMaxDelta carried in continuous values", () =>
        FlagCarried(id => CardEffectFactory.ChangeSelfLinkMaxStaticEffect(1, false, id, null), ModifierHelpers.LinkedMaxDeltaKey)),
    ("GrantedReduceLinkCost 2 -> linkCostDelta carried in continuous values", () =>
        FlagCarried(id => CardEffectFactory.GrantedReduceLinkCostClass(id, 2, cardSourceCondition: _ => true, permanentCondition: _ => true, rootCondition: _ => true), ModifierHelpers.LinkCostDeltaKey)),
    ("UseRequirements -> ignoreColorRequirement flag read by the digivolve gate", () =>
        FlagCarried(id => CardEffectFactory.UseRequirements(id, cardCondition: _ => true), DigivolveAction.IgnoreColorRequirementKey)),
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

async Task FlagCarried(Func<CardSource, ICardEffect> build, string expectedKey)
{
    EngineContext context = Context();
    var id = await Place(context, P1, "SELF");
    ICardEffect builtEffect = build(new CardSource(context, id, P1));
    // STOP (post-stage-B re-diagnosis, per task brief — heavy-substrate, outside touch scope):
    // ChangeLinkMaxClass/ChangeLinkCostClass/IgnoreColorConditionClass are real new-model kind-classes
    // (IChangeLinkMaxEffect.GetLinkMax / IChangeLinkCostEffect.GetCost / IIgnoreColorConditionEffect.
    // IgnoreColorCondition — no ToBinding). Their REAL consumers are LinkHelpers.ResolveLinkedMax/
    // ResolveLinkCost (Headless/Runtime/LinkHelpers.cs, folds ContinuousScopeEvaluation's LEGACY modifiers
    // only — see G9-056.M4LinkUnseal, same root cause) and DigivolveAction's ignore-color-requirement check
    // (Headless/Runtime/DigivolveAction.cs, same class as G9-024/G9-044's added-requirement gap). Neither
    // LinkHelpers.cs nor DigivolveAction.cs is a Headless/Runtime/*Gate.cs file (nor does
    // ContinuousScopeEvaluation.cs, the shared modifier source, match that glob) — this pass's mandated touch
    // scope is NewModelContinuousScan.cs + *Gate.cs + MatchStateMutationSink.cs only, with no Gate-level
    // indirection to union a new-model fold through to reach these consumers. The raw-registry check below
    // (unchanged from the pre-diagnosis version) was ALREADY testing the wrong layer regardless — even a
    // Gate-level fix would not change what THIS assertion observes. Not forced; left failing.
    if (LegacyBindingBridge.TryToBinding(builtEffect, $"eff:{expectedKey}:{id.Value}", out var builtBinding) && builtBinding is not null)
        context.EffectRegistry.Register(builtBinding);

    bool carried = context.EffectRegistry
        .GetContinuousEffects(new EffectQueryContext(ContinuousRestrictionGate.Scope, targetEntityId: id))
        .Any(effect => effect.Context.Values.ContainsKey(expectedKey));
    AssertTrue(carried, $"a continuous binding on the card carries '{expectedKey}'");
}

// --- Helpers -------------------------------------------------------------

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 937);
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
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = false }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
