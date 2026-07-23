# Headless substrate audit — Part 5/8 (`hl_part_05.txt`)

담당 파일 27개, 누락 0. 판정 카테고리: 각 파일 실독 기반 개별 판정. 문제 발견 0건(정당 substrate 27/27); 경미한 관찰사항 2건은 하단에 별도 기재(차단 사유 아님).

## 파일별 판단

1. **Headless/Runtime/BattleResolver.cs** — 정당 substrate. 필드전투 판정 파이프라인 전체가 AS-IS `CardController.Battle`/`DestroyPermanentsClass.Destroy()` 정확한 줄번호 인용(:4478, :3684-3732, :3762-3783, :4700-4758 등)으로 뒷받침됨. HasDP 게이트(`Dp <= NoDpValue`)는 AS-IS `Permanent.HasDP`(DP sentinel -1) 1:1. Iceclad 비교(`CompareBattleStats`)는 AS-IS `CompareStats` 1:1. PRE cut-in 윈도우 개폐·survivor fix·casualty 확정 로직 모두 AS-IS 인용 동반. `ResolveKnockOutWindowAsync`는 "헤드리스 발명 LATENT 윈도우"로 명시적으로 자기-신고(0 리액터, 오늘은 완전 no-op) — 발명이지만 은폐 없이 문서화되어 있고 현재 게임 결과에 영향 없음(하단 관찰사항 참고).

2. **Headless/Runtime/DcgoMatch.cs** — 정당 substrate. 매치 라이프사이클(Initialize/Reset/Step/ApplyAction) 오케스트레이션이며 게임 규칙 판단은 전부 `GameFlowProcessor`/`RuleQueryService`/`TerminalOutcome`에 위임. `NormalizeForPump`가 pump-driven 모드에서 InitialHandSize/InitialSecuritySize/EnableMulligan을 0/false로 강제하는 것은 TurnFlowPump가 실제 AS-IS `TurnStateMachine.StartGameAsync`를 소유하므로 이중 처리를 막기 위한 배선 결정(주석에 근거 명시) — 게임규칙 임의결정 아님.

3. **Headless/Runtime/HeadlessActionPayloads.cs** — 정당 substrate. 순수 페이로드 파싱/직렬화(엔티티id/존/불리언/플레이어id 타입 변환) — 게임 규칙 판단 없음.

4. **Headless/Runtime/HeadlessGameLoop.cs** — 정당 substrate. 액션 큐 소비 → `GameFlowProcessor.RunToStableAsync` 위임 → 관측/마스크 조립. Perspective 필터링(`FilterChoiceForPerspective`/`BuildZoneObservations`)은 G3.5-RL-A4 설계에 따른 정보-은닉 규칙이며 `ZoneState.DefaultVisibility`(다른 파트 소유)를 그대로 따름 — 임의 결정 아님.

5. **Headless/Effects/OptionalPromptQueue.cs** — 정당 substrate. 선택적 트리거 프롬프트 큐잉/해석 메커니즘. 게이트(`trigger.Kind != Optional`, `ControllerId != playerId`)는 실제 제약이며 무조건 통과 스텁 없음.

6. **Headless/Runtime/CardLeavePlayCleanup.cs** — 정당 substrate. AS-IS "record parameters just before deletion"(`CardController.cs:3762-3783`)을 정밀 인용하며 DP/Level/Cost/Names/Traits/PermanentIdentity 스냅샷을 1:1 재현. `OnLeftPlay`가 빈 본문(`_ = cardId;`)인 것은 "레지스트리 바인딩 드롭은 RETIRED(프로듀서 0)"로 명시적으로 문서화된 은퇴이지 은폐된 스텁이 아님(4개 호출부는 계약 유지를 위해 남김, 코멘트로 근거 제시).

7. **Headless/Bridge/ContinuousContext.cs** — 정당 substrate. 매치 설정 값 객체(플레이어/덱/메모리 범위) 검증 로직이며 게임 규칙 발명 없음. 메모리 최소/최대 -10/10은 AS-IS `MemoryObject.cs:146-148`/`Player.cs:1020-1022`의 `-10` 하한과 일치(`ContinuousContext`가 임의로 정한 값 아님).

8. **Headless/State/DigivolutionStackReader.cs** — 정당 substrate. `sourceIds` 메타데이터를 `DigivolutionStack` 타입 뷰로 투영하는 순수 리더 + 캐시(weak-keyed, object-identity 검증)이며 게임 판단 없음. 캐시 무효화는 top/under 레코드의 참조 동일성으로 검증되어 안전.

9. **Headless/Runtime/CardStateMutationPort.cs** — 정당 substrate. Suspend/Unsuspend/Reveal/Hide 카드-상태 변이 포트, 이벤트 기록. 게임 규칙 판단 없음(순수 상태 전이 + 이벤트 생성).

10. **Headless/Runtime/LegalActionSetValidator.cs** — 정당 substrate. G3.5-RL-A1 에이전트-액션 적법성 경계. `AgentFacingTypes`는 AS-IS에 대응 없는 헤드리스/RL 고유 개념(Unity 클라이언트엔 "에이전트 액션 공간" 경계가 없음) — 이는 실행 substrate 고유 정의이며 게임 규칙(어떤 카드가 무엇을 할 수 있는가)은 건드리지 않고 "어떤 액션 타입이 이 경계 안에 있는가"만 결정. 컬렉션 값 파라미터 비교(`SequenceValueEquals`)는 GPT-#1 버그 수정 근거 명시.

11. **Headless/Runtime/ContinuousKeywordGate.cs** — 정당 substrate. `HasKeyword`는 AS-IS `NewModelContinuousScan.HasKeyword`(인터페이스 스캔)에 직접 위임 — 스텁 아님. `IsDigimon`은 AS-IS `Permanent.IsDigimon`(Permanent.cs:3438) 1:1: `isFlipped` 체크, CardType Digimon/DigiEgg/Digitama, TreatAsDigimon 폴백까지 정확히 재현. 키워드 상수명(`ArmorPurge = "Armor Purge"`)도 AS-IS `KeywordBaseBatch2Factory.KeywordName`과 일치하도록 스페이스까지 맞춤.

12. **Headless/Runtime/InMemoryHeadlessChoiceController.cs** — 정당 substrate. 선택 상태 기계(RequestChoice/ResolveChoice/ToggleCandidate). `ToggleCandidate`의 tap/재-tap 로직은 AS-IS `SelectPermanentEffect.cs:431-464`/`SelectHandEffect.cs:271-311`를 순서까지 인용하며 1:1 재현.

13. **Headless/Effects/SkillInfo.cs** — 정당 substrate. 효과 요청의 불변 레코드 래퍼(정의/요청/모드/우선순위/시퀀스) + 생성자 검증(id/타이밍 일치). 게임 판단 없음.

14. **Headless/Runtime/AttackPipeline.cs** — 정당 substrate. 얇은 위임 shim — 실제 공격 상태 기계는 미러 레이어(`Assets/Scripts/Script/AttackProcess.cs`)에 있음(문서화된 아키텍처 결정: Headless=substrate만, 미러=게임로직). 게임 규칙 없음.

15. **Headless/Runtime/HeadlessActionTypes.cs** — 정당 substrate. 액션 타입 문자열 상수 + 정규화 함수. 규칙 없음.

16. **Headless/Runtime/TerminalEvaluator.cs** — 정당 substrate. `PlayerStatusController.IsLose` 값을 `PlayerRuleAdapter.EvaluateLoseFlag`(미러 레이어)에 위임하는 브릿지 — 종료 판정 로직 자체는 어댑터가 소유, 이 파일은 상태 조립만 수행.

17. **Headless/Rules/TimingWindowTrigger.cs** — 정당 substrate. 트리거 레코드(요청/모드/종류/우선순위/시퀀스/BatchId) + 생성자 검증. `BatchId`의 교차-배치 순서 규칙은 주석에 근거(AS-IS는 각 `Destroy()`를 자기 윈도우에서 처리하므로 동일 배치 내에서만 순서 선택을 열어야 함) 명시.

18. **Headless/Effects/TriggerEventEmitter.cs** — 정당 substrate. 타이밍 윈도우 게임이벤트 발행 헬퍼. Subject 스코핑(W4)은 AS-IS `StackSkillInfos(timing)`의 소스-필터링 동작을 재현한다고 문서화.

19. **Headless/Effects/CardEffectSchedulerResolver.cs** — 정당 substrate. 레지스트리(프로듀서 0으로 이미 은퇴)의 always-null lookup 경로를 정직하게 "always-unbound" 분기로 축소했다고 명시. strictUnbound=true일 때 실패 반환, false일 때 카운트 가능한 Unbound 상태 반환 — 무조건 통과 스텁 아님(라이브 효과 바디는 이 경로를 타지 않는다고 명시).

20. **Headless/Runtime/HeadlessAction.cs** — 정당 substrate. 액션 레코드(id/playerId/actionType/parameters) + 유효성 검증(빈 문자열 거부). 규칙 없음.

21. **Headless/Runtime/PlayerTurnCounterController.cs** — 정당 substrate. AS-IS `Player.DigivolveCount_ThisTurn`(CardController.cs:1528 증가, TurnStateMachine.cs:3181 턴시작 리셋) 1:1 미러라고 명시.

22. **Headless/Runtime/AttackPhase.cs** — 정당 substrate. AS-IS `AttackProcess.ProcessNextState()`(Counter/Block/Battle/Security/End/CleanUp) 서브상태 열거형. 각 값에 AS-IS 근거 주석.

23. **Headless/Runtime/IHeadlessChoiceController.cs** — 정당 substrate. 선택 컨트롤러 인터페이스 계약. `ToggleCandidate`의 XML 주석이 AS-IS tap/재-tap 소스 줄번호까지 인용.

24. **Headless/Runtime/IHeadlessTurnController.cs** — 정당 substrate. 턴 컨트롤러 인터페이스 계약(순수 시그니처, 판단 없음).

25. **Headless/Runtime/HeadlessMemoryState.cs** — 정당 substrate. 메모리 게이지 값 레코드. 기본값(Min -10/Max 10)은 AS-IS `MemoryObject.cs`/`Player.cs`의 -10 하한과 일치(§7 확인).

26. **Headless/Effects/EffectResolutionMode.cs** — 정당 substrate. 효과 해석 모드 열거형(MainStack/CutIn/Background/RuleProcess) — 실사용 확인(`AutoProcessing.For`/`ForCutIn` 등 다른 substrate 파일에서 실제로 이 모드들로 배선됨, placeholder 아님).

27. **Headless/Diagnostics/TraceOptions.cs** — 정당 substrate. 트레이스 옵션 레코드(Enabled/MaxEvents). 규칙 없음.

## 관찰사항 (차단 사유 아님, 참고용)

- **BattleResolver.cs `ResolveKnockOutWindowAsync`** — "(C2 decision-1) OnKnockOut은 헤드리스 발명 LATENT 윈도우"라고 자체 신고: AS-IS에 `StackSkillInfos(OnKnockOut)` 대응이 없고, 현재 0장 카드가 반응. 오늘은 완전 no-op(빈 해시테이블로 GetSkillInfos 호출 후 버림)이라 결과에 영향 없으나, 카테고리 (1)/(3) 관점에서 "AS-IS 근거 없는 substrate 발명"에 해당하는 요소이므로 기록. 리뷰 필요 시 향후 battle rehousing 트랙에서 정리 대상으로 이미 명시되어 있음.
- **스테일 리터럴 `TODO` 주석** — `HeadlessMemoryState.cs:3`("Replace with full DCGO memory gauge semantics…")와 `EffectResolutionMode.cs:3`("Replace placeholder values…")가 남아있으나, 실사용 확인 결과 두 타입 모두 이미 실질적으로 배선되어 있어(메모리 -10/10 AS-IS 일치, 4개 해석모드 실사용) 문구가 실제 상태에 뒤처진 것으로 판단됨. 기능적 문제는 아니고 사용자 메모리의 "TODO lint-guard pitfall" 규약과 충돌하는 잔존 주석이라 참고로 남김(엔진 소스 전반에 동일 패턴의 스테일 TODO가 다수 존재 — 이 27개 파일만의 문제 아님).

## 결론

파트 5/8 27개 파일 전수 실독 완료. **문제 발견 0건** — 게임규칙 substrate 임의결정, 게이트 스텁(무조건 통과), 미러로직 substrate 침투, AS-IS 발산 4개 축 모두 위반 없음. 위 관찰사항 2건은 기능적 결함이 아닌 문서/설계 노트 수준.
