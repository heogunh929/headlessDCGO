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

(filled from group B report)

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

(filled from group D report)

## 5. Consumed-by-Tfx-only inventory

(filled from group reports)

## 6. End state

(final build result)
