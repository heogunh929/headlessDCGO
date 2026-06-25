namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Services;

public sealed record ActionMask
{
    private IReadOnlyList<LegalAction> _legalActions = Array.Empty<LegalAction>();

    public ActionMask(IReadOnlyList<LegalAction> LegalActions)
    {
        this.LegalActions = LegalActions;
    }

    public IReadOnlyList<LegalAction> LegalActions
    {
        get => _legalActions;
        init => _legalActions = CopyLegalActions(value);
    }

    public static ActionMask Empty { get; } = new(Array.Empty<LegalAction>());

    public bool HasAnyLegalAction => LegalActions.Count > 0;

    public int Count => LegalActions.Count;

    public bool ContainsAction(LegalAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        return LegalActions.Any(candidate =>
            candidate.Id == action.Id &&
            candidate.PlayerId == action.PlayerId);
    }

    public bool ContainsActionId(HeadlessEntityId actionId)
    {
        return LegalActions.Any(candidate => candidate.Id == actionId);
    }

    public LegalAction? FindById(HeadlessEntityId actionId)
    {
        return LegalActions.FirstOrDefault(candidate => candidate.Id == actionId);
    }

    private static IReadOnlyList<LegalAction> CopyLegalActions(
        IEnumerable<LegalAction>? legalActions)
    {
        ArgumentNullException.ThrowIfNull(legalActions);

        LegalAction[] snapshot = legalActions.ToArray();
        if (snapshot.Any(action => action is null))
        {
            throw new ArgumentException("ActionMask legal actions must not contain null items.", nameof(legalActions));
        }

        return Array.AsReadOnly(snapshot);
    }
}
