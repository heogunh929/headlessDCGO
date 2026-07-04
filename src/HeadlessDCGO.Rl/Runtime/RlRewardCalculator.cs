namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Services;

// TODO: Replace this terminal-only reward with game-specific reward shaping after rules are ported.
public interface IRlRewardCalculator
{
    RlReward Evaluate(
        bool isTerminal,
        MatchResult? result,
        HeadlessPlayerId? perspectivePlayerId);
}

public sealed class TerminalRlRewardCalculator(RlRewardOptions? options = null) : IRlRewardCalculator
{
    public static TerminalRlRewardCalculator Default { get; } = new();

    private readonly RlRewardOptions _options = options ?? RlRewardOptions.Default;

    public RlReward Evaluate(
        bool isTerminal,
        MatchResult? result,
        HeadlessPlayerId? perspectivePlayerId)
    {
        if (!isTerminal || result is null)
        {
            return new RlReward(_options.StepReward, _options.NonTerminalDiscount);
        }

        if (result.IsDraw)
        {
            return new RlReward(_options.DrawReward, _options.TerminalDiscount);
        }

        if (result.WinnerId is null || perspectivePlayerId is null)
        {
            return new RlReward(_options.UnscoredTerminalReward, _options.TerminalDiscount);
        }

        bool perspectiveWon = result.WinnerId.Value.Equals(perspectivePlayerId.Value);
        return new RlReward(
            perspectiveWon ? _options.WinReward : _options.LossReward,
            _options.TerminalDiscount);
    }
}

public sealed record RlReward(double Reward, double Discount)
{
    public static RlReward ZeroStep { get; } = new(Reward: 0d, Discount: 1d);

    public static RlReward ZeroTerminal { get; } = new(Reward: 0d, Discount: 0d);
}

public sealed record RlRewardOptions
{
    public static RlRewardOptions Default { get; } = new();

    public double StepReward { get; init; }

    public double WinReward { get; init; } = 1d;

    public double LossReward { get; init; } = -1d;

    public double DrawReward { get; init; }

    public double UnscoredTerminalReward { get; init; }

    public double NonTerminalDiscount { get; init; } = 1d;

    public double TerminalDiscount { get; init; }
}
