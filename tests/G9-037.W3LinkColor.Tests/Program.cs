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
    // MIGRATION-NOTE (P7 test-fix): the new-model kind-classes constructed via FlagCarried
    // (ChangeLinkMaxClass / ChangeLinkCostClass / IgnoreColorConditionClass) have no ToBinding/EffectRegistry
    // bridge (stage-B RED, docs/audit/rebuild_p6_stageA_notes.md). The continuous-effect query below reads only
    // the substrate EffectRegistry, not the AS-IS live scan, so there is no buildable way to make this grant
    // observable yet. The factory call above is kept (exercises construction); registration is skipped when no
    // bridge exists. Assertion below is UNCHANGED and EXPECTED TO FAIL until stage B lands — tracked, not
    // silently weakened.
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
