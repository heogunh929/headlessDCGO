# G2D-001 Card identity binding unit test results

## 실행 일시

- 2026-06-25 14:05:20 +09:00

## 수정/생성 파일

- `src/HeadlessDCGO.Engine/Headless/State/CardIdentityAdapter.cs`
- `tests/G2D-001.Card.identity.binding.Tests/G2D-001.Card.identity.binding.Tests.csproj`
- `tests/G2D-001.Card.identity.binding.Tests/Program.cs`
- `docs/test-results/goals/G2D-001_card_identity_binding_unit_test_results.md`

## 테스트 명령

- `.\.dotnet\dotnet.exe run --project .\tests\G2D-001.Card.identity.binding.Tests\G2D-001.Card.identity.binding.Tests.csproj`
- `.\.dotnet\dotnet.exe build .\src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj`
- `.\.dotnet\dotnet.exe run --project .\tests\G2C-001.Player.zone.ownership.Tests\G2C-001.Player.zone.ownership.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project .\tests\G1B-004.CardInstanceState.Tests\G1B-004.CardInstanceState.Tests.csproj`

## 전체/통과/실패/스킵 수

| 범위 | 전체 | 통과 | 실패 | 스킵 |
| --- | ---: | ---: | ---: | ---: |
| G2D-001 Card identity binding | 10 | 10 | 0 | 0 |
| HeadlessDCGO.Engine build | 1 | 1 | 0 | 0 |
| G2C-001 Player zone ownership 회귀 | 9 | 9 | 0 | 0 |
| G1B-004 CardInstanceState 회귀 | 8 | 8 | 0 | 0 |

## 실패 상세

- 최종 실행 실패 없음.
- 중간 실행에서 테스트 코드의 CSV 헤더명(`task_scope` 대신 `scope`)과 snapshot 배열 참조 비교 기대값이 실패했으며, G2D-001 테스트 코드 범위 안에서 수정 후 재실행 통과.

## 미해결 리스크

- 이 Goal은 CardController/CardObjectController의 실제 카드 효과나 룰 흐름을 포팅하지 않았다. 범위는 CardSource 식별 의미를 Headless `CardInstanceState`/player zone 상태에 연결하는 adapter 계약으로 제한했다.
- AS-IS의 Unity 오브젝트 생명주기, 시각 효과, 네트워크 동기화는 의도적으로 제외했다.

## 완료 판정

- COMPLETE
- 완료 기준 `card identity 테스트 통과` 충족.
