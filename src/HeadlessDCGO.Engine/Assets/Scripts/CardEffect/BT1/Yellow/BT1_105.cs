// Source: DCGO/Assets/Scripts/CardEffect/BT1/Yellow/BT1_105.cs — an Option (single OptionSkill block).
// P8/R6-A CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass). 1:1 mirror of the AS-IS
// BT1_105 — inline `new ActivateClass()` + local functions.
//   [Main] Change the original DP of 1 of your opponent's Digimon to 3000 until the end of your opponent's
//   next turn.
// AS-IS: ActivateClass on OptionSkill, CanUseCondition = CanTriggerOptionMainEffect, CanActivateCondition = null,
//   ORDER=-1, ISOPTIONAL=false. CanSelectPermanentCondition = IsPermanentExistsOnOpponentBattleAreaDigimon.
//   ActivateCoroutine (unconditional SetUp): maxCount = Min(1, MatchConditionPermanentCount); SelectPermanentEffect
//   .SetUp(mode: Custom, canNoSelect:false, canEndNotMax:false, selectPermanentCoroutine); per selected permanent
//   CardEffectCommons.ChangeBaseDigimonDP(permanent, 3000, UntilOpponentTurnEnd, activateClass).
// Substrate translations only: IEnumerator->Task, StartCoroutine->await; AS-IS `Func<Permanent,bool>
//   CanSelectPermanentCondition` is expressed verbatim on the canonical Func<Permanent,bool> shape (id-flip 3b),
//   serving both MatchConditionPermanentCount and SelectPermanentEffect.canTargetCondition;
//   `GManager.instance.GetComponent<SelectPermanentEffect>()` -> bridge W4.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Yellow;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_105 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect(card.BaseENGCardNameFromEntity, CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetEffectDiscription(EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Main] Change the original DP of 1 of your opponent's Digimon to 3000 until the end of your opponent's next turn.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card))
                {
                    return true;
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOptionMainEffect(hashtable, card);
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

                selectPermanentEffect.SetUpCustomMessage(customMessageArray: CardEffectCommons.customPermanentMessageArray_ChangeOriginDP(changeValue: 3000, maxCount: maxCount));

                await selectPermanentEffect.Activate();

                async Task SelectPermanentCoroutine(Permanent permanent)
                {
                    await CardEffectCommons.ChangeBaseDigimonDP(targetPermanent: permanent, changeValue: 3000, effectDuration: EffectDuration.UntilOpponentTurnEnd, activateClass: activateClass);
                }
            }
        }

        return cardEffects;
    }
}
