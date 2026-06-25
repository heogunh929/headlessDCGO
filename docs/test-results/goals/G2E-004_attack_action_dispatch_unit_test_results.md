# G2E-004 Attack action 연결 포팅 테스트 결과

## 실행 일시 및 환경

- 실행 일시: 2026-06-25 14:56:41 +09:00
- Workspace: `E:\headlessDCGO_new`
- Runtime: `.NET 8.0`

## 수정/생성 파일

- 수정: `src/HeadlessDCGO.Engine/Headless/Runtime/HeadlessActionFactory.cs`
- 수정: `src/HeadlessDCGO.Engine/Headless/Runtime/HeadlessActionPayloads.cs`
- 수정: `src/HeadlessDCGO.Engine/Headless/Runtime/HeadlessAttackState.cs`
- 수정: `src/HeadlessDCGO.Engine/Headless/Runtime/HeadlessLegalActionDispatcher.cs`
- 수정: `src/HeadlessDCGO.Engine/Headless/Runtime/MetadataActionProcessor.cs`
- 생성: `src/HeadlessDCGO.Engine/Headless/Runtime/AttackPermanentAction.cs`
- 생성: `tests/G2E-004.Attack.action.Tests/G2E-004.Attack.action.Tests.csproj`
- 생성: `tests/G2E-004.Attack.action.Tests/Program.cs`
- 생성: `docs/test-results/goals/G2E-004_attack_action_dispatch_unit_test_results.md`

## 읽기 전용으로 확인한 AS-IS 파일

- `DCGO/Assets/Scripts/Script/MainPhaseAction/AttackPermanentAction.cs`
- `DCGO/Assets/Scripts/Script/TurnStateMachine.cs`
- `DCGO/Assets/Scripts/Script/AttackProcess.cs`
- `DCGO/Assets/Scripts/Script/Permanent.cs`
- `docs/goal-specs/G2E-004_attack_action_연결_포팅.md`
- `docs/headless_complete_goal_breakdown.csv`
- `docs/test-results/goals/G2A-006_legal_action_dispatch_unit_test_results.md`

## 테스트 명령

- `.\.dotnet\dotnet.exe run --project .\tests\G2E-004.Attack.action.Tests\G2E-004.Attack.action.Tests.csproj`
- `.\.dotnet\dotnet.exe build .\src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj`
- `.\.dotnet\dotnet.exe run --project .\tests\G2A-006.Legal.action.dispatch.hook.Tests\G2A-006.Legal.action.dispatch.hook.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project .\tests\G2E-001.PlayCardAction.Tests\G2E-001.PlayCardAction.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project .\tests\G2E-002.Digivolve.action.Tests\G2E-002.Digivolve.action.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project .\tests\G2E-003.Option.activate.action.Tests\G2E-003.Option.activate.action.Tests.csproj`

## 전체/통과/실패/스킵 수

| 범위 | 전체 | 통과 | 실패 | 스킵 |
| --- | ---: | ---: | ---: | ---: |
| G2E-004 Attack.action.Tests | 10 | 10 | 0 | 0 |
| HeadlessDCGO.Engine build | 1 | 1 | 0 | 0 |
| G2A-006 Legal.action.dispatch.hook.Tests | 10 | 10 | 0 | 0 |
| G2E-001 PlayCardAction.Tests | 9 | 9 | 0 | 0 |
| G2E-002 Digivolve.action.Tests | 9 | 9 | 0 | 0 |
| G2E-003 Option.activate.action.Tests | 10 | 10 | 0 | 0 |

## 실패 상세 및 수정 여부

- 최종 실행 실패 없음.
- 중간에 테스트 헬퍼의 C# static local function 캡처 오류가 있었고, 테스트 헬퍼를 non-static으로 바꿔 수정했다.

## 완료 기준 충족 근거

- 선행 Goal `G2A-006` 결과 문서가 `COMPLETE`임을 확인했다.
- AS-IS `AttackPermanentAction`의 `PermanentIndex`, `AttackTargetPermanentIndex`, `TurnStateMachine.SetAttackingPermaent(...)` 흐름을 읽기 전용으로 확인했다.
- AS-IS `TurnStateMachine`에서 `AttackPermanentAction`이 queue되고 `AttackProcess.Attack(...)`으로 이어지는 흐름을 확인했다.
- AS-IS `AttackProcess`의 `AttackCount++`와 공격자 suspend 흐름, `Permanent.CanAttack`/`CanAttackTargetDigimon` 조건을 Headless 기준 attack intent 계약으로 분리했다.
- `AttackPermanentAction` Headless 포트를 생성해 Main phase legal action dispatch에서 직접 공격과 대상 공격 intent를 노출한다.
- `DeclareAttack` action id가 직접 공격과 대상 공격을 구분하도록 target key를 포함했다.
- `DeclareAttack` 처리 시 같은 검증 조건을 재사용하고, 성공 시 `AttackController.DeclareAttack(...)`로 pending attack 상태를 만들며 공격자 `isSuspended` metadata를 true로 갱신한다.
- suspended attacker, unsuspended target, entered-this-turn without rush는 illegal result를 반환하고 attack/card 상태를 변경하지 않는다.
- pending attack 상태에서는 추가 attack legal action이 노출되지 않는다.

## 테스트하지 못한 항목과 이유

- 실제 battle/security check, blocker 선택, redirect, battle DP 비교, security trash/승패 처리는 이번 Goal 범위가 아니므로 실행하지 않았다.
- 카드별 `ICanAttackTargetDefendingPermanentEffect`, rush 외 세부 키워드, 효과 기반 공격 가능 조건은 후속 카드/효과 포팅 범위로 남겼다.
- Unity UI, target arrow, animation, Photon RPC는 Headless runtime 산출물이 아니므로 테스트하지 않았다.
- 현재 작업 폴더는 `.git` 저장소로 인식되지 않아 `git status/diff` 기반 변경 추적은 수행하지 못했다.

## 미해결 리스크

- Headless attack legality는 현재 카드 인스턴스/카드 정의 metadata의 `isSuspended`, `canAttack`, `cannotAttack`, `canSuspend`, `enteredThisTurn`, `hasRush`, `canAttackPlayer`, `cannotAttackPlayer`, `canAttackUnsuspendedDigimon` 키를 사용한다. 실제 카드 효과 전체가 포팅되면 이 metadata 공급 경로를 효과/상태 시스템과 연결해야 한다.
- `ResolveAttack`와 전투 결과 적용은 기존 기초 action으로 남아 있으며, 이번 Goal에서는 attack intent dispatch와 declare 연결까지만 완료했다.

## 다음 Goal 진행 가능 여부

- 가능. G2E-004 완료 기준 `attack action dispatch 테스트 통과`를 충족했다.

## 완료 판정

- COMPLETE
