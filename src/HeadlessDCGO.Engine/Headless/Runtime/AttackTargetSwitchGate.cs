namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (AD1-S) Mirror of the AS-IS chokepoint <c>Permanent.CanSwitchAttackTarget</c> (Permanent.cs:3745-3792):
/// scans every active <c>ICanNotSwitchAttackTargetEffect</c> and, when one's predicate matches the
/// ATTACKING permanent, the attack target is LOCKED. AS-IS gates exactly two actions with it — both are
/// wired here: block eligibility (Permanent.cs:2156 → <see cref="BlockTiming"/> offers no blocker
/// candidates) and <c>AttackProcess.SwitchDefender</c> (AttackProcess.cs:519, shared by blocker-redirect
/// AND effect retargets → <see cref="RaidAttackSwitch"/> and any future retarget effect must consult this
/// gate before switching).
/// </summary>
public static class AttackTargetSwitchGate
{
    public static bool IsLocked(EngineContext context, HeadlessEntityId attackerId)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (attackerId.IsEmpty ||
            !context.CardInstanceRepository.TryGetInstance(attackerId, out CardInstanceRecord? attacker) ||
            attacker is null)
        {
            return false;
        }

        var attackerView = new Permanent(context, attackerId, attacker.OwnerId);
        foreach (EffectRequest effect in context.EffectRegistry.GetContinuousEffects(
            new EffectQueryContext(ContinuousRestrictionGate.Scope)))
        {
            if (!effect.Context.Values.TryGetValue(CanNotSwitchAttackTargetClass.CannotSwitchAttackTargetKey, out object? raw) ||
                raw is not bool flag || !flag)
            {
                continue;
            }

            if (EffectInvalidation.IsEffectsDisabled(context, effect.Context.SourceEntityId) ||
                !ConditionPasses(effect))
            {
                continue;
            }

            // The AS-IS PermanentCondition over the ATTACKER (CanNotBeSwitchAttackTarget(this)); a binding
            // with no stored predicate locks only an explicitly targeted attacker.
            if (effect.Context.Values.TryGetValue(CanNotSwitchAttackTargetClass.AttackerConditionKey, out object? rawCond) &&
                rawCond is Func<Permanent, bool> attackerCondition)
            {
                if (attackerCondition(attackerView))
                {
                    return true;
                }
            }
            else if (effect.Context.TargetEntityIds.Contains(attackerId))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ConditionPasses(EffectRequest effect) =>
        !effect.Context.Values.TryGetValue(ContinuousSelfModifierEffect.ConditionKey, out object? raw)
        || raw is not Func<bool> condition
        || condition();
}
