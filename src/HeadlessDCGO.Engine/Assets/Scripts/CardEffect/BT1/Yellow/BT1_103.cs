// Source: DCGO/Assets/Scripts/CardEffect/BT1/Yellow/BT1_103.cs — an Option.
// P8/R6-A CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass). 1:1 mirror of the AS-IS
// BT1_103 — inline `new ActivateClass()` (twice) + local functions.
//   [Main] (OptionSkill) Until the end of your opponent's next turn, 1 of your Digimon gains <Blocker>.
//     ActivateClass(CanUseCondition = CanTriggerOptionMainEffect, CanActivateCondition = null, ORDER=-1,
//     ISOPTIONAL=false). CanSelectPermanentCondition = IsPermanentExistsOnOwnerBattleAreaDigimon.
//     ActivateCoroutine (guarded by HasMatchConditionPermanent): SelectPermanentEffect.SetUp(mode: Custom,
//     maxCount = Min(1, count), canNoSelect:false, canEndNotMax:false, selectPermanentCoroutine); per selected
//     permanent CardEffectCommons.GainBlocker(permanent, UntilOpponentTurnEnd, activateClass).
//   [Security] (SecuritySkill, independent) ActivateClass(CanUseCondition = CanTriggerSecurityEffect,
//     SetIsSecurityEffect(true)). ActivateCoroutine: DrawClass(owner, 1).Draw() THEN AddThisCardToHand(card).
// Substrate translations only: IEnumerator->Task, StartCoroutine->await; AS-IS `Func<Permanent,bool>` ->
//   `Func<HeadlessEntityId,bool>` idiom (IsOwnerBattleAreaDigimon); DrawClass ctor -> mirror shape;
//   `AddThisCardToHand(card, activateClass)` -> mirror `(card, card)` (BT1_098 convention).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Yellow;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_103 : CEntity_Effect
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
                return "[Main] Until the end of your opponent's next turn, 1 of your Digimon gains <Blocker>. (When an opponent's Digimon attacks, you may suspend this Digimon to force the opponent to attack it instead.)";
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

                    selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon that will get Blocker.", "The opponent is selecting 1 Digimon that will get Blocker.");

                    await selectPermanentEffect.Activate();

                    async Task SelectPermanentCoroutine(Permanent selectedPermanent)
                    {
                        await CardEffectCommons.GainBlocker(
                            targetPermanent: selectedPermanent,
                            effectDuration: EffectDuration.UntilOpponentTurnEnd,
                            activateClass: activateClass);
                    }
                }
            }
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect($"Draw 1 and add this card to hand", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetIsSecurityEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Security] Trigger <Draw 1> (Draw 1 card from your deck.) Then, add this card to your hand.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerSecurityEffect(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await new DrawClass(card.Context, card.Owner, 1, activateClass.EffectSourceCard?.InstanceId).Draw();

                await CardEffectCommons.AddThisCardToHand(card, card);
            }
        }

        return cardEffects;
    }
}
