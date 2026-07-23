// Source: DCGO/Assets/Scripts/CardEffect/BT1/Green/BT1_112.cs — a Green Option (two timings).
// TRUE AS-IS-verbatim re-port (P5 batch 2). 1:1 mirror of the original BT1_112 (BT1/Green).
//   [Main] 1 of your Digimon gains the following effect for the turn: When this Digimon deletes an
//   opponent's Digimon in battle and survives, unsuspend it.
//   [Security] Add this card to its owner's hand.
// AS-IS structure kept verbatim: inline ActivateClass + SelectPermanentEffect(Mode.Custom); the per-target
// coroutine builds a nested ActivateClass ("Unsuspend this Digimon", self = selected.TopCard) using the
// bridged Hashtable-based CanTriggerWhenDeleteOpponentDigimonByBattle + CanUnsuspend + IUnsuspendPermanents,
// then registers it on the selected permanent via AddEffectToPermanent.
// CreateBuffEffect (pure VFX/SE, Effects.cs:1433) is stripped.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Green;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_112 : CEntity_Effect
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
                return "[Main] 1 of your Digimon gains the following effect for the turn: When this Digimon deletes an opponent's Digimon in battle and survives, unsuspend it.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card);
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

                    if (selectedPermanent != null)
                    {
                        ActivateClass activateClass1 = new ActivateClass();
                        activateClass1.SetUpICardEffect("Unsuspend this Digimon", CanUseCondition1, selectedPermanent.TopCard);
                        activateClass1.SetUpActivateClass(CanActivateCondition1, ActivateCoroutine1, -1, false, EffectDiscription1());
                        activateClass1.SetEffectSourcePermanent(selectedPermanent);
                        CardEffectCommons.AddEffectToPermanent(targetPermanent: selectedPermanent, effectDuration: EffectDuration.UntilEachTurnEnd, card: card, cardEffect: activateClass1, timing: EffectTiming.OnEndBattle);

                        // AS-IS `GManager.instance.GetComponent<Effects>().CreateBuffEffect(selectedPermanent)`
                        // — pure VFX/SE (Effects.cs:1433), stripped (see file header).

                        string EffectDiscription1()
                        {
                            return "When this Digimon deletes an opponent's Digimon in battle and survives, unsuspend it.";
                        }

                        bool CanUseCondition1(Hashtable hashtable1)
                        {
                            if (CardEffectCommons.IsPermanentExistsOnBattleArea(selectedPermanent))
                            {
                                bool WinnerCondition(Permanent p) => p.InstanceId == selectedPermanent.InstanceId;
                                bool LoserCondition(Permanent p) => CardEffectCommons.IsOpponentPermanent(p, card);

                                if (CardEffectCommons.CanTriggerWhenDeleteOpponentDigimonByBattle(hashtable: hashtable1, winnerCondition: WinnerCondition, loserCondition: LoserCondition, isOnlyWinnerSurvive: true))
                                {
                                    return true;
                                }
                            }

                            return false;
                        }

                        bool CanActivateCondition1(Hashtable hashtable)
                        {
                            if (CardEffectCommons.IsPermanentExistsOnBattleArea(selectedPermanent))
                            {
                                if (CardEffectCommons.CanUnsuspend(selectedPermanent))
                                {
                                    return true;
                                }
                            }

                            return false;
                        }

                        async Task ActivateCoroutine1(Hashtable _hashtable1)
                        {
                            await new IUnsuspendPermanents(new List<Permanent>() { selectedPermanent }, activateClass1).Unsuspend();
                        }
                    }
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
                await CardEffectCommons.AddThisCardToHand(card, activateClass.EffectSourceCard);
            }
        }

        return cardEffects;
    }
}
