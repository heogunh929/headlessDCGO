# G1B-005 ZoneMover

## 실행 일시

- 2026-06-25 00:05:28 +09:00
- 환경: Windows PowerShell, `.NET 8` SDK via `.\.dotnet\dotnet.exe`

## 수정/생성 파일

- 수정: `src/HeadlessDCGO.Engine/Headless/Services/IZoneMover.cs`
- 수정: `src/HeadlessDCGO.Engine/Headless/Services/ZoneMoveRequest.cs`
- 수정: `src/HeadlessDCGO.Engine/Headless/Services/InMemoryZoneMover.cs`
- 생성: `src/HeadlessDCGO.Engine/Headless/Services/ZoneMoveResult.cs`
- 생성: `tests/G1B-005.ZoneMover.Tests/G1B-005.ZoneMover.Tests.csproj`
- 생성: `tests/G1B-005.ZoneMover.Tests/Program.cs`
- 생성: `docs/test-results/goals/G1B-005_zone_mover_unit_test_results.md`

## 읽기 전용 확인 파일

- `docs/goal-specs/G1B-005_zonemover.md`
- `docs/headless_complete_goal_breakdown.csv`
- `docs/test-results/goals/G1B-003_zone_state_unit_test_results.md`
- `docs/test-results/goals/G1B-004_card_instance_state_unit_test_results.md`
- `src/HeadlessDCGO.Engine/Headless/State/ZoneState.cs`
- `src/HeadlessDCGO.Engine/Headless/State/PlayerState.cs`
- `src/HeadlessDCGO.Engine/Headless/Runtime/GameEvent.cs`
- `src/HeadlessDCGO.Engine/Headless/Runtime/GameEventType.cs`

## 구현 요약

- `IZoneMover.MoveAsync`가 `ZoneMoveResult`를 반환하도록 계약을 고정했다.
- `IZoneMover.Events`를 추가해 mutation boundary에서 발생한 이벤트를 읽을 수 있게 했다.
- `ZoneMoveRequest`에 `Custom`, `None -> None`, 동일 zone 이동 거부 계약을 추가했다.
- `InMemoryZoneMover`가 insert/move/remove마다 `GameEventType.CardMoved` 이벤트를 기록하고, shuffle은 `GameEventType.StateChanged` 이벤트를 기록한다.
- `ZoneMoveResult`에 요청, 이벤트, source/destination zone 사후 스냅샷을 담았다.

## 테스트 명령

```powershell
.\.dotnet\dotnet.exe run --project tests\G1B-003.ZoneState.Tests\G1B-003.ZoneState.Tests.csproj
.\.dotnet\dotnet.exe run --project tests\G1B-004.CardInstanceState.Tests\G1B-004.CardInstanceState.Tests.csproj
.\.dotnet\dotnet.exe run --project tests\G1B-005.ZoneMover.Tests\G1B-005.ZoneMover.Tests.csproj
.\.dotnet\dotnet.exe run --project tests\G1B-001.Stable.ID.entity.registry.Tests\G1B-001.Stable.ID.entity.registry.Tests.csproj
.\.dotnet\dotnet.exe build src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj
```

## 테스트 결과

| 범위 | 전체 | 통과 | 실패 | 스킵 |
|---|---:|---:|---:|---:|
| G1B-005 ZoneMover | 7 | 7 | 0 | 0 |
| 선행 G1B-003 ZoneState 확인 | 9 | 9 | 0 | 0 |
| 선행 G1B-004 CardInstanceState 확인 | 8 | 8 | 0 | 0 |
| 회귀 확인 G1B-001 Stable ID/Registry | 7 | 7 | 0 | 0 |
| 합계 | 31 | 31 | 0 | 0 |

## 빌드 결과

```text
Build succeeded.
Warnings: 0
Errors: 0
```

## 실패 상세

- 최종 실패 없음.
- 중간 실행에서 `tests/G1B-005.ZoneMover.Tests/Program.cs`의 face-up 메타데이터 검증이 보안 추가 이벤트가 아니라 이후 trash 이벤트를 선택해 1개 테스트가 실패했다.
- 같은 Goal 범위 안에서 테스트 선택 조건을 `toZone == Security`인 이벤트로 보정한 뒤 G1B-005 테스트를 재실행하여 7/7 통과를 확인했다.

## 미해결 리스크

- `git status` 확인은 현재 작업 디렉터리가 Git 저장소로 인식되지 않아 실행할 수 없었다.
- 원본 `DCGO/Assets/...` 파일은 수정하지 않았고, 작업 중 해당 경로에 쓰기 명령을 실행하지 않았다.
- 실제 카드별 룰/효과 포팅, UI/Photon/Prefab 동작은 G1B-005 범위 밖이므로 수행하지 않았다.

## 완료 판정

- 선행 Goal `G1B-003`, `G1B-004`의 결과 문서와 현재 테스트 통과를 확인했다.
- CSV 기준 산출물 `IZoneMover ZoneMoveRequest CardMoved event`를 구현했다.
- CSV 기준 단위테스트 범위 `move insert remove shuffle event 테스트`를 G1B-005 테스트에서 직접 검증했다.
- 완료 기준 `zone mover 테스트 통과` 충족.
- 판정: COMPLETE
