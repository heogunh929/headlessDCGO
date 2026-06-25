namespace HeadlessDCGO.Engine.Headless.Services;

using HeadlessDCGO.Engine.Headless.Effects;

public sealed class InMemoryEffectQueryService : IEffectQueryService
{
    private readonly List<EffectRequest> _effects = new();

    public void Register(EffectRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        _effects.Add(request);
    }

    public IReadOnlyList<EffectRequest> GetEffectsForTiming(string timing)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timing);
        string normalizedTiming = timing.Trim();

        return _effects
            .Where(effect => string.Equals(effect.Timing, normalizedTiming, StringComparison.Ordinal))
            .ToArray();
    }

    public IReadOnlyList<EffectRequest> GetContinuousEffects(EffectQueryContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Array.Empty<EffectRequest>();
    }

    public IReadOnlyList<EffectRequest> GetReplacementEffects(EffectQueryContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Array.Empty<EffectRequest>();
    }

    public IReadOnlyList<EffectRequest> GetModifierEffects(EffectQueryContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Array.Empty<EffectRequest>();
    }

    public IReadOnlyList<EffectRequest> GetRestrictionEffects(EffectQueryContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Array.Empty<EffectRequest>();
    }

    public bool HasEffect(HeadlessEntityId effectId)
    {
        return !effectId.IsEmpty && _effects.Any(effect => effect.EffectId == effectId);
    }

    public void Clear()
    {
        _effects.Clear();
    }
}
