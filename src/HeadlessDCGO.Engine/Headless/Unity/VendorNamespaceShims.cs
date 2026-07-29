// ============================================================================================================
// THE REMAINING AS-IS VENDOR NAMESPACES. These are opened with `using` at the top of AS-IS files and used
// barely or not at all: `Photon.Pun` (the `[PunRPC]` marker), `ExitGames.Client.Photon`, `Cinemachine`,
// `Coffee.UIEffects`, `Shapes2D`, `UIShiny`, `WebP`, `WebSocketSharp`, `JetBrains.Annotations`,
// `AutoLayout3D`, `Unity.*`, `UnityEditor.*`, and a few empty `UnityEngine.*` / `DG.Tweening.*` leaves.
//
// EVERY TYPE HERE IS A DECLARATION DOING NOTHING. Most of these namespaces are imported and then never
// touched — the `using` line alone is what fails to compile. Where a member IS touched it is presentation or
// networking, neither of which exists in this process.
//
// TWO ENTRIES DESERVE A WORD.
//
//   Photon.Pun.PunRPCAttribute   A marker Photon's networking layer reads by reflection, on 42 sites. Photon
//                                is not running, and `PhotonView.RPC` is deliberately NOT declared (see
//                                Headless/Unity/PhotonPunBehaviours.cs) so no call site can be written that
//                                silently does nothing. The marker compiling does NOT mean an RPC dispatches.
//
//   MonoBehaviourPunCallbacks    The lobby files override `OnJoinedRoom`, `OnRoomListUpdate`, … The virtuals
//   room/lobby callbacks         are declared here so those overrides compile. NOTHING EVER CALLS THEM: there
//                                is no client, no room and no server. Every one of those files
//                                (`EnterRoom`, `LobbyManager_*`, `RoomManager`) is out of scope and slated for
//                                removal — see docs/out_of_scope.md.
// ============================================================================================================

namespace Photon.Pun
{
    using System;

    /// <summary>Photon <c>[PunRPC]</c>. A reflection marker for a networking layer that is not present.</summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class PunRPCAttribute : Attribute
    {
    }

    /// <summary>Photon <c>RpcTarget</c>. Carried so RPC call arguments compile where they appear.</summary>
    public enum RpcTarget
    {
        All = 0,
        Others = 1,
        MasterClient = 2,
        AllBuffered = 3,
        OthersBuffered = 4,
        AllViaServer = 5,
        AllBufferedViaServer = 6,
    }
}

namespace Photon.Pun.Demo
{
    /// <summary>Photon's bundled demo namespace. Imported by an AS-IS file; nothing in it is used.</summary>
    internal static class DemoNamespaceMarker
    {
    }
}

namespace ExitGames.Client.Photon
{
    using System.Collections;

    /// <summary>Photon's <c>Hashtable</c> — a plain <see cref="System.Collections.Hashtable"/> subclass in the
    /// real assembly, and the same here. The AS-IS sources pass these around as RPC payloads.</summary>
    public class Hashtable : System.Collections.Hashtable
    {
        public Hashtable()
        {
        }

        public Hashtable(int capacity) : base(capacity)
        {
        }

        /// <summary>Photon's Hashtable exposes a dictionary-style lookup; the AS-IS RPC payload readers use it.</summary>
        public bool TryGetValue(object key, out object? value)
        {
            value = base[key];

            return value is not null;
        }
    }

    /// <summary>Photon <c>Protocol</c> — serialisation helpers reached unqualified through
    /// <c>using ExitGames.Client.Photon;</c>. Nothing is serialised.</summary>
    public static class Protocol
    {
        /// <summary>Photon's byte-stream (de)serialisers, as the AS-IS action payloads use them
        /// (`Protocol.Serialize(value, bytes, ref index)` / `Protocol.Deserialize(out value, bytes, ref index)`).
        /// The RPC path they feed is not dispatched (see Headless/Unity/PhotonPunBehaviours.cs), so these move
        /// the offset and nothing else.</summary>
        public static void Serialize(int value, byte[] target, ref int targetOffset) => targetOffset += 4;

        public static void Serialize(short value, byte[] target, ref int targetOffset) => targetOffset += 2;

        public static void Serialize(float value, byte[] target, ref int targetOffset) => targetOffset += 4;

        public static void Deserialize(out int value, byte[] source, ref int offset)
        {
            value = 0;
            offset += 4;
        }

        public static void Deserialize(out short value, byte[] source, ref int offset)
        {
            value = 0;
            offset += 2;
        }

        public static void Deserialize(out float value, byte[] source, ref int offset)
        {
            value = 0f;
            offset += 4;
        }
    }
}

namespace UnityEngine
{
    using System;

    /// <summary>Unity <c>[Range(min, max)]</c>. Editor-only inspector slider; nothing reads it.</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class RangeAttribute(float min, float max) : Attribute
    {
        public float min { get; } = min;
        public float max { get; } = max;
    }

    /// <summary>Unity <c>[Space]</c>. Editor-only inspector spacing; nothing reads it.</summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public sealed class SpaceAttribute : Attribute
    {
        public SpaceAttribute()
        {
        }

        public SpaceAttribute(float height) => this.height = height;

        public float height { get; }
    }

    /// <summary>Unity <c>[Tooltip]</c>. Editor-only inspector hint; nothing reads it.</summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class TooltipAttribute(string tooltip) : Attribute
    {
        public string tooltip { get; } = tooltip;
    }
}

namespace UnityEngine.TextCore.Text
{
    /// <summary>TextCore's text sub-namespace. Imported; nothing in it is used.</summary>
    internal static class TextCoreTextNamespaceMarker
    {
    }
}

namespace UnityEngine.UIElements
{
    /// <summary>UIElements <c>UxmlAttributeDescription</c>. Imported; nothing in it is used.</summary>
    public abstract class UxmlAttributeDescription
    {
    }
}

namespace System.Runtime.InteropServices.WindowsRuntime
{
    /// <summary>A Windows-Runtime interop leaf an AS-IS file imports. Nothing in it is used.</summary>
    internal static class WindowsRuntimeNamespaceMarker
    {
    }
}

namespace DG.Tweening.Core
{
    /// <summary>DOTween's internals namespace. Imported; nothing in it is used.</summary>
    internal static class TweeningCoreNamespaceMarker
    {
    }
}

namespace DG.Tweening.Core.Easing
{
    /// <summary>DOTween's easing internals. Imported; nothing in it is used.</summary>
    internal static class EasingNamespaceMarker
    {
    }
}

namespace DG.Tweening.Plugins.Core
{
    /// <summary>DOTween's plugin internals. Imported; nothing in it is used.</summary>
    internal static class PluginsCoreNamespaceMarker
    {
    }
}

namespace DG.Tweening.Plugins.Core.PathCore
{
    /// <summary>DOTween's path-plugin internals. Imported; nothing in it is used.</summary>
    internal static class PathCoreNamespaceMarker
    {
    }
}

namespace Photon.Pun.Demo.PunBasics
{
    /// <summary>Photon's bundled demo sub-namespace. Imported; nothing in it is used.</summary>
    internal static class PunBasicsNamespaceMarker
    {
    }
}

namespace DG.Tweening.Plugins
{
    /// <summary>DOTween's plugins namespace. Imported; nothing in it is used.</summary>
    internal static class TweeningPluginsNamespaceMarker
    {
    }
}

namespace Cinemachine
{
    using UnityEngine;

    /// <summary>Cinemachine. There is no camera to drive.</summary>
    public class CinemachineImpulseSource : MonoBehaviour
    {
        public void GenerateImpulse()
        {
        }

        public void GenerateImpulse(Vector3 velocity)
        {
        }
    }

    public class CinemachineVirtualCamera : MonoBehaviour
    {
    }

    /// <summary>Cinemachine's own doc-tooling attribute. Nothing reads it.</summary>
    [System.AttributeUsage(System.AttributeTargets.All, AllowMultiple = false)]
    public sealed class DocumentationSortingAttribute : System.Attribute
    {
    }
}

namespace Coffee.UIEffects
{
    using UnityEngine;

    /// <summary>Coffee UIEffect. Nothing draws.</summary>
    public class UIEffect : MonoBehaviour
    {
    }

    public class UIShiny : MonoBehaviour
    {
        public float effectFactor { get; set; }
        public bool play { get; set; }
    }
}

namespace Shapes2D
{
    using UnityEngine;

    /// <summary>Shapes2D. Nothing draws.</summary>
    public class Shape : MonoBehaviour
    {
    }
}

namespace AutoLayout3D
{
    using UnityEngine;

    /// <summary>AutoLayout3D. Nothing lays out.</summary>
    public class AutoLayoutGroup3D : MonoBehaviour
    {
    }
}

namespace WebP
{
    using UnityEngine;

    /// <summary>WebP decoding. No textures are decoded.</summary>
    public static class Texture2DExt
    {
        /// <summary>Parameter names match the vendor's, because the AS-IS call sites pass them by name
        /// (`StreamingAssetsUtility.cs:101` uses <c>lMipmaps:</c>/<c>lLinear:</c>/<c>lError:</c>).</summary>
        public static Texture2D? CreateTexture2DFromWebP(
            byte[] lData, bool lMipmaps, bool lLinear, out Error lError, ScalingFunction? scalingFunction = null)
        {
            lError = Error.Success;

            return null;
        }

        public delegate void ScalingFunction(ref int width, ref int height);
    }

    public enum Error
    {
        Success = 0,
        DecodeFailure = 1,
    }
}

namespace WebSocketSharp
{
    using System;

    /// <summary>WebSocketSharp. There is no network.</summary>
    public class WebSocket : IDisposable
    {
        public WebSocket(string url, params string[] protocols)
        {
        }

        public bool IsAlive => false;

        public void Connect()
        {
        }

        public void Close()
        {
        }

        public void Send(string data)
        {
        }

        public void Dispose()
        {
        }
    }
}

namespace JetBrains.Annotations
{
    using System;

    /// <summary>ReSharper annotations. Static-analysis markers only.</summary>
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false)]
    public sealed class NotNullAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.All, AllowMultiple = false)]
    public sealed class CanBeNullAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.All, AllowMultiple = false)]
    public sealed class UsedImplicitlyAttribute : Attribute
    {
    }
}

namespace Unity.Mathematics
{
    /// <summary>Unity's math package. Imported; nothing in it is used.</summary>
    internal static class MathematicsNamespaceMarker
    {
    }
}

namespace Unity.Burst.Intrinsics
{
    /// <summary>Unity's Burst package. Imported; nothing in it is used.</summary>
    internal static class BurstIntrinsicsNamespaceMarker
    {
    }
}

namespace UnityEditor
{
    /// <summary>The Unity editor assembly. There is no editor.</summary>
    internal static class UnityEditorNamespaceMarker
    {
    }
}

namespace UnityEditor.Rendering
{
    /// <summary>The Unity editor's rendering namespace. There is no editor.</summary>
    internal static class UnityEditorRenderingNamespaceMarker
    {
    }
}
