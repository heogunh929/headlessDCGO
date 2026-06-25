# G2A-004 Main Phase / Memory Pass Unit Test Results

## 실행 일시

- 2026-06-25 13:23:34 +09:00

## Goal 범위

- Goal ID: G2A-004
- 목표: Main phase와 memory pass 포팅
- 작업 범위: main phase 진입과 memory pass 처리
- 완료 기준: main phase 테스트 통과
- 선행 Goal: G2A-003 완료 문서 확인

## 수정/생성 파일

- 수정: `src/HeadlessDCGO.Engine/Headless/Runtime/HeadlessActionParameterKeys.cs`
- 수정: `src/HeadlessDCGO.Engine/Headless/Runtime/MetadataActionProcessor.cs`
- 생성: `src/HeadlessDCGO.Engine/Headless/Runtime/HeadlessMainPhaseFlow.cs`
- 생성: `tests/G2A-004.Main.phase.memory.pass.Tests/G2A-004.Main.phase.memory.pass.Tests.csproj`
- 생성: `tests/G2A-004.Main.phase.memory.pass.Tests/Program.cs`
- 생성: `docs/test-results/goals/G2A-004_main_phase_memory_pass_unit_test_results.md`

## 읽기 전용 참조 파일

- `docs/goal-specs/G2A-004_main_phase와_memory_pass_포팅.md`
- `docs/headless_complete_goal_breakdown.csv`
- `docs/headless_complete_goal_breakdown_ko.csv`
- `docs/test-results/goals/G2A-003_early_phase_flow_unit_test_results.md`
- `DCGO/Assets/Scripts/Script/TurnStateMachine.cs`
- `DCGO/Assets/Scripts/Script/AutoProcessing.cs`
- `DCGO/Assets/Scripts/Script/Player.cs`

## 구현 요약

- `AdvancePhase`로 `Main`에 진입할 때 `mainPhaseEntered`, `memoryPassTriggered`, `memoryPassReason` 메타데이터를 반환하도록 고정했다.
- `Pass` 액션은 `Main` phase에서만 허용하고, 명시 pass 시 메모리를 `-3`으로 설정한 뒤 `MemoryPass` phase로 전이한다.
- `SetMemory`, `AddMemory`, `PayMemory` 중 현재 턴 플레이어가 `Main` phase에서 메모리를 `-1` 이하로 넘기면 `MemoryPass` phase로 전이한다.
- `MemoryPass` 상태에서 `EndTurn`을 처리하면 다음 플레이어의 `Active` phase로 넘어가며 음수 메모리를 양수 메모리로 전달한다.
- 범위 밖인 End phase cleanup, 공격 처리, 카드 효과 포팅은 구현하지 않았다.

## 테스트 명령 및 결과

### G2A-004 단위테스트

- 명령: `.\.dotnet\dotnet.exe run --project .\tests\G2A-004.Main.phase.memory.pass.Tests\G2A-004.Main.phase.memory.pass.Tests.csproj`
- 전체: 11
- 통과: 11
- 실패: 0
- 스킵: 0

검증 항목:

- Goal CSV 행과 G2A-003 선행 완료 문서 확인
- AS-IS `MainPhase`, `PassTurn`, `EndTurnCheck`, `EndTurnProcess`, `MemoryForPlayer` 참조 확인
- Main phase 진입 시 memory pass 미발생 확인
- Main phase 명시 Pass 시 `MemoryPass` 전이 및 `-3` 메모리 확인
- MemoryPass 이후 EndTurn 시 다음 플레이어에게 `+3` 메모리 전달 확인
- Main phase 중 `PayMemory(1)`로 `-1` 진입 시 memory pass 확인
- Main phase 중 `SetMemory(-2)` 시 memory pass 확인
- Main phase 중 `AddMemory(2)`는 Main 유지 확인
- Main phase 밖 Pass 불법 처리 확인
- non-turn player Pass 불법 처리 확인
- G2A-004 변경 파일 내 placeholder TODO 없음 확인

### Engine 빌드

- 명령: `.\.dotnet\dotnet.exe build .\src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj`
- 결과: 성공
- 경고: 0
- 오류: 0

### 선행 흐름 회귀 테스트

- 명령: `.\.dotnet\dotnet.exe run --project .\tests\G2A-003.Draw.Unsuspend.Breeding.phase.Tests\G2A-003.Draw.Unsuspend.Breeding.phase.Tests.csproj`
- 전체: 11
- 통과: 11
- 실패: 0
- 스킵: 0

- 명령: `.\.dotnet\dotnet.exe run --project .\tests\G2A-002.setup.Tests\G2A-002.setup.Tests.csproj`
- 전체: 9
- 통과: 9
- 실패: 0
- 스킵: 0

## 실패 상세

- 없음.

## 미해결 리스크

- AS-IS의 UI 선택 가능 여부, 자동 처리 큐, 카드별 메모리 변경 효과는 이번 Goal 범위가 아니므로 포팅하지 않았다.
- Headless 메모리는 현재 턴 기준 정수 게이지로 유지하며, AS-IS raw memory의 플레이어별 부호 변환은 `MemoryPass` 전이와 EndTurn 전달 계약으로만 반영했다.

## 완료 판정

- COMPLETE
