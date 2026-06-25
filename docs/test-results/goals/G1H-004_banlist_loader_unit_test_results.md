# G1H-004 Banlist Loader Unit Test Results

## 실행 일시

- 2026-06-25 10:12:08 +09:00

## Goal 범위

- Goal ID: G1H-004
- 목표: Banlist loader
- 작업 범위: banlist loading contract 확정
- 산출물: BanlistLoader
- 완료 기준: Banlist loader 테스트 통과

## 선행 Goal 확인

- G1H-001: `docs/test-results/goals/G1H-001_card_repository_contract_unit_test_results.md` COMPLETE 확인

## 수정/생성 파일

- 수정: `src/HeadlessDCGO.Engine/Headless/DataLoading/BanlistLoader.cs`
- 생성: `tests/G1H-004.Banlist.loader.Tests/G1H-004.Banlist.loader.Tests.csproj`
- 생성: `tests/G1H-004.Banlist.loader.Tests/Program.cs`
- 생성: `docs/test-results/goals/G1H-004_banlist_loader_unit_test_results.md`

## 읽기 전용 참조 파일

- `docs/goal-specs/G1H-004_banlist_loader.md`
- `docs/headless_complete_goal_breakdown.csv`
- `docs/headless_complete_goal_breakdown_ko.csv`
- `docs/test-results/goals/G1H-001_card_repository_contract_unit_test_results.md`
- `DCGO/Assets/Scripts/Script/ContinuousController.cs`
- `DCGO/Assets/Scripts/Script/GameplayOption.cs`

## 구현 요약

- `BanlistLoader.ParseCode`를 추가해 banlist code/string 입력을 파일 입력과 같은 규칙으로 파싱하도록 고정했다.
- banlist entry 파싱 실패에 line number를 포함하는 `InvalidDataException` 계약을 추가했다.
- file load 실패에는 파일 경로와 내부 line diagnostic을 함께 포함하도록 고정했다.
- invalid limit을 조용히 banned 처리하지 않고 명확한 실패로 반환하도록 고정했다.
- `BanlistLoader.cs`의 placeholder TODO를 제거하고 Unity/Resources/ScriptableObject 의존 없이 동작하도록 유지했다.

## 테스트 명령

- `.\.dotnet\dotnet.exe run --project tests\G1H-004.Banlist.loader.Tests\G1H-004.Banlist.loader.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project tests\G1H-001.Card.repository.contract.Tests\G1H-001.Card.repository.contract.Tests.csproj`
- `.\.dotnet\dotnet.exe build src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj`

## 테스트 결과

| 대상 | 전체 | 통과 | 실패 | 스킵 |
| --- | ---: | ---: | ---: | ---: |
| G1H-004 Banlist loader | 10 | 10 | 0 | 0 |
| G1H-001 Card repository contract 회귀 | 8 | 8 | 0 | 0 |
| HeadlessDCGO.Engine build | 1 | 1 | 0 | 0 |

## 실패 상세

- 없음

## 테스트하지 못한 항목과 이유

- 없음

## 미해결 리스크

- 이번 Goal은 banlist loading contract 확정에 한정했다.
- Unity asset exclusion scan은 후속 Goal 범위라 수행하지 않았다.
- 원본 온라인 banlist 전체 schema 포팅은 실제 데이터 고정 Goal이 열릴 때 수행한다.
- 원본 `DCGO/Assets/...` 파일은 읽기 전용 참조만 했고 수정하지 않았다.

## 완료 기준 충족 근거

- `valid invalid banlist 테스트`가 valid banlist parsing, invalid entry, file path/line diagnostic, async load, deterministic repeat, DeckValidator limit 적용을 직접 검증했다.
- G1H-001 회귀 테스트와 엔진 빌드가 통과했다.

## 다음 Goal 진행 가능 여부

- 가능

## 완료 판정

- COMPLETE
