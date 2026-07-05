# BeforePayCost 액션-루트 게이팅 — 설계 (PRIM-P0 B.O.4 #1 선행)

- 작성일: 2026-07-05. 근거: #1 시도 중 발견한 회귀(G9-011·G9-013) — BeforePayCost 타이밍이 play/digivolve/option
  공용이라 한 액션 의도의 효과가 다른 액션에서 발화.

## 0. 문제

`EffectTiming.BeforePayCost`는 play·digivolve·option 3액션 모두에서 방출됨(PlayCardAction:109·DigivolveAction:177·
OptionActivateAction:53). `ActivatedEffectResolver.ResolveAsync(BeforePayCost)`는 카드의 `CardEffects(BeforePayCost,
card)`가 반환한 효과를 **무조건** 해소. 그래서 play 의도 효과(예 EX8_074 서스펜드-감액)가 그 카드를 **digivolve**할
때도 발화 → WhenDigivolving choice와 충돌. AS-IS는 `ChangeCostClass.rootCondition`(Hand vs digivolve)으로 게이트하나
헤드리스 활성효과 경로엔 액션 컨텍스트가 없음(`CardEffects`는 timing만 받음). 기존 ctx 기반 헬퍼
(`CanTriggerWhenPermanentWouldPlay/Digivolve`, CardPortingFramework:6008)는 **트리거/연속 효과 전용**(ctx 받음),
활성효과가 CardEffects에서 못 씀.

## 1. 설계 — 컨텍스트 "현재 지불 루트" + non-ctx 게이트 헬퍼

### 단계 1: PayCostRoot + EngineContext 필드
`enum PayCostRoot { None, Play, Digivolve, Option }`. `EngineContext.CurrentPayCostRoot`(기본 None) 전이 필드.

### 단계 2: 각 액션이 루트 세팅/리셋
PlayCardAction=Play, DigivolveAction=Digivolve, OptionActivateAction=Option — BeforePayCost 방출/해소 **전** 세팅,
**후** try/finally로 None 리셋(중첩·예외 안전).

### 단계 3: non-ctx 게이트 헬퍼 + 카드 게이팅
`CardEffectCommons.CurrentPayCostRoot(card)` / `IsPayCostRoot(card, PayCostRoot)`. 카드의 `CardEffects(BeforePayCost)`가
이걸로 게이트(AS-IS rootCondition 미러). 예: EX8_074는 `IsPayCostRoot(card, Play)`일 때만 서스펜드-감액 반환.
- **주의(결합):** 게이트를 넣으려면 PlayCardAction이 root=Play를 세팅해야 함(안 그러면 play에서 발화 안 됨 → G9-006
  깨짐). 즉 단계2·3은 함께 착지.

### 단계 4: Digivolve/Option 재해소 seam (이제 안전)
play 의도 효과가 root로 게이트되므로, DigivolveAction/OptionActivateAction에 PlayCardAction식
`ResolveAsync(BeforePayCost)` → 비용 재-read(`TryGetEvolutionCost`/`ResolveOptionCost`) 추가해도 회귀 없음.
DeferredChoice(인터랙티브 before-pay)는 v1 미지원 → 원가 지불(catch).

## 2. 재사용/불변
- `BeforePayCostReductionEffect`(#2)는 그대로(play+digivolve 두 델타). 카드가 어느 root에 게이트하느냐로 적용 액션 결정.
- 기존 ctx 기반 트리거 게이트 헬퍼(6008)는 트리거 효과용 유지 — 이 설계는 활성효과용 non-ctx 헬퍼 추가.

## 3. 검증 순서 (각 단계 후 전체 스위트)
1. 필드+enum+헬퍼(무동작) → 빌드.
2. 3액션 루트 세팅/리셋(아직 읽는 곳 없음, 무동작) → 스위트.
3. EX8_074 fixture를 Play 게이트 + PlayCardAction root=Play → G9-006 play 발화 유지 확인.
4. Digivolve/Option seam 추가 → G9-011·G9-013 무회귀(EX8_074 digivolve 시 게이트아웃) + play 감액(#2) 유지.
5. digivolve/option 감액 실동작 테스트(각 root 게이트 fixture).

## 4. 경계
- 인터랙티브 before-pay(SuspendCostReduction) on digivolve/option은 v1 미지원(deferred resume 미구현) — catch 후 원가.
- ShowReducedCost no-op 유지.

## 5. 관련
- [cost_modification_design.md](cost_modification_design.md) B.O.4.
- 재사용: `BeforePayCostReductionEffect`, `TryGetEvolutionCost`/`ResolveOptionCost`, PlayCardAction 재해소 seam.
