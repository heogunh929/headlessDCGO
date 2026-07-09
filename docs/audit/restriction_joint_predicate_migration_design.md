# 코어 restriction 인프라 — canonical joint predicate 마이그레이션 설계

- 작성: 2026-07-09. 상태: **리뷰 대기** (승인 후 착수).
- 목적: 코어 restriction 인프라를 AS-IS와 **구조 동일**(전 필드 효과 순회 + joint 술어 런타임 평가)하게 전환. 현재의 per-card scope + causing 분리(split)는 **비-분리형 술어 표현 불가** → 미구현 카드 다수인 이상 잠재적 뭉갬. ([[fidelity-over-coverage]] · [[true-scan-for-joint-predicates]])

---

## 1. 현재 구조 (AS-IS와의 괴리)

### AS-IS
```
Permanent.CanX(causingEffect):
  foreach player in Players:
    foreach permanent in player.GetFieldPermanents():
      foreach effect in permanent.EffectList(None):
        if effect is ICanXEffect && effect.CanUse(null):
          if effect.CanX(candidate, causingEffect):   // ← 단일 JOINT 술어, 인라인 평가
            return false
  return true
```
프리미티브 인터페이스도 joint: `SetUpCanNotBeDestroyedBySkillClass(f)` 에서 `f = bool CanBeDestroyedBySkill(CardSource candidate, ICardEffect causing)`.

### 헤드리스 (현재)
- **Producer**: `ContinuousSelfRestrictionEffect` / `ContinuousPlayerScopeRestrictionEffect` — 바인딩에 `[RestrictionKey]=true`(bool) + `ScopePredicateKey`(candidate 술어) + `CausingEffectPredicateKey`(causing 술어) **3분리 저장**.
- **Consumer(sink)**: `IsRestrictedFromCause(cardId, key, causing)` — `ScopedEffects(cardId)`(candidate 스코프 필터) → bool 키 확인 → causing 술어 별도 평가.
- **Consumer(gate)**: `ContinuousRestrictionGate.Evaluate(entityId)` → `ContinuousScopeEvaluation.EvaluateForCard(entityId)` → **카드별 `CannotRestriction` 리스트**(causing-무관) → `EvaluateAttack/Block/…` 가 리스트 필터.

### 괴리
joint `f(candidate, causing)` 를 `scope(candidate) ∧ causing(causing)` 로 분리. **분리형**(f=g(c)∧h(s))만 등가; **비-분리형**(f가 c·s 결합, 예 "causing DP > candidate DP")은 표현 불가.

**규모**: 키 19 · gate Evaluate* 9 · sink IsRestrictedFromCause 호출 다수 · producer(`Continuous*RestrictionEffect`) ~40 · 카드 호출부 다수. + 동족 immunity 키(ImmuneFromDpMinus·ImmuneFromEffects·ImmuneFromCostReduction·CannotBeDeDigivolved).

---

## 2. 목표 구조 (canonical joint)

### 2.1 통합 저장 form
각 restriction 바인딩이 **키별 단일 joint 술어**를 보유:
```
values["restrict:{key}"] = Func<CardSource candidate, CardSource? causing, bool>   // AS-IS CanX 그대로
values[ConditionKey]     = Func<bool>?   // AS-IS CanUse(null) 게이트 (유지)
```
- bool 키 / ScopePredicateKey / CausingEffectPredicateKey **제거**.
- causing 없는 state-check 제약(CannotAttack 등)은 `causing = null` 로 평가; 술어는 candidate만 참조.

### 2.2 통합 소비 helper (true scan)
```csharp
// RestrictionScan.IsRestricted — AS-IS Permanent.CanX 순회 1:1
public static bool IsRestricted(EngineContext ctx, string key, CardSource candidate, CardSource? causing)
{
    foreach (EffectRequest e in ctx.EffectRegistry.GetContinuousEffects(new EffectQueryContext(ContinuousRestrictionGate.Scope)))
    {
        var v = e.Context.Values;
        if (!v.TryGetValue($"restrict:{key}", out var raw) || raw is not Func<CardSource, CardSource?, bool> f) continue;
        if (v.TryGetValue(ConditionKey, out var c) && c is Func<bool> cond && !cond()) continue;   // CanUse(null)
        if (f(candidate, causing)) return true;
    }
    return false;
}
```
+ CardSource **lazy 생성 + owner.IsEmpty 가드**(마커 없는 카드서 controller-empty 예외 방지 — CanNotSelectBySkill 재작업서 실증).

### 2.3 소비자 인터페이스 전환
"카드별 restriction 리스트"(causing-무관) 추상화는 joint와 근본 불일치 → **쿼리형으로 전환**:
- `ContinuousRestrictionGate.EvaluateAttack(attacker, defender)` → `IsRestricted(CannotAttack, attacker, defender)`
- `EvaluateBlock(blocker, attacker)` → `IsRestricted(CannotBlock, blocker, attacker)`
- `EvaluateBeAttacked(defender, attacker)` → `IsRestricted(CannotBeAttacked, defender, attacker)`
- `EvaluateBeBlocked(attacker, blocker)` → `IsRestricted(CannotBeBlocked, attacker, blocker)`
- `EvaluateDigivolve(target)` → `IsRestricted(CannotDigivolve, target, causing?)`
- `EvaluateSuspend/Unsuspend/DeleteBySkill(target)` → `IsRestricted(key, target, causing?)`
- sink `IsRestrictedFromCause(target, key, source)` → `IsRestricted(key, targetSource, causingSource)`
- 이미 joint 마커인 4종(Select/Ignore/Remove/Move)은 이 통합 helper로 흡수.

### 2.4 Producer form
```csharp
// AS-IS SetUpCanXClass(f) 1:1
public static ICardEffect CanNotXStaticEffect(Func<CardSource, CardSource?, bool> predicate, CardSource card, Func<bool>? condition = null)
    => new JointRestrictionEffect(card, RestrictionKeys.X, predicate, condition);
```
- 기존 40 producer 호출부: `permanentCondition`(candidate) [+ `causingEffectPredicate`(causing)] → joint `(cand, cause) => permanentCondition(cand) && (causing==null || causingEffectPredicate(cause))` 로 **기계적 합성** (분리형은 자동 등가; 비-분리형은 카드가 직접 joint 작성).

---

## 3. 마이그레이션 계획 (단계별, 각 단계 테스트 green)

- **Phase 1 (가산)**: `JointRestrictionEffect` + `RestrictionScan.IsRestricted` 신설. 기존 경로 무영향. 키 1개(예: CannotBeDeletedBySkill, sink 소비) end-to-end 전환 + 테스트로 패턴 실증.
- **Phase 2 (키별 이관)**: 19 키를 하나씩 — (a) 소비자를 `IsRestricted`로 전환, (b) producer를 joint form으로, (c) 카드 호출부 갱신, (d) 회귀. 순서: sink 계열(Delete/Return/Suspend) → gate 계열(Attack/Block/Digivolve) → 나머지.
- **Phase 3 (구 경로 제거)**: 모든 키 이관 후 `ScopePredicateKey`/`CausingEffectPredicateKey`/bool-키 추출 + `EvaluateForCard`의 restriction 파트 + 구 `Continuous*RestrictionEffect` split 경로 제거. 미사용 상수 정리.
- **동족 immunity**: ImmuneFromDpMinus/Effects/CostReduction·CannotBeDeDigivolved 도 동일 joint form으로 이관(별도 하위 단계).

---

## 4. 리스크 / 검증

- **코어·고소비 서브시스템** — 회귀 위험 큼. 완화: 키별 이관 + 각 단계 전체 회귀(370+ 테스트) + RuleAudit.
- **성능**: 전역 스캔이 per-card 스코프보다 스캔량 큼. 단 restriction 효과 수는 보통 소수 → AS-IS도 동일 순회. 병목이면 키별 pre-filter 캐시 검토(설계 외).
- **CardSource 생성 예외**: lazy + owner 가드 필수(실증됨).
- **성공 기준**: 회귀 370+/370+ green, RuleAudit 0, 4종 joint 마커와 구조 통일, split 경로 소멸.

---

## 5. 리뷰 포인트 (확인 요청)

1. **키별 이관 순서** — sink 계열 먼저(승인?).
2. **동족 immunity(DpMinus/Effects/CostReduction/DeDigivolve)** 도 이번 마이그레이션에 **포함**할지, 별도 후속으로 뺄지.
3. **분리형 기존 카드 호출부** — 기계적 합성(자동 joint)으로 이관하되, 비-분리형 여지가 있는 키는 카드별 joint 재작성. 이 자동-합성 방침 OK?
4. **성능 캐시**는 설계 범위 밖(병목 확인 시 별도)로 두는 것 OK?
