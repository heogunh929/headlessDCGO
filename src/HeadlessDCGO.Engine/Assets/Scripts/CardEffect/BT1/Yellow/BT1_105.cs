// Source: Assets/Scripts/CardEffect/BT1/Yellow/BT1_105.cs
//   [Main] Change the original DP of 1 of your opponent's Digimon to 3000 until the end of your opponent's
//   next turn.
// AS-IS: ActivateClass on EffectTiming.OptionSkill (single branch — no [Security] block in this file).
//   CanUseCondition = CanTriggerOptionMainEffect(hashtable, card)                    [ported: exists,
//                     CardPortingFramework.cs:6360]
//   CanSelectPermanentCondition = IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card) [ported:
//                     exists as the IsOpponentBattleAreaDigimon-style commons used by sibling BT1 cards]
//   ActivateCoroutine: maxCount = Math.Min(1, MatchConditionPermanentCount(CanSelectPermanentCondition));
//   SelectPermanentEffect.SetUp(mode: Custom, maxCount, canNoSelect:false, canEndNotMax:false) — since
//   maxCount<=1 here, canEndNotMax's value is moot (minCount == maxCount either way per
//   SelectPermanentEffect.cs), so the mandatory-pick shape is NOT the blocker for this card (unlike
//   BT1_061's maxCount=2 case). For the one selected permanent: CardEffectCommons.ChangeBaseDigimonDP(
//   targetPermanent, changeValue: 3000, effectDuration: UntilOpponentTurnEnd, activateClass) — this SETS
//   the base/original DP to the absolute value 3000 (delta computed internally as 3000 - current BaseDP),
//   not a fixed delta buff.
//
// STOP: no headless activated-effect primitive exists for "select target(s) then SET each one's base DP to
// an ABSOLUTE value" (as opposed to applying the same fixed delta to every target). `CardEffectCommons.
// ChangeBaseDigimonDP(Permanent?, int changeValue, EffectDuration, CardSource)` IS ported (CardPortingFramework.cs
// line ~8894) and correctly mirrors the AS-IS "set to X" semantics (computes `delta = changeValue -
// targetPermanent.BaseDP` per call) — the gap is entirely in the SELECT+APPLY glue that would invoke it per
// chosen target. The only matching-shape select+DP factory, `CardEffectFactory.SelectAndBuffDpEffect`
// (-> `ActivatedTargetBuffEffect`, CardPortingFramework.cs:5717/2395), hardcodes a single FIXED `ChangeValue`
// applied identically as a delta to every selected target (`ApplyBuff`, CardPortingFramework.cs:2429) — it
// has no per-target "read current BaseDP, compute delta to reach an absolute value" step, and it always
// writes `ModifierHelpers.DpDeltaKey` (current-DP delta), never `BaseDpDeltaKey` (base-DP delta) — so it
// cannot express "set the original DP to 3000" even for maxCount=1. Grepped (1) `SelectAndBuffDpEffect` /
// `ActivatedTargetBuffEffect` — delta-only, confirmed above; (2) `SelectAndSetBaseDp|SelectAndSetOriginDp|
// SelectAndChangeBaseDp|ActivatedTargetSetEffect|ActivatedSelectCustom` across Assets/Scripts — zero hits, no
// generic "select then run an arbitrary per-target action" activated-effect factory exists either. The
// dispatch side (`ActivatedEffectResolver.cs`, a `switch (cardEffect)` over concrete `IActivatedCardEffect`
// types, e.g. the `case ActivatedTargetBuffEffect targetBuff:` arm at line 277) would also need a new case
// for any new class. Building either the new activated-effect class or the resolver case is an engine-file
// change (CardPortingFramework.cs / ActivatedEffectResolver.cs) — out of scope for a per-card pass and
// prohibited by the porting rules (no new primitives / no engine edits during card porting). No cardEffects
// registered for this card (single-branch card; its only timing is fully blocked). — 강모델
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT1_105 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        // STOP: [Main] "change the original DP of 1 opponent Digimon to 3000 until end of opponent's next
        // turn" — needs a select-then-SET-absolute-base-DP activated-effect primitive that does not exist
        // yet (SelectAndBuffDpEffect/ActivatedTargetBuffEffect only apply a fixed delta to current DP via
        // DpDeltaKey, never an absolute-value set via BaseDpDeltaKey). See file header.

        return cardEffects;
    }
}
