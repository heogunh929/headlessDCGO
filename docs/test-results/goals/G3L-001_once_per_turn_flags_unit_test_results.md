# G3L-001 Once per turn flag helper 포팅

## 실행 일시

- 2026-06-25 20:37:51 +09:00

## 실행 환경

- 작업 디렉터리: `E:\headlessDCGO_new`
- 테스트 런타임: `.\.dotnet\dotnet.exe`

## 수정/생성 파일

- 생성: `src/HeadlessDCGO.Engine/Headless/Effects/OnceFlagHelpers.cs`
- 생성: `src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons/OnceFlagHelpers.cs`
- 생성: `tests/G3L-001.Once.per.turn.flag.helper.Tests/G3L-001.Once.per.turn.flag.helper.Tests.csproj`
- 생성: `tests/G3L-001.Once.per.turn.flag.helper.Tests/Program.cs`
- 생성: `docs/test-results/goals/G3L-001_once_per_turn_flags_unit_test_results.md`

## 읽기 전용으로 확인한 AS-IS 파일

- `DCGO/Assets/Scripts/Script/CEntity_EffectController.cs`
- `DCGO/Assets/Scripts/Script/TurnStateMachine.cs`
- `DCGO/Assets/Scripts/Script/ICardEffect.cs`
- `DCGO/Assets/Scripts/Script/AutoProcessing.cs`

## 테스트 명령

```powershell
.\.dotnet\dotnet.exe run --project .\tests\G3L-001.Once.per.turn.flag.helper.Tests\G3L-001.Once.per.turn.flag.helper.Tests.csproj
```

## 테스트 결과

- 전체: 10
- 통과: 10
- 실패: 0
- 스킵: 0

## 실패 상세

- 최종 실행 실패 없음.

## 테스트하지 못한 항목과 이유

- 실제 카드 효과별 `[Once Per Turn]` 텍스트 포팅과 개별 효과 연결은 G3L-001 범위가 아니므로 테스트하지 않았다.
- Unity `GameObject`/`MonoBehaviour` 기반 `CEntity_EffectController` 실행은 Headless 대체 계약 범위 밖이므로 읽기 전용 참조만 수행했다.

## 완료 기준 충족 근거

- `once flag helpers` public API가 첫 사용 허용, 중복 사용 차단, max count 제한, timing scope 분리, turn reset, remove use를 검증했다.
- `EffectContext`에 `CanUseEffectHelpers.UseCountThisTurnKey` 값을 기록해 기존 CanUse 계층과 연결 가능한 계약을 검증했다.
- 동일 reset 입력의 결과가 결정적으로 동일함을 검증했다.
- 원본 `DCGO/Assets/...` 파일은 수정하지 않았다.

## 미해결 리스크

- turn reset 호출 시점은 후속 turn lifecycle 연결 Goal에서 실제 루프와 연결되어야 한다.

## 완료 판정

- COMPLETE
- 완료 기준 `once flag 테스트 통과` 충족.
- 다음 Goal 진행 가능 여부: 가능.
