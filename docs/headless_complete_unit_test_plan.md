# HeadlessDCGO.Engine 단위테스트 및 결과 문서 계획

## 목적

HeadlessDCGO.Engine의 모든 과정은 구현 산출물과 함께 단위테스트, 테스트 실행, 테스트 결과 문서를 남겨야 한다. 테스트가 없는 구현은 완료로 인정하지 않는다.

이 문서는 Phase별 단위테스트 범위와 결과 문서 위치를 고정한다.

## 공통 완료 규칙

- 모든 구현 Phase는 단위테스트를 포함한다.
- 단위테스트가 실패하면 해당 Phase는 완료가 아니다.
- 단위테스트 결과 문서는 `docs/test-results/` 아래에 Markdown으로 남긴다.
- 결과 문서에는 테스트 명령, 실행 환경, 대상 커밋 또는 작업 시점, 통과/실패 수, 실패 상세, 미해결 리스크를 포함한다.
- 단위테스트 결과 문서가 없으면 다음 Phase로 넘어가지 않는다.
- 설계만 수행하는 Phase도 문서 검증 결과를 남긴다.
- Phase 내부 작업은 Goal 단위로 쪼개고 각 Goal은 별도 결과 문서를 남긴다.

Goal별 결과 문서:

- `docs/test-results/goals/<goal_id>_<short_name>_unit_test_results.md`

Goal 실행 프롬프트:

- `docs/headless_goal_execution_prompt.md`

## 결과 문서 공통 형식

각 결과 문서는 아래 형식을 따른다.

```markdown
# <Phase 이름> 단위테스트 결과

## 실행 정보

- 실행 일시:
- 실행자:
- 작업 범위:
- 테스트 명령:
- .NET SDK:
- 대상 프로젝트:

## 결과 요약

- 전체 테스트 수:
- 통과:
- 실패:
- 스킵:

## 실패 상세

- 없음 또는 실패 테스트 목록

## 확인한 범위

- 테스트로 보장한 항목
- 테스트하지 못한 항목

## 미해결 리스크

- 없음 또는 남은 리스크

## 다음 조치

- 다음 Phase 진행 가능 여부
```

## Phase별 테스트 요구사항

### Phase 0: 설계 확정

테스트 성격:

- 코드 단위테스트가 아니라 문서 검증 테스트다.

필수 검증:

- 설계 문서 4개가 존재한다.
- 모듈 CSV와 의존성 CSV가 파싱된다.
- Unity 대체 기반이 Phase 1로 명시되어 있다.
- Phase 1 완료 전 `Assets/...` 포팅 금지가 명시되어 있다.
- 테스트 계획 문서와 테스트 매트릭스가 존재한다.

결과 문서:

- `docs/test-results/headless_phase0_design_validation_results.md`

### Phase 1: Unity 대체 기반 구현

테스트 성격:

- Headless 기반 API의 단위테스트다.

필수 테스트 범위:

- Runtime contract
- State/Zone kernel
- Bridge/Context
- Coroutine replacement
- Choice replacement
- Effect queue/timing foundation
- Local session/network replacement
- Data loading contract
- Determinism/diagnostics

결과 문서:

- `docs/test-results/headless_phase1_unity_replacement_unit_test_results.md`

완료 기준:

- Phase 1 전체 단위테스트가 통과한다.
- Headless core가 Unity, Photon, UI, animation, audio, scene, prefab runtime dependency 없이 테스트된다.
- 결과 문서가 남아 있다.

### Phase 2: AS-IS 핵심 흐름 포팅

테스트 성격:

- 핵심 흐름 단위테스트다.

필수 테스트 범위:

- 턴/페이즈 진행
- legal action 생성
- action 적용
- player/card state 연결
- 기본 공격 흐름
- 기본 security 흐름
- 자동 효과 queue 연결

결과 문서:

- `docs/test-results/headless_phase2_core_flow_unit_test_results.md`

### Phase 3: 공통 룰/효과 인프라 포팅

테스트 성격:

- 공통 helper와 effect factory 단위테스트다.

필수 테스트 범위:

- condition helper
- requirement helper
- cost helper
- modifier helper
- keyword base effect
- effect registry binding
- typed effect context
- replacement/continuous effect query

결과 문서:

- `docs/test-results/headless_phase3_shared_rule_effect_unit_test_results.md`

### Phase 4: 개별 카드 효과와 카드풀 포팅

테스트 성격:

- 카드 효과 단위테스트와 mechanic별 대표 테스트다.

필수 테스트 범위:

- keyword effect
- trigger condition
- card-specific effect
- optional effect choice
- security effect
- inherited effect
- granted effect
- card data binding

결과 문서:

- `docs/test-results/headless_phase4_card_pool_unit_test_results.md`

### Phase 5: AI/RL Adapter

테스트 성격:

- RL adapter 단위테스트다.

필수 테스트 범위:

- observation masking
- legal action mask
- action encode/decode
- reward calculation
- terminal step result
- batch episode runner
- deterministic parallel simulation
- dataset export schema

결과 문서:

- `docs/test-results/headless_phase5_rl_adapter_unit_test_results.md`

### Phase 6: Parity And Regression

테스트 성격:

- 단위테스트와 regression/parity test를 함께 포함한다.

필수 테스트 범위:

- scenario runner
- replay runner
- golden trace comparison
- deterministic fingerprint
- curated AS-IS scenario comparison
- representative combat/security/effect scenario

결과 문서:

- `docs/test-results/headless_phase6_parity_regression_test_results.md`

## 테스트 프로젝트 배치 원칙

권장 테스트 프로젝트:

- `tests/HeadlessDCGO.Engine.Tests`
- `tests/HeadlessDCGO.Engine.ParityTests`
- `tests/HeadlessDCGO.Engine.RlTests`

테스트 프로젝트 원칙:

- 테스트는 Unity 설치 없이 local .NET SDK로 실행 가능해야 한다.
- Headless core 단위테스트는 Unity, Photon, TMPro, DOTween, UI package를 참조하지 않는다.
- 테스트 데이터는 작고 결정적이어야 한다.
- seed가 있는 테스트는 seed를 결과 문서에 기록한다.
