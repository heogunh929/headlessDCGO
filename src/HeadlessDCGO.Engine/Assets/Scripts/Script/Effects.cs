// Mirrored from DCGO/Assets/Scripts/Script/Effects.cs (2306 lines).
// NO-OP PRESENTATION SEAM for substrate root S1 (phase A1). The AS-IS `Effects` MonoBehaviour is the animation
// bank hanging off the GManager GameObject: every public member is either a serialized prefab/sprite/AudioClip
// handle or a coroutine that instantiates a particle prefab, runs DOTween moves/scales, plays an SE, waits, and
// destroys the object again. Card/rule mirrors reach it as `GManager.instance.GetComponent<Effects>().X(...)`;
// those statements were deleted during porting and this file re-creates the type so they can be restored
// verbatim later (phase A1 creates the class only — no call site is restored here).
//
// SIGNATURE CHANGES (rule 4 — no Unity types, and no mirror type exists for `HandCard`):
//   - every `IEnumerator` member -> `Task` returning `Task.CompletedTask` (established coroutine translation);
//   - `public GameObject <field>` -> `public object?` : DeleteHandCardEffect, FieldUnitEffectPrefab,
//     NewUnitEffect_OnLand, EvolutionUnitEffect, Red/Blue/Green/Yellow/Purple/Orange/Black/Silver/Dragon/Pink/
//     WhiteEvolutionEffect, ShowUseHandCardEffectPrefab, BattleAnimationPrefab;
//   - `public AudioClip <field>` -> `public object?` : EvolutionSE, EvolutionSE_Ultimate, BuffSE, DebuffSE,
//     DigiXrosSelectCardEffectSE, AssemblySelectCardEffectSE, BattleSE;
//   - `public Transform <field>` -> `public object?` : ShowCardParent, ShowCardParent2, ShowUseHandCardParent;
//   - `public TextMeshProUGUI <field>` -> `public object?` : ShowCardTitleText, ShowCardTitleText2;
//   - `public CinemachineImpulseSource impulseSource` -> `public object?`;
//   - `public HandCard ShowUseHandCard` -> `public object?` and
//     `ShrinkUpUseHandCard(HandCard handCard)` -> `ShrinkUpUseHandCard(object? handCard)` — the mirror has no
//     `HandCard` type (src/.../Script/HandCard.cs is a comment-only stub) and HandCard is not in this phase's
//     file list;
//   - `static IEnumerator DeleteCoroutine(GameObject effect, FieldPermanentCard fieldPermanentCard)` ->
//     `static Task DeleteCoroutine(object? effect, FieldPermanentCard fieldPermanentCard)`;
//   - `MoveToExecuteCardEffect_SetPosition(CardSource card, Vector3 startPos)` -> the `Vector3 startPos`
//     PARAMETER IS DROPPED (no neutral vector type exists in the substrate and rule 4 forbids adding a Unity
//     shim); the member is kept as `MoveToExecuteCardEffect_SetPosition(CardSource card)`.
//   All these fields are live scene objects in the original, so headless they are always null (rule 2).
//
// VALUE MEMBERS KEPT REAL (rule 2 — they are plain constants/state in the original, not display objects):
//   - `waitTime_DeleteHandEffect` => 0.29f (Effects.cs:55, a get-only auto-property initialised to 0.29f);
//   - `canCloseByClick { get; set; } = true` (Effects.cs:913).
//
// OMITTED MEMBERS (all private/serialized in the original, all pure Unity scene wiring):
//   `effectParent`, `ShowEffectDiscriptionObjectPrefab`, `ShowEffectDiscriptionObjectParent`,
//   `renderingFiedlPermanentCard`, `renderingHandCard`, the cached `hideShowCard`/`hideShowCard2` coroutine
//   handles, and every private helper coroutine they drive.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

/// <summary>Headless no-op stand-in for the AS-IS <c>Effects</c> animation bank. Member names, order and
/// parameter lists mirror DCGO/Assets/Scripts/Script/Effects.cs; all behaviour is stripped.</summary>
public class Effects
{
    /// <summary>AS-IS <c>Init()</c> (Effects.cs:14) — hides the show-card panels and clears the effect-text
    /// column at scene start.</summary>
    public void Init()
    {
    }

    /// <summary>AS-IS <c>ShowActivateCardEffectDiscription(ICardEffect)</c> (Effects.cs:36) — spawns the
    /// "activating effect" description row.</summary>
    public Task ShowActivateCardEffectDiscription(ICardEffect cardEffect) => Task.CompletedTask;

    /// <summary>AS-IS <c>public GameObject DeleteHandCardEffect</c> (Effects.cs:52).</summary>
    public object? DeleteHandCardEffect;

    /// <summary>AS-IS <c>public float waitTime_DeleteHandEffect { get; } = 0.29f</c> (Effects.cs:55) — the
    /// hand-card vanish duration. A plain constant in the original, mirrored verbatim.</summary>
    public float waitTime_DeleteHandEffect { get; } = 0.29f;

    /// <summary>AS-IS <c>DeleteHandCardEffectCoroutine(CardSource)</c> (Effects.cs:58).</summary>
    public Task DeleteHandCardEffectCoroutine(CardSource card) => Task.CompletedTask;

    /// <summary>AS-IS <c>static IEnumerator DeleteCoroutine(GameObject, FieldPermanentCard)</c>
    /// (Effects.cs:177) — waits out an effect object's lifetime, then destroys it (and the card widget).
    /// </summary>
    public static Task DeleteCoroutine(object? effect, FieldPermanentCard fieldPermanentCard) => Task.CompletedTask;

    /// <summary>AS-IS <c>public GameObject FieldUnitEffectPrefab</c> (Effects.cs:192).</summary>
    public object? FieldUnitEffectPrefab;

    /// <summary>AS-IS <c>ActivateFieldPokemonSkillEffect(Permanent, ICardEffect)</c> (Effects.cs:193) — the
    /// "this permanent's effect is firing" flash + skill-name banner.</summary>
    public Task ActivateFieldPokemonSkillEffect(Permanent permanent, ICardEffect cardEffect) => Task.CompletedTask;

    /// <summary>AS-IS <c>ActivateTrashCardSkillEffect(ICardEffect)</c> (Effects.cs:214).</summary>
    public Task ActivateTrashCardSkillEffect(ICardEffect cardEffect) => Task.CompletedTask;

    /// <summary>AS-IS <c>ActivateExecutingCardSkillEffect(CardSource, ICardEffect)</c> (Effects.cs:293).</summary>
    public Task ActivateExecutingCardSkillEffect(CardSource cardSource, ICardEffect cardEffect) => Task.CompletedTask;

    /// <summary>AS-IS <c>ActivateHandCardSkillEffect(CardSource, ICardEffect)</c> (Effects.cs:378).</summary>
    public Task ActivateHandCardSkillEffect(CardSource cardSource, ICardEffect cardEffect) => Task.CompletedTask;

    /// <summary>AS-IS <c>public GameObject NewUnitEffect_OnLand</c> (Effects.cs:450).</summary>
    public object? NewUnitEffect_OnLand;

    /// <summary>AS-IS <c>public GameObject EvolutionUnitEffect</c> (Effects.cs:453).</summary>
    public object? EvolutionUnitEffect;

    /// <summary>AS-IS <c>public GameObject RedEvolutionEffect</c> (Effects.cs:456).</summary>
    public object? RedEvolutionEffect;

    /// <summary>AS-IS <c>public GameObject BlueEvolutionEffect</c> (Effects.cs:459).</summary>
    public object? BlueEvolutionEffect;

    /// <summary>AS-IS <c>public GameObject GreenEvolutionEffect</c> (Effects.cs:462).</summary>
    public object? GreenEvolutionEffect;

    /// <summary>AS-IS <c>public GameObject YellowEvolutionEffect</c> (Effects.cs:465).</summary>
    public object? YellowEvolutionEffect;

    /// <summary>AS-IS <c>public GameObject PurpleEvolutionEffect</c> (Effects.cs:468).</summary>
    public object? PurpleEvolutionEffect;

    /// <summary>AS-IS <c>public GameObject OrangeEvolutionEffect</c> (Effects.cs:471).</summary>
    public object? OrangeEvolutionEffect;

    /// <summary>AS-IS <c>public GameObject BlackEvolutionEffect</c> (Effects.cs:474).</summary>
    public object? BlackEvolutionEffect;

    /// <summary>AS-IS <c>public GameObject SilverEvolutionEffect</c> (Effects.cs:477).</summary>
    public object? SilverEvolutionEffect;

    /// <summary>AS-IS <c>public GameObject DragonEvolutionEffect</c> (Effects.cs:480).</summary>
    public object? DragonEvolutionEffect;

    /// <summary>AS-IS <c>public GameObject PinkEvolutionEffect</c> (Effects.cs:483).</summary>
    public object? PinkEvolutionEffect;

    /// <summary>AS-IS <c>public GameObject WhiteEvolutionEffect</c> (Effects.cs:486).</summary>
    public object? WhiteEvolutionEffect;

    /// <summary>AS-IS <c>CreateFieldPermanentCardEffect(FieldPermanentCard, bool, CardSource[], bool)</c>
    /// (Effects.cs:488) — the play/DigiXros landing animation.</summary>
    public Task CreateFieldPermanentCardEffect(
        FieldPermanentCard fieldPermanentCard,
        bool isDigiXros,
        CardSource[]? jogressEvoRoots = null,
        bool HasETB = false) => Task.CompletedTask;

    /// <summary>AS-IS <c>public AudioClip EvolutionSE</c> (Effects.cs:600).</summary>
    public object? EvolutionSE;

    /// <summary>AS-IS <c>public AudioClip EvolutionSE_Ultimate</c> (Effects.cs:603).</summary>
    public object? EvolutionSE_Ultimate;

    /// <summary>AS-IS <c>public CinemachineImpulseSource impulseSource</c> (Effects.cs:606) — the camera-shake
    /// source.</summary>
    public object? impulseSource;

    /// <summary>AS-IS <c>DigivolveFieldPermanentCardEffect(FieldPermanentCard, bool, bool, bool)</c>
    /// (Effects.cs:607).</summary>
    public Task DigivolveFieldPermanentCardEffect(
        FieldPermanentCard targetFieldPermanentCard,
        bool isBurst,
        bool isBlast,
        bool isAppFusion) => Task.CompletedTask;

    /// <summary>AS-IS <c>BounceEffect(Permanent, bool)</c> (Effects.cs:734) — the return-to-hand animation.
    /// </summary>
    public Task BounceEffect(Permanent permanent, bool playSE = true) => Task.CompletedTask;

    /// <summary>AS-IS <c>DeckBounceEffect(Permanent)</c> (Effects.cs:813).</summary>
    public Task DeckBounceEffect(Permanent permanent) => Task.CompletedTask;

    /// <summary>AS-IS <c>public Transform ShowCardParent</c> (Effects.cs:909).</summary>
    public object? ShowCardParent;

    /// <summary>AS-IS <c>public TextMeshProUGUI ShowCardTitleText</c> (Effects.cs:912).</summary>
    public object? ShowCardTitleText;

    /// <summary>AS-IS <c>public bool canCloseByClick { get; set; } = true</c> (Effects.cs:913) — whether the
    /// reveal panel may be dismissed by clicking. Plain state in the original, mirrored verbatim.</summary>
    public bool canCloseByClick { get; set; } = true;

    /// <summary>AS-IS <c>ShowCardEffect(List&lt;CardSource&gt;, string, bool, bool)</c> (Effects.cs:914) — the
    /// card-reveal panel.</summary>
    public Task ShowCardEffect(List<CardSource> ShownCards, string Title, bool willHide, bool ShowReverseCard) =>
        Task.CompletedTask;

    /// <summary>AS-IS <c>HideShowCard()</c> (Effects.cs:997).</summary>
    public Task HideShowCard() => Task.CompletedTask;

    /// <summary>AS-IS <c>OffShowCard()</c> (Effects.cs:1022).</summary>
    public void OffShowCard()
    {
    }

    /// <summary>AS-IS <c>OnClickShowCardBackground1()</c> (Effects.cs:1032).</summary>
    public void OnClickShowCardBackground1()
    {
    }

    /// <summary>AS-IS <c>public Transform ShowCardParent2</c> (Effects.cs:1043).</summary>
    public object? ShowCardParent2;

    /// <summary>AS-IS <c>public TextMeshProUGUI ShowCardTitleText2</c> (Effects.cs:1046).</summary>
    public object? ShowCardTitleText2;

    /// <summary>AS-IS <c>ShowCardEffect2(List&lt;CardSource&gt;, string, bool, bool)</c> (Effects.cs:1047) — the
    /// second, independently stacked reveal panel.</summary>
    public Task ShowCardEffect2(List<CardSource> ShownCards, string Title, bool willHide, bool ShowReverseCard) =>
        Task.CompletedTask;

    /// <summary>AS-IS <c>HideShowCard2()</c> (Effects.cs:1121).</summary>
    public Task HideShowCard2() => Task.CompletedTask;

    /// <summary>AS-IS <c>OffShowCard2()</c> (Effects.cs:1146).</summary>
    public void OffShowCard2()
    {
    }

    /// <summary>AS-IS <c>OnClickShowCardBackground2()</c> (Effects.cs:1156).</summary>
    public void OnClickShowCardBackground2()
    {
    }

    /// <summary>AS-IS <c>public GameObject ShowUseHandCardEffectPrefab</c> (Effects.cs:1167).</summary>
    public object? ShowUseHandCardEffectPrefab;

    /// <summary>AS-IS <c>public HandCard ShowUseHandCard</c> (Effects.cs:1170) — the enlarged centre-screen card
    /// widget. No <c>HandCard</c> mirror type exists (see file header); always null headless.</summary>
    public object? ShowUseHandCard;

    /// <summary>AS-IS <c>public Transform ShowUseHandCardParent</c> (Effects.cs:1173).</summary>
    public object? ShowUseHandCardParent;

    /// <summary>AS-IS <c>AddHandCardEffect(CardSource)</c> (Effects.cs:1174) — the draw animation.</summary>
    public Task AddHandCardEffect(CardSource cardSource) => Task.CompletedTask;

    /// <summary>AS-IS <c>ShrinkUpUseHandCard(HandCard)</c> (Effects.cs:1267).</summary>
    public Task ShrinkUpUseHandCard(object? handCard) => Task.CompletedTask;

    /// <summary>AS-IS <c>ShowUseHandCardEffect_PlayCard(CardSource)</c> (Effects.cs:1318).</summary>
    public Task ShowUseHandCardEffect_PlayCard(CardSource card) => Task.CompletedTask;

    /// <summary>AS-IS <c>ShowUseHandCardEffect(CardSource)</c> (Effects.cs:1335).</summary>
    public Task ShowUseHandCardEffect(CardSource card) => Task.CompletedTask;

    /// <summary>AS-IS <c>public AudioClip BuffSE</c> (Effects.cs:1431).</summary>
    public object? BuffSE;

    /// <summary>AS-IS <c>CreateBuffEffect(Permanent)</c> (Effects.cs:1433).</summary>
    public Task CreateBuffEffect(Permanent permanent) => Task.CompletedTask;

    /// <summary>AS-IS <c>public AudioClip DebuffSE</c> (Effects.cs:1472).</summary>
    public object? DebuffSE;

    /// <summary>AS-IS <c>CreateDebuffEffect(Permanent)</c> (Effects.cs:1474).</summary>
    public Task CreateDebuffEffect(Permanent permanent) => Task.CompletedTask;

    /// <summary>AS-IS <c>public AudioClip DigiXrosSelectCardEffectSE</c> (Effects.cs:1521).</summary>
    public object? DigiXrosSelectCardEffectSE;

    /// <summary>AS-IS <c>CreateDigiXrosSelectCardEffect(Permanent, Player)</c> (Effects.cs:1523).</summary>
    public Task CreateDigiXrosSelectCardEffect(Permanent permanent, Player? player = null) => Task.CompletedTask;

    /// <summary>AS-IS <c>public AudioClip AssemblySelectCardEffectSE</c> (Effects.cs:1560).</summary>
    public object? AssemblySelectCardEffectSE;

    /// <summary>AS-IS <c>CreateAssemblySelectCardEffect(Permanent, Player)</c> (Effects.cs:1562).</summary>
    public Task CreateAssemblySelectCardEffect(Permanent permanent, Player? player = null) => Task.CompletedTask;

    /// <summary>AS-IS <c>FreezePermanentEffect(Permanent)</c> (Effects.cs:1598).</summary>
    public Task FreezePermanentEffect(Permanent permanent) => Task.CompletedTask;

    /// <summary>AS-IS <c>CreateRecoveryEffect(Player)</c> (Effects.cs:1633).</summary>
    public Task CreateRecoveryEffect(Player player) => Task.CompletedTask;

    /// <summary>AS-IS <c>BreakSecurityEffect(Player)</c> (Effects.cs:1653).</summary>
    public Task BreakSecurityEffect(Player player) => Task.CompletedTask;

    /// <summary>AS-IS <c>DestroyPermanentEffect(Permanent)</c> (Effects.cs:1688).</summary>
    public Task DestroyPermanentEffect(Permanent permanent) => Task.CompletedTask;

    /// <summary>AS-IS <c>EnterSecurityCardEffect(CardSource)</c> (Effects.cs:1780).</summary>
    public Task EnterSecurityCardEffect(CardSource card) => Task.CompletedTask;

    /// <summary>AS-IS <c>MoveToExecuteCardEffect_SetPosition(CardSource, Vector3)</c> (Effects.cs:1862) — the
    /// <c>Vector3 startPos</c> parameter is dropped (see file header).</summary>
    public Task MoveToExecuteCardEffect_SetPosition(CardSource card) => Task.CompletedTask;

    /// <summary>AS-IS <c>MoveToExecuteCardEffect(CardSource)</c> (Effects.cs:1875).</summary>
    public Task MoveToExecuteCardEffect(CardSource card) => Task.CompletedTask;

    /// <summary>AS-IS <c>DestroySecurityEffect(CardSource)</c> (Effects.cs:1941).</summary>
    public Task DestroySecurityEffect(CardSource destroyedSecurityCard) => Task.CompletedTask;

    /// <summary>AS-IS <c>public GameObject BattleAnimationPrefab</c> (Effects.cs:2029).</summary>
    public object? BattleAnimationPrefab;

    /// <summary>AS-IS <c>public AudioClip BattleSE</c> (Effects.cs:2032).</summary>
    public object? BattleSE;

    /// <summary>AS-IS <c>BattleEffect(List&lt;Permanent&gt;, List&lt;Permanent&gt;, CardSource)</c>
    /// (Effects.cs:2033).</summary>
    public Task BattleEffect(List<Permanent> WinnerPermanents, List<Permanent> LoserPermanents, CardSource LoserCard) =>
        Task.CompletedTask;

    /// <summary>AS-IS <c>RemoveDigivolveRootEffect(CardSource, Permanent)</c> (Effects.cs:2162).</summary>
    public Task RemoveDigivolveRootEffect(CardSource card, Permanent targetPermanent) => Task.CompletedTask;

    /// <summary>AS-IS <c>FailedPlayCardEffect(CardSource)</c> (Effects.cs:2267).</summary>
    public Task FailedPlayCardEffect(CardSource cardSource) => Task.CompletedTask;
}
