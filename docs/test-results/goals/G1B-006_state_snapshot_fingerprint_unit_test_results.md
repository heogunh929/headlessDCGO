# G1B-006 State snapshot fingerprint

## 실행 일시

- 2026-06-25 00:10:08 +09:00
- 환경: Windows PowerShell, `.NET 8` SDK via `.\.dotnet\dotnet.exe`

## 수정/생성 파일

- 수정: `src/HeadlessDCGO.Engine/Headless/State/MatchState.cs`
- 생성: `src/HeadlessDCGO.Engine/Headless/Services/IStateFingerprintService.cs`
- 생성: `src/HeadlessDCGO.Engine/Headless/Services/StateFingerprintService.cs`
- 생성: `tests/G1B-006.State.snapshot.fingerprint.Tests/G1B-006.State.snapshot.fingerprint.Tests.csproj`
- 생성: `tests/G1B-006.State.snapshot.fingerprint.Tests/Program.cs`
- 생성: `docs/test-results/goals/G1B-006_state_snapshot_fingerprint_unit_test_results.md`

## 읽기 전용 확인 파일

- `docs/goal-specs/G1B-006_state_snapshot_fingerprint.md`
- `docs/headless_complete_goal_breakdown.csv`
- `docs/headless_complete_unit_test_plan.md`
- `docs/test-results/goals/G1B-005_zone_mover_unit_test_results.md`
- `src/HeadlessDCGO.Engine/Headless/State/PlayerState.cs`
- `src/HeadlessDCGO.Engine/Headless/State/CardInstanceState.cs`
- `src/HeadlessDCGO.Engine/Headless/State/ZoneState.cs`
- `src/HeadlessDCGO.Engine/Headless/Runtime/GameEvent.cs`

## 구현 요약

- `IStateFingerprintService`와 `StateFingerprintService`를 추가했다.
- `StateFingerprintService`는 `MatchState` 또는 `MatchStateSnapshot`을 canonical snapshot 문자열로 정규화하고 SHA256 lowercase hex fingerprint를 계산한다.
- canonical snapshot은 version, terminal, player memory/zone/flags, card instance fingerprint segment, event sequence/type/message/metadata를 결정적 순서로 포함한다.
- `MatchState.ComputeFingerprint()`는 새 fingerprint service로 위임한다.

## 테스트 명령

```powershell
.\.dotnet\dotnet.exe run --project tests\G1B-005.ZoneMover.Tests\G1B-005.ZoneMover.Tests.csproj
.\.dotnet\dotnet.exe run --project tests\G1B-006.State.snapshot.fingerprint.Tests\G1B-006.State.snapshot.fingerprint.Tests.csproj
.\.dotnet\dotnet.exe run --project tests\G1B-002.MatchState.PlayerState.Tests\G1B-002.MatchState.PlayerState.Tests.csproj
.\.dotnet\dotnet.exe run --project tests\G1B-004.CardInstanceState.Tests\G1B-004.CardInstanceState.Tests.csproj
.\.dotnet\dotnet.exe build src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj
```

## 테스트 결과

| 범위 | 전체 | 통과 | 실패 | 스킵 |
|---|---:|---:|---:|---:|
| G1B-006 State snapshot fingerprint | 7 | 7 | 0 | 0 |
| 선행 G1B-005 ZoneMover 확인 | 7 | 7 | 0 | 0 |
| 회귀 확인 G1B-002 MatchState/PlayerState | 7 | 7 | 0 | 0 |
| 회귀 확인 G1B-004 CardInstanceState | 8 | 8 | 0 | 0 |
| 합계 | 29 | 29 | 0 | 0 |

## 빌드 결과

```text
Build succeeded.
Warnings: 0
Errors: 0
```

## 실패 상세

- 최종 실패 없음.
- 별도 수정이 필요한 실패 테스트는 발생하지 않았다.

## 미해결 리스크

- `git status`와 `git status --short -- DCGO\Assets`는 현재 작업 디렉터리가 Git 저장소로 인식되지 않아 실행할 수 없었다.
- 원본 `DCGO/Assets/...` 파일은 수정하지 않았고, 작업 중 해당 경로에 쓰기 명령을 실행하지 않았다.
- 실제 카드 룰/효과 포팅과 parity fingerprint 비교는 G1B-006 범위 밖이므로 수행하지 않았다.

## 완료 판정

- 선행 Goal `G1B-005`의 결과 문서와 현재 테스트 통과를 확인했다.
- CSV 기준 산출물 `Snapshot Fingerprint service`를 구현했다.
- CSV 기준 단위테스트 범위 `same state same fingerprint 테스트`를 G1B-006 테스트에서 직접 검증했다.
- 완료 기준 `state 결정성 테스트 통과` 충족.
- 판정: COMPLETE
