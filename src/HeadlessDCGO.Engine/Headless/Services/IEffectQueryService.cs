namespace HeadlessDCGO.Engine.Headless.Services;

using HeadlessDCGO.Engine.Headless.Effects;

public interface IEffectQueryService
{
    IReadOnlyList<EffectRequest> GetEffectsForTiming(string timing);

    IReadOnlyList<EffectRequest> GetContinuousEffects(EffectQueryContext context);

    IReadOnlyList<EffectRequest> GetReplacementEffects(EffectQueryContext context);

    IReadOnlyList<EffectRequest> GetModifierEffects(EffectQueryContext context);

    IReadOnlyList<EffectRequest> GetRestrictionEffects(EffectQueryContext context);

    bool HasEffect(HeadlessEntityId effectId);
}

[Flags]
public enum EffectQueryRole
{
    None = 0,
    Continuous = 1,
    Replacement = 2,
    Modifier = 4,
    Restriction = 8,
}

public sealed record EffectQueryContext
{
    public EffectQueryContext(
        string scope,
        HeadlessEntityId? sourceEntityId = null,
        HeadlessPlayerId? playerId = null,
        HeadlessEntityId? targetEntityId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);

        if (sourceEntityId is { IsEmpty: true })
        {
            throw new ArgumentException("Effect query source entity id must not be empty.", nameof(sourceEntityId));
        }

        if (playerId is { IsEmpty: true })
        {
            throw new ArgumentException("Effect query player id must not be empty.", nameof(playerId));
        }

        if (targetEntityId is { IsEmpty: true })
        {
            throw new ArgumentException("Effect query target entity id must not be empty.", nameof(targetEntityId));
        }

        Scope = scope.Trim();
        SourceEntityId = sourceEntityId;
        PlayerId = playerId;
        TargetEntityId = targetEntityId;
    }

    public string Scope { get; }

    public HeadlessEntityId? SourceEntityId { get; }

    public HeadlessPlayerId? PlayerId { get; }

    public HeadlessEntityId? TargetEntityId { get; }

    public bool Matches(EffectRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (SourceEntityId is HeadlessEntityId sourceEntityId
            && request.Context.SourceEntityId != sourceEntityId)
        {
            return false;
        }

        if (PlayerId is HeadlessPlayerId playerId
            && request.ControllerId != playerId
            && request.Context.OwnerPlayerId != playerId
            && request.Context.SourcePlayerId != playerId)
        {
            return false;
        }

        if (TargetEntityId is HeadlessEntityId targetEntityId
            && !request.Context.TargetEntityIds.Contains(targetEntityId))
        {
            return false;
        }

        return true;
    }
}
