namespace HeadlessDCGO.Engine.Headless.Services;

using System.Collections.ObjectModel;

public sealed record CardInstanceRecord
{
    private IReadOnlyDictionary<string, object?> _metadata = ReadOnlyDictionary<string, object?>.Empty;

    public CardInstanceRecord(
        HeadlessEntityId InstanceId,
        HeadlessEntityId DefinitionId,
        HeadlessPlayerId OwnerId,
        bool IsToken = false,
        IReadOnlyDictionary<string, object?>? Metadata = null)
    {
        if (InstanceId.IsEmpty)
        {
            throw new ArgumentException("Card instance id must not be empty.", nameof(InstanceId));
        }

        if (DefinitionId.IsEmpty)
        {
            throw new ArgumentException("Card definition id must not be empty.", nameof(DefinitionId));
        }

        if (OwnerId.IsEmpty)
        {
            throw new ArgumentException("Owner id must not be empty.", nameof(OwnerId));
        }

        this.InstanceId = InstanceId;
        this.DefinitionId = DefinitionId;
        this.OwnerId = OwnerId;
        this.IsToken = IsToken;
        this.Metadata = Metadata ?? new Dictionary<string, object?>();
    }

    public HeadlessEntityId InstanceId { get; init; }

    public HeadlessEntityId DefinitionId { get; init; }

    public HeadlessPlayerId OwnerId { get; init; }

    public bool IsToken { get; init; }

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
