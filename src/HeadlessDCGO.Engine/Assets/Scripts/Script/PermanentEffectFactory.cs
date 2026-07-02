namespace HeadlessDCGO.Engine.Assets.Scripts.Script;

using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

public static class PermanentEffectFactory
{
    public static PermanentEffectFactoryBindingRegistry CreateRegistry(
        IEnumerable<PermanentEffectFactoryBindingRule>? rules = null)
    {
        var registry = new PermanentEffectFactoryBindingRegistry();
        if (rules is null)
        {
            return registry;
        }

        foreach (PermanentEffectFactoryBindingRule rule in rules)
        {
            registry.Register(rule);
        }

        return registry;
    }

    /// <summary>(AD1-S) 1:1 mirror of AS-IS <c>PermanentEffectFactory.CanNotSwitchAttackTargetEffect(targetPermanent,
    /// activateClass)</c> (PermanentEffectFactory.cs:109-127): "This Digimon's attack target can't be switched."
    /// CanUse mirror = target on the battle area AND the controller's turn (<c>IsOwnerTurn</c>) — evaluated
    /// LIVE; predicate = <c>permanent == targetPermanent</c> (locks the effect to this attacker).
    /// <paramref name="activateClass"/> is accepted for source-signature fidelity (the AS-IS
    /// <c>CanNotBeAffected(activateClass)</c> live guard has no port surface on a bare ICardEffect — the
    /// grant is a SELF/own effect in every AS-IS caller, where that guard is vacuous).
    /// Register with <c>ToBinding(id, EffectDuration.UntilEachTurnEnd)</c> to mirror the AS-IS
    /// <c>UntilEachTurnEndEffects.Add(...)</c> bucket.</summary>
    public static CardEffects.CanNotSwitchAttackTargetClass CanNotSwitchAttackTargetEffect(
        CardEffectCommons.Permanent targetPermanent, CardEffectCommons.ICardEffect? activateClass = null)
    {
        ArgumentNullException.ThrowIfNull(targetPermanent);
        _ = activateClass;
        CardEffectCommons.CardSource topCard = targetPermanent.TopCard;
        var effect = new CardEffects.CanNotSwitchAttackTargetClass();
        effect.SetUpICardEffect(
            "This Digimon's attack target can't be switched.",
            () => CardEffectCommons.CardEffectCommons.IsExistOnBattleArea(topCard)
                && CardEffectCommons.CardEffectCommons.IsOwnerTurn(topCard),
            topCard);
        effect.SetUpCanNotSwitchAttackTargetClass(
            permanent => permanent is not null && permanent.InstanceId == targetPermanent.InstanceId);
        return effect;
    }

    public static PermanentEffectFactoryBindingRule DeleteSelfEffect(
        string id,
        IReadOnlyList<string> permanentKeys,
        string trigger = PermanentEffectFactoryBindingRules.DeleteSelfTiming)
    {
        return PermanentEffectFactoryBindingRules.DeleteSelf(id, permanentKeys, trigger);
    }

    public static PermanentEffectFactoryBindingRule DigimonEffectImmunity(
        string id,
        IReadOnlyList<string> permanentKeys,
        string trigger = PermanentEffectFactoryBindingRules.ImmunityTiming)
    {
        return PermanentEffectFactoryBindingRules.Immunity(id, permanentKeys, "DigimonEffect", trigger);
    }

    public static PermanentEffectFactoryBindingRule OptionEffectImmunity(
        string id,
        IReadOnlyList<string> permanentKeys,
        string trigger = PermanentEffectFactoryBindingRules.ImmunityTiming)
    {
        return PermanentEffectFactoryBindingRules.Immunity(id, permanentKeys, "OptionEffect", trigger);
    }

    public static PermanentEffectFactoryBindingRule CollisionEffect(
        string id,
        IReadOnlyList<string> permanentKeys,
        string trigger = PermanentEffectFactoryBindingRules.CollisionTiming)
    {
        return PermanentEffectFactoryBindingRules.Collision(id, permanentKeys, trigger);
    }

    public static PermanentEffectFactoryBindingRule AddDetailClass(
        string id,
        IReadOnlyList<string> permanentKeys,
        string detail,
        bool triggerEffect,
        string trigger = PermanentEffectFactoryBindingRules.DetailTiming)
    {
        return PermanentEffectFactoryBindingRules.Detail(id, permanentKeys, detail, triggerEffect, trigger);
    }

    public static PermanentEffectFactoryBindingResult Bind(
        PermanentEffectFactoryBindingRegistry registry,
        CardInstanceState permanent,
        string trigger,
        HeadlessPlayerId controllerId,
        EffectContext context,
        CardRecord? topCard = null)
    {
        ArgumentNullException.ThrowIfNull(registry);

        return registry.Bind(new PermanentEffectFactoryBindingRequest(
            permanent,
            trigger,
            controllerId,
            context,
            topCard));
    }
}
