// Source: DCGO/Assets/Scripts/CardEffect/ST3/Yellow/ST3_01.cs
// TRUE AS-IS-verbatim re-port (ST3 Yellow batch). 1:1 mirror of the original ST3_01 (ST3/Yellow).
//   [Your Turn][Once Per Turn] When an opponent's Digimon is deleted by dropping to 0 DP, this Digimon
//   gets +1000 DP for the turn.
// Replaces the PREVIOUS pass's old-model `CardEffectFactory.SelfDpBuffTriggerEffect(...)` call (an invented
// helper with no AS-IS counterpart) with the literal AS-IS inline `new ActivateClass()` structure.
// AS-IS structure kept verbatim: inline ActivateClass, SetIsInheritedEffect(true), SetHashString.
// Substrate translation only: IEnumerator->Task, `ContinuousController.instance.StartCoroutine(X)`->`await X`;
// `card.PermanentOfThisCard()` used as a `Permanent` argument -> `ICardEffect.ResolvePermanentOfThisCard(card)`.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST3.Yellow;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class ST3_01 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("DP +1000", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, false, EffectDiscription());
            activateClass.SetIsInheritedEffect(true);
            activateClass.SetHashString("DP+1000_ST3_01");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Your Turn][Once Per Turn] When an opponent's Digimon is deleted by dropping to 0 DP, this Digimon gets +1000 DP for the turn.";
            }

            bool PermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.IsOwnerTurn(card))
                    {
                        if (CardEffectCommons.CanTriggerOnPermanentDeleted(hashtable, PermanentCondition))
                        {
                            if (CardEffectCommons.IsDPZeroDelete(hashtable))
                            {
                                return true;
                            }
                        }
                    }
                }

                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    return true;
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await CardEffectCommons.ChangeDigimonDP(
                    targetPermanent: ICardEffect.ResolvePermanentOfThisCard(card),
                    changeValue: 1000,
                    effectDuration: EffectDuration.UntilEachTurnEnd,
                    activateClass: activateClass);
            }
        }

        return cardEffects;
    }
}
