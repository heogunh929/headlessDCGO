namespace HeadlessDCGO.Engine.Headless.State;

using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

public enum VisibilityViewMode
{
    Player,
    DebugFull
}

public static class VisibilityView
{
    public static VisibilityViewSnapshot ForPlayer(
        GameContextStateAccessor accessor,
        HeadlessPlayerId viewerId)
    {
        ArgumentNullException.ThrowIfNull(accessor);
        return ForPlayer(accessor.ReadState(), viewerId);
    }

    public static VisibilityViewSnapshot ForPlayer(
        GameContextStateSnapshot snapshot,
        HeadlessPlayerId viewerId)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (viewerId.IsEmpty)
        {
            throw new ArgumentException("Viewer id must not be empty.", nameof(viewerId));
        }

        _ = snapshot.State.GetPlayer(viewerId);

        PlayerStateView[] players = snapshot.Players
            .OrderBy(player => player.PlayerId.Value)
            .Select(player => player.ToView(viewerId))
            .ToArray();

        return CreateSnapshot(
            snapshot,
            VisibilityViewMode.Player,
            viewerId,
            players,
            FilterVisibleActiveCards(snapshot, viewerId));
    }

    public static VisibilityViewSnapshot ForDebugFull(GameContextStateAccessor accessor)
    {
        ArgumentNullException.ThrowIfNull(accessor);
        return ForDebugFull(accessor.ReadState());
    }

    public static VisibilityViewSnapshot ForDebugFull(GameContextStateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        PlayerStateView[] players = snapshot.Players
            .OrderBy(player => player.PlayerId.Value)
            .Select(player => player.ToView(player.PlayerId))
            .ToArray();

        return CreateSnapshot(
            snapshot,
            VisibilityViewMode.DebugFull,
            null,
            players,
            snapshot.ActiveCardIds);
    }

    public static bool IsCardVisibleToPlayer(
        GameContextStateSnapshot snapshot,
        HeadlessEntityId cardId,
        HeadlessPlayerId viewerId)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (cardId.IsEmpty)
        {
            throw new ArgumentException("Card id must not be empty.", nameof(cardId));
        }

        if (viewerId.IsEmpty)
        {
            throw new ArgumentException("Viewer id must not be empty.", nameof(viewerId));
        }

        _ = snapshot.State.GetPlayer(viewerId);
        foreach (PlayerState player in snapshot.Players)
        {
            foreach (KeyValuePair<ChoiceZone, IReadOnlyList<HeadlessEntityId>> zone in player.Zones)
            {
                if (!zone.Value.Contains(cardId))
                {
                    continue;
                }

                return player.PlayerId == viewerId || ZoneState.DefaultVisibility(zone.Key) == ZoneVisibility.Public;
            }
        }

        return false;
    }

    private static VisibilityViewSnapshot CreateSnapshot(
        GameContextStateSnapshot snapshot,
        VisibilityViewMode mode,
        HeadlessPlayerId? viewerId,
        IReadOnlyList<PlayerStateView> players,
        IEnumerable<HeadlessEntityId> activeCardIds)
    {
        return new VisibilityViewSnapshot(
            mode,
            viewerId,
            snapshot.Memory,
            snapshot.TurnPhase,
            snapshot.TurnPlayerId,
            snapshot.NonTurnPlayerId,
            snapshot.FirstPlayerId,
            players,
            activeCardIds.ToArray(),
            snapshot.DoSwitchTurnPlayer,
            snapshot.IsSecurityLooking);
    }

    private static IReadOnlyList<HeadlessEntityId> FilterVisibleActiveCards(
        GameContextStateSnapshot snapshot,
        HeadlessPlayerId viewerId)
    {
        return snapshot.ActiveCardIds
            .Where(cardId => IsCardVisibleToPlayer(snapshot, cardId, viewerId))
            .ToArray();
    }
}

public sealed record VisibilityViewSnapshot(
    VisibilityViewMode Mode,
    HeadlessPlayerId? ViewerId,
    int Memory,
    HeadlessPhase TurnPhase,
    HeadlessPlayerId TurnPlayerId,
    HeadlessPlayerId? NonTurnPlayerId,
    HeadlessPlayerId FirstPlayerId,
    IReadOnlyList<PlayerStateView> Players,
    IReadOnlyList<HeadlessEntityId> ActiveCardIds,
    bool DoSwitchTurnPlayer,
    bool IsSecurityLooking)
{
    public bool IsDebugFullView => Mode == VisibilityViewMode.DebugFull;

    public bool IsPlayerView => Mode == VisibilityViewMode.Player;

    public PlayerStateView Player(HeadlessPlayerId playerId)
    {
        return Players.FirstOrDefault(player => player.PlayerId == playerId)
            ?? throw new InvalidOperationException($"Player id '{playerId}' is not in the visibility view.");
    }

    public IReadOnlyDictionary<string, object?> ToMetadata()
    {
        return new Dictionary<string, object?>
        {
            ["mode"] = Mode.ToString(),
            ["viewerId"] = ViewerId?.Value,
            ["memory"] = Memory,
            ["turnPhase"] = TurnPhase.ToString(),
            ["turnPlayerId"] = TurnPlayerId.Value,
            ["nonTurnPlayerId"] = NonTurnPlayerId?.Value,
            ["firstPlayerId"] = FirstPlayerId.Value,
            ["playerCount"] = Players.Count,
            ["activeCardIds"] = ActiveCardIds.Select(id => id.Value).ToArray(),
            ["doSwitchTurnPlayer"] = DoSwitchTurnPlayer,
            ["isSecurityLooking"] = IsSecurityLooking,
        };
    }
}
