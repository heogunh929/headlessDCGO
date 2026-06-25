# G2E-003 Option activate action 포팅 테스트 결과

## 실행 일시

- 2026-06-25 14:48:49 +09:00

## 수정/생성 파일

- 수정: `src/HeadlessDCGO.Engine/Headless/Runtime/HeadlessActionTypes.cs`
- 수정: `src/HeadlessDCGO.Engine/Headless/Runtime/HeadlessActionParameterKeys.cs`
- 수정: `src/HeadlessDCGO.Engine/Headless/Runtime/HeadlessActionFactory.cs`
- 수정: `src/HeadlessDCGO.Engine/Headless/Runtime/HeadlessLegalActionDispatcher.cs`
- 수정: `src/HeadlessDCGO.Engine/Headless/Runtime/MetadataActionProcessor.cs`
- 수정: `src/HeadlessDCGO.Engine/Headless/Runtime/PlayCardAction.cs`
- 생성: `src/HeadlessDCGO.Engine/Headless/Runtime/OptionActivateAction.cs`
- 생성: `tests/G2E-003.Option.activate.action.Tests/G2E-003.Option.activate.action.Tests.csproj`
- 생성: `tests/G2E-003.Option.activate.action.Tests/Program.cs`
- 생성: `docs/test-results/goals/G2E-003_option_activate_action_unit_test_results.md`

## 테스트 명령

- `.\.dotnet\dotnet.exe run --project .\tests\G2E-003.Option.activate.action.Tests\G2E-003.Option.activate.action.Tests.csproj`
- `.\.dotnet\dotnet.exe build .\src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj`
- `.\.dotnet\dotnet.exe run --project .\tests\G2E-001.PlayCardAction.Tests\G2E-001.PlayCardAction.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project .\tests\G2E-002.Digivolve.action.Tests\G2E-002.Digivolve.action.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project .\tests\G2A-006.Legal.action.dispatch.hook.Tests\G2A-006.Legal.action.dispatch.hook.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project .\tests\G2D-002.Zone.movement.event.Tests\G2D-002.Zone.movement.event.Tests.csproj`

## 전체/통과/실패/스킵 수

| 범위 | 전체 | 통과 | 실패 | 스킵 |
| --- | ---: | ---: | ---: | ---: |
| G2E-003 Option.activate.action.Tests | 10 | 10 | 0 | 0 |
| HeadlessDCGO.Engine build | 1 | 1 | 0 | 0 |
| G2E-001 PlayCardAction.Tests | 9 | 9 | 0 | 0 |
| G2E-002 Digivolve.action.Tests | 9 | 9 | 0 | 0 |
| G2A-006 Legal.action.dispatch.hook.Tests | 10 | 10 | 0 | 0 |
| G2D-002 Zone.movement.event.Tests | 10 | 10 | 0 | 0 |

## 실패 상세

- 없음

## 확인한 계약

- 선행 Goal `G2E-001` 결과 문서가 `COMPLETE`임을 확인했다.
- AS-IS `ActivateCardAction`의 `CardIndex`, `SkillIndex`, `TurnStateMachine.SetActCardSkill(...)` 흐름과 `UseCardEffect.CanUse(null)`, `CanNotPlayThisOption` 조건을 읽기 전용으로 확인했다.
- `ActivateOption` legal action은 Main phase에서 사용 가능한 Option 카드에만 노출된다.
- `ActivateOption` apply는 동일 검증 조건으로 memory cost를 지불하고, 카드를 `Hand`에서 `Trash`로 이동시키며, option effect request를 `EffectScheduler`에 enqueue한다.
- match loop 경유 처리에서는 enqueue된 option effect request가 기본 resolver를 통해 resolve되고 observation effect counter에 반영된다.
- non-option 카드, 잘못된 cost, option lock, memory 부족은 illegal result를 반환하고 memory/zone/effect 상태를 변경하지 않는다.
- Option 카드는 `PlayCard` legal action으로 중복 노출되지 않도록 분리했다.

## 미해결 리스크

- 실제 카드별 option effect 본문 실행은 이번 Goal 범위가 아니며, 현재 구현은 effect request enqueue 계약까지만 고정한다.
- AS-IS의 세부 타깃 선택, 발동 횟수 제한, 카드별 `ActivateICardEffect` 내부 처리 전체는 후속 카드/효과 포팅 Goal에서 확장해야 한다.
- 현재 작업 폴더는 `.git` 저장소로 인식되지 않아 `git status/diff` 기반 변경 추적은 수행하지 못했다.

## 완료 판정

- COMPLETE
