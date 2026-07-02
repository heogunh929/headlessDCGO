namespace HeadlessDCGO.Engine.Headless.Choices;

using HeadlessDCGO.Engine.Headless.Services;

public sealed record ChoiceRequest
{
    public ChoiceRequest(
        ChoiceType type,
        HeadlessPlayerId playerId,
        string message,
        int minCount,
        int maxCount,
        bool canSkip,
        ChoiceZone sourceZone,
        IReadOnlyList<ChoiceCandidate> candidates)
    {
        if (!Enum.IsDefined(type) || type == ChoiceType.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(type), "Choice type must be a known non-placeholder value.");
        }

        if (playerId.IsEmpty)
        {
            throw new ArgumentException("Choice player id must not be empty.", nameof(playerId));
        }

        ArgumentNullException.ThrowIfNull(message);

        if (minCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minCount), "Choice minimum count must not be negative.");
        }

        if (maxCount < minCount)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCount), "Choice maximum count must be greater than or equal to the minimum count.");
        }

        if (!Enum.IsDefined(sourceZone))
        {
            throw new ArgumentOutOfRangeException(nameof(sourceZone), "Choice source zone must be a known value.");
        }

        ArgumentNullException.ThrowIfNull(candidates);

        ChoiceCandidate[] candidateSnapshot = candidates
            .Select(candidate => candidate ?? throw new ArgumentException("Choice candidates must not contain null.", nameof(candidates)))
            .ToArray();

        Type = type;
        PlayerId = playerId;
        Message = message.Trim();
        MinCount = minCount;
        MaxCount = maxCount;
        CanSkip = canSkip;
        SourceZone = sourceZone;
        Candidates = Array.AsReadOnly(candidateSnapshot);
    }

    public ChoiceType Type { get; }

    public HeadlessPlayerId PlayerId { get; }

    public string Message { get; }

    public int MinCount { get; }

    public int MaxCount { get; }

    public bool CanSkip { get; }

    public ChoiceZone SourceZone { get; }

    public IReadOnlyList<ChoiceCandidate> Candidates { get; }

    /// <summary>(P2) An optional COMBINATION gate over the selected SET (AS-IS
    /// <c>SelectPermanentEffect.CanEndSelect</c>'s <c>canEndSelectCondition(permanents)</c> — e.g. "two of
    /// different colours"). Per-candidate legality lives in <see cref="ChoiceCandidate.IsSelectable"/>; this
    /// validates the whole selection at resolve time. A failing set is rejected like any other invalid
    /// result (the choice stays pending; the agent retries) — action masks cannot express set constraints,
    /// so try-reject-retry is the contract. Skips are not validated.</summary>
    public Func<IReadOnlyList<HeadlessEntityId>, bool>? SelectionValidator { get; init; }

    public IReadOnlyList<ChoiceCandidate> SelectableCandidates =>
        Candidates.Where(candidate => candidate.IsSelectable).ToArray();
}
