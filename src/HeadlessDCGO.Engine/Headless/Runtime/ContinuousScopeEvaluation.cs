namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

/// <summary>
/// (F-5) Shared continuous evaluation for a single card that folds in BOTH card-targeted continuous
/// effects (matched by <c>targetEntityId</c>) AND player-scope continuous effects (matched by the
/// card's owner + condition, via <see cref="PlayerScopeContinuousHelpers"/>). The continuous gates
/// (DP / restriction) call this so a "your Digimon get +1000 DP / cannot block" effect reaches every
/// applicable permanent without being individually targeted.
/// </summary>
public static class ContinuousScopeEvaluation
{
    /// <summary>Marks a continuous binding as an inherited (digivolution-source) effect: it applies to the
    /// TOP card of the stack the source is buried in, never to the source as a stand-alone permanent.
    /// (W3c-final Stage 1) Rehoused here from ContinuousSelfModifierEffect so the continuous scope-evaluation
    /// keys survive that type's deletion; string values are byte-identical.</summary>
    public const string InheritedEffectKey = "continuous.isInherited";

    /// <summary>Carries the card-authored <c>condition</c> predicate (a <c>Func&lt;bool&gt;</c>) evaluated
    /// at read time by <see cref="ContinuousScopeEvaluation"/>.</summary>
    public const string ConditionKey = "continuous.condition";

    /// <summary>Carries a card-authored dynamic delta (<c>Func&lt;int&gt;</c>, e.g. "+X where X = sources / 2")
    /// evaluated at read time; the resolved int is written under <see cref="DynamicMetricKey"/>'s metric.</summary>
    public const string DynamicValueKey = "continuous.dynamicValue";

    /// <summary>The metric delta key a resolved <see cref="DynamicValueKey"/> should be written under.</summary>
    public const string DynamicMetricKey = "continuous.dynamicMetric";

    // (C5-1) EvaluateForCard + ResolveCard were DELETED with the empty-union evaluator: EvaluateForCard fed the
    // ApplicableEffects producer-0 set (permanently empty) into the now-deleted ContinuousEffectEvaluator, whose
    // NumericModifier / restriction / replacement output was always empty. All four live callers (LinkHelpers /
    // MatchStateMutationSink / BattleDeletionGate / ContinuousRestrictionGate.Evaluate) were re-based onto the
    // AS-IS live scans (NewModelContinuousScan / Permanent.CanSuspend / Permanent.CanBeDestroyed). What survives
    // here is the still-empty ApplicableEffects query stub (consumed by DigivolveAction's added-requirement /
    // ignore-flag loops, harmless empty unions with their new-model scans) and the continuous-scope const keys
    // (InheritedEffectKey / ConditionKey) still read by ContinuousFieldMembership / CardSource / DigivolveAction.

    /// <summary>(FR-P3) The continuous effects that APPLY to <paramref name="cardId"/> under <paramref name="scope"/>
    /// — card-targeted + inherited + player-scope (owner + condition + arbitrary permanentCondition predicate,
    /// evaluated 1:1) — after disable/condition filtering and dynamic-value resolution. Registry-only gates
    /// (sink / battle-deletion) scan this to honour player-scope effects with predicates, not just self.</summary>
    public static EffectRequest[] ApplicableEffects(
        EngineContext context, string scope, HeadlessEntityId cardId, HeadlessEntityId digivolveTargetPermanentId = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);

        // (④) The EffectRegistry continuous producer reached 0 (card-targeted / inherited / player-scope arms all
        // read a permanently-empty store) and the face-up-security scan (CardEffectRegistrar.BuildContinuousRequests)
        // lowered only OLD-model ToBinding carriers — all of which are now deleted (real cards are new-model
        // kind-classes with no ToBinding). So this old-model continuous collection is production-inert: every live
        // continuous DP / restriction / immunity answer is served by the AS-IS live scans (Permanent.DP fold,
        // NewModelContinuousScan). Reduced to empty (behavior-neutral, digest-verified).
        _ = cardId;
        _ = digivolveTargetPermanentId;
        return Array.Empty<EffectRequest>();
    }
}
