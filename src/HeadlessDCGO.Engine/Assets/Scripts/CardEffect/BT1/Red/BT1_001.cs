// Source: DCGO/Assets/Scripts/CardEffect/BT1/Red/BT1_001.cs
// TRUE AS-IS-verbatim re-port (P5, bridge-complete pass). 1:1 mirror of the original BT1_001 (BT1/Red).
//   [When Attacking] If you attack an opponent's Digimon, this Digimon gets +1000 DP for the turn.
// AS-IS structure kept verbatim: inline `new ActivateClass()` + SetUpICardEffect/SetUpActivateClass + local
// functions (CanUseCondition/CanActivateCondition/ActivateCoroutine), SetIsInheritedEffect(true).
// Substrate-only translations: IEnumerator -> Task; `yield return ContinuousController.instance.StartCoroutine(X)`
// -> `await X`; `card.PermanentOfThisCard()` (used as a `Permanent`) -> `ICardEffect.ResolvePermanentOfThisCard(card)`;
// `GManager.instance.attackProcess.DefendingPermanent` resolves directly (mirror AttackProcess exposes it 1:1).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Red;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;

public sealed class BT1_001 : CEntity_Effect
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
                return "[When Attacking] If you attack an opponent's Digimon, this Digimon gets +1000 DP for the turn.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.CanTriggerOnAttack(hashtable, card))
                {
                    if (GManager.instance.attackProcess.DefendingPermanent != null)
                    {
                        return true;
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
