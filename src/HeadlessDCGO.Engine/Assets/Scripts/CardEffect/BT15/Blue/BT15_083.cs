// Source: DCGO/Assets/Scripts/CardEffect/BT15/Blue/BT15_083.cs — a Tamer.
// P8 CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass) of the [Your Turn] OnAddHand branch
// (F1-Tier1 OnAddHand SELF + CAUSE witness):
//   * [Your Turn] When one of your Digimon's effects adds cards to your hand, by suspending this Tamer, gain 1
//     memory. — AS-IS `new ActivateClass()` + SetUpActivateClass(..., -1, true, ...) (uncapped, isOptional true =
//     "by suspending"). CanUse = IsExistOnBattleArea && IsOwnerTurn && CanTriggerWhenAddHand(player == card.Owner,
//     CAUSE = IsOwnerEffect && IsDigimonEffect). CanActivate = IsExistOnBattleArea && CanActivateSuspendCostEffect.
//     Body = SuspendPermanentsClass(self).Tap() then card.Owner.AddMemory(1) (BT15_083.cs:83-146).
//   * SecuritySkill -> PlaySelfTamerSecurityEffect (self-Tamer security play, unchanged factory).
// Substrate translations only: IEnumerator->Task, StartCoroutine->await; `player => player == card.Owner` ->
// `player.PlayerId == card.Owner` (Hashtable-overload playerCondition is a mirror Player); AS-IS `new
// SuspendPermanentsClass(new List<Permanent>{ card.PermanentOfThisCard() }, CardEffectHashtable(activateClass))` ->
// the mirror ctor with `ICardEffect.ResolvePermanentOfThisCard(card)` (ST16_14 idiom).
//
// STOP / design item RD-R6-05 (BT15_083 [On Play] reveal): the AS-IS OnEnterFieldAnyone effect (BT15_083.cs:14-80:
// "reveal top 3, add 1 [Gabumon]/[Garurumon] to hand, return the rest to the deck bottom") is NOT re-housed in the
// new model. It needs (a) `CardSource.HasGarurumonName` — a printed-name-family predicate that has NO mirror surface
// (only HasSameCardName exists), and (b) the AS-IS `SimplifiedRevealDeckTopCardsAndSelect` COROUTINE, whose mirror is
// an `IActivatedCardEffect` factory (not an awaitable coroutine), so it cannot be `await`-ed inside an ActivateClass
// body. Both are primitive/infra gaps (invention forbidden); the branch stays OMITTED, orthogonal to the OnAddHand
// bridge under test.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT15.Blue;

using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class BT15_083 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Your Turn
        if (timing == EffectTiming.OnAddHand)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Memory +1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Your Turn] When one of your Digimon's effects adds cards to your hand, by suspending this Tamer, gain 1 memory.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.IsOwnerTurn(card))
                    {
                        bool CardEffectCondition(ICardEffect cardEffect)
                        {
                            if (CardEffectCommons.IsOwnerEffect(cardEffect.EffectSourceCard, card))
                            {
                                if (cardEffect.IsDigimonEffect)
                                {
                                    return true;
                                }
                            }
                            return false;
                        }

                        if (CardEffectCommons.CanTriggerWhenAddHand(
                            hashtable,
                            player => player.PlayerId == card.Owner,
                            CardEffectCondition))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.CanActivateSuspendCostEffect(card))
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await new SuspendPermanentsClass(new List<Permanent>() { ICardEffect.ResolvePermanentOfThisCard(card) }, activateClass, isBlock: false).Tap();

                await card.Owner.AddMemory(1, activateClass);
            }
        }
        #endregion

        //Security Effect
        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.PlaySelfTamerSecurityEffect(card));
        }

        return cardEffects;
    }
}
