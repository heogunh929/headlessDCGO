// Source: DCGO/Assets/Scripts/CardEffect/BT1/Blue/BT1_043.cs
// TRUE AS-IS-verbatim re-port (P5 batch 2). 1:1 mirror of the original BT1_043 (BT1/Blue).
//   [When Digivolving] Trash 4 digivolution cards under 1 of your opponent's Digimon.
// AS-IS structure kept verbatim: inline ActivateClass + SelectPermanentEffect(Mode.Custom), per-target
// coroutine calling CardEffectCommons.TrashDigivolutionCardsFromTopOrBottom (bridged, verified). AS-IS
// `permanent.DigivolutionCards.Count((cardSource) => !cardSource.CanNotTrashFromDigivolutionCards(activateClass))
// >= 1` -> the established `HasTrashableDigivolutionCards(card, id)` helper, fed `permanent.InstanceId` from the
// canonical Func<Permanent,bool> canTargetCondition (id-flip 3b).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Blue;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_043 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Trash digivolution cards", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Digivolving] Trash 4 digivolution cards under 1 of your opponent's Digimon.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsOpponentBattleAreaDigimon(card, permanent.InstanceId))
                {
                    if (CardEffectCommons.HasTrashableDigivolutionCards(card, permanent.InstanceId))
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);
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

                    await CardEffectCommons.TrashDigivolutionCardsFromTopOrBottom(targetPermanent: selectedPermanent, trashCount: 4, isFromTop: false, activateClass: activateClass);
                }
            }
        }

        return cardEffects;
    }
}