namespace HeadlessDCGO.Engine.Headless.State;

using HeadlessDCGO.Engine.Headless.Services;

public sealed class PlayerRuleAdapter
{
    public const string LoseFlagKey = "isLose";

    public PlayerRuleAdapter(
        PlayerZoneAdapter zones,
        int memory,
        bool isSecurityLooking = false,
        int minimumMemory = -10,
        int maximumMemory = 10)
    {
        ArgumentNullException.ThrowIfNull(zones);
        if (minimumMemory > maximumMemory)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumMemory), "Minimum memory must be less than or equal to maximum memory.");
        }

        Zones = zones;
        Memory = memory;
        IsSecurityLooking = isSecurityLooking;
        MinimumMemory = minimumMemory;
        MaximumMemory = maximumMemory;
        PositiveMemoryPlayerId = Zones.State.Players
            .OrderBy(player => player.PlayerId.Value)
            .FirstOrDefault()?.PlayerId
            ?? throw new InvalidOperationException("At least one player is required.");
    }

    public PlayerRuleAdapter(GameContextStateSnapshot snapshot, int minimumMemory = -10, int maximumMemory = 10)
        : this(
            new PlayerZoneAdapter(snapshot?.State ?? throw new ArgumentNullException(nameof(snapshot))),
            snapshot.Memory,
            snapshot.IsSecurityLooking,
            minimumMemory,
            maximumMemory)
    {
    }

    public PlayerZoneAdapter Zones { get; }

    public int Memory { get; }

    public int MinimumMemory { get; }

    public int MaximumMemory { get; }

    public bool IsSecurityLooking { get; }

    public HeadlessPlayerId PositiveMemoryPlayerId { get; }

    public int MaxMemoryCost(HeadlessPlayerId playerId)
    {
        _ = Zones.State.GetPlayer(playerId);
        return IsPositiveMemoryPlayer(playerId)
            ? Math.Abs(MaximumMemory - Memory)
            : Math.Abs(Memory - MinimumMemory);
    }

    public int ExpectedMemory(HeadlessPlayerId playerId, int memoryCost)
    {
        if (memoryCost < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(memoryCost), "Memory cost must not be negative.");
        }

        _ = Zones.State.GetPlayer(playerId);
        return IsPositiveMemoryPlayer(playerId)
            ? Memory + memoryCost
            : Memory - memoryCost;
    }

    public bool CanPayMemoryCost(HeadlessPlayerId playerId, int memoryCost)
    {
        if (memoryCost < 0)
        {
            return false;
        }

        _ = Zones.State.GetPlayer(playerId);
        return memoryCost <= MaxMemoryCost(playerId);
    }

    public bool CanAddSecurity(HeadlessPlayerId playerId, int count = 1)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Security count must not be negative.");
        }

        _ = Zones.ReadPlayer(playerId);
        return !IsSecurityLooking;
    }

    public bool CanReduceSecurity(HeadlessPlayerId playerId, int count = 1)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Security count must not be negative.");
        }

        PlayerZoneOwnershipSnapshot player = Zones.ReadPlayer(playerId);
        return !IsSecurityLooking && player.SecurityCount >= count;
    }

    public bool CanDraw(HeadlessPlayerId playerId, int count = 1)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Draw count must not be negative.");
        }

        return Zones.ReadPlayer(playerId).LibraryCount >= count;
    }

    public PlayerTerminalCheck EvaluateDeckLossOnDraw(HeadlessPlayerId drawingPlayerId, int drawCount = 1)
    {
        if (drawCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(drawCount), "Draw count must not be negative.");
        }

        PlayerZoneOwnershipSnapshot drawingPlayer = Zones.ReadPlayer(drawingPlayerId);
        if (drawingPlayer.LibraryCount >= drawCount)
        {
            return PlayerTerminalCheck.NotTerminal(PlayerTerminalReason.DeckLoss);
        }

        return PlayerTerminalCheck.Terminal(
            PlayerTerminalReason.DeckLoss,
            WinnerPlayerId: OpponentOf(drawingPlayerId),
            LosingPlayerId: drawingPlayerId,
            Message: $"Player {drawingPlayerId} cannot draw {drawCount} card(s) from a library with {drawingPlayer.LibraryCount} card(s).");
    }

    public PlayerTerminalCheck EvaluateSecurityAttack(
        HeadlessPlayerId attackingPlayerId,
        HeadlessPlayerId defendingPlayerId,
        int securityChecks = 1)
    {
        if (securityChecks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(securityChecks), "Security check count must not be negative.");
        }

        _ = Zones.State.GetPlayer(attackingPlayerId);
        PlayerZoneOwnershipSnapshot defender = Zones.ReadPlayer(defendingPlayerId);
        if (securityChecks == 0 || defender.SecurityCount > 0)
        {
            return PlayerTerminalCheck.NotTerminal(PlayerTerminalReason.SecurityLoss);
        }

        return PlayerTerminalCheck.Terminal(
            PlayerTerminalReason.SecurityLoss,
            WinnerPlayerId: attackingPlayerId,
            LosingPlayerId: defendingPlayerId,
            Message: $"Player {defendingPlayerId} has no security for a direct attack.");
    }

    public PlayerTerminalCheck EvaluateLoseFlag(HeadlessPlayerId playerId)
    {
        PlayerState player = Zones.State.GetPlayer(playerId);
        bool isLose = player.Flags.TryGetValue(LoseFlagKey, out bool value) && value;
        if (!isLose)
        {
            return PlayerTerminalCheck.NotTerminal(PlayerTerminalReason.PlayerLoseFlag);
        }

        return PlayerTerminalCheck.Terminal(
            PlayerTerminalReason.PlayerLoseFlag,
            WinnerPlayerId: OpponentOf(playerId),
            LosingPlayerId: playerId,
            Message: $"Player {playerId} is marked as lose.");
    }

    public PlayerTerminalCheck EvaluatePlayerChecks(HeadlessPlayerId playerId, int nextDrawCount = 0)
    {
        PlayerTerminalCheck loseFlag = EvaluateLoseFlag(playerId);
        if (loseFlag.IsTerminal)
        {
            return loseFlag;
        }

        return nextDrawCount > 0
            ? EvaluateDeckLossOnDraw(playerId, nextDrawCount)
            : PlayerTerminalCheck.NotTerminal(PlayerTerminalReason.None);
    }

    private bool IsPositiveMemoryPlayer(HeadlessPlayerId playerId)
    {
        return playerId == PositiveMemoryPlayerId;
    }

    private HeadlessPlayerId? OpponentOf(HeadlessPlayerId playerId)
    {
        _ = Zones.State.GetPlayer(playerId);
        return Zones.State.Players
            .Where(player => player.PlayerId != playerId)
            .OrderBy(player => player.PlayerId.Value)
            .FirstOrDefault()?.PlayerId;
    }
}

public enum PlayerTerminalReason
{
    None,
    DeckLoss,
    SecurityLoss,
    PlayerLoseFlag
}

public sealed record PlayerTerminalCheck(
    bool IsTerminal,
    PlayerTerminalReason Reason,
    HeadlessPlayerId? WinnerPlayerId,
    HeadlessPlayerId? LosingPlayerId,
    string Message)
{
    public static PlayerTerminalCheck NotTerminal(PlayerTerminalReason reason = PlayerTerminalReason.None)
    {
        return new PlayerTerminalCheck(false, reason, null, null, string.Empty);
    }

    public static PlayerTerminalCheck Terminal(
        PlayerTerminalReason Reason,
        HeadlessPlayerId? WinnerPlayerId,
        HeadlessPlayerId? LosingPlayerId,
        string Message)
    {
        if (Reason == PlayerTerminalReason.None)
        {
            throw new ArgumentException("Terminal checks require a concrete reason.", nameof(Reason));
        }

        if (WinnerPlayerId.HasValue && LosingPlayerId.HasValue && WinnerPlayerId.Value == LosingPlayerId.Value)
        {
            throw new ArgumentException("Winner and losing player must be different.", nameof(WinnerPlayerId));
        }

        return new PlayerTerminalCheck(
            true,
            Reason,
            WinnerPlayerId,
            LosingPlayerId,
            Message ?? string.Empty);
    }

    public IReadOnlyDictionary<string, object?> ToMetadata()
    {
        return new Dictionary<string, object?>
        {
            ["isTerminal"] = IsTerminal,
            ["reason"] = Reason.ToString(),
            ["winnerPlayerId"] = WinnerPlayerId?.Value,
            ["losingPlayerId"] = LosingPlayerId?.Value,
            ["message"] = Message
        };
    }
}
