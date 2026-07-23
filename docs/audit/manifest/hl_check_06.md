# Headless substrate audit — part 6/8

Manifest: `docs/audit/manifest/hl_part_06.txt` (27 files). All 27 read in full. Judgment axes: (1) substrate
arbitrarily deciding game rules, (2) gate stubs, (3) mirror logic leaking into substrate, (4) AS-IS divergence
(order/condition/set). Citations verified against `DCGO/Assets/Scripts/Script/*` where the file made a specific
AS-IS claim (spot-checks documented inline).

## Verdict summary

25/27 files: legitimate substrate, no problem found. 2 files carry a genuine finding (below); several others carry
dormant/dead-code notes that are not active problems but are worth recording.

## Files with findings

### 1. `Headless/Effects/PlayCostHelpers.cs` — invented, unpopulated cost-modifier apparatus (dead-but-live-called)

`PlayCostModifier`/`PlayCostRequest`/`PlayCostResult`/`Evaluate`/`ApplyModifiers` implement a generic staged
(`CostItself`/`PayingCost`), rooted (`Hand`/`Trash`/`Library`/…), mode-flagged (`Add`/`Set`) cost-modifier engine
read from card/instance **metadata** keys `fixedPlayCost` / `playCostDelta` / `payingCostDelta` /
`playCostModifiers`. Grepped the whole engine (`grep -rn "PlayCostDeltaKey\|PayingCostDeltaKey\|PlayCostModifiersKey\|FixedPlayCostKey"`):
**no writer exists anywhere outside this file** — the modifier/fixed-cost metadata is never populated by any
card, effect, or action. Yet `PlayCostHelpers.TryResolveCost` **is** live-called from
`PlayCardAction.cs:466`, `OptionActivateAction.cs:319`, and `DigivolutionCostHelpers.cs:257`.

Net effect today: harmless — `Evaluate()` always finds zero modifiers, so it degenerates to
`baseCost = card.PlayCost`, and the real AS-IS cost pipeline (DigiXros/Assembly fold, `IChangeCostEffect`,
floor-at-0) runs downstream through the verified orchestrator `CardSource.GetPayingCostWithBaseCost`
(cited correctly in `PlayCardAction.cs` as "the single AS-IS orchestrator"). So no behavioral divergence today.
But the file itself is an invented generic cost-modifier **engine** (stages/roots/set-vs-add/availability-check)
with no AS-IS counterpart and no live producer — exactly the kind of substrate-invented apparatus the audit
flags category (3) for, sitting dormant as engine surface that could silently start being fed and diverge from
AS-IS `ChangeCostClass`/`UntilCalculateFixedCostEffect` semantics without anyone noticing (it has its own
independent stage/root model, not derived from the AS-IS classes). Given the current repo history is mid
"cost pipeline AS-IS화" (legacy cost-fold retirement, per recent commits), this looks like a leftover layer that
was superseded by the `GetPayingCostWithBaseCost` fold and never deleted.

**Recommendation**: not an active fidelity bug, but flag for deletion/consolidation — the modifier apparatus is
unused invented machinery in substrate that should either be wired to nothing (deleted) or documented as
intentionally-dormant extension point.

### 2. `Headless/Services/InMemoryRuleQueryService.cs` — dead gate stub

`CanPayCost(HeadlessPlayerId, HeadlessEntityId, int cost) => cost >= 0;` is a textbook category-(2) gate stub
(always passes for any non-negative cost, no memory-availability check). However `grep -rn "\.CanPayCost("
--include=*.cs src/` finds **zero call sites** anywhere in the engine — only the interface declaration in
`IRuleQueryService.cs`. Dormant, not currently bypassing any live constraint. Recorded because the class carries
a literal `// TODO: Replace with real DCGO legal-action and rule checks as rules are ported.` header comment
that is itself stale (the surrounding engine has since grown a real cost pipeline elsewhere) — if this stub is
ever wired up as a real gate it will silently pass everything.

## Files read clean (no problem) with notable spot-checks

- **`SkillWindowSupply.cs`** (876 lines, DORMANT per its own header — only referenced by W2 tests, no live
  state mutation). Extremely thorough AS-IS citation discipline (RDW-01..07 design items, per-timing payload
  provenance). Spot-checked two citations against AS-IS source and both matched exactly:
  `AttackProcess.cs:98-99` (`OnAttackCheckHashtableOfPermanent(AttackingPermanent, attackEffect)`) and
  `CardObjectController.cs:1111` (`OnMove` payload `{"Permanent", movingPermanent}`). No fabricated payload
  found; every unhandled timing is explicitly refused (`TryBuildHashtable` returns false) rather than guessed.
- **`ActivatedEffectResolver.cs`** — resolves activated effects via the live AS-IS surface
  (`CardSource.EffectList`, `CanTrigger`/`CanActivate`/`CanUse` splits cited against `MultipleSkills.cs`,
  `AutoProcessing.cs`, `ICardEffect.cs`). Spot-checked the P1-2 removal note ("DCGO has no
  `StackSkillInfos(null, OnDeclaration)` call anywhere") — confirmed via grep, zero matches. The large block of
  `DELETED with the invented carrier` comments documents retired invented kind-classes being replaced by literal
  AS-IS `ActivateClass` idioms; consistent with the registry-teardown effort recorded in project memory.
- **`CanNotPlayOptionScan.cs`** — claims to mirror AS-IS `CardSource.CanNotPlayThisOption` regions ①②③
  verbatim (`CardSource.cs:184-248`). Read the AS-IS property directly: the three-region scan (player
  `EffectList(None)`, field-permanent `EffectList(None)`, and self `EffectList(None)` when not a field
  permanent) matches exactly, including the `CanUse(null) && CanNotPlay(this)` conjunction and the separate
  color-requirement half being excluded (handled elsewhere, correctly noted). Faithful mirror.
- **`DpCalculator.cs`** — claims to mirror `Permanent.BaseDP` (G3.5-RL-B1). Read AS-IS `Permanent.cs:193-322`:
  AS-IS applies all `IsUpDown` (relative) effects first via sequential `GetDP` calls, then all non-up-down
  (absolute/"set") effects ordered by `ActivatedTime` ascending (last-applied wins), then floors at 0. The
  mirror's "sum relative deltas, then apply absolute sets in ActivatedOrder, then clamp at 0" is behaviorally
  equivalent (relative deltas are commutative addition) and order-faithful for the absolute-set tie-break.
  Correct.
- **`PermanentBookkeepingStore.cs`** — carrier for AS-IS `Permanent` "just-after" fields
  (`PlayingEffect`/`LevelJustAfterPlayed`/etc., cited to `Permanent.cs:3686-3941` per field). Lifecycle
  (create-on-field-entry / persist-across-top-swap / reset-on-field-leave) is well-reasoned substrate
  translation of AS-IS object lifetime, not a rule decision.
- **`MatchSetupFlow.cs`** — `ResolveFirstPlayer`'s indirection (pick a random `setupTurnPlayer` index, then
  return the *other* player as `FirstPlayerId`) looks odd on first read, but reduces to a fair 50/50 pick
  between the two players (validated as exactly-2-player only) — no behavioral divergence, just an indirect
  coding path. Mulligan-defers-security-deal (N-5) and shuffle-both-decks-by-default (N-4) are both cited to
  design notes; not independently re-verified against AS-IS `CreatePlayerDecks` line-for-line but the shuffle
  default direction (both decks shuffled) is uncontroversial for a card game and consistent with the class doc.
- **`TurnFlowDriver.cs`** — packet-index resolution (`ActiveCardList`/field-permanent index lookups) and the
  "main-phase packet surface is armed" gate (mirroring the AS-IS UI's `NextPhaseButton.OnClick` producer
  discipline) are well-cited substrate translations, not invented rules.
- **`ObservationSnapshot.cs`, `GameEvent.cs`, `MatchResult.cs`, `PlayerZoneAdapter.cs`, `CardIdentityAdapter.cs`,
  `HeadlessEntityRegistry.cs`, `DeckListLoader.cs`, `InMemoryHeadlessAttackController.cs`, `EngineTaskRunner.cs`,
  `GameRandomSource.cs`, `PolicyChoiceProvider.cs`, `EffectDuration.cs` (enum mirror only),
  `AutoProcessingTriggerCollector.cs` (now a pure const-key holder per its own RC-6 header — the registry-reader
  half it used to hold was excised, producer-0, matches the "registry deletion = rebuild endpoint" project
  policy), `IRandomSource.cs`, `NullTraceSink.cs`** — pure data/DTO/plumbing substrate (validation, indexing,
  PRNG, event schema, task scheduling, deck-list text parsing, default choice-policy fallback). No game-rule
  content; no AS-IS counterpart is expected or needed for any of these.

## Minor/dead-code notes (not active problems)

- **`NoOpActionProcessor.cs`** — reports `Success` for any action without doing anything; carries a `TODO`
  comment. Zero references anywhere in the engine outside its own file (`grep -rn "NoOpActionProcessor"`) —
  fully unwired dead code, no live risk today.
- **`IActionProcessor.cs`, `IHeadlessMemoryController.cs`** — both carry stale `// TODO: Replace ... as AS-IS
  actions are ported` header comments that predate the substantial porting work now live elsewhere
  (`MetadataActionProcessor`, `TurnFlowDriver`, `InMemoryHeadlessMemoryController`). Doc staleness only, not a
  functional finding — noted so a future pass can clean the comment.

## Files not independently flagged

`InMemoryRuleQueryService.cs`'s other members (`SetLegalActions`/`GetLegalActions`/terminal-outcome plumbing)
are pure storage with no rule computation — only `CanPayCost` (above) carries a stub.
