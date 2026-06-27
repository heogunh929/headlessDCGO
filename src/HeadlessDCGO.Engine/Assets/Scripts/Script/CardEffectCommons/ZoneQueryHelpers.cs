namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using HeadlessDCGO.Engine.Headless.Effects;

using System.Collections.ObjectModel;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

public sealed record ZoneQueryRequest
{
    public ZoneQueryRequest(
        MatchState matchState,
        HeadlessPlayerId playerId,
        HeadlessPlayerId viewerId,
        ChoiceZone zone,
        HeadlessEntityId? rootCardId = null,
        bool includeHidden = false)
    {
        ArgumentNullException.ThrowIfNull(matchState);
        if (playerId.IsEmpty)
        {
            throw new ArgumentException("Player id must not be empty.", nameof(playerId));
        }

        if (viewerId.IsEmpty)
        {
            throw new ArgumentException("Viewer id must not be empty.", nameof(viewerId));
        }

        if (!Enum.IsDefined(zone))
        {
            throw new ArgumentOutOfRangeException(nameof(zone), "Zone must be known.");
        }

        if (rootCardId.HasValue && rootCardId.Value.IsEmpty)
        {
            throw new ArgumentException("Root card id must not be empty.", nameof(rootCardId));
        }

        MatchState = matchState;
        PlayerId = playerId;
        ViewerId = viewerId;
        Zone = zone;
        RootCardId = rootCardId;
        IncludeHidden = includeHidden;
    }

    public MatchState MatchState { get; }

    public HeadlessPlayerId PlayerId { get; }

    public HeadlessPlayerId ViewerId { get; }

    public ChoiceZone Zone { get; }

    public HeadlessEntityId? RootCardId { get; }

    public bool IncludeHidden { get; }
}

public sealed record ZoneQueryCard
{
    public ZoneQueryCard(
        HeadlessEntityId cardId,
        HeadlessEntityId? definitionId,
        HeadlessPlayerId ownerId,
        ChoiceZone zone,
        int index,
        bool isVisible,
        bool isFaceUp,
        bool isSuspended)
    {
        if (cardId.IsEmpty)
        {
            throw new ArgumentException("Card id must not be empty.", nameof(cardId));
        }

        if (definitionId.HasValue && definitionId.Value.IsEmpty)
        {
            throw new ArgumentException("Definition id must not be empty.", nameof(definitionId));
        }

        if (ownerId.IsEmpty)
        {
            throw new ArgumentException("Owner id must not be empty.", nameof(ownerId));
        }

        if (!Enum.IsDefined(zone) || zone is ChoiceZone.None or ChoiceZone.Custom)
        {
            throw new ArgumentException("Zone query card must use a concrete zone.", nameof(zone));
        }

        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Zone index must not be negative.");
        }

        CardId = cardId;
        DefinitionId = definitionId;
        OwnerId = ownerId;
        Zone = zone;
        Index = index;
        IsVisible = isVisible;
        IsFaceUp = isFaceUp;
        IsSuspended = isSuspended;
    }

    public HeadlessEntityId CardId { get; }

    public HeadlessEntityId? DefinitionId { get; }

    public HeadlessPlayerId OwnerId { get; }

    public ChoiceZone Zone { get; }

    public int Index { get; }

    public bool IsVisible { get; }

    public bool IsFaceUp { get; }

    public bool IsSuspended { get; }
}

public sealed record ZoneQueryResult
{
    private ZoneQueryResult(
        bool isSuccess,
        string reason,
        IReadOnlyList<ZoneQueryCard> cards,
        IReadOnlyDictionary<string, object?> values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(values);

        IsSuccess = isSuccess;
        Reason = reason.Trim();
        Cards = Array.AsReadOnly(cards.ToArray());
        Values = CopyValues(values);
    }

    public bool IsSuccess { get; }

    public string Reason { get; }

    public IReadOnlyList<ZoneQueryCard> Cards { get; }

    public IReadOnlyDictionary<string, object?> Values { get; }

    public static ZoneQueryResult Success(
        IReadOnlyList<ZoneQueryCard> cards,
        string reason,
        IReadOnlyDictionary<string, object?> values)
    {
        return new ZoneQueryResult(true, reason, cards, values);
    }

    public static ZoneQueryResult Failure(
        string reason,
        IReadOnlyDictionary<string, object?> values)
    {
        return new ZoneQueryResult(false, reason, Array.Empty<ZoneQueryCard>(), values);
    }

    private static IReadOnlyDictionary<string, object?> CopyValues(IReadOnlyDictionary<string, object?> values)
    {
        Dictionary<string, object?> copy = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, object?> pair in values.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pair.Key);
            copy[pair.Key.Trim()] = pair.Value;
        }

        return new ReadOnlyDictionary<string, object?>(copy);
    }
}

public static class ZoneQueryHelpers
{
    public static ZoneQueryResult Query(ZoneQueryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Zone is ChoiceZone.None or ChoiceZone.Custom)
        {
            return ZoneQueryResult.Failure(
                $"Zone '{request.Zone}' is not a concrete queryable gameplay zone.",
                BaseValues(request));
        }

        if (!HasPlayer(request.MatchState, request.PlayerId))
        {
            return ZoneQueryResult.Failure(
                $"Player '{request.PlayerId}' was not found.",
                BaseValues(request));
        }

        if (!HasPlayer(request.MatchState, request.ViewerId))
        {
            return ZoneQueryResult.Failure(
                $"Viewer player '{request.ViewerId}' was not found.",
                BaseValues(request));
        }

        IReadOnlyList<HeadlessEntityId> cardIds;
        if (request.Zone == ChoiceZone.DigivolutionCards)
        {
            if (!request.RootCardId.HasValue)
            {
                return ZoneQueryResult.Failure(
                    "DigivolutionCards query requires a root card id.",
                    BaseValues(request));
            }

            if (!request.MatchState.CardInstances.TryGetValue(request.RootCardId.Value, out CardInstanceState? root))
            {
                return ZoneQueryResult.Failure(
                    $"Root card '{request.RootCardId.Value}' was not found.",
                    BaseValues(request));
            }

            if (root.OwnerId != request.PlayerId)
            {
                return ZoneQueryResult.Failure(
                    $"Root card '{request.RootCardId.Value}' is owned by player '{root.OwnerId}', not player '{request.PlayerId}'.",
                    BaseValues(request));
            }

            cardIds = root.SourceIds;
        }
        else
        {
            cardIds = request.MatchState.GetPlayer(request.PlayerId).GetZone(request.Zone);
        }

        List<ZoneQueryCard> cards = new();
        for (int index = 0; index < cardIds.Count; index++)
        {
            HeadlessEntityId cardId = cardIds[index];
            if (!request.MatchState.CardInstances.TryGetValue(cardId, out CardInstanceState? instance))
            {
                Dictionary<string, object?> failureValues = BaseValues(request);
                failureValues["missingCardId"] = cardId.Value;
                failureValues["zoneIndex"] = index;
                return ZoneQueryResult.Failure(
                    $"Zone '{request.Zone}' references missing card '{cardId}'.",
                    failureValues);
            }

            bool isVisible = IsVisible(instance, request.Zone, request.ViewerId, request.IncludeHidden);
            cards.Add(new ZoneQueryCard(
                instance.InstanceId,
                isVisible ? instance.DefinitionId : null,
                instance.OwnerId,
                request.Zone,
                index,
                isVisible,
                instance.IsFaceUp,
                instance.IsSuspended));
        }

        Dictionary<string, object?> values = BaseValues(request);
        values["cardCount"] = cards.Count;
        values["cardIds"] = cards.Select(card => card.CardId.Value).ToArray();
        values["visibleCardCount"] = cards.Count(card => card.IsVisible);
        values["visibleCardIds"] = cards.Where(card => card.IsVisible).Select(card => card.CardId.Value).ToArray();

        return ZoneQueryResult.Success(cards, "Zone cards resolved.", values);
    }

    public static ZoneQueryResult Library(
        MatchState state,
        HeadlessPlayerId playerId,
        HeadlessPlayerId viewerId,
        bool includeHidden = false)
    {
        return Query(new ZoneQueryRequest(state, playerId, viewerId, ChoiceZone.Library, includeHidden: includeHidden));
    }

    public static ZoneQueryResult Trash(
        MatchState state,
        HeadlessPlayerId playerId,
        HeadlessPlayerId viewerId,
        bool includeHidden = false)
    {
        return Query(new ZoneQueryRequest(state, playerId, viewerId, ChoiceZone.Trash, includeHidden: includeHidden));
    }

    public static ZoneQueryResult Security(
        MatchState state,
        HeadlessPlayerId playerId,
        HeadlessPlayerId viewerId,
        bool includeHidden = false)
    {
        return Query(new ZoneQueryRequest(state, playerId, viewerId, ChoiceZone.Security, includeHidden: includeHidden));
    }

    public static ZoneQueryResult Sources(
        MatchState state,
        HeadlessEntityId rootCardId,
        HeadlessPlayerId viewerId,
        bool includeHidden = false)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (rootCardId.IsEmpty)
        {
            throw new ArgumentException("Root card id must not be empty.", nameof(rootCardId));
        }

        if (!state.CardInstances.TryGetValue(rootCardId, out CardInstanceState? root))
        {
            return ZoneQueryResult.Failure(
                $"Root card '{rootCardId}' was not found.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["playerId"] = 0,
                    ["viewerId"] = viewerId.Value,
                    ["zone"] = ChoiceZone.DigivolutionCards.ToString(),
                    ["rootCardId"] = rootCardId.Value,
                    ["includeHidden"] = includeHidden,
                });
        }

        return Query(new ZoneQueryRequest(
            state,
            root.OwnerId,
            viewerId,
            ChoiceZone.DigivolutionCards,
            rootCardId,
            includeHidden));
    }

    private static bool IsVisible(
        CardInstanceState instance,
        ChoiceZone zone,
        HeadlessPlayerId viewerId,
        bool includeHidden)
    {
        return includeHidden ||
            instance.OwnerId == viewerId ||
            instance.IsFaceUp ||
            ZoneState.DefaultVisibility(zone) == ZoneVisibility.Public;
    }

    private static bool HasPlayer(MatchState state, HeadlessPlayerId playerId)
    {
        return state.Players.Any(player => player.PlayerId == playerId);
    }

    private static Dictionary<string, object?> BaseValues(ZoneQueryRequest request)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["playerId"] = request.PlayerId.Value,
            ["viewerId"] = request.ViewerId.Value,
            ["zone"] = request.Zone.ToString(),
            ["rootCardId"] = request.RootCardId?.Value,
            ["includeHidden"] = request.IncludeHidden,
        };
    }
}
