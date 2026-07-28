// Mirrored from DCGO/Assets/Scripts/Script/ContinuousController.cs (1843 lines).
// PRESENTATION SEAM for substrate root S1 (phase A1) — MOSTLY no-op, with FOUR REAL members.
//
// The AS-IS `ContinuousController` is the app-lifetime singleton living in its own additive scene: card-entity
// tables, deck-recipe persistence (PlayerPrefs), sound/BGM volume, Photon room plumbing, scene loading — and,
// mixed in, a handful of plain gameplay-option flags that RULE code reads. This seam keeps the flags real and
// no-ops everything else.
//
// REAL MEMBERS (rule 3 — verified one by one in the original; each is plain state there too):
//   - `isAI`                   AS-IS :1323 `public bool isAI { get; set; } = false;`
//                              (written by the mode-select/lobby screens, read by GManager.cs:258/511 and
//                              TurnStateMachine.cs:3319/3357).
//   - `isRandomMatch`          AS-IS :64   `public bool isRandomMatch { get; set; }` (default false; written by
//                              the lobby screens and TurnStateMachine.cs:54, read at :3319 and GManager.cs:511).
//   - `autoMaxCardCount`       AS-IS :853  `[HideInInspector] public bool autoMaxCardCount = false;`
//                              (read by SelectCountEffect.cs:118 — auto-pick the max count).
//   - `autoMinDigivolutionCost` AS-IS :838 `[HideInInspector] public bool autoMinDigivolutionCost = false;`
//                              (read by SelectCountEffect.cs:119 and CardController.cs:527).
//   They are mirrored with the AS-IS shape (two auto-properties, two fields) and the AS-IS default (false).
//   Substrate home: the owning INSTANCE is resolved from `AmbientMatchContext.Current` and cached in that
//   EngineContext's service registry (the GManager.instance precedent), so writes persist for the whole match
//   and N concurrent matches never collide. NOTE FOR THE REPORT: AS-IS these four are APP-scoped (a
//   cross-scene singleton fed by PlayerPrefs), not match-scoped, and no substrate setting store exists —
//   nothing outside a match scope can set them, and they reset to the AS-IS default at every new context.
//   A real home (an engine-level options object seeded per match) is a wiring decision, not invented here.
//
// SIGNATURE CHANGES (rule 4 — no Unity types):
//   - `public static ContinuousController instance = null;` (:508, a FIELD assigned in Awake) -> a static
//     get-only PROPERTY resolving the match-scoped instance; null outside any match scope, exactly like the
//     mirror `GManager.instance`. AS-IS writes to it happen only in Unity's Awake, which has no counterpart.
//   - `StartCoroutine(IEnumerator)` (inherited MonoBehaviour member, the single most-cited deleted call —
//     `yield return ContinuousController.instance.StartCoroutine(X)`) -> `Task StartCoroutine(Task routine)`
//     under the established IEnumerator->Task translation. It RETURNS THE ARGUMENT rather than
//     Task.CompletedTask: a mirror `Task` is already running when the argument is evaluated, so returning a
//     completed task would silently change await semantics. The seam itself adds no behaviour.
//   - `public Sprite ReverseCard` / `ReverseCard_Digitama` (:33-34) -> `public object?`.
//   - `public SoundObject soundObject` (:37) -> `public object?`; `public SoundObject PlaySE(AudioClip clip)`
//     (:1068) -> `public object? PlaySE(object? clip)` returning null (the original returns a freshly
//     Instantiated scene object — absent headless, so null per rule 2).
//   - `public Coroutine LoadingTextCoroutine` (:1144) -> `public object?`.
//   - `public static IEnumerator LoadCoroutine()` (:83) -> `public static Task LoadCoroutine()`;
//     `public IEnumerator EndBattleCoroutine()` (:1156) -> `public Task EndBattleCoroutine()`.
//   - `public async void Init()` (:515) -> `public void Init()` (AS-IS body = targetFrameRate + GameRandom
//     seeding; the substrate seeds through EngineContext.RandomSource, so this is a no-op here).
//   - `ChangeBGMVolume(AudioSource)` / `ChangeSEVolume(AudioSource)` (:1009/:1014) -> the `AudioSource`
//     PARAMETER IS DROPPED (no neutral equivalent; rule 4 forbids adding a Unity shim).
//   - `public String GameVerString => Application.version` (:66) -> returns `string.Empty`: there is no Unity
//     `Application`, and the original's value comes from the player build, not from any headless source.
//
// OMITTED MEMBERS (types with no mirror, or non-presentation logic outside this phase):
//   - `CardList` / `SortedCardList` / the 17 `*Token` properties / `getCardEntityByCardID(int)` (:27-30,
//     :107-123, :1079): all `CEntity_Base`-typed; the mirror CEntity_Base.cs carries only the CardColor enum
//     (card data is served by ICardRepository), so no faithful signature is available.
//   - `BattleDeckData` / `LastBattleDeckData` / `DeckDatas` / `SaveDeckData(DeckData)` / `RenameDeck(DeckData,
//     string)` / `DeleteDeck(DeckData)` (:43-60, :100, :616-648): all `DeckData`-typed; no DeckData mirror type
//     (src/.../Script/DeckData.cs is a comment-only stub).
//   - `public ShuffleDeckCode ShuffleDeckCode` (:40) and `public BanList BanList { get; private set; }` (:125):
//     no mirror type for either.
//   - `public Language language` + the `Language` enum (:1839) — `SaveLanguage()`/`LoadLanguage()` themselves
//     ARE mirrored as no-ops since their signatures do not mention it.
//   - `RandomUtility` (:1480) and `PhotonUtility` (:1585), the two other top-level types in this file:
//     RandomUtility is REAL logic (secure seed, probability roll, deck shuffle) that the substrate already
//     serves through `EngineContext.RandomSource` — porting it here would create a second, competing RNG, so
//     it is deliberately left out of a presentation seam; PhotonUtility is networking only.
//   - the `#if UNITY_EDITOR` Photon debug HUD (:1330-1478, private `PhotonDebugListener`), `Awake`, `OnEnable`,
//     `OnDisable`, `LoadBanListOnline`, `LoadVolume`, `SetRandomCoroutine`, and every private PlayerPrefs key
//     field.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script;

using HeadlessDCGO.Engine.Headless.Bridge;
using UnityEngine;

/// <summary>Headless stand-in for the AS-IS <c>ContinuousController</c> app singleton. Member names, order and
/// parameter lists mirror DCGO/Assets/Scripts/Script/ContinuousController.cs. Everything is a no-op EXCEPT the
/// gameplay-option flags <see cref="isAI"/>, <see cref="isRandomMatch"/>, <see cref="autoMaxCardCount"/> and
/// <see cref="autoMinDigivolutionCost"/>, which carry real per-match state (see the file header).</summary>
public class ContinuousController
{
    /// <summary>AS-IS <c>public float GameVer</c> (ContinuousController.cs:21).</summary>
    public float GameVer;

    /// <summary>AS-IS <c>public bool IgnoreUpdate</c> (:24) — skips the update-required check.</summary>
    public bool IgnoreUpdate;

    /// <summary>AS-IS <c>public Sprite ReverseCard</c> (:33) — the card-back image.</summary>
    public object? ReverseCard;

    /// <summary>AS-IS <c>public Sprite ReverseCard_Digitama</c> (:34).</summary>
    public object? ReverseCard_Digitama;

    /// <summary>AS-IS <c>public SoundObject soundObject</c> (:37) — the SE prefab cloned by
    /// <see cref="PlaySE"/>.</summary>
    public object? soundObject;

    /// <summary>AS-IS <c>public bool NeedUpdate { get; set; }</c> (:62).</summary>
    public bool NeedUpdate { get; set; }

    /// <summary>REAL (rule 3). AS-IS <c>public bool isRandomMatch { get; set; }</c> (:64) — set when the seat
    /// came from random matchmaking (or an AI match); read by the mirror-side rule code AS-IS reads it from
    /// (GManager.cs:511, TurnStateMachine.cs:3319). Default false, as in the original.</summary>
    public bool isRandomMatch { get; set; }

    /// <summary>AS-IS <c>public String GameVerString => Application.version</c> (:66). No Unity
    /// <c>Application</c> headless — returns <see cref="string.Empty"/> (see file header).</summary>
    public string GameVerString => string.Empty;

    /// <summary>AS-IS <c>public static string DeckDataPropertyKey => "BattleDeckData"</c> (:68) — a plain
    /// constant, mirrored verbatim.</summary>
    public static string DeckDataPropertyKey => "BattleDeckData";

    /// <summary>AS-IS <c>public static string PlayerNameKey => "PlayerNameKey"</c> (:72) — verbatim.</summary>
    public static string PlayerNameKey => "PlayerNameKey";

    /// <summary>AS-IS <c>public static string WinCountKey => "WinCountKey"</c> (:76) — verbatim.</summary>
    public static string WinCountKey => "WinCountKey";

    /// <summary>AS-IS <c>public int PlayerNameMaxLength</c> (:80).</summary>
    public int PlayerNameMaxLength;

    /// <summary>AS-IS <c>public static IEnumerator LoadCoroutine()</c> (:83) — additively loads the
    /// ContinuousControllerScene and waits for its Awake. Scene plumbing: no-op.</summary>
    public static Task LoadCoroutine() => Task.CompletedTask;

    /// <summary>AS-IS <c>public string DeckDatasPlayerPrefsKey { get { return "DeckDatas3"; } }</c> (:104) — a
    /// plain constant, mirrored verbatim.</summary>
    public string DeckDatasPlayerPrefsKey { get { return "DeckDatas3"; } }

    /// <summary>AS-IS <c>public static ContinuousController instance</c> (:508, a field assigned in Awake).
    /// Resolves the match-scoped instance from <see cref="AmbientMatchContext"/> (cached in that context's
    /// service registry), or null outside any match scope — the same shape as the mirror
    /// <c>GManager.instance</c>. See the file header for why this is a get-only property.</summary>
    public static ContinuousController? instance
    {
        get
        {
            if (AmbientMatchContext.Current is not { } context)
            {
                return null;
            }

            if (context.TryGetService(out ContinuousController? existing) && existing is not null)
            {
                return existing;
            }

            var created = new ContinuousController();
            context.RegisterService(created);
            return created;
        }
    }

    /// <summary>Unity <c>MonoBehaviour.StartCoroutine</c> as the AS-IS call sites use it
    /// (<c>ContinuousController.instance.StartCoroutine(X)</c>), under the IEnumerator-&gt;Task translation.
    /// Returns the argument unchanged (see file header) — the seam adds nothing.
    /// RETIRING: kept for the mirror files still written as <c>async Task</c>; the
    /// <see cref="StartCoroutine(System.Collections.IEnumerator)"/> overload below is the AS-IS one.</summary>
    public Task StartCoroutine(Task routine) => routine;

    /// <summary>(coroutine restore) Unity <c>MonoBehaviour.StartCoroutine(IEnumerator)</c> — the AS-IS
    /// signature, so <c>yield return ContinuousController.instance.StartCoroutine(x);</c> compiles verbatim
    /// (14205 AS-IS sites). Returns a COLD <see cref="Coroutine"/> handle: headless there is no player loop
    /// to hand the routine to, so the routine runs when the handle is yielded and the
    /// <see cref="HeadlessDCGO.Engine.Headless.Coroutines.CoroutineDriver"/> consumes it — which, for the
    /// `yield return StartCoroutine(x)` shape the original always writes, is the same net order Unity
    /// produces. For the fire-and-forget shape (handle discarded) see
    /// <c>CoroutineDriver.RunDetached</c>.</summary>
    public Coroutine StartCoroutine(System.Collections.IEnumerator routine) => new(routine);

    /// <summary>AS-IS <c>public async void Init()</c> (:515) — frame-rate cap + GameRandom seeding. No-op
    /// headless (the substrate seeds through its own random source).</summary>
    public void Init()
    {
    }

    /// <summary>AS-IS <c>ModifyAllDeckDatas()</c> (:579) — deck-recipe migration. No-op.</summary>
    public void ModifyAllDeckDatas()
    {
    }

    /// <summary>AS-IS <c>SaveDeckDatas()</c> (:609) — PlayerPrefs write. No-op.</summary>
    public void SaveDeckDatas()
    {
    }

    /// <summary>AS-IS <c>DeleteAllDecks()</c> (:649). No-op.</summary>
    public void DeleteAllDecks()
    {
    }

    /// <summary>AS-IS <c>LoadDeckLists()</c> (:657) — PlayerPrefs read. No-op.</summary>
    public void LoadDeckLists()
    {
    }

    /// <summary>AS-IS <c>public string PlayerName</c> (:733) — returns "Player" while the backing field is
    /// empty; the setter runs the value through <c>DeckData.ValidateDeckName</c>. Headless the setter is a
    /// no-op (no DeckData mirror), so the getter always answers the original's empty-field value.</summary>
    public string PlayerName
    {
        get => "Player";
        set { }
    }

    /// <summary>AS-IS <c>SavePlayerName(string)</c> (:751). No-op.</summary>
    public void SavePlayerName(string playerName)
    {
    }

    /// <summary>AS-IS <c>LoadPlayerName()</c> (:758). No-op.</summary>
    public void LoadPlayerName()
    {
    }

    /// <summary>AS-IS <c>public int WinCount { get; set; }</c> (:774) — plain state, mirrored as such.
    /// </summary>
    public int WinCount { get; set; }

    /// <summary>AS-IS <c>SaveWinCount()</c> (:777). No-op.</summary>
    public void SaveWinCount()
    {
    }

    /// <summary>AS-IS <c>LoadWinCount()</c> (:782). No-op.</summary>
    public void LoadWinCount()
    {
    }

    /// <summary>AS-IS <c>[HideInInspector] public bool autoEffectOrder = false</c> (:793) — auto-order the
    /// trigger stack. Kept as an AS-IS-shaped field at its AS-IS default; its PlayerPrefs load is a no-op.
    /// </summary>
    public bool autoEffectOrder = false;

    /// <summary>AS-IS <c>SaveAutoEffectOrder()</c> (:796). No-op.</summary>
    public void SaveAutoEffectOrder()
    {
    }

    /// <summary>AS-IS <c>LoadAutoEffectOrder()</c> (:801). No-op.</summary>
    public void LoadAutoEffectOrder()
    {
    }

    /// <summary>AS-IS <c>[HideInInspector] public bool autoDeckBottomOrder = false</c> (:807).</summary>
    public bool autoDeckBottomOrder = false;

    /// <summary>AS-IS <c>SaveAutoDeckBottomOrder()</c> (:811). No-op.</summary>
    public void SaveAutoDeckBottomOrder()
    {
    }

    /// <summary>AS-IS <c>LoadAutoDeckBottomOrder()</c> (:816). No-op.</summary>
    public void LoadAutoDeckBottomOrder()
    {
    }

    /// <summary>AS-IS <c>[HideInInspector] public bool autoDeckTopOrder = false</c> (:822).</summary>
    public bool autoDeckTopOrder = false;

    /// <summary>AS-IS <c>SaveAutoDeckTopOrder()</c> (:826). No-op.</summary>
    public void SaveAutoDeckTopOrder()
    {
    }

    /// <summary>AS-IS <c>LoadAutoDeckTopOrder()</c> (:831). No-op.</summary>
    public void LoadAutoDeckTopOrder()
    {
    }

    /// <summary>REAL (rule 3). AS-IS <c>[HideInInspector] public bool autoMinDigivolutionCost = false</c>
    /// (:838) — when set, the digivolution-cost count prompt auto-picks the minimum (SelectCountEffect.cs:119,
    /// CardController.cs:527). Mirrored as an AS-IS-shaped public field at the AS-IS default.</summary>
    public bool autoMinDigivolutionCost = false;

    /// <summary>AS-IS <c>SaveAutoMinDigivolutionCost()</c> (:841) — PlayerPrefs write. No-op (no substrate
    /// setting store); the in-memory flag above keeps whatever the match set.</summary>
    public void SaveAutoMinDigivolutionCost()
    {
    }

    /// <summary>AS-IS <c>LoadAutoMinDigivolutionCost()</c> (:846) — reads the pref, defaulting to false. No-op
    /// headless; the field already holds that default.</summary>
    public void LoadAutoMinDigivolutionCost()
    {
    }

    /// <summary>REAL (rule 3). AS-IS <c>[HideInInspector] public bool autoMaxCardCount = false</c> (:853) —
    /// when set, a non-digivolution-cost count prompt auto-picks the maximum (SelectCountEffect.cs:118).
    /// Mirrored as an AS-IS-shaped public field at the AS-IS default.</summary>
    public bool autoMaxCardCount = false;

    /// <summary>AS-IS <c>SaveAutoMaxCardCount()</c> (:856). No-op (see SaveAutoMinDigivolutionCost).</summary>
    public void SaveAutoMaxCardCount()
    {
    }

    /// <summary>AS-IS <c>LoadAutoMaxCardCount()</c> (:861) — pref default false. No-op headless.</summary>
    public void LoadAutoMaxCardCount()
    {
    }

    /// <summary>AS-IS <c>[HideInInspector] public bool autoHatch = false</c> (:868).</summary>
    public bool autoHatch = false;

    /// <summary>AS-IS <c>SaveAutoHatch()</c> (:871). No-op.</summary>
    public void SaveAutoHatch()
    {
    }

    /// <summary>AS-IS <c>LoadAutoHatch()</c> (:876). No-op.</summary>
    public void LoadAutoHatch()
    {
    }

    /// <summary>AS-IS <c>public bool useBanlist = true</c> (:883) — mirrored with the AS-IS default (true).
    /// </summary>
    public bool useBanlist = true;

    /// <summary>AS-IS <c>SaveUseBanlist()</c> (:886). No-op.</summary>
    public void SaveUseBanlist()
    {
    }

    /// <summary>AS-IS <c>LoadUseBanlist()</c> (:891). No-op.</summary>
    public void LoadUseBanlist()
    {
    }

    /// <summary>AS-IS <c>[HideInInspector] public bool showCutInAnimation = false</c> (:898).</summary>
    public bool showCutInAnimation = false;

    /// <summary>AS-IS <c>SaveShowCutInAnimation()</c> (:901). No-op.</summary>
    public void SaveShowCutInAnimation()
    {
    }

    /// <summary>AS-IS <c>LoadShowCutInAnimation()</c> (:906) — note the original hard-sets false there.
    /// No-op.</summary>
    public void LoadShowCutInAnimation()
    {
    }

    /// <summary>AS-IS <c>[HideInInspector] public bool reverseOpponentsCards = false</c> (:915) — read by the
    /// widgets' rotation pass (e.g. BrainStormObject.RotationCards).</summary>
    public bool reverseOpponentsCards = false;

    /// <summary>AS-IS <c>SaveReverseOpponentsCards()</c> (:918). No-op.</summary>
    public void SaveReverseOpponentsCards()
    {
    }

    /// <summary>AS-IS <c>LoadReverseOpponentsCards()</c> (:923). No-op.</summary>
    public void LoadReverseOpponentsCards()
    {
    }

    /// <summary>AS-IS <c>[HideInInspector] public bool turnSuspendedCards = false</c> (:930).</summary>
    public bool turnSuspendedCards = false;

    /// <summary>AS-IS <c>SaveTurnSuspendedCards()</c> (:933). No-op.</summary>
    public void SaveTurnSuspendedCards()
    {
    }

    /// <summary>AS-IS <c>LoadTurnSuspendedCards()</c> (:938). No-op.</summary>
    public void LoadTurnSuspendedCards()
    {
    }

    /// <summary>AS-IS <c>[HideInInspector] public bool checkBeforeEndingSelection = false</c> (:945).</summary>
    public bool checkBeforeEndingSelection = false;

    /// <summary>AS-IS <c>SaveCheckBeforeEndingSelection()</c> (:948). No-op.</summary>
    public void SaveCheckBeforeEndingSelection()
    {
    }

    /// <summary>AS-IS <c>LoadCheckBeforeEndingSelection()</c> (:953). No-op.</summary>
    public void LoadCheckBeforeEndingSelection()
    {
    }

    /// <summary>AS-IS <c>[HideInInspector] public bool suspendedCardsDirectionIsLeft = false</c> (:960).
    /// </summary>
    public bool suspendedCardsDirectionIsLeft = false;

    /// <summary>AS-IS <c>SaveSuspendedCardsDirectionIsLeft()</c> (:963). No-op.</summary>
    public void SaveSuspendedCardsDirectionIsLeft()
    {
    }

    /// <summary>AS-IS <c>LoadSuspendedCardsDirectionIsLeft()</c> (:968). No-op.</summary>
    public void LoadSuspendedCardsDirectionIsLeft()
    {
    }

    /// <summary>AS-IS <c>[HideInInspector] public bool showBackgroundParticle = false</c> (:975).</summary>
    public bool showBackgroundParticle = false;

    /// <summary>AS-IS <c>SaveShowBackgroundParticle()</c> (:978). No-op.</summary>
    public void SaveShowBackgroundParticle()
    {
    }

    /// <summary>AS-IS <c>LoadShowBackgroundParticle()</c> (:983). No-op.</summary>
    public void LoadShowBackgroundParticle()
    {
    }

    /// <summary>AS-IS <c>public float BGMVolume { get; set; }</c> (:990).</summary>
    public float BGMVolume { get; set; }

    /// <summary>AS-IS <c>public float SEVolume { get; set; }</c> (:991).</summary>
    public float SEVolume { get; set; }

    /// <summary>AS-IS <c>SetBGMVolume(float)</c> (:993) — assigns <see cref="BGMVolume"/> and writes the pref.
    /// No-op headless (audio only).</summary>
    public void SetBGMVolume(float BGMVolume)
    {
    }

    /// <summary>AS-IS <c>SetSEVolume(float)</c> (:1001). No-op.</summary>
    public void SetSEVolume(float SEVolume)
    {
    }

    /// <summary>AS-IS <c>ChangeBGMVolume(AudioSource)</c> (:1009) — parameter dropped (see file header).
    /// </summary>
    public void ChangeBGMVolume()
    {
    }

    /// <summary>AS-IS <c>ChangeSEVolume(AudioSource)</c> (:1014) — parameter dropped.</summary>
    public void ChangeSEVolume()
    {
    }

    /// <summary>AS-IS <c>[HideInInspector] public string serverRegion = "us"</c> (:1037) — mirrored with the
    /// AS-IS default.</summary>
    public string serverRegion = "us";

    /// <summary>AS-IS <c>SaveServerRegion()</c> (:1040). No-op.</summary>
    public void SaveServerRegion()
    {
    }

    /// <summary>AS-IS <c>LoadServerRegion()</c> (:1045) — the original body is commented out. No-op.</summary>
    public void LoadServerRegion()
    {
    }

    /// <summary>AS-IS <c>public string LastConnectServerRegion = ""</c> (:1049).</summary>
    public string LastConnectServerRegion = "";

    /// <summary>AS-IS <c>SaveLanguage()</c> (:1056). No-op (the <c>Language</c> field it persists is omitted —
    /// see file header).</summary>
    public void SaveLanguage()
    {
    }

    /// <summary>AS-IS <c>LoadLanguage()</c> (:1061). No-op.</summary>
    public void LoadLanguage()
    {
    }

    /// <summary>AS-IS <c>public SoundObject PlaySE(AudioClip clip)</c> (:1068) — Instantiates the SE prefab and
    /// plays the clip, returning the new object. Returns null headless (rule 2).</summary>
    public object? PlaySE(object? clip) => null;

    /// <summary>AS-IS <c>public Coroutine LoadingTextCoroutine</c> (:1144).</summary>
    public object? LoadingTextCoroutine;

    /// <summary>AS-IS <c>EndBattle()</c> (:1148) — guards against re-entry and starts
    /// <see cref="EndBattleCoroutine"/>. No-op (scene teardown / Photon disconnect).</summary>
    public void EndBattle()
    {
    }

    /// <summary>AS-IS <c>public IEnumerator EndBattleCoroutine()</c> (:1156) — leaves the room and returns to
    /// the title scene. No-op.</summary>
    public Task EndBattleCoroutine() => Task.CompletedTask;

    /// <summary>REAL (rule 3). AS-IS <c>public bool isAI { get; set; } = false</c> (:1323) — whether this match
    /// is against the built-in AI; read by the rule layer AS-IS reads it from (GManager.cs:258/511,
    /// TurnStateMachine.cs:3319). Default false, as in the original.</summary>
    public bool isAI { get; set; } = false;

    /// <summary>AS-IS <c>public bool DoneSetRandom { get; set; } = false</c> (:1326) — the peer-seed handshake
    /// flag. Plain state; nothing headless sets it (no Photon).</summary>
    public bool DoneSetRandom { get; set; } = false;

    /// <summary>AS-IS <c>public bool CanSetRandom { get; set; } = false</c> (:1327).</summary>
    public bool CanSetRandom { get; set; } = false;

    /// <summary>AS-IS <c>[PunRPC] public void SetRandom(long)</c> (:1329) — the Photon RPC that seeds
    /// GameRandom from the master client. No-op: the substrate owns its RNG (EngineContext.RandomSource).
    /// </summary>
    public void SetRandom(long random)
    {
    }
}
