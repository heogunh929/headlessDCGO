// Source: DCGO/Assets/Scripts/CardEffect/BT1/Blue/BT1_039.cs
// R5-A re-port (RD-R6-01 / RD-P8-01 resolved): old-model ActivatedEffect -> new-model ActivateClass now that the
// mirror SelectHandEffect exists (Script/SelectHandEffect.cs).
//   [When Attacking][Twice Per Turn] You can unsuspend this Digimon by trashing 3 cards in your hand.
// AS-IS structure kept verbatim: inline `new ActivateClass()` + SetUpICardEffect/SetUpActivateClass(..., 2, true, ...)
// (ORDER 2 = twice per turn, optional) + SetHashString("Unsuspend_BT1_039") (BT1_039.cs:17-71). The AS-IS
// ActivateCoroutine (:46-71) is mirrored 1:1: SelectHandEffect(Mode.Discard, maxCount = Min(3, hand)) as a COST,
// then IUnsuspendPermanents(this Digimon).
// Substrate translations only: IEnumerator->Task, StartCoroutine->await; `GManager.instance.GetComponent<
// SelectHandEffect>()` (bridge W4); `card.Owner.HandCards.Count` -> new Player(ctx, owner).HandCards.Count;
// `card.PermanentOfThisCard()` -> ICardEffect.ResolvePermanentOfThisCard(card); `IUnsuspendPermanents(list,
// activateClass)` -> the mirror ctor (cause = effect-source id).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Blue;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class BT1_039 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Unsuspend this Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 2, true, EffectDiscription());
            activateClass.SetHashString("Unsuspend_BT1_039");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Attacking][Twice Per Turn] You can unsuspend this Digimon by trashing 3 cards in your hand.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnAttack(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (new Player(card.Context, card.Owner).HandCards.Count >= 3)
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                int discardCount = Math.Min(3, new Player(card.Context, card.Owner).HandCards.Count);

                SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();

                selectHandEffect.SetUp(
                    selectPlayer: card.Owner,
                    canTargetCondition: (cardSource) => true,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    maxCount: discardCount,
                    canNoSelect: false,
                    canEndNotMax: false,
                    isShowOpponent: true,
                    selectCardCoroutine: null,
                    afterSelectCardCoroutine: null,
                    mode: SelectHandEffect.Mode.Discard,
                    cardEffect: activateClass);

                await selectHandEffect.Activate();

                Permanent selectedPermanent = ICardEffect.ResolvePermanentOfThisCard(card);

                await new IUnsuspendPermanents(new List<Permanent>() { selectedPermanent }, activateClass.EffectSourceCard?.InstanceId).Unsuspend();
            }
        }

        return cardEffects;
    }
}
