# G3K-002 Timing priority helper 포팅

## 실행 일시

- 2026-06-25 20:33:07 +09:00

## 실행 환경

- 작업 디렉터리: `E:\headlessDCGO_new`
- 테스트 런타임: `.\.dotnet\dotnet.exe`

## 수정/생성 파일

- 생성: `src/HeadlessDCGO.Engine/Headless/Effects/TimingPriorityHelpers.cs`
- 생성: `src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons/TimingPriorityHelpers.cs`
- 생성: `tests/G3K-002.Timing.priority.helper.Tests/G3K-002.Timing.priority.helper.Tests.csproj`
- 생성: `tests/G3K-002.Timing.priority.helper.Tests/Program.cs`
- 생성: `docs/test-results/goals/G3K-002_timing_priority_helpers_unit_test_results.md`

## 읽기 전용으로 확인한 AS-IS 파일

- `DCGO/Assets/Scripts/Script/AutoProcessing.cs`
- `DCGO/Assets/Scripts/Script/MultipleSkills.cs`
- `DCGO/Assets/Scripts/Script/ContinuousController.cs`

## 테스트 명령

```powershell
.\.dotnet\dotnet.exe run --project .\tests\G3K-002.Timing.priority.helper.Tests\G3K-002.Timing.priority.helper.Tests.csproj
```

## 테스트 결과

- 전체: 10
- 통과: 10
- 실패: 0
- 스킵: 0

## 실패 상세

- 최종 실행 실패 없음.
- 구현 중 테스트 보조 CSV reader의 `List` 생성자 사용 오류를 수정한 뒤 재실행했다.

## 테스트하지 못한 항목과 이유

- 개별 카드 효과의 실제 발동 연결은 G3K-002 범위가 아니므로 테스트하지 않았다.
- Unity/Photon coroutine UI 실행 흐름은 Headless 대체 계약 범위 밖이라 실행하지 않고 AS-IS 파일을 읽기 전용으로만 확인했다.

## 완료 기준 충족 근거

- `timing priority helpers` public API가 강제/선택 효과 순서, 턴플레이어/비턴플레이어 순서, priority/sequence/input-order 안정 정렬을 검증했다.
- 불법 입력은 예외로 흘리지 않고 명시적 실패 결과를 반환하는 테스트를 통과했다.
- 동일 입력 반복 결과가 결정적으로 동일함을 검증했다.
- 원본 `DCGO/Assets/...` 파일은 수정하지 않았다.

## 미해결 리스크

- Unknown player 트리거는 명시적 `nonTurnPlayerId`가 제공된 경우 별도 목록으로 분리된다. 실제 다인전/특수 플레이어 정책은 후속 Goal에서 별도 정책이 필요할 수 있다.

## 완료 판정

- COMPLETE
- 완료 기준 `timing priority 테스트 통과` 충족.
- 다음 Goal 진행 가능 여부: 가능.
