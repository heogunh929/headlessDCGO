# Substrate root causes behind `repair_substrate.tsv`

Input: `/home/hg/git/headlessDCGO/docs/audit/repair_substrate.tsv` — **291 rows**, each one place where a
mirror file could not keep the AS-IS statement.

Every row is assigned to **exactly one** root. Rows are cited by their TSV data-row number (`r001` = the
first data row after the header) followed by that row's `port` value, so the mapping is reversible.
Where a row named more than one difference it was assigned to its **dominant** root (the substrate member
whose absence actually forced the rewrite); the secondary facet is covered by the sibling root's own rows.

**Totals.** 15 substrate roots accounting for **282 rows**, plus **9 MISCLASSIFIED**
rows and **0 UNGROUPED** rows. 282 + 9 + 0 = **291**. Confirmed.

| # | Root | Rows |
|---|------|------|
| 1 | **S1** — No presentation layer in the substrate (Effects / PlayLog / UI objects / frame clock) | 73 |
| 2 | **S2** — `GManager.instance` static singleton has no substrate counterpart; every service root must be re-derived from an `EngineContext` | 45 |
| 3 | **S8** — Mirror `Permanent`/`CardSource`/`Player` cannot hold per-instance mutable state — AS-IS live fields become `CardInstanceRepository` metadata or side stores | 38 |
| 4 | **S6** — Mirror `CardSource`/`Permanent` are transient id-keyed *views*, not live objects — reference identity, cached properties and live re-reads are lost | 25 |
| 5 | **S5** — `ICardEffect` cannot cross a substrate zone move — effect causality is re-expressed as a `HeadlessEntityId` cause id plus a batch id | 24 |
| 6 | **S7** — Players are `HeadlessPlayerId` values, not live `Player` objects — every `card.Owner.X` becomes `new Player(context, id).X` | 17 |
| 7 | **S9** — No field-slot / `FieldCardFrame` model — the fixed frame array is replaced by a compacted permanent list and zone membership | 15 |
| 8 | **S11** — Unity / Photon library types are absent — `Photon Hashtable`, `Mathf`, and the AS-IS `List<T>.Clone()` extension | 10 |
| 9 | **S3** — `Player`'s `List<CardSource>` zone fields are absent — zone reads go through `IZoneStateReader.GetCards` | 9 |
| 10 | **S4** — `CardObjectController`'s zone-move coroutines are absent — all moves route through the async `IZoneMover` | 8 |
| 11 | **S10** — `CardSource.IsFlipped` / `SetFace()` / `SetReverse()` are absent — security face-up state is a substrate metadata stamp | 8 |
| 12 | **S12** — Coroutines became `async Task` — `ref` parameters are illegal and the synchronous `SelectCardEffect.Activate()` choice becomes a parked async request | 6 |
| 13 | **S13** — No location-change timestamp facility | 2 |
| 14 | **S14** — Memory is a single signed gauge, not per-player memory with a sign convention | 1 |
| 15 | **S15** — `CEntity_Base` (the Unity `ScriptableObject` card-data asset) has no substrate mirror | 1 |
| — | *MISCLASSIFIED (not substrate-forced)* | 9 |
| — | *UNGROUPED* | 0 |
| | **TOTAL** | **291** |

All paths below are absolute. TSV `port` values are quoted verbatim from the input, including the rows the
TSV itself records as `ABSENT` (the AS-IS statement has no port counterpart at all).

---

## 1. S1 — No presentation layer in the substrate (Effects / PlayLog / UI objects / frame clock)

**Rows accounted for: 73.**

**Substrate member or decision at fault.** `EngineContext` — the substrate's complete service surface — is
constructed from twelve services (`/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/Bridge/EngineContext.cs:14-29`:
`IChoiceProvider, IRandomSource, ICardRepository, ICardInstanceRepository, IZoneMover, IRuleQueryService,
IHeadlessTurnController, IHeadlessChoiceController, IHeadlessAttackController, IHeadlessMemoryController,
ILogSink, EngineTaskRunner`). **None of them is a display, animation, particle, sound, or play-log service**,
and there is no per-frame clock. The only output seam is `ILogSink`
(`/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/Services/ILogSink.cs:3-9`), a three-method text sink (`Info`/`Warn`/`Error`) with no
notion of a game log the AS-IS `PlayLog` UI consumes. `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/` contains no UI/animation
directory at all (129 .cs files: Bridge, Choices, Coroutines, DataLoading, Diagnostics, Runtime, Services,
State). `GManagerBridge` (`/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/Bridge/GManagerBridge.cs:6-23`) — the substrate stand-in for the
AS-IS `GManager` MonoBehaviour — exposes only `Turn`, `Attack`, `State`, `Log`, `CurrentMatch`; the AS-IS
`GManager` UI component fields have no counterpart.

**What AS-IS offered.**
- `Effects` (`/home/hg/git/headlessDCGO/DCGO/Assets/Scripts/Script/Effects.cs:11`, a `MonoBehaviour`) — the whole coroutine VFX vocabulary:
  `BounceEffect` (:734), `DeckBounceEffect` (:813), `DestroyPermanentEffect` (:1688), `ShowCardEffect`,
  `MoveToExecuteCardEffect`, `ShrinkUpUseHandCard`, `CreateDebuffEffect`, `CreateRecoveryEffect`,
  `BattleEffect`, `RemoveDigivolveRootEffect` (:2162), `FailedPlayCardEffect`, …
- `PlayLog.OnAddLog` (`/home/hg/git/headlessDCGO/DCGO/Assets/Scripts/Script/PlayLog.cs:17`, `public static Action<string>`) — the play-log fan-out.
- `Permanent`'s 11 `Show*/Hide*Effect` methods (`/home/hg/git/headlessDCGO/DCGO/Assets/Scripts/Script/Permanent.cs:3995` `ShowUnsuspendEffect`,
  `:4012` `ShowDeckBounceEffect`, `:4084` `ShowDeleteEffect`, `:4120` `ShowWillRemoveFieldEffect`, …) and
  `Permanent.ShowingPermanentCard` (`/home/hg/git/headlessDCGO/DCGO/Assets/Scripts/Script/Permanent.cs:1644`, a `FieldPermanentCard`).
- `Player.brainStormObject` (`/home/hg/git/headlessDCGO/DCGO/Assets/Scripts/Script/Player.cs:231`, a `BrainStormObject`), `SecurityObject`,
  `MemoryObject.OffMemoryPredictionLine`, `SelectCardPanel` display, `PlaySE`, and Unity's
  `WaitForSeconds` frame clock.

The port's `Effects.cs` is a 7-line stub (`/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/Effects.cs`); `PlayLog.cs`,
`BrainStormObject.cs`, `SecurityObject.cs`, `MemoryObject.cs` do not exist in the port at all.

**What the substrate would have to provide.** A no-op-capable presentation seam registered on
`EngineContext` — an `IPresentationSink` (or the AS-IS `Effects`/`PlayLog`/`BrainStormObject`/
`SecurityObject`/`FieldPermanentCard` surfaces mirrored as awaitable no-ops) plus an awaitable
`WaitForSeconds` shim on `EngineTaskRunner`. With that, every stripped `Effects.*` / `PlayLog.OnAddLog` /
`Show*/Hide*Effect` / `brainStormObject.*` statement could be restored verbatim and simply resolve to
nothing in headless runs.

<details><summary><b>73 rows</b> (TSV row → port <code>file:line</code>)</summary>

- `r006` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:141` — `DeleteHandCardEffectCoroutine(cardSource)` call removed (UI coroutine, no headless shim)
- `r015` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:3145` — jogress-target loop drops `permanent.ShowingPermanentCard.SetPermanentIndexText(targetPermanents)` display loop (:378-381)
- `r016` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:3205` — entire `if (card.IsPermanent && !isEvolution && PermanentOfThisCard()!=null && (Root==Digivolution||Root==Linked)) RemoveDigivolveRootEffect(...)` block removed (helper is pure DOTween/UI, no state change, confirmed Effects.cs:2162-2263)
- `r017` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:3277` — SelectCost drops the `MoveToExecuteCardEffect` bool/ShowingHandCard probe and the AI (`!isYou && IsAI`) and auto-min (`isYou && autoMinDigivolutionCost`) branches that reset `costSelected=false` (:520-532); costSelected only gates the UI-stripped effect region so no game-state effect
- `r021` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:3400` — `#region effect` bodies removed: the `noHandCard` ShowingHandCard probe + MoveToExecuteCardEffect (:652-671) and ShrinkUpUseHandCard (:677) are stripped, leaving empty if/else skeletons
- `r022` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:3418` — entire `#region show expected cost` (:687-704, GetPayingCostWithBaseCost probe + ShowMemoryPredictionLine) removed
- `r023` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:3437` — `if (IsShowEffect()) targetPermanent.ShowWillEvolutionEffect()` loop (:716-725) and the `HideWillEvolutionEffect()` loop (:729-735) removed
- `r027` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:3640` — failed-play UI removed: PlayLog.OnAddLog (:902), FailedPlayCardEffect (:909), OffMemoryPredictionLine (:918), brainStorm CloseBrainstrorm loop (:920-926), FieldPermanentObjects OffPermanentIndexText loop (:928-937)
- `r028` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:3717` — fire-and-forget `OffMemoryPredictionLine()` call removed (:978 and :918)
- `r029` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:3784` — `IEnumerator OffMemoryPredictionLine()` method (WaitForSeconds + memoryObject.OffMemoryPredictionLine) removed entirely
- `r032` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:3883` — `card.Owner.brainStormObject.CloseBrainstrorm(card)` call removed (UI brainstorm overlay absent from substrate)
- `r036` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:3883` — second `card.Owner.brainStormObject.CloseBrainstrorm(card)` call removed
- `r043` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:4010` — DoneStartGame block (HasETB probe + CreateFieldPermanentCardEffect / DigivolveFieldPermanentCardEffect) removed; verified UI/animation-only (DOTween/Instantiate/SE, no rule mutation)
- `r055` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:4177` — "move permanents (hybrid)" block removed; verified UI (transform.localPosition comparisons, MovePermanent canvas repositioning)
- `r056` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:4337` — `_showEffect` Effects.* coroutines (DeleteHandCardEffect/ShowUseHandCardEffect_PlayCard/MoveToExecuteCardEffect) removed
- `r057` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:4338` — PlayLog.OnAddLog Play-Option log removed (PlayLog UI absent)
- `r060` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:4348` — card.Owner.brainStormObject.BrainStormCoroutine removed (brainStorm UI absent)
- `r063` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:4511` — card.Owner.brainStormObject.CloseBrainstrorm removed (brainStorm UI absent)
- `r067` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:277` — PlayLog draw-count log removed
- `r072` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:360` — ShowCardEffect (gated by !_notShowCards) removed, leaving SetNotShowCards/_notShowCards inert
- `r073` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:364` — log string build + PlaySE/WaitForSeconds/PlayLog removed
- `r076` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:463` — `useEffect: (i == 0)` argument dropped (it gated only the CreateRecoveryEffect VFX)
- `r077` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:466` — `count` accumulator + PlayLog security-add log removed
- `r080` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:515` — CreateRecoveryEffect VFX removed
- `r083` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:670` — Digiburst add-log build + PlayLog removed
- `r085` `ABSENT` — foreach loop calling permanent.ShowDeckBounceEffect() removed (Permanent.ShowDeckBounceEffect UI not present in headless)
- `r086` `ABSENT` — foreach loop calling permanent.HideDeckBounceEffect() removed (Permanent.HideDeckBounceEffect UI not present in headless)
- `r087` `ABSENT` — returnedCards Map and the !_notShowCards ShowCardEffect(returnedCards,"Deck bottom cards",true,true) show-cards block removed (Effects.ShowCardEffect UI not present)
- `r088` `ABSENT` — await Effects.DeckBounceEffect(permanent) call removed (Effects.DeckBounceEffect UI not present)
- `r090` `ABSENT` — foreach loop calling permanent.ShowDeckBounceEffect() removed (Permanent.ShowDeckBounceEffect UI not present in headless)
- `r091` `ABSENT` — foreach loop calling permanent.HideDeckBounceEffect() removed (Permanent.HideDeckBounceEffect UI not present in headless)
- `r092` `ABSENT` — returnedCards Map and the !_notShowCards ShowCardEffect(returnedCards,"Deck bottom cards",true,true) show-cards block removed (Effects.ShowCardEffect UI not present)
- `r093` `ABSENT` — await Effects.DeckBounceEffect(permanent) call removed (Effects.DeckBounceEffect UI not present)
- `r095` `ABSENT` — foreach loop calling permanent.ShowHandBounceEffect() removed (Permanent.ShowHandBounceEffect UI not present in headless)
- `r096` `ABSENT` — await autoProcessing_CutIn.ShrinkSecurityDigimonDisplay() call removed (ShrinkSecurityDigimonDisplay UI not present in headless)
- `r097` `ABSENT` — foreach loop calling permanent.HideHandBounceEffect() removed (Permanent.HideHandBounceEffect UI not present in headless)
- `r098` `ABSENT` — add-log block building "Return to hand" text and calling PlayLog.OnAddLog?.Invoke(log) removed (PlayLog.OnAddLog stripped project-wide, not functional in headless)
- `r099` `ABSENT` — returnedCards Map and the !_notShowCards ShowCardEffect(returnedCards,"Cards returned to hand",true,true) show-cards block removed (Effects.ShowCardEffect UI not present)
- `r100` `ABSENT` — await Effects.BounceEffect(permanent, !permanent.IsReturnedToHandByBurstDigivolution) call removed (Effects.BounceEffect UI not present)
- `r105` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:2368` — showEffect() local fn and its `if(showEffect())` display block (WillRemoveFieldObject SetActive, MoveToExecuteCardEffect, BrainStormCoroutine, shrink-security) removed; TriggeredSkillProcess kept unconditional
- `r108` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:2426` — add-log (PlayLog.OnAddLog) and hide-icon (ShowCardEffect "Digivolution Cards") blocks removed
- `r113` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:2565` — showEffect() local fn and its `if(showEffect())` display block (WillRemoveFieldObject, MoveToExecuteCardEffect, BrainStormCoroutine, shrink-security) removed; TriggeredSkillProcess kept unconditional
- `r116` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:2627` — add-log (PlayLog.OnAddLog) and hide-icon (ShowCardEffect "Link Cards") blocks removed
- `r123` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:5907` — ShowWillRemoveFieldEffect / ShrinkSecurityDigimonDisplay / HideWillRemoveFieldEffect removed inside the HasAwaitingActivateEffects branch (TriggeredSkillProcess kept)
- `r124` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:5923` — add-log (PlayLog.OnAddLog) block removed
- `r125` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:5927` — show-cards (ShowCardEffect "Security Top/Bottom Card") block removed
- `r127` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:5962` — CreateRecoveryEffect(topCard.Owner) removed
- `r128` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:6085` — cut-in `permanent.ShowDeleteEffect()` loop, `ShrinkSecurityDigimonDisplay()`, and `HideDeleteEffect()` loop removed (no Effects/Permanent UI service)
- `r129` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:6152` — "add log" region building Delete log and calling `PlayLog.OnAddLog?.Invoke` removed (no PlayLog service)
- `r130` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:6152` — "show cards" region computing destroyedCards and calling `Effects.ShowCardEffect(...)` removed (no Effects UI service)
- `r131` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:6180` — `Effects.DestroyPermanentEffect(permanent)` call removed (no Effects UI service)
- `r132` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:4730` — `PlayLog.OnAddLog?.Invoke(Security Check ...)` removed (no PlayLog service)
- `r134` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:4743` — effect block `securityObject.securityBreakGlass.ShowBlueMatarial()`, `Effects.BreakSecurityEffect`, `WaitForSeconds(0.1f)`, `Effects.EnterSecurityCardEffect` removed (no securityObject/Effects UI service)
- `r136` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:4787` — inside `if(!brokenSecurityCard.IsDigimon)` the `WaitForSeconds(0.3f)` + `Effects.ShrinkUpUseHandCard(...)` removed (no Effects UI service)
- `r137` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:4792` — `Effects.MoveToExecuteCardEffect(brokenSecurityCard)` + `player.brainStormObject.BrainStormCoroutine(...)` removed (no Effects/brainStormObject UI service)
- `r139` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:4949` — `WaitForSeconds(0.3f)` before Battle removed (no frame clock)
- `r140` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:4960` — `brokenSecurityCard.Owner.brainStormObject.CloseBrainstrorm(...)` removed (no brainStormObject UI service)
- `r142` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:4966` — `if(ShowUseHandCard.gameObject.activeSelf) ShrinkUpUseHandCard(...)` removed (no Effects UI service)
- `r143` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:4969` — `Effects.ShowUseHandCard.OffDP()` removed (no Effects UI service)
- `r152` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:811` — effect block ShowBlueMatarial/BreakSecurityEffect/WaitForSeconds/EnterSecurityCardEffect/WaitForSeconds/DestroySecurityEffect removed (no securityObject/Effects UI service)
- `r154` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:825` — `Effects.ShowCardEffect(discardedCards, "Discarded Cards", true, true)` removed (no Effects UI service)
- `r156` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:847` — "add log" region (modeString switch + `PlayLog.OnAddLog?.Invoke`) removed (no PlayLog service)
- `r158` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:5110` — PlayLog log-string build and `PlayLog.OnAddLog?.Invoke(log)` (4518-4530) removed as UI logging
- `r160` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:5137` — `ShowEffect()` local function plus BrainStorm/MoveToExecuteCardEffect display block (4561-4595) removed as UI
- `r161` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:5246` — `Effects.BattleEffect(WinnerPermanents,LoserPermanents,LoserCard)` coroutine call removed as UI
- `r162` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:5263` — move-up region `if(ShowEffect()) ShrinkSecurityDigimonDisplay()` (4720-4727) removed as UI
- `r167` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:942` — first-iteration `if(count==0) Effects.CreateDebuffEffect(_permanent)` removed as UI
- `r169` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:960` — `_permanent.ShowingPermanentCard.ShowPermanentData(true)` removed as UI
- `r170` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:960` — `Effects.RemoveDigivolveRootEffect(cardSource,_permanent)` removed; body (Effects.cs:2162) is DOTween/transform animation only, UI
- `r171` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:979` — De-Digivolve add-log region (4919-4940) removed as UI
- `r221` `src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:671` — drops the `card1.Owner.brainStormObject.CloseBrainstrorm(card1)` call before adding the card to hand
- `r255` `ABSENT` — ShowingPermanentCard (Unity UI type FieldPermanentCard) has no port counterpart — grep of whole port file finds neither ShowingPermanentCard nor FieldPermanentCard [no headless UI layer]
- `r290` `ABSENT` — The 10 Unity Show*/Hide*Effect methods (ShowUnsuspendEffect/ShowDeckBounceEffect/HideDeckBounceEffect/ShowHandBounceEffect/HideHandBounceEffect/ShowDeleteEffect/HideDeleteEffect/ShowWillRemoveFieldEffect/HideWillRemoveFieldEffect/ShowWillEvolutionEffect/HideWillEvolutionEffect) are purely visual GameObject/particle SetActive calls (willBeRemoveField only read, never mutated); no headless UI substrate, all omitted, referenced only in stripped-UI comments

</details>

---

## 2. S2 — `GManager.instance` static singleton has no substrate counterpart; every service root must be re-derived from an `EngineContext`

**Rows accounted for: 45.**

**Substrate member or decision at fault.** The substrate replaced the process-wide Unity singleton with a
per-match, explicitly-passed object: `EngineContext`
(`/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/Bridge/EngineContext.cs:10`, `public sealed class EngineContext`) and its bridge
`GManagerBridge(EngineContext context)` (`/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/Bridge/GManagerBridge.cs:6-13`). There is **no
static instance accessor** on either type. The only ambient escape hatch is
`AmbientMatchContext` (`/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/Bridge/AmbientMatchContext.cs:16`), an `AsyncLocal` scope that must
be explicitly `Enter`ed (`:38`) and `Require()`d (`:29`) — i.e. a caller-supplied ambient, not a global.
The turn roster likewise comes from `IHeadlessTurnController` (`/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/Bridge/EngineContext.cs:74`),
whose turn player is nullable, so roster reads acquire null-fallbacks the AS-IS never needed.

**What AS-IS offered.** `GManager.instance` (`/home/hg/git/headlessDCGO/DCGO/Assets/Scripts/Script/GManager.cs:211`, `public static GManager instance`)
reachable from any statement, carrying `turnStateMachine` (`:209`), `autoProcessing` (`:84`),
`autoProcessing_CutIn` (`:112`), `attackProcess` (`:99`), `selectBurstDigivolutionEffect` (`:106`),
`selectAppFusionEffect` (`:109`) as plain fields; and through it
`turnStateMachine.gameContext.Players` / `.Players_ForTurnPlayer`
(`/home/hg/git/headlessDCGO/DCGO/Assets/Scripts/Script/GameContext.cs:41` and `:54`) — the roster source of essentially every continuous-effect scan.

**What the substrate would have to provide.** Either a match-scoped static ambient (`GManager.instance`
resolving to `AmbientMatchContext.Require()`'s bridge, entered once per match at the pump) exposing the
AS-IS field names `turnStateMachine.gameContext`, `autoProcessing`, `autoProcessing_CutIn`,
`attackProcess`, `selectBurstDigivolutionEffect`, `selectAppFusionEffect` — or a non-nullable turn-player
guarantee plus those same names on `GManagerBridge`. Then every
`new GameContext(_context).Players_ForTurnPlayer` reverts to
`GManager.instance.turnStateMachine.gameContext.Players_ForTurnPlayer`, every `AutoProcessing.For(context)`
to `GManager.instance.autoProcessing`, and the `EngineContext context = …[0].TopCard.Context` service-root
derivations (with their added empty-collection early-returns) disappear.

<details><summary><b>45 rows</b> (TSV row → port <code>file:line</code>)</summary>

- `r002` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:78` — added early-return `if (discardHands.Count == 0) return;` not in original, guarding the discardHands[0].CardSource.Context batch access
- `r024` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:3488` — `GManager.instance.selectBurstDigivolutionEffect` field access became `GManager.instance.GetComponent<SelectBurstDigivolutionEffect>()` (cached in local)
- `r025` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:3515` — `GManager.instance.selectAppFusionEffect` field access became `GManager.instance.GetComponent<SelectAppFusionEffect>()`
- `r050` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:4129` — `new DrawClass(card.Owner, 1, null)` → `new DrawClass(card.Context, card.Owner, 1, (ICardEffect?)null)` (ctor adds context param)
- `r051` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:4142` — `GManager.instance.selectBurstDigivolutionEffect.AddTrashTopCardAtTurnEnd(permanent)` → `GManager.instance.GetComponent<SelectBurstDigivolutionEffect>().AddTrashTopCardAtTurnEnd(permanent)` (singleton → match-scoped component)
- `r101` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:2320` — added `EngineContext context = removeFieldPermanents[0].TopCard.Context` as service root since GManager singleton is unavailable
- `r103` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:2333` — `GManager.instance.turnStateMachine.gameContext.Players_ForTurnPlayer` → `new GameContext(context).Players_ForTurnPlayer`
- `r104` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:2365` — `GManager.instance.autoProcessing_CutIn.PutStackedSkill` → `AutoProcessing.ForCutIn(context).PutStackedSkill`
- `r109` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:2518` — added `EngineContext context = removeFieldPermanents[0].TopCard.Context` as service root
- `r111` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:2530` — `GManager.instance.turnStateMachine.gameContext.Players_ForTurnPlayer` → `new GameContext(context).Players_ForTurnPlayer`
- `r112` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:2562` — `GManager.instance.autoProcessing_CutIn.PutStackedSkill` → `AutoProcessing.ForCutIn(context).PutStackedSkill`
- `r114` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:2584` — OnLeaveFieldAnyone `GManager.instance.autoProcessing.StackSkillInfos` → `AutoProcessing.For(context).StackSkillInfos`
- `r117` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:2672` — added `EngineContext context = _linkCard.Context` as service root
- `r118` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:2687` — WhenWouldLink `GManager.instance.autoProcessing_CutIn.StackSkillInfos` → `AutoProcessing.ForCutIn(context).StackSkillInfos`
- `r174` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:1041` — added EngineContext extraction from first non-null permanent (with early return if all null)
- `r184` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:1210` — cut-in uses `AutoProcessing.ForCutIn(context)` instead of shared `GManager.instance.autoProcessing_CutIn`; `ShrinkSecurityDigimonDisplay()` call dropped
- `r207` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:1988` — cut-in push/drain uses `AutoProcessing.ForCutIn(context)` instead of shared `autoProcessing_CutIn`
- `r213` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:2145` — added `if (_cardSources.Count == 0) return;` early-out (needed for `_cardSources[0].Context`)
- `r215` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:2159` — OrderBy turn-player key adds `turnPlayer is {} tp && ...` null-fallback (all-equal ordering) absent from original which always has a TurnPlayer
- `r228` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/Permanent.cs:209` — GetDP player enumeration GManager.instance.turnStateMachine.gameContext.Players_ForTurnPlayer becomes new GameContext(_context).Players_ForTurnPlayer
- `r230` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/Permanent.cs:388` — DP getter opens an AmbientMatchContext.Scope for the fold with no original counterpart [AmbientMatchContext.Enter]
- `r231` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/Permanent.cs:400` — DP getter player enumeration GManager.instance.turnStateMachine.gameContext.Players_ForTurnPlayer becomes new GameContext(_context).Players_ForTurnPlayer
- `r234` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/Permanent.cs:751` — CannotReturnToHand player enumeration GManager.instance.turnStateMachine.gameContext.Players becomes new GameContext(_context).Players
- `r235` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/Permanent.cs:798` — CannotReturnToLibrary player enumeration GManager.instance.turnStateMachine.gameContext.Players becomes new GameContext(_context).Players
- `r236` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/Permanent.cs:876` — HasBlocker collision short-circuit GManager.instance.attackProcess becomes AttackProcess.For(_context) (ActiveAttack and AttackingPermanent)
- `r237` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/Permanent.cs:894` — HasBlocker player enumeration GManager.instance.turnStateMachine.gameContext.Players_ForTurnPlayer becomes new GameContext(_context).Players_ForTurnPlayer
- `r238` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/Permanent.cs:967` — HasJamming player enumeration GManager.instance.turnStateMachine.gameContext.Players_ForTurnPlayer becomes new GameContext(_context).Players_ForTurnPlayer
- `r239` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/Permanent.cs:1024` — HasIceclad player enumeration GManager.instance.turnStateMachine.gameContext.Players_ForTurnPlayer becomes new GameContext(_context).Players_ForTurnPlayer
- `r241` `Permanent.cs:2634` — ImmuneFromDPMinus body identical except the roster source GManager.instance.turnStateMachine.gameContext.Players is swapped for new GameContext(_context).Players
- `r242` `Permanent.cs:751` — CannotReturnToHand identical incl. nested player-loop quirk except roster source swap [new GameContext(_context).Players]
- `r243` `Permanent.cs:798` — CannotReturnToLibrary identical incl. nested player-loop quirk except roster source swap [new GameContext(_context).Players]
- `r244` `Permanent.cs:3170` — ImmuneFromDeDigivolve body identical except roster source swap [new GameContext(_context).Players]
- `r245` `Permanent.cs:3204` — ImmuneFromStackTrashing body identical except roster source swap [new GameContext(_context).Players]
- `r246` `Permanent.cs:1105` — HasReboot (representative of all Has* scan members) sources players via new GameContext(_context).Players_ForTurnPlayer instead of GManager.instance.turnStateMachine.gameContext.Players_ForTurnPlayer; tiers/conditions otherwise identical.
- `r247` `Permanent.cs:1200` — HasRush body identical except turn-order roster source swap [new GameContext(_context).Players_ForTurnPlayer]
- `r248` `Permanent.cs:1488` — HasAlliance body identical except turn-order roster source swap [new GameContext(_context).Players_ForTurnPlayer]
- `r249` `Permanent.cs:1552` — HasCollision body identical except turn-order roster source swap [new GameContext(_context).Players_ForTurnPlayer]
- `r259` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/Permanent.cs:3137` — CanSelectBySkill/InvertSecutiryValue/SecurityAttackChanges/Strike_AllowMinus swap AS-IS `GManager.instance.turnStateMachine.gameContext.Players[_ForTurnPlayer]` for `new GameContext(_context).Players[_ForTurnPlayer]`; scan scope/interface/gate order verbatim [new GameContext(_context) replaces GManager.instance.turnStateMachine.gameContext]
- `r271` `Permanent.cs:3742` — CanBeDestroyedByBattle player-order source translated to new GameContext(_context).Players_ForTurnPlayer; all three tiers (field permanents, faceup security, player effects) and negation logic identical [GameContext.Players_ForTurnPlayer]
- `r272` `Permanent.cs:3828` — CanBeDestroyedBySkill player-order source translated to new GameContext(_context).Players_ForTurnPlayer; CanNotBeAffected/CanBeDestroyed guards and both scan tiers identical [GameContext.Players_ForTurnPlayer]
- `r273` `Permanent.cs:3879` — CanBeRemoved player-order source translated to new GameContext(_context).Players_ForTurnPlayer; both ICanNotBeRemovedEffect tiers identical [GameContext.Players_ForTurnPlayer]
- `r274` `Permanent.cs:3932` — CanSubstituteForDigiXrosCondition player-order source translated to new GameContext(_context).Players_ForTurnPlayer; ICanSelectDigiXrosEffect scan identical [GameContext.Players_ForTurnPlayer]
- `r275` `Permanent.cs:3983` — CanSubstituteForAssemblyCondition player-order source translated to new GameContext(_context).Players_ForTurnPlayer; ICanSelectAssemblyEffect scan identical [GameContext.Players_ForTurnPlayer]
- `r276` `Permanent.cs:3085` — CanBeDestroyed body is logic-identical; only the turn-player enumeration source changed (GManager.instance.turnStateMachine.gameContext.Players_ForTurnPlayer -> new GameContext(_context).Players_ForTurnPlayer).
- `r277` `Permanent.cs:624` — IsDigimon body is logic-identical (TopCard flip/print check, then ITreatAsDigimonEffect over TopCard, then field permanents via EffectList_Added, then players); only the Players_ForTurnPlayer source changed [GameContext(_context).Players_ForTurnPlayer]

</details>

---

## 3. S8 — Mirror `Permanent`/`CardSource`/`Player` cannot hold per-instance mutable state — AS-IS live fields become `CardInstanceRepository` metadata or side stores

**Rows accounted for: 38.**

**Substrate member or decision at fault.** Card state lives in an immutable record in a repository, not on
the object: `CardInstanceRecord` is a `sealed record` whose only extensibility point is
`IReadOnlyDictionary<string, object?> Metadata` (`/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/Services/CardInstanceRecord.cs:5` and
`:46`), mutated only by whole-record replacement through
`ICardInstanceRepository.Upsert(CardInstanceRecord)` (`/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/Services/ICardInstanceRepository.cs:5`,
read via `TryGetInstance` `:7`). Mirror `Permanent`/`CardSource` are constructed per access from
`(EngineContext, HeadlessEntityId, HeadlessPlayerId)`, so any field they declared would be discarded on the
next construction. The same decision drives the ad-hoc side stores the port had to invent —
`PermanentBookkeepingStore` (`/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/State/PermanentBookkeepingStore.cs:82`),
`PlayerTurnCounterController` (`/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/Runtime/PlayerTurnCounterController.cs:13`, e.g.
`DigivolveCountKey` `:16`, `Increment` `:25`), and the turn-scoped move stamp
`ZoneMoveMetadataKeys.EnteredThisTurnKey` (`/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/Services/ZoneMoveMetadataKeys.cs:16`).

**What AS-IS offered.** Plain mutable fields/auto-properties on the live objects:
`Permanent.IsSuspended` (`/home/hg/git/headlessDCGO/DCGO/Assets/Scripts/Script/Permanent.cs:1956`), `Permanent.LinkedCards`
(`:1041`), `Permanent.LinkedDP` (`:670`), `Permanent.LinkedMax` (`:896`), `Permanent.Boosts` (`:672`),
`Permanent.willBeRemoveField` (`:3434`), `Permanent.EnterFieldTurnCount` (`:1640`),
`Permanent.IsPlaceToTrashDueToNotHavingDP` (`:3694`), `Permanent.IsPlayedOptionPermanent` (`:3946`),
`Permanent.oldIsTapped_playCard` (`:45`), `Permanent.battle` (`:3182`), the nine duration buckets
`UntilOwnerTurnEndEffects … PermanentEffects` (`:1575-1607`), `Permanent.cardSources` (`:880`) as the single
ordered under-card list that `AddLinkCard` `Insert(1,…)`ed into; and on the player side
`Player.DigivolveCount_ThisTurn` (`/home/hg/git/headlessDCGO/DCGO/Assets/Scripts/Script/Player.cs:1176`).

**What the substrate would have to provide.** Stable, identity-keyed *mutable* game objects — a
`Permanent`/`CardSource` whose lifetime the substrate owns (e.g. a repository that hands back the *same*
instance for an id, rather than a record + metadata dictionary). Then `IsSuspended = true`,
`LinkedCards.Insert(0, …)`, `LinkedDP += …`, `willBeRemoveField = true`, `EnterFieldTurnCount = -1`,
`battle = this`, and the nine `Until*Effects` list fields all revert to plain field assignment, and
`LinkedCardIdsKey`/`LinkedDpKey`/`LinkedMaxKey`/`SourceIdsKey`/`IsSuspendedKey`/`PlaceToTrashDueToNoDpKey`/
`IsPlayedOptionPermanentKey`/`DpBoostsKey`/`willBeRemoveField` and the `WithLinked`/`ReadLinkMetadataInt`
writer-readers (which have **no AS-IS counterpart at all**) can be deleted outright.

<details><summary><b>38 rows</b> (TSV row → port <code>file:line</code>)</summary>

- `r049` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:4128` — `card.Owner.DigivolveCount_ThisTurn++` → `card.Context.PlayerTurnCounters.Increment(card.Owner, PlayerTurnCounterController.DigivolveCountKey)`
- `r157` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:5102` — four `AttackingPermanent.battle=this`/`DefendingPermanent.battle=this` writes (and the =null resets at AS-IS:4763-4771) omitted; mirror Permanent lacks the `battle` auto-property present at AS-IS Permanent.cs:3182
- `r165` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:900` — `_permanent.ImmuneFromDeDigivolve()` (pure continuous-effect scan at Permanent.cs:826) rehomed to static `ImmuneFromDeDigivolve(context,id)` which still calls the instance scan but ORs an extra check ahead of it
- `r175` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:1116` — ValidTarget immune check wraps `permanent.ImmuneFromDeDigivolve()` in a static helper that first ORs an extra `Permanent.CannotBeDeDigivolvedKey` metadata pre-check absent from original
- `r182` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:1289` — `cardSource.CanNotTrashFromDigivolutionCards(_cardEffect)` wrapped in static helper that first ORs an extra `CardEffectCommons.TrashProtectedKey` metadata pre-check absent from original
- `r183` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:1301` — `source.willBeRemoveSources = true` instance flag becomes metadata write via SetWillBeRemoveSources/ReadWillBeRemoveSources
- `r185` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:1256` — willBeRemoveSources clear moved from AFTER removal (:5232) to BEFORE the removal helper
- `r189` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:1460` — `willBeRemoveSources` flag becomes metadata via WillBeRemoveSourcesKey/Set/ReadWillBeRemoveSources
- `r202` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:1854` — `permanent.IsSuspended = true` property set becomes SetIsSuspended metadata write (Permanent.IsSuspendedKey)
- `r203` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:1857` — `permanent.DPWhenSuspended = permanent.DP` becomes SetDpWhenSuspended metadata write under new DpWhenSuspendedKey
- `r208` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:2005` — `permanent.IsSuspended = false` property set becomes SetIsSuspended metadata write
- `r214` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:2178` — `IsACE`/`OverflowMemory`/`IsFlipped` properties become metadata reads via IsAceKey/OverflowMemoryKey/IsFlippedKey (ReadBool/ReadInt)
- `r224` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/Permanent.cs:1719` — oldIsTapped_playCard is a plain bool auto-property in the original but is backed by an OldIsTappedPlayCardStore dictionary keyed by InstanceId in the port (identical get/set and default-false semantics).
- `r233` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/Permanent.cs:2510` — LinkedDP plain { get; set; } auto-property becomes CardInstanceRepository metadata-backed get/set on LinkedDpKey [CardInstanceRepository metadata / LinkedDpKey]
- `r240` `Permanent.cs:2534` — Boosts is a plain List<DPBoost> field in original but a computed read-only view over id->dp instance metadata in port [CardInstanceRepository metadata key DpBoostsKey "dpBoosts"]
- `r250` `Permanent.cs:1695` — IsSuspended AS-IS public mutable field becomes a metadata-backed property (IsSuspendedKey "isSuspended" via CardInstanceRepository.Metadata) [CardInstanceRecord.Metadata IsSuspendedKey]
- `r251` `Permanent.cs:2064` — AS-IS cardSources is a live list where AddLinkCard Insert(1,...) interleaves newest link directly under the top; port getter reconstructs [TopCard]+DigivolutionCards+LinkedCards so links always sort AFTER all digivolution sources [link cards live in a separate LinkedCardIds instance-metadata list, not the single ordered cardSources list]
- `r254` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/Permanent.cs:2226` — The 9 duration buckets (UntilOwnerTurnEnd/OwnerDrawPhase/EachTurnEnd/OpponentTurnEnd/EndBattle/EndAttack/OwnerTurnStart/NextUntap + PermanentEffects) are plain settable List fields AS-IS but delegating properties in port; get returns the live backing list (Add persists) and set replaces it, semantics match [PermanentEffectListStore/PermanentEffectLists, ConditionalWeakTable keyed by EngineContext+InstanceId, port 5866-5889]
- `r256` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/Permanent.cs:3288` — EnterFieldTurnCount is a plain `{get;set;}=-1` int AS-IS but a property over a boolean metadata flag in port; get returns current TurnNumber when entered-this-turn else -1, set stamps value==currentTurn, preserving all AS-IS `==TurnCount` reads [EnteredThisTurn / ZoneMoveMetadataKeys.EnteredThisTurnKey]
- `r258` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/Permanent.cs:2847` — Strike_AllowMinus seeds `int Strike = 1` (literal constant) AS-IS vs `int Strike = ReadSubstrateStrikeSeed()` in port (defaults to 1 when the strike metadata key is unset, production never writes it); rest of body verbatim [ReadSubstrateStrikeSeed / SecurityResolver.StrikeKey]
- `r263` `Permanent.cs:3599` — CanAttackTargetDigimon summoning-sickness test EnterFieldTurnCount==TurnCount replaced by EnteredThisTurn boolean substrate flag [MatchStateMutationSink.EnteredThisTurnKey / EnteredThisTurn]
- `r265` `Permanent.cs:3011` — LinkedCards AS-IS public mutable List<CardSource> field becomes a computed getter reading linked-card ids from CardInstanceRepository metadata (ReadLinkedCardIds) [CardInstanceRecord.Metadata linked-card ids]
- `r266` `Permanent.cs:3037` — LinkedMax AS-IS inline live IChangeLinkMaxEffect scan over Players_ForTurnPlayer replaced by substrate base ReadLinkedMax folded via NewModelContinuousScan.FoldLinkedMax [NewModelContinuousScan.FoldLinkedMax + ReadLinkedMax metadata base]
- `r267` `Permanent.cs:3058` — IsPlaceToTrashDueToNotHavingDP AS-IS auto-property {get;set;}=true becomes a metadata-backed get/set (PlaceToTrashDueToNoDpKey), default-true / false-opt-out semantics preserved [CardInstanceRecord.Metadata PlaceToTrashDueToNoDpKey]
- `r268` `Permanent.cs:3077` — IsPlayedOptionPermanent AS-IS auto-property {get;set;}=false becomes a getter-ONLY metadata read (IsPlayedOptionPermanentKey); the AS-IS setter is dropped, writes relocated to CardEffectCommons metadata stamping [CardEffectCommons.IsPlayedOptionPermanentKey metadata; setter relocated]
- `r269` `ABSENT` — Permanent.battle (IBattle) field absent in port; current-battle tracking / enemyPermanent lookup relocated to service CardEffectCommons.CurrentBattleOpponent(card) (IBattle type itself exists as a mirror in CardController.cs).
- `r270` `Permanent.cs:3288` — Plain settable int (=-1) replaced by a derived property over the enteredThisTurn boolean metadata flag; getter returns current TurnNumber when set else -1, so ==TurnCount reads reduce to the flag; turn-boundary expiry via TurnFlowPump.ExpireEnteredThisTurnFlags matches AS-IS auto-expiry, no off-by-one [ZoneMoveMetadataKeys.EnteredThisTurnKey + TurnFlowPump.ExpireEnteredThisTurnFlags]
- `r278` `Permanent.cs:1774` — willBeRemoveField is a plain auto-property bool field in AS-IS; the port backs it with CardInstanceRepository instance-metadata ("willBeRemoveField") get/set (the IsBeingRevealed store pattern) [CardInstanceRepository metadata willBeRemoveField]
- `r279` `Permanent.cs:4945` — AS-IS holds link cards in a live List<CardSource> LinkedCards field on the Permanent; the port has no such field and reads them via static ReadLinkedCardIds off the host top card's "linkedCardIds" instance metadata (newest-first, preserving Insert(0,...) order) [LinkedCardIdsKey host metadata replacing List<CardSource> LinkedCards]
- `r280` `Permanent.cs:4969` — AS-IS LinkedDP is a mutable int auto-property; the port reads it via static ReadLinkedDp off host "linkedDp" metadata [LinkedDpKey host metadata replacing LinkedDP field]
- `r281` `Permanent.cs:4974` — AS-IS LinkedMax is a full effect-folding property; the port ReadLinkedMax returns only the BASE (metadata override "linkedMax" else default 1), with the ChangeLinkedMax effect fold applied separately at the LinkedMax accessor (Permanent.cs:3042) [LinkedMaxKey base half; effect fold split out]
- `r282` `Permanent.cs:4982` — AS-IS RemoveLinkedCard mutates LinkedCards/LinkedDP inline and trashes via CardObjectController.AddTrashCard; the port RemoveLinkCardAsync is the STORAGE half only (list rewrite + LinkedDP subtract + IZoneMover trash), matching AS-IS :1308-1319; the removeCount>0 SelectCardEffect branch lives in the instance RemoveLinkedCard wrapper, and AS-IS's "//TODO event call" no-window behavior is preserved [metadata rewrite + IZoneMover replacing LinkedCards.Remove/AddTrashCard]
- `r283` `Permanent.cs:5023` — AS-IS AddLinkCard does LinkedCards.Insert(0,..)/LinkedDP += LinkDP/cardSources.Insert(1,..) inline; the port AttachLinkCard is the storage half (prepend id to "linkedCardIds", add LinkDP) as a static host-keyed metadata rewrite [metadata prepend replacing LinkedCards.Insert/LinkedDP +=]
- `r284` `Permanent.cs:5042` — WithLinked has no AS-IS counterpart by name; it is the metadata-writer that persists the linked-id list + linkedDp (removing both keys when empty) — the substrate persistence step for the AS-IS in-place LinkedCards/LinkedDP field mutation [metadata writer for LinkedCards/LinkedDP mutation]
- `r285` `Permanent.cs:5059` — ReadLinkMetadataInt has no AS-IS counterpart; it is a substrate int-coercion reader (int/long/string) over the metadata dictionary, needed because AS-IS int fields become boxed metadata values [metadata int coercion for LinkedDP/LinkedMax/LinkDP fields]
- `r287` `Permanent.cs:4930` — The link-cluster key constants (LinkedCardIdsKey/LinkedDpKey/LinkedMaxKey "linkedMax", LinkDpKey "linkDp", DefaultLinkedMax=1) are substrate metadata keys standing in for the AS-IS live fields LinkedCards/LinkedDP/LinkedMax and CardSource.LinkDP; DefaultLinkedMax=1 mirrors the AS-IS LinkedMax default [metadata keys for LinkedCards/LinkedDP/LinkedMax/LinkDP fields]
- `r288` `Permanent.cs:4869` — The non-link metadata-key constants (SourceIdsKey, IsSuspendedKey, CanSuspendKey, CannotBeDeDigivolvedKey, DeletedBy*/PendingDeletion, PlaceToTrashDueToNoDpKey, IsPlayedOptionPermanentKey) are substrate metadata keys backing AS-IS bool fields/properties (IsSuspended, CanSuspend, ImmuneFromDeDigivolve static half, deletion markers, IsPlaceToTrashDueToNotHavingDP, IsPlayedOptionPermanent) that AS-IS held as live object state [instance-metadata keys for AS-IS bool fields]
- `r291` `src/HeadlessDCGO.Engine/Assets/Scripts/Script/Permanent.cs:5866` — PermanentEffectLists/PermanentEffectListStore is the coherent SUBSTRATE backing for the 9 Until*Effects duration buckets (AS-IS settable list fields Permanent.cs:1575-1608); ConditionalWeakTable(context)->ConcurrentDictionary(instanceId) returns a stable per-(context,instanceId) instance so populated lists persist across calls (no fresh-empty-list loss)

</details>

---

## 4. S6 — Mirror `CardSource`/`Permanent` are transient id-keyed *views*, not live objects — reference identity, cached properties and live re-reads are lost

**Rows accounted for: 25.**

**Substrate member or decision at fault.** Identity in the substrate is a value-struct id, not an object
reference: `HeadlessEntityId` is a `readonly record struct` over a `string`
(`/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/Services/HeadlessEntityId.cs:7-15`), and instances are looked up, not held
(`ICardInstanceRepository.TryGetInstance` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/Services/ICardInstanceRepository.cs:7`; the
stack itself is rebuilt by `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/State/DigivolutionStackReader.cs` /
`/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/State/DigivolutionStack.cs`). A mirror `Permanent` is therefore pinned to a top-card
`HeadlessEntityId` at construction and cannot observe its own mutation; a mirror `CardSource` is
re-materialised on every access, so nothing it computed can be cached in a field, and two `CardSource`
values for the same card are never reference-equal.

**What AS-IS offered.** Live reference objects: `Permanent(List<CardSource> cardSources)`
(`/home/hg/git/headlessDCGO/DCGO/Assets/Scripts/Script/Permanent.cs:9`) holding `cardSources` (`:880`) with `TopCard` (`:1352`) and `StackCards` (`:884`)
recomputed off the *same* mutating instance; `CardSource.Init()` (`/home/hg/git/headlessDCGO/DCGO/Assets/Scripts/Script/CardSource.cs:345`) resetting
per-instance state; and cached-condition properties `CardSource.IsPermanent` (`:3488`),
`CardSource.HasDigiXros` (`:2569`), `burstDigivolutionCondition`, `appFusionCondition`, `jogressCondition`,
`BasePlayCostFromEntity`, plus `CardSource.PermanentOfThisCard()`. Reference identity made
`List<CardSource>.Contains(cardSource)` correct.

**What the substrate would have to provide.** Instance-identity objects with substrate-owned lifetime: a
`CardSource`/`Permanent` the repository returns *by reference* per id (so `Contains` works, `Init()` has a
target, properties can cache, and a `Permanent` observes its own `AddCardSource`), plus a `Permanent`
constructor taking the under-card list. Then `Any(x => x.InstanceId == …)` reverts to `Contains(…)`,
`XConditionOf()` methods revert to properties, the per-iteration `new Permanent(context, currentTopId, …)`
re-pinning reverts to re-reading `_permanent.TopCard`, and
`ICardEffect.ResolvePermanentOfThisCard(card)` reverts to `card.PermanentOfThisCard()`.

<details><summary><b>25 rows</b> (TSV row → port <code>file:line</code>)</summary>

- `r009` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:2965` — IsBurst `card.burstDigivolutionCondition` property became `card.BurstDigivolutionConditionOf()` method (re-evaluated each access)
- `r014` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:3118` — `card.IsPermanent` property became `card.IsPermanent()` method (also at filter-cards :1001-1002)
- `r018` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:3318` — `card.BasePlayCostFromEntity` property became `card.BasePlayCostFromEntity()` method
- `r019` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:3356` — `cardSource.PermanentOfThisCard()` became `ICardEffect.ResolvePermanentOfThisCard(cardSource)`
- `r020` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:3376` — `card.HasDigiXros` property became `card.HasDigiXros()` method (IsShowEffect and DigiXros-select :745)
- `r026` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:3544` — `card.jogressCondition` property became `card.JogressConditionOf()` method (:819/:821 and :3544/:3546)
- `r033` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:3889` — `card.HasDigiXros` property became `card.HasDigiXros()` method call
- `r040` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:3985` — `card.Init()` call removed (transient-view substrate no-op)
- `r041` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:3998` — `permanent.AddCardSource(card)` (void mutate) → `permanent = await permanent.AddCardSource(card, ct)` (returns rebound permanent view)
- `r044` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:4022` — jogress `card.Init()` call removed
- `r058` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:4339` — card.Init() call removed (CardSource transient-view has no Init state)
- `r119` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:2702` — `_linkCard.PermanentOfThisCard()` → `ICardEffect.ResolvePermanentOfThisCard(_linkCard)` (CardSource.PermanentOfThisCard bridge)
- `r133` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:4738` — `attackProcess.SecurityDigimon = brokenSecurityCard` changed to `= brokenSecurityCard.InstanceId` (mirror AttackProcess.SecurityDigimon is HeadlessEntityId? not a CardSource)
- `r147` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:751` — IsDestroyed `DestroyedSecurity.Contains(cardSource)` changed to `DestroyedSecurity.Any(d => d.InstanceId == cardSource.InstanceId)` (CardSource is a value wrapper recreated per access, no reference identity)
- `r159` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:5121` — snapshot constructor `new Permanent(permanent.cardSources)` changed to `new Permanent(context, InstanceId, OwnerId)` at all four snapshot sites (4541/4549/4682/4690); mirror Permanent has no cardSources ctor
- `r166` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:933` — loop reads live `_permanent.TopCard` each iteration; port pins a `new Permanent(context,currentTopId,OwnerId)` view and walks currentTopId via NextPromotedSourceId + ArmorPurgeTopAsync return; mirror Permanent is an id-pinned view not a live mutating object
- `r176` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:1077` — loop rebuilds `new Permanent(context, currentTopId, ownerId)` each iteration via NextPromotedSourceId walk instead of re-reading `_permanent.TopCard`
- `r180` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:1154` — IsTrashed `TrashedCards.Contains(cardSource)` becomes `.Any(t=>t.InstanceId==cardSource.InstanceId)`
- `r187` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:1357` — IsTrashed `TrashedLinkCards.Contains` becomes `.Any(InstanceId==)`
- `r200` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:1805` — IsSuspended `SuspendedPermanents.Contains` becomes `.Any(InstanceId==)`
- `r205` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:1958` — `permanent.CanUnsuspend` property becomes `CardEffectCommons.CanUnsuspend(permanent)`
- `r218` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:2766` — `_permanent.StackCards.Count` becomes `_permanent.DigivolutionCards.Count + 1` (no StackCards property on mirror Permanent)
- `r219` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:2777` — loop rebuilds `new Permanent(...)` each iteration; `RemoveFromAllArea`+`AddTrashCard(IsToken)` replaced by `ArmorPurgeTopAsync`
- `r223` `ABSENT` — Original ctor Permanent(List<CardSource>) plus SetCardSources bulk-seed a per-instance under-card list via Clone+AddCardSource loop; the port Permanent is a transient view over DigivolutionStack state with no per-instance list and no SetCardSources method (its ctors take EngineContext/HeadlessEntityId).
- `r253` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/Permanent.cs:107` — Port TopCard is `new CardSource(_context, InstanceId, OwnerId)` — the mirror Permanent is keyed on the top-card InstanceId itself, dropping AS-IS `cardSources.First(not-in-LinkedCards)` selection and the empty-permanent null return [CardSource/InstanceId identity model]

</details>

---

## 5. S5 — `ICardEffect` cannot cross a substrate zone move — effect causality is re-expressed as a `HeadlessEntityId` cause id plus a batch id

**Rows accounted for: 24.**

**Substrate member or decision at fault.** `IZoneMover` is the trigger chokepoint, and its API carries
causality as **ids stamped on the move**, not as the effect object:
`TrashCardAsync(HeadlessPlayerId, HeadlessEntityId cardId, long? discardBatchId,
HeadlessEntityId? causeEffectId, bool isRevealTrash, CancellationToken)`
(`/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/Services/IZoneMover.cs:33-39`), with the same `causeEffectId`/batch-id pair on
`AddToHandAsync` (`:15-20`), `AddToSecurityAsync` (`:45`) and `DrawAsync` (`:55-60`). The batch ids
themselves are minted by the context (`NextDiscardBatchId` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/Bridge/EngineContext.cs:144`,
`NextAddHandBatchId` `:157`, `NextSecurityAddBatchId` `:172`, `NextDeletionBatchId` `:120`) because the
substrate derives reactor fires from emitted `CardMoved` events and needs an explicit key to collapse one
effect's multi-card move into a single fire — the AS-IS coroutine call stack supplied that grouping
implicitly.

**What AS-IS offered.** The `ICardEffect` object itself was passed down and its
`EffectSourceCard` (`/home/hg/git/headlessDCGO/DCGO/Assets/Scripts/Script/ICardEffect.cs:49`) read at the trigger site; a `cardEffect == null` test *was*
the "was this an effect?" gate, and the enclosing coroutine *was* the batch.

**What the substrate would have to provide.** `IZoneMover` overloads that accept the `ICardEffect` itself
(and derive both cause and batch identity from it — e.g. a substrate-owned "current resolving effect"
frame). Then ~24 constructors shed their `HeadlessEntityId? causeEffectSourceId` parameter and their
`long? batchId` threading, and the collapsed `_causeEffectSourceId is not { IsEmpty: false }` gates revert
to the AS-IS two-guard `_cardEffect == null || _cardEffect.EffectSourceCard == null` form.

<details><summary><b>24 rows</b> (TSV row → port <code>file:line</code>)</summary>

- `r001` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:57` — IDiscardHands ctor gains a `HeadlessEntityId? causeEffectSourceId` param and makes cardEffect optional; the id is stamped on the substrate trash move
- `r003` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:85` — added `context.NextDiscardBatchId()` batch-id derivation threaded into each Discard() call, absent in original
- `r004` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:133` — Discard() gains `long? discardBatchId, HeadlessEntityId? causeEffectSourceId, CancellationToken` params for the substrate move
- `r078` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:482` — ctor `(Player, int, ICardEffect cardEffect)` became `(EngineContext, HeadlessPlayerId, int, HeadlessEntityId? causeEffectSourceId)`
- `r082` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:663` — `new ITrashDigivolutionCards(_permanent, selectedCards, _cardEffect)` gained an extra `CauseEffectSourceId` argument before `_cardEffect`
- `r106` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:2409` — AddDigivolutionCardsTop 2nd arg `_cardEffect` → `_cardEffect?.EffectSourceCard?.InstanceId` (Permanent.AddDigivolutionCardsTop param is InstanceId not ICardEffect)
- `r107` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:2413` — AddDigivolutionCardsBottom 2nd arg `_cardEffect` → `_cardEffect?.EffectSourceCard?.InstanceId`
- `r115` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:2615` — AddLinkCard 2nd arg `_cardEffect` → `_cardEffect?.EffectSourceCard?.InstanceId`
- `r120` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:2704` — AddLinkCard 2nd arg `_cardEffect` → `_cardEffect?.EffectSourceCard?.InstanceId`
- `r163` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:874` — constructor adds `HeadlessEntityId? causeEffectSourceId` param and `_causeEffectSourceId` field and makes cardEffect an optional trailing param; cause identity threaded as an id instead of via _cardEffect
- `r164` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:894` — two guards `_cardEffect==null`/`_cardEffect.EffectSourceCard==null` (4803-4804) collapsed into id-presence guard `_causeEffectSourceId is not { IsEmpty:false }`
- `r172` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:1016` — ctor gains `HeadlessEntityId? causeEffectSourceId` param (cause identified by source-entity id) alongside the retained `_cardEffect`
- `r173` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:1037` — two guards `_cardEffect==null` + `_cardEffect.EffectSourceCard==null` collapsed into one `_causeEffectSourceId is not {IsEmpty:false}` check
- `r179` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:1146` — ctor gains `causeEffectSourceId`; `_cardEffect==null` guard becomes an id gate
- `r181` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:1167` — guard `_cardEffect==null` replaced by `_causeEffectSourceId is not {IsEmpty:false} causeId`
- `r186` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:1347` — ctor gains `causeEffectSourceId`; `.Clone()` becomes null-guarded copy
- `r188` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:1377` — guard `_cardEffect != null && ...` becomes `_causeEffectSourceId is {} causeId && !causeId.IsEmpty && ...`
- `r191` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:1536` — CanNotBeAffected gate `CardEffect != null && ...` becomes `_causeEffectSourceId is {IsEmpty:false} && ...`
- `r201` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:1833` — PermanentCondition gate `CardEffect != null` becomes `_causeEffectSourceId is {IsEmpty:false}`
- `r204` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:1929` — ctor derives `_causeEffectSourceId = cardEffect?.EffectSourceCard?.InstanceId`; permanents null-guarded
- `r206` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:1960` — CanNotBeAffected gate `_cardEffect == null || ...` becomes `_causeEffectSourceId is not {IsEmpty:false} || ...`
- `r209` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:2064` — ctor derives `_causeEffectSourceId` from cardEffect
- `r216` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:2732` — ctor gains `causeEffectSourceId`; `_cardEffect` demoted to optional
- `r217` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:2753` — two guards `_cardEffect==null` + `_cardEffect.EffectSourceCard==null` collapsed into one `_causeEffectSourceId is not {IsEmpty:false}`

</details>

---

## 6. S7 — Players are `HeadlessPlayerId` values, not live `Player` objects — every `card.Owner.X` becomes `new Player(context, id).X`

**Rows accounted for: 17.**

**Substrate member or decision at fault.** `HeadlessPlayerId` is a `readonly record struct` wrapping an
`int` (`/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/Services/HeadlessPlayerId.cs:7-19`), and it is the player currency of every
substrate API — `IZoneStateReader.GetCards(HeadlessPlayerId, ChoiceZone)`
(`/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/Services/IZoneStateReader.cs:8`), every `IZoneMover` method
(`/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/Services/IZoneMover.cs:15,22,33,45,55`),
`PlayerTurnCounterController.Increment(HeadlessPlayerId, string)`
(`/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/Runtime/PlayerTurnCounterController.cs:25`),
`SecurityFaceState.FaceUpSecurityCards(EngineContext, HeadlessPlayerId)`
(`/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/Runtime/SecurityFaceState.cs:67`). There is no substrate type that *is* a player, so the
mirror `Player` is a view constructed on demand from `(EngineContext, HeadlessPlayerId)` and
`CardSource.Owner` can only return the id.

**What AS-IS offered.** `CardSource.Owner` was a `Player` reference —
`public Player Owner { get; private set; }` (`/home/hg/git/headlessDCGO/DCGO/Assets/Scripts/Script/CardSource.cs:25`) — so `card.Owner.HandCards`,
`card.Owner.ExecutingCards`, `card.Owner.brainStormObject`, `card.Owner.UntilCalculateFixedCostEffect`,
`card.Owner.MaxMemoryCost`, `card.Owner.GetFieldPermanents()` were single member accesses, and constructors
took `Player player` directly.

**What the substrate would have to provide.** A substrate-owned, id-stable `Player` instance (a
`IPlayerRepository.Get(HeadlessPlayerId) -> Player` returning the same object) and a mirror
`CardSource.Owner` typed `Player`. Then every `new Player(card.Context, card.Owner).X` collapses back to
`card.Owner.X`, and the ctor/field pairs `(EngineContext context, HeadlessPlayerId playerId)` revert to the
single `Player player` parameter/field.

<details><summary><b>17 rows</b> (TSV row → port <code>file:line</code>)</summary>

- `r013` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:3087` — recurring `card.Owner.<HandCards/LibraryCards/GetFieldPermanents/SecurityCards/TrashCards/MaxMemoryCost/UntilCalculateFixedCostEffect>` wrapped as `new Player(card.Context, card.Owner).<...>` throughout PlayCard (CardSource.Owner returns HeadlessEntityId, not Player)
- `r034` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:3923` — `card.Owner.GetFieldPermanents()` → `new Player(card.Context, card.Owner).GetFieldPermanents()` (card.Owner is an id, mirror Player must be constructed)
- `r035` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:3926` — `card.Owner.SecurityCards` → `new Player(card.Context, card.Owner).SecurityCards`
- `r061` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:4386` — `card.Owner.ExecutingCards` became `new Player(card.Context, card.Owner).ExecutingCards` at all three sites (:1788/:1836/:1888) because CardSource.Owner returns HeadlessPlayerId not Player
- `r064` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:235` — ctor param `Player player` became `EngineContext context, HeadlessPlayerId playerId`
- `r068` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:289` — StackSkillInfos entry `{"Player", _player}` became `{"Player", new Player(_context, _playerId)}`
- `r069` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:311` — ctor `(int, Player player, ICardEffect)` became `(EngineContext, HeadlessPlayerId, int, ICardEffect?)`
- `r074` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:431` — ctor `(Player, int addLifeCount)` became `(EngineContext, HeadlessPlayerId, int addSecurityCount, bool faceUp=false)`
- `r121` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:2708` — `_linkCard.Owner.UntilCalculateFixedCostEffect = ...` → `new Player(context, _linkCard.Owner).UntilCalculateFixedCostEffect = ...` (Owner is id, wrapped in mirror Player)
- `r122` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:5890` — `_permanent.TopCard.Owner.CanAddSecurity(cardEffect)` → `new Player(...Context, ...Owner).CanAddSecurity(cardEffect?.EffectSourceCard?.InstanceId)` (Owner id wrapped in Player; param InstanceId not ICardEffect)
- `r138` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:4835` — SelectCardEffect.SetUp arg `selectPlayer: player` changed to `selectPlayer: player.PlayerId` (SetUp takes HeadlessPlayerId not Player)
- `r141` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:4962` — `brokenSecurityCard.Owner.ExecutingCards.Contains(...)` changed to `new Player(brokenSecurityCard.Context, brokenSecurityCard.Owner).ExecutingCards.Contains(...)` (Owner is HeadlessPlayerId id, wrapped into a Player view)
- `r144` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:718` — ctor1 param `Player player` changed to `EngineContext context, HeadlessPlayerId playerId` and adds `_causeEffectSourceId = cardEffect?.EffectSourceCard?.InstanceId`
- `r145` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:730` — ctor2 param `Player player` changed to `EngineContext context, HeadlessPlayerId playerId` and adds `_causeEffectSourceId = cardEffect?.EffectSourceCard?.InstanceId`
- `r146` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:742` — field `Player _player` replaced by `EngineContext _context` + `HeadlessPlayerId _playerId`
- `r194` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:1617` — hashtable "Player" value built via `new Player(_context, _playerId)` instead of the passed Player
- `r195` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:1669` — `_player` field type `Player` becomes `HeadlessPlayerId` (from source.Owner)

</details>

---

## 7. S9 — No field-slot / `FieldCardFrame` model — the fixed frame array is replaced by a compacted permanent list and zone membership

**Rows accounted for: 15.**

**Substrate member or decision at fault.** The substrate models board position as **zone membership only**:
`ChoiceZone` (`/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/Choices/ChoiceZone.cs:3-18`) has `BattleArea` and `BreedingArea` but no slot,
index, or capacity concept, and `IZoneStateReader.GetCards` returns a flat
`IReadOnlyList<HeadlessEntityId>` (`/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/Services/IZoneStateReader.cs:8`) — a compacted list with
no empty slots. There is no `FieldCardFrame` anywhere under `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/`; the port re-invented a
frame-shaped facade in the mirror instead (`/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/Player.cs:991`,
`public sealed class FieldCardFrame`, which documents that "the Unity slot GameObject … and the fixed
per-player `fieldCardFrames` slot array are NOT ported (no slot/capacity model)" and answers
`IsBattleAreaFrame()` by zone membership rather than by thresholding `FrameID`).

**What AS-IS offered.** `Player.fieldCardFrames` (`/home/hg/git/headlessDCGO/DCGO/Assets/Scripts/Script/Player.cs:602`, a
`List<FieldCardFrame>`) and the parallel fixed array `Player.FieldPermanents = new Permanent[16]`
(`/home/hg/git/headlessDCGO/DCGO/Assets/Scripts/Script/Player.cs:663`); `FieldCardFrame` itself (`/home/hg/git/headlessDCGO/DCGO/Assets/Scripts/Script/Player.cs:1542`) with `FrameID` (`:1551`),
`GetFramePermanent()` (`:1578`), `IsEmptyFrame()`, `IsBattleAreaFrame()`/`isBreedingAreaFrame()`; and
`Permanent.PermanentFrame` (`/home/hg/git/headlessDCGO/DCGO/Assets/Scripts/Script/Permanent.cs:27`) resolving a permanent to its slot. Frame ids were
stable board *positions*, which is why `CreateNewPermanent(permanent, frameId)`, `PreferredFrame`,
`CanPlayCardTargetFrame(digieggFrame, …)` and the jogress survivor's `targetFrameID` existed.

**What the substrate would have to provide.** A slot-addressed field model — fixed-capacity per-player
field arrays with stable indices (`Permanent?[16]` with empties) exposed off the zone reader, plus a
`BreedingArea` slot flag. Then `GetFieldPermanents()[i]` reverts to
`fieldCardFrames[i].GetFramePermanent()`, `framePlaceable`/`isBreedingArea` revert to `frameId`/
`PreferredFrame` resolution, the added `0 <= frameID < count` bounds checks disappear, and the jogress
survivor again keeps its board position.

<details><summary><b>15 rows</b> (TSV row → port <code>file:line</code>)</summary>

- `r008` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:2876` — SetBurst bound `card.Owner.fieldCardFrames.Count-1` became `new Player(card.Context, card.Owner).GetFieldPermanents().Count-1`, a tighter compacted-list bound (no slot array)
- `r010` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:2991` — BurstTamer `card.Owner.fieldCardFrames[_burstTamerFrameID].GetFramePermanent()` became compacted `GetFieldPermanents()[_burstTamerFrameID]`
- `r011` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:3010` — IsAppFusion `card.appFusionCondition` property and `fieldCardFrames[i].GetFramePermanent()` became `card.AppFusionConditionOf()` method and `GetFieldPermanents()[i]`
- `r012` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:3043` — LinkedCard `card.Owner.fieldCardFrames[i].GetFramePermanent()` became `new Player(...).GetFieldPermanents()[i]`
- `r037` `ABSENT` — `int frameId = -1;` local removed (replaced by `framePlaceable` bool)
- `r038` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:3942` — frame-targeting block rewritten: breeding `card.CanPlayCardTargetFrame(digieggFrame, false, CardEffect, isBreedingArea)` became `card.CanEnterField(CardEffect)`; PreferredFrame and target-frame FrameID resolution collapsed to `framePlaceable=true` (no FieldCardFrame model)
- `r039` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:3974` — `CanPlayAsNewPermanent(..., isBreedingArea: _isBreedingArea, isPlayOption:...)` — `isBreedingArea` argument dropped (mirror CanPlayAsNewPermanent has no isBreedingArea parameter)
- `r042` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:4006` — `new Permanent{IsSuspended=_isTapped}` + `CreateNewPermanent(permanent, frameId)` collapsed to single `CreateNewPermanent(card, isSuspended:_isTapped, isBreedingArea:_isBreedingArea)`; frameId argument replaced by isBreedingArea
- `r045` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:4032` — `card.Owner.fieldCardFrames[frameID].GetFramePermanent()` → `GetFieldPermanents()[frameID]` with added `0<=frameID<count` bounds check (frame IDs reinterpreted as field-permanent list indexes)
- `r046` `ABSENT` — `int targetFrameID = evoRootPermanents[0].PermanentFrame.FrameID;` removed (frame-less; jogress survivor field position not preserved)
- `r047` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:4087` — jogress `new Permanent{IsSuspended=false}` + `CreateNewPermanent(permanent, targetFrameID)` → `CreateNewPermanent(card, isSuspended:false)`; targetFrameID dropped
- `r225` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/Permanent.cs:124` — PermanentFrame resolves the slot from the fixed TopCard.Owner.FieldPermanents array and returns a pre-built fieldCardFrames[index]; the port uses new Player(_context,OwnerId).GetFieldPermanents() list index and constructs new FieldCardFrame(...) on demand (no slot/frame arrays in mirror Player).
- `r260` `Permanent.cs:3407` — CanMove breeding outer test changes from TopCard.PermanentOfThisCard().PermanentFrame.isBreedingAreaFrame() to GetBreedingAreaPermanents().Contains(this), making the nested !Contains(this) guard tautologically unreachable (behaviour-equivalent under frame→membership map) [Player.GetBreedingAreaPermanents membership; frame model absent RD-P6C1-1]
- `r261` `Permanent.cs:3502` — CanBlock frame check `if(PermanentFrame!=null){if(!IsBattleAreaFrame())return false;}` becomes unconditional `if(!IsPermanentExistsOnBattleArea(this))return false;` (null-frame no longer skips the check) [CardEffectCommons.IsPermanentExistsOnBattleArea; frame model absent RD-P6C1-1]
- `r262` `Permanent.cs:3593` — CanAttackTargetDigimon frame check adapted the same way as CanBlock (PermanentFrame!=null gate → unconditional IsPermanentExistsOnBattleArea) [CardEffectCommons.IsPermanentExistsOnBattleArea; frame model absent RD-P6C1-1]

</details>

---

## 8. S11 — Unity / Photon library types are absent — `Photon Hashtable`, `Mathf`, and the AS-IS `List<T>.Clone()` extension

**Rows accounted for: 10.**

**Substrate member or decision at fault.** The substrate is a plain .NET class library: nothing under
`/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/` references `UnityEngine` or `ExitGames.Client.Photon`, and no substrate file supplies a
`Clone()` extension or a `Hashtable`-based effect-payload carrier. Effect payloads that survived are carried
as `System.Collections.Hashtable`, and the substrate offers no `GetCardEffectFromHashtable`-style indirection —
causality is an id (see S5, `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/Services/IZoneMover.cs:33-39`).

**What AS-IS offered.**
- `ExitGames.Client.Photon.Hashtable` as the universal effect-parameter bag (`HatchDigiEggClass(Player, Hashtable)`
  `/home/hg/git/headlessDCGO/DCGO/Assets/Scripts/Script/CardController.cs:1058`; `IChangeMemoryPlayCost(Hashtable)`-style ctors throughout).
- `IEnumerableExtension.Clone<T>(this List<T>)` (`/home/hg/git/headlessDCGO/DCGO/Assets/Scripts/Script/IEnumerableExtension.cs:82`) and
  `CloneArray<T>` (`:87`).
- `UnityEngine.Mathf.Clamp` (used at `/home/hg/git/headlessDCGO/DCGO/Assets/Scripts/Script/Permanent.cs:1728`).

**What the substrate would have to provide.** A mirrored `Hashtable` payload type (or the Photon
`Hashtable` shim) accepted by the substrate move/trigger APIs, the `Clone<T>`/`CloneArray<T>` extension
methods, and a `Mathf` shim. Then `.ToList()` reverts to `.Clone()`, `Math.Clamp` to `Mathf.Clamp`, and the
`Hashtable`-typed constructors are restorable verbatim.

<details><summary><b>10 rows</b> (TSV row → port <code>file:line</code>)</summary>

- `r031` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/Services/InMemoryZoneMover.cs:281` — constructor `_hashtable` parameter/field dropped; HatchDigitamaAsync takes (playerId, cancellationToken) only
- `r052` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:4154` — `permanent.TopCard.CardNames.Clone()` → `.ToList()`
- `r053` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:4172` — `permanent.TopCard.CardNames.Clone()` → `.ToList()`
- `r054` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:4174` — `permanent.TopCard.CardTraits.Clone()` → `.ToList()`
- `r081` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:619` — SetUp arg `customRootCardList: _permanent.DigivolutionCards` became `_permanent.DigivolutionCards.ToList()`
- `r190` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:1505` — ctor `Hashtable hashtable` replaced by `causeEffectSourceId` + `cardEffect`; the run-time `GetCardEffectFromHashtable(_hashtable)` (:5377) indirection dropped
- `r199` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:1794` — ctor `Hashtable hashtable` replaced by `(ICardEffect? cardEffect, bool isBlock)`; the `IsAttack` extraction (read at :5583, never used) is dropped
- `r257` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/Permanent.cs:2730` — InvertSecutiryValue clamp uses Unity `Mathf.Clamp(Invert,-1,1)` AS-IS vs `Math.Clamp(Invert,-1,1)` in port; otherwise verbatim [System.Math replaces UnityEngine.Mathf]
- `r264` `Permanent.cs:2730` — InvertSecutiryValue clamp uses Math.Clamp instead of Unity Mathf.Clamp (same [-1,1] result) [System.Math.Clamp for UnityEngine.Mathf.Clamp]
- `r286` `Permanent.cs:5080` — EmitWhenLinkedAsync faithfully ports AS-IS AddLinkCard's WhenLinked emit (:1281-1290): same Hashtable payload {Permanent, CardEffect, Card, isFromDigimon}, single StackSkillInfos(hashtable, EffectTiming.WhenLinked); AS-IS Photon Hashtable + GManager.autoProcessing become System.Collections.Hashtable + AutoProcessing.For(context); null CardEffect via BareCauseEffect.ForOrNull [AutoProcessing.For(context).StackSkillInfos replacing GManager.instance.autoProcessing]

</details>

---

## 9. S3 — `Player`'s `List<CardSource>` zone fields are absent — zone reads go through `IZoneStateReader.GetCards`

**Rows accounted for: 9.**

**Substrate member or decision at fault.** Zone contents live in substrate match state and are surfaced only
as id lists: `IZoneStateReader.GetCards(HeadlessPlayerId playerId, ChoiceZone zone)` returning
`IReadOnlyList<HeadlessEntityId>` (`/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/Services/IZoneStateReader.cs:6-10` — the file still
carries its own `// TODO: Replace with read-only access to final Player/Card zone state.` marker at `:5`),
over the `ChoiceZone` enum (`/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/Choices/ChoiceZone.cs:3-18`). Two consequences beyond the call
shape: the result is a **snapshot of ids**, so `CardSource` values must be re-constructed per element, and a
live per-iteration re-read of a mutating list is no longer expressible.

**What AS-IS offered.** Live, indexable, mutating `List<CardSource>` fields on `Player`:
`LibraryCards` (`/home/hg/git/headlessDCGO/DCGO/Assets/Scripts/Script/Player.cs:498`), `DigitamaLibraryCards` (`:502`), `HandCards` (`:506`),
`TrashCards` (`:510`), `LostCards` (`:514`), `SecurityCards` (`:518`), `ExecutingCards` (`:522`).

**What the substrate would have to provide.** Those same `List<CardSource>` collections owned by a
substrate-backed live `Player` (see S7) — i.e. a zone reader that hands back the *live* card collection
rather than an id snapshot. Then `zones.GetCards(_playerId, ChoiceZone.Library).Count` reverts to
`_player.LibraryCards.Count`, `library[i]` + `new CardSource(...)` reverts to `_player.LibraryCards[i]`, and
`.Contains(InstanceId)` reverts to `.Contains(cardSource)`.

<details><summary><b>9 rows</b> (TSV row → port <code>file:line</code>)</summary>

- `r005` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:139` — `cardSource.Owner.HandCards.Contains(cardSource)` became `zones.GetCards(Owner, ChoiceZone.Hand).Contains(InstanceId)`
- `r065` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:267` — `_player.LibraryCards.Count <= 0` became `zones.GetCards(_playerId, ChoiceZone.Library).Count <= 0`
- `r070` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:345` — `_player.LibraryCards.Count == 0` became `zones.GetCards(_playerId, ChoiceZone.Library).Count == 0`
- `r071` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:350` — live per-iteration `_player.LibraryCards[i]` reads replaced by a single `zones.GetCards(...Library)` snapshot indexed as `library[i]` + `new CardSource(...)` construction
- `r079` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:498` — `_player.LibraryCards.Count == 0` became `zones.GetCards(_playerId, ChoiceZone.Library).Count == 0`
- `r148` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:760` — StopDestroySecurity `_player.SecurityCards.Count == 0` changed to `zones.GetCards(_playerId, ChoiceZone.Security).Count == 0` via IZoneStateReader (no Player.SecurityCards)
- `r150` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:773` — outer guard `_player.SecurityCards.Count >= 1` changed to `zones.GetCards(_playerId, ChoiceZone.Security).Count >= 1`
- `r151` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:791` — inner guard and switch read `zones.GetCards(_playerId, Security)` and build `new CardSource(_context, security[i], _playerId, _playerId)` instead of indexing `_player.SecurityCards[i]`
- `r210` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:2078` — `cardSource.Owner.LibraryCards.Contains(cardSource)` becomes `zones.GetCards(Owner,Library).Contains(InstanceId)`

</details>

---

## 10. S4 — `CardObjectController`'s zone-move coroutines are absent — all moves route through the async `IZoneMover`

**Rows accounted for: 8.**

**Substrate member or decision at fault.** `IZoneMover` (`/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/Services/IZoneMover.cs:5`) is the
only move API, and its shape differs from the AS-IS helpers in three ways: it is `Task`-returning
(`MoveAsync` `:9`), it is **request-based** with an explicit source zone
(`ZoneMoveRequest`, `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/Services/ZoneMoveRequest.cs`) rather than deriving the source from the
card, and it fuses multi-step AS-IS sequences into single primitives
(`DrawAsync` `:55-60` = remove-from-all-areas + add-hand; `AddToSecurityAsync` `:45`;
`TrashCardAsync` `:33-39`). It also emits the `GameEvent` stream (`Events` `:7`) that reactors key off.

**What AS-IS offered.** `CardObjectController` static coroutines:
`RemoveFromAllArea(CardSource)` (`/home/hg/git/headlessDCGO/DCGO/Assets/Scripts/Script/CardObjectController.cs:370`),
`AddHandCards(List<CardSource>, bool isDraw, ICardEffect)` (`:559`),
`AddTrashCard(CardSource)` (`:717`) — which **derived** the card's current zone —
`AddTrashCards` (`:739`), `AddLibraryBottomCards(List<CardSource>, bool notAddLog)` (`:863`),
`AddSecurityCard(CardSource, bool toTop, bool faceUp, bool useEffect)` (`:976`).

**What the substrate would have to provide.** A mirrored `CardObjectController` façade over `IZoneMover`
with the AS-IS signatures and *decomposed* semantics — in particular a zone-deriving
`AddTrashCard(cardSource)` (so `ZoneMoveRequest(..., From: ChoiceZone.Security, ...)` need not hardcode a
source), a batch `AddLibraryBottomCards(cardSources)`, and separate `RemoveFromAllArea` + `AddHandCards`
instead of the fused `DrawAsync`. Then the per-card loops and fused calls revert to the AS-IS statements.

<details><summary><b>8 rows</b> (TSV row → port <code>file:line</code>)</summary>

- `r007` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:144` — `CardObjectController.AddTrashCard(cardSource)` became `context.ZoneMover.TrashCardAsync(Owner, InstanceId, discardBatchId, causeEffectSourceId, ...)`
- `r066` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:272` — per-card RemoveFromAllArea loop + `AddHandCards(DrawCards, true, _cardEffect)` collapsed into `ZoneMover.DrawAsync(_playerId, _drawCount, addHandBatchId, _causeEffectSourceId)`
- `r075` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:448` — loop `CardObjectController.AddSecurityCard(LibraryCards[0], useEffect:i==0)` replaced by `ZoneMover.AddSecurityFromLibraryAsync(...)` + per-card `SecurityFaceState.Stamp` + `new IAddSecurity(cardSource).AddSecurity()`
- `r153` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:817` — `CardObjectController.AddTrashCard(destroyedSecurityCard)` replaced by `_context.ZoneMover.MoveAsync(new ZoneMoveRequest(_playerId, card.InstanceId, ChoiceZone.Security, ChoiceZone.Trash, Metadata: null))` which hardcodes source zone Security whereas AddTrashCard derived it
- `r168` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:954` — `RemoveFromAllArea(cardSource)` + `if(!IsToken) AddTrashCard(cardSource)` (4887-4892) replaced by single `Permanent.ArmorPurgeTopAsync(...)` ZoneMover call; isToken guard preserved inside (Permanent.cs:5459 isToken?None:Trash)
- `r177` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:1090` — `RemoveFromAllArea(cardSource)` + `if(!IsToken) AddTrashCard(cardSource)` replaced by single `Permanent.ArmorPurgeTopAsync(...)`
- `r192` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:1562` — `AddLibraryBottomCards(_cardSources)` batch call becomes per-card loop of `Permanent.PlaySpecificSourceAsync(..., ChoiceZone.Library, ...)`
- `r211` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:2094` — `AddTrashCard(cardSource)` becomes `ZoneMover.TrashCardAsync(Owner, InstanceId, discardBatchId, causeId)` with a shared NextDiscardBatchId

</details>

---

## 11. S10 — `CardSource.IsFlipped` / `SetFace()` / `SetReverse()` are absent — security face-up state is a substrate metadata stamp

**Rows accounted for: 8.**

**Substrate member or decision at fault.** Face state is not on the card; it is a metadata key written and
read by a static substrate helper: `SecurityFaceState`
(`/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/Runtime/SecurityFaceState.cs:21`) with `FaceUpKey = "securityFaceUp"` (`:25`),
`Stamp(ICardInstanceRepository, HeadlessEntityId, bool faceUp)` (`:29`, which upserts a replacement
`CardInstanceRecord`), `IsFaceUpInSecurity(EngineContext, HeadlessEntityId)` (`:46`) and
`FaceUpSecurityCards(EngineContext, HeadlessPlayerId)` (`:67`). Face-up-ness is additionally stamped by
`IZoneMover.AddToSecurityAsync(..., bool faceUp, ...)` (`/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/Services/IZoneMover.cs:45`) — i.e.
it is a property of the *move*, not of the card. Note the polarity inversion this forces: AS-IS tests
`!cardSource.IsFlipped`, the port tests `IsFaceUpInSecurity(...)`.

**What AS-IS offered.** `CardSource.IsFlipped { get; private set; }` (`/home/hg/git/headlessDCGO/DCGO/Assets/Scripts/Script/CardSource.cs:56`) with
`SetFace(string str = "")` (`:76`) and `SetReverse()` (`:91`) on the card object itself.

**What the substrate would have to provide.** `IsFlipped` as real per-card state (see S8) with `SetFace()`/
`SetReverse()` mutators on the mirror `CardSource`. Then `!cardSource.IsFlipped` and
`topCard.SetFace()`/`SetReverse()` are restorable verbatim and `SecurityFaceState` can be deleted.

<details><summary><b>8 rows</b> (TSV row → port <code>file:line</code>)</summary>

- `r059` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:4339` — card.SetFace() call removed (face state carried by the move stamp)
- `r126` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:5960` — `topCard.SetReverse()`/`topCard.SetFace()` face restamp → `Headless.Runtime.SecurityFaceState.Stamp(repo, InstanceId, faceUp:_isFaceup)` (CardSource.SetReverse/SetFace absent in mirror)
- `r196` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:1690` — face-up test `!_cardSource.IsFlipped` becomes `SecurityFaceState.IsFaceUpInSecurity(context, InstanceId)`
- `r197` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:1740` — entry guard `!_player.SecurityCards.Contains(_cardSource) || !_cardSource.IsFlipped` becomes `!zones.GetCards(_player,Security).Contains(InstanceId) || IsFaceUpInSecurity(...)`
- `r198` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:1747` — `_cardSource.SetFace()` becomes `SecurityFaceState.Stamp(repo, InstanceId, faceUp:true)`
- `r226` `Permanent.cs:437` — DP fold's face-up-security gate uses !cardSource.IsFlipped in original but SecurityFaceState.IsFaceUpInSecurity(_context, InstanceId) in port [Headless.Runtime.SecurityFaceState]
- `r229` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/Permanent.cs:253` — GetDP face-up security gate !cardSource.IsFlipped becomes !SecurityFaceState.IsFaceUpInSecurity(_context, InstanceId) [Headless.Runtime.SecurityFaceState]
- `r232` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/Permanent.cs:437` — DP getter face-up security gate !cardSource.IsFlipped becomes !SecurityFaceState.IsFaceUpInSecurity(_context, InstanceId) [Headless.Runtime.SecurityFaceState]

</details>

---

## 12. S12 — Coroutines became `async Task` — `ref` parameters are illegal and the synchronous `SelectCardEffect.Activate()` choice becomes a parked async request

**Rows accounted for: 6.**

**Substrate member or decision at fault.** The substrate's execution model is `Task`-based
(`EngineTaskRunner` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/Coroutines/EngineTaskRunner.cs`, threaded as
`EngineContext.TaskRunner` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/Bridge/EngineContext.cs:84`), and every move/choice API is
`async` with a `CancellationToken` (`/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/Services/IZoneMover.cs:9,15,33,45,55`). C# forbids
`ref`/`out` parameters on async methods, so AS-IS `ref List<SkillInfo>` collectors cannot survive.
Choices are worse than async — they are **re-entrant and parkable**:
`IChoiceProvider.ChooseAsync(ChoiceRequest, CancellationToken)`
(`/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/Choices/IChoiceProvider.cs:5`) is implemented by `DeferredChoiceProvider`
(`/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/Runtime/DeferredChoiceProvider.cs:60`) which throws
`DeferredChoicePendingException` (`:12`) and *parks* the frame (`SuspendResolution` `:135`) to be re-run,
against a request the controller holds as `PendingRequest`
(`/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/Runtime/InMemoryHeadlessChoiceController.cs:10`). A caller therefore cannot assume the
choice has resolved when the call returns.

**What AS-IS offered.** `IEnumerator` coroutines driven by
`ContinuousController.instance.StartCoroutine(...)`, which allowed `ref` collectors —
`IReduceSecurity(Player player, ref List<SkillInfo> refSkillInfos, ICardEffect cardEffect)`
(`/home/hg/git/headlessDCGO/DCGO/Assets/Scripts/Script/CardController.cs:5414`) — and a **synchronous-to-completion** selection primitive:
`SelectCardEffect.Activate()` (`/home/hg/git/headlessDCGO/DCGO/Assets/Scripts/Script/SelectCardEffect.cs:332`), plus `SelectCardPanel.OpenSelectCardPanel`
for its presentation.

**What the substrate would have to provide.** A coroutine-shaped driver (an `IEnumerator`-equivalent that
allows by-reference collectors) and a **run-to-completion** choice primitive — a `ChooseSync` that blocks
the resolution frame rather than unwinding it. Then `ref List<SkillInfo>` reverts, and
`Permanent.RemoveLinkedCard(removeCount > 0)` again trashes before returning, so `AddLinkCard`'s overflow
attach observes the trimmed list (currently it does not — this is the one row in this root with a
*behavioural* delta, `r252`).

<details><summary><b>6 rows</b> (TSV row → port <code>file:line</code>)</summary>

- `r048` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:4112` — `new IReduceSecurity(player, ref ContinuousController.instance.nullSkillInfos, ...)` → `new IReduceSecurity(card.Context, card.Owner, null, ...)`; `ref nullSkillInfos` collector became `null`
- `r062` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:4465` — selectCardPanel.OpenSelectCardPanel replaced by ChoiceProvider.ChooseAsync over synthesized ChoiceCandidates (index remapped via `InstanceId#i` ids)
- `r135` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:4754` — IReduceSecurity ctor `(player: brokenSecurityCard.Owner, refSkillInfos: ref triggeredSkillInfos, null)` changed to `(brokenSecurityCard.Context, brokenSecurityCard.Owner, refCollector: triggeredSkillInfos, cardEffect: null)` (Owner is HeadlessPlayerId, ref-param becomes list refCollector, added EngineContext)
- `r155` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:832` — IReduceSecurity ctor `(player: _player, refSkillInfos: ref ContinuousController.instance.nullSkillInfos, cardEffect: _cardEffect)` changed to `(_context, _playerId, refCollector: null, _cardEffect)` (Player→id, ref-nullSkillInfos sentinel becomes null collector)
- `r193` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:1596` — ctor `(Player, ref List<SkillInfo>, ICardEffect)` becomes `(EngineContext, HeadlessPlayerId, List<SkillInfo>?, ICardEffect?)` — `ref` and `Player` substituted
- `r252` `Permanent.cs:4116` — AS-IS RemoveLinkedCard(removeCount>0) runs SelectCardEffect.Activate() synchronously (owner picks and cards are trashed before return); port RequestChoice PARKS the choice and returns immediately, so callers (AddLinkCard overflow) attach before the trims resolve [IChoiceController async park; no synchronous SelectCardEffect coroutine primitive]

</details>

---

## 13. S13 — No location-change timestamp facility

**Rows accounted for: 2.**

**Substrate member or decision at fault.** No substrate service records a per-card "when did this card last
change location" stamp. The zone-move metadata vocabulary
(`/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/Services/ZoneMoveMetadataKeys.cs`) carries batch ids, cause ids, reveal flags and
`EnteredThisTurnKey` (`:16`) — a *turn-granular boolean*, not a monotonic timestamp — and neither
`CardInstanceRecord` (`/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/Services/CardInstanceRecord.cs:5-46`) nor `IZoneMover`
(`/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/Services/IZoneMover.cs`) exposes one. Tracked in the port as design item
`MIG3-LOCATIONTIME`.

**What AS-IS offered.** `CardSource.SetChangedLocationTime()` (`/home/hg/git/headlessDCGO/DCGO/Assets/Scripts/Script/CardSource.cs:130`), called on every
relocation (e.g. from `CardSource.Init()` at `:349`), stamping a per-card ordering timestamp that
"most recently moved" comparisons read.

**What the substrate would have to provide.** A monotonic per-card location-change counter on the instance
record (or a `IZoneMover` stamp incrementing it on every `CardMoved`). Then the two dropped
`_permanent.TopCard.SetChangedLocationTime()` calls are restorable verbatim.

<details><summary><b>2 rows</b> (TSV row → port <code>file:line</code>)</summary>

- `r178` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:1096` — `_permanent.TopCard.SetChangedLocationTime()` dropped (design item MIG3-LOCATIONTIME, no headless timestamp facility)
- `r220` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:2798` — `_permanent.TopCard.SetChangedLocationTime()` dropped (design item MIG3-LOCATIONTIME)

</details>

---

## 14. S14 — Memory is a single signed gauge, not per-player memory with a sign convention

**Rows accounted for: 1.**

**Substrate member or decision at fault.** `IHeadlessMemoryController`
(`/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/Runtime/IHeadlessMemoryController.cs:6`) models memory as one shared signed value —
`HeadlessMemoryState Current { get; }` (`:8`), `Initialize(int initialMemory, int minimum = -10,
int maximum = 10)` (`:10`), `Set(int)` (`:12`), `Add(int amount)` (`:14`), `CanPay`/`Pay` (`:16,:18`).
`Add` takes **no player argument**, so a per-player delta must first be converted to a turn-player-relative
sign at the call site. The file still carries its own
`// TODO: Replace with AS-IS memory/cost flow after card payment logic is ported.` marker (`:5`).

**What AS-IS offered.** `Player.AddMemory(int plusMemory, ICardEffect cardEffect)`
(`/home/hg/git/headlessDCGO/DCGO/Assets/Scripts/Script/Player.cs:1082`) — a coroutine on the *owning player*, taking the player-relative delta and the
causing effect directly, with the sign convention handled inside.

**What the substrate would have to provide.** A player-scoped memory API
(`Add(HeadlessPlayerId owner, int delta, ICardEffect cause)`) that owns the turn-player sign convention.
Then `cardSource.Owner.AddMemory(-cardSource.OverflowMemory, null)` is restorable verbatim and the
`MemoryDelta(overflow, owner, turnPlayer)` sign derivation at the call site disappears.

<details><summary><b>1 row</b> (TSV row → port <code>file:line</code>)</summary>

- `r212` `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:2167` — `cardSource.Owner.AddMemory(-cardSource.OverflowMemory, null)` becomes `context.MemoryController.Add(MemoryDelta(overflow, owner, turnPlayer))` deriving a turn-player-relative sign (single-signed gauge)

</details>

---

## 15. S15 — `CEntity_Base` (the Unity `ScriptableObject` card-data asset) has no substrate mirror

**Rows accounted for: 1.**

**Substrate member or decision at fault.** The substrate loads card data as its own JSON-backed records —
`CardRecord` / `ICardRepository` / `InMemoryCardRepository`
(`/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/Services/CardRecord.cs`, `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/Services/ICardRepository.cs`,
`/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/Services/InMemoryCardRepository.cs`), fed by
`/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/DataLoading/CardBaseEntityLoader.cs` / `CardAssetJsonLoader.cs`. There is no
`CEntity_Base`-shaped type anywhere under `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/`, so any AS-IS data literal typed as a
`CEntity_Base` (notably the hard-coded token definitions) has no type to be expressed in and had to be
re-encoded under an invented name.

**What AS-IS offered.** `CEntity_Base : ScriptableObject` (`/home/hg/git/headlessDCGO/DCGO/Assets/Scripts/Script/CEntity_Base.cs:9`) — the single card-data
type that both real cards and generated tokens were instances of.

**What the substrate would have to provide.** A `CEntity_Base` mirror record that `ICardRepository` returns,
so token specs are ordinary `CEntity_Base` instances again and the invented `TokenSpec` record/table can be
deleted.

<details><summary><b>1 row</b> (TSV row → port <code>file:line</code>)</summary>

- `r222` `src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs:1073` — TokenSpec record and TokenSpecs table re-encode the AS-IS CEntity_Base token data as a new type with no AS-IS name, since CEntity_Base has no headless mirror

</details>

---

## MISCLASSIFIED — 9 rows whose difference was not forced by the substrate

### `r030` — `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/Services/InMemoryZoneMover.cs:281`

> entire HatchDigiEggClass relocated to InMemoryZoneMover.HatchDigitamaAsync and reduced to a raw DigitamaLibrary→BreedingArea move (MoveFromZoneTop count:1)

**Not substrate-forced — fidelity/housing defect.** The AS-IS `HatchDigiEggClass.Hatch()` (`/home/hg/git/headlessDCGO/DCGO/Assets/Scripts/Script/CardController.cs:1056-1092`) runs a full `PlayPermanentClass` (`SetIsBreedingArea` + `SetIsHatching` + `PlayPermanent()` + the `EnterFieldTurnCount = -1` reset). The port relocated it INTO the substrate (`/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/Services/InMemoryZoneMover.cs:281`) and reduced it to a raw DigitamaLibrary->BreedingArea move, dropping the play pipeline. Nothing in the substrate prevented mirroring `HatchDigiEggClass` in `CardController.cs`. Should be classified as: **rule logic relocated into the substrate + logic loss (repair-ledger item, not a substrate root).**

### `r084` — `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:5376`

> instance call `autoProcessing_CutIn.HasAwaitingActivateEffects()` replaced by private static helper HasAwaitingActivateEffects(autoProcessing_CutIn) because port AutoProcessing surface does not expose HasAwaitingActivateEffects

**Not substrate-forced — the claim is false.** The port `AutoProcessing` DOES expose `public bool HasAwaitingActivateEffects()` (`/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/AutoProcessing.cs:1366`), and other port code calls it as an instance method (`/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Headless/Runtime/HeadlessGameLoop.cs:88,96`; `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:1216`). The five private static `HasAwaitingActivateEffects(AutoProcessing)` wrappers (CardController.cs:5457/5623/5838/5976/6213) are a mirror-local stylistic choice. Should be classified as: **gratuitous mirror deviation (revert to the instance call).**

### `r089` — `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:5544`

> instance call `autoProcessing_CutIn.HasAwaitingActivateEffects()` replaced by private static helper HasAwaitingActivateEffects(autoProcessing_CutIn) because port AutoProcessing surface does not expose HasAwaitingActivateEffects

Same as `r084` — the port `AutoProcessing.HasAwaitingActivateEffects()` exists (`/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/AutoProcessing.cs:1366`).

### `r094` — `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:5707`

> instance call `autoProcessing_CutIn.HasAwaitingActivateEffects()` replaced by private static helper HasAwaitingActivateEffects(autoProcessing_CutIn) because port AutoProcessing surface does not expose HasAwaitingActivateEffects

Same as `r084` — the port `AutoProcessing.HasAwaitingActivateEffects()` exists (`/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/AutoProcessing.cs:1366`).

### `r102` — `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:2324`

> SkillInfo list/ctor `SkillInfo` → `CardEffectCommons.SkillInfo` (type relocated into CardEffectCommons)

**Not substrate-forced — namespace qualification only.** The mirror `SkillInfo` exists as its own 1:1 file (`/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/SkillInfo.cs:23`), exactly as AS-IS (`/home/hg/git/headlessDCGO/DCGO/Assets/Scripts/Script/SkillInfo.cs:3`); the `CardEffectCommons.` prefix is the C# namespace it was housed in (`...Script.CardEffectCommons`, chosen to sit with `ICardEffect`/`EffectTiming`). The file's own header claims it disambiguates from a substrate `Headless/Effects/SkillInfo.cs` record — **that file does not exist** (no `Headless/Effects/` directory). Should be classified as: **mirror namespace/housing note, no substrate cause.**

### `r110` — `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:2522`

> SkillInfo `SkillInfo` → `CardEffectCommons.SkillInfo` (type relocated)

Same as `r102` — namespace qualification, not a substrate gap.

### `r149` — `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:765`

> `!_player.CanReduceSecurity()` changed to `!SecurityRuleGateSeam.CanReduceSecurity(_context, _playerId)` (rule relocated to substrate seam)

**Not substrate-forced — mirror housing choice.** `SecurityRuleGateSeam` is not substrate; it is an `internal static class` inside the mirror file itself (`/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs:383`), and its body is documented as AS-IS `Player.CanReduceSecurity()` (`/home/hg/git/headlessDCGO/DCGO/Assets/Scripts/Script/Player.cs:1521-1529`) **verbatim**, reading `new GameContext(context).IsSecurityLooking`. Nothing stopped the mirror `Player` from carrying the method. Should be classified as: **member relocated out of the mirror `Player` (housing item)** — the only genuinely substrate-forced part (the `GameContext` roster/flag source) is already covered by S2.

### `r227` — `Permanent.cs:568`

> Level original re-asserts `if (!TopCard.HasLevel) Level = 1145140` no-level sentinel; port omits it, relying on mirror TopCard.Level whose sentinel is -1 (folded upstream in CardSource.Level/PrintedLevel)

**Not substrate-forced — mirror model choice.** AS-IS `Permanent.Level` re-asserts the `1145140` no-level sentinel when `!TopCard.HasLevel`; the port omits the re-assertion because the mirror `CardSource.Level`/`PrintedLevel` uses a different sentinel (`-1`) folded upstream. That is a *mirror* sentinel-model decision inside the ported card layer, not a substrate member gap. Should be classified as: **mirror sentinel-model divergence (verify no `== 1145140` reader survives).**

### `r289` — `src/HeadlessDCGO.Engine/Assets/Scripts/Script/Permanent.cs:2021`

> DigivolutionCardsColors folds mirror string colors through CardSource.ToCardColorList and drops the trailing redundant .Distinct() (both loops already dedupe via Contains, so result-equivalent)

**Not substrate-forced — gratuitous simplification.** Dropping the trailing `.Distinct()` in `DigivolutionCardsColors` is justified in the row itself as "result-equivalent" because both loops already dedupe via `Contains`. Result-equivalence is not a substrate constraint. Should be classified as: **AS-IS statement dropped as redundant (revert for 1:1, or record as an accepted deviation).**

---

## UNGROUPED

None. All 291 rows landed in a root or in MISCLASSIFIED.

