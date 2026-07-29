// ============================================================================================================
// AS-IS UI TYPES: namespaces `UnityEngine.UI`, `UnityEngine.Events`, `UnityEngine.EventSystems`, and `TMPro`.
//
// WHY THIS FILE EXISTS. The AS-IS sources hold widgets as fields and wire click handlers. These live in
// compiled Unity/TextMeshPro assemblies, so the AS-IS HOME is the namespace, not a file.
//
// EVERY TYPE HERE IS A DECLARATION DOING NOTHING. There is no screen, no pointer and no event system. Measured
// usage in the rule layer is field declarations and assignments only — zero condition sites
// (docs/symbol_classification.md).
//
// ONE THING TO BE CLEAR ABOUT. `Button.onClick.AddListener(...)` compiles here and REGISTERS the callback, but
// nothing ever invokes it: there is no input. Where the AS-IS engine genuinely waits on the player it does so
// through the coroutine gate — `yield return new WaitWhile(() => …commandText.gameObject.activeSelf)`, 24 such
// sites — and satisfying that gate is roadmap step 2.5 (IChoiceProvider), not this file's job. A registered
// listener firing is NOT how the headless engine will make choices.
// ============================================================================================================

namespace UnityEngine.Events
{

    using System;
    using System.Collections.Generic;

    /// <summary>Unity <c>UnityAction</c> delegates.</summary>
    public delegate void UnityAction();

    public delegate void UnityAction<in T0>(T0 arg0);

    public delegate void UnityAction<in T0, in T1>(T0 arg0, T1 arg1);

    public delegate void UnityAction<in T0, in T1, in T2>(T0 arg0, T1 arg1, T2 arg2);

    /// <summary>Unity <c>UnityEventBase</c>. Listeners are registered and never invoked — see the file header.</summary>
    public abstract class UnityEventBase
    {
        public abstract void RemoveAllListeners();
    }

    public class UnityEvent : UnityEventBase
    {
        private readonly List<UnityAction> _calls = new();

        public void AddListener(UnityAction call) => _calls.Add(call);

        public void RemoveListener(UnityAction call) => _calls.Remove(call);

        public override void RemoveAllListeners() => _calls.Clear();

        public void Invoke()
        {
            foreach (UnityAction call in _calls.ToArray())
            {
                call();
            }
        }
    }

    public class UnityEvent<T0> : UnityEventBase
    {
        private readonly List<UnityAction<T0>> _calls = new();

        public void AddListener(UnityAction<T0> call) => _calls.Add(call);

        public void RemoveListener(UnityAction<T0> call) => _calls.Remove(call);

        public override void RemoveAllListeners() => _calls.Clear();

        public void Invoke(T0 arg0)
        {
            foreach (UnityAction<T0> call in _calls.ToArray())
            {
                call(arg0);
            }
        }
    }

}

namespace UnityEngine.EventSystems
{

    using System;

    /// <summary>Unity <c>UIBehaviour</c>. The base of the UI widgets below.</summary>
    public abstract class UIBehaviour : MonoBehaviour
    {
    }

    /// <summary>Unity <c>BaseEventData</c> / <c>PointerEventData</c>. Nothing raises events.</summary>
    public class BaseEventData
    {
        public GameObject? selectedObject { get; set; }
    }

    public class PointerEventData : BaseEventData
    {
        public PointerEventData()
        {
        }

        public PointerEventData(EventSystem? eventSystem)
        {
        }

        public GameObject? pointerCurrentRaycast { get; set; }
        public GameObject? pointerEnter { get; set; }
        public GameObject? pointerPress { get; set; }
        public Vector2 position { get; set; }
        public Vector2 delta { get; set; }
        public int pointerId { get; set; }
        public int clickCount { get; set; }
    }

    /// <summary>Unity pointer handler interfaces. Nothing dispatches to them.</summary>
    public interface IEventSystemHandler
    {
    }

    public interface IPointerClickHandler : IEventSystemHandler
    {
        void OnPointerClick(PointerEventData eventData);
    }

    public interface IPointerEnterHandler : IEventSystemHandler
    {
        void OnPointerEnter(PointerEventData eventData);
    }

    public interface IPointerExitHandler : IEventSystemHandler
    {
        void OnPointerExit(PointerEventData eventData);
    }

    public interface IPointerDownHandler : IEventSystemHandler
    {
        void OnPointerDown(PointerEventData eventData);
    }

    public interface IPointerUpHandler : IEventSystemHandler
    {
        void OnPointerUp(PointerEventData eventData);
    }

    public interface IBeginDragHandler : IEventSystemHandler
    {
        void OnBeginDrag(PointerEventData eventData);
    }

    public interface IDragHandler : IEventSystemHandler
    {
        void OnDrag(PointerEventData eventData);
    }

    public interface IEndDragHandler : IEventSystemHandler
    {
        void OnEndDrag(PointerEventData eventData);
    }

    public interface IDropHandler : IEventSystemHandler
    {
        void OnDrop(PointerEventData eventData);
    }

    /// <summary>Unity <c>EventTrigger</c>. Entries are held; nothing fires them.</summary>
    public class EventTrigger : UIBehaviour
    {
        public sealed class Entry
        {
            public EventTriggerType eventID { get; set; }
            public TriggerEvent callback { get; } = new();
        }

        public sealed class TriggerEvent : Events.UnityEvent<BaseEventData>
        {
        }

        public System.Collections.Generic.List<Entry> triggers { get; } = new();
    }

    public enum EventTriggerType
    {
        PointerEnter = 0,
        PointerExit = 1,
        PointerDown = 2,
        PointerUp = 3,
        PointerClick = 4,
        Drag = 5,
        Drop = 6,
        BeginDrag = 9,
        EndDrag = 10,
    }

    /// <summary>Unity <c>EventSystem</c>. Nothing here routes input — but the OBJECT has to exist, because the
    /// AS-IS code dereferences <c>EventSystem.current</c> without a null check. `TurnStateMachine.EndGame`
    /// (TurnStateMachine.cs:3345) moves keyboard focus onto the result panel that way, and a null `current`
    /// threw there on every single match. In the real scene an EventSystem component is always present, so
    /// returning null was the divergence; this hands back a live one that ignores what it is told.</summary>
    public sealed class EventSystem : UIBehaviour
    {
        private static EventSystem? _current;

        public static EventSystem? current =>
            _current ??= new GameObject("EventSystem").AddComponent<EventSystem>();

        public GameObject? currentSelectedGameObject => null;

        public void SetSelectedGameObject(GameObject? selected)
        {
        }

        /// <summary>Unity <c>EventSystem.RaycastAll</c>. Nothing raycasts; the result list is left empty.</summary>
        public void RaycastAll(PointerEventData eventData, System.Collections.Generic.List<RaycastResult> raycastResults)
        {
        }
    }

}

namespace UnityEngine.UI
{

    using UnityEngine.Events;
    using UnityEngine.EventSystems;

    /// <summary>Unity <c>Graphic</c> — the base of the drawable widgets. Nothing draws.</summary>
    public abstract class Graphic : UIBehaviour
    {
        public Color color { get; set; } = Color.white;
        public Material? material { get; set; }
        public bool raycastTarget { get; set; } = true;

        public void SetAllDirty()
        {
        }

        public void SetVerticesDirty()
        {
        }
    }

    /// <summary>Unity <c>Image</c> / <c>RawImage</c>. Nothing draws.</summary>
    public class Image : Graphic
    {
        public Sprite? sprite { get; set; }
        public Sprite? overrideSprite { get; set; }
        public float fillAmount { get; set; } = 1f;
        public bool preserveAspect { get; set; }
        public Type type { get; set; }

        public enum Type
        {
            Simple = 0,
            Sliced = 1,
            Tiled = 2,
            Filled = 3,
        }
    }

    public sealed class RawImage : Graphic
    {
        public Texture? texture { get; set; }
    }

    /// <summary>Unity <c>Text</c>. Nothing draws.</summary>
    public class Text : Graphic
    {
        public string text { get; set; } = string.Empty;
        public Font? font { get; set; }
        public int fontSize { get; set; }
        public bool resizeTextForBestFit { get; set; }
    }

    /// <summary>Unity <c>Selectable</c> — the base of the interactive widgets. Nothing is interactive.</summary>
    public class Selectable : UIBehaviour
    {
        public bool interactable { get; set; } = true;
        public Image? targetGraphic { get; set; }
    }

    /// <summary>Unity <c>Button</c>. <c>onClick</c> registers listeners that nothing invokes — see the file
    /// header.</summary>
    public class Button : Selectable
    {
        public Image? image { get; set; }

        public sealed class ButtonClickedEvent : UnityEvent
        {
        }

        public ButtonClickedEvent onClick { get; } = new();
    }

    /// <summary>Unity <c>Toggle</c>. Nothing is interactive.</summary>
    public class Toggle : Selectable
    {
        public sealed class ToggleEvent : UnityEvent<bool>
        {
        }

        public bool isOn { get; set; }
        public ToggleEvent onValueChanged { get; } = new();
        public Graphic? graphic { get; set; }
    }

    /// <summary>Unity <c>Slider</c>. Nothing is interactive.</summary>
    public class Slider : Selectable
    {
        public sealed class SliderEvent : UnityEvent<float>
        {
        }

        public float value { get; set; }
        public float minValue { get; set; }
        public float maxValue { get; set; } = 1f;
        public bool wholeNumbers { get; set; }
        public SliderEvent onValueChanged { get; } = new();
    }

    /// <summary>Unity <c>InputField</c>. Nothing is interactive.</summary>
    public class InputField : Selectable
    {
        public sealed class OnChangeEvent : UnityEvent<string>
        {
        }

        public sealed class SubmitEvent : UnityEvent<string>
        {
        }

        public string text { get; set; } = string.Empty;
        public int characterLimit { get; set; }
        public OnChangeEvent onValueChanged { get; } = new();
        public SubmitEvent onEndEdit { get; } = new();

        public void ActivateInputField()
        {
        }
    }

    /// <summary>Unity <c>Dropdown</c>. Nothing is interactive.</summary>
    public class Dropdown : Selectable
    {
        public sealed class OptionData
        {
            public OptionData()
            {
            }

            public OptionData(string text) => this.text = text;

            public OptionData(string text, Sprite? image)
            {
                this.text = text;
                this.image = image;
            }

            public string text { get; set; } = string.Empty;
            public Sprite? image { get; set; }
        }

        public sealed class DropdownEvent : UnityEvent<int>
        {
        }

        public int value { get; set; }
        public System.Collections.Generic.List<OptionData> options { get; set; } = new();
        public DropdownEvent onValueChanged { get; } = new();
        public Text? captionText { get; set; }
        public Image? captionImage { get; set; }

        public void AddOptions(System.Collections.Generic.List<OptionData> options) => this.options.AddRange(options);

        public void ClearOptions() => options.Clear();

        public void RefreshShownValue()
        {
        }
    }

    /// <summary>Unity <c>ScrollRect</c>. Nothing scrolls.
    ///
    /// KNOWN GAP — content/viewport are ORPHAN roots, not descendants of the ScrollRect's GameObject
    /// (a scene authored in Unity would parent them). Direct access (`scrollRect.content.childCount`,
    /// `GetChild`) works; what does NOT work is reaching content's children by walking the hierarchy from
    /// an ancestor (`panel.GetComponentsInChildren&lt;T&gt;()` misses them — measured 2026-07-30, the
    /// SelectCardPanel candidate-card stall; the substrate reads the panel's own `_handCards` instead).
    /// AS-IS hierarchy walks are 2 sites (DropArea·BreakGlass), both presentation, neither over scroll
    /// content [census 2026-07-30]. Left unparented deliberately: parenting would put the panel into the
    /// cards' `activeInHierarchy` chain — a real-work member — changing behaviour no consumer asked for.</summary>
    public class ScrollRect : UIBehaviour
    {
        public sealed class ScrollRectEvent : UnityEvent<Vector2>
        {
        }

        /// <summary>Unity's ScrollRect always has a content RectTransform — the scene could not be authored
        /// without one, and the AS-IS sources walk it unguarded (`CheckCardPanel.cs:410`
        /// `scrollRect.content.childCount`). Created with the widget for the same reason
        /// <see cref="GameObject.transform"/> is: an absent one is a state the original never has.</summary>
        public RectTransform content { get; set; } = NewChildRect("Content");

        public RectTransform viewport { get; set; } = NewChildRect("Viewport");

        private static RectTransform NewChildRect(string name)
        {
            GameObject holder = new(name);

            return holder.AddComponent<RectTransform>();
        }
        public bool horizontal { get; set; } = true;
        public bool vertical { get; set; } = true;
        public Vector2 normalizedPosition { get; set; }
        public float verticalNormalizedPosition { get; set; }
        public float horizontalNormalizedPosition { get; set; }
        public ScrollRectEvent onValueChanged { get; } = new();
        public Scrollbar? verticalScrollbar { get; set; }
        public Scrollbar? horizontalScrollbar { get; set; }
    }

    /// <summary>Unity layout group family. Nothing lays out.</summary>
    public interface ILayoutElement
    {
    }

    public interface ILayoutGroup
    {
    }

    public interface ILayoutController
    {
    }

    public class LayoutGroup : UIBehaviour, ILayoutGroup, ILayoutController
    {
        public RectOffset padding { get; set; } = new();
    }

    public class GridLayoutGroup : LayoutGroup
    {
        public Vector2 cellSize { get; set; }
        public Vector2 spacing { get; set; }
        public int constraintCount { get; set; }
    }

    public class HorizontalOrVerticalLayoutGroup : LayoutGroup
    {
        public float spacing { get; set; }
    }

    public sealed class HorizontalLayoutGroup : HorizontalOrVerticalLayoutGroup
    {
    }

    public sealed class VerticalLayoutGroup : HorizontalOrVerticalLayoutGroup
    {
    }

    public sealed class ContentSizeFitter : UIBehaviour
    {
        public void SetLayoutVertical()
        {
        }

        public void SetLayoutHorizontal()
        {
        }
    }

    public sealed class RectOffset
    {
        public int left { get; set; }
        public int right { get; set; }
        public int top { get; set; }
        public int bottom { get; set; }
    }

    public static class LayoutRebuilder
    {
        public static void ForceRebuildLayoutImmediate(RectTransform? layoutRoot)
        {
        }
    }

}

namespace TMPro
{

    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>TextMeshPro <c>TMP_FontAsset</c>. Nothing draws.</summary>
    public sealed class TMP_FontAsset : Object
    {
    }

    /// <summary>TextMeshPro <c>TMP_Text</c> and its concrete widgets. Nothing draws.</summary>
    public abstract class TMP_Text : Graphic
    {
        public string text { get; set; } = string.Empty;
        public float fontSize { get; set; }
        public TMP_FontAsset? font { get; set; }
        public bool enableWordWrapping { get; set; } = true;
        public TextAlignmentOptions alignment { get; set; }

        public Material? fontSharedMaterial { get; set; }
        public float fontSizeMax { get; set; }
        public float fontSizeMin { get; set; }
        public bool enableAutoSizing { get; set; }
        public Vector4 margin { get; set; }
        public TMP_TextInfo textInfo { get; } = new();

        public void ForceMeshUpdate()
        {
        }

        public void SetText(string value) => text = value;

        public bool havePropertiesChanged { get; set; }

        public void UpdateVertexData()
        {
        }

        public void UpdateVertexData(object flags)
        {
        }
    }

    public class TextMeshProUGUI : TMP_Text
    {
    }

    public class TextMeshPro : TMP_Text
    {
    }

    public enum TextAlignmentOptions
    {
        Left = 0,
        Center = 1,
        Right = 2,
        TopLeft = 3,
        Top = 4,
        TopRight = 5,
        MidlineLeft = 6,
        Midline = 7,
        MidlineRight = 8,
    }

    /// <summary>TextMeshPro <c>TMP_InputField</c>. Nothing is interactive.</summary>
    public sealed class TMP_InputField : Selectable
    {
        public sealed class OnChangeEvent : UnityEngine.Events.UnityEvent<string>
        {
        }

        public sealed class SubmitEvent : UnityEngine.Events.UnityEvent<string>
        {
        }

        public string text { get; set; } = string.Empty;
        public int characterLimit { get; set; }
        public OnChangeEvent onValueChanged { get; } = new();
        public SubmitEvent onEndEdit { get; } = new();

        public void ActivateInputField()
        {
        }
    }

    /// <summary>TextMeshPro <c>TMP_Dropdown</c>. Nothing is interactive.</summary>
    public sealed class TMP_Dropdown : Selectable
    {
        public sealed class OptionData
        {
            public OptionData()
            {
            }

            public OptionData(string text) => this.text = text;

            public string text { get; set; } = string.Empty;
        }

        public sealed class DropdownEvent : UnityEngine.Events.UnityEvent<int>
        {
        }

        public int value { get; set; }
        public System.Collections.Generic.List<OptionData> options { get; } = new();
        public DropdownEvent onValueChanged { get; } = new();
    }
}
