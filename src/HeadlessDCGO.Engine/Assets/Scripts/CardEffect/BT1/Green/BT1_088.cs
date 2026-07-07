// 1:1 mirror of the original BT1_088 (BT1/Green) — a Tamer (mixed timings).
//   [Main] If you have a level 5 or higher green Digimon in play, you can suspend this Tamer to reveal the
//          top card of your deck. If that card is a Digimon card, add it to your hand. Otherwise place it at
//          the bottom of your deck.
//     -> STOP (see below).
//   [Security] Play this Tamer.  -> PlaySelfTamerSecurityEffect (security-skill flow, mirrors ST1_12/ST2_12/
//      ST3_12/ST4_14).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT1_088 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // STOP: [Main] "If you have a level 5+ green Digimon in play, you can suspend this Tamer to reveal the
        // top card of your deck. If that card is a Digimon card, add it to your hand. Otherwise place it at the
        // bottom of your deck." (AS-IS: ActivateClass on OnDeclaration, CanUseCondition = IsExistOnBattleArea(card)
        // && CanActivateSuspendCostEffect(card) && HasMatchConditionOwnersPermanent(card, p => p.IsDigimon &&
        // p.TopCard.CardColors.Contains(CardColor.Green) && p.Level >= 5 && p.TopCard.HasLevel), ORDER=-1
        // [canActivate=null, maxCountPerTurn=null], ISOPTIONAL=false, ActivateCoroutine = suspend/tap THIS card's
        // own permanent [new SuspendPermanentsClass({ card.PermanentOfThisCard() }).Tap()] THEN
        // RevealDeckTopCardsAndProcessForAll(revealCount:1, SimplifiedSelectCardConditionClass(canTarget =
        // cardSource.IsDigimon, mode: AddHand, maxCount:-1), remainingCardsPlace: DeckBottom).)
        //
        // The activated shape is "pay a self-SUSPEND cost, THEN reveal the top deck card and route it (add-to-hand
        // if it matches, else deck-bottom)". No uniform-ActivatedEffect IEffectBody covers this composed shape:
        //   - SuspendSelfAndGainMemoryBody (ActivatedEffect.cs:205) does self-suspend cost -> a FIXED simple
        //     mutation (gain N memory); its follow-up cannot peek the top library card and branch.
        //   - SelectTrashHandThenSelfMutationBody (ActivatedEffect.cs:143) is a hand-trash cost -> single self
        //     mutation — wrong cost and no reveal-route follow-up.
        //   - The reveal-and-route primitive (SimplifiedRevealDeckTopCardsAndSelect / SimplifiedRevealAndSelectEffect,
        //     CardPortingFramework.cs:5518) is a standalone IActivatedCardEffect dispatched by its own switch arm
        //     in ActivatedEffectResolver — it is NOT an IEffectBody, so it cannot be plugged as the follow-up of a
        //     suspend-self body, and it carries no self-suspend cost parameter. Adding it as a separate cardEffect
        //     would drop the suspend cost and the [Main]-activation atomicity (the reveal would fire without paying
        //     / while the cost is unpayable) — infidelity.
        // Grepped ActivatedEffect.cs (all IEffectBody impls) and the reveal factories in CardPortingFramework.cs
        // twice: no "suspend-self cost THEN reveal-top-and-route" body/factory exists. Per the primitive-gap rule
        // this needs a new IEffectBody (e.g. a SuspendSelfThenRevealTopRouteBody mirroring SuspendSelfAndGainMemoryBody's
        // two-sink.Apply shape but with a reveal-route follow-up) — engine-side primitive development, forbidden in
        // this porting pass and deferred to the strong-model primitive pass. Same precedent as BT1_081's STOP
        // ("no cost-then-follow-up-mutation body exists"). — 강모델
        // if (timing == EffectTiming.OnDeclaration) { ... }

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.PlaySelfTamerSecurityEffect(card));
        }

        return cardEffects;
    }
}
