namespace HeadlessDCGO.Engine.Headless.Services;

using HeadlessDCGO.Engine.Headless.Choices;

public sealed record ZoneMoveRequest
{
    public ZoneMoveRequest(
        HeadlessPlayerId PlayerId,
        HeadlessEntityId CardId,
        ChoiceZone FromZone,
        ChoiceZone ToZone,
        bool FaceUp = false,
        IReadOnlyDictionary<string, object?>? Metadata = null)
    {
        if (PlayerId.IsEmpty)
        {
            throw new ArgumentException("Player id must not be empty.", nameof(PlayerId));
        }

        if (CardId.IsEmpty)
        {
            throw new ArgumentException("Card id must not be empty.", nameof(CardId));
        }

        if (FromZone == ChoiceZone.Custom)
        {
            throw new ArgumentException("From zone must not be Custom.", nameof(FromZone));
        }

        if (ToZone == ChoiceZone.Custom)
        {
            throw new ArgumentException("To zone must not be Custom.", nameof(ToZone));
        }

        if (FromZone == ChoiceZone.None && ToZone == ChoiceZone.None)
        {
            throw new ArgumentException("At least one zone must be concrete.", nameof(ToZone));
        }

        if (FromZone == ToZone)
        {
            throw new ArgumentException("From zone and to zone must be different.", nameof(ToZone));
        }

        this.PlayerId = PlayerId;
        this.CardId = CardId;
        this.FromZone = FromZone;
        this.ToZone = ToZone;
        this.FaceUp = FaceUp;
        this.Metadata = Metadata;
    }

    public HeadlessPlayerId PlayerId { get; }

    public HeadlessEntityId CardId { get; }

    public ChoiceZone FromZone { get; }

    public ChoiceZone ToZone { get; }

    public bool FaceUp { get; }

    /// <summary>(G3) Optional extra metadata merged into the emitted CardMoved event (e.g. a one-shot
    /// <c>suppressOnPlay</c> marker so an ETB-suppressed play skips the moved card's own OnPlay/OnEnterField
    /// triggers). Null for the overwhelming majority of moves.</summary>
    public IReadOnlyDictionary<string, object?>? Metadata { get; }
}
