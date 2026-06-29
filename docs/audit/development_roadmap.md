# 개발 로드맵 (앞으로의 계획)

- 작성일: 2026-06-29
- 기준: 엔진 코어 완성(테스트 199/199 green). 상세 항목은 [engine_completion_backlog.md](engine_completion_backlog.md), 작업기준은 [engine_completion_handoff.md](engine_completion_handoff.md) §2.

## 현재 위치
- **A 기반 + B 공통연산 + C 키워드(소비측) + D 서브시스템(코어) + 엔진 잔여(B-10·D-7정밀화·F-5.3·F-6.3 OnKnockOut·F-1.7·F-8.5) 전부 완료.**
- **남은 본질 = per-card 포팅** (원본 키워드/효과 트리거를 엔진 헬퍼·뮤테이션에 바인딩).
- 선결 미비: (1) **카드 데이터 로더 없음**(`CardBaseEntity` JSON은 스텁, `CardDatabase`는 수동 Upsert만), (2) **카드효과 3918개 스켈레톤**.

---

## Phase 1 — 수직 슬라이스 (강모델, 소수 카드 수동 포팅) 🔴 다음
**목표**: 1:1 포팅 레시피 + 헬퍼 바인딩 패턴을 실제 카드로 확정.
- 대상(효과 풍부 후보에서, 기능 다양성 우선): BT10_012(DigiXros=D-5), BT20_021(Blast=D-6), BT12_084(DigiXros+ArmorPurge), BT17_095(effect-play=B-8), EX3_013(De-Digivolve=D-4, 난이도 점검).
- 산출물: ① 원본 `CardEffect/<set>/<color>/<id>.cs` → 미러 1:1 포팅(스켈레톤 채움) ② grant/trigger를 엔진 헬퍼에 바인딩 ③ 카드별 e2e 테스트 ④ **"카드 포팅 레시피" 문서**(반복 패턴·체크리스트).
- 종료조건: 5장 green + 레시피 확정(로컬 LLM이 따라 할 수 있는 수준).

## Phase 2 — 카드 데이터 로더 🔴 대량 포팅 선결
**목표**: 카드 스탯/조건을 실제 로딩(이름·color·level·DP·playCost·evolutionCost·trait·evolutionCondition·효과텍스트).
- 원본 `DCGO/Assets/CardBaseEntity/.../*.asset`(CardName_ENG/EffectDiscription_ENG/스탯) → 헤드리스 실 JSON 변환 + `CardDatabase` 파일/JSON 로더 구현(현재 스텁 대체).
- 종료조건: 전 카드 `CardRecord`가 실데이터로 로드, 기존 199 테스트 무회귀.

## Phase 3 — 대량 per-card 포팅 (로컬 LLM) 🟠 본 작업
**목표**: 3918개 효과를 레시피대로 바인딩.
- 진행: 세트/키워드 그룹 단위 배치(예: DigiXros군 → Blast군 → 삭제대체군 …). 각 카드 (포팅+테스트) 1세트, `bash scripts/run-tests.sh` 회귀 게이트.
- 강모델 역할: 레시피 유지보수·난해 카드(상위 700줄+) 처리·신규 엔진 갭 발생 시 보강. 로컬 LLM 역할: 정형 per-card 바인딩.
- 종료조건: 목표 세트 커버리지 달성(우선순위 세트부터).

## Phase 4 — emit-only 타이밍/엣지 보강 🟡 (Phase 3와 병행)
**목표**: 카드군이 실제로 요구할 때 윈도우 배선(지금 일괄 emit은 클러터라 보류 중).
- OnStartBattle(DP비교 전 동기 해결 윈도우), OnGetDamage, OnAttackTargetChanged, OnEndBlockDesignation, OnDetermineDoSecurityCheck, OnAllyAttack, OnDeclaration, OnUseDigiburst.
- 종료조건: 해당 타이밍을 쓰는 카드군 포팅 시 배선+테스트.

## Phase 5 — 통합 / RL 🟢 마무리
**목표**: 풀매치 자가대전 + RL 학습 환경 검증.
- 풀게임 루프 안정성, 관전/액션 인코딩, 결정론(시드) 재현, 셀프플레이 성능.
- 종료조건: 포팅된 카드풀로 RL 학습 가능.

---

## 교차 원칙 (전 Phase 공통)
- **AS-IS 1:1**(handoff §2-1): 카드-facing은 원본 파일/심볼 1:1 미러, 엔진 배관은 `Headless/`. 룰 불변.
- **증분 리듬**: 구현 → 테스트 → `bash scripts/run-tests.sh` 전체 green → 백로그 갱신.
- **커밋**: 사용자가 직접. `DCGO/`는 커밋 금지(로컬 참조).
- **재사용 자산**(handoff §2-6): SelectPermanent/SelectCardEffect·MatchStateMutationSink kind·Continuous*Gate·TriggerTimings/Map·OnceFlagController·Link/DeDigivolve/Fusion/FreeDigivolve helpers·SpecialConditionHelpers.

## 권장 즉시 다음 액션
**Phase 1 수직 슬라이스 1장(BT10_012 Shoutmon X4B, DigiXros)** 실포팅 → 레시피 초안 확정.
