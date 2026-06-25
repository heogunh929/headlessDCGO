# G1C-004 Unity-only exclusion policy Unit Test Results

## Goal

- Goal ID: G1C-004 Unity-only exclusion policy
- Deliverable: UnityNullObjectPolicy
- Completion gate: Unity-only exclusion tests passed

## Execution Time And Environment

- Executed at: 2026-06-25 00:55:27 +09:00
- Environment: Windows PowerShell, .NET 8 SDK via `.\.dotnet\dotnet.exe`

## Modified Or Created Files

- Modified: `src/HeadlessDCGO.Engine/Headless/Bridge/UnityNullObjectPolicy.cs`
- Created: `tests/G1C-004.Unity.only.exclusion.policy.Tests/G1C-004.Unity.only.exclusion.policy.Tests.csproj`
- Created: `tests/G1C-004.Unity.only.exclusion.policy.Tests/Program.cs`
- Created: `docs/test-results/goals/G1C-004_unity_exclusion_policy_unit_test_results.md`

## Read-Only AS-IS Reference Files

- `DCGO/Assets/Scripts/Script/GManager.cs`
- `DCGO/Assets/Scripts/Script/ContinuousController.cs`
- `DCGO/Assets/Scripts/Script/AutoProcessing.cs`
- `DCGO/Assets/Scripts/Script/AttackProcess.cs`

## Implementation Summary

- Replaced the placeholder `UnityNullObjectPolicy` with a deterministic public policy API.
- Added `UnityOnlyAccess`, `UnityNullObjectDecision`, `UnityNullObjectDecisionKind`, and `UnityOnlyAccessCategory`.
- UI, scene lifecycle, animation, audio, camera, and client network access resolve to explicit headless no-op exclusions when they do not mutate gameplay state.
- `GManager.instance`, `ContinuousController.instance`, `GetComponent<Effects>`, scene hierarchy, and typed effect context cases resolve to explicit headless service replacement requirements.
- Unknown categories and invalid inputs resolve to clear rejection or argument failure.

## Test Commands

```powershell
.\.dotnet\dotnet.exe run --project tests\G1C-004.Unity.only.exclusion.policy.Tests\G1C-004.Unity.only.exclusion.policy.Tests.csproj
.\.dotnet\dotnet.exe run --project tests\G1C-002.GManagerBridge.Tests\G1C-002.GManagerBridge.Tests.csproj
.\.dotnet\dotnet.exe run --project tests\G1C-003.ContinuousContext.Tests\G1C-003.ContinuousContext.Tests.csproj
.\.dotnet\dotnet.exe build src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj
```

## Test Counts

| Scope | Total | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|
| G1C-004 Unity-only exclusion policy | 7 | 7 | 0 | 0 |
| Predecessor G1C-002 GManagerBridge regression | 6 | 6 | 0 | 0 |
| Predecessor G1C-003 ContinuousContext regression | 7 | 7 | 0 | 0 |
| Total tests executed | 20 | 20 | 0 | 0 |

## Build Result

```text
Build succeeded.
Warnings: 0
Errors: 0
```

## Failure Details And Fixes

- Final failures: none.
- During implementation, the first G1C-004 test run failed because the test expected an English CSV `scope` string while the CSV contains the Korean scope text. The test was corrected to assert the stable `Unity-only` contract marker from the actual CSV row.

## Untested Items And Reasons

- No Unity runtime, scene, UI, animation, camera, audio, or Photon package execution was tested. G1C-004 scope is the headless exclusion policy contract, not Unity runtime integration.
- No card rule or card effect porting was performed or tested because this Goal is limited to Phase 1 bridge policy.

## DCGO/Assets Safety

- Original `DCGO/Assets/...` files were read only for AS-IS behavior confirmation.
- `DCGO/Assets` recent modified file count during final verification: 0.
- `git status --short` could not be used because the current workspace is not recognized as a Git repository.

## Completion Gate Evidence

- `UI scene animation access exclusion` is directly tested by `PolicyExcludesUiSceneAnimationAccess`.
- Invalid and unknown Unity-only access is directly tested by `PolicyRejectsInvalidOrUnknownAccess`.
- Determinism is directly tested by `PolicyDecisionsAreDeterministic`.
- Service replacement requirements are directly tested by `PolicyMapsGameplayUnityAccessToServices`.

## Next Goal Readiness

- G1C-004 is COMPLETE.
- Next Goal can proceed if it depends on `UnityNullObjectPolicy` and the G1C-002/G1C-003 predecessor contracts.
