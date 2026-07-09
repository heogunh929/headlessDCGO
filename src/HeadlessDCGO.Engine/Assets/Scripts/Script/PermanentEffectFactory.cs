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

    /// <summary>(AD1) 1:1 mirror of AS-IS <c>PermanentEffectFactory.DigimonEffectImmunity(permanent)</c>
    /// (PermanentEffectFactory.cs:51-78): "&lt;permanent&gt; is not affected by the OPPONENT's DIGIMON effects."
    /// Builds a <see cref="CardEffectCommons.ContinuousImmunityEffect"/> whose TargetPredicate protects exactly
    /// this permanent (AS-IS <c>CardCondition = cardSource == permanent.TopCard</c>) and whose SkillCondition
    /// admits only opponent-owned Digimon effects (AS-IS <c>IsOpponentEffect &amp;&amp; IsDigimonEffect</c>, mapped to
    /// the causing effect's SOURCE card). Live existence gate mirrors AS-IS CanUseCondition
    /// (<c>IsExistOnBattleArea</c>). Register with a duration to mirror the AS-IS <c>Until…Effects.Add(…)</c>
    /// bucket. Replaces the earlier flattened binding-rule form that produced BLANKET effect immunity.</summary>
    public static CardEffectCommons.ContinuousImmunityEffect DigimonEffectImmunity(CardEffectCommons.Permanent permanent)
    {
        ArgumentNullException.ThrowIfNull(permanent);
        CardEffectCommons.CardSource topCard = permanent.TopCard;
        HeadlessPlayerId owner = permanent.OwnerId;
        return new CardEffectCommons.ContinuousImmunityEffect(
            card: topCard,
            skillCondition: src => src is not null && src.Owner != owner && src.IsDigimon,
            isInheritedEffect: false,
            condition: () => CardEffectCommons.CardEffectCommons.IsExistOnBattleAreaDigimon(topCard),
            targetPredicate: cs => cs is not null && cs.InstanceId == permanent.InstanceId);
    }

    /// <summary>(AD1) 1:1 mirror of AS-IS <c>PermanentEffectFactory.OptionEffectImmunity(permanent)</c>
    /// (PermanentEffectFactory.cs:80-107): "&lt;permanent&gt; is not affected by the OPPONENT's OPTION effects."
    /// As <see cref="DigimonEffectImmunity"/> but the SkillCondition admits only opponent-owned OPTION effects
    /// (AS-IS <c>IsOpponentEffect &amp;&amp; !IsDigimonEffect &amp;&amp; !IsTamerEffect</c>).</summary>
    public static CardEffectCommons.ContinuousImmunityEffect OptionEffectImmunity(CardEffectCommons.Permanent permanent)
    {
        ArgumentNullException.ThrowIfNull(permanent);
        CardEffectCommons.CardSource topCard = permanent.TopCard;
        HeadlessPlayerId owner = permanent.OwnerId;
        return new CardEffectCommons.ContinuousImmunityEffect(
            card: topCard,
            skillCondition: src => src is not null && src.Owner != owner && !src.IsDigimon && !src.IsTamer,
            isInheritedEffect: false,
            condition: () => CardEffectCommons.CardEffectCommons.IsExistOnBattleAreaDigimon(topCard),
            targetPredicate: cs => cs is not null && cs.InstanceId == permanent.InstanceId);
    }

    /// <summary>(AD1) 1:1 mirror of AS-IS <c>PermanentEffectFactory.CollisionEffect(targetPermanent,
    /// activateClass)</c> (PermanentEffectFactory.cs:131-144): grants &lt;Collision&gt; to exactly this permanent.
    /// Delegates to <c>CollisionStaticEffect</c> with <c>permanentCondition = permanent == targetPermanent</c>
    /// and the live existence gate (AS-IS CanUseCondition = <c>IsPermanentExistsOnBattleArea</c>). Replaces the
    /// flattened binding-rule form that dropped the target predicate. <paramref name="activateClass"/> is
    /// accepted for source-signature fidelity (AS-IS <c>CanNotBeAffected(activateClass)</c> guard is vacuous on
    /// a self grant, no port surface).</summary>
    public static CardEffectCommons.ICardEffect CollisionEffect(
        CardEffectCommons.Permanent targetPermanent, CardEffectCommons.ICardEffect? activateClass = null)
    {
        ArgumentNullException.ThrowIfNull(targetPermanent);
        _ = activateClass;
        CardEffectCommons.CardSource topCard = targetPermanent.TopCard;
        return CardEffectCommons.CardEffectFactory.CollisionStaticEffect(
            permanentCondition: permanent => permanent is not null && permanent.InstanceId == targetPermanent.InstanceId,
            isInheritedEffect: false,
            card: topCard,
            condition: () => CardEffectCommons.CardEffectCommons.IsPermanentExistsOnBattleArea(targetPermanent));
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
