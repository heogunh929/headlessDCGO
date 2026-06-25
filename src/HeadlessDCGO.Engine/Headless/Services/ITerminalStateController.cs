namespace HeadlessDCGO.Engine.Headless.Services;

// TODO: Replace with real winner/loser terminal state control when rule flow is ported.
public interface ITerminalStateController
{
    void SetTerminal(bool isTerminal);
}
