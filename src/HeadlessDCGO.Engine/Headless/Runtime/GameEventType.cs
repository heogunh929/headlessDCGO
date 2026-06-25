namespace HeadlessDCGO.Engine.Headless.Runtime;

public enum GameEventType
{
    Unknown = 0,
    StateChanged,
    CardMoved,
    EffectQueued,
    EffectResolved,
    ChoiceRequested,
    ChoiceResolved,
    ChoiceCleared,
    ActionQueued,
    ActionProcessed,
    InvalidAction,
    AttackDeclared,
    AttackResolved,
    AttackCleared,
    SecurityCheck,
    SecuritySkill,
    DelayedTrigger,
    GameEnded
}
