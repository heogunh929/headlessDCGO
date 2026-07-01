# 원본 DCGO ↔ 헤드리스 포팅 자체 대조 감사

- 작성일: 2026-06-27
- 방법: 5개 병렬 에이전트가 하위 시스템별로 원본(`DCGO/Assets/Scripts/Script/`)과 포팅(`src/HeadlessDCGO.Engine/Headless/`)의 **엔진 규칙 로직**을 대조. 미포팅 카드 효과(Phase 4)·네이밍·스타일은 제외. 본 문서는 에이전트 결과를 **작성자가 직접 스폿체크·판단해 필터링**한 결과(오탐 제거, confidence 재조정).
- 분류: 🔴확정버그(수정후보) / 🟡설계단순화(의도가능·검증필요) / 🔵Phase4 배선커버리지 / ⚪저신뢰·데이터의존
- 관련: GPT 검수 항목은 [gpt_review_followups.md](gpt_review_followups.md) 별도 추적.
- **진행 현황(2026-06-27)**: D-1·D-2·D-5 수정 / D-3·D-4 수정(옵셔널 자동발동 한계 명시) / D-6 수정(에이전트 결정) / S-1 검증=버그아님 / S-2 LOW / 🔵 타이밍 갭 [체크리스트화](timing_emission_gaps.md) / ⚪ 기록.

---

## 🔴 확정 divergence (수정 후보)

### D-1. 피어싱 시큐리티 체크가 시큐리티 디지몬 전투를 건너뜀 (W5 비대칭) → ✅ **수정 완료(2026-06-27)**
> **수정**: `SecurityResolver`에 per-card 체크 루프를 `RunSecurityCheckLoopAsync`로 추출(이동+OnSecurityCheck emit+디지몬전투+StopSecurityCheck). 직접공격(`ResolveAsync`)·피어싱(`AttackPipeline.ApplyPiercingSecurityAsync`) **둘 다 동일 루프 호출** → 비대칭 해소. 신규 `tests/G3.5-D1.PiercingSecurityBattle.Tests` 3/3 PASS(강한 시큐리티→공격자 삭제 / 약한→생존 / OnSecurityCheck 효과 발동). 회귀(G2G-004·W4·W5·005·C2) 0.

- **원본**: 피어싱도 동일한 `ISecurityCheck.SecurityCheck()` 루프를 타서, 공개된 시큐리티 디지몬이 공격자와 전투(`CardController.cs` ~4123-4181). 공격자가 삭제될 수 있음.
- **포팅**: `AttackPipeline.ApplyPiercingSecurityAsync`(`AttackPipeline.cs:136-174`)가 **자체 축약 루프**로 상위 N장을 `Security→Trash` 이동만 함. `SecurityResolver`를 호출하지 않아 **시큐리티 디지몬 전투·OnSecurityCheck 윈도우가 없음**. 직접공격 경로(`SecurityResolver.ResolveAsync` + `ResolveSecurityDigimonBattleAsync`, W5)와 **비대칭**.
- **영향**: 피어싱으로 시큐리티를 깨는 공격자가 강한 시큐리티 디지몬과 전투/삭제되지 않음. 시큐리티 효과도 미발동.
- **심각도**: HIGH · **확신**: HIGH (작성자 직접 확인)
- **수정 방향**: `ApplyPiercingSecurityAsync`를 `SecurityResolver.ResolveAsync` 재사용으로 통합(또는 공통 헬퍼 추출).

### D-2. 필드 DP≤0 상태기반 삭제 규칙 미구현 → ✅ **수정 완료(2026-06-27)**
> **수정**: `GameFlowProcessor.RuleProcessAsync`에 필드 Digimon effective-DP(`DpCalculator`) ≤ 0 삭제 추가. **DP가 정의된 경우에만** 적용(미정의=무시, DP-less 픽스처 보호 — W5와 동일 가드). 신규 `tests/G3.5-D2.DpZeroDeletion.Tests` 5/5 PASS(모디파이어로 0/음수→삭제, 양수/미정의/비-Digimon→생존). 회귀(C1·004·G2G-003·C2·W5·V) 0. *follow-up: "파괴 불가" 일반 플래그는 효과 포팅 시 존중.*

- **원본**: `AutoProcessing.DoRuleProcess`(`AutoProcessing.cs:319-484`) + `CutInProcess`(`:38-106`)가 전투 외에도 **DP==0/DP<0 디지몬, 무DP permanent, 브리딩 비디지몬**을 상태기반으로 trash.
- **포팅**: `GameFlowProcessor.RuleProcessAsync`(`GameFlowProcessor.cs:111-156`)는 **`pendingDeletion` 플래그가 찍힌 카드만** 스윕. 효과가 명시적으로 플래그를 안 찍으면 DP≤0 디지몬이 필드에 잔류.
- **영향**: 전투 밖에서 DP가 0/음수가 된 디지몬이 원본은 삭제, 포팅은 잔류 → 보드 상태·승패 하류 영향.
- **심각도**: HIGH · **확신**: HIGH (C1이 "플래그 스윕"만 구현한 것과 일치 — 의도된 단순화이나 규칙상 갭)
- **수정 방향**: `RuleProcessAsync`에 필드 effective-DP≤0 스캔 추가(DpCalculator 사용).

### D-3. 라이브 트리거 루프에 mandatory→optional / 턴플레이어 우선순위 없음 → ✅ **수정 완료(2026-06-27, 부분·한계 명시)**
> **수정**: `AutoProcessingTriggerCollector.CollectAllTriggers`(enqueue 분리) + `GameFlowProcessor.AutoProcessAsync`가 한 배치의 트리거를 `MandatoryEffectOrdering`으로 정렬(**턴플레이어 우선 → 비턴 → mandatory→optional**) 후 enqueue. 신규 `tests/G3.5-D3.TriggerOrdering.Tests` 2/2 PASS(등록 역순에도 턴플레이어 먼저, mandatory→optional + 옵셔널 발동). 회귀(006/004/G2F-001/002/W1/V) 0.
> **한계(수용)**: ~~옵셔널 트리거는 드롭 대신 mandatory 뒤에 enqueue=자동발동(강제)~~ → ✅ **해소(2026-06-27)**: 아래 #2 참조.
> **#2 옵셔널 = 원본대로 닫음(2026-06-27)**: trigger Kind를 **효과별 `CardEffectDefinition.IsOptional`**에서 재분류(`GameFlowProcessor.ReclassifyKind`, `EffectRegistry.Find` 경유). **강제(IsOptional=false)는 즉시 enqueue·해결**, **선택(IsOptional=true)은 자동발동 안 함** — `EngineContext.OptionalPromptQueue`(신규 보유)에 턴플레이어 우선으로 큐잉 → `RequestNextChoice`로 pending choice 오픈(루프 일시정지) → A2가 ResolveChoice(활성/스킵)로 노출 → `MetadataActionProcessor`가 `ChoiceType.OptionalEffect`를 `OptionalPromptQueue.ResolveChoice`로 라우팅(고른 것만 enqueue). 신규 `tests/G3.5-OPT2.OptionalTriggerChoice.Tests` 3/3 PASS(강제 즉시·선택 일시정지·활성 해결·스킵 미해결). 회귀(006/004/G2F-002/G2F-003/A2/W1/V/D3) 0. *부품(OptionalPromptQueue·A2·pause·ChoiceType.OptionalEffect) 재사용, IsOptional 토대 기존재 — 배선만.*

- **원본**: `MultipleSkills.ActivateMultipleSkills`(`MultipleSkills.cs:55-`)가 **턴플레이어 트리거 먼저→비턴플레이어**, 동시 트리거는 액티브 플레이어가 순서 선택.
- **포팅**: `GameFlowProcessor.AutoProcessAsync`가 `EffectRegistry.GetEffectsForTiming`의 **등록 순서(FIFO)**로 enqueue. `MandatoryEffectOrdering`/`OptionalPromptQueue`는 **Runtime 라이브 경로에서 참조 0건**(에이전트 확인).
- **영향**: 동시 트리거가 덱 등록순으로 임의 해결. 턴플레이어 우선(DCG 핵심 규칙) 미적용. 옵셔널이 선택 없이 자동 해결.
- **심각도**: MED~HIGH · **확신**: HIGH (부품 존재·미배선 = 알려진 "축약판" 패턴)
- **수정 방향**: AutoProcess 수집 후 `MandatoryEffectOrdering`+`OptionalPromptQueue` 경유.

### D-4. End-of-Attack 윈도우가 옵셔널 트리거를 전부 누락 → ✅ **수정 완료(2026-06-27, 한계 명시)**
> **수정**: `EndAttackTriggerHook`의 계약(mandatory/optional **분리**, mandatory enqueue)은 보존(=G2G-005 그대로 green, "defer for choice"가 올바른 토대). 대신 호출처 `AttackPipeline.AdvanceEndAttack`가 `result.MandatoryOrder.DeferredOptionalTriggers`를 mandatory 뒤에 enqueue=**자동발동**(드롭 방지). 
> **한계/실태**: 현재 파이프라인의 end-attack 이벤트는 Mandatory kind라 실전에서 optional end-attack 트리거가 발생하지 않음(잠재) — 본 수정은 optional-kind가 들어올 때의 안전망. 에이전트 결정 노출은 Phase 4. G2G-005·005 회귀 0.

- **원본**: `OnEndAttack`(`AttackProcess.cs:480`)이 `TriggeredSkillProcess`로 옵셔널 [공격종료시] 효과 활성화 허용.
- **포팅**: `EndAttackTriggerHook.Process`(`EndAttackTriggerHook.cs:129-133`)가 `MandatoryEffectOrdering.OrderAndEnqueue`로 **mandatory만** enqueue. `DeferredOptionalTriggers`는 어디서도 enqueue 안 됨.
- **영향**: 옵셔널 [공격 종료시] 효과가 영영 발동 불가.
- **심각도**: MED · **확신**: HIGH

### D-5. OnDeletion(OnDestroyedAnyone) 윈도우가 과도하게 넓게 열림 → ✅ **수정 완료(2026-06-27)**
> **수정**: `TriggerTimingMap.DeriveZoneTransition` — OnDeletion을 `fromField && to==Trash`로 제한(필드 파괴만). 핸드 버림/밀/시큐리티 trash는 OnDeletion 미발생. 기존 W1 `SecurityLossDerivesTimings`의 잘못된 단언(Security→Trash=OnDeletion) 정정. 신규 `tests/G3.5-D5.DeletionTimingScope.Tests` 6/6 PASS(BattleArea·BreedingArea→Trash만 OnDeletion, Hand·Library·Security→Trash 제외, E2E 효과 스코핑). 회귀(W1·W4·G2G-004·006) 0.

- **원본**: `OnDestroyedAnyone`은 **필드 파괴**에만(`CardController.cs:3743`). 핸드 버림/밀/시큐리티 trash는 별도 타이밍.
- **포팅**: `TriggerTimingMap.DeriveZoneTransition`(`TriggerTimingMap.cs:70-73`)이 **`to==Trash`면 무조건** OnDeletion. 출발 존 무관.
- **영향**: "파괴됐을 때" 효과가 핸드 버림/밀/시큐리티 trash로 오발동 가능(카드 self-gate가 일부 가림).
- **심각도**: MED · **확신**: HIGH
- **수정 방향**: `from`이 필드(Battle/Breeding)일 때만 OnDeletion 파생.

### D-6. 브리딩 페이즈가 플레이어 선택(부화/이동/스킵)을 제거하고 자동 처리 → ✅ **수정 완료(2026-06-27)**
> **수정**: 브리딩을 에이전트 결정으로 전환. (1) `HeadlessEarlyPhaseFlow` 자동 부화/이동 제거, (2) `HeadlessLegalActionDispatcher.BuildBreedingActions`가 Hatch(디지타마>0&브리딩 비어있음)/Move(브리딩 점유)/AdvancePhase(거절) 제공, (3) `LegalActionSetValidator` agent-facing에 Hatch/Move 추가, (4) `FactoredActionEncoder`에 Hatch/Move 단일 레인(RL 접근), (5) `CheatActionGuard`에서 Hatch/Move 제거(더 이상 cheat 아님). 기존 인프라(액션타입·팩토리·`MetadataActionProcessor` 핸들러·ZoneMover)는 이미 존재했음 — 배선만 연결. 신규 `tests/G3.5-D6.BreedingChoice.Tests` 3/3 PASS(부화/거절 제공·거절 시 미부화·팩토리드로 부화). `G2A-003` 갱신(11/11, 자동→에이전트), `A3` 스키마 +2 갱신. 회귀(006/A1/A5/V/G2E-005) 0.

- **원본**: `BreedingPhase`(`TurnStateMachine.cs:719-816`) — 플레이어가 부화 여부/이동 여부/무행동을 **선택**.
- **포팅**: `HeadlessEarlyPhaseFlow.ResolveBreedingAsync`(`HeadlessEarlyPhaseFlow.cs:107-139`) — 디지타마 있으면 **항상 부화**, 브리딩 카드 있으면 **항상 이동**. 거부 경로 없음.
- **영향**: 합법적 게임 결정(이동 보류 등) 상실. *RL 행동공간에서도 누락.*
- **심각도**: MED · **확신**: HIGH(자동처리 사실) / MED(의도 정책 여부)

---

## 🟡 설계 단순화 (의도 가능 · 검증 필요 — 확정버그 아님)

### S-1. 메모리: 양면 게이지 → 턴-상대 단면 게이지 + 초과분 abs-플립 → ✅ **검증 완료: 버그 아님(기능적 동등)**
- **포팅**: `Pay=Set(Current-cost)` 무조건 감소, pass 임계 `<= -1` 고정, 핸드오프 `Math.Abs` 플립(`HeadlessMainPhaseFlow.cs:130`), empty-pass `Set(-3)`.
- **에이전트 판정**: "원본 양면 부호 게이지를 무너뜨림 → 양 플레이어 중 한쪽 메모리/패스 항상 틀림" (HIGH).
- **원본 규칙(코드 확인)**: `Memory`는 부호 단일 게이지(P0=`-Memory`, P1=`+Memory`, 턴전환 시 리셋 없음). 턴플레이어가 c 지불 → 상대 시작 메모리 = **초과분 c−m**(m=턴플레이어 시작). 패스 조건 c≥m+1. voluntary pass=상대에게 3.
- **작성자 판정(에이전트 오탐)**: 에이전트는 포팅 게이지를 **절대 프레임**으로 오해. 실제로는 abs-플립이 **매 턴 턴플레이어 기준으로 재정렬**하므로, P0·P1 양쪽 모두 `상대 시작 = c−m`을 산출. 대수적으로 원본과 **완전 동일**(양 플레이어).
- **검증**: 신규 `tests/G3.5-S1.MemoryEquivalence.Tests` **7/7 PASS** — 실제 매치 E2E로 (1) P1 초과지불→P2가 초과분, (2) **P2 초과지불→P1이 초과분(2번째 플레이어 대칭 — 에이전트 핵심 주장 반증)**, (3) P1 voluntary pass→P2가 3, (4) **P2 voluntary pass→P1이 3(대칭)**, (5) 0까지 지불은 턴 유지, (6) 부분지불 Main 유지, (7) 다중턴 체인 초과분 정확 이월. **전부 원본 규칙과 일치.**
- **잔여 한계(Phase 4)**: 상대 메모리를 직접 조작하는 효과(예: "상대 메모리 −3")는 단면 모델로 표현 불가 → 해당 효과 포팅 시 양면 표현 필요. *코어 턴/패스 루프는 동등하므로 지금 수정 불요.*
- **결론**: 🟢 **버그 아님. S-1 종결.** (성급히 "고치지" 않고 검증해서 회귀 도입을 피함.)

### S-2. 양 플레이어 동시 패배 → 원본 무승부, 포팅은 승리
- **원본**: `EndGameProcess`(`AutoProcessing.cs:386-405`) 양쪽 IsLose면 `EndGame(null)`=무승부.
- **포팅**: `TerminalEvaluator`/`GameFlowProcessor.cs:233-235`가 `isDraw:false` 하드코딩, 양패 체크 없음.
- **실제 노출**: 원본의 `Player.SetLose()`는 **호출처 0건**(에이전트 확인) → 실게임 패배는 전부 `EndGame()` 직접 경로. 무승부 인프라(`MatchResult.IsDraw`)는 존재. 노출도 낮음.
- **심각도**: LOW~MED · **확신**: HIGH(로직)/LOW(노출)

---

## 🔵 Phase 4 트리거 배선 커버리지 (엔진이 발화하나 포팅 미발행 — 카드 포팅 전 보완 대상)

원본이 실제 발화하지만 포팅에서 **어디서도 emit 안 되는** 타이밍(바인딩해도 발동 불가):

`OnStartMainPhase`, `OnTappedAnyone`/`OnUnTappedAnyone`, `OnAddDigivolutionCards`, `WhenLinked`, `OnFaceUpSecurityIncreased`, `OnDigivolutionCardDiscarded`, `OnLinkCardDiscarded`, `WhenTopCardTrashed`, `OnReturnCardsTo{Hand,Library}FromTrash`, `OnMove`, `OnDiscardHand`/`OnDiscardSecurity`, `Before/AfterPayCost`, `OnUseOption`, `OnUseDigiburst`, `OnStartBattle`/`OnEndBattle`.

- **영향**: 위 타이밍에 바인딩된 카드 본문은 dead. 빈도 높은 것(시작시 타머·[연결시]·[이동시]·전투 시작/종료 DP)은 HIGH.
- **권장**: 해당 카드군 포팅 시점에 emit 지점 배선(W1-2 패턴). `TriggerTimings` 상수 추가 + 발화 지점.
- → ✅ **체크리스트화 완료**: [timing_emission_gaps.md](timing_emission_gaps.md)에 미발행 타이밍 18종 + 원본 발화지점 + 포팅 emit 추가 위치 정리(Phase 4 카드군별 배선). 무검증 일괄 emit은 하지 않음(각 연산+바인딩 효과 전제).

---

## ⚪ 저신뢰 · 데이터 의존 (기록만)

- **C-1. 디지볼브 추가 EvolutionCondition 게이트**: 포팅 `DigivolveAction.Validate`가 색/레벨 비용매칭 외에 `MatchesEvolutionCondition`(정의id/번호/타입) **2차 게이트**를 추가. 원본엔 없음. `evolutionCondition` 데이터가 채워진 카드에서만 영향. MED/MED.
- **C-2. 요구매칭 null=any**: `DigivolutionCostHelpers.Matches`가 레벨/색 null이면 검사 스킵(any 매칭). 원본은 둘 다 필수. fallback 경로(`:291-294`)가 레벨/색 없는 요구 생성. MED/MED.
- **C-3. 레벨 매칭이 정의 레벨 사용**: 타깃의 효과보정 레벨이 아닌 printed 레벨로 비용매칭. 레벨변경 효과 활성 시만 차이. LOW/MED.
- **C-4. Iceclad/무DP 영속가산(BaseDP) 미반영**: 전투 비교축 변경(Iceclad=진화소재수) / `CardSource.BaseDP` 영속분이 런타임 미반영. 효과레이어 의존. LOW.

---

## 작성자 종합 판단

- **즉시 수정 후보(엔진 규칙)**: D-1(피어싱 전투), D-5(OnDeletion 범위), D-4(End-attack 옵셔널) — 작고 명확.
- **구조적(축약판 → 본격 배선 필요)**: D-2(상태기반 삭제), D-3(트리거 우선순위/옵셔널) — C1/효과파이프라인 확장과 함께.
- ~~**검증 우선(버그 단정 전)**: S-1(메모리 동등성 테스트).~~ → ✅ **완료: S-1은 버그 아님**(G3.5-S1 7/7 PASS, 양 플레이어 대칭 확인). 에이전트 HIGH 오탐.
- **Phase 4 동반**: 🔵 타이밍 emit 갭, ⚪ 디지볼브 매칭 정밀화.
- **GPT 항목과 무중복**: 본 감사 발견(D/S/🔵)은 GPT 7항목과 별개 레이어(공격/전투/트리거/규칙) — 상호 보완적.
