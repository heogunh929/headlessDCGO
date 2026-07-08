// Source: Assets/Scripts/CardEffect/BT3/Green/BT3_103.cs — an Option (two timing blocks).
// AS-IS:
//   [Main] (OptionSkill) "The next time one of your green Digimon digivolves this turn, you may suspend 1 of
//   your Digimon to reduce the memory cost of the digivolution by 5." ActivateClass(CanUseCondition =
//   CanTriggerOptionMainEffect, ORDER=-1, ISOPTIONAL=false). ActivateCoroutine builds a nested ActivateClass1
//   ("Digivolution Cost -5") whose CanUseCondition1 = CanTriggerWhenPermanentWouldDigivolve(permanentCondition
//   = the digivolving-FROM permanent is an owner battle-area permanent with CardColors containing Green),
//   registered via CardEffectCommons.AddEffectToPlayer(UntilEachTurnEnd, card, getCardEffect) at
//   EffectTiming.BeforePayCost, paired with a background "Remove Effect" cleanup ActivateClass2 (fires on the
//   SAME CanTriggerWhenPermanentWouldDigivolve match, at AfterPayCost, and removes BOTH
//   UntilEachTurnEndEffects entries) — giving the AS-IS "next time only, this turn only" one-shot semantics.
//   ActivateCoroutine1 (when the nested effect actually triggers): interactive SelectPermanentEffect(Mode.Tap,
//   canNoSelect:true) suspending 1 of the owner's OWN Digimon (CanActivateSuspendCostEffect-gated), THEN on a
//   successful pick registers a temporary ChangeCostClass (-5 digivolution cost, gated on ANY currently
//   battle-area target permanent) into card.Owner.UntilCalculateFixedCostEffect for the in-flight cost pass.
//
// STOP (OptionSkill branch — genuine structural primitive gap, not a per-card shortcut; grepped 2x+ per rule 4):
// This needs a digivolution-cost reduction gated on the DIGIVOLVING-FROM permanent's identity (owner
// battle-area + Green color) — the SAME structural gap the BT1_109 sibling documents and this porting unit
// re-confirmed (grepped again): the headless digivolution-cost-resolution pipeline
// (ContinuousModifierGate.ResolveDigivolutionCost(context, cardId, baseCost) — Headless/Runtime/
// ContinuousModifierGate.cs:48, called from DigivolveAction.cs:549 with `cardId` = the NEW (post-digivolve,
// level-up) card only) has no parameter carrying the FROM permanent's identity to any registered cost
// modifier's predicate at all — so a headless effect cannot express "reduce cost only when the
// digivolving-FROM permanent is green", no matter how it is composed. This is compounded (not simplified) by
// two further AS-IS layers with no primitive: (1) the "next time only, this turn only" fire-once player-scope
// grant — CardEffectCommons.AddEffectToPlayer is documented/implemented as a one-shot "fires once at `timing`
// then cleared" primitive (CardPortingFramework.cs:8498, the same BT1_104/BT1_109 finding), not a
// register-then-remove-on-a-DIFFERENT-trigger pairing; and (2) the interactive suspend-cost gate on the
// reduction itself (no IEffectBody in ActivatedEffect.cs composes "select 1 of the owner's own Digimon as a
// cost, then register a temporary cost-reduction for the CURRENT cost calculation"). Per rule 4 this is a
// primitive gap requiring new engine-layer work (extending the digivolution-cost query to also carry the
// FROM-permanent id, plus a suspend-cost-then-register-reduction IEffectBody), out of scope for a single-card
// porting pass. No cardEffects registered for OptionSkill (SecuritySkill is unaffected and fully ported
// below). — 강모델
// if (timing == EffectTiming.OptionSkill) { ... }
//
//   [Security] Add this card to its owner's hand. AS-IS: ActivateClass(CanUseCondition =
//   CanTriggerSecurityEffect, IsSecurityEffect=true) on SecuritySkill, ActivateCoroutine =
//   CardEffectCommons.AddThisCardToHand(card, activateClass) — verbatim factory match
//   (CardEffectFactory.AddThisCardToHandEffect, BT1_108/BT1_112 pattern).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT3_103 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.AddThisCardToHandEffect(card));
        }

        return cardEffects;
    }
}
