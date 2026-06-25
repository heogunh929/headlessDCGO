# G1B-001 Stable ID Entity Registry Unit Test Results

## Execution

- Execution date: 2026-06-24 23:38:36 +09:00
- Goal ID: G1B-001
- Phase: Phase 1
- Scope: player card definition card instance id baseline fixed
- Deliverable: HeadlessPlayerId HeadlessEntityId registry
- Completion gate: stable id test pass
- Status: PASS

## Files Changed Or Created

- Modified: `src/HeadlessDCGO.Engine/Headless/Services/HeadlessEntityId.cs`
- Modified: `src/HeadlessDCGO.Engine/Headless/Services/HeadlessPlayerId.cs`
- Created: `src/HeadlessDCGO.Engine/Headless/Services/HeadlessEntityRegistry.cs`
- Modified: `src/HeadlessDCGO.Engine/Headless/Services/CardRecord.cs`
- Modified: `src/HeadlessDCGO.Engine/Headless/Services/CardInstanceRecord.cs`
- Modified: `src/HeadlessDCGO.Engine/Headless/Services/ICardRepository.cs`
- Modified: `src/HeadlessDCGO.Engine/Headless/Services/ICardInstanceRepository.cs`
- Modified: `src/HeadlessDCGO.Engine/Headless/Services/InMemoryCardRepository.cs`
- Modified: `src/HeadlessDCGO.Engine/Headless/Services/InMemoryCardInstanceRepository.cs`
- Modified: `src/HeadlessDCGO.Engine/Headless/Services/IZoneMover.cs`
- Modified: `src/HeadlessDCGO.Engine/Headless/Services/ZoneMoveRequest.cs`
- Modified: `src/HeadlessDCGO.Engine/Headless/Services/InMemoryZoneMover.cs`
- Modified: `src/HeadlessDCGO.Engine/Headless/DataLoading/CardDatabase.cs`
- Modified: `src/HeadlessDCGO.Engine/Headless/DataLoading/CardAssetJsonLoader.cs`
- Created: `tests/G1B-001.Stable.ID.entity.registry.Tests/G1B-001.Stable.ID.entity.registry.Tests.csproj`
- Created: `tests/G1B-001.Stable.ID.entity.registry.Tests/Program.cs`
- Created: `docs/test-results/goals/G1B-001_stable_ids_unit_test_results.md`

## References Checked

- `docs/goal-specs/G1B-001_stable_id와_entity_registry.md`
- `docs/headless_complete_goal_breakdown.csv`
- `docs/headless_complete_unit_test_plan.md`
- `docs/test-results/goals/G1A-001_runtime_models_unit_test_results.md`
- `src/HeadlessDCGO.Engine/Headless/Services`
- `src/HeadlessDCGO.Engine/Headless/DataLoading/CardAssetJsonLoader.cs`

## Read-Only AS-IS Files Checked

- None. The existing Headless service and data-loading surfaces were sufficient for this stable ID/registry contract, so no original `DCGO/Assets/...` AS-IS file read was required.

## Predecessor Check

G1B-001 is blocked by G1A-001. Before completing this goal, G1A-001 was checked with:

```powershell
.\.dotnet\dotnet.exe run --project tests\G1A-001.RuntimeModels.Tests\G1A-001.RuntimeModels.Tests.csproj
```

Result:

```text
7 test(s) passed.
```

## Test Intent

The G1B-001 test verifies only stable ID and entity registry behavior in this goal:

- G1B-001 CSV contract remains fixed.
- `HeadlessEntityId` and `HeadlessPlayerId` preserve equality, hash uniqueness, parsing, and JSON serialization contracts.
- invalid entity/player id values fail explicitly.
- card definition and card instance records defensively copy metadata.
- `HeadlessEntityRegistry` enforces player, card definition, and card instance uniqueness.
- registry card instances fail when owner player or card definition is missing.
- repositories preserve stable ID lookup and snapshot isolation.
- `InMemoryZoneMover` preserves deterministic single-zone ordering and fails explicit moves from missing source cards.
- stable ID/registry source files no longer contain placeholder TODO contracts.

## Test Command

```powershell
.\.dotnet\dotnet.exe run --project tests\G1B-001.Stable.ID.entity.registry.Tests\G1B-001.Stable.ID.entity.registry.Tests.csproj
```

## Test Counts

- Total: 7
- Passed: 7
- Failed: 0
- Skipped: 0

## Test Output

```text
PASS G1B-001 goal row keeps the stable id registry contract
PASS Headless ids preserve equality serialization and invalid value contracts
PASS Card definition and instance records preserve metadata snapshots
PASS HeadlessEntityRegistry enforces player definition and instance uniqueness
PASS Repositories preserve stable id lookup and immutable snapshots
PASS Zone mover preserves single-zone ordering and missing card failures
PASS Stable id registry source files no longer contain placeholder TODO contracts

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

None.

## Untested Items And Reasons

- Real card effects, full player/card state machines, hidden-information opponent observation policies, Unity scene behavior, Photon, UI, animation, audio, and prefab behavior were not tested because they are outside G1B-001 and later Goal/Phase scope.
- Original `DCGO/Assets/...` files were not tested or modified because this Goal only fixes Headless stable ID and registry contracts.

## Unresolved Risks

- Existing Unity `.meta` changes are visible under `DCGO/Assets`, but this Goal did not modify original `DCGO/Assets/...` files.
- Broader state/zone kernel behavior beyond stable ID registration and deterministic zone mutation remains for later G1B/G1C Goals.

## Completion Decision

COMPLETE. G1B-001 satisfies `stable id test pass` with stable ID value contracts, entity registry uniqueness checks, focused unit tests passing, and this result document recorded.
