# G3F-002 Zone Query Helpers Unit Test Results

## 실행 일시

- 2026-06-25 19:28:05 +09:00

## Goal 범위

- Goal ID: G3F-002
- 목표: Zone query helper 포팅
- 작업 범위: library trash security source zone query helper 포팅
- 완료 기준: zone query 테스트 통과
- 선행 Goal 확인: `G3F-001_target_filtering_helpers_unit_test_results.md`에서 COMPLETE 확인

## 수정/생성 파일

- 생성: `src/HeadlessDCGO.Engine/Headless/Effects/ZoneQueryHelpers.cs`
- 생성: `tests/G3F-002.Zone.query.helper.Tests/G3F-002.Zone.query.helper.Tests.csproj`
- 생성: `tests/G3F-002.Zone.query.helper.Tests/Program.cs`
- 생성: `docs/test-results/goals/G3F-002_zone_query_helpers_unit_test_results.md`

## 참조한 AS-IS 파일

- 읽기 전용 확인: `DCGO/Assets/Scripts/Script/SelectCardEffect.cs`
- 읽기 전용 확인: `DCGO/Assets/Scripts/Script/Player.cs`
- 읽기 전용 확인: `DCGO/Assets/Scripts/Script/CardSource.cs`
- 원본 `DCGO/Assets/...` 파일 수정 없음

## 구현 요약

- `ZoneQueryRequest`, `ZoneQueryCard`, `ZoneQueryResult`, `ZoneQueryHelpers`를 추가했다.
- `Library`, `Trash`, `Security`, `Sources` 조회 API를 고정했다.
- `DigivolutionCards` 조회는 루트 카드의 `CardInstanceState.SourceIds` 순서를 사용한다.
- 숨김 zone(`Library`, `Security`)은 상대 viewer에게 카드 수와 위치는 유지하되 `DefinitionId`를 노출하지 않는다.
- `includeHidden`은 엔진 내부 처리용으로 숨김 zone 정의 접근을 허용한다.
- `Trash`와 진화원(source)은 공개 조회로 처리한다.
- 누락된 player/viewer/root/card와 `None`, `Custom`, root 없는 `DigivolutionCards` 조회는 throw 대신 failure result로 반환한다.

## 테스트 명령

```powershell
.\.dotnet\dotnet.exe run --project .\tests\G3F-002.Zone.query.helper.Tests\G3F-002.Zone.query.helper.Tests.csproj
```

## 테스트 결과

- 전체: 10
- 통과: 10
- 실패: 0
- 스킵: 0

통과한 테스트:

- G3F-002 goal row and predecessor are satisfied
- AS-IS zone query roots are recorded
- Library trash and security queries preserve zone order
- Opponent library and security hide definitions by default
- IncludeHidden exposes private zone definitions for engine use
- Trash query is public for opponent viewer
- Digivolution source query uses root source order
- Missing player viewer root or card returns failure
- Invalid zones return failure without throwing
- G3F-002 source files stay inside zone query scope

## 실패 상세

- 최종 실행 실패 없음

## 미해결 리스크

- `LinkedCards`, `Custom`, `Execution` 등은 이번 CSV 범위가 아닌 `library trash security source zone query helper` 밖이라 구현하지 않았다.
- 실제 카드 효과별 선택 조건과 target filtering 조합은 G3F-001/G3F-002 이후 Goal에서 연결되어야 한다.

## 완료 판정

- COMPLETE
