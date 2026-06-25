# G2F-002 Mandatory effect ordering 포팅

## 실행 일시

- 2026-06-25 15:15:56 +09:00
- 환경: Windows PowerShell, .NET SDK via `.\.dotnet\dotnet.exe`

## Goal 범위

- Goal ID: G2F-002
- 목표: Mandatory effect ordering 포팅
- 작업 범위: mandatory effect 순서 처리
- 산출물: mandatory effect ordering
- 선행 Goal: G2F-001
- 완료 기준: mandatory ordering 테스트 통과

## 수정/생성 파일

- 생성: `src/HeadlessDCGO.Engine/Headless/Effects/MandatoryEffectOrdering.cs`
- 생성: `tests/G2F-002.Mandatory.effect.ordering.Tests/G2F-002.Mandatory.effect.ordering.Tests.csproj`
- 생성: `tests/G2F-002.Mandatory.effect.ordering.Tests/Program.cs`
- 생성: `docs/test-results/goals/G2F-002_mandatory_effect_order_unit_test_results.md`

## 읽기 전용 AS-IS 참조 파일

- `DCGO/Assets/Scripts/Script/AutoProcessing.cs`
- `DCGO/Assets/Scripts/Script/MultipleSkills.cs`
- `DCGO/Assets/Scripts/Script/ContinuousController.cs`

원본 `DCGO/Assets/...` 파일은 수정하지 않았다.

## 구현 요약

- AS-IS `MultipleSkills.ActivateMultipleSkills`가 trigger 후보를 턴 플레이어 그룹과 비턴 플레이어 그룹으로 나누고 턴 플레이어를 먼저 처리하는 의미를 Headless ordering 계약으로 분리했다.
- `MandatoryEffectOrdering`을 추가해 G2F-001 collector가 만든 `TimingWindowTrigger` 후보 중 mandatory trigger만 정렬한다.
- optional trigger는 후속 G2F-003 범위로 넘기기 위해 `DeferredOptionalTriggers`로 분리하고 enqueue하지 않는다.
- mandatory trigger는 턴 플레이어, 비턴 플레이어 순으로 그룹화하고, 그룹 안에서는 priority, sequence, 입력 순서로 안정 정렬한다.
- `OrderAndEnqueue` API를 통해 정렬된 mandatory trigger만 `EffectScheduler`에 FIFO 순서로 넣을 수 있게 했다.
- turn/non-turn player id와 trigger 입력 null에 대해 상태 변경 없는 명시적 실패 결과를 반환한다.

## 테스트 명령

- `.\.dotnet\dotnet.exe run --project .\tests\G2F-002.Mandatory.effect.ordering.Tests\G2F-002.Mandatory.effect.ordering.Tests.csproj`
- `.\.dotnet\dotnet.exe build .\src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj`
- `.\.dotnet\dotnet.exe run --project .\tests\G2F-001.Trigger.event.collection.Tests\G2F-001.Trigger.event.collection.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project .\tests\G1F-004.TimingWindowResolver.contract.Tests\G1F-004.TimingWindowResolver.contract.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project .\tests\G1F-003.EffectScheduler.Tests\G1F-003.EffectScheduler.Tests.csproj`

## 테스트 결과

| 구분 | 전체 | 통과 | 실패 | 스킵 |
| --- | ---: | ---: | ---: | ---: |
| G2F-002 전용 테스트 | 10 | 10 | 0 | 0 |
| 선행/인접 회귀 테스트 합계 | 32 | 32 | 0 | 0 |
| 전체 실행 테스트 합계 | 42 | 42 | 0 | 0 |

빌드 결과:

- `HeadlessDCGO.Engine.csproj`: 성공, 경고 0개, 오류 0개

## G2F-002 전용 테스트 상세

- G2F-002 goal row and predecessor are satisfied: PASS
- AS-IS mandatory ordering references are recorded: PASS
- Ordering places turn player mandatory effects before non-turn player effects: PASS
- Ordering defers optional triggers without enqueueing them: PASS
- Ordering sorts mandatory effects by priority sequence and stable input order: PASS
- Ordering keeps deterministic results for repeated identical input: PASS
- Ordering reports unknown player mandatory triggers separately: PASS
- Ordering enqueues mandatory effects into scheduler in sorted order: PASS
- Ordering returns explicit failure results for invalid input: PASS
- G2F-002 source files contain no placeholder or Unity dependency: PASS

## 실패 상세와 수정 여부

- 최종 실패 없음.
- 테스트 작성 중 `EffectContext` fixture 생성자 호출 오류가 1회 있었고, 테스트 helper를 올바른 생성자 형태로 수정한 뒤 G2F-002 전용 테스트 10개가 모두 통과했다.

## 테스트하지 못한 항목과 이유

- optional effect 선택 프롬프트 큐는 후속 Goal G2F-003 범위라 구현/검증하지 않았다.
- 실제 카드 효과 본문 실행, `CanActivate` 세부 효과 판정, UI 선택 패널, 자동 효과 순서 설정 UI는 이번 Goal 범위 밖이다.

## 미해결 리스크

- `git status --short`는 현재 작업 디렉터리가 git repository가 아니어서 `fatal: not a git repository`로 확인되지 않았다.
- 이번 구현은 mandatory trigger ordering 계약이다. 실제 카드별 mandatory 효과 본문 포팅은 후속 카드 효과/자동처리 Goal에서 확장되어야 한다.

## 완료 기준 충족 근거

- CSV의 단위테스트 범위 `mandatory order 테스트`를 G2F-002 전용 테스트에서 직접 검증했다.
- 전용 테스트가 턴/비턴 플레이어 그룹 순서, optional 분리, priority/sequence 안정 정렬, 결정성, scheduler enqueue, 실패 모델을 포함한다.
- 선행 Goal `G2F-001` 결과 문서의 COMPLETE를 확인했다.
- 원본 `DCGO/Assets/...` 파일을 수정하지 않았다.

## 다음 Goal 진행 가능 여부

- 가능. G2F-002 산출물 `mandatory effect ordering`과 결과 문서가 준비되었고, 완료 기준 `mandatory ordering 테스트 통과`를 충족했다.

## 완료 판정

COMPLETE
