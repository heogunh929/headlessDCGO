// Source: DCGO/Assets/Scripts/Script/MainPhaseAction/ActivateCardAction.cs (Photon transport stripped — see
// MainPhaseAction.cs).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public class ActivateCardAction : MainPhaseAction
{
    int CardIndex;
    int SkillIndex;

    public ActivateCardAction(int cardIndex, int skillIndex)
    {
        CardIndex = cardIndex;
        SkillIndex = skillIndex;
    }

    public override Task Execute(TurnStateMachine stateMachine)
    {
        stateMachine.SetActCardSkill(CardIndex, SkillIndex);
        return Task.CompletedTask;
    }
}
