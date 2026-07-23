# Headless substrate audit — part 1/8 (`docs/audit/manifest/hl_part_01.txt`)

Scope: 27 files under `src/HeadlessDCGO.Engine/Headless/`. Each read in full; judged against the
four axes (substrate deciding game rules / gate stubs / mirror-logic-in-substrate / AS-IS divergence)
using only real source (AS-IS `DCGO/Assets/Scripts/...` and the mirror `src/.../Assets/Scripts/...`),
not existing comments/audit notes as evidence.

## Verdict summary

| # | File | Verdict |
|---|---|---|
| 1 | Runtime/MetadataActionProcessor.cs | Clean — dispatch shell |
| 2 | Runtime/RevealAndSelect.cs | **PROBLEM — orphaned mirror-logic duplicate, dead in production** |
| 3 | Bridge/EngineContext.cs | Clean — DI container |
| 4 | Runtime/DeletionReplacementTiming.cs | **Finding — inert retired scaffolding, still wired but fails closed** |
| 5 | Runtime/HeadlessActionQueue.cs | Clean — replay/serialization infra |
| 6 | Runtime/DeDigivolveHelpers.cs | **PROBLEM — rookie-floor check reads static level, diverges from AS-IS live continuous level** |
| 7 | State/GameContextStateAccessor.cs | Clean — state holder |
| 8 | Runtime/CardMovementPort.cs | Clean — zone-move plumbing |
| 9 | Runtime/CardEffectRegistrar.cs | Clean — enter-play registration seam |
| 10 | Choices/ChoiceCompletability.cs | Finding — documented deterministic approximation of AS-IS random retry (informational) |
| 11 | Effects/EffectContext.cs | Clean — data record |
| 12 | Effects/PlayerScopeContinuousHelpers.cs | Clean — generic scope-matching infra |
| 13 | Coroutines/CoroutineAdapter.cs | Clean — coroutine→Task infra |
| 14 | Choices/ChoiceRequest.cs | Clean — data record |
| 15 | Coroutines/EngineWaitCondition.cs | Clean — wait-condition infra |
| 16 | Runtime/ContinuousModifierGate.cs | Clean — thin delegate to AS-IS cost orchestrator |
| 17 | Bridge/BareCauseEffect.cs | Finding — self-disclosed invented type, latent divergence (informational) |
| 18 | Runtime/DeletionReplacementCandidateConditions.cs | Finding — vestigial seam tied to #4 (informational) |
| 19 | Runtime/ActionMask.cs | Clean — data container |
| 20 | Runtime/BattleDeletionGate.cs | Clean — delegates to AS-IS-grounded continuous scan |
| 21 | Runtime/HeadlessTurnState.cs | Clean — phase-state derivation |
| 22 | Runtime/TurnStepCursor.cs | Clean — enum/mapping doc |
| 23 | Effects/EffectDurationExpiry.cs | Clean — thin AS-IS-grounded bucket reset |
| 24 | Runtime/HeadlessEffectState.cs | Clean (stale "TODO" hygiene note) |
| 25 | Bridge/PayCostRoot.cs | Clean — enum |
| 26 | Services/IZoneStateReader.cs | Clean (stale "TODO" hygiene note) |
| 27 | Services/IHeadlessMatchStateResettable.cs | Clean (stale "TODO" hygiene note) |

## Problem findings (detail)

### 1. `Runtime/RevealAndSelect.cs` — orphaned mirror-logic duplicate, dead in production

602 lines reimplementing AS-IS `RevealLibrary` reveal-and-select mechanics directly in the substrate:
condition-matching selection passes, min/max counts, `canNoSelect`/`mutualConditions` AS-IS relaxation
rule, deck-top/bottom ordering with reversal, `DeckTopOrBottom` binary pick, etc. This is genuine
game-rule content (axis 3, mirror-logic-in-substrate) that should live in the mirror layer.

Verified it is **not reachable from any production code path**:
- Its three opening entry points (`RequestChoice`, `RequestMultiChoice`, `RevealAndProcessAllAsync` —
  the only places that open `ChoiceType.RevealSelect`) have **zero production callers** anywhere in
  `src/HeadlessDCGO.Engine` (confirmed by exhaustive grep). Only five test files call them directly,
  in isolation.
- The real, AS-IS-faithful reveal/select logic now lives in the mirror bridge
  `Assets/Scripts/Script/CardEffectCommons/RevealLibrary.cs` (`SimplifiedRevealDeckTopCardsAndSelect` /
  `RevealDeckTopCardsAndSelect` / `ReturnRevealedCardsToLibraryBottom`), which opens choices as
  `ChoiceType.Card` via `context.ChoiceProvider` directly, not `ChoiceType.RevealSelect`.
- `Assets/Scripts/Script/CardEffectFactory.cs:1414`: "`SimplifiedRevealAndSelectEffect` /
  `RevealMultiSelectEffect` ... DELETED with their invented bodies" — the declarative effect classes
  that used to call into `Headless/Runtime/RevealAndSelect` were removed during the bigbang cutover
  (git history confirms the mirror bridge file postdates this file: `6bf5a053`/`6b9cb36b` vs
  `81c3e76e`/`a5835f4f`), but this 602-line substrate file and its `MetadataActionProcessor.cs:679`
  wiring were never deleted.
- `MetadataActionProcessor.ResolveChoiceAsync`'s `ChoiceType.RevealSelect` branch (line 677-687) is
  therefore also dead: nothing ever opens that choice type in production.

Net effect: a full duplicate game-logic implementation sits in `Headless/Runtime/`, unreachable in
production, unguarded (no `[Obsolete]`, no retirement note acknowledging the duplication — contrast
with finding #4 below, which *does* document its own retirement). It is exercised only by its own
unit-test suite in isolation, so tests are green while nothing in the live match loop ever calls it.

### 2. `Runtime/DeDigivolveHelpers.cs` — rookie-floor check diverges from AS-IS

`DeDigivolveAsync`'s loop stop condition (lines 168-173):
```csharp
int? level = ReadInt(top.Metadata, LevelKey);
if (level is int lvl && lvl <= LevelFloor)   // LevelFloor = 3
{
    break;
}
```
reads `top.Metadata[DeDigivolveHelpers.LevelKey]`, which is populated **once, at load time**, from the
card database DTO (`Headless/DataLoading/CardBaseEntityLoader.cs:71`: `["level"] = dto.Level`) — a
static, printed value that is never updated by continuous effects.

AS-IS `IDegeneration.Degeneration` (`DCGO/Assets/Scripts/Script/CardController.cs:4801-4945`)
stops with:
```csharp
if (_permanent.Level == 3) { if (_permanent.TopCard.HasLevel) return true; }
```
where `_permanent.Level` (`Permanent.cs:48-102`, mirrored 1:1 in
`Assets/Scripts/Script/CardEffectCommons/Permanent.cs` around line 565) is a **live getter** that
folds every active `IChangePermanentLevelEffect` continuous effect over the top card's printed level
before comparing.

Two divergences from the same root cause (static vs. live level):
- **Data source**: headless compares the printed/base level; AS-IS compares the continuous-modified
  live level. Any card with a level-changing continuous effect active on the digivolving Digimon
  (`IChangePermanentLevelEffect`) will make the headless floor check evaluate against stale data.
- **Comparator**: headless uses `<=3`; AS-IS uses the exact `==3`. These coincide for the normal
  monotonic level sequence (2/3/4/5/6/7) but diverge if a continuous effect ever drives the live level
  below 3 mid-loop (AS-IS would keep de-digivolving past it since `==3` is false; headless would still
  floor at `<=3`).

The mirror layer's own `Permanent.Level` getter is the established, already-ported live idiom for
exactly this — `DeDigivolveHelpers` (a `Headless/Runtime` substrate file, no `EngineContext` in its
signature) does not have access to it and falls back to the static metadata instead of threading
context through, so this is a substrate correctness gap rather than an intentional adaptation (no
comment discloses or justifies the divergence, unlike the other findings below).

Separately, `DeDigivolveAsync`'s own inline `CannotBeDeDigivolvedKey` immunity check (line 157) reads
a metadata flag that the file's own doc comment (lines 31-38) says is retired and unproduced —
harmless because the sole production caller (`MatchStateMutationSink.cs:765`) pre-gates with the live
`IsDeDigivolveImmune` before ever calling `DeDigivolveAsync`, so this particular check is redundant
dead weight, not a live gap.

## Lower-severity / informational findings

### 3. `Runtime/DeletionReplacementTiming.cs` (+ sibling `Runtime/DeletionReplacementCandidateConditions.cs`)

Every `PreOptions(...)` overload is hard-coded to `return options;` on an always-empty list (the
8 keyword replacements — Evade/Barrier/ArmorPurge/Scapegoat/Fragment/Decode/Partition/Decoy — and the
POST Ascension option were retired to the AS-IS PRE/POST cut-in window, verified against
`Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/Evade.cs` and `CutInProcess.cs`, which do carry
the real logic now). Consequently `RequestChoice` can never open `ChoiceType.DeletionReplacement`, so
`ResolveChoice`/`ResolveKeywordStep`/`ApplyNoTarget` (which itself is an empty `switch` with only a
`default: return false`) are unreachable, and `MetadataActionProcessor.cs:692-707`'s handling branch is
dead. Unlike finding #1, this is fail-closed (no game behavior is wrongly granted) and is explicitly
self-documented at each retirement point with a rationale and a design-doc pointer
(`keyword_rehoming_design_2026-07-15.md`). Flagged only because the scaffolding (class, interface seam,
constructor wiring in `GameFlowProcessor`/`BattleResolver`/`AutoProcessing.cs`) remains fully live-wired
around a permanently-empty core, which is architectural noise rather than a behavioral defect.

### 4. `Bridge/BareCauseEffect.cs`

Self-documented as "an invented minimal cause-carrier (no AS-IS analogue as a standalone type)". Its
own doc comment (lines 22-31) discloses a real semantic gap: a source-less `BareCauseEffect` is *not*
byte-identical to AS-IS `null` — AS-IS's `_cardEffect == null` early-out and "not an opponent effect"
treatment are skipped, reading a null-owner instead. The comment states this is currently latent
because every caller supplies a real card source. Recorded here as a genuine (if currently dormant)
axis-4 divergence, exactly as the code's own `RD-BCE-01` design item flags it.

### 5. `Choices/ChoiceCompletability.cs`

Deliberately deterministic (lexicographic combinations, capped at 200 validator evaluations)
translation of AS-IS's random bounded retry (`SelectHandEffect.cs`, confirmed: 1000-iteration
`GetRandom` sampling, not 200 and not deterministic). The 200 vs. 1000 cap difference is disclosed in
the file's own comment as a deliberate choice for consistency with an existing established cap
(`RD-R4P4-02`), not silently invented. For requests with >200 viable combinations at the boundary size,
this could in principle answer differently than the true AS-IS random search would over many trials;
recorded as a bounded, disclosed approximation rather than a hidden defect.

### 6. Stale literal `TODO` comments

`Runtime/HeadlessEffectState.cs:3`, `Services/IZoneStateReader.cs:5`, and
`Services/IHeadlessMatchStateResettable.cs:3` each carry a literal `// TODO: ...` comment predating the
"design item RDx-NN" convention (project's own TODO lint-guard rule forbids literal `TODO` in engine
source). All three types are still live and correctly used; this is a documentation-hygiene note only,
not a functional finding.

## Clean files (no findings)

`Runtime/MetadataActionProcessor.cs` (pure dispatch switch — every branch either forwards to a mirror
action class or is honestly gated Illegal for retired step-driver currency; the one embedded constant,
`DefaultMemoryPassValue = 3`, is verified against AS-IS `AutoProcessing.cs:685/690`), `Bridge/EngineContext.cs`
(DI container; batch-id counters are pure bookkeeping, each doc-linked to a specific AS-IS
`StackSkillInfos` collapse rule), `Runtime/HeadlessActionQueue.cs`, `State/GameContextStateAccessor.cs`,
`Runtime/CardMovementPort.cs`, `Runtime/CardEffectRegistrar.cs`, `Effects/EffectContext.cs`,
`Effects/PlayerScopeContinuousHelpers.cs`, `Coroutines/CoroutineAdapter.cs`, `Choices/ChoiceRequest.cs`,
`Coroutines/EngineWaitCondition.cs`, `Runtime/ContinuousModifierGate.cs` (thin delegate to AS-IS
`CardSource.GetPayingCostWithBaseCost`), `Runtime/ActionMask.cs`, `Runtime/BattleDeletionGate.cs`
(delegates to `NewModelContinuousScan`, verified against AS-IS `Permanent.cs:3184-3233`),
`Runtime/HeadlessTurnState.cs`, `Runtime/TurnStepCursor.cs`, `Effects/EffectDurationExpiry.cs`
(verified against AS-IS `CardController.cs:961`), `Bridge/PayCostRoot.cs` — all are either pure data
holders/plumbing or thin delegates to already-verified AS-IS-grounded mirror-layer getters, with no
independent game-rule decisions and no unconditional gate stubs.
