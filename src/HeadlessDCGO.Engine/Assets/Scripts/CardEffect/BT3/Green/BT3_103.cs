// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Sonnet 트랜치 S4 카드 — BT3_103 (Option / Green)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT3/Green/BT3_103.cs (261 lines, 2 timing 블록)
//    * [Main]     :16-232 (OptionSkill — 이번 턴 다음 자기 초록 디지몬 진화 시, 자기 디지몬 1체 서스펜드로
//      해당 진화 메모리비용 -5; BeforePayCost/AfterPayCost 페어 grant + cleanup)
//    * [Security] :234-256 (SecuritySkill — 손패로)
//
// ② 프리미티브 매핑:
//    * P:ChangeCostClass — [Main] 몸통이 BeforePayCost 타이밍에 등록하는 비용감소 그랜트(AS-IS :135-160).
//    * P:AddEffectToPlayer — grant/cleanup 등재·해제 페어(AS-IS :42,49, :215-216 Remove).
//    * P:AddThisCardToHand — [Security] 몸통.
//
// ③ 배선 관례 근거: [Main] → OptionSkill + CanTriggerOptionMainEffect 그대로; [Security] → SecuritySkill +
//    CanTriggerSecurityEffect + SetIsSecurityEffect(true) 그대로.
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, StartCoroutine→await (BT8_092 idiom); AS-IS :34-36
//      `ContinuousController.instance.PlaySE(...)` + `yield return new WaitForSeconds(0.2f)` — UI 연출(가드
//      없음), 스트립.
//    * `card.Owner.UntilCalculateFixedCostEffect`/`UntilEachTurnEndEffects` → `new Player(card.Context,
//      card.Owner).*`(symbol_map_guide §2.2).
//    * `CardEffectCommons.HasMatchConditionPermanent(CanSelectPermanentCondition)`(AS-IS 1-인자, :98) →
//      mirror는 leading card 파라미터 필수(§2.3 비대칭 — card-less 오버로드 삭제됨):
//      `HasMatchConditionPermanent(card, CanSelectPermanentCondition)`.
//    * `SelectPermanentEffect.canTargetCondition`은 id-형 — AS-IS `Func<Permanent,bool>` 술어는 그대로 두고
//      id 어댑터를 덧댐(BT17_026 idiom).
//    * (RD-S4-BT3_103-note, 정보성 — STOP 아님) AS-IS :42,49 `CardEffectCommons.AddEffectToPlayer(...,
//      cardEffect: null, ..., getCardEffect: getCardEffect)` — mirror AddEffectToPlayer가
//      `ArgumentNullException.ThrowIfNull(cardEffect)`를 추가(AS-IS 원본엔 이 가드가 없음 — mirror가 AS-IS보다
//      엄격한 시그니처 델타). `getCardEffect` 오버라이드가 주어지면 `getCardEffect ??= ...` 단락평가로 인해
//      `cardEffect` 인자값은 어떤 경로로도 읽히지 않음(증명: `??=`는 좌변이 non-null이면 우변을 절대 평가하지
//      않음) — 따라서 실제 동작에 아무 영향 없는 자리에 AS-IS의 리터럴 `null` 대신 실제 non-null 값
//      (동일 effect 객체 activateClass1/activateClass2)을 전달해 가드만 통과시킴(공용층 미수정, 순수 호출부
//      어댑테이션 — id-adapter와 동종의 조정). 제거 시점의 `.Remove(getCardEffect)` 매칭은 델리게이트
//      값-동등성(대상+메서드)으로 유지되므로 동일 로컬함수 참조를 그대로 저장·전달.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Green;

using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT3_103 : CEntity_Effect
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
                return "[Main] The next time one of your green Digimon digivolves this turn, you may suspend 1 of your Digimon to reduce the memory cost of the digivolution by 5.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOptionMainEffect(hashtable, card);
            }

            Task ActivateCoroutine(Hashtable _hashtable)
            {
                // AS-IS :34-36 PlaySE + WaitForSeconds(0.2f) — UI 연출(가드 없음), 스트립. 스트립 후 이 메서드
                // 자체 몸통엔 실제 await 지점이 없음(중첩 로컬함수 ActivateCoroutine1/2는 별도 델리게이트로
                // 등록되어 있어 여기서 직접 호출되지 않음) — BT8_092 "lone terminal yield return null" idiom
                // 적용: non-async Task 메서드로 전환.

                ActivateClass activateClass1 = new ActivateClass();
                Func<EffectTiming, ICardEffect> getCardEffect = GetCardEffect;
                activateClass1.SetUpICardEffect("Digivolution Cost -5", CanUseCondition1, card);
                activateClass1.SetUpActivateClass(CanActivateCondition1, ActivateCoroutine1, -1, true, EffectDiscription1());
                CardEffectCommons.AddEffectToPlayer(effectDuration: EffectDuration.UntilEachTurnEnd, card: card, cardEffect: activateClass1, timing: EffectTiming.None, getCardEffect: getCardEffect);

                ActivateClass activateClass2 = new ActivateClass();
                Func<EffectTiming, ICardEffect> getCardEffect1 = GetCardEffect1;
                activateClass2.SetUpICardEffect("Remove Effect", CanUseCondition1, card);
                activateClass2.SetUpActivateClass(null, ActivateCoroutine2, -1, false, "");
                activateClass2.SetIsBackgroundProcess(true);
                CardEffectCommons.AddEffectToPlayer(effectDuration: EffectDuration.UntilEachTurnEnd, card: card, cardEffect: activateClass2, timing: EffectTiming.None, getCardEffect: getCardEffect1);

                string EffectDiscription1()
                {
                    return "You may suspend 1 of your Digimon to reduce the memory cost of the digivolution by 5.";
                }

                bool PermanentCondition(Permanent targetPermanent)
                {
                    if (CardEffectCommons.IsPermanentExistsOnOwnerBattleArea(targetPermanent, card))
                    {
                        if (targetPermanent.TopCard.CardColors.Contains("Green"))
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
                        if (CardEffectCommons.CanActivateSuspendCostEffect(permanent.TopCard))
                        {
                            return true;
                        }
                    }

                    return false;
                }

                Permanent? PermanentOf(HeadlessEntityId id) =>
                    card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
                        ? new Permanent(card.Context, id, rec.OwnerId)
                        : null;

                bool CanSelectPermanentById(HeadlessEntityId id)
                {
                    Permanent? permanent = PermanentOf(id);
                    return permanent is not null && CanSelectPermanentCondition(permanent);
                }

                bool CanUseCondition1(Hashtable hashtable)
                {
                    if (CardEffectCommons.CanTriggerWhenPermanentWouldDigivolve(
                        hashtable: hashtable,
                        permanentCondition: PermanentCondition,
                        cardCondition: null))
                    {
                        return true;
                    }

                    return false;
                }

                bool CanActivateCondition1(Hashtable hashtable)
                {
                    if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                    {
                        return true;
                    }

                    return false;
                }

                async Task ActivateCoroutine1(Hashtable _hashtable1)
                {
                    int maxCount = 1;

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectPermanentById,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: true,
                        canEndNotMax: false,
                        selectPermanentCoroutine: null,
                        afterSelectPermanentCoroutine: AfterSelectPermanentCoroutine,
                        mode: SelectPermanentEffect.Mode.Tap,
                        cardEffect: activateClass1);

                    await selectPermanentEffect.Activate();

                    async Task AfterSelectPermanentCoroutine(List<Permanent> permanents)
                    {
                        if (permanents.Count >= 1)
                        {
                            // AS-IS :133 PlaySE — UI 연출, 스트립.

                            ChangeCostClass changeCostClass = new ChangeCostClass();
                            changeCostClass.SetUpICardEffect("Digivolution Cost -5", CanUseCondition1, card);
                            changeCostClass.SetUpChangeCostClass(changeCostFunc: ChangeCost, cardSourceCondition: CardSourceCondition, rootCondition: RootCondition, isUpDown: isUpDown, isCheckAvailability: () => false, isChangePayingCost: () => true);
                            new Player(card.Context, card.Owner).UntilCalculateFixedCostEffect.Add((_timing) => changeCostClass);

                            await CardEffectCommons.ShowReducedCost(_hashtable1);

                            bool CanUseCondition1(Hashtable hashtable)
                            {
                                return true;
                            }

                            int ChangeCost(CardSource cardSource, int Cost, SelectCardEffect.Root root, List<Permanent> targetPermanents)
                            {
                                if (CardSourceCondition(cardSource))
                                {
                                    if (RootCondition(root))
                                    {
                                        if (PermanentsCondition(targetPermanents))
                                        {
                                            Cost -= 5;
                                        }
                                    }
                                }

                                return Cost;
                            }

                            bool PermanentsCondition(List<Permanent> targetPermanents)
                            {
                                if (targetPermanents != null)
                                {
                                    if (targetPermanents.Count(PermanentCondition) >= 1)
                                    {
                                        return true;
                                    }
                                }

                                return false;
                            }

                            bool PermanentCondition(Permanent targetPermanent)
                            {
                                return CardEffectCommons.IsPermanentExistsOnBattleArea(targetPermanent);
                            }

                            bool CardSourceCondition(CardSource cardSource)
                            {
                                return true;
                            }

                            bool RootCondition(SelectCardEffect.Root root)
                            {
                                return true;
                            }

                            bool isUpDown()
                            {
                                return true;
                            }
                        }
                    }
                }

                ICardEffect GetCardEffect(EffectTiming _timing)
                {
                    if (_timing == EffectTiming.BeforePayCost)
                    {
                        return activateClass1;
                    }

                    return null!;
                }

                async Task ActivateCoroutine2(Hashtable _hashtable1)
                {
                    if (CardEffectCommons.CanTriggerWhenPermanentWouldDigivolve(
                        hashtable: _hashtable1,
                        permanentCondition: PermanentCondition,
                        cardCondition: null))
                    {
                        new Player(card.Context, card.Owner).UntilEachTurnEndEffects.Remove(getCardEffect);
                        new Player(card.Context, card.Owner).UntilEachTurnEndEffects.Remove(getCardEffect1);
                        await Task.CompletedTask;
                    }
                }

                ICardEffect GetCardEffect1(EffectTiming _timing)
                {
                    if (_timing == EffectTiming.AfterPayCost)
                    {
                        return activateClass2;
                    }

                    return null!;
                }

                return Task.CompletedTask;
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
