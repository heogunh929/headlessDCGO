# G3A-001 ICardEffect Contract Unit Test Results

## 실행 일시

- 실행 일시: 2026-06-25 18:20:29 +09:00
- Goal ID: G3A-001
- 목표: ICardEffect contract 포팅
- 작업 범위: typed card effect 실행 계약 포팅
- 산출물: ICardEffect equivalent
- 완료 기준: effect 계약 테스트 통과
- 최종 상태: PASS

## 수정/생성 파일

- 수정: `src/HeadlessDCGO.Engine/Headless/Effects/EffectContext.cs`
- 생성: `src/HeadlessDCGO.Engine/Headless/Effects/HeadlessCardEffectContract.cs`
- 생성: `tests/G3A-001.ICardEffect.contract.Tests/G3A-001.ICardEffect.contract.Tests.csproj`
- 생성: `tests/G3A-001.ICardEffect.contract.Tests/Program.cs`
- 생성: `docs/test-results/goals/G3A-001_icard_effect_contract_unit_test_results.md`

## 읽기 전용으로 확인한 파일

- `docs/goal-specs/G3A-001_icardeffect_contract_포팅.md`
- `docs/headless_complete_goal_breakdown.csv`
- `docs/headless_complete_goal_breakdown_ko.csv`
- `docs/headless_complete_goal_breakdown_detailed_ko.csv`
- `docs/test-results/goals/G2Z-001_phase2_aggregate_unit_test_results.md`
- `src/HeadlessDCGO.Engine/Headless/Effects/EffectContext.cs`
- `src/HeadlessDCGO.Engine/Headless/Effects/EffectRequest.cs`
- `src/HeadlessDCGO.Engine/Headless/Effects/EffectResult.cs`
- `src/HeadlessDCGO.Engine/Headless/Effects/EffectRegistry.cs`
- `src/HeadlessDCGO.Engine/Headless/Effects/EffectScheduler.cs`
- `DCGO/Assets/Scripts/Script/ICardEffect.cs`
- `DCGO/Assets/Scripts/Script/CardEffectInterfaces.cs`
- `DCGO/Assets/Scripts/Script/SkillInfo.cs`
- `DCGO/Assets/Scripts/Script/AutoProcessing.cs`

## 테스트 의도

- G3A-001 CSV 행과 선행 Goal G2Z-001 완료 증빙을 검증한다.
- AS-IS `ICardEffect`, `SkillInfo`, `AutoProcessing`의 실행 계약 참조를 읽기 전용으로 확인한다.
- Headless `IHeadlessCardEffect`가 `CanResolve`와 `ResolveAsync` 계약을 제공하는지 검증한다.
- `CardEffectDefinition`이 effect id, source id, name, timing, optional/background/max-count/hash 메타데이터를 검증하고 정규화하는지 확인한다.
- `EffectContext`가 typed 값 조회와 필수 값 검증을 제공하는지 확인한다.
- `CanResolve` 실패 시 `EffectResult.Failure`가 반환되고 mutation이 발생하지 않는지 확인한다.
- 성공 resolve는 `IEffectMutationSink`를 통해서만 상태 변경 의도를 기록하는지 확인한다.
- target 누락, 잘못된 typed context 값 같은 실패 케이스가 예외 대신 명확한 실패 결과로 반환되는지 확인한다.
- 동일 입력 반복 시 result와 mutation 기록이 결정적인지 확인한다.
- 새 계약 코드가 UnityEngine/MonoBehaviour/TODO placeholder에 의존하지 않는지 확인한다.

## 테스트 명령

```powershell
.\.dotnet\dotnet.exe run --project tests\G3A-001.ICardEffect.contract.Tests\G3A-001.ICardEffect.contract.Tests.csproj
.\.dotnet\dotnet.exe run --project tests\G1F-005.EffectRegistry.contract.Tests\G1F-005.EffectRegistry.contract.Tests.csproj
.\.dotnet\dotnet.exe run --project tests\G2F-004.Security.delayed.trigger.hook.Tests\G2F-004.Security.delayed.trigger.hook.Tests.csproj
.\.dotnet\dotnet.exe build src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj
```

## 전체/통과/실패/스킵 수

| 범위 | 전체 | 통과 | 실패 | 스킵 |
|---|---:|---:|---:|---:|
| G3A-001 ICardEffect contract 테스트 | 10 | 10 | 0 | 0 |
| G1F-005 EffectRegistry 회귀 테스트 | 11 | 11 | 0 | 0 |
| G2F-004 Security delayed trigger hook 회귀 테스트 | 10 | 10 | 0 | 0 |
| Total tests | 31 | 31 | 0 | 0 |

빌드 결과:

- 명령: `.\.dotnet\dotnet.exe build src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj`
- 경고: 0
- 오류: 0

## 실패 상세 및 수정 여부

- 최초 G3A-001 테스트 실행에서 `Missing required target prevents resolution` 테스트 1건이 실패했다.
- 원인: missing-target 실패 케이스 fixture가 기본 target을 포함한 request를 사용하고 있었다.
- 수정: `tests/G3A-001.ICardEffect.contract.Tests/Program.cs`의 `CreateRequest`에 target 목록 주입 인자를 추가하고, 해당 테스트는 빈 target 목록을 전달하도록 수정했다.
- 최종 실행 기준 실패 없음. G3A-001 전용 테스트 10/10, 관련 회귀 테스트 21/21, 엔진 빌드 모두 통과했다.

## 테스트하지 않은 항목

- 개별 카드 효과 구현과 카드별 effect binding 배치는 Phase 4 범위이므로 구현하거나 테스트하지 않았다.
- G3A-002의 `SkillInfo` 모델 포팅은 다음 Goal 범위이므로 선행 구현하지 않았다.
- 원본 `DCGO/Assets/...` 파일은 수정하지 않고 읽기 전용으로만 참조했다.

## 미해결 리스크

- 새 계약은 `IHeadlessCardEffect`, `CardEffectDefinition`, `CardEffectResolveContext`, `IEffectMutationSink` 기준의 headless 실행 경계를 고정한다. AS-IS `ICardEffect`의 모든 Unity UI/coroutine 표시 동작을 복제하지는 않는다.
- 실제 카드별 효과가 이 계약 위로 올라오는 시점에는 각 카드 효과가 `IEffectMutationSink` 외부에서 상태를 직접 바꾸지 않는지 추가 검증이 필요하다.

## 완료 기준 충족 근거

- 선행 Goal `G2Z-001` 결과 문서에서 COMPLETE를 확인했다.
- 산출물 `ICardEffect equivalent`가 `src/HeadlessDCGO.Engine/Headless/Effects/HeadlessCardEffectContract.cs`에 구현되었다.
- `resolve contract 테스트`가 `tests/G3A-001.ICardEffect.contract.Tests/Program.cs`에 작성되었다.
- 정상 케이스, 실패 케이스, 잘못된 typed context 케이스, 결정성 케이스, mutation boundary 케이스를 검증했다.
- 전용 테스트와 관련 회귀 테스트가 모두 실패 없이 통과했다.
- 원본 `DCGO/Assets/...` 파일을 수정하지 않았다.
- Goal 범위 밖인 G3A-002/Phase 4 작업을 선행하지 않았다.

## 완료 판정

COMPLETE - G3A-001 ICardEffect contract 포팅이 완료되었다. `effect 계약 테스트 통과` 완료 기준을 충족한다.
