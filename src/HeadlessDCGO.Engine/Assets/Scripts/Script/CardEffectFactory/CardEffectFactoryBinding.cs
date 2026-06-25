namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory;

using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public static class CardEffectFactoryBinding
{
    public static CardEffectFactoryBindingRegistry CreateRegistry(
        IEnumerable<CardEffectFactoryBindingRule>? rules = null)
    {
        var registry = new CardEffectFactoryBindingRegistry();
        if (rules is null)
        {
            return registry;
        }

        foreach (CardEffectFactoryBindingRule rule in rules)
        {
            registry.Register(rule);
        }

        return registry;
    }

    public static CardEffectFactoryBindingRule BindKeywordBaseBatch1(
        string id,
        IReadOnlyList<string> cardKeys,
        string trigger,
        KeywordBaseBatch1Kind kind,
        bool isInherited = false,
        bool isLinked = false)
    {
        return CardEffectFactoryBindingRules.KeywordBaseBatch1(
            id,
            cardKeys,
            trigger,
            kind,
            isInherited,
            isLinked);
    }

    public static CardEffectFactoryBindingRule BindKeywordBaseBatch2(
        string id,
        IReadOnlyList<string> cardKeys,
        string trigger,
        KeywordBaseBatch2Kind kind,
        bool isInherited = false,
        bool isLinked = false,
        string? triggerReason = null)
    {
        return CardEffectFactoryBindingRules.KeywordBaseBatch2(
            id,
            cardKeys,
            trigger,
            kind,
            isInherited,
            isLinked,
            triggerReason);
    }

    public static CardEffectFactoryBindingResult Bind(
        CardEffectFactoryBindingRegistry registry,
        CardRecord card,
        string trigger,
        HeadlessEntityId sourceEntityId,
        HeadlessPlayerId controllerId,
        EffectContext context,
        HeadlessEntityId? targetEntityId = null)
    {
        ArgumentNullException.ThrowIfNull(registry);

        return registry.Bind(new CardEffectFactoryBindingRequest(
            card,
            trigger,
            sourceEntityId,
            controllerId,
            context,
            targetEntityId));
    }
}
