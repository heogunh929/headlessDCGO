# G1I-004 Forbidden dependency scan

## Execution

- Goal ID: G1I-004
- Executed at: 2026-06-25 10:36:10 +09:00
- Environment: Windows PowerShell, .NET SDK via `.\.dotnet\dotnet.exe`
- Completion gate: forbidden dependency test pass

## Changed Files

- Created `tests/G1I-004.Forbidden.dependency.scan.Tests/G1I-004.Forbidden.dependency.scan.Tests.csproj`
- Created `tests/G1I-004.Forbidden.dependency.scan.Tests/Program.cs`
- Created `docs/test-results/goals/G1I-004_forbidden_dependency_scan_unit_test_results.md`

## Read-Only References

- `docs/goal-specs/G1I-004_forbidden_dependency_scan.md`
- `docs/headless_complete_goal_breakdown.csv`
- `docs/headless_complete_goal_breakdown_ko.csv`
- `docs/headless_complete_goal_breakdown_detailed_ko.csv`
- `docs/test-results/goals/G1G-003_photon_dependency_guard_unit_test_results.md`
- `docs/test-results/goals/G1H-005_unity_asset_exclusion_guard_unit_test_results.md`
- `src/HeadlessDCGO.Engine/Headless`
- `src/HeadlessDCGO.Engine/HeadlessDCGO.Engine.csproj`
- `src/HeadlessDCGO.Engine/Assets`
- `DCGO/Assets/Scripts/Script/TurnStateMachine.cs`
- `DCGO/Assets/Scripts/Script/PlayLog.cs`
- `DCGO/Assets/Scripts/Script/GameRandom.cs`

## Test Intent

The G1I-004 tests verify the dependency scan contract for Headless source and project files. The scan fails on Unity, Photon, TMPro, DOTween, and Unity UI namespace/runtime tokens in controlled sample input, while confirming the actual Headless source and `HeadlessDCGO.Engine.csproj` contain none of those dependencies. The AS-IS `Assets` source trees are read-only references and are intentionally outside the Headless scan target.

## Test Commands

```powershell
.\.dotnet\dotnet.exe run --project tests\G1I-004.Forbidden.dependency.scan.Tests\G1I-004.Forbidden.dependency.scan.Tests.csproj
.\.dotnet\dotnet.exe run --project tests\G1G-003.Photon.dependency.guard.Tests\G1G-003.Photon.dependency.guard.Tests.csproj
.\.dotnet\dotnet.exe run --project tests\G1H-005.Unity.asset.exclusion.guard.Tests\G1H-005.Unity.asset.exclusion.guard.Tests.csproj
.\.dotnet\dotnet.exe build src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj
```

## Results

| Scope | Total | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|
| G1I-004 direct unit tests | 10 | 10 | 0 | 0 |
| G1G-003 predecessor regression tests | 8 | 8 | 0 | 0 |
| G1H-005 predecessor regression tests | 10 | 10 | 0 | 0 |
| Total tests | 28 | 28 | 0 | 0 |

Build result:

- Command: `.\.dotnet\dotnet.exe build src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj`
- Warnings: 0
- Errors: 0

## Failure Details

- None.

## Untested Items

- No original `DCGO/Assets/...` files were modified; they were read only as AS-IS references.
- `src/HeadlessDCGO.Engine/Assets/...` remains out of scope for the Headless dependency scan because it is an AS-IS source mirror, not the Headless runtime surface.
- Phase 1 aggregate reporting is reserved for G1I-005 and was not performed here.

## Open Risks

- The scan is token based. It is intentionally strict for direct namespace/runtime usage, but it does not parse C# semantic references.
- Conceptual terms such as `UnityNullObjectPolicy` are allowed when they do not import or invoke Unity runtime namespaces.

## Completion Judgment

COMPLETE - G1I-004 implements the dependency scan test for Unity, Photon, TMPro, DOTween, and UI namespace absence in Headless source/project files, passes predecessor regression tests, and builds with 0 warnings and 0 errors.
