// Source: DCGO/Assets/Scripts/CardEffect/BT2/Blue/BT2_025.cs
// TRUE AS-IS-verbatim re-port (batch 3). 1:1 mirror of the original BT2_025 (BT2/Blue).
//   [When Attacking][Inherited] Trash the top digivolution card of 1 of your opponent's Digimon.
// Replaces the PREVIOUS pass's old-model `CardEffectFactory.SelectAndTrashDigivolutionEffect(...)` call (an
// invented helper with no AS-IS counterpart) with the literal AS-IS inline `new ActivateClass()` structure +
// `GManager.instance.GetComponent<SelectPermanentEffect>()` select-and-follow-up pattern (bridge W4).
// Substrate translations: IEnumerator->Task, `ContinuousController.instance.StartCoroutine(X)`->`await X`;
// AS-IS `Func<Permanent,bool> CanSelectPermanentCondition` kept 1:1 (composed from
// `CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon`), passed directly to
// `SelectPermanentEffect.SetUp`/`MatchConditionPermanentCount`/`HasMatchConditionPermanent`;
// `card.Owner.GetBattleAreaDigimons().Contains(card.PermanentOfThisCard())`
// -> `new Player(card.Context, card.Owner).GetBattleAreaDigimons().Some(p => p.InstanceId ==
// card.PermanentOfThisCard().TopInstanceId)` (Player reconstruction + the established Permanent-vs-PermanentView
// identity idiom, see BT2_002.cs).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Blue;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT2_025 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Trash 1 digivolution card", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetIsInheritedEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Attacking] Trash the top digivolution card of 1 of your opponent's Digimon.";
            }

            // Permanent-form predicate (RULE A/D): feeds MatchConditionPermanentCount/HasMatchConditionPermanent and the SelectPermanentEffect.SetUp canTargetCondition consumer directly.
            bool CanSelectPermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnAttack(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (isExistOnField(card))
                {
                    if (new Player(card.Context, card.Owner).GetBattleAreaDigimons().Some(p => p.InstanceId == card.PermanentOfThisCard().TopInstanceId))
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
                                selectPermanentCoroutine: SelectPermanentCoroutine,
                                afterSelectPermanentCoroutine: null,
                                mode: SelectPermanentEffect.Mode.Custom,
                                cardEffect: activateClass);

                            selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon that will trash digivolution cards.", "The opponent is selecting 1 Digimon that will trash digivolution cards.");

                            await selectPermanentEffect.Activate();

                            async Task SelectPermanentCoroutine(Permanent permanent)
                            {
                                Permanent selectedPermanent = permanent;

                                await CardEffectCommons.TrashDigivolutionCardsFromTopOrBottom(targetPermanent: selectedPermanent, trashCount: 1, isFromTop: true, activateClass: activateClass);
                            }
                        }
                    }
                }
            }
        }

        return cardEffects;
    }
}
