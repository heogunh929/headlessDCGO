# Headless substrate 감사 — 파트 2/8 (hl_part_02.txt, 27개 파일)

감사 축: (1) 게임규칙 substrate 임의결정 (2) 게이트 스텁 (3) 미러로직 substrate 침투 (4) AS-IS 발산(순서/조건/집합).
판정은 실소스 근거로만; 기존 판정·주석은 근거로 사용하지 않음(단, AS-IS 대응 확인을 위한 line-ref 교차검증은 수행).

## 요약

27개 파일 중 25개는 정당 substrate(순수 실행 인프라 — DTO/인터페이스/로더/로깅/디스패치 배선이며 룰 판정 자체는 미러 계층(`Assets/Scripts/Script/...`)에 위임). 2개 파일에서 실질 발견:

- **발견 1 (축 2/4, 중대)**: `Headless/Runtime/DigivolveAction.cs` — `IgnoreDigivolutionRequirementKey`(디지볼브 요구조건 "전체 무시" 연속효과 판정)가 오직 영구적으로 빈 배열만 반환하는 `ContinuousScopeEvaluation.ApplicableEffects`(레지스트리, 4번 파일에서 자체 확인)에만 배선되어 있고, 자매 판정인 색상-무시(`CanIgnoreColorRequirement`)와 달리 라이브 AS-IS 스캔으로의 union 경로가 없다. AS-IS의 실제 프로듀서(`GainIgnoreDigivolutionRequirementPlayerEffect`, `Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPlayer/IgnoreDigivolutionRequirement.cs`)는 자체 헤더에 "Latent (0 callers)"로 명시되어 있어 현재 카드 풀에서는 무증상이지만, 소비 경로 자체가 구조적으로 죽어있어 이 프로듀서가 훗날 배선되어도 절대 반영되지 않는다.
- **발견 2 (축 1, 중대)**: `Headless/State/VisibilityView.cs` — "본인(owner)의 hidden zone(Security/Library/DigitamaLibrary 포함)은 카드 ID까지 전부 공개, 상대만 카드 수만 공개"라는 판정이 AS-IS 게임규칙 근거 없이 내려져 있다(디지몬 TCG는 Security/Library 순서를 소유자 본인도 모른다 — 확인 전까지 비공개). 해당 Goal의 자체 결과 문서(`docs/test-results/goals/G2B-002_visibility_view_unit_test_results.md`)가 "미해결 리스크"로 스스로 인정하고 있고, AS-IS 근거는 제시되지 않았다.
- **경미 발견**: `Headless/DataLoading/DeckValidationSmoke.cs` — 프로덕션 네임스페이스에 있는 completely-orphaned 스모크 헬퍼(어디서도 호출되지 않음), 리터럴 `TODO` 주석 보유("Move to tests once executable test infrastructure is available" — 테스트 인프라는 이미 수백 개 존재). 4대 축과 직접 관련은 없으나 청소 대상.
- `Headless/Runtime/DeletionReplacementGate.cs`는 대부분이 "RETIRED" 주석(과거 발화-반이 창구로 이관됨)으로 채워져 있으나, 남아있는 `SacrificeAsync`는 `OverclockEffect.cs`에서 실제로 호출되는 살아있는 경로임을 확인(죽은 코드 아님).

---

## 파일별 판정

### 1. Headless/Runtime/DigivolveAction.cs — **문제 있음 (발견 1)**
디지볼브 합법 액션 열거/검증/실행. 대부분 AS-IS 라인 참조(`CardController.cs:1367/1526-1529/1691`, `CardSource.cs:587-609` 등)로 1:1 근거를 제시하며 미러(`CardSource`/`Permanent`/`Player`) 호출로 판정을 위임 — 구조 자체는 정당.
- **문제**: `CanIgnoreDigivolutionRequirement(context, playerId, cardId)` (1005번대) = `HasContinuousFlag(..., IgnoreDigivolutionRequirementKey)` 단독 — 이 헬퍼는 `ContinuousScopeEvaluation.ApplicableEffects`만 스캔하는데, 그 함수는 파일 17(`ContinuousScopeEvaluation.cs`)에서 확인했듯 하드코딩으로 `Array.Empty<EffectRequest>()`를 반환(자체 주석: "production-inert"). 자매 판정 `CanIgnoreColorRequirement`는 동일한 죽은 스캔에 더해 `NewModelIgnoreColorActive`(`CardSource.IgnoreColorConditionActive()`, 라이브 AS-IS 스캔)로 union되어 실질 동작하는 반면, "전체 무시(All/Level)" 경로만 union 파트너가 없다.
  - 영향 경로 3곳이 항상 죽음: `TryGetDeclaredEvolutionCost`의 ②단계(전체 무시 재해석), `AddedLevelGatePasses`의 무시-레벨 게이트 waive, `Validate()`의 인라인 무시 체크.
  - AS-IS 측 실제 프로듀서 `GainIgnoreDigivolutionRequirementPlayerEffect`(`Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPlayer/IgnoreDigivolutionRequirement.cs`)는 자체 헤더 주석이 "Latent (0 callers)"라고 명시 — 현재 포팅된 카드 풀에서 무증상이나, 소비측이 구조적으로 죽어 있어 "죽은 판정에 AS-IS 발화 경로 없음" 확인 기준을 충족한다기보다는 "발화 경로가 미래에 생겨도 절대 반영 못하는 소비 배선"이라는 점이 실질 결함.
- App-Fusion 분기(`ValidateAppFusion`), 브리딩존 타겟 허용(GR-004), 소스-부착(`AttachTargetAsSource`) 등 나머지 로직은 AS-IS 라인 인용과 부합.

### 2. Headless/Runtime/AttackPermanentAction.cs — 정당 substrate
공격 선언 합법성/실행. Blitz 관련 옛 "메모리패스 중 공격 허용" 게이트를 재심사로 폐기한 이력이 주석에 재판정 근거(창 기반 EffectDrivenAttack 경로)와 함께 남아있음 — 발명이 아니라 이전 발명물의 철회 기록. `IsMainPlayPhase` 게이트, `ContinuousRestrictionGate.EvaluateAttack/EvaluateBeAttacked/EvaluateBlock` 호출, `ContinuousKeywordGate.Execute`(공격 대상은 서스펜드 불필요) 사용은 모두 미러 위임. 게이트 스텁 없음.

### 3. Headless/Runtime/BlockTiming.cs — 정당 substrate
블로커 후보 열거/선택 해소. 실제 방어 전환은 `Assets.Scripts.Script.AttackProcess.For(context).SwitchDefender(...)`로 미러에 위임(발명 아님). Collision 강제-블로커 판정은 `Permanent.HasBlocker`/`CanNotBeAffected` 라이브 스캔과 키워드 게이트 union — AS-IS 1:1 언급(Permanent.cs:2401-2417, 2411-2415) 확인.

### 4. Headless/Runtime/TurnFlowPump.cs — 정당 substrate (구조적 조정 문서화됨)
AS-IS 코루틴 턴 루프의 async pump 번역. 메모리 게이지 부호 반전(`MemoryController.Set(-...)`)은 "좌표계 변환"으로 명시 문서화 — AS-IS는 seat-absolute(`Player.AddMemory`, PlayerID==0 기준 부호 고정, `Player.cs:1082-1108` 확인), 미러는 turn-player-relative로 재부호화하는 것이 의도된 substrate 어댑테이션이며 `AceOverflowGate.cs`의 동일 컨벤션과 일치. `ExpireEnteredThisTurnFlags`는 AS-IS `TurnCount++` 부작용(요약 판정: "entered field this turn" 만료)의 substrate 번역으로 근거 있음.

### 5. Headless/Effects/SkillWindowContinuation.cs — 정당 substrate (DORMANT, 명시)
자체 주석에 "No live caller drives it yet" 명시 — 신규 SkillInfo-currency 윈도우 배선을 위한 선행 인프라(cutover batch C 대기). 게이트 스텁이 아니라 아직 배선되지 않은 잠재 substrate. AWAIT-모드(pump 통합) 코드는 `TurnFlowPumpHost.FindExecuting()`을 통해 조건부로만 작동, THROW 계약(WindowChoicePendingException)과 공존 — 이중발화 소지 없음.

### 6. Headless/Runtime/HeadlessLegalActionDispatcher.cs — 정당 substrate
합법 액션 최상위 디스패치 테이블. pump 설치 여부에 따라 SpecialPlay를 별도 테이블에 넣거나(비-pump) 뺴는(pump, PlayCardAction 경유로 대체) 판단이 상세 주석과 함께 근거 제시(RD-RLENV-05/Option A). 멀티선택 세션 테이블(Toggle/Confirm/Skip)은 AS-IS `SelectHandEffect.cs:271-289/433-446/575-591` 라인 인용과 함께 1:1 대응 주장 — 표본 검증 결과 논리 정합.

### 7. Headless/Runtime/DeletionReplacementGate.cs — 정당 substrate (대부분 RETIRED 주석)
Evade/Barrier/Decoy/Fragment/Scapegoat/Save/Ascension/Decode/Partition 발화-반은 모두 "RETIRED"로 표시되고 PRE/POST cut-in 창으로 이관됨 — 이중발화 방지 근거 명시. 잔존 `SacrificeAsync`는 죽은 코드가 아니라 `OverclockEffect.cs:130`에서 실제 호출되는 라이브 경로(확인). `HasReplacementKeyword`의 `AmbientMatchContext.Current` 폴백은 컨텍스트리스 호출부 보정용으로 문서화된 substrate 관례와 일치.

### 8. Headless/Bridge/UnityNullObjectPolicy.cs — 정당 substrate
Unity 전용 접근을 Exclude/Replace/Reject로 분류하는 정책 프레임워크. 게임 룰 판정 없음(순수 분류기). 실사용처 확인: `AutoProcessing.cs`, `AttackProcess.cs`, `G1C-004` 테스트 — 고아 코드 아님.

### 9. Headless/State/VisibilityView.cs — **문제 있음 (발견 2)**
`ForPlayer`가 "본인 소유 zone은 무조건 카드ID까지 전부 공개, 상대는 hidden zone일 때 개수만" 규칙을 적용(`IsCardVisibleToPlayer`: `player.PlayerId == viewerId || DefaultVisibility(zone)==Public`). `ZoneState.DefaultVisibility`가 Hidden으로 지정한 zone: Library/Hand/Security/DigitamaLibrary. Hand는 본인 공개가 규칙상 맞으나, **Security와 Library(덱)는 실제 카드게임 규칙상 소유자 본인도 순서/정체를 모른다**(확인 전까지 비공개 — 시큐리티 체크나 리빌 효과로만 알게 됨). 이 substrate는 이를 무조건 소유자에게 완전 공개한다.
- AS-IS 근거 부재: `docs/goal-specs/G2B-002_visibility_view_포팅.md`에는 이 결정에 대한 AS-IS 대응 언급이 없고, 결과 문서(`docs/test-results/goals/G2B-002_visibility_view_unit_test_results.md`) 자신이 "미해결 리스크"로 "카드별 face-up 공개 규칙이 별도 Goal에서 구체화되면 이 view의 공개 조건을 확장해야 한다"고 인정.
- 테스트(`tests/G2B-002.Visibility.view.Tests`)는 이 동작을 의도적으로 단언(`PlayerViewRevealsOwnHiddenAndPublicOpponentZones`가 owner Security/Library를 카드ID까지 노출됨을 검증)하고 있어, 실수가 아니라 명시적 설계 — 그러나 게임규칙 근거가 없는 substrate 임의결정(축 1)에 해당.
- 부수: 이 view가 RL observation/정보집합의 기반이라면(M2-001 InfoSetObservation 계열), 에이전트가 자기 자신의 덱/시큐리티 순서를 완전히 아는 채로 학습하게 되어 실제 인간 대전 환경과 정보 비대칭이 생긴다.

### 10. Headless/Runtime/ProgressImmunity.cs — 정당 substrate
AS-IS `KeyWordEffects/Progress.cs:62-108`를 라인 단위로 인용하며 `CanNotAffectedClass` 구성(CardCondition/SkillCondition 클로저)까지 1:1 재현. 레지스트리 프로듀서 폐기 후 UntilEndAttackEffects 버킷 직접 추가로 전환된 경위도 명시(R3-W3c-2). `AppliedKey` per-attack dedup은 cut-in 재진입 방지용으로 근거 있음.

### 11. Headless/DataLoading/CardBaseEntityLoader.cs — 정당 substrate
임베디드 `cards.json` → `CardRecord` 변환. 룰 판정 없음(순수 데이터 매핑). EvolutionCondition 인코딩("Color@Level:Cost")은 `CardSource.cs:2516`의 `DigivolutionCostHelpers.ParseEvolutionCondition`과 공유 파서로 확인(발견 1 검증 과정에서 교차 확인).

### 12. Headless/Diagnostics/MatchEventLog.cs — 정당 substrate
JSONL 이벤트 로거. `LevelOf` 타입→레벨 분류는 로깅 심각도 분류이며 게임 룰이 아님. `ZoneMover.Events` append-only 스트림 소비만 하고 신규 계측 없음(자체 주석과 일치).

### 13. Headless/Runtime/AceOverflowGate.cs — 정당 substrate
`AceOverflowClass.Overflow()`(`CardController.cs:5827-5850`) 확인: `cardSource.Owner.AddMemory(-cardSource.OverflowMemory, null)`, 필터 조건(`IsACE && !IsFlipped && (배틀 or 브리딩)`) 일치. `ApplyTopOverflowOnDelete`의 raw-move 3경로 커버 범위(호출측은 별도 파일)는 본 파일 범위 밖이라 미검증이나, 본 파일 자체 로직은 AS-IS 라인과 부합.

### 14. Headless/Runtime/ActionProcessResult.cs — 정당 substrate
성공/실패/불법 액션 결과 DTO. 게임 룰 없음.

### 15. Headless/Services/InMemoryLogSink.cs — 정당 substrate
순수 로그 싱크 구현. 게임 룰 없음.

### 16. Headless/Services/HeadlessPlayerId.cs — 정당 substrate
값 타입 + JSON 컨버터. 게임 룰 없음.

### 17. Headless/Runtime/ContinuousScopeEvaluation.cs — 구조적으로 죽어있음(발견 1의 원인 파일)
`ApplicableEffects`가 하드코딩으로 빈 배열 반환. 자체 주석이 이를 "production-inert"로 명시하고, 4개 라이브 호출자(LinkHelpers/MatchStateMutationSink/BattleDeletionGate/ContinuousRestrictionGate.Evaluate)는 이미 AS-IS 라이브 스캔으로 재배선되어 무해하다고 주장하나, **DigivolveAction의 `IgnoreDigivolutionRequirementKey` 소비자는 union 파트너가 없어 예외**(발견 1 참조). `InheritedEffectKey`/`ConditionKey` 상수 자체는 다른 파일(ContinuousFieldMembership/CardSource)에서 여전히 읽힘 — 상수 정의부는 유효.

### 18. Headless/DataLoading/DeckValidationSmoke.cs — **경미 문제**
`src`/`tests` 전체에서 호출부 0곳(고아 코드). 리터럴 `TODO` 주석("Move to tests once executable test infrastructure is available") 보유 — 현재 저장소에 테스트 프로젝트가 수백 개 존재하므로 이 TODO는 이미 이행 가능한데 방치됨. 4대 축(룰/게이트/침투/발산)과 직접 관련은 없으나 정리 대상으로 기록.

### 19. Headless/Services/InMemoryCardRepository.cs — 정당 substrate
`ICardRepository`의 인메모리 구현. 게임 룰 없음.

### 20. Headless/Services/LegalAction.cs — 정당 substrate
합법 액션 레코드 DTO. 게임 룰 없음.

### 21. Headless/Choices/ChoiceType.cs — 정당 substrate
선택 종류 열거형. 각 값의 주석이 대응 AS-IS 소스(SelectPermanentEffect/RevealLibrary/UserSelectionManager 등)를 명시 — 발명 항목 아님.

### 22. Headless/Services/CardQuery.cs — 정당 substrate
카드 검색 프레디케이트. 게임 룰 없음.

### 23. Headless/Runtime/IActionLegality.cs — 정당 substrate
합법성 검증 인터페이스 계약. 게임 룰 없음(구현체가 룰을 담당).

### 24. Headless/Services/ITerminalOutcomeSink.cs — 정당 substrate
종료 결과 payload 인터페이스. 게임 룰 없음.

### 25. Headless/Services/IStateFingerprintService.cs — 정당 substrate
상태 지문 인터페이스. 게임 룰 없음.

### 26. Headless/Services/ICardInstanceRepository.cs — 정당 substrate
카드 인스턴스 저장소 인터페이스. 게임 룰 없음.

### 27. Headless/Services/ITerminalStateController.cs — 정당 substrate (주석 노후화)
`SetTerminal(bool)` 계약. 리터럴 `TODO` 주석("Replace with real winner/loser terminal state control when rule flow is ported")이 남아있으나 **실제로는 이미 실 룰과 배선됨**: `InMemoryRuleQueryService`가 구현하고, `AutoProcessing.cs:505`(AS-IS 미러)와 `GameFlowProcessor.cs:1194`가 실제 승패 판정 시 `SetTerminal(true)`를 호출 — 게이트 스텁 아님, 단순 주석 노후화(기능은 이미 완성된 상태에서 문구만 안 지워짐).

---

## 발견 전건 목록 (재정리)

1. **[중대/축2·4]** `Headless/Runtime/DigivolveAction.cs` — "디지볼브 요구조건 전체 무시" 연속효과 판정이 영구 빈-배열 스텁(`ContinuousScopeEvaluation.ApplicableEffects`)에만 배선, 라이브 union 파트너 부재(색상-무시 판정과 비대칭). 3개 소비 지점(TryGetDeclaredEvolutionCost②/AddedLevelGatePasses/Validate 인라인)이 구조적으로 영구 무효화.
2. **[중대/축1]** `Headless/State/VisibilityView.cs` — 소유자 본인의 Security/Library(덱) 카드 식별자를 무조건 완전 공개하는 판정이 AS-IS 게임규칙 근거 없이 내려짐(실제로는 확인/공개 전까지 소유자도 비공개여야 함). 결과 문서 자신이 "미해결 리스크"로 인정.
3. **[경미]** `Headless/DataLoading/DeckValidationSmoke.cs` — 호출부 0, 리터럴 TODO 방치된 고아 스모크 헬퍼.
4. **[정보]** `Headless/Services/ITerminalStateController.cs` — 리터럴 TODO 주석이 이미 완료된 배선을 오도. 기능 결함 아님, 문서 노후화.
