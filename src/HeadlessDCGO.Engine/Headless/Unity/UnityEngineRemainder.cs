// ============================================================================================================
// THE REMAINING AS-IS SURFACE the compiler asked for after the object model, value types, presentation, UI and
// vendor shims were in place: `Resources`, `JsonUtility`, `Matrix4x4`, `RectTransformUtility`, scene/web/TMP
// leaves, and Photon's room-property surface.
//
// EVERY TYPE HERE IS A DECLARATION DOING NOTHING. None of it is reached by a rule path: the readers are the
// out-of-scope lobby/deck-editor files and the presentation layer. `Resources.Load` returning null is the
// honest answer to "load a Unity asset" in a process with no asset bundles — supplying card data is roadmap
// step 2 (data loading), through the AS-IS `DataBase`/`DeckData` path, not through this.
// ============================================================================================================

namespace UnityEngine
{
    using System;
    using System.Collections.Generic;

    /// <summary>Unity <c>Resources</c>. There are no asset bundles; every load returns null.</summary>
    public static class Resources
    {
        public static T? Load<T>(string path) where T : Object => null;

        public static Object? Load(string path) => null;

        public static T[] LoadAll<T>(string path) where T : Object => Array.Empty<T>();

        /// <summary>Unity <c>Resources.UnloadUnusedAssets</c> returns an operation the AS-IS code yields on.
        /// Nothing unloads; the handle reports done immediately.</summary>
        public static SceneManagement.AsyncOperation UnloadUnusedAssets() => new();
    }

    /// <summary>Unity <c>JsonUtility</c>. Forwarded to <see cref="System.Text.Json"/> so the shape survives;
    /// Unity's serializer has different field rules, and nothing rule-bearing round-trips through it.</summary>
    public static class JsonUtility
    {
        public static string ToJson(object? obj) => System.Text.Json.JsonSerializer.Serialize(obj);

        public static string ToJson(object? obj, bool prettyPrint)
            => System.Text.Json.JsonSerializer.Serialize(
                obj, new System.Text.Json.JsonSerializerOptions { WriteIndented = prettyPrint });

        public static T? FromJson<T>(string json) => System.Text.Json.JsonSerializer.Deserialize<T>(json);

        public static void FromJsonOverwrite(string json, object objectToOverwrite)
        {
        }
    }

    /// <summary>Unity <c>Matrix4x4</c>. A held value; nothing transforms.</summary>
    public struct Matrix4x4
    {
        public static Matrix4x4 identity => default;

        public static Matrix4x4 TRS(Vector3 pos, Quaternion q, Vector3 s) => identity;
    }

    /// <summary>Unity <c>Outline</c> (the UI effect component). Nothing draws.</summary>
    public sealed class Outline : Component
    {
        public Color effectColor { get; set; } = Color.black;
        public Vector2 effectDistance { get; set; }
    }

    /// <summary>Unity <c>Vector4</c>. A held value; TMP margins use it.</summary>
    public struct Vector4(float x, float y, float z, float w)
    {
        public float x = x;
        public float y = y;
        public float z = z;
        public float w = w;

        public static Vector4 zero => new(0f, 0f, 0f, 0f);
    }

    /// <summary>Unity <c>NetworkReachability</c>. There is no network.</summary>
    public enum NetworkReachability
    {
        NotReachable = 0,
        ReachableViaCarrierDataNetwork = 1,
        ReachableViaLocalAreaNetwork = 2,
    }
}

namespace UnityEngine.Device
{
    /// <summary>Unity's device-simulator mirrors of <c>Screen</c>/<c>Application</c>. They forward to the real
    /// statics, which are themselves inert here.</summary>
    public static class Screen
    {
        public static int width => UnityEngine.Screen.width;
        public static int height => UnityEngine.Screen.height;

        public static bool fullScreen
        {
            get => UnityEngine.Screen.fullScreen;
            set => UnityEngine.Screen.fullScreen = value;
        }

        public static void SetResolution(int width, int height, bool fullscreen)
        {
        }
    }

    public static class Application
    {
        public static string persistentDataPath => UnityEngine.Application.persistentDataPath;
        public static bool isEditor => UnityEngine.Application.isEditor;
    }

    public static class SystemInfo
    {
        public static string deviceModel => string.Empty;
    }
}

namespace UnityEngine
{
    /// <summary>Unity ships a <c>string.IsNullOrEmpty()</c> EXTENSION alongside the BCL static, and the AS-IS
    /// sources call the extension form (`Type.IsNullOrEmpty()`). Same test, no behaviour of its own.</summary>
    public static class StringExtensions
    {
        public static bool IsNullOrEmpty(this string? value) => string.IsNullOrEmpty(value);
    }
}

namespace UnityEngine.EventSystems
{
    using System.Collections.Generic;

    /// <summary>Unity <c>RaycastResult</c>. Nothing raycasts.</summary>
    public struct RaycastResult
    {
        public GameObject? gameObject { get; set; }
        public float distance { get; set; }
        public Vector2 screenPosition { get; set; }
    }
}

namespace UnityEngine.UI
{
    /// <summary>Unity <c>RectTransformUtility</c>. Nothing lays out.</summary>
    public static class RectTransformUtility
    {
        public static bool ScreenPointToLocalPointInRectangle(
            RectTransform rect, Vector2 screenPoint, Camera? cam, out Vector2 localPoint)
        {
            localPoint = default;

            return false;
        }

        public static bool RectangleContainsScreenPoint(RectTransform rect, Vector2 screenPoint, Camera? cam)
            => false;
    }

    /// <summary>Unity <c>Scrollbar</c>. Nothing scrolls.</summary>
    public class Scrollbar : Selectable
    {
        public float value { get; set; }
        public float size { get; set; }
    }
}

namespace UnityEngine.SceneManagement
{
    /// <summary>Unity <c>LoadSceneMode</c>. There are no scenes.</summary>
    public enum LoadSceneMode
    {
        Single = 0,
        Additive = 1,
    }

    /// <summary>An inert async operation handle for the scene loads that never happen.</summary>
    public class AsyncOperation
    {
        public bool isDone => true;
        public float progress => 1f;
        public bool allowSceneActivation { get; set; } = true;
    }
}

namespace UnityEngine.Networking
{
    using System;

    /// <summary>Unity web-request leaves. There is no network; every request is already "done" and empty.</summary>
    public sealed class DownloadHandler
    {
        public string text => string.Empty;
        public byte[] data => Array.Empty<byte>();
    }

    public sealed class UnityWebRequestAsyncOperation
    {
        public bool isDone => true;
        public float progress => 1f;
    }

    public static class UnityWebRequestTexture
    {
        public static UnityWebRequest GetTexture(string uri) => new();
    }
}

namespace TMPro
{
    using UnityEngine;

    /// <summary>TextMeshPro <c>TMP_LinkInfo</c> / <c>TMP_TextUtilities</c>. Nothing draws or hit-tests.</summary>
    public struct TMP_LinkInfo
    {
        public string GetLinkID() => string.Empty;

        public string GetLinkText() => string.Empty;
    }

    /// <summary>TextMeshPro <c>TMP_TextInfo</c>. Nothing is laid out, so every count is zero.</summary>
    public sealed class TMP_TextInfo
    {
        public int characterCount => 0;
        public int lineCount => 0;
        public int pageCount => 0;
        public TMP_LinkInfo[] linkInfo => System.Array.Empty<TMP_LinkInfo>();
        public TMP_CharacterInfo[] characterInfo => System.Array.Empty<TMP_CharacterInfo>();
        public TMP_MeshInfo[] meshInfo => System.Array.Empty<TMP_MeshInfo>();
    }

    /// <summary>TextMeshPro per-character / per-mesh info. Nothing is laid out.</summary>
    public struct TMP_CharacterInfo
    {
        public char character { get; set; }
        public int index { get; set; }
        public bool isVisible { get; set; }
        public int vertexIndex { get; set; }
        public int materialReferenceIndex { get; set; }
        public float baseLine { get; set; }
    }

    public struct TMP_MeshInfo
    {
        public UnityEngine.Vector3[] vertices { get; set; }
    }

    public static class TMP_TextUtilities
    {
        public static int FindIntersectingLink(TMP_Text text, Vector3 position, Camera? camera) => -1;

        public static int FindIntersectingCharacter(TMP_Text text, Vector3 position, Camera? camera, bool visibleOnly)
            => -1;
    }
}

namespace Photon.Pun
{
    using UnityEngine;

    /// <summary>Photon <c>ServerSettings</c> / <c>PhotonServerSettings</c>. There is no client to configure.</summary>
    public class ServerSettings : ScriptableObject
    {
        public AppSettings AppSettings { get; set; } = new();

        public static void ResetBestRegionCodeInPreferences()
        {
        }
    }

    /// <summary>Photon <c>AppSettings</c>. There is no client to configure.</summary>
    public sealed class AppSettings
    {
        public string AppIdRealtime { get; set; } = string.Empty;
        public string AppIdChat { get; set; } = string.Empty;
        public string AppVersion { get; set; } = string.Empty;
        public string FixedRegion { get; set; } = string.Empty;
    }
}

namespace Unity.Mathematics
{
    /// <summary>Unity's math package entry point. Imported by an out-of-scope file; nothing rule-bearing uses
    /// it.</summary>
    public static class math
    {
        public static float abs(float v) => System.MathF.Abs(v);

        public static float min(float a, float b) => System.MathF.Min(a, b);

        public static float max(float a, float b) => System.MathF.Max(a, b);

        public static int abs(int v) => System.Math.Abs(v);

        public static int min(int a, int b) => System.Math.Min(a, b);

        public static int max(int a, int b) => System.Math.Max(a, b);
    }
}

namespace DG.Tweening
{
    using UnityEngine;

    /// <summary>DOTween <c>RotateMode</c> and the shake shortcuts. Inert, like the rest of the tween shim.</summary>
    public enum RotateMode
    {
        Fast = 0,
        FastBeyond360 = 1,
        WorldAxisAdd = 2,
        LocalAxisAdd = 3,
    }

    public static class ShakeExtensions
    {
        public static Tweener DOShakePosition(this Transform target, float duration) => new();

        public static Tweener DOShakePosition(
            this Transform target,
            float duration,
            float strength = 1f,
            int vibrato = 10,
            float randomness = 90f,
            bool snapping = false,
            bool fadeOut = true)
            => new();

        public static Tweener DOShakeScale(this Transform target, float duration) => new();

        public static Tweener DOShakeRotation(this Transform target, float duration) => new();
    }
}
