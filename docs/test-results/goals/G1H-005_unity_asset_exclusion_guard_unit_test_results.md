# G1H-005 Unity asset exclusion guard

## Execution

- Goal ID: G1H-005
- Executed at: 2026-06-25 10:18:02 +09:00
- Environment: Windows PowerShell, .NET SDK via `.\.dotnet\dotnet.exe`
- Completion gate: Unity asset guard test pass

## Changed Files

- Created `tests/G1H-005.Unity.asset.exclusion.guard.Tests/G1H-005.Unity.asset.exclusion.guard.Tests.csproj`
- Created `tests/G1H-005.Unity.asset.exclusion.guard.Tests/Program.cs`
- Created `docs/test-results/goals/G1H-005_unity_asset_exclusion_guard_unit_test_results.md`

## Read-Only References

- `docs/goal-specs/G1H-005_unity_asset_exclusion_guard.md`
- `docs/headless_complete_goal_breakdown.csv`
- `docs/headless_complete_goal_breakdown_ko.csv`
- `docs/headless_complete_goal_breakdown_detailed_ko.csv`
- `docs/test-results/goals/G1H-002_card_json_loader_unit_test_results.md`
- `docs/test-results/goals/G1H-003_deck_list_loader_unit_test_results.md`
- `src/HeadlessDCGO.Engine/Headless/DataLoading/BanlistLoader.cs`
- `src/HeadlessDCGO.Engine/Headless/DataLoading/CardAssetJsonLoader.cs`
- `src/HeadlessDCGO.Engine/Headless/DataLoading/DeckListLoader.cs`
- `src/HeadlessDCGO.Engine/Headless/Services/ICardRepository.cs`
- `src/HeadlessDCGO.Engine/Headless/Services/CardRecord.cs`
- `DCGO/Assets/CardBaseEntity`
- `DCGO/Assets/Scripts/Script/DeckData.cs`
- `DCGO/Assets/Scripts/Script/DeckCodeUtility.cs`

## Test Intent

G1H-005 fixes the asset exclusion scan contract. The tests verify that gameplay data loading and repository contracts do not expose Unity runtime or visual asset dependencies such as image, prefab, audio, or animation types. Visual asset paths in converted card JSON are accepted only as metadata and are not promoted into public gameplay fields.

## Test Commands

```powershell
.\.dotnet\dotnet.exe run --project tests\G1H-005.Unity.asset.exclusion.guard.Tests\G1H-005.Unity.asset.exclusion.guard.Tests.csproj
.\.dotnet\dotnet.exe run --project tests\G1H-002.Card.JSON.loader.Tests\G1H-002.Card.JSON.loader.Tests.csproj
.\.dotnet\dotnet.exe run --project tests\G1H-003.Deck.list.loader.Tests\G1H-003.Deck.list.loader.Tests.csproj
.\.dotnet\dotnet.exe build src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj
```

## Results

| Scope | Total | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|
| G1H-005 direct unit tests | 10 | 10 | 0 | 0 |
| G1H-002 regression tests | 10 | 10 | 0 | 0 |
| G1H-003 regression tests | 10 | 10 | 0 | 0 |
| Total tests | 30 | 30 | 0 | 0 |

Build result:

- Command: `.\.dotnet\dotnet.exe build src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj`
- Warnings: 0
- Errors: 0

## Failure Details

- None.

## Untested Items

- No write attempt was made under `DCGO/Assets/...`; those paths were read-only references for AS-IS confirmation.
- Actual card effect or Unity asset porting remains outside this Goal and was not tested.

## Open Risks

- Card JSON may retain visual path strings in `CardRecord.Metadata` for traceability. G1H-005 only guarantees that those paths are not exposed as public gameplay fields or required by loader/repository runtime contracts.
- Broader forbidden dependency scanning across all Phase 1 source is reserved for the later dependency scan Goal, not this Goal.

## Completion Judgment

COMPLETE - G1H-005 asset exclusion scan is implemented as a unit test guard, predecessor Goals G1H-002 and G1H-003 are confirmed COMPLETE, all direct and regression tests pass, and the engine builds with 0 warnings and 0 errors.
