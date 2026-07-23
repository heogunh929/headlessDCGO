namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

// (C5-0) The invented choice-resolution engine (EffectChoiceResolution record, ApplyResult / ResolveAsync /
// WithValues / RequestValues / ResultValues / Key / CandidatesFromIds, the 14 value-key consts, and the
// EffectChoiceHelperFactory facade) was DELETED here — it had ZERO live consumers (only tests/G3K-001 exercised
// it) and no AS-IS analogue. Live effect resolution runs through the substrate (context.ChoiceProvider.ChooseAsync
// / ChoiceController.RequestChoice, Headless/Choices). Only the substrate request-builders below remain live.
public static class EffectChoiceHelpers
{
    public static ChoiceCandidate Candidate(
        HeadlessEntityId id,
        string label,
        ChoiceZone zone,
        bool isSelectable = true,
        HeadlessPlayerId? ownerId = null)
    {
        return new ChoiceCandidate(id, label, zone, isSelectable, ownerId);
    }

    public static ChoiceRequest CreateRequest(
        ChoiceType type,
        HeadlessPlayerId playerId,
        string message,
        int minCount,
        int maxCount,
        bool canSkip,
        ChoiceZone sourceZone,
        IReadOnlyList<ChoiceCandidate> candidates)
    {
        return new ChoiceRequest(
            type,
            playerId,
            message,
            minCount,
            maxCount,
            canSkip,
            sourceZone,
            candidates);
    }

    public static ChoiceRequest CreateCardRequest(
        HeadlessPlayerId playerId,
        string message,
        int minCount,
        int maxCount,
        bool canSkip,
        ChoiceZone sourceZone,
        IReadOnlyList<ChoiceCandidate> candidates)
    {
        return CreateRequest(ChoiceType.Card, playerId, message, minCount, maxCount, canSkip, sourceZone, candidates);
    }

    public static ChoiceRequest CreatePermanentRequest(
        HeadlessPlayerId playerId,
        string message,
        int minCount,
        int maxCount,
        bool canSkip,
        IReadOnlyList<ChoiceCandidate> candidates)
    {
        return CreateRequest(ChoiceType.Permanent, playerId, message, minCount, maxCount, canSkip, ChoiceZone.BattleArea, candidates);
    }

    public static ChoiceRequest CreateCountRequest(
        HeadlessPlayerId playerId,
        string message,
        int minCount,
        int maxCount,
        bool canSkip,
        IEnumerable<int>? candidateCounts = null)
    {
        if (minCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minCount), "Minimum count must not be negative.");
        }

        if (maxCount < minCount)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCount), "Maximum count must be greater than or equal to minimum count.");
        }

        int[] counts = (candidateCounts ?? Enumerable.Range(minCount, maxCount - minCount + 1))
            .Distinct()
            .OrderBy(count => count)
            .ToArray();
        if (counts.Length == 0)
        {
            throw new ArgumentException("Count choice requires at least one count candidate.", nameof(candidateCounts));
        }

        if (counts.Any(count => count < minCount || count > maxCount))
        {
            throw new ArgumentException("Count choice candidates must stay inside the request count range.", nameof(candidateCounts));
        }

        ChoiceCandidate[] candidates = counts
            .Select(count => Candidate(new HeadlessEntityId($"count:{count}"), count.ToString(), ChoiceZone.Custom))
            .ToArray();
        return CreateRequest(ChoiceType.Count, playerId, message, minCount, maxCount, canSkip, ChoiceZone.Custom, candidates);
    }
}
