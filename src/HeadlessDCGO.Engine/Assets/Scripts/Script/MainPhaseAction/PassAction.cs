// Source: DCGO/Assets/Scripts/Script/MainPhaseAction/PassAction.cs (Photon transport stripped — see
// MainPhaseAction.cs).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public class PassAction : MainPhaseAction
{
    public PassAction()
    {
    }

    public override async Task Execute(TurnStateMachine stateMachine)
    {
        // AS-IS PassTurn fire-and-forgets EndTurnProcess (StartCoroutine); the async translation awaits it
        // inline — same single-threaded interleaving outcome (the wait loop idles until the phase leaves Main).
        await stateMachine.PassTurn();
    }
}
