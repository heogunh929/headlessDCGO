// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Sonnet 트랜치 S4 카드 — P_165 (Digimon / Yellow)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/P/Yellow/P_165.cs (101 lines, 4 regions)
//    * [Security]         :15-18  (SecuritySkill — PlaySelfDigimonAfterBattleSecurityEffect)
//    * [When Digivolving] :22-52  (OnEnterFieldAnyone, CanTriggerWhenDigivolving — [Familiar] 토큰 플레이)
//    * [On Play]          :56-86  (OnEnterFieldAnyone, CanTriggerOnPlay — 동일 몸통)
//    * Inherited Effect   :90-96  (WhenPermanentWouldBeDeleted — BarrierSelfEffect, isInheritedEffect)
//
// ② 프리미티브 매핑:
//    * P:PlaySelfDigimonAfterBattleSecurityEffect — [Security] (AS-IS :17; CardEffectFactory.cs:988).
//      **표면 확인**: 등록 arm(CanUseCondition/CanActivateCondition)은 실장돼 실행되나, ActivateCoroutine
//      본문은 design item RD-P6C3-B2로 NotSupportedException throw(unlanded Player.UntilEndBattleEffects/
//      Permanent.UntilOpponentTurnEndEffects grant bucket + DestroyPermanentsClass 필요). 이 latent 갭은
//      기존 실카드 EX10_029([Security] 동일 몸통, EXEMPLAR-T1 정본)가 이미 동일하게 안고 있는 선례 —
//      등록-arm만 포팅하고 소비자(런타임 발화) 갭은 그대로 상속(공용층 미수정).
//    * P:PlaySelfDeleteFamiliarToken — [When Digivolving]/[On Play] 공유 몸통 (AS-IS :50,84;
//      CardEffectCommons.cs:2545).
//    * P:BarrierSelfEffect — Inherited Effect (AS-IS :94; CardEffectFactory/KeyWordEffects/Barrier.cs).
//
// ③ 배선 관례 근거: AS-IS 자체가 [When Digivolving]과 [On Play] 둘 다 timing ==
//    EffectTiming.OnEnterFieldAnyone 한 키에 SEPARATE ActivateClass로 등록 — EX5_058/EX10_043과 동형.
//    trigger-wiring rule 3(이중-키 등록 금지) 적용: [When Digivolving] arm만 WhenDigivolving 전용 키로
//    재배선, [On Play] arm은 그대로 둔다.
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, StartCoroutine→await (BT8_092 idiom).
//    * AS-IS :45,79 `card.Owner.fieldCardFrames.Count(frame => frame.IsEmptyFrame()) >= 1` — 빈-프레임 용량
//      체크(RD-P6C1-1). 이 카드는 `IsExistOnBattleAreaDigimon(card) && <프레임체크>` 형태로 co-conjunct가
//      있으므로 co-conjunct 절반만 유지하고 프레임체크 절반 소거(BT19_091 관례).
//    * `PlaySelfDeleteFamiliarToken(activateClass)`(AS-IS 1st arg=ICardEffect) →
//      `PlaySelfDeleteFamiliarToken(card)`(mirror 1st arg=CardSource sourceCard; ST3_13 idiom과 동형).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.P.Yellow;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class P_165 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Security

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.PlaySelfDigimonAfterBattleSecurityEffect(card: card));
        }

        #endregion

        #region When Digivolving

        // ③ 배선: AS-IS 이 arm도 OnEnterFieldAnyone(:24)에 등록하지만, 미러 방언은 WhenDigivolving
        //   전용 키(trigger-wiring rule 3 — 이중-키 등록 금지).
        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Play 1 [Familiar] Token", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDescription());
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return
                    "[When Digivolving] Play 1 [Familiar] Token. (Digimon/Yellow/3000 DP/[On Deletion] 1 of your opponent's Digimon gets -3000 DP for the turn.) At the end of your opponent's turn, delete that token.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                // AS-IS :45 — 빈-프레임 용량 체크 절반 소거(RD-P6C1-1); co-conjunct만 유지.
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                await CardEffectCommons.PlaySelfDeleteFamiliarToken(card);
            }
        }

        #endregion

        #region On Play

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Play 1 [Familiar] Token", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDescription());
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return
                    "[On Play] Play 1 [Familiar] Token. (Digimon/Yellow/3000 DP/[On Deletion] 1 of your opponent's Digimon gets -3000 DP for the turn.) At the end of your opponent's turn, delete that token.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnPlay(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                // AS-IS :79 — 빈-프레임 용량 체크 절반 소거(RD-P6C1-1); co-conjunct만 유지.
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                await CardEffectCommons.PlaySelfDeleteFamiliarToken(card);
            }
        }

        #endregion

        #region Inherited Effect

        if (timing == EffectTiming.WhenPermanentWouldBeDeleted)
        {
            cardEffects.Add(CardEffectFactory.BarrierSelfEffect(isInheritedEffect: true, card: card, condition: null));
        }

        #endregion

        return cardEffects;
    }
}
