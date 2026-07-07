namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// Player-scoped, turn-scoped integer counters — the headless mirror of AS-IS per-turn <c>Player</c> fields
/// such as <c>Player.DigivolveCount_ThisTurn</c> (CardController.cs:1528 increments on each digivolve,
/// TurnStateMachine.cs:3181 resets to 0 at turn start). Reset every turn boundary (alongside
/// <see cref="Effects.OnceFlagController"/>), so a card gate like BT1_007 "[When Attacking] if you've
/// digivolved this turn …" reads the live count. General on purpose — new per-turn player counters add a
/// key rather than a new service.
/// </summary>
public sealed class PlayerTurnCounterController : IHeadlessMatchStateResettable
{
    /// <summary>Number of times the player has digivolved this turn (AS-IS <c>DigivolveCount_ThisTurn</c>).</summary>
    public const string DigivolveCountKey = "digivolveCount";

    private readonly Dictionary<(HeadlessPlayerId Player, string Key), int> _counts = new();

    /// <summary>Reset all per-turn counters at a turn boundary (mirror of <c>InitUseCountThisTurn</c> /
    /// the AS-IS turn-start reset). Both players are cleared — a non-turn player's counters are already 0.</summary>
    public void ResetForTurn() => _counts.Clear();

    /// <summary>Increment <paramref name="player"/>'s <paramref name="key"/> counter by one.</summary>
    public void Increment(HeadlessPlayerId player, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _counts.TryGetValue((player, key), out int current);
        _counts[(player, key)] = current + 1;
    }

    /// <summary>Current value of <paramref name="player"/>'s <paramref name="key"/> counter (0 if unset).</summary>
    public int Get(HeadlessPlayerId player, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _counts.TryGetValue((player, key), out int current) ? current : 0;
    }

    public void ResetMatchState() => _counts.Clear();
}
