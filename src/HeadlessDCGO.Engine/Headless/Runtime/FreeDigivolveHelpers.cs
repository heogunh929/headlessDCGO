namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (D-6 Blast / Arts Digivolve) A costless single-target digivolve. Both AS-IS keywords digivolve a card
/// onto a Digimon with <c>payCost: false</c>: Blast Digivolve is a triggered (opponent-turn) reaction,
/// Arts Digivolve is an option that selects a target — the digivolve mechanic is identical, only the cost
/// is skipped and the trigger/selection differs (authored at porting time).
///
/// This reuses the fusion primitive (<see cref="FusionDigivolveHelpers.FuseAsync"/>) with a single
/// material — which produces the same source ordering as a normal digivolve (target, then its sources,
/// then the card's) — but, unlike a Jogress, the result INHERITS the target's summoning-sickness state
/// (a normal digivolve keeps the same permanent's field time). No memory is paid.
/// </summary>
public static class FreeDigivolveHelpers
{
    public const string EnteredThisTurnKey = "enteredThisTurn";

    /// <summary>
    /// Digivolve <paramref name="cardId"/> (from <paramref name="fromZone"/>, default Hand) onto
    /// <paramref name="targetCardId"/> for free. Returns true when performed.
    /// </summary>
    public static async Task<bool> DigivolveFreeAsync(
        ICardInstanceRepository repository,
        IZoneMover zoneMover,
        HeadlessEntityId cardId,
        HeadlessEntityId targetCardId,
        ChoiceZone fromZone = ChoiceZone.Hand,
        GameEventQueue? gameEventQueue = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(zoneMover);

        if (!repository.TryGetInstance(targetCardId, out CardInstanceRecord? target) || target is null ||
            !repository.TryGetInstance(cardId, out CardInstanceRecord? _))
        {
            return false;
        }

        // A normal digivolve keeps the permanent's field time: inherit the target's entered-this-turn.
        bool inheritedSick = ReadFlag(target.Metadata, EnteredThisTurnKey);

        IReadOnlyList<HeadlessEntityId> merged = await FusionDigivolveHelpers.FuseAsync(
            repository,
            zoneMover,
            cardId,
            fromZone,
            new[] { targetCardId },
            materialFromZone: ChoiceZone.BattleArea,
            gameEventQueue: gameEventQueue,
            enteredThisTurnOverride: inheritedSick,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return merged.Count > 0;
    }

    private static bool ReadFlag(IReadOnlyDictionary<string, object?> metadata, string key) =>
        metadata.TryGetValue(key, out object? raw) && raw is bool b && b;
}
