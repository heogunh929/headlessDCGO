# Red ledger — consolidated 33-red mapping (2026-07-23)

Source of the failing set: the R7 gate suite run captured at
`/home/hg/.claude/jobs/dae5cd41/tmp/r7_gate_suite.log` (`grep ^FAIL` = **34** rows),
**minus `FAILa-11.MindLinkTamerScope`** which was a build-failure row and is now **GREEN**
(verified 2026-07-23: `2 test(s) passed` — the mind-link Tamer-scope select builds and both pins pass).
Net: **33 red suites**.

Classification model (base = the R7 campaign report triage, verified here against each suite's in-file header):

- **stale-pin (7)** — the suite pins to a structure that the R7 teardown *rehoused or deleted*; the assertion
  is now stale (it describes a shape that no longer exists), NOT a live engine defect. These are the campaign's
  named 7: `G9-003 / G9-043 / G9-061 / G9-065 / G9-070 / G9-071 / G9-073`.
- **documented-latent (26)** — the suite documents *latent infra* (a design item prebuilt/stubbed but not yet
  fully live, or a behavior gap) whose id is carried in the suite header. The RED is the tracking pin for that
  design item; it clears when the infra lands and the suite is re-driven through the real pipeline.

> Note: `FAILa-11` build-fix aside, this ledger is a **triage document only** — no suite is flipped here.
> Verify column reasons against each suite's `tests/<suite>.Tests/Program.cs` header (line-1 block).

---

## stale-pin (7) — assertion pins a rehoused/deleted structure

| # | Suite | Rehousing it pins to | Reason (from header) | Resolution path |
|---|-------|----------------------|----------------------|-----------------|
| 1 | `G9-003.PlayCostFactory` | play-cost factory → EffectRegistry cost pipeline | Pins the old `ChangePlayCostStaticEffect` / `MandatorySelfPlayCostReduction` factory shape; the live play-cost engine now pulls continuous modifiers from the EffectRegistry at play time (W3c cost-fold retirement). | Rewrite the pin to observe the paid cost through the live `PlayCardAction` cost pipeline, or retire it. |
| 2 | `G9-043.ViewLayer` | card-query view layer (CardSource/Permanent members) | Pins the view-layer member surface that predicates read; the members were rehoused into the live CardSource/Permanent views. | Re-drive the predicate reads off live engine state; drop the structural pin. |
| 3 | `G9-061.A3ViewLayerFolds` | continuous view-layer folds (Level/Color/Traits) | Pins the five adapter-class folds (`IChangeCardLevel/PermanentLevel/CardColor/Traits`) that were rehoused into `CardSource.Level` / `Permanent.Level` / `CardColors` / `CardTraits`. | Re-assert the folded values via the live getters; retire the skeleton-adapter pin. |
| 4 | `G9-065.Assembly` | Runtime `MainPhaseAction` (name-shared mirror) | Header is explicit: **"STALE FIXTURE, not an engine gap"** — the 2 red cases drive a synthetic fixture against the pre-rehousing assembly shape. | Retire the synthetic fixture cases (or re-point to the Runtime `MainPhaseAction`). |
| 5 | `G9-070.W6LinkCondition` | W6 link-condition declaration/consumption | Pins `AddSelfLinkConditionStaticEffect` → LinkEffect wiring that was rehoused into the live link-cost pipeline (`LinkHelpers.ResolveLinkCost`). | Re-drive link declaration+consumption through the live `Link` action; drop the structural pin. |
| 6 | `G9-071.W6AppFusion` | W6 App-Fusion declaration/execution | Pins `AddAppfuseMethodByName` / `AppFusionCondition` executed-as-evolution wiring that was rehoused. | Re-drive App-Fusion through the live evolution path; retire the pin. |
| 7 | `G9-073.W6ProcessCommons` | W6 process commons (same-name coroutine mirrors) | Pins the verbatim process-commons mirrors (`ChangeDigimonDP/SAttack`, `PlayPermanentCards`, `AddEffectTo…`) that were rehoused into the live process surface. | Re-assert each process through its live coroutine; drop the structural pin. |

---

## documented-latent (26) — RED tracks a latent design item

| # | Suite | Design item / RD id | Reason (from header) | Resolution path |
|---|-------|---------------------|----------------------|-----------------|
| 8 | `B2-MainSkillDeclare` | B-2 / P1-5 `MainSkillActivateAction` | The main-phase "declare a battle-area [Main] skill" action (AS-IS `TurnStateMachine.SetActSkill`) — offer/consume/reset/scope. | Land `MainSkillActivateAction` in the live turn loop, then drive the 5 pins through it. |
| 9 | `BT1.StopRemainder` | BT1 STOP-remainder card ports | Drives the 5 newly-ported BT1 cards + their primitives via `ActivatedEffectResolver`. | Complete the BT1 STOP-remainder ports/primitives; re-drive. |
| 10 | `BT23.PrimTranche1` | G2 (OnUseOption dispatch) / G4 (draw-then-discard) | Option-use reactive window + atomic draw-then-discard primitives. | Land the G2/G4 primitives on the live pipeline; re-drive. |
| 11 | `C4-Witness` | C-4 / P1-10 post-trash [On Deletion] window (BT9_081) | Battle knock-out must resolve [On Deletion] AFTER own sources+top trashed (AS-IS `DestroyPermanentsClass.Destroy`). | Land post-trash deletion-window ordering; re-drive BT9_081 witness. |
| 12 | `G12-003.LiveCrossCardTrigger` | G12-003 cross-card "Anyone" broadcast | ST3_01/04 must fire on a cross-card deletion via `GameFlowProcessor` "Anyone" broadcast. | Activate cross-card Anyone-timing broadcast; re-drive. |
| 13 | `F1-M1-InheritScan` | F1-M1-INHERITSCAN | The activated bridge never iterated digivolution-SOURCE inherited reactors (structural, all bridge timings). | Land the source-scan in the activated bridge; re-drive. |
| 14 | `G11-002.RlDeferredChoiceE2E` | G11-002 commit-once resume | Option [Main] select suspends → ResolveChoice resumes without re-paying cost. | Complete the deferred-choice commit-once loop in `HeadlessRlEnvironment`; re-drive. |
| 15 | `F1-M1-OnLoseSecurity` | F1-M1 OnLoseSecurity bridge | Player-scope `OnLoseSecurity` broadcast to other field cards via `ActivatedBridgeTimings.EventBroadcast`. | Register/activate the OnLoseSecurity broadcast; re-drive. |
| 16 | `F1-M2-OnMove` | F1 Tier1 OnMove bridge | Breeding→battle promotion `OnMove` broadcast (self + cross-card reactors). | Register/activate the OnMove broadcast; re-drive. |
| 17 | `F1-Tier1-OnAdd` | F1-Tier1 OnAddHand/OnAddSecurity | Batch-once OnAddHand + per-card OnAddSecurity broadcast semantics. | Land the OnAdd broadcast batching; re-drive. |
| 18 | `F1-Tier1-OnDiscard` | F1-Tier1 OnDiscardHand/Security/Library | Broadcast the three discard timings + shared batch id + cause-effect stamp. | Land the OnDiscard broadcast + batch/cause stamping; re-drive. |
| 19 | `FAILa-13.OptionMainDiscriminator` | OptionMainEffect [Main] discriminator | `ActivateMainOfOptionSide` must re-run only the [Main]-tagged OptionSkill, not every OptionSkill. | Land the [Main]-tag discriminator; re-drive `TfxMainDisc`. |
| 20 | `FAILb-01.InvertSAttack` | InvertSAttackClass (dead delta) | The invert-security-attack delta was accumulated but never applied (`Permanent.InvertSecutiryValue`). | Wire the invert delta into `ChangeSAttackClass.GetSAttack`; re-drive. |
| 21 | `G3.5-RL-B1.DpModel` | G3.5-RL-B1 typed DP model | `DpModifier`/`DpCalculator` relative-then-absolute fold + BattleResolver integration. | Land the typed DP model on the live BattleResolver; re-drive. |
| 22 | `G6-002.OptionActivatedChoice` | G6-002 OptionActivateAction choice seam | Option [Main] activated effects resolve their choice via engine `IChoiceProvider` (no manual BuildRequest). | Land `OptionActivateAction` choice routing; re-drive. |
| 23 | `G6-005.EmitOnlyTimings` | G6-005 attack-declaration windows | Attack-declaration windows emitted by `AttackPermanentAction`, drained through the game-loop scheduler. | Land the attack-declaration window emission; re-drive ST1_06. |
| 24 | `G7-004.SecuritySkillActivation` | G7-004 [Security] activated effect | A revealed security card's [Security] effect fires during the security-check loop. | Land [Security]-timing activation in the security loop; re-drive ST1_13. |
| 25 | `G8-005.OptionDeferredE2E` | G8-005 deferred-option commit-once | `DeferredChoiceProvider` action-level commit-once/resolve-resume (cost paid once). | Complete the deferred-option action cycle; re-drive. |
| 26 | `G9-038.W4Batch1a` | PRIM-W4 batch 1a | CanNotBlock / CanNotBeDestroyed / ImmuneFromDPMinus / Alliance / Jamming / Ascension variants. | Land the PRIM-W4 batch-1a seams; re-drive behavior-live. |
| 27 | `G9-046.SelectAndPlay` | PRIM-W5 select-and-play (partial whitebox disposal) | The `SelectAndPlayFromZoneEffect`/`DeDigivolve` invented-helper cases are REMOVED (R6-Da'); the remaining play-from-under pin is latent on the live substrate. | Re-drive the surviving pin through `DigivolutionStackHelpers`; the disposed invented cases stay retired. |
| 28 | `G9-049.SpecialPlayDiscovery` | PRIM-W5 DigiXros discovery | `SpecialPlayAction.GetLegalActions` must offer an on-demand DigiXros from hand without pre-registration. | Land the on-demand recipe registration; re-drive. |
| 29 | `MIG2-RuleProcess` | MIG2 rule processes | 5 AS-IS `AutoProcessing.RuleProcess` processes (TrashNonDigimon/CardFaceDown/LackLinkCondition/…). | Land the 5 rule processes on the live `AutoProcessing`; re-drive. |
| 30 | `MIG5-CardSource` | MIG5 CardSource surface | Instance-method surface (`CanNotBeAffected`, `CanNotTrashFromDigivolutionCards`) delegating to headless gates. | Complete the CardSource gate delegations; re-drive smoke coverage. |
| 31 | `R2-DeletionPipeline` | R2-P1-1 whole-batch PRE defer | `DestroyPermanentsClass.Destroy` must defer the WHOLE sink batch on any PRE option (no per-card split). | Land whole-batch deletion defer; re-drive the cluster witnesses. |
| 32 | `SEC-FaceUpSecuritySource` | face-up-security continuous source scan | Face-up security cards must be a continuous-effect SOURCE population in every getter (DP/immunity/deletion). | Add face-up-security to `ContinuousScopeEvaluation`; re-drive. |
| 33 | `PRIM.TriggeredActivatedBridge` | triggered-activated resolution bridge | An ACTIVATED effect at a general trigger timing (OnAllyAttack) must resolve via `GameFlowProcessor` auto-processing. | Land the triggered→activated resolution bridge; re-drive `TfxWhenAttackDraw`. |

---

### Totals

- stale-pin: **7** (rows 1–7)
- documented-latent: **26** (rows 8–33)
- **Total red: 33** (`r7_gate_suite.log` 34 FAIL − `FAILa-11` now green)
