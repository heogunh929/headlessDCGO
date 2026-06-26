namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// Single authoritative legality predicate shared by legal-action generation and action
/// acceptance (G3.5-RL-A1 / fixes P0-3). When a match is configured with an
/// <see cref="IActionLegality"/>, the acceptance boundary (<c>DcgoMatch.ApplyActionAsync</c>)
/// validates an incoming action with the same rule the dispatcher used to generate the legal set,
/// so "what is offered" and "what is accepted" can never diverge.
/// </summary>
public interface IActionLegality
{
    LegalityVerdict Validate(LegalAction action, EngineContext context);
}

/// <summary>Result of an <see cref="IActionLegality"/> check.</summary>
public sealed record LegalityVerdict(bool IsLegal, string Reason)
{
    public static LegalityVerdict Legal { get; } = new(true, string.Empty);

    public static LegalityVerdict Illegal(string reason)
    {
        return new LegalityVerdict(
            false,
            string.IsNullOrWhiteSpace(reason) ? "Action is not legal." : reason.Trim());
    }
}
