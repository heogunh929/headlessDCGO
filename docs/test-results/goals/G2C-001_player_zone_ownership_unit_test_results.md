# G2C-001 Player zone ownership 포팅 테스트 결과

## 실행 일시

- 2026-06-25 13:52:03 +09:00
- 실행 환경: Windows PowerShell, `.NET SDK` via `.\.dotnet\dotnet.exe`

## 수정/생성 파일

- 생성: `src/HeadlessDCGO.Engine/Headless/State/PlayerZoneAdapter.cs`
- 생성: `tests/G2C-001.Player.zone.ownership.Tests/G2C-001.Player.zone.ownership.Tests.csproj`
- 생성: `tests/G2C-001.Player.zone.ownership.Tests/Program.cs`
- 생성: `docs/test-results/goals/G2C-001_player_zone_ownership_unit_test_results.md`

## 읽기 전용 AS-IS 확인 파일

- `DCGO/Assets/Scripts/Script/Player.cs`
- `docs/goal-specs/G2C-001_player_zone_ownership_포팅.md`
- `docs/headless_complete_goal_breakdown.csv`
- `docs/headless_complete_goal_breakdown_ko.csv`
- `docs/test-results/goals/G2B-001_gamecontext_state_accessor_unit_test_results.md`

## 구현 요약

- `PlayerZoneAdapter`를 추가해 `MatchState` 기반 player zone ownership read/mutation 경계를 고정했다.
- AS-IS `Player`의 `LibraryCards`, `HandCards`, `FieldPermanents`, `TrashCards`, `SecurityCards`, `DigitamaLibraryCards`에 대응하는 Headless owned zone snapshot을 제공한다.
- `LocateCard`, `TryLocateCard`, `GetZone`, `GetZones`, `ReadPlayer`, `ReadAllPlayers`, `ApplyPlayerMutation`, `PlaceOwnedCard`, `WouldDeckOutOnDraw` API를 제공한다.
- adapter 생성과 mutation 적용 시 zone에 들어간 카드의 `CardInstanceState.OwnerId`가 zone owner와 일치하는지 검증한다.

## 테스트 명령 및 결과

| 명령 | 전체 | 통과 | 실패 | 스킵 |
|---|---:|---:|---:|---:|
| `.\.dotnet\dotnet.exe run --project .\tests\G2C-001.Player.zone.ownership.Tests\G2C-001.Player.zone.ownership.Tests.csproj` | 9 | 9 | 0 | 0 |
| `.\.dotnet\dotnet.exe build .\src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj` | 1 | 1 | 0 | 0 |
| `.\.dotnet\dotnet.exe run --project .\tests\G1B-002.MatchState.PlayerState.Tests\G1B-002.MatchState.PlayerState.Tests.csproj` | 7 | 7 | 0 | 0 |
| `.\.dotnet\dotnet.exe run --project .\tests\G2B-001.GameContext.state.accessor.Tests\G2B-001.GameContext.state.accessor.Tests.csproj` | 9 | 9 | 0 | 0 |
| `.\.dotnet\dotnet.exe run --project .\tests\G2B-002.Visibility.view.Tests\G2B-002.Visibility.view.Tests.csproj` | 9 | 9 | 0 | 0 |

## 실패 상세 및 수정

- 최초 G2C-001 테스트 실행은 `PlayerZoneAdapter.cs`에서 `IReadOnlyList<HeadlessEntityId>.IndexOf`를 직접 호출해 컴파일 실패했다.
- 수정: adapter 내부에 `FindIndex` helper를 추가하고 nullable 반환 경고를 정리했다.
- 두 번째 실행은 테스트 기대값 2개가 기존 state 계약과 맞지 않아 실패했다.
  - field count는 battle area와 breeding area 합산이므로 2가 맞다.
  - trash 이동은 기존 zone insertion 계약에 따라 destination bottom에 추가되어 `p1-trash`, `p1-hand` 순서가 맞다.
- 수정 후 전용 테스트 9/9 통과.
- 전용 테스트 실행 중 기존 런타임 파일의 nullable warning이 출력되었으나, 별도 엔진 build는 경고 0개/오류 0개로 통과했다.

## 테스트하지 못한 항목과 이유

- Unity `Permanent`, `CardSource`, frame UI 배치와 클릭 이벤트는 G2C-001의 Headless player zone adapter 범위 밖이라 테스트하지 않았다.
- 실제 카드 효과에 따른 소유권 변경, 컨트롤 변경, UI 표시 갱신은 후속 Goal 범위로 남겼다.
- `DCGO/Assets/...` 원본 파일은 읽기 전용으로만 확인했고 수정하지 않았다.

## 미해결 리스크

- 현재 adapter는 player-owned zone에 들어간 카드가 항상 `CardInstanceState.OwnerId`와 같은 player zone에 있어야 한다고 검증한다. 후속 카드 효과에서 컨트롤 변경이나 일시적 상대 zone 배치가 필요해지면 별도의 control owner 모델을 추가해야 한다.
- deck loss 판단은 state service 수준의 `WouldDeckOutOnDraw`로 제한했다. 실제 패배 처리 이벤트와 terminal result 반영은 후속 매치 룰 포팅 범위다.

## 완료 기준 충족 근거

- 선행 Goal `G2B-001` 결과 문서에서 COMPLETE를 확인했다.
- 산출물 `Player zone adapter`를 public API `PlayerZoneAdapter`와 snapshot/location 모델로 구현했다.
- 단위테스트 범위 `zone owner 테스트`를 다음 케이스로 검증했다: owned zone count/read, card owner-zone-location, owner checked mutation, non-owner/invalid zone failure, mismatched existing ownership rejection, deck loss state, deterministic snapshot.
- 전용 테스트와 관련 회귀 테스트가 모두 실패 없이 통과했다.

## 다음 Goal 진행 가능 여부

- 가능. G2C-001 완료 기준 `player zone 테스트 통과`를 충족했다.

## 완료 판정

- COMPLETE
