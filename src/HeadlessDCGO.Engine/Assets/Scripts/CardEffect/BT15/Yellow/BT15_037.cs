// Source: DCGO/Assets/Scripts/CardEffect/BT15/Yellow/BT15_037.cs
// P8 CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass). Both activated branches are now
// re-housed as AS-IS `new ActivateClass()`:
//   * WhenPermanentWouldBeDeleted -> <Barrier> on self, main (isInheritedEffect:false) + inherited
//     (isInheritedEffect:true). CardEffectFactory.BarrierSelfEffect (unchanged, keyword factory).
//   * [All Turns][Once Per Turn] OnLoseSecurity -> ActivateClass, ORDER 1, mandatory. CanUse =
//     IsExistOnBattleArea && CanTriggerWhenLoseSecurity(player == card.Owner). CanActivate =
//     IsExistOnBattleAreaDigimon. Body = card.Owner.AddMemory(1). (F1-M1 activated-bridge witness.)
//   * OnDiscardSecurity -> ActivateClass, ORDER -1, isOptional true. This RESOLVES the prior pass's STOP: the
//     AS-IS body `PlayPermanentCards(payCost:false, root:Execution, activateETB:true)` bridge is now ported
//     (PlayCardsBridge.cs), so the "play this card from the security trash without paying the cost" branch is
//     re-housed 1:1. CanUse = CanTriggerOnTrashSelfSecurity(cardEffect != null). CanActivate = IsExistOnTrash.
// Substrate translations only: IEnumerator->Task, StartCoroutine->await; `player => player == card.Owner` ->
// `player => player.PlayerId == card.Owner` (the Hashtable-overload playerCondition is a mirror Player).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT15.Yellow;

using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class BT15_037 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.WhenPermanentWouldBeDeleted)
        {
            cardEffects.Add(CardEffectFactory.BarrierSelfEffect(isInheritedEffect: false, card: card, condition: null));
        }

        if (timing == EffectTiming.WhenPermanentWouldBeDeleted)
        {
            cardEffects.Add(CardEffectFactory.BarrierSelfEffect(isInheritedEffect: true, card: card, condition: null));
        }

        #region All Turns
        if (timing == EffectTiming.OnLoseSecurity)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Gain 1 memory.", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[All Turns][Once per turn] When a card is removed from your security stack, gain 1 memory.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.CanTriggerWhenLoseSecurity(hashtable, player => player.PlayerId == card.Owner))
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
                {
                    return true;
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await card.Owner.AddMemory(1, activateClass);
            }
        }
        #endregion

        #region When trashed from security
        if (timing == EffectTiming.OnDiscardSecurity)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Play this card without paying the cost", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "When an effect trashes this card from your security stack, you may play this card without paying the cost.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnTrashSelfSecurity(hashtable, cardEffect => cardEffect != null, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnTrash(card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await CardEffectCommons.PlayPermanentCards(cardSources: new List<CardSource>() { card }, activateClass: activateClass, payCost: false, isTapped: false, root: SelectCardEffect.Root.Execution, activateETB: true);
            }
        }
        #endregion

        return cardEffects;
    }
}
