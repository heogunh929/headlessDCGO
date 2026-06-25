namespace HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// Extends <see cref="ITerminalStateController"/> with a winner/reason payload so the rule query service
/// can carry a concrete terminal outcome (not just a boolean). The common loop's end-turn check populates
/// this from a <c>PlayerRuleAdapter</c> verdict and <c>DcgoMatch</c> reads it into the public match result (X-02).
/// </summary>
public interface ITerminalOutcomeSink
{
    void SetTerminalOutcome(HeadlessPlayerId? winnerPlayerId, bool isDraw, string reason);

    bool TryGetTerminalOutcome(out TerminalOutcome? outcome);
}

public sealed record TerminalOutcome(
    HeadlessPlayerId? WinnerPlayerId,
    bool IsDraw,
    string Reason);
