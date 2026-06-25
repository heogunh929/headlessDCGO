# G2A-006 Legal action dispatch hook 포팅

## 실행 일시

- 2026-06-25 13:35:03 +09:00

## Goal 범위

- Goal ID: G2A-006
- 목표: Legal action dispatch hook 포팅
- 작업 범위: phase별 legal action 조회 연결
- 완료 기준: legal dispatch 테스트 통과
- 선행 Goal: G2A-005 결과 문서 COMPLETE 확인

## 수정/생성 파일

- 수정: `src/HeadlessDCGO.Engine/Headless/Runtime/HeadlessGameLoop.cs`
- 생성: `src/HeadlessDCGO.Engine/Headless/Runtime/HeadlessLegalActionDispatcher.cs`
- 생성: `tests/G2A-006.Legal.action.dispatch.hook.Tests/G2A-006.Legal.action.dispatch.hook.Tests.csproj`
- 생성: `tests/G2A-006.Legal.action.dispatch.hook.Tests/Program.cs`
- 생성: `docs/test-results/goals/G2A-006_legal_action_dispatch_unit_test_results.md`

## 읽기 전용 AS-IS 참조 파일

- `docs/goal-specs/G2A-006_legal_action_dispatch_hook_포팅.md`
- `docs/headless_complete_goal_breakdown.csv`
- `docs/headless_complete_goal_breakdown_ko.csv`
- `docs/test-results/goals/G2A-005_end_turn_cleanup_unit_test_results.md`
- `DCGO/Assets/Scripts/Script/TurnStateMachine.cs`
- `DCGO/Assets/Scripts/Script/Player.cs`

## 구현 요약

- `HeadlessLegalActionDispatcher`를 추가해 현재 phase와 turn player 기준으로 기본 legal action을 반환한다.
- `Setup`, `Active`, `Unsuspend`, `Draw`, `Breeding` phase에서는 현재 턴 플레이어에게 `AdvancePhase`를 노출한다.
- `Main` phase에서는 현재 턴 플레이어에게 `Pass`를 노출한다.
- `MemoryPass`와 `End` phase에서는 현재 턴 플레이어에게 `EndTurn`을 노출한다.
- non-turn player, terminal state, pending choice, pending effect, setup 없는 빈 match에서는 phase dispatch action을 반환하지 않는다.
- 기존 `IRuleQueryService`에 수동으로 등록한 seeded legal action은 유지하고, dispatcher 결과와 병합한다.
- 카드 플레이/공격의 실제 합법성 계산은 G2E-001/G2E-004 등 후속 Goal 범위라 구현하지 않았다.

## 테스트 명령 및 결과

### G2A-006 단위테스트

- 명령: `.\.dotnet\dotnet.exe run --project .\tests\G2A-006.Legal.action.dispatch.hook.Tests\G2A-006.Legal.action.dispatch.hook.Tests.csproj`
- 결과: 성공
- 전체: 10
- 통과: 10
- 실패: 0
- 스킵: 0

검증 항목:

- Goal CSV 행과 G2A-005 선행 완료 문서 확인
- AS-IS `CanSelect`, `CanPlayFromHandDuringMainPhase`, `CanAttack`, `QueueMainPhaseAction`, `PassTurn`, `HasMainPhaseAction` 참조 확인
- Setup phase에서 current turn player에게만 `AdvancePhase` 노출
- `Setup -> Active -> Unsuspend -> Draw -> Breeding -> Main` 흐름을 legal action 조회 결과로 진행
- Main phase에서 `Pass`, MemoryPass phase에서 `EndTurn` 노출
- dispatched action과 seeded legal action 병합
- terminal state에서 dispatched legal action 숨김
- pending effect가 있으면 legal action 숨김, effect resolved 후 다시 노출
- setup 없는 빈 match의 기존 empty action mask 계약 보존
- G2A-006 변경 파일 내 placeholder TODO 없음

### Engine 빌드

- 명령: `.\.dotnet\dotnet.exe build .\src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj`
- 결과: 성공
- 경고: 0
- 오류: 0

### 회귀 테스트

- 명령: `.\.dotnet\dotnet.exe run --project .\tests\G2A-005.End.turn.cleanup.Tests\G2A-005.End.turn.cleanup.Tests.csproj`
- 전체: 9
- 통과: 9
- 실패: 0
- 스킵: 0

- 명령: `.\.dotnet\dotnet.exe run --project .\tests\G1A-004.Observation.LegalAction.Tests\G1A-004.Observation.LegalAction.Tests.csproj`
- 전체: 7
- 통과: 7
- 실패: 0
- 스킵: 0

## 실패 상세 및 수정 여부

- 없음.

## 테스트하지 못한 항목과 이유

- 카드 플레이 legal action, 공격 legal action의 세부 합법성 계산은 후속 Goal 범위라 테스트하지 않았다.
- AS-IS UI command panel, drag/drop, target frame 표시 로직은 Headless 상태가 아니므로 테스트하지 않았다.
- effect trigger collection과 자동 효과 후보 수집은 G2F-001 이후 범위라 테스트하지 않았다.

## 미해결 리스크

- 현재 dispatcher는 phase 진행을 위한 기본 legal action hook이다. 카드별 play/attack/activate action은 후속 Goal에서 실제 룰 포팅과 함께 확장해야 한다.
- setup 없는 빈 match는 기존 G1A empty legal action 계약 보존을 위해 자동 phase dispatch를 비활성화한다.

## 완료 판정

- COMPLETE
