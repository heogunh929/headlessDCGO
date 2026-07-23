// Source: DCGO/Assets/Scripts/CardEffect/ST2/Blue/ST2_03.cs
// TRUE AS-IS-verbatim re-port (batch: ST2 Blue). 1:1 mirror of the original ST2_03.
//   [When Attacking][Inherited] Trash the digivolution card at the bottom of 1 of your opponent's Digimon
//   with a level of 5 or less.
// Replaces the PREVIOUS pass's old-model `CardEffectFactory.SelectAndTrashDigivolutionEffect(...)` call (an
// invented helper with no AS-IS counterpart) with the literal AS-IS inline `new ActivateClass()` +
// `GManager.instance.GetComponent<SelectPermanentEffect>()` select flow (bridge W4 — see ST1_08.cs/BT1_017.cs).
// AS-IS structure kept verbatim: inline ActivateClass, SetIsInheritedEffect(true), no SetHashString; the
// ActivateCoroutine computes maxCount/selects WITHOUT re-checking HasMatchConditionPermanent first (unlike the
// sibling ST2_06/09/14/16, which DO re-check — this AS-IS difference is preserved as-is).
// Substrate translation only: IEnumerator->Task, `ContinuousController.instance.StartCoroutine(X)`->`await X`;
// AS-IS `CanSelectPermanentCondition(Permanent permanent)` kept on the canonical `Func<Permanent,bool>` shape
// (id-flip 3b); the id-native LevelOf/HasTrashableDigivolutionCards/TopCardHasLevel commons calls (no
// Permanent-form sibling) are called with `permanent.InstanceId` (commons signature unchanged), while
// IsOpponentBattleAreaDigimon converts to its Permanent-form sibling IsPermanentExistsOnOpponentBattleAreaDigimon.
// AS-IS `CardEffectCommons.HasMatchConditionPermanent(cond)` /
// `MatchConditionPermanentCount(cond)` (global scan, no CardSource arg in AS-IS) -> mirror's `(card, condition)`
// overloads (same substrate adaptation already established, e.g. ST1_08.cs/BT1_017.cs).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST2.Blue;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST2_03 : CEntity_Effect
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
                return "[When Attacking] Trash the digivolution card at the bottom of 1 of your opponent's Digimon with a level of 5 or less.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card)
                    && CardEffectCommons.LevelOf(card, permanent.InstanceId) <= 5
                    && CardEffectCommons.HasTrashableDigivolutionCards(card, permanent.InstanceId)
                    && CardEffectCommons.TopCardHasLevel(card, permanent.InstanceId);
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
                    await CardEffectCommons.TrashDigivolutionCardsFromTopOrBottom(targetPermanent: permanent, trashCount: 1, isFromTop: false, activateClass: activateClass);
                }
            }
        }

        return cardEffects;
    }
}
