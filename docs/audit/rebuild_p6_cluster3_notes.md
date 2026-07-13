# P6 dispatch-flip unmask remediation — cluster 3 (FINAL sweep): the remaining 113 build errors (2026-07-14)

Scope: everything left after clusters 1 (PlayCardClass/BlastDNA) and 2 (KeyWordEffects) — the stage-A error
inventory's tail: CardEffectFactory.cs monolith (38), CardSource.cs (14), the CardEffectCommons/CanUseEffects
hashtable-gate layer (~30), the kind-class-key engine gate (AttackTargetSwitchGate), real-card stragglers
(BT9_109 / AD1_025 / BT1_034 / BT2_029 / EX8_051 / EX8_061) and 2 Tfx fixtures. Bar: behavioral identity with
AS-IS; (a) adapt-to-mirror-shape with cited anchors / (b) small pure 1:1 port at the AS-IS path / (c) heavy
dependency → explicit NotSupportedException STOP + design item; NO silent stubs.

Result: **project-wide build errors 113 → 0** (see §6).

Work split: A = shared mirror surfaces (CardSource.cs / Permanent.cs / IBattle.cs / AttackTargetSwitchGate.cs,
this section) · B = factory layer · C = CardEffectCommons / CanUseEffects layer · D = real cards + Tfx.
Design items are numbered RD-P6C3-A#/B#/C#/D# by group.

## 1. Group A — the kind-class-key retirement (CardSource.cs 14 · Permanent.cs 1 · AttackTargetSwitchGate.cs 2 · IBattle.cs 3)

ROOT CAUSE for all the `*Key` CS0117s: the pre-flip mirror evaluated per-kind continuous effects by reading
REGISTRY BINDING VALUES under per-kind keys (`ChangeBaseCardColorClass.ChangeBaseCardColorsKey` etc., written
by the old classes' `ToBinding`). The P4 kind-class 1:1 rebuild replaced those classes with the AS-IS shapes
(plain `Func` state + interface methods, no `ToBinding`, no key constants) — so no producer writes those keys
any more and the registry folds are PROVABLY dead. Resolution (a): every consumer re-anchored to the AS-IS
mechanism itself — the live `EffectList(EffectTiming.None)` interface scan (the flip's enumeration model;
mirror `CardSource.EffectList` already covers dispatched per-card effect classes via
`CEntity_EffectControllerStore` → `CardEffectDispatch`).

Re-folded 1:1 (each cites its AS-IS anchor inline):

* `CardSource.BaseCardColors` / `CardColors` / `BaseDualCardColors` / `DualCardColors` (AS-IS
  CardSource.cs:364-401/:446-483 + dual variants) — the AS-IS branch structure preserved: effects of ITSELF
  apply only while `PermanentOfThisCard() == null` (mirror: `.IsEmpty`), then all field permanents
  (`Players_ForTurnPlayer` scan), `Distinct()`. Shared loop factored as `FoldColorEffects<TInterface>`.
* `CardSource.CardTraits` (:2581-2604) — self-only `IChangeTraitsEffect.ChangTraits` scan (no membership gate,
  no Distinct — AS-IS exact).
* `CardSource.BaseCardNames` **(new, AS-IS :1371-1436)** + `CardNames` rewritten over it (:1442-1460): the
  full AS-IS branch structure (digivolution-source → self `EffectList_ExceptAddedEffects` only; otherwise
  self-if-not-permanent + field permanents + PLAYER effects), then `IChangeCardNamesEffect` self folds,
  Distinct. The substrate `AddedCardNameKey` registry read is KEPT alongside (it is the still-live old-model
  `ChangeCardNamesClass` lowering produced by ContinuousAndRestrictionEffects.GrantAdditionalCardName).
* `CardSource.Level` (:941-975 TreatedLevel) — self `IChangeCardLevelEffect` scan; mirror -1 sentinel kept
  (AS-IS 1145140; all consumers guard on HasLevel).
* `CardSource.JogressLevelsAgainst` — now 1:1 with AS-IS `Permanent.Levels_ForJogress(CardSource)`
  (Permanent.cs:3554-3605): seed = MATERIAL PERMANENT's `Level` gated on `jogressCard.HasLevel` (the mirror's
  previous self-HasLevel/card-Level seed was an approximation — fixed), then `IAddJogressLevelsEffect.
  GetJogressLevels(jogressCard, material)` over all field permanents' + players' effects.
* `CardSource.LinkConditionOf` / `AppFusionConditionOf` / `AssemblyConditionOf` (AS-IS linkCondition :2727 /
  appFusionCondition :3005 / assemblyCondition :3043) — the pre-flip "dispatch-first + registry-fallback"
  split replaced by the single AS-IS `EffectList(EffectTiming.None)` scan with `CanUse(null)` (this also
  resolved the three `CanUse()` missing-argument errors — AS-IS calls `CanUse(null)`).
* `Permanent.Level` (AS-IS Permanent.cs:48-102) — `IChangePermanentLevelEffect` scan over all field
  permanents' + players' effects (was a registry-key fold).
* `AttackTargetSwitchGate.IsLocked` (stage-B preview per brief) — re-anchored to AS-IS
  `Permanent.CanSwitchAttackTarget` (Permanent.cs:3745-3792): `ICanNotSwitchAttackTargetEffect` scan,
  `CanUse(null)`-gated, `CanNotBeSwitchAttackTarget(attacker)`; IsLocked == !CanSwitchAttackTarget. NOTE: the
  old gate's explicit `EffectInvalidation.IsEffectsDisabled` pre-check is dropped — AS-IS CanSwitchAttackTarget
  carries no such check; invalidation flows where AS-IS flows it (inside the effect gates).
* Retired dead private helpers `FoldListTransforms` / `SelfTransforms` (registry-key folds with no producers).
  `EffectConditionPasses` kept (still consumed by CanNotPlayOptionScan / OptionColorRequirement).

### COLOR-MODEL-DUALITY reconciliation (design item closed for the fold layer)

The kind-class interfaces transform `List<CardColor>` (AS-IS); the mirror accessors keep their established
STRING signatures (consumer corpus: OptionColorRequirement, BT2_099, colour predicates). Bridged by two public
statics on CardSource — `ToCardColorList(IEnumerable<string>)` / `ToColorNames(IEnumerable<CardColor>)` —
lossless over the closed enum (string values are exactly the enum names); an unparseable fixture string is
dropped from the enum view, never guessed. Groups B/C use the same helpers (AddDigivolutionRequirement enum
compare; HashtableSetting's "CardColors" payload stays AS-IS-typed `List<CardColor>` for the verbatim
OnDeletion reader).

### Group A additive AS-IS-surface members (contracts consumed by groups B/C/D)

* `Permanent.IsDestroyedByBattle { get; set; }` (AS-IS Permanent.cs:3666) — carrier = the instance
  `deletedByBattle` metadata flag the live battle pipeline ALREADY stamps (BattleResolver.DeletedByBattleKey):
  gates see exactly the live pipeline's answer; the Hashtable builders' verbatim `{ IsDestroyedByBattle =
  true }` writes land on the same shared flag.
* `Permanent.CanSuspend` (AS-IS :3698-3742) — `ICanNotSuspendEffect` scan (field permanents + players,
  `gameContext.Players` seat order), 1:1.
* `Permanent.HasIceclad` (AS-IS :2540-2582) — `IIcecladEffect` scan; AS-IS gates with `CanTrigger(null)` (not
  CanUse) — preserved, including the verbatim per-player re-scan quirk of the permanent's own list.
* `CardSource.IsBeingRevealed { get; set; }` (AS-IS CardSource.cs:3565) — instance-metadata carrier
  (`isBeingRevealed`, the IsSuspended setter pattern). **Design item RD-P6C3-A2**: the AS-IS reveal-pipeline
  WRITERS are unported — until that slice lands the flag is its default false (== a card not mid-reveal).
* `CardSource.PermanentJustBeforeRemoveField { get; set; }` (AS-IS :3571) — per-match service store keyed by
  InstanceId (oldIsTapped_playCard pattern). **Design item RD-P6C3-A3**: the AS-IS WRITER (CardController
  stamps it just before RemoveFromAllArea) belongs to the unported CardController deletion slice; until it
  lands the property is null (== a card that never left the field). The OnDeletion Hashtable gates that read
  it are therefore compile-complete but inert until the writer lands.
* `CardSource.HasSaveText` (AS-IS :2181 = `HasText("<Save>")`, a printed-TEXT scan) — the mirror carries no
  rules text; carrier = instance `hasSave` metadata OR live Save keyword grant, exactly the pair
  `DeletionReplacementGate.TrySaveAsync` gates on (documented reduction: text-scan → keyword-carrier).
* **Design item RD-P6C3-A1**: AS-IS BaseCardNames' dual-card branch (`!isPermanent && IsDualCard` adds
  `dualEffect`, the second printed name) has no mirror data carrier yet — lands with dual-card definition data.
* `IBattle.CompareStats` — `Mathf.Clamp` → `Math.Clamp` (established substitute, identical int semantics).

### Group A findings for the coordinator

* Registry-registered per-kind effects no longer participate in the folds — any test fixture that registers a
  kind-class effect as a REGISTRY BINDING (instead of on the card's effect list / dispatch) will stop
  influencing colours/levels/jogress-levels: e.g. tests/PRIM.JogressLevels.Tests registers via the factory's
  old lowering. Same finding class as cluster 1's G9-048 note — runtime test re-anchoring is the next phase,
  not a build item.
* `CardSource.CardNames` now applies AS-IS `Distinct()` (it previously did not) and BaseCardNames now folds
  FIELD + PLAYER ChangeBaseCardName effects (previously registry-only) — strictly closer to AS-IS.
* RD-P6C1-9 relocation (CardSourceAsIsPlayAccessors → CardSource.cs instance members) NOT performed: the 14
  CardSource errors were resolvable without it, and moving the accessors mid-cluster while three sibling
  groups compile against the extension surface would be needless churn. Still open as RD-P6C1-9.

## 2. Group B — factory layer (CardEffectFactory.cs monolith + partials + CardEffects/*)

Picked up at 54 remaining project-wide errors (Groups A/C above + clusters 1/2 already landed; a prior partial
run had brought the 113-error baseline down to 54: monolith 38, BT9_109.cs 5, and 11 singleton/kind-class
files). Final state: **0 errors project-wide** (see §6). Singletons first (quick wins), then the monolith.

### Singletons / small kind-class files (11 errors → 0)

* **`Script/PermanentEffectFactory.cs`** (1) — `CanNotSwitchAttackTargetEffect`'s inline `CanUseCondition` was a
  0-arg lambda where `SetUpICardEffect` needs `Func<Hashtable,bool>` (ICardEffect.cs:55); added the `Hashtable`
  parameter (unused), the established idiom every other `SetUpICardEffect` call site in the corpus uses.
* **`CardEffects/ChangeSAttackClass.cs` / `ChangeLinkMaxClass.cs`** (1 each) — `Mathf.Abs` → `Math.Abs` (AS-IS
  uses Unity `Mathf`; identical int semantics, the task brief's own suggested substitute — no Mathf shim
  needed, this is the only two-site use in these files).
* **`CardEffectFactory/ChangeCardDP.cs`** (1) — AS-IS `GManager.instance.attackProcess.SecurityDigimon ==
  cardSource` (a `CardSource` identity compare); the mirror `AttackProcess.SecurityDigimon` is a
  `HeadlessEntityId?` (documented divergence already noted in the file's own header) → compares against
  `cardSource.InstanceId` instead (the established `CardSource.Equals` identity idiom). Updated the header
  comment to record the resolution (was flagging the divergence as still-masked).
* **`CardEffectFactory/ChangeSAttack.cs`** (1) — `card.CardID` → `card.CardNumber` (the cluster-2 §1 established
  substitute — `CardSource` has no `CardID` member).
* **`Script/CardEffects/ChangeCostClass.cs`** (1) + **`Script/Player.cs`** (additive) — AS-IS
  `cardSource.Owner.CanReduceCost(targetPermanents, cardSource)` (Player.cs:1329-1368): a clean, small,
  portable `ICannotReduceCostEffect` veto scan (`Players_ForTurnPlayer`'s field permanents' + own player-scope
  effect lists, `CanUse(null)`-gated) — SAME shape as the already-ported `CanAddSecurity`/`CanReduceSecurity`
  siblings on `Player`, so ported 1:1 onto `Player.CanReduceCost` rather than STOPped (retires design item
  MIG5-CANREDUCECOST, which Player.cs's own header had flagged as deliberately-not-stubbed for exactly this
  reason: "a compile-error is a better card-port signal... until now, when the compile error IS the port
  signal"). Call site bridged via `new Player(cardSource.Context, cardSource.Owner).CanReduceCost(...)`.
* **`CardEffectFactory/AddDigivolutionRequirement.cs`** (4) — same treatment: `Player.CanIgnoreDigivolutionRequirement`
  ported 1:1 (AS-IS Player.cs:1422-1465, identical veto-scan shape, `ICannotIgnoreDigivolutionConditionEffect`),
  called via the same `new Player(...)` bridge at its 3 call sites; the 4th error
  (`permanent.TopCard.CardColors.Contains(cardColor)`, `IReadOnlyList<string>` vs `CardColor` enum) bridged via
  the Group A `CardSource.ToCardColorList` helper (§1 COLOR-MODEL-DUALITY).
* **`CardEffectFactory/AddAppfusionMethod.cs`** (1) — AS-IS `if (permanent.LinkedCards.Find(...))` relies on
  Unity `MonoBehaviour`'s implicit-bool null check (AS-IS `CardSource : MonoBehaviour`); the mirror `CardSource`
  is a plain class, so `!= null` is the faithful adaptation (resolution (a), cited inline).

### Monolith `Script/CardEffectFactory.cs` (38 errors → 0)

All 38 resolved WITHOUT deleting any method — every one is an AS-IS-named factory function real or future card
ports read 1:1 against (per the file's own doc comment: "Method names match the original"). Breakdown:

* **Adapt (a) — 24 errors, no behavior loss:**
  - `ChangeBaseCardNameStaticEffect` — `SetUpChangeBaseCardNameClass` (typo, doesn't exist) →
    `SetUpChangeBaseCardNamesClass` (the actual member); `condition` threaded through a `Hashtable`-taking
    `CanUseCondition` wrapper (same 0-arg-lambda pattern as PermanentEffectFactory.cs above).
  - `PlayMindLinkTamerFromDigivolutionCards` — `selectedPermanent.DigivolutionCards` (`IReadOnlyList<CardSource>`)
    → `.ToList()` for the `SelectCardEffect.SetUp` 16-param overload's `customRootCardList: List<CardSource>?`.
  - `ActivateMainOptionSecurityEffect` — `CardEffectCommons.OptionMainEffect(card)` had no mirror member at
    all despite 17 REAL card consumers (BT1_094/095/101/102/107/111, BT2_091/097/102/110, ST1_15/16, ST2_15/16,
    ST3_16, ST4_15/16) — this is the one case in the monolith pass that needed a genuine small port, not just
    an adaptation; see "additive" below.
  - `PlaySelfTamerSecurityEffect` (13+ real consumers) — AS-IS `card.Owner.ExecutingCards.Contains(card)` (the
    Player mutable-list AS-IS field) re-expressed as `CardEffectCommons.IsExistOnExecutingArea(card)`, the
    mirror's own established zone-membership idiom (already used one function over, in
    `PlaySelfDigimonAfterBattleSecurityEffect.CanActivateCondition` — this file's cross-function precedent).
  - `AddJogressLevelsEffect` (zero card consumers, mirror-invented convenience wrapper, no AS-IS factory
    equivalent) — its `getLevels` parameter was the WRONG shape for the underlying `AddJogressLevelsClass`
    kind-class (`Func<CardSource,IReadOnlyList<int>>` vs the class's real
    `Func<CardSource,Permanent,List<int>>`, AS-IS `IAddJogressLevelsEffect.GetJogressLevels(jogressCard,
    material)`); widened to the correct 2-arg shape (free to do so — zero consumers lock in nothing) + the same
    `Hashtable`-wrapper condition fix.
  - `PlaceToSecurityEffect.CanResolveCondition` — `optionCard.Owner.CanAddSecurity(placeToSecurityEffect)` (an
    `ICardEffect` arg where `Player.CanAddSecurity` takes `HeadlessEntityId?`) → bridged via
    `new Player(optionCard.Context, optionCard.Owner).CanAddSecurity(placeToSecurityEffect.EffectSourceCard?.InstanceId)`.
  - Remaining ~15: `Hashtable`-wrapper condition fixes and `new Player(card.Context, card.Owner).X` bridges,
    same shape as clusters 1/2's established idioms (no new patterns).

* **Additive small ports (b) — 2 helpers, both genuinely consumed:**
  - **`CardSource.CardNames_DigiXros`** (new, `CardSource.cs`, AS-IS :2193-2207) — same shape as the
    already-ported `CardTraits` (self-only `IChangeCardNamesForDigiXrosEffect` scan over
    `EffectList(EffectTiming.None)`, `CanUse(null)`-gated, no re-Distinct after the fold). Needed by
    `GetDigiXrosConditionsFromNames` below (3 real consumers: AD1_025/BT16_025/TfxDigiXros).
  - **`CardEffectCommons/DigiXrosEffects.cs`** (was a 0-type skeleton) — 1:1 port of the AS-IS file's one
    member, `GetDigiXrosConditionsFromNames` (material-slot-per-name via `CardNames_DigiXros` membership on a
    same-owner Digimon).
  - **`CardEffectCommons/DNADigivolveEffects.cs`** (additive region) — 1:1 port of AS-IS `GetJogressConditions`
    (DNADigivolveEffects.cs:630-649), kept VERBATIM including the AS-IS quirk that
    `PermanentCondition`/`FullPermanentCondition{1,2}` local helpers are declared but never referenced by the
    returned `JogressCondition` (dead code in the original — 3 real consumers: AD1_025/BT16_025/TfxDigiXros).
  - **`CardEffectCommons/GameContextDeterminarion.cs`** (was a 0-type skeleton) — the AS-IS
    `IsExistOnBattleAreaDigimonTrigger`/`IsExistOnBattleAreaDigimonActivate` pair (:49/:95), same collapse the
    monolith's own `IsExistOnBattleAreaTrigger`/`Activate` already use (headless permanent identity IS the
    instance id, so the AS-IS trigger-time-cache/activate-time-recheck pair collapses to one live check) —
    gated on `IsExistOnBattleAreaDigimon` instead. Consumed by `WhenLinkingClass` (Digimon-only timing).
  - **`Script/CardEffectCommons/OptionMainEffect.cs`** (new sibling file) — 1:1 port of AS-IS
    `CardEffectCommons.OptionMainEffect(card)` (CardEffectCommons.cs:711): the resolved `[Main]`-tagged
    `ActivateClass` among the card's OptionSkill-timing effects. All additions land in NEW sibling
    partial-class files (never in `CardEffectCommons.cs` itself — the standing rebuild_p5_gates_missing.md
    prohibition, same convention cluster 2 established).

* **STOP (c) — 3 functions, all zero-consumer + heavy/multi-axis substrate gaps:**
  - **`ReplaceBottomSecurityWithFaceUpOptionEffect`** / **`ReplaceTopSecurityWithFaceUpOptionEffect`** (AS-IS
    CardEffectFactory.cs:645/:684) — zero consumers anywhere in the mirror corpus (no real card, no Tfx; grep
    confirmed). STOP citing **design item RD-P6C3-B1**: needs `ContinuousController.instance` (a 0-type
    skeleton, no `.instance` at all — CS0103, not even CS0117), `CardObjectController.AddHandCards`/
    `AddSecurityCard` (the pre-existing RD-P6C1-8/RD-P6C2-1 zone-move-statics gap), and
    `GManager.GetComponent<Effects>().CreateRecoveryEffect` (`Effects` is a 0-type skeleton — AS-IS UI/VFX
    component per cluster-1 §4 precedent, never modeled headless-side). Kept as the AS-IS-named entry point
    (not deleted) per the file's method-name-fidelity contract, in case a future card port needs this exact
    text — the equivalent live behavior for currently-witnessed cards already exists independently
    (`ReturnTopSecurityToHandThenUnsuspendSelfBody`, BT9_043's OnEndAttack body, `ActivatedEffect.cs`).
  - **`PlaySelfDigimonAfterBattleSecurityEffect`** (AS-IS CardEffectFactory.cs:285) — zero consumers. STOP
    citing **design item RD-P6C3-B2**: the AS-IS body nests a SECOND delayed grant
    (`card.Owner.UntilEndBattleEffects.Add(...)` — the AS-IS `Player` mutable `EffectTiming.OnEndBattle`
    bucket, part of the unlanded player-grant store, P6A-PLAYER-EFFECTLIST) and conditionally a THIRD
    (`playedDigimon.UntilOpponentTurnEndEffects.Add(...)`, same gap on `Permanent`) plus `DestroyPermanentsClass`
    (a batch-delete helper with no mirror). Only the outer `ActivateCoroutine` local function was replaced by
    the throw; `CanUseCondition`/`CanActivateCondition` (both real, no missing members) are untouched, so the
    declaration-time legality gate still evaluates correctly — only resolution STOPs.
  - **`PlaceToSecurityEffect.ResolutionCoroutine`** — same `CardObjectController.AddSecurityCard` gap as above
    (RD-P6C3-B1); `CanResolveCondition` (the real gate half) was fixed via the `Player` bridge instead of
    STOPped, since it had a clean adaptation available.

## 3. Group C — CardEffectCommons / CanUseEffects layer (~30 errors → 0)

* **CanUseEffectHelpers.cs (6)** — root cause: the P6 AS-IS-mirror `SkillInfo` (Script/SkillInfo.cs, same
  namespace) SHADOWS the substrate record `Headless.Effects.SkillInfo` this pre-existing substrate helper was
  written against. A same-name alias is CS0576-illegal in the enclosing namespace, so:
  `using SubstrateSkillInfo = HeadlessDCGO.Engine.Headless.Effects.SkillInfo;` + the two type-reference sites
  (ctor param, property type). `CanUseEffectRequest.SkillInfo` keeps its NAME — consumer surface unchanged,
  zero behavior change.
* **ToBinding wave (6 sites: CardEffectCommons.cs AddEffectToPermanent / AddSelfRemovalEffectToPermanent /
  AddEffectToPlayer / AddContinuousEffectToPlayer; ActivatedEffect.cs GrantContinuousBody.Apply;
  ContinuousAndRestrictionEffects.cs PlayerScopeTriggerGrantEffect.ToBinding)** — the rebuild removed
  `ToBinding` from the ICardEffect contract; each site lowered through
  `LegacyBindingBridge.TryToBinding(cardEffect, id, out binding)` (byte-identical for old-model effects —
  today's ENTIRE caller set); the false branch (a NEW-model effect reaching a grant path — no new-model grant
  store yet, = stage-A P6A-PERMANENT-EFFECTLIST-ADDED) throws NotSupportedException — **design item
  RD-P6C3-C1**. All retarget/SurviveOwnLeave/DelayedOneShot/re-source logic untouched. No other monolith
  edits (rebuild_p5_gates_missing.md prohibition respected).
* **HashtableSetting.cs (8)** — side-by-side with AS-IS: `gameContext.ActiveCardList` → new mirror member
  (below) + the unmasked `Owner.Enemy` cascade adapted via the established `new Player(context, owner).Enemy`
  bridge; `new Permanent(permanent.cardSources)` ×2 → `new Permanent(context, permanent.InstanceId,
  permanent.OwnerId)` (cluster-2 §1 convention); `.Clone()` on IReadOnlyList ×3 → `.ToList()` (Clone IS a
  shallow copy), with the "CardColors" payload built via `CardSource.ToCardColorList` so it stays the
  AS-IS-typed `List<CardColor>` the verbatim OnDeletion reader casts to; `IsDestroyedByBattle = true` /
  `HasSaveText` compile against the Group A contract members.
* **GameContext.ActiveCardList (additive)** — AS-IS GameContext.cs:31 (every live card): computed over
  `CardInstanceRepository.Snapshot()`, records → mirror CardSource views.
* **CanUseEffects/OnDeletion.cs (7) / CanSuspend.cs (2) / WhenDiscardLibrary.cs / WhenDeleteOpponentDigimon
  (ByBattle).cs** — all resolved by the Group A contract members; no edits needed.

## 4. Group D — real cards + Tfx fixtures

**`CardEffect/BT9/White/BT9_109.cs`** (5 errors → 0) — the sole real-card straggler, a C-3 WITNESS card whose
own file header already documented these exact gaps (§"UNRESOLVED MEMBERS", written by the prior bridge-W5
pass) as pre-flagged STOP territory, matching the task brief's own prediction ("heavy — frame model / hand-select
subsystem"). Both STOPs are scoped to the smallest possible unit so the surrounding `[When Attacking]` timing
block's real gates stay live:

* **`CanSelectCardCondition`** (design item **RD-P6C3-D1**, cites the pre-existing MIG5-FRAME-MODEL gap) — AS-IS
  `cardSource.CanPlayCardTargetFrame(card.PermanentOfThisCard().PermanentFrame, true, activateClass, root:
  SelectCardEffect.Root.Hand)`: `CanPlayCardTargetFrame` is declared nowhere on the mirror `CardSource`
  (`PlayCardClass.cs` has the same masked-verbatim reference, per cluster-1 §3 RD-P6C1-1) and `PermanentFrame`
  has no mirror (no frame/slot model — headless zones are lists, not a slot array). The throw sits BEHIND the
  real AS-IS pre-conditions (`HasXAntibodyTraits && IsDigimon && IsExistOnBattleArea`), so a non-matching hand
  card still correctly returns `false` without touching the missing members — only a genuine candidate trips
  the STOP.
* **`ActivateCoroutine`'s select-block** (design item **RD-P6C3-D2**) — AS-IS constructs
  `GManager.instance.GetComponent<SelectHandEffect>()`: `Script/SelectHandEffect.cs` is a 0-type skeleton (no
  hand-select subsystem exists on the mirror at all, unlike `SelectCardEffect`/`SelectPermanentEffect` which are
  fully ported). The whole AS-IS select-1-hand-card-then-`PlayCardClass` block is kept as an AS-IS-named
  COMMENT (not deleted) for the eventual hand-select-subsystem port, gated behind the same
  `HandCards.Count(CanSelectCardCondition) >= 1` pre-check AS-IS uses (so it only throws when there is an
  actual selectable hand card — an empty/no-match hand is a correct silent no-op, matching AS-IS' own
  `if (...>= 1)` guard).
* Everything else in the file (`[None]` IgnoreColorConditionClass, `[Security]` memory+hand, `[Main]`
  tuck-under-Digimon via `SelectPermanentEffect`, `[None]` `CanNotTrashFromDigivolutionCardsClass` — the C-3
  witnessed half) is untouched and was already compiling; only the two `[When Attacking]` STOP-territory
  members changed.

No Tfx fixture required changes this pass — the stage-A inventory's Tfx entries were already resolved by
clusters 1/2 or by the Group A/C contract members landing.

## 5. Consumed-by-Tfx-only inventory

None found in this cluster's remaining 54-error slice — every symbol touched (Group A/B/C members, the
monolith's 38, BT9_109.cs's 5) traced to either a REAL card consumer (documented per-function above) or ZERO
consumers project-wide (also documented per-function; those became STOPs, never deletions — see §2's STOP
list). No case required the "Tfx-only, so adapt-cheaply-and-move-on" middle path this pass.

## 6. End state

`dotnet build src/HeadlessDCGO.Engine/HeadlessDCGO.Engine.csproj` → **0 errors, 0 warnings** (was 54 at this
cluster's start — a prior partial run had already brought the stage-A/cluster-1/cluster-2 baseline of 402 down
to 113, then to 54; Groups A and C above closed the rest of that gap before this session; this session closed
Group B's 11 singletons + 38 monolith errors + Group D's BT9_109.cs 5, netting the final 54 → 0). No method was
deleted; every zero-consumer function became a STOP instead (RD-P6C3-B1/B2, RD-P6C3-D1/D2), preserving the
AS-IS-named surface for future card ports per the file's own method-name-fidelity contract.

### Finding for the coordinator (not a build item — runtime test re-anchoring, same class as cluster-1's G9-048)

`tests/PRIM.JogressLevels.Tests/Program.cs:68` declares a local `RegisterJogressLevels(..., Func<CardSource,
IReadOnlyList<int>> getLevels)` that calls `CardEffectFactory.AddJogressLevelsEffect(card, getLevels)`. Fixing
that factory's WRONG parameter shape (§2, `Func<CardSource,IReadOnlyList<int>>` → the AS-IS-correct
`Func<CardSource,Permanent,List<int>>`, zero real-card consumers so free to correct) means this test's local
function signature now needs updating to match — it will fail to build once the test suite itself is next run.
Not fixed here (out of this pass's declared scope, `src/HeadlessDCGO.Engine` only, and the ENTIRE test suite
was already unbuildable against the pre-fix 54/113/402-error Engine, so this is not a regression this pass
introduced against a previously-green suite). `tests/G9-031.LinkSecurity.Tests/Program.cs:74` and
`tests/FAILa-10.PlayAfterBattle.Tests/Program.cs:37` both cast `CardEffectFactory.PlaySelfDigimonAfterBattleSecurityEffect(...)`
to `(PlaySelfAtEndOfBattleSecurityEffect)` — that cast target was already stale before this cluster (the P4
rewrite replaced the old mirror-invented implementation the file's own header says it replaces); the function
still returns a plain `ActivateClass`, so this cast was already an invalid-cast waiting to happen independent
of this session's STOP.
