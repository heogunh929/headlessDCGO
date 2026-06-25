# G1G-002 Action Queue Replay Metadata Unit Test Results

## 실행 일시

- 2026-06-25 09:46:02 +09:00

## Goal 범위

- Goal ID: G1G-002
- 목표: Action queue replay metadata
- 작업 범위: action queue와 replay metadata 확정
- 산출물: HeadlessActionQueue ReplayActionRecord
- 완료 기준: action queue 테스트 통과

## 선행 Goal 확인

- G1G-001: `docs/test-results/goals/G1G-001_player_session_model_unit_test_results.md` COMPLETE 확인
- G1A-003: `docs/test-results/goals/G1A-003_action_contract_unit_test_results.md` COMPLETE 확인

## 수정/생성 파일

- 수정: `src/HeadlessDCGO.Engine/Headless/Runtime/HeadlessActionQueue.cs`
- 생성: `tests/G1G-002.Action.queue.replay.metadata.Tests/G1G-002.Action.queue.replay.metadata.Tests.csproj`
- 생성: `tests/G1G-002.Action.queue.replay.metadata.Tests/Program.cs`
- 생성: `docs/test-results/goals/G1G-002_action_queue_replay_unit_test_results.md`

## 읽기 전용 참조 파일

- `docs/goal-specs/G1G-002_action_queue_replay_metadata.md`
- `docs/headless_complete_goal_breakdown.csv`
- `docs/headless_complete_goal_breakdown_ko.csv`
- `docs/headless_complete_unit_test_plan.md`
- `docs/headless_complete_architecture_design.md`
- `docs/headless_complete_porting_sequence.md`
- `DCGO/Assets/Scripts/Script/GManager.cs`
- `DCGO/Assets/Scripts/Script/TurnStateMachine.cs`
- `DCGO/Assets/Scripts/Script/Player.cs`

## 구현 요약

- `HeadlessActionQueue`가 기존 `LegalAction` FIFO 계약을 유지하면서 `ReplayActionRecord`를 함께 보관하도록 확장했다.
- `ReplayActionRecord`에 순서 번호, 세션 ID, 액션 payload, replay metadata를 고정했다.
- replay record peek/dequeue/snapshot API와 deterministic JSON serialize/deserialize 계약을 추가했다.
- `Clear()`가 큐 내용과 replay sequence를 함께 초기화하도록 고정했다.

## 테스트 명령

- `.\.dotnet\dotnet.exe run --project tests\G1G-002.Action.queue.replay.metadata.Tests\G1G-002.Action.queue.replay.metadata.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project tests\G1G-001.Player.session.model.Tests\G1G-001.Player.session.model.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project tests\G1A-003.ActionContract.Tests\G1A-003.ActionContract.Tests.csproj`
- `.\.dotnet\dotnet.exe build src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj`

## 테스트 결과

| 대상 | 전체 | 통과 | 실패 | 스킵 |
| --- | ---: | ---: | ---: | ---: |
| G1G-002 Action queue replay metadata | 11 | 11 | 0 | 0 |
| G1G-001 Player session model 회귀 | 11 | 11 | 0 | 0 |
| G1A-003 Action contract 회귀 | 6 | 6 | 0 | 0 |
| HeadlessDCGO.Engine build | 1 | 1 | 0 | 0 |

## 실패 상세

- 없음

## 미해결 리스크

- 이번 Goal은 action queue replay metadata 계약 고정에 한정했다.
- 실제 룰/카드 효과 포팅 및 다음 Phase 작업은 수행하지 않았다.
- 원본 `DCGO/Assets/...` 파일은 읽기 전용 참조만 했고 수정하지 않았다.

## 완료 판정

- COMPLETE
