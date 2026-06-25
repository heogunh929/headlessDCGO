namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Services;

public sealed class InMemoryHeadlessTurnController : IHeadlessTurnController
{
    private readonly List<HeadlessPlayerId> _playerOrder = new();

    public HeadlessTurnState Current { get; private set; } = HeadlessTurnState.Empty;

    public void Initialize(
        IReadOnlyList<HeadlessPlayerId> playerIds,
        HeadlessPlayerId? firstPlayerId = null)
    {
        ArgumentNullException.ThrowIfNull(playerIds);

        _playerOrder.Clear();
        _playerOrder.AddRange(playerIds.Distinct());

        if (_playerOrder.Count == 0)
        {
            Current = HeadlessTurnState.Empty;
            return;
        }

        HeadlessPlayerId turnPlayerId = ResolveFirstPlayer(firstPlayerId);
        Current = CreateState(
            turnNumber: 1,
            turnPlayerId,
            HeadlessPhase.Setup);
    }

    public HeadlessTurnState AdvancePhase()
    {
        if (Current.TurnPlayerId is null)
        {
            return Current;
        }

        Current = Current with { Phase = HeadlessPhaseMapping.Next(Current.Phase) };
        return Current;
    }

    public HeadlessTurnState EndTurn()
    {
        if (Current.TurnPlayerId is null || _playerOrder.Count == 0)
        {
            return Current;
        }

        HeadlessPlayerId nextPlayerId = NextPlayer(Current.TurnPlayerId.Value);
        Current = CreateState(
            Current.TurnNumber + 1,
            nextPlayerId,
            HeadlessPhase.Active);

        return Current;
    }

    public HeadlessTurnState SetPhase(HeadlessPhase phase)
    {
        HeadlessPhaseMapping.EnsureDefined(phase);
        Current = Current with { Phase = phase };
        return Current;
    }

    public void ResetMatchState()
    {
        _playerOrder.Clear();
        Current = HeadlessTurnState.Empty;
    }

    private HeadlessPlayerId ResolveFirstPlayer(HeadlessPlayerId? firstPlayerId)
    {
        if (firstPlayerId.HasValue && _playerOrder.Contains(firstPlayerId.Value))
        {
            return firstPlayerId.Value;
        }

        return _playerOrder[0];
    }

    private HeadlessTurnState CreateState(
        int turnNumber,
        HeadlessPlayerId turnPlayerId,
        HeadlessPhase phase)
    {
        return new HeadlessTurnState(
            turnNumber,
            turnPlayerId,
            ResolveNonTurnPlayer(turnPlayerId),
            phase,
            turnNumber == 1,
            _playerOrder.ToArray());
    }

    private HeadlessPlayerId? ResolveNonTurnPlayer(HeadlessPlayerId turnPlayerId)
    {
        foreach (HeadlessPlayerId playerId in _playerOrder)
        {
            if (playerId != turnPlayerId)
            {
                return playerId;
            }
        }

        return null;
    }

    private HeadlessPlayerId NextPlayer(HeadlessPlayerId currentPlayerId)
    {
        int currentIndex = _playerOrder.IndexOf(currentPlayerId);
        if (currentIndex < 0)
        {
            return _playerOrder[0];
        }

        int nextIndex = (currentIndex + 1) % _playerOrder.Count;
        return _playerOrder[nextIndex];
    }

}
