// Source: DCGO/Assets/Scripts/CardEffect/BT2/Red/BT2_092.cs
// TRUE AS-IS-verbatim re-port (batch 3). 1:1 mirror of the original BT2_092 (BT2/Red, an Option).
//   [Main] Up to 2 of your Digimon gain <Security Attack +1> (This Digimon checks 1 additional security card)
//   for the turn.
// Replaces the PREVIOUS pass's old-model `CardEffectFactory.SelectAndBuffSAttackEffect(...)` call (an invented
// helper — explicitly prohibited/retired) with the literal AS-IS inline `new ActivateClass()` structure +
// `GManager.instance.GetComponent<SelectPermanentEffect>()` (Mode.Custom, per-target `SelectPermanentCoroutine`)
// selection pattern (bridge W4).
// Substrate translations: IEnumerator->Task, `ContinuousController.instance.StartCoroutine(X)`->`await X`;
// AS-IS `Func<Permanent,bool> CanSelectPermanentCondition` -> the established entity-id predicate idiom
// (`CardEffectCommons.IsOwnerBattleAreaDigimon(card, id)`); `CardEffectCommons.customPermanentMessageArray_
// ChangeSAttack(changeValue:, maxCount:)` and `CardEffectCommons.ChangeDigimonSAttack(targetPermanent:,
// changeValue:, effectDuration:, activateClass:)` are the real, already-bridged AS-IS helpers, unchanged.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Red;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT2_092 : CEntity_Effect
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
                return "[Main] Up to 2 of your Digimon gain <Security Attack +1> (This Digimon checks 1 additional security card) for the turn.";
            }

            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                return CardEffectCommons.IsOwnerBattleAreaDigimon(card, id);
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOptionMainEffect(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                int maxCount = Math.Min(2, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentCondition));

                SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                selectPermanentEffect.SetUp(
                    selectPlayer: card.Owner,
                    canTargetCondition: CanSelectPermanentCondition,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: CanEndSelectCondition,
                    maxCount: maxCount,
                    canNoSelect: false,
                    canEndNotMax: true,
                    selectPermanentCoroutine: SelectPermanentCoroutine,
                    afterSelectPermanentCoroutine: null,
                    mode: SelectPermanentEffect.Mode.Custom,
                    cardEffect: activateClass);

                selectPermanentEffect.SetUpCustomMessage(customMessageArray: CardEffectCommons.customPermanentMessageArray_ChangeSAttack(changeValue: +1, maxCount: maxCount));

                await selectPermanentEffect.Activate();

                bool CanEndSelectCondition(List<Permanent> permanents)
                {
                    if (CardEffectCommons.HasNoElement(permanents))
                    {
                        return false;
                    }

                    return true;
                }

                async Task SelectPermanentCoroutine(Permanent permanent)
                {
                    await CardEffectCommons.ChangeDigimonSAttack(targetPermanent: permanent, changeValue: 1, effectDuration: EffectDuration.UntilEachTurnEnd, activateClass: activateClass);
                }
            }
        }

        return cardEffects;
    }
}
