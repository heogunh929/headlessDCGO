# G2C-002 Memory security deck loss check 포팅 테스트 결과

## 실행 일시

- 2026-06-25 13:57:08 +09:00
- 실행 환경: Windows PowerShell, `.NET SDK` via `.\.dotnet\dotnet.exe`

## 수정/생성 파일

- 생성: `src/HeadlessDCGO.Engine/Headless/State/PlayerRuleAdapter.cs`
- 생성: `tests/G2C-002.Memory.security.deck.loss.check.Tests/G2C-002.Memory.security.deck.loss.check.Tests.csproj`
- 생성: `tests/G2C-002.Memory.security.deck.loss.check.Tests/Program.cs`
- 생성: `docs/test-results/goals/G2C-002_player_terminal_checks_unit_test_results.md`

## 읽기 전용 AS-IS 확인 파일

- `DCGO/Assets/Scripts/Script/Player.cs`
- `DCGO/Assets/Scripts/Script/AttackProcess.cs`
- `DCGO/Assets/Scripts/Script/TurnStateMachine.cs`
- `DCGO/Assets/Scripts/Script/AutoProcessing.cs`
- `docs/goal-specs/G2C-002_memory_security_deck_loss_check_포팅.md`
- `docs/headless_complete_goal_breakdown.csv`
- `docs/headless_complete_goal_breakdown_ko.csv`
- `docs/test-results/goals/G2C-001_player_zone_ownership_unit_test_results.md`

## 구현 요약

- `PlayerRuleAdapter`를 추가해 `PlayerZoneAdapter`와 `GameContextStateSnapshot` 기반의 player rule 판단 API를 고정했다.
- 구현한 판단: player-relative memory cost/expected memory, security add/reduce 가능 여부, draw deck loss, empty-security direct attack loss, explicit lose flag.
- `PlayerTerminalCheck`와 `PlayerTerminalReason` 모델을 추가해 winner/loser/reason/message와 metadata 변환을 제공한다.
- 기존 runtime terminal controller는 변경하지 않고, G2C-002 범위의 순수 rule adapter만 추가했다.

## 테스트 명령 및 결과

| 명령 | 전체 | 통과 | 실패 | 스킵 |
|---|---:|---:|---:|---:|
| `.\.dotnet\dotnet.exe run --project .\tests\G2C-002.Memory.security.deck.loss.check.Tests\G2C-002.Memory.security.deck.loss.check.Tests.csproj` | 10 | 10 | 0 | 0 |
| `.\.dotnet\dotnet.exe build .\src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj` | 1 | 1 | 0 | 0 |
| `.\.dotnet\dotnet.exe run --project .\tests\G2C-001.Player.zone.ownership.Tests\G2C-001.Player.zone.ownership.Tests.csproj` | 9 | 9 | 0 | 0 |
| `.\.dotnet\dotnet.exe run --project .\tests\G2B-001.GameContext.state.accessor.Tests\G2B-001.GameContext.state.accessor.Tests.csproj` | 9 | 9 | 0 | 0 |
| `.\.dotnet\dotnet.exe run --project .\tests\G2A-004.Main.phase.memory.pass.Tests\G2A-004.Main.phase.memory.pass.Tests.csproj` | 11 | 11 | 0 | 0 |

## 실패 상세 및 수정

- 최초 G2C-002 테스트 실행은 `Program.cs`의 deterministic flatten 문자열 보간식에 괄호가 하나 빠져 컴파일 실패했다.
- 수정: `EvaluateSecurityAttack(...)` 결과를 `Format(...)`에 넘기는 괄호를 보정했다.
- 수정 후 전용 테스트 10/10 통과.
- 전용 테스트 실행 중 기존 런타임 파일의 nullable warning이 출력되었으나, 별도 엔진 build는 경고 0개/오류 0개로 통과했다.

## 테스트하지 못한 항목과 이유

- 실제 공격 처리, draw 실행, security card 이동, match terminal 반영은 후속 flow/rule 포팅 범위라 이번 Goal에서는 순수 판단 API만 테스트했다.
- 카드 효과로 인한 security 증감 금지/허용 변경은 실제 효과 포팅 이전이라 테스트하지 않았다.
- `DCGO/Assets/...` 원본 파일은 읽기 전용으로만 확인했고 수정하지 않았다.

## 미해결 리스크

- 메모리 방향은 현재 Headless player id 정렬 기준 첫 player를 AS-IS `PlayerID == 0` 방향, 나머지를 반대 방향으로 매핑한다. 다인전 또는 seat 모델이 별도로 생기면 명시적인 memory side 모델이 필요하다.
- `PlayerTerminalCheck`는 terminal 판단 결과를 모델링하지만 아직 `DcgoMatch.GetResult()`와 자동 연결하지 않는다. match terminal 적용은 별도 Goal에서 연결해야 한다.

## 완료 기준 충족 근거

- 선행 Goal `G2C-001` 결과 문서에서 COMPLETE를 확인했다.
- 산출물 `Player rule adapter`를 public API `PlayerRuleAdapter`, `PlayerTerminalCheck`, `PlayerTerminalReason`으로 구현했다.
- 단위테스트 범위 `memory security deck loss 테스트`를 다음 케이스로 검증했다: player-relative memory, security looking 중 security 증감 금지, deck loss, security loss, player lose flag, invalid input 실패, deterministic terminal check.
- 전용 테스트와 관련 회귀 테스트가 모두 실패 없이 통과했다.

## 다음 Goal 진행 가능 여부

- 가능. G2C-002 완료 기준 `player terminal check 테스트 통과`를 충족했다.

## 완료 판정

- COMPLETE
