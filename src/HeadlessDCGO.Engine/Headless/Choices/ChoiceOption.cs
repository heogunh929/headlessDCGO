namespace HeadlessDCGO.Engine.Headless.Choices;

using HeadlessDCGO.Engine.Headless.Services;

public sealed record ChoiceOption
{
    public ChoiceOption(
        HeadlessEntityId id,
        string label,
        ChoiceZone zone)
    {
        if (id.IsEmpty)
        {
            throw new ArgumentException("Choice option id must not be empty.", nameof(id));
        }

        ArgumentNullException.ThrowIfNull(label);

        if (!Enum.IsDefined(zone) || zone == ChoiceZone.None)
        {
            throw new ArgumentOutOfRangeException(nameof(zone), "Choice option zone must be a concrete zone.");
        }

        Id = id;
        Label = label.Trim();
        Zone = zone;
    }

    public HeadlessEntityId Id { get; }

    public string Label { get; }

    public ChoiceZone Zone { get; }

    public static ChoiceOption FromCandidate(ChoiceCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return new ChoiceOption(candidate.Id, candidate.Label, candidate.Zone);
    }
}
