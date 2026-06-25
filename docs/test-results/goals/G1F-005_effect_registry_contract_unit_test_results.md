# G1F-005 EffectRegistry contract

## 실행 일시
- 2026-06-25 09:28:04 +09:00
- 환경: Windows PowerShell, .NET SDK via `.\.dotnet\dotnet.exe`

## 수정/생성 파일
- 생성: `src/HeadlessDCGO.Engine/Headless/Effects/EffectRegistry.cs`
- 생성: `tests/G1F-005.EffectRegistry.contract.Tests/G1F-005.EffectRegistry.contract.Tests.csproj`
- 생성: `tests/G1F-005.EffectRegistry.contract.Tests/Program.cs`
- 생성: `docs/test-results/goals/G1F-005_effect_registry_contract_unit_test_results.md`

## 읽기 전용으로 확인한 AS-IS 파일
- `DCGO/Assets/Scripts/Script/AutoProcessing.cs`
- `DCGO/Assets/Scripts/Script/Effects.cs`
- `DCGO/Assets/Scripts/Script/MultipleSkills.cs`

## 선행 Goal 확인
- `docs/test-results/goals/G1F-001_effect_context_schema_unit_test_results.md`: COMPLETE 확인

## 구현 요약
- `EffectRegistry` interface를 `Register`, `GetEffects`, `GetKeywordEffects`, `Find`, `IEffectQueryService` lookup 계약으로 고정했다.
- `InMemoryEffectRegistry`는 effect id 중복 등록을 거부하고, missing binding은 예외 대신 빈 결과 또는 false/null로 표현한다.
- `EffectBinding`은 G1F-001의 `EffectRequest`를 보존하고 keyword를 trim, deduplicate, immutable snapshot으로 저장한다.
- source entity와 timing이 모두 일치하는 binding 조회, keyword lookup, timing-only query service 조회를 지원한다.

## 테스트 명령
- `.\.dotnet\dotnet.exe run --project tests\G1F-005.EffectRegistry.contract.Tests\G1F-005.EffectRegistry.contract.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project tests\G1F-001.Effect.request.context.schema.Tests\G1F-001.Effect.request.context.schema.Tests.csproj`
- `.\.dotnet\dotnet.exe build src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj`

## 테스트 결과
| 범위 | 전체 | 통과 | 실패 | 스킵 |
|---|---:|---:|---:|---:|
| G1F-005 EffectRegistry contract | 11 | 11 | 0 | 0 |
| G1F-001 predecessor regression | 10 | 10 | 0 | 0 |
| Engine build | 1 | 1 | 0 | 0 |

## 실패 상세
- 없음.

## 참고 사항
- G1F-005 테스트 명령 실행 중 `HeadlessGameLoop.cs`와 `MetadataActionProcessor.cs`의 기존 nullable warning이 출력되었으나 실패는 없었다.
- 별도 엔진 빌드 명령은 경고 0개, 오류 0개로 완료되었다.

## 테스트하지 못한 항목
- 없음. CSV의 단위테스트 범위 `register lookup missing binding 테스트`를 전용 테스트로 검증했다.

## 미해결 리스크
- 실제 카드별 effect resolver 연결과 카드 효과 포팅은 후속 Goal/Phase 범위이므로 수행하지 않았다.
- registry를 `EngineContext` 기본 서비스로 노출하는 통합은 별도 지시 Goal 범위에서 다루어야 한다.

## 완료 기준 충족 근거
- register, duplicate rejection, source+timing lookup, keyword lookup, missing binding behavior, query service contract, input validation을 단위테스트로 검증했다.
- 원본 `DCGO/Assets/...` 파일은 읽기 전용으로만 확인했고 수정하지 않았다.

## 완료 판정
- COMPLETE
