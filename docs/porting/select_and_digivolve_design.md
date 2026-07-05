# Select-and-Digivolve — 설계 (PRIM-P0-flow Build Order 3 배치 C)

- 작성일: 2026-07-05. 근거: AS-IS `DigivolveIntoHandOrTrashCard` + 헤드리스 digivolve 인프라 조사.
- 대상: 손/트래시 카드를 골라 자기 디지몬 위에 진화(대개 코스트프리/감액). ~309장(339 call-site). Build Order 3
  유일한 대형 신규 동사.

## 0. 결론

**단일 AS-IS API `DigivolveIntoHandOrTrashCard`(CardEffectCommons.cs:756)가 ~309장 전부.** 헤드리스는 저수준
조각 전부 존재(`AttachTargetAsSource`·`TryGetEvolutionCost`·`MemoryController.Pay`·`EvaluateDigivolve` 게이트·
`WhenDigivolving` 타이밍)하고 `ArtsDigivolveSelfEffect`(CardPortingFramework.cs:982)가 ~90% 조립됨. **갭은 얇은
선택+비용 프런트엔드** — 신규 digivolve-flow 코드 불필요. 프리미티브 = ArtsDigivolve의 일반화.

## 1. AS-IS 형태

`DigivolveIntoHandOrTrashCard(targetPermanent, cardCondition, payCost, reduceCostTuple, fixedCostTuple,
ignoreReqFixedCost, isHand, …, ignoreRequirements, isOptional)`:
- 타깃 디지몬은 caller가 사전 선택(207/309가 SelectPermanentEffect). 소스 카드는 Hand(~82%)/Trash(~18%)에서 선택.
- 비용: free ~39% / paid·reduced·fixed ~61%. **비용 필수 — free-only는 39%만 커버.**
- 요구조건: 대부분 준수(None); 명시 bypass ~25장(IgnoreRequirement.All/Level).
- 전체 진화는 `PlayCardClass(payCost, target, root).PlayCard()`로 — 정상 digivolve 기계(비용·요구·색·레벨·
  WhenDigivolving·ETB).

## 2. 헤드리스 재사용 (신규 flow 코드 0)

| 부분 | 재사용 |
|---|---|
| 타깃 디지몬 선택 | `ChoiceProvider.ChooseAsync`(Arts 패턴, :1011) |
| 후보 legality/비용 | `DigivolveAction.TryGetEvolutionCost(context, source, target, out cost, out err)`(:473) + `ContinuousRestrictionGate.EvaluateDigivolve`(:999) |
| **소스카드 선택(Hand/Trash)** | 존 카드 두 번째 ChooseAsync — **유일한 신규 글루** |
| attach + 스택 | `DigivolveAction.AttachTargetAsSource`(:789) |
| 비용 지불 | `MemoryController.Pay(cost)`(DigivolveAction.cs:178) |
| 타이밍 | `TriggerEventEmitter.Emit(WhenDigivolving)`(:1032) |
| 등록 | `CardEffectRegistrar.RegisterCard`(:1033) |

## 3. 설계

### `SelectAndDigivolveEffect : IActivatedCardEffect`
```
CardEffectFactory.SelectAndDigivolveEffect(
  card, ChoiceZone sourceZone {Hand|Trash}, Func<HeadlessEntityId,bool> sourcePredicate,
  Func<HeadlessEntityId,bool> targetPredicate, DigivolveCost cost, int costAmount, string description)
DigivolveCost = { Free, Normal, Fixed, Reduced }
```
`ResolveAsync`:
1. 타깃 후보 = 자기 BattleArea ∩ targetPredicate ∩ !EvaluateDigivolve. 1개 선택(없으면 return).
2. 소스 후보 = owner의 sourceZone 카드 ∩ sourcePredicate ∩ `TryGetEvolutionCost(source,target)` 성공
   (=legality+요구조건 게이트). 1개 선택(없으면 return).
3. 비용 = Free→0 / Normal→TryGetEvolutionCost 결과 / Reduced→max(0,normal-costAmount) / Fixed→costAmount.
   (Reduced가 normal에서 감액 — ContinuousModifierGate가 이미 TryGetEvolutionCost 내부(:520)라 스택 modifier 보존.)
4. cost>0면 `CanPay` 확인 후 `Pay(cost)`.
5. 타깃 off(BattleArea→None) → 소스 onto BattleArea(sourceZone→) → `AttachTargetAsSource` → `WhenDigivolving`
   emit → `RegisterCard`. (Arts와 동일 순서.)

### 플러그인
- `ActivatedEffectResolver`에 `case SelectAndDigivolveEffect`(ArtsDigivolve case :180 옆) — `eff.ResolveAsync(ct)`.
- OptionSkill/Security 진입 무변경.

## 4. 경계 (fidelity debt 명시)
- **v1은 요구조건 준수**(TryGetEvolutionCost 게이트). AS-IS `ignoreRequirements`(All/Level, ~25장)는 **후속** —
  게이트 우회 + fixed-cost 경로 필요. 지금은 skipRequirements 미지원(fidelity_debt 기록).
- **DNA/Jogress/DigiXros**(다중 재료)·**App Fusion**은 별개 경로(`FusionDigivolveHelpers`/`DigivolveAction`
  AppFusionLinkCardId) — 이 프리미티브 밖.
- **per-card 추가 요구조건**(AddDigivolutionRequirement)은 직교(DigivolveAction:562 기존 기계가 조립).
- 개별 카드의 타깃/소스 술어·비용값은 per-card 포팅 몫(데이터).

## 5. 구현 & 검증
1. `DigivolveCost` enum + `SelectAndDigivolveEffect` 클래스 + 팩토리.
2. 리졸버 case.
3. 테스트(픽스처): 손 카드로 자기 디지몬 진화 — (a) free, (b) reduced/normal 비용 지불, (c) 소스/타깃 없으면
   no-op. 스택 top이 소스로 바뀌고 타깃이 아래로 접히는지, 메모리 차감, WhenDigivolving 발화 확인.
4. 전체 스위트 무회귀.

## 6. 관련
- [ALL_CARD_PRIMITIVE_BACKLOG.md](ALL_CARD_PRIMITIVE_BACKLOG.md) B.O.3.
- 재사용: `ArtsDigivolveSelfEffect`, `DigivolveAction.{TryGetEvolutionCost,AttachTargetAsSource}`,
  `ContinuousRestrictionGate.EvaluateDigivolve`, `MemoryController.Pay`.
