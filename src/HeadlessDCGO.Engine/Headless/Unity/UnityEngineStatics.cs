// ============================================================================================================
// AS-IS STATIC ENTRY POINTS: `Object.Instantiate`/`Destroy`, `Time`, `Input`, `Application`, `Screen`,
// `PlayerPrefs`, `Gizmos`, `GUIUtility`, `Random`, and Photon's `PhotonNetwork`/`Protocol`.
//
// WHY THIS FILE EXISTS. The AS-IS sources call these as bare statics inherited from `UnityEngine.Object` or
// reached through the engine's global classes. They live in compiled assemblies, so the AS-IS HOME is the
// namespace, not a file.
//
// ONE MEMBER HERE DOES REAL WORK, AND IT IS LOAD-BEARING
//
//     Object.Instantiate<T>(T)   `CardObjectController.cs:351` creates EVERY card with
//                                `CardSource cardSource = Instantiate(GManager.instance.CardPrefab, …)`, and
//                                :495 / :651 create the field and hand objects the same way. If this returned
//                                null or threw, no card could exist.
//
//                                WHAT IT DOES: constructs a fresh instance of the original's runtime type on a
//                                fresh GameObject, and reparents it if a parent was given.
//                                WHAT IT DOES NOT DO: copy the original's field values. Unity clones a PREFAB —
//                                an asset whose serialized fields were set in the editor. Those assets are not
//                                in this process, so there is nothing to copy from; a prefab reference here is
//                                whatever the bootstrap put in that field. Supplying prefab data is roadmap
//                                step 2.1 (lifecycle/bootstrap), NOT this file's job. Until then a card comes
//                                back with default fields, which is the honest result of having no prefab.
//
// MEMBERS THAT ARE DECLARATIONS DOING NOTHING
//
//     Object.Destroy             Detaches the target from its parent so the hierarchy stops reporting it, and
//                                nothing else. Measured rule-core uses are display cleanup
//                                (`Destroy(HandTransform.GetChild(i).gameObject)`, `Destroy(targetArrow…)`).
//                                NOTE the game's own destruction is a different thing entirely — the AS-IS
//                                `DestroyPermanentsClass` coroutine — and is untouched by this.
//     Time / Input / Screen / Application / PlayerPrefs / Gizmos / GUIUtility / KeyCode
//                                There is no frame clock, no input device, no screen, no player prefs store and
//                                no gizmo pass. Every reader of these is presentation or out-of-scope.
//     PhotonNetwork / Protocol   There is no Photon client. `IsConnected` is false and stays false; nothing
//                                connects, joins or sends.
//     Random                     Declared so the sources compile. Seeding it and routing every draw through a
//                                deterministic source is roadmap step 3.1 — until then this is
//                                `System.Random` with an unspecified seed and MUST NOT be relied on for
//                                reproducibility.
//
// Add a member here only when an AS-IS file cannot compile without it, and say which list it belongs to.
// ============================================================================================================

namespace UnityEngine
{
    using System;
    using System.Collections.Generic;

    /// <summary>The static half of <see cref="Object"/>: creation and destruction. Declared as a partial-style
    /// extension of the same type is not possible across files without `partial`, so these live on a static
    /// helper the AS-IS call sites reach through inheritance from <see cref="Component"/>.</summary>
    public partial class Object
    {
        /// <summary>Unity <c>Object.Instantiate&lt;T&gt;</c>. LOAD-BEARING — see the file header for exactly
        /// what is and is not copied.</summary>
        public static T Instantiate<T>(T original) where T : Object
        {
            ArgumentNullException.ThrowIfNull(original);

            return (T)CreateLike(original);
        }

        public static T Instantiate<T>(T original, Transform? parent) where T : Object
        {
            T created = Instantiate(original);

            if (parent is not null)
            {
                HostOf(created)?.transform.SetParent(parent);
            }

            return created;
        }

        public static T Instantiate<T>(T original, Transform? parent, bool worldPositionStays) where T : Object
            => Instantiate(original, parent);

        public static T Instantiate<T>(T original, Vector3 position, Quaternion rotation) where T : Object
            => Instantiate(original);

        public static T Instantiate<T>(T original, Vector3 position, Quaternion rotation, Transform? parent)
            where T : Object => Instantiate(original, parent);

        /// <summary>Unity <c>Object.Destroy</c>. QUEUES the release for end of the current driver tick, as
        /// Unity applies destruction at end of frame. The deferral is load-bearing, not cosmetic: AS-IS
        /// iteration relies on the hierarchy staying intact until the frame ends —
        /// `SelectCommandPanel.cs:45-48` runs `for (i &lt; childCount) Destroy(GetChild(i))` then parks on
        /// `WaitWhile(childCount &gt; 0)`; a synchronous detach slides the indices, every other child
        /// survives, and the wait never ends (measured 2026-07-30: End-Selection button never rebuilt →
        /// wired-selection loops, step_cap 34%). The release itself is REAL (see
        /// <see cref="FlushPendingDestroys"/>) — per-tick, so the in-match memory release behind the
        /// 22:36-22:40 OOM repair is preserved.</summary>
        public static void Destroy(Object? target)
        {
            if (target is null)
            {
                return;
            }

            _pendingDestroys.Add(target);
        }

        private static readonly List<Object> _pendingDestroys = new();

        /// <summary>Applies this frame's queued destroys — called by <c>CoroutineDriver.Tick()</c> at end of
        /// tick (Unity's end-of-frame) and by scene teardown as a backstop. Index-based: applying one destroy
        /// never enqueues another today, but growth during the walk must not throw.</summary>
        public static void FlushPendingDestroys()
        {
            for (int i = 0; i < _pendingDestroys.Count; i++)
            {
                ApplyDestroy(_pendingDestroys[i]);
            }

            _pendingDestroys.Clear();
        }

        /// <summary>Detach + REAL release: the whole subtree is marked destroyed, unregistered from the
        /// teardown census (GameObject.Registry — holding destroyed objects there until teardown was the
        /// in-match accumulator behind the 22:36-22:40 OOMs), and its components' coroutines stop (Unity
        /// stops a destroyed object's coroutines). Static-anchor purge runs so a destroyed subscriber
        /// (FieldPermanentCard's never-unsubscribed GManager events) cannot pin the freed graph until
        /// teardown. A Component target removes just that component, as Unity does. The game's own
        /// destruction flow (<c>DestroyPermanentsClass</c>) is AS-IS logic and untouched.</summary>
        private static void ApplyDestroy(Object? target)
        {
            if (target is Component component && target is not GameObject)
            {
                component.gameObject.Remove(component);
                component.DestroyedByTeardown = true;
                (component as MonoBehaviour)?.StopRunningCoroutines();
                HeadlessDCGO.Engine.Headless.Bootstrap.HeadlessScene.PurgeStaticAnchors();

                return;
            }

            GameObject? host = HostOf(target);

            if (host is null)
            {
                return;
            }

            host.transform.SetParent(null);
            DestroySubtree(host);
            HeadlessDCGO.Engine.Headless.Bootstrap.HeadlessScene.PurgeStaticAnchors();
        }

        private static void DestroySubtree(GameObject host)
        {
            for (int i = host.transform.childCount - 1; i >= 0; i--)
            {
                DestroySubtree(host.transform.GetChild(i).gameObject);
            }

            host.DestroyedByTeardown = true;

            foreach (Component component in host.Components)
            {
                component.DestroyedByTeardown = true;
                (component as MonoBehaviour)?.StopRunningCoroutines();
            }

            GameObject.Unregister(host);
        }

        public static void Destroy(Object? target, float delay) => Destroy(target);

        /// <summary>Unity <c>DestroyImmediate</c> applies NOW, not at end of frame — that is its contract.</summary>
        public static void DestroyImmediate(Object? target) => ApplyDestroy(target);

        /// <summary>Unity <c>Object.DontDestroyOnLoad</c>. There are no scenes to survive.</summary>
        public static void DontDestroyOnLoad(Object? target)
        {
        }

        /// <summary>Unity <c>Object.FindObjectOfType</c>. There is no scene graph to search.</summary>
        public static T? FindObjectOfType<T>() where T : Object => null;

        public static T[] FindObjectsOfType<T>() where T : Object => Array.Empty<T>();

        private static Object CreateLike(Object original)
        {
            Type type = original.GetType();

            if (type == typeof(GameObject))
            {
                GameObject copy = new(original.name);
                InstantiatedObject?.Invoke(copy);

                return copy;
            }

            object created = Activator.CreateInstance(type)
                ?? throw new InvalidOperationException($"Could not construct {type.FullName}.");

            var createdObject = (Object)created;
            createdObject.name = original.name;

            if (createdObject is Component component)
            {
                _ = component.gameObject;   // materialise the host GameObject, which raises HostCreated
                Instantiated?.Invoke(component);
            }

            return createdObject;
        }

        /// <summary>Raised for every component produced by <see cref="Instantiate{T}(T)"/>. The scene
        /// subscribes so a copy gets the widget components and inspector slots the ORIGINAL PREFAB carried —
        /// a Unity prefab is a fully built object, and `Instantiate` here can only construct a bare one.
        /// `CardObjectController.CreateCardSource` depends on it: the line after `Instantiate` is
        /// `cardSource.cEntity_EffectController.AddCardEffect(...)`, unguarded.</summary>
        public static event Action<Component>? Instantiated;

        /// <summary>Raised for a GameObject produced by <see cref="Instantiate{T}(T)"/>. Same purpose as
        /// <see cref="Instantiated"/>: a prefab copy is a fully built object, and this one is not.</summary>
        public static event Action<GameObject>? InstantiatedObject;

        private static GameObject? HostOf(Object? target) => target switch
        {
            GameObject go => go,
            Component c => c.gameObject,
            _ => null,
        };
    }

    /// <summary>Unity <c>Time</c>. There is no wall clock; the driver tick IS the frame, so
    /// <see cref="deltaTime"/> reports one 60fps frame per tick.
    ///
    /// It must NOT be 0: Unity's deltaTime never is, and the AS-IS auto-close idiom
    /// `timer += Time.deltaTime` (Effects.HideShowCard :1015 / HideShowCard2 :1139, the main-phase-end
    /// backstop TurnStateMachine.cs:1267) then never advances — measured 2026-07-29: the show-card panel
    /// never auto-closed and every DigiXros match stalled in `AddDigivolutiuonCards` waiting on it
    /// (8/10 매치). With one frame per tick the 2.5s auto-close arrives after 150 ticks, which is the wait
    /// the AS-IS shows a human. No rule reads deltaTime — the in-scope users are these timers.</summary>
    public static class Time
    {
        public static float time => 0f;
        public static float deltaTime => 1f / 60f;
        public static float unscaledTime => 0f;
        public static float unscaledDeltaTime => deltaTime;
        public static float fixedDeltaTime => deltaTime;
        public static float realtimeSinceStartup => 0f;
        public static float timeScale { get; set; } = 1f;
        public static int frameCount => 0;
    }

    /// <summary>Unity <c>Input</c> and <c>KeyCode</c>. There is no input device.</summary>
    public static class Input
    {
        public static Vector3 mousePosition => Vector3.zero;
        public static bool anyKey => false;
        public static bool anyKeyDown => false;
        public static string inputString => string.Empty;
        public static int touchCount => 0;

        public static bool GetKey(KeyCode key) => false;

        public static bool GetKeyDown(KeyCode key) => false;

        public static bool GetKeyUp(KeyCode key) => false;

        public static bool GetMouseButton(int button) => false;

        public static bool GetMouseButtonDown(int button) => false;

        public static bool GetMouseButtonUp(int button) => false;

        public static float GetAxis(string axisName) => 0f;

        public static float GetAxisRaw(string axisName) => 0f;
    }

    public enum KeyCode
    {
        None = 0,
        Backspace = 8,
        Tab = 9,
        Return = 13,
        Escape = 27,
        Space = 32,
        Delete = 127,
        UpArrow = 273,
        DownArrow = 274,
        RightArrow = 275,
        LeftArrow = 276,
        F1 = 282,
        F2 = 283,
        F3 = 284,
        F4 = 285,
        Alpha0 = 48,
        Alpha1 = 49,
        Alpha2 = 50,
        Alpha3 = 51,
        A = 97,
        C = 99,
        D = 100,
        E = 101,
        Q = 113,
        R = 114,
        S = 115,
        V = 118,
        W = 119,
        Z = 122,
        LeftControl = 306,
        LeftShift = 304,
        Mouse0 = 323,
        Mouse1 = 324,
        L = 108,
        Equals = 61,
        Plus = 43,
        T = 116,
        Minus = 45,
        LeftAlt = 308,
    }

    /// <summary>Unity <c>Screen</c>. There is no screen.</summary>
    public static class Screen
    {
        public static int width => 0;
        public static int height => 0;
        public static bool fullScreen { get; set; }

        public static void SetResolution(int width, int height, bool fullscreen)
        {
        }
    }

    /// <summary>Unity <c>Application</c>. There is no player.</summary>
    public static class Application
    {
        public static string dataPath => AppContext.BaseDirectory;
        public static string persistentDataPath => AppContext.BaseDirectory;
        public static string streamingAssetsPath => AppContext.BaseDirectory;
        public static string version => "0.0.0";
        public static bool isPlaying => true;
        public static bool isEditor => false;
        public static RuntimePlatform platform => RuntimePlatform.LinuxPlayer;
        public static int targetFrameRate { get; set; } = -1;
        public static NetworkReachability internetReachability => NetworkReachability.NotReachable;

        public static void Quit()
        {
        }

        public static void OpenURL(string url)
        {
        }
    }

    public enum RuntimePlatform
    {
        WindowsPlayer = 2,
        OSXPlayer = 1,
        LinuxPlayer = 13,
        Android = 11,
        IPhonePlayer = 8,
        WebGLPlayer = 17,
    }

    /// <summary>Unity <c>PlayerPrefs</c>. There is no preferences store; reads return the supplied default.</summary>
    public static class PlayerPrefs
    {
        private static readonly Dictionary<string, object> Values = new(StringComparer.Ordinal);

        public static int GetInt(string key, int defaultValue = 0)
            => Values.TryGetValue(key, out object? v) && v is int i ? i : defaultValue;

        public static float GetFloat(string key, float defaultValue = 0f)
            => Values.TryGetValue(key, out object? v) && v is float f ? f : defaultValue;

        public static string GetString(string key, string defaultValue = "")
            => Values.TryGetValue(key, out object? v) && v is string s ? s : defaultValue;

        public static void SetInt(string key, int value) => Values[key] = value;

        public static void SetFloat(string key, float value) => Values[key] = value;

        public static void SetString(string key, string value) => Values[key] = value;

        public static bool HasKey(string key) => Values.ContainsKey(key);

        public static void DeleteKey(string key) => Values.Remove(key);

        public static void DeleteAll() => Values.Clear();

        public static void Save()
        {
        }
    }

    /// <summary>Unity <c>Gizmos</c> / <c>GUIUtility</c>. Editor drawing; nothing draws.</summary>
    public static class Gizmos
    {
        public static Color color { get; set; }
        public static Matrix4x4 matrix { get; set; }

        public static void DrawLine(Vector3 from, Vector3 to)
        {
        }

        public static void DrawWireSphere(Vector3 center, float radius)
        {
        }

        public static void DrawSphere(Vector3 center, float radius)
        {
        }

        public static void DrawWireCube(Vector3 center, Vector3 size)
        {
        }
    }

    public static class GUIUtility
    {
        public static string systemCopyBuffer { get; set; } = string.Empty;
    }

    /// <summary>Unity <c>Random</c>. Seedable via the real Unity API <see cref="InitState"/> — the
    /// determinism harness calls it alongside <c>GameRandom.Seed</c> (roadmap step 3.1, see
    /// Headless/Determinism/MatchSeed.cs). Census 2026-07-29: every in-scope gameplay caller of this class is
    /// dead under self-play wiring (the `IsAI && !isYou` branches) or deterministic by construction
    /// (`Range(0,1)` on a one-deck collection); the live remainder is presentation (SecurityBreakGlass debris,
    /// BGM pick behind an empty-list guard). Seeding is therefore a safety net, not a load-bearing fix.</summary>
    public static class Random
    {
        private static System.Random Source = new();

        public static int Range(int minInclusive, int maxExclusive) => Source.Next(minInclusive, maxExclusive);

        public static float Range(float minInclusive, float maxInclusive)
            => minInclusive + ((float)Source.NextDouble() * (maxInclusive - minInclusive));

        public static float value => (float)Source.NextDouble();

        /// <summary>Unity <c>Random.InitState(int)</c> — restarts the sequence from a seed, as Unity does.</summary>
        public static void InitState(int seed) => Source = new System.Random(seed);
    }

    /// <summary>Unity <c>Color32</c>. Display only; the game's card colour is the AS-IS <c>CardColor</c>.</summary>
    public struct Color32(byte r, byte g, byte b, byte a)
    {
        public byte r = r;
        public byte g = g;
        public byte b = b;
        public byte a = a;

        public static implicit operator Color(Color32 c) => new(c.r / 255f, c.g / 255f, c.b / 255f, c.a / 255f);

        public static implicit operator Color32(Color c)
            => new((byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f), (byte)(c.a * 255f));
    }

    /// <summary>Unity <c>BoxCollider</c>. There is no physics.</summary>
    public sealed class BoxCollider : Collider
    {
        public Vector3 center { get; set; }
        public Vector3 size { get; set; }
    }
}

namespace Photon.Pun
{
    using System.Collections.Generic;
    using ExitGames.Client.Photon;
    using UnityEngine;

    /// <summary>Photon <c>PhotonNetwork</c> — a LOCAL SINGLE-PROCESS ROOM.
    ///
    /// The AS-IS engine runs even its vs-AI matches inside a Photon room: `TurnStateMachine.Init()` connects,
    /// joins a lobby, creates a room and waits on `IsConnectedAndReady` / `InLobby` / `InRoom` before the game
    /// starts. A shim that reports "never connected" leaves those waits unsatisfiable and the match never
    /// begins, so this one reports a room that is always there — two seats, both local.
    ///
    /// WHY THAT IS FAITHFUL AND NOT A SKIP. The AS-IS code path is executed in full: it connects, it joins, it
    /// creates, and every predicate it waits on is answered. What is not reproduced is the NETWORK — there is
    /// no server, no latency and no second process. For this engine that costs nothing measurable: every one
    /// of the 32 `photonView.RPC` sites targets `RpcTarget.All`, which in a single process is one local call
    /// (and RPC dispatch itself is still unimplemented and throws — see
    /// Headless/Unity/PhotonPunBehaviours.cs).
    ///
    /// SEATS. `LocalPlayer` is actor 1 and master; the opponent is actor 2. `CardObjectController.DeckRecipie`
    /// splits decks by comparing against the master player, so the two must be distinguishable.</summary>
    public static class PhotonNetwork
    {
        private static readonly Realtime.Player Master = new()
        {
            ActorNumber = 1, NickName = "You", IsLocal = true, IsMasterClient = true, UserId = "local-1",
        };

        private static readonly Realtime.Player Guest = new()
        {
            ActorNumber = 2, NickName = "Opponent", IsLocal = false, IsMasterClient = false, UserId = "local-2",
        };

        private static readonly Realtime.Room LocalRoom = CreateLocalRoom();

        // Always connected, always in a lobby, always in the room: the AS-IS waits are satisfiable from the
        // first tick, which is what lets Init() proceed.
        public static bool IsConnected => true;
        public static bool IsConnectedAndReady => true;
        public static bool InLobby => true;
        public static bool InRoom => true;
        public static bool IsMasterClient => true;
        public static bool OfflineMode { get; set; } = true;
        public static bool AutomaticallySyncScene { get; set; }
        public static string GameVersion { get; set; } = string.Empty;
        public static string NickName { get; set; } = "You";
        public static string CloudRegion => "local";
        public static Realtime.Room CurrentRoom => LocalRoom;
        public static Realtime.Player LocalPlayer => Master;
        public static Realtime.Player MasterClient => Master;
        public static Realtime.Player[] PlayerList => new[] { Master, Guest };
        public static int CountOfPlayers => 2;
        public static LoadBalancingClient NetworkingClient { get; } = new();
        public static ServerSettings PhotonServerSettings { get; } = new();
        public static Realtime.ClientState NetworkClientState => Realtime.ClientState.Joined;
        public static Realtime.DisconnectCause DisconnectedCause => Realtime.DisconnectCause.None;
        public static double Time => 0d;
        public static int ServerTimestamp => 0;

        // The connect/join/create calls succeed immediately; the state above never changes.
        public static bool ConnectUsingSettings() => true;

        public static bool ConnectToRegion(string region) => true;

        public static bool Disconnect() => false;

        public static bool JoinLobby() => true;

        public static bool LeaveLobby() => true;

        public static bool JoinRoom(string roomName) => true;

        public static bool JoinRandomRoom() => true;

        public static bool CreateRoom(
            string? roomName,
            Realtime.RoomOptions? roomOptions = null,
            object? typedLobby = null,
            string[]? expectedUsers = null) => true;

        public static bool LeaveRoom(bool becomeInactive = true) => true;

        public static void LoadLevel(string levelName)
        {
        }

        public static void LoadLevel(int levelNumber)
        {
        }

        public static GameObject? Instantiate(string prefabName, Vector3 position, Quaternion rotation) => null;

        /// <summary>PUN destroys a networked object — locally that is exactly <c>Object.Destroy</c>.</summary>
        public static void Destroy(GameObject? targetGo) => Object.Destroy(targetGo);

        public static void SetMasterClient(Realtime.Player masterClientPlayer)
        {
        }

        public static void RemoveCallbackTarget(object target)
        {
        }

        public static void AddCallbackTarget(object target)
        {
        }

        private static Realtime.Room CreateLocalRoom()
        {
            Realtime.Room room = new()
            {
                Name = "local", MaxPlayers = 2, PlayerCount = 2, IsOpen = true, IsVisible = false,
                MasterClientId = 1,
            };

            room.Players[Master.ActorNumber] = Master;
            room.Players[Guest.ActorNumber] = Guest;

            return room;
        }
    }

    /// <summary>Photon <c>LoadBalancingClient</c>. There is no client; the state mirrors the local room.</summary>
    public sealed class LoadBalancingClient
    {
        public Realtime.ClientState State => Realtime.ClientState.Joined;
        public Realtime.DisconnectCause DisconnectedCause => Realtime.DisconnectCause.None;
        public string AppId { get; set; } = string.Empty;
    }

    /// <summary>Photon <c>Protocol</c> — the serialisation helpers. Nothing is serialised.</summary>
    public static class Protocol
    {
        public static byte[] Serialize(object? obj) => System.Array.Empty<byte>();

        public static object? Deserialize(byte[] bytes) => null;
    }
}

namespace Photon.Realtime
{
    using System.Collections.Generic;

    /// <summary>Photon <c>Room</c>. There is no room.</summary>
    public class Room : RoomInfo
    {
        public int MasterClientId { get; set; }

        public Dictionary<int, Player> Players { get; } = new();

        public void SetCustomProperties(ExitGames.Client.Photon.Hashtable propertiesToSet)
        {
        }
    }
}
