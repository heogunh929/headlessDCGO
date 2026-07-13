# Bridge wrapper batch W2 — notes

Implements docs/audit/effect_model_rebuild_design_2026-07-13.md §11.11 rule set, batch W2 (sonnet):
Group A (the 7 W1-deferred `Func<ICardEffect,bool>` rows) + Group B (the `*AndProcessAccordingToResult`
family + list-adapter rows), sourced from docs/audit/mutation_helper_bridge_map.md. Continues the W1
conventions (docs/audit/rebuild_bridge_w1_notes.md): AS-IS-path files, `static partial class CardEffectCommons`,
wrapper body = one call into the existing verified substrate overload with `activateClass?.EffectSourceCard`
swapped in for `sourceCard`, brace-scoped namespaces where a file already has a conflicting file-scoped one.

## Group A — 7 wrappers (`Func<ICardEffect,bool>` adaptation)

Files (all previously bare golem7.5 skeleton stubs, "TODO: Skeleton only" — filled, not appended):
- `GiveEffect/GiveEffectToPermanent/CanNotBeDeletedByEffect.cs` → `GainCanNotBeDeletedByEffect`
- `GiveEffect/GiveEffectToPermanent/CanNoReturnToDeck.cs` → `GainCanNotReturnToDeck`
- `GiveEffect/GiveEffectToPermanent/CanNotReturnToHand.cs` → `GainCanNotReturnToHand`
- `GiveEffect/GiveEffectToPermanent/ImmuneFromDPMinus.cs` → `GainImmuneFromDPMinus`
- `GiveEffect/GiveEffectToPlayer/CanNoReturnToDeck.cs` → `GainCanNotReturnToDeckPlayerEffect`
- `GiveEffect/GiveEffectToPlayer/CanNotReturnToHand.cs` → `GainCanNotReturnToHandPlayerEffect`
- `GiveEffect/GiveEffectToPlayer/ImmuneFromDPMinus.cs` → `GainImmuneFromDPMinusPlayerEffect`

### Design item RD-W2-1: `Func<ICardEffect,bool>` → `Func<CardSource,bool>` adapter

**The gap.** AS-IS's `cardEffectCondition` param on all 7 helpers is `Func<ICardEffect,bool>` — it tests the
CAUSING EFFECT INSTANCE (e.g. `cardEffect.IsTamerEffect`, `cardEffect.EffectSourceCard`). The substrate's
`causingEffectPredicate` gate (`MatchStateMutationSink.IsRestrictedFromCause` /
`ContinuousAndRestrictionEffects`'s `CausingEffectPredicate`) only ever supplies the causing effect's SOURCE
CARD as a bare `CardSource` at gate-evaluation time (confirmed by reading `MatchStateMutationSink.cs:1277-1289`
and `:1334`, and the `RestrictionScan.IsRestricted(context, key, cardId, causingSourceId)` call it wraps — the
predicate is invoked as `predicate(new CardSource(_context, causingSourceId, causeOwner, causeOwner))`). A real
`ICardEffect` instance therefore cannot be reconstructed at that point — no overload taking the effect-level
predicate exists on the substrate.

**Real-usage survey.** Wrote a small Python scan (not committed) over every AS-IS card-effect file matching
`CardEffectCommons.<one of the 7 helpers>(` and traced each `cardEffectCondition:` argument back to its local
function/lambda body. Found 77 real call sites across the 7 helpers (BT18_062/063, ST17_07, BT8_069, BT15_061,
BT7_064, ST13_14, BT10_105, EX8_070/015/043, BT18_064, BT22_058/059(x4), BT17_038, BT19_051, BT21_074, BT3_105,
P_215, BT24_056, BT23_033/054/044, BT12_084, BT10_068, BT9_098, BT13_072, BT19_089, BT11_069, P_162, BT23_085,
BT16_093/055 — full list reproducible via the same grep-plus-brace-matching approach). Every shape found reduces
to one of:
- `null` (no filter — blocks unconditionally): 8 call sites.
- constant `true` / `cardEffect != null`: a handful.
- `CardEffectCommons.IsOpponentEffect(cardEffect, card)` (inlined or via local `CardEffectCondition`/lambda) —
  i.e. `cardEffect.EffectSourceCard.Owner == card.Owner.Enemy` — the overwhelming majority.
- **One exception**: BT19_089's local `SkillCondition`, passed to `GainImmuneFromDPMinus`:
  `cardEffect.EffectSourceCard.Owner == card.Owner.Enemy && (!cardEffect.IsDigimonEffect || !cardEffect.IsTamerEffect)`.
  `IsDigimonEffect`/`IsTamerEffect` are per-instance flags on `ICardEffect` (`ICardEffect.cs:602,619`, settable via
  `SetIsDigimonEffect`/`SetIsTamerEffect`, both default `false` per `SetUpICardEffect`'s own ctor-flow) — they are
  NOT derivable from a bare `CardSource`.

**The fix.** `AdaptCardEffectCondition` (defined once, in `GiveEffectToPermanent/CanNotBeDeletedByEffect.cs`,
shared by all 7 files via the same `static partial class CardEffectCommons`) invokes the REAL AS-IS predicate
delegate — unchanged, not re-implemented — against a minimal cause-effect carrier: `new ActivateClass()` (the
existing, already-1:1-ported, most-generic `ICardEffect` subclass) with `SetUpICardEffect("(RD-W2-1 bridge
cause-effect carrier)", _ => true, causingCard)` called on it. This sets `EffectSourceCard = causingCard` and
leaves every other `ICardEffect` flag at its own honest ctor-default (`false`) — the SAME defaults
`SetUpICardEffect` itself assigns for a freshly-constructed real effect, before any factory customizes it. This
is faithful (not a re-implementation/simplification of the predicate's logic) for every confirmed real-usage
shape above, since all of them read only `EffectSourceCard`-derived data or nothing at all — and it stays
correct automatically for any FUTURE predicate of that same shape, because the real delegate is genuinely
invoked, not pattern-matched.

**Residual gap.** It is lossy for a predicate that also inspects a flag never set on the carrier. The one
confirmed instance is BT19_089's `SkillCondition` (not yet ported — this residual only fires if/when that card
is ported verbatim against this bridge): with both `IsDigimonEffect`/`IsTamerEffect` defaulting `false`, the
adapted predicate answers `true` whenever `Owner == Enemy`, over-approving (dropping the "excludes an effect
flagged as both Digimon- and Tamer-effect" refinement — in practice this only matters for a causing effect that
is BOTH digimon- and tamer-scoped simultaneously, a narrow real-world case). Whoever ports BT19_089 needs either
(a) a bespoke non-bridged call for that one site, or (b) to extend the carrier construction to thread the real
flags through if the calling context can supply them (it currently cannot, since the gate only has a
`CardSource`). Flagged here rather than silently accepted.

The map's own text under-flagged this: it explicitly called out `GainCanNotBeDeletedByEffect` and
`GainCanNotReturnToDeck` with a ⚠️, but not `GainCanNotReturnToHand`, `GainImmuneFromDPMinus`, or either
`*PlayerEffect` sibling — direct inspection confirms all 7 share the identical `Func<ICardEffect,bool>` shape
and therefore the identical adapter/gap (this is the same map-completeness note W1 already flagged and W2
resolves for all 7).

## Group B — 9 wrappers (`*AndProcessAccordingToResult` family + `SelectTrashDigivolutionCards`)

Per the task brief's explicit scope: every `*AndProcessAccordingToResult` row except the two W3-deferred,
incompatible-shape rows (`TrashDigivolutionCardsAndProcessAccordingToResult`,
`TrashDigivolutionCardsFromTopOrBottom` — left untouched in `TrashDigivolutionCards.cs`, noted in that file's
header), plus `SelectTrashDigivolutionCards` (the "SelectTrash*" list-adapter row).

### File placement note

8 of the 9 AS-IS methods (`Bounce`/`DeckBounce`/`Delete`/`Suspend`/`PlacePermanentInSecurity`/`TrashHand`/
`TrashLink`/`TrashSecurity`...AndProcessAccordingToResult) live directly in the AS-IS MONOLITH
`DCGO/Assets/Scripts/Script/CardEffectCommons.cs` (lines 437-644) — NOT in a per-method subfolder file the way
every Group-A / W1 row was. The mirror's own substrate translations of these same 8 methods already live in the
mirror's `Script/CardEffectCommons.cs` (its "W6-S" region, lines ~104-485) — the file this batch's rules forbid
editing. Since AS-IS itself has no separate subfolder skeleton for these 8 (there's nothing to "fill"), and the
mirror's own `Script/CardEffectCommons.cs` header explicitly documents that AS-IS's `partial class
CardEffectCommons` is ALREADY split across sibling files under `Script/CardEffectCommons/` (e.g.
`TrashDigivolutionCards.cs`, `TrashLinkedCards.cs`, `RevealLibrary.cs` — real AS-IS subfolder files, not
mirror inventions), this batch adds ONE MORE sibling partial-class file for exactly this bridge family:
`Script/CardEffectCommons/ProcessAccordingToResultBridge.cs`. It does not touch the existing
`Script/CardEffectCommons.cs`. The 9th method, `SelectTrashDigivolutionCards`, has a genuine AS-IS subfolder
home (`CardEffectCommons/TrashDigivolutionCards.cs`) and was filled there instead (leaving the two W3-deferred
methods in that same AS-IS file untouched, documented in its header).

### Per-method notes

- **`BouncePeremanentAndProcessAccordingToResult` / `DeckBouncePeremanentAndProcessAccordingToResult`**:
  AS-IS's `successProcess`/`failureProcess` are bare `IEnumerator` instances (no `Func<>` wrapper at all in the
  AS-IS signature) — a C# iterator method call does not run ANY body code until explicitly driven (`MoveNext`),
  so these are "inert, not-yet-started" references, not "already-running" ones despite the map's "already-started"
  phrasing. The faithful C#-native translation of an inert coroutine reference is a deferred `Func<Task>` factory
  — which the substrate already expects verbatim, so these two are a straight pass-through (only
  `ICardEffect`→`CardSource` extraction, no delegate adapter needed).
- **`DeletePeremanentAndProcessAccordingToResult` / `SuspendPeremanentAndProcessAccordingToResult`**: AS-IS
  already declares `successProcess` as `Func<List<Permanent>,IEnumerator>` (a real factory); substrate payload
  is the exact destroyed/suspended `Permanent` views (not a mere count) — clean 1:1, only
  `List`↔`IReadOnlyList`/`.ToList()` needed.
- **`PlacePermanentInSecurityAndProcessAccordingToResult`**: AS-IS param order differs from the substrate
  (`activateClass` precedes `toTop` in AS-IS; substrate takes `sourceCard` after `toTop`) — a pure reorder in
  the wrapper, no payload adapter (substrate's `successProcess` is already `Func<CardSource,Task>`, an exact
  match for AS-IS's placed-card payload).
- **`TrashHandAndProcessAccordingToResult`**: AS-IS's dead `Player player`/`Hashtable hashtable` params (never
  read in the AS-IS body) are KEPT in the wrapper signature (not dropped) so any AS-IS-verbatim card that still
  passes them positionally keeps compiling — they are simply discarded (`_ = player; _ = hashtable;`) before
  delegating. AS-IS types `activateClass` as the concrete `ActivateClass` (not `ICardEffect`) — widened to
  `ICardEffect` for this batch's uniform convention (every real caller constructs a genuine `ActivateClass`,
  which IS-A `ICardEffect`, so this is a safe widening, matching the map's own "minor outlier, no behavioral
  implication found" note). The substrate's `successProcess` takes no payload, but AS-IS's takes the trashed
  `CardSource` — since this method only ever attempts to trash the SAME `cardToTrash` the caller supplied,
  re-supplying `cardToTrash` on success is exact (it IS the trashed card), not a guess.
- **`SelectTrashDigivolutionCards`**: clean 1:1 — substrate's `afterSelectionCoroutine` already hands back the
  exact selected/trashed `CardSource` list; only `List`↔`IReadOnlyList` needed.

### Design item RD-W2-2: `int`/count payload → AS-IS `List<CardSource>` payload reconstruction

**The gap.** `TrashLinkCardsAndProcessAccordingToResult` and `TrashSecurityAndProcessAccordingToResult`'s
substrate success payload is an `int` (trashed count / diff), but AS-IS's `successProcess` expects the actual
`List<CardSource>` of cards that ended up trashed. Per the task brief: "if the substrate can't supply the list,
reconstruct if cheaply possible from context ... NO silent drop." A naive "return the full input list on any
success" would be WRONG whenever a real partial failure occurs within the batch (e.g. one link card is
protected, another isn't — `TrashLinkCardsAndProcessAccordingToResult`'s substrate body loops
`RemoveLinkCardAsync` per id and can succeed on some, fail on others).

**The fix (faithful, not a guess).**
- `TrashLinkCardsAndProcessAccordingToResult`: after the substrate call, re-check each of the caller's original
  `targetLinkCards` candidates against the actual post-mutation Trash-zone membership
  (`zones.GetCards(candidate.Owner, ChoiceZone.Trash).Contains(candidate.InstanceId)`) — the exact same kind of
  "did it actually land" evidence the substrate itself uses internally to compute its own count, just re-applied
  per-candidate instead of via a running tally. Real zone-state evidence, not an assumption.
- `TrashSecurityAndProcessAccordingToResult`: snapshot the player's Security zone contents BEFORE calling the
  substrate; on success, diff against the AFTER snapshot (`before.Except(after)`) to recover the exact ids that
  left. The substrate always removes from the specified end (`fromTop`) deterministically (it is itself a
  before/after COUNT diff over the same zone), so this before/after ID diff is exact, not approximate.

Both reconstructions live entirely in the wrapper (reading zone state via the same public `IZoneStateReader`
the substrate itself uses) — no substrate edits were needed or made.

## Final build

Baseline before this batch (per task brief): `59 error CS0246` (all `IActivatedCardEffect`, pre-existing engine
gap). After Group A + Group B, rebuilt clean:

```
dotnet build src/HeadlessDCGO.Engine/HeadlessDCGO.Engine.csproj -clp:ErrorsOnly 2>&1 \
  | grep -oE 'error CS[0-9]+' | sort | uniq -c
     59 error CS0246
```

Identical to baseline. No new declaration errors (no CS0111 duplicate-overload, no CS8954 duplicate file-scoped
namespace, no new CS0246/CS0234/CS0121). One intermediate build (before adding the missing
`using HeadlessDCGO.Engine.Headless.Effects;` to the 7 Group-A files, needed for the unqualified `EffectDuration`
reference) surfaced 7 extra `CS0246` (`EffectDuration` not found) on top of the 59 baseline — fixed by adding
that using directive to all 7 Group-A files (the same import W1's `ChangeDP.cs` reference wrapper already
carries), back down to the 59-baseline.
