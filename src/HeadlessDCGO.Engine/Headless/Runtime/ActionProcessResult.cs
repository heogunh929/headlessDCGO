namespace HeadlessDCGO.Engine.Headless.Runtime;

using System.Collections.ObjectModel;
using HeadlessDCGO.Engine.Headless.Services;

public sealed record ActionProcessResult
{
    private string _message = string.Empty;
    private IReadOnlyDictionary<string, object?> _metadata = ReadOnlyDictionary<string, object?>.Empty;

    public ActionProcessResult(
        bool IsSuccess,
        string Message,
        IReadOnlyDictionary<string, object?> Metadata,
        IllegalAction? IllegalAction = null)
    {
        if (IsSuccess && IllegalAction is not null)
        {
            throw new ArgumentException("Successful results cannot carry IllegalAction details.", nameof(IllegalAction));
        }

        this.IsSuccess = IsSuccess;
        this.Message = Message;
        this.Metadata = Metadata;
        this.IllegalAction = IllegalAction;
    }

    public bool IsSuccess { get; init; }

    public string Message
    {
        get => _message;
        init => _message = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public IReadOnlyDictionary<string, object?> Metadata
    {
        get => _metadata;
        init => _metadata = CopyMetadata(value);
    }

    public IllegalAction? IllegalAction { get; init; }

    public bool IsIllegal => IllegalAction is not null;

    public static ActionProcessResult Success(
        string message = "Action processed.",
        IReadOnlyDictionary<string, object?>? metadata = null)
    {
        return new ActionProcessResult(
            IsSuccess: true,
            message,
            metadata ?? new Dictionary<string, object?>());
    }

    public static ActionProcessResult Failure(
        string message,
        IReadOnlyDictionary<string, object?>? metadata = null)
    {
        return new ActionProcessResult(
            IsSuccess: false,
            message,
            metadata ?? new Dictionary<string, object?>());
    }

    public static ActionProcessResult Illegal(
        IllegalAction illegalAction,
        IReadOnlyDictionary<string, object?>? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(illegalAction);

        Dictionary<string, object?> merged = new(metadata ?? new Dictionary<string, object?>());
        foreach (KeyValuePair<string, object?> pair in illegalAction.Metadata)
        {
            merged.TryAdd(pair.Key, pair.Value);
        }

        return new ActionProcessResult(
            IsSuccess: false,
            illegalAction.Reason,
            merged,
            illegalAction);
    }

    public static ActionProcessResult Illegal(
        LegalAction action,
        string reason,
        IReadOnlyDictionary<string, object?>? metadata = null)
    {
        return Illegal(IllegalAction.FromLegalAction(action, reason, metadata), metadata);
    }

    private static IReadOnlyDictionary<string, object?> CopyMetadata(
        IReadOnlyDictionary<string, object?>? metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        return new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>(metadata));
    }
}
