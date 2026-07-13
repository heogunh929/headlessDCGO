# Effect-model rebuild — P5 CanUseEffects (trigger-gate) port: notes / missing / collisions

Scope: 1:1 verbatim port of the 41 AS-IS `DCGO/.../CardEffectCommons/CanUseEffects/**/*.cs` files
into the mirror stubs at `src/.../Script/CardEffectCommons/CanUseEffects/**/*.cs`. New Hashtable-based
overloads (`CanTrigger*/CanActivate*(Hashtable, ...)`) coexist with the pre-existing ctx-based versions in
`CardEffectCommons.cs` (different first param → distinct overloads). Substrate adaptations only.

## Adaptation applied (behavior preserved)
- `card.PermanentOfThisCard()` (mirror returns `PermanentView`) → `ICardEffect.ResolvePermanentOfThisCard(card)`
  (returns `Permanent`), where AS-IS treats the result as a `Permanent`. Sites:
  - CanSuspend.cs `CanActivateSuspendCostEffect` (REMOVED — see collisions below; the surviving
    `CanActivatePermanentSuspendCostEffect` takes a `Permanent` directly, no adaptation needed).
  - OnReturnLibraryBottomDigivolutionCards.cs `CanTriggerOnReturnToLibraryBottomDigivolutionCard`
    (`Permanent == card.PermanentOfThisCard()`).
  - OnTrashBySelfDigiBurst.cs `CanTriggerOnTrashBySelfDigiBurst`
    (`cardEffect.EffectSourceCard.PermanentOfThisCard().cardSources.Contains(card)`).
  - OptionEffect.cs `CanDeclareOptionDelayEffect` (REMOVED — see collisions; adaptation was on
    `card.PermanentOfThisCard().EnterFieldTurnCount`).
  - WhenPermanentWouldDigivolve.cs `CanTriggerWhenPermanentWouldDigivolveOfCard`
    (`permanent == card.PermanentOfThisCard()`).
- Stripped `using UnityEngine;` from all files; stripped `using UnityEditor.Rendering;` from OnDeletion.cs.
- Added `using HeadlessDCGO.Engine.Assets.Scripts.Script;` to the 4 files referencing `SelectCardEffect.Root`
  (WhenWouldLink.cs, PermanentEnterField/OnPlay.cs, PermanentEnterField/PermanentEnterField.cs,
  PermanentEnterField/WhenDigivolving.cs).

## CS0111 collisions — 3 AS-IS methods already reimplemented in CardEffectCommons.cs (identical signature)
These 3 AS-IS gate methods do NOT take a `Hashtable`/`ctx` first parameter, so they cannot coexist as an
overload with the versions ALREADY present in `CardEffectCommons.cs`. Those pre-existing versions are
SUBSTRATE REIMPLEMENTATIONS (headless: `ContinuousRestrictionGate`, `DigivolutionStackReader`,
`enteredThisTurn` metadata, `IsSuspended(...)`), NOT byte-for-byte AS-IS. Their own docstrings cite the same
AS-IS source lines. Because the hard rule forbids touching `CardEffectCommons.cs`, the verbatim duplicate was
REMOVED from the P5 file (a documenting comment left in place) to satisfy "verify no CS0111".

| AS-IS method | signature | pre-existing (kept) | P5 file action |
|---|---|---|---|
| `CanActivateSuspendCostEffect(CardSource, bool=false)` | CanSuspend.cs:10 | CardEffectCommons.cs:4180 | removed from CanSuspend.cs (kept `CanActivatePermanentSuspendCostEffect`) |
| `CanUnsuspend(Permanent)` | CanUnsuspend.cs:10 | CardEffectCommons.cs:4228 (`Permanent?`) | removed from CanUnsuspend.cs (file has only this method) |
| `CanDeclareOptionDelayEffect(CardSource)` | OptionEffect.cs:27 | CardEffectCommons.cs:4213 | removed from OptionEffect.cs (kept `CanTriggerOptionMainEffect`) |

BEHAVIORAL RISK / FLAG: the pre-existing 3 implementations are substrate reimplementations, not verbatim
AS-IS. If the P5 rebuild intends the CanUseEffects files to hold the canonical verbatim gate logic, the human
must reconcile these 3 (replace the CardEffectCommons.cs substrate versions with the verbatim AS-IS bodies, or
confirm the substrate versions are behavior-equivalent). Until then the substrate versions remain live.

## Verbatim-missing members (kept verbatim; may surface as masked body-level errors)
None at the type/signature level (all signature types resolve). Body-level members read verbatim and, if
absent on the mirror, remain masked per the intentionally-RED build. Notable verbatim member usages kept
as-is: `GManager.instance.turnStateMachine.TurnCount` (OptionEffect — in the REMOVED method),
`IBattle.hashtable`, `Permanent.IsDestroyedByBattle`, `CardSource.ContainsTraits/IsDigiEgg/IsBeingRevealed/
PermanentJustBeforeRemoveField/IsToken`, `ICardEffect.EffectDiscription/EffectSourcePermanent`, and the
`List<T>.Filter/.Some` AS-IS extension methods (present in IEnumerableExtension.cs).
