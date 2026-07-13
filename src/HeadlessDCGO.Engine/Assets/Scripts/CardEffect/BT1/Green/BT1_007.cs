// Source: DCGO/Assets/Scripts/CardEffect/BT1/Green/BT1_007.cs
// TRUE AS-IS-verbatim re-port (P5 batch 2). 1:1 mirror of the original BT1_007 (BT1/Green).
//   [When Attacking] If you've digivolved this turn, this Digimon gets +1000 DP for the turn.
// AS-IS structure kept verbatim: inline ActivateClass, SetIsInheritedEffect(true). AS-IS `card.Owner.
// DigivolveCount_ThisTurn >= 1` -> the bridged `CardEffectCommons.DigivolveCountThisTurn(card)` (PlayerTurnCounters
// service). AS-IS `card.PermanentOfThisCard()` (used as a Permanent) -> ICardEffect.ResolvePermanentOfThisCard(card).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Green;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;

public sealed class BT1_007 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("DP +1000", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetIsInheritedEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Attacking] If you've digivolved this turn, this Digimon gets +1000 DP for the turn.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnAttack(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.DigivolveCountThisTurn(card) >= 1)
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await CardEffectCommons.ChangeDigimonDP(targetPermanent: ICardEffect.ResolvePermanentOfThisCard(card), changeValue: 1000, effectDuration: EffectDuration.UntilEachTurnEnd, activateClass: activateClass);
            }
        }

        return cardEffects;
    }
}
