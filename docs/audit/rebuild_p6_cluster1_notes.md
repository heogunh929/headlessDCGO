# P6 unmask remediation — cluster 1: PlayCardClass.cs + BlastDNADigivolution.cs (2026-07-14)

Scope: the two biggest post-flip mirror-layer error files (PlayCardClass.cs 121 + BlastDNADigivolution.cs 48
of the 402-error stage-A inventory, docs/audit/rebuild_p6_stageA_notes.md §6). Both were ported VERBATIM while
declaration-masking hid body diagnostics; this pass resolves every unresolved member per the (a) adapt /
(b) small-pure port / (c) STOP triage, behavioral identity with AS-IS as the bar. Result: **0 errors in both
files** (project total at end of this pass: 132 unique — the remainder belongs to the other clusters:
CardEffectFactory.cs monolith 38, CardSource.cs 14, HashtableSetting/CanUseEffects/KeyWordEffects others,
real-card backlog).

## 1. (a) Adapted — the member exists under a mirror shape (call sites cite each anchor inline)

* `card.Owner.<Player property>` (HandCards ×6, LibraryCards, SecurityCards, TrashCards, GetFieldPermanents ×2,
  MaxMemoryCost ×2) → `new Player(card.Context, card.Owner).<property>` — the established BT2_023 route (a bare
  `HeadlessPlayerId` cannot carry an extension property).
* `card.Owner.GetBattleAreaPermanents()` (BlastDNA head guard) → new `PlayerIdAsIsExtensions.
  GetBattleAreaPermanents` (2-line sibling of the existing `GetBattleAreaDigimons` ambient-context bridge).
* `card.Owner.AddMemory(-1 * Cost, null)` — already-existing W4 extension, kept verbatim.
* `card.appFusionCondition` → the EXISTING mirror `CardSource.AppFusionConditionOf()`.
* `card.BasePlayCostFromEntity` (AS-IS CardSource.cs:757 = `_cEntity_Base.PlayCost`) → extension
  `BasePlayCostFromEntity()` delegating to the mirror `CardSource.GetCostItself` (`Definition?.PlayCost ?? 0` —
  exactly the raw printed cost).
* `card.Owner.UntilCalculateFixedCostEffect = new List<…>()` (CardController.cs:851) →
  `EffectDurationExpiry.ExpireFixedCostCalc(card.Context.EffectRegistry)` — the mirror carrier of the AS-IS
  per-player bucket is the `EffectDuration.UntilCalculateFixedCost` binding set; PlayCardAction.cs:169 performs
  the same clear at the same AS-IS anchor.
* BlastDNA `SelectPermanentEffect.SetUp(canTargetCondition:)` — W4 takes `Func<HeadlessEntityId,bool>`; added
  the `CanSelectPermanentById` id adapter (BT2_097 idiom) over the verbatim AS-IS Permanent predicate.
* `AutoProcessing.GetSkillInfos / ActivateBackgroundEffects / StackSkillInfos / PutStackedSkill` — already
  stage-A members, consumed as-is.

## 2. (b) Small 1:1 ports added at their AS-IS paths

* **`Script/SelectDNACondition.cs`** (was a 0-type skeleton) — FULL 1:1 port of the 90-line AS-IS component
  (SetUp/ResetSelectDNAConditionClass/`_selectedCount`/Activate). Activate rides the already-mirrored
  `UserSelectionManager.SetIntSelection → WaitForEndSelect → SelectedIntValue`; commandText close = UI strip;
  AS-IS dead inner `Count == 1` arm kept verbatim. `_targetDNA.jogressCondition` → `JogressConditionOf()`.
* **`Script/SelectDigiXrosClass.cs`** (was a 0-type skeleton) — the AS-IS STATE surface 1:1
  (selectedDigicrossCards/addDigivolutionCardInfos/excludedCards/playCard, ResetSelectDigiXrosClass,
  AddDigivolutionCardInfos, SetExcludedCards, the `AddDigivolutionCardsInfo` holder :1033-1048). The
  interactive `Select(card)` flow (:368-880) = STOP RD-P6C1-5 (below).
* **`Script/SelectCountEffect.cs`** — ADDITIVE AS-IS component surface next to the MIG7 deterministic members
  (which keep their consumers/tests): the 7-param `SetUp(SelectPlayer, targetPermanent, MaxCount, CanNoSelect,
  Message, Message_Enemy, SelectCountCoroutine)`, `SetCandidates`, `SetPreferMin`, `SetIsDigivolutionCost`,
  and `Task Activate()` — AS-IS SelectCountEffect.cs:60-186 with verbatim candidate construction
  (0..MaxCount skipping 0 unless CanNoSelect → SetCandidates override → Distinct+ascending), single-candidate
  auto-resolve, and ONE ChoiceProvider count request (`CreateCountRequest(candidateCounts:)`) replacing the
  AS-IS owner-buttons/auto-min/AI presentation split (the provider is the decider; preferMin/isDigivolutionCost
  kept as state). Registered in `GManager.GetComponent<T>` with AttachContext.
* **`Script/AutoProcessing.cs`** — `GetSkillInfosOfCards` (AS-IS :993-1021, verbatim scan with the established
  ResolvePermanentOfThisCard bridge), `HasExecutedSameEffect` (AS-IS :624-627 verbatim), `skipSkillInfos`
  (AS-IS :18), `TriggeredSkillProcess` (AS-IS :572-600 — empty-stack fast path exact; non-empty drain = STOP
  RD-P6C1-3), and `ForCutIn(context)` (a SECOND context-cached instance under its own service key).
* **`Script/GManager.cs`** — `autoProcessing_CutIn` (AS-IS GManager.cs:112, the second AutoProcessing
  component → `AutoProcessing.ForCutIn`); `GetComponent<T>` grew SelectCountEffect / SelectDigiXrosClass /
  SelectDNACondition.
* **`Script/Player.cs`** — `LibraryCards` (AS-IS Player.cs:514, same zone-read shape as HandCards);
  `MaxMemoryCost` (AS-IS :1127-1146 — both seat branches reduce to `|MemoryForPlayer + 10|` under the verified
  AddMemory sign mapping); `PlayerIdAsIsExtensions.GetBattleAreaPermanents`.
* **`Script/Permanent.cs`** — `IsSuspended` SETTER (AS-IS Permanent.cs:1956 is a public FIELD; the setter is
  the raw metadata write — deliberately no gate / no OnTapped emission, matching the AS-IS direct assignment
  used by the failed-play restore); `oldIsTapped_playCard` (AS-IS :45 — the pre-play suspension snapshot, also
  read by real cards BT8_102/RB1_023/BT16_046; per-match store keyed by InstanceId since the mirror Permanent
  is a transient view).
* **`CardEffectCommons/CardPortingFramework.cs`** — `BlastDNACondition` record extended ADDITIVELY with the
  AS-IS shape (`Name`, mutable `Permanents`/`CardSources`, `(string name)` ctor). Existing consumers
  (Matches/Label/ByName) untouched; observable equality unchanged (ByName instances were already unequal via
  the per-instance lambda).
* **`Script/PlayCardClass.cs` bottom — `CardSourceAsIsPlayAccessors`** (relocation design item RD-P6C1-9,
  because the AS-IS home CardSource.cs is cluster-3's file): REAL 1:1 accessors over the live
  `EffectList(EffectTiming.None)` scan (the AppFusionConditionOf-established shape) —
  `JogressConditionOf()` (AS-IS :2707), `BurstDigivolutionConditionOf()` (:2987), `DigiXrosConditionOf()`
  (:2959), `HasDigiXros()` (:2569), `IsPermanent()` (:3488 → CEntity_Base.cs:238 Digimon|Tamer|DigiEgg),
  `BasePlayCostFromEntity()` (:757).
* `DataBase.BlastDNADigivolveEffectDiscription` — already landed by cluster 2 (no action).

## 3. (c) STOPs — explicit throws, design items (each call site keeps the AS-IS text as comments)

* **RD-P6C1-1 — field-frame model** (`Player.fieldCardFrames` / `FieldCardFrame.GetFramePermanent` /
  `Permanent.PermanentFrame` / `CardSource.PreferredFrame` — the MIG5-FRAME-MODEL gap; headless zones are
  lists, no slot model): PlayCardClass `SetBurst`/`BurstTamer`/`IsAppFusion`/`LinkedCard`/jogress target
  resolution/`CanPlayCardTargetFrame` site; BlastDNA SelectCardCoroutine (commented). The not-set fallthroughs
  (`_burstTamerFrameID < 0`, `_appFusionFrameIDs == null`) are kept live, so IsBurst/IsAppFusion correctly
  return false on ordinary plays without touching frames.
* **RD-P6C1-2 — play/digivolution cost+requirement engine** (the MIG5 PLAY-COST gap: AS-IS `EvoCosts`/
  `GetChangedCostItselef`/`GetChangedPayingCost`/requirement scans): STOP extensions `CanEvolve`, `CostList`,
  `GetPayingCostWithBaseCost`, `CanJogressFromTargetPermanents`, `CanBurstDigivolutionFromTargetPermanent`,
  `CanAppFusionFromTargetPermanent` (call-site text verbatim); BlastDNA `CanPlayJogress` (commented region).
  **Consequence: every PayCost=true or digivolution-target run of the mirror `PlayCard()` STOPs at cost
  fixing/evolution determination** until the cost engine is ported — honest by design; the mirror
  PlayCardClass is a foundation seam, not the live play path (PlayCardAction is).
* **RD-P6C1-3 — cut-in drain**: `AutoProcessing.TriggeredSkillProcess` STOPs only when the stack actually
  holds skills (AS-IS `availableMultipleSkills.ActivateMultipleSkills` needs the unported MultipleSkills
  window; == stage-A P6A-STACKED-DRAIN). The unconditional AfterPayCost drain of a play with no collected
  cut-in effect is an exact no-op.
* **RD-P6C1-4 — `PlayPermanentClass`/`UseOptionClass`**: the final hand-off STOPs (siblings unported;
  re-entering PlayCardsBridge would double-pay the already-paid cost). **Every complete `PlayCard()` run ends
  at this STOP today.**
* **RD-P6C1-5 — Assembly/DigiXros interactive pre-play selection**: `SelectDigiXrosClass.Select` STOPs inside
  the new state mirror; the Assembly component `SetExcludedCards+Select` STOPs at the call site and the two
  `ResetSelectAssemblyClass()` calls are stripped WITH anchors (the mirror `SelectAssemblyClass` is the STATIC
  feasibility half — there is no component state to reset headless-side).
* **RD-P6C1-6 — `GManager.selectBurstDigivolutionEffect` / `selectAppFusionEffect`** (345/241-line components;
  the tamer bounce / link re-source are game state): call-site STOPs, unreachable today (RD-P6C1-1 gates them).
* **RD-P6C1-7 — `SelectHandEffect`** (942-line Select* component, no mirror): BlastDNA hand-material pick STOP.
* **RD-P6C1-8 — `CardObjectController` zone-move statics** (RemoveFromAllArea/AddHandCards/AddTrashCard here;
  CreateNewPermanent/AddHandCard in the BlastDNA commented region) — same gap cluster 2 logged as RD-P6C2-1.
  RESOLVED (수리 배치 5): the `CardObjectController` mirror type now exists with all these statics (RemoveFromAllArea
  R3-A, AddTrashCard R3-A, AddHandCards/AddHandCard R6-P/RD-R6-03, CreateNewPermanent R4-S3b). The failed-play
  restore call sites in `CardController.PlayCard` (was CardController.cs:3577/3606 STOP) now re-point to the 1:1
  AS-IS calls (AS-IS CardController.cs:913/915/953) — L4-001.MatchEventLog honest-red flipped green. RESIDUAL: the
  BlastDNA hand-material STOP (BlastDNADigivolution.cs) stays STOP — blocked ALSO on RD-P6C1-7/-1/-2 (SelectHandEffect
  / field-frame model / CanPlayJogress), not solely on the zone-move statics.
* **RD-P6C1-9 — relocation**: `CardSourceAsIsPlayAccessors` members belong in the mirror `CardSource` once
  CardSource.cs is free. (Instance members will silently win over the extensions when they land — remove the
  bridge then.)

## 4. UI strips (adaptation (4), each cited inline at its AS-IS anchor)

Effects.RemoveDigivolveRootEffect (:444, Effects.cs:2162 = DOTween only) · Effects.MoveToExecuteCardEffect
(:529/:673, Effects.cs:1875 = ShowUseHandCard/brainstorm display; the game-state ExecutingCards membership is
written by CardObjectController, not this) · Effects.ShrinkUpUseHandCard/ShowUseHandCard (:679) ·
Effects.FailedPlayCardEffect (:791, shake animation) · memoryObject.Show/OffMemoryPredictionLine + the whole
OffMemoryPredictionLine() helper + `card.Owner.ExpectedMemory` probes (:594-600/:688-704/:826/:861/:1044-1049
— the "show expected cost" region computes a cost solely for the overlay) · SetPermanentIndexText /
OffPermanentIndexText (:390/:811-821) · Show/HideWillEvolutionEffect (:745-757/:763-769, WillEvolutionObject
display) · brainStormObject loop (:803-809) · ShowingHandCard visibility probes + `isYou`/`GManager.IsAI`/
`ContinuousController.autoMinDigivolutionCost` client-presentation branches inside SelectCost (:506-530,
including their client-side `costSelected=false` resets) and the `noHandCard` probe (:649-668) — the mirror
ChoiceProvider is the decider those branches steered the Unity client toward.

## 5. Findings for the coordinator (not cluster-1 build items)

* **G9-048 anchor moved**: `tests/G9-048.SpecialPlay.Tests` expects `CardEffectFactory.BlastDNADigivolveEffect`
  to register a `SpecialPlayRecipe` (`SpecialPlayKind.DnaDigivolve`) — that was the pre-P4 monolith factory's
  behavior. The AS-IS-verbatim P4 file returns an ActivateClass and registers nothing; moreover the test's
  fixture (1 hand card, empty battle area) hits the AS-IS head guards (`GetBattleAreaPermanents().Count == 0`
  / `HandCards.Count < 2`) → `null`. The test will FAIL at runtime once the project builds. Either the recipe
  registration belongs at another seam (SpecialPlayAction availability) or the test needs re-anchoring.
* Every complete mirror `PlayCard()` run STOPs at RD-P6C1-4 (and every paid one earlier at RD-P6C1-2) — the
  four factory consumers (ArtsDigivolve / BlastDigivolution / BlastDNADigivolution / BT9_109) reach a loud
  throw, never silent divergence.
* `Permanent.IsSuspended` now has a raw setter (no gate, no timing emission) — it mirrors the AS-IS public
  FIELD; effect-driven suspends must keep using the sink's Suspend/Unsuspend kinds.
* Pre-existing errors NOT touched (other clusters): `Permanent.cs:153` GetPermanentLevelKey (stage-A
  inventory), the ICardEffect.ToBinding wave in CardEffectCommons.cs/ContinuousAndRestrictionEffects.cs.

## 6. End state

`dotnet build src/HeadlessDCGO.Engine` → **0 errors in PlayCardClass.cs and BlastDNADigivolution.cs**;
project total 132 unique errors remaining (other clusters' files; was 402 at the stage-A baseline, 352 when
this cluster started — concurrent clusters are landing too).
