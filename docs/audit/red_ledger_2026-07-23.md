# Red ledger — CLEARED (rewritten 2026-07-23, post final-polish batch, HEAD 9d32fa97)

## Bottom line

- **Remaining red suites: NONE.** The full local suite (`scripts/run-tests.sh`, **425 test projects**) runs
  **425 green / 0 fail** at HEAD 9d32fa97 (verified 2026-07-23, this batch — sync full-suite run,
  `jobs=12 build_jobs=8`). Every row of the prior consolidated 33-red ledger is resolved.
- **Remaining STOP seats: 4 — all permanent-justified** (defensive 2 + AS-IS-불성립 1 + narrowed
  direct-live-top guard). No latent/tracking STOP remains. BlastDNA's STOP was retired this batch (PORTED,
  item 3a), so it is no longer a seat.
- **Extended digest trio** (seeds 1000/1001/1002) bit-identical: `9F1DA795…` / `143A5B0C…` / `3D5F41C5…`
  (engine behaviour unchanged across this batch's edits).

The prior classification (stale-pin 7 + documented-latent 26) is retained below as an audit trail of HOW each
row cleared. This is now a **closure record**, not a tracking document.

---

## stale-pin (7) — RESOLVED (re-homed to live-state assertions, or retired fixtures)

The R7 teardown rehoused/deleted the structures these pinned; each suite was re-pointed at the live engine
state (3 re-homed to live-state reads) or its stale synthetic fixture retired. All 7 GREEN.

| # | Suite | Disposition |
|---|-------|-------------|
| 1 | `G9-003.PlayCostFactory` | GREEN — re-homed: the paid cost is now observed through the live `PlayCardAction` cost pipeline (continuous modifiers pulled from the live cost fold; W3c cost-fold retirement landed). |
| 2 | `G9-043.ViewLayer` | GREEN — re-homed: predicate reads re-driven off the live `CardSource`/`Permanent` view members; structural pin dropped. |
| 3 | `G9-061.A3ViewLayerFolds` | GREEN — re-homed: the five folds re-asserted via the live getters (`CardSource.Level` / `Permanent.Level` / `CardColors` / `CardTraits`); skeleton-adapter pin retired. |
| 4 | `G9-065.Assembly` | GREEN — retired the stale synthetic fixture cases; assembly is exercised through the Runtime `MainPhaseAction`. |
| 5 | `G9-070.W6LinkCondition` | suite deleted outright (pins sat on undrivable latent declaration surface) — verified: `tests/G9-070.W6LinkCondition.Tests` carries no tracked source (commit 254a7756 removed `Program.cs`+`.csproj`, no `G9-070r` replacement was cut, unlike G9-003r/G9-043r/G9-073r). |
| 6 | `G9-071.W6AppFusion` | GREEN — re-driven App-Fusion through the live evolution path; `AddAppfuseMethodByName` structural pin retired. |
| 7 | `G9-073.W6ProcessCommons` | GREEN — each process re-asserted through its live coroutine (`ChangeDigimonDP/SAttack`, `PlayPermanentCards`, `AddEffectTo…`); verbatim-mirror pin dropped. |

---

## documented-latent (26) — RESOLVED (infra landed across the L1–L4 repair tranches, suites re-driven)

Each design item the suite pinned went live; the suite was re-driven through the now-live pipeline. All 26 GREEN.

| # | Suite | What landed (design item now live) |
|---|-------|-------------------------------------|
| 8 | `B2-MainSkillDeclare` | `MainSkillActivateAction` in the live turn loop (offer/consume/reset/scope; AS-IS `SetActSkill`). |
| 9 | `BT1.StopRemainder` | BT1 STOP-remainder card ports + primitives live via `ActivatedEffectResolver`. |
| 10 | `BT23.PrimTranche1` | G2 OnUseOption reactive window + G4 atomic draw-then-discard primitives live. |
| 11 | `C4-Witness` | Post-trash [On Deletion] window ordering (BT9_081) — sources+top trashed before the deletion window. |
| 12 | `G12-003.LiveCrossCardTrigger` | Cross-card "Anyone" deletion broadcast via `GameFlowProcessor` (ST3_01/04). |
| 13 | `F1-M1-InheritScan` | Digivolution-SOURCE inherited-reactor scan in the activated bridge (all bridge timings). |
| 14 | `G11-002.RlDeferredChoiceE2E` | Commit-once deferred-choice resume in `HeadlessRlEnvironment` (no cost re-pay). |
| 15 | `F1-M1-OnLoseSecurity` | Player-scope `OnLoseSecurity` broadcast (`ActivatedBridgeTimings.EventBroadcast`). |
| 16 | `F1-M2-OnMove` | Breeding→battle promotion `OnMove` broadcast (self + cross-card reactors). |
| 17 | `F1-Tier1-OnAdd` | Batch-once OnAddHand + per-card OnAddSecurity broadcast semantics. |
| 18 | `F1-Tier1-OnDiscard` | OnDiscardHand/Security/Library broadcast + shared batch id + cause-effect stamp. |
| 19 | `FAILa-13.OptionMainDiscriminator` | [Main]-tag OptionSkill discriminator (`ActivateMainOfOptionSide` re-runs only [Main]). |
| 20 | `FAILb-01.InvertSAttack` | Invert-security-attack delta wired into `ChangeSAttackClass.GetSAttack`. |
| 21 | `G3.5-RL-B1.DpModel` | Typed DP model (`DpModifier`/`DpCalculator` relative-then-absolute fold) on the live BattleResolver. |
| 22 | `G6-002.OptionActivatedChoice` | `OptionActivateAction` choice routing via engine `IChoiceProvider`. |
| 23 | `G6-005.EmitOnlyTimings` | Attack-declaration windows emitted by `AttackPermanentAction`, drained through the scheduler (ST1_06). |
| 24 | `G7-004.SecuritySkillActivation` | [Security]-timing activation in the security-check loop (ST1_13). |
| 25 | `G8-005.OptionDeferredE2E` | `DeferredChoiceProvider` action-level commit-once/resolve-resume (cost paid once). |
| 26 | `G9-038.W4Batch1a` | PRIM-W4 batch-1a seams (CanNotBlock / CanNotBeDestroyed / ImmuneFromDPMinus / Alliance / Jamming / Ascension). |
| 27 | `G9-046.SelectAndPlay` | **L1 PRIM closure:** invented `SelectAndPlayFromZoneEffect`/`DeDigivolve` cases disposed (R6-Da'); the surviving play-from-under pin re-driven through `DigivolutionStackHelpers`. |
| 28 | `G9-049.SpecialPlayDiscovery` | **L1 PRIM closure:** on-demand DigiXros recipe registration — `SpecialPlayAction.GetLegalActions` offers it from hand without pre-registration. |
| 29 | `MIG2-RuleProcess` | The 5 AS-IS `AutoProcessing.RuleProcess` processes live (TrashNonDigimon/CardFaceDown/LackLinkCondition/…). |
| 30 | `MIG5-CardSource` | CardSource gate delegations (`CanNotBeAffected`, `CanNotTrashFromDigivolutionCards`) to headless gates. |
| 31 | `R2-DeletionPipeline` | Whole-batch PRE defer in `DestroyPermanentsClass.Destroy` (no per-card split). |
| 32 | `SEC-FaceUpSecuritySource` | Face-up security cards added to the continuous-effect SOURCE population in every getter (`ContinuousScopeEvaluation`). |
| 33 | `PRIM.TriggeredActivatedBridge` | Triggered→activated resolution bridge (an ACTIVATED effect at a general trigger timing resolves via auto-processing). |

---

## Latent-STOP closures

- **RD-3A-02 — RETIRED.** The invented `AddSelfRemovalEffectToPermanent` temp is deleted (③-A resolved,
  `CardEffectCommons.cs:2760`); no live reader remained.
- **BT7_058 / EX8_059 — LIVE.** Survive-own-leave is live (the collect-BEFORE-removal bucket idiom,
  `EX8_059.cs:149/324` verbatim); no STOP.
- **BlastDNA (`BlastDNADigivolution`) — PORTED (item 3a, this batch).** The jogress-FRAME play is live
  (SelectHandEffect → frameless zone-append `CreateNewPermanent` → `SetJogress` → `PlayCardClass` →
  `DiscardEvoRoots(putToTrash:false)` collapse, else `AddHandCard`). Both former residual blockers are closed:
  `MIG4-DISCARDEVOROOTS-PUTTOTRASH` is LIVE (`Permanent.cs:3920`, bare detach-without-trash) and the
  `AddHandCard` single-card overload is live (`RD-P6C1-8` resolved). Latent (no live card caller) — a
  construction smoke test (`DNATEMP-Witness` Test 7) exercises it.

---

## Remaining STOP seats (4) — permanent-justified, not tracking pins

| Seat | Kind | Why it is permanent |
|------|------|---------------------|
| `CardController.cs:4242` | **defensive** | DISPATCH-REMAP double-key guard: a card that registers the identical effect under BOTH `OnEnterFieldAnyone` and `WhenDigivolving` would double-fire (AS-IS has a single key). Structural-invariant guard against a malformed registration. |
| `GManager.cs:198` | **defensive** | `RD-W4-3`: `GetComponent<T>()` throws for a bridge-component type with no mirror (bridge W4 supports `SelectPermanentEffect`/`SelectCardEffect`/`SelectBurstDigivolutionEffect`). Guards an unreachable-in-live request. |
| `TrashLinkedCards.cs:72` | **AS-IS-불성립** | `RD-SKEL-01`: the AS-IS loop budgets per-permanent trashing on `DigivolutionCards.Count` while drawing the pool from `LinkedCards` with no used-host tracking — a faithful headless `ChoiceProvider` translation cannot reproduce it without a non-terminating loop or an invented used-host guard. STOP-guarding is the faithful choice (no simplification, no invention). |
| `Permanent.cs:4549` | **narrowed direct-live-top guard** | `MIG4-DETACH-LIVE-TOP`: no AS-IS caller re-parents a card that is STILL a live field top directly (`IPlacePermanentToDigivolutionCards` RemoveFields first; the host's own live-top fold is the `AddDigivolutionCardsTop` re-root arm). The bare-detach leaf itself is now LIVE; only this residual direct path is guarded. |

### Totals

- Prior stale-pin: **7 → 0** (re-homed/retired).
- Prior documented-latent: **26 → 0** (infra landed, re-driven).
- Prior latent-STOP: RD-3A-02 retired, BT7_058/EX8_059 live, BlastDNA ported.
- **Remaining red: 0** (full suite 425/425 green).
- **Remaining STOP seats: 4** (all permanent-justified; no tracking pins).

---

## Open ledger lines (flagged by REPAIR-batch adversarial review, 2026-07-23)

Two witness-adequacy gaps the "0 red" count did not surface. **Both REPAID 2026-07-23** (REPAIR batch); the
lines are retained below for provenance.

- **P2-8 — BlastDNA witness-pin-on-first-caller. ✅ REPAID 2026-07-23.** The first real card wiring
  `BlastDNADigivolveEffect` is now ported: **`EX6_011`** (RagnaLoardmon, EX6/Red — its `[Ace]` `OnCounterTiming`
  arm) — a full 1:1 port of the DCGO original (was a 7-line stub), giving the keyword body its FIRST live caller.
  The `DNATEMP-Witness.Tests` smoke pin is UPGRADED to behavioral with two new witnesses driving `EX6_011`:
  (1) **trigger window** — `new EX6_011().CardEffects(OnCounterTiming, card)` yields the live `ActivateClass`
  whose `CanTrigger` opens on an OPPONENT permanent's attack (`AttackingPermanent`=P2 Digimon → TRUE) and stays
  shut on an ally attack (→ FALSE, no blast offer — negative control); (2) **DNA jogress end-to-end** — a field
  `[Durandamon]` Lv.6 Red + a hand `[BryweLudramon]` Lv.6 Black → `CanActivate` TRUE → `Activate` drives the blast
  DNA flow (select field root → select hand material → `CreateNewPermanent` → `EX6_011.CanPlayJogress` →
  `PlayCardClass.SetJogress` collapse WRITE); EX6_011 survives as the field top with BOTH material roots stacked
  underneath (full live frame-write collapse), plus a negative control (no hand material → `CanActivate` FALSE).
  Construction-smoke Test 7 is retained; the pin is now behavioral. DNATEMP 9/9 green.
- **P2-9 — DigiXros translated-recipe MemoryCost verification. ✅ REPAID 2026-07-23 (VERDICT: hardcoded 0 is
  INERT for the live play).** AS-IS charges, at DigiXros payment, `baseCost − selectedMaterialCount ×
  digiXrosCondition.reduceCostPerCard` (`CardSource.GetPayingCostWithBaseCost`, DCGO CardSource.cs:664-701/:695);
  the mirror ports this 1:1 (`CardSource.cs:1382`). `SpecialPlayAction.EnsureSpecialPlayRecipe` hardcodes
  `MemoryCost: 0` (`SpecialPlayAction.cs:185`), and that field IS consumed by `ProcessAsync` (`Pay(memoryCost)`,
  :385) — but ONLY in the retained NON-pump boundary lane (G8-006 pin). The LIVE (pump) lane DELIBERATELY OMITS
  `SpecialPlayAction` (`HeadlessLegalActionDispatcher` :77-88, Option A / batch 7b) and routes a DigiXros through
  `PlayCardAction → PlayCardClass.PlayCard → SelectDigiXros → GetPayingCostWithBaseCost`, so the translated
  recipe's `MemoryCost: 0` is NEVER the amount charged for a live DigiXros — it is inert as a pay amount (its only
  live role is the AS-IS-faithful availability gate `CanPay(0)`, mirroring AS-IS's `if (checkAvailability)
  return 0`). PAYMENT-TIME WITNESS added to `RD-BATCH7B.Witness.Tests` `W3_PumpDigiXrosPlay` (a real full-pump
  `BT18_065` DigiXros play): playCost 6, `reduceCostPerCard` 1, 3 materials fused → the live charge is `6 − 3×1 =
  3` (memory 10→7), asserted against the AS-IS formula. Were the recipe's 0 governing, the delta would be 0; it is
  3 — proving the field is inert for the live play. RD-BATCH7B 5/5 green.
