# G1G-001 Player session model

## 실행 일시
- 2026-06-25 09:40:22 +09:00
- 환경: Windows PowerShell, .NET SDK via `.\.dotnet\dotnet.exe`

## 수정/생성 파일
- 생성: `src/HeadlessDCGO.Engine/Headless/Runtime/SessionContext.cs`
- 생성: `tests/G1G-001.Player.session.model.Tests/G1G-001.Player.session.model.Tests.csproj`
- 생성: `tests/G1G-001.Player.session.model.Tests/Program.cs`
- 생성: `docs/test-results/goals/G1G-001_player_session_model_unit_test_results.md`

## 읽기 전용으로 확인한 AS-IS 파일
- `DCGO/Assets/Scripts/Script/GManager.cs`
- `DCGO/Assets/Scripts/Script/GameContext.cs`
- `DCGO/Assets/Scripts/Script/TurnStateMachine.cs`

## 선행 Goal 확인
- `docs/test-results/goals/G1B-001_stable_ids_unit_test_results.md`: COMPLETE 확인

## 구현 요약
- `SessionContext`를 추가해 local player session id, player order, local player, turn player, turn number를 immutable snapshot으로 보존한다.
- player membership, owner/viewer identity, local owner, turn player, non-turn player, local turn 여부를 public API로 고정했다.
- `WithTurn`, `AdvanceTurn`, `Fingerprint`로 deterministic turn identity와 반복 가능한 session fingerprint를 제공한다.
- 무효 player, 중복 player, session 밖 local/turn/owner player 입력은 명확한 예외로 실패한다.

## 테스트 명령
- `.\.dotnet\dotnet.exe run --project tests\G1G-001.Player.session.model.Tests\G1G-001.Player.session.model.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project tests\G1B-001.Stable.ID.entity.registry.Tests\G1B-001.Stable.ID.entity.registry.Tests.csproj`
- `.\.dotnet\dotnet.exe build src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj`

## 테스트 결과
| 범위 | 전체 | 통과 | 실패 | 스킵 |
|---|---:|---:|---:|---:|
| G1G-001 Player session model | 11 | 11 | 0 | 0 |
| G1B-001 predecessor regression | 7 | 7 | 0 | 0 |
| Engine build | 1 | 1 | 0 | 0 |

## 실패 상세
- 없음.

## 참고 사항
- G1G-001 테스트 명령 실행 중 `HeadlessGameLoop.cs`와 `MetadataActionProcessor.cs`의 기존 nullable warning이 출력되었으나 실패는 없었다.
- 별도 엔진 빌드 명령은 경고 0개, 오류 0개로 완료되었다.

## 테스트하지 못한 항목
- 없음. CSV의 단위테스트 범위 `player ownership turn identity 테스트`를 전용 테스트로 검증했다.

## 미해결 리스크
- Photon room/player/RPC/ownership transport 대체는 이 Goal 범위 밖이므로 구현하지 않았다.
- `SessionContext`를 `DcgoMatch` 또는 원격 runner와 통합하는 작업은 후속 Goal 범위에서 다루어야 한다.

## 완료 기준 충족 근거
- player ownership, local ownership, turn identity, non-turn identity, deterministic turn advance, invalid input failure, fingerprint stability를 단위테스트로 검증했다.
- 원본 `DCGO/Assets/...` 파일은 읽기 전용으로만 확인했고 수정하지 않았다.

## 완료 판정
- COMPLETE
