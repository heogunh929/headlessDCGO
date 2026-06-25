# G2E-004 Attack action 연결 포팅 상세 지시서

## 1. Goal 식별 정보

- Goal ID: `G2E-004`
- 단계: `Phase 2 - AS-IS 핵심 흐름 포팅`
- 영역: `메인 페이즈 액션`
- 우선순위: `높음`
- 선행 Goal: `G2A-006`
- 결과 문서: `docs/test-results/goals/G2E-004_attack_action_dispatch_unit_test_results.md`

## 2. 완성 목표

Attack action 연결 포팅 Goal은 '공격 action을 AttackProcess로 연결'를 완성하기 위한 작업이다. MainPhaseAction 하위 play, digivolve, option, attack, pass 흐름을 LegalAction과 ActionProcessor로 연결한다. 산출물은 'AttackPermanentAction attack intent'이며, 완료 기준은 'attack action dispatch 테스트 통과'이다.

이 Goal은 `공격 action을 AttackProcess로 연결` 범위를 완성형 기준으로 닫는 작업이다. 완료 판정은 `attack action dispatch 테스트 통과`이며, 구현 산출물만으로는 완료가 아니다. 단위테스트와 결과 문서가 함께 있어야 다음 Goal로 넘어갈 수 있다.

## 3. 작업 순서

1. `G2A-006` 선행 Goal의 결과 문서와 실패/미해결 리스크를 확인한다.
2. 작업 범위를 `공격 action을 AttackProcess로 연결`로 제한하고, 산출물을 `AttackPermanentAction attack intent`로 고정한다.
3. 아래 AS-IS 확인 대상 파일을 읽기 전용으로 확인하고, gameplay 의미와 Unity/클라이언트 의존 의미를 분리한다.
4. 아래 대상 파일/폴더 중 Goal 산출물과 직접 관련된 위치만 수정하거나 생성한다.
5. public API, 입력 모델, 출력 모델, 실패 모델을 먼저 정하고 테스트 이름에 반영한다.
6. 구현 또는 문서 작성 후 단위테스트를 작성하고 같은 Goal 범위 안에서 실패를 수정한다.
7. 테스트 결과와 완료 기준 `attack action dispatch 테스트 통과` 충족 근거를 `docs/test-results/goals/G2E-004_attack_action_dispatch_unit_test_results.md`에 기록한다.

## 4. 작업 대상 파일과 생성 위치

아래 위치는 우선 확인 대상이다. 실제 수정은 Goal 산출물과 직접 연결되는 파일로 제한한다. 없는 파일은 해당 Goal 산출물이 요구할 때만 생성한다.

- `src/HeadlessDCGO.Engine/Headless/Services/LegalAction.cs`
- `src/HeadlessDCGO.Engine/Headless/Runtime/HeadlessActionPayloads.cs`
- `src/HeadlessDCGO.Engine/Assets/Scripts/Script/TurnStateMachine.cs`
- `src/HeadlessDCGO.Engine/Assets/Scripts/Script/AttackProcess.cs`
- `src/HeadlessDCGO.Engine/Assets/Scripts/Script/Player.cs`
- `src/HeadlessDCGO.Engine/Headless/Runtime`
- `tests/G2E-004.Attack.action.Tests`
- `docs/test-results/goals/G2E-004_attack_action_dispatch_unit_test_results.md`

권장 테스트 위치:

- `tests/G2E-004.Attack.action.Tests/Program.cs`

## 5. AS-IS 확인 대상과 대체 관계

### 직접 참조 파일

- `docs/headless_complete_goal_breakdown.csv`
- `docs/headless_complete_goal_breakdown_detailed_ko.csv`
- `docs/headless_goal_execution_prompt.md`
- `docs/headless_complete_unit_test_plan.md`
- `docs/headless_complete_unit_test_matrix.csv`
- `src/HeadlessDCGO.Engine/Headless/Runtime`
- `DCGO/Assets/Scripts/Script/MainPhaseAction`
- `docs/test-results/goals/G2E-004_attack_action_dispatch_unit_test_results.md`

### Headless 모듈 매핑

| 모듈 | 대상 경로 | 책임 | public API |
|---|---|---|---|
| 액션 모델 | `src/HeadlessDCGO.Engine/Headless/Runtime/HeadlessActionPayloads.cs` | UI/RPC를 대체하는 typed action payload 정의 | PlayCard; Digivolve; Attack; Block; Activate; Choose; Pass |
| 턴 상태 포팅 | `src/HeadlessDCGO.Engine/Assets/Scripts/Script/TurnStateMachine.cs` | 실제 턴과 phase flow 포팅 | AdvanceTurn; AdvancePhase; GetLegalActions |
| AttackProcess 포팅 | `src/HeadlessDCGO.Engine/Assets/Scripts/Script/AttackProcess.cs` | 실제 공격과 security flow 포팅 | Declare; Block; ResolveBattle; SecurityCheck |
| 플레이어 포팅 | `src/HeadlessDCGO.Engine/Assets/Scripts/Script/Player.cs` | player zone과 memory logic 포팅 | GetZones; ApplyPlayerMutation |

### Unity/클라이언트 의존 대체

| 의존성 | 원본 역할 | Headless 대체 | 완료 기준 |
|---|---|---|---|
| Photon | remote room; ownership; RPC sync; player identity | network를 deterministic local match context와 action/event stream으로 대체 | RPC gameplay 흐름이 local action/event로 매핑됨 |
| Addressables | client asset async loading | core engine에서는 사용하지 않음 | Headless runtime이 Addressables에 의존하지 않음 |
| Animation Animator | 공격; 페이즈; 효과 표시 | visual animation 대신 gameplay event 발생 | animation-triggered gameplay side effect가 runtime/effect service로 이동 |
| Audio | client feedback | engine replacement 없음 | gameplay path가 audio API를 요구하지 않음 |
| Card GameObject State | display card와 gameplay flag를 함께 유지 | card definition; card instance; visual concern 분리 | card state가 GameObject 없이 snapshot/mutation 가능 |

### 원본 위치 매핑

| Headless 위치 | AS-IS 원본 | 대체 대상 | 포팅 메모 |
|---|---|---|---|
| `src/HeadlessDCGO.Engine/Headless/Runtime/DcgoMatch.cs` | DCGO/Assets/Scripts/Script/GManager.cs; DCGO/Assets/Scripts/Script/TurnStateMachine.cs; DCGO/Assets/Scripts/Script/GameContext.cs | GManager.Init; TurnStateMachine.Init/GameStateMachine; GameContext state; EndGame result | Public entry point for initialize/reset/step/apply-action/result and terminal MatchResult synchronization. |
| `src/HeadlessDCGO.Engine/Headless/Runtime/ActionProcessResult.cs` | DCGO/Assets/Scripts/Script/TurnStateMachine.cs; DCGO/Assets/Scripts/Script/Player.cs; DCGO/Assets/Scripts/Script/CardController.cs | MainPhaseAction execution result; action side effects; state transition outcome | Represents success/message/metadata until concrete action handlers are ported. |
| `src/HeadlessDCGO.Engine/Headless/Runtime/ActionMask.cs` | DCGO/Assets/Scripts/Script/TurnStateMachine.cs; DCGO/Assets/Scripts/Script/Player.cs | main phase legal actions; command availability; QueueMainPhaseAction | Represents legal actions before compact tensor mask encoding is defined. |
| `src/HeadlessDCGO.Engine/Headless/Runtime/ActionEncoder.cs` | DCGO/Assets/Scripts/Script/TurnStateMachine.cs; DCGO/Assets/Scripts/Script/Player.cs | main phase legal action encoding; command availability; policy action selection | Converts ActionMask values into stable action slots and numeric mask vectors. |
| `src/HeadlessDCGO.Engine/Headless/Runtime/GameEvent.cs` | DCGO/Assets/Scripts/Script/TurnStateMachine.cs; DCGO/Assets/Scripts/Script/CardObjectController.cs; DCGO/Assets/Scripts/Script/AutoProcessing.cs | phase changes; card moves; effect queue/resolution | Will be emitted instead of UI callbacks/log-only side effects. |

## 6. 구현 또는 문서 작성 지시

- Play, Digivolve, Option activate, Attack, Pass를 HeadlessAction으로 입력받아 ActionProcessResult로 반환한다.
- legal action query와 action execution이 같은 룰 조건을 공유해야 한다.
- 비용 지불, zone 이동, trigger enqueue, illegal reason을 각각 검증 가능하게 분리한다.
- cheat/debug action은 명시적으로 허용된 테스트 경로가 아니면 legal action에 포함하지 않는다.
- `AttackPermanentAction attack intent` 산출물이 실제 public API, 모델, 문서, 테스트 중 어디에 속하는지 명확히 분리한다.
- 완료 기준은 `attack action dispatch 테스트 통과`이며, 이 기준을 테스트와 결과 문서에서 직접 증명한다.

추가 세부 지시:

- 산출물 `AttackPermanentAction attack intent`이 어느 파일과 public API에 반영되는지 결과 문서에 적는다.
- AS-IS와 다르게 설계한 부분은 이유를 적는다. 단, 화면/연출/입력/UI 차이는 Headless 설계 차이로 분리한다.
- 상태를 바꾸는 작업이면 변경 전 상태, 입력, 변경 후 상태, 발생 이벤트를 테스트에서 확인한다.
- 실패 결과가 가능한 작업이면 예외만 던지고 끝내지 말고 호출자가 검증할 수 있는 실패 모델 또는 명확한 예외 계약을 정한다.

## 7. 하지 말아야 할 작업

- 원본 `DCGO/Assets/...` 파일을 수정하지 않는다. 필요한 경우 읽기 전용으로만 확인한다.
- Goal 범위를 넘어 다음 Goal이나 상위 Phase 전체를 함께 처리하지 않는다.
- 단위테스트와 결과 문서 없이 완료를 선언하지 않는다.
- 완성 기준을 충족하지 않는 빈 동작, 자리표시 구현, TODO-only 구현을 완료로 보지 않는다.
- asset/card effect 실제 포팅은 해당 단계가 열리기 전까지 수행하지 않는다.

## 8. 단위테스트 지시

CSV 기준 단위테스트 범위:

> attack action dispatch 테스트

반드시 포함할 테스트 관점:

- Given 충분한 비용과 유효한 카드, When PlayCardAction을 적용하면, Then 비용 지불과 zone 이동이 기록된다.
- Given 유효하지 않은 진화 조건, When DigivolveAction을 적용하면, Then 상태 변화 없이 illegal reason이 반환된다.
- Given option 카드, When Activate action을 적용하면, Then 효과 queue와 trash 이동이 기대와 일치한다.
- Given pass action, When 적용하면, Then memory와 turn ownership이 규칙대로 변경된다.
- CSV에 적힌 단위테스트 범위 `테스트는 'attack action dispatch 테스트'를 직접 검증해야 한다. 정상 케이스, 실패/예외 케이스, 결정성이 필요한 경우 동일 입력 반복 케이스를 포함한다. 테스트 파일명과 테스트 명령을 결과 문서에 기록한다. Goal 범위 밖 동작을 검증하기 위해 새 구현을 끌어오지 않는다.`가 실제 테스트명 또는 assertion으로 추적 가능해야 한다.

테스트 작성 규칙:

- 테스트는 Goal 산출물의 public API 또는 문서 검증 포인트를 직접 호출해야 한다.
- 같은 입력을 반복했을 때 결과가 달라질 수 있는 부분은 seed 또는 deterministic fixture를 고정한다.
- 실패 케이스는 최소 1개 이상 포함한다. 입력 검증, illegal action, 누락 데이터, 잘못된 상태 중 Goal에 맞는 것을 고른다.
- 테스트 명령은 `.\.dotnet\dotnet.exe run --project <테스트 csproj>` 형태로 결과 문서에 기록한다.
- 테스트가 아직 생성되지 않은 Goal이면 이 Goal에서 테스트 프로젝트 또는 테스트 파일을 함께 만든다.

## 9. 결과 문서 작성 지시

결과 문서 경로:

- `docs/test-results/goals/G2E-004_attack_action_dispatch_unit_test_results.md`

결과 문서에는 다음 항목을 반드시 포함한다.

- Goal ID와 제목: `G2E-004 Attack action 연결 포팅`
- 실행 일시와 실행 환경
- 수정/생성 파일 목록
- 읽기 전용으로 확인한 AS-IS 파일 목록
- 테스트 명령 전체
- 전체/통과/실패/스킵 수
- 실패 상세와 수정 여부
- 테스트하지 못한 항목과 이유
- 완료 기준 `attack action dispatch 테스트 통과` 충족 근거
- 다음 Goal 진행 가능 여부

## 10. 완료 판정 체크리스트

- [ ] 선행 Goal `G2A-006` 상태를 확인했다.
- [ ] 작업 범위 `공격 action을 AttackProcess로 연결` 밖의 변경을 하지 않았다.
- [ ] 원본 `DCGO/Assets/...` 파일을 수정하지 않았다.
- [ ] 대상 파일과 AS-IS 확인 파일을 결과 문서에 기록했다.
- [ ] 산출물 `AttackPermanentAction attack intent`을 구현 또는 문서화했다.
- [ ] 단위테스트 `attack action dispatch 테스트`를 작성했다.
- [ ] 단위테스트를 실행했고 실패가 없다.
- [ ] 금지 dependency 또는 금지 작업 위반이 없다.
- [ ] 결과 문서 `docs/test-results/goals/G2E-004_attack_action_dispatch_unit_test_results.md`를 작성했다.
- [ ] 완료 기준 `attack action dispatch 테스트 통과`을 결과 문서에서 증명했다.

## 11. 실행 프롬프트

```text
HeadlessDCGO.Engine Goal G2E-004를 수행하라.

반드시 먼저 이 상세 지시서를 읽어라:
docs/goal-specs/G2E-004_attack_action_연결_포팅.md

이번 작업은 G2E-004 하나만 완료하는 것이 목표다.
선행 Goal: G2A-006
작업 범위: 공격 action을 AttackProcess로 연결
산출물: AttackPermanentAction attack intent
단위테스트 범위: attack action dispatch 테스트
결과 문서: docs/test-results/goals/G2E-004_attack_action_dispatch_unit_test_results.md
완료 기준: attack action dispatch 테스트 통과

원본 DCGO/Assets 파일은 수정하지 말라.
Goal 범위 밖 작업을 하지 말라.
단위테스트와 결과 문서 없이는 완료로 말하지 말라.
```
