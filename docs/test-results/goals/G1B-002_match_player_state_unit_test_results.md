# G1B-002 MatchState PlayerState Unit Test Results

## Execution

- Execution date: 2026-06-24 23:47:28 +09:00
- Goal ID: G1B-002
- Phase: Phase 1
- Scope: both player and global state model fixed
- Deliverable: MatchState PlayerState
- Completion gate: state model test pass
- Status: PASS

## Files Changed Or Created

- Created: `src/HeadlessDCGO.Engine/Headless/State/CardInstanceState.cs`
- Created: `src/HeadlessDCGO.Engine/Headless/State/PlayerState.cs`
- Created: `src/HeadlessDCGO.Engine/Headless/State/MatchState.cs`
- Created: `tests/G1B-002.MatchState.PlayerState.Tests/G1B-002.MatchState.PlayerState.Tests.csproj`
- Created: `tests/G1B-002.MatchState.PlayerState.Tests/Program.cs`
- Created: `docs/test-results/goals/G1B-002_match_player_state_unit_test_results.md`

## References Checked

- `docs/goal-specs/G1B-002_matchstate_playerstate.md`
- `docs/headless_complete_goal_breakdown.csv`
- `docs/headless_complete_unit_test_plan.md`
- `docs/test-results/goals/G1B-001_stable_ids_unit_test_results.md`
- `src/HeadlessDCGO.Engine/Headless/Services`
- `src/HeadlessDCGO.Engine/Headless/Runtime`

## Read-Only AS-IS Files Checked

- None. Existing Headless service/runtime state surfaces were sufficient to define the G1B-002 data models, so no original `DCGO/Assets/...` AS-IS file read was required.

## Predecessor Check

G1B-002 is blocked by G1B-001. Before completing this goal, G1B-001 was checked with:

```powershell
.\.dotnet\dotnet.exe run --project tests\G1B-001.Stable.ID.entity.registry.Tests\G1B-001.Stable.ID.entity.registry.Tests.csproj
```

Result:

```text
7 test(s) passed.
```

## Test Intent

The G1B-002 test verifies only MatchState/PlayerState behavior in this goal:

- G1B-002 CSV contract remains fixed.
- `MatchState.CreateInitial` creates deterministic initial player snapshots.
- `PlayerState` preserves memory, flags, zone order, and immutable zone snapshots.
- owner views expose hidden zone identities while opponent views preserve counts and hide hidden card identities.
- `MatchState.MoveCard` removes from the previous zone, appends to the destination zone, increments state version, and records a `CardMoved` event.
- repeated equivalent move sequences produce identical fingerprints.
- invalid player/card owner state and duplicate player state fail explicitly.
- G1B-002 state model files no longer contain placeholder TODO contracts.

## Test Command

```powershell
.\.dotnet\dotnet.exe run --project tests\G1B-002.MatchState.PlayerState.Tests\G1B-002.MatchState.PlayerState.Tests.csproj
```

## Test Counts

- Total: 7
- Passed: 7
- Failed: 0
- Skipped: 0

## Test Output

```text
PASS G1B-002 goal row keeps the match player state contract
PASS MatchState creates deterministic initial two-player snapshots
PASS PlayerState preserves zone order memory flags and immutable snapshots
PASS Opponent view hides hidden zone card identities while preserving counts
PASS MatchState move records card moved event and deterministic fingerprint
PASS MatchState rejects invalid owner missing card and duplicate player state
PASS Match player state source files no longer contain placeholder TODO contracts

7 test(s) passed.
```

## Build Check

```powershell
.\.dotnet\dotnet.exe build src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj
```

Result:

```text
Build succeeded with 0 warnings and 0 errors.
```

## Failure Details

None in the final run.

During final evidence gathering, a parallel check caused transient .NET/MSBuild `OutOfMemoryException` failures. A leftover `dotnet` process was stopped, then the predecessor test, G1B-002 test, and engine build were rerun sequentially and all passed.

## Untested Items And Reasons

- Real card effects, full turn/phase rules, complete hidden-information policies, Unity scene behavior, Photon, UI, animation, audio, and prefab behavior were not tested because they are outside G1B-002 and later Goal/Phase scope.
- Original `DCGO/Assets/...` files were not tested or modified because this Goal only fixes Headless state model contracts.

## Unresolved Risks

- Existing Unity `.meta` changes are visible under `DCGO/Assets`, but this Goal did not modify original `DCGO/Assets/...` files.
- The new MatchState/PlayerState models are standalone data models; integration into `EngineContext`/runtime mutation flow is intentionally left to later State/Zone Goals.

## Completion Decision

COMPLETE. G1B-002 satisfies `state model test pass` with MatchState, PlayerState, and CardInstanceState implemented, focused unit tests passing, and this result document recorded.
