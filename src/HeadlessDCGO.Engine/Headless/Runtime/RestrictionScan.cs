namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (joint-migration) Canonical restriction consumer. Mirrors the AS-IS <c>Permanent.CanX</c> shape 1:1: SCAN every
/// field permanent's continuous effects and, for each usable <see cref="JointRestrictionEffect"/> of the given
/// <c>kind</c>, evaluate its JOINT predicate <c>f(subject, counterpart)</c>. Any match ⇒ restricted.
///
/// The 2nd argument is polymorphic per restriction kind (see the migration design doc): defender for CannotAttack,
/// attacker for CannotBlock/CannotBeAttacked, blocker for CannotBeBlocked, the causing effect's source for
/// delete/return/suspend, null when there is no counterpart.
///
/// (R1-d) 잔존 사유: the suspend/unsuspend and the effect-bearing select-by-skill restrictions were rehoused onto
/// the mirror <c>Permanent</c> getters and their consumers rewired off this scan. What still reads this scan is
/// NOT R1-d's: ImmuneStackTrashingKey / CannotBeRemovedKey (the deletion / stack-mutation machine = R2),
/// CannotDigivolveKey (AS-IS CardSource.CanNotEvolve = R1-e), the CannotAttack/Block family (attack/block flow =
/// R2/R3), and the one effect-less legacy select path (SelectPermanentEffect.IsUntargetableBySkill, no ICardEffect
/// object to feed Permanent.CanSelectBySkill).
/// (이연④-f) CannotMoveKey is now DEAD (formerly a live reader): its sole reader (BT1_089's breeding→battle move gate)
/// was re-pointed to the AS-IS interface-scan half (Permanent.CanMove — the ICanNotMoveEffect scan AS-IS BT1_089:56
/// uses), and its sole producer CanNotMoveEffect no longer writes the CannotMoveKey binding (ToBinding retired).
/// </summary>
public static class RestrictionScan
{
    /// <summary>True when any active <see cref="JointRestrictionEffect"/> of <paramref name="kind"/> forbids the
    /// pairing (<paramref name="subject"/>, <paramref name="counterpart"/>).</summary>
    public static bool IsRestricted(EngineContext context, string kind, CardSource subject, CardSource? counterpart)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        if (subject is null)
        {
            return false;
        }

        string predicateKey = JointRestrictionEffect.PredicateKey(kind);
        var query = new EffectQueryContext(ContinuousRestrictionGate.Scope);

        // AS-IS Permanent.CanX SCANs every field effect once. Headless splits field effects across the Continuous
        // and Restriction query roles (a card can carry either), so the canonical scan unions both — deduped by
        // effect id so a binding tagged with both roles is evaluated once.
        var seen = new HashSet<HeadlessEntityId>();
        foreach (EffectRequest effect in Enumerable.Concat(
            context.EffectRegistry.GetContinuousEffects(query),
            context.EffectRegistry.GetRestrictionEffects(query)))
        {
            if (!seen.Add(effect.EffectId))
            {
                continue;
            }

            IReadOnlyDictionary<string, object?> values = effect.Context.Values;
            if (!values.TryGetValue(predicateKey, out object? rawPred) || rawPred is not Func<CardSource, CardSource?, bool> predicate)
            {
                continue;
            }

            // AS-IS cardEffect.CanUse(null): the effect's own condition gate.
            if (values.TryGetValue(ContinuousSelfModifierEffect.ConditionKey, out object? condRaw)
                && condRaw is Func<bool> condition && !condition())
            {
                continue;
            }

            if (predicate(subject, counterpart))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Query by entity ids — builds the subject/counterpart CardSources lazily with an owner guard (an
    /// unknown/empty owner ⇒ not restricted, avoiding the controller-empty CardSource ctor throw).</summary>
    public static bool IsRestricted(EngineContext context, string kind, HeadlessEntityId subjectId, HeadlessEntityId counterpartId)
    {
        ArgumentNullException.ThrowIfNull(context);
        CardSource? subject = MakeSource(context, subjectId);
        if (subject is null)
        {
            return false;
        }

        return IsRestricted(context, kind, subject, MakeSource(context, counterpartId));
    }

    /// <summary>Build a CardSource for a live instance, or null when the instance/owner is unknown.</summary>
    public static CardSource? MakeSource(EngineContext context, HeadlessEntityId id)
    {
        if (context is null || id.IsEmpty
            || !context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) || rec is null || rec.OwnerId.IsEmpty)
        {
            return null;
        }

        return new CardSource(context, id, rec.OwnerId, rec.OwnerId);
    }
}
