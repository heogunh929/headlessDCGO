# G2F-004 Security delayed trigger hook 포팅 테스트 결과

## 실행 일시

- 2026-06-25 15:28:53 +09:00

## 수정/생성 파일

- 수정: `src/HeadlessDCGO.Engine/Headless/Runtime/GameEventType.cs`
- 생성: `src/HeadlessDCGO.Engine/Headless/Effects/SecurityDelayedTriggerHook.cs`
- 생성: `tests/G2F-004.Security.delayed.trigger.hook.Tests/G2F-004.Security.delayed.trigger.hook.Tests.csproj`
- 생성: `tests/G2F-004.Security.delayed.trigger.hook.Tests/Program.cs`
- 생성: `docs/test-results/goals/G2F-004_security_delayed_trigger_hook_unit_test_results.md`

## 구현 범위

- G2F-004 범위인 `security와 delayed trigger 연결`만 구현했다.
- 원본 `DCGO/Assets/...` 파일은 읽기만 했고 수정하지 않았다.
- 실제 security battle 처리, 카드 효과 해석, 다음 Phase 선행 작업은 구현하지 않았다.
- AS-IS 참조 확인:
  - `AttackProcess.cs`: `DoSecurityCheck`, `SecurityDigimon`
  - `CardController.cs`: `ISecurityCheck`, `OnSecurityCheck`, `SecuritySkill`
  - `AutoProcessing.cs`: `StackSkillInfos`
  - `CardEffectFactory.cs`: `PlaceSelfDelayOptionSecurityEffect`

## 테스트 명령

- `.\.dotnet\dotnet.exe run --project .\tests\G2F-004.Security.delayed.trigger.hook.Tests\G2F-004.Security.delayed.trigger.hook.Tests.csproj`
- `.\.dotnet\dotnet.exe build .\src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj`
- `.\.dotnet\dotnet.exe run --project .\tests\G2F-001.Trigger.event.collection.Tests\G2F-001.Trigger.event.collection.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project .\tests\G2F-002.Mandatory.effect.ordering.Tests\G2F-002.Mandatory.effect.ordering.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project .\tests\G2F-003.Optional.prompt.queue.Tests\G2F-003.Optional.prompt.queue.Tests.csproj`

## 전체/통과/실패/스킵 수

| 범위 | 전체 | 통과 | 실패 | 스킵 |
|---|---:|---:|---:|---:|
| G2F-004 전용 테스트 | 10 | 10 | 0 | 0 |
| HeadlessDCGO.Engine 빌드 | 1 | 1 | 0 | 0 |
| G2F-001 회귀 테스트 | 10 | 10 | 0 | 0 |
| G2F-002 회귀 테스트 | 10 | 10 | 0 | 0 |
| G2F-003 회귀 테스트 | 10 | 10 | 0 | 0 |
| 합계 | 41 | 41 | 0 | 0 |

## 실패 상세

- 최종 실행 기준 실패 없음.
- 초기 G2F-004 테스트 1회에서 AS-IS 참조 파일 경로가 `DCGO/Assets/Scripts/Card/CardEffectFactory.cs`로 잘못 지정되어 실패했다.
- 같은 Goal 범위 안에서 읽기 경로를 실제 위치인 `DCGO/Assets/Scripts/Script/CardEffectFactory.cs`로 수정했고, 재실행 결과 10/10 통과했다.

## 미해결 리스크

- `SecurityDelayedTriggerHook`은 security/delayed trigger를 기존 `AutoProcessingTriggerCollector`, `MandatoryEffectOrdering`, `OptionalPromptQueue`, `EffectScheduler`에 연결하는 계약만 제공한다.
- 실제 security battle, security card reveal, 카드별 효과 실행 의미는 G2F-004 범위 밖이라 구현하지 않았다.
- 작업 디렉터리에서 `git status --short`와 `git diff`는 `fatal: not a git repository`로 실행되지 않았다.

## 완료 판정

- COMPLETE
- 완료 기준 `security delayed hook 테스트 통과` 충족.
