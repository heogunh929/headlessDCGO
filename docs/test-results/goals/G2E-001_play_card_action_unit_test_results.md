# G2E-001 PlayCardAction 포팅 테스트 결과

## 실행 일시

- 2026-06-25 14:30:45 +09:00

## 수정/생성 파일

- 수정: `src/HeadlessDCGO.Engine/Headless/Runtime/HeadlessActionTypes.cs`
- 수정: `src/HeadlessDCGO.Engine/Headless/Runtime/HeadlessActionFactory.cs`
- 수정: `src/HeadlessDCGO.Engine/Headless/Runtime/HeadlessLegalActionDispatcher.cs`
- 수정: `src/HeadlessDCGO.Engine/Headless/Runtime/MetadataActionProcessor.cs`
- 생성: `src/HeadlessDCGO.Engine/Headless/Runtime/PlayCardAction.cs`
- 생성: `tests/G2E-001.PlayCardAction.Tests/G2E-001.PlayCardAction.Tests.csproj`
- 생성: `tests/G2E-001.PlayCardAction.Tests/Program.cs`
- 생성: `docs/test-results/goals/G2E-001_play_card_action_unit_test_results.md`

## 테스트 명령

- `.\.dotnet\dotnet.exe run --project .\tests\G2E-001.PlayCardAction.Tests\G2E-001.PlayCardAction.Tests.csproj`
- `.\.dotnet\dotnet.exe build .\src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj`
- `.\.dotnet\dotnet.exe run --project .\tests\G2A-006.Legal.action.dispatch.hook.Tests\G2A-006.Legal.action.dispatch.hook.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project .\tests\G2D-002.Zone.movement.event.Tests\G2D-002.Zone.movement.event.Tests.csproj`

## 전체/통과/실패/스킵 수

| 범위 | 전체 | 통과 | 실패 | 스킵 |
| --- | ---: | ---: | ---: | ---: |
| G2E-001 PlayCardAction.Tests | 9 | 9 | 0 | 0 |
| HeadlessDCGO.Engine build | 1 | 1 | 0 | 0 |
| G2A-006 Legal action dispatch hook.Tests | 10 | 10 | 0 | 0 |
| G2D-002 Zone movement event.Tests | 10 | 10 | 0 | 0 |

## 실패 상세

- 없음

## 확인한 계약

- `PlayCardAction`은 AS-IS `MainPhaseAction/PlayCardAction.cs`의 `CardIndex`, 대상 프레임 전달과 `TurnStateMachine.SetPlayCard(...)` 호출 의미를 Headless action payload로 고정한다.
- Main phase legal action 조회는 손패에 있고 카드 DB에 play cost가 있으며 현재 memory controller가 지불 가능한 카드만 `PlayCard`로 노출한다.
- `PlayCard` apply는 같은 검증 조건을 다시 사용한 뒤 memory cost를 지불하고 `Hand`에서 `BattleArea`로 카드를 이동한다.
- 잘못된 cost, 손패 밖 카드, 누락된 카드 definition은 illegal result를 반환하고 memory/zone 상태를 변경하지 않는다.
- 이번 Goal 범위를 벗어나는 option activation, attack, pass, digivolve 구현은 하지 않았다.

## 미해결 리스크

- 실제 카드별 배치 프레임, option activation, digivolve/jogress/burst/app fusion 세부 처리는 후속 Goal 범위로 남아 있다.
- 현재 `PlayCardAction`은 카드 DB의 `PlayCost`와 서비스 기반 zone/memory 상태를 사용한다. AS-IS의 모든 비용 변경/감소 효과는 아직 반영하지 않았다.
- `git status/diff` 확인은 현재 작업 폴더가 `.git` 저장소로 인식되지 않아 수행하지 못했다.

## 완료 판정

- COMPLETE
