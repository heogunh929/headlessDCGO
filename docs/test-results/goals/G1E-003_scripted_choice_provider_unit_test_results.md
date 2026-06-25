# G1E-003 ScriptedChoiceProvider Unit Test Results

## 실행 일시

- 실행 일시: 2026-06-25 08:51:54 +09:00
- 실행 환경: Windows PowerShell, .NET 8, `E:\headlessDCGO_new`
- Goal ID: G1E-003
- 완료 기준: Scripted provider 테스트 통과

## 수정/생성 파일

- 수정: `src/HeadlessDCGO.Engine/Headless/Choices/ScriptedChoiceProvider.cs`
- 생성: `tests/G1E-003.ScriptedChoiceProvider.Tests/G1E-003.ScriptedChoiceProvider.Tests.csproj`
- 생성: `tests/G1E-003.ScriptedChoiceProvider.Tests/Program.cs`
- 생성: `docs/test-results/goals/G1E-003_scripted_choice_provider_unit_test_results.md`

## 읽기 전용 AS-IS 확인 파일

- `DCGO/Assets/Scripts/Script/SelectCardEffect.cs`
- `DCGO/Assets/Scripts/Script/SelectPermanentEffect.cs`
- `DCGO/Assets/Scripts/Script/SelectCountEffect.cs`
- `DCGO/Assets/Scripts/Script/SelectHandEffect.cs`
- `DCGO/Assets/Scripts/Script/SelectAttackEffect.cs`
- `DCGO/Assets/Scripts/Script/PlayerSelection/CardSelection.cs`
- `DCGO/Assets/Scripts/Script/PlayerSelection/PermanentSelection.cs`
- `DCGO/Assets/Scripts/Script/PlayerSelection/ValueSelection.cs`

## 구현 요약

- `ScriptedChoiceProvider`가 scripted `ChoiceResult`를 FIFO 순서로 deterministic하게 반환하도록 고정했다.
- queued result는 `ChoiceResult.ThrowIfInvalid(request)`로 검증한 뒤에만 dequeue한다.
- invalid scripted result, cancellation, null input은 queue를 소비하지 않는다.
- queue가 비었을 때 fallback은 `CanSkip`, count request의 `MinCount`, selectable candidate의 입력 순서를 기준으로 deterministic하게 반환한다.
- `UnityEngine`/`MonoBehaviour` 의존성 및 TODO placeholder 없이 Headless 선택 계약 안에서만 구현했다.

## 테스트 의도

- CSV의 G1E-003 계약 행이 산출물, 테스트 범위, 결과 문서, 선행 Goal을 유지하는지 검증한다.
- scripted queue가 동일 입력에서 항상 같은 순서로 결과를 반환하는지 검증한다.
- invalid result와 cancellation이 queue를 조기 소비하지 않는지 검증한다.
- empty queue fallback이 skip/count/card selection을 deterministic하게 생성하는지 검증한다.
- AS-IS Unity 선택 파일은 읽기 전용 참조로만 확인하고 원본 파일을 수정하지 않았음을 검증한다.

## 테스트 명령 및 결과

| 명령 | 전체 | 통과 | 실패 | 스킵 | 결과 |
|---|---:|---:|---:|---:|---|
| `.\.dotnet\dotnet.exe run --project tests\G1E-003.ScriptedChoiceProvider.Tests\G1E-003.ScriptedChoiceProvider.Tests.csproj` | 10 | 10 | 0 | 0 | 통과 |
| `.\.dotnet\dotnet.exe run --project tests\G1E-002.ChoiceResult.validation.Tests\G1E-002.ChoiceResult.validation.Tests.csproj` | 9 | 9 | 0 | 0 | 통과 |
| `.\.dotnet\dotnet.exe run --project tests\G1E-001.Choice.schema.Tests\G1E-001.Choice.schema.Tests.csproj` | 8 | 8 | 0 | 0 | 통과 |
| `.\.dotnet\dotnet.exe build src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj` | 1 | 1 | 0 | 0 | 통과, 경고 0개/오류 0개 |

## 실패 상세

- 최종 실패 테스트 없음.
- G1E-003 최초 실행에서 프로젝트 복원/빌드 중 기존 nullable warning이 표시되었으나, 테스트는 10/10 통과했다.
- 이후 엔진 단독 빌드는 경고 0개/오류 0개로 통과했다.

## 범위 준수

- 원본 `DCGO/Assets/...` 파일은 수정하지 않았다.
- `DCGO/Assets` 최근 변경 파일 수: 0
- G1E-004 `PolicyChoiceProvider` 및 G1E-005 choice pause/resume 작업은 수행하지 않았다.
- 카드별 실제 룰/효과 포팅은 수행하지 않았다.

## 미해결 리스크

- `ScriptedChoiceProvider`는 G1E-003 범위에 따라 deterministic scripted provider만 고정한다.
- 실제 policy 기반 선택 provider와 choice pause/resume runtime integration은 후속 Goal 범위로 남긴다.

## 완료 판정

- COMPLETE
- 근거: G1E-003 산출물 `ScriptedChoiceProvider` 구현 완료, queued result deterministic 단위테스트 10/10 통과, 선행 선택 계약 회귀 17/17 통과, 엔진 빌드 통과.
