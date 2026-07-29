// ============================================================================================================
// THE AS-IS OBJECT MODEL: `UnityEngine.Object` / `Component` / `Behaviour` / `Transform` / `GameObject`.
//
// WHY THIS FILE EXISTS. These types live in a compiled Unity assembly, so there is no AS-IS *file* to mirror —
// the AS-IS HOME is the namespace `UnityEngine`, exactly as for MonoBehaviour and the yield types. Declaring
// them here lets the AS-IS sources compile and run unmodified.
//
// UNLIKE THE OTHER SHIMS IN THIS FOLDER, THIS ONE CARRIES REAL STATE. The AS-IS game logic reads it to make
// decisions, so an empty declaration would compile and then be silently wrong. Two measured cases:
//
//   Player.cs:23-44          `for (i < BattleAreaFrameParent.childCount) … GetChild(i).GetChild(0).gameObject`
//                            builds `fieldCardFrames` — THE FIELD SLOT ARRAY. A childCount that is always 0
//                            leaves the array empty and every play/move path collapses.
//   TurnStateMachine.cs:1429 `if (fieldPermanentCard.gameObject.activeSelf && …CanDeclareSkill())` registers a
//                            permanent as selectable. An activeSelf that is always false means NOTHING can
//                            ever be selected.
//
// And the component registry is how the engine instantiates itself at all:
//
//   GManager.cs:269          `turnStateMachine = gameObject.AddComponent<TurnStateMachine>();`
//   CEntity_EffectController.cs:212-221
//                            `t = Type.GetType($"DCGO.CardEffects.{ID}.{ClassName}");`
//                            `Component component = this.gameObject.AddComponent(t);`
//                            EVERY card's effect class is created by reflection on its AS-IS namespace. This is
//                            why the AS-IS namespaces (`DCGO.CardEffects.BT25`, …) must be carried verbatim:
//                            rename them and every lookup returns null.
//
// MEMBERS THAT DO REAL WORK
//     GameObject.activeSelf / SetActive     A real flag. Read by the selectability gate above.
//     GameObject.activeInHierarchy          Derived by walking the parent chain, as Unity does.
//     GameObject.transform                  The GameObject's own Transform. Created with it, never null.
//     GameObject.AddComponent<T>() / (Type) Constructs the component, binds it to this GameObject, registers
//                                           it. Reflection form used by the card-effect loader.
//     GameObject.GetComponent<T>() / (Type) Searches this GameObject's registered components.
//     Component.gameObject / .transform     The owning GameObject and its Transform.
//     Component.GetComponent<T>() / (Type)  Delegates to the owning GameObject.
//     Transform.parent / SetParent          A real hierarchy link, maintained on both sides.
//     Transform.childCount / GetChild(int)  Real child list. Feeds the field-slot construction above.
//     Transform.gameObject                  The GameObject this Transform belongs to.
//
// MEMBERS THAT ARE DECLARATIONS DOING NOTHING
//     Object.name                           Stored and returned; nothing reads it for a decision (10 sites,
//                                           all logging/lookup in files that are out of scope).
//     Behaviour.enabled                     Stored and returned. Unity would stop ticking a disabled component;
//                                           there is no tick loop here.
//     Transform.localPosition / position / localScale / localRotation / SetSiblingIndex
//                                           Stored and returned; all default. Measured uses are display
//                                           placement. The one rule-adjacent reader is
//                                           `CardSource.PreferredFrame()` (CardSource.cs:2306-2352), which
//                                           ORDERS empty battle-area frames by screen position — a layout
//                                           choice, not a rule. With every position equal the ordering degrades
//                                           to list order, which is deterministic, and E-01 keeps spare slots
//                                           available so the choice does not bind. See
//                                           docs/audit/sanctioned_exceptions.md.
//
// NOT DECLARED, ON PURPOSE
//     Awake / Start / Update / OnDestroy    AddComponent does NOT invoke Unity's message hooks. The lifecycle
//                                           is driven in ONE place, Headless/Bootstrap/HeadlessScene.cs: Awake
//                                           and Start when the scene runs, OnDestroy when it tears down. What
//                                           this file contributes is only the census that teardown walks —
//                                           GameObject.Registry (every object constructed since the last
//                                           teardown, the list Unity's scene keeps) and
//                                           Object.DestroyedByTeardown (read by the scene's static-field purge;
//                                           NOT by the `==`/truthiness operators, see below).
//     Object's `==` / `!=` overloads        Unity makes a destroyed object compare equal to null. Reproducing
//                                           that requires a destruction model that does not exist. Equality
//                                           here is plain reference identity — see the note in
//                                           Headless/Unity/UnityEngineMonoBehaviour.cs, which made the same
//                                           call for the same reason.
//     Destroy / Instantiate / Find / CompareTag / layer
//                                           Measured only in out-of-scope files (`ColorDropdownController`,
//                                           `Opening`). Left out so a rule path cannot reach a stand-in.
//
// Add a member here only when an AS-IS file cannot compile without it, and say in this header which of the
// three lists above it belongs to.
// ============================================================================================================

namespace UnityEngine;

using System;
using System.Collections.Generic;

/// <summary>Unity <c>UnityEngine.Object</c> — the root of the engine object hierarchy.</summary>
public partial class Object
{
    /// <summary>Unity <c>Object.name</c>. Stored and returned; nothing reads it for a decision.</summary>
    public string name { get; set; } = string.Empty;

    /// <summary>Unity's truthiness conversion, which is why the AS-IS sources can write
    /// <c>if (permanent.TopCard &amp;&amp; …)</c> and <c>if (!player)</c>. In Unity this reports "alive": a
    /// DESTROYED object converts to false even though the managed reference is non-null. There is no
    /// destruction model here (see the file header on the omitted <c>==</c>/<c>!=</c> overloads), so this means
    /// exactly "the reference is not null" and nothing more.</summary>
    public static implicit operator bool(Object? exists) => exists is not null;

    /// <summary>Unity <c>Object.GetInstanceID()</c>. A per-instance identity number; here it is the managed
    /// hash code, which is stable for the object's lifetime as Unity's is.</summary>
    public int GetInstanceID() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);

    /// <summary>True once the object is dead — set by <c>Object.Destroy</c> (frame-level release) or by
    /// HeadlessScene.Teardown (scene unload). Read ONLY by the static-anchor purge; the <c>==</c>/truthiness
    /// operators deliberately do not consult it (their fake-null behaviour is still not modelled — see the
    /// file header). Named to dodge the AS-IS member <c>TargetArrow.Destroyed</c>, which a plain "Destroyed"
    /// would collide with.</summary>
    internal bool DestroyedByTeardown { get; set; }

    public override string ToString() => string.IsNullOrEmpty(name) ? GetType().Name : name;
}

/// <summary>Unity <c>UnityEngine.Component</c> — anything attached to a <see cref="GameObject"/>.</summary>
public class Component : Object
{
    private GameObject? _gameObject;

    /// <summary>The <see cref="GameObject"/> this component is attached to. Unity guarantees every component
    /// has one; a component reached here without going through
    /// <see cref="GameObject.AddComponent(System.Type)"/> (i.e. constructed directly) gets a host GameObject
    /// created on first access so that invariant still holds.</summary>
    public GameObject gameObject
    {
        get
        {
            if (_gameObject is null)
            {
                _gameObject = new GameObject(GetType().Name);
                _gameObject.Attach(this);
                HostCreated?.Invoke(_gameObject);
            }

            return _gameObject;
        }
    }

    /// <summary>The owning GameObject's <see cref="Transform"/>.</summary>
    public Transform transform => gameObject.transform;

    internal void BindTo(GameObject owner) => _gameObject = owner;

    /// <summary>Raised when a component materialises its own host GameObject because nothing attached it.
    /// The scene subscribes so such a host gets the same widget components a prefab would have carried — see
    /// Headless/Bootstrap/HeadlessScene.cs.</summary>
    public static event Action<GameObject>? HostCreated;

    /// <summary>Unity <c>Component.GetComponent&lt;T&gt;()</c>. Searches the owning GameObject.</summary>
    public T? GetComponent<T>() where T : class => gameObject.GetComponent<T>();

    /// <summary>Unity <c>Component.GetComponent(Type)</c>.</summary>
    public Component? GetComponent(Type type) => gameObject.GetComponent(type);

    /// <summary>Unity <c>GetComponentInChildren</c>/<c>GetComponentsInChildren</c>. Walks the real hierarchy
    /// declared above.</summary>
    public T? GetComponentInChildren<T>() where T : class
    {
        foreach (T found in GetComponentsInChildren<T>())
        {
            return found;
        }

        return null;
    }

    public T[] GetComponentsInChildren<T>() where T : class
    {
        List<T> found = new();
        Collect(transform, found);

        return found.ToArray();

        static void Collect(Transform node, List<T> into)
        {
            if (node.gameObject.GetComponent<T>() is { } hit)
            {
                into.Add(hit);
            }

            for (int i = 0; i < node.childCount; i++)
            {
                Collect(node.GetChild(i), into);
            }
        }
    }
}

/// <summary>Unity <c>UnityEngine.Behaviour</c> — a component that can be enabled or disabled.</summary>
public class Behaviour : Component
{
    /// <summary>Unity <c>Behaviour.enabled</c>. Stored and returned; there is no tick loop to suppress.</summary>
    public bool enabled { get; set; } = true;
}

/// <summary>Unity <c>UnityEngine.Transform</c> — a GameObject's place in the scene hierarchy. The hierarchy is
/// real: <see cref="childCount"/> and <see cref="GetChild"/> supply the field-slot array in Player.cs.</summary>
public class Transform : Component
{
    private readonly List<Transform> _children = new();
    private Transform? _parent;

    internal Transform(GameObject owner) => BindTo(owner);

    /// <summary>Only <c>UnityEngine.RectTransform</c> derives from this, mirroring Unity's own hierarchy.</summary>
    private protected Transform()
    {
    }

    /// <summary>Unity <c>Transform.parent</c>. Assigning maintains both sides of the link.</summary>
    public Transform? parent
    {
        get => _parent;
        set => SetParent(value);
    }

    /// <summary>Unity <c>Transform.childCount</c>. The real child count.</summary>
    public int childCount => _children.Count;

    /// <summary>Unity <c>Transform.GetChild(int)</c>. The real child at <paramref name="index"/>.</summary>
    public Transform GetChild(int index) => _children[index];

    /// <summary>Unity <c>Transform.SetParent(Transform)</c>. Detaches from the previous parent and attaches to
    /// the new one. The <c>worldPositionStays</c> overload is accepted and ignored — there are no world
    /// coordinates to preserve.</summary>
    public void SetParent(Transform? newParent)
    {
        if (ReferenceEquals(_parent, newParent))
        {
            return;
        }

        _parent?._children.Remove(this);
        _parent = newParent;
        newParent?._children.Add(this);
    }

    public void SetParent(Transform? newParent, bool worldPositionStays) => SetParent(newParent);

    /// <summary>Unity <c>Transform.SetSiblingIndex(int)</c>. Reorders within the parent's child list. Ordering
    /// among siblings is display placement; see the file header.</summary>
    public void SetSiblingIndex(int index)
    {
        if (_parent is null)
        {
            return;
        }

        List<Transform> siblings = _parent._children;
        siblings.Remove(this);
        siblings.Insert(Math.Clamp(index, 0, siblings.Count), this);
    }

    /// <summary>Unity <c>Transform.GetSiblingIndex()</c>.</summary>
    public int GetSiblingIndex() => _parent?._children.IndexOf(this) ?? 0;

    /// <summary>Unity's Transform enumerates its CHILDREN, which is why `foreach (Transform t in someTransform)`
    /// compiles in the AS-IS sources. Backed by the real child list above.</summary>
    public IEnumerator<Transform> GetEnumerator() => _children.GetEnumerator();

    // Declarations doing nothing — stored and returned, all default. See the file header.
    public Vector3 localPosition { get; set; }
    public Vector3 position { get; set; }
    public Vector3 localScale { get; set; } = new(1f, 1f, 1f);
    public Quaternion localRotation { get; set; }
    public Quaternion rotation { get; set; }
    public Vector3 lossyScale => localScale;
    public Vector3 forward { get; set; } = new(0f, 0f, 1f);
    public Vector3 right { get; set; } = new(1f, 0f, 0f);
    public Vector3 up { get; set; } = new(0f, 1f, 0f);
}

/// <summary>Unity <c>UnityEngine.GameObject</c> — a named node carrying a <see cref="Transform"/> and a set of
/// components. Its active flag and component registry are read by the AS-IS rules; see the file header.</summary>
public sealed class GameObject : Object
{
    private readonly List<Component> _components = new();

    public GameObject() : this(nameof(GameObject))
    {
    }

    public GameObject(string objectName)
    {
        name = objectName;
        transform = new Transform(this);
        _components.Add(transform);
        RegistrySlot = Registry.Count;
        Registry.Add(this);
    }

    /// <summary>Every LIVE GameObject since the last teardown — the census Unity's scene keeps and a headless
    /// process otherwise lacks. HeadlessScene.Teardown walks it to deliver OnDestroy and then clears it.
    /// `Object.Destroy` UNREGISTERS (Unity frees a destroyed object at end of frame — keeping it here until
    /// teardown was the in-match accumulator behind the 22:36-22:40 OOMs). Removal is O(1) swap-remove so a
    /// churn-heavy match cannot go quadratic; iteration order stays deterministic (same operations → same
    /// order).</summary>
    internal static readonly List<GameObject> Registry = new();

    private int RegistrySlot = -1;

    internal static void Unregister(GameObject target)
    {
        int slot = target.RegistrySlot;

        if (slot < 0 || slot >= Registry.Count || !ReferenceEquals(Registry[slot], target))
        {
            return;   // 이미 제거됐거나 teardown이 Clear한 뒤의 낡은 슬롯
        }

        GameObject last = Registry[^1];
        Registry[slot] = last;
        last.RegistrySlot = slot;
        Registry.RemoveAt(Registry.Count - 1);
        target.RegistrySlot = -1;
    }

    /// <summary>Unity <c>GameObject.transform</c>. Created with the GameObject; never null.</summary>
    public Transform transform { get; }

    /// <summary>Unity <c>GameObject.gameObject</c> returns the object itself.</summary>
    public GameObject gameObject => this;

    /// <summary>Unity <c>GameObject.activeSelf</c>. A real flag —
    /// <c>TurnStateMachine.cs:1429</c> reads it to decide whether a permanent is selectable.</summary>
    public bool activeSelf { get; private set; } = true;

    /// <summary>Unity <c>GameObject.activeInHierarchy</c>. True when this object and every ancestor are
    /// active, as in Unity.</summary>
    public bool activeInHierarchy
    {
        get
        {
            for (Transform? t = transform; t is not null; t = t.parent)
            {
                if (!t.gameObject.activeSelf)
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>Unity <c>GameObject.SetActive(bool)</c>. Deactivating also STOPS the coroutines this object's
    /// components started, as Unity does — see MonoBehaviour.StopRunningCoroutines for why the AS-IS code
    /// depends on it.</summary>
    public void SetActive(bool value)
    {
        activeSelf = value;

        if (value)
        {
            return;
        }

        foreach (Component component in _components.ToArray())
        {
            if (component is MonoBehaviour behaviour && behaviour.HasRunningCoroutines)
            {
                Deactivations.Add($"{name} / {component.GetType().Name}");
            }

            (component as MonoBehaviour)?.StopRunningCoroutines();
        }
    }

    /// <summary>Unity <c>GameObject.layer</c> / <c>CompareTag</c>. There is no layer or tag system; measured
    /// readers are the out-of-scope `Opening` file.</summary>
    public int layer { get; set; }

    public string tag { get; set; } = "Untagged";

    public bool CompareTag(string tag) => string.Equals(this.tag, tag, StringComparison.Ordinal);

    /// <summary>Unity <c>GameObject.AddComponent&lt;T&gt;()</c>. Constructs, binds and registers.
    /// Unity's message hooks are NOT invoked — see the file header.</summary>
    public T AddComponent<T>() where T : Component, new()
    {
        T component = new();
        Attach(component);

        return component;
    }

    /// <summary>Unity <c>GameObject.AddComponent(Type)</c>. The reflection form used by
    /// <c>CEntity_EffectController</c> to instantiate a card's effect class from its AS-IS namespace.</summary>
    public Component AddComponent(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (Activator.CreateInstance(type) is not Component component)
        {
            throw new ArgumentException($"{type.FullName} is not a UnityEngine.Component.", nameof(type));
        }

        Attach(component);

        return component;
    }

    /// <summary>Unity <c>GameObject.GetComponent&lt;T&gt;()</c>.</summary>
    public T? GetComponent<T>() where T : class
    {
        foreach (Component component in _components)
        {
            if (component is T match)
            {
                return match;
            }
        }

        return null;
    }

    /// <summary>Unity <c>GameObject.GetComponent(Type)</c>.</summary>
    public Component? GetComponent(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        foreach (Component component in _components)
        {
            if (type.IsInstanceOfType(component))
            {
                return component;
            }
        }

        return null;
    }

    /// <summary>Unity <c>GameObject.GetComponentInChildren</c>/<c>GetComponentsInChildren</c>.</summary>
    public T? GetComponentInChildren<T>() where T : class => transform.GetComponentInChildren<T>();

    public T[] GetComponentsInChildren<T>() where T : class => transform.GetComponentsInChildren<T>();

    /// <summary>Unity <c>GameObject.Find</c>. There is no scene graph to search; measured callers are
    /// out-of-scope UI (`ColorDropdownController`).</summary>
    public static GameObject? Find(string name) => null;

    /// <summary>Objects whose deactivation stopped running coroutines, for diagnosis.</summary>
    public static List<string> Deactivations { get; } = new();

    /// <summary>The components on this object. Exposed for PUN's RPC resolution, which reflects over the
    /// view's GameObject exactly as PUN does — see Headless/Unity/PhotonPunBehaviours.cs.</summary>
    public IReadOnlyList<Component> Components => _components;

    internal void Attach(Component component)
    {
        component.BindTo(this);

        if (!_components.Contains(component))
        {
            _components.Add(component);
        }
    }

    /// <summary>Unity <c>Destroy(component)</c>의 실체 — 이 오브젝트에서 그 컴포넌트만 뗀다.</summary>
    internal void Remove(Component component) => _components.Remove(component);
}
