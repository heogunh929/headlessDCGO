namespace HeadlessDCGO.Engine.Headless.State;

using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class CardIdentityAdapter
{
    public const string TokenFlagKey = "isToken";

    public CardIdentityAdapter(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        State = new PlayerZoneAdapter(state).State;
    }

    public MatchState State { get; }

    public CardIdentitySnapshot Bind(HeadlessEntityId cardId)
    {
        CardInstanceState instance = State.GetCardInstance(cardId);
        PlayerZoneAdapter zones = new(State);
        PlayerZoneCardLocation? location = zones.TryLocateCard(cardId, out PlayerZoneCardLocation? found)
            ? found
            : null;

        return new CardIdentitySnapshot(
            instance.InstanceId,
            instance.DefinitionId,
            instance.OwnerId,
            instance.IsSuspended,
            instance.IsFaceUp,
            instance.HasFlag(TokenFlagKey),
            instance.SourceIds.ToArray(),
            location?.ZoneOwnerId,
            location?.Zone,
            location?.Index);
    }

    public bool TryBind(HeadlessEntityId cardId, out CardIdentitySnapshot? snapshot)
    {
        if (cardId.IsEmpty)
        {
            throw new ArgumentException("Card id must not be empty.", nameof(cardId));
        }

        if (!State.CardInstances.ContainsKey(cardId))
        {
            snapshot = null;
            return false;
        }

        snapshot = Bind(cardId);
        return true;
    }

    public CardIdentityAdapter CreateCard(
        HeadlessEntityId instanceId,
        HeadlessEntityId definitionId,
        HeadlessPlayerId ownerId,
        ChoiceZone initialZone = ChoiceZone.None,
        bool isToken = false,
        bool faceUp = false)
    {
        if (State.CardInstances.ContainsKey(instanceId))
        {
            throw new InvalidOperationException($"Card instance id '{instanceId}' is already bound.");
        }

        CardInstanceState instance = new(instanceId, definitionId, ownerId, IsFaceUp: faceUp);
        if (isToken)
        {
            instance = instance.SetFlag(TokenFlagKey, true);
        }

        CardIdentityAdapter adapter = new(State.WithCardInstance(instance));
        return initialZone == ChoiceZone.None
            ? adapter
            : adapter.PlaceCard(instanceId, initialZone);
    }

    public CardIdentityAdapter PlaceCard(HeadlessEntityId cardId, ChoiceZone zone)
    {
        PlayerZoneAdapter zones = new(State);
        return new CardIdentityAdapter(zones.PlaceOwnedCard(cardId, zone).State);
    }

    public CardIdentityAdapter MoveCard(ZoneMoveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        PlayerZoneAdapter zones = new(State);
        CardIdentityAdapter moved = new(zones.ApplyPlayerMutation(request).State);
        return request.FaceUp ? moved.Reveal(request.CardId) : moved;
    }

    public CardIdentityAdapter Reveal(HeadlessEntityId cardId)
    {
        return ReplaceInstance(State.GetCardInstance(cardId).Reveal());
    }

    public CardIdentityAdapter Hide(HeadlessEntityId cardId)
    {
        return ReplaceInstance(State.GetCardInstance(cardId).Hide());
    }

    public CardIdentityAdapter Suspend(HeadlessEntityId cardId)
    {
        return ReplaceInstance(State.GetCardInstance(cardId).Suspend());
    }

    public CardIdentityAdapter Unsuspend(HeadlessEntityId cardId)
    {
        return ReplaceInstance(State.GetCardInstance(cardId).Unsuspend());
    }

    public CardIdentityAdapter AttachSource(HeadlessEntityId cardId, HeadlessEntityId sourceId)
    {
        CardInstanceState card = State.GetCardInstance(cardId);
        CardInstanceState source = State.GetCardInstance(sourceId);
        if (card.InstanceId == source.InstanceId)
        {
            throw new InvalidOperationException("A card cannot be attached as its own source.");
        }

        if (card.OwnerId != source.OwnerId)
        {
            throw new InvalidOperationException(
                $"Source card '{sourceId}' is owned by player '{source.OwnerId}', not player '{card.OwnerId}'.");
        }

        return ReplaceInstance(card.AttachSource(sourceId));
    }

    public CardIdentityAdapter DetachSource(HeadlessEntityId cardId, HeadlessEntityId sourceId)
    {
        return ReplaceInstance(State.GetCardInstance(cardId).DetachSource(sourceId));
    }

    public CardIdentityAdapter ClearSources(HeadlessEntityId cardId)
    {
        return ReplaceInstance(State.GetCardInstance(cardId).ClearSources());
    }

    public CardInstanceRecord ToRecord(HeadlessEntityId cardId)
    {
        CardIdentitySnapshot snapshot = Bind(cardId);
        Dictionary<string, object?> metadata = new(StringComparer.Ordinal)
        {
            ["isSuspended"] = snapshot.IsSuspended,
            ["isFaceUp"] = snapshot.IsFaceUp,
            ["sourceCount"] = snapshot.SourceIds.Count
        };

        if (snapshot.ZoneOwnerId is not null)
        {
            metadata["zoneOwnerId"] = snapshot.ZoneOwnerId.Value.Value;
            metadata["zone"] = snapshot.Zone!.Value.ToString();
            metadata["zoneIndex"] = snapshot.ZoneIndex!.Value;
        }

        return new CardInstanceRecord(
            snapshot.InstanceId,
            snapshot.DefinitionId,
            snapshot.OwnerId,
            snapshot.IsToken,
            metadata);
    }

    public void UpsertRecord(ICardInstanceRepository repository, HeadlessEntityId cardId)
    {
        ArgumentNullException.ThrowIfNull(repository);
        repository.Upsert(ToRecord(cardId));
    }

    private CardIdentityAdapter ReplaceInstance(CardInstanceState instance)
    {
        return new CardIdentityAdapter(State.WithCardInstance(instance));
    }
}

public sealed record CardIdentitySnapshot(
    HeadlessEntityId InstanceId,
    HeadlessEntityId DefinitionId,
    HeadlessPlayerId OwnerId,
    bool IsSuspended,
    bool IsFaceUp,
    bool IsToken,
    IReadOnlyList<HeadlessEntityId> SourceIds,
    HeadlessPlayerId? ZoneOwnerId,
    ChoiceZone? Zone,
    int? ZoneIndex)
{
    public bool IsLocated => ZoneOwnerId is not null && Zone is not null && ZoneIndex is not null;

    public bool IsInOwnerZone => IsLocated && ZoneOwnerId == OwnerId;
}
