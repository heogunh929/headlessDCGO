# G1F-004 TimingWindowResolver contract

## 실행 일시
- 2026-06-25 09:23:39 +09:00
- 환경: Windows PowerShell, .NET SDK via `.\.dotnet\dotnet.exe`

## 수정/생성 파일
- 생성: `src/HeadlessDCGO.Engine/Headless/Rules/TimingWindowResolver.cs`
- 생성: `tests/G1F-004.TimingWindowResolver.contract.Tests/G1F-004.TimingWindowResolver.contract.Tests.csproj`
- 생성: `tests/G1F-004.TimingWindowResolver.contract.Tests/Program.cs`
- 생성: `docs/test-results/goals/G1F-004_timing_window_contract_unit_test_results.md`

## 읽기 전용으로 확인한 AS-IS 파일
- `DCGO/Assets/Scripts/Script/AutoProcessing.cs`
- `DCGO/Assets/Scripts/Script/Effects.cs`
- `DCGO/Assets/Scripts/Script/MultipleSkills.cs`

## 선행 Goal 확인
- `docs/test-results/goals/G1F-003_effect_scheduler_unit_test_results.md`: COMPLETE 확인

## 구현 요약
- `TimingWindowResolver` interface를 `CollectTriggers`, `SortTriggers`, `OpenWindow` 계약으로 고정했다.
- `DefaultTimingWindowResolver`는 `IEffectQueryService`에서 timing이 일치하는 `EffectRequest`만 수집하고, `TimingWindowTrigger`로 변환한다.
- trigger ordering은 mandatory 먼저, priority 오름차순, sequence 오름차순, 원래 입력 순서 순으로 결정되도록 고정했다.
- `OpenWindow`는 정렬된 trigger를 `PendingEffect`로 변환해 G1F-003 `EffectScheduler.Enqueue(request, mode)`와 연결 가능한 형태를 제공한다.
- `TimingWindow`, `TimingWindowTrigger`, `TimingWindowTriggerKind` 입력 검증을 추가했다.

## 테스트 명령
- `.\.dotnet\dotnet.exe run --project tests\G1F-004.TimingWindowResolver.contract.Tests\G1F-004.TimingWindowResolver.contract.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project tests\G1F-003.EffectScheduler.Tests\G1F-003.EffectScheduler.Tests.csproj`
- `.\.dotnet\dotnet.exe build src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj`

## 테스트 결과
| 범위 | 전체 | 통과 | 실패 | 스킵 |
|---|---:|---:|---:|---:|
| G1F-004 TimingWindowResolver contract | 11 | 11 | 0 | 0 |
| G1F-003 predecessor regression | 11 | 11 | 0 | 0 |
| Engine build | 1 | 1 | 0 | 0 |

## 실패 상세
- 없음.

## 참고 사항
- G1F-004 테스트 명령 실행 중 `HeadlessGameLoop.cs`와 `MetadataActionProcessor.cs`의 기존 nullable warning이 출력되었으나 실패는 없었다.
- 별도 엔진 빌드 명령은 경고 0개, 오류 0개로 완료되었다.

## 테스트하지 못한 항목
- 없음. CSV의 단위테스트 범위 `trigger collection ordering contract 테스트`를 전용 테스트로 검증했다.

## 미해결 리스크
- 실제 카드별 trigger/effect 등록과 `EffectRegistry` 연동은 후속 Goal 범위이므로 수행하지 않았다.
- timing window의 게임별 세부 명칭과 카드별 optional 선택 흐름은 실제 카드 효과 포팅 단계에서 확장해야 한다.

## 완료 기준 충족 근거
- trigger 수집 필터링, ordering contract, deterministic repeatability, empty window, 입력 검증, scheduler 연결 형태를 단위테스트로 검증했다.
- 원본 `DCGO/Assets/...` 파일은 읽기 전용으로만 확인했고 수정하지 않았다.

## 완료 판정
- COMPLETE
