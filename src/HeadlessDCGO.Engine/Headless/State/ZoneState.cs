namespace HeadlessDCGO.Engine.Headless.State;

using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public readonly record struct ZoneId
{
    public ZoneId(ChoiceZone value)
    {
        if (value is ChoiceZone.None or ChoiceZone.Custom)
        {
            throw new ArgumentException("ZoneId must use a concrete gameplay zone.", nameof(value));
        }

        Value = value;
    }

    public ChoiceZone Value { get; }

    public static ZoneId FromChoiceZone(ChoiceZone zone)
    {
        return new ZoneId(zone);
    }

    public override string ToString()
    {
        return Value.ToString();
    }
}

public enum ZoneVisibility
{
    Public,
    Hidden
}

public sealed record ZoneState
{
    private IReadOnlyList<HeadlessEntityId> _cardIds = Array.Empty<HeadlessEntityId>();

    public ZoneState(
        ZoneId Id,
        ZoneVisibility Visibility,
        IReadOnlyList<HeadlessEntityId>? CardIds = null)
    {
        this.Id = Id;
        this.Visibility = Visibility;
        this.CardIds = CardIds ?? Array.Empty<HeadlessEntityId>();
    }

    public ZoneId Id { get; init; }

    public ZoneVisibility Visibility { get; init; }

    public IReadOnlyList<HeadlessEntityId> CardIds
    {
        get => _cardIds;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            HeadlessEntityId[] snapshot = value.ToArray();
            if (snapshot.Any(id => id.IsEmpty))
            {
                throw new ArgumentException("Zone card ids must not contain empty ids.", nameof(value));
            }

            if (snapshot.Distinct().Count() != snapshot.Length)
            {
                throw new InvalidOperationException("Zone card ids must be unique within a zone.");
            }

            _cardIds = Array.AsReadOnly(snapshot);
        }
    }

    public int Count => CardIds.Count;

    public bool IsEmpty => Count == 0;

    public static ZoneState Create(
        ChoiceZone zone,
        IEnumerable<HeadlessEntityId>? cardIds = null,
        ZoneVisibility? visibility = null)
    {
        return new ZoneState(
            new ZoneId(zone),
            visibility ?? DefaultVisibility(zone),
            (cardIds ?? Array.Empty<HeadlessEntityId>()).ToArray());
    }

    public static ZoneVisibility DefaultVisibility(ChoiceZone zone)
    {
        return zone is ChoiceZone.Library
            or ChoiceZone.Hand
            or ChoiceZone.Security
            or ChoiceZone.DigitamaLibrary
            ? ZoneVisibility.Hidden
            : ZoneVisibility.Public;
    }

    public ZoneState InsertTop(HeadlessEntityId cardId)
    {
        ValidateCardId(cardId);
        EnsureAbsent(cardId);
        return this with { CardIds = new[] { cardId }.Concat(CardIds).ToArray() };
    }

    public ZoneState InsertBottom(HeadlessEntityId cardId)
    {
        ValidateCardId(cardId);
        EnsureAbsent(cardId);
        return this with { CardIds = CardIds.Concat(new[] { cardId }).ToArray() };
    }

    public ZoneState InsertAt(int index, HeadlessEntityId cardId)
    {
        ValidateCardId(cardId);
        EnsureAbsent(cardId);

        if (index < 0 || index > CardIds.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Insert index must be inside the zone bounds.");
        }

        List<HeadlessEntityId> cards = CardIds.ToList();
        cards.Insert(index, cardId);
        return this with { CardIds = cards };
    }

    public ZoneState Remove(HeadlessEntityId cardId)
    {
        ValidateCardId(cardId);
        List<HeadlessEntityId> cards = CardIds.ToList();
        if (!cards.Remove(cardId))
        {
            throw new InvalidOperationException($"Card id '{cardId}' is not in zone '{Id}'.");
        }

        return this with { CardIds = cards };
    }

    public ZoneState MoveToTop(HeadlessEntityId cardId)
    {
        return Remove(cardId).InsertTop(cardId);
    }

    public ZoneState MoveToBottom(HeadlessEntityId cardId)
    {
        return Remove(cardId).InsertBottom(cardId);
    }

    public ZoneState Reveal()
    {
        return this with { Visibility = ZoneVisibility.Public };
    }

    public ZoneState Hide()
    {
        return this with { Visibility = ZoneVisibility.Hidden };
    }

    public ZoneState Shuffle(IRandomSource randomSource)
    {
        ArgumentNullException.ThrowIfNull(randomSource);
        HeadlessEntityId[] cards = CardIds.ToArray();
        randomSource.Shuffle(cards);
        return this with { CardIds = cards };
    }

    public ZoneMoveStateResult MoveCardTo(ZoneState destination, HeadlessEntityId cardId)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (destination.Id == Id)
        {
            throw new InvalidOperationException("Source and destination zones must be different.");
        }

        return new ZoneMoveStateResult(
            Source: Remove(cardId),
            Destination: destination.InsertBottom(cardId));
    }

    public ZoneStateView ToView(bool isOwnerView)
    {
        bool isHidden = Visibility == ZoneVisibility.Hidden && !isOwnerView;
        return new ZoneStateView(
            Id,
            Visibility,
            Count,
            isHidden ? Array.Empty<HeadlessEntityId>() : CardIds,
            isHidden);
    }

    public string FingerprintSegment()
    {
        return $"{Id}:{Visibility}:{string.Join(",", CardIds.Select(id => id.Value))}";
    }

    private void EnsureAbsent(HeadlessEntityId cardId)
    {
        if (CardIds.Contains(cardId))
        {
            throw new InvalidOperationException($"Card id '{cardId}' is already in zone '{Id}'.");
        }
    }

    private static void ValidateCardId(HeadlessEntityId cardId)
    {
        if (cardId.IsEmpty)
        {
            throw new ArgumentException("Card id must not be empty.", nameof(cardId));
        }
    }
}

public sealed record ZoneStateView(
    ZoneId Id,
    ZoneVisibility Visibility,
    int Count,
    IReadOnlyList<HeadlessEntityId> CardIds,
    bool IsHidden);

public sealed record ZoneMoveStateResult(
    ZoneState Source,
    ZoneState Destination);
