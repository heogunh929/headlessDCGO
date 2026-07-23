// Source: DCGO/Assets/Scripts/CardEffect/BT9/Blue/BT9_021.cs
// P8 CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass) of the [Your Turn][Once Per Turn]
// OnAddHand INHERITED (digivolution-source) branch.
//   [Your Turn][Once Per Turn] When an effect adds a card to your hand, return 1 of your opponent's level 3
//   Digimon to its owner's hand.
// AS-IS structure kept verbatim: inline `new ActivateClass()` + SetUpActivateClass(..., 1, false, ...) (ORDER 1 =
// once per turn, mandatory) + SetIsInheritedEffect(true) + SetHashString("Bounce_BT9_021") (BT9_021.cs:76-156).
// Substrate translations only: IEnumerator->Task, StartCoroutine->await;
// `GManager.instance.GetComponent<SelectPermanentEffect>()` + full AS-IS SetUp(Mode.Bounce) (bridge W4, BT2_092
// idiom); AS-IS `Func<Permanent,bool> CanSelectPermanentCondition` supplied directly to SetUp's canonical
// Func<Permanent,bool> overload / MatchConditionPermanentCount (no id adapter); AS-IS `player => player == card.Owner` ->
// `player.PlayerId == card.Owner` (Hashtable-overload playerCondition is a mirror `Player`).
//
// The AS-IS OnEnterFieldAnyone [On Play] "when you play a blue Tamer, <Draw 1>" effect (BT9_021.cs:15-74) is a
// NON-inherited top-card reactor served by the action-wired OnEnterFieldAnyone path, orthogonal to this inherited
// OnAddHand branch; it remains deliberately OMITTED (same scoping as the prior pass).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT9.Blue;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT9_021 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAddHand)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Return 1 level 3 Digimon to hand", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, false, EffectDiscription());
            activateClass.SetIsInheritedEffect(true);
            activateClass.SetHashString("Bounce_BT9_021");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Your Turn][Once Per Turn] When an effect adds a card to your hand, return 1 of your opponent's level 3 Digimon to its owner's hand.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card))
                {
                    if (permanent.Level == 3)
                    {
                        if (permanent.TopCard.HasLevel)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.IsOwnerTurn(card))
                    {
                        if (CardEffectCommons.CanTriggerWhenAddHand(hashtable, player => player.PlayerId == card.Owner, cardEffect => cardEffect != null))
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
                    return true;
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                {
                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentCondition));

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectPermanentCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: false,
                        canEndNotMax: false,
                        selectPermanentCoroutine: null,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Bounce,
                        cardEffect: activateClass);

                    await selectPermanentEffect.Activate();
                }
            }
        }

        return cardEffects;
    }
}
