# PRIM-W1 — 진화 기반 프리미티브 (선행개발 웨이브 1)

> **위치:** `primitive_backlog.md`의 W1. **진화/링크 조건·코스트** 프리미티브 = 거의 모든 디지몬이 호출하는 기반. 단일 임팩트 최대(`AddSelfDigivolutionRequirement` 1282회). 이걸 먼저 깔면 로컬 모델이 디지몬 포팅 시 진화 조건을 확정적으로 채운다.
>
> **공통 종료 기준:** `bash scripts/run-tests.sh` 전체 green + 격리/픽스처 테스트로 동작 단언 + `tools/RuleAudit` 0. **AS-IS 미러**(원본 이름·시그니처 1:1, 가드 완화·추측=FAIL). **probe-first**(엔진 seam 재사용; `DigivolveAction`/`CardRecord.EvolutionCondition`/`ContinuousModifierGate`가 이미 진화·코스트를 다루는지 먼저 확인). `Headless/**` change-control. 신설: 카드-facing은 `CardEffectFactory`(원본 이름), 배관은 `Headless/`. 픽스처 `TestFixtures/Tfx*.cs`.
> **순서:** 빈도·의존순. 각 항목 독립 green 게이트.

## 선결 probe (구현 전 1회)
헤드리스가 **진화(Digivolve) 검증을 어떻게 하는지** 먼저 파악: `DigivolveAction.Validate`가 색/레벨/코스트 조건을 어디서 읽는지(`CardRecord.EvolutionConditions`? 정적 게이트?), 그리고 진화-코스트가 `ContinuousModifierGate`류로 조정 가능한지. 이 결과에 따라 아래 프리미티브가 "정적 조건 추가" 게이트에 위임된다.

## 서브goal (AS-IS 위치 = 원본, 구현 시 1:1 확인)
| # | 프리미티브 (사용) | 원본 위치 | 의도 |
|---|---|---|---|
| W1-1 | **AddSelfDigivolutionRequirementStaticEffect** (1282) | `Script/CardEffectFactory/AddDigivolutionRequirement.cs` | 이 카드에 진화 요건(색·레벨·트레잇 등) 추가(정적) |
| W1-2 | **AddSelfLinkConditionStaticEffect** (70) | `Script/CardEffectFactory/AddLinkRequirement.cs` | 링크 조건 추가(정적) |
| W1-3 | **ChangeDigivolutionCostStaticEffect** (49) | `Script/CardEffectFactory/ChangeDigivolutionCost.cs` | 진화 코스트 ± (정적) |
| W1-4 | **CanNotDigivolveClass** (14) | `Script/CardEffects/CanNotEvolveClass.cs` | 진화 불가(대상 지정) |
| W1-5 | **CanNotDigivolveStaticSelfEffect** (13) | `Script/CardEffectFactory/CanNotDigivolve.cs` | 자기 진화 불가(정적) |
| W1-6 | **AddDigivolutionRequirementClass** (3) | `Script/CardEffects/AddEvolutionConditionClass.cs` | 진화 요건 추가(대상 지정 Class) |
| W1-7 | **GetJogressConditionClass** (3) | `CardEffectFactory` (probe) | 조그레스 조건 취득 |
| W1-8 | **CanNotDigivolveStaticEffect** (2) | `Script/CardEffectFactory/CanNotDigivolve.cs` | 진화 불가(정적, 대상) |
| W1-9 | **AddDigivolutionRequirementStaticEffect** (1) | `Script/CardEffectFactory/AddDigivolutionRequirement.cs` | 진화 요건 추가(정적, 대상) |

> W1-1/6/9는 같은 계열(진화 요건 추가) — 공통 헬퍼로 묶고 self/대상·정적/Class 변형만 분기. W1-5/8/4는 진화-불가 계열.

## 진행 요약
- [x] **W1-1 AddSelfDigivolutionRequirement — SUBSUMED** ✅ 신설 불필요. 헤드리스는 진화 요건을 데이터-구동(JSON `evolutionConditions` → `CardRecord.EvolutionCondition` "Color@Level" → `DigivolveAction.MatchesEvolutionCondition`)으로 강제. 포팅 시 요건은 카드 JSON에 존재. 잠금: G9-020 3/3(매칭 진화 legal / 색·레벨 불일치 illegal). 252 green, RuleAudit 0.
- [x] **W1-3 ChangeDigivolutionCost** — ✅ `CardEffectFactory.ChangeDigivolutionCostStaticEffect`(int/Func) = `ContinuousSelfModifierEffect(DigivolutionCostDeltaKey)`(ChangeSelfDP 동형). `ResolveDigivolutionCost`(D-8) 소비. G9-021 4/4. 253 green. (setFixedCost·per-target는 후속.)
- [ ] **W1-2 AddSelfLinkCondition** — probe: Link는 `LinkHelpers`로 부분 모델링(attach/LinkedDP/Max/timings)되나 **링크 요건**은 Link 서브시스템 의존. → **W2 `LinkEffect`와 함께 처리** 권장(단독 진행 시 요건 데이터/게이트 경로 추가 probe 필요).
- [x] **W1-5 CanNotDigivolveStaticSelfEffect** — ✅ `CardEffectFactory.CanNotDigivolveStaticSelfEffect` = 재사용 신설 **`ContinuousSelfRestrictionEffect`**(role Continuous로 `ContinuousRestrictionGate`가 읽음, 조건/inherited honor). DigivolveAction:277 게이트 이미 배선. G9-022 3/3(제약 target 진화 불가). 254 green. **`ContinuousSelfRestrictionEffect`는 W3/W4 제약류(CanNotAttackSelf·CanNotBlockSelf·CantUnsuspend 등) 재사용 기반.**
- [x] **W1-4/8 CanNotDigivolve(대상 지정, 16) — 구조화 코어 BUILT** ✅ `CardEffectFactory.CanNotDigivolveStaticEffect(scopePlayerId, scopeCardType,…)` = 신설 재사용 `ContinuousPlayerScopeRestrictionEffect`(PlayerScopeKey+CannotDigivolveKey, ScopeCardType 지원). "상대 디지몬 진화불가" 구조화 케이스 커버. G9-023 3/3(P2 scoped 불가 / P1 자기 통제 legal / 조건 lift). 255 green. **임의 per-permanent 술어는 per-card**(구조화 스코프 밖). `ContinuousPlayerScopeRestrictionEffect`도 W3/W4 제약류 플레이어-스코프 재사용 기반.
- [x] **W1-6/9 AddDigivolutionRequirement(4) — BUILT** ✅ `CardEffectFactory.AddDigivolutionRequirementStaticEffect(fromColor, fromLevel,…)` = 신설 `AddedDigivolutionRequirementEffect`(대안 "Color@Level" 진화경로). **DigivolveAction 확장**: `MatchesAddedDigivolutionRequirement`(등록된 added-condition 조회+조건 평가)를 조건 게이트에 배선 → 프린트 실패해도 added 매칭 시 legal. G9-024 3/3(프린트 Green@4 + added Red@4 → Red 타겟 legal / 조건 lift). 256 green. (per-path 코스트는 W1-3 합성/per-card; 임의 술어는 per-card.)
- [x] **W1-7 GetJogressConditionClass(3) — SUBSUMED** ✅ 헤드리스 Jogress/DNA는 **데이터/레시피-구동**: `SpecialPlayAction`이 선택된 재료를 받고 재료 자격은 "per-card 조건 데이터"(코드 주석 명시). Jogress 조건 선언 = 카드 레시피 데이터 → 카드-facing 프리미티브 불필요(W1-1과 동형). (predicate-기반 재료 조건은 레시피 로더 확장 사안, 별개.)
- ✅ **W1 전 항목 해소**(built/subsumed): 1-1 subsumed · 1-3/1-5/1-4·8/1-6·9 built · 1-7 subsumed. W1-2 Link → W2. → PRIM-W2.

### 선행개발 전략 정제 (W1에서 도출)
프리미티브 처리 유형 4가지 (W2~W4에 동일 적용):
1. **데이터-구동 SUBSUME** — 헤드리스가 이미 데이터로 같은 규칙 강제 → 신설 불필요 (W1-1 진화요건, W1-7 jogress조건). 잠금 테스트만.
2. **기존 seam 1줄 미러** — 엔진 메커니즘 존재, 팩토리만 (W1-3 진화코스트).
3. **재사용 인프라 신설** — 파라미터화 continuous 효과 (W1-5/4·8: `ContinuousSelfRestrictionEffect`·`ContinuousPlayerScopeRestrictionEffect` → W3/W4 제약류 재사용).
4. **바운드 서브시스템 확장** — 액션에 조회 게이트 추가 (W1-6/9: DigivolveAction added-requirement). per-card 임의술어/코스트는 조합/per-card로 분리하면 프리미티브 코어는 선행빌드 가능.
→ "per-card 복합"은 **프리미티브 코어(구조화)**와 **per-card 잔여(임의술어/특수코스트)**로 분리; 코어는 선행빌드, 잔여는 카드 몫.

---

## 실행 대화문 (복붙용)
```
PRIM-W1 진행. docs/audit/primitive_w1_goals.md 스펙대로 순차 실행:
먼저 선결 probe(헤드리스 DigivolveAction의 진화 검증 경로 = CardRecord.EvolutionConditions/정적 게이트/코스트 조정 방식) 1회 파악.
그다음 W1-1 AddSelfDigivolutionRequirement(1282) → W1-2 AddSelfLinkCondition → W1-3 ChangeDigivolutionCost → W1-4/5/8 CanNotDigivolve 계열 → W1-6/9 AddDigivolutionRequirement 계열 → W1-7 GetJogressCondition.

각 항목: 구현 전 원본(위 표의 위치)에서 시그니처·가드·타이밍 1:1 확인(추측 금지) → probe(엔진 seam 재사용, 없을 때만 최소 신설) → 원본 이름·시그니처 미러(CardEffectFactory + 필요 시 Headless 배관) → bash scripts/run-tests.sh 전체 green + 격리/픽스처 테스트(TestFixtures/Tfx*) + tools/RuleAudit 0.
이전 항목 green 후 다음. 면역/중앙화된 게이트는 술어 재구현 금지(EX8_074 교훈). 없는 메커니즘은 실 카드 수요 범위만, 나머지 NotSupported 명시. AS-IS 불명확하면 중단·확인. 커밋은 내가 지시할 때.
```
