# Seal 수정 구현 goal (체크리스트)

> **설계·AS-IS 검증 근거**: [fidelity_fix_design.md](fidelity_fix_design.md) (각 항목 원본 코드 대조 완료). seal 목록: [fidelity_full_audit_goals.md](fidelity_full_audit_goals.md).
> **공통 종료 기준·규율 (엄격)**:
> - 구현 전 해당 항목 **원본 프로세스 재확인**(설계 문서 + 코드), 추측 금지 [[check-asis-before-implementing]].
> - **라이브 신호(HasKeyword/정확한 키) 직접 읽기 + AS-IS 술어 평가.** 메타-동기화 브릿지·opponent-only 하드코딩·flattening **금지** [[fidelity-over-coverage]].
> - 각 항목: **동작을 단언하는 테스트**(seal이 실제로 걷혀 게임 동작이 나옴 + 조건/술어 매칭·비매칭) 신설 + `bash scripts/run-tests.sh` green + `tools/RuleAudit` 0.
> - 이전 항목 green 후 다음. 불가/미모델은 STOP + fidelity_debt 기록("동작함"이라 하지 말 것).
> - 커밋은 지시 시.

## 체크리스트 (우선순위순)

### [x] S1. Raid ✅(G3.5-C3 키워드 un-seal 테스트) (~80장)
- 수정: `RaidAttackSwitch.HasRaid` = `ReadFlag(hasRaid) || ContinuousKeywordGate.HasKeyword(context, id, Raid)`.
- AS-IS: `Raid.cs CanActivateRaid/RaidProcess` (공격 시, 적 최대DP·미서스펜드 1택, 홀더 owner 선택, 선택 안 함 가능).
- 테스트: Raid 키워드 부여 → 공격 시 switch 후보/선택 열림(메타 미설정에도). 대조: 키워드 없으면 안 열림.

### [x] S2. CanNotAffected ✅(G9-057: SkillCondition 1:1·flattening 없음) (~39장) — SkillCondition 포함
- 수정: 팩토리 `CanNotAffectedStaticEffect(permanentCondition, skillCondition, ...)` 신설 인자; `ContinuousImmunityGate.Scope`+게이트가 읽는 키로 등록; `BlocksOpponentEffect`가 `CardCondition(target) && SkillCondition(원인소스)` 평가(하드코딩 opponent-only 제거).
- AS-IS: `CanNotAffectedClass.CanNotAffect = CardCondition && SkillCondition`(SkillCondition 복합, 예 `IsOpponentEffect && IsDigimonEffect`).
- 테스트: 매칭 효과 차단 + 비매칭 효과(자기 효과/비-디지몬 효과) 통과. **flattening 금지 단언.**

### [x] S3. 삭제-치환 7종 ✅(G9-058 옵션-게이팅 키워드 인식) (Evade·Barrier·Save·Fortitude·Ascension·Scapegoat·Fragment)
- 수정: 각 게이트가 `HasKeyword`도 읽기. Scapegoat/Fragment는 호출부(sink·DeletionReplacementTiming)에 `effectRegistry` 전달만. 나머지는 게이트에 registry/context + `HasReplacementKeyword`.
- AS-IS: 각 `XProcess`(설계 문서 표 — 전부 대조됨).
- 테스트: 각 키워드 부여 → 삭제 시 해당 치환/사후효과 발동(메타 미설정에도).

### [x] S4. Collision ✅(G3.5-C910 키워드 forced-block) (~5장)
- 수정: `BlockTiming`의 `attackerHasCollision` = `ReadBool(HasCollisionKey) || HasKeyword(context, attackerId, Collision)`.
- AS-IS: "공격 중 상대 디지몬 Blocker 획득 + must-block".
- 테스트: Collision 키워드 공격자 → 상대 디지몬 블록 후보 + must-block.

### [x] S5. Execute ✅부분(파트2·3)·(1)STOP (~8장) — 부분
- 수정: `AttackPermanentAction`(대상 적격) + `AttackPipeline`(종료 self-delete)이 `HasKeyword(Execute)` 인식.
- AS-IS: (2) 상대 미서스펜드 공격 가능, (3) 공격 종료 시 자신 삭제.
- **STOP**: (1) 턴 종료 공격창은 미모델 → fidelity_debt 기록.

### [x] S6. Partition 트리거 갭 ✅(G9-059 by-own-effect)
- **선결**: 엔진에 "삭제/이탈 원인이 자기 효과인가" 구분 플래그 없음 → by-own-effect 추적 먼저.
- AS-IS: "당신의 효과·전투가 **아닌** 이탈 시" 발동. 현재 `!DeletedByBattleKey`만.
- STOP 후보(플래그 선결) 또는 원인-소유자 추적 설계 후 진행.

## 진행 요약
- [x] S1 Raid  [ ] S2 CanNotAffected  [ ] S3 삭제-치환 7  [ ] S4 Collision  [ ] S5 Execute(부분)  [ ] S6 Partition(선결)

---

## 실행 대화문 (복붙용)
```
Seal 수정 진행. docs/audit/fidelity_seal_fix_goals.md 체크리스트 우선순위대로(S1 Raid → S2 CanNotAffected → S3 삭제-치환 7 → S4 Collision → S5 Execute → S6 Partition).
각 항목: 구현 전 원본 프로세스 재확인(추측 금지) → 소비 게이트가 라이브 키워드/정확한 키를 직접 읽고 AS-IS 술어를 평가하도록 배선(메타 브릿지·opponent-only 하드코딩·flattening 금지) → bash scripts/run-tests.sh green + 동작-단언 테스트(seal 걷힘 + 조건 매칭/비매칭) + tools/RuleAudit 0. 이전 항목 green 후 다음. 미모델(Execute 턴종료창·Partition own-effect)은 STOP+fidelity_debt. 커밋은 내가 지시할 때.
```
