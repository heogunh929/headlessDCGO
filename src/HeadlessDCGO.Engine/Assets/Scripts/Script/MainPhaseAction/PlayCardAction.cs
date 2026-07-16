// Source: DCGO/Assets/Scripts/Script/MainPhaseAction/PlayCardAction.cs (Photon transport stripped — see
// MainPhaseAction.cs).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public class PlayCardAction : MainPhaseAction
{
    int CardIndex;
    int TargetFrameID;
    int[]? JogressEvoRootsFrameIDs;
    int BurstTamerFrameID;
    int[]? AppFusionFrameIDs;

    public PlayCardAction(int cardIndex, int targetFrameID, int[]? jogressEvoRootsFrameIDs, int burstTamerFrameID, int[]? appFusionFrameIDs)
    {
        CardIndex = cardIndex;
        TargetFrameID = targetFrameID;
        JogressEvoRootsFrameIDs = jogressEvoRootsFrameIDs;
        BurstTamerFrameID = burstTamerFrameID;
        AppFusionFrameIDs = appFusionFrameIDs;
    }

    public override Task Execute(TurnStateMachine stateMachine)
    {
        stateMachine.SetPlayCard(CardIndex, TargetFrameID, JogressEvoRootsFrameIDs, BurstTamerFrameID, AppFusionFrameIDs);
        return Task.CompletedTask;
    }
}
