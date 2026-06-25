# G2A-002 Match Setup And First Player Unit Test Results

## 실행 일시

- 2026-06-25 13:08:00 +09:00

## 수정/생성 파일

- `src/HeadlessDCGO.Engine/Headless/Runtime/MatchSetupFlow.cs`
- `src/HeadlessDCGO.Engine/Headless/Runtime/MatchConfig.cs`
- `src/HeadlessDCGO.Engine/Headless/Runtime/DcgoMatch.cs`
- `tests/G2A-002.setup.Tests/G2A-002.setup.Tests.csproj`
- `tests/G2A-002.setup.Tests/Program.cs`
- `docs/test-results/goals/G2A-002_setup_first_player_unit_test_results.md`

## 읽기 전용 AS-IS 확인 파일

- `DCGO/Assets/Scripts/Script/TurnStateMachine.cs`
- `DCGO/Assets/Scripts/Script/GameContext.cs`
- `DCGO/Assets/Scripts/Script/CardController.cs`
- `DCGO/Assets/Scripts/Script/CardObjectController.cs`
- `docs/headless_complete_goal_breakdown.csv`
- `docs/headless_complete_goal_breakdown_ko.csv`
- `docs/test-results/goals/G2A-001_phase_mapping_unit_test_results.md`

## 테스트 명령

```powershell
.\.dotnet\dotnet.exe run --project .\tests\G2A-002.setup.Tests\G2A-002.setup.Tests.csproj
```

```powershell
.\.dotnet\dotnet.exe build .\src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj
```

```powershell
.\.dotnet\dotnet.exe run --project .\tests\G1A-002.MatchLifecycle.Tests\G1A-002.MatchLifecycle.Tests.csproj
```

```powershell
.\.dotnet\dotnet.exe run --project .\tests\G2A-001.Phase.enum.state.mapping.Tests\G2A-001.Phase.enum.state.mapping.Tests.csproj
```

## 테스트 결과

- G2A-002 단위테스트: 전체 9, 통과 9, 실패 0, 스킵 0
- 엔진 빌드: 경고 0, 오류 0
- G1A-002 회귀 테스트: 전체 5, 통과 5, 실패 0, 스킵 0
- G2A-001 선행 회귀 테스트: 전체 9, 통과 9, 실패 0, 스킵 0

## 실패 상세와 수정 여부

- 최종 실패 없음.
- 최초 G2A-002 테스트 실행 시 기존 `HeadlessGameLoop.cs`, `MetadataActionProcessor.cs` nullable 경고가 출력되었으나 테스트는 통과했다. 이후 엔진 단독 빌드는 경고 0, 오류 0으로 통과했다.

## 테스트하지 못한 항목과 이유

- AS-IS mulligan 선택 UI/Photon 동기화는 G2A-002의 Headless setup flow 범위를 벗어나므로 구현/검증하지 않았다.
- 실제 카드 효과, 드로우 트리거 효과, 보안 회복 효과 처리는 이후 Goal 범위로 남겼다.
- 원본 `DCGO/Assets/...` 파일은 읽기 전용으로만 확인했고 수정하지 않았다.

## 완료 기준 충족 근거

- `MatchConfig.Setup`과 `MatchSetupFlow`로 초기 player turn, 선공, opening hand 5장, security 5장, library/digitama library 배치를 수행한다.
- 지정 선공과 seed 기반 미지정 선공 모두 테스트로 검증했다.
- setup reset 재적용, deck entry count 확장, 잘못된 setup 입력 실패를 테스트로 검증했다.
- 완료 기준 `setup 테스트 통과`를 G2A-002 단위테스트 9/9 통과로 충족했다.

## 다음 Goal 진행 가능 여부

- 가능. G2A-002 범위의 테스트와 결과 문서가 완료되었고 실패 테스트가 없다.

## 완료 판정

- COMPLETE
