namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public static class EffectChoiceHelperFactory
{
    public static ChoiceRequest CreateCardRequest(
        HeadlessPlayerId playerId,
        string message,
        int minCount,
        int maxCount,
        bool canSkip,
        ChoiceZone sourceZone,
        IReadOnlyList<ChoiceCandidate> candidates)
    {
        return EffectChoiceHelpers.CreateCardRequest(playerId, message, minCount, maxCount, canSkip, sourceZone, candidates);
    }

    public static ChoiceRequest CreatePermanentRequest(
        HeadlessPlayerId playerId,
        string message,
        int minCount,
        int maxCount,
        bool canSkip,
        IReadOnlyList<ChoiceCandidate> candidates)
    {
        return EffectChoiceHelpers.CreatePermanentRequest(playerId, message, minCount, maxCount, canSkip, candidates);
    }

    public static ChoiceRequest CreateCountRequest(
        HeadlessPlayerId playerId,
        string message,
        int minCount,
        int maxCount,
        bool canSkip,
        IEnumerable<int>? candidateCounts = null)
    {
        return EffectChoiceHelpers.CreateCountRequest(playerId, message, minCount, maxCount, canSkip, candidateCounts);
    }

    public static EffectChoiceResolution ApplyResult(
        EffectContext context,
        ChoiceRequest request,
        ChoiceResult result,
        string keyPrefix = EffectChoiceHelpers.DefaultKeyPrefix)
    {
        return EffectChoiceHelpers.ApplyResult(context, request, result, keyPrefix);
    }

    public static Task<EffectChoiceResolution> ResolveAsync(
        EffectContext context,
        ChoiceRequest request,
        IChoiceProvider provider,
        string keyPrefix = EffectChoiceHelpers.DefaultKeyPrefix,
        CancellationToken cancellationToken = default)
    {
        return EffectChoiceHelpers.ResolveAsync(context, request, provider, keyPrefix, cancellationToken);
    }
}
