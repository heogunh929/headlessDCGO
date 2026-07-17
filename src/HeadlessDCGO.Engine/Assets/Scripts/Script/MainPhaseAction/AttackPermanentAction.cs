// Source: DCGO/Assets/Scripts/Script/MainPhaseAction/AttackPermanentAction.cs (Photon transport stripped —
// see MainPhaseAction.cs).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public class AttackPermanentAction : MainPhaseAction
{
    int PermanentIndex;
    int AttackTargetPermanentIndex;

    public AttackPermanentAction(int permanentIndex, int attackTargetPermanentIndex)
    {
        PermanentIndex = permanentIndex;
        AttackTargetPermanentIndex = attackTargetPermanentIndex;
    }

    public override Task Execute(TurnStateMachine stateMachine)
    {
        // AS-IS method-name typo ("Permaent") preserved — mechanical mirror.
        stateMachine.SetAttackingPermaent(PermanentIndex, AttackTargetPermanentIndex);
        return Task.CompletedTask;
    }
}
