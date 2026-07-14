// Source: DCGO/Assets/Scripts/CardEffect/ST5/Black/ST5_09.cs
// P8 CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass). 1:1 mirror of the AS-IS ST5_09
// (ST5/Black).
//   [When Digivolving] Until the end of your opponent's next turn, 1 of your Digimon gains <Blocker>.
// AS-IS: ActivateClass on OnEnterFieldAnyone, ORDER=-1, ISOPTIONAL=false. CanUseCondition =
// CanTriggerWhenDigivolving. CanActivateCondition = IsExistOnBattleArea && HasMatchConditionPermanent(
// CanSelectPermanentCondition). ActivateCoroutine = SelectPermanentEffect (Mode.Custom, select 1 of owner's
// battle-area Digimon) then GainBlocker(selected, UntilOpponentTurnEnd).
// Substrate translations only: IEnumerator->Task, StartCoroutine->await; select flow =
// `GManager.instance.GetComponent<SelectPermanentEffect>()` (bridge W4, BT9_109/BT2_092 idiom); AS-IS
// `Func<Permanent,bool> CanSelectPermanentCondition = IsPermanentExistsOnOwnerBattleAreaDigimon` -> the
// existing id predicate `CardEffectCommons.IsOwnerBattleAreaDigimon(card, id)` (BT2_092 idiom); `Mathf`->`Math`.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST5.Black;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST5_09 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Your 1 Digimon gains Blocker", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Digivolving] Until the end of your opponent's next turn, 1 of your Digimon gains <Blocker>. (When an opponent's Digimon attacks, you may suspend this Digimon to force the opponent to attack it instead.)";
            }

            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                return CardEffectCommons.IsOwnerBattleAreaDigimon(card, id);
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

                async Task SelectPermanentCoroutine(Permanent permanent)
                {
                    await CardEffectCommons.GainBlocker(targetPermanent: permanent, effectDuration: EffectDuration.UntilOpponentTurnEnd, activateClass: activateClass);
                }
            }
        }

        return cardEffects;
    }
}
