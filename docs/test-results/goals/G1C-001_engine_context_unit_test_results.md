# G1C-001 EngineContext

## 실행 일시

- 2026-06-25 00:38:58 +09:00
- 환경: Windows PowerShell, `.NET 8` SDK via `.\.dotnet\dotnet.exe`

## 수정/생성 파일

- 수정: `src/HeadlessDCGO.Engine/Headless/Bridge/EngineContext.cs`
- 수정: `src/HeadlessDCGO.Engine/Headless/Runtime/DcgoMatch.cs`
- 생성: `tests/G1C-001.EngineContext.Tests/G1C-001.EngineContext.Tests.csproj`
- 생성: `tests/G1C-001.EngineContext.Tests/Program.cs`
- 생성: `docs/test-results/goals/G1C-001_engine_context_unit_test_results.md`

## 읽기 전용 확인 파일

- `docs/goal-specs/G1C-001_enginecontext.md`
- `docs/headless_complete_goal_breakdown.csv`
- `docs/headless_complete_unit_test_plan.md`
- `docs/test-results/goals/G1A-002_match_lifecycle_unit_test_results.md`
- `src/HeadlessDCGO.Engine/Headless/Bridge/ContinuousContext.cs`
- `src/HeadlessDCGO.Engine/Headless/Bridge/GManagerBridge.cs`
- `src/HeadlessDCGO.Engine/Headless/Effects/EffectContext.cs`
- `src/HeadlessDCGO.Engine/Headless/Runtime/DcgoMatch.cs`

## 구현 요약

- `EngineContext`를 명시적 service container로 확정했다.
- 기본 headless services를 interface type과 concrete type으로 등록하고, `GetService`, `TryGetService`, `RegisterService`, `Services` snapshot API를 추가했다.
- 미등록 service, null type/service, assign 불가능한 service 등록은 명확한 예외로 실패한다.
- `CurrentMatch`와 `CurrentState`를 추가하고, `DcgoMatch` 생성/step 결과와 연결했다.
- `ResetMatchState`가 scoped service와 current state를 함께 초기화하도록 보강했다.

## 테스트 명령

```powershell
.\.dotnet\dotnet.exe run --project tests\G1A-002.MatchLifecycle.Tests\G1A-002.MatchLifecycle.Tests.csproj
.\.dotnet\dotnet.exe run --project tests\G1C-001.EngineContext.Tests\G1C-001.EngineContext.Tests.csproj
.\.dotnet\dotnet.exe run --project tests\G1A-003.ActionContract.Tests\G1A-003.ActionContract.Tests.csproj
.\.dotnet\dotnet.exe run --project tests\G1A-004.Observation.LegalAction.Tests\G1A-004.Observation.LegalAction.Tests.csproj
.\.dotnet\dotnet.exe build src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj
```

## 테스트 결과

| 범위 | 전체 | 통과 | 실패 | 스킵 |
|---|---:|---:|---:|---:|
| G1C-001 EngineContext | 7 | 7 | 0 | 0 |
| 선행 G1A-002 MatchLifecycle 확인 | 5 | 5 | 0 | 0 |
| 회귀 확인 G1A-003 ActionContract | 6 | 6 | 0 | 0 |
| 회귀 확인 G1A-004 Observation/LegalAction | 7 | 7 | 0 | 0 |
| 합계 | 25 | 25 | 0 | 0 |

## 빌드 결과

```text
Build succeeded.
Warnings: 0
Errors: 0
```

## 실패 상세

- 최종 실패 없음.
- 중간 실행에서 G1C-001 테스트 fixture가 `MatchConfig` 생성자 인자를 잘못 사용해 컴파일 오류가 발생했다.
- 같은 Goal 범위 안에서 테스트 fixture를 `MatchConfig.Create(...)`로 수정한 뒤 G1C-001 테스트를 재실행하여 7/7 통과를 확인했다.

## 테스트하지 않은 항목과 이유

- `GManagerBridge`, `ContinuousContext`, `EffectContext`의 전체 포팅은 이후 Bridge/Effect Goal 범위이므로 이번 Goal에서는 구현하지 않았다.
- 실제 카드별 룰/효과 포팅과 Unity `GameObject/GetComponent` 대체는 G1C-001 범위 밖이므로 수행하지 않았다.

## 미해결 리스크

- `git status`와 `git status --short -- DCGO\Assets`는 현재 작업 디렉터리가 Git 저장소로 인식되지 않아 실행할 수 없었다.
- 원본 `DCGO/Assets/...` 파일은 수정하지 않았고, 작업 중 해당 경로에 쓰기 명령을 실행하지 않았다.

## 완료 판정

- 선행 Goal `G1A-002`의 결과 문서와 현재 테스트 통과를 확인했다.
- CSV 기준 산출물 `EngineContext`를 구현했다.
- CSV 기준 단위테스트 범위 `service registration lookup 테스트`를 G1C-001 테스트에서 직접 검증했다.
- 완료 기준 `EngineContext 테스트 통과` 충족.
- 판정: COMPLETE
