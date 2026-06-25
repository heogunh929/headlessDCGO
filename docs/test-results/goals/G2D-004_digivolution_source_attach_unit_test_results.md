# G2D-004 Digivolution source attach 포팅

## 실행 일시

- 2026-06-25 14:22:18 +09:00

## 실행 환경

- Workspace: `E:\headlessDCGO_new`
- Runtime: `.NET 8.0`

## 수정/생성 파일

- `src/HeadlessDCGO.Engine/Headless/Runtime/DigivolutionSourceStackPort.cs`
- `tests/G2D-004.Digivolution.source.attach.Tests/G2D-004.Digivolution.source.attach.Tests.csproj`
- `tests/G2D-004.Digivolution.source.attach.Tests/Program.cs`
- `docs/test-results/goals/G2D-004_digivolution_source_attach_unit_test_results.md`

## 읽기 전용으로 확인한 AS-IS 파일

- `DCGO/Assets/Scripts/Script/Permanent.cs`
- `DCGO/Assets/Scripts/Script/CardController.cs`
- `DCGO/Assets/Scripts/Script/CardObjectController.cs`

## 테스트 명령

- `.\.dotnet\dotnet.exe run --project .\tests\G2D-004.Digivolution.source.attach.Tests\G2D-004.Digivolution.source.attach.Tests.csproj`
- `.\.dotnet\dotnet.exe build .\src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj`
- `.\.dotnet\dotnet.exe run --project .\tests\G2D-003.Suspend.reveal.state.Tests\G2D-003.Suspend.reveal.state.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project .\tests\G2D-001.Card.identity.binding.Tests\G2D-001.Card.identity.binding.Tests.csproj`

## 전체/통과/실패/스킵 수

| 범위 | 전체 | 통과 | 실패 | 스킵 |
| --- | ---: | ---: | ---: | ---: |
| G2D-004 Digivolution source attach | 10 | 10 | 0 | 0 |
| HeadlessDCGO.Engine build | 1 | 1 | 0 | 0 |
| G2D-003 Suspend reveal state 회귀 | 10 | 10 | 0 | 0 |
| G2D-001 Card identity binding 회귀 | 10 | 10 | 0 | 0 |

## 실패 상세 및 수정 여부

- 최종 실행 실패 없음.

## 테스트하지 못한 항목과 이유

- Unity 시각 효과, `ShowingPermanentCard.ShowAddDigivolutionCardEffect`, `RemoveDigivolveRootEffect` 애니메이션은 Headless runtime 범위 밖이므로 테스트하지 않았다.
- 실제 카드 효과 발동 포팅은 이 Goal 범위가 아니므로 `StateChanged` 이벤트, deterministic trace, `EffectContext` payload 연결까지만 검증했다.

## 완료 기준 충족 근거

- `DigivolutionSourceStackPort`가 attach top, attach bottom, detach 요청을 `CardInstanceState.SourceIds`의 stable order mutation으로 연결한다.
- 동일 source 재배치 시 중복 없이 순서를 갱신하고, 다른 카드 스택에 있던 source는 원래 스택에서 제거한다.
- self source, cross-owner source, missing source, duplicate input, token target, unattached detach를 실패 결과로 반환하며 원본 상태와 이벤트 목록을 보존한다.
- 완료 기준 `진화원 스택 테스트 통과`를 G2D-004 전용 테스트 10/10 통과로 충족했다.

## 다음 Goal 진행 가능 여부

- 가능. G2D-004는 COMPLETE이며 선행 Goal G2D-003 완료 상태도 확인했다.

## 완료 판정

- COMPLETE
