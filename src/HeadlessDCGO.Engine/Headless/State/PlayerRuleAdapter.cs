namespace HeadlessDCGO.Engine.Headless.State;

using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (X-02) The terminal-verdict seam: consolidates the AS-IS win/loss checks (deck-out on draw, direct-hit on
/// empty security, and the consolidated <c>Player.IsLose</c> flag) into <see cref="PlayerTerminalCheck"/>
/// verdicts read by <c>TerminalEvaluator</c> / the match result.
///
/// (DEF-S11 retirement) The adapter formerly also carried simplified memory-cost / security / draw PREDICATES
/// (MaxMemoryCost, ExpectedMemory, CanPayMemoryCost, CanAddSecurity, CanReduceSecurity, CanDraw). Those were a
/// dead duplicate of the fidelity mirror and had ZERO production consumers: production reads
/// <c>new Player(context, owner).MaxMemoryCost</c> (Player.cs:255, AS-IS Player.cs:1127-1146) and
/// <c>new Player(context, owner).CanAddSecurity(...)</c> (Player.cs:477, the AS-IS-literal
/// <c>ICannotAddSecurityEffect</c> LIVE scan) directly. The adapter is a scan-less snapshot (built from bare
/// lose flags, with no effect registry) and cannot host the continuous-restriction scan, so the simplified
/// copies were removed rather than rewired — the mirror <c>Player</c> members are the single fidelity path.
/// </summary>
public sealed class PlayerRuleAdapter
{
    public const string LoseFlagKey = "isLose";

    public PlayerRuleAdapter(PlayerZoneAdapter zones)
    {
        ArgumentNullException.ThrowIfNull(zones);
        Zones = zones;
    }

    public PlayerRuleAdapter(GameContextStateSnapshot snapshot)
        : this(new PlayerZoneAdapter(snapshot?.State ?? throw new ArgumentNullException(nameof(snapshot))))
    {
    }

    public PlayerZoneAdapter Zones { get; }

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
