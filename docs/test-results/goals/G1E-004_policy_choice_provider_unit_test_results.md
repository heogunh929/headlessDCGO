# G1E-004 PolicyChoiceProvider Unit Test Results

## 실행 일시

- 실행 일시: 2026-06-25 08:57:10 +09:00
- 실행 환경: Windows PowerShell, .NET 8, `E:\headlessDCGO_new`
- Goal ID: G1E-004
- 완료 기준: Policy provider 테스트 통과

## 수정/생성 파일

- 수정: `src/HeadlessDCGO.Engine/Headless/Choices/PolicyChoiceProvider.cs`
- 수정: `src/HeadlessDCGO.Engine/Headless/Choices/IChoiceProvider.cs`
- 생성: `tests/G1E-004.PolicyChoiceProvider.Tests/G1E-004.PolicyChoiceProvider.Tests.csproj`
- 생성: `tests/G1E-004.PolicyChoiceProvider.Tests/Program.cs`
- 생성: `docs/test-results/goals/G1E-004_policy_choice_provider_unit_test_results.md`

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

- `PolicyChoiceProvider`가 `ChoiceRequest`와 `CancellationToken`을 policy delegate로 전달하도록 계약을 고정했다.
- delegate가 반환한 `ChoiceResult`는 `ChoiceResult.ThrowIfInvalid(request)`로 검증한 뒤에만 반환한다.
- pre-canceled token은 delegate 호출 전에 `OperationCanceledException`으로 중단한다.
- delegate cancellation과 delegate exception은 호출자에게 그대로 전파한다.
- delegate가 null task 또는 null result를 반환하면 명확한 `InvalidOperationException`으로 실패한다.
- delegate가 없을 때의 default policy는 skip/count/minimum selectable candidate 순서로 deterministic한 합법 결과를 만든다.

## 테스트 의도

- CSV의 G1E-004 계약 행이 산출물, 테스트 범위, 결과 문서, 선행 Goal을 유지하는지 검증한다.
- policy delegate가 동일 request instance와 cancellation token을 받는지 검증한다.
- delegate 정상 결과, invalid 결과, null task/result, exception, cancellation을 각각 검증한다.
- default policy가 동일 입력에서 deterministic하고 G1E-002 validation을 통과하는 결과를 반환하는지 검증한다.
- AS-IS Unity 선택 파일은 읽기 전용 참조로만 확인하고 원본 파일을 수정하지 않았음을 검증한다.

## 테스트 명령 및 결과

| 명령 | 전체 | 통과 | 실패 | 스킵 | 결과 |
|---|---:|---:|---:|---:|---|
| `.\.dotnet\dotnet.exe run --project tests\G1E-004.PolicyChoiceProvider.Tests\G1E-004.PolicyChoiceProvider.Tests.csproj` | 11 | 11 | 0 | 0 | 통과 |
| `.\.dotnet\dotnet.exe run --project tests\G1E-002.ChoiceResult.validation.Tests\G1E-002.ChoiceResult.validation.Tests.csproj` | 9 | 9 | 0 | 0 | 통과 |
| `.\.dotnet\dotnet.exe run --project tests\G1E-001.Choice.schema.Tests\G1E-001.Choice.schema.Tests.csproj` | 8 | 8 | 0 | 0 | 통과 |
| `.\.dotnet\dotnet.exe run --project tests\G1E-003.ScriptedChoiceProvider.Tests\G1E-003.ScriptedChoiceProvider.Tests.csproj` | 10 | 10 | 0 | 0 | 통과 |
| `.\.dotnet\dotnet.exe build src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj` | 1 | 1 | 0 | 0 | 통과, 경고 0개/오류 0개 |

## 실패 상세

- 최종 실패 테스트 없음.
- 첫 G1E-004 실행에서 테스트 코드가 CSV 헤더를 `completion gate`로 잘못 조회해 1개 테스트가 실패했다.
- 같은 Goal 범위 안에서 테스트 코드를 `completion_gate`로 수정했고, 이후 G1E-004 테스트는 11/11 통과했다.
- 첫 G1E-004 실행의 프로젝트 빌드 중 기존 nullable warning이 표시되었으나, 최종 엔진 단독 빌드는 경고 0개/오류 0개로 통과했다.

## 범위 준수

- 원본 `DCGO/Assets/...` 파일은 수정하지 않았다.
- `DCGO/Assets` 최근 변경 파일 수: 0
- G1E-005 choice pause/resume 작업은 수행하지 않았다.
- 카드별 실제 룰/효과 포팅은 수행하지 않았다.

## 미해결 리스크

- `PolicyChoiceProvider`는 G1E-004 범위에 따라 delegate 기반 선택 provider 계약만 고정한다.
- 실제 RL policy 모델, action selection 정책, choice pause/resume runtime integration은 후속 Goal 범위로 남긴다.

## 완료 판정

- COMPLETE
- 근거: G1E-004 산출물 `PolicyChoiceProvider` 구현 완료, delegate cancellation error 단위테스트 11/11 통과, 선행 G1E-002 회귀 9/9 통과, 인접 선택 provider 회귀 18/18 통과, 엔진 빌드 통과.
