# 충실도 부채 복원 goal 리스트 (G10)

- 작성일: 2026-06-30
- 출처: [fidelity_debt.md](fidelity_debt.md) — 부채 11장(STUB 1·PARTIAL 3·FAIL 7)을 **1:1로 복원**.
- 원칙: [[asis-structure-mirror-rule]] 1:1. 각 goal = **엔진 primitive 수정 + 영향 카드 복원 + 테스트 강화**.
- ⚠️ **테스트 강화 필수**: 현재 카드 테스트는 *완화 상태로도 PASS*함(예: once-per-turn 없어도 통과). 복원 후엔 **빠졌던 가드를 실제로 단언**하도록 테스트를 고쳐야 함(2회째 무발동, 0DP 아닌 격파엔 무발동 등). 안 그러면 부채가 조용히 남음.
- 공통 종료조건: 대상 카드가 fidelity_debt.md에서 **PASS로 승격** + 전체 `bash scripts/run-tests.sh` green + 레저 갱신. 커밋은 사용자 지시 시.

| Goal | primitive | 복원 카드 | 난이도 | 의존 |
|---|---|---|---|---|
| `G10-001` | once-per-turn 게이트 | ST2_11 | 낮음 | — |
| `G10-002` | 0DP-격파 트리거 컨텍스트 | ST3_01, ST3_04 | 중 | G10-001 |
| `G10-003` | 테이머 시큐리티 플레이 | ST1_12, ST2_12, ST3_12 | 중~상 | — |
| `G10-004` | 보호 전위카드(protected source) | ST2_03, ST2_09 | 중 | — |
| `G10-005` | 동적 삭제 임계값 | ST1_15 | 중 | — |
| `G10-006` | 배틀-페어링 컨텍스트 | ST2_01 | 중~상 | — |
| `G10-007` | play-from-under(특수 플레이) | ST2_15 | 상 | — |

권장 순서: 001 → 002 → 003 → 004 → 005 → 006 → 007 (쌈→비쌈).

---

## `G10-001` — once-per-turn 게이트
**부채**: ST2_11([어택시] 언서스펜드)이 `SetHashString`+activatedOrder=1(턴 1회)을 빼서 매 공격 발동.
**엔진**: `OnceFlagHelpers`(이미 존재) 기반 once-per-turn 게이트를 트리거 효과(`TriggeredUnsuspendSelfEffect` 등)에 배선 — 카드+해시 키별 "이번 턴 사용됨" 플래그 검사·기록·턴 종료 리셋.
**카드**: ST2_11에 hashString/once 파라미터 복원.
**테스트 강화**: 같은 턴 2회 해소 시 **1회만 언서스펜드**, 턴 넘기면 재사용 가능.
**승격**: ST2_11 FAIL→PASS.

## `G10-002` — 0DP-격파 트리거 전제조건  *(의존: G10-001)*
**부채**: ST3_01(자신 DP+1000)·ST3_04(메모리+1)이 `CanTriggerOnPermanentDeleted`(상대 디지몬 격파)+`IsDPZeroDelete`(0DP 격파)+once-per-turn을 빼서 *아무 격파에 무제한* 발동.
**엔진**: `OnDestroyedAnyone` 발화 시 GameEvent에 **격파 컨텍스트**(대상 소유자·격파 원인=0DP인지)를 실어, 헤드리스 `CardEffectCommons.{IsDPZeroDelete, CanTriggerOnPermanentDeleted}` 술어로 복원. + G10-001 once 게이트 적용.
**카드**: ST3_01/04 조건에 0DP·상대-디지몬·once 복원.
**테스트 강화**: 상대 디지몬 0DP 격파 시에만 발동(자멸/비0DP/내 디지몬 격파엔 무발동), 턴 1회.
**승격**: ST3_01·ST3_04 FAIL→PASS.

## `G10-003` — 테이머 시큐리티 플레이
**부채**: ST1_12·ST2_12·ST3_12의 [Security] `PlaySelfTamerSecurityEffect`가 `DeferredCardEffect` stub(시큐리티에서 테이머를 코스트 없이 등장).
**엔진**: `PlaySelfTamerSecurityEffect` 실구현 — 시큐리티 체크에서 이 테이머를 배틀에리어로 플레이하는 플로우(시큐리티 해소 경로 `SecurityResolver`와 연계, G6-001 자동등록 재사용).
**카드**: 3장 모두 팩토리 호출은 그대로(이미 1:1) — 엔진만 채우면 자동 해결.
**테스트 강화**: [Security] 발동 시 테이머가 배틀에리어에 등장하고 효과 등록됨.
**승격**: ST1_12·ST2_12·ST3_12 PARTIAL→PASS.

## `G10-004` — 보호 전위카드(protected source)
**부채**: ST2_03·ST2_09 대상 가드가 `DigivolutionCards.Count(!CanNotTrashFromDigivolutionCards)>=1`(보호 안 된 카드만)을 "전위카드 있음"으로 단순화. (+ST2_03 `TopCard.HasLevel`)
**엔진**: 전위 소스의 **trash-보호 개념**(`CanNotTrashFromDigivolutionCards`) 도입 — 소스별 보호 플래그/조회.
**카드**: ST2_03/09 대상 조건을 "보호 안 된 전위카드 ≥1"로 복원, ST2_03에 `TopCard.HasLevel`.
**테스트 강화**: 보호된 소스만 가진 디지몬은 대상에서 제외.
**승격**: ST2_03·ST2_09 FAIL→PASS.

## `G10-005` — 동적 삭제 임계값
**부채**: ST1_15 삭제 임계값 `card.Owner.MaxDP_DeleteEffect(4000)`(다른 효과가 상승시킬 수 있음)을 고정 `<=4000`으로 박음.
**엔진**: 플레이어별 **삭제-임계값 modifier** 서브시스템(기본 4000 + 상승 효과 누적), 조회 헬퍼.
**카드**: ST1_15 임계값을 동적 조회로 복원.
**테스트 강화**: 임계값 상승 효과가 있을 때 4000 초과 디지몬도 대상.
**승격**: ST1_15 FAIL→PASS.

## `G10-006` — 배틀-페어링 컨텍스트
**부채**: ST2_01(자신 DP+1000)이 *이 카드가 싸우는 특정 적*(`PermanentOfThisCard().battle.enemyPermanent`)의 무전위 판정을 "무전위 상대 아무나"로 단순화.
**엔진**: 연속효과 read-time 평가에 **현재 전투 상대(battle pairing)** 컨텍스트 노출 — `CardSource`/`PermanentView`에 battle 상대 접근.
**카드**: ST2_01 조건을 "내가 싸우는 적이 무전위"로 복원.
**테스트 강화**: 전투 중인 특정 상대 기준으로만 +1000(비전투/타 상대엔 무).
**승격**: ST2_01 FAIL→PASS.

## `G10-007` — play-from-under (특수 플레이)
**부채**: ST2_15 [Main] 전체 stub — 내 디지몬 밑 전위카드를 코스트 없이 다른 디지몬으로 플레이.
**엔진**: **밑카드→독립 디지몬 플레이** 특수-플레이 서브시스템(선택→카드선택→PlayPermanentCards 체인), 코스트 면제, 등장 후 자동등록.
**카드**: ST2_15 [Main]을 실효과로 교체, [Security]는 Main 재사용(이미 1:1).
**테스트 강화**: 선택한 밑 전위카드가 배틀에리어에 새 디지몬으로 등장.
**승격**: ST2_15 STUB→PASS.

---

## 진행 메모
- G10 완료 시 **부채 0 → ST1/ST2/ST3 진짜 1:1 35/35**.
- 엔진 통합 갭(활성효과 실루프 자동발동 ~15장)은 **별도 트랙** — G10에 포함 안 함(카드-코드는 이미 1:1). 필요 시 별도 goal.
