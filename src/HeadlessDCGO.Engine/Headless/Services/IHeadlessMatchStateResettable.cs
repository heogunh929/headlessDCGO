namespace HeadlessDCGO.Engine.Headless.Services;

// TODO: Replace with concrete match-state lifecycle once full GameContext state is ported.
public interface IHeadlessMatchStateResettable
{
    void ResetMatchState();
}
