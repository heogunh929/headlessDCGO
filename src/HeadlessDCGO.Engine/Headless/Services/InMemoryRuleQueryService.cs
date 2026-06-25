namespace HeadlessDCGO.Engine.Headless.Services;

// TODO: Replace with real DCGO legal-action and rule checks as rules are ported.
public sealed class InMemoryRuleQueryService :
    IRuleQueryService,
    ITerminalStateController,
    ITerminalOutcomeSink,
    IHeadlessMatchStateResettable,
    IHeadlessLegalActionController
{
    private readonly List<LegalAction> _legalActions = new();
    private bool _isTerminal;
    private TerminalOutcome? _terminalOutcome;

    public void SetLegalActions(IEnumerable<LegalAction> legalActions)
    {
        ArgumentNullException.ThrowIfNull(legalActions);
        _legalActions.Clear();
        _legalActions.AddRange(legalActions);
    }

    public void AddLegalActions(IEnumerable<LegalAction> legalActions)
    {
        ArgumentNullException.ThrowIfNull(legalActions);

        foreach (LegalAction legalAction in legalActions)
        {
            _legalActions.RemoveAll(action => action.Id == legalAction.Id);
            _legalActions.Add(legalAction);
        }
    }

    public bool RemoveLegalAction(HeadlessEntityId actionId)
    {
        int removedCount = _legalActions.RemoveAll(action => action.Id == actionId);
        return removedCount > 0;
    }

    public void ClearLegalActions()
    {
        _legalActions.Clear();
    }

    public void SetTerminal(bool isTerminal)
    {
        _isTerminal = isTerminal;
        if (!isTerminal)
        {
            _terminalOutcome = null;
        }
    }

    public void SetTerminalOutcome(HeadlessPlayerId? winnerPlayerId, bool isDraw, string reason)
    {
        _isTerminal = true;
        _terminalOutcome = new TerminalOutcome(winnerPlayerId, isDraw, reason ?? string.Empty);
    }

    public bool TryGetTerminalOutcome(out TerminalOutcome? outcome)
    {
        outcome = _terminalOutcome;
        return _terminalOutcome is not null;
    }

    public IReadOnlyList<LegalAction> GetLegalActions(HeadlessPlayerId playerId)
    {
        return _legalActions
            .Where(action => action.PlayerId == playerId)
            .ToArray();
    }

    public bool CanPayCost(HeadlessPlayerId playerId, HeadlessEntityId sourceId, int cost)
    {
        return cost >= 0;
    }

    public bool IsTerminal()
    {
        return _isTerminal;
    }

    public void Clear()
    {
        ResetMatchState();
    }

    public void ResetMatchState()
    {
        _legalActions.Clear();
        _isTerminal = false;
        _terminalOutcome = null;
    }
}
