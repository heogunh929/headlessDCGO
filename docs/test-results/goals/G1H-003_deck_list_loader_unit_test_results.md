# G1H-003 Deck List Loader Unit Test Results

## 실행 일시

- 2026-06-25 10:08:01 +09:00

## Goal 범위

- Goal ID: G1H-003
- 목표: Deck list loader
- 작업 범위: decklist parsing contract 확정
- 산출물: DeckListLoader
- 완료 기준: Deck loader 테스트 통과

## 선행 Goal 확인

- G1H-001: `docs/test-results/goals/G1H-001_card_repository_contract_unit_test_results.md` COMPLETE 확인

## 수정/생성 파일

- 수정: `src/HeadlessDCGO.Engine/Headless/DataLoading/DeckListLoader.cs`
- 생성: `tests/G1H-003.Deck.list.loader.Tests/G1H-003.Deck.list.loader.Tests.csproj`
- 생성: `tests/G1H-003.Deck.list.loader.Tests/Program.cs`
- 생성: `docs/test-results/goals/G1H-003_deck_list_loader_unit_test_results.md`

## 읽기 전용 참조 파일

- `docs/goal-specs/G1H-003_deck_list_loader.md`
- `docs/headless_complete_goal_breakdown.csv`
- `docs/headless_complete_goal_breakdown_ko.csv`
- `docs/test-results/goals/G1H-001_card_repository_contract_unit_test_results.md`
- `DCGO/Assets/Scripts/Script/DeckData.cs`
- `DCGO/Assets/Scripts/Script/DeckCodeUtility.cs`

## 구현 요약

- `DeckListLoader.ParseCode`를 추가해 deck code/string 입력을 파일 입력과 같은 규칙으로 파싱하도록 고정했다.
- deck entry 파싱 실패에 line number를 포함하는 `InvalidDataException` 계약을 추가했다.
- file load 실패에는 파일 경로와 내부 line diagnostic을 함께 포함하도록 고정했다.
- `DeckListLoader.cs`의 placeholder TODO를 제거하고 Unity/Resources/ScriptableObject 의존 없이 동작하도록 유지했다.
- loader 출력이 기존 `DeckValidator`를 통해 card count limit 검증에 연결됨을 테스트했다.

## 테스트 명령

- `.\.dotnet\dotnet.exe run --project tests\G1H-003.Deck.list.loader.Tests\G1H-003.Deck.list.loader.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project tests\G1H-001.Card.repository.contract.Tests\G1H-001.Card.repository.contract.Tests.csproj`
- `.\.dotnet\dotnet.exe build src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj`

## 테스트 결과

| 대상 | 전체 | 통과 | 실패 | 스킵 |
| --- | ---: | ---: | ---: | ---: |
| G1H-003 Deck list loader | 10 | 10 | 0 | 0 |
| G1H-001 Card repository contract 회귀 | 8 | 8 | 0 | 0 |
| HeadlessDCGO.Engine build | 1 | 1 | 0 | 0 |

## 실패 상세

- 없음

## 테스트하지 못한 항목과 이유

- 없음

## 미해결 리스크

- 이번 Goal은 decklist parsing contract 확정에 한정했다.
- banlist loader와 Unity asset exclusion scan은 후속 Goal 범위라 수행하지 않았다.
- DCGO 원본 압축 deck code의 완전한 bit/base 변환 포팅은 Phase 1 범위를 넘어 실제 Assets 포팅 시점으로 남겼다.
- 원본 `DCGO/Assets/...` 파일은 읽기 전용 참조만 했고 수정하지 않았다.

## 완료 기준 충족 근거

- `deck code file invalid entry 테스트`가 deck code parsing, file loading, invalid entry, path/line diagnostic, deterministic repeat, validator count-limit 연결을 직접 검증했다.
- G1H-001 회귀 테스트와 엔진 빌드가 통과했다.

## 다음 Goal 진행 가능 여부

- 가능

## 완료 판정

- COMPLETE
