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
///
/// (R1-d) 잔존 사유: the AS-IS Permanent restriction predicates (CanUnsuspend/CanSuspend/CanBeDestroyed*)
/// now live on the mirror <c>Permanent</c> getters and their consumers were rewired to them. What REMAINS
/// here is NOT R1-d's:
///  * EvaluateDigivolve — AS-IS <c>CardSource.CanNotEvolve</c>: (R1-e) CanNotEvolve is now an AS-IS-literal
///    ICanNotDigivolveEffect scan on the mirror CardSource and no longer delegates here. 잔존=R2 몫: the surviving
///    EvaluateDigivolve call sites are the DigivolveAction / fusion pipeline legality checks (플레이 파이프라인
///    내부 판정) + ActivatedEffects / CardEffectCommons rule-process filters + BT1_078 (card) — they fold away as
///    those consumers are rehoused in R2.
///  * EvaluateAttack / EvaluateBeAttacked / EvaluateBlock / EvaluateBeBlocked — AS-IS Permanent.CanAttack(
///    TargetDigimon)/CanBlock, but consumed by the attack/block FLOW validators (AttackPermanentAction /
///    BlockTiming) at fine-grained points; folding them back into the monolithic Permanent.CanAttack/CanBlock
///    is the attack/block flow restructure = R2/R3 몫 (leave the scan wiring intact until then).
///  * EvaluateDeleteBySkill — AS-IS Permanent.CanBeDestroyedBySkill, but consumed by the deletion machine
///    (MatchStateMutationSink) = R2 몫.
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
        HeadlessEntityId? defenderId = null) =>
        // (joint-migration) AS-IS Permanent.CanAttack: SCAN every field effect and evaluate the joint
        // CanNotAttack(attacker, defender). A defender-conditional effect's predicate returns false for a
        // non-matching defender, so it does not restrict that pairing (subsumes the FR-P3 counterpart-softening logic).
        JointResult(context, RestrictionHelpers.CannotAttackKey, attackerId, defenderId, "cannotAttack", "attack");

    /// <summary>(joint-migration) Canonical evaluator: SCAN all field effects for a joint restriction of
    /// <paramref name="kind"/> forbidding (<paramref name="subjectId"/>, <paramref name="counterpartId"/>).
    /// Mirrors AS-IS <c>Permanent.CanX</c> (a single joint predicate over every field effect).</summary>
    private static CannotRestrictionResult JointResult(
        EngineContext context, string kind, HeadlessEntityId subjectId, HeadlessEntityId? counterpartId,
        string appliedTag, string noun) =>
        (RestrictionScan.IsRestricted(context, kind, subjectId, counterpartId ?? default)
            // (P6 STAGE B) UNION the new-model interface scan (AS-IS Permanent.CanSuspend/CanUnsuspend/
            // CanBlock/CanAttackTargetDigimon, CardSource.CanNotEvolve) alongside the legacy registry scan.
            // Ported kind-classes (CanNotUnsuspendClass, CannotBlockClass, …) implement the marker interfaces
            // and register no binding, so the interface scan is their only path; the two are interface-disjoint
            // (legacy joint-restriction effects are `: ICardEffect` only) — no double count.
            || Assets.Scripts.Script.CardEffectCommons.NewModelContinuousScan.IsRestrictedNewModel(context, kind, subjectId, counterpartId))
            ? CannotRestrictionResult.Success(true, $"Cannot {noun}.", new[] { appliedTag }, Array.Empty<string>(), new Dictionary<string, object?>())
            : CannotRestrictionResult.Success(false, $"No {noun} restriction.", Array.Empty<string>(), Array.Empty<string>(), new Dictionary<string, object?>());

    public static CannotRestrictionResult EvaluateBlock(
        EngineContext context,
        HeadlessEntityId blockerId,
        HeadlessEntityId? attackerId = null) =>
        JointResult(context, RestrictionHelpers.CannotBlockKey, blockerId, attackerId, "cannotBlock", "block");

    // (D-A5) Continuous "cannot digivolve" restriction targeting the under-card being evolved.
    public static CannotRestrictionResult EvaluateDigivolve(
        EngineContext context,
        HeadlessEntityId targetCardId,
        HeadlessEntityId? sourceEntityId = null) =>
        JointResult(context, RestrictionHelpers.CannotDigivolveKey, targetCardId, sourceEntityId, "cannotDigivolve", "digivolve");

    // (R1-d) EvaluateUnsuspend / EvaluateSuspend REMOVED — AS-IS Permanent.CanUnsuspend / Permanent.CanSuspend
    // are now housed on the mirror getters and their consumers read `permanent.CanUnsuspend` / `permanent.CanSuspend`
    // directly. (R2-D) the last laggard, MatchStateMutationSink's SuspendKind case, was rewired to the getter too,
    // so NewModelContinuousScan.CanNotSuspend was DELETED (no remaining consumer).

    // (PRIM-W3) Continuous "cannot be blocked" restriction on the attacker — consulted when enumerating blockers.
    // (W6-G) blocker-conditional form supported (AS-IS GainCanNotBeBlocked defenderCondition, embedded in the joint predicate).
    public static CannotRestrictionResult EvaluateBeBlocked(EngineContext context, HeadlessEntityId attackerId, HeadlessEntityId? blockerId = null) =>
        JointResult(context, RestrictionHelpers.CannotBeBlockedKey, attackerId, blockerId, "cannotBeBlocked", "be blocked");

    // (PRIM-W3) Continuous "cannot be deleted by effect/skill" restriction — consulted by the effect-delete path.
    public static CannotRestrictionResult EvaluateDeleteBySkill(EngineContext context, HeadlessEntityId targetId) =>
        JointResult(context, RestrictionHelpers.CannotBeDeletedBySkillKey, targetId, null, "cannotBeDeletedBySkill", "be deleted by skill");

    // (PRIM-W4) Continuous "cannot be attacked" restriction on the defender — consulted by AttackPermanentAction.
    // (W6-G) attacker-conditional form supported (AS-IS GainCanNotBeAttacked attackerCondition, embedded in the joint predicate).
    public static CannotRestrictionResult EvaluateBeAttacked(EngineContext context, HeadlessEntityId defenderId, HeadlessEntityId? attackerId = null) =>
        JointResult(context, RestrictionHelpers.CannotBeAttackedKey, defenderId, attackerId, "cannotBeAttacked", "be attacked");
}
