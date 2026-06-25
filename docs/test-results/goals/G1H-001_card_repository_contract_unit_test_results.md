# G1H-001 Card Repository Contract Unit Test Results

## 실행 일시

- 2026-06-25 09:57:24 +09:00

## Goal 범위

- Goal ID: G1H-001
- 목표: Card repository contract
- 작업 범위: card lookup contract 확정
- 산출물: ICardRepository CardRecord
- 완료 기준: Card repository 테스트 통과

## 선행 Goal 확인

- G1B-001: `docs/test-results/goals/G1B-001_stable_ids_unit_test_results.md` COMPLETE 확인

## 수정/생성 파일

- 수정: `src/HeadlessDCGO.Engine/Headless/Services/CardRecord.cs`
- 생성: `src/HeadlessDCGO.Engine/Headless/Services/CardQuery.cs`
- 수정: `src/HeadlessDCGO.Engine/Headless/Services/ICardRepository.cs`
- 수정: `src/HeadlessDCGO.Engine/Headless/Services/InMemoryCardRepository.cs`
- 수정: `src/HeadlessDCGO.Engine/Headless/DataLoading/CardDatabase.cs`
- 수정: `src/HeadlessDCGO.Engine/Headless/DataLoading/CardAssetJsonLoader.cs`
- 생성: `tests/G1H-001.Card.repository.contract.Tests/G1H-001.Card.repository.contract.Tests.csproj`
- 생성: `tests/G1H-001.Card.repository.contract.Tests/Program.cs`
- 생성: `docs/test-results/goals/G1H-001_card_repository_contract_unit_test_results.md`

## 읽기 전용 참조 파일

- `docs/goal-specs/G1H-001_card_repository_contract.md`
- `docs/headless_complete_goal_breakdown.csv`
- `docs/headless_complete_goal_breakdown_ko.csv`
- `docs/headless_complete_goal_breakdown_detailed_ko.csv`
- `docs/headless_complete_unit_test_plan.md`
- `docs/headless_complete_architecture_design.md`
- `docs/headless_complete_porting_sequence.md`
- `docs/test-results/goals/G1B-001_stable_ids_unit_test_results.md`
- `DCGO/Assets/CardBaseEntity`
- `DCGO/Assets/Scripts/Script/DeckData.cs`
- `DCGO/Assets/Scripts/Script/DeckCodeUtility.cs`

## 구현 요약

- `CardRecord`에 card type, play cost, evolution cost, evolution condition, effect binding key lookup 필드를 추가했다.
- `ICardRepository`에 실패 진단용 `GetCard`와 조건 조회용 `Query(CardQuery)` 계약을 추가했다.
- `InMemoryCardRepository`와 `CardDatabase`가 get/missing/query 계약을 구현하도록 갱신했다.
- `CardAssetJsonLoader`가 기존 JSON 입력에서 새 `CardRecord` lookup 필드를 채우도록 매핑했다.
- Unity prefab/image/audio path는 gameplay 필드가 아니라 metadata로만 보존되도록 테스트했다.

## 테스트 명령

- `.\.dotnet\dotnet.exe run --project tests\G1H-001.Card.repository.contract.Tests\G1H-001.Card.repository.contract.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project tests\G1B-001.Stable.ID.entity.registry.Tests\G1B-001.Stable.ID.entity.registry.Tests.csproj`
- `.\.dotnet\dotnet.exe build src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj`

## 테스트 결과

| 대상 | 전체 | 통과 | 실패 | 스킵 |
| --- | ---: | ---: | ---: | ---: |
| G1H-001 Card repository contract | 8 | 8 | 0 | 0 |
| G1B-001 Stable ID 회귀 | 7 | 7 | 0 | 0 |
| HeadlessDCGO.Engine build | 1 | 1 | 0 | 0 |

## 실패 상세

- 없음

## 테스트하지 못한 항목과 이유

- 없음

## 미해결 리스크

- 이번 Goal은 card lookup repository 계약 고정에 한정했다.
- 전체 card JSON schema 검증, deck list loader, banlist loader의 세부 계약은 후속 Goal 범위라 수행하지 않았다.
- 실제 카드 효과 포팅은 수행하지 않았다.
- 원본 `DCGO/Assets/...` 파일은 읽기 전용 참조만 했고 수정하지 않았다.

## 완료 기준 충족 근거

- `get missing query 테스트`가 `GetCard`, `TryGetCard`, `Query(CardQuery)`의 정상/실패/결정성 동작을 직접 검증했다.
- CardRecord lookup 필드와 JSON 매핑이 테스트로 검증되었다.
- G1B-001 회귀 테스트와 엔진 빌드가 통과했다.

## 다음 Goal 진행 가능 여부

- 가능

## 완료 판정

- COMPLETE
