// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/P/Blue/P_098.cs (Digimon / Blue, 3 regions, all AS-IS OnEnterFieldAnyone)
//    * [On Play]  can't-be-deleted-in-battle grant :10-98   (CanTriggerOnPlay)
//    * [When Digivolving] same grant                :100-192 (CanTriggerWhenDigivolving)
//    * [Your Turn][Once Per Turn] GainRush          :194-297 (CanTriggerOnPermanentPlay + IsByEffect, maxUse 1)
//
// ② 프리미티브 매핑: P:GainCanNotBeDeletedByBattle (relocated+live, sibling template AD1_011), K:Rush (GainRush).
//
// ③ 배선 관례 (trigger-wiring-porting-rules + AD1_011/BT1_086/BT14_030 확립):
//    * [On Play]            → OnEnterFieldAnyone + CanTriggerOnPlay(hashtable, card).
//    * [When Digivolving]   → WhenDigivolving 전용 키 + CanTriggerWhenDigivolving(hashtable, card) (미러 DigivolveAction은
//      WhenDigivolving만 해소 — AD1_011 규약; AS-IS는 OnEnterFieldAnyone 이중이지만 이중-키 등록 금지).
//    * [Your Turn] OnPermanentPlay → OnEnterFieldAnyone + CanTriggerOnPermanentPlay (BT1_086 관례).
//
// 치환(substrate translations only):
//    * `card.PermanentOfThisCard()` → `ICardEffect.ResolvePermanentOfThisCard(card)`.
//    * `permanent.TopCard.CardColors.Contains(CardColor.Blue)` → `.CardColors.Contains("Blue")` (미러 문자열-색 표준).
//    * `permanent != card.PermanentOfThisCard()` → `permanent != ICardEffect.ResolvePermanentOfThisCard(card)`
//      (Permanent == 오버로드 InstanceId 기반; BT2_082/BT14_030 확립).
//    * `Math.Min(1, MatchConditionPermanentCount(cond))` → Permanent landing overload
//      `MatchConditionPermanentCount(card, CanSelectPermanentCondition)` (AS-IS 1:1);
//      SelectPermanentEffect.SetUp canTargetCondition = 동일 Permanent-형 predicate 직접 전달(id-어댑터 3b 이관 완료).
//    * `yield return StartCoroutine(GainCanNotBeDeletedByBattle(...))` → 미러 SYNC bool (AS-IS 코루틴은 UI만 구동) —
//      plain 호출(AD1_011 확립). `GainRush`는 async Task → await.
//    * `SelectPermanentCoroutine` IEnumerator + `yield return null` → `async Task` + `await Task.CompletedTask`.
//    * 4-arg 배틀 술어 첫 파라미터 `permanent` → `permanent1` (enclosing SelectPermanentCoroutine(Permanent permanent)
//      과의 CS0136 쉐도잉 회피 — AD1_011 미러 확립 rename; 몸통 참조 동반 개명).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.P.Blue;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class P_098 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region On Play

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Your 1 Digimon cannot be deleted in battle", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[On Play] 1 of your blue Digimon cannot be deleted in battle until the end of your opponent's turn.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card))
                {
                    if (permanent.TopCard.CardColors.Contains("Blue"))
                    {
                        return true;
                    }
                }

                return false;
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
                    bool CanNotBeDestroyedByBattleCondition(Permanent permanent1, Permanent AttackingPermanent, Permanent DefendingPermanent, CardSource DefendingCard)
                    {
                        if (permanent1 == AttackingPermanent)
                        {
                            return true;
                        }

                        if (permanent1 == DefendingPermanent)
                        {
                            return true;
                        }

                        return false;
                    }

                    CardEffectCommons.GainCanNotBeDeletedByBattle(
                        targetPermanent: permanent,
                        canNotBeDestroyedByBattleCondition: CanNotBeDestroyedByBattleCondition,
                        effectDuration: EffectDuration.UntilOpponentTurnEnd,
                        activateClass: activateClass,
                        effectName: "Can't be deleted in battle");

                    await Task.CompletedTask;
                }
            }
        }

        #endregion

        #region When Digivolving

        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Your 1 Digimon cannot be deleted in battle", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Digivolving] 1 of your blue Digimon cannot be deleted in battle until the end of your opponent's turn.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card))
                {
                    if (permanent.TopCard.CardColors.Contains("Blue"))
                    {
                        return true;
                    }
                }

                return false;
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

                selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon that will get effects.", "The opponent is selecting 1 Digimon that will get effects.");

                await selectPermanentEffect.Activate();

                async Task SelectPermanentCoroutine(Permanent permanent)
                {
                    bool CanNotBeDestroyedByBattleCondition(Permanent permanent1, Permanent AttackingPermanent, Permanent DefendingPermanent, CardSource DefendingCard)
                    {
                        if (permanent1 == AttackingPermanent)
                        {
                            return true;
                        }

                        if (permanent1 == DefendingPermanent)
                        {
                            return true;
                        }

                        return false;
                    }

                    CardEffectCommons.GainCanNotBeDeletedByBattle(
                        targetPermanent: permanent,
                        canNotBeDestroyedByBattleCondition: CanNotBeDestroyedByBattleCondition,
                        effectDuration: EffectDuration.UntilOpponentTurnEnd,
                        activateClass: activateClass,
                        effectName: "Can't be deleted in battle");

                    await Task.CompletedTask;
                }
            }
        }

        #endregion

        #region Your Turn - Once Per Turn Rush

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Your 1 Digimon gains Rush", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, false, EffectDiscription());
            activateClass.SetIsInheritedEffect(true);
            activateClass.SetHashString("GainRush_P_098");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Your Turn] [Once Per Turn] When you play another Digimon by an effect, 1 of your blue Digimon gains <Rush> (This Digimon can attack the turn it comes into play) for the turn.";
            }

            bool PermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card))
                {
                    if (permanent != ICardEffect.ResolvePermanentOfThisCard(card))
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card))
                {
                    if (permanent.TopCard.CardColors.Contains("Blue"))
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.IsOwnerTurn(card))
                    {
                        if (CardEffectCommons.CanTriggerOnPermanentPlay(hashtable, PermanentCondition))
                        {
                            if (CardEffectCommons.IsByEffect(hashtable, null))
                            {
                                return true;
                            }
                        }
                    }
                }

                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    return true;
                }

                return false;
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

                    selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon that will get Rush.", "The opponent is selecting 1 Digimon that will get Rush.");

                    await selectPermanentEffect.Activate();

                    async Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        await CardEffectCommons.GainRush(
                            targetPermanent: permanent,
                            effectDuration: EffectDuration.UntilEachTurnEnd,
                            activateClass: activateClass);
                    }
                }
            }
        }

        #endregion

        return cardEffects;
    }
}
