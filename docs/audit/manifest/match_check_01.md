# AS-IS↔TO-BE 매칭 검증 — Part 1/13

담당 파일: `Script/Permanent.cs` (유일 항목)
AS-IS: `DCGO/Assets/Scripts/Script/Permanent.cs` (4187줄)
TO-BE: `src/HeadlessDCGO.Engine/Assets/Scripts/Script/Permanent.cs` (4640줄)

전문 실독 완료(양측). AS-IS는 Unity MonoBehaviour 필드 컨테이너(`cardSources` 리스트 필드 기반), TO-BE는 `EngineContext`+`HeadlessEntityId` 기반의 무상태 VIEW(`sealed class Permanent`, 매 접근마다 재구성)로 재작성됨 — 이 프로젝트의 확립된 "substrate 번역" 방침(메모리: AS-IS mirror migration decision)에 부합하는 근본 아키텍처 차이. 아래는 그 틀 안에서 발견한 개별 항목.

## 총평
대부분의 getter/스캔 로직(HasDP, BaseDP, GetDP/DP, Level, IsDigimon, IsTamer, IsOption, CanMove류, HasBlocker/HasJamming/HasReboot/HasRush 등 키워드 판정, LinkedMax→FoldLinkedMax, CannotReturnToHand)은 AS-IS 블록별로 정확한 줄 번호를 인용하는 주석과 함께 스캔 순서·중첩 구조·게이트 순서까지 축자 이식되어 있음을 실코드 대조로 확인. Add/Remove 계열(AddDigivolutionCardsTop/Bottom, AddLinkCard, RemoveLinkedCard)은 IEnumerator→async Task 전환과 함께 대규모로 재구성되었으나, 각 분기(re-root, 토큰 가드, 배치 처리)에 AS-IS 줄 대응 근거가 달려 있고 표본 대조 결과 충실함.

그러나 **AS-IS에 존재하는 심볼 중 TO-BE Permanent.cs에서 전혀 찾을 수 없고, 파일 밖에서도 이식 근거를 못 찾은 것**이 다수 있음. 아래 "문제" 항목 참조.

---

## 문제로 분류한 발견

### P1. `DPWhenSuspended` 필드 완전 소실 (getter 없음, write-only 고아 데이터)
- AS-IS: `public int DPWhenSuspended = 114514;` (Permanent.cs:1958), `CardController.cs:5620`에서 `permanent.DPWhenSuspended = permanent.DP;`로 기록, `BT9_018.cs:147`에서 `permanent.DPWhenSuspended <= 6000`로 **실제 게임로직에 소비**(UI 아님).
- TO-BE: Permanent.cs에 `DPWhenSuspended`라는 이름의 프로퍼티/필드가 전혀 없음. 쓰기 쪽만 `CardController.cs`의 `SuspendPermanentsClass`가 `"dpWhenSuspended"` 메타데이터 키에 기록(주석에 "design item RD9-87"로 자인). 그러나 **읽기 접근자가 Permanent.cs 어디에도 없음** — `dpWhenSuspended`/`DpWhenSuspendedKey`를 grep해도 쓰기 1곳만 존재, 읽는 코드 0.
- 판단: 실제 결함. BT9_018.cs는 현재 스텁(미이식)이라 당장 컴파일 깨짐은 없지만, Permanent.cs 자체가 AS-IS 공개 API를 온전히 미러하지 못한 상태 — 향후 BT9_018류 카드를 이식할 때 `permanent.DPWhenSuspended`에 대응하는 멤버가 Permanent 클래스에 없다.

### P2. `DigivolutionOrLinkCards` 계산 프로퍼티 완전 소실, 이식 흔적 없음
- AS-IS: `public List<CardSource> DigivolutionOrLinkCards => cardSources.Filter(cardSource => cardSource != TopCard);` (Permanent.cs:892) — TopCard를 제외한 전 카드(진화원+링크 카드 전부). `BT25_085.cs`(:197, :242)에서 실제 게임로직(트래시 대상 판정, `customRootCardList`)에 사용.
- TO-BE: 전체 트리 grep 결과 `DigivolutionOrLinkCards` 문자열 자체가 0건. `DigivolutionCards`(진화원만, 링크 제외)만 존재 — AS-IS의 `DigivolutionCards`(링크 제외)와는 이름이 같지만 별개 심볼인 `DigivolutionOrLinkCards`(링크 포함)의 대응물이 없음.
- 판단: 실제 결함. BT25_085.cs도 현재 스텁이라 당장 발현되지 않지만, 향후 이식 시 대응 멤버 부재.

### P3. `LibraryBounceEffect` / `HandBounceEffect` 프로퍼티 완전 소실 (게임로직, UI 아님)
- AS-IS: `public ICardEffect HandBounceEffect { get; set; } = null;` / `public ICardEffect LibraryBounceEffect { get; set; } = null;` (Permanent.cs:3678, 3682). `CardController.cs`에서 바운스 발생 시 기록, BT22_007/BT5_110/EX10_052/EX10_055/BT11_033/P_214/BT23_043/EX11_031/ST22_10/BT4_102/P_024/BT11_072 등 최소 12개 카드효과 파일에서 "이 퍼머넌트의 제거가 특정 효과에 의한 바운스였는가"(`bouncePermanent.HandBounceEffect == activateClass`) 인과관계 판정에 사용 — 순수 게임로직, `Show/HideHandBounceEffect`(UI 연출)와는 별개 심볼.
- TO-BE: `.HandBounceEffect`/`.LibraryBounceEffect` 프로퍼티 접근 패턴이 전체 트리에 0건(주석에서 `HideHandBounceEffect` 등 UI 메서드명의 부분 문자열로만 우연히 매칭됨 — 실제 프로퍼티 이식 아님). `PlayingEffect`/`DigivolvingEffect`/`PlaceOtherPermanentEffect`(자매 필드들, Permanent.cs:4372-4398 부근)는 정상 이식되었는데 이 둘만 누락.
- 판단: 실제 결함. 소비 카드 12+개가 전부 스텁 상태라 당장 미발현이나, 자매 필드들과의 비일관성(선택적 부분 이식)이 뚜렷함.

### P4. `OldIsSuspended` 필드 소실 — UI 전용으로 판단, 근거 확인
- AS-IS: `public bool OldIsSuspended = false;` (Permanent.cs:1955). 실사용처는 `FieldPermanentCard.cs:390-422`(UI, 애니메이션 트리거 판정) 뿐 — `BT14_054.cs`의 동명 로컬 변수는 필드가 아닌 지역 섀도잉이라 무관.
- 판단: FieldPermanentCard.cs는 Unity UI 컴포넌트이며 TO-BE에 이식 대상이 아님(확인: TO-BE 트리에 "OldIsSuspended" 0건, 게임로직 소비자 0). 정상 — UI 전용 소실로 간주.

### P5. `battle` 프로퍼티(`IBattle battle`) 소실 — 재배치, 근거 확인됨(정상)
- AS-IS: `public IBattle battle { get; set; } = null;` (Permanent.cs:3182), `CardController.cs`가 전투 중 대입/해제, `ST2_01.cs:23-25`가 `card.PermanentOfThisCard().battle.enemyPermanent(...)`로 실사용.
- TO-BE: Permanent.cs에 없음. 그러나 `ST2_01.cs`(TO-BE) 주석에 "Battle-pairing restored (G10-006): ... exactly as the original card.PermanentOfThisCard().battle.enemyPermanent(...)"라 명시하고 `CardEffectCommons.CurrentBattleOpponent(card)`(AttackController.Current 기반)로 대체 구현되어 있음 실확인.
- 판단: 정상 — 문서화된 재배치, AS-IS 대응 근거 실소스로 확인됨.

### P6. `CannotReturnToLibrary(ICardEffect)` 애그리게이터 메서드 소실 — 재배치, 근거 확인됨(정상이나 일관성 문제 병기)
- AS-IS: `public bool CannotReturnToLibrary(ICardEffect cardEffect)` (Permanent.cs:785-822) — 필드 퍼머넌트/플레이어의 `ICannotReturnToLibraryEffect`를 스캔하는 애그리게이터. BT20_085/BT8_112/BT8_099/LM_006/BT11_072/BT12_031/P_024/BT10_086/BT16_095/BT4_062 등에서 `permanent.CannotReturnToLibrary(cardEffect)` 형태로 호출.
- TO-BE: Permanent.cs에 없음. `CardEffectCommons/NewModelContinuousScan.cs:1588-1613`에 `HasCannotReturnToLibrary`로 이식되어 있고 주석이 "AS-IS Permanent.CannotReturnToLibrary(ICardEffect) (Permanent.cs:785-822)"를 명시 — 근거 확인.
- 판단: 기능적으로는 정상(재배치+근거 확인)이나, **구조가 거의 동일한 자매 메서드 `CannotReturnToHand`는 Permanent.cs 안에 그대로 남아 있는데 `CannotReturnToLibrary`만 NewModelContinuousScan.cs로 이동** — 같은 파일 내 두 쌍둥이 메서드의 이식 위치가 불일치. 버그는 아니나 구조적 비일관성으로 기록.

### P7. `AddBoost`/`RemoveBoost` 인스턴스 메서드 소실 — 재배치, 근거 확인됨(정상)
- AS-IS: `Permanent.AddBoost(DPBoost)` / `RemoveBoost(string)` (Permanent.cs:674-686).
- TO-BE: `Headless/Runtime/DpBoostHelpers.cs`에 정적 메서드로 재배치, 문서 주석이 AS-IS 줄 번호(672-699) 명시. 호출부 `EX10_010.cs` 주석에 "미러 Permanent엔 인스턴스 AddBoost/RemoveBoost가 없고 Boosts는 읽기-뷰"라 명시하고 실제로 `DpBoostHelpers.AddBoost(...)`로 변환된 호출 확인. `Permanent.Boosts` 게터도 같은 메타데이터 키를 읽어 정합.
- 판단: 정상 — 문서화된 재배치, 호출부 변환까지 확인됨.

### P8. `Levels_ForJogress(CardSource)` 소실 — 재배치, 근거 확인됨(정상)
- AS-IS: `Permanent.Levels_ForJogress(CardSource)` (Permanent.cs:3554-3607) — Jogress/DNA진화 대체소재 판정에 널리 쓰임(AD1_011 외 50+ 카드효과 파일).
- TO-BE: `CardSource.cs`에 `JogressLevelsAgainst(CardSource)`로 재배치, 주석이 "1:1 of AS-IS Permanent.Levels_ForJogress(CardSource) (Permanent.cs:3554-3605)"를 명시하며 "mirror keeps this accessor on CardSource (its established consumer surface)"로 재배치 근거 서술.
- 판단: 정상 — 문서화된 재배치.

### P9. `Names_ForDNA(CardSource)` / `IsAddedAsSourceByAppFusion` 소실 — AS-IS 자체 사문(dead code), 저위험
- AS-IS 전체 저장소(빌드 산출물 제외) grep 결과 두 심볼 모두 **Permanent.cs 자체 정의 외 외부 참조 0건** — AS-IS에서도 실제로 발화하지 않는 죽은 표면.
- TO-BE: 둘 다 없음, 재배치 흔적도 없음(단순 누락).
- 판단: 게임 동작에 영향 없음(AS-IS에서도 미발화). 다만 프로젝트의 "1:1 미러" 원칙(메모리: mirror-into-asis-file-not-invented)을 엄격 적용하면 죽은 필드라도 구조적으로는 이식 대상 — 낮은 심각도의 구조적 누락으로 기록.

### P10. `HasDP` / `Level`에서 `TopCard == null` 계열 가드가 조용히 제거됨 — 구조적 이유는 타당하나 `Level`의 sentinel 처리 로직 자체가 빠짐
- AS-IS `HasDP`: `if (TopCard == null) return false;`를 `IsDigimon` 체크보다 먼저 수행(Permanent.cs:150-153). TO-BE `HasDP`(Permanent.cs:142-180)는 이 널가드 없이 바로 `if (!IsDigimon) return false;`로 시작 — TO-BE `TopCard`가 `new(_context, InstanceId, OwnerId)`로 항상 non-null 객체이므로 구조적으로 무해(AS-IS의 "카드 0장 퍼머넌트" 케이스가 이 아키텍처엔 대응 개념이 없음). 이 패턴은 파일 전역에 반복되며 일관되게 제거되어 있어 개별 버그라기보다 아키텍처 전환의 systemic 결과로 판단.
- 단, **`Level`은 단순 널가드 제거를 넘어선 로직 차이가 있음**: AS-IS `Level`(Permanent.cs:48-102)은 `Level = TopCard.Level; if (!TopCard.HasLevel) Level = 1145140;`로, **HasLevel이 false면 TopCard.Level이 무엇을 계산했든 무조건 sentinel(1145140)로 덮어쓴 뒤** PermanentLevel 변경효과 스캔을 진행한다. TO-BE `Level`(Permanent.cs:565-594)은 `int level = TopCard.Level;`만 하고 이 덮어쓰기 분기가 없음 — `CardSource.Level`이 `PrintedLevel`(HasLevel=false인 카드는 음수로 문서화됨, -1 sentinel)을 시드로 `IChangeCardLevelEffect`를 접어 계산하므로, **레벨 없는 카드에 "레벨을 부여하는" 카드효과가 존재하면 AS-IS는 그 폴드 결과를 버리고 sentinel로 강제 회귀시키는 반면 TO-BE는 그 폴드 결과를 그대로 채택**할 수 있음 — 로직 자체가 다름.
- 판단: 발견. 이 프로젝트의 문서화 관례(`CardSource.Level`)가 "모든 소비자는 Level 값을 쓰기 전에 HasLevel로 먼저 게이트한다"는 불변식을 전제로 sentinel 값 자체(-1 vs 1145140)는 비교되지 않는다고 명시하고 있어 실피해 가능성은 낮아 보이나, AS-IS 원문의 명시적 override 분기 자체가 TO-BE Permanent.Level에 재현되어 있지 않다는 사실은 실제 코드 대조로 확인됨 — 잠재적 fidelity 갭으로 기록.

### P11. `cardSources` 계산 프로퍼티의 순서 재구성 — 근거 주석의 사실관계 오류
- TO-BE `cardSources`(Permanent.cs:1911-1934) 게터는 `[TopCard] + reverse(DigivolutionCards) + LinkedCards`로 항상 "링크 카드는 맨 뒤"로 재구성한다. 이 게터의 주석은 "then the linked cards (AS-IS AddLinkCard appends)"라 서술하지만, **AS-IS `AddLinkCard`는 실제로 `this.cardSources.Insert(1, addedLinkCard);`(Permanent.cs:1266)로 top 바로 아래(index 1)에 삽입**하지 append가 아님. `AddDigivolutionCardsTop`도 동일하게 index 1에 삽입한다(:1090). 즉 AS-IS의 실제 `cardSources` 필드는 진화원-top-삽입과 링크-top-삽입이 섞인 이력에 따라 상호 교차 배치될 수 있는 리스트인데, TO-BE는 이를 "진화원 먼저, 링크는 항상 마지막"이라는 고정 순서로 재구성한다.
- 판단: 발견(사실관계). `EffectList_ForCard`처럼 `cardSources`를 순회하되 멤버십(존재 여부)만 따지는 소비자에게는 무해할 가능성이 높지만, 순서 자체가 의미를 갖는 소비자가 있다면(미검증) 실제 순서 불일치가 될 수 있음. 최소한 근거 주석 "AS-IS AddLinkCard appends"는 AS-IS 원문과 불일치하는 서술이므로 문서 정확성 문제로도 기록.

---

## 정상으로 확인한 대규모 재구성(문서화·근거 확인됨, 문제 아님)

- UI 전용 표면 전량 스트립: `ShowingPermanentCard`(FieldPermanentCard 참조), `ShowUnsuspendEffect`/`ShowDeckBounceEffect`/`HideDeckBounceEffect`/`ShowHandBounceEffect`/`HideHandBounceEffect`/`ShowDeleteEffect`/`HideDeleteEffect`/`ShowWillRemoveFieldEffect`/`HideWillRemoveFieldEffect`/`ShowWillEvolutionEffect`/`HideWillEvolutionEffect`(Permanent.cs:3994-4186) — 전부 `transform.gameObject.SetActive(...)` 류 순수 Unity 연출로 AS-IS 원문 확인, TO-BE의 `MatchStateMutationSink.cs`에 "AS-IS ShowDeleteEffect / ... = UI (stripped)" 명시적 스트립 주석 존재.
- `IEnumerator`→`async Task` + `Hashtable`→구조화 파라미터 전환(DiscardEvoRoots, AddDigivolutionCardsTop/Bottom, AddLinkCard, RemoveCardSource, RemoveLinkedCard) — 각 분기(재루팅, 토큰 가드, ACE-Overflow, WhenLinked/OnAddDigivolutionCards 창) AS-IS 줄 대응 주석과 함께 확인, 표본 대조 결과 충실.
- `LinkedMax`→`LinkHelpers.ResolveLinkedMax`→`NewModelContinuousScan.FoldLinkedMax`: AS-IS의 UpToConstant/UpDownValue/DownToConstant 3그룹 폴드, Players_ForTurnPlayer 스캔, 페이스업 시큐리티 스캔, CanNotBeAffected 게이트까지 구조 일치 확인.
- DP/BaseDP/GetDP 폴드: IsMinusDP/ImmuneFromDPMinus/CanNotBeAffected/isUpDown-NotIsUpDown 분리/LinkedDP 삽입 지점/DPBoosts/0-클램프까지 AS-IS와 순서 일치 확인. "페이스업 시큐리티" 판정만 `cardSource.IsFlipped`→`SecurityFaceState.IsFaceUpInSecurity(...)`로 치환되어 있으나 "established" 패턴으로 문서화(다른 폴드들과 공유).
- `IsPlayedOptionPermanent`(필드→읽기전용 프로퍼티, `GameFlowProcessor.IsPlayedOptionPermanentKey` 메타데이터), `IsDestroyedByBattle`, `willBeRemoveField`, `DestroyingEffect`, `DPJustBeforeRemoveField` 등 "JustBefore/JustAfter" 계열 전부 메타데이터 스토어로 치환되어 있고, 각 쓰기 측(`GameFlowProcessor.cs`, `CardEffectCommons.cs`, `CardLeavePlayCleanup`)이 실존함을 개별 확인.
- `IsSuspended`(필드→프로퍼티, `"isSuspended"` 메타데이터), `oldIsTapped_playCard`(필드→퍼-매치 스토어) 등도 동일 패턴으로 정상.

## 결론
Permanent.cs 포팅은 규모(4187→4640줄)에 비례하는 실질적 재작성이지만, 표본 대조한 핵심 게임로직(DP/Level/Link/키워드 판정/Add·Remove 계열)은 AS-IS 대응 근거가 실코드로 확인되는 고충실도 이식임. 다만 **P1(DPWhenSuspended)·P2(DigivolutionOrLinkCards)·P3(HandBounceEffect/LibraryBounceEffect)** 는 게임로직에 실사용되는 AS-IS 심볼임에도 TO-BE Permanent.cs 및 전체 트리 어디에도 대응 멤버나 재배치 근거가 없는 순수 누락으로 판정. **P10(Level sentinel 로직)·P11(cardSources 순서 근거 주석 오류)**은 재구성 과정에서 세부 로직/문서가 AS-IS 원문과 어긋나는 지점으로 판정. 나머지(P4~P9)는 재배치 근거를 실소스로 확인해 정상으로 분류.
