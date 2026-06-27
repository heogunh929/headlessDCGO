namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory;

using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;

using System.Collections.ObjectModel;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

public delegate IReadOnlyList<EffectBinding> PermanentEffectBindingFactory(PermanentEffectFactoryBindingRequest request);

public sealed record PermanentEffectFactoryBindingRequest
{
    public PermanentEffectFactoryBindingRequest(
        CardInstanceState permanent,
        string trigger,
        HeadlessPlayerId controllerId,
        EffectContext context,
        CardRecord? topCard = null,
        IReadOnlyDictionary<string, object?>? values = null)
    {
        ArgumentNullException.ThrowIfNull(permanent);
        ArgumentException.ThrowIfNullOrWhiteSpace(trigger);
        if (controllerId.IsEmpty)
        {
            throw new ArgumentException("Permanent effect controller id must not be empty.", nameof(controllerId));
        }

        ArgumentNullException.ThrowIfNull(context);
        if (context.SourceEntityId != permanent.InstanceId)
        {
            throw new ArgumentException("Permanent effect context source must match permanent instance id.", nameof(context));
        }

        if (topCard is not null && topCard.Id != permanent.DefinitionId)
        {
            throw new ArgumentException("Permanent effect top card definition must match permanent definition id.", nameof(topCard));
        }

        Permanent = permanent;
        Trigger = trigger.Trim();
        ControllerId = controllerId;
        Context = context;
        TopCard = topCard;
        Values = CopyValues(values);
    }

    public CardInstanceState Permanent { get; }

    public string Trigger { get; }

    public HeadlessPlayerId ControllerId { get; }

    public EffectContext Context { get; }

    public CardRecord? TopCard { get; }

    public IReadOnlyDictionary<string, object?> Values { get; }

    public HeadlessEntityId PermanentId => Permanent.InstanceId;

    public HeadlessEntityId DefinitionId => Permanent.DefinitionId;

    public IReadOnlyList<string> PermanentLookupKeys()
    {
        var keys = new List<string>
        {
            Permanent.InstanceId.Value,
            Permanent.DefinitionId.Value,
        };

        if (TopCard is not null)
        {
            keys.Add(TopCard.CardNumber);
            if (!string.IsNullOrWhiteSpace(TopCard.EffectBindingKey))
            {
                keys.Add(TopCard.EffectBindingKey!);
            }
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
                throw new ArgumentException("Permanent effect binding values must not contain null or whitespace keys.", nameof(values));
            }

            copy[pair.Key.Trim()] = pair.Value;
        }

        return new ReadOnlyDictionary<string, object?>(copy);
    }
}

public sealed record PermanentEffectFactoryBindingRule
{
    public PermanentEffectFactoryBindingRule(
        string id,
        IReadOnlyList<string> permanentKeys,
        string trigger,
        PermanentEffectBindingFactory factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(trigger);
        ArgumentNullException.ThrowIfNull(factory);

        Id = id.Trim();
        PermanentKeys = CopyPermanentKeys(permanentKeys);
        Trigger = trigger.Trim();
        Factory = factory;
    }

    public string Id { get; }

    public IReadOnlyList<string> PermanentKeys { get; }

    public string Trigger { get; }

    public PermanentEffectBindingFactory Factory { get; }

    public bool Matches(PermanentEffectFactoryBindingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return string.Equals(Trigger, request.Trigger, StringComparison.Ordinal)
            && request.PermanentLookupKeys().Any(key => PermanentKeys.Contains(key, StringComparer.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string> CopyPermanentKeys(IReadOnlyList<string> permanentKeys)
    {
        ArgumentNullException.ThrowIfNull(permanentKeys);

        string[] keys = permanentKeys
            .Select(key => string.IsNullOrWhiteSpace(key)
                ? throw new ArgumentException("Permanent effect binding keys must not contain null or whitespace values.", nameof(permanentKeys))
                : key.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ThenBy(key => key, StringComparer.Ordinal)
            .ToArray();

        if (keys.Length == 0)
        {
            throw new ArgumentException("Permanent effect binding requires at least one lookup key.", nameof(permanentKeys));
        }

        return Array.AsReadOnly(keys);
    }
}

public sealed record PermanentEffectFactoryBindingResult
{
    private PermanentEffectFactoryBindingResult(
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

    public static PermanentEffectFactoryBindingResult Success(
        IReadOnlyList<EffectBinding> bindings,
        IReadOnlyList<string> matchedRuleIds,
        IReadOnlyDictionary<string, object?> values)
    {
        return new PermanentEffectFactoryBindingResult(true, bindings, matchedRuleIds, null, null, values);
    }

    public static PermanentEffectFactoryBindingResult Failure(
        string errorCode,
        string message,
        IReadOnlyDictionary<string, object?> values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new PermanentEffectFactoryBindingResult(false, Array.Empty<EffectBinding>(), Array.Empty<string>(), errorCode, message, values);
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

public sealed class PermanentEffectFactoryBindingRegistry
{
    private readonly List<PermanentEffectFactoryBindingRule> _rules = new();
    private readonly Dictionary<string, PermanentEffectFactoryBindingRule> _rulesById = new(StringComparer.Ordinal);

    public void Register(PermanentEffectFactoryBindingRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (_rulesById.ContainsKey(rule.Id))
        {
            throw new InvalidOperationException($"Permanent effect binding rule '{rule.Id}' is already registered.");
        }

        _rules.Add(rule);
        _rulesById[rule.Id] = rule;
    }

    public IReadOnlyList<PermanentEffectFactoryBindingRule> Lookup(PermanentEffectFactoryBindingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _rules
            .Where(rule => rule.Matches(request))
            .OrderBy(rule => rule.Id, StringComparer.Ordinal)
            .ToArray();
    }

    public PermanentEffectFactoryBindingResult Bind(PermanentEffectFactoryBindingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        PermanentEffectFactoryBindingRule[] matchedRules = Lookup(request).ToArray();
        if (matchedRules.Length == 0)
        {
            return PermanentEffectFactoryBindingResult.Failure(
                "permanent_binding_not_found",
                $"No PermanentEffectFactory binding matched permanent '{request.PermanentId.Value}' and trigger '{request.Trigger}'.",
                BaseValues(request, matchedRules, Array.Empty<EffectBinding>()));
        }

        var bindings = new List<EffectBinding>();
        foreach (PermanentEffectFactoryBindingRule rule in matchedRules)
        {
            IReadOnlyList<EffectBinding> created = rule.Factory(request)
                ?? throw new InvalidOperationException($"Permanent effect binding rule '{rule.Id}' returned null.");
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
            return PermanentEffectFactoryBindingResult.Failure(
                "duplicate_permanent_effect_binding",
                $"PermanentEffectFactory produced duplicate effect id '{duplicateEffectId}'.",
                BaseValues(request, matchedRules, orderedBindings));
        }

        return PermanentEffectFactoryBindingResult.Success(
            orderedBindings,
            matchedRules.Select(rule => rule.Id).ToArray(),
            BaseValues(request, matchedRules, orderedBindings));
    }

    public PermanentEffectFactoryBindingResult BindAndRegister(
        EffectRegistry registry,
        PermanentEffectFactoryBindingRequest request)
    {
        ArgumentNullException.ThrowIfNull(registry);
        PermanentEffectFactoryBindingResult result = Bind(request);
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
        PermanentEffectFactoryBindingRequest request,
        IReadOnlyList<PermanentEffectFactoryBindingRule> matchedRules,
        IReadOnlyList<EffectBinding> bindings)
    {
        return new ReadOnlyDictionary<string, object?>(
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["permanentId"] = request.PermanentId.Value,
                ["definitionId"] = request.DefinitionId.Value,
                ["topCardNumber"] = request.TopCard?.CardNumber,
                ["effectBindingKey"] = request.TopCard?.EffectBindingKey,
                ["trigger"] = request.Trigger,
                ["controllerId"] = request.ControllerId.Value,
                ["permanentLookupKeys"] = request.PermanentLookupKeys().ToArray(),
                ["matchedRuleIds"] = matchedRules.Select(rule => rule.Id).ToArray(),
                ["bindingEffectIds"] = bindings.Select(binding => binding.Request.EffectId.Value).ToArray(),
                ["bindingCount"] = bindings.Count,
            });
    }
}

public static class PermanentEffectFactoryBindingRules
{
    public const string DeleteSelfTiming = "PermanentDeleteSelf";
    public const string ImmunityTiming = "PermanentImmunity";
    public const string CollisionTiming = "PermanentCollision";
    public const string DetailTiming = "PermanentDetail";

    public const string DeleteSelfScope = "PermanentDeleteSelf";
    public const string ImmunityScope = "PermanentImmunity";
    public const string CollisionScope = "PermanentCollision";
    public const string DetailScope = "PermanentDetail";

    public const string PermanentEffectKindKey = "permanentEffectKind";
    public const string PermanentIdKey = "permanentId";
    public const string DefinitionIdKey = "definitionId";
    public const string SourcePermanentKey = "sourcePermanent";
    public const string DetailKey = "detail";
    public const string TriggerEffectKey = "triggerEffect";

    public static PermanentEffectFactoryBindingRule FromBindingFactory(
        string id,
        IReadOnlyList<string> permanentKeys,
        string trigger,
        PermanentEffectBindingFactory factory)
    {
        return new PermanentEffectFactoryBindingRule(id, permanentKeys, trigger, factory);
    }

    public static PermanentEffectFactoryBindingRule DeleteSelf(
        string id,
        IReadOnlyList<string> permanentKeys,
        string trigger = DeleteSelfTiming)
    {
        return FromBindingFactory(
            id,
            permanentKeys,
            trigger,
            request => SingleBinding(
                request,
                "DeleteSelf",
                DeleteSelfTiming,
                EffectQueryRole.Replacement,
                DeleteSelfScope,
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [ReplacementHelpers.PreventDeletionKey] = false,
                    ["action"] = "DeleteSelf",
                }));
    }

    public static PermanentEffectFactoryBindingRule Immunity(
        string id,
        IReadOnlyList<string> permanentKeys,
        string immunityKind,
        string trigger = ImmunityTiming)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(immunityKind);

        return FromBindingFactory(
            id,
            permanentKeys,
            trigger,
            request => SingleBinding(
                request,
                immunityKind.Trim(),
                ImmunityTiming,
                EffectQueryRole.Replacement,
                ImmunityScope,
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [ReplacementHelpers.ImmuneFromEffectsKey] = true,
                    [ReplacementHelpers.MutationKindKey] = immunityKind.Trim(),
                }));
    }

    public static PermanentEffectFactoryBindingRule Collision(
        string id,
        IReadOnlyList<string> permanentKeys,
        string trigger = CollisionTiming)
    {
        return FromBindingFactory(
            id,
            permanentKeys,
            trigger,
            request => SingleBinding(
                request,
                "Collision",
                CollisionTiming,
                EffectQueryRole.Continuous,
                CollisionScope,
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["keyword"] = "Collision",
                    ["canAttackSuspended"] = true,
                }));
    }

    public static PermanentEffectFactoryBindingRule Detail(
        string id,
        IReadOnlyList<string> permanentKeys,
        string detail,
        bool triggerEffect,
        string trigger = DetailTiming)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);

        return FromBindingFactory(
            id,
            permanentKeys,
            trigger,
            request => SingleBinding(
                request,
                "Detail",
                DetailTiming,
                EffectQueryRole.Continuous,
                DetailScope,
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [DetailKey] = detail.Trim(),
                    [TriggerEffectKey] = triggerEffect,
                }));
    }

    private static IReadOnlyList<EffectBinding> SingleBinding(
        PermanentEffectFactoryBindingRequest request,
        string effectKind,
        string timing,
        EffectQueryRole queryRole,
        string queryScope,
        IReadOnlyDictionary<string, object?> values)
    {
        var contextValues = new Dictionary<string, object?>(request.Context.Values, StringComparer.Ordinal);
        foreach (KeyValuePair<string, object?> pair in request.Values)
        {
            contextValues[pair.Key] = pair.Value;
        }

        foreach (KeyValuePair<string, object?> pair in values)
        {
            contextValues[pair.Key] = pair.Value;
        }

        contextValues[PermanentEffectKindKey] = effectKind;
        contextValues[PermanentIdKey] = request.PermanentId.Value;
        contextValues[DefinitionIdKey] = request.DefinitionId.Value;
        contextValues[SourcePermanentKey] = true;

        var context = new EffectContext(
            request.Context.SourcePlayerId,
            request.Context.OwnerPlayerId,
            request.PermanentId,
            request.Context.TriggerEntityId,
            request.Context.TargetEntityIds.Count == 0
                ? new[] { request.PermanentId }
                : request.Context.TargetEntityIds,
            contextValues);
        string normalizedKind = effectKind.Replace(" ", string.Empty, StringComparison.Ordinal);
        var requestEffect = new EffectRequest(
            new HeadlessEntityId($"{request.PermanentId.Value}:permanent:{normalizedKind}"),
            request.ControllerId,
            timing,
            context);
        var binding = new EffectBinding(
            requestEffect,
            new[] { effectKind, $"Permanent:{effectKind}" },
            queryRole,
            new[] { queryScope });

        return Array.AsReadOnly(new[] { binding });
    }
}
