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

    // (③-B) GetEffectsForTiming(string) RETIRED — timing-keyed read, no live consumer at producer 0.

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

    public void Clear()
    {
        _effects.Clear();
    }
}
