// Source: DCGO/Assets/Scripts/CardEffect/BT1/Yellow/BT1_061.cs
// P8/R6-A CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass). 1:1 mirror of the AS-IS
// BT1_061 — inline `new ActivateClass()` + local functions.
//   [On Play] 2 of your opponent's Digimon get -3000 DP for the turn.
// AS-IS: ActivateClass on OnEnterFieldAnyone, CanUseCondition = CanTriggerOnPlay, CanActivateCondition =
//   IsExistOnBattleArea && HasMatchConditionPermanent(CanSelectPermanentCondition). CanSelectPermanentCondition =
//   IsPermanentExistsOnOpponentBattleAreaDigimon. ORDER=-1, ISOPTIONAL=false. ActivateCoroutine: maxCount =
//   Min(2, MatchConditionPermanentCount); SelectPermanentEffect.SetUp(mode: Custom, canNoSelect:false,
//   canEndNotMax:false, selectPermanentCoroutine); per selected permanent CardEffectCommons.ChangeDigimonDP(
//   permanent, -3000, UntilEachTurnEnd, activateClass).
// Substrate translations only: IEnumerator->Task, StartCoroutine->await; AS-IS `Func<Permanent,bool>` kept
//   verbatim on the canonical shape (id-flip 3b); `GManager.instance.GetComponent<
//   SelectPermanentEffect>()` -> bridge W4.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Yellow;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_061 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("DP -3000", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[On Play] 2 of your opponent's Digimon get -3000 DP for the turn.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnPlay(hashtable, card);
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
                int maxCount = Math.Min(2, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentCondition));

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

                selectPermanentEffect.SetUpCustomMessage("Select Digimon to DP -3000.", "The opponent is selecting Digimon to DP -3000.");

                await selectPermanentEffect.Activate();

                async Task SelectPermanentCoroutine(Permanent permanent)
                {
                    await CardEffectCommons.ChangeDigimonDP(targetPermanent: permanent, changeValue: -3000, effectDuration: EffectDuration.UntilEachTurnEnd, activateClass: activateClass);
                }
            }
        }

        return cardEffects;
    }
}
