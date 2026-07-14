// Source: DCGO/Assets/Scripts/CardEffect/BT1/Red/BT1_093.cs — a Red Option.
// P8/R6-A CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass). 1:1 mirror of the AS-IS
// BT1_093 — inline `new ActivateClass()` (twice) + local functions.
//   [Main] (OptionSkill) 1 of your Digimon gets +2000 DP and <Security Attack +1> for the turn.
//     ActivateClass(CanUseCondition = CanTriggerOptionMainEffect, CanActivateCondition = null, ORDER=-1,
//     ISOPTIONAL=false). ActivateCoroutine (guarded by HasMatchConditionPermanent): SelectPermanentEffect.SetUp(
//     mode: Custom, maxCount = Min(1, count), canNoSelect:false, canEndNotMax:false). SelectPermanentCoroutine
//     applies BOTH to the SAME picked permanent: ChangeDigimonDP(permanent, 2000, UntilEachTurnEnd) THEN
//     ChangeDigimonSAttack(permanent, 1, UntilEachTurnEnd).
//   [Security] (SecuritySkill, independent) SetIsSecurityEffect(true); ActivateCoroutine: AddThisCardToHand(card).
// Substrate translations only: IEnumerator->Task, StartCoroutine->await; AS-IS `Func<Permanent,bool>` ->
//   `Func<HeadlessEntityId,bool>` idiom (IsOwnerBattleAreaDigimon); GManager.GetComponent -> bridge W4;
//   `AddThisCardToHand(card, activateClass)` -> mirror `(card, card)` (BT1_098 convention).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Red;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_093 : CEntity_Effect
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
                return "[Main] 1 of your Digimon gets +2000 DP and <Security Attack +1> (This Digimon checks 1 additional security card) for the turn.";
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

                    selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon to DP +2000 and Security Attack +1.", "The opponent is selecting 1 Digimon to DP +2000 and Security Attack +1.");

                    await selectPermanentEffect.Activate();

                    async Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        Permanent selectedPermanent = permanent;

                        if (selectedPermanent != null)
                        {
                            await CardEffectCommons.ChangeDigimonDP(targetPermanent: permanent, changeValue: 2000, effectDuration: EffectDuration.UntilEachTurnEnd, activateClass: activateClass);

                            await CardEffectCommons.ChangeDigimonSAttack(targetPermanent: permanent, changeValue: 1, effectDuration: EffectDuration.UntilEachTurnEnd, activateClass: activateClass);
                        }
                    }
                }
            }
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect($"Add this card to hand", CanUseCondition, card);
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
