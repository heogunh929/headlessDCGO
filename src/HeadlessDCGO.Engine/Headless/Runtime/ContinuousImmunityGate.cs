namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (S2) Effect-immunity (AS-IS <c>CardSource.CanNotBeAffected</c>). A continuous immunity effect registered
/// on a card makes it unaffected by the opponent's effects. The original checks <c>CanNotBeAffected</c> at
/// ~20 effect-application sites with a per-effect <c>SkillCondition</c> (e.g. Progress = <c>IsOpponentEffect</c>);
/// the headless port centralises the check at the mutation sink: before an effect mutation targeting a card
/// is applied, this gate asks whether an active immunity blocks it. Source-relativity does the filtering — a
/// mutation whose source is owned by the target's controller (own/ally effect) is never blocked; only
/// opponent-sourced effects are. No card registers immunity until one is ported, so this is a no-op by default.
/// Unlocks C-15 Progress.
/// </summary>
public static class ContinuousImmunityGate
{
    public const string Scope = "ContinuousImmunity";

    // (joint-migration) canonical joint predicate — the AS-IS ICanNotAffectedEffect.CanNotAffect(cardSource,
    // cardEffect) shape: a single Func<CardSource /*protected target*/, CardSource /*causing effect source*/, bool>
    // evaluated by SCANNING every field immunity effect (mirrors CardSource.CanNotBeAffected). Producers embed the
    // AS-IS CardCondition ∧ SkillCondition conjunction into it, so non-separable immunities are expressible.
    public const string JointPredicateKey = "joint.immunity";

    /// <summary>True when an opponent-sourced effect mutation on <paramref name="targetId"/> is prevented by
    /// an active opponent-only immunity. Works from the registry + repository alone (the sink has no
    /// EngineContext). Returns false when the source is the target's own controller (own/ally effect), when
    /// the source/target cannot be resolved, or when no immunity is registered.</summary>
    public static bool BlocksOpponentEffect(
        IEffectQueryService? registry,
        ICardInstanceRepository repository,
        HeadlessEntityId targetId,
        HeadlessEntityId sourceEntityId,
        Bridge.EngineContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        if (registry is null || targetId.IsEmpty || sourceEntityId.IsEmpty)
        {
            return false;
        }

        if (!repository.TryGetInstance(targetId, out CardInstanceRecord? target) || target is null ||
            !repository.TryGetInstance(sourceEntityId, out CardInstanceRecord? source) || source is null)
        {
            return false;
        }

        // (joint-migration) AS-IS CardSource.CanNotBeAffected: SCAN every field immunity effect and evaluate the
        // joint CanNotAffect(protected target, causing effect source), gated by the effect's own CanUse condition.
        // Needs the EngineContext to build the CardSource views the joint predicate operates on.
        if (context is not null)
        {
            var protectedSource = new Assets.Scripts.Script.CardEffectCommons.CardSource(context, targetId, target.OwnerId, target.OwnerId);
            var causeSource = new Assets.Scripts.Script.CardEffectCommons.CardSource(context, sourceEntityId, source.OwnerId, source.OwnerId);
            foreach (EffectRequest request in registry.GetContinuousEffects(new EffectQueryContext(Scope)))
            {
                IReadOnlyDictionary<string, object?> values = request.Context.Values;
                if (!values.TryGetValue(JointPredicateKey, out object? jointRaw)
                    || jointRaw is not Func<Assets.Scripts.Script.CardEffectCommons.CardSource, Assets.Scripts.Script.CardEffectCommons.CardSource, bool> joint)
                {
                    continue;
                }

                // AS-IS cardEffect.CanUse(null): the immunity effect's own condition gate (fixes over-immunity when
                // an immunity is conditional, e.g. "while I have 3+ memory").
                if (values.TryGetValue(Assets.Scripts.Script.CardEffectCommons.ContinuousSelfModifierEffect.ConditionKey, out object? condRaw)
                    && condRaw is Func<bool> condition && !condition())
                {
                    continue;
                }

                if (joint(protectedSource, causeSource))
                {
                    return true;
                }
            }

            return false;
        }

        // AS-IS CardSource.CanNotBeAffected always runs with full Permanent/CardSource objects — there is NO
        // context-less path. When the headless sink lacks an EngineContext (registry-only unit mode, where no
        // immunity is registered) we simply cannot build the CardSource views the joint predicate needs, so this
        // mirrors AS-IS by reporting no immunity rather than inventing an owner-comparison heuristic AS-IS lacks.
        return false;
    }

    // (R1-e / R3-W3c-1 / R3-W3c-2) CardSource.CanNotBeAffected reads an AS-IS-literal ICanNotAffectedEffect scan and
    // no longer delegates here. As of R3-W3c-2 the registry "ContinuousImmunity" scope has ZERO production producers:
    // ProgressImmunity flipped its attack-time grant to the UntilEndAttack bucket, and CanNotAffectedStaticEffect
    // flipped to the CanNotAffectedClass kind-class (W3c-1, 0 production callers). So BlocksOpponentEffect's registry
    // read is now ALWAYS false in production — a dead read. The surviving call sites are: MatchStateMutationSink
    // (:527/1926/1944 — W3c-4, needs live-effect threading into EffectMutation, 불가침 this batch) and
    // CardEffectCommons continuous-grant cores (:358/1750/1759/2945/2954/3069 — W3c-3/4, the numeric/joint registry
    // model). The CardController pipelines were rehomed to the live TopCard.CanNotBeAffected scan in W3c-2. This file
    // cannot be deleted until the sink + Commons call sites fold away (W3c-final).
}
