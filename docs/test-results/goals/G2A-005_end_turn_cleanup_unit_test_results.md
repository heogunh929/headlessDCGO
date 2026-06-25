# G2A-005 End turn cleanup 포팅

## 실행 일시

- 2026-06-25 13:30:01 +09:00

## Goal 범위

- Goal ID: G2A-005
- 목표: End turn cleanup 포팅
- 작업 범위: 턴 종료와 once flag cleanup 처리
- 완료 기준: end turn 테스트 통과
- 선행 Goal: G2A-004 결과 문서 COMPLETE 확인

## 수정/생성 파일

- 수정: `src/HeadlessDCGO.Engine/Headless/Runtime/HeadlessActionParameterKeys.cs`
- 수정: `src/HeadlessDCGO.Engine/Headless/Runtime/IHeadlessAttackController.cs`
- 수정: `src/HeadlessDCGO.Engine/Headless/Runtime/InMemoryHeadlessAttackController.cs`
- 수정: `src/HeadlessDCGO.Engine/Headless/Runtime/MetadataActionProcessor.cs`
- 생성: `src/HeadlessDCGO.Engine/Headless/Runtime/HeadlessEndTurnCleanupFlow.cs`
- 생성: `tests/G2A-005.End.turn.cleanup.Tests/G2A-005.End.turn.cleanup.Tests.csproj`
- 생성: `tests/G2A-005.End.turn.cleanup.Tests/Program.cs`
- 생성: `docs/test-results/goals/G2A-005_end_turn_cleanup_unit_test_results.md`

## 읽기 전용 AS-IS 참조 파일

- `docs/goal-specs/G2A-005_end_turn_cleanup_포팅.md`
- `docs/headless_complete_goal_breakdown.csv`
- `docs/headless_complete_goal_breakdown_ko.csv`
- `docs/test-results/goals/G2A-004_main_phase_memory_pass_unit_test_results.md`
- `DCGO/Assets/Scripts/Script/TurnStateMachine.cs`
- `DCGO/Assets/Scripts/Script/AutoProcessing.cs`
- `DCGO/Assets/Scripts/Script/AttackProcess.cs`

## 구현 요약

- `EndTurn` 처리 전에 `HeadlessEndTurnCleanupFlow`를 실행하도록 연결했다.
- AS-IS `EndPhase`의 `AttackCount = 0`에 맞춰 턴 종료용 `ResetTurnAttackState` API를 추가했다.
- 전장과 육성 영역의 카드 인스턴스에서 턴 종료 한정 메타데이터를 제거한다.
- 제거 대상은 `untilEachTurnEndEffects`, `untilOwnerTurnEndEffects`, `untilOpponentTurnEndEffects`, `untilEndTurnEffects`, `oncePerTurnUsed`, `useCountThisTurn` 등 턴 종료/once 계열 키로 제한했다.
- hand 카드와 지속 메타데이터(`persistentKeyword`, `isSuspended` 등)는 유지한다.
- MemoryPass에서 EndTurn이 발생해도 cleanup과 memory handoff가 함께 동작하도록 했다.
- 카드 효과 본문, Legal action dispatch, 다음 Phase 작업은 구현하지 않았다.

## 테스트 명령 및 결과

### G2A-005 단위테스트

- 명령: `.\.dotnet\dotnet.exe run --project .\tests\G2A-005.End.turn.cleanup.Tests\G2A-005.End.turn.cleanup.Tests.csproj`
- 최종 결과: 성공
- 전체: 9
- 통과: 9
- 실패: 0
- 스킵: 0

검증 항목:

- Goal CSV 행과 G2A-004 선행 완료 문서 확인
- AS-IS `EndPhase`, `EndTurnProcess`, `AttackCount = 0`, `UntilEachTurnEndEffects`, `UntilOwnerTurnEndEffects`, `UntilOpponentTurnEndEffects`, `InitUseCountThisTurn` 참조 확인
- EndTurn 시 전장 카드의 턴 한정 메타데이터 제거
- EndTurn 시 공격 횟수와 pending attack 상태 초기화
- 지속 메타데이터 보존
- hand 카드의 turn metadata 미정리
- EndTurn 전에는 turn metadata 유지
- MemoryPass 이후 EndTurn에서도 cleanup 적용
- G2A-005 변경 파일 내 placeholder TODO 없음

### Engine 빌드

- 명령: `.\.dotnet\dotnet.exe build .\src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj`
- 결과: 성공
- 경고: 0
- 오류: 0

### 회귀 테스트

- 명령: `.\.dotnet\dotnet.exe run --project .\tests\G2A-004.Main.phase.memory.pass.Tests\G2A-004.Main.phase.memory.pass.Tests.csproj`
- 전체: 11
- 통과: 11
- 실패: 0
- 스킵: 0

- 명령: `.\.dotnet\dotnet.exe run --project .\tests\G2A-003.Draw.Unsuspend.Breeding.phase.Tests\G2A-003.Draw.Unsuspend.Breeding.phase.Tests.csproj`
- 전체: 11
- 통과: 11
- 실패: 0
- 스킵: 0

## 실패 상세 및 수정 여부

- 최초 G2A-005 실행에서 `G2A-005 source files contain no placeholder TODOs` 1건 실패.
- 원인: 이번 Goal에서 수정한 `IHeadlessAttackController.cs`에 기존 placeholder TODO 주석이 남아 있었다.
- 수정: 해당 주석을 제거하고 재실행했다.
- 최종 재실행 결과: 9/9 통과.

## 테스트하지 못한 항목과 이유

- 실제 카드 효과의 `OnEndTurn` 해석과 자동 효과 큐 해결은 이번 Goal 범위가 아니라 테스트하지 않았다.
- AS-IS UI 선택 해제와 target arrow 정리는 Headless 런타임 상태가 아니므로 테스트하지 않았다.
- Legal action dispatch hook은 G2A-006 범위이므로 테스트하지 않았다.

## 미해결 리스크

- 턴 종료 cleanup 키는 Headless 메타데이터 계약으로 고정했으며, 실제 카드 효과 포팅이 진행되면 카드별 효과 상태와 연결되는 추가 키가 필요할 수 있다.
- 현재 cleanup은 전장과 육성 영역 카드 인스턴스에 한정한다. AS-IS의 player-level 임시 효과는 아직 별도 player effect state 모델이 없어서 카드 인스턴스 메타데이터로만 검증했다.

## 완료 판정

- COMPLETE
