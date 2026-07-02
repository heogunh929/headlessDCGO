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
    public const string ImmunityFromOpponentOnlyKey = "immunityFromOpponentOnly";

    // (S2) AS-IS CanNotAffectedClass.SkillCondition — an arbitrary predicate over the CAUSING effect (headless:
    // its source card) deciding WHICH effects this card is immune to (e.g. "opponent's Digimon effects only").
    // Value: Func<CardSource,bool> over the causing effect's source. Evaluated when an EngineContext is available.
    public const string SkillPredicateKey = "immunitySkillPredicate";

    // (C2) AS-IS CanNotAffectedClass.CardCondition (the factory's permanentCondition) — WHICH permanents the
    // immunity protects (AS-IS: CanNotAffect = CardCondition(target) && SkillCondition(cause)). A grant
    // carrying this key is registered field-wide (no target) and evaluated live against the PROTECTED card.
    public const string TargetPredicateKey = "immunity.targetPredicate";

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

        foreach (EffectRequest request in registry.GetContinuousEffects(new EffectQueryContext(Scope, targetEntityId: targetId)))
        {
            IReadOnlyDictionary<string, object?> values = request.Context.Values;

            // (S2) AS-IS SkillCondition: this card is immune to the causing effect iff the predicate matches it
            // (evaluated over the causing effect's source card). The predicate itself encodes the "opponent /
            // Digimon-effect / ..." condition (1:1 with the original), so NO blanket opponent hardcoding here.
            if (values.TryGetValue(SkillPredicateKey, out object? skillRaw)
                && skillRaw is Func<Assets.Scripts.Script.CardEffectCommons.CardSource, bool> skill)
            {
                if (context is not null
                    && skill(new Assets.Scripts.Script.CardEffectCommons.CardSource(context, sourceEntityId, source.OwnerId, source.OwnerId)))
                {
                    return true;
                }

                continue;
            }

            // Opponent-only immunity (ProgressImmunity): blocks only an effect sourced by the opponent.
            if (values.TryGetValue(ImmunityFromOpponentOnlyKey, out object? raw) && raw is bool flag && flag
                && source.OwnerId != target.OwnerId)
            {
                return true;
            }
        }

        // (C2) predicate-scoped immunity grants (registered field-wide, no target): AS-IS CanNotAffect =
        // CardCondition(target) && SkillCondition(cause) — BOTH must be present and pass (CanNotAffectedClass
        // returns true only when both conditions are non-null and match). Needs the EngineContext to build
        // the CardSource views; context-less callers skip (no such grant exists without a ported card).
        if (context is not null)
        {
            foreach (EffectRequest request in registry.GetContinuousEffects(new EffectQueryContext(Scope)))
            {
                IReadOnlyDictionary<string, object?> values = request.Context.Values;
                if (!values.TryGetValue(TargetPredicateKey, out object? targetRaw)
                    || targetRaw is not Func<Assets.Scripts.Script.CardEffectCommons.CardSource, bool> targetPredicate
                    || !values.TryGetValue(SkillPredicateKey, out object? skillRaw)
                    || skillRaw is not Func<Assets.Scripts.Script.CardEffectCommons.CardSource, bool> skillPredicate)
                {
                    continue;
                }

                if (targetPredicate(new Assets.Scripts.Script.CardEffectCommons.CardSource(context, targetId, target.OwnerId, target.OwnerId))
                    && skillPredicate(new Assets.Scripts.Script.CardEffectCommons.CardSource(context, sourceEntityId, source.OwnerId, source.OwnerId)))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
