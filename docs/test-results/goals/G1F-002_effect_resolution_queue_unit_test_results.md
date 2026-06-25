# G1F-002 EffectResolutionQueue Unit Test Results

## 실행 일시

- 실행 일시: 2026-06-25 09:11:48 +09:00
- 실행 환경: Windows PowerShell, .NET 8, `E:\headlessDCGO_new`
- Goal ID: G1F-002
- 완료 기준: Effect queue 테스트 통과

## 수정/생성 파일

- 수정: `src/HeadlessDCGO.Engine/Headless/Effects/EffectResolutionQueue.cs`
- 수정: `src/HeadlessDCGO.Engine/Headless/Effects/PendingEffect.cs`
- 생성: `tests/G1F-002.EffectResolutionQueue.Tests/G1F-002.EffectResolutionQueue.Tests.csproj`
- 생성: `tests/G1F-002.EffectResolutionQueue.Tests/Program.cs`
- 생성: `docs/test-results/goals/G1F-002_effect_resolution_queue_unit_test_results.md`

## 읽기 전용 AS-IS 확인 파일

- `DCGO/Assets/Scripts/Script/AutoProcessing.cs`
- `DCGO/Assets/Scripts/Script/Effects.cs`
- `DCGO/Assets/Scripts/Script/MultipleSkills.cs`

## 구현 요약

- `PendingEffect`가 null request와 unknown resolution mode를 명시적으로 거부하도록 고정했다.
- `EffectResolutionQueue`가 FIFO enqueue/dequeue 순서를 보존하도록 테스트로 고정했다.
- `TryPeek`을 추가해 첫 pending effect를 소비하지 않고 확인할 수 있게 했다.
- `Snapshot`을 추가해 현재 queue 순서를 immutable snapshot으로 관찰할 수 있게 했다.
- `Clear`가 제거한 effect 수를 반환하도록 해 clear 결과를 명확히 했다.

## 테스트 의도

- CSV의 G1F-002 계약 행이 산출물, 테스트 범위, 결과 문서, 선행 Goal을 유지하는지 검증한다.
- 선행 G1F-001 결과 문서가 COMPLETE를 기록하는지 검증한다.
- `PendingEffect`가 request와 resolution mode를 보존하고 invalid input을 거부하는지 검증한다.
- queue가 enqueue 순서대로 dequeue되는지, peek이 소비하지 않는지, clear가 제거 수와 empty 상태를 보장하는지 검증한다.
- snapshot이 이후 queue mutation과 분리되는지 검증한다.
- AS-IS Unity effect queue 관련 파일은 읽기 전용 참조로만 확인하고 원본 파일을 수정하지 않았음을 검증한다.

## 테스트 명령 및 결과

| 명령 | 전체 | 통과 | 실패 | 스킵 | 결과 |
|---|---:|---:|---:|---:|---|
| `.\.dotnet\dotnet.exe run --project tests\G1F-002.EffectResolutionQueue.Tests\G1F-002.EffectResolutionQueue.Tests.csproj` | 10 | 10 | 0 | 0 | 통과 |
| `.\.dotnet\dotnet.exe run --project tests\G1F-001.Effect.request.context.schema.Tests\G1F-001.Effect.request.context.schema.Tests.csproj` | 10 | 10 | 0 | 0 | 통과 |
| `.\.dotnet\dotnet.exe build src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj` | 1 | 1 | 0 | 0 | 통과, 경고 0개/오류 0개 |

## 실패 상세

- 최종 실패 테스트 없음.
- G1F-002 최초 테스트 실행부터 10/10 통과했다.
- G1F-002 테스트 빌드 중 기존 nullable warning이 표시되었으나, 최종 엔진 단독 빌드는 경고 0개/오류 0개로 통과했다.

## 범위 준수

- 원본 `DCGO/Assets/...` 파일은 수정하지 않았다.
- `DCGO/Assets` 최근 변경 파일 수: 0
- G1F-003 `EffectScheduler` resolve orchestration, choice pause, resolver failure tracing은 수행하지 않았다.
- G1F-005 `EffectRegistry` 및 실제 카드별 룰/효과 포팅은 수행하지 않았다.

## 미해결 리스크

- 이 Goal은 `EffectResolutionQueue/PendingEffect`의 ordering, peek, snapshot, clear 계약만 고정한다.
- scheduler resolution order, timing priority, pending choice integration, resolver failure handling은 후속 Goal 범위로 남긴다.

## 완료 판정

- COMPLETE
- 근거: G1F-002 산출물 `EffectResolutionQueue PendingEffect` 구현 완료, enqueue dequeue order clear 단위테스트 10/10 통과, 선행 G1F-001 회귀 10/10 통과, 엔진 빌드 통과.
