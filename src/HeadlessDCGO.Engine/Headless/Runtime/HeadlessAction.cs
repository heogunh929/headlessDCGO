namespace HeadlessDCGO.Engine.Headless.Runtime;

using System.Collections.ObjectModel;
using HeadlessDCGO.Engine.Headless.Services;

public sealed record HeadlessAction
{
    private string _actionType = string.Empty;
    private IReadOnlyDictionary<string, object?> _parameters = ReadOnlyDictionary<string, object?>.Empty;

    public HeadlessAction(
        HeadlessEntityId Id,
        HeadlessPlayerId PlayerId,
        string ActionType,
        IReadOnlyDictionary<string, object?> Parameters)
    {
        this.Id = Id;
        this.PlayerId = PlayerId;
        this.ActionType = ActionType;
        this.Parameters = Parameters;
    }

    public HeadlessEntityId Id { get; init; }

    public HeadlessPlayerId PlayerId { get; init; }

    public string ActionType
    {
        get => _actionType;
        init => _actionType = string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("ActionType must not be empty.", nameof(value))
            : value.Trim();
    }

    public IReadOnlyDictionary<string, object?> Parameters
    {
        get => _parameters;
        init => _parameters = CopyParameters(value);
    }

    public LegalAction ToLegalAction()
    {
        return new LegalAction(Id, PlayerId, ActionType, Parameters);
    }

    private static IReadOnlyDictionary<string, object?> CopyParameters(
        IReadOnlyDictionary<string, object?>? parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        return new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>(parameters));
    }
}
