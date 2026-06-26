namespace HeadlessDCGO.Engine.Headless.Runtime;

using System.Collections.ObjectModel;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public sealed record GameEvent
{
    private string _message = string.Empty;
    private IReadOnlyDictionary<string, object?> _metadata = ReadOnlyDictionary<string, object?>.Empty;

    public GameEvent(
        long Sequence,
        GameEventType Type,
        string Message,
        IReadOnlyDictionary<string, object?> Metadata)
    {
        if (Sequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Sequence), "Sequence must not be negative.");
        }

        this.Sequence = Sequence;
        this.Type = Type;
        this.Message = Message;
        this.Metadata = Metadata;
    }

    public long Sequence { get; init; }

    public GameEventType Type { get; init; }

    // G3.5-RL-B2: structured "who did what to what, and why" fields. Optional and additive — events
    // that predate the schema leave them null. Populated at the semantic emission points (zone moves,
    // attacks, terminal) for trigger collection, replay, and reward shaping.
    public HeadlessPlayerId? Actor { get; init; }

    public HeadlessEntityId? Subject { get; init; }

    public HeadlessEntityId? Target { get; init; }

    public ChoiceZone? ZoneFrom { get; init; }

    public ChoiceZone? ZoneTo { get; init; }

    public string? Cause { get; init; }

    public string Message
    {
        get => _message;
        init => _message = value ?? string.Empty;
    }

    public IReadOnlyDictionary<string, object?> Metadata
    {
        get => _metadata;
        init => _metadata = CopyMetadata(value);
    }

    private static IReadOnlyDictionary<string, object?> CopyMetadata(
        IReadOnlyDictionary<string, object?>? metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        return new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>(metadata));
    }
}
