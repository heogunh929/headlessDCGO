// Source: DCGO/Assets/Scripts/Script/MainPhaseAction/MainPhaseAction.cs
// (R4 S3b) The AS-IS main-phase intent packet base: the TurnStateMachine selection wait (:971-1170) dequeues
// these from the turn player's queue and Execute writes the TSM intent fields (PlayCard / UseCardEffect /
// AttackingPermanent / ...). ADAPTATION: the AS-IS IGamePacket half (Photon Serialize/Deserialize — network
// transport) is stripped; the headless transport is the agent LegalAction the TurnFlowDriver converts
// (Headless/Runtime/TurnFlowDriver.cs). Namespace = ...CardEffectCommons alongside TurnStateMachine (the
// AS-IS classes live in the global namespace; the mirror follows the TurnStateMachine.cs precedent).
// CheatAction is NOT mirrored — the headless boundary rejects cheat/debug actions outright (CheatActionGuard).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public abstract class MainPhaseAction
{
    public abstract Task Execute(TurnStateMachine stateMachine);
}
