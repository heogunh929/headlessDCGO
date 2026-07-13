// Source: DCGO/Assets/Scripts/CardEffect/BT2/Blue/BT2_002.cs
// TRUE AS-IS-verbatim re-port (batch 3). 1:1 mirror of the original BT2_002.
//   [Your Turn][Once Per Turn] When this Digimon becomes unsuspended during your main phase, it gets +1000 DP
//   for the turn.
// Replaces the PREVIOUS pass's old-model `CardEffectFactory.SelfDpBuffTriggerEffect(...)` call (an invented
// helper with no AS-IS counterpart) with the literal AS-IS inline `new ActivateClass()` structure.
// AS-IS structure kept verbatim: inline ActivateClass, SetIsInheritedEffect(true), SetHashString.
// Substrate translation only: IEnumerator->Task, `ContinuousController.instance.StartCoroutine(X)`->`await X`;
// `permanent => permanent == card.PermanentOfThisCard()` -> `permanent.InstanceId ==
// card.PermanentOfThisCard().TopInstanceId` (the established Permanent-vs-PermanentView identity idiom, see
// BT22_003.cs/BT8_057.cs); `card.PermanentOfThisCard()` used as a `Permanent` argument ->
// `ICardEffect.ResolvePermanentOfThisCard(card)`.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Blue;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;

public sealed class BT2_002 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnUnTappedAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("DP +1000", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, false, EffectDiscription());
            activateClass.SetIsInheritedEffect(true);
            activateClass.SetHashString("DP+1000_BT2_002");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Your Turn][Once Per Turn] When this Digimon becomes unsuspended during your main phase, it gets +1000 DP for the turn.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.IsOwnerTurn(card))
                    {
                        if (GManager.instance.turnStateMachine.gameContext.TurnPhase == GameContext.phase.Main)
                        {
                            if (CardEffectCommons.CanTriggerWhenPermanentUnsuspends(hashtable, permanent => permanent.InstanceId == card.PermanentOfThisCard().TopInstanceId))
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
                await CardEffectCommons.ChangeDigimonDP(targetPermanent: ICardEffect.ResolvePermanentOfThisCard(card), changeValue: 1000, effectDuration: EffectDuration.UntilEachTurnEnd, activateClass: activateClass);
            }
        }

        return cardEffects;
    }
}
