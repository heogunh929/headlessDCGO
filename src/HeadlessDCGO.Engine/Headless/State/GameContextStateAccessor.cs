namespace HeadlessDCGO.Engine.Headless.State;

using System.Collections.ObjectModel;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class GameContextStateAccessor
{
    private IReadOnlyList<HeadlessEntityId> _activeCardIds = Array.Empty<HeadlessEntityId>();

    public GameContextStateAccessor(
        MatchState state,
        HeadlessPlayerId turnPlayerId,
        HeadlessPhase turnPhase = HeadlessPhase.None,
        int memory = 0,
        HeadlessPlayerId? firstPlayerId = null,
        bool doSwitchTurnPlayer = true,
        bool isSecurityLooking = false,
        IEnumerable<HeadlessEntityId>? activeCardIds = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (turnPlayerId.IsEmpty)
        {
            throw new ArgumentException("Turn player id must not be empty.", nameof(turnPlayerId));
        }

        HeadlessPhaseMapping.EnsureDefined(turnPhase);
        State = state;
        _ = State.GetPlayer(turnPlayerId);
        if (firstPlayerId is { IsEmpty: true })
        {
            throw new ArgumentException("First player id must not be empty.", nameof(firstPlayerId));
        }

        if (firstPlayerId.HasValue)
        {
            _ = State.GetPlayer(firstPlayerId.Value);
        }

        TurnPlayerId = turnPlayerId;
        FirstPlayerId = firstPlayerId ?? turnPlayerId;
        TurnPhase = turnPhase;
        Memory = memory;
        DoSwitchTurnPlayer = doSwitchTurnPlayer;
        IsSecurityLooking = isSecurityLooking;
        ActiveCardIds = CopyActiveCardIds(activeCardIds ?? Array.Empty<HeadlessEntityId>());
    }

    public MatchState State { get; private set; }

    public int Memory { get; private set; }

    public HeadlessPhase TurnPhase { get; private set; }

    public HeadlessPlayerId TurnPlayerId { get; private set; }

    public HeadlessPlayerId FirstPlayerId { get; private set; }

    public bool DoSwitchTurnPlayer { get; private set; }

    public bool IsSecurityLooking { get; private set; }

    public IReadOnlyList<HeadlessEntityId> ActiveCardIds
    {
        get => _activeCardIds;
        private set => _activeCardIds = CopyActiveCardIds(value);
    }

    public IReadOnlyList<PlayerState> Players => State.Players;

    public IReadOnlyList<PlayerState> PlayersForTurnPlayer =>
        new[] { TurnPlayer, NonTurnPlayer }.Where(player => player is not null).Cast<PlayerState>().ToArray();

    public IReadOnlyList<PlayerState> PlayersForNonTurnPlayer =>
        new[] { NonTurnPlayer, TurnPlayer }.Where(player => player is not null).Cast<PlayerState>().ToArray();

    public PlayerState TurnPlayer => State.GetPlayer(TurnPlayerId);

    public PlayerState? NonTurnPlayer => State.Players.FirstOrDefault(player => player.PlayerId != TurnPlayerId);

    public IReadOnlyList<HeadlessEntityId> PermanentsForTurnPlayer =>
        PlayersForTurnPlayer
            .SelectMany(player => player.GetZone(ChoiceZone.BattleArea))
            .ToArray();

    public GameContextStateSnapshot ReadState()
    {
        return new GameContextStateSnapshot(
            State,
            Memory,
            TurnPhase,
            TurnPlayerId,
            NonTurnPlayer?.PlayerId,
            FirstPlayerId,
            Players,
            PlayersForTurnPlayer,
            PlayersForNonTurnPlayer,
            PermanentsForTurnPlayer,
            ActiveCardIds,
            DoSwitchTurnPlayer,
            IsSecurityLooking);
    }

    public PlayerState PlayerFromId(HeadlessPlayerId playerId)
    {
        return State.GetPlayer(playerId);
    }

    public bool TryPlayerFromId(
        HeadlessPlayerId playerId,
        out PlayerState? player)
    {
        player = State.Players.FirstOrDefault(candidate => candidate.PlayerId == playerId);
        return player is not null;
    }

    public void WriteState(GameContextStateWrite write)
    {
        ArgumentNullException.ThrowIfNull(write);

        MatchState nextState = write.State ?? State;
        HeadlessPlayerId nextTurnPlayerId = write.TurnPlayerId ?? TurnPlayerId;
        HeadlessPlayerId nextFirstPlayerId = write.FirstPlayerId ?? FirstPlayerId;
        _ = nextState.GetPlayer(nextTurnPlayerId);
        _ = nextState.GetPlayer(nextFirstPlayerId);

        HeadlessPhase nextPhase = write.TurnPhase ?? TurnPhase;
        HeadlessPhaseMapping.EnsureDefined(nextPhase);
        IReadOnlyList<HeadlessEntityId> nextActiveCardIds = write.ActiveCardIds is null
            ? ActiveCardIds
            : CopyActiveCardIds(write.ActiveCardIds);

        State = nextState;
        TurnPlayerId = nextTurnPlayerId;
        FirstPlayerId = nextFirstPlayerId;
        TurnPhase = nextPhase;
        Memory = write.Memory ?? Memory;
        DoSwitchTurnPlayer = write.DoSwitchTurnPlayer ?? DoSwitchTurnPlayer;
        IsSecurityLooking = write.IsSecurityLooking ?? IsSecurityLooking;
        ActiveCardIds = nextActiveCardIds;
    }

    public void SetMemory(int memory)
    {
        Memory = memory;
    }

    public void SetTurnPhase(HeadlessPhase phase)
    {
        HeadlessPhaseMapping.EnsureDefined(phase);
        TurnPhase = phase;
    }

    public void SetSecurityLooking(bool isSecurityLooking)
    {
        IsSecurityLooking = isSecurityLooking;
    }

    public void SwitchTurnPlayer()
    {
        if (!DoSwitchTurnPlayer)
        {
            DoSwitchTurnPlayer = true;
            return;
        }

        PlayerState? nextPlayer = State.Players.FirstOrDefault(player => player.PlayerId != TurnPlayerId);
        if (nextPlayer is null)
        {
            throw new InvalidOperationException("Cannot switch turn player when no non-turn player exists.");
        }

        TurnPlayerId = nextPlayer.PlayerId;
        DoSwitchTurnPlayer = true;
    }

    private static IReadOnlyList<HeadlessEntityId> CopyActiveCardIds(IEnumerable<HeadlessEntityId> activeCardIds)
    {
        ArgumentNullException.ThrowIfNull(activeCardIds);
        HeadlessEntityId[] snapshot = activeCardIds.ToArray();
        if (snapshot.Any(id => id.IsEmpty))
        {
            throw new ArgumentException("Active card ids must not contain empty ids.", nameof(activeCardIds));
        }

        if (snapshot.Distinct().Count() != snapshot.Length)
        {
            throw new InvalidOperationException("Active card ids must be unique.");
        }

        return Array.AsReadOnly(snapshot);
    }
}

public sealed record GameContextStateWrite(
    MatchState? State = null,
    int? Memory = null,
    HeadlessPhase? TurnPhase = null,
    HeadlessPlayerId? TurnPlayerId = null,
    HeadlessPlayerId? FirstPlayerId = null,
    bool? DoSwitchTurnPlayer = null,
    bool? IsSecurityLooking = null,
    IReadOnlyList<HeadlessEntityId>? ActiveCardIds = null);

public sealed record GameContextStateSnapshot(
    MatchState State,
    int Memory,
    HeadlessPhase TurnPhase,
    HeadlessPlayerId TurnPlayerId,
    HeadlessPlayerId? NonTurnPlayerId,
    HeadlessPlayerId FirstPlayerId,
    IReadOnlyList<PlayerState> Players,
    IReadOnlyList<PlayerState> PlayersForTurnPlayer,
    IReadOnlyList<PlayerState> PlayersForNonTurnPlayer,
    IReadOnlyList<HeadlessEntityId> PermanentsForTurnPlayer,
    IReadOnlyList<HeadlessEntityId> ActiveCardIds,
    bool DoSwitchTurnPlayer,
    bool IsSecurityLooking)
{
    public IReadOnlyDictionary<string, object?> ToMetadata()
    {
        return new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>
        {
            ["memory"] = Memory,
            ["turnPhase"] = TurnPhase.ToString(),
            ["turnPlayerId"] = TurnPlayerId.Value,
            ["nonTurnPlayerId"] = NonTurnPlayerId?.Value,
            ["firstPlayerId"] = FirstPlayerId.Value,
            ["playerCount"] = Players.Count,
            ["activeCardIds"] = ActiveCardIds.Select(id => id.Value).ToArray(),
            ["doSwitchTurnPlayer"] = DoSwitchTurnPlayer,
            ["isSecurityLooking"] = IsSecurityLooking,
        });
    }
}
