# G1E-001 Choice Schema Unit Test Results

## Execution Time And Environment

- Executed at: 2026-06-25 08:41:55 +09:00
- Environment: Windows PowerShell, .NET 8 SDK via `.\.dotnet\dotnet.exe`

## Goal Scope

- Goal ID: G1E-001
- Goal: Choice schema
- Scope: choice request candidate zone type schema contract
- Deliverable: ChoiceRequest ChoiceCandidate ChoiceType ChoiceZone
- Unit test scope: schema validation tests
- Required predecessor: G1A-003
- Completion gate: Choice schema tests pass

## Predecessor Check

- `docs/test-results/goals/G1A-003_action_contract_unit_test_results.md` exists and records `COMPLETE`.
- `tests/G1A-003.ActionContract.Tests` exists and was re-run successfully during this goal.
- G1E-001 proceeded only after the required predecessor was satisfied.

## Modified Or Created Files

- Modified: `src/HeadlessDCGO.Engine/Headless/Choices/ChoiceCandidate.cs`
- Modified: `src/HeadlessDCGO.Engine/Headless/Choices/ChoiceRequest.cs`
- Modified: `src/HeadlessDCGO.Engine/Headless/Choices/ChoiceType.cs`
- Modified: `src/HeadlessDCGO.Engine/Headless/Choices/ChoiceZone.cs`
- Created: `tests/G1E-001.Choice.schema.Tests/G1E-001.Choice.schema.Tests.csproj`
- Created: `tests/G1E-001.Choice.schema.Tests/Program.cs`
- Created: `docs/test-results/goals/G1E-001_choice_schema_unit_test_results.md`

## Read-Only Files Checked

- `docs/goal-specs/G1E-001_choice_schema.md`
- `docs/headless_complete_goal_breakdown.csv`
- `docs/headless_complete_unit_test_plan.md`
- `docs/headless_complete_unit_test_matrix.csv`
- `docs/test-results/goals/G1A-003_action_contract_unit_test_results.md`
- `src/HeadlessDCGO.Engine/Headless/Choices/ChoiceResult.cs`
- `src/HeadlessDCGO.Engine/Headless/Choices/IChoiceProvider.cs`
- `src/HeadlessDCGO.Engine/Headless/Choices/PolicyChoiceProvider.cs`
- `src/HeadlessDCGO.Engine/Headless/Choices/ScriptedChoiceProvider.cs`
- `DCGO/Assets/Scripts/Script/SelectCardEffect.cs`
- `DCGO/Assets/Scripts/Script/SelectPermanentEffect.cs`
- `DCGO/Assets/Scripts/Script/SelectCountEffect.cs`
- `DCGO/Assets/Scripts/Script/SelectHandEffect.cs`
- `DCGO/Assets/Scripts/Script/SelectAttackEffect.cs`
- `DCGO/Assets/Scripts/Script/PlayerSelection/CardSelection.cs`
- `DCGO/Assets/Scripts/Script/PlayerSelection/PermanentSelection.cs`
- `DCGO/Assets/Scripts/Script/PlayerSelection/ValueSelection.cs`

## Test Intent

- Verify the CSV row still defines G1E-001 as the Choice schema goal and keeps the expected scope, deliverable, test scope, predecessor, result document, and completion gate.
- Verify `ChoiceCandidate` preserves candidate id, owner id, zone, label, and selectable flag.
- Verify `ChoiceCandidate` rejects empty ids, null labels, `ChoiceZone.None`, and unknown zone values.
- Verify `ChoiceRequest` preserves type, player, message, min/max count, skip flag, source zone, and immutable candidate snapshots.
- Verify `ChoiceRequest` rejects placeholder or unknown choice types, empty player ids, null messages, invalid min/max values, unknown source zones, null candidate lists, and null candidate entries.
- Verify `ChoiceType` and `ChoiceZone` cover the AS-IS SelectCardEffect, SelectPermanentEffect, SelectCountEffect, SelectHandEffect, and SelectAttackEffect decision categories.
- Verify AS-IS Unity selection files were inspected as read-only input.
- Verify the delivered schema source files have no placeholder TODO contract and no Unity dependency.

## Test Commands

- `.\.dotnet\dotnet.exe run --project tests\G1E-001.Choice.schema.Tests\G1E-001.Choice.schema.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project tests\G1A-003.ActionContract.Tests\G1A-003.ActionContract.Tests.csproj`
- `.\.dotnet\dotnet.exe build src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj`

## Test Counts

| Scope | Total | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|
| G1E-001 Choice schema | 8 | 8 | 0 | 0 |
| G1A-003 predecessor regression | 6 | 6 | 0 | 0 |
| Engine build | 1 | 1 | 0 | 0 |

## Command Results

- G1E-001 command result: `8 test(s) passed.`
- G1A-003 regression command result: `6 test(s) passed.`
- Engine build result: build succeeded with 0 warnings and 0 errors.
- The first G1E-001 test run exposed a public API named-argument compatibility issue for `ChoiceCandidate(IsSelectable: ...)`; it was fixed inside the G1E-001 scope and the test was re-run successfully.
- The first successful G1E-001 test run emitted existing nullable warnings from runtime files outside this goal during compilation, but the explicit engine build completed cleanly afterward with 0 warnings and 0 errors.

## Failure Details And Fixes

- Initial failure: `MetadataActionProcessor` used the existing public named argument `IsSelectable:` and the first schema constructor used `isSelectable`.
- Fix: preserved the existing public constructor argument name `IsSelectable` while adding schema validation and owner support.
- No G1E-001 test failed after that fix.

## Untested Items And Reasons

- `ChoiceResult` select/count/skip validation is not completed here because that is G1E-002 scope.
- `ScriptedChoiceProvider` and `PolicyChoiceProvider` behavior is not completed here because this goal only fixes the schema deliverables.
- Runtime choice suspension/resolution flow is not implemented here because this goal is limited to request/candidate/type/zone schema.
- Card/rule/effect-specific selection porting is not implemented because Phase 1 does not port real `Assets/...` card effects.

## DCGO/Assets Safety

- Original `DCGO/Assets/...` files were read only for AS-IS selection semantics.
- Recent modified-file check under `DCGO/Assets` returned `0`.
- No original `DCGO/Assets/...` file was modified.

## Completion Gate Evidence

- `ChoiceCandidate` now validates candidate id, label, concrete zone, selectable flag, and optional owner.
- `ChoiceRequest` now validates type, player, message, count bounds, source zone, and candidate list shape.
- `ChoiceRequest.Candidates` is an immutable snapshot of the supplied candidate list.
- `ChoiceType` and `ChoiceZone` no longer contain placeholder TODO contracts.
- G1E-001 unit tests passed 8/8.
- Required predecessor regression tests passed 6/6.
- Engine project build succeeded.

## Next Goal Availability

- G1E-002 can proceed from the G1E-001 perspective because the Choice schema completion gate passed.

## Completion Judgment

- COMPLETE
