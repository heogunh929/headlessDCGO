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

    /// <summary>Photon <c>PhotonView.RPC</c>. DECLARED BUT NOT IMPLEMENTED — it throws.
    /// The AS-IS sources call this at 32 sites, all with <c>RpcTarget.All</c>, which in a single process is
    /// equivalent to invoking the target method locally once. Doing that dispatch is roadmap step 2.6; until
    /// then this throws rather than return quietly, so a rule path that reaches it FAILS LOUDLY instead of
    /// silently dropping a message. The declaration exists only because the AS-IS call sites must compile.</summary>
    public void RPC(string methodName, RpcTarget target, params object[] parameters) =>
        throw new NotSupportedException(
            $"RPC('{methodName}') is not dispatched: there is no Photon client and local dispatch is not " +
            "implemented yet (roadmap step 2.6). See Headless/Unity/PhotonPunBehaviours.cs.");

    public void RPC(string methodName, Realtime.Player targetPlayer, params object[] parameters) =>
        RPC(methodName, RpcTarget.All, parameters);
}

/// <summary>Photon <c>Photon.Pun.MonoBehaviourPun</c> — a MonoBehaviour with a cached <see cref="PhotonView"/>.
/// This declaration exists so mirror files can carry the AS-IS class declaration verbatim; it carries no Photon
/// behaviour.</summary>
public class MonoBehaviourPun : MonoBehaviour
{
    /// <summary>Photon <c>MonoBehaviourPun.photonView</c> — in PUN, the view component cached off this
    /// GameObject. Headless there is no GameObject and no Photon client, so this cannot be answered: it throws
    /// instead of returning a stand-in that would read as a live view. Lower-case to match the AS-IS spelling
    /// at the call sites.</summary>
    public PhotonView photonView =>
        throw new NotSupportedException(
            "There is no Photon client in the headless process, so there is no PhotonView. See " +
            "Headless/Unity/PhotonPunBehaviours.cs.");
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
