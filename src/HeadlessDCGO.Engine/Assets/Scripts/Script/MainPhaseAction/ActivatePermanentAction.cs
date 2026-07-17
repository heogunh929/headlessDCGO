// Source: DCGO/Assets/Scripts/Script/MainPhaseAction/ActivatePermanentAction.cs (Photon transport stripped —
// see MainPhaseAction.cs).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public class ActivatePermanentAction : MainPhaseAction
{
    int PermanentIndex;
    int SkillIndex;

    public ActivatePermanentAction(int permanentIndex, int skillIndex)
    {
        PermanentIndex = permanentIndex;
        SkillIndex = skillIndex;
    }

    public override Task Execute(TurnStateMachine stateMachine)
    {
        stateMachine.SetActSkill(PermanentIndex, SkillIndex);
        return Task.CompletedTask;
    }
}
