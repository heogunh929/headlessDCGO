# RL 엔진 갭 조치방안 설계 (Remediation Design)

- 작성일: 2026-06-26
- 목적: 외부(GPT) 분석 + 본 저장소 코드 교차검증으로 확정된 9개 P0/P1 갭에 대한 **조치 설계**를 고정한다. (구현 전 설계 단계)
- 검증 근거: `src/HeadlessDCGO.Engine/Headless/**` 실제 코드 + `DCGO/Assets/Scripts/Script/**` 원본 대조
- 선행 문서: [engine_flow_asis_vs_tobe.md](engine_flow_asis_vs_tobe.md), [phase3_parity_audit_report.md](phase3_parity_audit_report.md)
- ⚠️ 참고: 위 두 audit 문서(2026-06-25)는 B-03/X-01/X-02/X-04/X-05를 "끊김"으로 기술하나, **현재 코드(2026-06-26)에서는 해당 배선이 연결 완료**되었다(`GameFlowProcessor.RunToStableAsync` while 루프 + `CardEffectSchedulerResolver` + `MatchStateMutationSink` 실측). 즉 통합 Phase 3.5 배선은 끝났고, **남은 갭은 배선이 아니라 콘텐츠(카드/규칙) + RL 인터페이스 정합성**이다.

---

## 0. 검증된 갭 요약

| # | 주장 | 판정 | 핵심 근거 |
|---|------|------|-----------|
| P0-1 | RL 에이전트가 실제 choice 불가 | 인정 | `HeadlessLegalActionDispatcher.cs:46-60`(pending choice면 빈 배열), `GameFlowProcessor.cs:45-48`(Paused), `PolicyChoiceProvider` 기본정책이 루프 내부 즉시 소비 |
| P0-2 | Action mask가 카드·대상 미구분 | 부분인정 | `ActionEncoder.cs:40-56`(actionIndex=타입 28슬롯), `EncodedActionMask.ToMaskVector`가 같은 슬롯에 OR. EncodedKey/`action.Id`로만 구분 |
| P0-3 | ApplyActionAsync가 권위 경계 아님 | 인정(경계는 존재) | `DcgoMatch.cs:211-241`(검증없이 enqueue), 재검증은 하류 `*Action.Validate()`에 산재 |
| P0-4 | 관측이 과다+과소 동시 | 인정 | (과다) `ObservationSnapshot.cs:240-254` hidden zone cardId 노출. (과소) `ObservationEncoder.cs:63-178` 존별 개수만 |
| P0-5 | permanent 모델이 규칙 담기 어려움 | 부분인정 | `CardInstanceState.cs` 진화스택=`SourceIds`, 상태=문자열 dict. 원본 `Permanent.cs:193-322` 타입드 스택+DP 우선순위 부재 |
| P0-6 | 전투·security 축약판 | 인정 | `BattleResolver.cs:35-40` 순수 DP비교, `SecurityResolver.cs:56-75` trash만(시큐리티 효과 미발동) |
| P0-7 | 효과 실패해도 성공처럼 진행 | 부분인정 | `CardEffectSchedulerResolver.cs:28-38` 미바인딩→`Success(unresolved:true)`, `EffectScheduler.cs:113` `!Resolved`에서만 중단 |
| P1 | GameEvent가 행동 의미 미표현 | 부분인정 | `GameEventType.cs:3-23` 일부 고수준 존재하나 Metadata 비정규화·zone-move 수준 |
| P1 | 공통 RuleProcess가 placeholder | 인정 | `GameFlowProcessor.cs:93-96` 항상 `false` 반환 |

종합: 5 인정 / 4 부분인정 / 0 반박. **2부류** — 콘텐츠 포팅으로 자동 해소(P0-7, 일부 P0-6) vs 설계가 필요한 구조적 갭(P0-1·2·3·4·5, P1×2).

---

## 1. 설계 원칙 (공통)

1. **단일 권위 경계**: "합법 행동 생성"과 "행동 수락 검증"이 같은 술어(predicate)를 공유한다.
2. **표현 못 하는 결정은 없다**: 게임이 묻는 모든 것(메인행동·choice·블록·머리건)은 legal action으로 노출.
3. **관측 = perspective 1인칭**: 항상 `perspectivePlayerId` 시점 필터. God's-eye는 디버그 전용.
4. **무음 실패 금지**: unresolved/illegal/no-op은 카운트·트레이스되어 관측 가능.
5. **스키마 버저닝**: 관측/행동 스키마에 버전을 박아 학습 코드가 깨짐을 감지.

---

## 2. 계층 A — RL 인터페이스 정합성 (학습 가능성의 전제조건)

콘텐츠 무관, 즉시 착수 가능. 이 계층 없이는 어떤 학습도 무의미.

### A1. 단일 권위 검증 경계 (P0-3)
- 근본원인: `ApplyActionAsync`는 검증 없이 enqueue, 검증은 하류 `*Action.Validate()`에 산재.
- 조치:
  - `IActionLegality` 서비스 신설 — `Validate(LegalAction, EngineContext) → LegalityVerdict{ IsLegal, Reason }`. 액션 타입별 검증을 여기로 추출.
  - `HeadlessLegalActionDispatcher`는 후보를 열거 → `IActionLegality`로 필터해 생성(생성/수락 술어 공유).
  - `DcgoMatch.ApplyActionAsync`/RL `StepAsync` 진입에서 enqueue 전 검증. illegal이면 **상태 변경 없이** `RlStepResult{ ActionRejected=true, reward=옵션 페널티, observation=동일 }`.
- 파일: 신규 `Services/IActionLegality.cs`·`ActionLegality.cs`; `HeadlessLegalActionDispatcher.cs`, `DcgoMatch.cs`, 각 `*Action.cs`.
- 수용기준: 임의 illegal action 주입 시 (a) state fingerprint 불변 (b) `ActionRejected=true` (c) legal 목록 불변.

### A2. Choice를 행동으로 노출 (P0-1) — 인프라 대부분 존재, 배선만
- 근본원인: pending choice면 dispatcher 빈 배열 + `PolicyChoiceProvider` 기본정책 즉시 소비.
- 기존 자산: `HeadlessActionTypes`에 `RequestChoice`/`ResolveChoice`/`ClearChoice` 상수 존재, `IHeadlessChoiceController.ResolveChoice(ChoiceResult)`(검증 `ThrowIfInvalid` 포함) 존재, `FlowProcessResult.Paused()` + G1E-005 pause/resume 계약 존재.
- 조치:
  - **`DeferredChoiceProvider` 신설**: 효과 중 choice 요청 시 외부 주입 선택이 있으면 소비, 없으면 `ChoiceController.RequestChoice()`로 pending 등록 + flow를 Paused로 unwind.
  - **Dispatcher 변경**: `Current.IsPending`일 때 빈 배열 대신 `PendingRequest.Candidates`마다 `ResolveChoice` LegalAction 방출.
  - **Processor 변경**: `ResolveChoice` 처리 시 `ChoiceController.ResolveChoice()` 호출 → provider 주입 → flow 재개.
  - RL env 기본 provider를 `DeferredChoiceProvider`로 교체(스크립트 시나리오는 `ScriptedChoiceProvider` 유지).
- 파일: 신규 `Choices/DeferredChoiceProvider.cs`; `HeadlessLegalActionDispatcher.cs`, `MetadataActionProcessor.cs`, `HeadlessRlEnvironment.cs`.
- 수용기준: choice를 요구하는 더미 효과로, step N에서 `ResolveChoice` 후보가 마스크에 노출되고 에이전트 선택이 결과를 가르는 E2E.

### A3. 행동공간 표현 — 고정 팩터드 마스크 (P0-2)  ★결정 확정
- 근본원인: `actionIndex`가 타입 28슬롯으로만 정규화 → 같은 타입 다른 카드/대상이 한 슬롯에 OR.
- **결정 (개인 학습용)**: **고정 팩터드 마스크를 1차 타깃으로 확정.** per-action 피처(가변 리스트)는 후속 옵션으로 보존.
  - 근거: 솔로 개발·기성 라이브러리 사용 조건에서는 표현력보다 도구 호환성·디버깅 용이성이 지배적. `MultiDiscrete([type, source, target])` + invalid-action mask는 `sb3-contrib MaskablePPO`/CleanRL에 바로 연결된다. 손패~10·필드 소수로 상한이 작아 sparse mask 부담이 미미. 가변 action-embedding 포인터망은 더 강력하나 커스텀 PyTorch 디버깅 비용이 솔로 진도를 잠식.
  - 비파괴 확장: 엔진은 이미 `EncodedActionMask.LegalActions`(가변 리스트)를 보존하므로, 추후 정책을 action-embedding으로 업그레이드할 때 엔진 재작업 불필요.
- 조치:
  - `ActionEncoder.EncodeFactored()` 추가 — index를 `(type, slot-role)`로 산정. PlayCard→hand slot(0..maxHand), Attack→(attacker field slot × defender slot|player), Digivolve→(target field slot × hand slot) 등.
  - 고정크기 마스크 벡터(각 칸 = 구체적 (타입,위치) 쌍). `RlVectorSchema`에 slot-role 차원 등록 + 스키마 버전.
  - (옵션, 후속) per-action `ActionFeature[]`(타입 one-hot, cardNumber 임베딩 id, 대상 position, 비용, choice 인덱스).
- 파일: `Runtime/ActionEncoder.cs`, `Runtime/ActionMask.cs`, `Runtime/RlVectorSchema.cs`, (옵션) 신규 `Runtime/ActionFeature.cs`.
- 수용기준: PlayCard(카드A) vs PlayCard(카드B)가 서로 다른 fixed index. 충돌 0. MultiDiscrete 마스크가 합법 조합만 1.

### A4. 관측 perspective 필터 + 카드 피처 (P0-4)
- 근본원인: `GetObservation()`이 visibility 무시·hidden zone cardId 노출 + 인코더는 존별 개수만.
- 조치:
  - (과다 제거) `HeadlessRlEnvironment.Observe()`가 `VisibilityView.ForPlayer(perspectivePlayerId)` 경유. hidden zone(상대 hand/library/security)은 count-only, 본인/공개존만 cardId.
  - (과소 보강) `ObservationEncoder`에 **카드 피처 블록** 추가: 가시 카드별 cardNumber(임베딩 id), cardType, DP, level, play/evo cost, suspended, 진화스택 깊이, 주요 modifier 요약. 존별 최대 슬롯 고정(field N체 × K피처).
  - 디버그용 `ForDebugFull()` 토글 유지 + 스키마 버전.
- 파일: `Runtime/HeadlessRlEnvironment.cs`, `Runtime/ObservationEncoder.cs`, `Runtime/ObservationSnapshot.cs`, `State/VisibilityView.cs`.
- 수용기준: 상대 hand cardId 0건 노출, 자기 field digimon DP/타입 관측에 존재.
- 의존: 카드 DP/스택 피처는 **B1**에 의존 → A4의 카드-피처 부분은 B1 후 완성. 누출 제거 부분은 선행 가능.

---

## 3. 계층 B — 상태 모델 & 진실성 인프라 (규칙 정확성의 전제조건)

### B1. 1급 permanent/카드 상태 모델 (P0-5) — 최대 리팩터·최대 레버리지
- 근본원인: 진화스택=`SourceIds`(평면), 상태=문자열 dict. 원본 타입드 CardSource 스택+DP 우선순위 부재.
- 조치:
  - 진화스택을 `StackedCard{ instanceId, cardNumber, role(Egg/Digivolution/Top), level }` 순서있는 리스트로.
  - 타입드 수정치 모델 `DpModifier{ delta, source, isUpDown, activatedOrder, duration }` 리스트 + 원본 `Permanent.GetDP` 우선순위(UpDown vs NotUpDown, ActivatedTime 정렬) 재현 `ComputeDp()`.
  - 부여/계승 효과 리스트, 링크 상태, DigiXros 소재를 1급 필드로. 핵심(DP·제한)은 타입드 백킹.
- 파일: `State/CardInstanceState.cs`, `Services/CardInstanceRecord.cs`, 신규 `State/DigivolutionStack.cs`·`State/DpModifier.cs`; 소비처 `BattleResolver`·`ObservationEncoder`.
- 수용기준: 원본 DP 계산(수정치 누적+우선순위) 골든 테스트 일치.

### B2. 구조화 GameEvent 스키마 (P1)
- 근본원인: Metadata free-form dict, 대체로 zone-move 수준.
- 조치: `GameEvent{ seq, type, actor(playerId), subject(cardInstanceId), target, zoneFrom, zoneTo, cause(effectId|actionId) }` 타입드 정규화. trigger 수집·리플레이·reward shaping 신호가 소비.
- 파일: `Runtime/GameEvent.cs`, `Runtime/GameEventType.cs`, `Runtime/GameEventQueue.cs`; 발행처 `DcgoMatch`·`ZoneMover`.
- 수용기준: "공격으로 상대 디지몬 삭제" 이벤트가 actor/subject/target/cause 모두 채움.

### B3. 효과 해석 상태 가시화 (P0-7)
- 근본원인: 미바인딩 효과가 `Success(unresolved:true)`로 무음 통과.
- 조치: `EffectResolutionStatus{ Resolved, Unbound, Failed, Suspended }` 도입. `Unbound`는 트레이스+카운트, 테스트 strict 모드 throw, 런타임/학습은 no-op이되 관측/메트릭에 unresolved 카운트 노출.
- 파일: `Effects/EffectResult.cs`, `Effects/CardEffectSchedulerResolver.cs`, `Effects/EffectScheduler.cs`, `Diagnostics/EngineTrace.cs`, `Runtime/ObservationEncoder.cs`.
- 수용기준: skeleton 효과 enqueue 시 unresolved 카운트 증가가 관측에 반영.

---

## 4. 계층 C — 규칙 충실도 (B 위에 구축, 이후 Phase 4 카드 포팅)

### C1. 공통 RuleProcess 구현 (P1)
- 근본원인: `GameFlowProcessor.RuleProcess()`가 항상 `false`(G3.5-007 placeholder).
- 조치: 원본 `AutoProcessCheck`의 "룰 절반" state-based action — 메모리 정산, suspend/unsuspend 정리, 시큐리티 0장+직접타격→패배, 삭제 정리, hand size 등.
- 파일: `Runtime/GameFlowProcessor.cs`, `State/PlayerRuleAdapter.cs`, `Runtime/TerminalEvaluator.cs`.
- 의존: B1, B2.

### C2. 전투·시큐리티 풀 규칙 (P0-6)
- 근본원인: `BattleResolver`=순수 DP비교, `SecurityResolver`=trash만, 시큐리티 효과 미발동.
- 조치:
  - `BattleResolver`: `ComputeDp`(B1) 사용 + 키워드/효과 훅(Piercing→초과데미지 시큐리티, Jamming, SecurityAttack±, can't-be-deleted-by-battle).
  - `SecurityResolver`: 공개 시큐리티마다 효과 발동(효과시스템 경유), 공격자 삭제/게임종료 가능, SecurityAttack 다중 체크.
- 파일: `Runtime/BattleResolver.cs`, `Runtime/SecurityResolver.cs`, `Runtime/AttackPipeline.cs`.
- 의존: B1, B3, 효과시스템.
- → 이후 Phase 4: 카드 효과 3918개 포팅(대표 카드 골든 시나리오 + fingerprint 회귀 게이트 동반).

---

## 5. 의존성 로드맵

```
A1(검증경계) ──┬─> A2(choice 행동화) ──> A3(팩터드 행동공간, 고정마스크)
               └─> A4-누출제거 ───────────────────────────┐
                                                           │
B1(상태모델) ──┬───────────────────────> A4-카드피처 완성  │
               ├─> B2(이벤트스키마)                        │
               └─> B3(효과 가시화)                         │
B1+B2 ──> C1(RuleProcess) ──┐                              │
B1+B3 ──> C2(전투/시큐리티) ─┴─> Phase 4 카드 포팅 ─────────┘
```

권장 착수 순서: **A1 → A2 → A4(누출제거) → B1 → A3 + A4(카드피처) → B2/B3 → C1 → C2 → Phase 4**.
- A1·A2·A4누출제거: 싸고 학습 차단 즉시 해제.
- B1: 이후 거의 모든 것의 토대 → 일찍.
- A3: B1 카드 피처가 있으면 더 풍부 → B1 직후.

---

## 6. Goal 분할안 (구현 단계용)

| Goal | 내용 | 의존 | 수용 테스트 |
|------|------|------|-------------|
| G3.5-RL-A1 ✅ | IActionLegality 단일 검증 경계 | — | illegal 주입 → 상태불변+reject (7/7 PASS) |
| G3.5-RL-A2 ✅ | 펜딩 choice를 ResolveChoice 행동으로 노출 (선택값 carry) | A1 | choice E2E (6/6 PASS) |
| G3.5-RL-A3 ✅ | 고정 팩터드 행동공간(위치 기반 인덱스) | A1 | (타입,위치) 충돌 0 (9/9 PASS) |
| G3.5-RL-A4a ✅ | 관측 perspective 누출 제거 | — | 상대 hidden zone cardId 0 (6/6 PASS) |
| G3.5-RL-B1 ✅ | 타입드 DP 모델(ComputeDp) + 진화스택 | — | 원본 DP 우선순위 골든 + 전투 A/B (9/9 PASS) |
| G3.5-RL-A4b ✅ | 관측 카드 피처 블록 + 타입드 카드뷰(B1b-lite) | B1 | field DP/타입 노출 (5/5 PASS) |
| G3.5-RL-B2 ✅ | 구조화 GameEvent 스키마 | — | actor/subject/zone/cause (8/8 PASS, B3 공유) |
| G3.5-RL-B3 ✅ | EffectResolutionStatus 가시화 | — | unbound 카운트 관측 노출 |
| G3.5-RL-C1 ✅ | 공통 RuleProcess(state-based action) + 덱아웃 패배 수정 | B1,B2 | 덱아웃 승자/삭제 sweep (4/4 PASS) |
| G3.5-RL-C2 ✅ | 전투 키워드(Prevent/Piercing) + 원본대조 수정 | B1,B3 | 키워드별 삭제/시큐리티 (6/6 PASS) |

---

## 7. 부수 작업

- stale audit 갱신: `engine_flow_asis_vs_tobe.md` / `phase3_parity_audit_report.md`에 "3.5 배선 완료, 잔여 갭은 콘텐츠+RL 인터페이스" 노트 추가(후속 작업자 오판 방지).
- 회귀 게이트: 각 goal에 `StateFingerprintService`(SHA256 full-state) 기반 결정성 회귀 + 배치 병렬 결정성 검증을 CI 게이트로.

---

## 7.1 구현 진행 로그

### G3.5-RL-A1 — 완료 (2026-06-26)
- 신규: `Headless/Runtime/IActionLegality.cs`(인터페이스 + `LegalityVerdict`), `Headless/Runtime/LegalActionSetValidator.cs`(생성기와 술어 공유; 에이전트 행동 7종만 검사, 내부/시스템 액션은 deferred).
- 배선: `DcgoMatch` 4번째 ctor 인자 `IActionLegality?` 추가 → `ApplyActionAsync`가 enqueue 전 검증, illegal이면 `InvalidAction` 이벤트만 남기고 **enqueue·상태변경 없음**. 기본값 null이라 기존 244 테스트 비파괴.
- RL: `HeadlessRlEnvironment`가 기본 match에 validator 부착(`EnforceAgentActionLegality` 옵션, 기본 true) + apply 거부 시 loop step 건너뛰고 페널티 반환.
- **부수 버그 수정 (P0-4 관련, RL env 전체를 막던 latent NRE)**: `ObservationEncodingOptions`/`ActionEncodingOptions`의 **static 초기화 순서 버그** — `Default`가 `DefaultZoneOrder`/`DefaultActionTypeOrder`보다 먼저 초기화되어 `ZoneOrder`/`ActionTypeOrder`가 null → 실제 매치 관측/행동 인코딩 시 NRE. 선언 순서를 뒤집어 수정. (이 버그로 RL env 기본 관측이 한 번도 동작한 적 없었음 = RL 레이어 테스트 커버리지 부재의 방증.)
- 테스트: 신규 `tests/G3.5-RL-A1.ActionLegality.Tests` 7/7 PASS. 영향권(MatchLifecycle, Observation.LegalAction, Legal.action.dispatch, Phase.enum) 회귀 없음.
- 알려진 무관 실패: `G1E-001`이 `ChoiceType` enum 수를 7로 기대하나 실제 9 (선행 drift, 본 작업과 무관).

### G3.5-RL-A2 — 완료 (2026-06-26)
- 문제(P0-1): `ResolveChoiceAsync`가 펜딩 choice를 **provider가 대신 결정**(에이전트 개입 불가) + dispatcher가 펜딩 choice면 빈 배열 반환.
- 조치:
  - `HeadlessActionFactory.ResolveChoice(player, ChoiceResult, actionId)` 오버로드 — 선택값(ids/skip/count)을 action 파라미터(`ChoiceSelectedIds`/`ChoiceSkipped`/`ChoiceSelectedCount`, 기존 키)로 carry.
  - `HeadlessLegalActionDispatcher` — 펜딩 choice면 phase 행동 대신 **후보별 ResolveChoice 행동**(+CanSkip 시 Skip, Count형이면 count별) 방출. choice 소유자에게만 노출. 단일선택/Count 완전 지원, 다중선택(MinCount>1) 전체 부분집합 열거는 A3로 위임(현재 Skip만).
  - `MetadataActionProcessor.ResolveChoiceAsync` — action이 선택값을 carry하면 그것을 직접 적용(provider는 legacy/effect-driven fallback). Blocker 라우팅 유지.
  - `LegalActionSetValidator` — `ResolveChoice`를 에이전트 행동공간에 추가(A1과 결합: 펜딩 아닐 때 ResolveChoice 제출 → 경계가 거부).
- 테스트: 신규 `tests/G3.5-RL-A2.ChoiceAsAction.Tests` 6/6 PASS (후보 노출·소유자 한정·선택값 반영·skip·RL E2E·미펜딩 거부). 영향권(G1E-005 pause/resume, Scripted/Policy provider, BlockTiming, SecurityCheck, LegalDispatch) 회귀 0.
- **미완(의도적 defer)**: **effect-driven choice**(효과 실행 중 `EffectChoiceHelpers`가 provider.ChooseAsync 호출)의 pause/resume로 에이전트 노출하는 `DeferredChoiceProvider`는 **실제 choice를 요구하는 카드 효과가 생긴 뒤**(Phase 4 또는 합성 효과) 구현. 현재 모든 효과가 skeleton이라 이 경로는 휴면 상태이며, action-driven 경로(RequestChoice→ResolveChoice)로 메커니즘은 검증됨.

### G3.5-RL-A4a — 완료 (2026-06-26)
- 문제(P0-4 "너무 많음"): `HeadlessGameLoop.BuildZoneObservations`가 **모든 존의 카드 id를 무필터 노출** → self-play에서 상대 손패/덱/시큐리티 누출. `VisibilityView`는 별도 상태표현을 써서 관측 파이프라인에 미사용.
- 조치: `GetObservation`에 `perspectivePlayerId` 인자 추가, `BuildZoneObservations`가 **canonical `ZoneState.DefaultVisibility` 규칙** 적용 — viewer가 아닌 플레이어의 hidden zone(Library/Hand/Security/DigitamaLibrary)은 **count만 유지하고 card id는 withhold**. perspective=null이면 full(디버그/back-compat).
  - `DcgoMatch.GetObservation(perspective)` 오버로드 추가(no-arg는 full 유지 → 기존 테스트 비파괴).
  - `HeadlessRlEnvironment.Encode`가 **perspective-filtered 관측을 단일 소스로** 인코딩. perspective = `PerspectivePlayerId` 우선, 없으면 현재 턴 플레이어(self-play 시 행동 주체 시점).
- 테스트: 신규 `tests/G3.5-RL-A4a.ObservationVisibility.Tests` 6/6 PASS. count는 정확히 보존(인코더는 count만 내므로 인코딩 벡터 불변 → 회귀 0).
- **미완(B1 의존)**: **A4b(카드 피처 보강)** — 가시 카드의 cardNumber/타입/DP/진화스택/수정치를 관측에 인코딩하는 "너무 적음" 해소는 **B1(상태모델)** 이후. 현재는 존별 count만 인코딩됨.

### G3.5-RL-B1 — 핵심 완료 (2026-06-26), 일부 follow-up
- 문제(P0-5): `CardInstanceState`가 진화스택=평면 `SourceIds`, 상태=문자열 dict. 전투 DP는 `Metadata["dp"]` 단일 int(수정치 누적·우선순위 없음).
- 조치(타입드 DP 모델 — 원본 `Permanent.BaseDP` 충실 이식):
  - `State/DpModifier.cs` — `Relative`(±델타, 원본 IsUpDown) / `Absolute`(set, 원본 NotIsUpDown) + `ActivatedOrder`.
  - `State/DpCalculator.ComputeDp(baseDp, modifiers)` — **상대 델타 먼저 합산 → 절대 set을 ActivatedOrder 순(마지막 우선) → 0 클램프**. 원본 line 268-317 순서 그대로.
  - `State/DigivolutionStack.cs` + `StackedCard` — 순서있는 스택(egg→top), `TopCard`/`BaseDp`/`Depth`/`UnderCards`, 역할 검증.
  - `BattleResolver`가 base DP + 타입드 modifier(`dpModifiers` 메타키)를 `DpCalculator`로 합성. **modifier 없으면 base와 동일 → 회귀 0.**
- 테스트: 신규 `tests/G3.5-RL-B1.DpModel.Tests` 9/9 PASS — DP 우선순위 6종 골든 + 스택 2종 + **전투 A/B**(modifier 유무로 누가 죽는지 뒤집힘). 영향권(G2G-003 battle, AttackPipeline, G3H modifier) 회귀 0.
- **Follow-up(B1b, 별도)**: `CardInstanceState`의 문자열 `Modifiers`/`SourceIds`를 타입드 모델로 전면 교체, 링크/DigiXros 소재, 부여·계승 효과 1급화, 진화스택을 인스턴스 상태에 실제 저장(현재 DpCalculator/Stack는 값 모델 + battle 합성까지). A4b(카드 피처 인코딩)가 이 위에 올라감.

### G3.5-RL-A3 — 완료 (2026-06-26)
- 문제(P0-2): `ActionEncoder`가 타입(28슬롯)으로만 정규화 → 같은 타입 다른 카드/대상이 한 마스크 슬롯에 붕괴.
- 조치(고정 팩터드 행동공간, **위치 기반**):
  - `FactoredActionSchema` — 레인별 고정 용량(MaxHand/MaxField/MaxChoice) + 누적 오프셋 + `TotalSize` + Version. PlayCard→hand슬롯, Digivolve→hand×field, DeclareAttack→attacker×(field+1, +1=direct), ResolveChoice→candidate(+skip), 싱글톤(Pass/Advance/EndTurn/NoOp).
  - `FactoredPositionContext` — 존 순서(hand/field) + choice 후보로 위치 인덱스 해석. `FromContext(EngineContext)`(prod) + 명시 resolver ctor(test).
  - `FactoredActionEncoder.Encode` → `FactoredActionMask`(고정크기 mask vector + index→action 역참조 + **Unmapped 표면화**(용량초과 무음드롭 금지)).
  - `DcgoMatch.EncodeFactoredActionMask`, `HeadlessRlEnvironment.EncodeFactoredActionMask` / `StepByFactoredIndexAsync`.
- 결정: **고정 마스크 1차**(MaskablePPO/MultiDiscrete 호환). 가변 legal-action 리스트(`EncodedActionMask.LegalActions`)는 보존돼 추후 action-embedding 정책으로 비파괴 업그레이드 가능.
- 테스트: 신규 `tests/G3.5-RL-A3.FactoredActionSpace.Tests` 9/9 PASS — 카드/공격쌍/choice 후보 인덱스 distinct, 타입-only 붕괴 대조, 고정 벡터 one-hot, 용량초과 Unmapped, 매치 round-trip. **순수 추가(기존 경로 무변경) → 회귀 0.**
- **Follow-up**: 위치 인덱스는 현재 존 순서(삽입순) 기반 — 카드 이동 시 인덱스 shift 가능. 안정적 위치(보드 좌표 고정) 매핑은 학습 일관성을 위해 추후 개선 가능.

### G3.5-RL-A4b / B1b-lite — 완료 (2026-06-26)
- 문제(P0-4 "너무 적음"): 관측이 존별 count만 인코딩 → 카드 정체성·DP·타입·진화스택 등 의사결정 필수 정보 부재.
- 조치:
  - **B1b-lite — 타입드 카드뷰**: `Runtime/CardObservation.cs` + `CardObservationView.Build(instance, definition)` — 흩어진 메타데이터 읽기를 1개 타입드 record로 통합. **DP는 B1 `DpCalculator`로 계산**(관측 DP = 전투 DP 일치). cardNumber/type/level/cost/suspend/faceUp/stackDepth.
  - `ZoneObservation`에 `Cards`(가시 카드별 `CardObservation`) 추가 — hidden zone은 빈 채로(A4a 일관).
  - `HeadlessGameLoop.BuildZoneObservations`가 가시 존의 카드마다 `CardObservation` 빌드(Context repo 사용).
  - `ObservationEncoder`에 **고정 per-card 피처 슬롯**(`IncludeCardFeatures` 기본 on, `CardFeatureZones`=BattleArea, `MaxCardsPerZone`=8). 슬롯당 10피처(present/dp/level/playCost/evoCost/suspended/stackDepth/isDigimon/isTamer/isOption), 빈 슬롯 0-fill → 고정 벡터.
- 테스트: 신규 `tests/G3.5-RL-A4b.CardFeatures.Tests` 5/5 PASS — 가시 카드 DP=base+modifier(B1 일치)·타입·level, suspend·stackDepth, hidden zone 피처 0, 인코더 고정 슬롯, 비활성화 옵션. 영향권(A4a·Observation·A1·A3) 회귀 0.
- **남은 B1b(전면)**: `CardInstanceState`의 문자열 `Modifiers`/`SourceIds`를 타입드로 전면 교체, 링크/DigiXros 소재, 부여·계승 효과 1급화. (관측·전투엔 불필요해 보류; 효과 포팅 본격화 시 진행.)

### G3.5-RL-B2 / B3 — 완료 (2026-06-26)
- **B2(P1, 구조화 이벤트)**: `GameEvent`에 타입드 옵셔널 필드 `Actor/Subject/Target/ZoneFrom/ZoneTo/Cause` 추가(가산적, 기존 metadata 유지). `InMemoryZoneMover.RecordCardMoved`가 zone move마다 이 필드를 채움(actor=player, subject=card, zoneFrom/To, cause=operation). trigger 수집·리플레이·reward shaping 신호의 1급 소스.
- **B3(P0-7, 효과 상태 가시화)**: `EffectResolutionStatus{Resolved,Unbound,Failed,Suspended}` + `EffectResult.Unbound`(Resolved=true라 큐는 계속 비우되 status로 추적). `CardEffectSchedulerResolver`가 미바인딩 효과를 `Success(unresolved)` → **`Unbound`로** 변경. `EffectScheduler.TotalUnboundCount` 집계 → `HeadlessEffectState.TotalUnboundCount` → 관측 `effects.totalUnbound` 피처로 노출. **무음 성공 → 카운트 가능한 신호.**
- 테스트: 신규 `tests/G3.5-RL-B2B3.EventEffectStatus.Tests` 8/8 PASS — 구조화 필드·legacy 호환·plain null / Unbound status·Success/Failure 매핑·스케줄러 카운트(큐 드레인 유지)·실제 리졸버 Unbound·관측 노출. 영향권(EffectScheduler·AutoProcessing·Zone movement event·Observation) 회귀 0.
- **Follow-up**: B2 구조화 필드를 attack/action/terminal 이벤트까지 확대(현재 CardMoved 채움). B3 strict 모드(테스트에서 Unbound throw)는 필요 시 추가.

### G3.5-RL-C1 — 완료 (2026-06-26)
- 문제(P1): `GameFlowProcessor.RuleProcess()`가 항상 `false`(placeholder). 또한 **덱아웃 버그 발견** — `HeadlessEarlyPhaseFlow`가 덱아웃 시 `SetTerminal(true)`만 호출(패자 미지정) → 터미널이 **승자 없는 무승부**로 처리되어 RL 보상이 틀림.
- 조치:
  - **덱아웃 패배 수정**: 덱아웃 시 `PlayerStatusController.MarkLose(turnPlayer, "deck-out")` → `EndTurnCheck`/`TerminalEvaluator`가 **올바른 승자(상대)**로 터미널 확정. (RL 보상: 무승부 → 정확한 승/패)
  - **RuleProcess 구현**: state-based action 패스 — 필드 존(BattleArea/BreedingArea)에서 `pendingDeletion` 플래그가 붙은 카드를 trash로 sweep하고 플래그 해제. 포팅될 효과의 **통일된 삭제 경로**(AS-IS rule timing의 삭제 정리). 진행 시 true 반환해 루프가 안정될 때까지 반복.
- 테스트: 신규 `tests/G3.5-RL-C1.RuleProcess.Tests` 4/4 PASS — 덱아웃 패자 마킹·승자=상대(무승부 아님)·삭제 sweep·정상카드 미영향. 영향권(G2A-003 덱아웃, G3.5-008 terminal, G2C-002 loss check, GameFlowProcessor) 회귀 0.
- **남은 RuleProcess 확장(점진)**: 메모리 정산/클램프, suspend/unsuspend 정리, hand size 등 추가 state-based action은 효과/규칙 포팅과 함께 확장.

### G3.5-RL-C2 — 핵심 완료 (2026-06-26), 일부 follow-up
- 문제(P0-6): `BattleResolver`가 순수 DP 비교만(키워드 무시). 시큐리티는 strike 다중체크는 있으나 키워드 미반영.
- 조치(B1 DP 위에 전투 키워드):
  - `BattleResolver` 삭제 계산을 키워드 보정: **PreventBattleDeletion**(`preventBattleDeletion` 플래그 → 전투 삭제 면역), **Jamming**(`hasJamming` 공격자 → 전투서 삭제될 때 방어자도 삭제), **Piercing**(`hasPiercing` 공격자가 방어자를 삭제하고 생존 시 → `TriggersPiercingSecurityCheck`).
  - `AttackPipeline`이 Piercing 시 방어 플레이어 **시큐리티 체크 수행**(strike만큼, 시큐리티 0이면 패배 마킹). B1/SecurityResolver의 strike 규칙 재사용.
- 테스트: 신규 `tests/G3.5-RL-C2.BattleKeywords.Tests` 6/6 PASS — 순수DP 베이스라인·Prevent 생존·Jamming 상호삭제·Piercing 시큐리티 체크·strike2 2체크·미Piercing 무영향. 영향권(G2G-003/004/005 battle·security, AttackPipeline, B1) 회귀 0.
- **원본 대조 수정(2026-06-26)**: 초기 C2의 **Jamming 의미 오류 발견·제거**. 원본 `CardEffectFactory/KeyWordEffects/Jamming.cs`는 Jamming을 **조건부 `CanNotBeDestroyedByBattle`(공격자가 시큐리티 디지몬과 전투 시 삭제 면역)**로 정의 — "공격자 삭제 시 방어자도 삭제(상호파괴)"가 아님. 시큐리티 디지몬 전투가 headless에 미모델링이라 Jamming은 적용 surface가 없어 **제거하고 시큐리티 카드 효과와 함께 보류**. (regression 가드 테스트로 mutual-deletion 미발생 확인.)
- **남은 follow-up**: 시큐리티 카드 **효과 발동/시큐리티 디지몬 전투**(공격자 삭제/게임종료/Jamming 면역/보안효과)는 카드 효과 포팅(Phase 4) 의존. `hasPiercing`/`preventBattleDeletion`는 효과 시스템이 mutation으로 세팅하면 자동 적용(B-02 sink).

### G3.5-RL-V — 인터페이스 검증 (2026-06-26)
- 목적: Python/PyTorch 없이, trainer가 하는 그대로 **마스킹 랜덤 정책 × 팩터드 행동공간**으로 전체 self-play 에피소드를 끝까지 돌려 RL 인터페이스가 학습 가능한 형태인지 C#에서 검증. A1~C2를 한 번에 관통.
- 테스트: 신규 `tests/G3.5-RL-V.InterfaceSmoke.Tests` 5/5 PASS —
  1. **차원 불변**: 에피소드 전체에서 관측 벡터 길이·팩터드 마스크 크기가 상수(NN 입력/출력층 요건).
  2. **종료+보상**: 빈 라이브러리(deck-out)로 실제 터미널 도달, discount=0, 보상 ∈{-1,0,1}, perspective P1 → P2 덱아웃 → +1, 비터미널 보상 0.
  3. **결정성**: 같은 시드 → 동일 trajectory fingerprint·step수·보상.
  4. **합법성**: 마스크에서만 step → InvalidAction 0건.
  5. **A1 경계**: 마스크 밖 인덱스 → 거부 + 관측 불변.
- 결론: **헤드리스 엔진이 MaskablePPO/MultiDiscrete 류 trainer에 그대로 연결 가능한 형태(고정 obs/action 차원, invalid-action mask, 결정성, 터미널 보상)임을 E2E로 확인.** Python 연동은 obs/mask 벡터 + StepByFactoredIndex를 socket/stdio로 노출하면 됨(인터페이스는 준비 완료).

### G3.5-B1b — DigivolutionStack 통합 (2026-06-26)
- 문제(GPT A1 지적): 타입드 `DigivolutionStack`/`StackedCard`(B1)가 **자기 정의 파일에서만 참조되는 dead code**. `DigivolveAction`은 flat `sourceIds` 메타데이터만 사용 → LLM이 진화 효과에서 스택(소재)에 타입드로 접근할 단일 경로 부재.
- 결정: **`sourceIds` 메타데이터 = 기록의 저장소(storage), 타입드 `DigivolutionStack` = 그 위의 view.** (전면 immutable-state 교체는 관측·전투에 불필요해 보류 — 위 "남은 B1b(전면)"과 동일 판단.)
- 조치:
  - 신규 `State/DigivolutionStackReader.cs` — live `sourceIds`(newest-under-card first)를 타입드 스택(DigiEgg 바닥 → Top)으로 투영. 각 `StackedCard`의 CardNumber/Level/BaseDp는 인스턴스 메타 → 정의 메타 순으로 해석. `BaseDp`=Top 카드, `UnderCards`=소재(에그+하위 진화).
  - `DigivolveAction.ProcessAsync`가 attach 직후 리더로 스택을 **빌드·검증**(DigiEgg..Top 불변식 위반 시 throw)하고 결과 메타에 `stackDepth`/`stackBaseDp` 스탬프 → 타입드 스택을 **실제 사용**.
- 테스트: 신규 `tests/G3.5-B1b.DigivolutionStackIntegration.Tests` 5/5 PASS — 리더 정렬(DigiEgg..Top)·BaseDp(Top)·무소재 단일 Top·미지 top 빈 스택, **E2E: 실제 digivolve 후 스택 depth 2·소재=타깃·stackDepth 메타 스탬프**. 영향권(G2E-002/B1/A4b/G2D-004/G3G-002/C2/V) 회귀 0.
- 결과: 엔진/배틀/효과가 진화스택을 **단일 타입드 API(`DigivolutionStackReader.Read`)**로 읽음. *follow-up*: 스택 조작 mutation kind(소재 trash/de-digivolve)는 W2 어휘 확장 시 추가.

### G3.5-A3 — Blocker suspend 버그 수정 (2026-06-26)
- 문제(GPT A3 지적, 🔴엔진버그): `BlockTiming.ResolveBlockChoice` → `AttackController.SelectBlocker`는 공격 **타깃만 블로커로 전환**하고 블로커를 suspend하지 않음. AS-IS와 불일치.
- AS-IS 근거(`AttackProcess.SwitchDefender`, line 542~562): `isBlock`이면 (1) `SuspendPermanentsClass.Tap()`으로 블로커를 **suspend**, (2) `StackSkillInfos(OnBlockAnyone)`으로 "블록 시" 효과 발화.
- 조치(`BlockTiming.cs`): `SelectBlocker` 직후 — (1) 신규 `SuspendBlocker`가 블로커 인스턴스 메타에 `isSuspended=true` write, (2) `TriggerEventEmitter.Emit(OnBlock, actor=수비측, subject=블로커)`로 W4 스코핑(블로커 한정) 타이밍 윈도우 발행. `TriggerTimings.OnBlock="OnBlockAnyone"` 상수 추가. (AttackController는 순수 상태만 유지 — 메타 write는 BlockTiming 책임.)
- 테스트: 신규 `tests/G3.5-A3.BlockerSuspend.Tests` 4/4 PASS — 블록→블로커 suspend, OnBlock 윈도우가 블로커로 스코핑 발행, **skip 시 suspend·발행 없음**, E2E: 블로커의 OnBlock 효과만 발동(다른 카드 휴면). 영향권(G2G-002 블록·G3.5-005 파이프라인·G3.5-007 제약) 회귀 0.
- 결과: 블록 규칙이 AS-IS 충실. Phase 4 `[Blocker]`/"when blocking" 효과가 OnBlockAnyone 바인딩으로 자동 발동.

### G3.5-RL-A5 — factored mask를 RlStepResult에 포함 (2026-06-27)
- 문제(GPT A5 지적): `RlStepResult.ActionMask`는 타입기반 `EncodedActionMask`(28슬롯)뿐 — A3의 팩터드 마스크는 별도 `EncodeFactoredActionMask()` 호출로만 접근. trainer가 매 step마다 추가 호출 필요.
- 조치: `RlStepResult`에 `FactoredActionMask FactoredActionMask` + 편의 `FactoredActionMaskVector` 추가. `HeadlessRlEnvironment.Encode`가 **stepResult.ActionMask와 동일한 legal-action 집합** + 현재 보드 위치로 팩터드 마스크를 빌드 → 타입마스크와 팩터드마스크가 항상 일치. 스키마는 `HeadlessRlEnvironmentOptions.FactoredActionSchema`(기본 `FactoredActionSchema.Default`)로 설정.
- 일관성: 정상 step(post-step 상태)·거부(상태 불변)·Observe 모두 `_match` 현재 상태로 빌드하므로 ActionMask와 동일 상태 반영.
- 테스트: 신규 `tests/G3.5-RL-A5.FactoredMaskInStepResult.Tests` 5/5 PASS — reset이 스키마 크기 마스크 보유, 임베디드=standalone 일치, set-bit이 전부 합법 액션으로 resolve, step 후 갱신·일관, 커스텀 스키마 반영. 영향권(A3·V·A1·A2·A4a·A4b) 회귀 0.
- 결과: **MaskablePPO/MultiDiscrete trainer가 매 step의 `FactoredActionMaskVector`를 바로 사용** — 별도 인코딩 왕복 불필요. RL 인터페이스(A1~A5) 완성.

### G3.5-RL-A4 — Strict effect gate (2026-06-27)
- 문제(GPT A4 지적): B3로 Unbound가 카운트 관측은 되지만, **strict 실패 모드가 없어** Phase 4 포팅 중 미바인딩 효과(커버리지 갭)가 조용히 드레인됨.
- 조치: `CardEffectSchedulerResolver.Create(strictUnbound: false)` 옵션. strict일 때 미바인딩 효과를 `EffectResult.Failure`(`strictUnbound` 마커 + effectId/timing)로 반환 → 갭이 **즉시 loud failure**로 드러남(스케줄러가 큐 잔류·진행 중단). 기본(프로덕션)은 종전 Unbound 드레인.
- 테스트: 신규 `tests/G3.5-RL-A4.StrictEffectGate.Tests` 4/4 PASS — strict 미바인딩=Failure(드레인 안 됨), 마커/id/timing surface, lenient는 Unbound 드레인(카운트), 바인딩 효과는 strict와 무관하게 정상. 회귀 0.
- 결과: Phase 4 테스트 모드에서 strict 게이트로 **효과 커버리지 갭을 조기 검출**. (throw 대신 Failure로, 스케줄러의 기존 예외→Failure 변환과 충돌 없이 결정적.)

## 8. 미결 결정

- 보상 셰이핑(sparse terminal → 중간 신호): C1/C2로 규칙이 살아난 뒤 메모리/시큐리티/필드 우위 기반 shaping 설계 — 별도 goal(G3.5-RL-D)로 분리 예정.
