namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>Where a revealed card is sent after the reveal-and-select decision.</summary>
public enum RevealDestination
{
    Hand = 0,
    DeckTop = 1,
    DeckBottom = 2,
    Trash = 3,
}

/// <summary>
/// (B-7) Reveal the top N library cards and let the controller SELECT some of them (AS-IS
/// <c>RevealLibrary.RevealDeckTopCardsAndSelect</c>): the selected cards go to one destination (e.g. the
/// hand), the remaining revealed cards to another (e.g. the deck bottom). The reveal only peeks the library
/// top; the cards move when the choice resolves. The selection is an agent choice (rules-faithful). The two
/// destinations are encoded in the choice request id so <see cref="ResolveChoice"/> can route both sets.
/// </summary>
public static class RevealAndSelect
{
    public const string RequestIdPrefix = "reveal-select";
    private const char Delimiter = ':';

    /// <summary>Reveals the top <paramref name="revealCount"/> library cards and opens the selection choice
    /// (up to <paramref name="maxSelect"/>). Returns true when a choice opened (false if the library is empty).</summary>
    public static bool RequestChoice(
        EngineContext context,
        HeadlessPlayerId player,
        int revealCount,
        int maxSelect,
        RevealDestination selectedTo,
        RevealDestination remainingTo)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.ChoiceController.Current.IsPending ||
            context.ZoneMover is not IZoneStateReader zones ||
            player.IsEmpty)
        {
            return false;
        }

        HeadlessEntityId[] revealed = zones.GetCards(player, ChoiceZone.Library).Take(Math.Max(0, revealCount)).ToArray();
        if (revealed.Length == 0)
        {
            return false;
        }

        ChoiceCandidate[] candidates = revealed
            .Select(id => new ChoiceCandidate(id, $"Reveal {id.Value}", ChoiceZone.Library, IsSelectable: true, ownerId: player))
            .ToArray();

        int max = Math.Clamp(maxSelect, 0, revealed.Length);
        var request = new ChoiceRequest(
            ChoiceType.RevealSelect,
            player,
            $"Reveal {revealed.Length}: select up to {max} card(s).",
            minCount: 0,
            maxCount: Math.Max(1, max),
            canSkip: true,
            ChoiceZone.Library,
            candidates);
        context.ChoiceController.RequestChoice(
            request,
            new HeadlessEntityId($"{RequestIdPrefix}{Delimiter}{(int)selectedTo}{Delimiter}{(int)remainingTo}"));
        return true;
    }

    /// <summary>Resolves the reveal-and-select choice: the selected revealed cards go to the selected
    /// destination, the remaining revealed cards to the remaining destination.</summary>
    public static async Task<bool> ResolveChoice(EngineContext context, ChoiceResult result)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(result);
        if (!context.ChoiceController.Current.IsPending ||
            context.ChoiceController.PendingRequest is not ChoiceRequest request ||
            request.Type != ChoiceType.RevealSelect)
        {
            return false;
        }

        HeadlessPlayerId player = request.PlayerId;
        var revealed = request.Candidates.Select(candidate => candidate.Id).ToList();
        (RevealDestination selectedTo, RevealDestination remainingTo) = ParseDestinations(context.ChoiceController.Current.RequestId ?? default);

        try
        {
            context.ChoiceController.ResolveChoice(result);
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        var selected = result.IsSkipped ? new List<HeadlessEntityId>() : result.SelectedIds.ToList();
        foreach (HeadlessEntityId cardId in revealed)
        {
            RevealDestination destination = selected.Contains(cardId) ? selectedTo : remainingTo;
            await MoveAsync(context, player, cardId, destination).ConfigureAwait(false);
        }

        return true;
    }

    private static Task MoveAsync(EngineContext context, HeadlessPlayerId player, HeadlessEntityId cardId, RevealDestination destination) =>
        destination switch
        {
            RevealDestination.Hand => context.ZoneMover.AddToHandAsync(player, cardId),
            RevealDestination.DeckTop => context.ZoneMover.MoveToDeckTopAsync(player, cardId),
            RevealDestination.DeckBottom => context.ZoneMover.MoveToDeckBottomAsync(player, cardId),
            RevealDestination.Trash => context.ZoneMover.AddToTrashAsync(player, cardId),
            _ => context.ZoneMover.MoveToDeckBottomAsync(player, cardId),
        };

    private static (RevealDestination Selected, RevealDestination Remaining) ParseDestinations(HeadlessEntityId requestId)
    {
        string[] parts = requestId.Value.Split(Delimiter);
        // {prefix}:{selected}:{remaining}
        if (parts.Length == 3 &&
            int.TryParse(parts[1], out int selected) &&
            int.TryParse(parts[2], out int remaining))
        {
            return ((RevealDestination)selected, (RevealDestination)remaining);
        }

        return (RevealDestination.Hand, RevealDestination.DeckBottom);
    }
}
