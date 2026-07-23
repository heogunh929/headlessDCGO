namespace HeadlessDCGO.Engine.Headless.Services;

using HeadlessDCGO.Engine.Headless.Effects;

// (④) interface IEffectQueryService + [Flags] enum EffectQueryRole DELETED — the invented EffectRegistry
// query surface (GetContinuousEffects/GetReplacement/GetModifier/GetRestrictionEffects + the role flags) had
// producer 0 and no surviving consumer. EffectQueryContext survives (the continuous-scope query key still used
// by ContinuousScopeEvaluation; the former ContinuousEffectEvaluator.BuildValues consumer is now deleted).

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
