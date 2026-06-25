# G1F-003 EffectScheduler

## 실행 일시
- 2026-06-25 09:18:55 +09:00
- 환경: Windows PowerShell, .NET SDK via `.\.dotnet\dotnet.exe`

## 수정/생성 파일
- 수정: `src/HeadlessDCGO.Engine/Headless/Effects/EffectScheduler.cs`
- 생성: `tests/G1F-003.EffectScheduler.Tests/G1F-003.EffectScheduler.Tests.csproj`
- 생성: `tests/G1F-003.EffectScheduler.Tests/Program.cs`
- 생성: `docs/test-results/goals/G1F-003_effect_scheduler_unit_test_results.md`

## 읽기 전용으로 확인한 AS-IS 파일
- `DCGO/Assets/Scripts/Script/AutoProcessing.cs`
- `DCGO/Assets/Scripts/Script/Effects.cs`
- `DCGO/Assets/Scripts/Script/MultipleSkills.cs`

## 선행 Goal 확인
- `docs/test-results/goals/G1F-002_effect_resolution_queue_unit_test_results.md`: COMPLETE 확인
- `docs/test-results/goals/G1E-005_choice_pause_resume_unit_test_results.md`: COMPLETE 확인

## 구현 요약
- `EffectScheduler.ResolveNextAsync`가 queue head를 먼저 peek하고, resolver 성공 결과(`Resolved == true`)에서만 pending effect를 dequeue하도록 고정했다.
- choice pause나 resolver failure처럼 unresolved 결과가 반환되면 pending effect를 보존하고 `LastResolvedCount`를 0으로 기록한다.
- resolver 예외는 `EffectResult.Failure`로 변환하고 effect id, mode, error, error type을 trace metadata에 담는다.
- cancellation은 `OperationCanceledException`으로 전파하며 pending effect를 보존한다.
- `ResolveAllAsync`는 FIFO로 성공 effect를 계속 resolve하되 unresolved 결과에서 멈춘다.
- `Enqueue(EffectRequest, EffectResolutionMode)` overload로 mode가 있는 pending effect enqueue 계약을 노출했다.

## 테스트 명령
- `.\.dotnet\dotnet.exe run --project tests\G1F-003.EffectScheduler.Tests\G1F-003.EffectScheduler.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project tests\G1F-002.EffectResolutionQueue.Tests\G1F-002.EffectResolutionQueue.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project tests\G1E-005.Choice.pause.resume.contract.Tests\G1E-005.Choice.pause.resume.contract.Tests.csproj`
- `.\.dotnet\dotnet.exe build src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj`

## 테스트 결과
| 범위 | 전체 | 통과 | 실패 | 스킵 |
|---|---:|---:|---:|---:|
| G1F-003 EffectScheduler | 11 | 11 | 0 | 0 |
| G1F-002 predecessor regression | 10 | 10 | 0 | 0 |
| G1E-005 predecessor regression | 10 | 10 | 0 | 0 |
| Engine build | 1 | 1 | 0 | 0 |

## 실패 상세
- 없음.

## 참고 사항
- G1F-003 테스트 명령 실행 중 `HeadlessGameLoop.cs`와 `MetadataActionProcessor.cs`의 기존 nullable warning이 출력되었으나 실패는 없었다.
- 별도 엔진 빌드 명령은 경고 0개, 오류 0개로 완료되었다.

## 테스트하지 못한 항목
- 없음. CSV의 단위테스트 범위 `resolve next resolve all choice pause 테스트`를 전용 테스트로 검증했다.

## 미해결 리스크
- 실제 카드별 effect 포팅은 Phase 1 범위 밖이므로 수행하지 않았다.
- `EffectRegistry` 및 timing window resolver는 후속 Goal 범위이므로 변경하지 않았다.

## 완료 기준 충족 근거
- `EffectScheduler` public API가 resolve next, resolve all, choice pause 보존, resolver failure, cancellation, 입력 검증을 단위테스트로 검증했다.
- 원본 `DCGO/Assets/...` 파일은 읽기 전용으로만 확인했고 수정하지 않았다.

## 완료 판정
- COMPLETE
