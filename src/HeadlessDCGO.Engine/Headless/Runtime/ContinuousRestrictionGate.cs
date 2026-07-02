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

        // F-5: fold in player-scope continuous restrictions (e.g. "your opponent's Digimon cannot
        // block") alongside the card-targeted ones.
        ContinuousEvaluationResult result = ContinuousScopeEvaluation.EvaluateForCard(context, Scope, entityId);

        return result.Restrictions;
    }

    public static CannotRestrictionResult EvaluateAttack(
        EngineContext context,
        HeadlessEntityId attackerId,
        HeadlessEntityId? defenderId = null)
    {
        CannotRestrictionResult result = RestrictionHelpers.CannotAttack(attackerId, Evaluate(context, attackerId), defenderId);
        if (!result.IsRestricted || defenderId is not { } defender)
        {
            return result;
        }

        // (FR-P3) A CannotAttack restriction may be defender-conditional (AS-IS defenderCondition): it only
        // forbids attacking defenders matching its predicate. If EVERY applicable CannotAttack effect carries
        // a defenderPredicate that this defender fails, the attacker may attack it after all.
        bool appliesToThisDefender = false;
        bool anyDefenderConditional = false;
        HeadlessPlayerId defenderOwner = context.CardInstanceRepository.TryGetInstance(defender, out CardInstanceRecord? di) && di is not null ? di.OwnerId : default;
        foreach (EffectRequest effect in ContinuousScopeEvaluation.ApplicableEffects(context, Scope, attackerId))
        {
            IReadOnlyDictionary<string, object?> values = effect.Context.Values;
            if (!(values.TryGetValue(RestrictionHelpers.CannotAttackKey, out object? on) && on is bool b && b))
            {
                continue;
            }

            if (values.TryGetValue(RestrictionHelpers.DefenderPredicateKey, out object? raw)
                && raw is Func<Assets.Scripts.Script.CardEffectCommons.CardSource, bool> pred)
            {
                anyDefenderConditional = true;
                if (pred(new Assets.Scripts.Script.CardEffectCommons.CardSource(context, defender, defenderOwner, defenderOwner)))
                {
                    appliesToThisDefender = true;
                    break;
                }
            }
            else
            {
                appliesToThisDefender = true; // unconditional CannotAttack
                break;
            }
        }

        return (anyDefenderConditional && !appliesToThisDefender)
            ? CannotRestrictionResult.Success(false, "Defender not in the restricted set.", Array.Empty<string>(), Array.Empty<string>(), new Dictionary<string, object?>())
            : result;
    }

    public static CannotRestrictionResult EvaluateBlock(
        EngineContext context,
        HeadlessEntityId blockerId,
        HeadlessEntityId? attackerId = null)
    {
        CannotRestrictionResult result = RestrictionHelpers.CannotBlock(blockerId, Evaluate(context, blockerId), attackerId);
        return SoftenByCounterpart(context, result, RestrictionHelpers.CannotBlockKey, blockerId, attackerId);
    }

    /// <summary>(W6-G) FR-P3 generalised: a counterpart-conditional restriction (AS-IS attackerCondition /
    /// defenderCondition on the Gain grants) only applies when the counterpart matches its predicate — if
    /// EVERY applicable effect of <paramref name="restrictionKey"/> carries a counterpart predicate that
    /// <paramref name="counterpartId"/> fails, the restriction does not apply to this pairing.</summary>
    private static CannotRestrictionResult SoftenByCounterpart(
        EngineContext context, CannotRestrictionResult result, string restrictionKey,
        HeadlessEntityId subjectId, HeadlessEntityId? counterpartId)
    {
        if (!result.IsRestricted || counterpartId is not { } counterpart || counterpart.IsEmpty)
        {
            return result;
        }

        bool appliesToCounterpart = false;
        bool anyConditional = false;
        HeadlessPlayerId counterpartOwner = context.CardInstanceRepository.TryGetInstance(counterpart, out CardInstanceRecord? ci) && ci is not null
            ? ci.OwnerId
            : default;
        foreach (EffectRequest effect in ContinuousScopeEvaluation.ApplicableEffects(context, Scope, subjectId))
        {
            IReadOnlyDictionary<string, object?> values = effect.Context.Values;
            if (!(values.TryGetValue(restrictionKey, out object? on) && on is bool b && b))
            {
                continue;
            }

            if (values.TryGetValue(RestrictionHelpers.CounterpartPredicateKey, out object? raw)
                && raw is Func<Assets.Scripts.Script.CardEffectCommons.CardSource, bool> pred)
            {
                anyConditional = true;
                if (pred(new Assets.Scripts.Script.CardEffectCommons.CardSource(context, counterpart, counterpartOwner, counterpartOwner)))
                {
                    appliesToCounterpart = true;
                    break;
                }
            }
            else
            {
                appliesToCounterpart = true;   // unconditional restriction
                break;
            }
        }

        return (anyConditional && !appliesToCounterpart)
            ? CannotRestrictionResult.Success(false, "Counterpart not in the restricted set.", Array.Empty<string>(), Array.Empty<string>(), new Dictionary<string, object?>())
            : result;
    }

    // (D-A5) Continuous "cannot digivolve" restriction targeting the under-card being evolved.
    public static CannotRestrictionResult EvaluateDigivolve(
        EngineContext context,
        HeadlessEntityId targetCardId,
        HeadlessEntityId? sourceEntityId = null)
    {
        return RestrictionHelpers.CannotDigivolve(targetCardId, Evaluate(context, targetCardId), sourceEntityId);
    }

    // (PRIM-W3) Continuous "does not unsuspend" restriction — consulted by the Unsuspend step.
    public static CannotRestrictionResult EvaluateUnsuspend(EngineContext context, HeadlessEntityId targetId) =>
        RestrictionHelpers.CannotUnsuspend(targetId, Evaluate(context, targetId));

    // (W6-P) Continuous "cannot suspend" restriction — the AS-IS Permanent.CanSuspend gate half of
    // CanActivatePermanentSuspendCostEffect.
    public static CannotRestrictionResult EvaluateSuspend(EngineContext context, HeadlessEntityId targetId) =>
        RestrictionHelpers.CannotSuspend(targetId, Evaluate(context, targetId));

    // (PRIM-W3) Continuous "cannot be blocked" restriction on the attacker — consulted when enumerating blockers.
    // (W6-G) blocker-conditional form supported (AS-IS GainCanNotBeBlocked defenderCondition).
    public static CannotRestrictionResult EvaluateBeBlocked(EngineContext context, HeadlessEntityId attackerId, HeadlessEntityId? blockerId = null) =>
        SoftenByCounterpart(context, RestrictionHelpers.CannotBeBlocked(attackerId, Evaluate(context, attackerId)), RestrictionHelpers.CannotBeBlockedKey, attackerId, blockerId);

    // (PRIM-W3) Continuous "cannot be deleted by effect/skill" restriction — consulted by the effect-delete path.
    public static CannotRestrictionResult EvaluateDeleteBySkill(EngineContext context, HeadlessEntityId targetId) =>
        RestrictionHelpers.CannotBeDeletedBySkill(targetId, Evaluate(context, targetId));

    // (PRIM-W4) Continuous "cannot be attacked" restriction on the defender — consulted by AttackPermanentAction.
    // (W6-G) attacker-conditional form supported (AS-IS GainCanNotBeAttacked attackerCondition).
    public static CannotRestrictionResult EvaluateBeAttacked(EngineContext context, HeadlessEntityId defenderId, HeadlessEntityId? attackerId = null) =>
        SoftenByCounterpart(context, RestrictionHelpers.CannotBeAttacked(defenderId, Evaluate(context, defenderId)), RestrictionHelpers.CannotBeAttackedKey, defenderId, attackerId);
}
