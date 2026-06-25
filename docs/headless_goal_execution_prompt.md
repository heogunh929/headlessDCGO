# HeadlessDCGO.Engine Goal 실행용 목표 프롬프트

## 목적

이 문서는 특정 Goal을 작업시킬 때 사용할 목표 프롬프트를 정의한다.
실제로 Goal을 맡길 때는 긴 상세 지시서를 붙여넣지 않고, `docs/headless_goal_prompts_compact_ko.csv`의 짧은 Goal 프롬프트를 사용한다.

목표는 하나다.

> Goal 하나를 맡겼을 때 범위가 새지 않고, 구현/테스트/결과 문서까지 완료 기준에 맞게 닫히도록 한다.

## 기본 원칙

- 한 번에 하나의 Goal만 수행한다.
- Goal ID는 `docs/headless_goal_prompts_compact_ko.csv`에 존재해야 한다.
- 작업자는 짧은 Goal 프롬프트에 적힌 `docs/goal-specs/` 상세 지시서를 먼저 읽는다.
- 작업 범위는 해당 Goal의 상세 목표 설명, 해야 할 작업, 하지 말아야 할 작업, 산출물, 단위테스트 상세, 완료 기준, 완료 체크리스트를 따른다.
- 상위 Phase 전체를 임의로 진행하지 않는다.
- 선행 Goal이 필요한 경우 `blocked_until`을 확인하고, 미충족이면 구현하지 말고 차단 사유를 보고한다.
- 단위테스트가 없으면 완료가 아니다.
- 단위테스트 결과 문서가 없으면 완료가 아니다.
- 실제 asset/card effect 포팅은 Phase 1 완료 전에는 수행하지 않는다.
- 원본 `DCGO/Assets/...` 파일은 분석 대상으로만 사용하고 수정하지 않는다.

## 기본 사용 방식

Goal로 직접 사용할 텍스트는 아래 CSV의 `짧은 Goal 프롬프트` 컬럼이다.

- `docs/headless_goal_prompts_compact_ko.csv`
- `docs/headless_goal_prompt_usage.md`

상세 기준은 Goal 프롬프트 안에 적힌 상세 지시서 파일을 읽어서 적용한다.

## 공통 목표 프롬프트

아래 템플릿은 CSV를 쓰기 어려울 때만 사용한다. 기본은 compact CSV의 프롬프트다.

```text
HeadlessDCGO.Engine Goal <GOAL_ID>를 수행하라.

중요:
- 이번 작업은 <GOAL_ID> 하나만 완료하는 것이 목표다.
- `docs/headless_complete_goal_breakdown_detailed_ko.csv`에서 <GOAL_ID> 행을 읽고, 해당 행의 상세 목표 설명, 해야 할 작업, 하지 말아야 할 작업, 산출물, 단위테스트 상세, 결과 문서, 선행 Goal, 완료 기준, 완료 체크리스트를 기준으로 작업하라.
- 원본 `docs/headless_complete_goal_breakdown.csv`의 scope, deliverables, unit_test_scope, result_document, blocked_until, completion_gate는 보조 검증 기준으로 함께 확인하라.
- 선행 Goal이 필요하면 blocked_until을 확인하라. 선행 Goal이 완료되지 않았으면 구현하지 말고 차단 사유와 필요한 선행 Goal을 문서화하라.
- Goal 범위를 벗어난 Phase/Goal 작업을 하지 말라.
- 원본 `DCGO/Assets/...` 파일은 수정하지 말라.
- Phase 1 완료 전에는 asset/card effect 실제 포팅을 하지 말라.
- 완성 기준을 충족하지 않는 자리표시 구현을 완료로 간주하지 말라.
- 완료 기준은 구현 산출물 + 단위테스트 + 단위테스트 결과 문서다.

작업 절차:
1. `docs/headless_complete_goal_breakdown_detailed_ko.csv`에서 <GOAL_ID>를 읽는다.
2. 원본 Goal CSV와 상세 지시서가 같은 Goal을 가리키는지 확인한다.
3. blocked_until 선행 Goal을 확인한다.
4. 작업 대상 파일과 금지 대상 파일을 정리한다.
5. 필요한 경우 AS-IS 원본 파일을 읽어 의존 호출과 역할을 확인한다. 단, 원본은 수정하지 않는다.
6. Goal 범위 안에서만 구현 또는 문서 작업을 수행한다.
7. Goal의 unit_test_scope에 맞는 단위테스트를 작성하거나 갱신한다.
8. 단위테스트를 실행한다.
9. 결과 문서를 goal 행의 result_document 경로에 작성한다.
10. 결과 문서에는 테스트 명령, 전체/통과/실패/스킵 수, 실패 상세, 미해결 리스크, 다음 조치를 포함한다.
11. 최종 응답에는 완료 여부, 수정 파일, 테스트 결과, 결과 문서 경로, 남은 리스크를 요약한다.

완료 조건:
- <GOAL_ID>의 completion_gate가 충족되어야 한다.
- 단위테스트가 통과해야 한다.
- result_document가 생성되어야 한다.
- Goal 범위 밖 변경이 없어야 한다.
```

## 더 엄격한 실행 프롬프트

작업 범위가 흔들릴 가능성이 큰 Goal에는 아래 버전을 사용한다.

```text
HeadlessDCGO.Engine Goal <GOAL_ID>만 수행하라.

절대 조건:
- <GOAL_ID> 외의 Goal을 함께 수행하지 말라.
- 후속 Phase 작업을 앞당기지 말라.
- 원본 `DCGO/Assets/...` 파일을 수정하지 말라.
- Phase 1이 완료되기 전에는 `Assets/...` 포팅 파일의 실제 룰/카드 효과 구현을 하지 말라.
- 컴파일만 되는 빈 동작, 자리표시 구현, TODO-only 구현은 완료가 아니다.
- 테스트 없는 구현은 완료가 아니다.
- 결과 문서 없는 구현은 완료가 아니다.

필수 입력:
- `docs/headless_goal_prompts_compact_ko.csv`
- `docs/headless_goal_prompt_usage.md`
- `docs/headless_complete_goal_breakdown.csv`
- `docs/headless_complete_goal_breakdown_ko.csv`
- `docs/headless_complete_goal_breakdown_detailed_ko.csv`
- `docs/headless_goal_spec_index.csv`
- Goal ID: <GOAL_ID>
- 해당 Goal의 상세 지시서: `docs/goal-specs/<GOAL_ID>_*.md`

필수 산출:
- Goal deliverables에 해당하는 파일 변경
- Goal unit_test_scope에 해당하는 단위테스트
- Goal result_document 경로의 테스트 결과 문서

실행 순서:
1. `docs/headless_goal_prompts_compact_ko.csv`에서 Goal 행을 읽는다.
2. 짧은 Goal 프롬프트에 적힌 상세 지시서 경로를 확인하고 해당 지시서를 읽는다.
3. 선행 Goal이 미충족이면 바로 중단하고 blocked 보고서를 작성한다.
4. 작업 범위를 벗어나는 파일은 수정하지 않는다.
5. 구현 전 테스트 의도를 먼저 정리한다.
6. 구현한다.
7. 단위테스트를 실행한다.
8. 실패하면 같은 Goal 범위 안에서 수정하고 다시 테스트한다.
9. 테스트 결과 문서를 작성한다.
10. 최종 응답에서 Goal 완료 여부를 `COMPLETE` 또는 `BLOCKED`로 명확히 말한다.
```

## Goal 결과 문서 템플릿

각 Goal의 `result_document`에는 아래 형식을 사용한다.

```markdown
# <GOAL_ID> <Goal 이름> 단위테스트 결과

## 실행 정보

- Goal ID:
- Phase:
- 실행 일시:
- 작업 범위:
- 테스트 명령:
- .NET SDK:

## 산출물

- 수정/생성 파일:
- 테스트 파일:
- 참조한 원본 파일:

## 결과 요약

- 전체 테스트 수:
- 통과:
- 실패:
- 스킵:

## 테스트 상세

- 테스트한 항목:
- 테스트하지 못한 항목:

## 실패 상세

- 없음 또는 실패 목록:

## 미해결 리스크

- 없음 또는 남은 리스크:

## 완료 판정

- completion_gate 충족 여부:
- 다음 Goal 진행 가능 여부:
```

## Goal 시작 전 체크리스트

작업자는 Goal 시작 전에 아래 질문에 답해야 한다.

- 이 Goal ID가 CSV에 존재하는가?
- `blocked_until`이 비어 있거나 충족되었는가?
- 이 Goal이 Phase 1 이전/이후 제약을 위반하지 않는가?
- 수정 가능한 파일 범위가 명확한가?
- 원본 `DCGO/Assets/...` 수정이 필요하지 않은가?
- 단위테스트 대상이 명확한가?
- 결과 문서 경로가 명확한가?

## Goal 완료 전 체크리스트

완료 선언 전에 아래 항목을 확인한다.

- Goal 범위 밖 파일을 수정하지 않았다.
- 단위테스트를 작성했다.
- 단위테스트를 실행했다.
- 실패 테스트가 없다.
- 결과 문서를 작성했다.
- 결과 문서에 테스트 명령과 통과/실패 수를 기록했다.
- 미해결 리스크를 숨기지 않고 기록했다.
- 최종 응답에 결과 문서 경로를 포함했다.

## 예시: Phase 1 Goal 실행

```text
HeadlessDCGO.Engine Goal G1D-004를 수행하라.

중요:
- 이번 작업은 G1D-004 TaskRunner 안정화 하나만 완료하는 것이 목표다.
- `docs/headless_complete_goal_breakdown_detailed_ko.csv`에서 G1D-004 행을 읽고 상세 목표 설명, 해야 할 작업, 하지 말아야 할 작업, 단위테스트 상세, 결과 문서, 완료 체크리스트를 따른다.
- `docs/headless_complete_goal_breakdown.csv`에서 G1D-004 행을 함께 확인해 blocked_until, deliverables, unit_test_scope, result_document를 검증한다.
- G1D-004의 범위를 벗어나 Choice, Effect, State 포팅을 진행하지 말라.
- 단위테스트와 결과 문서 없이는 완료가 아니다.
- 원본 `DCGO/Assets/...` 파일은 수정하지 말라.

완료 조건:
- EngineTaskRunner의 run until idle, queue order, error propagation 테스트가 통과한다.
- 결과 문서 `docs/test-results/goals/G1D-004_task_runner_unit_test_results.md`가 작성된다.
```

## 예시: 선행 Goal 차단 시

```text
HeadlessDCGO.Engine Goal G2A-001을 수행하라.

단, blocked_until이 충족되지 않았으면 구현하지 말고 다음 문서만 작성하라.

- 차단 사유
- 필요한 선행 Goal
- 지금 수행 가능한 검토 작업
- 다음 실행 조건
```

## 권장 최종 응답 형식

```text
Goal <GOAL_ID>는 COMPLETE/BLOCKED 상태다.

수정 파일:
- ...

테스트:
- 명령:
- 결과:

결과 문서:
- ...

남은 리스크:
- ...
```
