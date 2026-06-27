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
