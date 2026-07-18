// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// EXEMPLAR-T3B 정본 카드 — Ceresmon (BT3_056, Digimon / Green) — 수확 STOP 카드
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT3/Green/BT3_056.cs (487 lines, 3 regions)
//    * <Digisorption -3>          :16-277 (BeforePayCost — 진화-흡수 시 아군 1체 서스펜드→진화 코스트 -3)
//    * [Your Turn][OPT] 상대 대체  :279-407(WhenDigisorption — Digisorption 서스펜드 대상을 상대로 대체)
//    * ESS 상대 대체(상시)         :409-483(None — CanSuspendByDigisorptionClass 등록)
//
// ② 프리미티브 매핑 (감사 축 이름 — coverage_exemplar_audit_2026-07-18.md §4 #16, 3축):
//    * P:CanSuspendByDigisorptionClass — 감사 MISSING(§3 E:CanSuspendByDigisorptionClass ❌미커버, AS-IS 1장).
//    * T:WhenDigisorption — 감사 🟡stop-only(BT2_045/BT2_050).
//    * X:Digisorption — 감사 🟡stop-only.
//
// ★수확 STOP (design item RD-EXT3-03) — 예상 적중(감사 §6 "Digisorption / G11 연계 / CanSuspendByDigisorptionClass
//   MISSING"). 걸리는 AS-IS 시스템:
//   ─ 근본 갭: <Digisorption -N>의 진입점은 AS-IS BeforePayCost 팔(:16-277)이며, 그 CanUse/CanActivate/후보
//     술어가 `card.Owner.CanTapWhenAbsorbEvolution(permanent, activateClass)`(AS-IS Player.cs) +
//     `CanTapWhenAbsorbEvolution_CheckAvailability`를 호출한다. 이 두 Player 메서드는 미러 Player에 부재
//     (grep 0건) — "진화-흡수 시 아군을 서스펜드해도 되는가"의 가용성/실행 판정. 이것이 없으면 <Digisorption>
//     특수-코스트(옵션 서스펜드→ChangeCostClass -N)의 인과 사슬(AS-IS :184-274, afterSelect에서 서스펜드된
//     경우에만 -3 등록)을 미러 팩토리로 표현할 수 없다. 미러 BeforePayCostReductionEffect는 정적/조건부 델타
//     전용(옵션-코스트 오버로드 없음) — BT2_045/BT2_050 판례와 동일 STOP.
//   ─ 소비 흐름 부재: WhenDigisorption 팔(:279-407)은 진화-흡수 서스펜드 시점에 `autoProcessing_CutIn.
//     PutStackedSkill(...WhenDigisorption 리액터...)`로 컷인을 쌓고, `Mode.Tap` SelectPermanentEffect로 상대
//     디지몬을 대신 서스펜드시키며 `CanSuspendByDigisorptionClass`를 UntilCalculateFixedCostEffect에 등록한다.
//     이 팔의 서브-표면(autoProcessing_CutIn/PutStackedSkill/TriggeredSkillProcess/Players_ForTurnPlayer/
//     GetFieldPermanents/Mode.Tap/CanSuspendByDigisorptionClass)은 미러에 실존하나, 이들을 소비하는 흡수-진화
//     서스펜드-선택 파이프라인(CanTapWhenAbsorbEvolution 경유)이 없어 발화 지점이 없다(잠복).
//   ─ 미러에 없는 표면 목록: Player.CanTapWhenAbsorbEvolution(permanent, cardEffect) /
//     Player.CanTapWhenAbsorbEvolution_CheckAvailability(permanent, cardEffect) — 진화-흡수 서스펜드 가용성
//     판정 2종(2건 모두 부재). 이 2종이 인프라 골의 최소 입력이며, 그 위에 <Digisorption> 옵션-코스트 파이프
//     (선택 서스펜드→조건부 ChangeCost)가 얹혀야 한다.
//   ─ 대응 원장: 예상 적중(감사 §6 Digisorption 클러스터, G11 연계). 상환 시 이 STOP throw를 제거하고 세 팔을
//     1:1 포팅(위 AS-IS 라인 앵커)하며 witness의 assert를 뒤집을 것.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Green;

using System;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT3_056 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // ★수확 STOP (RD-EXT3-03): <Digisorption -3>의 진입점 BeforePayCost 팔은 미러 Player에 부재한
        //   CanTapWhenAbsorbEvolution/_CheckAvailability(진화-흡수 서스펜드 가용성 판정)에 걸린다. AS-IS
        //   BeforePayCost 창(:16-277)이 스캔될 때 정직 STOP — 헤더 명세 참조. 상환 시 이 throw를 제거하고
        //   BeforePayCost/WhenDigisorption/None 세 팔을 1:1 포팅.
        if (timing == EffectTiming.BeforePayCost)
        {
            throw new NotSupportedException(
                "STOP: BT3_056 <Digisorption -3> BeforePayCost — 진화-흡수 옵션 서스펜드→조건부 코스트 -3의 " +
                "진입 판정 Player.CanTapWhenAbsorbEvolution(_CheckAvailability)이 미러 Player에 부재 — design item RD-EXT3-03.");
        }

        // WhenDigisorption(:279-407) / None ESS(:409-483) 팔은 위 BeforePayCost 진입 흐름
        // (CanTapWhenAbsorbEvolution 경유)이 없어 발화 지점이 없다 — 소비 흐름 부재로 등록 생략(잠복 STOP,
        // 동일 RD-EXT3-03). 상환 시 세 팔 동반 포팅.
        _ = card;
        return cardEffects;
    }
}
