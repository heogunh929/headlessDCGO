# G2G-002 Block timing 포팅 테스트 결과

## 실행 일시

- 2026-06-25 15:43:36 +09:00

## 수정/생성 파일

- 생성: `src/HeadlessDCGO.Engine/Headless/Runtime/BlockTiming.cs`
- 수정: `src/HeadlessDCGO.Engine/Headless/Runtime/HeadlessAttackState.cs`
- 수정: `src/HeadlessDCGO.Engine/Headless/Runtime/IHeadlessAttackController.cs`
- 수정: `src/HeadlessDCGO.Engine/Headless/Runtime/InMemoryHeadlessAttackController.cs`
- 수정: `src/HeadlessDCGO.Engine/Headless/Runtime/AttackPermanentAction.cs`
- 수정: `src/HeadlessDCGO.Engine/Headless/Choices/ChoiceType.cs`
- 수정: `src/HeadlessDCGO.Engine/Headless/Runtime/HeadlessActionParameterKeys.cs`
- 수정: `src/HeadlessDCGO.Engine/Headless/Runtime/ObservationEncoder.cs`
- 수정: `src/HeadlessDCGO.Engine/Headless/Runtime/MetadataActionProcessor.cs`
- 생성: `tests/G2G-002.Block.timing.Tests/G2G-002.Block.timing.Tests.csproj`
- 생성: `tests/G2G-002.Block.timing.Tests/Program.cs`
- 생성: `docs/test-results/goals/G2G-002_block_timing_unit_test_results.md`

## 읽기 전용으로 확인한 AS-IS 파일

- `DCGO/Assets/Scripts/Script/AttackProcess.cs`
- `DCGO/Assets/Scripts/Script/Permanent.cs`
- `DCGO/Assets/Scripts/Script/SelectPermanentEffect.cs`

## 구현 범위

- G2G-002 범위인 `block timing window와 blocker 선택`만 구현했다.
- `BlockTiming.GetBlockerCandidates`, `RequestBlockChoice`, `ResolveBlockChoice` API를 추가했다.
- pending attack 중 defender의 battle area Digimon 중 blocker 후보만 선택지로 만든다.
- blocker 선택 시 attack state의 `TargetId`, `BlockerId`, `IsBlocked`, `IsDirectAttack`을 갱신한다.
- skip 선택 시 공격 target은 유지한다.
- attacker가 collision을 가진 경우 AS-IS `canNoSelect: !AttackingPermanent.HasCollision` 의미에 맞춰 skip을 금지한다.
- DP 비교, deletion, security check, end attack trigger는 G2G-002 범위 밖이라 구현하지 않았다.
- 원본 `DCGO/Assets/...` 파일은 읽기만 했고 수정하지 않았다.

## 테스트 명령

- `.\.dotnet\dotnet.exe run --project .\tests\G2G-002.Block.timing.Tests\G2G-002.Block.timing.Tests.csproj`
- `.\.dotnet\dotnet.exe build .\src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj`
- `.\.dotnet\dotnet.exe run --project .\tests\G2G-001.Attack.declaration.target.Tests\G2G-001.Attack.declaration.target.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project .\tests\G2E-004.Attack.action.Tests\G2E-004.Attack.action.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project .\tests\G1E-005.Choice.pause.resume.contract.Tests\G1E-005.Choice.pause.resume.contract.Tests.csproj`

## 전체/통과/실패/스킵 수

| 범위 | 전체 | 통과 | 실패 | 스킵 |
|---|---:|---:|---:|---:|
| G2G-002 전용 테스트 | 10 | 10 | 0 | 0 |
| HeadlessDCGO.Engine 빌드 | 1 | 1 | 0 | 0 |
| G2G-001 회귀 테스트 | 10 | 10 | 0 | 0 |
| G2E-004 회귀 테스트 | 10 | 10 | 0 | 0 |
| G1E-005 회귀 테스트 | 10 | 10 | 0 | 0 |
| 합계 | 41 | 41 | 0 | 0 |

## 실패 상세

- 최종 실행 기준 실패 없음.
- 구현 중 1차 빌드에서 `AttackPermanentAction` 메타데이터 보강 위치 오류가 발생했고, 같은 Goal 범위 안에서 `AddAttackState` 경로로 정리했다.
- G2G-002 테스트 1차 실행에서 collision 후보 수 기대가 AS-IS보다 좁아 실패했다. Collision은 여러 상대 Digimon에게 blocker 후보성을 줄 수 있으므로 “후보 존재와 skip 불가” 계약으로 테스트를 수정했고, 재실행 결과 10/10 통과했다.
- G1E-005 회귀 테스트는 처음에 잘못된 프로젝트 경로로 실행해 MSB1009가 발생했다. 실제 경로 `tests/G1E-005.Choice.pause.resume.contract.Tests`로 재실행해 10/10 통과했다.

## 테스트하지 못한 항목과 이유

- block 이후 DP battle resolver, deletion/trash, security check, end-attack trigger는 후속 공격 처리 흐름이며 G2G-002 산출물인 `block timing`과 CSV 단위테스트 범위 `block choice 테스트`를 넘어가므로 이번 Goal에서 테스트하지 않았다.

## 미해결 리스크

- blocker 판정은 현재 Headless metadata(`hasBlocker`, `canBlock`, `cannotBlock`, `canSuspend`, `isSuspended`, `hasCollision`)를 기준으로 한다.
- 실제 카드 효과가 동적으로 부여하는 blocker/collision/면역 판정은 이후 effect/rule 포팅 Goal에서 확장되어야 한다.
- 작업 디렉터리에서 `git status --short`는 `fatal: not a git repository`로 실행되지 않았다.

## 완료 판정

- COMPLETE
- 완료 기준 `block timing 테스트 통과` 충족.
- 다음 Goal 진행 가능.
