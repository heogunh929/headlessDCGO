# G4B-003 Barrier Decoy Save MaterialSave 포팅 상세 지시서

## 1. Goal 식별 정보

- Goal ID: `G4B-003`
- 단계: `Phase 4 - 개별 카드 효과와 카드풀 포팅`
- 영역: `키워드 효과`
- 우선순위: `높음`
- 선행 Goal: `G4B-002`
- 결과 문서: `docs/test-results/goals/G4B-003_keyword_protection_batch_unit_test_results.md`

## 2. 완성 목표

Barrier Decoy Save MaterialSave 포팅 Goal은 '보호와 save 키워드 구현'를 완성하기 위한 작업이다. 키워드별 공통 동작을 카드별 구현 전에 재사용 가능한 base effect로 정리한다. 산출물은 '키워드 batch protection'이며, 완료 기준은 'protection 키워드 테스트 통과'이다.

이 Goal은 `보호와 save keyword 구현` 범위를 완성형 기준으로 닫는 작업이다. 완료 판정은 `protection 키워드 테스트 통과`이며, 구현 산출물만으로는 완료가 아니다. 단위테스트와 결과 문서가 함께 있어야 다음 Goal로 넘어갈 수 있다.

## 3. 작업 순서

1. `G4B-002` 선행 Goal의 결과 문서와 실패/미해결 리스크를 확인한다.
2. 작업 범위를 `보호와 save keyword 구현`로 제한하고, 산출물을 `keyword batch protection`로 고정한다.
3. 아래 AS-IS 확인 대상 파일을 읽기 전용으로 확인하고, gameplay 의미와 Unity/클라이언트 의존 의미를 분리한다.
4. 아래 대상 파일/폴더 중 Goal 산출물과 직접 관련된 위치만 수정하거나 생성한다.
5. public API, 입력 모델, 출력 모델, 실패 모델을 먼저 정하고 테스트 이름에 반영한다.
6. 구현 또는 문서 작성 후 단위테스트를 작성하고 같은 Goal 범위 안에서 실패를 수정한다.
7. 테스트 결과와 완료 기준 `protection 키워드 테스트 통과` 충족 근거를 `docs/test-results/goals/G4B-003_keyword_protection_batch_unit_test_results.md`에 기록한다.

## 4. 작업 대상 파일과 생성 위치

아래 위치는 우선 확인 대상이다. 실제 수정은 Goal 산출물과 직접 연결되는 파일로 제한한다. 없는 파일은 해당 Goal 산출물이 요구할 때만 생성한다.

- `src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectFactory`
- `src/HeadlessDCGO.Engine/Headless/Effects/EffectRegistry.cs`
- `src/HeadlessDCGO.Engine/Headless/Effects`
- `tests/G4B-003.Barrier.Decoy.Save.MaterialSave.Tests`
- `docs/test-results/goals/G4B-003_keyword_protection_batch_unit_test_results.md`

권장 테스트 위치:

- `tests/G4B-003.Barrier.Decoy.Save.MaterialSave.Tests/Program.cs`

## 5. AS-IS 확인 대상과 대체 관계

### 직접 참조 파일

- `docs/headless_complete_goal_breakdown.csv`
- `docs/headless_complete_goal_breakdown_detailed_ko.csv`
- `docs/headless_goal_execution_prompt.md`
- `docs/headless_complete_unit_test_plan.md`
- `docs/headless_complete_unit_test_matrix.csv`
- `src/HeadlessDCGO.Engine/Headless/Effects`
- `DCGO/Assets/Scripts/Script/CardEffectCommons/KeyWordEffects`
- `DCGO/Assets/Scripts/Script/CardEffectFactory/KeyWordEffects`
- `docs/test-results/goals/G4B-003_keyword_protection_batch_unit_test_results.md`

### Headless 모듈 매핑

| 모듈 | 대상 경로 | 책임 | public API |
|---|---|---|---|
| 효과 Factory 포팅 | `src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectFactory` | effect binding과 keyword factory 포팅 | Bind; Create; Resolve |
| 효과 Registry Contract | `src/HeadlessDCGO.Engine/Headless/Effects/EffectRegistry.cs` | card와 trigger를 resolver에 매핑 | Register; GetEffects; GetKeywordEffects |

### Unity/클라이언트 의존 대체

| 의존성 | 원본 역할 | Headless 대체 | 완료 기준 |
|---|---|---|---|
| AttackProcess | attack declaration; blocking; battle; security 처리 | attack을 typed state machine과 event로 모델링 | 공격 포팅이 붙을 API가 고정됨 |
| AutoProcessing | triggered effect 수집과 coroutine resolution | explicit trigger collection; queue; priority; choice suspension 사용 | mandatory/optional effect의 처리 기준이 고정됨 |
| Camera Cinemachine | client viewing과 camera effect | engine replacement 없음 | gameplay path가 camera API를 요구하지 않음 |
| Coroutine | 효과; 공격; 선택 대기; 애니메이션 gate | deterministic engine task와 pending state로 대체 | gameplay coroutine flow가 frame timing 없이 표현됨 |
| GManager | turn; auto processing; attack; effects; players; UI 연결 | global manager를 injected context와 authoritative match로 대체 | ported logic이 static singleton 없이 service 접근 가능 |

### 원본 위치 매핑

| Headless 위치 | AS-IS 원본 | 대체 대상 | 포팅 메모 |
|---|---|---|---|
| `src/HeadlessDCGO.Engine/Headless/Effects/EffectContext.cs` | DCGO/Assets/Scripts/Script/CardController.cs; DCGO/Assets/Scripts/Script/CardEffectCommons.cs; DCGO/Assets/Scripts/Script/AutoProcessing.cs | ExitGames Hashtable effect payloads; CardEffectCommons hashtable builders | Gradually replace string-keyed hashtable context with typed context. |
| `src/HeadlessDCGO.Engine/Headless/Effects/EffectRequest.cs` | DCGO/Assets/Scripts/Script/AutoProcessing.cs; DCGO/Assets/Scripts/Script/MultipleSkills.cs; DCGO/Assets/Scripts/Script/CardEffectFactory.cs | SkillInfo; StackSkillInfos; ActivateEffectProcess | Wraps queued effect id/controller/timing/context without Photon Hashtable. |
| `src/HeadlessDCGO.Engine/Headless/Effects/EffectResolutionMode.cs` | DCGO/Assets/Scripts/Script/AutoProcessing.cs; DCGO/Assets/Scripts/Script/MultipleSkills.cs | main stack; cut-in; background effects; RuleProcess | Separates AutoProcessing flows for deterministic resolution. |
| `src/HeadlessDCGO.Engine/Headless/Effects/EffectResolutionQueue.cs` | DCGO/Assets/Scripts/Script/AutoProcessing.cs; DCGO/Assets/Scripts/Script/MultipleSkills.cs | StackedSkillInfos; PutStackedSkill; ActivateMultipleSkills | Replaces list-based UI/coroutine effect stack management. |
| `src/HeadlessDCGO.Engine/Headless/Effects/EffectResult.cs` | DCGO/Assets/Scripts/Script/AutoProcessing.cs; DCGO/Assets/Scripts/Script/CardController.cs | ActivateEffectProcess; Card effect coroutine outcomes | Placeholder for success/log/events produced by effect resolution. |

## 6. 구현 또는 문서 작성 지시

- Goal의 산출물을 완성형 기준으로 정의하고 public contract를 먼저 고정한다.
- 선행 Goal의 결과를 재구현하지 말고 공개된 API만 사용한다.
- 상태를 바꾸는 코드는 이벤트와 테스트로 관측 가능해야 한다.
- 완료 기준을 만족하지 않는 빈 동작이나 자리표시 구현은 완료로 보지 않는다.
- `키워드 batch protection` 산출물이 실제 public API, 모델, 문서, 테스트 중 어디에 속하는지 명확히 분리한다.
- 완료 기준은 `protection 키워드 테스트 통과`이며, 이 기준을 테스트와 결과 문서에서 직접 증명한다.

추가 세부 지시:

- 산출물 `키워드 batch protection`이 어느 파일과 public API에 반영되는지 결과 문서에 적는다.
- AS-IS와 다르게 설계한 부분은 이유를 적는다. 단, 화면/연출/입력/UI 차이는 Headless 설계 차이로 분리한다.
- 상태를 바꾸는 작업이면 변경 전 상태, 입력, 변경 후 상태, 발생 이벤트를 테스트에서 확인한다.
- 실패 결과가 가능한 작업이면 예외만 던지고 끝내지 말고 호출자가 검증할 수 있는 실패 모델 또는 명확한 예외 계약을 정한다.

## 7. 하지 말아야 할 작업

- 원본 `DCGO/Assets/...` 파일을 수정하지 않는다. 필요한 경우 읽기 전용으로만 확인한다.
- Goal 범위를 넘어 다음 Goal이나 상위 Phase 전체를 함께 처리하지 않는다.
- 단위테스트와 결과 문서 없이 완료를 선언하지 않는다.
- 완성 기준을 충족하지 않는 빈 동작, 자리표시 구현, TODO-only 구현을 완료로 보지 않는다.

## 8. 단위테스트 지시

CSV 기준 단위테스트 범위:

> barrier decoy save material save 테스트

반드시 포함할 테스트 관점:

- Given `키워드 batch protection` 기본 입력, When Goal 범위의 기능을 실행하면, Then `protection 키워드 테스트 통과`을 만족하는 결과가 나온다.
- Given 유효하지 않은 입력, When 기능을 실행하면, Then 상태 오염 없이 명확한 실패 결과를 반환한다.
- Given 동일 입력을 두 번 적용할 때, When 결과를 비교하면, Then deterministic하게 같은 값이 나온다.
- Given Goal 범위 밖 기능, When 테스트를 작성할 때, Then 선행 Goal의 내부 구현을 재검증하지 않는다.
- CSV에 적힌 단위테스트 범위 `테스트는 'barrier decoy save material save 테스트'를 직접 검증해야 한다. 정상 케이스, 실패/예외 케이스, 결정성이 필요한 경우 동일 입력 반복 케이스를 포함한다. 테스트 파일명과 테스트 명령을 결과 문서에 기록한다. Goal 범위 밖 동작을 검증하기 위해 새 구현을 끌어오지 않는다.`가 실제 테스트명 또는 assertion으로 추적 가능해야 한다.

테스트 작성 규칙:

- 테스트는 Goal 산출물의 public API 또는 문서 검증 포인트를 직접 호출해야 한다.
- 같은 입력을 반복했을 때 결과가 달라질 수 있는 부분은 seed 또는 deterministic fixture를 고정한다.
- 실패 케이스는 최소 1개 이상 포함한다. 입력 검증, illegal action, 누락 데이터, 잘못된 상태 중 Goal에 맞는 것을 고른다.
- 테스트 명령은 `.\.dotnet\dotnet.exe run --project <테스트 csproj>` 형태로 결과 문서에 기록한다.
- 테스트가 아직 생성되지 않은 Goal이면 이 Goal에서 테스트 프로젝트 또는 테스트 파일을 함께 만든다.

## 9. 결과 문서 작성 지시

결과 문서 경로:

- `docs/test-results/goals/G4B-003_keyword_protection_batch_unit_test_results.md`

결과 문서에는 다음 항목을 반드시 포함한다.

- Goal ID와 제목: `G4B-003 Barrier Decoy Save MaterialSave 포팅`
- 실행 일시와 실행 환경
- 수정/생성 파일 목록
- 읽기 전용으로 확인한 AS-IS 파일 목록
- 테스트 명령 전체
- 전체/통과/실패/스킵 수
- 실패 상세와 수정 여부
- 테스트하지 못한 항목과 이유
- 완료 기준 `protection 키워드 테스트 통과` 충족 근거
- 다음 Goal 진행 가능 여부

## 10. 완료 판정 체크리스트

- [ ] 선행 Goal `G4B-002` 상태를 확인했다.
- [ ] 작업 범위 `보호와 save keyword 구현` 밖의 변경을 하지 않았다.
- [ ] 원본 `DCGO/Assets/...` 파일을 수정하지 않았다.
- [ ] 대상 파일과 AS-IS 확인 파일을 결과 문서에 기록했다.
- [ ] 산출물 `keyword batch protection`을 구현 또는 문서화했다.
- [ ] 단위테스트 `barrier decoy save material save 테스트`를 작성했다.
- [ ] 단위테스트를 실행했고 실패가 없다.
- [ ] 금지 dependency 또는 금지 작업 위반이 없다.
- [ ] 결과 문서 `docs/test-results/goals/G4B-003_keyword_protection_batch_unit_test_results.md`를 작성했다.
- [ ] 완료 기준 `protection 키워드 테스트 통과`을 결과 문서에서 증명했다.

## 11. 실행 프롬프트

```text
HeadlessDCGO.Engine Goal G4B-003를 수행하라.

반드시 먼저 이 상세 지시서를 읽어라:
docs/goal-specs/G4B-003_barrier_decoy_save_materialsave_포팅.md

이번 작업은 G4B-003 하나만 완료하는 것이 목표다.
선행 Goal: G4B-002
작업 범위: 보호와 save keyword 구현
산출물: keyword batch protection
단위테스트 범위: barrier decoy save material save 테스트
결과 문서: docs/test-results/goals/G4B-003_keyword_protection_batch_unit_test_results.md
완료 기준: protection 키워드 테스트 통과

원본 DCGO/Assets 파일은 수정하지 말라.
Goal 범위 밖 작업을 하지 말라.
단위테스트와 결과 문서 없이는 완료로 말하지 말라.
```
