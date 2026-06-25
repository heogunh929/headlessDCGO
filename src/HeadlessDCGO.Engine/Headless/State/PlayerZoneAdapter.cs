namespace HeadlessDCGO.Engine.Headless.State;

using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class PlayerZoneAdapter
{
    private static readonly ChoiceZone[] OwnedZoneOrder =
    {
        ChoiceZone.Library,
        ChoiceZone.Hand,
        ChoiceZone.BattleArea,
        ChoiceZone.BreedingArea,
        ChoiceZone.Trash,
        ChoiceZone.Security,
        ChoiceZone.DigitamaLibrary
    };

    public PlayerZoneAdapter(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        ValidateZoneOwnership(state);
        State = state;
    }

    public MatchState State { get; }

    public IReadOnlyList<ChoiceZone> OwnedZones => OwnedZoneOrder;

    public PlayerZoneOwnershipSnapshot ReadPlayer(HeadlessPlayerId playerId)
    {
        PlayerState player = State.GetPlayer(playerId);
        PlayerZoneOwnership[] zones = OwnedZoneOrder
            .Select(zone => new PlayerZoneOwnership(
                player.PlayerId,
                zone,
                player.GetZone(zone).ToArray()))
            .ToArray();

        return new PlayerZoneOwnershipSnapshot(
            player.PlayerId,
            player.Memory,
            zones,
            LibraryCount: player.GetZone(ChoiceZone.Library).Count,
            HandCount: player.GetZone(ChoiceZone.Hand).Count,
            BattleAreaCount: player.GetZone(ChoiceZone.BattleArea).Count,
            BreedingAreaCount: player.GetZone(ChoiceZone.BreedingArea).Count,
            TrashCount: player.GetZone(ChoiceZone.Trash).Count,
            SecurityCount: player.GetZone(ChoiceZone.Security).Count,
            DigitamaLibraryCount: player.GetZone(ChoiceZone.DigitamaLibrary).Count);
    }

    public IReadOnlyList<PlayerZoneOwnershipSnapshot> ReadAllPlayers()
    {
        return State.Players
            .OrderBy(player => player.PlayerId.Value)
            .Select(player => ReadPlayer(player.PlayerId))
            .ToArray();
    }

    public PlayerZoneOwnership GetZone(HeadlessPlayerId playerId, ChoiceZone zone)
    {
        ValidateOwnedZone(zone);
        return ReadPlayer(playerId).Zone(zone);
    }

    public IReadOnlyList<PlayerZoneOwnership> GetZones(HeadlessPlayerId playerId)
    {
        return ReadPlayer(playerId).Zones;
    }

    public PlayerZoneCardLocation LocateCard(HeadlessEntityId cardId)
    {
        return TryLocateCard(cardId, out PlayerZoneCardLocation? location)
            ? location!
            : throw new InvalidOperationException($"Card id '{cardId}' is not in a player zone.");
    }

    public bool TryLocateCard(
        HeadlessEntityId cardId,
        out PlayerZoneCardLocation? location)
    {
        if (cardId.IsEmpty)
        {
            throw new ArgumentException("Card id must not be empty.", nameof(cardId));
        }

        CardInstanceState instance = State.GetCardInstance(cardId);
        foreach (PlayerState player in State.Players)
        {
            foreach (ChoiceZone zone in OwnedZoneOrder)
            {
                IReadOnlyList<HeadlessEntityId> cards = player.GetZone(zone);
                int index = FindIndex(cards, cardId);
                if (index < 0)
                {
                    continue;
                }

                location = new PlayerZoneCardLocation(
                    cardId,
                    player.PlayerId,
                    instance.OwnerId,
                    zone,
                    index);
                return true;
            }
        }

        location = null;
        return false;
    }

    public bool WouldDeckOutOnDraw(HeadlessPlayerId playerId, int drawCount)
    {
        if (drawCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(drawCount), "Draw count must not be negative.");
        }

        return ReadPlayer(playerId).LibraryCount < drawCount;
    }

    public PlayerZoneAdapter ApplyPlayerMutation(ZoneMoveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ToZone == ChoiceZone.None)
        {
            throw new ArgumentException("Player zone mutations require a destination zone.", nameof(request));
        }

        ValidateOwnedZone(request.ToZone);
        if (request.FromZone != ChoiceZone.None)
        {
            ValidateOwnedZone(request.FromZone);
        }

        CardInstanceState instance = State.GetCardInstance(request.CardId);
        if (instance.OwnerId != request.PlayerId)
        {
            throw new InvalidOperationException(
                $"Card id '{request.CardId}' is owned by player '{instance.OwnerId}', not player '{request.PlayerId}'.");
        }

        MatchState moved = State.MoveCard(request);
        return new PlayerZoneAdapter(moved);
    }

    public PlayerZoneAdapter PlaceOwnedCard(HeadlessEntityId cardId, ChoiceZone zone)
    {
        ValidateOwnedZone(zone);
        CardInstanceState instance = State.GetCardInstance(cardId);
        return ApplyPlayerMutation(new ZoneMoveRequest(instance.OwnerId, cardId, ChoiceZone.None, zone));
    }

    private static void ValidateZoneOwnership(MatchState state)
    {
        HashSet<HeadlessEntityId> seen = new();
        foreach (PlayerState player in state.Players)
        {
            foreach (KeyValuePair<ChoiceZone, IReadOnlyList<HeadlessEntityId>> zone in player.Zones)
            {
                ValidateOwnedZone(zone.Key);
                foreach (HeadlessEntityId cardId in zone.Value)
                {
                    if (!seen.Add(cardId))
                    {
                        throw new InvalidOperationException($"Card id '{cardId}' appears in more than one player zone.");
                    }

                    CardInstanceState instance = state.GetCardInstance(cardId);
                    if (instance.OwnerId != player.PlayerId)
                    {
                        throw new InvalidOperationException(
                            $"Card id '{cardId}' is owned by player '{instance.OwnerId}', but is in player '{player.PlayerId}' zone '{zone.Key}'.");
                    }
                }
            }
        }
    }

    private static int FindIndex(IReadOnlyList<HeadlessEntityId> cards, HeadlessEntityId cardId)
    {
        for (int index = 0; index < cards.Count; index++)
        {
            if (cards[index] == cardId)
            {
                return index;
            }
        }

        return -1;
    }

    private static void ValidateOwnedZone(ChoiceZone zone)
    {
        if (!OwnedZoneOrder.Contains(zone))
        {
            throw new ArgumentException($"Zone '{zone}' is not a player-owned zone.", nameof(zone));
        }
    }
}

public sealed record PlayerZoneOwnershipSnapshot(
    HeadlessPlayerId PlayerId,
    int Memory,
    IReadOnlyList<PlayerZoneOwnership> Zones,
    int LibraryCount,
    int HandCount,
    int BattleAreaCount,
    int BreedingAreaCount,
    int TrashCount,
    int SecurityCount,
    int DigitamaLibraryCount)
{
    public bool IsDeckEmpty => LibraryCount == 0;

    public int FieldCount => BattleAreaCount + BreedingAreaCount;

    public PlayerZoneOwnership Zone(ChoiceZone zone)
    {
        return Zones.FirstOrDefault(snapshot => snapshot.Zone == zone)
            ?? throw new InvalidOperationException($"Zone '{zone}' is not in the player zone snapshot.");
    }
}

public sealed record PlayerZoneOwnership(
    HeadlessPlayerId OwnerId,
    ChoiceZone Zone,
    IReadOnlyList<HeadlessEntityId> CardIds)
{
    public int Count => CardIds.Count;
}

public sealed record PlayerZoneCardLocation(
    HeadlessEntityId CardId,
    HeadlessPlayerId ZoneOwnerId,
    HeadlessPlayerId CardOwnerId,
    ChoiceZone Zone,
    int Index)
{
    public bool IsInOwnerZone => ZoneOwnerId == CardOwnerId;
}
