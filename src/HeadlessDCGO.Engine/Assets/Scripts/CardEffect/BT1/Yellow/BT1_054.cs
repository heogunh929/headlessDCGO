// Source: DCGO/Assets/Scripts/CardEffect/BT1/Yellow/BT1_054.cs
// P8/R6-A CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass). 1:1 mirror of the AS-IS
// BT1_054 — inline `new ActivateClass()` + local functions.
//   [When Attacking] If you have 3 or more memory, 1 of your opponent's Digimon gets -2000 DP for the turn.
// AS-IS: ActivateClass on OnAllyAttack, CanUseCondition = CanTriggerOnAttack, CanActivateCondition =
//   IsExistOnBattleArea && HasMatchConditionPermanent(CanSelectPermanentCondition) && card.Owner.MemoryForPlayer
//   >= 3. CanSelectPermanentCondition = IsPermanentExistsOnOpponentBattleAreaDigimon. ORDER=-1, ISOPTIONAL=false.
//   ActivateCoroutine: maxCount = Min(1, MatchConditionPermanentCount); SelectPermanentEffect.SetUp(mode: Custom,
//   canNoSelect:false, canEndNotMax:false, selectPermanentCoroutine); per selected permanent
//   CardEffectCommons.ChangeDigimonDP(permanent, -2000, UntilEachTurnEnd, activateClass).
// Substrate translations only: IEnumerator->Task, StartCoroutine->await; AS-IS `Func<Permanent,bool>` kept
//   verbatim on the canonical shape (id-flip 3b); `card.Owner.MemoryForPlayer` ->
//   `new Player(card.Context, card.Owner).MemoryForPlayer`; GManager.GetComponent -> bridge W4.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Yellow;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_054 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("DP -2000", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Attacking] If you have 3 or more memory, 1 of your opponent's Digimon gets -2000 DP for the turn.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnAttack(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                    {
                        if (new Player(card.Context, card.Owner).MemoryForPlayer >= 3)
                        {
                            return true;
                        }
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

                selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon that will get DP -2000.", "The opponent is selecting 1 Digimon that will get DP -2000.");

                await selectPermanentEffect.Activate();

                async Task SelectPermanentCoroutine(Permanent permanent)
                {
                    await CardEffectCommons.ChangeDigimonDP(targetPermanent: permanent, changeValue: -2000, effectDuration: EffectDuration.UntilEachTurnEnd, activateClass: activateClass);
                }
            }
        }

        return cardEffects;
    }
}
