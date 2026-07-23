# AS-IS↔TO-BE 매칭 검증 — 파트 8/13 (both_part_08.txt, 15파일)

검증 방식: 양측 전문 실독, AS-IS 전 심볼을 TO-BE와 대조. 판단은 실소스 관찰 기반(기존 감사 판정·코드 주석 설명은 근거로 사용하지 않음).

## 요약

| # | 파일 | 판정 |
|---|------|------|
| 1 | CardEffectCommons/GameContextDeterminarion.cs | 정상 |
| 2 | FieldPermanentCard.cs | 정상(스켈레톤, 근거 확인) |
| 3 | SelectAssemblyClass.cs | 정상 |
| 4 | GManager.cs | 정상 |
| 5 | DeckData.cs | **문제 — 미포팅, 무근거 스켈레톤** |
| 6 | CardEffectCommons/CanUseEffects/OnDeletion.cs | 정상 |
| 7 | SelectBurstDigivolutionEffect.cs | 정상 |
| 8 | CardEffectCommons/HashtableSetting.cs | 정상 |
| 9 | SelectCardPanel.cs | 정상(스켈레톤, 근거 명시) |
| 10 | CardPrefab_CreateDeck.cs | 정상(스켈레톤, 근거 확인) |
| 11 | CardEffectFactory/KeyWordEffects/BlastDNADigivolution.cs | 정상 |
| 12 | CardEffectFactory/ChangeSAttack.cs | 정상(경미한 미기재 리네임 1건) |
| 13 | DetailCard_DeckEditor.cs | 정상(스켈레톤, 근거 확인) |
| 14 | SelectAppFusionEffect.cs | 정상 |
| 15 | CardEffectCommons/KeyWordEffects/Alliance.cs | 정상 |

---

## 1. CardEffectCommons/GameContextDeterminarion.cs — 정상

AS-IS(881줄)는 `CardPermanenceMap`/`EnforceLocationCheck`/다수의 `IsExist*Trigger`/`IsExist*Activate`/`IsPermanentExists*`/`MatchCondition*`/`HasMatchCondition*`/`IsOwner*`/`IsOpponent*`/`GetUniqueColourCount*`/`OwnerHas1OrLessTamers`/`UniversalRootCanNoSelectCondition` 등을 포함하는 대형 파셜.

TO-BE(55줄)는 `IsExistOnBattleAreaDigimonTrigger`/`Activate`와 `TurnOwnershipHelpers`(`IsOwnerTurn`/`IsOpponentTurn`/`IsOwner`/`IsOpponent`, HeadlessPlayerId 오버로드)만 보유하고, 나머지 전 심볼은 같은 네임스페이스의 형제 파셜 `CardEffectCommons.cs`(이 매니페스트 밖 파일)에 이식되어 있음을 실그렙으로 확인:
- `EnforceLocationCheck`/`CardPermanenceMap` 존재 확인(`CardEffectCommons.cs:2572,2575`).
- `IsOwnerTurn(CardSource)`/`IsOpponentTurn(CardSource)` 존재 확인(`CardEffectCommons.cs:3207,3246`) — TurnOwnershipHelpers의 HeadlessPlayerId 오버로드와 시그니처 겹치지 않음(별도 오버로드셋).
- 나머지 AS-IS 심볼명 전수(`IsExistOnField` 계열 등) `CardEffectCommons.cs`에서 164건 매치 확인.
- `IsExistOnBattleAreaDigimonTrigger/Activate`가 `CardEffectCommons.cs`에는 없음을 확인 — 중복 정의 없음.

분할 배치는 실소스로 뒷받침됨. 문제 없음.

## 2. FieldPermanentCard.cs — 정상(스켈레톤)

AS-IS(894줄) 전문을 스캔: `Image`/`Text`/`Animator`/`GameObject`/`ParticleSystem` 필드와 `OnClick`/`PointerDown`/`OnBeginDrag`/`ExpandCoroutine`/`ShowPermanentData`(연출) 등 전량이 Unity 렌더링/입력 콜백. 게임 규칙 연산은 없음(표시 갱신만).
TO-BE는 `// TODO: Skeleton only` 헤더뿐(근거 코멘트 없음)이지만, 실제 AS-IS 내용이 순수 뷰 컴포넌트이므로 헤더의 "CoreRule 아님" 결론은 타당. 스켈레톤으로 남겨도 규칙 로직 손실 없음.

## 3. SelectAssemblyClass.cs — 정상

AS-IS(358줄)의 인스턴스 표면(`selectedAssemblyCards`/`CanSelectAssembly`/`CanFulfillConditions`/`CanFulfillEachElementCondition`/`Select`/`SelectTrashCard`/`AddDigivolutiuonCards`/`AddDigivolutiuonCardsByEffect`)이 TO-BE에 라인 단위로 1:1 이식됨(코루틴→Task만 치환). 추가로 파라미터화 액션 경로용 정적 `TryMatchMaterials`/`CanFulfillConditions(static)`/`ValidateMaterials`가 신설되어 있으나 이는 신규 발명이 아니라 AS-IS `CanFulfillEachElementCondition`의 백트래킹 배정 로직을 재사용하는 확장(주석에 AS-IS 라인 대응 명시).
검증한 개별 치환:
- `card.assemblyCondition` → `card.AssemblyConditionOf()`: AS-IS `CardSource.cs:3043`(`assemblyCondition` getter, 캐시된 `IAddAssemblyConditionEffect` 스캔) ↔ TO-BE `CardSource.cs:2248`(`AssemblyConditionOf()`) 동일 로직 확인.
- `card.PermanentOfThisCard().AddDigivolutionCardsBottom(cards, info.cardEffect)` → `permanent.AddDigivolutionCardsBottom(trashCards, info.cardEffect?.EffectSourceCard?.InstanceId)`: TO-BE `Permanent.cs:4252` 시그니처가 `HeadlessEntityId? causeEffectSourceId`를 받음을 확인 — id-스레딩 치환이며 정보 손실 없음(null 케이스도 일치).

문제 없음.

## 4. GManager.cs — 정상

AS-IS(672줄)는 거의 전량 Unity `[Header]` 필드(인스펙터 참조)/Photon/오디오/이벤트이며, `Cheats` 리전(`AllowCheats`/`DrawCard`/`AlterMemory`/`TrashCard`/`TopDeckCard`/`PlaceInSecurity`)만 게임 로직 성격.
TO-BE(208줄)는 UI/Photon 필드를 전부 제거하고 `turnStateMachine`/`autoProcessing`/`autoProcessing_CutIn`/`attackProcess`/`userSelectionManager`/`GetComponent<T>()`만 컨텍스트 스코프 서비스 라우터로 재구현. `Cheats` 리전이 구동하는 `CheatAction`(치트 액션 타입)이 TO-BE에서 별도 파일로 실존 이식되어 있음을 확인(`Headless/Runtime/CheatActionGuard.cs`, `MainPhaseAction/CheatAction.cs`, `HeadlessLegalActionDispatcher.cs` 등에서 `CheatAction` 참조) — GManager 자신은 UI 필드만 삭제한 것이 아니라 실제 게임로직 표면은 위임되어 다른 곳에 살아있음을 확인. `GetComponent<T>()`의 화이트리스트가 AS-IS 컴포넌트 전량을 커버하지 못하면 `NotSupportedException`을 던지도록 설계되어 있어 무음 누락을 방지.

문제 없음.

## 5. DeckData.cs — **문제: 미포팅, 무근거 스켈레톤**

AS-IS(868줄)는 다음을 포함하는 실질적 게임 로직 클래스:
- 생성자에서 콤마 구분 덱코드 문자열을 파싱해 `m`(256)진수/`n`(256)진수 상호 변환(`ConvertBinaryNumber.NStringToInt`/`NKStringToNString`)으로 카드ID 리스트·장수·KeyCardId를 복원(:18-143).
- `DeckCards()`/`DigitamaDeckCards()`/`AllDeckCards()`/`KeyCard`(정렬 규칙 포함)/`AddCard`/`RemoveCard`/`IsValidDeckData()`(덱코드 유효성 검증, 매직넘버 114514 센티널 체크 등, :600줄대)/`GetDeckCode`(역방향 인코딩) 등.
- 이 덱코드 코덱은 AS-IS 매치-셋업 경로에서 실호출됨: `CardObjectController.cs:16-141`(`CreatePlayerDecks`, AI/네트워크 상대의 초기 덱 구성)와 `SelectDeck.cs`/`DeckBuildingRule.cs`/`ContinuousController.cs`/`CreateNewDeckButton.cs`/`StarterDeck.cs`에서 `new DeckData(...)` 다수 호출 확인.

TO-BE는 `// TODO: Skeleton only. Port or implement deterministic .NET logic later.` 8줄 placeholder뿐 — 코덱 로직 전무. 헤더는 `Category: CoreRule`, `Priority: HIGH`로 자체 분류되어 있음에도 미이식 상태.

실그렙 확인 결과 TO-BE `CardObjectController.cs`에는 `CreatePlayerDecks`/`DeckData`/`DeckCodeUtility` 참조가 전무하며, 대신 `Headless/DataLoading/DeckListLoader.cs`(파일 기반 평문 덱리스트 로더)가 별도 경로로 존재. 즉 헤드리스 엔진은 AS-IS 덱코드(base-256 압축 문자열) 포맷을 전혀 지원하지 않고 다른 포맷(`DeckListLoader`의 텍스트 리스트)으로 대체한 것으로 보이나, 이 대체에 대한 근거/문서화가 `DeckData.cs`(또는 `DeckCodeUtility.cs`, 같은 스켈레톤 패턴)어디에도 없음 — "TODO: skeleton only, implement later"는 정당화가 아니라 미완료 표시임.

`SelectCardPanel.cs`(9번)는 동일하게 스켈레톤이지만 UI 전용이라는 근거가 실소스로 뒷받침되는 구체적 코멘트를 남겼고(§9 참조), `DeckData.cs`는 그런 근거 코멘트가 없다는 점에서 구조적으로 다름 — DeckData.cs의 스켈레톤 상태는 검토·정당화되지 않은 채 방치된 것으로 판단.

**결론**: 덱코드 인코딩/디코딩 알고리즘(플레이어가 공유하는 표준 덱코드 포맷의 유일한 구현)이 헤드리스 엔진에 전혀 존재하지 않음. 외부 인터페이스가 AS-IS와 동일한 덱코드 문자열을 받아야 하는 경우 실패한다. 대체 경로(DeckListLoader)가 있다는 사실만으로는 무죄 처리할 수 없음(그 경로가 AS-IS 덱코드 포맷과 호환되는지 근거 없음).

## 6. CardEffectCommons/CanUseEffects/OnDeletion.cs — 정상

AS-IS(404줄)의 전 함수(`CanTriggerOnDeletion`/`CanTriggerOnPermanentDeleted`/`CanTriggerOnPermanentLeave`/`IsByBattle`/`IsByEffect`/`CanActivateOnDeletion`/`IsTopCardInTrashOnDeletion`/`IsTopCardSamePermanent`/`CanActivate*WithContainingCardName`/`...WithContainingTrait`/`...WithCardColors`/`...WithSaveText`)가 TO-BE(433줄)에 라인 단위 동일하게 존재. 추가된 것은 `IsByBattle`/`IsByEffect`에 대한 폴백 마커 리드(`ByBattleCauseKey`/`ByEffectCauseKey`)뿐이며, 이는 라이브 `IBattle`/`ICardEffect` 객체가 없는 트랜스포트 경로를 위한 파생-불리언 어댑테이션으로 원본 truth table을 보존한다고 코멘트에 근거 명시. OR-폴백 구조이므로 AS-IS 경로(라이브 객체 존재 시)는 원본 그대로 동작.

문제 없음.

## 7. SelectBurstDigivolutionEffect.cs — 정상

AS-IS(345줄) 전체(`SetUp_SelectWheterToBurst`/`SetUp_SelectTamer`/`SelectWheterToBurst`/`SelectTamer`/`BounceTamer`/`AddTrashTopCardAtTurnEnd`)가 TO-BE(443줄)에 1:1 이식. AS-IS 특유의 버그(:214에서 `_endSelectCoroutine_Burst != null` 가드 후 `_endSelectCoroutine_SelectTamer` 호출)가 TO-BE에도 동일하게 보존되어 있음을 확인(코멘트로 "AS-IS QUIRK KEPT" 명시) — no-simplification 원칙 준수.
`OpenSelectCardPanel` 2択 패널이 `ChoiceType.ModeChoice` 요청으로 대체된 것은 SelectCardPanel.cs가 UI 전용으로 스킵된 것(§9)과 정합적인 어댑테이션.

문제 없음.

## 8. CardEffectCommons/HashtableSetting.cs — 정상

AS-IS(333줄)의 전 빌더 함수(`CardEffectHashtable`/`PierceCheckHashtableOfPermanent`/`OnDeletionCheckHashtableOfPermanent`/`WhenPermanentWouldRemoveFieldCheckHashtable`/`OnDeletionHashtable`/`OnEnterFieldHashtable`/`WouldEnterFieldHashtable`/`WouldLinkHashtable`/`OnPlayCheckHashtableOfCard`/`WhenDigivolvingCheckHashtableOfCard`/`OptionMainCheckHashtable`/`OnPlayCheckHashtableOfPermanent`/`WhenDigivolutionCheckHashtableOfPermanent`/`OnAttackCheckHashtableOfCard`/`OnAttackCheckHashtableOfPermanent`/`WhenDigivolutionCardWouldDiscardedCheckHashtable`)가 TO-BE(444줄)에 키 문자열까지 동일하게 존재. 확장은 (a) 배틀 트랜스포트용 오버로드 2건(byBattleCause/byEffectCause 파생 마커, §6과 동일 패턴), (b) `Permanent`/`CardColor` 타입을 mirror-view 생성자로 바꾸는 치환(`new Permanent(context, instanceId, ownerId)` 등)뿐. 치환 심볼 존재 확인:
- `CardSource.ToCardColorList` 실존(`CardSource.cs:188`).
- `Permanent.OwnerId` 실존(`Permanent.cs:74`).

문제 없음.

## 9. SelectCardPanel.cs — 정상(스켈레톤, 근거 명시)

AS-IS(688줄)는 `TextMeshProUGUI`/`ScrollRect`/`Button`/`DG.Tweening`/`GameObject.SetActive`/`Instantiate` 등 순수 Unity UI 패널. TO-BE 헤더 코멘트가 "카드 선택은 헤드리스에서 ChoiceProvider substrate로 대체"라고 명시하고, 실제로 §7·§14에서 확인했듯 이 패널을 호출하던 AS-IS 지점들(`SelectBurstDigivolutionEffect`/`SelectAppFusionEffect`)이 TO-BE에서 `ChoiceType.ModeChoice` 요청으로 대체되어 있음을 실소스로 확인 — 근거 있는 스킵.

문제 없음.

## 10. CardPrefab_CreateDeck.cs — 정상(스켈레톤, 근거 확인)

AS-IS(636줄) 전문 필드/헤더 스캔: `Image`/`ScrollRect`/`Animator`/`TextMeshProUGUI`/`Shapes2D`/`Coffee.UIEffects` 등 전량 덱 에디터 카드 프리팹의 UI 컴포넌트. 게임 규칙 연산 없음. TO-BE는 무근거 헤더뿐이나 실제 내용이 UI 전용이므로 결과적으로 문제 없음.

## 11. CardEffectFactory/KeyWordEffects/BlastDNADigivolution.cs — 정상

AS-IS(259줄)의 `BlastDNACondition`(top-level 헬퍼 클래스)과 `BlastDNADigivolveEffect`(트리거 조건/활성 조건/`ActivateCoroutine`의 퍼머넌트·핸드소스 선택·조그레스 플레이 전체)가 TO-BE(305줄)에 1:1 이식. `card.Owner.HandCards`(Player 프로퍼티) 등은 `new Player(card.Context, card.Owner).HandCards` 경로로, `selectedCardSource.PermanentOfThisCard()`는 `ICardEffect.ResolvePermanentOfThisCard`로 치환 — 모두 문서화된 어댑테이션.

문제 없음.

## 12. CardEffectFactory/ChangeSAttack.cs — 정상(경미한 미기재 리네임 1건)

AS-IS(271줄)의 `ChangeSelfSAttackStaticEffect`/`ChangeTargetSAttackStaticEffect`/`ChangeSAttackStaticEffect`/`InvertSelfSAttackStaticEffect`/`InvertTargetSAttackStaticEffect`/`InvertSAttackStaticEffect` 전체가 TO-BE(282줄)에 1:1. 파일 헤더가 명시한 어댑테이션은 2건뿐(`PermanentOfThisCard()`→`ResolvePermanentOfThisCard`, `CanNotBeAffected(ICardEffect)`→동일)이나, 실제로는 3번째 미기재 치환이 있음:
- AS-IS `invertSAttackClass.SetHashString($"InvertSecA_{card.CardID}")`(:228) → TO-BE `SetHashString($"InvertSecA_{card.CardNumber}")`(:238).
- 검증: AS-IS `CardSource.CardID`(`CardSource.cs:3454`)는 `_cEntity_Base.CardID`(카드 고유 번호 문자열)를 반환. TO-BE `CardSource.CardNumber`(`CardSource.cs:1166`)는 `Definition?.CardNumber`를 반환 — 양쪽 다 "카드의 고유 식별 번호 문자열"이라는 동일 의미론을 가짐. AS-IS 쪽에는 `CardNumber`라는 이름의 심볼이 없고(`CardID`만 존재), TO-BE 쪽에는 `CardID`가 없어(`CardNumber`만 존재) 순수 리네임으로 판단됨.

의미론적으로는 근거 있는 치환이나, 파일 헤더 ADAPTATIONS 목록에 기재되지 않은 채 이뤄진 점은 문서화 누락. 기능적 결함은 아님(해시스트링 유일성 목적에 부합).

## 13. DetailCard_DeckEditor.cs — 정상(스켈레톤, 근거 확인)

AS-IS(528줄) 앞부분(71줄 확인) 및 필드 전체가 `TextMeshProUGUI`/`Image`/`SerializeField` UI 요소와 `SetUpDetailCard`(카드 스프라이트 로드·텍스트 바인딩) 등 표시 전용. 게임 규칙 없음. TO-BE 무근거 헤더지만 결과적으로 문제 없음.

## 14. SelectAppFusionEffect.cs — 정상

AS-IS(241줄) 전체(`SetUp_SelectWheterToAppFusion`/`SetUp_SelectLink`/`SelectWheterToAppFusion`/`SelectLink`/`AddToSources`)가 TO-BE(281줄)에 1:1. §7과 동일한 AS-IS 버그 보존 패턴 확인(`SelectLink`의 `_endSelectCoroutine_AppFusion != null` 가드 후 `_endSelectCoroutine_SelectLink` 호출, :212 대응) — 코멘트로 "AS-IS quirk KEPT" 명시.

문제 없음.

## 15. CardEffectCommons/KeyWordEffects/Alliance.cs — 정상

AS-IS(220줄)의 `CanActivateAlliance`/`AllianceProcess`/`GainAlliance`/`GainAlliancePlayerEffect`가 TO-BE에 이식되어 있으며, AS-IS `Alliance.cs:95`의 `!selected.TopCard.CanNotBeAffected(activateClass)` 서스펜드 가드가 한 차례 리팩터(P6 cluster2)에서 누락되었다가 복원된 이력이 코멘트로 자체 기록됨("C-Atk fidelity ... Restored here") — 현재 코드에는 가드가 실존.
`SuspendPermanentsClass` 생성자가 AS-IS `(permanents, Hashtable)`에서 TO-BE `(permanents, ICardEffect?, bool isBlock)`로 변경되어 있으나, AS-IS 쪽에서 `CardEffectHashtable(activateClass)`(= `{"CardEffect": activateClass}`만 포함, IsBlock 키 없음)를 넘기던 호출은 `Tap()` 내부에서 `IsBlock(hashtable)`이 키 부재 시 false를 반환하는 것과 동일하므로, TO-BE의 `isBlock: false` 인자는 동치. (AS-IS 생성자 정의: `CardController.cs:5560`; TO-BE 생성자 정의: `CardController.cs:1873`.)

문제 없음.

---

## 종합

- **문제 발견**: 1건 — DeckData.cs(§5), 근거 없는 미포팅 스켈레톤. 매니페스트 자체가 `CoreRule`/`HIGH`로 분류했음에도 AS-IS의 덱코드 코덱 알고리즘이 전혀 이식되지 않았고, 스킵에 대한 근거 코멘트(SelectCardPanel.cs식)도 없음. 헤드리스 엔진이 다른 경로(DeckListLoader, 평문 덱리스트)를 쓰고 있다는 정황은 확인했으나 AS-IS 덱코드 포맷과의 호환/대체 근거는 어디에도 문서화되어 있지 않음.
- **경미 사항**: 1건 — ChangeSAttack.cs(§12)의 `CardID`→`CardNumber` 미기재 리네임(의미론 확인됨, 기능 결함 아님).
- **나머지 13개 파일**: 실소스 대조 결과 AS-IS↔TO-BE 매칭 정상(스켈레톤 4개는 전문 스캔으로 UI 전용임을 직접 확인).
