# G3C-002 CanUseEffects helper unit test results

## 실행 일시

- 2026-06-25 18:43:01 +09:00

## Goal

- Goal ID: G3C-002
- 목표: CanUseEffects helper 포팅
- 작업 범위: CanUseEffects 하위 condition 포팅
- 산출물: can use helpers
- 완료 기준: CanUseEffects 테스트 통과

## 선행 Goal 확인

- G3C-001 결과 문서: `docs/test-results/goals/G3C-001_trigger_condition_helpers_unit_test_results.md`
- 확인 결과: COMPLETE

## 수정/생성 파일

- 생성: `src/HeadlessDCGO.Engine/Headless/Effects/CanUseEffectHelpers.cs`
- 생성: `tests/G3C-002.CanUseEffects.helper.Tests/G3C-002.CanUseEffects.helper.Tests.csproj`
- 생성: `tests/G3C-002.CanUseEffects.helper.Tests/Program.cs`
- 생성: `docs/test-results/goals/G3C-002_can_use_effect_helpers_unit_test_results.md`

## 읽기 전용 참조 파일

- `docs/goal-specs/G3C-002_canuseeffects_helper_포팅.md`
- `docs/headless_complete_goal_breakdown.csv`
- `docs/headless_complete_goal_breakdown_ko.csv`
- `docs/headless_complete_goal_breakdown_detailed_ko.csv`
- `docs/test-results/goals/G3C-001_trigger_condition_helpers_unit_test_results.md`
- `DCGO/Assets/Scripts/Script/ICardEffect.cs`
- `DCGO/Assets/Scripts/Script/CardEffectCommons/CanUseEffects/PermanentEnterField/PermanentEnterField.cs`
- `DCGO/Assets/Scripts/Script/CardEffectCommons/CanUseEffects/PermanentEnterField/OnPlay.cs`
- `DCGO/Assets/Scripts/Script/CardEffectCommons/CanUseEffects/PermanentEnterField/WhenDigivolving.cs`
- `DCGO/Assets/Scripts/Script/CardEffectCommons/CanUseEffects/OnAttack.cs`
- `DCGO/Assets/Scripts/Script/CardEffectCommons/CanUseEffects/WhenPermanentWouldPlay.cs`
- `DCGO/Assets/Scripts/Script/CardEffectCommons/CanUseEffects/WhenPermanentWouldDigivolve.cs`
- `src/HeadlessDCGO.Engine/Headless/Effects/TriggerConditionHelpers.cs`

## 구현 요약

- `CanUseEffectEvaluationKind`로 Trigger, Activate, Use 평가 단계를 고정했다.
- `CanUseEffectCondition`으로 AS-IS `Func<Hashtable,bool>` 하위 condition을 typed key/value 조건으로 표현했다.
- `CanUseEffectRequest`는 `MatchState`, `SkillInfo`, 선택적 `TriggerConditionKind`, trigger/activation condition 목록을 입력으로 받는다.
- `CanUseEffectHelpers.CanTrigger`는 max count, G3C-001 trigger helper 결과, trigger typed condition을 순서대로 평가한다.
- `CanUseEffectHelpers.CanActivate`는 max count, source 존재/owner, disabled, canActivate, requiresTopSource, activation typed condition을 평가한다.
- `CanUseEffectHelpers.CanUse`는 AS-IS와 동일하게 `CanTrigger` 실패 시 중단하고, 이후 `CanActivate`를 평가한다.
- 상태 변경, 효과 실행, queue enqueue, G3D-001의 min/max DP cost level helper 포팅은 수행하지 않았다.

## 테스트 명령

- `.\.dotnet\dotnet.exe run --project tests\G3C-002.CanUseEffects.helper.Tests\G3C-002.CanUseEffects.helper.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project tests\G3C-001.Trigger.condition.helper.Tests\G3C-001.Trigger.condition.helper.Tests.csproj`
- `.\.dotnet\dotnet.exe build src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj`

## 테스트 결과

| 명령 | 전체 | 통과 | 실패 | 스킵 |
| --- | ---: | ---: | ---: | ---: |
| G3C-002 CanUseEffects helper tests | 10 | 10 | 0 | 0 |
| G3C-001 Trigger condition helper regression tests | 10 | 10 | 0 | 0 |
| HeadlessDCGO.Engine build | 1 | 1 | 0 | 0 |

## 실패 상세

- 없음.

## 경고

- G3C-002 전용 테스트 실행 중 기존 Runtime 파일의 nullable 경고가 출력되었다.
- G3C-002 신규 파일의 nullable 경고는 수정 후 재실행에서 사라졌다.
- 최종 `HeadlessDCGO.Engine` 빌드는 경고 0개, 오류 0개로 완료되었다.

## 테스트하지 않은 항목

- G3D-001 범위인 DP/cost/level min max 조건 helper는 테스트하지 않았다.
- 개별 카드 효과 텍스트별 `CanUseEffects` 전체 포팅은 Phase 4 및 후속 Goal 범위로 남겼다.
- 효과 실행, coroutine, UI choice, queue enqueue는 이번 Goal의 산출물이 아니므로 테스트하지 않았다.

## 미해결 리스크

- 현재 helper는 typed condition과 G3C-001 trigger helper 조합 계약을 제공한다.
- AS-IS `CanUseEffects` 폴더의 모든 세부 helper는 후속 Goal에서 별도 typed helper로 확장해야 한다.

## 다음 Goal 진행 가능 여부

- G3C-002 완료 기준을 충족했으므로 G3D-001 진행 가능.

## 완료 판정

- COMPLETE
