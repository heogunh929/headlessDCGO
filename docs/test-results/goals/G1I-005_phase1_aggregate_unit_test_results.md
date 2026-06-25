# G1I-005 Phase 1 Aggregate Result Unit Test Results

## Execution

- Executed at: 2026-06-25 10:43:28 +09:00
- Goal ID: G1I-005
- Goal: Phase 1 aggregate result
- Scope: Phase 1 full result aggregation
- Deliverable: phase1 result document
- Completion gate: Phase 2 start allowed
- Final status: PASS

## Files Changed Or Created

- Created: `docs/test-results/headless_phase1_unity_replacement_unit_test_results.md`
- Created: `tests/G1I-005.Phase.1.aggregate.result.Tests/G1I-005.Phase.1.aggregate.result.Tests.csproj`
- Created: `tests/G1I-005.Phase.1.aggregate.result.Tests/Program.cs`
- Created: `docs/test-results/goals/G1I-005_phase1_aggregate_unit_test_results.md`

## Read-Only References Checked

- `docs/goal-specs/G1I-005_phase_1_aggregate_result.md`
- `docs/headless_complete_goal_breakdown.csv`
- `docs/headless_complete_goal_breakdown_detailed_ko.csv`
- `docs/headless_complete_porting_sequence.md`
- `docs/test-results/goals/G1A-005_runtime_aggregate_unit_test_results.md`
- `docs/test-results/goals/G1B-006_state_snapshot_fingerprint_unit_test_results.md`
- `docs/test-results/goals/G1C-004_unity_exclusion_policy_unit_test_results.md`
- `docs/test-results/goals/G1D-004_task_runner_unit_test_results.md`
- `docs/test-results/goals/G1E-005_choice_pause_resume_unit_test_results.md`
- `docs/test-results/goals/G1F-006_continuous_replacement_query_unit_test_results.md`
- `docs/test-results/goals/G1G-003_photon_dependency_guard_unit_test_results.md`
- `docs/test-results/goals/G1H-005_unity_asset_exclusion_guard_unit_test_results.md`
- `docs/test-results/goals/G1I-004_forbidden_dependency_scan_unit_test_results.md`

## Test Intent

- Verify the G1I-005 CSV row keeps the Phase 1 aggregate contract.
- Verify all 9 required predecessor result documents exist, record COMPLETE, and include zero-failure evidence.
- Verify all 9 required predecessor test projects still exist.
- Verify `docs/test-results/headless_phase1_unity_replacement_unit_test_results.md` links every required predecessor result document and test project.
- Verify the aggregate document records 9 required gates, 9 complete gates, 0 blocked gates, and 0 failed gates.
- Verify an incomplete or missing predecessor document is rejected by the aggregate evidence model.
- Verify aggregate evidence fingerprinting is deterministic.
- Verify this Goal remains limited to documents and tests, with no Phase 2 implementation and no original `DCGO/Assets/...` modification.

## Test Commands

```powershell
.\.dotnet\dotnet.exe run --project tests\G1I-005.Phase.1.aggregate.result.Tests\G1I-005.Phase.1.aggregate.result.Tests.csproj
.\.dotnet\dotnet.exe run --project tests\G1A-005.Runtime.Aggregate.Tests\G1A-005.Runtime.Aggregate.Tests.csproj
.\.dotnet\dotnet.exe run --project tests\G1B-006.State.snapshot.fingerprint.Tests\G1B-006.State.snapshot.fingerprint.Tests.csproj
.\.dotnet\dotnet.exe run --project tests\G1C-004.Unity.only.exclusion.policy.Tests\G1C-004.Unity.only.exclusion.policy.Tests.csproj
.\.dotnet\dotnet.exe run --project tests\G1D-004.TaskRunner.Tests\G1D-004.TaskRunner.Tests.csproj
.\.dotnet\dotnet.exe run --project tests\G1E-005.Choice.pause.resume.contract.Tests\G1E-005.Choice.pause.resume.contract.Tests.csproj
.\.dotnet\dotnet.exe run --project tests\G1F-006.Continuous.Replacement.query.contract.Tests\G1F-006.Continuous.Replacement.query.contract.Tests.csproj
.\.dotnet\dotnet.exe run --project tests\G1G-003.Photon.dependency.guard.Tests\G1G-003.Photon.dependency.guard.Tests.csproj
.\.dotnet\dotnet.exe run --project tests\G1H-005.Unity.asset.exclusion.guard.Tests\G1H-005.Unity.asset.exclusion.guard.Tests.csproj
.\.dotnet\dotnet.exe run --project tests\G1I-004.Forbidden.dependency.scan.Tests\G1I-004.Forbidden.dependency.scan.Tests.csproj
.\.dotnet\dotnet.exe build src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj
```

## Test Counts

| Scope | Total | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|
| G1I-005 direct aggregate tests | 9 | 9 | 0 | 0 |
| G1A-005 Runtime aggregate tests | 4 | 4 | 0 | 0 |
| G1B-006 State snapshot fingerprint tests | 7 | 7 | 0 | 0 |
| G1C-004 Unity-only exclusion policy tests | 7 | 7 | 0 | 0 |
| G1D-004 TaskRunner tests | 9 | 9 | 0 | 0 |
| G1E-005 Choice pause resume tests | 10 | 10 | 0 | 0 |
| G1F-006 Continuous replacement query tests | 11 | 11 | 0 | 0 |
| G1G-003 Photon dependency guard tests | 8 | 8 | 0 | 0 |
| G1H-005 Unity asset exclusion guard tests | 10 | 10 | 0 | 0 |
| G1I-004 Forbidden dependency scan tests | 10 | 10 | 0 | 0 |
| Total tests | 85 | 85 | 0 | 0 |

Build result:

- Command: `.\.dotnet\dotnet.exe build src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj`
- Warnings: 0
- Errors: 0

## Failure Details

- Final failure count: 0.
- The first G1I-005 direct test run failed 1 assertion because the CSV unit test scope is Korean text and does not contain the English word `test`.
- The assertion was corrected inside the G1I-005 test scope to require a populated unit test scope and the stable `Goal` contract marker.
- The direct G1I-005 test was rerun and passed 9/9.

## Phase 2 Start Gate Evidence

- All required predecessor gate documents exist and record COMPLETE.
- All required predecessor gate documents include zero-failure evidence.
- All required predecessor gate test projects exist and were rerun successfully.
- The Phase 1 aggregate result document exists at `docs/test-results/headless_phase1_unity_replacement_unit_test_results.md`.
- The engine project builds with 0 warnings and 0 errors.
- No Phase 2 implementation was started.
- No original `DCGO/Assets/...` files were modified.

## Untested Items

- Phase 2 AS-IS core flow porting, gameplay rule/card effect behavior, and card-specific effect implementation were not tested or implemented because they are outside G1I-005.
- Original Unity asset behavior was not tested because G1I-005 is a result aggregation Goal.

## Unresolved Risks

- The aggregate gate relies on each predecessor result document accurately recording its final state; G1I-005 verifies document presence, COMPLETE status, zero-failure evidence, test project presence, and reruns the 9 gate test projects.
- Some older result documents contain mojibake in Korean sections, but their stable COMPLETE markers and numeric pass/fail tables remain machine-verifiable.

## Completion Judgment

COMPLETE - G1I-005 Phase 1 aggregate result is complete. The Phase 1 aggregate result document links all 9 required gate results, all direct and predecessor gate tests pass 85/85, the engine builds with 0 warnings and 0 errors, and the documented completion gate is Phase 2 start allowed.
