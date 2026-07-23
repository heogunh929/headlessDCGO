# Headless substrate 감사 — 파트 4/8

담당: `docs/audit/manifest/hl_part_04.txt` (27개 파일, 5,057줄). 전문 실독 + AS-IS(`DCGO/Assets/Scripts/...`) 실소스 대조. 기존 판정/주석은 근거로 채택하지 않고, 아래 각 발견은 직접 읽은 AS-IS 라인으로 재확인함.

## 종합 판정: **문제 있음** (실질 결함 3건 + 저영향 1건, 나머지 23개 파일은 정상)

---

## 문제 발견 전건 (심각도순)

### P1 — `Headless/Runtime/SecurityResolver.cs`: `[Security]` 스킬과 `OnSecurityCheck`/`OnLoseSecurity` 해결 순서가 AS-IS와 반대 (AS-IS 발산, 상시 발생 — 인터랙티브 케이스만이 아님)

- AS-IS `CardController.cs` `ISecurityCheck.SecurityCheck()` (3940-4117)를 직접 대조:
  1. `:3954-3957` `OnSecurityCheck` 스킬인포를 `triggeredSkillInfos`에 **수집만**(스택/해결 안 함).
  2. `:3982-3985` `IReduceSecurity(refSkillInfos: ref triggeredSkillInfos)` — `OnLoseSecurity` 스킬인포를 같은 리스트에 **추가만**.
  3. `:3987-4105` `[Security]` `ActivateICardEffect`(`secuityEffectSkillInfos`) — 후보가 2개 이상이면 `SelectCardEffect` 인터랙티브 선택 루프를 돌며 **즉시** `ActivateEffectProcess`로 발동(먼저 실행·완결).
  4. `:4111-4114` 그제서야 `triggeredSkillInfos`(`OnSecurityCheck`+`OnLoseSecurity`)를 `autoProcessing`에 스택.
  5. `:4117` `AutoProcessCheck()` — 여기서 비로소 `OnSecurityCheck`/`OnLoseSecurity`가 해결됨.
  → **AS-IS 순서: `[Security]` 먼저 → `OnSecurityCheck`/`OnLoseSecurity` 나중.**

- TO-BE `SecurityResolver.RunSecurityCheckLoopAsync` (:158-210):
  - `:180` `ResolveSecurityCheckWindowAsync`(`OnSecurityCheck`+`OnLoseSecurity` 스택+`AutoProcessCheck`) 먼저 호출.
  - `:198` `ActivatedEffectResolver.ResolveAsync(..., EffectTiming.SecuritySkill, ...)` **그 다음** 호출.
  → **TO-BE 순서: `OnSecurityCheck`/`OnLoseSecurity` 먼저 → `[Security]` 나중** — 정반대.

- 이 순서는 인터랙티브 예외 분기(파일 내 주석의 `design item RD-C2-SECCHECK-INTERACTIVE-ORDERING`)에만 해당하는 게 아니라 **매 보안체크의 기본 실행 경로**에 적용됨. 파일 내 주석은 이 스왑을 "AS-IS는 배틀보다 창을 먼저 처리" 정도로만 서술(:158-161)하고 `[Security]`-vs-창 순서 자체의 역전은 명시 인정하지 않음 — 즉 실제로는 self-documented보다 넓은 범위의 미인지 발산.
- 실질 영향: `OnSecurityCheck`/`OnLoseSecurity` 반응자가 만든 상태 변화(DP 변경, security 추가/제거, 카드 이동 등)를 AS-IS에서는 `[Security]` 효과가 못 보고 먼저 실행되지만, TO-BE에서는 `[Security]` 효과가 그 이후 상태를 보고 실행됨(그 역도 마찬가지). `[Security]`+`OnSecurityCheck`/`OnLoseSecurity`가 공존하는 카드 조합에서 관측 가능한 룰 차이.

### P2 — `Headless/Runtime/SecurityResolver.cs`: `checkCount` 상한이 루프 시작 시점의 `available`로 고정 (AS-IS는 매 iteration `Strike` 기준으로만 재평가) — 저빈도 발산

- AS-IS (`CardController.cs:3924-3940`): `while(true)` 루프 head에서 `player.SecurityCards.Count >= 1`을 **매 iteration** 재평가(줄어들면 break, 늘어나도 계속 반영), 정지 조건은 `checkedCount >= Strike`뿐 — 최초 security 매수가 아니라 매 시점의 실제 매수 + Strike로 계속 체크 가능.
- TO-BE (`SecurityResolver.cs:114-115`): `int checkCount = Math.Min(Math.Max(0, strike), available);`를 루프 진입 **1회**만 계산 — 이후 `for (index < checkCount)`로 상한 고정. 루프 바디는 매 iteration `security.Count == 0`이면 break하므로 매수가 줄어드는 쪽은 반영되지만, 체크 도중 `OnSecurityCheck`/`[Security]` 반응으로 security가 **늘어나는** 경우 AS-IS는 Strike 한도까지 추가로 체크할 수 있는 반면 TO-BE는 최초 계산된 `checkCount`를 넘지 못함.
- 드문 조합(체크 중 시큐리티 추가 효과 + 남은 Strike)에서만 드러나는 좁은 케이스지만 axis(4) 실제 로직 차이.

### P3 — `Headless/DataLoading/DeckValidator.cs`: 덱 구성 범위 게이트가 AS-IS 실제 규칙과 불일치 (`// TODO: Replace these coarse checks with final DCGO deck construction rules.` 자기-인정 placeholder), 임의결정(axis 1)

- AS-IS `DeckData.cs:689-719` `IsValidDeckData()`: 메인 덱은 **정확히 50장**(`DeckCards().Count != 50` → invalid, 범위 아님), 디지타마 덱은 **5장 초과 금지**(`DigitamaDeckCards().Count > 5`). `EditDeck.cs:667` UI 표기도 `"X+Y/50+5"`로 동일 규칙 확인.
- `Headless/DataLoading/StarterDecks.cs`(같은 감사 대상, 정상 판정)의 ST1/ST2/ST3 실데이터가 메인 50장 + 디지타마 4장으로 이 규칙과 정확히 일치 — 즉 저장소 내부에 올바른 기준이 이미 존재함.
- 그러나 `DeckValidator.cs`의 `DeckValidationOptions.Default`: `MinimumMainDeckCount = 0`(디폴트), `MaximumMainDeckCount = 60`; `MinimumDigitamaDeckCount = 0`, `MaximumDigitamaDeckCount = 10` — AS-IS의 "정확히 50" / "최대 5"와 무관한 임의 범위. 예컨대 45장·55장·디지타마 8장짜리 덱도 이 밸리데이터를 통과함(스타터 덱이 우연히 50/4라 통과할 뿐).
- `DefaultCardLimit = 4`는 AS-IS `CardLimitCount` 기본값(4)과 일치 — 이 부분은 정당.
- 추가 누락: AS-IS `DeckBuildingRule.cs`/`BanList`의 `BannedPair`(카드 A를 넣으면 카드 B 계열을 못 넣는 상호배제 밴 규칙, `CanAddCard`:190-207)가 `DeckValidator.cs`에 전혀 없음 — 이 파일이 받는 `Banlist`(`Headless/DataLoading/BanlistLoader.cs:170-185`, 별도 파트 파일)는 카드별 `Limits`(개수 제한)만 모델링하고 `BannedPair` 개념 자체가 substrate에 없어 원천적으로 검증 불가.
- 파일 자체 주석이 "coarse checks, 최종 규칙 아님"이라 명시하므로 정직한 placeholder이긴 하나, 게임 규칙(덱 구성 합법성)을 AS-IS 근거 없이 임의로 정한 substrate 결정이라는 점에서 axis(1) 위반으로 flag.

### P4 — `Headless/Bridge/ActivatedHashtableBridge.cs`: `SecuritySkill` 페이로드의 `isFaceDown`이 항상 `true`로 고정 (axis 1, 현재 영향도 낮음)

- AS-IS `CardController.cs:3946,3997`: `isFaceDown = brokenSecurityCard.IsFlipped`(동적으로 읽은 실제 카드 상태)를 `[Security]` 스킬의 `CanUse`/`Activate` 해시테이블에 실어보냄. `CardSource.cs:56-93` `IsFlipped`는 `SetReverse()`(면다운)/`SetFace()`(면업) 호출로 갱신되는 실제 상태 — `IPutSecurityPermanent` 등 얼굴을 세워 시큐리티에 넣는 효과가 존재하므로 페이스업 시큐리티 카드가 체크될 수 있음.
- TO-BE `ActivatedHashtableBridge.cs:139-144` `EffectTiming.SecuritySkill` 케이스는 `{"Card":..., "isFaceDown": true}`를 **무조건 상수**로 채움 — 실제 카드가 페이스업 시큐리티였어도 항상 face-down으로 보고.
- 흥미롭게도 이 저장소엔 페이스업 시큐리티 상태를 정확히 추적하는 substrate(`Headless/Runtime/SecurityFaceState.cs`, 본 파트 감사 대상, 정상 판정 — AS-IS `IsFlipped` 의미론을 정확히 미러)가 **이미 존재**하는데, `ActivatedHashtableBridge`가 이를 조회하지 않고 상수를 씀 — 있는 인프라를 안 쓴 임의결정.
- 저장소 전체 검색(`GetFaceDownFromHashtable`, `DCGO/Assets/Scripts/Script/CardEffectCommons/GetFromHashtable.cs:337-347`) 결과 이 값을 실제로 읽는 카드 효과가 AS-IS 카드 코퍼스에 **현재 하나도 없음**(호출부 0) — 그래서 당장 관측 가능한 동작 차이는 없음. 파일 자체도 "design item P6A-HT-SECURITY"로 명시 인지하고 있어 완전히 숨겨진 결함은 아니나, 감사 기준상 axis(1) 임의 상수 substrate 결정으로 기록.

---

## 파일별 판정 (27개 전건, 누락 0)

| # | 파일 | 판정 | 비고 |
|---|---|---|---|
| 1 | `Headless/Runtime/SecurityResolver.cs` | **문제** | P1, P2. 그 외(StopSecurityCheck 재평가, IDontBattleSecurityDigimonEffect 스캔, CardDP 폴드, PRE cut-in 창, 배틀지연/재개 상태기계)는 AS-IS 대조 결과 정확 |
| 2 | `Headless/Services/InMemoryZoneMover.cs` | 정상 | 존 리스트 관리 substrate(캐시/삽입순서/북키핑 리셋)만, 게임 룰 임의결정 없음 |
| 3 | `Headless/Runtime/HeadlessActionFactory.cs` | 정상 | 순수 액션 빌더, 판단 로직 없음 |
| 4 | `Headless/Bridge/ActivatedHashtableBridge.cs` | **문제(저영향)** | P4. 나머지 타이밍 케이스는 전건 AS-IS 해시테이블 빌더 인용과 일치(OnDeletion/OnEnterField/OnAddSecurity 등 스팟체크 통과) |
| 5 | `Headless/Runtime/MainSkillActivateAction.cs` | 정상 | `DeclarableZones = BattleArea/Hand/Trash`를 AS-IS `TurnStateMachine.cs:910-933 CanSelect()`와 대조 확인(Security 미포함도 일치) |
| 6 | `Headless/Effects/HeadlessCardEffectContract.cs` | 정상 | 은퇴된 인터페이스의 잔존 레코드 타입만, 게임 로직 없음 |
| 7 | `Headless/State/MatchState.cs` | 정상 | 순수 상태 레코드(플레이어/카드인스턴스/이벤트), 룰 판단 없음 |
| 8 | `Headless/Runtime/HeadlessPhaseMapping.cs` | 정상 | AS-IS `GameContext.cs:116-124 enum phase`(Active/Draw/Breeding/Main/End/None)와 1:1 대조 확인, 서브커서 폴딩 근거 타당 |
| 9 | `Headless/Effects/TriggerTimings.cs` | 정상 | AS-IS `EffectTiming` 이름 상수 집합, 임의결정 없음 |
| 10 | `Headless/DataLoading/DeckValidator.cs` | **문제** | P3 |
| 11 | `Headless/Services/CardRecord.cs` | 정상 | 순수 데이터 레코드, `IsCardType` dual-kind 판정만 |
| 12 | `Headless/Services/StateFingerprintService.cs` | 정상 | 순수 해시 인프라 |
| 13 | `Headless/Choices/ScriptedChoiceProvider.cs` | 정상 | 폴백 선택 로직이 `SelectionValidator`를 실제 평가(무조건 통과 스텁 아님), 실패 시 `ThrowIfInvalid`로 정직하게 실패 |
| 14 | `Headless/Services/IZoneMover.cs` | 정상 | 인터페이스, `InMemoryZoneMover` 구현과 일치 |
| 15 | `Headless/Runtime/SecurityFaceState.cs` | 정상 | AS-IS `CardSource.IsFlipped`/`SetFace`/`SetReverse` 의미론을 정확히 미러(존 이탈 시 무효화 로직도 타당) |
| 16 | `Headless/Services/IEffectQueryService.cs` | 정상 | 은퇴 인터페이스, 잔존 레코드만(주석에 은퇴 사유 명시) |
| 17 | `Headless/Diagnostics/EngineTrace.cs` | 정상 | 순수 진단/로깅 인프라 |
| 18 | `Headless/DataLoading/StarterDecks.cs` | 정상 | ST1/ST2/ST3 수량 합계 각 50(메인)+4(디지타마) 검산 통과, AS-IS 정식 규칙(50장 정확·디지타마 ≤5)과 일치 |
| 19 | `Headless/Runtime/StepResult.cs` | 정상 | 순수 데이터 레코드 |
| 20 | `Headless/Effects/EffectResolutionQueue.cs` | 정상 | 순수 큐 인프라 |
| 21 | `Headless/Runtime/IHeadlessAttackController.cs` | 정상 | 인터페이스, 메서드 시그니처가 AS-IS `SwitchDefender` 의미론 서술과 부합 |
| 22 | `Headless/Services/InMemoryCardInstanceRepository.cs` | 정상 | 순수 인메모리 저장소 |
| 23 | `Headless/Runtime/HeadlessAttackState.cs` | 정상 | 순수 상태 레코드 |
| 24 | `Headless/Runtime/HeadlessPhase.cs` | 정상 | AS-IS `GameContext.phase`와 값/순서 1:1(#8과 동일 근거) |
| 25 | `Headless/Services/IRandomSeedController.cs` | 정상 | 순수 인터페이스 |
| 26 | `Headless/Services/IRuleQueryService.cs` | 정상 | 순수 인터페이스(3메서드); 구현체 `InMemoryRuleQueryService.cs`는 다른 파트 파일이라 본 감사 범위 밖 |
| 27 | `Headless/Choices/IChoiceProvider.cs` | 정상 | 순수 인터페이스 |

---

## 게이트 스텁(axis 2) 관련 참고

`ScriptedChoiceProvider.CreateFallbackChoice`(#13)는 표면적으로 "스텁"처럼 보이나 실제로 `SelectionValidator`를 평가하고 `ChoiceCompletability.TryFindPassingSelection`으로 유효 조합을 탐색 — 무조건 통과 게이트가 아니므로 axis(2) 위반 아님으로 판정. 그 외 26개 파일 중 `=> true`류 무조건 통과 게이트나 빈 본문 스텁은 발견되지 않음.

## 미러로직 침투(axis 3) 관련 참고

`SecurityResolver.cs`는 게임 로직(보안체크 시퀀스) 자체를 substrate 층에 재구현한 파일이나, AS-IS `ISecurityCheck`의 실질 이식(포팅)이지 "미러여야 할 로직이 substrate로 새어든" 발명이 아님 — 다만 P1/P2가 보여주듯 그 이식 자체에 실 로직 결함이 있음. 나머지 파일은 substrate 역할(존 이동/이벤트 기록/직렬화/인터페이스 계약)에 충실하고 게임 로직 발명 없음.
