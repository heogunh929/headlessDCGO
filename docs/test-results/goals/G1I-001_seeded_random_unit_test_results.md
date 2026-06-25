# G1I-001 Seeded random

## Execution

- Goal ID: G1I-001
- Executed at: 2026-06-25 10:22:49 +09:00
- Environment: Windows PowerShell, .NET SDK via `.\.dotnet\dotnet.exe`
- Completion gate: Seeded random test pass

## Changed Files

- Modified `src/HeadlessDCGO.Engine/Headless/Services/IRandomSource.cs`
- Modified `src/HeadlessDCGO.Engine/Headless/Services/IRandomSeedController.cs`
- Modified `src/HeadlessDCGO.Engine/Headless/Services/GameRandomSource.cs`
- Created `tests/G1I-001.Seeded.random.Tests/G1I-001.Seeded.random.Tests.csproj`
- Created `tests/G1I-001.Seeded.random.Tests/Program.cs`
- Created `docs/test-results/goals/G1I-001_seeded_random_unit_test_results.md`

## Read-Only References

- `docs/goal-specs/G1I-001_seeded_random.md`
- `docs/headless_complete_goal_breakdown.csv`
- `docs/headless_complete_goal_breakdown_ko.csv`
- `docs/headless_complete_goal_breakdown_detailed_ko.csv`
- `docs/test-results/goals/G1A-001_runtime_models_unit_test_results.md`
- `DCGO/Assets/Scripts/Script/GameRandom.cs`

## Test Intent

The tests verify only the G1I-001 deterministic random contract: `IRandomSource` exposes `NextInt`, `NextDouble`, and `Shuffle`; `GameRandomSource` produces repeatable sequences for the same seed; different seeds produce observably different sequences; `ResetSeed` replays the original sequence; and shuffle order is deterministic while preserving members.

## Test Commands

```powershell
.\.dotnet\dotnet.exe run --project tests\G1I-001.Seeded.random.Tests\G1I-001.Seeded.random.Tests.csproj
.\.dotnet\dotnet.exe run --project tests\G1A-001.RuntimeModels.Tests\G1A-001.RuntimeModels.Tests.csproj
.\.dotnet\dotnet.exe run --project tests\G1B-003.ZoneState.Tests\G1B-003.ZoneState.Tests.csproj
.\.dotnet\dotnet.exe run --project tests\G1B-005.ZoneMover.Tests\G1B-005.ZoneMover.Tests.csproj
.\.dotnet\dotnet.exe build src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj
```

## Results

| Scope | Total | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|
| G1I-001 direct unit tests | 11 | 11 | 0 | 0 |
| G1A-001 predecessor regression tests | 7 | 7 | 0 | 0 |
| G1B-003 random consumer regression tests | 9 | 9 | 0 | 0 |
| G1B-005 random consumer regression tests | 7 | 7 | 0 | 0 |
| Total tests | 34 | 34 | 0 | 0 |

Build result:

- Command: `.\.dotnet\dotnet.exe build src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj`
- Warnings: 0
- Errors: 0

## Failure Details

- None.

## Untested Items

- No original `DCGO/Assets/...` files were modified; `DCGO/Assets/Scripts/Script/GameRandom.cs` was read only as the AS-IS algorithm reference.
- Engine trace, log sink, and broad forbidden dependency scan are separate later Goals and were not implemented here.

## Open Risks

- `GameRandomSource` now uses a Headless-local Xoshiro256**/SplitMix64 implementation rather than `System.Random`; existing tests validate determinism and random consumers, but no external serialized random-state compatibility contract exists yet.

## Completion Judgment

COMPLETE - G1I-001 implements the `IRandomSource/GameRandomSource` seeded random contract, removes placeholder TODOs from the scoped random source files, passes direct and regression tests, and builds with 0 warnings and 0 errors.
