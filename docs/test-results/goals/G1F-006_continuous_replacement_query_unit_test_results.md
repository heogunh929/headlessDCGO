# G1F-006 Continuous Replacement query contract

## 실행 일시
- 2026-06-25 09:34:58 +09:00
- 환경: Windows PowerShell, .NET SDK via `.\.dotnet\dotnet.exe`

## 수정/생성 파일
- 수정: `src/HeadlessDCGO.Engine/Headless/Services/IEffectQueryService.cs`
- 수정: `src/HeadlessDCGO.Engine/Headless/Services/InMemoryEffectQueryService.cs`
- 수정: `src/HeadlessDCGO.Engine/Headless/Effects/EffectRegistry.cs`
- 생성: `tests/G1F-006.Continuous.Replacement.query.contract.Tests/G1F-006.Continuous.Replacement.query.contract.Tests.csproj`
- 생성: `tests/G1F-006.Continuous.Replacement.query.contract.Tests/Program.cs`
- 생성: `docs/test-results/goals/G1F-006_continuous_replacement_query_unit_test_results.md`

## 읽기 전용으로 확인한 AS-IS 파일
- `DCGO/Assets/Scripts/Script/AutoProcessing.cs`
- `DCGO/Assets/Scripts/Script/Effects.cs`
- `DCGO/Assets/Scripts/Script/MultipleSkills.cs`
- `DCGO/Assets/Scripts/Script/ContinuousController.cs`

## 선행 Goal 확인
- `docs/test-results/goals/G1F-005_effect_registry_contract_unit_test_results.md`: COMPLETE 확인

## 구현 요약
- `IEffectQueryService`에 continuous, replacement, modifier, restriction role별 query 계약을 추가했다.
- `EffectQueryRole`과 `EffectQueryContext`를 추가해 query scope, source entity, player, target entity 경계를 명시했다.
- `EffectBinding`에 query role과 query scope snapshot을 추가하고, trim/deduplicate/validation 계약을 고정했다.
- `InMemoryEffectRegistry`는 role + scope + context가 모두 맞는 request만 반환하도록 구현했다.
- 기본 `InMemoryEffectQueryService`는 role-specific query를 빈 결과로 명시 반환해 missing boundary를 예외 없이 표현한다.

## 테스트 명령
- `.\.dotnet\dotnet.exe run --project tests\G1F-006.Continuous.Replacement.query.contract.Tests\G1F-006.Continuous.Replacement.query.contract.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project tests\G1F-005.EffectRegistry.contract.Tests\G1F-005.EffectRegistry.contract.Tests.csproj`
- `.\.dotnet\dotnet.exe build src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj`

## 테스트 결과
| 범위 | 전체 | 통과 | 실패 | 스킵 |
|---|---:|---:|---:|---:|
| G1F-006 Continuous Replacement query contract | 11 | 11 | 0 | 0 |
| G1F-005 predecessor regression | 11 | 11 | 0 | 0 |
| Engine build | 1 | 1 | 0 | 0 |

## 실패 상세
- 없음.

## 참고 사항
- G1F-006 테스트 명령 실행 중 `HeadlessGameLoop.cs`와 `MetadataActionProcessor.cs`의 기존 nullable warning이 출력되었으나 실패는 없었다.
- 별도 엔진 빌드 명령은 경고 0개, 오류 0개로 완료되었다.

## 테스트하지 못한 항목
- 없음. CSV의 단위테스트 범위 `modifier restriction replacement query 테스트`를 전용 테스트로 검증했다.

## 미해결 리스크
- 실제 카드별 continuous/replacement/modifier/restriction 효과 포팅은 후속 Phase/Goal 범위이므로 수행하지 않았다.
- query scope 명칭의 최종 게임 룰 매핑은 실제 카드 효과 등록 단계에서 확장해야 한다.

## 완료 기준 충족 근거
- continuous, replacement, modifier, restriction query role isolation, missing boundary, source/player/target context filtering, input validation을 단위테스트로 검증했다.
- 원본 `DCGO/Assets/...` 파일은 읽기 전용으로만 확인했고 수정하지 않았다.

## 완료 판정
- COMPLETE
