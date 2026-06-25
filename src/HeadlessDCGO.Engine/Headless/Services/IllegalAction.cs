namespace HeadlessDCGO.Engine.Headless.Services;

using System.Collections.ObjectModel;

public sealed record IllegalAction
{
    private string _actionType = string.Empty;
    private string _reason = string.Empty;
    private IReadOnlyDictionary<string, object?> _metadata = ReadOnlyDictionary<string, object?>.Empty;

    public IllegalAction(
        HeadlessEntityId? Id,
        HeadlessPlayerId? PlayerId,
        string ActionType,
        string Reason,
        IReadOnlyDictionary<string, object?> Metadata)
    {
        this.Id = Id;
        this.PlayerId = PlayerId;
        this.ActionType = ActionType;
        this.Reason = Reason;
        this.Metadata = Metadata;
    }

    public HeadlessEntityId? Id { get; init; }

    public HeadlessPlayerId? PlayerId { get; init; }

    public string ActionType
    {
        get => _actionType;
        init => _actionType = string.IsNullOrWhiteSpace(value) ? "Unknown" : value.Trim();
    }

    public string Reason
    {
        get => _reason;
        init => _reason = string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Illegal action reason must not be empty.", nameof(value))
            : value.Trim();
    }

    public IReadOnlyDictionary<string, object?> Metadata
    {
        get => _metadata;
        init => _metadata = CopyMetadata(value);
    }

    public static IllegalAction FromLegalAction(
        LegalAction action,
        string reason,
        IReadOnlyDictionary<string, object?>? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(action);

        Dictionary<string, object?> merged = new(metadata ?? new Dictionary<string, object?>())
        {
            ["actionId"] = action.Id.Value,
            ["playerId"] = action.PlayerId.Value,
            ["actionType"] = action.ActionType
        };

        return new IllegalAction(action.Id, action.PlayerId, action.ActionType, reason, merged);
    }

    private static IReadOnlyDictionary<string, object?> CopyMetadata(
        IReadOnlyDictionary<string, object?>? metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        return new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>(metadata));
    }
}
