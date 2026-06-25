# G2D-003 Suspend reveal state 포팅

## 실행 일시

- 2026-06-25 14:16:06 +09:00

## 실행 환경

- Workspace: `E:\headlessDCGO_new`
- Runtime: `.NET 8.0`

## 수정/생성 파일

- `src/HeadlessDCGO.Engine/Headless/Runtime/CardStateMutationPort.cs`
- `tests/G2D-003.Suspend.reveal.state.Tests/G2D-003.Suspend.reveal.state.Tests.csproj`
- `tests/G2D-003.Suspend.reveal.state.Tests/Program.cs`
- `docs/test-results/goals/G2D-003_card_suspend_reveal_unit_test_results.md`

## 읽기 전용으로 확인한 AS-IS 파일

- `DCGO/Assets/Scripts/Script/CardSource.cs`
- `DCGO/Assets/Scripts/Script/CardController.cs`
- `DCGO/Assets/Scripts/Script/TurnStateMachine.cs`

## 테스트 명령

- `.\.dotnet\dotnet.exe run --project .\tests\G2D-003.Suspend.reveal.state.Tests\G2D-003.Suspend.reveal.state.Tests.csproj`
- `.\.dotnet\dotnet.exe build .\src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj`
- `.\.dotnet\dotnet.exe run --project .\tests\G2D-002.Zone.movement.event.Tests\G2D-002.Zone.movement.event.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project .\tests\G2D-001.Card.identity.binding.Tests\G2D-001.Card.identity.binding.Tests.csproj`

## 전체/통과/실패/스킵 수

| 범위 | 전체 | 통과 | 실패 | 스킵 |
| --- | ---: | ---: | ---: | ---: |
| G2D-003 Suspend reveal state | 10 | 10 | 0 | 0 |
| HeadlessDCGO.Engine build | 1 | 1 | 0 | 0 |
| G2D-002 Zone movement event 회귀 | 10 | 10 | 0 | 0 |
| G2D-001 Card identity binding 회귀 | 10 | 10 | 0 | 0 |

## 실패 상세 및 수정 여부

- 최종 실행 실패 없음.

## 테스트하지 못한 항목과 이유

- Unity 표시 오브젝트 회전, 애니메이션, UI 아이콘, `GManager.OnCardFlippedChanged`/`OnSecurityStackChanged` 콜백은 Headless runtime 범위 밖이므로 테스트하지 않았다.
- 실제 카드 효과 발동 포팅은 이 Goal 범위가 아니므로 `StateChanged` 이벤트와 `EffectContext` payload 연결까지만 검증했다.

## 완료 기준 충족 근거

- `CardStateMutationPort`가 suspend, unsuspend, reveal, hide 요청을 `CardInstanceState` mutation으로 연결한다.
- 변경이 있는 경우 `StateChanged` 이벤트, deterministic trace, `EffectContext`를 생성한다.
- 이미 같은 상태인 요청은 no-op으로 처리해 상태 fingerprint와 이벤트 목록을 보존한다.
- 잘못된 카드 id는 실패 결과를 반환하고 원본 상태를 보존한다.
- 완료 기준 `card state mutation 테스트 통과`를 G2D-003 전용 테스트 10/10 통과로 충족했다.

## 다음 Goal 진행 가능 여부

- 가능. G2D-003은 COMPLETE이며 선행 Goal G2D-002 완료 상태도 확인했다.

## 완료 판정

- COMPLETE
