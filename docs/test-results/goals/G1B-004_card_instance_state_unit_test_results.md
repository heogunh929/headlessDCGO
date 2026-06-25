# G1B-004 CardInstanceState

## 실행 일시

- 2026-06-24 23:58:40 +09:00
- 환경: Windows PowerShell, `.NET 8` SDK via `.\.dotnet\dotnet.exe`

## 수정/생성 파일

- 수정: `src/HeadlessDCGO.Engine/Headless/State/CardInstanceState.cs`
- 생성: `tests/G1B-004.CardInstanceState.Tests/G1B-004.CardInstanceState.Tests.csproj`
- 생성: `tests/G1B-004.CardInstanceState.Tests/Program.cs`
- 생성: `docs/test-results/goals/G1B-004_card_instance_state_unit_test_results.md`

## 읽기 전용 확인 파일

- `docs/goal-specs/G1B-004_cardinstancestate.md`
- `docs/headless_complete_goal_breakdown.csv`
- `docs/headless_complete_unit_test_plan.md`
- `docs/test-results/goals/G1B-003_zone_state_unit_test_results.md`
- `src/HeadlessDCGO.Engine/Headless/State/MatchState.cs`
- `tests/G1B-002.MatchState.PlayerState.Tests/Program.cs`
- `tests/G1B-003.ZoneState.Tests/Program.cs`

## 구현 요약

- `CardInstanceState`에 `Flags` 읽기 전용 스냅샷을 추가했다.
- suspend/unsuspend, reveal/hide, source attach/detach/clear, modifier add/remove, flag set/clear/query 계약을 고정했다.
- source 중복/빈 id, 빈 key, 없는 source/modifier/flag 제거 시 명확한 예외를 발생시킨다.
- `FingerprintSegment()`를 추가해 card instance mutable state의 결정적 표현을 제공한다.

## 테스트 명령

```powershell
.\.dotnet\dotnet.exe run --project tests\G1B-003.ZoneState.Tests\G1B-003.ZoneState.Tests.csproj
.\.dotnet\dotnet.exe run --project tests\G1B-004.CardInstanceState.Tests\G1B-004.CardInstanceState.Tests.csproj
.\.dotnet\dotnet.exe run --project tests\G1B-002.MatchState.PlayerState.Tests\G1B-002.MatchState.PlayerState.Tests.csproj
.\.dotnet\dotnet.exe run --project tests\G1B-003.ZoneState.Tests\G1B-003.ZoneState.Tests.csproj
```

## 테스트 결과

| 범위 | 전체 | 통과 | 실패 | 스킵 |
|---|---:|---:|---:|---:|
| G1B-004 CardInstanceState | 8 | 8 | 0 | 0 |
| 선행 G1B-003 ZoneState 확인 | 9 | 9 | 0 | 0 |
| 호환성 G1B-002 MatchState/PlayerState 확인 | 7 | 7 | 0 | 0 |
| 합계 | 24 | 24 | 0 | 0 |

## 실패 상세

- 최종 실패 없음.
- 중간 실행에서 `tests/G1B-004.CardInstanceState.Tests/Program.cs`의 예외 검증 람다 안 `with` 식이 단독 문장으로 작성되어 컴파일 오류가 발생했다.
- 같은 Goal 범위 안에서 테스트 코드만 `_ = state with { ... }` 형태로 수정한 뒤 재실행하여 8/8 통과를 확인했다.

## 미해결 리스크

- `git status` 확인은 현재 작업 디렉터리가 Git 저장소로 인식되지 않아 실행할 수 없었다.
- 원본 `DCGO/Assets/...` 파일은 수정하지 않았고, 작업 중 해당 경로에 쓰기 명령을 실행하지 않았다.
- 실제 카드 룰/효과 포팅은 Phase 1 범위 밖이므로 수행하지 않았다.

## 완료 판정

- 선행 Goal `G1B-003` 결과 문서와 현재 테스트 통과를 확인했다.
- CSV 기준 단위테스트 범위 `suspend face-up source modifier flag 테스트`를 G1B-004 테스트에서 직접 검증했다.
- 완료 기준 `card instance state 테스트 통과` 충족.
- 판정: COMPLETE
