// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Sonnet 트랜치 S4 카드 — BT5_056 (Digimon / Green)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT5/Green/BT5_056.cs (149 lines, 2 timing 블록)
//    * [Main] <Digi-Burst 2>          :16-57  (OnDeclaration — 자기 디지볼루션카드 2장 트래시로 발동, 전
//      디지몬 DP+2000 for the turn)
//    * [Your Turn][Once Per Turn] ESS :59-145 (OnUseDigiburst — 자기 디지몬이 Digi-Burst 발동 시 상대
//      디지몬 1체 공격/블록 불가 until 상대 다음 턴 끝)
//
// ② 프리미티브 매핑 (ST4_13 established idiom과 동형 — 감사 시절 STOP-예상 스테일, G-Xros 아님 G-DigiBurst
//    소비 파이프가 착지돼 있음):
//    * P:IDigiBurst — [Main] CanUseCondition/ActivateCoroutine 몸통(AS-IS :32,43; mirror Script/CardController.cs
//      "Digi-Burst" region, ST4_13 idiom).
//    * P:ChangeDigimonDPPlayerEffect — [Main] 후반 DP+2000 (AS-IS :50-54; mirror overload은 Task 반환 —
//      CardEffectCommons/GiveEffect/GiveEffectToPlayer/ChangeDP.cs:13, ST3_13:118 established idiom).
//    * T:OnUseDigiburst — [Your Turn] 신규 창 타이밍 소비자. 표면 실존 확인: EffectTiming.OnUseDigiburst 키
//      실존(CardController.cs:465,579,589 StackSkillInfos 발화), CanTriggerWhenUseDigiBurst(Hashtable,
//      Func<Permanent,bool>, Func<ICardEffect,bool>) 실장(CanUseEffects/WhenUseDigiBurst.cs:13, 몸통 실재).
//      그대로 포팅.
//    * P:GainCanNotAttack / GainCanNotBlock — ESS 몸통(AS-IS :139,141; CardEffectCommons.cs:3059,3067).
//
// ③ 배선 관례 근거: [Main] → OnDeclaration 그대로(선언형); [Your Turn] ESS → AS-IS 자체가
//    EffectTiming.OnUseDigiburst 키를 씀(방언 변환 대상 아님, trigger-wiring-porting-rules에 항목 없음).
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, StartCoroutine→await (BT8_092 idiom); `card.PermanentOfThisCard()`(가변 필요)
//      → `ICardEffect.ResolvePermanentOfThisCard(card)`(ST4_13 idiom).
//    * `Func<Permanent,bool> CanSelectPermanentCondition` → SelectPermanentEffect.canTargetCondition에 직접 전달
//      (정본 Func<Permanent,bool>, id 어댑터 없음); MatchConditionPermanentCount는 (card, Permanent-술어) 오버로드.
//    * `ChangeDPClass changeDPClass = new ChangeDPClass();`(AS-IS :55, 미사용 dead-local) — AS-IS 원문 그대로
//      보존(버그/사문 보존 원칙; 아무 것도 하지 않는 죽은 변수 선언 1:1 유지).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT5.Green;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT5_056 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Main - Digi-Burst

        if (timing == EffectTiming.OnDeclaration)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("DP +2000 to your all Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Main] <Digi-Burst 2> (Trash 2 of this Digimon's Digivolution cards to activate the effect below.) - All of your Digimon get +2000 DP for the turn.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (new IDigiBurst(ICardEffect.ResolvePermanentOfThisCard(card), 2, activateClass).CanDigiBurst())
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await new IDigiBurst(ICardEffect.ResolvePermanentOfThisCard(card), 2, activateClass).DigiBurst();

                bool PermanentCondition(Permanent permanent)
                {
                    return CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card);
                }

                await CardEffectCommons.ChangeDigimonDPPlayerEffect(
                                    permanentCondition: PermanentCondition,
                                    changeValue: 2000,
                                    effectDuration: EffectDuration.UntilEachTurnEnd,
                                    activateClass: activateClass);

                ChangeDPClass changeDPClass = new ChangeDPClass();
            }
        }

        #endregion

        #region Your Turn ESS - When Use Digi-Burst

        if (timing == EffectTiming.OnUseDigiburst)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Opponent's 1 Digimon can't Attack or Block", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, false, EffectDiscription());
            activateClass.SetHashString("CantAttackBlock_BT5_056");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Your Turn][Once Per Turn] When one of your Digimon activates <Digi-Burst>, 1 of your opponent's Digimon can't attack or block until the end of your opponent's next turn.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card))
                {
                    if (permanent.CanSelectBySkill(activateClass))
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
                    if (CardEffectCommons.CanTriggerWhenUseDigiBurst(hashtable: hashtable,
                        permanentCondition: permanent => CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card),
                        cardEffectCondition: null))
                    {
                        return true;
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

                    selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon that will get effects.", "The opponent is selecting 1 Digimon that will get effects.");

                    await selectPermanentEffect.Activate();

                    Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        Permanent selectedPermanent = permanent;

                        // GainCanNotAttack/GainCanNotBlock are synchronous bool-returning mutators on the
                        // mirror (CardEffectCommons.cs:3059,3067) — no Task/await surface (unlike the AS-IS
                        // IEnumerator coroutine shape); BT8_092 "lone terminal yield return null" idiom applies.
                        CardEffectCommons.GainCanNotAttack(targetPermanent: selectedPermanent, defenderCondition: null, effectDuration: EffectDuration.UntilOpponentTurnEnd, sourceCard: activateClass.EffectSourceCard ?? card, effectName: "Can't Attack");

                        CardEffectCommons.GainCanNotBlock(targetPermanent: selectedPermanent, attackerCondition: null, effectDuration: EffectDuration.UntilOpponentTurnEnd, sourceCard: activateClass.EffectSourceCard ?? card, effectName: "Can't Block");

                        return Task.CompletedTask;
                    }
                }
            }
        }

        #endregion

        return cardEffects;
    }
}
