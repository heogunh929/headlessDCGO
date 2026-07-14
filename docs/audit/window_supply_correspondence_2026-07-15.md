# SkillInfo window SUPPLY-layer correspondence table (R3-W1b batch W2)

2026-07-15. Design: `window_skillinfo_cutover_design_2026-07-14.md` §3 2차 W2, ADAPTATION A3/A4.
Companion to `src/HeadlessDCGO.Engine/Headless/Effects/SkillWindowSupply.cs` (the DORMANT converter + A3 feeder).

**Purpose.** AS-IS game code opens a trigger window by calling `autoProcessing.StackSkillInfos(hashtable, timing)` at the emit point, passing an AS-IS `Hashtable` it builds inline (or via a `CardEffectCommons.<X>Hashtable` builder). The port instead emits `GameEvent`s (metadata dicts) and the LIVE window self-seeds via `WindowResolverWiring.CollectUnifiedSeed`. At cutover (batch C) the seed step must instead convert each drained `GameEvent` → the AS-IS `(Hashtable, EffectTiming)` pair and call the mirror `AutoProcessing.StackSkillInfos` (`GameEventQueue` retained as transport, A4). This table is the load-bearing artifact: for every emitted timing it records the AS-IS emit point, the AS-IS Hashtable keys, the port event keys, and whether the pair is faithfully reconstructable here (HANDLED) or a GAP (with the design item the C batch must close).

**Ground rule.** A timing is HANDLED only when (1) its AS-IS payload is built by a builder that EXISTS in the mirror `HashtableSetting.cs` (byte-identical keys reused, never synthesised) AND (2) every argument that builder needs is reconstructable from the `GameEvent` (subject id + owner + zones + metadata). Otherwise it is a GAP — left unhandled, loudly, never fabricated (no-simplification rule).

Mirror builders that EXIST (`HashtableSetting.cs`): `CardEffectHashtable`, `PierceCheckHashtableOfPermanent`, `OnDeletionCheckHashtableOfPermanent`, `WhenPermanentWouldRemoveFieldCheckHashtable`, `OnDeletionHashtable`, `OnEnterFieldHashtable`, `WouldEnterFieldHashtable`, `WouldLinkHashtable`, `OnPlayCheckHashtableOfCard`, `WhenDigivolvingCheckHashtableOfCard`, `OptionMainCheckHashtable`, `OnPlayCheckHashtableOfPermanent`, `WhenDigivolutionCheckHashtableOfPermanent`, `OnAttackCheckHashtableOfCard`, `OnAttackCheckHashtableOfPermanent`, `WhenDigivolutionCardWouldDiscardedCheckHashtable`.

## 1. Design items (GAP classes)

| id | GAP class |
|----|-----------|
| RDW-01 | `OnDestroyedAnyone` / `OnLeaveFieldAnyone` etc. use `OnDeletionHashtable(perms, cardEffect, battle, isDPZero)` — builder exists, but the payload needs the PRE-removal per-permanent snapshot (TopCard/CardSources/CardNames/…) plus `battle` + `isDPZero` + `cardEffect`, none carried by the POST-move `CardMoved` deletion event. AS-IS builds it before the card leaves the field (collect-before-removal, design §5.5 F2r). C batch: carry the payload on the emit, or re-position `StackSkillInfos` to the AS-IS pre-removal position. |
| RDW-02 | Inline-hashtable timings whose builder is NOT in the mirror `HashtableSetting.cs`. C batch: mirror each inline builder (byte-identical keys) into `HashtableSetting`, or re-position the call. |
| RDW-03 | `OnSecurityCheck` — AS-IS uses `GetSkillInfos` (not `StackSkillInfos`) with an inline `{AttackingPermanent, Card}` drawn from ATTACK context, absent from the `SecurityCheck` event. Also the security-check window is a synchronous seam (design §1.1 seam 7). |
| RDW-04 | `OnPlay` / `OnEnterFieldAnyone` / `WhenDigivolving` — AS-IS uses the FULL `OnEnterFieldHashtable` (`OnEnterFieldHashtableParams`: evoRoots/Root/oldLevels/digiXrosCount/…), none of which the event carries. The Check-variant builders are the CanTrigger-probe shape, NOT the StackSkillInfos payload; using them would diverge. |
| RDW-05 | Effect-driven attack — `attackEffect` (live `ICardEffect`) is a GAP; the event carries only `attackCauseEffectId`. Plain attacks (`attackEffect == null`, `TurnStateMachine.cs:1250`) are HANDLED. |
| RDW-06 | Cut-in-routed timings — AS-IS stacks these on the SECOND instance `autoProcessing_CutIn` (BeforePayCost/AfterPayCost/Counter/would-be-deleted/bounce PRE windows). These belong to the play/battle-pipeline seams (design §1.1 seams 2–7), not the main-loop supply; their payloads are ALSO inline (RDW-02 class). |

## 2. Correspondence table

Timing = the mirror `EffectTiming` value the port's `TriggerTimingMap.Derive` string parses to. "instance" = `autoProcessing` (main) or `autoProcessing_CutIn` (cut-in). Port event keys = the metadata the drained `GameEvent` carries.

| EffectTiming | AS-IS emit (file:line) | instance | AS-IS Hashtable keys (builder / inline) | port event → keys | status |
|---|---|---|---|---|---|
| OnAllyAttack | AttackProcess.cs:199 (payload :98) | main | `OnAttackCheckHashtableOfPermanent` → {AttackingPermanent, CardEffect} | StateChanged, Subject=attacker, Actor=owner, meta[attackCauseEffectId?] | **HANDLED** (CardEffect=null; RDW-05 if effect-driven) |
| OnEndAttack | AttackProcess.cs:480 (payload :98) | main | `OnAttackCheckHashtableOfPermanent` → {AttackingPermanent, CardEffect} | (queued OnEndAttack; EndAttackTriggerHook owns scheduler half off-queue) | **HANDLED** (payload); caveat F1-ENDATTACK-HOOK double-fire, verify in C |
| OnCounterTiming | AttackProcess.cs:266/285 (payload :99) | **cut-in** | `OnAttackCheckHashtableOfPermanent(new Permanent(cardSources), …)` → {AttackingPermanent, CardEffect} | StateChanged, Subject=attacker, meta[CounterPassKey], meta[counterSourcesSnapshot] | **HANDLED (payload)**; loop routing = cut-in instance + IsCounterEffect two-pass split → C batch (RDW-06) |
| OnUseAttack ("OnAttack") | — (no AS-IS StackSkillInfos) | — | — | StateChanged / AttackDeclared | GAP: port-only choke, no AS-IS supply point (folded into OnAllyAttack) |
| OnDestroyedAnyone | CardController.cs:3737 | main | `OnDeletionHashtable(perms, cardEffect, battle, isDPZero)` → {CardEffect?, battle?, DPZero?, hashtables:[{Permanent,TopCard,CardSources,DigivolutionSources,CardNames,CardColors,HasSaveText,Level}]} | CardMoved field→Trash, meta[deletionBatchId] | GAP **RDW-01** |
| OnLeaveFieldAnyone | CardController.cs:3749/2373/2539/2704/3360/3598/1117 (CardObjectController) | main | `OnDeletionHashtable(...)` (battle/DPZero=false for bounce paths) | CardMoved field→{Trash/Hand/Library/Security} | GAP **RDW-01** |
| OnPermamemtReturnedToHand | CardController.cs:2692 | main | `OnDeletionHashtable(bounceTargets, cardEffect, null, false)` | CardMoved field→Hand | GAP **RDW-01** |
| OnPlay / OnEnterFieldAnyone | CardController.cs:1691 | main | `OnEnterFieldHashtable(params, isEvolution, isJogress, digiXrosCount, assemblyCount, cardEffect)` | CardMoved Hand/Breeding→BattleArea | GAP **RDW-04** |
| WhenDigivolving | (play/digivolve pipeline) | main | `OnEnterFieldHashtable` (isEvolution=true) | StateChanged, Subject=topCard | GAP **RDW-04** |
| OnMove | CardObjectController.cs:1111 | main | inline {Permanent} | CardMoved Breeding→BattleArea, Subject | GAP **RDW-02** |
| OnDiscardHand | CardController.cs:56 | main | inline {DiscardedCards, CardEffect} | CardMoved Hand→Trash, meta[discardBatchId] | GAP **RDW-02** |
| OnDiscardSecurity | CardController.cs:4377 | main | inline {DiscardedCards, CardEffect} | CardMoved Security→Trash, meta[securityLossBatchId] | GAP **RDW-02** |
| OnDiscardLibrary | CardController.cs:5815 | main | inline {DiscardedCards, CardEffect} | CardMoved Library→Trash, meta[discardBatchId] | GAP **RDW-02** |
| OnAddHand | CardObjectController.cs:620 | main | inline {Players, CardEffect, CardSources} | CardMoved →Hand, meta[addHandBatchId] | GAP **RDW-02** |
| OnReturnCardsToHandFromTrash | CardObjectController.cs:578 | main | inline {CardSources} | CardMoved Trash→Hand | GAP **RDW-02** |
| OnReturnCardsToLibraryFromTrash | CardObjectController.cs:800/882 | main | inline {CardSources} | CardMoved Trash→Library | GAP **RDW-02** |
| OnLoseSecurity | CardController.cs:5444 | main | inline {Player, SkillInfo, CardEffect} | CardMoved Security→non-Security, meta[securityLossBatchId] | GAP **RDW-02** |
| OnAddSecurity | CardController.cs:5489 | main | inline {Player, CardSources} | CardMoved →Security, meta[addSecurityBatchId] | GAP **RDW-02** |
| OnFaceUpSecurityIncreased | CardController.cs:5506/5548 | main | inline {Player, CardSources} | StateChanged, Subject, meta | GAP **RDW-02** |
| OnUseOption | CardController.cs:1765 | main | inline {Card, Root, Cost} | StateChanged, Subject=optionId | GAP **RDW-02** |
| OnDraw | CardController.cs:1960 | main | inline {Player, CardEffect} | StateChanged, Actor | GAP **RDW-02** |
| OnUseDigiburst | CardController.cs:2228 | main | inline {Permanent, CardEffect} | StateChanged, Subject | GAP **RDW-02** |
| OnTappedAnyone | CardController.cs:5648 | main | inline {Permanents, IsBlock, CardEffect?} | StateChanged, meta | GAP **RDW-02** |
| OnUnTappedAnyone | CardController.cs:5754 | main | inline {CardEffect, Permanents} | StateChanged, Subject | GAP **RDW-02** |
| WhenTopCardTrashed | CardController.cs:4915/5092/5958, ArmorPurge.cs:79, BT8_110.cs:160 | main | inline {Permanent, CardSources} | StateChanged, Subject | GAP **RDW-02** |
| OnDigivolutionCardDiscarded | CardController.cs:5215 | main | inline {CardEffect, Permanent, DiscardedCards} | StateChanged, Subject, meta[discardedCardIds] | GAP **RDW-02** |
| OnDigivolutionCardReturnToDeckBottom | CardController.cs:5400 | main | inline {Permanent, DeckBottomCards, CardEffect} | StateChanged, Subject | GAP **RDW-02** |
| OnLinkCardDiscarded | CardController.cs:5327 | main | inline {CardEffect, Permanent, DiscardedCards} | StateChanged, Subject | GAP **RDW-02** |
| OnAddDigivolutionCards | Permanent.cs:1119/1223 | main | inline {Permanent, CardEffect, CardSources, isFromSameDigimon, isFromDigimon} | StateChanged, Subject | GAP **RDW-02** |
| WhenLinked | Permanent.cs:1290 | main | inline {Permanent, CardEffect, Card, isFromDigimon} | StateChanged, Subject=host | GAP **RDW-02** |
| OnBlockAnyone | AttackProcess.cs:560 | main | inline {AttackingPermanent, DefendingPermanent, CardEffect, IsBlock?} | StateChanged | GAP **RDW-02** |
| OnAttackTargetChanged | AttackProcess.cs:622 | main | inline {AttackingPermanent, DefendingPermanent, CardEffect, IsBlock?} | StateChanged, Subject=attacker | GAP **RDW-02** |
| OnStartBattle | CardController.cs:4557 | main | inline {AttackingPermanent, DefendingPermanent, DefendingCard} | StateChanged | GAP **RDW-02** |
| OnEndBattle | CardController.cs:4718 | main | inline {AttackingPermanent, DefendingPermanent, DefendingCard, WinnerPermanents(+_real), LoserPermanents(+_real), LoserCard, WasTie, battle} | StateChanged, Actor | GAP **RDW-02** |
| OnSecurityCheck | CardController.cs:3954 (GetSkillInfos, payload :3948) | main | inline {AttackingPermanent, Card} | SecurityCheck, Subject=checkedCard | GAP **RDW-03** |
| OnStartTurn | TurnStateMachine.cs:564 | main | null | StateChanged, Actor | GAP: AS-IS passes `null` hashtable — trivially reconstructable, but the C batch must confirm the null-payload StackSkillInfos position (turn boundary) is the right seam |
| OnStartMainPhase | TurnStateMachine.cs:905 | main | null | StateChanged, Actor | GAP (as OnStartTurn) |
| OnEndTurn | AutoProcessing.cs:699 | main | null | StateChanged, Actor | GAP (as OnStartTurn) |
| RulesTiming / AfterEffectsActivate | AutoProcessing.cs:134/597, ICardEffect.cs:1283 | main | null | (internal AutoProcessCheck / post-resolve re-stack) | N/A — driven by the mirror `AutoProcessCheck`/`TriggeredSkillProcess` tail, NOT a GameEvent |
| BeforePayCost / AfterPayCost | CardController.cs:985 etc. | **cut-in** | `WouldEnterFieldHashtable(...)` etc. | StateChanged, Subject=card | GAP **RDW-06** (play-pipeline seam) |
| WhenWouldLink / WhenPermanentWouldBeDeleted / WhenRemoveField / WhenReturnto{Hand,Library}Anyone / OnRemovedField / WhenWouldDigivolutionCardDiscarded | CardController.cs:3475/3690/3699/2317/2327/2483/2493/2644/2654/3532/5175, CardObjectController.cs:518/1042 | **cut-in** | `WouldLinkHashtable` / `WhenPermanentWouldRemoveFieldCheckHashtable` / `WhenDigivolutionCardWouldDiscardedCheckHashtable` (builders exist) | (PRE-move cut-in windows) | GAP **RDW-06** (PRE cut-in seams; payloads need the pre-move target list) |

## 3. Converter coverage (SkillWindowSupply.TryBuildHashtable)

**HANDLED** (produces a byte-equivalent AS-IS payload): `OnAllyAttack`, `OnCounterTiming`, `OnEndAttack` — all via `CardEffectCommons.OnAttackCheckHashtableOfPermanent(attackingPermanent, cardEffect: null)`, for a PLAIN declared attack. An effect-driven attack (event carries `attackCauseEffectId`) is left UNHANDLED (RDW-05).

**UNHANDLED** (every other derived timing): returns false from `TryBuildHashtable`; the timing is enumerable via `UnhandledTimings` so nothing is silently dropped. Design items per §1/§2.

## 4. Minimum-batch sequencing (A3) — parity with `WindowResolver.FilterToMinimumBatch`

The old window sequenced cross-batch deletions INSIDE the loop: each pass, `FilterToMinimumBatch` (WindowResolver.cs:324) restricted the active set to `{ batch-less } ∪ { minimum remaining batch }`, keyed on `TimingWindowTrigger.BatchId` (stamped in `WindowResolverWiring` — :348 deletion; :990/:1002/:1015/:1026/:1041 the leave/security-loss/discard/add-hand/add-security bridge stamps). All ids draw from ONE globally-unique counter (`EngineContext.Next*BatchId`), so they compare across timings.

A3 moves this ENTIRELY into the supply feeder; the mirror `MultipleSkills` loop stays batch-free (AS-IS 1:1). `SkillWindowSupply`:

- `ReadSequencingBatchId(event, timing)` reads the batch id off the event by the SAME per-timing rules the old stamps used (deletion/leave → `deletionBatchId`; loss → `securityLossBatchId`; discard → `discardBatchId`, OnDiscardSecurity falling back to `securityLossBatchId`; add-hand → `addHandBatchId`; add-security → `addSecurityBatchId` else `Sequence`). A real (non-zero) id sequences; the sentinel `0` is batch-less (`null`) — exactly the old `deletionBatchId != 0` guard.
- `SequenceByMinimumBatch(entries)` splits a co-drained set into ORDERED passes: pass 0 = every batch-less entry + the lowest batch; each later pass = the next ascending batch alone. This is `FilterToMinimumBatch` re-running per pass (batch-less entries stay eligible every pass but resolve in the first, so they ride pass 0). Order is stable within a pass. The C batch feeds one pass per `TriggeredSkillProcess` invocation, holding the rest — so N cards of one Destroy() collapse into one window, an independent later Destroy() fires in the next pass, matching AS-IS resolving each Destroy()'s window before the next.

## 5. Ownership / dormancy

New files only: `src/HeadlessDCGO.Engine/Headless/Effects/SkillWindowSupply.cs`, `tests/W2-SkillWindowSupply.Tests/`, this doc. No live seam rewired (cutover = batch C). The converter has no side effects (reads a `GameEvent`, builds a transient `Permanent` view + a mirror Hashtable). Build 0 errors; `Stage5-WindowResolver.Tests`, `G3.5-006.AutoProcessing.events.Tests`, `G9-006.BeforePayCostWindowE2E.Tests` unchanged (no behavior change).
