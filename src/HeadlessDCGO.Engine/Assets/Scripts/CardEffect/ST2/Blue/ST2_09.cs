// Source: DCGO/Assets/Scripts/CardEffect/ST2/Blue/ST2_09.cs
// TRUE AS-IS-verbatim re-port (batch: ST2 Blue). 1:1 mirror of the original ST2_09.
//   [When Digivolving] Trash 2 digivolution cards at the bottom of 1 of your opponent's Digimon.
// Replaces the PREVIOUS pass's old-model `CardEffectFactory.SelectAndTrashDigivolutionEffect(...)` call (an
// invented helper with no AS-IS counterpart) with the literal AS-IS inline `new ActivateClass()` +
// `GManager.instance.GetComponent<SelectPermanentEffect>()` select flow (bridge W4 — see ST1_08.cs/BT1_017.cs).
// TIMING: AS-IS registers this under `EffectTiming.OnEnterFieldAnyone` (gated by `CanTriggerWhenDigivolving`
// inside CanUseCondition); the mirror registers under the WhenDigivolving dispatch key — see the DISPATCH
// REMAP note at the timing check below (batch-2 BT1_025/BT1_062 convention).
// AS-IS structure kept verbatim: inline ActivateClass, no SetIsInheritedEffect, no SetHashString; the
// ActivateCoroutine re-checks HasMatchConditionPermanent before selecting.
// Substrate translation only: IEnumerator->Task, `ContinuousController.instance.StartCoroutine(X)`->`await X`;
// AS-IS `CanSelectPermanentCondition(Permanent permanent)` kept on the canonical `Func<Permanent,bool>` shape
// (id-flip 3b; IsOpponentBattleAreaDigimon converts to its Permanent-form sibling
// IsPermanentExistsOnOpponentBattleAreaDigimon; HasTrashableDigivolutionCards has no Permanent-form sibling,
// so it is called with `permanent.InstanceId`, commons signature unchanged); AS-IS
// `CardEffectCommons.HasMatchConditionPermanent(cond)` / `MatchConditionPermanentCount(cond)` (global
// scan, no CardSource arg in AS-IS) -> mirror's `(card, condition)` overloads.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST2.Blue;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST2_09 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // DISPATCH REMAP (batch-3): AS-IS registers under EffectTiming.OnEnterFieldAnyone (the shared
        // enter-field timing, discriminated by the CanTriggerWhenDigivolving hashtable gate). The mirror
        // engine dispatches [When Digivolving] activated effects on the DEDICATED WhenDigivolving key
        // (DigivolveAction.cs ResolveAsync(..., EffectTiming.WhenDigivolving)) and plain plays on
        // OnEnterFieldAnyone — registering under the literal AS-IS key would silently never fire on digivolve
        // (batch-2 BT1_025/BT1_062 established this exact remap; the AS-IS gate below is kept verbatim).
        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Trash digivolution cards", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Digivolving] Trash 2 digivolution cards at the bottom of 1 of your opponent's Digimon.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card)
                    && CardEffectCommons.HasTrashableDigivolutionCards(card, permanent.InstanceId);
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
                        await CardEffectCommons.TrashDigivolutionCardsFromTopOrBottom(targetPermanent: permanent, trashCount: 2, isFromTop: false, activateClass: activateClass);
                    }
                }
            }
        }

        return cardEffects;
    }
}
