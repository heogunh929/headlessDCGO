# G1D-003 CoroutineAdapter Unit Test Results

## Execution Time And Environment

- Executed at: 2026-06-25 08:18:15 +09:00
- Environment: Windows PowerShell, .NET 8 SDK via `.\.dotnet\dotnet.exe`

## Goal Scope

- Goal ID: G1D-003
- Goal: CoroutineAdapter
- Scope: IEnumerator adapter contract
- Deliverable: CoroutineAdapter
- Unit test scope: nested enumerator completion tests
- Required predecessor: G1D-002
- Completion gate: CoroutineAdapter tests pass

## Predecessor Check

- `docs/test-results/goals/G1D-002_wait_condition_unit_test_results.md` exists and records `COMPLETE`.
- `tests/G1D-002.WaitCondition.Tests` exists and was re-run successfully during this goal.
- G1D-003 proceeded only after the required predecessor was satisfied.

## Modified Or Created Files

- Modified: `src/HeadlessDCGO.Engine/Headless/Coroutines/CoroutineAdapter.cs`
- Created: `tests/G1D-003.CoroutineAdapter.Tests/G1D-003.CoroutineAdapter.Tests.csproj`
- Created: `tests/G1D-003.CoroutineAdapter.Tests/Program.cs`
- Updated: `docs/test-results/goals/G1D-003_coroutine_adapter_unit_test_results.md`

## Read-Only Files Checked

- `docs/goal-specs/G1D-003_coroutineadapter.md`
- `docs/headless_complete_goal_breakdown.csv`
- `docs/headless_complete_unit_test_plan.md`
- `docs/test-results/goals/G1D-002_wait_condition_unit_test_results.md`
- `src/HeadlessDCGO.Engine/Headless/Coroutines/IEngineTask.cs`
- `src/HeadlessDCGO.Engine/Headless/Coroutines/EngineWaitCondition.cs`
- `src/HeadlessDCGO.Engine/Headless/Coroutines/EngineTaskRunner.cs`
- `DCGO/Assets/Scripts/Script/AutoProcessing.cs`
- `DCGO/Assets/Scripts/Script/AttackProcess.cs`
- `DCGO/Assets/Scripts/Script/TurnStateMachine.cs`

## Test Intent

- Verify the CSV row still defines G1D-003 as the CoroutineAdapter goal and keeps the expected scope, deliverable, test scope, predecessor, result document, and completion gate.
- Verify `CoroutineAdapter.FromEnumerator` rejects null input.
- Verify nested `IEnumerator` execution completes the child enumerator before the parent continues.
- Verify `yield return null` and unknown yielded objects produce deterministic pending steps.
- Verify yielded `EngineWaitCondition` keeps the task waiting until its predicate is satisfied.
- Verify enumerator exceptions are captured as `Faulted` task results and exposed through `IEngineTask.Error`.
- Verify AS-IS Unity coroutine references were inspected as read-only input.
- Verify the delivered source has no placeholder TODO contract and no Unity coroutine dependency.

## Test Commands

- `.\.dotnet\dotnet.exe run --project tests\G1D-003.CoroutineAdapter.Tests\G1D-003.CoroutineAdapter.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project tests\G1D-002.WaitCondition.Tests\G1D-002.WaitCondition.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project tests\G1D-001.EngineTask.contract.Tests\G1D-001.EngineTask.contract.Tests.csproj`
- `.\.dotnet\dotnet.exe build src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj`

## Test Counts

| Scope | Total | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|
| G1D-003 CoroutineAdapter | 8 | 8 | 0 | 0 |
| G1D-002 predecessor regression | 7 | 7 | 0 | 0 |
| G1D-001 coroutine contract regression | 7 | 7 | 0 | 0 |
| Engine build | 1 | 1 | 0 | 0 |

## Command Results

- G1D-003 command result: `8 test(s) passed.`
- G1D-002 regression command result: `7 test(s) passed.`
- G1D-001 regression command result: `7 test(s) passed.`
- Engine build result: build succeeded with 0 warnings and 0 errors.
- The first G1D-003 test run emitted existing nullable warnings from runtime files outside this goal during compilation, but the explicit engine build completed cleanly afterward with 0 warnings and 0 errors.

## Failure Details And Fixes

- No G1D-003 test failed.
- No same-goal repair loop was required after the G1D-003 test run.

## Untested Items And Reasons

- Task queue ordering and run-until-idle behavior are not tested here because they belong to G1D-004 TaskRunner stabilization.
- Wall-clock elapsed advancement for seconds waits is not implemented or tested here because G1D-003 only fixes the `IEnumerator` adapter contract.
- Real Unity `StartCoroutine` execution is not tested because original Unity assets are read-only references and the headless adapter must remain Unity-free.

## DCGO/Assets Safety

- Original `DCGO/Assets/...` files were read only for AS-IS coroutine semantics.
- Recent modified-file check under `DCGO/Assets` returned `0`.
- No original `DCGO/Assets/...` file was modified.

## Completion Gate Evidence

- `CoroutineAdapter.FromEnumerator` returns an `IEngineTask` for deterministic `IEnumerator` stepping.
- Nested enumerator completion is directly verified by execution order assertions.
- Wait conditions, pending yields, unknown yields, and exception propagation are directly verified.
- G1D-003 unit tests passed 8/8.
- Required predecessor regression tests passed 7/7.
- Engine project build succeeded.

## Next Goal Availability

- G1D-004 can proceed from the G1D-003 perspective because the CoroutineAdapter completion gate passed.

## Completion Judgment

- COMPLETE
