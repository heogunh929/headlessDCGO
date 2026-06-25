# HeadlessDCGO.Engine 상세 Goal 단위 작업 분해

## 목적

Phase는 로드맵과 게이트를 나타내는 단위다. 실제 작업 완료 판정은 Goal 단위로 한다.

이 문서는 HeadlessDCGO.Engine 작업을 테스트 가능한 Goal 단위로 더 세분화한다. 모든 Goal은 구현 산출물, 단위테스트, 단위테스트 결과 문서를 함께 가져야 완료로 인정한다.

## Goal 정의

하나의 Goal은 다음 조건을 만족해야 한다.

- 명확한 입력과 출력이 있다.
- 영향 범위가 좁다.
- 완료 여부를 단위테스트로 판정할 수 있다.
- 결과 문서가 `docs/test-results/goals/` 아래에 남는다.
- 다른 Goal의 내부 구현을 추측하지 않는다.
- 실패 시 어느 영역이 막혔는지 바로 식별할 수 있다.

## 완료 기준

각 Goal은 아래 항목이 모두 만족되어야 완료다.

- 구현 또는 설계 산출물이 존재한다.
- 대응 단위테스트가 존재한다.
- 단위테스트가 통과한다.
- Goal 결과 문서가 남아 있다.
- 결과 문서에는 테스트 명령, 통과/실패 수, 실패 상세, 미해결 리스크가 포함된다.
- 상위 Phase 결과 문서에 Goal 완료 요약이 반영된다.

## 결과 문서 경로 규칙

Goal별 결과 문서:

```text
docs/test-results/goals/<goal_id>_<short_name>_unit_test_results.md
```

Phase별 집계 결과 문서:

```text
docs/test-results/headless_phase<N>_<phase_name>_unit_test_results.md
```

## Goal 수 요약

| Phase | Goal 수 | 설명 |
|---|---:|---|
| Phase 0 | 3 | 설계와 테스트 정책 확정 |
| Phase 1 | 43 | Unity 대체 기반 구현 |
| Phase 2 | 29 | AS-IS 핵심 흐름 포팅 |
| Phase 3 | 24 | 공통 룰/효과 인프라 포팅 |
| Phase 4 | 27 | 개별 카드 효과와 카드풀 포팅 |
| Phase 5 | 17 | AI/RL adapter |
| Phase 6 | 18 | parity와 regression 검증 |

총 Goal 수: 161

## Phase 1을 선행으로 유지하는 이유

Phase 1은 후속 `Assets/...` 포팅의 기준을 닫는 단계다. 따라서 다른 Phase보다 먼저 완료되어야 하며, Phase 2~6의 Goal들은 Phase 1 완료 후 구현에 들어간다.

## 상세 Goal 목록

전체 Goal 목록과 각 Goal의 의존 관계, 테스트 범위, 결과 문서 경로는 CSV에 둔다.
실제 Goal을 맡길 때는 짧은 Goal 프롬프트 CSV를 사용하고, 상세 지시서는 작업자가 읽는 기준 문서로 둔다.

- `docs/headless_complete_goal_breakdown.csv`
- `docs/headless_complete_goal_breakdown_ko.csv`
- `docs/headless_complete_goal_breakdown_detailed_ko.csv`
- `docs/headless_goal_prompts_compact_ko.csv`
- `docs/headless_goal_prompt_usage.md`
- `docs/goal-specs/*.md`
- `docs/test-results/headless_goal_spec_quality_results.md`

각 `docs/goal-specs/*.md` 상세 지시서는 다음 내용을 직접 포함해야 한다.

- 작업 대상 파일과 생성 위치
- AS-IS 확인 대상과 Headless 대체 관계
- 관련 Headless 모듈과 public API
- 구현 또는 문서 작성 지시
- 하지 말아야 할 작업
- Given/When/Then 형태의 단위테스트 관점
- 결과 문서 필수 항목
- 완료 판정 체크리스트

## Phase별 세분화 방향

### Phase 0: 설계와 테스트 정책

- 설계 산출물 확정
- 테스트 정책 확정
- Phase 1 착수 게이트 검증

### Phase 1: Unity 대체 기반

- Runtime contract
- State/Zone kernel
- Bridge/Context
- Coroutine replacement
- Choice replacement
- Effect queue/timing foundation
- Local session/network replacement
- Data loading contract
- Determinism/diagnostics

### Phase 2: AS-IS 핵심 흐름 포팅

Phase 2는 원본 핵심 파일과 실행 흐름 기준으로 세분화한다.

- `TurnStateMachine`: setup, draw/unsuspend/breeding/main/end, memory pass, legal action dispatch
- `GameContext`: state accessor, visibility view
- `Player`: zone ownership, memory/security/deck loss check
- `CardController`/`CardObjectController`: identity binding, movement, suspend/reveal, source attach
- `MainPhaseAction`: play, digivolve, option, attack, pass/cheat guard
- `AutoProcessing`: trigger collection, mandatory ordering, optional prompt, delayed/security trigger
- `AttackProcess`: declaration, target, block, battle, security, end attack trigger

### Phase 3: 공통 룰/효과 인프라

Phase 3은 개별 카드 효과가 의존할 공통 기반을 세분화한다.

- effect contract와 `SkillInfo`
- `Hashtable` 제거와 typed context
- condition/requirement/cost/modifier helper
- target filtering과 zone query helper
- keyword base effect
- restriction, replacement, continuous effect
- effect factory와 permanent effect factory
- timing priority, once-per-turn, inherited/granted/security helper

### Phase 4: 개별 카드 효과와 카드풀

Phase 4는 mechanic과 카드풀 coverage 기준으로 세분화한다.

- card pool schema와 effect binding coverage
- keyword batch
- trigger batch
- generic modifier/restriction/immunity batch
- digivolution/source/link 관련 batch
- set/source directory별 card-specific batch
- missing behavior report와 card pool aggregate

### Phase 5: AI/RL Adapter

Phase 5는 완성된 Headless 실행기를 학습 시스템에 연결하는 adapter 기준으로 세분화한다.

- observation schema와 hidden information masking
- vector schema와 normalization
- legal action mask와 action encode/decode
- reward와 terminal step
- environment reset/step
- scripted/self-play policy
- batch/parallel determinism
- dataset export와 validation

### Phase 6: Parity And Regression

Phase 6은 검증 범주별로 세분화한다.

- scenario model과 runner
- replay와 golden trace
- AS-IS parity harness
- setup/phase/memory/movement/main action/combat/security/effect parity
- card pool coverage report
- regression suite
- performance benchmark
- final completion report
