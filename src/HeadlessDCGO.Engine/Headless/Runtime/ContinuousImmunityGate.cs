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

    // (R1-e / R3-W3c-1 / R3-W3c-2 / B군 P0-1) CardSource.CanNotBeAffected reads an AS-IS-literal ICanNotAffectedEffect
    // scan and no longer delegates here. Both registry "ContinuousImmunity" producers are gone: ProgressImmunity
    // flipped its attack-time grant to the UntilEndAttack bucket, and CanNotAffectedStaticEffect flipped to the
    // CanNotAffectedClass kind-class (W3c-1). As of B군 P0-1 the LAST production consumers of BlocksOpponentEffect —
    // the mutation sink (general-immunity :527, return-sources :1935, trash-sources :1959), the CardEffectCommons
    // continuous-grant cores (GainRestrictionToPermanent grant-time + liveCondition, GainToPlayerScope scopePredicate)
    // and IsHostStackTrashGated — were rehomed onto the live TopCard.CanNotBeAffected getter. So BlocksOpponentEffect
    // now has ZERO production consumers; its registry read is fully dead.
    //
    // (B군 P0-1 residual) This file is NOT deleted yet only because two TEST suites still reference the gate directly:
    // G3.5-C15.Progress (GateSourceRelativity + RegisterImmunity) and FAILa-03.PermanentEffectImmunity both probe
    // BlocksOpponentEffect / register via JointPredicateKey+Scope against the vestigial registry path. They are
    // false-green dead-path probes; retiring the gate needs them re-aimed to the live CanNotBeAffected scan (precedent:
    // G9-057, G9-040 — already re-aimed). Until that follow-up the class stays as a compile-only stub.
}
