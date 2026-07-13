// Source: DCGO/Assets/Scripts/CardEffect/BT1/Green/BT1_113.cs
// TRUE AS-IS-verbatim re-port (P5 batch 2). 1:1 mirror of the original BT1_113 (BT1/Green) — an Option.
//   [Main] Until the end of your opponent's next turn, 1 of your opponent's Digimon can't attack or block.
//   [Security] Your opponent's Digimon don't unsuspend during their next unsuspend phase.
// AS-IS structure kept verbatim: [Main] is inline ActivateClass + SelectPermanentEffect(Mode.Custom), per-target
// coroutine calling GainCanNotAttack + GainCanNotBlock (both bridged, verified). [Security] has NO
// SelectPermanentEffect — it directly calls the bridged GainCanNotUnsuspendPlayerEffect (a broad, no-select,
// player-scope grant).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Green;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_113 : CEntity_Effect
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
                return "[Main] Until the end of your opponent's next turn�1 of your opponent's Digimon can't attack or block.";
            }

            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                return CardEffectCommons.IsOpponentBattleAreaDigimon(card, id);
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

                selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon that will get effects.", "The opponent is selecting 1 Digimon that will get effects.");

                await selectPermanentEffect.Activate();

                async Task SelectPermanentCoroutine(Permanent permanent)
                {
                    Permanent selectedPermanent = permanent;

                    CardEffectCommons.GainCanNotAttack(targetPermanent: selectedPermanent, defenderCondition: null, effectDuration: EffectDuration.UntilOpponentTurnEnd, sourceCard: activateClass.EffectSourceCard, effectName: "Can't Attack");

                    CardEffectCommons.GainCanNotBlock(targetPermanent: selectedPermanent, attackerCondition: null, effectDuration: EffectDuration.UntilOpponentTurnEnd, sourceCard: activateClass.EffectSourceCard, effectName: "Can't Block");

                    await Task.CompletedTask;
                }
            }
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Can't Unsuspend", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetIsSecurityEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Security] Your opponent's Digimon don't unsuspend during their next unsuspend phase.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerSecurityEffect(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                bool PermanentCondition(Permanent permanent)
                {
                    if (CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card))
                    {
                        if (!permanent.TopCard.CanNotBeAffected(activateClass.EffectSourceCard?.InstanceId))
                        {
                            return true;
                        }
                    }

                    return false;
                }

                CardEffectCommons.GainCanNotUnsuspendPlayerEffect(
                    permanentCondition: PermanentCondition,
                    effectDuration: EffectDuration.UntilOwnerActivePhase,
                    sourceCard: activateClass.EffectSourceCard,
                    isOnlyActivePhase: true,
                    effectName: "Your Digimon can't unsuspend");

                await Task.CompletedTask;
            }
        }

        return cardEffects;
    }
}
