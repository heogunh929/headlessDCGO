namespace HeadlessDCGO.Engine.Headless.Runtime;

using System.Collections.ObjectModel;
using HeadlessDCGO.Engine.Headless.Services;

public sealed record SessionContext
{
    public SessionContext(
        string? sessionId,
        IEnumerable<HeadlessPlayerId> playerIds,
        HeadlessPlayerId? localPlayerId = null,
        HeadlessPlayerId? turnPlayerId = null,
        int turnNumber = 0)
    {
        HeadlessPlayerId[] players = CopyPlayerIds(playerIds);
        HeadlessPlayerId resolvedLocalPlayerId = ResolveLocalPlayer(players, localPlayerId);
        HeadlessPlayerId? resolvedTurnPlayerId = ResolveOptionalPlayer(players, turnPlayerId, nameof(turnPlayerId));

        if (turnNumber < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(turnNumber), "Turn number must not be negative.");
        }

        SessionId = string.IsNullOrWhiteSpace(sessionId) ? "local" : sessionId.Trim();
        PlayerIds = Array.AsReadOnly(players);
        LocalPlayerId = resolvedLocalPlayerId;
        TurnPlayerId = resolvedTurnPlayerId;
        TurnNumber = turnNumber;
    }

    public string SessionId { get; }

    public IReadOnlyList<HeadlessPlayerId> PlayerIds { get; }

    public HeadlessPlayerId LocalPlayerId { get; }

    public HeadlessPlayerId? TurnPlayerId { get; }

    public int TurnNumber { get; }

    public HeadlessPlayerId? NonTurnPlayerId
    {
        get
        {
            if (TurnPlayerId is null)
            {
                return null;
            }

            foreach (HeadlessPlayerId playerId in PlayerIds)
            {
                if (playerId != TurnPlayerId.Value)
                {
                    return playerId;
                }
            }

            return null;
        }
    }

    public bool IsLocalPlayerTurn => TurnPlayerId == LocalPlayerId;

    public bool ContainsPlayer(HeadlessPlayerId playerId)
    {
        return !playerId.IsEmpty && PlayerIds.Contains(playerId);
    }

    public bool IsOwner(HeadlessPlayerId ownerId, HeadlessPlayerId viewerId)
    {
        EnsurePlayerExists(ownerId, nameof(ownerId));
        EnsurePlayerExists(viewerId, nameof(viewerId));
        return ownerId == viewerId;
    }

    public bool IsLocalOwner(HeadlessPlayerId ownerId)
    {
        EnsurePlayerExists(ownerId, nameof(ownerId));
        return ownerId == LocalPlayerId;
    }

    public SessionContext WithTurn(
        HeadlessPlayerId turnPlayerId,
        int turnNumber)
    {
        EnsurePlayerExists(turnPlayerId, nameof(turnPlayerId));

        if (turnNumber < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(turnNumber), "Turn number must not be negative.");
        }

        return new SessionContext(SessionId, PlayerIds, LocalPlayerId, turnPlayerId, turnNumber);
    }

    public SessionContext AdvanceTurn()
    {
        if (PlayerIds.Count == 0)
        {
            return this;
        }

        HeadlessPlayerId nextPlayerId = TurnPlayerId is null
            ? PlayerIds[0]
            : PlayerIds[(IndexOf(TurnPlayerId.Value) + 1) % PlayerIds.Count];

        int nextTurnNumber = TurnNumber == 0 ? 1 : TurnNumber + 1;
        return WithTurn(nextPlayerId, nextTurnNumber);
    }

    public string Fingerprint()
    {
        return string.Join(
            "|",
            SessionId,
            LocalPlayerId.Value.ToString(),
            TurnPlayerId?.Value.ToString() ?? string.Empty,
            TurnNumber.ToString(),
            string.Join(",", PlayerIds.Select(playerId => playerId.Value)));
    }

    private int IndexOf(HeadlessPlayerId playerId)
    {
        for (var i = 0; i < PlayerIds.Count; i++)
        {
            if (PlayerIds[i] == playerId)
            {
                return i;
            }
        }

        return 0;
    }

    private void EnsurePlayerExists(HeadlessPlayerId playerId, string parameterName)
    {
        if (playerId.IsEmpty)
        {
            throw new ArgumentException("Player id must not be empty.", parameterName);
        }

        if (!ContainsPlayer(playerId))
        {
            throw new InvalidOperationException($"Player '{playerId.Value}' is not part of session '{SessionId}'.");
        }
    }

    private static HeadlessPlayerId[] CopyPlayerIds(IEnumerable<HeadlessPlayerId>? playerIds)
    {
        ArgumentNullException.ThrowIfNull(playerIds);

        HeadlessPlayerId[] players = playerIds.ToArray();
        if (players.Length == 0)
        {
            throw new ArgumentException("Session must contain at least one player.", nameof(playerIds));
        }

        if (players.Any(playerId => playerId.IsEmpty))
        {
            throw new ArgumentException("Session player ids must not contain empty values.", nameof(playerIds));
        }

        if (players.Distinct().Count() != players.Length)
        {
            throw new InvalidOperationException("Session player ids must be unique.");
        }

        return players;
    }

    private static HeadlessPlayerId ResolveLocalPlayer(
        IReadOnlyList<HeadlessPlayerId> players,
        HeadlessPlayerId? localPlayerId)
    {
        if (localPlayerId is null)
        {
            return players[0];
        }

        if (localPlayerId.Value.IsEmpty)
        {
            throw new ArgumentException("Local player id must not be empty.", nameof(localPlayerId));
        }

        if (!players.Contains(localPlayerId.Value))
        {
            throw new InvalidOperationException($"Local player '{localPlayerId.Value.Value}' is not part of the session.");
        }

        return localPlayerId.Value;
    }

    private static HeadlessPlayerId? ResolveOptionalPlayer(
        IReadOnlyList<HeadlessPlayerId> players,
        HeadlessPlayerId? playerId,
        string parameterName)
    {
        if (playerId is null)
        {
            return null;
        }

        if (playerId.Value.IsEmpty)
        {
            throw new ArgumentException("Player id must not be empty.", parameterName);
        }

        if (!players.Contains(playerId.Value))
        {
            throw new InvalidOperationException($"Player '{playerId.Value.Value}' is not part of the session.");
        }

        return playerId.Value;
    }
}
