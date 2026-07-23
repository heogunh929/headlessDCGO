# AS-IS ↔ TO-BE 매칭 검증 — 파트 10/13 (55파일)

- 담당 manifest: `docs/audit/manifest/both_part_10.txt`
- AS-IS: `DCGO/Assets/Scripts/<relpath>`
- TO-BE: `src/HeadlessDCGO.Engine/Assets/Scripts/<relpath>`
- 방식: 양측 전문 실독 + 심볼 전수 대조. 판정은 실소스 관찰 기반(감사판정·주석 근거 불사용). 55파일 전건, 누락 0.
- 리포 상태: main e5ea69d7 (읽기 전용, 무수정)

## 요약

- MATCH: 48
- PROBLEM: 7 — #8, #12, #16, #31, #45, #48, #49

PROBLEM 분류:
- **누락된 게임 로직 스켈레톤 스텁 4건**: #31 Combinations, #45 ShuffleDeckCode, #48 CreateNewDeckButton, #49 DeckCodeUtility — TO-BE가 헤더 주석만 있는 미구현 스텁이며 로직이 TO-BE 트리 어디에도 존재하지 않음(전 트리 grep 0). 순수 결정론 게임 로직으로 substrate 면제 불가.
- **부분 누락 1건**: #12 Progress(commons) — `GainProgress` grant 메서드가 가드/[Obsolete] 없이 전면 부재(caller-less 형제 `ProgressProcess`는 포팅됨 → 비일관 누락).
- **동작 발산 2건**: #8 TrashDigivolutionCards(합법 선택집합 협착), #16 MindLink(`!cs.IsFlipped` 협착 조건 소실).

---

## 전건 판정

### 1. Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/ChangeSAttack.cs
Verdict: MATCH
근거: TO-BE는 브릿지 — 두 `ChangeDigimonSAttack` 오버로드(async Task)가 substrate impl `CardEffectCommons.cs:1667`에 위임, AS-IS 1:1 미러(가드 `targetPermanent`/battle-area/`changeValue==0`, `CanUseCondition`, `ChangeTargetSAttackStaticEffect(...hashstring)`, `AddEffectToPermanent(timing:None)`). `CreateBuffEffect`/`CreateDebuffEffect`는 UI 전용. AS-IS `InverteDigimonSAttack`/`InvertDigimonSAttack` 래퍼(124,176행)는 TO-BE에 없으나 grep으로 AS-IS 호출부 0 확인(정의만 존재)이고 하부 팩토리 `InvertTargetSAttackStaticEffect`는 포팅됨(`CardEffectFactory/ChangeSAttack.cs:194`) — 정당한 dead-code 생략. 경미: null `activateClass`가 AS-IS `yield break` 대신 `ThrowIfNull` throw이나 호출부 항상 non-null.

### 2. Script/StreamingAssetsUtility.cs
Verdict: MATCH (substrate exception)
근거: AS-IS는 100% Unity 클라이언트 에셋 로딩 — `Texture2D`/`Sprite`/`UnityWebRequest`/`WebP`/`Application.streamingAssetsPath`(ReadFile, BinaryToTexture, GetSprite, GetCardImageData, HandleCardImage, GetStreamingAssetPath). 결정론 게임 규칙 부재. TO-BE는 의도적 스켈레톤 스텁, `src/HeadlessDCGO.Engine/` grep 상 `StreamingAssetsUtility` 참조 0 → 소실 로직 없음.

### 3. Script/CardEffectCommons/KeyWordEffects/Raid.cs
Verdict: MATCH
근거: AS-IS 3메서드 전건 포팅. `GainRaid` 1:1(RaidEffect + AddEffectToPermanent `OnAllyAttack`; CreateBuffEffect 제거). `CanActivateRaid`는 attacking-permanent + IsMaxDP(enemy, `!=DefendingPermanent && !IsSuspended`) 미러(가드 folding, 전부 AND, 결과 등가). `RaidProcess`는 IsMaxDP(`!IsSuspended`) 선택→`SwitchDefender`(빈 선택 early-return = AS-IS `maxCount=Min(1,count)` no-op). `Owner.Enemy`→`new Player(...).Enemy` substrate 브릿지만.

### 4. Script/CardEffectFactory/KeyWordEffects/Blitz.cs
Verdict: MATCH
근거: `BlitzSelfEffect`/`BlitzEffect` 바이트-수준 구조 미러 — `EffectDiscription` [When Digivolving]/[On Play] 분기, `rootCardEffect` 재부모, `CanUseCondition`/`CanActivateCondition`/`ActivateCoroutine` 로컬 포함. substrate 번역만: `PermanentOfThisCard()`→`ResolvePermanentOfThisCard`, `Func<IEnumerator>`→`Func<Task>`, fallback Permanent ctor.

### 5. Script/CardEffectFactory/AddDigivolutionRequirement.cs
Verdict: MATCH
근거: `AddSelfDigivolutionRequirementStaticEffect`/`AddDigivolutionRequirementStaticEffect` 양측 존재; `GetEvoCost` 로직 verbatim — ignore-requirement 단락, color 분기, `ignoreLevel` 계산, exact/min/max level 검사, `costEquation ?? digivolutionCost`. substrate 브릿지만: `Owner.CanIgnoreDigivolutionRequirement`→`new Player(...)...`, `TopCard.CardColors.Contains`→`CardSource.ToCardColorList(...).Contains`.

### 6. Script/CardEffectCommons/KeyWordEffects/Pierce.cs
Verdict: MATCH
근거: AS-IS 4메서드 전건 포팅. `GainPierce` 1:1(PierceEffect + AddEffectToPermanent `OnDetermineDoSecurityCheck`; CreateBuffEffect 제거). `CanTriggerPierce` verbatim(winner/loser, `isOnlyWinnerSurvive:true`). `CanActivatePierce`는 4가드 + (battle-area && Enemy.SecurityCards≥1 && AttackingPermanent contains TopCard && `!DoSecurityCheck`) 미러. `PierceProcess`는 `DoSecurityCheck=true`.

### 7. Script/OptionalSkill.cs
Verdict: MATCH (substrate exception)
근거: 게임-로직 스켈레톤이 `SelectOptional`에 1:1 포팅 — decider=`cardEffect.EffectSourceCard.Owner`, 동일 `_Message` 빌드(`EffectTargets(hash)` 타겟팅 변형 + `TopCard.CardNames[0]` join 포함), yes/no 대기(AS-IS `WaitUntil(HasPlayerSelection)`→`ChoiceProvider.ChooseAsync`; select=YES/skip=NO가 `ValueSelection.ValueAsBool` 미러), `cardEffect.SetUseOptional(_useOptional)`. 제거분은 전부 표현/전송 — trash-card 표시, outline/highlight, command panel, `[PunRPC] SetUseOptional`/`QueuePlayerSelection` Photon 왕복, 클라 `IsAI` 0.9-확률 자동응답(AI 결정 이제 ChoiceProvider 외부화). AS-IS 36-58,60-115,121-132행이 UI/전송으로 실인용.

### 8. Script/CardEffectCommons/TrashDigivolutionCards.cs
Verdict: PROBLEM
근거: TO-BE 브릿지가 substrate `SelectTrashDigivolutionCards`(`CardEffectCommons.cs:2045`)에 위임하는데, 합법 선택집합에서 AS-IS와 발산(substrate-강제 아님):
(a) **호스트 필터** — AS-IS `CanSelectPermanentCondition`(18-29행)=battle-area + `permanentCondition`만이라 소스 없는 permanent도 선택가능 호스트; substrate `HostQualifies`(`CardEffectCommons.cs:2068-2077`)는 `SourcesOf(id).Any(SourceQualifies)` 연언 추가로 trash 가능 소스 0인 호스트를 배제. `isFromOnly1Permanent`에서 동작 변화(AS-IS는 단일 호스트 pick을 빈 permanent에 소진해 0 trash 허용, substrate 금지).
(b) **소스-선택 skip** — AS-IS `SelectCardEffect`는 `canNoSelect: () => canNoTrash && NotSelectYet()`(129행)로 호스트 pick 후 0 trash 허용; substrate `sourceRequest`(`CardEffectCommons.cs:2146-2149`)는 `canSkip:false` + `minCount≥1`로 호스트 선택 시 trash 강제.
(c) substrate가 AS-IS에 없는 `usedHosts` dedup 추가.
"전혀 trash 안 함" 결과는 호스트-레벨 skip으로 여전히 도달가능하나 중간 옵션집합이 1:1 아님 → 파일의 "verbatim verified" 주석과 모순.

### 9. Script/CardEffectFactory/CanNotBlock.cs
Verdict: MATCH
근거: `CanNotBlockStaticSelfEffect`/`CanNotBlockStaticEffect` 라인-수준 미러 — `CanUseCondition`(IsExistOnBattleAreaDigimon), `PermanentsCondition = AttackerCondition && DefenderCondition`, attacker `!CanNotBeAffected` 게이트, `defenderCondition` null-coalescing. substrate 적응만: `PermanentOfThisCard()`→`ResolvePermanentOfThisCard`, `CanNotBeAffected(ICardEffect)`→`CanNotBeAffected(InstanceId)`.

### 10. Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/ChangeLinkMax.cs
Verdict: MATCH
근거: 두 `ChangeDigimonLinkMax` 오버로드 AS-IS 1:1 — 5가드, `CanUseCondition`, `ChangeTargetLinkMaxStaticEffect(...hashstring)`, `AddEffectToPermanent(timing:None)`. `CreateBuffEffect`/`CreateDebuffEffect`(및 그것만 구동하던 `isUpValue`/`activateAnimation` 게이팅)는 UI 전용 제거; `activateAnimation`은 시그니처 패리티 유지(`_ = activateAnimation`).

### 11. Script/CardEffectCommons/GiveEffect/GiveEffectToPermanentOrPlayer.cs
Verdict: MATCH
근거: `AddEffectToPermanent`/`AddEffectToPlayer` 양측 존재, 모든 switch 케이스 AS-IS 동일: permanent측 — UntilOpponentTurnEnd/UntilOwnerTurnEnd(`IsOwnerPermanent` owner-swap), UntilEachTurnEnd, UntilEndAttack, UntilNextUntap; player측 — UntilOpponentTurnEnd, UntilOwnerTurnEnd, UntilEachTurnEnd, UntilEndBattle, UntilOwnerActivePhase(`Enemy` redirect), UntilCalculateFixedCost, `getCardEffect ??=` 기본. substrate: `card.Owner`→`new Player(card.Context, card.Owner)`; `ThrowIfNull`/null-empty 가드 추가(호출부 non-null). 헤더 주석의 "AddEffectToPlayer가 CardEffectCommons.cs에 있다"는 stale(실제 이 파일에 존재) — 주석 불일치일 뿐 로직 아님.

### 12. Script/CardEffectCommons/KeyWordEffects/Progress.cs
Verdict: PROBLEM
근거: TO-BE는 `CanActivateProgress`/`ProgressProcess`를 포팅하나 AS-IS 세번째 public 메서드 `GainProgress(Permanent, EffectDuration, ICardEffect)`(AS-IS 10-40행) — `CardEffectFactory.ProgressStaticEffect` 빌드 + `AddEffectToPermanent(...timing: EffectTiming.None)`로 [Progress] 부여 — 가 TO-BE 트리 어디에도 대응 없음(`grep GainProgress` over `src/HeadlessDCGO.Engine` 0)이고 `[Obsolete]`/가드/노트 없음. AS-IS `GainProgress` 자체는 caller-less이나 형제 `ProgressProcess`(역시 caller-less, TO-BE 주석 자인)는 포팅됨 → 비일관 누락. `CanActivateProgress`/`ProgressProcess` 자체는 충실(적응: `PermanentOfThisCard()`→`ResolvePermanentOfThisCard`+`InstanceId` 비교; `IsOpponentEffect(cardEffect,…)`→`IsOpponentEffect(cardEffect.EffectSourceCard,…)`; VFX 제거).

### 13. Script/CardEffectFactory/CanNotDigivolve.cs
Verdict: MATCH
근거: `CanNotDigivolveStaticSelfEffect`/`CanNotDigivolveStaticEffect` AS-IS 1:1(동일 params, 중첩 `CanUseCondition`/`PermanentCondition`/`CardCondition`, `SetIsInheritedEffect` 분기). 실적응은 `card.PermanentOfThisCard()`→`ICardEffect.ResolvePermanentOfThisCard(card)`(42행)만. 헤더 "ADAPTATION (2)" 주석은 stale — 88행 `permanent.TopCard.CanNotBeAffected(canNotEvolveClass)`는 verbatim AS-IS.

### 14. Script/CardEffectCommons/KeyWordEffects/MaterialSave.cs
Verdict: MATCH
근거: `CanActivateMaterialSave`/`MaterialSaveProcess`가 AS-IS 제어흐름 보존(가드 `IsExistOnBattleArea`, `DigivolutionCards.Count(cond) >= 1`, `SelectPermanentEffect`로 Tamer 1 선택, `Math.Min(materialSaveCount, count)` 카드 `SelectCardEffect`, `AddDigivolutionCardsBottom`). 적응은 일관 substrate 번역: `HasMatchConditionPermanent(cond)`→`HasMatchConditionPermanent(card, cond)`, `MatchConditionPermanentCount(...)`에 선행 `card` arg 추가(미러 헬퍼 시그니처와 일치); coroutine→`Task`; `DigivolutionCards`→`.ToList()`; `AddDigivolutionCardsBottom(..., activateClass)`→`(..., activateClass?.EffectSourceCard?.InstanceId)`. AS-IS `ShowCardEffect2`(113행)는 순수 card-reveal VFX 제거. 잉여 AS-IS inner `if(selectedPermanent!=null)`는 동일 효과로 folding.

### 15. Script/CardEffectCommons/CanUseEffects/WhenDeleteOpponentDigimonByBattle.cs
Verdict: MATCH
근거: `CanTriggerWhenDeleteOpponentDigimonByBattle`가 AS-IS↔TO-BE 문자-수준 동일(동일 6-param 시그니처, 동일 `WinnerPermanents`/`LoserPermanents`/`_real` 해시테이블 추출, 동일 `isOnlyWinnerSurvive`/`winnerCondition==null` 단락, 동일 `IsDestroyedByBattle` loser-real 검사). Unity `using`만 제거. 진짜 1:1.

### 16. Script/CardEffectCommons/KeyWordEffects/MindLink.cs
Verdict: PROBLEM
근거: 대체로 충실한 재작성(coroutine `MindLink()`→`BuildRequest()`+`MindLink(selectedDigimonId)`; select-1 optional/`canSkip` 보존; `IPlacePermanentToDigivolutionCards`→`AddSourcesBottomAsync`/`MoveSourcesBottom`). 실발산: AS-IS `CanSelectPermanentCondition`(25행)은 `DigivolutionCards.Count(cs => cs.IsTamer && !cs.IsFlipped) == 0` 카운트, TO-BE(57행)는 `!cs.IsFlipped` 협착을 누락: `DigivolutionCards.Count(cs => cs.IsTamer) == 0`. TO-BE 주석은 "flipped under-cards는 headless 미모델링"으로 정당화하나 `CardSource.IsFlipped`는 실제 live 모델링 플래그(src/.../Script/CardSource.cs:1180, 인스턴스 메타데이터 read)이며 다른 곳(ContinuousFieldMembership.cs, SecurityFaceState.cs)에서 존중됨 → 뒤집힌 Tamer가 digivolution 카드 중 있으면 발산: AS-IS는 해당 Digimon 적격 판정, TO-BE는 부적격.

### 17. Script/NextPhaseButton.cs
Verdict: MATCH (substrate/UI exception)
근거: AS-IS는 `MonoBehaviourPunCallbacks` UI 위젯 — sprites(`MyTurnSprite`/`OpponentTurnSprite`), `Button`/`Outline`/`Cover` GameObject, `Update()` 렌더링, 지역화 버튼 텍스트. 유일 게임로직 `OnClick`: Breeding→`turnStateMachine.SendShouldHatch(false)`, Main→`turnStateMachine.QueueMainPhaseAction(TurnPlayer, new PassAction())`(55/58행). 양자 모두 포팅된 비-UI 코드에 모델링(TurnStateMachine.cs가 SendShouldHatch 시맨틱 + `QueueMainPhaseAction` 참조, `MainPhaseAction/PassAction.cs`가 `class PassAction` 정의). 버튼은 이미 포팅된 턴 액션 위 순수 입력 어댑터 → 스켈레톤 스텁이 미러 안 된 규칙 없음.

### 18. Script/CardEffectFactory/CanNotAttack.cs
Verdict: MATCH
근거: `CanNotAttackSelfStaticEffect`/`CanNotAttackStaticEffect` AS-IS 1:1(동일 params, `CanUseCondition`/`AttackerCondition`/`DefenderCondition` 중첩 func, `IsExistOnBattleAreaDigimon` 가드, `SetIsInheritedEffect` 분기, `DefenderCondition` pass-through). 유일 실적응: `card.PermanentOfThisCard()`→`ICardEffect.ResolvePermanentOfThisCard(card)`(42행). 헤더 "ADAPTATION (2)" stale — 88행 `CanNotBeAffected(canNotAttackClass)`는 verbatim AS-IS.

### 19. Script/CardEffectCommons/KeyWordEffects/Fortitude.cs
Verdict: MATCH
근거: AS-IS 4메서드 전건 충실: `CanTriggerFortitude`(`CanTriggerOnDeletion` verbatim 위임), `CanActivateFortitude`(동일 trash/deleted-stack/≥1-source/`CanPlayAsNewPermanent` 로직), `FortitudeProcess`(`PlayPermanentCards` Trash 무료 replay), `GainFortitude`(`EvadeEffect`를 `EffectTiming.OnDestroyedAnyone`에 부여 — AS-IS copy/paste quirk를 verbatim 보존). 적응은 미러 헬퍼 형태: `CanPlayAsNewPermanent`/`PlayPermanentCards`가 `SelectCardEffect.Root` arg 탈락(zone live 해석; `FortitudeProcess`는 여전히 `ChoiceZone.Trash` 전달), `activateClass`→`sourceCard: card`, coroutine→`Task`, `CreateBuffEffect` VFX 제거.

### 20. Script/IEnumerableExtension.cs
Verdict: MATCH
근거: 전 확장 메서드 AS-IS↔TO-BE 동일 — `GetRandom`, `Map`(List+array), `Filter`(List+array), `Some`, `Flat`(List+array), `Reduce`, `Clone`/`CloneArray`, `Every`. 순수 LINQ, 바이트-수준 바디; `using UnityEngine;`만 제거(substrate). Verbatim.

### 21. Script/CardEffectFactory/AddAppfusionMethod.cs
Verdict: MATCH
근거: `AddAppfuseMethodByName`/`AddAppfuseMethodByCondition` AS-IS 1:1(중첩 `GetAppFusion`/`linkCondition`/`digimonCondition`, 동일 이중루프 `i!=j` cardCondition 페어링, `cardSource == card` 게이트). 유일 적응: AS-IS `if (permanent.LinkedCards.Find(x => cardConditions[j](x)))`(Unity MonoBehaviour 암시 bool)→`... != null`(81행), `List.Find`가 무매치 시 `null`/default 반환하므로 시맨틱 등가. UnityEngine/Photon 제거.

### 22. Script/CardEffectCommons/KeyWordEffects/Blocker.cs
Verdict: MATCH
근거: `GainBlocker`/`GainBlockerPlayerEffect` 1:1 — 동일 null/`IsPermanentExistsOnBattleArea` 가드, 동일 `BlockerStaticEffect` 구성(동일 `PermanentCondition`/`CanUseCondition`), `AddEffectToPermanent`/`AddEffectToPlayer`를 `EffectTiming.None`. coroutine→`Task`. 유일 제거는 terminal `CreateBuffEffect` VFX(및 `GainBlockerPlayerEffect`에서 그 VFX만 구동하던 `PermanentsForTurnPlayer` 루프) — 게임-상태 효과 없음.

### 23. Script/CardEffectFactory/KeyWordEffects/Decoy.cs
Verdict: MATCH
근거: `DecoySelfEffect`/`DecoyEffect` 존재, 동일 제어흐름·가드(`targetPermanent==null`, `TopCard==null`, `card==null`)·전 중첩 로컬(`CanSelectPermanentCondition`, `CanUseCondition`, `CanActivateCondition`, `ActivateCoroutine`). substrate 번역: `card.PermanentOfThisCard()`→`ICardEffect.ResolvePermanentOfThisCard(card)`; `IEnumerator`→`Task`; `card.Owner.Enemy`→`new Player(card.Context, card.Owner).Enemy?.PlayerId`. 해시 문자열 rename: AS-IS `Decoy_{card.CardID}`→TO-BE `Decoy_{card.CardNumber}`; 정당 — AS-IS `CardSource.CardID => _cEntity_Base.CardID`(CardSource.cs:3454)는 카드-번호 식별자, 미러가 `CardNumber`로 rename(CardSource.cs:1166); 값은 effect-identity 해시에만 사용 → 등가.

### 24. Script/CardEffects/ChangeCostClass.cs
Verdict: MATCH
근거: `ChangeCostClass : ICardEffect, IChangeCostEffect`, 6 `Func` 필드, `SetUpChangeCostClass`, `GetCost`, `CardCondition`, `IsUpDown`, `IsCheckAvailability`, `IsChangePayingCost` 전건 statement-동일(기본 반환 포함 — `IsChangePayingCost` returns `true`). 유일 substrate 변경: `cardSource.Owner.CanReduceCost(...)`→`new Player(cardSource.Context, cardSource.Owner).CanReduceCost(...)`. 동일 중첩-if 순서 + `newCost < cost`/`newCost = cost` 감소 가드 보존.

### 25. Script/CardEffectFactory/AddLinkRequirement.cs
Verdict: MATCH
근거: `AddSelfLinkConditionStaticEffect`/`AddLinkConditionStaticEffect` 바디 바이트-동일(동일 `cardCondition ?? (cs => cs == card)` 기본, `effectName ?? "Link"`, `GetLink`/`CardCondition`/`PermanentCondition` 로컬, `LinkCondition(digimonCondition, cost)` 구성). 유일 제거: `using UnityEngine;` + AS-IS 미사용 `using System.Net.NetworkInformation;`.

### 26. Script/CardEffectCommons/KeyWordEffects/Barrier.cs
Verdict: MATCH
근거: 3심볼 전건 — `CanActivateBarrier`, `BarrierProcess`, `GainBarrier`(단일 정의, 중복 없음). 로직 보존: `CanActivateBarrier`=`IsPermanentExistsOnBattleArea && SecurityCards.Count >= 1`; `BarrierProcess`는 security top 1 파괴 후 `willBeRemoveField = false`(동일 순서); `GainBarrier`는 `BarrierEffect`(`card = targetPermanent.TopCard`)를 `WhenPermanentWouldBeDeleted`에 버킷. substrate: `IEnumerator`→`Task`, `topCard.Owner.SecurityCards`→`new Player(...).SecurityCards`, `IDestroySecurity` ctor 재형성. 제거분은 UI/VFX만이며 AS-IS 실인용: `ShowDeleteEffect`/`HideDeleteEffect`/`PlayLog`(AS-IS 35,45,47-56), terminal `CreateBuffEffect` VFX(AS-IS 101-104).

### 27. Script/CardEffectCommons/KeyWordEffects/Rush.cs
Verdict: MATCH
근거: `GainRush`/`GainRushPlayerEffect` 양측 존재, 단일 정의. 동일 가드, `PermanentCondition`/`CanUseCondition` 로컬, `RushStaticEffect` 구성, 버킷 타이밍(`AddEffectToPermanent`/`AddEffectToPlayer` with `EffectTiming.None`). 유일 제거는 terminal `CreateBuffEffect` VFX(AS-IS 40) + per-permanent VFX 루프(AS-IS 78-84) — VFX 전용; AS-IS `PermanentsForTurnPlayer` 순회는 그 VFX emit만을 위한 것.

### 28. Script/SelectDNACondition.cs
Verdict: MATCH
근거: `SetUp`, 전 필드(AS-IS 미사용 `_candidates` 포함), `ResetSelectDNAConditionClass`, `Activate` 미러. AS-IS dead inner arm `if (jogressCondition.Count == 1)`(outer `Count > 1` 가드 하)를 verbatim 보존. 선택흐름(`SetIntSelection`→`WaitForEndSelect`→`SelectedIntValue`) 동일. substrate: `MonoBehaviourPunCallbacks` base + Photon 제거, `IEnumerator`/`Func<int,IEnumerator>`→`Task`/`Func<int,Task>`, `_targetDNA.jogressCondition`→`_targetDNA.JogressConditionOf()`. 제거된 `commandText.CloseCommandText()`/`WaitWhile`는 UI(AS-IS 82-83); photon sync는 AS-IS에서 이미 주석처리(43).

### 29. Script/CardEffectFactory/VortexCanAttackPlayers.cs
Verdict: MATCH
근거: `VortexCanAttackPlayersSelfStaticEffect`/`VortexCanAttackPlayersStaticEffect` 동일(양측 `AttackerCondition` 로컬, `IsPermanentExistsOnBattleArea` 가드, `CanNotBeAffected(vortexCanAttackPlayersClass)` 부정, `isInheritedEffect` 분기 포함). 유일 substrate 변경: `card.PermanentOfThisCard()`→`ICardEffect.ResolvePermanentOfThisCard(card)`. (헤더 주석은 `CanNotBeAffected(...InstanceId)` 적응 주장하나 실제 87행은 verbatim `attacker.TopCard.CanNotBeAffected(vortexCanAttackPlayersClass)` — 주석 아닌 코드로 검증.)

### 30. Script/CardEffectCommons/CanUseEffects/WhenWinBattle.cs
Verdict: MATCH
근거: `CanTriggerWhenWinBattle` 로직-동일 line-for-line — 동일 시그니처/기본값(`winnerRealCondition = null`, `isSecurityOnly = false`), 동일 해시테이블 키 read(`WinnerPermanents`, `WasTie`, `LoserCard`, `WinnerPermanents_real`), 동일 `.Some(...)` 술어, 동일 `!isSecurityOnly || LoserCard != null` 게이트, 동일 중첩-if 순서. `using UnityEngine;`만 제거.

### 31. Script/Combinations.cs
Verdict: PROBLEM
근거: TO-BE 파일은 헤더 주석 + `// TODO: Skeleton only. Port or implement deterministic .NET logic later.`만 — 코드 0. 전 AS-IS 심볼 부재: `Sample`, `NameSample`, `GetCombinations<T>`, `GetCombinationsCore<T>`, `HighestValue`, `GetUniqueNameCardCount`, `GetUniqueColorCardCount`, color-dedup 로직 `GetDifferenetColorCardCount`(재귀 조합 열거 + per-color slotting + `allowSkip` SequenceEqual dedup). 타처 folding 아님 확인: `src/HeadlessDCGO.Engine` 전역 grep(`GetCombinations`, `GetUniqueColorCardCount`, `GetUniqueNameCardCount`, `GetDifferenetColorCardCount`, `class Combinations`) 타파일 0. unique-name/unique-color/different-color 카운팅(cost/count 효과에서 사용)은 실 게임로직 → substrate 아닌 진짜 포팅 갭.

### 32. Script/CardEffectFactory/ChangeOriginDP.cs
Verdict: MATCH
근거: `ChangeBaseDPStaticEffect<T>`/`ChangeBaseDPGlobalEffect<T>` verbatim — 제네릭 타입 디스패치(`isInt`/`isIntFunc`), early-return(`!isInt && !isIntFunc`, `(int)changeValue == 0`, `Func<int> == null`), `effectName()`=`$"Origin DP is {_changeValue()}"`, `ChangeDP`, `PermanentCondition`, `_isUpDown()==false`, `isMinusDPFunc: () => false` 포함. substrate만: `permanent.TopCard.CanNotBeAffected(...)` 미러형; UnityEngine 제거.

### 33. Script/CardEffectCommons/CustomMessage.cs
Verdict: MATCH
근거: 4헬퍼 — `customPermanentMessageArrayTemplate`, `customPermanentMessageArray_ChangeDP`, `customPermanentMessageArray_ChangeOriginDP`, `customPermanentMessageArray_ChangeSAttack` — statement-동일(정확한 리터럴 "Digimon or Tamer", `"1 {permanentKindText()}"`, `$"that will gain DP +{changeValue}"`, `$"whose origin DP will be {changeValue}"`, `$"that will gain Security Attack +{changeValue}"` 포함), 2원소 `{Select..., The opponent is selecting...}` 배열. 순수 문자열 템플릿, 무상태; 미사용 usings만 제거.

### 34. Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/CanNotSuspend.cs
Verdict: MATCH
근거: `GainCantSuspendUntilOpponentTurnEnd`(AS-IS 8)와 `GainCanNotSuspend`(AS-IS 34) 1:1. 가드(10-13,36-39), `PermanentCondition == target`(43), 3중첩 `CanUseCondition`(on-battle-area/condition/`!TopCard.CanNotBeAffected(cause)`)(45-59), `CantSuspendStaticEffect(...)`(61), `AddEffectToPermanent(timing:None)`(63) 재현. TO-BE는 live `activateClass`를 `CanNotBeAffected` cause로 전달(AS-IS도 `activateClass`). 유일 제거는 AS-IS 65-68 `CreateDebuffEffect` UI.

### 35. Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/StartOfMainAttack.cs
Verdict: MATCH
근거: 인라인 `ActivateClass` 셋업(SetUpICardEffect/SetUpActivateClass(-1,false)/SetEffectSourcePermanent), `UntilOwnerTurnEndEffects.Add(GetCardEffect)`, 3 로컬 func, `SetCanNotSelectNotAttack`, `GetCardEffect`가 `OnStartMainPhase`에서만 yield — 바이트-충실. substrate 번역: AS-IS `TopCard.Owner.GetBattleAreaDigimons()`→`new Player(context, Owner).GetBattleAreaDigimons()`; `gameContext.TurnPlayer == TopCard.Owner`→`IsOwnerTurn(TopCard)`; coroutine→Task/await. AS-IS 77 tail `CreateDebuffEffect`(UI) 제거.

### 36. Script/CardEffectFactory/KeyWordEffects/Barrier.cs
Verdict: MATCH
근거: `BarrierSelfEffect`/`BarrierEffect` 1:1 — null 가드(34-36), SetUp 호출, `SetHashString`, `SetIsInheritedEffect`, rootCardEffect 분기, `CanUseCondition`(CanTriggerWhenPermanentRemoveField+IsByBattle), `CanActivateCondition`(CanActivateBarrier), `BarrierProcess` 위임. `PermanentOfThisCard()`→`ICardEffect.ResolvePermanentOfThisCard`. rename 1: 해시 `Barrier_{card.CardID}`→`Barrier_{card.CardNumber}`; 정당 — AS-IS `CardSource.CardID => _cEntity_Base.CardID`는 per-definition 수집기 id(사본 공유, cf. `SameCardIDCount`), TO-BE `CardSource`는 `CardID` 미노출·`CardNumber => Definition.CardNumber`가 dedup 키에만 쓰이는 동일 per-definition 식별자.

### 37. Script/CardEffectCommons/KeyWordEffects/ArmorPurge.cs
Verdict: MATCH
근거: `CanActivateArmorPurge`(IsExistOnBattleArea && DigivolutionCards.Count>=1)와 `ArmorPurgeProcess` 가드 미러. substrate 프리미티브 `DeDigivolveHelpers.ArmorPurgeTopAsync`(DeDigivolveHelpers.cs:57)에 위임, AS-IS `ArmorPurgeClass.ArmorPurge` 재현: under-source 요구(else false=Count>=1 가드), token top→Trash 아닌 None 이동(AS-IS 54-57 `if(!IsToken) AddTrashCard`), non-token→Trash, source[0] 승격, suspend/location-time 상태 반출(AS-IS SetChangedLocationTime 67), `WhenTopCardTrashed` emit(AS-IS 78-79). `willBeRemoveField=false`(AS-IS 63). 주의: TO-BE는 trashed-window를 `subject: sources[0]`(승격된 새 top)로 emit하나 AS-IS 해시테이블은 `CardSources:[topCard]`(trash된 카드) + `Permanent`를 반출 — substrate 표현차, 단일 fire 보존. UI 호출(CreateDebuffEffect/RemoveDigivolveRootEffect/log) 제거.

### 38. Script/CardEffectCommons/GiveEffect/GiveEffectToPlayer/CanNotUnsuspend.cs
Verdict: MATCH
근거: `GainCanNotUnsuspendPlayerEffect` 1:1 — 가드(12-13), inner `_PermanentCondition`(on-battle-area/`!CanNotBeAffected(cause)`/caller 술어), outer `PermanentCondition` isOnlyActivePhase 협착, `CanUseCondition`(`!isOnlyActivePhase || phase==Active`), `CantUnsuspendStaticEffect(...)`, `AddEffectToPlayer(timing:None)`. substrate: `gameContext.TurnPlayer == TopCard.Owner`→`TurnController.Current.TurnPlayerId == permanent.OwnerId`; `gameContext.TurnPhase`→`new GameContext(context).TurnPhase`. AS-IS 60-66 CreateDebuffEffect 루프 제거(UI).

### 39. Script/DeckListPanel.cs
Verdict: MATCH (UI-only substrate exception)
근거: AS-IS는 `MonoBehaviour`, 전 멤버가 순수 Unity 표현: `Animator anim`, `ScrollRect`, `Text`, `Instantiate(cardPrefab...)`, `Destroy`, `WaitWhile`/`WaitForSeconds`, `transform.localScale`, `Color32` 컬러링(166,171). 게임-규칙 계산 부재 — 덱 유효성/카운트는 타처 정의 `DeckData`(`IsValidDeckData()`, `DeckCards().Count`)에서 read, 이 파일은 렌더만. TO-BE는 의도적 스켈레톤 스텁 — 소실할 헤드리스 로직 없어 수용.

### 40. Script/CardEffectFactory/KeyWordEffects/Fragment.cs
Verdict: MATCH
근거: `FragmentSelfEffect`/`FragmentEffect` 1:1(전 param 리스트 trashValue/effectName/effectDiscription/rootCardEffect 기본, 가드, SetUp, rootCardEffect 분기, `CanUseCondition`(IsPermanentExistsOnBattleArea+CanTriggerWhenRemoveField), `CanActivateCondition`(CanActivateFragment(...,trashValue,activateClass)), `FragmentProcess(activateClass, targetPermanent, trashValue)` 위임). `PermanentOfThisCard`→`ResolvePermanentOfThisCard`; coroutine→Task. 파일 36과 동일 정당 `CardID`→`CardNumber` 해시 rename.

### 41. Script/CardEffectFactory/KeyWordEffects/Pierce.cs
Verdict: MATCH
근거: `PierceSelfEffect`/`PierceEffect` 1:1 — `isLinkedEffect` 기본 param, `SetUpActivateClass(...,-1,false,...)`, `SetIsInheritedEffect`/`SetIsLinkedEffect`, rootCardEffect 분기(inherited+linked 양쪽 clear), `CanTriggerPierce`/`CanActivatePierce`, `PierceProcess()` 위임. 양측 `SetHashString` 없음(CardID 무관). `PermanentOfThisCard`→`ResolvePermanentOfThisCard`; coroutine→Task.

### 42. Script/CardEffectFactory/KeyWordEffects/Evade.cs
Verdict: MATCH
근거: `EvadeSelfEffect`/`EvadeEffect` 1:1 — 가드, SetUp(`-1,true,EvadeEffectDiscription`), `SetHashString`, rootCardEffect 분기, `CanTriggerEvade`/`CanActivateEvade`. AS-IS `ActivateCoroutine`의 `yield return StartCoroutine(EvadeProcess(...))`→`async Task`+`await CardEffectCommons.EvadeProcess(...)` 번역. 파일 36과 동일 정당 `CardID`→`CardNumber` 해시 rename.

### 43. Script/CardEffectCommons/GiveEffect/GiveEffectToPlayer/CanNotSuspend.cs
Verdict: MATCH
근거: `GainCanNotSuspendPlayerEffect` 1:1 — 파일 38 unsuspend 변형과 구조 동일: 가드(12-13), `_PermanentCondition`/`PermanentCondition`/`CanUseCondition` 체인, `CantSuspendStaticEffect(...)`, `AddEffectToPlayer(timing:None)`. 동일 substrate 번역(`TurnController.Current.TurnPlayerId == permanent.OwnerId`; `new GameContext(context).TurnPhase`). AS-IS 60-66 CreateDebuffEffect 루프 제거(UI).

### 44. Script/CardEffectCommons/KeyWordEffects/Iceclad.cs
Verdict: MATCH
근거: `GainIceclad`(AS-IS 10)/`GainIcecladPlayerEffect`(AS-IS 46) 1:1 — 전 가드, `PermanentCondition` 바디, `CanUseCondition`(permanent-scope: on-battle-area+`!CanNotBeAffected`; player-scope: `true`), `IcecladStaticEffect(...)`, `AddEffectToPermanent`/`AddEffectToPlayer(timing:None)` verbatim. `CardSource card = activateClass.EffectSourceCard` 유지. AS-IS 40/82 `CreateBuffEffect` VFX(및 per-permanent 루프) 제거 — UI만.

### 45. Script/ShuffleDeckCode.cs
Verdict: PROBLEM
근거: TO-BE는 스켈레톤 스텁 — 헤더 주석("// TODO: Skeleton only. Port or implement deterministic .NET logic later.")만, 코드 없음. 전 AS-IS 심볼 미포팅: 필드 `ShuffledNumberIDs`, 메서드 `ReversNumberIDs`, `ConvertString`, `ReturnConvertString`, `ConvFactor`(`5.6*x^3 + 13.7*x^2 + 7.2*x + 0.7) % 13` 덱코드 암호), `GetConvertDeckCode`, `GetDeckCode`. 전 TO-BE 트리 grep(`GetConvertDeckCode`/`ReturnConvertString`/`ConvFactor`/`ShuffledNumberIDs`) 0(바이너리 `.pdb` 파일명만). 순수 결정론 게임 로직, UI/substrate 아님 → 면제 불가.

### 46. Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/CanNotAttack.cs
Verdict: MATCH
근거: `GainCanNotAttack`가 async `Task` 오버로드 + 공유 private `GainCanNotAttackImpl`로 1:1. AS-IS 가드(17-20), `AttackerCondition(attacker==targetPermanent)`(24), 래핑 `DefenderCondition`(26), `CanUseCondition`(`TopCard.CanNotBeAffected(activateClass)` read)(36-47), `CanNotAttackStaticEffect(...)`(49), 무조건 `AddEffectToPermanent(timing: EffectTiming.None)`(57) 보존. AS-IS 64-67 `CreateDebuffEffect`는 순수 UI(VFX icon) 제거; grant는 무조건 유지(AS-IS도 시각만 게이팅).

### 47. Script/CardEffectFactory/KeyWordEffects/Raid.cs
Verdict: MATCH
근거: `RaidSelfEffect`/`RaidEffect` verbatim(가드, `SetUpICardEffect`/`SetUpActivateClass(-1,true,...)`, inherited/linked 플래그, rootCardEffect 블록). 두 delta는 동작-보존 substrate 브릿지: `card.PermanentOfThisCard()`→`ICardEffect.ResolvePermanentOfThisCard(card)`, `CanActivateRaid(targetPermanent)`/`RaidProcess(targetPermanent,...)`에 선행 `card` arg. commons Raid.cs 대조: 미러 `CanActivateRaid(card,targetPermanent)`/`RaidProcess(card,...)`는 `card`를 `card.Context`/Player 해석(`new Player(card.Context, Owner).Enemy.PlayerId`)에만 사용, AS-IS 술어 로직 온전. `IEnumerator`→`Task`는 확립된 substrate 번역.

### 48. Script/CreateNewDeckButton.cs
Verdict: PROBLEM
근거: TO-BE는 스켈레톤 스텁(헤더 주석만, "Category: UnityMixedLogic"). 미포팅: `CreateNewDeck()`, `OnClickFromDeckCode()`(클립보드 덱코드 import→`DeckCodeUtility.GetAllDeckCardsFrom...`, DigiEgg-vs-main 분할 69-80, `DeckData` 구성/`ModifiedDeckData`), UI 핸들러. TO-BE grep(`class CreateNewDeckButton`/`CreateNewDeck`/`OnClickFromDeckCode`) 0. 상당수가 Unity UI(MonoBehaviour, Outline, transform 스케일링)이나 덱-import 오케스트레이션은 게임 로직이며 전면 부재 — 무-로직 substrate 케이스로 인용되지 않음.

### 49. Script/DeckCodeUtility.cs
Verdict: PROBLEM
근거: TO-BE는 스켈레톤 스텁(헤더만, 자체분류 "Category: CoreRule / Priority: HIGH"). 전 로직 미포팅: `GetDeckBuilderFile`, `GetTTSDeckCode`, `GetDeckBuilderDeckCode`(distinct-by-`CardSpriteName` + per-card 카운트 + `DataBase.ReplaceToASCII`), `GetAllDeckCardsFromTTSDeckCode`, `GetAllDeckCardsFromDeckBuilderDeckCode`(multi-line/`plusCount`/4-iteration 카드-ID 해석기 103-155), `GetCardFromCardID`. TO-BE grep(이 메서드명들 + `class DeckCodeUtility`) 비-스텁 0. 결정론 파싱 로직, substrate 면제 불가.

### 50. Script/CardEffectFactory/KeyWordEffects/Progress.cs
Verdict: MATCH
근거: `ProgressSelfStaticEffect`/`ProgressStaticEffect` verbatim(`CanActivateProgress`, `SetUpCanNotAffectedClass`, `SetIsBackgroundProcess(true)`, `CardCondition` 체인 with `attackProcess.IsAttacking`/`AttackingPermanent==...`, `SkillCondition`). 두 substrate delta: `cardSource.PermanentOfThisCard()`→`ICardEffect.ResolvePermanentOfThisCard(cardSource)`; `IsOpponentEffect(cardEffect, card)`→`IsOpponentEffect(cardEffect.EffectSourceCard, card)`. 등가 확인: AS-IS `IsOpponentEffect(ICardEffect,...)`(GameContextDeterminarion.cs:808)는 내부적으로 `cardEffect.EffectSourceCard.Owner == card.Owner.Enemy` 검사; 미러 오버로드(CardEffectCommons.cs:3807)는 `EffectSourceCard` 직접 받아 `!Owner.IsEmpty && Owner != card.Owner` 검사 — 2인 게임에서 동일 결과, 팩토리가 `EffectSourceCard` 선-null체크.

### 51. Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/CanNotBeAttacked.cs
Verdict: MATCH
근거: `GainCanNotBeAttacked` 1:1(async 오버로드 + `GainCanNotBeAttackedImpl`). CanNotAttack 대비 역할 정확 미러: 래핑 `AttackerCondition` from caller(AS-IS 24-32)와 `DefenderCondition(attacker==targetPermanent)`(34); 가드, `CanUseCondition`/`CanNotBeAffected(cause)`, `CanNotAttackStaticEffect(...)`, 무조건 `AddEffectToPermanent(EffectTiming.None)` 보존. AS-IS 64-67 `CreateBuffEffect` UI 제거; grant는 AS-IS대로 무조건.

### 52. Script/CardEffectCommons/KeyWordEffects/Evade.cs
Verdict: MATCH
근거: `CanTriggerEvade`/`CanActivateEvade` verbatim(동일 술어). `GainEvade` 1:1(EvadeEffect 빌드 + `AddEffectToPermanent(timing: WhenPermanentWouldBeDeleted)`), terminal `CreateBuffEffect` VFX 제거. `EvadeProcess`: `SuspendPermanentsClass` ctor를 AS-IS `(list, CardEffectHashtable(activateClass))`→미러 `(list, activateClass, isBlock:false)`로 변경 — substrate ctor-형태 번역; `willBeRemoveField=false` 보존(45→85), `HideDeleteEffect()` UI 제거. `IEnumerator`→`Task`.

### 53. Script/CardEffectCommons/KeyWordEffects/Fragment.cs
Verdict: MATCH
근거: `CanActivateFragment` verbatim(`IsPermanentExistsOnBattleArea` && `CanBeDestroyedBySkill(activateClass)` && `DigivolutionCards.Count >= trashValue`). `FragmentProcess`는 선택 셋업(`maxCount:3`, `canNoSelect:()=>false`, faceDown, custom 메시지, `selectPlayer=EffectSourceCard.Owner`)과 `selectedCards.Count == trashValue` 게이트(미러 `!= trashValue → return`, 등가) 보존. AS-IS `ITrashDigivolutionCards(...).TrashDigivolutionCards()`→substrate `DigivolutionStackHelpers.TrashSpecificSourcesAsync(...)`가 동일 permanent/selected-source id/cause 전달. 성공 시 `willBeRemoveField=false` 보존(77→87); `HideDeleteEffect()` UI 제거.

### 54. Script/CardEffectFactory/KeyWordEffects/Decode.cs
Verdict: MATCH
근거: `DecodeSelfEffect`/`DecodeEffect` verbatim(null/`sourceCondition ??= _=>true` 가드, `effectname = $"Decode {decodeStrings[0]}"`, `SetUpActivateClass(...,-1,true,...)`, rootCardEffect 블록, `CanUseCondition`(`IsExistOnBattleAreaDigimon && CanTriggerWhenRemoveField && !IsByBattle`), `CanActivateCondition`(`CanActivateDecode(TopCard, sourceCondition, activateClass)`), `DecodeProcess` 위임). substrate delta만: `PermanentOfThisCard()` 브릿지 + `IEnumerator`→`Task`. 추가 `static class Decode { const DecodeSourceConditionKey }`는 보존된 const 홀더로 실 외부 소비자 존재 — grep으로 `KeyWordEffects.Decode.DecodeSourceConditionKey` 사용 확인(DeletionReplacementTiming/CardLeavePlayCleanup 참조); 발명 발산 아님.

### 55. Script/CardEffectFactory/KeyWordEffects/Jamming.cs
Verdict: MATCH
근거: `JammingSelfStaticEffect`/`JammingStaticEffect` verbatim(`effectName="Jamming"`, `CanUseCondition`, `PermanentCondition` with `IsPermanentExistsOnBattleArea`, `CanNotBeDestroyedByBattleStaticEffect`에 동일 named args로 위임). substrate delta: `PermanentOfThisCard()` 브릿지; `CanNotBeDestroyedByBattleCondition`에서 AS-IS `DefendingCard == attackProcess.SecurityDigimon`→미러 `DefendingCard.InstanceId == attackProcess.SecurityDigimon`. 확인: AS-IS `AttackProcess.SecurityDigimon`은 `CardSource`(AttackProcess.cs:17), 미러는 `HeadlessEntityId? SecurityDigimon`(AttackProcess.cs:128) 타입 → `.InstanceId` 비교가 정확한 id-vs-reference substrate 번역, 보존된 `DefendingCard != null` 체크 가드.

---

## 계통 노트(정상 처리, 참고)

- `card.CardID`→`card.CardNumber` 해시 rename(#23, #36, #40, #42): TO-BE `CardSource`에 `CardID` 미노출, 양자 모두 dedup 키에만 쓰이는 per-definition 식별자 → 일관·정당 substrate rename.
- `CreateBuffEffect`/`CreateDebuffEffect`/VFX·로그 제거: 다수 파일에서 UI 전용, 게임-상태 무영향(실인용 확인).
- UI 스켈레톤 스텁 정당 면제: #2 StreamingAssetsUtility, #17 NextPhaseButton, #39 DeckListPanel — AS-IS가 순수 Unity 표현/입력 어댑터이며 게임 규칙은 이미 포팅된 층에 존재하거나 부재.
- dead-code 정당 생략: #1 `InverteDigimonSAttack`/`InvertDigimonSAttack`(AS-IS 호출부 0 확인).
