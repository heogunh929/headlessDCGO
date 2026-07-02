# PRIM-W6 선행개발 설계 — 잔여 프리미티브 전수 소진 (설계)

- 작성: 2026-07-02. 입력: AS-IS 전 표면 diff + 카드 실사용 빈도 census (아래). 목표: 로컬모델 per-card 포팅에서 STOP/의도-번역 마찰을 구조적으로 제거.
- 원칙: [[fidelity-over-coverage]] — 동명 미러는 **원본 시맨틱 1:1**(뭉개기 금지), 표현 불가만 STOP 유지.
- **진행 (2026-07-02)**: parity 게이트 신설(`scripts/audit-commons-parity.py`, 베이스라인 240) → **W6-P 1차 완료**(술어/카운트 ~30종, G9-066; 기존 owner-only 축소판 2종을 충실판으로 대체; `CanActivateSuspendCostEffect`는 시맨틱 검증 후 후속) → **W6-G 키워드형 완료**(Gain 16종, G9-067; 제약형 Gain은 게이트 소비 확인 후 후속) → **W6-T 1차 완료**(이벤트 메타 enrichment `event.<key>` + sink 삭제원 스탬프 + 게이트 미러 15종, G9-068). parity 240→212→201→186 → **W6-T 2차**(CanActivateOnDeletion 311·CanTriggerWhenLoseSecurity 76) → **W6-S 완료**(Delete형 `DeletionOutcomeWatcher` — Evade 창-일시정지 continuation 증명 + 형제 7종: Suspend/Bounce/DeckBounce/TrashDigivolutionCards(+FromTopOrBottom 121)/TrashLinkCards/TrashSecurity/TrashHand/PlaceSecurity, G9-069 8/8) → **W6-D 완료**(`PlaceDelayOptionCards` — P7 면제 키 재사용, 진입-턴 게이트 검증) → parity 173 → **W6-L 완료**(`LinkCondition`/`AddLinkConditionClass` 1:1 + `LinkConditionOf` 뷰 + `AddSelfLinkConditionStaticEffect` 팩토리 + LinkSelfEffect 호스트필터/비용 개편, G9-070 2/2; 링크 회귀 clean) → parity 172 → **W6-F 완료**(`AppFusionCondition`/`AddAppFusionConditionClass`/`AddAppfuseMethodByName·ByCondition` + DigivolveAction 앱퓨전 라이더 — 링크재료 선편입→통상 진화 스태킹, i≠j 재료 규칙, G9-071) → **W6-A2 완료**(`ArtsDigivolveEffect` — 실행존에서 무료 진화, 통상 요건 게이트 재사용) → **W6-X 완료**(`AddDetailClass` 표시-전용 inert 미러; 타이밍-래퍼 5종(각 1장)은 레시피 매핑으로 종결 — 코드 불요) → parity **170**, **카드-facing 팩토리 갭 = 래퍼 5종(각 1장)뿐**. → **`CanActivateSuspendCostEffect` verbatim 확인 후 미러**(+`ContinuousRestrictionGate.EvaluateSuspend` 신설) → **fidelity 자체감사 교정 3건**(앱퓨전·Arts의 CannotDigivolve 게이트 누락, Gain류 면역 라이브 재평가) → parity **169**. → **W6-G 제약형 완료**(쌍-술어 게이트 일반화 `SoftenByCounterpart` — Block/BeAttacked/BeBlocked에 FR-P3 패턴 확장 + 호출부 쌍-평가 배선; GainCanNotAttack/Block/BeAttacked/BeBlocked/Suspend/Unsuspend + Until 래퍼 3종, G9-072 5/5) → **W6-T 롱테일 1차**(방출 보강: BeforePayCost isEvolution/targetCardId·WhenLinked linkCardId·OnAddDigivolutionCards addedCardIds; 게이트 WouldPlay/WouldDigivolve/WhenLinked/OnAddDigivolutionCard/OnMove/IsByBattle + 액세서 4종 + Tamer 3종/Breeding/Security 술어, G9-068 12/12) → parity **146**. → **프로세스 커먼즈 완료**(verbatim 덤프 기반 10종: ChangeDigimonDP/SAttack·ChangeDigimonDPPlayerEffect·AddThisCardToHand·PlayPermanentCards(977)·AddEffectToPermanent/Player·DigivolveIntoHandOrTrashCard(342 — **레시피 오매핑 발견·수정**: 이전 표가 디제너로 반대 매핑)·SelectTrashDigivolutionCards(+TrashSpecificSourcesAsync 헬퍼)·DNADigivolvePermanentsIntoHandOrTrashCard; 공용 sink에 memory 배선 — 비용 지불 실동작, G9-073 7/7) → parity **142**. 잔여 = 참조 <25의 순수 꼬리(게이트/술어/소형 프로세스 — 전부 기확립 패턴 1:1 반복).

## Census 결과 (2026-07-02)

**카드-facing 팩토리(CardEffectFactory)**: AS-IS 129종 중 포트 미러 없음 55종 → 카드 직접 호출 기준 실질:
| 팩토리 | 카드파일 | 분류 |
|---|---|---|
| `AddSelfLinkConditionStaticEffect` | 70 | W6-L |
| `AddAppfuseMethodByName`(+ByCondition) | 26 | W6-F |
| `ArtsDigivolveEffect` | 6 | W6-A2 |
| `AddDetailClass` | 5 | W6-X (표시 전용 확인 후 no-op) |
| 타이밍 래퍼 5종·`ActivateClassesForSharedEffects` | 각 1 | W6-X |
| 나머지 44종 | 0 | 커먼즈 내부용 — 카드-facing 아님(대상 제외) |

**커먼즈(CardEffectCommons)**: 카드가 부르는 259종 중 동명 미존재 244종 — 시그니처 분류:
| 부류 | 종수 | 총 참조(카드파일) | 처리 |
|---|---|---|---|
| **술어** (bool, 카드/퍼머넌트 상태) | 47 | **8,772** | W6-P: 전량 동명 1줄 위임 미러 |
| **트리거 게이트** (bool, Hashtable) | 72 | **6,714** | W6-T: `CardEffectResolveContext` 기반 동명 미러(상위 우선) |
| **코루틴 프로세스** | 88 | 3,991 | 3분: 기능存(이름매핑, 레시피 커버) / Gain류 일괄(W6-G) / 신규(W6-S·W6-D 등) |
| **기타** (Hashtable 액세서·카운트·grant 인프라) | 36 | 1,262 | 카운트→W6-P, 액세서→W6-T, AddEffectTo*→W6-G 일반화 |

상위 신규 프로세스: `DeletePeremanentAndProcessAccordingToResult`(**322**) `PlaceDelayOptionCards`(**182**) `TrashDigivolutionCardsFromTopOrBottom`(121) `SelectTrashDigivolutionCards`(67) `DNADigivolvePermanentsIntoHandOrTrashCard`(55) `PlayOptionCards`(43).

---

## W6-P — 술어/카운트 커먼즈 일괄 미러 (기계적, 최대 마찰 절감)

**대상**: predicate 47종(8,772 참조) + `MatchCondition*Count` 4종 + `HasNoElement`. 전부 `bool`/`int` 반환, 포트 뷰(CardSource/Permanent/게이트)로 1줄 위임 가능.

**설계**:
- `CardEffectCommons`에 동명·동일-파라미터 순서로 추가. 대표 매핑:
  - `IsExistOnBattleAreaDigimon(card)` → `IsExistOnBattleArea(card) && card.PermanentOfThisCard()?.TopCard.IsDigimon`(원본: 배틀에리어 + 디지몬 판정 — AS-IS 본문 확인 후 1:1; TreatAsDigimon 포함 여부 원본 그대로).
  - `HasMatchConditionOwnersCardInTrash(card, cond)` → owner 트래시 스캔 `.Any(cond(view))`.
  - `HasMatchConditionOwnersPermanent/Hand`, `IsPermanentExistsOn(Owner|Opponent)BattleArea(Digimon)`, `IsOwner/OpponentPermanent`, `IsOwner/OpponentEffect`, `IsExistOnTrash/Field/BreedingArea/InSecurity` — 존/소유 스캔 1줄.
  - `CanPlayAsNewPermanent(cardSource, payCost, effect)` → PlayCardAction 검증 로직 위임(payCost=false면 비용 게이트 생략) — **본문 확인 필수**(등록금지/특수조건 포함 여부).
  - `IsMinDP/IsMinLevel/IsMaxDP류` → 기존 RaidAttackSwitch MaxDp 패턴 일반화.
  - `CanUnsuspend(permanent)` → RestrictionHelpers.CannotUnsuspend 게이트 부정.
  - `CanActivateSuspendCostEffect` → EX8_074 게이트 재사용.
  - `CanDeclareOptionDelayEffect` → **W6-D 의존**(딜레이 시스템 후).
- **검증 방식**: 각 미러는 AS-IS 본문을 열어 1:1 확인 후 작성(추측 금지). 47종을 5~6묶음으로 나눠 묶음별 테스트(경계: 소유/존/빈 상태) + 기존 스위트 회귀.
- **자동화**: `scripts/audit-commons-parity.py` 신설 — 카드가 부르는 커먼즈 이름 vs 포트 표면 diff를 상시 리포트(카탈로그 재생성 스크립트 형제; 웨이브 완료 판정 게이트).

## W6-T — 트리거 게이트 동명 미러

**대상**: hashgate 72종(6,714 참조) + Hashtable 액세서(GetPermanentFromHashtable 등).

**설계**:
- 포트 트리거 팩토리는 `Func<CardEffectResolveContext,bool> triggerGate`를 이미 받음 → AS-IS `CanTriggerX(hashtable, ...)`를 **`CanTriggerX(CardEffectResolveContext ctx, ...)` 동명 오버로드**로 미러. 로컬 모델 번역: `CanActivateCondition(Hashtable h)` 본문 → `triggerGate: ctx => CardEffectCommons.CanTriggerOnPlay(ctx, card, rootCond) && ...` (이름 보존).
- 필요한 컨텍스트 노출 필드 맵(구현 전 포트 트리거 값 실측 필요): subject(TriggerEntityId) · root(플레이 출처 존) · byEffect/byBattle · winner/loser(전투) · isJogress/assemblyCount(플레이 파라미터) · suspend 주체 등. **미노출 값은 이 웨이브에서 트리거 방출부에 값 추가**(HashtableSetting.cs 대응 표 작성).
- 우선순위: 상위 20종(참조 ≥40) 먼저 — CanTriggerWhenDigivolving/OnPlay/OnAttack/OptionMainEffect/OnDeletion/CanActivateOnDeletion/SecurityEffect/OnPermanentPlay/IsByEffect/WhenRemoveField/OnPermanentAttack/WhenPermanentSuspends 등. 나머지 52종은 동일 패턴 후속.
- 액세서: `GetPermanent(s)FromHashtable(ctx)` → subject/targets 뷰 반환; `CardEffectHashtable(effect)`(489)는 원본이 "효과→해시 생성"이므로 포트에선 컨텍스트 생성 스텁(대개 번역 시 소멸 — 레시피 명기).

## W6-G — Gain류/부여 인프라 일괄 (AD1-G 패턴 복제)

**대상**: `GainBlocker`(87) `GainCanNotAttack`(56) `GainRush`(44) `GainPierce`(44) `GainCantUnsuspendUntilOpponentTurnEnd`(33) `GainCanNotUnsuspend`(25) `GainRetaliation`(23) + Gain* 잔여(~15종) / 일반화: `AddEffectToPermanent`(30) `AddEffectToPlayer`(40).

**설계**: AD1-G(`GainCanNotBeDeletedByBattle`)에서 확립한 패턴 — 대상-잠금·duration-태그 바인딩 + 라이브 필드가드 + 부여시 면역거부 — 를 키워드/제약별로 복제. 키워드형(Blocker/Rush/Pierce/Retaliation)은 키워드 바인딩 + duration; 제약형(CanNotAttack/CanNotUnsuspend)은 restriction 키 + defenderCondition류 술어 저장. `AddEffectToPermanent/Player`는 임의 ICardEffect의 duration 등록 일반화(`ToBinding(id, duration)` 관례 — AD1-S에서 확립). 각 5~8줄 + 묶음 테스트.

## W6-S — successProcess 계열 (322 + 형제 8종) — probe 완료

**AS-IS** (`CardEffectCommons.cs:463-483` + 형제 `:437-644`): `DeletePeremanentAndProcessAccordingToResult(targets, activateClass, successProcess: Func<List<Permanent>,IEnumerator>, failureProcess)` — `DestroyPermanentsClass.Destroy()` **풀 파이프라인**(CanNotBeAffected/CanBeDestroyedBySkill 필터 → would-be-deleted 컷인 응답 가능 → 생존자 재확정 → OnDestroyedAnyone/OnLeaveField → 트래시) 후, **실제로 필드를 떠난 대상이 1+**(`DestroyedPermanents` 멤버십)면 success(destroyed 목록 전달), 아니면 failure. **형제 8종 동형**: Suspend(:437)/Bounce(:489)/DeckBounce(:515)/TrashDigivolutionCards(:541)/TrashLinkCards(:567)/TrashSecurity(:593)/TrashHand(:619)/PlacePermanentInSecurity(:644) — "I-클래스 액션 실행 → 실발생 여부로 분기".

**포트 설계**:
1. **Delete형(본체)**: sink Delete는 PRE 치환 존재 시 `pendingDeletion` defer → 창이 에이전트 초이스로 나중에 해소되므로, continuation이 일시정지를 건너 보존돼야 함 → **`DeletionOutcomeWatcher` 컨텍스트 서비스**(P6 `sacrificeAwaiting`의 일반화; RevealFlowState 선례): `Watch(targets, onSettled(destroyed, spared))` 등록 → GameFlowProcessor 루프의 Settle 단계(P6 옆)에서 전 대상 확정 시(트래시 or 생존/치환완료) 콜백 실행. 커먼즈 시그니처 동명: `DeletePeremanentAndProcessAccordingToResult(EngineContext, List<Permanent>, CardSource source, Func<IReadOnlyList<Permanent>,Task>? successProcess, Func<Task>? failureProcess)` — 원본 철자(Peremanent) 유지.
2. **비-Delete 형제 8종**: 포트에서 해당 액션들(suspend/bounce/deck-bounce/소스·링크·시큐리티·핸드 트래시/시큐리티 배치)은 치환 창이 없어 **동기 확정** — 단순 async 커먼즈로 실행→결과분기(각 5~10줄; CanAddSecurity 게이트는 PlaceSecurity형에서 K2 라틴트 폴딩 지점과 접합).
3. 성공 판정은 "시도"가 아니라 "실발생"(1:1) — 면역/prevent로 전멸 시 failure.
4. 테스트: Delete형 — 치환 없는 대상 즉시 success / Evade 활성화로 생존 시 failure / 혼합(2대상 중 1생존) 시 success+destroyed=1 / 창 일시정지 후 재개까지 continuation 보존. 형제 — 대표 2종(suspend·bounce) 성공/실패.

## W6-D — [Delay] 옵션 (182) — probe 완료: 신규 서브시스템 아님

**AS-IS 확정** (probe): [Delay]는 별도 스택/존이 없다.
- `PlaceDelayOptionCards`(`CardEffectCommons.cs:113-134`): 옵션을 **비용 없이 일반 퍼머넌트로 배틀에리어에 플레이**(face-up·untapped·ETB active, `CanPlayAsNewPermanent(isPlayOption:true)` 게이트) 후 `Permanent.IsPlayedOptionPermanent = true` — 이 태그가 "무DP 옵션 트래시" 룰 면제(`AutoProcessing.cs:182`)의 전부. **포트는 P7에서 이미 이 면제 키(`IsPlayedOptionPermanentKey`)를 모델링함.**
- 발동은 평범한 **OnDeclaration 활성 스킬**(메인페이즈 커맨드) — 게이트 `CanDeclareOptionDelayEffect(card)` = 배틀에리어 존재 && **진입 턴 아님**(`EnterFieldTurnCount != TurnCount` — 포트 `enteredThisTurn` 메타 1줄). 해소 첫 단계가 자기 삭제(W6-S 본체 사용) → 성공 시 payoff.
- `PlaceSelfDelayOptionSecurityEffect`([Security] 배치형)는 이미 포트에 존재; `Gain2MemoryOptionDelayEffect`(공용 payoff)도 존재.

**포트 설계** (소형으로 축소):
1. `CardEffectCommons.PlaceDelayOptionCards(card, effect, root)` — cost-free play-to-battle(기존 PlayThisCardToBattle 뮤테이션 재사용) + 인스턴스 메타 `IsPlayedOptionPermanentKey=true` + ETB 트리거. `CanPlayAsNewPermanent(isPlayOption:true)` 게이트(W6-P의 그 술어).
2. `CanDeclareOptionDelayEffect(card)` 술어(W6-P 묶음에 포함) — `IsExistOnBattleArea && !enteredThisTurn`.
3. OnDeclaration 활성 열거는 기존 활성 시스템 그대로(신규 없음) — 배틀에리어의 옵션 퍼머넌트도 스킬 열거에 포함되는지 1건 검증.
4. 의존: W6-S(자기삭제 successProcess). 테스트: 배치→면제 확인(무DP 스윕 생존), 진입 턴 발동 불가/다음 턴 가능, 상대가 삭제 가능(일반 퍼머넌트).

## W6-L — 링크 조건 선언 (70) — probe 완료

**AS-IS** (`CardEffectFactory/AddLinkRequirement.cs`, `CardSource.cs:2727/3140/3267/3337/4286`, `CardController.cs:3456-3492`):
- `AddSelfLinkConditionStaticEffect(permanentCondition, linkCost, card, condition?, cardCondition?, effectName?)` — timing None 정적 선언. 실체는 `LinkCondition { digimonCondition(Func<Permanent,bool>), cost(int) }` — "이 카드는 `digimonCondition`을 만족하는 **자기** 배틀에리어 디지몬에 비용 `cost`로 링크 가능". **LinkDP는 여기 없음** — 카드 데이터의 정적 필드(`CEntity_Base.LinkDP`), AddLinkCard 시 `LinkedDP += LinkDP` → 호스트 DP 합산.
- 소비: `CardSource.linkCondition`(EffectList(None) 스캔) → `CanLink`/`CanLinkToTargetPermanent`(호스트 필터: 자기 배틀에리어·비토큰·digimonCondition·비용 지불가능) → 실행은 **별도** `LinkEffect(card)`(OnDeclaration) → `ILinkCard.LinkCard()`: WhenWouldLink 창 → `GetChangedLinkCost`(수정자 폴딩, floor 0) 지불 → `AddLinkCard`.
- 비용 수정자 4종(`Reduce/Change/GrantedReduce/GrantedChangeLinkCostClass`)은 전부 `IChangeLinkCostEffect` → `GetChangedLinkCost`가 필드 전체+자기에서 수집(비-UpDown 먼저), 9장 사용.
- `AddLinkConditionStaticEffect`(non-self)는 카드 직접 호출 0 — self가 유일 진입.

**포트 현황**: 링크 실행부는 존재(`LinkSelfEffect` 액션 + `LinkHelpers.AddLinkCardAsync/ResolveLinkCost`(G9-056 linkCostDelta metric) + `LinkedDpKey`). **빠진 것 = 선언(조건·기본비용)과 그 소비.**

**설계**:
1. `AddSelfLinkConditionStaticEffect` 동명 팩토리: `LinkCondition` 1:1 클래스(digimonCondition·cost) + timing-None 바인딩(값 `link.getCondition` — AddAssemblyConditionClass 패턴 복제). `CardSource.linkCondition` 뷰(dispatch 우선 + 레지스트리 폴백 — AssemblyConditionOf 패턴).
2. `LinkSelfEffect` 소비 개편: 호스트 후보 = `linkCondition.digimonCondition` 평가(현재 하드코딩/무조건이면 교체), 비용 = `linkCondition.cost` 기반 `LinkHelpers.ResolveLinkCost`(수정자 폴딩 — cardSourceCondition/permanentCondition/rootCondition 술어를 metric 평가에 전달, 9장 케이스).
3. LinkDP: 정의 메타 `linkDP` → `AddLinkCardAsync`가 `LinkedDpKey` 누적(기존) — 로더/카탈로그에 명기.
4. `GrantedReduceLinkCostClass` 등 4종 동명 팩토리(기존 linkCostDelta 바인딩으로 lower, 술어 저장).
5. 테스트: 조건 만족 호스트만 링크 후보, 비용 지불(수정자 -1 포함), WhenWouldLink 창은 트리거 census 후(현행 카드 사용 확인) 결정, LinkDP 합산.

## W6-F — 앱퓨전 (26) — probe 완료

**AS-IS** (`CardEffectFactory/AddAppfusionMethod.cs`, `CardSource.cs:4298`, `CardController.cs:251/276/400/786`):
- `AddAppfuseMethodByName(cardNames, card, cost=0)` → ByCondition → `AppFusionCondition { digimonCondition, linkedCondition, cost }`: "탑카드가 재료 i와 일치하고 **링크 카드** 중 하나가 **다른** 재료 j(i≠j)와 일치하는 자기 디지몬 위로 앱퓨전 가능".
- 실행 = **진화**(isEvolution=true): 선택된 링크 카드를 진화원(sources)에 편입 + 융합 카드를 호스트 탑에 배치, 비용은 일반 플레이 비용 파이프라인(`GetPayingCostWithBaseCost`), 전용 존 없음.

**설계**: DigivolveAction 변형(진화 계열이므로 SpecialPlayAction fusion이 아님):
1. `AddAppfuseMethodByName/ByCondition` 동명 팩토리 → `AppFusionCondition` 1:1 + timing-None 바인딩(`appfusion.getCondition`).
2. DigivolveAction(or 전용 소형 액션 `AppFusionAction`)에 열거 추가: 손패 카드에 appFusionCondition 존재 && 호스트(탑카드=재료i && linkedCardIds 중 재료j, i≠j) && CanNotEvolve 게이트 && 비용 지불가능 → 파라미터(호스트, 링크재료 id). 실행: 링크재료를 linkedIds에서 sourceIds로 재편입 → 통상 진화 배치(WhenDigivolving 트리거·상속 규칙 동일).
3. 테스트: 재료쌍 열거(같은 이름 이중사용 불가 i≠j), 실행 후 링크재료가 진화원, 진화 트리거 발화.

## W6-A2 — Arts 진화 (6) — probe 완료

**AS-IS** (`CardEffectFactory/KeyWordEffects/ArtsDigivolve.cs`): 옵션 해소 중(executing area) `OptionResolutionClass` — 자기 디지몬 중 **통상 진화 요건을 만족하는**(`CanPlayCardTargetFrame`) 대상을 골라 이 카드를 `payCost:false`로 그 위에 진화 배치. = "옵션 존에서 무료 진화".

**설계**: `ArtsDigivolveEffect(card)` 동명 팩토리 → IActivatedCardEffect(옵션 해소 플로우): 후보 = 자기 디지몬 && 통상 진화 요건 통과(DigivolveAction 요건 판정 재사용, payCost 생략) → ChoiceProvider 선택 → 진화 배치 뮤테이션(payCost 없이, Execution 존 출발). 소형.

## W6-X — 잔챙이 — probe 완료

- **`AddDetailClass`** — **표시 전용 확정**(툴팁 텍스트; `triggerEffect` bool은 소비자 0). → 동명 no-op 팩토리(인자 보존·무동작, SetNotShowUI 관례) + 카탈로그 명기.
- **`ActivateClassesForSharedEffects`** — 타이밍 멀티플렉서(1개 본문을 whenMoving/onPlay/whenDigivolving/… 플래그별 타이밍에 반복 등록; **~85장 사용** — 당초 census의 1장은 직접-이름 grep 한계). 본문이 코루틴이라 의도-번역 대상 → **레시피 규칙 추가**: "플래그가 켜진 각 타이밍 분기에 동일 의도 팩토리를 반복 등록". 동명 미러는 만들지 않음(코루틴 인자라 표현 불가) — 로컬모델 STOP 아님을 명시.
- **타이밍 래퍼**(WhenMovingClass 등, 직접 호출 각 1장) — 전부 `ActivateClass(...)` 보일러플레이트 + 프리셋 CanTriggerX. → 레시피 매핑 행("래퍼명 → 해당 타이밍 분기 + W6-T 게이트")으로 처리, 별도 코드 없음.

---

## 실행 순서 제안 (확정)

1. **W6-P** 술어 47+카운트(기계적·최대효과, `audit-commons-parity.py` 동시 신설) → 2. **W6-T** 상위 20 게이트+액세서(+트리거 값 보강 표) → 3. **W6-G** Gain류 일괄 → 4. **W6-S** successProcess(Delete형 watcher + 형제 8) → 5. **W6-D** [Delay] 커먼즈(W6-S 의존) → 6. **W6-L** 링크 조건 → 7. **W6-F** 앱퓨전 → 8. **W6-A2/X** → 9. **W6-T 잔여 52**. 각 묶음 green 게이트(테스트+run-tests+RuleAudit) + parity 리포트 감소 확인 + 카탈로그/레시피 동기화. 완료 판정: parity의 "카드호출-미존재"에 (레시피-매핑 코루틴 + 명시 STOP)만 잔존.
