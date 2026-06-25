namespace HeadlessDCGO.Engine.Headless.Runtime;

using System.Collections.ObjectModel;

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
