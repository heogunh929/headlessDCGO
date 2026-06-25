# G2B-002 Visibility view 포팅 테스트 결과

## 실행 일시

- 2026-06-25 13:46:25 +09:00
- 실행 환경: Windows PowerShell, `.NET SDK` via `.\.dotnet\dotnet.exe`

## 수정/생성 파일

- 생성: `src/HeadlessDCGO.Engine/Headless/State/VisibilityView.cs`
- 생성: `tests/G2B-002.Visibility.view.Tests/G2B-002.Visibility.view.Tests.csproj`
- 생성: `tests/G2B-002.Visibility.view.Tests/Program.cs`
- 생성: `docs/test-results/goals/G2B-002_visibility_view_unit_test_results.md`

## 읽기 전용 AS-IS 확인 파일

- `DCGO/Assets/Scripts/Script/GameContext.cs`
- `docs/goal-specs/G2B-002_visibility_view_포팅.md`
- `docs/headless_complete_goal_breakdown.csv`
- `docs/headless_complete_goal_breakdown_ko.csv`
- `docs/test-results/goals/G2B-001_gamecontext_state_accessor_unit_test_results.md`

## 구현 요약

- `VisibilityView.ForPlayer(...)`를 추가해 `GameContextStateAccessor` 또는 `GameContextStateSnapshot`을 특정 플레이어 시야의 `VisibilityViewSnapshot`으로 변환한다.
- 플레이어 시야는 본인 hidden zone의 카드 ID를 볼 수 있고, 상대 hidden zone은 카드 수량만 보며 카드 ID는 비운다.
- `VisibilityView.ForDebugFull(...)`를 추가해 모든 플레이어의 hidden zone 카드 ID를 드러내는 debug full view를 제공한다.
- active card 목록은 player view에서 현재 viewer에게 보이는 카드만 포함하고, debug full view에서는 전체 active card 목록을 유지한다.

## 테스트 명령 및 결과

| 명령 | 전체 | 통과 | 실패 | 스킵 |
|---|---:|---:|---:|---:|
| `.\.dotnet\dotnet.exe run --project .\tests\G2B-002.Visibility.view.Tests\G2B-002.Visibility.view.Tests.csproj` | 9 | 9 | 0 | 0 |
| `.\.dotnet\dotnet.exe build .\src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj` | 1 | 1 | 0 | 0 |
| `.\.dotnet\dotnet.exe run --project .\tests\G2B-001.GameContext.state.accessor.Tests\G2B-001.GameContext.state.accessor.Tests.csproj` | 9 | 9 | 0 | 0 |
| `.\.dotnet\dotnet.exe run --project .\tests\G1B-003.ZoneState.Tests\G1B-003.ZoneState.Tests.csproj` | 9 | 9 | 0 | 0 |

## 실패 상세 및 수정

- 최초 G2B-002 테스트 실행은 `VisibilityView.cs`의 `HeadlessPhase` 참조에 `HeadlessDCGO.Engine.Headless.Runtime` using이 없어 컴파일 실패했다.
- 수정: `VisibilityView.cs`에 Runtime namespace using을 추가했다.
- 재실행 결과 G2B-002 전용 테스트 9/9 통과.
- 전용 테스트 실행 중 기존 파일 `HeadlessGameLoop.cs`, `MetadataActionProcessor.cs`의 nullable warning이 출력되었으나, 별도 엔진 build 재실행은 경고 0개/오류 0개로 통과했다.

## 테스트하지 못한 항목과 이유

- Unity UI, Photon 네트워크, 실제 `CardSource` 객체 표시 동작은 G2B-002의 Headless visibility view 범위 밖이라 테스트하지 않았다.
- `DCGO/Assets/...` 원본 파일은 읽기 전용으로만 확인했고 수정하지 않았다.

## 미해결 리스크

- 현재 active card visibility는 카드가 위치한 zone의 기본 visibility와 owner 기준으로 판정한다. 이후 카드별 face-up 공개 규칙이 별도 Goal에서 구체화되면 이 view의 공개 조건을 확장해야 한다.
- debug full view는 테스트/디버그 용도의 전체 정보 view로 고정했으며, 외부 플레이어 observation API와 연결하는 작업은 후속 Goal 범위로 남긴다.

## 완료 기준 충족 근거

- 선행 Goal `G2B-001` 결과 문서에서 COMPLETE를 확인했다.
- 산출물 `visibility view`를 public API `VisibilityView.ForPlayer`, `VisibilityView.ForDebugFull`, `VisibilityViewSnapshot`으로 구현했다.
- 단위테스트 범위 `hidden information view 테스트`를 다음 케이스로 검증했다: 상대 hidden zone ID 은닉, 본인 hidden zone 공개, public zone 공개, debug full view 전체 공개, active card 시야 필터, 잘못된 viewer 실패, 결정성.
- 전용 테스트와 관련 회귀 테스트가 모두 실패 없이 통과했다.

## 다음 Goal 진행 가능 여부

- 가능. G2B-002 완료 기준 `visibility 테스트 통과`를 충족했다.

## 완료 판정

- COMPLETE
