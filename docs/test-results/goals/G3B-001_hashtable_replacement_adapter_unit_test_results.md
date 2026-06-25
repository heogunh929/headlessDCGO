# G3B-001 Hashtable replacement adapter unit test results

## 실행 일시

- 2026-06-25 18:32:01 +09:00

## Goal

- Goal ID: G3B-001
- 목표: Hashtable 제거 adapter 포팅
- 작업 범위: GetFromHashtable와 HashtableSetting typed 전환
- 산출물: typed context adapter
- 완료 기준: Hashtable 대체 효과 테스트 통과

## 선행 Goal 확인

- G3A-002 결과 문서: `docs/test-results/goals/G3A-002_skill_info_unit_test_results.md`
- 확인 결과: COMPLETE

## 수정/생성 파일

- 생성: `src/HeadlessDCGO.Engine/Headless/Effects/EffectContextAdapter.cs`
- 생성: `tests/G3B-001.Hashtable.replacement.adapter.Tests/G3B-001.Hashtable.replacement.adapter.Tests.csproj`
- 생성: `tests/G3B-001.Hashtable.replacement.adapter.Tests/Program.cs`
- 생성: `docs/test-results/goals/G3B-001_hashtable_replacement_adapter_unit_test_results.md`

## 읽기 전용 참조 파일

- `docs/goal-specs/G3B-001_hashtable_제거_adapter_포팅.md`
- `docs/headless_complete_goal_breakdown.csv`
- `docs/headless_complete_goal_breakdown_ko.csv`
- `docs/headless_complete_goal_breakdown_detailed_ko.csv`
- `DCGO/Assets/Scripts/Script/CardEffectCommons/HashtableSetting.cs`
- `DCGO/Assets/Scripts/Script/AutoProcessing.cs`
- `DCGO/Assets/Scripts/Script/CardController.cs`

## 구현 요약

- `EffectContextAdapterKeys`로 Headless 구조 키를 고정했다.
- AS-IS `HashtableSetting`의 대표 문자열 키를 `LegacyAliases`로 명시 매핑했다.
- `EffectContextAdapter.TryCreate`로 source player, owner player, source entity, trigger entity, target entity 목록을 `EffectContext` 필드로 변환한다.
- 구조 키는 `EffectContext.Values`에 중복 저장하지 않고, DPZero/isEvolution 같은 추가 값만 typed payload로 보존한다.
- 누락/잘못된 구조 값은 `EffectContextAdapterResult`의 실패 코드와 메시지로 반환한다.
- `ExportValues`로 adapted context를 다시 결정적으로 내보내는 계약을 추가했다.

## 테스트 명령

- `.\.dotnet\dotnet.exe run --project tests\G3B-001.Hashtable.replacement.adapter.Tests\G3B-001.Hashtable.replacement.adapter.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project tests\G3A-002.SkillInfo.Tests\G3A-002.SkillInfo.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project tests\G3A-001.ICardEffect.contract.Tests\G3A-001.ICardEffect.contract.Tests.csproj`
- `.\.dotnet\dotnet.exe build src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj`

## 테스트 결과

| 명령 | 전체 | 통과 | 실패 | 스킵 |
| --- | ---: | ---: | ---: | ---: |
| G3B-001 Hashtable replacement adapter tests | 9 | 9 | 0 | 0 |
| G3A-002 SkillInfo regression tests | 11 | 11 | 0 | 0 |
| G3A-001 ICardEffect regression tests | 10 | 10 | 0 | 0 |
| HeadlessDCGO.Engine build | 1 | 1 | 0 | 0 |

## 실패 상세

- 없음.

## 경고

- G3B-001 전용 테스트 재실행 중 기존 Runtime 파일의 nullable 경고가 출력되었다.
- 최종 `HeadlessDCGO.Engine` 빌드는 경고 0개, 오류 0개로 완료되었다.
- 기존 Runtime nullable 경고는 이번 Goal 범위 밖이므로 수정하지 않았다.

## 미해결 리스크

- AS-IS `Hashtable` 키 전체의 의미 포팅은 후속 카드 효과 포팅 단계에서 추가 검증이 필요하다.
- 이번 Goal은 typed adapter 계약과 대표 legacy alias 매핑만 고정하며, 실제 카드 효과 룰 구현은 수행하지 않았다.

## 완료 판정

- COMPLETE
