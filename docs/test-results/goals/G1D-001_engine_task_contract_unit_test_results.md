# G1D-001 EngineTask Contract Unit Test Results

## Execution Time And Environment

- Executed at: 2026-06-25 08:08:45 +09:00
- Environment: Windows PowerShell, .NET 8 SDK via `.\.dotnet\dotnet.exe`

## Goal Scope

- Goal ID: G1D-001
- Goal: EngineTask contract
- Scope: Headless task 계약 확정
- Deliverable: IEngineTask EngineTaskStatus
- Unit test scope: task step completion error 테스트
- Required predecessor: G1A-002
- Completion gate: EngineTask contract 테스트 통과

## Predecessor Check

- `docs/test-results/goals/G1A-002_match_lifecycle_unit_test_results.md` exists and records COMPLETE.
- Current predecessor test command passed:
  - `.\.dotnet\dotnet.exe run --project tests\G1A-002.MatchLifecycle.Tests\G1A-002.MatchLifecycle.Tests.csproj`
  - Total 5, passed 5, failed 0, skipped 0

## Modified Or Created Files

- Modified: `src/HeadlessDCGO.Engine/Headless/Coroutines/IEngineTask.cs`
- Modified: `src/HeadlessDCGO.Engine/Headless/Coroutines/EngineTaskRunner.cs`
- Modified: `src/HeadlessDCGO.Engine/Headless/Coroutines/CoroutineAdapter.cs`
- Created: `tests/G1D-001.EngineTask.contract.Tests/G1D-001.EngineTask.contract.Tests.csproj`
- Created: `tests/G1D-001.EngineTask.contract.Tests/Program.cs`
- Created: `docs/test-results/goals/G1D-001_engine_task_contract_unit_test_results.md`

## Read-Only AS-IS Reference Files

- `DCGO/Assets/Scripts/Script/AutoProcessing.cs`
- `DCGO/Assets/Scripts/Script/AttackProcess.cs`
- `DCGO/Assets/Scripts/Script/TurnStateMachine.cs`

These files were checked only to confirm AS-IS coroutine, `StartCoroutine`, `WaitForSeconds`, `WaitWhile`, `WaitUntil`, and `yield return` dependencies. They were not modified.

## Implementation Summary

- `IEngineTask` now exposes:
  - `EngineTaskStatus Status`
  - `EngineWaitCondition? CurrentWait`
  - `Exception? Error`
  - terminal helper properties
  - `Task<EngineTaskStepResult> StepAsync(...)`
- Added `EngineTaskStatus` values: `Pending`, `Waiting`, `Completed`, `Faulted`, `Canceled`.
- Added `EngineTaskStepResult` factory methods for pending, waiting, completed, faulted, and canceled step outcomes.
- Updated existing coroutine runner/adapter code to compile against the typed step result contract and to preserve fault status/error propagation.

## Test Commands

```powershell
.\.dotnet\dotnet.exe run --project tests\G1D-001.EngineTask.contract.Tests\G1D-001.EngineTask.contract.Tests.csproj
.\.dotnet\dotnet.exe run --project tests\G1A-002.MatchLifecycle.Tests\G1A-002.MatchLifecycle.Tests.csproj
.\.dotnet\dotnet.exe run --project tests\G1C-001.EngineContext.Tests\G1C-001.EngineContext.Tests.csproj
.\.dotnet\dotnet.exe build src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj
```

## Test Counts

| Scope | Total | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|
| G1D-001 EngineTask contract | 7 | 7 | 0 | 0 |
| Predecessor G1A-002 MatchLifecycle regression | 5 | 5 | 0 | 0 |
| Bridge regression G1C-001 EngineContext | 7 | 7 | 0 | 0 |
| Total tests executed | 19 | 19 | 0 | 0 |

## Build Result

```text
Build succeeded.
Warnings: 0
Errors: 0
```

## Failure Details And Fixes

- Final failures: none.
- During implementation, the first G1D-001 test run failed to compile because the test accessed default interface helper properties through a concrete test class variable. The test was corrected to validate those helpers through the `IEngineTask` interface, which is the contract under test.

## Untested Items And Reasons

- `EngineWaitCondition` deterministic seconds semantics are not tested here; that is G1D-002 scope.
- `CoroutineAdapter` nested enumerator completion semantics are not tested here; that is G1D-003 scope.
- `EngineTaskRunner` queue ordering and run-until-idle stability are not tested here; that is G1D-004 scope.
- Actual card/rule/effect porting was not performed because this Goal is limited to the Phase 1 EngineTask contract.

## DCGO/Assets Safety

- Original `DCGO/Assets/...` files were read only.
- `DCGO/Assets` recent modified file count during verification: 0.
- `git status --short` could not be used because the current workspace is not recognized as a Git repository.

## Completion Gate Evidence

- `task step completion error` is directly tested by:
  - `IEngineTask step transitions from pending to completed deterministically`
  - `IEngineTask faulted step records error and exposes terminal failure`
  - `EngineTaskRunner propagates faulted task errors to caller`
- `IEngineTask EngineTaskStatus` deliverable is directly tested by:
  - `IEngineTask exposes status wait error and typed step result contract`
  - `EngineTaskStepResult factories encode terminal and nonterminal states`

## Completion Judgment

- COMPLETE
