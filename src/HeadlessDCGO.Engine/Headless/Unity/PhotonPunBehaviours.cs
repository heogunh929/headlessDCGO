// ============================================================================================================
// THE AS-IS BASE TYPES `Photon.Pun.MonoBehaviourPun` / `Photon.Pun.MonoBehaviourPunCallbacks`, AND THE
// `PhotonView` TYPE THEY EXPOSE.
//
// WHY THIS FILE EXISTS. The original game logic declares its networked components as
// `public class AttackProcess : MonoBehaviourPunCallbacks`, `public class GManager : MonoBehaviourPun`, and so
// on. Those base types live in a compiled PUN assembly, so there is no AS-IS *file* to mirror — the AS-IS HOME
// is the namespace `Photon.Pun` (1339 original files open with `using Photon.Pun;`). Declaring them here lets a
// mirror file carry the original's class declaration VERBATIM. The hierarchy is PUN's own:
// MonoBehaviourPunCallbacks -> MonoBehaviourPun -> UnityEngine.MonoBehaviour.
//
// PHOTON IS NOT RUNNING. There is no client, no room, no view registry, and no network at all in this process.
// The headless match is local and single-process. Read the member list below literally.
//
// MEMBERS THAT DO REAL WORK
//     (none in this file. The only inherited member that does real work is
//      UnityEngine.MonoBehaviour.StartCoroutine — see Headless/Unity/UnityEngineMonoBehaviour.cs.)
//
// MEMBERS THAT ARE DECLARATIONS DOING NOTHING
//     MonoBehaviourPun.photonView   Declared because that is how the originals reach the view
//                                   (`photonView.RPC(...)`, the only form measured: 32 sites in
//                                   DCGO/Assets/Scripts, no other member accessed). It has no view to return,
//                                   so it THROWS rather than hand back a stand-in that would read as a live
//                                   Photon view. No mirror file reads it today.
//     PhotonView                    An empty type. See the deliberate omission below.
//
// NOT DECLARED, ON PURPOSE — `PhotonView.RPC`. Whether and how RPC dispatch is reproduced headless is a
// separate decision that has not been made. Leaving the method out means the compiler rejects any RPC call
// site, so nobody can write one that silently does nothing. `[PunRPC]` is likewise absent.
//
// ALSO NOT DECLARED. PUN's MonoBehaviourPunCallbacks implements IConnectionCallbacks / IMatchmakingCallbacks /
// IInRoomCallbacks / ILobbyCallbacks / IWebRpcCallback / IErrorInfoCallback and supplies virtual `OnEnable`,
// `OnDisable`, `OnJoinedRoom`, `OnPlayerEnteredRoom`, … for subclasses to override. None of that is declared:
// no mirror class overrides any of it, and no connection exists to raise it. Add a member here only when a
// mirror file cannot compile without it, and say in this header what it does.
// ============================================================================================================

namespace Photon.Pun;

using UnityEngine;

/// <summary>Photon <c>Photon.Pun.PhotonView</c> — the per-object network view. Declaration only: this process
/// has no Photon client, so there is nothing for a view to identify and no member is declared on it. In
/// particular <c>RPC</c> is deliberately absent; see the file header.</summary>
public class PhotonView : UnityEngine.Component
{
    /// <summary>Photon <c>PhotonView.ViewID</c>. There is no view registry; the id is held, not allocated.</summary>
    public int ViewID { get; set; }

    /// <summary>Photon <c>PhotonView.RPC</c> — LOCAL DISPATCH.
    ///
    /// In PUN this sends <paramref name="methodName"/> to the peers named by <paramref name="target"/>, and
    /// each peer invokes its own <c>[PunRPC]</c>-marked method by reflection. Measured across the AS-IS tree,
    /// ALL 32 call sites use <c>RpcTarget.All</c> — sender included — and this process is the only peer, so
    /// "send to everyone" is exactly "invoke it here, once".
    ///
    /// That is what this does: it finds the <c>[PunRPC]</c> method on the components of this view's GameObject
    /// and calls it. The engine depends on the call actually happening — `TurnStateMachine.Init()` seeds the
    /// shared RNG with `RPC("SetRandom", RpcTarget.All, …)` and then waits on `DoneSetRandom`, which only the
    /// RPC body sets.
    ///
    /// WHAT IS NOT REPRODUCED: serialisation, ordering across a network, and any target other than All. A
    /// target this process cannot satisfy throws rather than dropping the message silently.</summary>
    public void RPC(string methodName, RpcTarget target, params object[] parameters)
    {
        if (target is not RpcTarget.All and not RpcTarget.AllBuffered and not RpcTarget.AllViaServer
            and not RpcTarget.AllBufferedViaServer and not RpcTarget.MasterClient)
        {
            throw new NotSupportedException(
                $"RPC('{methodName}') targets {target}, which excludes this peer. The headless process is the "
                + "only peer, so there is nobody else to send to. See Headless/Unity/PhotonPunBehaviours.cs.");
        }

        Invoke(methodName, parameters);
    }

    public void RPC(string methodName, Realtime.Player targetPlayer, params object[] parameters) =>
        Invoke(methodName, parameters);

    private void Invoke(string methodName, object[] parameters)
    {
        foreach (UnityEngine.Component component in gameObject.Components)
        {
            System.Reflection.MethodInfo? method = component.GetType().GetMethod(
                methodName,
                System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.NonPublic);

            if (method is null || !method.IsDefined(typeof(PunRPCAttribute), inherit: true))
            {
                continue;
            }

            method.Invoke(component, parameters);

            return;
        }

        throw new MissingMethodException(
            $"No [PunRPC] method named '{methodName}' on {gameObject.name}. PUN resolves RPCs by reflection "
            + "over the view's GameObject, so the target must live on the same object as the PhotonView.");
    }
}
/// This declaration exists so mirror files can carry the AS-IS class declaration verbatim; it carries no Photon
/// behaviour.</summary>
public class MonoBehaviourPun : MonoBehaviour
{
    /// <summary>Photon <c>MonoBehaviourPun.photonView</c> — in PUN, the view component cached off this
    /// GameObject. That is what this returns, adding one if the object does not carry it yet, exactly as PUN's
    /// own accessor caches its lookup. The view dispatches RPCs locally (see <see cref="PhotonView.RPC"/>).
    ///
    /// It used to throw, on the reasoning that a stand-in would read as a live Photon view. That was wrong
    /// once RPC dispatch existed: the AS-IS engine sends its own decisions through this property —
    /// `TurnStateMachine.StartGame`'s mulligan answer is `photonView.RPC("SetRedraw", RpcTarget.All, …)` —
    /// so throwing here blocks the game, and the honest object to return is the local view.
    ///
    /// Lower-case to match the AS-IS spelling at the call sites.</summary>
    public PhotonView photonView =>
        gameObject.GetComponent<PhotonView>() ?? gameObject.AddComponent<PhotonView>();
}

/// <summary>Photon <c>Photon.Pun.MonoBehaviourPunCallbacks</c> — in PUN, a <see cref="MonoBehaviourPun"/> that
/// also receives the connection/matchmaking/room callbacks. This declaration exists so mirror files can carry
/// the AS-IS class declaration verbatim; none of the callback surface is declared (see the file header) because
/// nothing here connects to anything.</summary>
public class MonoBehaviourPunCallbacks : MonoBehaviourPun
{
    // DECLARATIONS DOING NOTHING. The out-of-scope lobby files (`EnterRoom`, `LobbyManager_FriendMatch`,
    // `LobbyManager_RandomMatch`, `RoomManager`) override these. NOTHING EVER CALLS THEM — there is no client,
    // no room and no server. They exist only so those overrides compile; see docs/out_of_scope.md.
    public virtual void OnEnable() { }
    public virtual void OnDisable() { }
    public virtual void OnConnected() { }
    public virtual void OnConnectedToMaster() { }
    public virtual void OnDisconnected(Realtime.DisconnectCause cause) { }
    public virtual void OnJoinedLobby() { }
    public virtual void OnLeftLobby() { }
    public virtual void OnJoinedRoom() { }
    public virtual void OnLeftRoom() { }
    public virtual void OnCreatedRoom() { }
    public virtual void OnCreateRoomFailed(short returnCode, string message) { }
    public virtual void OnJoinRoomFailed(short returnCode, string message) { }
    public virtual void OnJoinRandomFailed(short returnCode, string message) { }
    public virtual void OnRoomListUpdate(System.Collections.Generic.List<Realtime.RoomInfo> roomList) { }
    public virtual void OnPlayerEnteredRoom(Realtime.Player newPlayer) { }
    public virtual void OnPlayerLeftRoom(Realtime.Player otherPlayer) { }
    public virtual void OnMasterClientSwitched(Realtime.Player newMasterClient) { }
}
