# FR — 충실도 위반 복원 goal (permanentCondition 술어 무시 수정)

> **위치:** 상세 전수 리스트는 [fidelity_remediation.md](fidelity_remediation.md). 이 문서는 goal 스펙(단계·종료기준·실행 대화문).
> **계기:** W3~W5 팩토리 다수가 `Func<Permanent,bool> permanentCondition`(임의 술어)을 **받아놓고 평가 안 하고 self/player-scope(전체)로 뭉갬** = 가드-축소 **FAIL**. (특수플레이·AddSelfDigivolutionRequirement는 이미 술어 평가로 수정됨 = 올바른 패턴.)
> **공통 종료 기준·규율:** `bash scripts/run-tests.sh` 전체 green + **술어가 실제 평가됨을 단언하는 테스트**(이름/전체 아닌 좁힌 조건으로 매칭 + 비매칭 제외) + `tools/RuleAudit` 0. AS-IS 1:1(술어를 뭉개면 FAIL). 뷰 계층(`Permanent`/`CardSource`)으로 후보 평가. 단순화 불가 시 STOP + fidelity_debt 기록(“포팅 가능”이라 하지 말 것). 각 단계 독립 green 게이트.

## 서브goal (위험순)

### FR-P1 — enabler: player-scope 연속효과가 임의 술어 평가 (공통 선결)
- 바인딩 `Context.Values`에 `permanentPredicate: Func<CardSource,bool>`(원본 `permanentCondition` 1:1) 실림.
- `PlayerScopeContinuousHelpers.CollectApplicable` + `ContinuousScopeEvaluation.EvaluateForCard`가 후보 카드를 `new CardSource(...)`(또는 `Permanent`)로 만들어 술어 평가 → **통과분만** modifier/keyword/restriction 적용. (특수플레이 `TryMatchMaterials`와 동형.)
- 테스트: 좁힌 술어(`Level==N`/트레잇)로 매칭·비매칭 제외.

### FR-P2 — A2(player-scope 전체로 뭉갠 11) 팩토리를 predicate 경로로
- `RushStatic·RebootStatic·Alliance·Jamming·Collision·Vortex·Blocker·ChangeSAttack·ChangeBaseDPGlobal·ChangeLinkMax·ChangeDP`.
- 규칙: `permanentCondition == null` → 기존(전체), non-null → **player-scope + predicate**.
- player-scope Keyword/Modifier 이펙트에 predicate 인자 추가(P1 소비).
- `ChangeBaseDPGlobal`은 "global"(양 플레이어) 스코프도 함께 바로잡기.

### FR-P3 — A1(self로 뭉갠 10) self→SET 승격
- `CanNotBeDestroyed·ImmuneFromDPMinus·InvertSAttack·CantSuspend·CannotReturnToHand·CannotReturnToDeck·CanNotBeDestroyedByBattle·CanNotBeTrashedBySkill·CanNotAffected·CanNotAttackSelf(defenderCondition)`.
- `permanentCondition == null` → self(현행). non-null → **player-scope-with-predicate**.
- 제약(Cannot*)·replacement(PreventDeletion/ImmuneFromDpMinus/ImmuneFromEffects/PreventBattleDeletion/ImmuneStackTrashing)의 **player-scope+predicate 평가 경로** 추가: `ContinuousRestrictionGate`·`BattleDeletionGate`·삭제/트래시 sink가 player-scope 술어 바인딩도 읽게.

### FR-P4 — 그룹별 술어-평가 테스트 (G9-050+)
- A2·A1 각각 "좁힌 술어로 매칭됨 + 비매칭 카드 제외"를 단언. self 누락/전체 과다적용이 없어짐을 검증.

### FR-P5 — B(preemptive-seal) 유지·기록
- 링크 소비자·키워드-동작 metadata 소비자 마이그레이션은 별도(수요 시). 지금은 fidelity_debt에 정직히 유지(침묵 아님).

## 진행 요약
- [x] **FR-P1 enabler** — player-scope 연속효과가 임의 술어 평가. `ScopePredicateKey` + `ContinuousScopeEvaluation.PlayerScopePredicatePasses`(modifier/restriction) + `ContinuousKeywordGate.ScopePredicatePasses`(keyword). 3개 player-scope 이펙트가 `scopePredicate` 전달. **G9-050**(Lv4 술어 매칭·Lv3 제외).
- [x] **FR-P2 A2 11종** — Rush·Reboot·Alliance·Jamming·Collision·Vortex·Blocker·ChangeSAttack·ChangeBaseDPGlobal·ChangeLinkMax·ChangeDP가 `ScopePred(permanentCondition)` 전달(null=전체, non-null=술어). **G9-050**(RushStatic·ChangeDP 좁힌 조건 준수).
- [x] **FR-P3 A1** — **전부 완료**. (a) predicate-aware(ImmuneFromDPMinus·InvertSAttack) self→SET. (b) registry-only sink/battle 게이트: `ContinuousScopeEvaluation.ApplicableEffects`(카드+player-scope술어) 노출 + **MatchStateMutationSink에 EngineContext 스레딩**(EngineContext.cs 배선) + BattleDeletionGate가 ApplicableEffects 사용 → CanNotBeDestroyed·ByBattle·CanNotBeTrashedBySkill·CantSuspend·CannotReturnToHand·CannotReturnToDeck·CanNotAffected SET형 작동. (c) `CanNotAttackSelf(defenderCondition)`: 신규 `CanNotAttackDefenderConditionEffect` + `DefenderPredicateKey`, EvaluateAttack가 defender 술어 평가(매칭 방어자만 제약).
- [x] **FR-P4** — **G9-050**(7 케이스: DP/키워드/팩토리 술어 · SET-form 삭제/서스펜드 · defenderCondition). 282 green.
- [x] **FR-P5** — seal(B) fidelity_debt 유지(별건).
- ✅ **FR 완료** — permanentCondition 술어 무시 21종 + defenderCondition 1종 전부 1:1 평가. 282 green, RuleAudit 0.
- ✅ 완료 → W3~W5 술어-보유 팩토리 **전부 1:1**(술어 평가). 이미 수정: 특수플레이(G9-049)·AddSelfDigivolutionRequirement(G9-044).

---

## 실행 대화문 (복붙용)
```
FR 충실도 복원 진행. docs/audit/fidelity_remediation_goals.md 스펙대로 위험순(P1 enabler → P2 player-scope predicate → P3 self→SET → P4 테스트).
각 단계: 구현 전 원본에서 permanentCondition이 실제로 무엇을 좁히는지 1:1 확인(추측 금지) → player-scope/self 연속효과가 뷰 계층으로 임의 술어를 평가하도록 배선 → 팩토리는 permanentCondition==null이면 기존, non-null이면 predicate 경로 → bash scripts/run-tests.sh green + 술어-평가 테스트(좁힌 조건 매칭+비매칭 제외) + tools/RuleAudit 0. 술어를 이름/스칼라/스코프로 뭉개면 FAIL. 이전 단계 green 후 다음. 불가하면 STOP+fidelity_debt 기록. 커밋은 내가 지시할 때.
```
