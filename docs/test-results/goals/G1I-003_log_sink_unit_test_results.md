# G1I-003 Log sink

## Execution

- Goal ID: G1I-003
- Executed at: 2026-06-25 10:31:44 +09:00
- Environment: Windows PowerShell, .NET SDK via `.\.dotnet\dotnet.exe`
- Completion gate: Log sink test pass

## Changed Files

- Modified `src/HeadlessDCGO.Engine/Headless/Services/ILogSink.cs`
- Modified `src/HeadlessDCGO.Engine/Headless/Services/NullLogSink.cs`
- Created `src/HeadlessDCGO.Engine/Headless/Services/InMemoryLogSink.cs`
- Created `tests/G1I-003.Log.sink.Tests/G1I-003.Log.sink.Tests.csproj`
- Created `tests/G1I-003.Log.sink.Tests/Program.cs`
- Created `docs/test-results/goals/G1I-003_log_sink_unit_test_results.md`

## Read-Only References

- `docs/goal-specs/G1I-003_log_sink.md`
- `docs/headless_complete_goal_breakdown.csv`
- `docs/headless_complete_goal_breakdown_ko.csv`
- `docs/headless_complete_goal_breakdown_detailed_ko.csv`
- `docs/test-results/goals/G1I-002_engine_trace_unit_test_results.md`
- `DCGO/Assets/Scripts/Script/PlayLog.cs`
- `DCGO/Assets/Scripts/Script/GameRandom.cs`

## Test Intent

The G1I-003 tests verify the `ILogSink/NullLogSink/InMemoryLogSink` contract: `Info`, `Warn`, and `Error` are available through the interface; in-memory logging records ordered entries with levels and exception details; snapshots are isolated; `Clear` resets sequence numbering; and `NullLogSink` accepts calls without observable state or Unity dependencies.

## Test Commands

```powershell
.\.dotnet\dotnet.exe run --project tests\G1I-003.Log.sink.Tests\G1I-003.Log.sink.Tests.csproj
.\.dotnet\dotnet.exe run --project tests\G1I-002.EngineTrace.Tests\G1I-002.EngineTrace.Tests.csproj
.\.dotnet\dotnet.exe build src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj
```

## Results

| Scope | Total | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|
| G1I-003 direct unit tests | 11 | 11 | 0 | 0 |
| G1I-002 predecessor regression tests | 11 | 11 | 0 | 0 |
| Total tests | 22 | 22 | 0 | 0 |

Build result:

- Command: `.\.dotnet\dotnet.exe build src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj`
- Warnings: 0
- Errors: 0

## Failure Details

- None.

## Untested Items

- No original `DCGO/Assets/...` files were modified; Unity log/random files were read only as AS-IS references.
- Persistent log export, trace-log bridging, and broad forbidden dependency scanning are outside this Goal.

## Open Risks

- `InMemoryLogSink` stores exception type and message only. Stack traces and structured exception metadata can be added by a later diagnostics Goal if needed.

## Completion Judgment

COMPLETE - G1I-003 implements the `ILogSink/NullLogSink/InMemoryLogSink` contract, passes direct and predecessor regression tests, and builds with 0 warnings and 0 errors.
