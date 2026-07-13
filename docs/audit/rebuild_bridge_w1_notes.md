# Bridge wrapper batch W1 — notes

Implements docs/audit/effect_model_rebuild_design_2026-07-13.md §11.11 rule set, batch W1 (sonnet):
"universal pattern" AS-IS-signature `Task` overloads for the `Gain*`/`Change*` families +
`AddSelfDeleteEffect`/`BecomeDigimonThatCantDigivolve` + `ShowReducedCost` (UI-ONLY no-op), sourced from
docs/audit/mutation_helper_bridge_map.md.

Wrapper form used throughout (rule 1): AS-IS signature kept verbatim (`ICardEffect activateClass`,
`IEnumerator`→`Task`, `List`→`IReadOnlyList` where applicable), body = one call into the existing verified
substrate overload with `activateClass?.EffectSourceCard` swapped in for `sourceCard`, then
`await Task.CompletedTask`. File placement = the AS-IS path (rule 2); all new code lives in the *flat*
`HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons` namespace (not a namespace nested per AS-IS
folder), per the existing convention established across `CardEffectCommons/CanUseEffects/*.cs` and per
§11.3's "AS-IS 경로 stub 채우기" rule — this is required for the new methods to be genuine overloads of the
same `public static partial class CardEffectCommons` type that ported cards already call.

## Wrappers created — 39 methods across 33 files

### GiveEffect/GiveEffectToPermanent/ (12 files, 16 methods)
- `CanNotAttack.cs` → `GainCanNotAttack`
- `CanNotBeAttacked.cs` → `GainCanNotBeAttacked`
- `CanNotBeBlocked.cs` → `GainCanNotBeBlocked`
- `CanNotBeDeletedByBattle.cs` → `GainCanNotBeDeletedByBattle`
- `CanNotBlock.cs` → `GainCanNotBlock`
- `CanNotSuspend.cs` → `GainCanNotSuspend` + `GainCantSuspendUntilOpponentTurnEnd`
- `CanNotUnsuspend.cs` → `GainCanNotUnsuspend` + `GainCantUnsuspendNextActivePhase` + `GainCantUnsuspendUntilOpponentTurnEnd`
- `ChangeDP.cs` → `ChangeDigimonDP`
- `ChangeOriginDP.cs` → `ChangeBaseDigimonDP`
- `ChangeSAttack.cs` → `ChangeDigimonSAttack` (both AS-IS overloads: 4-arg and the `activateAnimation`/`hashstring` 6-arg)
- `DeleteSelf.cs` → `enum DeleteTiming` (re-declared 1:1) + `AddSelfDeleteEffect`
- `TamerBecomesDigimonThatCanNotDigivolve.cs` → `BecomeDigimonThatCantDigivolve`

### GiveEffect/GiveEffectToPlayer/ (9 files, 9 methods)
- `CanNotAttack.cs` → `GainCanNotAttackPlayerEffect`
- `CanNotBeDeletedByBattle.cs` → `GainCanNotBeDeletedPlayerEffect`
- `CanNotBlock.cs` → `GainCanNotBlockPlayerEffect`
- `CanNotSuspend.cs` → `GainCanNotSuspendPlayerEffect`
- `CanNotUnsuspend.cs` → `GainCanNotUnsuspendPlayerEffect`
- `ChangeCardDP.cs` → `ChangeSecurityDigimonCardDPPlayerEffect`
- `ChangeDP.cs` → `ChangeDigimonDPPlayerEffect`
- `ChangePlayCost.cs` → `ChangePlayCostPlayerEffect`
- `ChangeSAttack.cs` → `ChangeDigimonSAttackPlayerEffect`

### KeyWordEffects/ (11 files, 13 methods)
- `Alliance.cs` → `GainAlliance`
- `Barrier.cs` → `GainBarrier`
- `Blocker.cs` → `GainBlocker` + `GainBlockerPlayerEffect`
- `Collision.cs` → `GainCollision`
- `Evade.cs` → `GainEvade`
- `Jamming.cs` → `GainJamming`
- `Pierce.cs` → `GainPierce`
- `Raid.cs` → `GainRaid`
- `Reboot.cs` → `GainReboot`
- `Retaliation.cs` → `GainRetaliation`
- `Rush.cs` → `GainRush` + `GainRushPlayerEffect`

### Root (1 file, 1 method)
- `ShowReducedCost.cs` → `ShowReducedCost(Hashtable)` no-op `Task` (rule 5, UI-ONLY; AS-IS has no
  `activateClass` param at all).

## Files with pre-existing content — appended, not overwritten

7 `KeyWordEffects/*.cs` files (`Alliance`, `Blocker`, `Jamming`, `Pierce`, `Reboot`, `Retaliation`, `Rush`)
already held a *different* AS-IS mirror: a `CanResolveX` partial of `KeywordBaseBatch1Effect` /
`KeywordBaseBatch2Effect` in the **nested** `...CardEffectCommons.KeyWordEffects` namespace (a file-scoped
namespace declaration). Appending a second file-scoped `namespace ...;` block for the flat
`CardEffectCommons` bridge wrapper triggers **CS8954** ("a source file can only contain one file-scoped
namespace declaration"). Fix: converted **both** namespace blocks in those 7 files to classic brace-scoped
`namespace X { ... }` form (semantically identical, just re-enables multiple namespace blocks per file).
Confirmed via full rebuild that this is purely syntactic — no behavior change to the pre-existing
`CanResolveX` bodies.

## Rows skipped as not-universal / deferred

**Deferred to W2 (adapter needed, per §11.11 rule 3 + the design doc's own W1/W2 split — "W2(sonnet)=
*ProcessAccordingToResult+어댑터")** — these AS-IS helpers take `Func<ICardEffect, bool> cardEffectCondition`
(tests the causing **effect instance**) but the mirror substrate only exposes `Func<CardSource, bool>
cardEffectSourceCondition` (tests the causing **source card**). There is no way to reconstruct a real
`ICardEffect` instance from a bare `CardSource` handed back by the sink at gate-evaluation time, so a
correct adapter needs real design work (e.g. a minimal carrier `ICardEffect` subclass), not a thin
type-swap — left as `// TODO: Skeleton only` stubs, untouched:
- `GiveEffectToPermanent/CanNoReturnToDeck.cs` (`GainCanNotReturnToDeck`)
- `GiveEffectToPermanent/CanNotBeDeletedByEffect.cs` (`GainCanNotBeDeletedByEffect`)
- `GiveEffectToPermanent/CanNotReturnToHand.cs` (`GainCanNotReturnToHand`)
- `GiveEffectToPermanent/ImmuneFromDPMinus.cs` (`GainImmuneFromDPMinus`)
- `GiveEffectToPlayer/CanNoReturnToDeck.cs` (`GainCanNotReturnToDeckPlayerEffect`)
- `GiveEffectToPlayer/CanNotReturnToHand.cs` (`GainCanNotReturnToHandPlayerEffect`)
- `GiveEffectToPlayer/ImmuneFromDPMinus.cs` (`GainImmuneFromDPMinusPlayerEffect`)

Note the bridge map's own ⚠️ markers under-flag this: it explicitly calls out
`GainCanNotBeDeletedByEffect` and `GainCanNotReturnToDeck`, but **not** `GainCanNotReturnToHand`,
`GainImmuneFromDPMinus`, or either `*PlayerEffect` sibling — direct inspection of the AS-IS source (all 7
files) confirms all 7 share the identical `Func<ICardEffect, bool>` shape and therefore the identical
adapter gap. Flagging this as a map-completeness gap for whoever does W2.

**Out of scope for this batch (not requested, not part of the Gain*/Change* family definition)** — left
untouched, no stub filled:
- `GiveEffectToPermanent/ChangeLinkMax.cs`, `StartOfMainAttack.cs`
- `GiveEffectToPlayer/ChangeDigivolutionCost.cs`, `IgnoreDigivolutionRequirement.cs`
- `KeyWordEffects/Fortitude.cs` (`FortitudeProcess`/`GainFortitude` — `GainFortitude` isn't even in the
  91-helper map intersection; `FortitudeProcess` is SAME-NAME-DIFF-SIG but not a `Gain*`/`Change*` family
  member, ambiguous whether it belongs in W1 — deferred)
- AS-IS siblings noted inline where relevant (`InvertDigimonSAttackPlayerEffect` in
  `GiveEffectToPlayer/ChangeSAttack.cs`; `CanTriggerEvade`/`CanActivateEvade`/`EvadeProcess` in
  `KeyWordEffects/Evade.cs`; `CanActivateRaid`/`RaidProcess` in `KeyWordEffects/Raid.cs`) — none of these are
  in the bridge map's 91-helper intersection.

## Ambiguous adaptations flagged

- **`AddSelfDeleteEffect` / `DeleteTiming`**: AS-IS enum (`AtTurnEnd`/`AtOwnTurnEnd`/`AtOpponentTurnEnd`) has
  no direct substrate counterpart — the substrate takes a raw `string deleteTiming` consumed by
  `HeadlessEndTurnCleanupFlow`/`PlaySelfAtEndOfBattleSecurityEffect` as `"own"`/`"opponent"`/(anything else,
  read as "each"/any-turn-end). Mapped `AtOwnTurnEnd→"own"`, `AtOpponentTurnEnd→"opponent"`,
  `AtTurnEnd→"each"` (verified against `ActivatedEffects.cs:2570` doc comment: `"own"/"opponent"/"each"`, and
  `HeadlessEndTurnCleanupFlow.cs:66-70`'s switch, whose `_ => true` default matches "each"/any).
- **`ChangeDigimonSAttack` two-overload AS-IS shape**: the substrate already collapsed both AS-IS overloads
  (4-arg, and the `activateAnimation`/`hashstring` 6-arg) into one method with optional params — confirmed
  no ambiguity risk since the 4-arg wrapper and 6-(5-min)-arg wrapper differ in arity, no CS0121 possible.

## Final build

Baseline before this batch: `59 error CS0246` (all `IActivatedCardEffect`, pre-existing engine gap, per
task brief). After this batch, rebuilt clean:

```
dotnet build src/HeadlessDCGO.Engine/HeadlessDCGO.Engine.csproj -clp:ErrorsOnly 2>&1 \
  | grep -oE 'error CS[0-9]+' | sort | uniq -c
     59 error CS0246
```

Identical to baseline — confirmed all 59 are still `IActivatedCardEffect` in
`Assets/Scripts/Script/CardEffectCommons/ActivatedEffect.cs` / `ActivatedEffects.cs` (pre-existing, untouched
by this batch). No new declaration errors (no CS0111 duplicate-overload, no CS8954 duplicate file-scoped
namespace after the KeyWordEffects fix, no new CS0246/CS0234).

An intermediate build (before the file-scoped-namespace fix above) surfaced `54 CS0234 + 203 CS0246 + 7
CS8954` — all traced to the 7 `KeyWordEffects/*.cs` files that already had a file-scoped namespace
declaration; converting both namespace blocks in those 7 files to brace form resolved all of it back to the
59-baseline. Recorded here in case the same pattern recurs in W2/W3 (any AS-IS-path file that already has
filled content in a *different* namespace needs the brace form, not a second `namespace X;`).
