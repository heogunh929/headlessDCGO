# G1H-002 Card JSON Loader Unit Test Results

## 실행 일시

- 2026-06-25 10:03:47 +09:00

## Goal 범위

- Goal ID: G1H-002
- 목표: Card JSON loader
- 작업 범위: card JSON loading contract 확정
- 산출물: CardAssetJsonLoader
- 완료 기준: Card JSON loader 테스트 통과

## 선행 Goal 확인

- G1H-001: `docs/test-results/goals/G1H-001_card_repository_contract_unit_test_results.md` COMPLETE 확인

## 수정/생성 파일

- 수정: `src/HeadlessDCGO.Engine/Headless/DataLoading/CardAssetJsonLoader.cs`
- 생성: `tests/G1H-002.Card.JSON.loader.Tests/G1H-002.Card.JSON.loader.Tests.csproj`
- 생성: `tests/G1H-002.Card.JSON.loader.Tests/Program.cs`
- 생성: `docs/test-results/goals/G1H-002_card_json_loader_unit_test_results.md`

## 읽기 전용 참조 파일

- `docs/goal-specs/G1H-002_card_json_loader.md`
- `docs/headless_complete_goal_breakdown.csv`
- `docs/headless_complete_goal_breakdown_ko.csv`
- `docs/headless_complete_goal_breakdown_detailed_ko.csv`
- `docs/headless_complete_unit_test_plan.md`
- `docs/test-results/goals/G1H-001_card_repository_contract_unit_test_results.md`
- `DCGO/Assets/CardBaseEntity`
- `DCGO/Assets/Scripts/Script/DeckData.cs`
- `DCGO/Assets/Scripts/Script/DeckCodeUtility.cs`

## 구현 요약

- `CardAssetJsonLoader.LoadFile`와 `LoadFileAsync`가 JSON parse/schema 실패를 진단 가능한 `InvalidDataException`으로 반환하도록 고정했다.
- card `id`, `cardNumber`, `name`을 필수 schema 필드로 고정했다.
- `playCost`, `evolutionCost`는 integer 및 non-negative 제약을 갖도록 검증했다.
- directory loading은 path 정렬 기준을 명확히 해 반복 실행과 실패 순서가 deterministic하도록 고정했다.
- Unity image/prefab/audio path는 gameplay 필드가 아니라 metadata로만 보존되는 계약을 테스트했다.

## 테스트 명령

- `.\.dotnet\dotnet.exe run --project tests\G1H-002.Card.JSON.loader.Tests\G1H-002.Card.JSON.loader.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project tests\G1H-001.Card.repository.contract.Tests\G1H-001.Card.repository.contract.Tests.csproj`
- `.\.dotnet\dotnet.exe build src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj`

## 테스트 결과

| 대상 | 전체 | 통과 | 실패 | 스킵 |
| --- | ---: | ---: | ---: | ---: |
| G1H-002 Card JSON loader | 10 | 10 | 0 | 0 |
| G1H-001 Card repository contract 회귀 | 8 | 8 | 0 | 0 |
| HeadlessDCGO.Engine build | 1 | 1 | 0 | 0 |

## 실패 상세

- 없음

## 테스트하지 못한 항목과 이유

- 없음

## 미해결 리스크

- 이번 Goal은 card JSON loading contract 확정에 한정했다.
- Deck list loader, banlist loader, Unity asset exclusion scan은 후속 Goal 범위라 수행하지 않았다.
- 실제 카드 효과 포팅은 수행하지 않았다.
- 원본 `DCGO/Assets/...` 파일은 읽기 전용 참조만 했고 수정하지 않았다.

## 완료 기준 충족 근거

- `valid invalid schema deterministic failure 테스트`가 valid schema, missing required field, invalid JSON, invalid integer, negative integer, directory deterministic order, deterministic first failure를 직접 검증했다.
- G1H-001 회귀 테스트와 엔진 빌드가 통과했다.

## 다음 Goal 진행 가능 여부

- 가능

## 완료 판정

- COMPLETE
