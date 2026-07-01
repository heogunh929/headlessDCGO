# Seal 수정안 설계 + AS-IS 로직 동일성 검증

> 각 seal에 대해: **[Seal] → [수정안] → [AS-IS 동일성 체크(원본 코드 대조)]**. 구현 아님(설계·검증 단계).
> 원칙: 소비 게이트가 **라이브 신호(HasKeyword/정확한 키)를 직접 읽기**(Decoy 방식). 메타-동기화 브릿지·opponent-only 하드코딩 = flattening, 금지. 원본과 로직 다르면 수정안에서 교정.

## 공통 수정 패턴 (Group A: 키워드↔소비 미정렬)
`SelfKeywordByNameEffect`로 grant된 키워드를, 소비 게이트가 죽은 메타 플래그 대신 **`HasKeyword`로도 인식**. Decoy(`HasDecoy` = 메타 OR HasKeyword)로 검증된 패턴. 각 항목은 하류 로직이 AS-IS와 같은지 별도 확인.

---

## 1. Raid (~80장) — ✅ 설계+검증 완료
- **Seal**: `RaidAttackSwitch.HasRaid`가 `hasRaid` 메타(GrantRaid 뮤테이션 전용)만 읽음. 키워드 grant는 그 뮤테이션 미발생 → 무동작.
- **수정안**: `HasRaid(context, id)` = `ReadFlag(hasRaid) || ContinuousKeywordGate.HasKeyword(context, id, Raid)`.
- **AS-IS 동일성 (Raid.cs `CanActivateRaid`/`RaidProcess` 대조)**:
  - 트리거: 이 카드 공격 시(`CanTriggerOnAttack`) = 헤드리스 RequestChoice가 공격 pending+attacker일 때. ✅
  - 대상: 적(Enemy) 배틀에리어 **최대DP** 디지몬, **미서스펜드**, 1택(`maxCount=min(1,...)`), 선택 안 함 가능(`canNoSelect`). = 헤드리스 GetSwitchCandidates(방어측 배틀에리어 max-DP·미서스펜드)+minCount0/canSkip. ✅
  - 선택자: Raid 홀더 owner(`selectPlayer: TopCard.Owner`) = 헤드리스 attackingPlayerId. ✅
  - 동작: `SwitchDefender` = 헤드리스 공격 전환. ✅
  - ⚠️ **차이(기존, 수정 무관)**: 헤드리스는 현재 방어자를 후보에서 제외; 원본 `RaidProcess`의 select 조건은 `!IsSuspended`만(방어자 포함 가능, 스위치해도 no-op). `CanActivate`는 방어자 제외. 실효 동일(방어자로 스위치=no-op). 별도 debt로 기록 가능하나 seal 수정과 독립.
- **결론**: 수정안은 seal만 제거(라이브 키워드 인식), 하류는 이미 AS-IS 등가. **1:1**.

## 2. 삭제-치환 7종 (Evade·Barrier·Save·Fortitude·Ascension·Scapegoat·Fragment)
- **Seal(공통)**: 각 게이트가 `Has*Key` 메타(프로덕션 미설정)만 읽음. 키워드 grant 인식 안 함.
- **수정안(공통)**: 게이트가 `HasKeyword`도 읽기 (Decoy `HasDecoy` 패턴). 구체 지점:
  - Evade: `TryEvade`가 `record` 외 registry/context 받아 HasEvadeKey OR HasKeyword(Evade).
  - Barrier: `TryBarrierAsync`(이미 context 보유) HasBarrierKey OR HasKeyword(Barrier).
  - Save: `TrySaveAsync` registry 추가.
  - Fortitude/Ascension: post-deletion 게이트에 registry 추가.
  - Scapegoat/Fragment: 이미 `HasReplacementKeyword` 파라미터 추가됨 → **호출부(sink·DeletionReplacementTiming)에 registry 전달**만 하면 됨.
- **AS-IS 동일성 (원본 프로세스 대조, 하류 로직)**:
  | 키워드 | AS-IS | 헤드리스 | 판정 |
  |---|---|---|---|
  | Evade | 삭제될 때 자신 서스펜드→생존(미서스펜드 전제) | TryEvade: 미서스펜드면 suspend | ✅ |
  | Barrier | 전투 삭제 시 시큐리티 top 트래시→생존 | TryBarrier: top security 트래시, 전투경로에서만 호출 | ✅ |
  | Save | [On Deletion] 테이머 밑에 놓기(사후) | TrySave: 삭제 후 테이머 밑 배치 | ✅ |
  | Fortitude | 진화원 보유 디지몬 삭제 후 무료 재플레이 | 트래시서 재등장 | ✅ |
  | Ascension | 삭제 후 시큐리티 top에 놓기 | 시큐리티 배치 | ✅ |
  | Fragment | `DigivolutionCards.Count>=N`→소스 N 트래시→생존 | CanFragment: `SourceCount>=cost`→소스 트래시 | ✅ (auto-trash deepest = select N 미구현, minor) |
  | Scapegoat | 아군 1택(permanentCondition) 삭제→생존 | 후보 아군 1택 | ✅ (permanentCondition은 candidateCondition으로, Decoy처럼 정밀화 필요) |
- **결론**: 수정안은 seal 제거(키워드 인식)만, 하류 로직은 AS-IS 등가. Scapegoat/Decoy의 per-card permanentCondition(어느 아군)은 candidateCondition 경로로 별도 정밀화(선택).

## 3. Collision (~5장)
- **Seal**: `BlockTiming`(44-45)이 `HasCollisionKey` 메타만 읽음(GrantCollision 뮤테이션 전용). 키워드 grant 미인식.
- **수정안**: BlockTiming의 `attackerHasCollision` = `ReadBool(HasCollisionKey) || HasKeyword(context, attackerId, Collision)`.
- **AS-IS 동일성**: 원본 "공격 중 상대 디지몬 전부 Blocker 획득 + 가능하면 블록". 헤드리스 하류(attackerHasCollision → 상대 디지몬 블록 후보화 + must-block, line 254/271) 일치. ✅ (하류 이미 AS-IS, seal만 제거.)

## 4. Execute (~8장) — 🔴 복잡, 삼중 미배선
- **AS-IS**: (1) 턴 종료 시 이 디지몬 공격 가능, (2) 상대 **미서스펜드** 디지몬도 공격 대상, (3) 그 공격 종료 시 자신 삭제.
- **Seal 상태**: `ExecuteSelfEffect`는 키워드만 grant. AttackPipeline에 self-delete·`canAttackUnsuspendedDigimon` 로직 흔적은 있으나 **그 flag 설정처 없음** = 키워드→소비 미연결(이중), (1) 턴종료 공격창은 아예 미모델.
- **수정안(다중)**:
  - 공격 대상 확장: `AttackPermanentAction`(대상 적격)이 `HasKeyword(Execute)` 시 상대 미서스펜드도 허용.
  - self-delete: `AttackPipeline` 종료 처리가 `HasKeyword(Execute)` 시 공격 후 자신 trash.
  - (턴종료 공격창은 별도 mechanic — STOP/debt 후보.)
- **AS-IS 동일성**: 위 2개는 원본 (2)(3)과 일치 예정. (1)은 미모델 → debt.
- **판정**: 다른 seal보다 크므로 우선순위 후순위 + (1)은 STOP.

## 5. CanNotAffected (~39장) — 🔴 SkillCondition 포함 (복잡)
- **AS-IS(`CanNotAffectedClass`)**: `CanNotAffect(target, effect) = CardCondition(target) && SkillCondition(effect)`. CardCondition=어느 카드 면역(=permanentCondition), SkillCondition=어느 효과에 면역(임의·복합, 예 `IsOpponentEffect(effect) && effect.IsDigimonEffect`).
- **Seal+flattening**: 팩토리가 죽은 `ImmuneFromEffectsKey` 등록(무동작) + **SkillCondition 아예 안 받음**. 작동 게이트 `BlocksOpponentEffect`는 opponent-only 하드코딩(SkillCondition 뭉갬).
- **수정안(M-2 방식)**:
  - 팩토리 `CanNotAffectedStaticEffect(permanentCondition, skillCondition, ...)` — skillCondition은 **원인 효과 술어**(Func<원인소스,bool>; owner·IsDigimonEffect 등 판독).
  - `ContinuousImmunityGate.Scope` + 게이트가 읽는 키로 등록(스코프-술어는 M-2 ScopePredicate 방식).
  - `BlocksOpponentEffect(target, sourceEntityId)`가 **CardCondition(target) && SkillCondition(원인소스)** 평가. 현재 하드코딩 `source.Owner != target.Owner`를 skillCondition으로 대체(포터가 IsOpponentEffect를 술어로 미러).
- **AS-IS 동일성**: `CardCondition && SkillCondition` 구조를 그대로 평가 → 1:1. **주의**: opponent-only 키로 등록하면 flattening(폐기). IsDigimonEffect 등은 원인 효과 CardType 판독 필요.
- **판정**: seal + 설계결함(SkillCondition) 동시 수정. Raid 다음 우선.

---

## 수정 우선순위 (설계 기준)
1. **Raid**(~80장, 단순=키워드 인식) → 2. **CanNotAffected**(~39장, SkillCondition 포함) → 3. **삭제-치환 7종**(공통 패턴) → 4. **Collision**(단순) → 5. **Execute**(복잡, 일부 STOP) → 6. **Partition 트리거 갭**(own-effect flag 선결).
모든 수정은 **라이브 신호 직접 읽기 + AS-IS 술어 평가**. 브릿지·opponent-only 하드코딩·flattening 금지.
