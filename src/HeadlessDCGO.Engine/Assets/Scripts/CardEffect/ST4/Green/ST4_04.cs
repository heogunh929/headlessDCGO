// Source: DCGO/Assets/Scripts/CardEffect/ST4/Green/ST4_04.cs
// TRUE AS-IS-verbatim re-port (batch 3). 1:1 mirror of the original ST4_04 (ST4/Green).
//   [When Attacking] If you attack an opponent's Digimon, this Digimon gets +2000 DP for the turn.
// Replaces the PREVIOUS pass's old-model `CardEffectFactory.SelfDpBuffTriggerEffect(...)` call (an invented
// helper with no AS-IS counterpart) with the literal AS-IS inline `new ActivateClass()` structure — this is
// the identical shape as BT1_001 (+1000 DP), just +2000 DP here.
// AS-IS structure kept verbatim: inline ActivateClass, SetIsInheritedEffect(true).
// Substrate translation only: IEnumerator->Task; `yield return ContinuousController.instance.StartCoroutine(X)`
// -> `await X`; `card.PermanentOfThisCard()` used as a `Permanent` argument ->
// `ICardEffect.ResolvePermanentOfThisCard(card)`; `GManager.instance.attackProcess.DefendingPermanent`
// resolves directly (mirror AttackProcess exposes it 1:1, see BT1_001.cs).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST4.Green;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;

public sealed class ST4_04 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("DP +2000", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetIsInheritedEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Attacking] If you attack an opponent's Digimon, this Digimon gets +2000 DP for the turn.";
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
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    await CardEffectCommons.ChangeDigimonDP(
                        targetPermanent: ICardEffect.ResolvePermanentOfThisCard(card),
                        changeValue: 2000,
                        effectDuration: EffectDuration.UntilEachTurnEnd,
                        activateClass: activateClass);
                }
            }
        }

        return cardEffects;
    }
}
