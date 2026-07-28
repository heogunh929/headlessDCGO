// Source: DCGO/Assets/Scripts/CardEffect/BT22/Yellow/BT22_003.cs — "Tapmon".
// P8 CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass) of the WhenLinked INHERITED
// (digivolution-source) branch — the card's ONLY effect (F1-Tier2 WhenLinked witness).
//   [Your Turn][Once Per Turn] When this Digimon gets linked, 1 of your opponent's Digimon gets -2000 DP for the
//   turn. — AS-IS `new ActivateClass()` + SetIsInheritedEffect(true) + SetHashString("BT22_003_DpMinus") +
//   SetUpActivateClass(..., 1, false, ...) (ORDER 1 = once per turn, mandatory) (BT22_003.cs:14-78). CanUse =
//   IsExistOnBattleAreaDigimon && CanTriggerWhenLinked(this-permanent-got-linked). CanActivate =
//   IsExistOnBattleAreaDigimon && IsOwnerTurn && an opponent battle-area Digimon exists. Body =
//   SelectPermanentEffect(Mode.Custom, maxCount Min(1,count)) -> ChangeDigimonDP(-2000, UntilEachTurnEnd) on the pick.
// Substrate translations only: IEnumerator->Task, StartCoroutine->await; `permanent == card.PermanentOfThisCard()`
// -> `permanent.InstanceId == card.PermanentOfThisCard().TopInstanceId` (an inherited source resolves its host via
// TopInstanceId); `GManager.instance.GetComponent<SelectPermanentEffect>()` + full AS-IS SetUp (bridge W4); AS-IS
// `Func<Permanent,bool> OpponentCondition` supplied directly as the Permanent predicate to both the scans
// (HasMatch / MatchCount) and SetUp. The DP change targets the selected Permanent (which carries its own
// OwnerId), so the opponent-stat target is registered against the correct owner.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT22.Yellow;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT22_003 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.WhenLinked)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("-2000 DP", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, false, EffectDiscription());
            activateClass.SetHashString("BT22_003_DpMinus");
            activateClass.SetIsInheritedEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Your Turn] [Once Per Turn] When this Digimon gets linked, 1 of your opponent's Digimon gets -2000 DP for the turn.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card) &&
                       CardEffectCommons.CanTriggerWhenLinked(hashtable, permanent => permanent.InstanceId == card.PermanentOfThisCard().TopInstanceId, null);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card)
                    && CardEffectCommons.IsOwnerTurn(card)
                    && CardEffectCommons.HasMatchConditionOpponentsPermanent(card, OpponentCondition);
            }

            bool OpponentCondition(Permanent permanent) => CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                if (CardEffectCommons.HasMatchConditionOpponentsPermanent(card, OpponentCondition))
                {
                    Permanent targetPermanent = null;
                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionOpponentsPermanentCount(card, OpponentCondition));

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();
                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: OpponentCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: false,
                        canEndNotMax: false,
                        selectPermanentCoroutine: SelectPermanentCoroutine,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Custom,
                        cardEffect: activateClass);

                    async Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        targetPermanent = permanent;
                        await Task.CompletedTask;
                    }

                    selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon to -2000 DP for the turn.", "Your opponent is selecting 1 Digimon to -2000 DP for the turn.");
                    await selectPermanentEffect.Activate();

                    if (targetPermanent != null)
                    {
                        await CardEffectCommons.ChangeDigimonDP(targetPermanent: targetPermanent, changeValue: -2000, effectDuration: EffectDuration.UntilEachTurnEnd, activateClass: activateClass);
                    }
                }
            }
        }

        return cardEffects;
    }
}
