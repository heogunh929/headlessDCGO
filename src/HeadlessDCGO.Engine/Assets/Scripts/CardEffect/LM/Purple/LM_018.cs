// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Sonnet 트랜치 S2 카드 — LM_018 (Digimon / Purple)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/LM/Purple/LM_018.cs (98 lines, no region markers)
//    * [On Play] :12-94 (timing == EffectTiming.OnEnterFieldAnyone + CanTriggerOnPlay 게이트)
//      — 레벨 4 이하 디지몬 1체 삭제 → 성공 시 [Gyuukimon] 토큰 1장 무료 플레이.
//
// ② 프리미티브 매핑:
//    * P:SelectPermanentEffect Mode.Destroy — 레벨 4 이하 디지몬 1체 삭제 (AS-IS :57-76).
//    * P:DeletePeremanentAndProcessAccordingToResult — 삭제 성공/실패 분기 (AS-IS :80).
//    * P:PlayGyuukimonToken — 성공 시 토큰 플레이 (AS-IS :90).
//
// ③ 배선 관례 근거 (trigger-wiring-porting-rules):
//    * [On Play] → EffectTiming.OnEnterFieldAnyone + CanTriggerOnPlay(hashtable, card) 게이트(AS-IS :42 그대로).
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)`→`await X`
//      (BT17_026 idiom).
//    * AS-IS card-less `HasMatchConditionPermanent(cond)`(전역 스캔) → 미러 `(card, cond)` 오버로드
//      (ST2_06/ST1_08 관례).
//    * `IsPermanentExistsOnBattleArea(permanent)`(1-인자, 양측 전장) — 미러 동일 시그니처 그대로.
//    * AS-IS :85-91 SuccessProcess 내부에서 새 로컬 activateClass를 만들어 `cardEffects.Add(activateClass)` 후
//      곧바로 PlayGyuukimonToken(activateClass)을 호출하는 원본 구조(코루틴 실행 시점엔 이미 CardEffects()가
//      반환된 뒤라 이 Add는 실질 무효 — AS-IS 원본의 dead-but-harmless 관용구, 내부 activateClass의
//      SetUpActivateClass도 바깥쪽과 같은 CanUseCondition/CanActivateCondition/ActivateCoroutine을 재참조하는
//      AS-IS 원본 그대로) — 단순화 없이 1:1 보존(no-simplification 원칙: AS-IS 버그/관용구도 보존).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.LM.Purple;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class LM_018 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Delete 1 level 4 Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[On Play] Delete 1 level 4 or lower Digimon. If you did, you may play 1 [Gyuukimon] Token without paying the cost. (Digimon/Cost 7/Lv.5/Purple/Ultimate/Virus/Dark Animal/3000 DP)";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnBattleArea(permanent))
                {
                    if (permanent.IsDigimon)
                    {
                        if (permanent.Level <= 4)
                        {
                            return true;
                        }
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
                    return true;
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

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                {
                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectPermanentById,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: 1,
                        canNoSelect: false,
                        canEndNotMax: false,
                        selectPermanentCoroutine: SelectPermanentCoroutine,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Destroy,
                        cardEffect: activateClass);

                    selectPermanentEffect.SetUpCustomMessage("Select 1 Level 4 or lower Digimon", "Opponent is selecting level 4 or lower Digimon");

                    await selectPermanentEffect.Activate();

                    async Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        await CardEffectCommons.DeletePeremanentAndProcessAccordingToResult(targetPermanents: new List<Permanent>() { permanent }, activateClass: activateClass, successProcess: permanents => SuccessProcess(), failureProcess: null);
                    }

                    async Task SuccessProcess()
                    {
                        ActivateClass innerActivateClass = new ActivateClass();
                        innerActivateClass.SetUpICardEffect("Play 1 Gyuukimon Token", CanUseCondition, card);
                        innerActivateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDiscription());
                        cardEffects.Add(innerActivateClass);

                        await CardEffectCommons.PlayGyuukimonToken(innerActivateClass);
                    }
                }
            }
        }

        return cardEffects;
    }
}
