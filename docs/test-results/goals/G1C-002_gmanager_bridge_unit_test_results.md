# G1C-002 GManagerBridge

## 실행 일시

- 2026-06-25 00:43:17 +09:00
- 환경: Windows PowerShell, `.NET 8` SDK via `.\.dotnet\dotnet.exe`

## 수정/생성 파일

- 수정: `src/HeadlessDCGO.Engine/Headless/Bridge/GManagerBridge.cs`
- 생성: `tests/G1C-002.GManagerBridge.Tests/G1C-002.GManagerBridge.Tests.csproj`
- 생성: `tests/G1C-002.GManagerBridge.Tests/Program.cs`
- 생성: `docs/test-results/goals/G1C-002_gmanager_bridge_unit_test_results.md`

## 읽기 전용 확인 파일

- `docs/goal-specs/G1C-002_gmanagerbridge.md`
- `docs/headless_complete_goal_breakdown.csv`
- `docs/headless_complete_unit_test_plan.md`
- `docs/test-results/goals/G1C-001_engine_context_unit_test_results.md`
- `src/HeadlessDCGO.Engine/Headless/Bridge/EngineContext.cs`
- `src/HeadlessDCGO.Engine/Headless/Runtime/IHeadlessTurnController.cs`
- `src/HeadlessDCGO.Engine/Headless/Runtime/IHeadlessAttackController.cs`
- `src/HeadlessDCGO.Engine/Headless/Runtime/IHeadlessChoiceController.cs`
- `src/HeadlessDCGO.Engine/Headless/Runtime/IHeadlessMemoryController.cs`

## 구현 요약

- `GManagerBridge`를 `EngineContext` 기반 GManager 대체 mapping으로 고정했다.
- `Turn`, `Effects`, `AutoProcessing`, `Attack`, `State`, `Log`, `CurrentMatch` 접근자를 추가했다.
- 기존 `GetTurnStateMachine`, `GetAutoProcessing`, `GetAttackProcess`, `GetEffectScheduler`는 호환 alias로 유지했다.
- `GetCurrentState`, `GetLog`, `GetService<T>`, `GetService(Type)`, `TryGetService<T>`를 추가해 service access 계약을 명시했다.
- null context와 미등록 service 조회는 명확한 예외/false 결과로 실패한다.

## 테스트 명령

```powershell
.\.dotnet\dotnet.exe run --project tests\G1C-001.EngineContext.Tests\G1C-001.EngineContext.Tests.csproj
.\.dotnet\dotnet.exe run --project tests\G1C-002.GManagerBridge.Tests\G1C-002.GManagerBridge.Tests.csproj
.\.dotnet\dotnet.exe run --project tests\G1A-002.MatchLifecycle.Tests\G1A-002.MatchLifecycle.Tests.csproj
.\.dotnet\dotnet.exe run --project tests\G1A-004.Observation.LegalAction.Tests\G1A-004.Observation.LegalAction.Tests.csproj
.\.dotnet\dotnet.exe build src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj
```

## 테스트 결과

| 범위 | 전체 | 통과 | 실패 | 스킵 |
|---|---:|---:|---:|---:|
| G1C-002 GManagerBridge | 6 | 6 | 0 | 0 |
| 선행 G1C-001 EngineContext 확인 | 7 | 7 | 0 | 0 |
| 회귀 확인 G1A-002 MatchLifecycle | 5 | 5 | 0 | 0 |
| 회귀 확인 G1A-004 Observation/LegalAction | 7 | 7 | 0 | 0 |
| 합계 | 25 | 25 | 0 | 0 |

## 빌드 결과

```text
Build succeeded.
Warnings: 0
Errors: 0
```

## 실패 상세

- 최종 실패 없음.

## 테스트하지 않은 항목과 이유

- Unity `GameObject`, scene component, UI, animation, Photon 접근은 G1C-002 범위 밖이므로 구현/테스트하지 않았다.
- 실제 카드별 룰/효과 포팅은 이후 Phase/Goal 범위이므로 수행하지 않았다.

## 미해결 리스크

- `git status`와 `git status --short -- DCGO\Assets`는 현재 작업 디렉터리가 Git 저장소로 인식되지 않아 실행할 수 없었다.
- 원본 `DCGO/Assets/...` 파일은 수정하지 않았고, 작업 중 해당 경로에 쓰기 명령을 실행하지 않았다.

## 완료 판정

- 선행 Goal `G1C-001`의 결과 문서와 현재 테스트 통과를 확인했다.
- CSV 기준 산출물 `GManagerBridge`를 구현했다.
- CSV 기준 단위테스트 범위 `turn effect attack state service access 테스트`를 G1C-002 테스트에서 직접 검증했다.
- 완료 기준 `GManagerBridge 테스트 통과` 충족.
- 판정: COMPLETE
