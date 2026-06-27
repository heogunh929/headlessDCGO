namespace HeadlessDCGO.Engine.Headless.Choices;

public enum ChoiceType
{
    Unknown = 0,
    Card,
    HandCard,
    Permanent,
    Count,
    AttackTarget,
    MainPhaseAction,
    OptionalEffect,
    Blocker,
    // N-5: the opening-hand mulligan decision (keep vs redraw), made per player before security is dealt.
    Mulligan
}
