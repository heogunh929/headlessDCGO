// Source: DCGO/Assets/Scripts/CardEffect/ST2/Blue/ST2_14.cs
// TRUE AS-IS-verbatim re-port (batch: ST2 Blue). 1:1 mirror of the original ST2_14 (an Option).
//   [Main]     Choose 1 of your opponent's Digimon with no digivolution cards. That Digimon can't attack or
//              block until the end of your opponent's next turn.
//   [Security] Same, until the end of your next turn.
// Replaces the PREVIOUS pass's old-model `CardEffectFactory.SelectAndRestrictEffect(...)` calls (an invented
// helper with no AS-IS counterpart) with the literal AS-IS inline `new ActivateClass()` +
// `GManager.instance.GetComponent<SelectPermanentEffect>()` select flow, applying `GainCanNotAttack` THEN
// `GainCanNotBlock` per selected permanent (bridge W4 — see ST1_08.cs; GainCanNotAttack/GainCanNotBlock are the
// real AS-IS-signature bridges at CardEffectCommons/GiveEffect/GiveEffectToPermanent/CanNotAttack.cs &
// CanNotBlock.cs).
// AS-IS structure kept verbatim: both blocks pass `null` for CanActivateCondition (SetUpActivateClass's first
// arg); [Security] calls `SetIsSecurityEffect(true)`; [Main] uses `UntilOpponentTurnEnd`, [Security] uses
// `UntilOwnerTurnEnd`.
// Substrate translation only: IEnumerator->Task, `ContinuousController.instance.StartCoroutine(X)`->`await X`;
// AS-IS `CanSelectPermanentCondition(Permanent permanent)` -> the established `Func<HeadlessEntityId,bool>`
// idiom (already correct in the pre-existing file: IsOpponentBattleAreaDigimon/HasNoDigivolutionCards); AS-IS
// `CardEffectCommons.HasMatchConditionPermanent(cond)` / `MatchConditionPermanentCount(cond)` (global scan, no
// CardSource arg in AS-IS) -> mirror's `(card, condition)` overloads.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST2.Blue;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST2_14 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect(card.BaseENGCardNameFromEntity, CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Main] Choose 1 of your opponent's Digimon with no digivolution cards. That Digimon can't attack or block until the end of your opponent's next turn.";
            }

            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                return CardEffectCommons.IsOpponentBattleAreaDigimon(card, id)
                    && CardEffectCommons.HasNoDigivolutionCards(card, id);
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOptionMainEffect(hashtable, card);
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

                    selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon that will get effects.", "The opponent is selecting 1 Digimon that will get effects.");

                    await selectPermanentEffect.Activate();

                    async Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        await CardEffectCommons.GainCanNotAttack(
                            targetPermanent: permanent,
                            defenderCondition: null,
                            effectDuration: EffectDuration.UntilOpponentTurnEnd,
                            activateClass: activateClass,
                            effectName: "Can't Attack");

                        await CardEffectCommons.GainCanNotBlock(
                            targetPermanent: permanent,
                            attackerCondition: null,
                            effectDuration: EffectDuration.UntilOpponentTurnEnd,
                            activateClass: activateClass,
                            effectName: "Can't Block");
                    }
                }
            }
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect($"Opponent's 1 Digimon can't Attack or Block", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetIsSecurityEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Security] Choose 1 of your opponent's Digimon with no digivolution cards. That Digimon can't attack or block until the end of your next turn.";
            }

            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                return CardEffectCommons.IsOpponentBattleAreaDigimon(card, id)
                    && CardEffectCommons.HasNoDigivolutionCards(card, id);
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerSecurityEffect(hashtable, card);
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

                    selectPermanentEffect.SetUpCustomMessage(
                        "Select 1 Digimon that will get effects.",
                        "The opponent is selecting 1 Digimon that will get effects.");

                    await selectPermanentEffect.Activate();

                    async Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        await CardEffectCommons.GainCanNotAttack(
                            targetPermanent: permanent,
                            defenderCondition: null,
                            effectDuration: EffectDuration.UntilOwnerTurnEnd,
                            activateClass: activateClass,
                            effectName: "Can't Attack");

                        await CardEffectCommons.GainCanNotBlock(
                            targetPermanent: permanent,
                            attackerCondition: null,
                            effectDuration: EffectDuration.UntilOwnerTurnEnd,
                            activateClass: activateClass,
                            effectName: "Can't Block");
                    }
                }
            }
        }

        return cardEffects;
    }
}
