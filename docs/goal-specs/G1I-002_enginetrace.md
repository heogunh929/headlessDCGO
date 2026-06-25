# G1I-002 EngineTrace 상세 지시서

## 1. Goal 식별 정보

- Goal ID: `G1I-002`
- 단계: `Phase 1 - Unity 대체 기반 구현`
- 영역: `진단/결정성`
- 우선순위: `높음`
- 선행 Goal: `G1A-001`
- 결과 문서: `docs/test-results/goals/G1I-002_engine_trace_unit_test_results.md`

## 2. 완성 목표

EngineTrace Goal은 'trace sequence와 fingerprint 확정'를 완성하기 위한 작업이다. seed, random, trace, log, dependency scan을 통해 같은 입력이 같은 결과를 내도록 검증한다. 산출물은 'EngineTrace/TraceEvent'이며, 완료 기준은 'EngineTrace 테스트 통과'이다.

이 Goal은 `trace sequence와 fingerprint 확정` 범위를 완성형 기준으로 닫는 작업이다. 완료 판정은 `EngineTrace 테스트 통과`이며, 구현 산출물만으로는 완료가 아니다. 단위테스트와 결과 문서가 함께 있어야 다음 Goal로 넘어갈 수 있다.

## 3. 작업 순서

1. `G1A-001` 선행 Goal의 결과 문서와 실패/미해결 리스크를 확인한다.
2. 작업 범위를 `trace sequence와 fingerprint 확정`로 제한하고, 산출물을 `EngineTrace TraceEvent`로 고정한다.
3. 아래 AS-IS 확인 대상 파일을 읽기 전용으로 확인하고, gameplay 의미와 Unity/클라이언트 의존 의미를 분리한다.
4. 아래 대상 파일/폴더 중 Goal 산출물과 직접 관련된 위치만 수정하거나 생성한다.
5. public API, 입력 모델, 출력 모델, 실패 모델을 먼저 정하고 테스트 이름에 반영한다.
6. 구현 또는 문서 작성 후 단위테스트를 작성하고 같은 Goal 범위 안에서 실패를 수정한다.
7. 테스트 결과와 완료 기준 `EngineTrace 테스트 통과` 충족 근거를 `docs/test-results/goals/G1I-002_engine_trace_unit_test_results.md`에 기록한다.

## 4. 작업 대상 파일과 생성 위치

아래 위치는 우선 확인 대상이다. 실제 수정은 Goal 산출물과 직접 연결되는 파일로 제한한다. 없는 파일은 해당 Goal 산출물이 요구할 때만 생성한다.

- `src/HeadlessDCGO.Engine/Headless/Diagnostics/EngineTrace.cs`
- `src/HeadlessDCGO.Engine/Headless/Diagnostics/TraceEvent.cs`
- `src/HeadlessDCGO.Engine/Headless/Services/ILogSink.cs`
- `src/HeadlessDCGO.Engine/Headless/Services/IRandomSource.cs`
- `src/HeadlessDCGO.Engine/Headless/Bridge/ContinuousContext.cs`
- `src/HeadlessDCGO.Engine/Headless/Diagnostics`
- `tests/G1I-002.EngineTrace.Tests`
- `docs/test-results/goals/G1I-002_engine_trace_unit_test_results.md`

권장 테스트 위치:

- `tests/G1I-002.EngineTrace.Tests/Program.cs`

## 5. AS-IS 확인 대상과 대체 관계

### 직접 참조 파일

- `docs/headless_complete_goal_breakdown.csv`
- `docs/headless_complete_goal_breakdown_detailed_ko.csv`
- `docs/headless_goal_execution_prompt.md`
- `docs/headless_complete_unit_test_plan.md`
- `docs/headless_complete_unit_test_matrix.csv`
- `src/HeadlessDCGO.Engine/Headless/Diagnostics`
- `src/HeadlessDCGO.Engine/Headless/Services/IRandomSource.cs`
- `src/HeadlessDCGO.Engine/Headless/Services/ILogSink.cs`
- `DCGO/Assets/Scripts/Script/GameRandom.cs`
- `docs/test-results/goals/G1I-002_engine_trace_unit_test_results.md`

### Headless 모듈 매핑

| 모듈 | 대상 경로 | 책임 | public API |
|---|---|---|---|
| Trace | `src/HeadlessDCGO.Engine/Headless/Diagnostics/EngineTrace.cs` | deterministic event trace 기록 | Record; Snapshot; Clear; Fingerprint |
| 로그 Sink | `src/HeadlessDCGO.Engine/Headless/Services/ILogSink.cs` | Debug.Log와 PlayLog 대체 | Info; Warn; Error |
| 랜덤 소스 | `src/HeadlessDCGO.Engine/Headless/Services/IRandomSource.cs` | deterministic random과 shuffle 제공 | NextInt; NextDouble; Shuffle |
| Continuous Context | `src/HeadlessDCGO.Engine/Headless/Bridge/ContinuousContext.cs` | ContinuousController global option 대체 | Random; options; deck config |

### Unity/클라이언트 의존 대체

| 의존성 | 원본 역할 | Headless 대체 | 완료 기준 |
|---|---|---|---|
| Debug Log | Unity console과 client log | structured deterministic log와 trace event로 대체 | trace가 replay와 parity debug에 충분함 |
| ContinuousController | random; option; deck/session/global flag 보관 | global value를 immutable config 또는 deterministic service로 전환 | match result가 external singleton state에 의존하지 않음 |
| TextMeshPro | label; message; counter; log 표시 | plain string과 structured metadata 사용 | Headless runtime이 TMPro에 의존하지 않음 |
| Unity Random | shuffle과 random effect | MatchConfig seed 기반 deterministic random 사용 | 같은 seed가 같은 shuffle; random choice; trace 생성 |
| Animation Animator | 공격; 페이즈; 효과 표시 | visual animation 대신 gameplay event 발생 | animation-triggered gameplay side effect가 runtime/effect service로 이동 |

### 원본 위치 매핑

| Headless 위치 | AS-IS 원본 | 대체 대상 | 포팅 메모 |
|---|---|---|---|
| `src/HeadlessDCGO.Engine/Headless/Diagnostics/EngineTrace.cs` | DCGO/Assets/Scripts/Script/PlayLog.cs; DCGO/Assets/Scripts/Script/AutoProcessing.cs; DCGO/Assets/Scripts/Script/TurnStateMachine.cs | PlayLog.OnAddLog; Debug.Log; effect/turn process logs | Use for parity tests and RL debugging instead of UI log. |
| `src/HeadlessDCGO.Engine/Headless/Diagnostics/ITraceSink.cs` | DCGO/Assets/Scripts/Script/PlayLog.cs; DCGO/Assets/Scripts/Script/TurnStateMachine.cs | PlayLog.OnAddLog; Debug.Log; runtime trace hooks | Allows trace collection or no-op sinks without UI logging. |
| `src/HeadlessDCGO.Engine/Headless/Diagnostics/NullTraceSink.cs` | DCGO/Assets/Scripts/Script/PlayLog.cs; DCGO/Assets/Scripts/Script/GManager.cs | PlayLog/UI log optional output | Used when simulations do not need trace output. |
| `src/HeadlessDCGO.Engine/Headless/Diagnostics/TraceEvent.cs` | DCGO/Assets/Scripts/Script/PlayLog.cs; DCGO/Assets/Scripts/Script/CardController.cs; DCGO/Assets/Scripts/Script/AttackProcess.cs | play/evolution/battle/security logs | Structured replacement for textual UI log entries. |
| `src/HeadlessDCGO.Engine/Headless/Diagnostics/TraceOptions.cs` | DCGO/Assets/Scripts/Script/ContinuousController.cs; DCGO/Assets/Scripts/Script/GManager.cs | debug options; display/log preferences | Controls which engine events are emitted during tests/simulations. |

## 6. 구현 또는 문서 작성 지시

- 동일 seed와 동일 action sequence는 동일 trace와 fingerprint를 만들어야 한다.
- 로그와 trace는 테스트에서 snapshot 비교가 가능하도록 순서와 sequence를 가진다.
- 금지 dependency scan은 Headless 영역에 Unity, Photon, TMPro, Unity UI가 들어오지 못하게 한다.
- 진단 기능은 게임 결과를 변경하지 않는다.
- `EngineTrace/TraceEvent` 산출물이 실제 public API, 모델, 문서, 테스트 중 어디에 속하는지 명확히 분리한다.
- 완료 기준은 `EngineTrace 테스트 통과`이며, 이 기준을 테스트와 결과 문서에서 직접 증명한다.

추가 세부 지시:

- 산출물 `EngineTrace/TraceEvent`이 어느 파일과 public API에 반영되는지 결과 문서에 적는다.
- AS-IS와 다르게 설계한 부분은 이유를 적는다. 단, 화면/연출/입력/UI 차이는 Headless 설계 차이로 분리한다.
- 상태를 바꾸는 작업이면 변경 전 상태, 입력, 변경 후 상태, 발생 이벤트를 테스트에서 확인한다.
- 실패 결과가 가능한 작업이면 예외만 던지고 끝내지 말고 호출자가 검증할 수 있는 실패 모델 또는 명확한 예외 계약을 정한다.

## 7. 하지 말아야 할 작업

- 원본 `DCGO/Assets/...` 파일을 수정하지 않는다. 필요한 경우 읽기 전용으로만 확인한다.
- Goal 범위를 넘어 다음 Goal이나 상위 Phase 전체를 함께 처리하지 않는다.
- 단위테스트와 결과 문서 없이 완료를 선언하지 않는다.
- 완성 기준을 충족하지 않는 빈 동작, 자리표시 구현, TODO-only 구현을 완료로 보지 않는다.
- Phase 1에서는 Unity 대체 기반까지만 구현하고, 카드별 실제 효과 포팅은 시작하지 않는다.
- asset/card effect 실제 포팅은 해당 단계가 열리기 전까지 수행하지 않는다.

## 8. 단위테스트 지시

CSV 기준 단위테스트 범위:

> record snapshot clear fingerprint 테스트

반드시 포함할 테스트 관점:

- Given 같은 seed와 action sequence, When 두 번 실행하면, Then trace와 fingerprint가 일치한다.
- Given 다른 seed, When random choice나 shuffle을 수행하면, Then 결과 차이가 trace에 설명 가능하게 남는다.
- Given forbidden dependency 문자열, When scan을 실행하면, Then Headless 영역의 위반 파일을 검출한다.
- Given Null sink, When log/trace를 기록하면, Then 게임 상태가 바뀌지 않는다.
- CSV에 적힌 단위테스트 범위 `테스트는 'record snapshot clear fingerprint 테스트'를 직접 검증해야 한다. 정상 케이스, 실패/예외 케이스, 결정성이 필요한 경우 동일 입력 반복 케이스를 포함한다. 테스트 파일명과 테스트 명령을 결과 문서에 기록한다. Goal 범위 밖 동작을 검증하기 위해 새 구현을 끌어오지 않는다.`가 실제 테스트명 또는 assertion으로 추적 가능해야 한다.

테스트 작성 규칙:

- 테스트는 Goal 산출물의 public API 또는 문서 검증 포인트를 직접 호출해야 한다.
- 같은 입력을 반복했을 때 결과가 달라질 수 있는 부분은 seed 또는 deterministic fixture를 고정한다.
- 실패 케이스는 최소 1개 이상 포함한다. 입력 검증, illegal action, 누락 데이터, 잘못된 상태 중 Goal에 맞는 것을 고른다.
- 테스트 명령은 `.\.dotnet\dotnet.exe run --project <테스트 csproj>` 형태로 결과 문서에 기록한다.
- 테스트가 아직 생성되지 않은 Goal이면 이 Goal에서 테스트 프로젝트 또는 테스트 파일을 함께 만든다.

## 9. 결과 문서 작성 지시

결과 문서 경로:

- `docs/test-results/goals/G1I-002_engine_trace_unit_test_results.md`

결과 문서에는 다음 항목을 반드시 포함한다.

- Goal ID와 제목: `G1I-002 EngineTrace`
- 실행 일시와 실행 환경
- 수정/생성 파일 목록
- 읽기 전용으로 확인한 AS-IS 파일 목록
- 테스트 명령 전체
- 전체/통과/실패/스킵 수
- 실패 상세와 수정 여부
- 테스트하지 못한 항목과 이유
- 완료 기준 `EngineTrace 테스트 통과` 충족 근거
- 다음 Goal 진행 가능 여부

## 10. 완료 판정 체크리스트

- [ ] 선행 Goal `G1A-001` 상태를 확인했다.
- [ ] 작업 범위 `trace sequence와 fingerprint 확정` 밖의 변경을 하지 않았다.
- [ ] 원본 `DCGO/Assets/...` 파일을 수정하지 않았다.
- [ ] 대상 파일과 AS-IS 확인 파일을 결과 문서에 기록했다.
- [ ] 산출물 `EngineTrace TraceEvent`을 구현 또는 문서화했다.
- [ ] 단위테스트 `record snapshot clear fingerprint 테스트`를 작성했다.
- [ ] 단위테스트를 실행했고 실패가 없다.
- [ ] 금지 dependency 또는 금지 작업 위반이 없다.
- [ ] 결과 문서 `docs/test-results/goals/G1I-002_engine_trace_unit_test_results.md`를 작성했다.
- [ ] 완료 기준 `EngineTrace 테스트 통과`을 결과 문서에서 증명했다.

## 11. 실행 프롬프트

```text
HeadlessDCGO.Engine Goal G1I-002를 수행하라.

반드시 먼저 이 상세 지시서를 읽어라:
docs/goal-specs/G1I-002_enginetrace.md

이번 작업은 G1I-002 하나만 완료하는 것이 목표다.
선행 Goal: G1A-001
작업 범위: trace sequence와 fingerprint 확정
산출물: EngineTrace TraceEvent
단위테스트 범위: record snapshot clear fingerprint 테스트
결과 문서: docs/test-results/goals/G1I-002_engine_trace_unit_test_results.md
완료 기준: EngineTrace 테스트 통과

원본 DCGO/Assets 파일은 수정하지 말라.
Goal 범위 밖 작업을 하지 말라.
단위테스트와 결과 문서 없이는 완료로 말하지 말라.
```
