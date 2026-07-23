// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// PILOT-S1 카드 — BT2_082 (Digimon / White)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT2/White/BT2_082.cs (159 lines, no region markers, 2 timing 블록)
//    * [When Attacking]           :15-49  (timing == OnAllyAttack — Diaboromon 토큰 무료 플레이)
//    * [All Turns] Prevent-removal :51-155 (timing == WhenPermanentWouldBeDeleted — 다른 [Diaboromon] 삭제로
//      대신 막기)
//
// ② 프리미티브 매핑:
//    * P:PlayDiaboromonToken(PlayCardsBridge) — [When Attacking] 토큰 플레이 (AS-IS :45-48)
//    * E:SelectPermanentEffect Mode.Custom + P:DeletePeremanentAndProcessAccordingToResult — [All Turns] 대체
//      삭제(다른 [Diaboromon] 삭제 → 이 카드 삭제 취소) (AS-IS :109-153)
//
// ③ 배선 관례 근거:
//    * [When Attacking] → OnAllyAttack + CanTriggerOnAttack(hashtable, card) 게이트(AS-IS :29 그대로).
//    * [All Turns] → WhenPermanentWouldBeDeleted + CanTriggerWhenRemoveField(hashtable, card) &&
//      IsByBattle(hashtable) 게이트(AS-IS :84-89 그대로).
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)`→`await X`
//      (BT8_092 idiom).
//    * `card.Owner.fieldCardFrames.Count(frame => frame.IsEmptyFrame() && frame.IsBattleAreaFrame()) >= 1`
//      (AS-IS :36) — 빈-프레임 용량 절반은 미러 frame-모델 부재로 소거(RD-P6C1-2, BT19_091/BT22_040 헤더 동일
//      근거); IsExistOnBattleArea 절반은 1:1 유지 — 토큰 커먼즈가 capacity 재검사.
//    * `permanent != card.PermanentOfThisCard()` → `permanent != ICardEffect.ResolvePermanentOfThisCard(card)`
//      (BT20_017/BT14_030/TfxDecoy idiom — 미러 Permanent는 InstanceId 값-동등 오버로드 보유).
//    * `card.PermanentOfThisCard()`(가변 필요, SuccessProcess 클로저) → `ICardEffect.ResolvePermanentOfThisCard(card)`
//      (BT8_092 관례).
//    * `thisCardPermanent.willBeRemoveField = false; thisCardPermanent.HideDeleteEffect();` — HideDeleteEffect는
//      UI 연출(Unity WillBeDeletedObject.SetActive) — 미러 확립 관례상 스트립(Decoy.cs/Evade.cs/BT5_086 동일).
//      로드-베어링 `willBeRemoveField = false`만 유지(sweep survivor-fix가 읽는 실효 플래그).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.White;

using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT2_082 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Play 1 Diaboromon Token", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Attacking] You may play 1 [Diaboromon] Token without paying its memory cost. (Diaboromon Tokens are level 6 white Digimon with a memory cost of 14, 3000 DP, and are Mega form, Unidentified type, and Unknown attribute.)";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnAttack(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                // AS-IS :36 `card.Owner.fieldCardFrames.Count(frame => frame.IsEmptyFrame() &&
                // frame.IsBattleAreaFrame()) >= 1` — 빈-프레임 용량 절반은 미러 frame-모델 부재로 소거
                // (확립 어댑테이션 RD-P6C1-2, BT19_091/BT22_040 헤더 동일 근거).
                return CardEffectCommons.IsExistOnBattleArea(card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await CardEffectCommons.PlayDiaboromonToken(activateClass);
            }
        }

        if (timing == EffectTiming.WhenPermanentWouldBeDeleted)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Prevent this Digimon from being deleted", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDiscription());
            activateClass.SetHashString("Substitute_BT2_082");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[All Turns] When this Digimon would be deleted in battle, you may delete one of your other [Diaboromon] to prevent this Digimon from being deleted. ";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card))
                {
                    if (permanent != ICardEffect.ResolvePermanentOfThisCard(card))
                    {
                        if (permanent.TopCard.CardNames.Contains("Diaboromon"))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.CanTriggerWhenRemoveField(hashtable, card))
                    {
                        if (CardEffectCommons.IsByBattle(hashtable))
                        {
                            return true;
                        }
                    }
                }

                return false;
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
                        canNoSelect: true,
                        canEndNotMax: false,
                        selectPermanentCoroutine: SelectPermanentCoroutine,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Custom,
                        cardEffect: activateClass);

                    selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon to delete.", "The opponent is selecting 1 Digimon to delete.");

                    await selectPermanentEffect.Activate();

                    async Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        Permanent thisCardPermanent = ICardEffect.ResolvePermanentOfThisCard(card);

                        await CardEffectCommons.DeletePeremanentAndProcessAccordingToResult(
                            targetPermanents: new List<Permanent>() { permanent },
                            activateClass: activateClass,
                            successProcess: permanents => SuccessProcess(),
                            failureProcess: null);

                        async Task SuccessProcess()
                        {
                            if (thisCardPermanent.TopCard != null)
                            {
                                thisCardPermanent.willBeRemoveField = false;

                                // AS-IS :146 HideDeleteEffect() = UI 연출 — 미러 확립 관례상 스트립
                                // (Decoy.cs/Evade.cs/BT5_086 동일). 로드-베어링 willBeRemoveField=false만 유지.
                            }

                            await Task.CompletedTask;
                        }
                    }
                }
            }
        }

        return cardEffects;
    }
}
