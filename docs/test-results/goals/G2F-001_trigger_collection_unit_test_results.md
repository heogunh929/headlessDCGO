# G2F-001 Trigger event collection 포팅

## 실행 일시

- 2026-06-25 15:11:12 +09:00
- 환경: Windows PowerShell, .NET SDK via `.\.dotnet\dotnet.exe`

## Goal 범위

- Goal ID: G2F-001
- 목표: Trigger event collection 포팅
- 작업 범위: GameEvent에서 trigger 후보 수집
- 산출물: AutoProcessing trigger collector
- 선행 Goal: G2A-006; G1F-006
- 완료 기준: trigger collection 테스트 통과

## 수정/생성 파일

- 생성: `src/HeadlessDCGO.Engine/Headless/Effects/AutoProcessingTriggerCollector.cs`
- 생성: `tests/G2F-001.Trigger.event.collection.Tests/G2F-001.Trigger.event.collection.Tests.csproj`
- 생성: `tests/G2F-001.Trigger.event.collection.Tests/Program.cs`
- 생성: `docs/test-results/goals/G2F-001_trigger_collection_unit_test_results.md`

## 읽기 전용 AS-IS 참조 파일

- `DCGO/Assets/Scripts/Script/AutoProcessing.cs`
- `DCGO/Assets/Scripts/Script/MultipleSkills.cs`
- `DCGO/Assets/Scripts/Script/SkillInfo.cs`

원본 `DCGO/Assets/...` 파일은 수정하지 않았다.

## 구현 요약

- AS-IS `AutoProcessing.GetSkillInfos`와 `StackSkillInfos`가 timing별 `CanTrigger` 통과 효과를 `StackedSkillInfos`에 모으는 의미를 Headless collector 계약으로 분리했다.
- `AutoProcessingTriggerCollector`를 추가해 `GameEvent`의 `triggerTiming`, `timing`, `effectTiming` metadata를 순서대로 읽고, 없으면 `GameEventType` 이름을 timing으로 사용한다.
- `IEffectQueryService.GetEffectsForTiming` 결과를 registration order 그대로 순회해 `TimingWindowTrigger` 후보를 만든다.
- `playerId`, `sourceEntityId`, `targetEntityId`, `cardId` metadata가 있으면 `EffectRequest.Context`와 비교해 후보를 필터링한다.
- `resolutionMode`, `triggerKind`, `priority` metadata를 trigger 후보에 반영한다.
- `CollectAndEnqueue` API를 통해 수집된 후보를 `EffectScheduler`에 같은 순서로 연결할 수 있게 했다.
- `GameEventType.Unknown`은 상태 변경 없이 명시적 실패 결과를 반환한다.

## 테스트 명령

- `.\.dotnet\dotnet.exe run --project .\tests\G2F-001.Trigger.event.collection.Tests\G2F-001.Trigger.event.collection.Tests.csproj`
- `.\.dotnet\dotnet.exe build .\src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj`
- `.\.dotnet\dotnet.exe run --project .\tests\G1F-006.Continuous.Replacement.query.contract.Tests\G1F-006.Continuous.Replacement.query.contract.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project .\tests\G1F-004.TimingWindowResolver.contract.Tests\G1F-004.TimingWindowResolver.contract.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project .\tests\G1F-003.EffectScheduler.Tests\G1F-003.EffectScheduler.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project .\tests\G2A-006.Legal.action.dispatch.hook.Tests\G2A-006.Legal.action.dispatch.hook.Tests.csproj`

## 테스트 결과

| 구분 | 전체 | 통과 | 실패 | 스킵 |
| --- | ---: | ---: | ---: | ---: |
| G2F-001 전용 테스트 | 10 | 10 | 0 | 0 |
| 선행/인접 회귀 테스트 합계 | 43 | 43 | 0 | 0 |
| 전체 실행 테스트 합계 | 53 | 53 | 0 | 0 |

빌드 결과:

- `HeadlessDCGO.Engine.csproj`: 성공, 경고 0개, 오류 0개

## G2F-001 전용 테스트 상세

- G2F-001 goal row and predecessors are satisfied: PASS
- AS-IS AutoProcessing trigger collection references are recorded: PASS
- Collector uses event timing metadata and keeps registration order: PASS
- Collector falls back to GameEventType when timing metadata is absent: PASS
- Collector filters candidates by source player and target metadata: PASS
- Collector enqueues collected triggers into EffectScheduler: PASS
- Collector rejects unknown events with an explicit failure result: PASS
- Collector returns deterministic candidates for repeated identical input: PASS
- Collector maps event mode kind and priority metadata to triggers: PASS
- G2F-001 source files contain no placeholder or Unity dependency: PASS

## 실패 상세와 수정 여부

- 최종 실패 없음.
- 구현 중 `targetEntityId` 필터가 source entity까지 허용해 후보가 과다 수집되는 실패가 1회 있었고, target 전용 매칭으로 수정한 뒤 G2F-001 전용 테스트 10개가 모두 통과했다.

## 테스트하지 못한 항목과 이유

- 실제 카드별 효과 본문 실행, mandatory/optional 상세 ordering, delayed/security trigger의 실제 룰 처리는 후속 Goal 범위이므로 수행하지 않았다.
- 이번 Goal에서는 `GameEvent`에서 trigger 후보를 수집하고 `EffectScheduler`로 넘길 수 있는 collector 계약까지만 검증했다.

## 미해결 리스크

- `git status --short`는 현재 작업 디렉터리가 git repository가 아니어서 `fatal: not a git repository`로 확인되지 않았다.
- `AutoProcessingTriggerCollector`는 실제 카드 효과 포팅이 아니라 trigger 후보 수집 계약이다. 카드 효과별 `CanTrigger` 의미의 세부 포팅은 후속 Goal에서 확장되어야 한다.

## 완료 기준 충족 근거

- CSV의 단위테스트 범위 `event trigger collection 테스트`를 G2F-001 전용 테스트에서 직접 검증했다.
- 전용 테스트가 정상 수집, 실패 결과, 결정성, metadata 필터, scheduler enqueue 연결을 포함한다.
- 선행 Goal `G2A-006`, `G1F-006` 결과 문서의 COMPLETE를 확인했다.
- 원본 `DCGO/Assets/...` 파일을 수정하지 않았다.

## 다음 Goal 진행 가능 여부

- 가능. G2F-001 산출물 `AutoProcessing trigger collector`와 결과 문서가 준비되었고, 완료 기준 `trigger collection 테스트 통과`를 충족했다.

## 완료 판정

COMPLETE
