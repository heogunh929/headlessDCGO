# G3C-001 Trigger condition helper unit test results

## 실행 일시

- 2026-06-25 18:37:14 +09:00

## Goal

- Goal ID: G3C-001
- 목표: Trigger condition helper 포팅
- 작업 범위: on play on digivolve when attacking 조건 헬퍼
- 산출물: trigger condition helpers
- 완료 기준: trigger condition 테스트 통과

## 선행 Goal 확인

- G3B-001 결과 문서: `docs/test-results/goals/G3B-001_hashtable_replacement_adapter_unit_test_results.md`
- 확인 결과: COMPLETE

## 수정/생성 파일

- 생성: `src/HeadlessDCGO.Engine/Headless/Effects/TriggerConditionHelpers.cs`
- 생성: `tests/G3C-001.Trigger.condition.helper.Tests/G3C-001.Trigger.condition.helper.Tests.csproj`
- 생성: `tests/G3C-001.Trigger.condition.helper.Tests/Program.cs`
- 생성: `docs/test-results/goals/G3C-001_trigger_condition_helpers_unit_test_results.md`

## 읽기 전용 참조 파일

- `docs/goal-specs/G3C-001_trigger_condition_helper_포팅.md`
- `docs/headless_complete_goal_breakdown.csv`
- `docs/headless_complete_goal_breakdown_ko.csv`
- `docs/headless_complete_goal_breakdown_detailed_ko.csv`
- `docs/test-results/goals/G3B-001_hashtable_replacement_adapter_unit_test_results.md`
- `DCGO/Assets/Scripts/Script/CardEffectCommons/HashtableSetting.cs`
- `DCGO/Assets/Scripts/Script/ICardEffect.cs`
- `DCGO/Assets/Scripts/Script/AutoProcessing.cs`
- `src/HeadlessDCGO.Engine/Headless/State/MatchState.cs`
- `src/HeadlessDCGO.Engine/Headless/State/CardInstanceState.cs`
- `src/HeadlessDCGO.Engine/Headless/Effects/EffectContext.cs`
- `src/HeadlessDCGO.Engine/Headless/Effects/EffectContextAdapter.cs`

## 구현 요약

- `TriggerConditionKind`로 OnPlay, OnDigivolve, WhenAttacking 조건 종류를 고정했다.
- `TriggerConditionRequest`와 `TriggerConditionResult`로 입력/출력/실패 이유 계약을 명시했다.
- `TriggerConditionHelpers.IsOnPlay`는 source card가 owner battle area에 있고 evolution context가 아닐 때 match를 반환한다.
- `TriggerConditionHelpers.IsOnDigivolve`는 source card가 owner battle area에 있고 `isEvolution` 또는 attached source id가 있을 때 match를 반환한다.
- `TriggerConditionHelpers.IsWhenAttacking`은 최신 `AttackDeclared` 이벤트의 attacker가 effect source와 같을 때 match를 반환한다.
- 상태 변경, 효과 실행, queue enqueue, G3C-002의 CanUseEffects 하위 helper 포팅은 수행하지 않았다.

## 테스트 명령

- `.\.dotnet\dotnet.exe run --project tests\G3C-001.Trigger.condition.helper.Tests\G3C-001.Trigger.condition.helper.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project tests\G3B-001.Hashtable.replacement.adapter.Tests\G3B-001.Hashtable.replacement.adapter.Tests.csproj`
- `.\.dotnet\dotnet.exe build src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj`

## 테스트 결과

| 명령 | 전체 | 통과 | 실패 | 스킵 |
| --- | ---: | ---: | ---: | ---: |
| G3C-001 Trigger condition helper tests | 10 | 10 | 0 | 0 |
| G3B-001 Hashtable replacement adapter regression tests | 9 | 9 | 0 | 0 |
| HeadlessDCGO.Engine build | 1 | 1 | 0 | 0 |

## 실패 상세

- 없음.

## 경고

- G3C-001 전용 테스트 실행 중 기존 Runtime 파일의 nullable 경고가 출력되었다.
- G3C-001 신규 파일의 nullable 경고는 수정 후 재실행에서 사라졌다.
- 최종 `HeadlessDCGO.Engine` 빌드는 경고 0개, 오류 0개로 완료되었다.

## 테스트하지 않은 항목

- 전체 `CanUseEffects` 하위 condition 포팅은 G3C-002 범위이므로 테스트하지 않았다.
- 개별 카드 효과의 세부 trigger text 파싱과 Phase 4 카드 효과 구현은 테스트하지 않았다.
- 효과 queue enqueue와 resolution 흐름은 이번 Goal의 산출물이 아니므로 테스트하지 않았다.

## 미해결 리스크

- AS-IS `Hashtable` 기반 helper의 모든 카드별 변형 조건은 후속 Goal에서 추가 helper로 확장해야 한다.
- `WhenAttacking`은 현재 Headless의 `AttackDeclared` 이벤트 metadata를 기준으로 판정하므로, 후속 공격 이벤트 payload 변경 시 이 helper 계약을 같이 확인해야 한다.

## 다음 Goal 진행 가능 여부

- G3C-001 완료 기준을 충족했으므로 G3C-002 진행 가능.

## 완료 판정

- COMPLETE
