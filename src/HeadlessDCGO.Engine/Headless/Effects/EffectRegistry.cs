namespace HeadlessDCGO.Engine.Headless.Effects;

using System.Collections.ObjectModel;
using HeadlessDCGO.Engine.Headless.Services;

public interface EffectRegistry : IEffectQueryService
{
    void Register(EffectBinding binding);

    IReadOnlyList<EffectBinding> GetEffects(
        HeadlessEntityId sourceEntityId,
        string timing);

    IReadOnlyList<EffectBinding> GetKeywordEffects(string keyword);

    EffectBinding? Find(HeadlessEntityId effectId);
}

public sealed class InMemoryEffectRegistry : EffectRegistry
{
    private readonly List<EffectBinding> _bindings = new();
    private readonly Dictionary<HeadlessEntityId, EffectBinding> _bindingsByEffectId = new();

    public void Register(EffectBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);

        if (_bindingsByEffectId.ContainsKey(binding.Request.EffectId))
        {
            throw new InvalidOperationException(
                $"Effect binding already exists for effect id '{binding.Request.EffectId.Value}'.");
        }

        _bindings.Add(binding);
        _bindingsByEffectId[binding.Request.EffectId] = binding;
    }

    public IReadOnlyList<EffectBinding> GetEffects(
        HeadlessEntityId sourceEntityId,
        string timing)
    {
        if (sourceEntityId.IsEmpty)
        {
            throw new ArgumentException("Effect source entity id must not be empty.", nameof(sourceEntityId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(timing);
        string normalizedTiming = timing.Trim();

        return _bindings
            .Where(binding =>
                binding.Request.Context.SourceEntityId == sourceEntityId
                && string.Equals(binding.Request.Timing, normalizedTiming, StringComparison.Ordinal))
            .ToArray();
    }

    public IReadOnlyList<EffectBinding> GetKeywordEffects(string keyword)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyword);
        string normalizedKeyword = keyword.Trim();

        return _bindings
            .Where(binding => binding.Keywords.Contains(normalizedKeyword, StringComparer.Ordinal))
            .ToArray();
    }

    public IReadOnlyList<EffectRequest> GetEffectsForTiming(string timing)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timing);
        string normalizedTiming = timing.Trim();

        return _bindings
            .Where(binding => string.Equals(binding.Request.Timing, normalizedTiming, StringComparison.Ordinal))
            .Select(binding => binding.Request)
            .ToArray();
    }

    public IReadOnlyList<EffectRequest> GetContinuousEffects(EffectQueryContext context)
    {
        return GetRequestsForRole(EffectQueryRole.Continuous, context);
    }

    public IReadOnlyList<EffectRequest> GetReplacementEffects(EffectQueryContext context)
    {
        return GetRequestsForRole(EffectQueryRole.Replacement, context);
    }

    public IReadOnlyList<EffectRequest> GetModifierEffects(EffectQueryContext context)
    {
        return GetRequestsForRole(EffectQueryRole.Modifier, context);
    }

    public IReadOnlyList<EffectRequest> GetRestrictionEffects(EffectQueryContext context)
    {
        return GetRequestsForRole(EffectQueryRole.Restriction, context);
    }

    public bool HasEffect(HeadlessEntityId effectId)
    {
        return !effectId.IsEmpty && _bindingsByEffectId.ContainsKey(effectId);
    }

    public EffectBinding? Find(HeadlessEntityId effectId)
    {
        return effectId.IsEmpty
            ? null
            : _bindingsByEffectId.GetValueOrDefault(effectId);
    }

    public void Clear()
    {
        _bindings.Clear();
        _bindingsByEffectId.Clear();
    }

    private IReadOnlyList<EffectRequest> GetRequestsForRole(
        EffectQueryRole role,
        EffectQueryContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return _bindings
            .Where(binding => binding.HasRole(role) && binding.MatchesQuery(context))
            .Select(binding => binding.Request)
            .ToArray();
    }
}

public sealed record EffectBinding
{
    public EffectBinding(
        EffectRequest request,
        IReadOnlyList<string>? keywords = null,
        EffectQueryRole queryRoles = EffectQueryRole.None,
        IReadOnlyList<string>? queryScopes = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!AreValidRoles(queryRoles))
        {
            throw new ArgumentOutOfRangeException(nameof(queryRoles), "Effect query roles must be known flag values.");
        }

        Request = request;
        Keywords = CopyKeywords(keywords);
        QueryRoles = queryRoles;
        QueryScopes = CopyQueryScopes(queryScopes);
    }

    public EffectRequest Request { get; }

    public IReadOnlyList<string> Keywords { get; }

    public EffectQueryRole QueryRoles { get; }

    public IReadOnlyList<string> QueryScopes { get; }

    public bool HasRole(EffectQueryRole role)
    {
        return role != EffectQueryRole.None && QueryRoles.HasFlag(role);
    }

    public bool MatchesQuery(EffectQueryContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return QueryScopes.Contains(context.Scope, StringComparer.Ordinal) && context.Matches(Request);
    }

    private static IReadOnlyList<string> CopyKeywords(IReadOnlyList<string>? keywords)
    {
        if (keywords is null || keywords.Count == 0)
        {
            return Array.AsReadOnly(Array.Empty<string>());
        }

        var values = new List<string>(keywords.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string keyword in keywords)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                throw new ArgumentException("Effect binding keywords must not contain null or whitespace values.", nameof(keywords));
            }

            string normalizedKeyword = keyword.Trim();
            if (seen.Add(normalizedKeyword))
            {
                values.Add(normalizedKeyword);
            }
        }

        return new ReadOnlyCollection<string>(values);
    }

    private static IReadOnlyList<string> CopyQueryScopes(IReadOnlyList<string>? queryScopes)
    {
        if (queryScopes is null || queryScopes.Count == 0)
        {
            return Array.AsReadOnly(Array.Empty<string>());
        }

        var values = new List<string>(queryScopes.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string queryScope in queryScopes)
        {
            if (string.IsNullOrWhiteSpace(queryScope))
            {
                throw new ArgumentException("Effect binding query scopes must not contain null or whitespace values.", nameof(queryScopes));
            }

            string normalizedScope = queryScope.Trim();
            if (seen.Add(normalizedScope))
            {
                values.Add(normalizedScope);
            }
        }

        return new ReadOnlyCollection<string>(values);
    }

    private static bool AreValidRoles(EffectQueryRole roles)
    {
        const EffectQueryRole allRoles =
            EffectQueryRole.Continuous
            | EffectQueryRole.Replacement
            | EffectQueryRole.Modifier
            | EffectQueryRole.Restriction;

        return (roles & ~allRoles) == EffectQueryRole.None;
    }
}
