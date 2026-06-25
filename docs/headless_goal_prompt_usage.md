# HeadlessDCGO.Engine 짧은 Goal 프롬프트 사용법

## 목적

`docs/goal-specs/*.md` 상세 지시서는 작업 기준을 보존하기 위한 긴 문서다.
실제로 Goal을 맡길 때는 긴 상세 지시서를 통째로 붙이지 말고, `docs/headless_goal_prompts_compact_ko.csv`의 짧은 Goal 프롬프트를 사용한다.

## 사용 규칙

- 실제 작업 지시는 짧은 Goal 프롬프트 하나만 보낸다.
- 작업자는 프롬프트에 적힌 상세 지시서를 먼저 읽는다.
- 상세 지시서는 기준 문서이고, Goal 프롬프트는 실행 지시다.
- Goal 하나가 끝나기 전 다음 Goal을 진행하지 않는다.
- 단위테스트와 결과 문서가 없으면 완료가 아니다.

## 출력 파일

- `docs/headless_goal_prompts_compact_ko.csv`
- `docs/goal-specs/*.md`

## 짧은 Goal 프롬프트 예시

```text
HeadlessDCGO.Engine Goal G0-001만 수행하라.
목표: 설계 산출물 확정
상세 지시서: docs/goal-specs/G0-001_설계_산출물_확정.md
선행 Goal: 없음
결과 문서: docs/test-results/goals/G0-001_design_artifacts_unit_test_results.md

규칙:
- 먼저 상세 지시서를 읽고 그 범위만 수행하라.
- 원본 DCGO/Assets 파일은 수정하지 말라.
- Goal 밖 작업과 다음 Phase 선행 작업을 하지 말라.
- 단위테스트와 결과 문서 없이는 완료로 말하지 말라.
- 완료 기준: 문서와 CSV가 검증됨
```

## 권장 사용 방식

```text
docs/headless_goal_prompts_compact_ko.csv에서 <GOAL_ID> 행의 "짧은 Goal 프롬프트"만 사용한다.
상세 지시서는 프롬프트 안의 경로를 작업자가 직접 읽는다.
```
