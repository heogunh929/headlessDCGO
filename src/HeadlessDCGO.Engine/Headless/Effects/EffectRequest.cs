namespace HeadlessDCGO.Engine.Headless.Effects;

using HeadlessDCGO.Engine.Headless.Services;

public sealed record EffectRequest
{
    public EffectRequest(
        HeadlessEntityId effectId,
        HeadlessPlayerId controllerId,
        string timing,
        EffectContext context)
    {
        if (effectId.IsEmpty)
        {
            throw new ArgumentException("Effect id must not be empty.", nameof(effectId));
        }

        if (controllerId.IsEmpty)
        {
            throw new ArgumentException("Effect controller id must not be empty.", nameof(controllerId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(timing);
        ArgumentNullException.ThrowIfNull(context);

        EffectId = effectId;
        ControllerId = controllerId;
        Timing = timing.Trim();
        Context = context;
    }

    public HeadlessEntityId EffectId { get; }

    public HeadlessPlayerId ControllerId { get; }

    public string Timing { get; }

    public EffectContext Context { get; }
}
