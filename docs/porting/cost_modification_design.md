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

### #1 Digivolve/Option 재해소 (후속 배치 — #2보다 큼)
DigivolveAction·OptionActivateAction의 BeforePayCost 방출 직후 `ResolveAsync(BeforePayCost)` → 비용
재-read(`ResolveDigivolutionCost`/`ResolvePlayCost`) → 감액분 반영 후 Pay. PlayCardAction:121-137 seam 미러.
availability 사전할인은 `BeforePayCostAvailabilityReduction` 미러.

**정밀 발견(metric 라우팅):** `NumericModifier`는 metric별(ModifierHelpers.cs:9). `playCostDelta`→**PlayCost**
metric(:429), `digivolutionCostDelta`→**DigivolutionCost** metric(:434) — 별개 키. Option 비용은 PlayCost
metric(`ResolveOptionCost`→`ResolvePlayCost`). 해결: `BeforePayCostReductionEffect`가 **두 델타 모두 등록**(카드는
play XOR digivolve XOR option이라 해당 metric만 적용 — 이미 반영됨, 무해).

**⚠️ #1 시도 결과 — 액션-컨텍스트 게이팅 필요(회귀 발견, 2026-07-05):** DigivolveAction/OptionActivateAction에
PlayCardAction식 `ResolveAsync(BeforePayCost)` 재해소 seam을 넣으니, **진화되는 카드의 BeforePayCost 효과가
digivolve 시에도 발화**해 회귀(G9-011·G9-013). 예: EX8_074의 서스펜드-감액(플레이 의도)이 EX8_074를 진화시킬 때
발화 → WhenDigivolving choice와 "another choice pending" 충돌. BeforePayCost 타이밍은 play/digivolve/option
공용이라, **한 액션 의도의 효과가 다른 액션에서 발화하면 안 됨**. AS-IS는 ChangeCostClass의 rootCondition(Hand vs
digivolve)으로 게이트하지만 헤드리스 효과엔 액션 컨텍스트가 없음.

→ #1은 단순 재해소 seam이 아니라 **액션-컨텍스트 게이팅**이 선행되어야 함. 방안: (a) 방출된 BeforePayCost
이벤트 metadata(`isEvolution`)를 `ResolveAsync`가 전달하고 효과가 게이트, 또는 (b) 타이밍 분리
(BeforePayCostPlay/Digivolve), 또는 (c) `BeforePayCostReductionEffect`에 적용 액션(Play|Digivolve|Option) 인자.
비용지불 경로 + 게이팅 설계라 별도 집중 패스. **2액션 seam은 게이팅 없이는 회귀 → 미착수(revert됨).**
#2(play 감액)는 완료·유효.

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
