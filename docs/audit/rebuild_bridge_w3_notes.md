# Bridge wrapper batch W3 — notes (heavyweight / per-helper-design rows)

Implements docs/audit/effect_model_rebuild_design_2026-07-13.md §11.11 rule set, batch W3 (opus): the
⚠️-flagged + NO-MIRROR rows of docs/audit/mutation_helper_bridge_map.md — the rows that needed REAL per-helper
design (substrate drops load-bearing params / architecture mismatch / no substrate equivalent), not a thin
type-swap. Continues the W1/W2 conventions: AS-IS-path files, `static partial class CardEffectCommons`, AS-IS
signatures translated (`IEnumerator`→`Task`, delegates likewise, `ICardEffect activateClass` KEPT), delegation
into verified substrate wherever the substrate models the behaviour, wrapper-side implementation where it does
not, explicit design item (RD-W3-n) where neither is possible — never a silent drop.

## Files

- `Script/CardEffectCommons/RevealLibrary.cs` (AS-IS-path skeleton FILLED) — the reveal-library family +
  the AS-IS namespace-level types (`RemainingCardsPlace`, AS-IS-shape constructors of
  `SimplifiedSelectCardConditionClass`/`SelectCardConditionClass`).
- `Script/CardEffectCommons/PlayCardsBridge.cs` (new sibling partial, W2 ProcessAccordingToResultBridge
  convention — these rows' AS-IS home is the monolith `CardEffectCommons.cs`) — PlayPermanentCards,
  PlayOptionCards, PlaceDelayOptionCards, the 14 named PlayToken helpers, and the wrapper-side
  `CanEnterFieldByEffect` gate.
- `Script/CardEffectCommons/DigivolveAndTrashBridge.cs` (new sibling partial) — DigivolveIntoHandOrTrashCard,
  DigivolveIntoExcecutingAreaCard, TrashDigivolutionCardsAndProcessAccordingToResult,
  TrashDigivolutionCardsFromTopOrBottom, ActivateMainOfOptionSide, DrawAndDiscardCards.
- `Script/CardEffectCommons/DNADigivolveEffects.cs` (AS-IS-path skeleton FILLED) —
  DNADigivolvePermanentsIntoHandOrTrashCard + the DNA-temp STOP row at its AS-IS signature.
- `Script/CardEffectCommons/KeyWordEffects/Blitz.cs` — pre-existing file-scoped namespace converted to brace
  form (W1 CS8954 lesson) + a second brace-scoped block appended for the AS-IS-signature `BlitzProcess`.
- `Script/CardEffectCommons/CardPortingFramework.cs` — **the one edit outside wrapper files, flagged**: the
  two legacy condition classes got the `partial` keyword (2 tokens, purely syntactic, zero behaviour change to
  their legacy parts) so the AS-IS constructor overloads could live in the AS-IS-path RevealLibrary.cs. This
  was forced by a genuine constraint the batch design didn't anticipate: the AS-IS type names
  `SimplifiedSelectCardConditionClass`/`SelectCardConditionClass` are already taken in the SAME namespace by
  mirror-invented classes with INCOMPATIBLE shapes (HeadlessEntityId predicates + RevealDestination), and a
  same-namespace re-declaration is CS0101 while a different-namespace twin would CS0104 every old-corpus card.
  Partial-izing is the minimal, additive resolution (same spirit as W1's brace-namespace conversion).
- `Script/CardEffectCommons/TrashDigivolutionCards.cs` — header pointer only (its "W3 will fill" note now
  points at DigivolveAndTrashBridge.cs).

## Per-row report

### 1. PlayPermanentCards (977 calls) — IMPLEMENTED (wrapper-side filter restored)
AS-IS filters the list by `CanPlayAsNewPermanent(cardSource, payCost, cardEffect: activateClass,
isBreedingArea, fixedCost)` before playing; the substrate re-runs that filter with `cardEffect: null` (its
`CanPlayAsNewPermanent` documents the discard), silently skipping the `CanEnterField(cardEffect)`
"can't be played by effects" scan (AS-IS CardSource.CanEnterField — the ICanNotPutFieldEffect three-region
scan). The wrapper pre-filters with the substrate's modeled halves PLUS a wrapper-side
`CanEnterFieldByEffect(cardSource, activateClass)` reproduction, then delegates to the verified substrate play
path (whose null-effect re-filter passes a superset, so the wrapper filter is the effective one).
`CanEnterFieldByEffect` coverage:
- region ① (field permanents): scanned via each field permanent's TOP-card `EffectList(EffectTiming.None)`
  (the AS-IS `Permanent.EffectList` aggregate's dominant source) — inherited/granted-scope producers have no
  pre-flip scan surface (design item RD-W3-2);
- region ② (players): NO pre-flip surface (player-bucket grants are registry bindings, not scannable
  ICardEffect lists) — design item RD-W3-2;
- region ③ (the card's own effects when not on a permanent): exact.
Evidence check: `CanNotPutFieldClass` (the only ICanNotPutFieldEffect producer) has ZERO factory/card
producers on the mirror today, so regions scanning nothing is currently exact behaviour; the gate is
structurally in place for the first producer card.

### 2. SimplifiedRevealDeckTopCardsAndSelect (409) + RevealDeckTopCardsAndSelect (25) — IMPLEMENTED (imperative)
Mirrors the AS-IS structure 1:1: `SimplifiedRevealDeckTopCardsAndSelect` is the AS-IS thin adapter (expands
the simplified conditions + SHARED advanced params into `SelectCardConditionClass[]` and forwards — including
the AS-IS QUIRK that its early empty-library guard checks the effect OWNER's deck even when
`isOpponentDeck`); the full `RevealDeckTopCardsAndSelect` is implemented imperatively over substrate
primitives in AS-IS statement order: guards → reveal top N → `canNoAction` whole-effect opt-out (≥2 passes) →
sequential passes over the shared pool (per-pass `maxCount = Min(pass max, matching-in-current-pool)`, no-match
pass skipped, the `mutualConditions` relaxation verbatim from RevealLibrary.cs:302-308) → per-pass selection →
per-card `SelectCardCoroutine` → Mode routing (AddHand/Discard/PlayForFree/PlayForCost/Custom) → pool removal →
`isSendAllCardsToSamePlace` → remaining routing → trailing `revealedCardsCoroutine` (AFTER the remaining
routing — AS-IS order for THIS method).
ALL previously-unmodeled params implemented:
- `canTargetCondition_ByPreSelecetedList`: incremental one-pick-at-a-time choice loop reproducing the AS-IS
  panel's per-pick re-filtering (SelectCardPanel.cs:451/527) — the same incremental semantics
  SelectAssemblyClass already established;
- `canEndSelectCondition`: AS-IS `CanEndSelection` formula verified from SelectCardPanel.cs:568
  (`(cond == null || cond(selected)) && (canEndNotMax || count == max)`) — batch path carries it as the
  ChoiceRequest `SelectionValidator` (the established SelectPermanentEffect route), incremental path folds it
  into the per-pick end-allowance;
- `canNoSelect` / `canEndNotMax`: min/max/canSkip mapping identical to the verified substrate selects;
- `isSendAllCardsToSamePlace`: remaining := ALL revealed (AS-IS :425-428);
- `revealedCardsCoroutine`: threaded (position differs per method, mirrored);
- `isOpponentDeck` / `mutualConditions` / `canNoAction`: as AS-IS (opt-out + reveal-player resolution copied
  from the verified RevealMultiSelectEffect patterns).
Mode routing / remaining routing use the exact verified conventions (ReturnToHand/TrashCard mutation kinds,
RevealTrashFlagKey stamp on revealed→trash so OnDiscardLibrary stays suppressed, DeckTopOrBottom binary pick,
≥2-card ordering pick with first-pick-topmost reversal for DeckTop, substrate
`ReturnRevealedCardsToLibraryBottom` for DeckBottom). PlayForFree/PlayForCost route through this batch's own
AS-IS-signature `PlayPermanentCards` bridge (so the cardEffect-gated filter applies there too).
Also added: the AS-IS-signature `ReturnRevealedCardsToLibraryBottom(List<CardSource>, ICardEffect)` overload
(AS-IS public sibling in the same AS-IS file, 7 card calls) delegating to the verified substrate.
UI-only strips (commented in-file): ShowCardEffect overlays, PlayLog strings, WaitForSeconds, IsBeingRevealed
flag flips.
**Design item RD-W3-1**: the incremental loop cannot reproduce one AS-IS panel corner — UN-picking an
already-picked card to satisfy `canEndSelectCondition` at maxCount. Unreachable for the AS-IS caller shapes
(prefix-monotone conditions); recorded, not silent.

### 3. TrashDigivolutionCardsAndProcessAccordingToResult (9) — IMPLEMENTED on TrashSpecificSourcesAsync
The same-named substrate method is the incompatible top/bottom-COUNT shape (bridge-map ⚠️⚠️ name collision);
the wrapper is built on `DigivolutionStackHelpers.TrashSpecificSourcesAsync` (explicitly documented as the
AS-IS `ITrashDigivolutionCards(permanent, selectedCards, …)` mirror). Host gates (top-card CanNotBeAffected +
ImmuneFromStackTrashing) via the substrate's own private `IsHostStackTrashGated` (accessible — same partial
class); per-card CanNotTrashFromDigivolutionCards protection is honoured inside the primitive. Success = any
requested card actually trashed (AS-IS `Some(IsTrashed)`); success payload (AS-IS `TrashedCards`) reconstructed
RD-W2-2-style from real state evidence (was-a-source-before ∧ not-a-source-after ∧ now-in-owner's-trash),
correct under partial success.

### 4. TrashDigivolutionCardsFromTopOrBottom (121) — IMPLEMENTED (cardCondition restored)
AS-IS signature incl. the dropped `Func<CardSource,bool> cardCondition`; the wrapper walks the digivolution
sources from top/bottom collecting up to `trashCount` cards passing the filter (protected cards still occupy
collection slots, exactly as AS-IS — protection filters afterwards inside the primitive), then trashes that
SPECIFIC list via `TrashSpecificSourcesAsync`. ORDER FINDING (load-bearing): the mirror
`Permanent.DigivolutionCards` lists sources BOTTOM→TOP (`DigivolutionStack.UnderCards` = `_cards.Take(n-1)`
with top at `[^1]`), while the AS-IS list is TOP→BOTTOM (AS-IS `cardSources[0]` = top card,
`AddDigivolutionCardsTop` inserts at index 1) — the wrapper reverses for `isFromTop`. A naive same-index walk
would have trashed from the wrong end for every conditional caller (ST24 series).

### 5. PlayToken family — IMPLEMENTED (capacity check restored); PlayToken(CEntity_Base) not declarable
All 14 named-token AS-IS-signature wrappers (`PlayDiaboromonToken`…`PlayPetrificationToken`) with the two
AS-IS gates the substrate `PlayToken` documents as unmodeled:
- the field-CAPACITY check `card.Owner.fieldCardFrames.Count(empty ∧ battle) >= quantity` (AS-IS :149).
  **Frame-count evidence**: parsed `DCGO/Assets/Scenes/BattleScene.unity` — `YourPermanentFrame`/
  `OpponentPermanentFrame` each hold exactly 16 qualifying children ("カード枠1..16", each with the 2
  sub-objects `Player.Start` requires) ⇒ battle-area capacity = 16 permanents/player. Implemented as
  `16 − |owner battle-area|`. AS-IS QUIRK KEPT: capacity is checked on the EFFECT-SOURCE OWNER's board even
  when the token enters the opponent's board (Fujitsumon `isOwnerPermanent:false` / Petrification);
- the empty-frame half of `CanPlayAsNewPermanent(playCards[0], payCost:false, …)` on the TOKEN owner's board
  (≥1 free of 16).
**Design item RD-W3-3**: the generic `PlayToken(CEntity_Base tokenData, …)` AS-IS overload is NOT declared —
the mirror's `Script/CEntity_Base.cs` carries only the `CardColor` enum (no `CEntity_Base` class), so the
AS-IS signature would itself be a new declaration error; grep evidence (`--binary-files=text`): ZERO card
files call `CardEffectCommons.PlayToken(` directly — every card call uses a named helper.
**Design item RD-W3-4**: the `CanEnterField(activateClass)` half of the token gate cannot run wrapper-side —
the token instance does not exist until the substrate creates it, and ICanNotPutFieldEffect's predicate takes
the token CardSource. Needs a substrate-side hook when the first "can't put field" producer is ported.

### 6. Digivolve rows — IMPLEMENTED (thin, all params threaded)
- `DigivolveIntoHandOrTrashCard` (342) and `DigivolveIntoExcecutingAreaCard` (1): clean AS-IS-signature
  delegation — the substrate (`DigivolveIntoZoneCoreAsync`, "verbatim verified") is already param-for-param
  (cost tuples, requirement-ignore, optionality, success/failed branches); only `ICardEffect`→source-card +
  the W2 inert-coroutine→`Func<Task>` translation.
- `DNADigivolvePermanentsIntoHandOrTrashCard` (55): clean delegation (bridge map: "near-perfect 1:1").
  **Design item RD-W3-6**: the substrate discards `payCost` at runtime (`_ = payCost` — predicate-form DNA is
  cost-0, cost carried by recipes); the wrapper threads the parameter through unchanged so the documented
  behaviour stays centralised in the substrate, and the nuance is recorded here rather than silently.

### 7. PlayOptionCards (43, NO-MIRROR) — IMPLEMENTED (imperative)
Pre-given-list semantics (unlike the mirror `PlayOptionCardEffect`, which runs its own zone select):
- filter by AS-IS `!CanNotPlayThisOption` = `!CanNotPlayOptionScan.CanNotPlay(...) &&
  OptionColorRequirement.Matches(...)` — the exact pair the verified effect-driven option-play path applies
  (E3-P1-1);
- per card: optional cost payment (`payCost` — resolved play cost via ContinuousModifierGate; unaffordable →
  card skipped, AS-IS endPlayCard; paid via the sink AddMemory mutation), then the VERIFIED effect-driven
  option-use order: move to trash (headless "trash-before-resolve" OptionActivate order) → emit OnUseOption →
  resolve ONLY the [Main]-tagged OptionSkill effect via `ActivatedEffectResolver.ResolveAsync(…,
  effectFilter: IsMainOptionEffect)` (= the substrate ActivateMainOfOptionSide route);
- `setAddSecurityEndOption`: implemented — AS-IS registers a `PlaceToSecurityEffect(toTop:true, face down)`
  hook on the owner's UntilEachTurnEndEffects for the duration of the play, redirecting each used Option's
  post-use placement from the trash to the TOP of security; the wrapper performs that redirect after the
  [Main] resolution via the sink's AddToSecurity route, which applies the central `CanAddSecurity` gate — the
  same gate `PlaceToSecurityEffect`'s own `CanResolveCondition` checks. (Real AS-IS user: BT10_041.)
- `playCard.SetShowEffect()` = UI (elided, commented).
Behaviour nuance (inherited from the verified substrate flow, not introduced here): headless options transit
the trash before resolving instead of an execution area — the pre-existing, documented substrate order.

### 8. RevealDeckTopCardsAndProcessForAll (34, NO-MIRROR) — IMPLEMENTED (imperative)
Full AS-IS body order: guards → reveal N → partition ALL by the condition (no player selection) → matched per
Mode (AddHand / Discard(reveal-stamped trash) / Custom per-card coroutine — AS-IS handles only these three) →
`revealedCardsCoroutine` (BEFORE the remaining routing — the OPPOSITE order of RevealDeckTopCardsAndSelect,
both mirrored exactly) → unmatched per `RemainingCardsPlace` → `refSelectedCards` out-list appended.

### 9. Dropped-param rows
- `BlitzProcess` (5): AS-IS-signature overload delegating to the verified substrate gate+offer
  (`CanActivateBlitz` + `EffectDrivenAttack.RequestChoice`, player+any-Digimon, AS-IS defaults).
  **Design item RD-W3-7**: (a) `activateClass` — AS-IS threads it into `CanAttack(activateClass)`/
  `SelectAttackEffect.SetUp(cardEffect:)`, so cause-conditioned "can't attack" restrictions would see the true
  causing effect; the substrate gate/offer has no causing-effect input (latent — no cause-conditioned attack
  restriction producer exists on the mirror today). (b) `beforeOnAttackCoroutine` — the substrate effect
  attack is a DEFERRED choice with no pre-OnAttack hook; a non-null hook THROWS (STOP) instead of running at
  the wrong time. Sole AS-IS caller passing it: ST13_06.
- `ActivateMainOfOptionSide` (1): `afterMainEffect` threaded (runs after the [Main] resolution with
  `activateClass`, AS-IS pass-through semantics). **Design item RD-W3-5**: AS-IS stamps the resolved [Main]
  instance with `SetIsDigimonEffect(asEffectOfThisDigimon)`/`SetIsTamerEffect(false)`; the resolver constructs
  the instance itself with no stamping hook — `asEffectOfThisDigimon: true` THROWS (STOP), the default-false
  path's residual (no explicit false-override of factory-set flags) is recorded. The sole AS-IS caller
  (BT25_104) uses the defaults.
- `PlaceDelayOptionCards` (182): AS-IS `SelectCardEffect.Root` overload added WITHOUT a default on `root`
  (deliberate signature deviation): the substrate overload `(CardSource, ICardEffect?, ChoiceZone=Execution)`
  already accepts the 2-arg AS-IS call shape, so a defaulted third parameter would make every 2-arg call
  CS0121-ambiguous. The 3-arg overload applies the wrapper-side `CanEnterFieldByEffect` gate; 2-arg calls bind
  to the substrate directly, whose body still discards `cardEffect` before `CanPlayAsNewPermanent` —
  pre-existing substrate gap, folded into design item RD-W3-2.
- `DrawAndDiscardCards` (3): AS-IS signature restored incl. the 4 dropped params. No-advanced-params calls
  delegate to the verified substrate unchanged; otherwise the wrapper stages the same DrawCards mutation, then
  runs the hand-discard selection with the full AS-IS panel semantics (shared helper with row 2 —
  `byPreSelecetedList` incremental loop / `canEndSelectCondition` SelectionValidator), stages the same
  TrashCard mutations, and runs `afterSelectPermanentCoroutine` with the ACTUALLY-discarded cards (invoked
  only when the select phase ran — ≥1 discardable candidate — mirroring SelectHandEffect's callback site).
  `card` is dead in the AS-IS body (kept, discarded); `isShowOpponent` is UI-only (kept, discarded, commented).

### 10. DNADigivolveWithHandOrTrashCardIntoHandOrTrash (2) — STOP kept, AS-IS-signature declaration
Declared at the full AS-IS signature (all 11 params, delegates translated) so verbatim cards COMPILE, with the
body throwing `NotSupportedException` and a doc-comment carrying the full rationale: the AS-IS body plays a
TEMPORARY throwaway permanent mid-resolution (PlayTempPermanent / CreateNewPermanent) for joint evaluation,
with rollback on failure — no headless substrate surface; any approximation (e.g. evaluating the material
in-hand) would change joint-evaluation/trigger semantics (no-simplification rule).

## Verification

Declaration gate (the batch's contract):

```
dotnet build src/HeadlessDCGO.Engine/HeadlessDCGO.Engine.csproj -clp:ErrorsOnly 2>&1 \
  | grep -oE 'error CS[0-9]+' | sort | uniq -c
     59 error CS0246        # identical to baseline; no CS0111 / CS8954 / CS0101 / CS0121
```

**Gate-strength finding (important for future batches)**: the 59 baseline CS0246s are DECLARATION-phase errors
(IActivatedCardEffect in base lists), and Roslyn suppresses ALL method-BODY binding diagnostics while the
declaration phase has errors — the baseline gate therefore verifies signatures only, not bodies (empirically
proven: a probe file calling a nonexistent method built "clean" at 59). Several pre-existing "masked-verbatim"
files (e.g. PlayCardClass.cs's `card.CanPlayCardTargetFrame` — declared NOWHERE in the mirror) ride on this.
To actually verify THIS batch's bodies, a temporary shim declaring an empty `IActivatedCardEffect` was added,
letting the compiler bind method bodies project-wide (~700 latent body errors surfaced across the old
masked-verbatim corpus, as expected for the intentionally-RED window), and the error list was filtered to the
W3 files: **zero body-binding errors in all six W3-touched files** — every symbol the new bodies call exists
with the used signature. The shim was then deleted and the 59-baseline build re-confirmed.

## Design items (RD-W3-n)

- **RD-W3-1** — reveal-select incremental loop cannot reproduce the AS-IS panel's "un-pick to satisfy
  canEndSelectCondition at maxCount" corner (unreachable for prefix-monotone AS-IS conditions).
- **RD-W3-2** — `CanEnterFieldByEffect` scan surfaces pre-flip: player-scope region ② has no scannable
  surface; region ①'s inherited/granted-scope producers likewise; and the substrate `PlaceDelayOptionCards`
  2-arg path / substrate `CanPlayAsNewPermanent` still discard `cardEffect` internally (pre-existing,
  documented there). Currently exact behaviour (zero ICanNotPutFieldEffect producers on the mirror); resolve
  at the flip (GetContinuousEffects scan) or when the first producer is ported.
- **RD-W3-3** — `PlayToken(CEntity_Base, …)` not declarable (no mirror CEntity_Base class; zero direct card
  callers).
- **RD-W3-4** — token-play `CanEnterField(activateClass)` half needs a substrate-side hook (token instance
  does not exist at wrapper gate time).
- **RD-W3-5** — `ActivateMainOfOptionSide` flag stamping (`SetIsDigimonEffect`/`SetIsTamerEffect`) on the
  resolver-constructed [Main] instance is unthreadable; `asEffectOfThisDigimon: true` STOPs.
- **RD-W3-6** — substrate `DNADigivolvePermanentsIntoHandOrTrashCard` discards `payCost` at runtime
  (predicate-form DNA = cost-0 by design; cost carried by recipes) — behaviour nuance, centralised in the
  substrate's own doc.
- **RD-W3-7** — `BlitzProcess`: causing-effect threading into the attack gate/offer absent (latent);
  `beforeOnAttackCoroutine` STOPs (no pre-OnAttack hook on the deferred effect-attack choice; ST13_06).
