# G2B-001 GameContext state accessor 포팅

## 실행 일시

- 2026-06-25 13:40:19 +09:00

## Goal 범위

- Goal ID: G2B-001
- 목표: GameContext state accessor 포팅
- 작업 범위: AS-IS GameContext 접근을 MatchState로 연결
- 완료 기준: GameContext accessor 테스트 통과
- 선행 Goal: G2A-002 결과 문서 COMPLETE 확인

## 수정/생성 파일

- 생성: `src/HeadlessDCGO.Engine/Headless/State/GameContextStateAccessor.cs`
- 생성: `tests/G2B-001.GameContext.state.accessor.Tests/G2B-001.GameContext.state.accessor.Tests.csproj`
- 생성: `tests/G2B-001.GameContext.state.accessor.Tests/Program.cs`
- 생성: `docs/test-results/goals/G2B-001_gamecontext_state_accessor_unit_test_results.md`

## 읽기 전용 AS-IS 참조 파일

- `docs/goal-specs/G2B-001_gamecontext_state_accessor_포팅.md`
- `docs/headless_complete_goal_breakdown.csv`
- `docs/headless_complete_goal_breakdown_ko.csv`
- `docs/test-results/goals/G2A-002_setup_first_player_unit_test_results.md`
- `DCGO/Assets/Scripts/Script/GameContext.cs`
- `DCGO/Assets/Scripts/Script/TurnStateMachine.cs`

## 구현 요약

- `GameContextStateAccessor`를 추가해 AS-IS `GameContext` 접근 패턴을 `MatchState` 기반 read/write view로 고정했다.
- 지원 접근자: `Memory`, `TurnPhase`, `TurnPlayer`, `NonTurnPlayer`, `FirstPlayer`, `Players`, `PlayersForTurnPlayer`, `PlayersForNonTurnPlayer`, `PermanentsForTurnPlayer`, `ActiveCardIds`, `DoSwitchTurnPlayer`, `IsSecurityLooking`.
- `PlayerFromId`, `TryPlayerFromId`, `SwitchTurnPlayer`, `ReadState`, `WriteState`, `SetMemory`, `SetTurnPhase`, `SetSecurityLooking` public API를 제공한다.
- invalid turn player, invalid first player, duplicate/empty active card id는 상태 변경 없이 실패한다.
- live runtime 서비스나 카드 효과 동작은 변경하지 않고, `MatchState` 어댑터 계약만 추가했다.

## 테스트 명령 및 결과

### G2B-001 단위테스트

- 명령: `.\.dotnet\dotnet.exe run --project .\tests\G2B-001.GameContext.state.accessor.Tests\G2B-001.GameContext.state.accessor.Tests.csproj`
- 결과: 성공
- 전체: 9
- 통과: 9
- 실패: 0
- 스킵: 0

검증 항목:

- Goal CSV 행과 G2A-002 선행 완료 문서 확인
- AS-IS `Memory`, `ActiveCardList`, `Players_ForTurnPlayer`, `Players_ForNonTurnPlayer`, `PlayerFromID`, `SwitchTurnPlayer`, `DoSwitchTurnPlayer`, `IsSecurityLooking`, `TurnPhase` 참조 확인
- player/turn view 읽기
- memory, phase, active cards, security flag 쓰기
- `DoSwitchTurnPlayer` 기반 switch contract
- `MatchState.MoveCard` 결과 write와 deterministic zone movement 보존
- invalid write 시 기존 상태 불변
- 동일 입력 snapshot deterministic metadata 확인
- G2B-001 변경 파일 내 placeholder TODO 없음

### Engine 빌드

- 명령: `.\.dotnet\dotnet.exe build .\src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj`
- 결과: 성공
- 경고: 0
- 오류: 0

### 회귀 테스트

- 명령: `.\.dotnet\dotnet.exe run --project .\tests\G1B-002.MatchState.PlayerState.Tests\G1B-002.MatchState.PlayerState.Tests.csproj`
- 전체: 7
- 통과: 7
- 실패: 0
- 스킵: 0

- 명령: `.\.dotnet\dotnet.exe run --project .\tests\G2A-002.setup.Tests\G2A-002.setup.Tests.csproj`
- 전체: 9
- 통과: 9
- 실패: 0
- 스킵: 0

## 실패 상세 및 수정 여부

- 최초 실행 1: `GameContextStateAccessor.cs` 생성자에서 `IEnumerable<HeadlessEntityId>`를 `IReadOnlyList<HeadlessEntityId>`에 직접 대입해 컴파일 실패.
- 수정: `CopyActiveCardIds` 경유로 active card id 목록을 복사하도록 변경.
- 최초 실행 2: 테스트 함수 일부가 `Task` 반환 시그니처에서 `Task.CompletedTask`를 반환하지 않아 컴파일 실패.
- 수정: 해당 테스트 함수들에 명시적 완료 반환 추가.
- 최종 재실행 결과: 9/9 통과.

## 테스트하지 못한 항목과 이유

- visibility view 분리는 G2B-002 범위라 구현/테스트하지 않았다.
- Player zone ownership adapter는 G2C-001 범위라 구현/테스트하지 않았다.
- 실제 runtime `EngineContext` 서비스와 `MatchState`의 완전한 live sync는 후속 통합 범위가 필요하므로 이번 Goal에서는 `MatchState` 기반 accessor 계약만 검증했다.

## 미해결 리스크

- AS-IS `GameContext.ActiveCardList`는 실제 `CardSource` 목록이지만 Headless에서는 `HeadlessEntityId` 목록으로 고정했다. 카드 효과 포팅 후 추가 메타데이터가 필요할 수 있다.
- AS-IS player-level 효과 상태는 아직 별도 player effect state 모델이 없으므로 이번 accessor는 플레이어/존/카드 인스턴스 상태와 scalar context 값을 중심으로 제공한다.

## 완료 판정

- COMPLETE
