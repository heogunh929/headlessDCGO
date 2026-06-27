namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (X-04) Bridges <see cref="ContinuousEffectEvaluator"/> restriction output into legal-action
/// generation. Mirrors Unity AS-IS <c>ContinuousController</c>'s constant re-evaluation: before a
/// candidate (attack / block) is offered, the registry is queried for continuous effects targeting
/// the entity and the resulting <see cref="CannotRestriction"/> set is checked. This complements the
/// existing static <c>CardInstanceRecord</c>/<c>CardRecord</c> metadata checks — those stay in place;
/// this gate adds restrictions sourced from other cards' continuous effects.
///
/// The gate queries continuous-role registry bindings only (card/instance metadata restrictions are
/// already enforced by the action validators), so it is a pure no-op until continuous effects are
/// registered (Phase 4 card pool).
/// </summary>
public static class ContinuousRestrictionGate
{
    /// <summary>Query scope used for continuous re-evaluation (matches the evaluator unit tests).</summary>
    public const string Scope = "ContinuousRecalculation";

    public static IReadOnlyList<CannotRestriction> Evaluate(EngineContext context, HeadlessEntityId entityId)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (entityId.IsEmpty)
        {
            return Array.Empty<CannotRestriction>();
        }

        ContinuousEvaluationResult result = ContinuousEffectEvaluator.Evaluate(
            context.EffectRegistry,
            new EffectQueryContext(Scope, targetEntityId: entityId));

        return result.Restrictions;
    }

    public static CannotRestrictionResult EvaluateAttack(
        EngineContext context,
        HeadlessEntityId attackerId,
        HeadlessEntityId? defenderId = null)
    {
        return RestrictionHelpers.CannotAttack(attackerId, Evaluate(context, attackerId), defenderId);
    }

    public static CannotRestrictionResult EvaluateBlock(
        EngineContext context,
        HeadlessEntityId blockerId,
        HeadlessEntityId? attackerId = null)
    {
        return RestrictionHelpers.CannotBlock(blockerId, Evaluate(context, blockerId), attackerId);
    }

    // (D-A5) Continuous "cannot digivolve" restriction targeting the under-card being evolved.
    public static CannotRestrictionResult EvaluateDigivolve(
        EngineContext context,
        HeadlessEntityId targetCardId,
        HeadlessEntityId? sourceEntityId = null)
    {
        return RestrictionHelpers.CannotDigivolve(targetCardId, Evaluate(context, targetCardId), sourceEntityId);
    }
}
