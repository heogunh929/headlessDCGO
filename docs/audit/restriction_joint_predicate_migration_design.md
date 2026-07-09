# 코어 restriction 인프라 — canonical joint predicate 마이그레이션 설계

- 작성: 2026-07-09. **v2 (리뷰 반영 개정)**. 상태: **리뷰 대기** (승인 후 착수).
- 목적: 코어 restriction **및 immunity** 인프라를 AS-IS와 **구조 동일**(전 필드 효과 순회 + joint 술어 런타임 평가)하게 전환. 현재의 subject-scope + counterpart/causing 분리(split)는 **비-분리형 술어 표현 불가** → 미구현 카드 다수인 이상 잠재적 뭉갬. ([[fidelity-over-coverage]] · [[true-scan-for-joint-predicates]])
- ⚠️ v2 변경: 분리 키가 scope+causing 2개가 아니라 **4~5개**(+ counterpart/defender)임을 정정, **ContinuousImmunityGate를 정식 범위에 포함**, per-restriction 특수 시맨틱 보존 명시, 규모 재산정.

---

## 1. 현재 구조 (AS-IS와의 괴리)

### AS-IS
```
Permanent.CanX(counterpart/causing):
  foreach player → foreach permanent → foreach effect(None):
    if effect is ICanXEffect && effect.CanUse(null):
      if effect.CanX(candidate, counterpart):   // ← 단일 JOINT 술어, 인라인 평가
        return false
  return true
```
프리미티브 인터페이스도 joint: 예) `CanNotAffectedClass` = `CardCondition(target) && SkillCondition(cause)`, `CanNotAttackClass(attackerCondition, defenderCondition)` = `f(attacker, defender)`.

### 헤드리스 (현재) — 분리 키 4~5개
| 키 | 의미 | 현재 저장 |
|---|---|---|
| `ScopePredicateKey` | candidate(subject) 소속 | 스코프 필터 |
| `CausingEffectPredicateKey` | 유발 효과 source | 별도 술어 |
| `DefenderPredicateKey` | CannotAttack의 defender | 별도 술어 (FR-P3) |
| `CounterpartPredicateKey` | Block/BeAttacked/BeBlocked의 상대 | 별도 술어 (W6-G) |
| (immunity) `TargetPredicateKey`+`SkillCondition` | target ∧ cause | 별도 술어 2개 |

- **Restriction 게이트**: `ContinuousRestrictionGate.Evaluate(entityId)` → 카드별 `CannotRestriction` 리스트(causing-무관) + `EvaluateAttack/Block/…` 가 `SoftenByCounterpart`로 counterpart 술어 별도 순회 평가. bool 키 + subject-scope + counterpart-술어.
- **Immunity 게이트**(병렬): `ContinuousImmunityGate.BlocksOpponentEffect` — `CardCondition(target) ∧ SkillCondition(cause)`, SkillCondition 경로는 **이미 `GetContinuousEffects(Scope)` 전역 스캔**. 소비처 ~20 effect-application sites.
- **Sink**: `IsRestrictedFromCause(cardId, key, source)` — subject-scope + causing 술어.

### 괴리 (핵심)
모든 경로가 joint `f(candidate, counterpart)` 를 `subjectScope(candidate) ∧ counterpart(counterpart)` 로 **분리(독립 AND)**. → **분리형**(f=g∧h)만 등가; **비-분리형**(f가 두 인자 결합, 예 "counterpart DP > candidate DP")은 **restriction·immunity 양쪽 다 표현 불가**. counterpart 키가 이미 있어 "2번째 인자"는 잡히나, subject와 **결합 평가**가 안 됨.

---

## 2. 목표 구조 (canonical joint)

### 2.1 통합 저장 form
각 restriction/immunity 바인딩이 **키별 단일 joint 술어**를 보유:
```
values["restrict:{kind}"] = Func<CardSource subject, CardSource? counterpart, bool>   // AS-IS CanX 그대로
values[ConditionKey]      = Func<bool>?   // AS-IS CanUse(null) 게이트 (유지)
```
- bool 키 / ScopePredicateKey / CausingEffectPredicateKey / DefenderPredicateKey / CounterpartPredicateKey / (immunity) TargetPredicateKey+SkillConditionKey **모두 제거** → 단일 joint 술어로 통합.
- counterpart 없는 state-check(없음 — 전부 counterpart 존재하거나 null)는 `counterpart = null`.

### 2.2 키별 (subject, counterpart) 2nd-arg 매핑 (★ 필수)
| kind | subject | counterpart(2nd-arg) | 소비자 |
|---|---|---|---|
| Attack | attacker | defender | EvaluateAttack |
| Block | blocker | attacker | EvaluateBlock |
| BeBlocked | attacker | blocker | EvaluateBeBlocked |
| BeAttacked | defender | attacker | EvaluateBeAttacked |
| Delete / BeDeleted / DeleteBySkill | target | 유발 효과 source | sink / EvaluateDeleteBySkill |
| ReturnToHand / ReturnToDeck / ReturnToLibrary | target | 유발 효과 source | sink |
| Suspend / Unsuspend | target | 유발 효과 source (or null) | EvaluateSuspend/Unsuspend |
| Digivolve | 디지볼브 대상 | 유발 효과 source (or null) | EvaluateDigivolve |
| BeSelectedBySkill · BeRemoved · Move · IgnoreDigivolutionCondition | (기존 joint 마커) | skill / (source) / null / (digivolvingCard) | 4종 이미 완료 |
| AddSecurity · AddMemory | player | 유발 효과 source | sink (player-scope) |
| (immunity) Effects | target | 유발 효과 source(skill) | ContinuousImmunityGate |
| (immunity) DpMinus | target | DP-감소 modifier source | ContinuousDpGate |
| (immunity) CostReduction | card | (cost kind) | ContinuousModifierGate |
| DeDigivolve | target | 유발 효과 source | sink |

### 2.3 통합 소비 helper (true scan)
```csharp
// RestrictionScan.IsRestricted — AS-IS Permanent.CanX 순회 1:1
public static bool IsRestricted(EngineContext ctx, string kind, CardSource subject, CardSource? counterpart)
{
    foreach (EffectRequest e in ctx.EffectRegistry.GetContinuousEffects(new EffectQueryContext(ContinuousRestrictionGate.Scope)))
    {
        var v = e.Context.Values;
        if (!v.TryGetValue($"restrict:{kind}", out var raw) || raw is not Func<CardSource, CardSource?, bool> f) continue;
        if (v.TryGetValue(ConditionKey, out var c) && c is Func<bool> cond && !cond()) continue;   // CanUse(null)
        if (f(subject, counterpart)) return true;
    }
    return false;
}
```
+ CardSource **lazy 생성 + owner.IsEmpty 가드** 필수(마커 없는 카드서 controller-empty 예외 — CanNotSelectBySkill 재작업서 실증).

### 2.4 소비자 인터페이스 전환 + per-restriction 특수 시맨틱 보존 (★)
"카드별 restriction 리스트"(causing-무관) 추상화는 joint와 근본 불일치 → **쿼리형**으로:
- `EvaluateAttack(attacker, defender)` → `IsRestricted(Attack, attacker, defender)`. **⚠️ 현 `EvaluateAttack`은 causing-무관 리스트 체크 + `SoftenByCounterpart`(defender-conditional: "모든 CannotAttack 효과가 defenderPredicate 있고 이 defender가 다 실패하면 허용")의 2단계 하이브리드.** joint 스캔은 이 시맨틱을 **자연 포함**(각 효과 joint(attacker, defender) 평가 → 하나라도 true면 금지, defender가 안 맞으면 그 효과는 false)하므로 SoftenByCounterpart 로직이 **불필요해지고 단순화**됨 — 단 이관 시 동치성 테스트로 검증.
- Block/BeBlocked/BeAttacked/Delete*/Return*/Suspend/Unsuspend/Digivolve/AddSecurity/AddMemory 동일하게 전환.
- sink `IsRestrictedFromCause(target, key, source)` → `IsRestricted(kind, targetSource, causingSource)`.
- 이미 joint 마커인 4종(Select/Ignore/Remove/Move)은 이 통합 helper로 흡수(중복 스캔 코드 제거).

### 2.5 Immunity 게이트 포함
`ContinuousImmunityGate`(Effects) · `ContinuousDpGate`(DpMinus) · `ContinuousModifierGate`(CostReduction) 도 **동일 joint form**으로:
- `BlocksOpponentEffect(target, causing)` → joint `f(target, causingSource)` 순회 평가(현 `CardCondition(target) ∧ SkillCondition(cause)` 분리를 joint로). SkillCondition 경로는 **이미 전역 스캔**이라 통합 자연스러움.
- DP-minus per-modifier 면역(#2에서 SourceEntityId 배선)도 joint(candidate, modifierSource)로 일관화.

### 2.6 Producer form
```csharp
// AS-IS SetUpCanXClass(f) 1:1
public static ICardEffect CanNotXStaticEffect(Func<CardSource subject, CardSource? counterpart, bool> predicate, CardSource card, Func<bool>? condition = null)
    => new JointRestrictionEffect(card, RestrictionKind.X, predicate, condition);
```
- 기존 ~40 producer + card 호출부: `permanentCondition`(subject) [+ counterpart/causing 술어] → joint `(subj, cp) => subjectCond(subj) && (cp==null || cpCond(cp))` 로 **기계적 합성**(분리형 자동 등가). 비-분리형 여지 키는 카드가 직접 joint 작성.

---

## 3. 마이그레이션 계획 (단계별, 각 단계 테스트 green)

- **Phase 1 (가산 + counterpart 실증)**: `JointRestrictionEffect` + `RestrictionScan.IsRestricted` 신설. **counterpart 있는 키(CannotAttack)로 end-to-end 전환**해 다형적 2nd-arg + SoftenByCounterpart 동치성을 **처음부터** 검증(단순 키 말고). 기존 경로 무영향.
- **Phase 2 (키별 이관)**: 각 키 — (a) 소비자를 `IsRestricted`로, (b) producer를 joint로, (c) 카드 호출부, (d) 회귀. 순서: **counterpart 계열(Attack/Block/BeAttacked/BeBlocked)** → sink 계열(Delete/Return/Suspend/Digivolve) → player-scope(AddSecurity/AddMemory).
- **Phase 2b (immunity)**: ContinuousImmunityGate(Effects) · DpMinus · CostReduction · DeDigivolve 를 joint form으로.
- **Phase 3 (구 경로 제거)**: 모든 키 이관 후 `ScopePredicateKey`/`CausingEffectPredicateKey`/`DefenderPredicateKey`/`CounterpartPredicateKey`/`TargetPredicateKey`+`SkillCondition`/bool-키 추출 + `EvaluateForCard`의 restriction 파트 + `SoftenByCounterpart` + 구 `Continuous*RestrictionEffect` split 경로 제거. 미사용 상수 정리.

---

## 4. 리스크 / 검증

- **코어·고소비 서브시스템 + 병렬 immunity** — 회귀 위험 큼. 완화: 키별 이관 + 각 단계 전체 회귀(370+ 테스트) + RuleAudit + **동치성 테스트**(특히 EvaluateAttack의 defender-conditional).
- **성능**: 전역 스캔이 per-card 스코프보다 스캔량 큼. restriction/immunity 효과 수는 보통 소수(AS-IS도 동일 순회)라 실무상 무해. 병목이면 키별 pre-filter 캐시 검토(**설계 범위 밖**).
- **CardSource 생성 예외**: lazy + owner 가드 필수(실증됨).
- **성공 기준**: 회귀 370+/370+ green, RuleAudit 0, restriction+immunity 전 경로 joint 통일, split·counterpart·SoftenByCounterpart 소멸.

### 규모 (v2 재산정)
- restriction 키 **19** · gate Evaluate* **9** + `SoftenByCounterpart` · sink `IsRestrictedFromCause` 호출 다수 · producer `Continuous*RestrictionEffect` **~40** · 카드 호출부 다수.
- **+ immunity 게이트**: `ContinuousImmunityGate` **~20 effect-application sites** · DpMinus/CostReduction/DeDigivolve 소비처.
- → v1의 "키19·producer40" 대비 **immunity 20+ 소비처·counterpart 다형성**만큼 확대. 다중 세션 소요.

---

## 5. 리뷰 포인트 (v2 해소/재확인)

1. **키별 이관 순서**: counterpart 계열 먼저(다형성 조기 검증) → sink → immunity. (v2 반영)
2. **immunity 포함**: v1은 "선택적 후속"이었으나 — 동일 분리 결함 + 일부 이미 전역스캔이라 **정식 포함**으로 변경(Phase 2b). ← **확인 요청**.
3. **분리형 기존 호출부 자동 joint 합성** 방침 OK? (비-분리형 여지 키만 카드별 joint 재작성)
4. **성능 캐시**는 설계 범위 밖(병목 시 별도)로 두는 것 OK?
5. **SoftenByCounterpart 제거**: joint 스캔이 그 시맨틱을 자연 포함하므로 제거 대상 — 동치성 테스트로 보증. OK?

> 개정 후에도 **착수 전 최종 승인** 필요.

---

## 6. 구현 진행 상태 (2026-07-09)

### joint 구조 = AS-IS 원본 대조 (충실도 근거)
"joint predicate"는 **헤드리스 라벨**일 뿐, 담는 구조는 AS-IS `Permanent.CanX`와 1:1.
- `DCGO/.../CardEffectInterfaces.cs:310` — `interface ICanNotAttackTargetDefendingPermanentEffect { bool CanNotAttackTargetDefendingPermanent(Permanent Attacker, Permanent Defender); }` → **AS-IS 제한 효과 인터페이스 자체가 (subject, counterpart) 2인자 bool 술어**.
- `DCGO/.../Permanent.cs:2255-2295` (`CanAttackTargetDigimon`) — `foreach player → foreach permanent → foreach effect: if is ICanNot…Effect && effect.CanUse(null) && effect.CanNotAttackTargetDefendingPermanent(this, Defender) && !TopCard.CanNotBeAffected(effect) → return false`. **필드 전 효과 순회 + joint 호출 + CanUse 게이트 + 면역 체크**.
- 대응: `RestrictionScan`(`GetContinuousEffects+GetRestrictionEffects` 순회 · `ConditionKey`=CanUse · `predicate(subject,cp)`=joint · 면역은 producer `liveCondition`/`scopePredicate`에 내장).
- **오히려 AS-IS에 없던 것**이 이전 헤드리스의 `subjectScope ∧ causing` split이었음 → joint 이관은 발명된 split을 제거하고 AS-IS 원형 복원.

### 완료 (회귀 371/371 green, RuleAudit 0)
- **인프라**: `JointRestrictionEffect`(정본 마커) · `RestrictionScan`(정본 소비자, **Continuous+Restriction 두 role 스캔** = AS-IS "필드 전 효과 단일 순회").
- **producer 전량 joint 방출**(additive): `ContinuousSelfRestrictionEffect`(self, causing+counterpart 내장) · `ContinuousPlayerScopeRestrictionEffect`(player-scope) · `GainRestrictionToPermanent`(grant) · `GainToPlayerScope`(player 그랜트, defender/counterpart 술어 내장) · `CanNotAttackDefenderConditionEffect` · SelectAndRestrict(activated per-target).
- **gate 소비자 전량 순수-joint**(레거시 제거): `EvaluateAttack/Block/BeAttacked/BeBlocked/Digivolve/Suspend/Unsuspend/DeleteBySkill` → `JointResult`→`RestrictionScan`. `SoftenByCounterpart` **삭제**.
- **sink 소비자 순수-joint**: `IsRestrictedFromCause`(ReturnToHand/Deck·DeleteBySkill·DeDigivolve·StackTrashing 면역) → `RestrictionScan`(_context 있을 때); registry-only 폴백은 무조건 제한만.
- **AddSecurity/AddMemory**(`IsPlayerRestricted`): subject=**플레이어**(카드 아님)라 card-subject RestrictionScan 부적합 — 단 이미 **split 아닌 단일 스캔**(bool + causing 술어 인라인 = AS-IS `Player.CanAddSecurity`)이라 그대로 충실. 이관 불요.

### Phase 2b immunity — 완료 (371/371)
- `ContinuousImmunityGate.BlocksOpponentEffect`를 AS-IS `CardSource.CanNotBeAffected`(`DCGO/.../CardSource.cs:1060`) → `ICanNotAffectedEffect.CanNotAffect(cardSource, cardEffect)`(`CardEffectInterfaces.cs:100`) 단일 joint 스캔으로 전환. `TargetPredicate(target) ∧ SkillCondition(cause)` split 제거.
- `CanNotAffectedClass`(`CardEffects/CanNotAffectedClass.cs:16`)의 `CardCondition(target) && SkillCondition(cause)` conjunction을 producer가 **하나의 `Func<CardSource,CardSource,bool>`**(`JointPredicateKey`)로 방출 → 비-분리 immunity 표현 가능.
- **AS-IS `CanUse(null)` 게이트 추가**(기존 누락 = 과다면역 원인) → 조건부 immunity 정상 게이트.
- context-less 폴백은 `ImmunityFromOpponentOnly`(owner 비교, CardSource 불요)만.
- 死키 `SkillPredicateKey`/`TargetPredicateKey` **제거 완료**(독자 전무 확인).

### 결론: 2-참가자 split 안티패턴 소멸
restriction(gate 8평가자·sink IsRestrictedFromCause)·immunity 전 소비자가 joint 스캔. AddSecurity/AddMemory(`IsPlayerRestricted`)는 subject=플레이어의 단일-스캔(AS-IS `Player.CanAddSecurity` 미러). self-restriction 읽기(HasSelfRestriction/Evaluate/ScopedResult)는 단일-참가자라 비-분리 여지 없음(split 아님) → bool 키 유지 정당.

### Phase 3 — 완료 (371/371 · RuleAudit 0)
- **restriction `DefenderPredicateKey`/`CounterpartPredicateKey` 완전 제거**: `GainToPlayerScope`에 `counterpartPredicate` 직접 파라미터 추가 → `GainCanNotAttack/BlockPlayerEffect`가 extraValues 대신 파라미터로 전달; `GainRestrictionToPermanent`·`ContinuousSelfRestrictionEffect`·`CanNotAttackDefenderConditionEffect`의 死방출 제거. const 선언도 삭제.
- **true-scan 4키 정본 통일**: `CanNotSelectBySkill`(→`CannotBeSelectedBySkillKey`)·`CanNotBeRemoved`(→`CannotBeRemovedKey`)·`CanNotMove`(→`CannotMoveKey`)·`CannotIgnoreDigivolutionCondition`(→`CannotIgnoreDigivolutionConditionKey`) 4 effect가 `view.*Predicate` 대신 `JointRestrictionEffect.PredicateKey(kind)`로 방출(예측자 `Func<CardSource,CardSource?,bool>` 래핑); 소비처 4곳(SelectPermanentEffect·sink IsRemovalBlockedByScan·dispatcher move-gate·DigivolveAction IsDigivolveIgnoreBlocked)이 각자 인라인 스캔 대신 `RestrictionScan.IsRestricted` 호출. 死 `MakeSource`도 제거.
- **immunity `Skill/TargetPredicateKey` 제거**(Phase 2b서 완료).
- 유지된 키(정당·제거 금지): bool 제한 키(self 단일-참가자 읽기)·`CausingEffectPredicateKey`(IsPlayerRestricted 플레이어 게이트 + sink registry-only 폴백)·`ScopePredicateKey`(player-scope DP/self fold)·`ImmunityFromOpponentOnlyKey`(context-less 폴백).

### 최종 상태
restriction·immunity 전 경로가 AS-IS `Permanent.CanX`/`CardSource.CanNotBeAffected`와 **구조 동일한 단일 joint 순회-스캔**(`RestrictionScan`/`ContinuousImmunityGate`). 2-참가자 split·발명된 별도 술어 키·`SoftenByCounterpart` 소멸. 비-분리 술어 표현 가능 → 미포팅 카드 대응 준비 완료.
