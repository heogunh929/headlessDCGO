// Mirrored from DCGO/Assets/Scripts/Script/FieldPermanentCard.cs (894 lines).
// NO-OP PRESENTATION SEAM for substrate root S1 (phase A1). The AS-IS `FieldPermanentCard` is the on-board card
// widget for one Permanent (image, DP/level/evo-root labels, blocker/link/suspend markers, outline, command
// panel, click + drag targets). Verified: every body only reads its `ThisPermanent` and writes Unity components
// — the widget is a VIEW (`SetPermanentData`/`ShowPermanentData` copy permanent state onto the UI, never back).
// Mirror logic anchors that were deleted and target this type: ICardEffect.cs:1178 (`OnSelectEffect` /
// `Outline_Select` / `SetOrangeOutline`), SelectJogressEffect.cs:24/476 and CardController.cs:3667
// (`SetPermanentIndexText` / `OffPermanentIndexText`). This file re-creates the type only; no call site is
// restored here.
//
// SIGNATURE CHANGES (rule 4 — no Unity types):
//   - `IEnumerator ShowAddDigivolutionCardEffect()` -> `Task ShowAddDigivolutionCardEffect()`;
//   - `public Image Outline_Select` / `CardImage` / `EvoRootCountBackground` / `LinkIcon` -> `public object?`;
//   - `public GameObject BlockerEffect` / `Collider` / `LinkedObject` / `Parent` / `UsingSkillEffect` /
//     `TapObject` / `SummonSicknessObject` / `WillEvolutionObject` / `WillBeDeletedObject` /
//     `WillBeDeckBounceObject` / `WillBeHandBounceObject` / `WillRemoveFieldObject` / `WillUntapObject`
//     -> `public object?`;
//   - `public Text EnergyCountText` / `SkillNameText` / `DPText` / `EvoRootCountText` / `DirectStrikeText` and
//     `public TextMeshProUGUI LevelText` / `permanentIndexText` -> `public object?`;
//   - `public Animator anim` -> `public object?`;
//   - `public ParticleSystem addDigivolutionCardsEffect` -> `public object?`;
//   - `public List<Image> DPBackground_color` -> `public List<object?>`;
//   - `public UnityAction<FieldPermanentCard> OnClickAction` -> `public Action<FieldPermanentCard>?` and
//     `AddClickTarget(UnityAction<FieldPermanentCard>)` -> `AddClickTarget(Action<FieldPermanentCard>)`
//     (System.Action is the exact neutral equivalent of UnityEngine.Events.UnityAction);
//   - `public UnityAction<FieldPermanentCard> OnBeginDragAction { get; set; }` ->
//     `public Action<FieldPermanentCard>? OnBeginDragAction { get; set; }`;
//   - `PointerDown/PointerUp/PointerExit(BaseEventData eventData)` -> the `BaseEventData` PARAMETER IS DROPPED
//     (Unity event-system payload; no neutral equivalent, and rule 4 forbids adding a Unity shim);
//   - `TextCardColorMaterial.public Material material` -> DROPPED field (see omissions).
//   Every widget handle above is a live scene object in the original, so headless it is always null (rule 2).
//
// OMITTED MEMBERS:
//   - `public Vector3 StartScale { get; set; }` (:67) and `public Vector3 GetLocalCanvasPosition()` (:835):
//     both are Vector3-valued and the substrate has no neutral vector type — a value member cannot be kept
//     without inventing one.
//   - `public CommandPanel fieldUnitCommandPanel` (:33): no `CommandPanel` mirror type exists (the parameter-
//     less `CloseCommandPanel()` that drives it IS mirrored).
//   - `public UnityAction<FieldPermanentCard, List<DropArea>> OnDragAction` / `OnEndDragAction` (:853-854) and
//     `AddDragTarget(...)` (:863): no `DropArea` mirror type exists (drag/drop is Unity input plumbing). The
//     parameterless `RemoveDragTarget` / `OnBeginDrag` / `OnDrag` / `OnEndDrag` ARE mirrored.
//   - `TextCardColorMaterial.material` (`UnityEngine.Material`, :891): no neutral equivalent; the class and its
//     `CardColor cardColor` field are mirrored.
//   - private Unity-internal members: `Awake()`, `LateUpdate()`, `DestroyCoroutine()`, `SetTransformRotation`,
//     `SetCardIsFlipped`, `_frameCount`/`_updateFrame`, `_pressing`/`_requiredTime`/`_validPressTime`.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

/// <summary>Headless no-op stand-in for the AS-IS <c>FieldPermanentCard</c> board widget. Member names, order
/// and parameter lists mirror DCGO/Assets/Scripts/Script/FieldPermanentCard.cs; all behaviour is stripped.
/// </summary>
public class FieldPermanentCard
{
    /// <summary>AS-IS <c>public Image Outline_Select</c> (FieldPermanentCard.cs:15) — the selection outline.
    /// </summary>
    public object? Outline_Select;

    /// <summary>AS-IS <c>public Image CardImage</c> (:18).</summary>
    public object? CardImage;

    /// <summary>AS-IS <c>public GameObject BlockerEffect</c> (:21).</summary>
    public object? BlockerEffect;

    /// <summary>AS-IS <c>public Text EnergyCountText</c> (:24).</summary>
    public object? EnergyCountText;

    /// <summary>AS-IS <c>public GameObject Collider</c> (:27) — the click hit-box.</summary>
    public object? Collider;

    /// <summary>AS-IS <c>public Animator anim</c> (:30).</summary>
    public object? anim;

    /// <summary>AS-IS <c>public Text SkillNameText</c> (:36).</summary>
    public object? SkillNameText;

    /// <summary>AS-IS <c>public Text DPText</c> (:39).</summary>
    public object? DPText;

    /// <summary>AS-IS <c>public List&lt;Image&gt; DPBackground_color</c> (:42).</summary>
    public List<object?> DPBackground_color = new List<object?>();

    /// <summary>AS-IS <c>public Text EvoRootCountText</c> (:45).</summary>
    public object? EvoRootCountText;

    /// <summary>AS-IS <c>public Image EvoRootCountBackground</c> (:48).</summary>
    public object? EvoRootCountBackground;

    /// <summary>AS-IS <c>public TextMeshProUGUI LevelText</c> (:51).</summary>
    public object? LevelText;

    /// <summary>AS-IS <c>public GameObject LinkedObject</c> (:54).</summary>
    public object? LinkedObject;

    /// <summary>AS-IS <c>public Image LinkIcon</c> (:55).</summary>
    public object? LinkIcon;

    /// <summary>AS-IS <c>public GameObject Parent</c> (:58).</summary>
    public object? Parent;

    /// <summary>AS-IS <c>public GameObject UsingSkillEffect</c> (:61).</summary>
    public object? UsingSkillEffect;

    /// <summary>AS-IS <c>public ParticleSystem addDigivolutionCardsEffect</c> (:64).</summary>
    public object? addDigivolutionCardsEffect;

    /// <summary>AS-IS <c>public TextMeshProUGUI permanentIndexText</c> (:70) — the jogress selection-order
    /// badge.</summary>
    public object? permanentIndexText;

    /// <summary>AS-IS <c>public GameObject TapObject</c> (:73).</summary>
    public object? TapObject;

    /// <summary>AS-IS <c>public Text DirectStrikeText</c> (:76).</summary>
    public object? DirectStrikeText;

    /// <summary>AS-IS <c>public GameObject SummonSicknessObject</c> (:79).</summary>
    public object? SummonSicknessObject;

    /// <summary>AS-IS <c>public GameObject WillEvolutionObject</c> (:82).</summary>
    public object? WillEvolutionObject;

    /// <summary>AS-IS <c>public GameObject WillBeDeletedObject</c> (:85).</summary>
    public object? WillBeDeletedObject;

    /// <summary>AS-IS <c>public GameObject WillBeDeckBounceObject</c> (:88).</summary>
    public object? WillBeDeckBounceObject;

    /// <summary>AS-IS <c>public GameObject WillBeHandBounceObject</c> (:91).</summary>
    public object? WillBeHandBounceObject;

    /// <summary>AS-IS <c>public GameObject WillRemoveFieldObject</c> (:94).</summary>
    public object? WillRemoveFieldObject;

    /// <summary>AS-IS <c>public GameObject WillUntapObject</c> (:97).</summary>
    public object? WillUntapObject;

    /// <summary>AS-IS <c>public List&lt;TextCardColorMaterial&gt; textCardColorMaterials</c> (:99) — the
    /// per-colour text material table.</summary>
    public List<TextCardColorMaterial> textCardColorMaterials = new List<TextCardColorMaterial>();

    /// <summary>AS-IS <c>public UnityAction&lt;FieldPermanentCard&gt; OnClickAction</c> (:101).</summary>
    public Action<FieldPermanentCard>? OnClickAction;

    /// <summary>AS-IS <c>public Permanent ThisPermanent { get; set; }</c> (:102) — the permanent this widget
    /// draws. Plain state in the original; mirrored as plain state.</summary>
    public Permanent? ThisPermanent { get; set; }

    /// <summary>AS-IS <c>public bool IsEffectPlaying { get; set; }</c> (:103) — set while an animation on this
    /// widget is running.</summary>
    public bool IsEffectPlaying { get; set; }

    /// <summary>AS-IS <c>IEnumerator ShowAddDigivolutionCardEffect()</c> (:179).</summary>
    public Task ShowAddDigivolutionCardEffect() => Task.CompletedTask;

    /// <summary>AS-IS <c>OffPermanentIndexText()</c> (:190) — hides the jogress order badge.</summary>
    public void OffPermanentIndexText()
    {
    }

    /// <summary>AS-IS <c>SetPermanentIndexText(List&lt;Permanent&gt;)</c> (:198) — writes this permanent's
    /// 1-based position in the picked-roots list onto the badge.</summary>
    public void SetPermanentIndexText(List<Permanent> permanents)
    {
    }

    /// <summary>AS-IS <c>OnSkillName(ICardEffect)</c> (:220) — shows the firing effect's name strip.</summary>
    public void OnSkillName(ICardEffect cardEffect)
    {
    }

    /// <summary>AS-IS <c>OffSkillName()</c> (:230).</summary>
    public void OffSkillName()
    {
    }

    /// <summary>AS-IS <c>OffUsingSkillEffect()</c> (:238).</summary>
    public void OffUsingSkillEffect()
    {
    }

    /// <summary>AS-IS <c>OnUsingSkillEffect()</c> (:246).</summary>
    public void OnUsingSkillEffect()
    {
    }

    /// <summary>AS-IS <c>OnClick()</c> (:261) — invokes the stored click callback. Headless nothing is stored.
    /// </summary>
    public void OnClick()
    {
    }

    /// <summary>AS-IS <c>AddClickTarget(UnityAction&lt;FieldPermanentCard&gt;)</c> (:290).</summary>
    public void AddClickTarget(Action<FieldPermanentCard> _OnClickAction)
    {
    }

    /// <summary>AS-IS <c>RemoveClickTarget()</c> (:297).</summary>
    public void RemoveClickTarget()
    {
    }

    /// <summary>AS-IS <c>SetPermanentData(Permanent, bool)</c> (:305) — binds a permanent to this widget and
    /// redraws it. View-only: it copies permanent state onto the UI, never back.</summary>
    public void SetPermanentData(Permanent permanent, bool updateIsTapped)
    {
    }

    /// <summary>AS-IS <c>ShowPermanentData(bool)</c> (:435) — the redraw pass (DP/level/markers/outline).
    /// </summary>
    public void ShowPermanentData(bool updateIsTapped)
    {
    }

    /// <summary>AS-IS <c>public bool destroyed { get; set; } = false</c> (:613) — set by the widget's destroy
    /// coroutine.</summary>
    public bool destroyed { get; set; } = false;

    /// <summary>AS-IS <c>public bool skipDestroy { get; set; } = false</c> (:614).</summary>
    public bool skipDestroy { get; set; } = false;

    /// <summary>AS-IS <c>public bool skipUpdate { get; set; } = false</c> (:615) — suppresses the LateUpdate
    /// redraw.</summary>
    public bool skipUpdate { get; set; } = false;

    /// <summary>AS-IS <c>PointerDown(BaseEventData)</c> (:708) — long-press detection; parameter dropped (see
    /// file header).</summary>
    public void PointerDown()
    {
    }

    /// <summary>AS-IS <c>PointerUp(BaseEventData)</c> (:722) — parameter dropped.</summary>
    public void PointerUp()
    {
    }

    /// <summary>AS-IS <c>PointerExit(BaseEventData)</c> (:730) — parameter dropped.</summary>
    public void PointerExit()
    {
    }

    /// <summary>AS-IS <c>public bool isExpand { get; set; }</c> (:748) — whether the widget is currently scaled
    /// up by the selection effect.</summary>
    public bool isExpand { get; set; }

    /// <summary>AS-IS <c>OnSelectEffect(float)</c> (:751) — shows the outline and scales the widget up.
    /// </summary>
    public void OnSelectEffect(float expand)
    {
    }

    /// <summary>AS-IS <c>SetOrangeOutline()</c> (:772).</summary>
    public void SetOrangeOutline()
    {
    }

    /// <summary>AS-IS <c>SetBlueOutline()</c> (:779).</summary>
    public void SetBlueOutline()
    {
    }

    /// <summary>AS-IS <c>RemoveSelectEffect()</c> (:818) — hides the outline and restores the start scale.
    /// </summary>
    public void RemoveSelectEffect()
    {
    }

    /// <summary>AS-IS <c>CloseCommandPanel()</c> (:842).</summary>
    public void CloseCommandPanel()
    {
    }

    /// <summary>AS-IS <c>public UnityAction&lt;FieldPermanentCard&gt; OnBeginDragAction { get; set; }</c>
    /// (:852).</summary>
    public Action<FieldPermanentCard>? OnBeginDragAction { get; set; }

    /// <summary>AS-IS <c>RemoveDragTarget()</c> (:856).</summary>
    public void RemoveDragTarget()
    {
    }

    /// <summary>AS-IS <c>OnBeginDrag()</c> (:870).</summary>
    public void OnBeginDrag()
    {
    }

    /// <summary>AS-IS <c>OnDrag()</c> (:875).</summary>
    public void OnDrag()
    {
    }

    /// <summary>AS-IS <c>OnEndDrag()</c> (:880).</summary>
    public void OnEndDrag()
    {
    }
}

/// <summary>Headless no-op stand-in for the AS-IS <c>TextCardColorMaterial</c> (FieldPermanentCard.cs:888) — one
/// row of the card-colour → text-material table. Its <c>Material material</c> field is dropped (see the
/// FieldPermanentCard file header).</summary>
public class TextCardColorMaterial
{
    /// <summary>AS-IS <c>public CardColor cardColor</c> (FieldPermanentCard.cs:890).</summary>
    public CardColor cardColor;
}
