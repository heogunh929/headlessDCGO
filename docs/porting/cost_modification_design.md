# Cost-Modification — 설계 (PRIM-P0 Build Order 4)

- 작성일: 2026-07-05. 근거: AS-IS `ChangeCostClass` 2-tier + 헤드리스 비용 파이프라인 조사.
- 대상: ~229장. 정적 ±비용은 대부분 존재; 갭은 one-shot before-pay 감액 + digivolve/option 재해소.

## 0. 결론

헤드리스 비용 파이프라인은 이미 AS-IS 2-tier의 충실한 미러이고, one-shot before-pay는 **PLAY엔 이미 구현**
(`SuspendCostReductionEffect` + `PlayCardAction` 재해소). 파이프라인 재작성 불필요. **두 구조적 언블록:**
- **#2 일반 비-인터랙티브 before-pay 감액 팩토리**(BT18_057 형태, ~134장, PLAY 즉시 유효) — `SuspendCostReductionEffect` 미러.
- **#1 DigivolveAction·OptionActivateAction 비용 재해소**(~85장) — PlayCardAction seam 미러, 액션 핸들러 국소 변경.

## 1. AS-IS

`ChangeCostClass`(2-tier: display `GetChangedCostItselef` / paying `GetChangedPayingCost`). One-shot =
`card.Owner.UntilCalculateFixedCostEffect.Add(_ => changeCostClass)` — 이번 계산에만 쓰이고 lock 후 clear.
- one-shot before-pay 감액 **164/226(지배)** — 대개 `BeforePayCost` ActivateClass에서 등록(134).
- 연속 정적 ~50, durational ~28-37, ShowReducedCost 131(UI no-op), 지불후 자기제거 7, SET 3.

## 2. 헤드리스 기존

- **연속 2-tier 미러**: `PlayCostModifier`(Stage/Mode/IsUpDown/Roots), `ContinuousModifierGate.Resolve{Play,Digivolution}Cost`
  (:33/:48), `CannotReduceCost` 면역(:62). 팩토리 `ChangePlayCostStaticEffect`/`ChangeDigivolutionCost`.
- **one-shot 기계(PLAY)**: `EffectDuration.UntilCalculateFixedCost`(=7) 스크래치, `ExpireFixedCostCalc` 자동 만료
  (PlayCardAction:76·Digivolve:181·Option:57 모두 호출). `SuspendCostReductionEffect`가 유일 구현체(인터랙티브).
- **BeforePayCost/AfterPayCost 방출**: 3액션 모두. 단 **재해소는 PLAY만**(PlayCardAction:121-137). Digivolve:176-177·
  Option:53-54는 `payload.MemoryCost`를 그대로 지불 = 액션 생성 시점 고정(= "cost locked").
- ShowReducedCost = skeleton no-op(정상). SET-cost = 팩토리 throw(3장).

## 3. 설계

### #2 일반 before-pay 감액 팩토리 (이 커밋)
`BeforePayCostReductionEffect : IActivatedCardEffect` — 선택 없이, 조건 충족 시 `UntilCalculateFixedCost` 태그
`PlayCostDelta = -amount` 바인딩을 등록(SuspendCostReductionEffect.BuildReductionBinding 미러, target=Card.InstanceId).
```
CardEffectFactory.BeforePayCostReductionEffect(card, Func<int> amount | int amount, Func<bool>? condition, desc)
```
- `ActivatedEffectResolver`에 case 추가(SuspendCostReduction 옆): 조건 통과 시 등록.
- PLAY: PlayCardAction이 BeforePayCost 재해소하므로 즉시 반영. Digivolve/Option: #1 후 반영.

### #1 Digivolve/Option 재해소 (후속 배치)
DigivolveAction·OptionActivateAction의 BeforePayCost 방출 직후 `ResolveAsync(BeforePayCost)` → 비용
재-read(`ResolveDigivolutionCost`/`ResolvePlayCost`) → 감액분 반영 후 Pay. PlayCardAction:121-137 seam 미러.
availability 사전할인은 `BeforePayCostAvailabilityReduction` 미러.

## 4. 경계 (fidelity)
- v1 #2는 **자기-비용(this play/digivolve)** 감액 — BeforePayCost ActivateClass가 자기 카드에 등록되는 지배 형태.
  타 카드 비용 감액은 연속 modifier(shape a, 기존 팩토리).
- 지불 후 자기제거 "next-time-only"(7장), SET-cost(3장)는 소규모 후속.
- ShowReducedCost는 no-op 유지(131장, 작업 0).

## 5. 검증
- #2: BeforePayCost 감액 효과를 가진 카드를 PlayCardAction으로 플레이 → 지불 메모리 = base - 감액 확인. 조건
  불충족 시 감액 안 됨. 만료(UntilCalculateFixedCost) 후 잔존 안 함.
- 전체 스위트 무회귀.

## 6. 관련
- [ALL_CARD_PRIMITIVE_BACKLOG.md](ALL_CARD_PRIMITIVE_BACKLOG.md) B.O.4.
- 재사용: `SuspendCostReductionEffect`(:2064)/`BuildReductionBinding`, `ContinuousModifierGate.Resolve*Cost`,
  `EffectDuration.UntilCalculateFixedCost`, `PlayCardAction`(재해소 seam).
