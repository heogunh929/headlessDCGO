// ============================================================================================================
// AS-IS EDITOR ATTRIBUTES: `UnityEngine.SerializeField` / `Header` / `HideInInspector` / `TextArea` /
// `RequireComponent` / `CreateAssetMenu` / `ExecuteInEditMode` / `RuntimeInitializeOnLoadMethod` /
// `DefaultExecutionOrder`, and `Photon.Pun.PunRPC`.
//
// WHY THIS FILE EXISTS. The AS-IS sources decorate fields and classes with these (`[SerializeField]` 203 sites,
// `[Header]` 392). They live in compiled Unity/PUN assemblies, so the AS-IS HOME is the namespace, not a file.
//
// EVERY TYPE HERE IS A DECLARATION DOING NOTHING. These attributes exist to drive the Unity editor — the
// inspector layout, asset-menu entries, edit-mode execution, component requirements. None of them is read at
// runtime by the AS-IS rules, and there is no editor here. They carry their constructor arguments so the
// decorations compile verbatim, and nothing ever asks for those arguments back.
//
// `PunRPC` is included because it is the same shape: a marker Photon's networking layer reads by reflection.
// Photon is not running. Note that `PhotonView.RPC` is deliberately NOT declared (see
// Headless/Unity/PhotonPunBehaviours.cs) so no call site can be written that silently does nothing — the
// marker compiling does not mean an RPC would dispatch.
// ============================================================================================================

namespace UnityEngine;

using System;

/// <summary>Unity <c>[SerializeField]</c>. Editor-only marker; nothing reads it.</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
public sealed class SerializeFieldAttribute : Attribute
{
}

/// <summary>Unity <c>[Header(string)]</c>. Editor-only inspector label; nothing reads it.</summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
public sealed class HeaderAttribute(string header) : Attribute
{
    public string header { get; } = header;
}

/// <summary>Unity <c>[HideInInspector]</c>. Editor-only marker; nothing reads it.</summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public sealed class HideInInspectorAttribute : Attribute
{
}

/// <summary>Unity <c>[TextArea]</c>. Editor-only inspector hint; nothing reads it.</summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public sealed class TextAreaAttribute : Attribute
{
    public TextAreaAttribute()
    {
    }

    public TextAreaAttribute(int minLines, int maxLines)
    {
        this.minLines = minLines;
        this.maxLines = maxLines;
    }

    public int minLines { get; }
    public int maxLines { get; }
}

/// <summary>Unity <c>[RequireComponent(Type…)]</c>. Editor-only; the headless object model does not enforce
/// component requirements.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class RequireComponentAttribute : Attribute
{
    public RequireComponentAttribute(Type requiredComponent) => m_Type0 = requiredComponent;

    public RequireComponentAttribute(Type requiredComponent, Type requiredComponent2)
        : this(requiredComponent) => m_Type1 = requiredComponent2;

    public RequireComponentAttribute(Type requiredComponent, Type requiredComponent2, Type requiredComponent3)
        : this(requiredComponent, requiredComponent2) => m_Type2 = requiredComponent3;

    public Type? m_Type0 { get; }
    public Type? m_Type1 { get; }
    public Type? m_Type2 { get; }
}

/// <summary>Unity <c>[CreateAssetMenu]</c>. Editor-only asset-menu entry; nothing reads it.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class CreateAssetMenuAttribute : Attribute
{
    public string? fileName { get; set; }
    public string? menuName { get; set; }
    public int order { get; set; }
}

/// <summary>Unity <c>[ExecuteInEditMode]</c>. Editor-only; there is no edit mode.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class ExecuteInEditModeAttribute : Attribute
{
}

/// <summary>Unity <c>[ExecuteAlways]</c>. Editor-only; there is no edit mode.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class ExecuteAlwaysAttribute : Attribute
{
}

/// <summary>Unity <c>[RuntimeInitializeOnLoadMethod]</c>. Unity calls the marked method during player startup.
/// There is no player loop here, so NOTHING CALLS IT — see the roadmap's step 2.1 (lifecycle).</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute
{
    public RuntimeInitializeOnLoadMethodAttribute()
    {
    }

    public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType loadType) => this.loadType = loadType;

    public RuntimeInitializeLoadType loadType { get; }
}

/// <summary>Unity <c>RuntimeInitializeLoadType</c>. Carried for the attribute above.</summary>
public enum RuntimeInitializeLoadType
{
    AfterSceneLoad = 0,
    BeforeSceneLoad = 1,
    AfterAssembliesLoaded = 2,
    BeforeSplashScreen = 3,
    SubsystemRegistration = 4,
}

/// <summary>Unity <c>[DefaultExecutionOrder(int)]</c>. Editor-only ordering hint; there is no tick loop to
/// order.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class DefaultExecutionOrderAttribute(int order) : Attribute
{
    public int order { get; } = order;
}
