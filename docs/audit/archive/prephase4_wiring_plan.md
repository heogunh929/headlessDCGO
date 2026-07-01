# Phase 4 사전 배선 계획 (Pre-Phase-4 Wiring)

- 작성일: 2026-06-26
- 배경: Phase 4(카드 효과 3918개 포팅)를 **로컬 LLM**이 수행 예정. LLM이 "카드 효과 본문만 채우면 자동 동작"하도록, 효과 인프라(이벤트→트리거→mutation→choice)를 **미리 배선**해 둔다. LLM이 엔진 구조(트리거 발화·시큐리티 파이프라인 등)까지 건드리지 않게 하는 것이 목표.
- 근거: `docs/audit/rl_gap_remediation_design.md`(RL 갭) 이후, 효과 배선 갭을 별도 조사(3 에이전트)로 정량화.

## 배선 갭 평가 (조사 결과)

| 영역 | 배선 | Phase 4 영향 |
|------|------|--------------|
| 트리거 타이밍 emission | 🔴 ~5% (CardMoved/AttackDeclared만, 원본 62 타이밍 중 대부분 이벤트 미발행) | 효과 채워도 발동 자리 없음 (최대 차단) |
| Mutation sink 어휘 | 🟡 8 키워드 중 5 적용, 3 드롭(Blitz/Retaliation/ArmorPurge). 이동/DP/부여 등 광범위 어휘 부재 | emit해도 무시 |
| B-01 lookup 하드코딩 | 🔴 player 1 고정 obsolete 오버로드 잔존 | 잘못된 바인딩 |
| 시큐리티 효과 배선 | 🟡 Hook 존재, SecurityResolver 미연결 | 시큐리티 효과 안 뜸 |
| 시큐리티 디지몬 전투 / Counter | 🔴 메커니즘 없음 | 신규 구현 |
| Effect-driven choice | 🟡 EffectChoiceHelpers 호출처 없음, DeferredChoiceProvider 없음 | 효과 중 선택 불가 |

## 작업 로드맵 (커버리지순)

| # | 작업 | 레버리지 | 상태 |
|---|------|----------|------|
| **W1** | 트리거 타이밍 이벤트 emission + 매핑 | ⭐⭐⭐ | **✅ 완료** (W1-1 파생 + W1-2 발행) |
| W2 | Mutation sink 어휘 확장 (이동/DP/부여/제한 + 드롭 3종) | ⭐⭐⭐ | **✅ 완료** (W2-core + W2-follow) |
| W3 | B-01 lookup 하드코딩 제거 | ⭐ | **✅ 완료** |
| W4 | 시큐리티 효과 배선 (SecurityResolver → SecurityCheck 이벤트) | ⭐⭐ | **✅ 완료** (subject-scoped) |
| W5 | 시큐리티 디지몬 전투 (신규) | ⭐⭐ | **✅ 완료** (DP 비교·StopSecurityCheck·Jamming) |
| W6 | Counter 페이즈 (신규) | ⭐ | **✅ 완료** (OnCounterTiming 윈도우) |
| W7 | Effect-driven choice (DeferredChoiceProvider) | ⭐⭐ | **✅ 완료** (suspend/resume) |

## W1 — 진행 로그

### W1-1: 트리거 타이밍 파생 + 다중수집 (2026-06-26)
- 문제: 수집기 `ResolveTiming`이 이벤트당 **단일 타이밍**(이벤트 타입명 or metadata override)만 산출 → `CardMoved`는 "CardMoved" 타이밍만 열고, 카드가 바인딩하는 의미 타이밍(OnPlay/OnDeletion/OnEnterField 등)은 안 열림.
- 조치:
  - `Effects/TriggerTimings.cs` — **canonical 타이밍 어휘**(원본 `EffectTiming` enum 정렬). 엔진 발화 ↔ 카드 바인딩의 계약.
  - `Effects/TriggerTimingMap.cs` — `Derive(gameEvent)`가 **구조화 이벤트(B2 ZoneFrom/ZoneTo)에서 다중 타이밍 파생**. 존 전이: Hand→BattleArea=OnPlay+OnEnterField, X→Trash=OnDeletion, field→비field=WhenRemoveField+OnLeaveField, →Hand=OnAddHand(+OnReturnToHand), →Library=OnReturnToLibrary, →Security=OnAddSecurity, Security→=OnLoseSecurity. AttackDeclared=OnUseAttack, SecurityCheck=OnSecurityCheck. metadata override 최우선, 이벤트 타입명은 **항상 추가**(back-compat).
  - `AutoProcessingTriggerCollector.CollectAndEnqueueAll` — 파생된 모든 타이밍에 대해 수집, effect id로 dedup. `Collect`는 `CollectForTiming`으로 리팩터(기존 동작 보존).
  - `GameFlowProcessor.AutoProcessAsync` → `CollectAndEnqueueAll` 사용.
- 테스트: 신규 `tests/G3.5-W1.TriggerTimingWiring.Tests` 8/8 PASS — 파생 정확성 6종 + **OnDeletion 효과가 카드 trash 시 실제 발화** + 다중타이밍 dedup. 영향권(G2F-001, G3.5-006, GameFlowProcessor, AttackPipeline, V 인터페이스) 회귀 0(additive).
### W1-2: 타이밍 이벤트 발행 지점 추가 (2026-06-26) — **W1 완료**
- 문제: W1-1은 이미 흐르는 이벤트(CardMoved/Attack)에서 파생만. 존이동이 없는 타이밍(턴/진화/드로우/시큐리티)은 **이벤트 자체가 발행 안 됨**.
- 조치:
  - `Effects/TriggerEventEmitter.cs` — `Emit(queue, timing, actor, subject)`로 `triggerTiming` 명시 이벤트를 `GameEventQueue`에 발행(MatchesEvent 필터 없음 → 해당 타이밍의 모든 효과 수집, 각자 self-gate = 원본 `StackSkillInfos(timing)` 방식). 결정성 유지(sequence 즉시 drain).
  - 발행 지점 배선: **OnEndTurn+OnStartTurn**(`MetadataActionProcessor.EndTurn`), **WhenDigivolving**(`DigivolveAction`), **OnDraw**(`HeadlessEarlyPhaseFlow` 드로우), **OnSecurityCheck**(`SecurityResolver` 카드별).
  - `TriggerTimings`에 WhenDigivolving/OnDraw 추가.
- 테스트: 신규 `tests/G3.5-W1b.TimingEventEmission.Tests` 5/5 PASS — emitter 발행+파생, 상수, **OnEndTurn/OnStartTurn 효과가 턴 종료 시 실제 발화(E2E)**, 무관 타이밍 미발화. 영향권(turn/digivolve/security/draw/loop/V) 회귀 0(additive).
- **남은 미세 갭**: 턴1의 OnStartTurn(이전 EndTurn 없음), Counter(W6)·Block/Counter 타이밍, OnGetDamage 등 일부 — 해당 카드군 포팅 시 발행지점 추가. 핵심 빈도 타이밍은 커버됨.

## W2 — 진행 로그

### W2-core: 동기 metadata mutation 어휘 (2026-06-26)
- 아키텍처: 효과는 **오직 `IEffectMutationSink.Apply(EffectMutation)`(동기)**로만 상태 변경(`CardEffectResolveContext`는 ZoneMover/Memory 미노출). sink는 `ICardInstanceRepository` 메타데이터만 씀.
- 조치(`MatchStateMutationSink` 어휘 확장 — 모두 동기 메타 쓰기):
  - **AddDpModifier** → 타입드 `DpModifier`(B1)를 `dpModifiers` 리스트에 append. **BattleResolver·CardObservationView가 이미 읽으므로 전투 DP·관측 DP에 즉시 반영.** (가장 큰 가치)
  - **Suspend/Unsuspend** → `isSuspended` 플래그.
  - **SetFlag/ClearFlag** → 임의 named 플래그(제한/once-flag/커스텀).
  - 드롭됐던 3 키워드(**RequestBlitzAttack/DeleteRetaliationTarget/ApplyArmorPurge**) → 플래그로 applied(더 이상 unsupported 아님). *단 실제 동작은 소비처 배선 필요.*
  - 미지 kind는 target 확인 **전에** unsupported로 분류(기존 동작 보존).
- 상수 공개(`MatchStateMutationSink.*Kind`, `*Key`) = **Phase 4 효과→상태 어휘 계약**.
- 테스트: 신규 `tests/G3.5-W2.MutationVocabulary.Tests` 8/8 PASS — DP 상대/절대/누적(DpCalculator 연동), suspend, named flag, 키워드 applied, 미지 unsupported, 미target skip. 영향권(G3.5-001/002 sink, 키워드 batch) 회귀 0.
- **W2-follow (남음, 비동기)**: 카드 이동(trash/bounce/draw/recover/return), 메모리 변경. 이들은 `ZoneMover.MoveAsync`/`MemoryController`가 필요(비동기/컨트롤러). 패턴: sink에 EngineContext + pending-op 큐를 주고, resolver가 `ResolveAsync` 후 `await FlushAsync(context)`로 적용. (effect→state의 single choke point는 그대로 유지.)

### W3: B-01 lookup 하드코딩 제거 (2026-06-26)
- 문제: `CardEffectFactoryBinding`에 `Lookup(card, trigger)` obsolete 오버로드가 `new HeadlessPlayerId(1)` 하드코딩 → player 2 효과가 player 1로 조회될 위험(B-01).
- 조치: **obsolete 2-인자 오버로드 제거.** 모든 조회는 `Lookup(card, trigger, controllerId, context)`로 controller 명시. 프로덕션 호출처 0건이라 비파괴(테스트 1곳만 갱신).
- 테스트: `G3.5-003` 갱신 — (1) 2-인자 오버로드가 **제거됐는지** 리플렉션 확인, (2) 소스에 `new HeadlessPlayerId(1)` 하드코딩 **부재** 확인, (3) 매칭 테스트는 4-인자 명시 controller로 전환. 7/7 PASS.

### W2-follow: 비동기/컨트롤러 mutation (2026-06-26) — **W2 완료**
- 조치(`MatchStateMutationSink`에 ZoneMover/MemoryController 주입 + 지연 적용):
  - **이동(비동기, flush 적용)**: TrashCard/ReturnToHand/ReturnToDeckTop/ReturnToDeckBottom/AddToSecurity → `Apply`에서 pending op로 기록, `FlushAsync(ct)`에서 `IZoneMover` 편의 메서드로 적용. **DrawCards**(playerId/count)도 동일.
  - **메모리(동기)**: AddMemory/SetMemory → `IHeadlessMemoryController.Add/Set` 즉시.
  - `IEffectMutationSink`에 `FlushAsync` 기본(no-op) 메서드 추가 → `CardEffectSchedulerResolver`가 `ResolveAsync` 직후 `await sink.FlushAsync(ct)`. RecordingEffectMutationSink 등은 no-op.
  - `EngineContext.CreateDefault`가 zoneMover/memoryController를 hoist해 production sink에 주입.
- 테스트: 신규 `tests/G3.5-W2b.AsyncMutations.Tests` 6/6 PASS — trash/bounce 적용, **flush 전까지 지연**, draw, 메모리 즉시, zoneMover 없으면 unsupported. 영향권(G3.5-001/002 resolver/sink, G3A-001 계약, V) 회귀 0.
- 결과: **Phase 4 LLM이 효과에서 이동/드로우/메모리/DP/키워드/플래그 mutation을 emit하면 모두 상태에 반영**됨. (가장 흔한 효과군 커버.)

### W4: 시큐리티 효과 배선 (2026-06-26) — **W4 완료**
- 갭의 정확한 위치: `SecurityResolver`는 이미 공개 카드마다 `OnSecurityCheck` 타이밍 이벤트를 **발행**(W1-2)하고, 공용 루프가 이를 수집·해결한다. 그러나 `TriggerEventEmitter.Emit`이 **카드 필터를 전혀 안 걸어서**, 한 장이 공개될 때 *모든* 카드의 OnSecurityCheck 효과가 발동되는 over-fire 상태였음. `[Security]` 효과는 **공개된 그 카드의 효과만** 발동해야 함.
- 조치(`TriggerEventEmitter.Emit`): `subject` 카드가 주어지면 메타데이터 `SourceEntityIdKey`에 기록 → 컬렉터의 `MatchesEvent`가 `Context.SourceEntityId`로 필터링해 **subject 카드의 효과만 발동**. subject가 없으면(턴 경계·드로우) 종전대로 전역 윈도우 유지. 부수효과로 **WhenDigivolving**도 진화한 카드로 스코핑됨(올바른 동작: 진화체 자신의 효과만).
- 단일 enqueue 경로(공용 루프 AutoProcess) 유지 — SecurityResolver는 별도 enqueue 경로를 만들지 않음.
- 테스트: 신규 `tests/G3.5-W4.SecurityEffectWiring.Tests` 5/5 PASS — (1) 스코프된 OnSecurityCheck는 subject 효과만 발동, (2) 다른 카드 효과 휴면, (3) subject 없으면 전역 발동 유지, (4) SecurityResolver가 공개 카드로 스코핑된 윈도우를 발행, (5) E2E: 공개된 시큐리티 카드 효과 1회 발동·다음 카드 미발동. 회귀(W1/W1b/G2G-004/G3.5-005/006/001/G2E-002/G2F-004/V) 0.
- 결과: **Phase 4 LLM이 `[Security]` 효과를 OnSecurityCheck로 바인딩하면, 그 카드가 시큐리티 체크로 공개될 때만 정확히 발동**됨.

### W5: 시큐리티 디지몬 전투 (2026-06-27) — **W5 완료**
- AS-IS 근거(`CardController.ISecurityCheck` line 3963·4123~4180): 공개된 시큐리티 카드가 `IsDigimon`이면 `IBattle(AttackingPermanent, DefendingCard: brokenSecurityCard)` — 공격자와 전투. `StopSecurityCheck()`(line 3895)은 공격자가 사라지면 체크 중단.
- 조치(`SecurityResolver`): 체크 루프에서 카드 trash 이동 후, 공개 카드가 디지몬이면 `ResolveSecurityDigimonBattleAsync` —
  - 공격자/시큐리티 DP를 `DpCalculator.ComputeDp`(base + modifiers)로 산출(BattleResolver와 동일).
  - 공격자 DP **≤** 시큐리티 DP → 공격자 삭제(equal=상호; 시큐리티는 체크로 이미 trash). `preventBattleDeletion`(Jamming이 부여) 시 생존.
  - 공격자 삭제 시 `deletedByBattle`/`dpBeforeBattle` 스탬프 + BattleArea→Trash 이동, **루프 break**(StopSecurityCheck 재현).
  - **DP 미정의면 전투 스킵**(BattleResolver의 "no battle DP"와 동일) — DP-less 추상 픽스처(G2G-004 등) 비파괴.
  - `SecurityResolutionResult`에 `SecurityDigimonBattles`/`AttackerDeletedBySecurity` 추가(가산적).
- **Jamming surface 활성화**: C2에서 "시큐리티 전투 미모델이라 Jamming 적용면 없음"으로 보류했던 부분 — 이제 보안 전투가 `preventBattleDeletion`을 존중하므로 Jamming(=preventBattleDeletion 부여)이 실제로 작동. BattleResolver 주석도 갱신.
- 테스트: 신규 `tests/G3.5-W5.SecurityDigimonBattle.Tests` 6/6 PASS — 강한 공격자 생존, 약한 공격자 삭제, equal 상호 삭제, **Jamming 생존**, 삭제 시 체크 중단(2장 중 1장만), 비-디지몬 시큐리티 전투 없음. 회귀: G2G-004(픽스처 DP 없음→전투 스킵)·C2·005·V 전부 PASS.

### W7: Effect-driven choice (2026-06-27) — **W7 완료**
- 목표: 효과 실행 중 발생하는 선택을 에이전트 행동으로 노출(스크립트 provider가 inline 결정하던 것을 A2 경로로). 
- 기반 substrate(이미 존재): `EffectResolutionStatus.Suspended` enum, `EffectScheduler`의 peek-후-Resolved시에만-dequeue(미해결 효과는 큐에 잔류·재실행), 공용 루프가 pending choice에서 일시정지, A2가 pending choice를 ResolveChoice 행동으로 노출.
- 조치:
  - 신규 `Runtime/DeferredChoiceProvider.cs` — `IChoiceProvider`+`IDeferredChoiceCoordinator`. `ChooseAsync`: 누적 답변이 있으면 순서대로 replay; 없으면 `ChoiceController.RequestChoice`로 surface + `DeferredChoicePendingException` throw. `BeginResolution`(에이전트 답변 harvest+cursor rewind)/`CompleteResolution`(답변 클리어) 훅.
  - `EffectResult.Suspended`(Resolved=false, Status=Suspended) 팩토리 + `IsSuspended`.
  - `EffectChoiceHelpers.ResolveAsync`·`HeadlessCardEffectResolver.ResolveAsync`가 `DeferredChoicePendingException`을 **삼키지 않고 rethrow**(기존 generic catch가 Failure로 변환하던 것 우회).
  - `CardEffectSchedulerResolver.Create`에 coordinator 훅 — 효과 전 `BeginResolution`, deferral catch시 `Suspended` 반환(flush/complete 안 함), 완료시 `CompleteResolution`.
- 재실행 계약: 효과는 **choose-then-apply**(선택을 모두 받은 뒤 적용)여야 재실행 안전(중복 적용 방지). Phase 4 효과 작성 가이드.
- 테스트: 신규 `tests/G3.5-W7.DeferredChoice.Tests` 5/5 PASS — 단일 선택 suspend→surface→answer→resume, suspended 효과 큐 잔류, **다중 선택 2사이클**(순서 보존), skip 답변, standalone provider replay. 회귀: G3.5-001/002·G1F-003·G3A-001·G2F-003·G3K-001·B2B3·W2b·A2 전부 PASS.
- 한계/follow-up: 효과 본문이 provider를 얻는 표준 경로(effect context 노출)는 더 큰 effect-context 설계의 일부 — DeferredChoiceProvider 사용 시 `EngineContext.ChoiceProvider`로 주입(기본은 ScriptedChoiceProvider 유지, 기존 테스트 비파괴).

### W6: Counter 타이밍 (2026-06-27) — **W6 완료**
- AS-IS 근거(`AttackProcess`: line 242 `State=Counter` → `CounterTiming()` line 255~ → Block): 공격 선언 후 블록 전에 카운터 타이밍이 열려 `EffectTiming.OnCounterTiming` 효과 발동(StackSkillInfos).
- 조치: `TriggerTimings.OnCounter="OnCounterTiming"` + `AttackPipeline.AdvanceBlockTiming`(phase=Declared)에서 블록 요청 **전에** `TriggerEventEmitter.Emit(OnCounter, actor=공격자)` — **글로벌 윈도우**(subject 없음 → 모든 OnCounter 효과 수집·self-gate). 새 phase 추가 없이 기존 전이(Declared→Blocking/Combat) 보존(공격 테스트 비파괴).
- 테스트: 신규 `tests/G3.5-W6.CounterTiming.Tests` 3/3 PASS — 선언 공격 advance 시 OnCounter 윈도우 발행, **글로벌(두 카드 OnCounter 효과 모두 발동)**, 블록 전 발행. 회귀(G3.5-005·G2G-005·C2·V) 0.
- 한계: 원본의 비-[Counter]/[Counter] 2단계 분리는 단일 윈도우로 단순화(효과 self-gate가 IsCounterEffect 구분 담당). 카운터 카드 포팅 시 세분화 가능.
