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

        /// <summary>Unity <c>Object.Destroy</c>. Detaches from the hierarchy; nothing more. The game's own
        /// destruction is the AS-IS <c>DestroyPermanentsClass</c> coroutine and is untouched by this.</summary>
        public static void Destroy(Object? target)
        {
            HostOf(target)?.transform.SetParent(null);
        }

        public static void Destroy(Object? target, float delay) => Destroy(target);

        public static void DestroyImmediate(Object? target) => Destroy(target);

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
                return new GameObject(original.name);
            }

            object created = Activator.CreateInstance(type)
                ?? throw new InvalidOperationException($"Could not construct {type.FullName}.");

            var createdObject = (Object)created;
            createdObject.name = original.name;

            if (createdObject is Component component)
            {
                _ = component.gameObject;   // materialise the host GameObject
            }

            return createdObject;
        }

        private static GameObject? HostOf(Object? target) => target switch
        {
            GameObject go => go,
            Component c => c.gameObject,
            _ => null,
        };
    }

    /// <summary>Unity <c>Time</c>. There is no frame clock.</summary>
    public static class Time
    {
        public static float time => 0f;
        public static float deltaTime => 0f;
        public static float unscaledTime => 0f;
        public static float unscaledDeltaTime => 0f;
        public static float fixedDeltaTime => 0f;
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

    /// <summary>Unity <c>Random</c>. NOT SEEDED — reproducibility is roadmap step 3.1; see the file header.</summary>
    public static class Random
    {
        private static readonly System.Random Source = new();

        public static int Range(int minInclusive, int maxExclusive) => Source.Next(minInclusive, maxExclusive);

        public static float Range(float minInclusive, float maxInclusive)
            => minInclusive + ((float)Source.NextDouble() * (maxInclusive - minInclusive));

        public static float value => (float)Source.NextDouble();

        public static void InitState(int seed)
        {
        }
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

    /// <summary>Photon <c>PhotonNetwork</c>. There is no client — <see cref="IsConnected"/> is false and stays
    /// false, and nothing connects, joins or sends.</summary>
    public static class PhotonNetwork
    {
        public static bool IsConnected => false;
        public static bool InRoom => false;
        public static bool IsMasterClient => false;
        public static bool OfflineMode { get; set; }
        public static bool AutomaticallySyncScene { get; set; }
        public static string GameVersion { get; set; } = string.Empty;
        public static string NickName { get; set; } = string.Empty;
        public static Realtime.Room? CurrentRoom => null;
        public static Realtime.Player? LocalPlayer => null;
        public static Realtime.Player[] PlayerList => System.Array.Empty<Realtime.Player>();
        public static int CountOfPlayers => 0;
        public static double Time => 0d;
        public static int ServerTimestamp => 0;

        public static bool ConnectUsingSettings() => false;

        public static bool Disconnect() => false;

        public static bool JoinLobby() => false;

        public static bool LeaveLobby() => false;

        public static bool JoinRoom(string roomName) => false;

        public static bool JoinRandomRoom() => false;

        public static bool CreateRoom(
            string? roomName,
            Realtime.RoomOptions? roomOptions = null,
            object? typedLobby = null,
            string[]? expectedUsers = null) => false;

        public static bool LeaveRoom(bool becomeInactive = true) => false;

        public static void LoadLevel(string levelName)
        {
        }

        public static void LoadLevel(int levelNumber)
        {
        }

        public static GameObject? Instantiate(string prefabName, Vector3 position, Quaternion rotation) => null;

        public static void Destroy(GameObject? targetGo)
        {
        }

        public static void SetMasterClient(Realtime.Player masterClientPlayer)
        {
        }

        public static bool InLobby => false;
        public static bool IsConnectedAndReady => false;
        public static string CloudRegion => string.Empty;
        public static Realtime.Player? MasterClient => null;
        public static LoadBalancingClient? NetworkingClient => null;
        public static Realtime.ClientState NetworkClientState => Realtime.ClientState.Disconnected;
        public static Realtime.DisconnectCause DisconnectedCause => Realtime.DisconnectCause.None;
        public static ServerSettings? PhotonServerSettings => null;

        public static bool ConnectToRegion(string region) => false;
    }

    /// <summary>Photon <c>LoadBalancingClient</c>. There is no client.</summary>
    public sealed class LoadBalancingClient
    {
        public Realtime.ClientState State => Realtime.ClientState.Disconnected;
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
