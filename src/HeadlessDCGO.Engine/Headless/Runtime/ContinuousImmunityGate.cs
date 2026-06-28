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

    /// <summary>True when an opponent-sourced effect mutation on <paramref name="targetId"/> is prevented by
    /// an active opponent-only immunity. Works from the registry + repository alone (the sink has no
    /// EngineContext). Returns false when the source is the target's own controller (own/ally effect), when
    /// the source/target cannot be resolved, or when no immunity is registered.</summary>
    public static bool BlocksOpponentEffect(
        IEffectQueryService? registry,
        ICardInstanceRepository repository,
        HeadlessEntityId targetId,
        HeadlessEntityId sourceEntityId)
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

        // Source-relativity: an own/ally effect (source owned by the target's controller) is never blocked.
        if (source.OwnerId == target.OwnerId)
        {
            return false;
        }

        foreach (EffectRequest request in registry.GetContinuousEffects(new EffectQueryContext(Scope, targetEntityId: targetId)))
        {
            if (request.Context.Values.TryGetValue(ImmunityFromOpponentOnlyKey, out object? raw) && raw is bool flag && flag)
            {
                return true;
            }
        }

        return false;
    }
}
