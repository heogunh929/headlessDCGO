namespace HeadlessDCGO.Engine.Headless.Effects;

public sealed record PendingEffect
{
    public PendingEffect(
        EffectRequest request,
        EffectResolutionMode mode)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), "Effect resolution mode must be a known value.");
        }

        Request = request;
        Mode = mode;
    }

    public EffectRequest Request { get; }

    public EffectResolutionMode Mode { get; }
}
