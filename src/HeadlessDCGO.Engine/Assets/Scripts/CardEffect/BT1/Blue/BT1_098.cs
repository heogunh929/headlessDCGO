// Source: DCGO/Assets/Scripts/CardEffect/BT1/Blue/BT1_098.cs — an Option.
// P8 CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass). 1:1 mirror of the AS-IS BT1_098.
//   [Main] (OptionSkill) ActivateClass(CanUseCondition = CanTriggerOptionMainEffect, ORDER=-1, ISOPTIONAL=false).
//     ActivateCoroutine: SelectPermanentEffect.SetUp(selectPlayer: owner, canTargetCondition =
//     IsPermanentExistsOnOwnerBattleAreaDigimon, maxCount = Min(1, MatchConditionPermanentCount),
//     canNoSelect:false, canEndNotMax:false, mode: Custom, selectPermanentCoroutine: SelectPermanentCoroutine).
//     SelectPermanentCoroutine(permanent): CardEffectCommons.GainJamming(targetPermanent: permanent,
//     EffectDuration.UntilEachTurnEnd, activateClass).
//   [Security] (SecuritySkill) ActivateClass(CanUseCondition = CanTriggerSecurityEffect, SetIsSecurityEffect(true)).
//     ActivateCoroutine: CardEffectCommons.AddThisCardToHand(card, activateClass).
// AS-IS structure kept verbatim: inline `new ActivateClass()` (twice) + local functions. Substrate
// translations only: IEnumerator->Task, StartCoroutine->await; the AS-IS `Func<Permanent,bool>
// CanSelectPermanentCondition` is expressed as the established `Func<HeadlessEntityId,bool>` idiom (single
// condition function serves both HasMatchConditionPermanent/MatchConditionPermanentCount call sites, same as
// ST1_08/BT1_017); `GManager.instance.GetComponent<SelectPermanentEffect>()` -> bridge W4;
// `CardEffectCommons.AddThisCardToHand(card, activateClass)` -> mirror `(card, card)` (ST3_13/BT9_109
// convention — sourceCard = the effect's source card).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Blue;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_098 : CEntity_Effect
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
                return "[Main] 1 of your Digimon gains <Jamming> (This Digimon can't be deleted in battles against Security Digimon) for the turn.";
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

                selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon that will gain Jamming.", "The opponent is selecting 1 Digimon that will gain Jamming.");

                await selectPermanentEffect.Activate();

                async Task SelectPermanentCoroutine(Permanent permanent)
                {
                    await CardEffectCommons.GainJamming(targetPermanent: permanent, effectDuration: EffectDuration.UntilEachTurnEnd, activateClass: activateClass);
                }
            }
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Add this card to hand", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetIsSecurityEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Security] Add this card to its owner's hand.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerSecurityEffect(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await CardEffectCommons.AddThisCardToHand(card, card);
            }
        }

        return cardEffects;
    }
}
