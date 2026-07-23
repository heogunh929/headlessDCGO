# AS-IS↔TO-BE 매칭 검증 — part 03

manifest: `docs/audit/manifest/both_part_03.txt`
AS-IS root: `DCGO/Assets/Scripts/<relpath>`
TO-BE root: `src/HeadlessDCGO.Engine/Assets/Scripts/<relpath>`

전문 실독 완료(양측). 판단 기준: AS-IS 심볼 소실/오배선 여부를 실소스 대조로 직접 확인(기존 코드주석·감사판정은 근거로 사용하지 않음).

---

## 1. Script/CardEffectCommons.cs

- AS-IS: 1448줄. `partial class CardEffectCommons`의 "main" 파일 — Digivolution 관련 enum 1개(`IgnoreRequirement`) + 정적 헬퍼 약 40개(PlayPermanentCards/PlayOptionCards/PlaceDelayOptionCards/PlayToken 계열 17종 named wrapper/AddThisCardToHand/`*AndProcessAccordingToResult` 8종/TrashDigivolutionCardsFromTopOrBottom/OptionMainEffect/OptionSecurityEffect/AddActivateMainOptionSecurityEffect/ActivateMainOfOptionSide/DigivolveIntoHandOrTrashCard/DigivolveIntoExcecutingAreaCard/GetCardEffectByEffectTiming/DrawAndDiscardCards).
- TO-BE: 5352줄. `static partial class CardEffectCommons`. 구조는 "substrate 구현체(ctx/`CardSource` 기반, `CardEffectCommons.cs:xxx` AS-IS 라인 인용과 함께 새로 작성)" + "AS-IS 원시그니처 BRIDGE 오버로드(`ICardEffect activateClass`/`Hashtable`/`SelectCardEffect.Root` 그대로 유지, substrate로 위임)"의 이중 계층. 각 BRIDGE는 헤더 주석에 정확한 AS-IS 줄번호를 인용하며 파라미터 재형(List↔IReadOnlyList, IEnumerator↔Func&lt;Task&gt; 등)만 수행 — 델리게이션 로직을 직접 추적한 결과 실제로 순수 위임(추가 로직 없음)임을 확인.

검증한 심볼 40개 중 **39개는 TO-BE에 실장 확인**(정의부 직접 열람, 문자열 매치가 아님):
- `IgnoreRequirement` enum — 동일 4값(None/All/Level/Color), 동일 위치(class 최상단 nested).
- `PlayPermanentCards`(:1795 substrate + :4935 bridge), `PlayOptionCards`(:4980 — bridge뿐, substrate 없음. AS-IS 명령형 그대로 재구현, 아래 세부 확인함), `PlaceDelayOptionCards`(:434 substrate + :5108 bridge), `PlayToken`(:2323) + 17개 named wrapper 전부(:2400-2464 substrate형 + :5170-5322 bridge형) — 토큰 스펙 테이블(`TokenSpecs`, AS-IS `ContinuousController.CreateTokenData` 인용)까지 일치.
- `AddThisCardToHand`(:1757), 8종 `*AndProcessAccordingToResult`(Suspend/Delete/Bounce/DeckBounce/TrashDigivolutionCards/TrashLinkCards/TrashSecurity/TrashHand/PlacePermanentInSecurity 전부 substrate+bridge 쌍 확인), `TrashDigivolutionCardsFromTopOrBottom`(:249 substrate + :4564 bridge — bridge가 substrate에 없는 `cardCondition` 필터를 보충하는 이유까지 주석에 근거 제시, 실제 바디도 그렇게 구현됨).
- `OptionMainEffect`(:4407 — "REHOUSED" 표시로 별도 sibling 파일에서 이 monolith로 재배치됐다는 주석. 실제로 `CardEffectCommons/OptionMainEffect.cs` sibling 파일은 현재 존재하지 않음 — 확인됨, 이관 완료 상태), `AddActivateMainOptionSecurityEffect`(:4135), `ActivateMainOfOptionSide`(:2737 substrate + :4621 bridge), `DigivolveIntoHandOrTrashCard`(:1874 substrate + :4424 bridge — AS-IS의 잘못된 매핑을 시정했다는 이력 주석까지 있음, 현재 코드는 올바른 "hand/trash에서 대상에게 진화" 방향), `DigivolveIntoExcecutingAreaCard`(:2708 + :4458), `GetCardEffectByEffectTiming`(:2782 — AS-IS :1402과 시그니처/바디 완전 동일), `DrawAndDiscardCards`(:2610 substrate + :4660 bridge).

**문제 발견: `OptionSecurityEffect(CardSource card)` (AS-IS CardEffectCommons.cs:717) 미이관.**
- AS-IS는 `OptionMainEffect`(:711)와 쌍을 이루는 대칭 getter로, `EffectList(EffectTiming.SecuritySkill)`에서 `"[Security]"` 태그를 가진 `ActivateClass`를 찾는다. `OptionMainEffect`는 TO-BE에 정식 bridge로 존재하지만, `OptionSecurityEffect`는 TO-BE `CardEffectCommons.cs` 어디에도 해당 이름의 메서드가 없다.
- 실사용처 확인: AS-IS에서 `CardEffectCommons.OptionSecurityEffect(` 직접 호출은 2곳뿐 — `BT18_098.cs`, `BT15_092.cs` (참고: `AddActivateMainOptionSecurityEffect`와 이름이 겹쳐 단순 grep은 241개로 오검출됨 — 재확인 시 실제 직접 호출은 2건).
- TO-BE `BT18_098.cs`는 결손을 인지하고 카드 파일 내부에 `OptionSecurityEffectOf(CardSource)`라는 private 로컬 메서드로 AS-IS 바디를 그대로 인라인 재구현("미러에 해당 브릿지 없음" 주석으로 명시). 기능적으로는 등가이지만, `OptionMainEffect`와 대칭인 공용 커먼즈 심볼이 이 file-pair에서 빠져 있고, 대신 호출부마다 중복 재구현하는 구조 — 향후 두 번째 실사용처(`LM_047`/`LM_046`, `BT15_092` 포팅 시)에서 또 별도 인라인이 반복될 위험이 있음(현재 `LM_047.cs`/`LM_046.cs`도 이미 자체 인라인 확인됨).

**구조적 관찰(결함 아님, 기록용):** TO-BE 파일은 AS-IS `Script/CardEffectCommons.cs` 1개 파일의 미러를 넘어, `CanUseEffects/*.cs` 및 `GetFromHashtable.cs`(AS-IS에서는 별도 sibling partial 파일)의 트리거-게이트 술어들을 `CardEffectResolveContext` 기반의 **새 오버로드**로 대량 추가 포함하고 있다(예: `CanTriggerOnPlay(ctx, card, ...)` — AS-IS는 `CanUseEffects/PermanentEnterField/OnPlay.cs`에 `Hashtable` 시그니처로만 존재; TO-BE는 그 파일에도 여전히 Hashtable 버전이 남아있고, 추가로 이 monolith에 ctx 버전이 병존). 각 항목은 정확한 AS-IS 원본 파일:줄 인용이 달려 있고 "(W6-T)" 설계 태그로 일관되게 표시되어 발명이 아님을 자체 문서화하고 있음(별도 설계서 W6-T 참조 표시). 다만 이 내용의 진짜 AS-IS 앵커는 본 manifest 파트가 아닌 다른 파일들(`CardEffectCommons/CanUseEffects/*.cs`, `CardEffectCommons/GetFromHashtable.cs`)이므로, 그쪽 매니페스트 파트에서 중복 여부(신·구 두 API 공존에 따른 드리프트 위험)를 함께 확인할 필요가 있음 — 이 파트의 판정 범위(AS-IS `Script/CardEffectCommons.cs` 자체)에서는 결손이 아니라고 판단.

**판정: 부분 정합 — 1건의 실질 결손(OptionSecurityEffect 커먼즈 미이관), 나머지는 검증 완료.**

---

## 2. Script/TurnStateMachine.cs

- AS-IS: 3373줄. `MonoBehaviourPunCallbacks`. Photon 네트워킹(룸/로비/RPC)·UI(commandText/selectCardPanel/ShowPhase/outline/드래그/BGM/SE)·룰 로직(턴 페이즈 바디, 승패 판정, mulligan, 공격 펌프)이 뒤섞인 단일 파일.
- TO-BE: 825줄. `sealed class TurnStateMachine`(순수 C#, Unity/Photon 의존 없음). 헤더 주석: "card effects가 접근하는 유일한 멤버는 `.gameContext`" + "턴-흐름 바디 자체는 substrate `GameFlowProcessor`/`TurnFlowPump`가 담당, 재배치는 고위험/저가치라 보류"라고 스코프를 명시.

검증 방법: TO-BE가 인용하는 AS-IS 줄번호(예: `:341-504`, `:530-648`, `:652-697`, `:701-837`, `:877-1351`, `:3151-3210`, `:3050-3372`)를 AS-IS 원문과 직접 대조.

- `StartGameAsync`↔AS-IS `StartGame()`(:341-504): draw 5·mulligan(MulliganCoordinator로 외부화)·security 5 확인. `:358 gameContext.FirstPlayer = gameContext.NonTurnPlayer`까지 정확히 인용.
- `ActivePhaseAsync`↔`ActivePhase()`(:530-648): OnStartTurn 윈도우(:564)·공격 펌프(:570-576, :632-638)·EndTurnCheck(:579, :641)·Unsuspend 블록(:586-624, `TopCard.Owner==turnPlayer || HasReboot` 조건 포함)까지 라인 대 라인 일치 확인.
- `DrawPhaseAsync`↔`DrawPhase()`(:652-697): 턴1 드로우 스킵(`TurnCount != 1`)·deck-out 패배(`LibraryCards.Count==0`→`EndGame(NonTurnPlayer,false)`) 확인.
- `BreedingPhaseAsync`↔`BreedingPhase()`(:701-837): "hatch가 둘 다 가능할 때 우선"하는 AS-IS 쿼크(`CanHatch || !CanMove`, :802-812)를 코드 원문 대조로 재확인 — TO-BE :371 동일 조건 그대로 보존.
- `MainPhaseAsync`↔`MainPhase()`(AS-IS 실제 선언은 :877, TO-BE 주석은 ":935-1351"로 펌프 루프 시작점만 표기하지만 :877-933의 `CanSelect()`/로그/윈도우 부분도 실제로는 :888-933 라인 대 라인으로 포팅되어 있음 — 인용 범위 표기가 다소 헐렁하나 누락은 아님, 직접 대조로 확인). `CanSelect()` 5개 조건 전부 동일 순서·동일 조건식. AI 자동 플레이 분기(:989-1160)는 "헤드리스 에이전트가 대체"로 명시적으로 대체(스코프 조정, 은닉 아님).
- `EndPhaseAsync`↔`EndPhase()`(:3151-3210): 버킷 리셋을 `HeadlessEndTurnCleanupFlow.Cleanup`으로 단일화(중복 소유 방지 근거 명시), `:3204-3208` 카드별 사용횟수 리셋은 직접 인라인 유지 — 이관 주체 분리 이유(신모델 vs 구모델)까지 근거 제시.
- `SetActSkill`/`SetActCardSkill`/`SetPlayCard`/`SetAttackingPermaent`/`PassTurn`/`EndGame`(:3050-3372) 전부 실장 확인. `SetAttackingPermaent`(원문 오타 그대로 보존), `feild`/`nonTurnPlayerFeid` 변수명 오타까지 그대로 유지된 것도 확인(기계적 미러 특성 일치).
- `EndGame`: AS-IS 본문은 Photon/씬전환/BGM/UI 위주(:3302-3360)이고 룰 관련은 `endGame = true`와 `resultObject.ShowResult(Winner,...)` 뿐임을 원문 확인. TO-BE는 승자 대신 패자를 마킹(`winner.Enemy`→`MarkLose`)하는 어댑테이션을 명시적으로 취함 — 로직 자체 손실 없음.

**AS-IS `Init()`(:34-340, TO-BE가 스코프 밖으로 선언한 영역)에 대한 별도 검증**: 이 영역엔 Photon 룸/로비 연결, 플레이어 이름, 랜덤시드 RPC, 덱 카드 생성, memory 초기화, 그리고 **`gameContext.TurnPlayer = gameContext.PlayerFromID(GameRandom.Range(0, 2))`(선공 랜덤 결정)**이 포함되어 있어 이 부분이 룰 로직 소실인지 확인 필요 판단. 실사냥 결과: 이 랜덤 선공 결정은 TurnStateMachine.cs가 아니라 **substrate `MatchSetupFlow.ResolveFirstPlayer`**(`Headless/Runtime/MatchSetupFlow.cs:144-157`)에 `randomSource.NextInt(0, playerIds.Count)`로 이식되어 있음을 확인 — `DcgoMatch.Initialize`가 `TurnController.Initialize(config.PlayerIds, setupResult?.FirstPlayerId)`로 호출하는 체인 확인. **소실 아님, 다른 파일로 재배치**로 판정(TurnStateMachine.cs 헤더의 "Init()은 스코프 밖" 선언과 정합).

AS-IS `SetMainPhase()`(:1354-2872, 1500줄+)는 TO-BE에서 "순수 UI"로 스킵 처리됨 — `SetTurnPhase`/`Hatch`/`MovePermanent`/`Draw(`/`EndTurnProcess`/`PlayCardClass`/`attackProcess.Attack` 등 룰-상태 변경 키워드로 전수 스캔했으나 해당 구간에 매치 0건, UI(마우스클릭→`SetPlayCard`/`SetActSkill`/`SetAttackingPermaent` 파라미터 세팅) 로직뿐임을 확인 — 스킵 판단이 근거 있음.

AS-IS `GameStateMachine()`(:296-337, 외부 루프: StartGame→{ActivePhase→DrawPhase→BreedingPhase→MainPhase→EndPhase}×무한)는 TO-BE에 동명 메서드가 없음 — 주석대로 `Headless/Runtime/TurnFlowPump.cs`(별도 파일)로 재배치되어 있음. 확인: 해당 파일이 실존하며(`Headless.Runtime.TurnFlowPumpHost`로 다수 참조됨) 헤더 주석의 "AS-IS continuous driver chain" 설명과 부합 — 파일 경로가 바뀐 재배치이지 소실이 아님.

**판정: 정합.** 대폭적인 줄수 축소(3373→825)는 UI/Photon 박리 + 턴-흐름 드라이버(펌프)를 별도 substrate 파일로 위임한 결과이며, 룰 로직 자체의 소실은 발견하지 못함. 표기된 AS-IS 줄번호 인용을 원문과 대조한 결과 모두 정확했음(단, MainPhase 인용 범위 표기가 실제 포팅 범위보다 좁게 적혀 있는 사소한 문서 정확도 이슈 — 기능 결손 아님).

---

## 요약

| 파일 | 판정 | 발견 |
|---|---|---|
| Script/CardEffectCommons.cs | 부분 정합 | `OptionSecurityEffect(CardSource)` (AS-IS :717) 커먼즈 브릿지 미이관 — 실사용 2곳(BT18_098/BT15_092)에서 카드별 인라인 재구현으로 대체(기능 등가이나 대칭 심볼 `OptionMainEffect`와 달리 공용화되지 않음) |
| Script/TurnStateMachine.cs | 정합 | 없음(줄수 축소는 UI/Photon 박리+substrate 재배치이며 룰 로직 소실 없음 확인) |
