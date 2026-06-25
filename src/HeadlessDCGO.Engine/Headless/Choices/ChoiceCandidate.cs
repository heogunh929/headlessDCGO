namespace HeadlessDCGO.Engine.Headless.Choices;

using HeadlessDCGO.Engine.Headless.Services;

public sealed record ChoiceCandidate
{
    public ChoiceCandidate(
        HeadlessEntityId id,
        string label,
        ChoiceZone zone,
        bool IsSelectable,
        HeadlessPlayerId? ownerId = null)
    {
        if (id.IsEmpty)
        {
            throw new ArgumentException("Choice candidate id must not be empty.", nameof(id));
        }

        ArgumentNullException.ThrowIfNull(label);

        if (!Enum.IsDefined(zone) || zone == ChoiceZone.None)
        {
            throw new ArgumentOutOfRangeException(nameof(zone), "Choice candidate zone must be a concrete zone.");
        }

        Id = id;
        Label = label.Trim();
        Zone = zone;
        this.IsSelectable = IsSelectable;
        OwnerId = ownerId;
    }

    public HeadlessEntityId Id { get; }

    public string Label { get; }

    public ChoiceZone Zone { get; }

    public bool IsSelectable { get; }

    public HeadlessPlayerId? OwnerId { get; }
}
