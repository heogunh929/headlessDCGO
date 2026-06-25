namespace HeadlessDCGO.Engine.Headless.Effects;

using System.Collections.ObjectModel;
using HeadlessDCGO.Engine.Headless.Services;

public delegate IReadOnlyList<EffectBinding> CardEffectBindingFactory(CardEffectFactoryBindingRequest request);

public sealed record CardEffectFactoryBindingRequest
{
    public CardEffectFactoryBindingRequest(
        CardRecord card,
        string trigger,
        HeadlessEntityId sourceEntityId,
        HeadlessPlayerId controllerId,
        EffectContext context,
        HeadlessEntityId? targetEntityId = null,
        IReadOnlyDictionary<string, object?>? values = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(trigger);
        if (sourceEntityId.IsEmpty)
        {
            throw new ArgumentException("Factory binding source entity id must not be empty.", nameof(sourceEntityId));
        }

        if (controllerId.IsEmpty)
        {
            throw new ArgumentException("Factory binding controller id must not be empty.", nameof(controllerId));
        }

        ArgumentNullException.ThrowIfNull(context);
        if (context.SourceEntityId != sourceEntityId)
        {
            throw new ArgumentException("Factory binding context source must match source entity id.", nameof(context));
        }

        if (targetEntityId is { IsEmpty: true })
        {
            throw new ArgumentException("Factory binding target entity id must not be empty.", nameof(targetEntityId));
        }

        Card = card;
        Trigger = trigger.Trim();
        SourceEntityId = sourceEntityId;
        ControllerId = controllerId;
        Context = context;
        TargetEntityId = targetEntityId;
        Values = CopyValues(values);
    }

    public CardRecord Card { get; }

    public string Trigger { get; }

    public HeadlessEntityId SourceEntityId { get; }

    public HeadlessPlayerId ControllerId { get; }

    public EffectContext Context { get; }

    public HeadlessEntityId? TargetEntityId { get; }

    public IReadOnlyDictionary<string, object?> Values { get; }

    public IReadOnlyList<string> CardLookupKeys()
    {
        var keys = new List<string> { Card.CardNumber, Card.Id.Value };
        if (!string.IsNullOrWhiteSpace(Card.EffectBindingKey))
        {
            keys.Add(Card.EffectBindingKey!);
        }

        return Array.AsReadOnly(keys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ThenBy(key => key, StringComparer.Ordinal)
            .ToArray());
    }

    private static IReadOnlyDictionary<string, object?> CopyValues(IReadOnlyDictionary<string, object?>? values)
    {
        if (values is null)
        {
            return new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>(StringComparer.Ordinal));
        }

        var copy = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, object?> pair in values)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                throw new ArgumentException("Factory binding values must not contain null or whitespace keys.", nameof(values));
            }

            copy[pair.Key.Trim()] = pair.Value;
        }

        return new ReadOnlyDictionary<string, object?>(copy);
    }
}

public sealed record CardEffectFactoryBindingRule
{
    public CardEffectFactoryBindingRule(
        string id,
        IReadOnlyList<string> cardKeys,
        string trigger,
        CardEffectBindingFactory factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(trigger);
        ArgumentNullException.ThrowIfNull(factory);

        Id = id.Trim();
        CardKeys = CopyCardKeys(cardKeys);
        Trigger = trigger.Trim();
        Factory = factory;
    }

    public string Id { get; }

    public IReadOnlyList<string> CardKeys { get; }

    public string Trigger { get; }

    public CardEffectBindingFactory Factory { get; }

    public bool Matches(CardEffectFactoryBindingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return string.Equals(Trigger, request.Trigger, StringComparison.Ordinal)
            && request.CardLookupKeys().Any(key => CardKeys.Contains(key, StringComparer.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string> CopyCardKeys(IReadOnlyList<string> cardKeys)
    {
        ArgumentNullException.ThrowIfNull(cardKeys);

        string[] keys = cardKeys
            .Select(key => string.IsNullOrWhiteSpace(key)
                ? throw new ArgumentException("Factory binding card keys must not contain null or whitespace values.", nameof(cardKeys))
                : key.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ThenBy(key => key, StringComparer.Ordinal)
            .ToArray();

        if (keys.Length == 0)
        {
            throw new ArgumentException("Factory binding requires at least one card key.", nameof(cardKeys));
        }

        return Array.AsReadOnly(keys);
    }
}

public sealed record CardEffectFactoryBindingResult
{
    private CardEffectFactoryBindingResult(
        bool isSuccess,
        IReadOnlyList<EffectBinding> bindings,
        IReadOnlyList<string> matchedRuleIds,
        string? errorCode,
        string? message,
        IReadOnlyDictionary<string, object?> values)
    {
        IsSuccess = isSuccess;
        Bindings = Array.AsReadOnly(bindings.ToArray());
        MatchedRuleIds = Array.AsReadOnly(matchedRuleIds.ToArray());
        ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? null : errorCode.Trim();
        Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim();
        Values = CopyValues(values);
    }

    public bool IsSuccess { get; }

    public IReadOnlyList<EffectBinding> Bindings { get; }

    public IReadOnlyList<string> MatchedRuleIds { get; }

    public string? ErrorCode { get; }

    public string? Message { get; }

    public IReadOnlyDictionary<string, object?> Values { get; }

    public static CardEffectFactoryBindingResult Success(
        IReadOnlyList<EffectBinding> bindings,
        IReadOnlyList<string> matchedRuleIds,
        IReadOnlyDictionary<string, object?> values)
    {
        return new CardEffectFactoryBindingResult(true, bindings, matchedRuleIds, null, null, values);
    }

    public static CardEffectFactoryBindingResult Failure(
        string errorCode,
        string message,
        IReadOnlyDictionary<string, object?> values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new CardEffectFactoryBindingResult(false, Array.Empty<EffectBinding>(), Array.Empty<string>(), errorCode, message, values);
    }

    private static IReadOnlyDictionary<string, object?> CopyValues(IReadOnlyDictionary<string, object?> values)
    {
        var copy = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, object?> pair in values.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pair.Key);
            copy[pair.Key.Trim()] = pair.Value;
        }

        return new ReadOnlyDictionary<string, object?>(copy);
    }
}

public sealed class CardEffectFactoryBindingRegistry
{
    private readonly List<CardEffectFactoryBindingRule> _rules = new();
    private readonly Dictionary<string, CardEffectFactoryBindingRule> _rulesById = new(StringComparer.Ordinal);

    public void Register(CardEffectFactoryBindingRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (_rulesById.ContainsKey(rule.Id))
        {
            throw new InvalidOperationException($"Factory binding rule '{rule.Id}' is already registered.");
        }

        _rules.Add(rule);
        _rulesById[rule.Id] = rule;
    }

    public IReadOnlyList<CardEffectFactoryBindingRule> Lookup(
        CardRecord card,
        string trigger,
        HeadlessPlayerId controllerId,
        EffectContext context)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(trigger);
        ArgumentNullException.ThrowIfNull(context);

        var request = new CardEffectFactoryBindingRequest(card, trigger, card.Id, controllerId, context);
        return Lookup(request);
    }

    [Obsolete(
        "Use Lookup(card, trigger, controllerId, context). This overload assumes player 1 " +
        "and is kept only for legacy/test callers (B-01).")]
    public IReadOnlyList<CardEffectFactoryBindingRule> Lookup(CardRecord card, string trigger)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(trigger);

        var controllerId = new HeadlessPlayerId(1);
        return Lookup(card, trigger, controllerId, new EffectContext(controllerId, card.Id));
    }

    public IReadOnlyList<CardEffectFactoryBindingRule> Lookup(CardEffectFactoryBindingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _rules
            .Where(rule => rule.Matches(request))
            .OrderBy(rule => rule.Id, StringComparer.Ordinal)
            .ToArray();
    }

    public CardEffectFactoryBindingResult Bind(CardEffectFactoryBindingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        CardEffectFactoryBindingRule[] matchedRules = Lookup(request).ToArray();
        if (matchedRules.Length == 0)
        {
            return CardEffectFactoryBindingResult.Failure(
                "factory_binding_not_found",
                $"No CardEffectFactory binding matched card '{request.Card.CardNumber}' and trigger '{request.Trigger}'.",
                BaseValues(request, matchedRules, Array.Empty<EffectBinding>()));
        }

        var bindings = new List<EffectBinding>();
        foreach (CardEffectFactoryBindingRule rule in matchedRules)
        {
            IReadOnlyList<EffectBinding> created = rule.Factory(request)
                ?? throw new InvalidOperationException($"Factory binding rule '{rule.Id}' returned null.");
            bindings.AddRange(created.Where(binding => binding is not null));
        }

        EffectBinding[] orderedBindings = bindings
            .OrderBy(binding => binding.Request.EffectId.Value, StringComparer.Ordinal)
            .ToArray();
        string? duplicateEffectId = orderedBindings
            .GroupBy(binding => binding.Request.EffectId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key.Value)
            .FirstOrDefault();

        if (duplicateEffectId is not null)
        {
            return CardEffectFactoryBindingResult.Failure(
                "duplicate_effect_binding",
                $"Factory binding produced duplicate effect id '{duplicateEffectId}'.",
                BaseValues(request, matchedRules, orderedBindings));
        }

        return CardEffectFactoryBindingResult.Success(
            orderedBindings,
            matchedRules.Select(rule => rule.Id).ToArray(),
            BaseValues(request, matchedRules, orderedBindings));
    }

    public CardEffectFactoryBindingResult BindAndRegister(
        EffectRegistry registry,
        CardEffectFactoryBindingRequest request)
    {
        ArgumentNullException.ThrowIfNull(registry);
        CardEffectFactoryBindingResult result = Bind(request);
        if (!result.IsSuccess)
        {
            return result;
        }

        foreach (EffectBinding binding in result.Bindings)
        {
            registry.Register(binding);
        }

        return result;
    }

    public void Clear()
    {
        _rules.Clear();
        _rulesById.Clear();
    }

    private static IReadOnlyDictionary<string, object?> BaseValues(
        CardEffectFactoryBindingRequest request,
        IReadOnlyList<CardEffectFactoryBindingRule> matchedRules,
        IReadOnlyList<EffectBinding> bindings)
    {
        return new ReadOnlyDictionary<string, object?>(
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["cardNumber"] = request.Card.CardNumber,
                ["cardId"] = request.Card.Id.Value,
                ["effectBindingKey"] = request.Card.EffectBindingKey,
                ["trigger"] = request.Trigger,
                ["sourceEntityId"] = request.SourceEntityId.Value,
                ["controllerId"] = request.ControllerId.Value,
                ["targetEntityId"] = request.TargetEntityId?.Value,
                ["cardLookupKeys"] = request.CardLookupKeys().ToArray(),
                ["matchedRuleIds"] = matchedRules.Select(rule => rule.Id).ToArray(),
                ["bindingEffectIds"] = bindings.Select(binding => binding.Request.EffectId.Value).ToArray(),
                ["bindingCount"] = bindings.Count,
            });
    }
}

public static class CardEffectFactoryBindingRules
{
    public static CardEffectFactoryBindingRule FromBindingFactory(
        string id,
        IReadOnlyList<string> cardKeys,
        string trigger,
        CardEffectBindingFactory factory)
    {
        return new CardEffectFactoryBindingRule(id, cardKeys, trigger, factory);
    }

    public static CardEffectFactoryBindingRule KeywordBaseBatch1(
        string id,
        IReadOnlyList<string> cardKeys,
        string trigger,
        KeywordBaseBatch1Kind kind,
        bool isInherited = false,
        bool isLinked = false)
    {
        return FromBindingFactory(
            id,
            cardKeys,
            trigger,
            request =>
            {
                KeywordBaseBatch1Effect effect = KeywordBaseBatch1Factory.Create(
                    kind,
                    request.SourceEntityId,
                    request.TargetEntityId,
                    isInherited,
                    isLinked);
                return Array.AsReadOnly(new[] { effect.ToBinding(request.ControllerId, request.Context) });
            });
    }

    public static CardEffectFactoryBindingRule KeywordBaseBatch2(
        string id,
        IReadOnlyList<string> cardKeys,
        string trigger,
        KeywordBaseBatch2Kind kind,
        bool isInherited = false,
        bool isLinked = false,
        string? triggerReason = null)
    {
        return FromBindingFactory(
            id,
            cardKeys,
            trigger,
            request =>
            {
                KeywordBaseBatch2Effect effect = KeywordBaseBatch2Factory.Create(
                    kind,
                    request.SourceEntityId,
                    request.TargetEntityId,
                    isInherited,
                    isLinked,
                    triggerReason);
                return Array.AsReadOnly(new[] { effect.ToBinding(request.ControllerId, request.Context) });
            });
    }
}
