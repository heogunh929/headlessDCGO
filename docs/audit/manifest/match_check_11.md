# AS-IS↔TO-BE 매칭 검증 — 파트 11/13 (86파일)

- 담당 매니페스트: `docs/audit/manifest/both_part_11.txt`
- AS-IS = `DCGO/Assets/Scripts/<relpath>`, TO-BE = `src/HeadlessDCGO.Engine/Assets/Scripts/<relpath>`
- 방식: 양측 전문 실독·AS-IS 전 심볼 TO-BE 대조. 판정 근거는 실소스 관찰만(기존 감사판정·코드주석 설명 근거 미사용).
- 검증일: 2026-07-24

## 총괄 요약
- 전건 86파일 판단 완료(누락 0).
- **문제 7건**: #13, #14, #44, #70, #71, #75, #86.
- 정상 79건.
- 문제 성격 분류:
  - **룰/데이터 결정 로직 미포팅 스텁(부당 이연)** — #14(ConvertBinaryNumber), #44(GameRandom), #70(SpellRestoration), #71(CheatAction), #75(JsonSerializedClass).
  - **AS-IS grant 미포팅(소실)** — #13(InvertDigimonSAttackPlayerEffect).
  - **1:1 위반 동작 변경(가드 추가)** — #86(Training Commons).
- 정상이나 명시 필요:
  - #2 CardInfo, #62 ShowPhaseObject — 완전 스텁이나 순수 UI(MonoBehaviour 프레젠테이션 전용, 룰 로직 0)라 미러 대상 부재 → 로직 소실 아님.
  - #33 CutInProcess — 미포팅 STOP 스텁이나 AS-IS 호출부 0(데드코드, 직접 grep 검증). 엄밀 1:1 기준으론 재오픈 조건부 이연.

---

## 문제 발견 전건

### 13. Script/CardEffectCommons/GiveEffect/GiveEffectToPlayer/ChangeSAttack.cs
- 판정: **문제 (AS-IS grant 미포팅/소실)**
- 근거: AS-IS 파일은 두 메서드 보유 — `ChangeDigimonSAttackPlayerEffect`(:10-68)와 `InvertDigimonSAttackPlayerEffect`(:73-131). TO-BE는 첫 번째만 포팅(substrate `CardEffectCommons.cs:3058` 위임, 가드·PermanentCondition 폴드·`ChangeSAttackStaticEffect`·`AddEffectToPlayer(None)` 1:1 확인). `InvertDigimonSAttackPlayerEffect`는 TO-BE 전역에 전무(grep 결과 주석 1줄 "left for a later batch"만). 팩토리 `InvertSAttackStaticEffect`는 ChangeSAttackClass.cs:215에 존재하나, 그것을 소비해 `InvertSAttackClass`를 player-scope로 부착하는 grant 래퍼(AS-IS :107 호출)가 소실. AS-IS 두 grant 중 하나 미포팅.

### 14. Script/ConvertBinaryNumber.cs
- 판정: **문제 (결정 로직 미포팅 스텁)**
- 근거: TO-BE는 헤더 7줄 스켈레톤 스텁(로직 0). AS-IS는 룰/데이터 결정 로직 실재 — 260개 글리프 `numbers[]` 진법 테이블, `NStringToNKString`/`NKStringToNString`(진법 변환), `IntToNString`(x<0→null, x==0→"0", 나머지 누적+Reverse), `NStringToInt`(자리별 `Array.IndexOf * Pow(n,i)` 누적), `114514` 센티널(불량 입력 표식) 전부 미포팅. UI 아닌 DataLoader 결정 로직이라 스텁 이연 부당.

### 44. Script/GameRandom.cs
- 판정: **문제 (결정론 RNG 미포팅 + 비-1:1 대체 재구현)**
- 근거: 정본 경로 `Script/GameRandom.cs`가 스켈레톤 스텁("TODO: Skeleton only")으로 AS-IS 전 구현(`Seed`/`Range(int)`/`Range(float)`/`Probability`/`NextState`/`NextUInt32`/`RotateLeft`/`SplitMix64`) 미포팅. 실구현으로 볼 `Headless/Services/GameRandomSource.cs`도 1:1 아님: (1) static `GameRandom`→인스턴스 `GameRandomSource`(IRandomSource)로 API 변경, (2) AS-IS `NextUInt32 = (uint)(NextState()>>32)`(상위 32비트)인데 TO-BE는 `NextUInt64()` 전체 64비트 소비 → 난수 시퀀스 자체 상이, (3) `Range(int)` 32비트 rejection → 64비트 rejection, (4) float `Range` `(NextUInt32()>>8)*(1/2^24)` → `NextDouble` `(NextUInt64()>>11)*(1/2^53)`, (5) `Probability(float)` 소실·`Shuffle<T>` 신설, (6) seed `long`→`int`. Xoshiro256** 코어·SplitMix64 시딩만 동일. "동일 시드→동일 시퀀스" 결정론 계약이 보존되지 않은 대체 재구현이며 요청 경로 파일은 미포팅.

### 70. Script/SpellRestoration.cs
- 판정: **문제 (완전 소실)**
- 근거: TO-BE는 미포팅 스켈레톤 스텁("// TODO: Skeleton only"). AS-IS의 `convertTable[65,2]`(A→あ … =→ん 65행 매핑), `ToSpellRestoration(object)`, `FromSpellRestoration<T>(string)`(FromBase64String→UTF8→JsonUtility.FromJson, FormatException catch) 전부 대응 없음. `grep -r "class SpellRestoration" src/` 결과 다른 이름/경로 포팅본도 없음 → 소실.

### 71. Script/MainPhaseAction/CheatAction.cs
- 판정: **문제 (실행 로직 미포팅 스텁)**
- 근거: TO-BE는 미포팅 스켈레톤 스텁. AS-IS의 `enum Type`(None/Draw/TrashCard/PlaceCardOnDeck/PlaceCardInSecurity/PlaceCardInSecurityFaceup/GainMemory/LoseMemory 8종), `Execute(TurnStateMachine)`의 7-case switch(AllowCheats 게이트 후 DrawCard/TrashCard/TopDeckCard/PlaceInSecurity(false/true)/AlterMemory(±1)), `Serialize`/`Deserialize` 미대응. `Headless/Runtime/CheatActionGuard.cs`가 존재하나 정반대 로직(치트 액션을 legal-action 경로에서 거부/필터 `Reject`/`IsCheatOrDebugAction`)이며 실행 커맨드 포팅 아님. 미러 파일 자체는 스텁으로 남아 AS-IS 미러 아님.

### 75. Script/JsonSerializedClass.cs
- 판정: **문제 (데이터 모델 소실 + 스키마 재발명)**
- 근거: TO-BE는 미포팅 스켈레톤 스텁. AS-IS의 `DCGO.CardEntities` 데이터 모델(`RootObject`, `CardData` 35필드[AAs/JAAs/aceEffect/assembly/attribute/block[]/burstDigivolve/…/digivolveCondition[]/name/restrictions/version 등], `AlternateArt`, `DigivolveCondition`, `CardName`{en/ja/ko/sc/tc}, `Restriction`) 미대응. substrate 카드 로딩은 `Headless/DataLoading/CardBaseEntityLoader.cs`의 다른 스키마 `CardJsonDto`(cardNumber/colors[]/evolutionConditions/types[]/attributes[]/forms[]/effectClass 등, 별도 cards.json)로 재발명됐고 AS-IS CardData와 1:1 아님. 데이터 모델 미러 소실+스키마 재설계.

### 86. Script/CardEffectCommons/KeyWordEffects/Training.cs
- 판정: **문제 (1:1 위반 — AS-IS에 없는 가드 추가)**
- 근거: AS-IS `TrainingClass.Training`(:29-30)은 `card.Owner.LibraryCards[0]`을 가드 없이 무조건 인덱싱(빈 라이브러리 시 IndexOutOfRange throw). TO-BE(:53-57)는 AS-IS에 없는 `if (libraryCards.Count > 0)` 가드 삽입 → 빈 라이브러리 시 조용히 no-op로 동작 변경. 그 가드는 팩토리 Training(#74) AS-IS에만 존재하고 이 TrainingClass AS-IS에는 없음. 나머지(`IsSuspended||!CanSuspend` 조기반환, SuspendPermanentsClass(activateClass, isBlock:false), InstanceId 어댑테이션)는 #74와 동일 substrate 패턴으로 충실. 0-caller latent 코드라 런타임 영향은 없으나 엄격 1:1 원칙 위반.

---

## 정상 판정 전건 (근거 포함)

### 1. Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/CanNotBeBlocked.cs
- 판정: 정상
- 근거: AS-IS `GainCanNotBeBlocked`의 4가드(:17 target null, :18 IsPermanentExistsOnBattleArea, :19-20 activateClass/EffectSourceCard null)가 `GainCanNotBeBlockedImpl`(:55-57)에 존재. `AttackerCondition`(attacker==target)/`DefenderCondition`(caller wrap)/`CanUseCondition`(on-area && !TopCard.CanNotBeAffected(cause)) 로직 동일. `CanNotBlockStaticEffect` 6인자 순서·`AddEffectToPermanent(None)` 일치. 드롭=`CreateBuffEffect`(UI VFX)뿐. 코루틴→async Task, cause(activateClass) 스레딩 동일.

### 2. Script/CardInfo.cs
- 판정: 정상 (미포팅 스켈레톤, 순수 UI)
- 근거: TO-BE는 헤더 스켈레톤(로직 0). AS-IS `CardInfo : MonoBehaviour` 전문이 순수 프레젠테이션(Image/TextMeshProUGUI/Sprite/Color/SetActive/폰트·언어 분기·`CardImage.sprite = await GetCardSprite()`·`OnClick`→OpenCardDetail)으로 게임 상태 변경 룰 로직 전무. 헤드리스 룰 충실도 무영향 UI-only라 스텁 이연 정당, 룰 로직 소실 없음.

### 3. Script/CardEffectCommons/GiveEffect/GiveEffectToPlayer/CanNotAttack.cs
- 판정: 정상
- 근거: `GainCanNotAttackPlayerEffect` 대응 완전. 가드 :17-18(impl :57). `AttackerCondition`이 on-area + !CanNotBeAffected(cause) + caller attackerCondition 3중 폴드(:59-73) AS-IS :22-36 동일. `DefenderCondition`(caller wrap), `Condition()`(상수 true) 일치. `CanNotAttackStaticEffect`·`AddEffectToPlayer(None)` 일치. 드롭=PermanentsForTurnPlayer 순회 `CreateDebuffEffect`(UI).

### 4. Script/CardEffectFactory/KeyWordEffects/Fortitude.cs
- 판정: 정상
- 근거: `FortitudeSelfEffect`+`FortitudeEffect` 양측 대응. 가드 3(:45-47), `SetUpICardEffect`/`SetUpActivateClass(-1,false,FortitudeEffectDiscription())`/`SetHashString`/`SetIsInheritedEffect`, rootCardEffect 3세터 분기 verbatim. `CanUseCondition`=CanTriggerFortitude && condition, ActivateCoroutine=FortitudeProcess 위임. Substrate: `PermanentOfThisCard()`→`ICardEffect.ResolvePermanentOfThisCard(card)`, `new Permanent(List)`→`new Permanent(Context,InstanceId,Owner)`, IEnumerator→Task, HashString `CardID`→`CardNumber`(확립된 identity 번역).

### 5. Script/CardEffectCommons/GiveEffect/GiveEffectToPlayer/CanNotBlock.cs
- 판정: 정상
- 근거: `GainCanNotBlockPlayerEffect` 대응. `AttackerCondition`(on-area+!CanNotBeAffected+caller, :55-69) AS-IS :22-36 일치, `DefenderCondition`(caller wrap), `CanUseCondition`=true. `CanNotBlockStaticEffect`/`AddEffectToPlayer(None)` 일치. #3(CanNotAttack)과 대비해 팩토리만 CanNotBlock으로 정확히 갈림. 드롭=CreateDebuffEffect UI.

### 6. Script/CardEffectFactory/ChangeCardDP.cs
- 판정: 정상
- 근거: `ChangeSecurityDigimonCardDPStaticEffect<T>` 라인 단위 일치 — 제네릭 판별(isInt/isIntFunc :23-29), `_changeValue()`/`isUpValue()`, `SetUpChangeCardDPClass(changeDPFunc,cardSourceCondition,isUpDown,isMinusDP:()=>!isUpValue())`, `SetEffectName` 동일. 유일 차이=`CardCondition` 내 `attackProcess.SecurityDigimon == cardSource`→`== cardSource.InstanceId`(identity 비교 축을 InstanceId로 이동한 substrate 번역).

### 7. Script/CardEffectCommons/CanUseEffects/WhenLinked.cs
- 판정: 정상
- 근거: `CanTriggerWhenLinking`(card 동일성 분기)/`CanTriggerWhenLinked`(sourcecCondition 분기) 두 메서드 모두 중첩 null 가드(hashtable→GetPermanentFromHashtable→TopCard→permanentCondition→GetCardEffectFromHashtable→GetCardFromHashtable) 라인 단위 동일. 오탈자 `sourcecCondition`까지 보존. UnityEngine using만 제거.

### 8. Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/TamerBecomesDigimonThatCanNotDigivolve.cs
- 판정: 정상 (드롭 1건 명시)
- 근거: 5가드(:12-16, DP<0 포함) 일치. 3연속효과 grant 대응 — `TreatAsDigimonStaticEffect`/`ChangeBaseDPStaticEffect`/`CanNotDigivolveStaticEffect(cardCondition:_=>true, effectName:"Can't digivolve")` 인자·순서 동일, 각 `AddEffectToPermanent(None)`. 드롭: (a) :73 `CreateBuffEffect`(UI), (b) :48 `changeBaseDPClass.SetActivatedTime(DateTime.Now)`(substrate 타임스탬프, 단일 grant라 레이어 순서 영향 미미). 룰 로직 소실 아님.

### 9. Script/CardEffectFactory/KeyWordEffects/Blocker.cs
- 판정: 정상
- 근거: `BlockerSelfStaticEffect`+`BlockerStaticEffect` 대응. Self `CanUseCondition`(IsExistOnBattleAreaDigimon && condition), Static `effectName="Blocker"`/`SetUpBlockerClass(PermanentCondition)`/isInherited·isLinked 분기, 내부 `PermanentCondition`(IsPermanentExistsOnBattleArea && caller) verbatim. Substrate: `PermanentOfThisCard()`→`ResolvePermanentOfThisCard`.

### 10. Script/CardEffectFactory/KeyWordEffects/Retaliation.cs
- 판정: 정상
- 근거: `RetaliationSelfEffect`+`RetaliationEffect` 대응. 가드 3, `SetUpActivateClass(...,-1,false,RetaliationEffectDiscription())`, isInherited·isLinked, rootCardEffect 분기 일치. `CanUseCondition`=`CanTriggerOnPermanentDeleted(hashtable, p=>p.cardSources.Contains(targetPermanent.TopCard))` && condition verbatim. ActivateCoroutine=RetaliationProcess 위임. Substrate: PermanentOfThisCard 브릿지, IEnumerator→비-async Task(:76-79 return) 등가.

### 11. Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/CanNotBlock.cs
- 판정: 정상
- 근거: `GainCanNotBlock` 대응. 4가드(:12-15). `AttackerCondition`(caller wrap)/`DefenderCondition`(attacker==target)/`CanUseCondition`(on-area && !CanNotBeAffected(cause)) 일치. #5(player-scope)과 달리 target 지정형이라 DefenderCondition이 target 동일성인 점 정확. `CanNotBlockStaticEffect`/`AddEffectToPermanent(None)` 일치, 드롭=CreateDebuffEffect UI.

### 12. Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/CanNotBeDeletedByEffect.cs
- 판정: 정상
- 근거: `GainCanNotBeDeletedByEffect` 대응. 4가드(:35-36). `PermanentCondition`(attacker==target)/`CanUseCondition`(on-area && !CanNotBeAffected(cause)) 일치. caller `Func<ICardEffect,bool> cardEffectCondition`을 down-adapt 없이 `CanNotBeDestroyedBySkillStaticEffect`에 그대로 전달(:84) — AS-IS :36 실 술어 평가(fidelity 유지). `AddEffectToPermanent(None)` 일치.

### 15. Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/CanNotBeDeletedByBattle.cs
- 판정: 정상
- 근거: `GainCanNotBeDeletedByBattle` 대응. 4가드(:35-43; on-area 가드는 `zones.GetCards(Owner,BattleArea).Contains(targetId)` substrate 번역). caller 4-arg `Func<Permanent,Permanent,Permanent,CardSource,bool>` 술어를 `CanNotBeDestroyedByBattleStaticEffect`에 verbatim 전달(:79). `PermanentCondition`(permanent==target)/`CanUseCondition`(on-area && !CanNotBeAffected(activateClass)) 일치, `AddEffectToPermanent(None)`. 코루틴→동기 bool, 드롭=CreateBuffEffect UI. cause 스레딩(ICardEffect activateClass) 보존.

### 16. Script/CardEffectFactory/KeyWordEffects/MaterialSave.cs
- 판정: 정상
- 근거: `MaterialSaveEffect(card, materialSaveCount)` 대응. `SetUpActivateClass(...,-1,true,EffectDiscription())` 4번째 true 보존, `EffectDiscription()`(Utils.PluralFormSuffix 포함) verbatim. `CanSelectCardCondition`=`card.IsContainDigiXrosCondition(cardSource)`, `CanSelectPermanentCondition`(IsPermanentExistsOnOwnerBattleArea && IsTamer && !IsToken), `CanUseCondition`(IsExistOnBattleArea && CanTriggerWhenRemoveField), ActivateCoroutine=MaterialSaveProcess 위임 동일. IEnumerator→비-async Task, HashString `CardID`→`CardNumber` identity 번역.

### 17. Script/CardEffectCommons/GiveEffect/GiveEffectToPlayer/CanNoReturnToDeck.cs
- 판정: 정상
- 근거: `GainCanNotReturnToDeckPlayerEffect` 대응. 가드 :17-18(impl :49). `PermanentCondition`(on-area + !CanNotBeAffected(cause) + caller, :51-65) AS-IS :22-36 일치, `CanUseCondition`=true. caller `Func<ICardEffect,bool> cardEffectCondition`을 `CannotReturnToDeckStaticEffect`에 verbatim 전달(:71, down-adapt 없음). `AddEffectToPlayer(None)`, 드롭=CreateBuffEffect UI.

### 18. Script/CardEffects/ChangeLinkMaxClass.cs
- 판정: 정상
- 근거: `ChangeLinkMaxClass : ICardEffect, IChangeLinkMaxEffect` 라인 단위 일치. `SetUpChangeLinkMaxClass`(3필드), `GetLinkMax`의 invertValue switch(case -1: LinkMax+Abs(delta), case 1: LinkMax-delta), `isUpDown()`(null→UpToConstant), `PermanentCondition`(null/TopCard/술어 중첩) 동일. 유일 차이 `Mathf.Abs`→`Math.Abs`(등가), Unity.Mathematics using 제거.

### 19. Script/CardEffects/ChangeSAttackClass.cs
- 판정: 정상
- 근거: `SetUpChangeSAttackClass`, 세 필드(_changeSAttackFunc/_permanentCondition/_isUpDown), `GetSAttack`의 invert switch(-1/1), `isUpDown()`의 CalculateOrder.UpToConstant fallback, `PermanentCondition` 3중 null 중첩 라인 단위 동일. 유일 차이 `Mathf.Abs`(:32)→`Math.Abs`(:36) substrate 치환.

### 20. Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/CanNoReturnToDeck.cs
- 판정: 정상
- 근거: `GainCanNotReturnToDeck`의 4가드(:12-15), `PermanentCondition = attacker==target`(:19), `CanUseCondition`(on-area && !TopCard.CanNotBeAffected(cause))(:21-32), `CannotReturnToDeckStaticEffect` 6 명명인자(:34-40), `AddEffectToPermanent(None)`(:42-47) 전부 Impl 대응. IEnumerator→Task, activateClass→cause 분리 substrate. 드롭=:49-52 `CreateBuffEffect`(UI).

### 21. Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/CanNotReturnToHand.cs
- 판정: 정상
- 근거: #20과 동형. `CannotReturnToHandStaticEffect` 팩토리·`cannotReturnToHandClass`·가드·두 로컬함수 대응. 차이 IEnumerator→Task, UI-only `CreateBuffEffect` 드롭.

### 22. Script/CardEffectCommons/GiveEffect/GiveEffectToPlayer/CanNotBeDeletedByBattle.cs
- 판정: 정상
- 근거: `GainCanNotBeDeletedPlayerEffect`의 가드 2(activateClass/EffectSourceCard, :12-13), `PermanentCondition` 3중(on-area + !CanNotBeAffected + caller, :17-31), `CanUseCondition=>true`(:33-36), 4-arg canNotBeDestroyedByBattleCondition 전달, `AddEffectToPlayer`(:46) 대응. 팩토리 명명인자라 순서차 무영향. :48-54 PermanentsForTurnPlayer 순회 `CreateBuffEffect` UI 드롭.

### 23. Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/ImmuneFromDPMinus.cs
- 판정: 정상
- 근거: #20/#21과 동형(permanent 대상). `ImmuneFromDPMinusStaticEffect`·`immuneFromDPMinusClass`·4가드·`PermanentCondition`·`CanUseCondition` 대응. IEnumerator→Task, UI-only 드롭.

### 24. Script/CardEffectFactory/KeyWordEffects/ArtsDigivolve.cs
- 판정: 정상
- 근거: `ArtsDigivolveEffect`의 `SetUpICardEffect("Arts Digivolve",...)`, `SetUpOptionResolutionClass`, `CanUseCondition=IsExistOnExecutingArea`, `CanSelectPermanentCondition`(IsOwnerPermanent && IsDigimon && CanPlayCardTargetFrame(...Root.Execution)), `SelectPermanentEffect.SetUp` 12인자, `PlayCardClass`(payCost:false, activateETB:true) 동일. Substrate: (a) IEnumerator/StartCoroutine→async Task/await, (b) 전역 `HasMatchConditionPermanent(pred,true)`→카드-컨텍스트 오버로드 `HasMatchConditionPermanent(card,pred,true)`(술어 자체가 IsOwnerPermanent(permanent,card)로 scope-neutral, 로직 누락 아님).

### 25. Script/CardEffectFactory/KeyWordEffects/Overclock.cs
- 판정: 정상
- 근거: `OverclockSelfEffect`+`OverclockEffect`, 가드 3(:31-33), `SetUpActivateClass(...,-1,true, DataBase.OverclockEffectDiscription(trait))`, rootCardEffect 분기(SetIsInheritedEffect(false)+SetEffectSourcePermanent+SetRootCardEffect), `CanUseCondition`(IsExistOnBattleArea && IsOwnerTurn), `CanActivateCondition`(CanActivateOverclock(trait,...) && condition), OverclockProcess 위임 동일. substrate: `PermanentOfThisCard()`→`ResolvePermanentOfThisCard(card)`, IEnumerator(순수 위임)→Task.

### 26. Script/CardEffectFactory/KeyWordEffects/Reboot.cs
- 판정: 정상
- 근거: `RebootSelfStaticEffect`+`RebootStaticEffect`, `effectName="Reboot"`, `RebootClass`+`SetUpRebootClass`, isInheritedEffect·isLinkedEffect 분기, 중첩 `PermanentCondition`(IsPermanentExistsOnBattleArea 게이트) 대응. substrate: `PermanentOfThisCard()`→`ResolvePermanentOfThisCard`. 로직 verbatim.

### 27. Script/CardEffectCommons/KeyWordEffects/Decoy.cs
- 판정: 정상 (읽기 지점 이동 인지)
- 근거: `CanActivateDecoy`(IsPermanentExistsOnBattleArea && CanBeDestroyedBySkill) verbatim. `DecoyProcess`: 가드(permanent?.TopCard), DeletePeremanentAndProcessAccordingToResult, HasMatchConditionPermanent/MatchConditionPermanentCount, maxCount=Math.Min(1,...), SelectPermanentEffect.SetUp(canNoSelect:false), 커스텀 메시지, SelectPermanentCoroutine의 willBeRemoveField=false 대응. 관찰: AS-IS는 삭제 전 `owner=permanent.TopCard.Owner`(:30) 선캡처, TO-BE는 success 콜백 내 `permanent.TopCard!.Owner`(:57) 후행 참조로 이동. 지연-삭제(willBeRemoveField+스윕) 전제에서 콜백 시점 TopCard 생존→결과 등가. UI `HideDeleteEffect`/`yield return null` 드롭 substrate.

### 28. Script/CardEffectFactory/KeyWordEffects/Execute.cs
- 판정: 정상
- 근거: #25(Overclock)와 동형. `ExecuteSelfEffect`+`ExecuteEffect`, `SetUpActivateClass(...,-1,true, DataBase.ExecuteEffectDiscription())`, rootCardEffect 3-set 분기, `CanActivateExecute(targetPermanent.TopCard,...)`, ExecuteProcess 위임 동일. substrate: ResolvePermanentOfThisCard, Task 치환.

### 29. Script/CardEffectFactory/KeyWordEffects/Vortex.cs
- 판정: 정상
- 근거: #28과 동형. `VortexSelfEffect`+`VortexEffect`, `DataBase.VortexEffectDiscription()`, `CanActivateVortex`, VortexProcess 위임, rootCardEffect 분기 동일. substrate 동일 치환.

### 30. Script/CardEffectCommons/GiveEffect/GiveEffectToPlayer/CanNotReturnToHand.cs
- 판정: 정상
- 근거: `GainCanNotReturnToHandPlayerEffect`의 가드 2, `PermanentCondition` 3중, `CanUseCondition=>true`, `CannotReturnToHandStaticEffect`, `AddEffectToPlayer` 대응. permanentCondition+cardEffectCondition 둘 다 실인자 전달(뭉개기 없음). UI-only `CreateBuffEffect` 순회 드롭.

### 31. Script/CardEffectCommons/GiveEffect/GiveEffectToPlayer/ImmuneFromDPMinus.cs
- 판정: 정상
- 근거: #30과 동형(player 대상, `ImmuneFromDPMinusStaticEffect`). `PermanentCondition`(on-area + !CanNotBeAffected(cause) + caller), `CanUseCondition=>true`, `AddEffectToPlayer(None)` 대응. UI 드롭.

### 32. Script/CardEffectFactory/CanNotBeDeletedByEffect.cs
- 판정: 정상
- 근거: `CanNotBeDestroyedBySkillStaticEffect` 라인 verbatim — `SetUpCanNotBeDestroyedBySkillClass`, `CanNotBeDestroyedCondition`(PermanentCondition && CardEffectCondition), `PermanentCondition`(IsPermanentExistsOnField && !TopCard.CanNotBeAffected(canNotBeDestroyedBySkillClass) && caller), `CardEffectCondition` 동일. 헤더주석(:4)이 "InstanceId 어댑테이션" 언급하나 실제 코드(:50)는 AS-IS와 동일 `CanNotBeAffected(canNotBeDestroyedBySkillClass)`(ICardEffect) 전달 — 주석 stale, 코드 1:1.

### 33. Script/CutInProcess.cs
- 판정: 정상 (데드코드 STOP 스텁; 재오픈 조건부 이연)
- 근거: TO-BE는 미러 없이 STOP 스텁. 직접 검증: `grep CutInProcessCoroutine DCGO/...`는 정의 1건뿐 호출부 0, `CutInProcess` 타입 인스턴스화/필드 참조 전역 무결과 → 데드코드. DP==0 분기가 `new DestroyPermanentsClass(...).Destroy()` 의존인데 스텁 시점 미러 부재. AS-IS 발화경로 0인 데드 코루틴이라 미포팅 방어. 엄밀 1:1 완성 기준으론 미미러 이연 항목(재오픈 조건: DestroyPermanentsClass 미러+실호출부 등장).

### 34. Script/CardEffectFactory/KeyWordEffects/Iceclad.cs
- 판정: 정상
- 근거: `IcecladSelfStaticEffect`+`IcecladStaticEffect`, `effectName="Iceclad"`, `IcecladClass`+`SetUpIcecladClass`, isInheritedEffect 분기, 중첩 `PermanentCondition`(IsPermanentExistsOnBattleArea 게이트) 대응. substrate: ResolvePermanentOfThisCard. 로직 verbatim.

### 35. Script/CardEffectFactory/KeyWordEffects/Rush.cs
- 판정: 정상
- 근거: #34와 동형. `RushSelfStaticEffect`+`RushStaticEffect`, `effectName="Rush"`, `RushClass`+`SetUpRushClass`, isInheritedEffect 분기, 중첩 PermanentCondition 대응. substrate 동일 치환. 로직 verbatim.

### 36. Script/CardEffectCommons/CanUseEffects/OnTrashDigivolutionCard.cs
- 판정: 정상
- 근거: `CanTriggerOnTrashSelfDigivolutionCard`/`CanTriggerOnTrashDigivolutionCard` 두 메서드, 로컬 `PermanentCondition`/`CardCondition`, 5중 중첩 가드(GetPermanentFromHashtable→TopCard!=null→permanentCondition→GetCardEffectFromHashtable→cardEffectCondition→GetDiscardedCardsFromHashtable→Count(...)>=1) 라인 단위 동일. 차이=substrate(using 제거, namespace 추가, partial→static partial).

### 37. Script/CardEffectCommons/CanUseEffects/OnTrashLinkCard.cs
- 판정: 정상
- 근거: `CanTriggerOnTrashSelfLinkCard`/`CanTriggerOnTrashLinkCard`, `PermanentCondition`에서 `permanent.LinkedCards.Contains(card)` 사용까지 #36과 동일 구조 1:1. Self 오버로드 LinkedCards 참조·코어 가드 체인 대응. substrate 차이만.

### 38. Script/CardEffectCommons/CanUseEffects/OnTrashLinkedCard.cs
- 판정: 정상
- 근거: `CanTriggerOnTrashSelfLinkedCard`/`CanTriggerOnTrashLinkedCard`. #37과 동일 `LinkedCards.Contains(card)` 로직이며 region 주석("linked card")만 다르고 본문 1:1. substrate 차이만.

### 39. Script/CardEffects/ChangeBaseDPClass.cs
- 판정: 정상
- 근거: 필드 `_changeDPFunc`/`_permanentCondition`/`_isUpDown`/`_isMinusDP`, `SetUpChangeBaseDPClass`, `GetDP`(중첩 null·TopCard·PermanentCondition 가드), `PermanentCondition`/`IsUpDown`/`IsMinusDP` 대응. 인터페이스 `ICardEffect, IChangeBaseDPEffect` 유지. 차이=namespace/using.

### 40. Script/CardEffects/ChangeDPClass.cs
- 판정: 정상
- 근거: `_changeDP`(AS-IS도 동일 필드명, BaseDP판과 다름), `SetUpChangeDPClass`, `GetDP`, `PermanentCondition`/`IsUpDown`/`IsMinusDP` 1:1. `IChangeDPEffect` 유지. substrate 차이만.

### 41. Script/CardEffectFactory/KeyWordEffects/Collision.cs
- 판정: 정상
- 근거: `CollisionSelfStaticEffect`/`CollisionStaticEffect`, 로컬 `PermanentCondition`/`CanUseCondition`, `SetUpICardEffect("Collision",...)`/`SetUpCollisionClass`/`SetIsInheritedEffect`/`SetIsLinkedEffect` 순서 동일. 유일 변경 `PermanentOfThisCard()`→`ResolvePermanentOfThisCard(card)`(PermanentView 반환 substrate, 동일 대상 해석).

### 42. Script/CardEffectCommons/KeyWordEffects/Save.cs
- 판정: 정상
- 근거: `CanActivateSave`(IsTopCardInTrashOnDeletion && HasMatchConditionPermanent), `SaveProcess`의 maxCount=Math.Min(1, MatchConditionPermanentCount(...)), `SelectPermanentEffect.SetUp`(selectPlayer=card.Owner, canNoSelect:true, canEndNotMax:false, Mode.Custom), `SetUpCustomMessage(...customText:"that will get a digivolution card", CanSelectDigimon:false, CanSelectTamer:true), 선택 후 AddDigivolutionCardsBottom 대응. IEnumerator→Task, `TrashHandCard.gameObject` UI 토글 제거, 스캔 스코프용 card 파라미터 추가(전용 오버로드), AddDigivolutionCardsBottom 2번째 인자 activateClass→`activateClass?.EffectSourceCard?.InstanceId`(일관 substrate 번역). 분기 누락 없음.

### 43. Script/CardEffects/ChangeCardDPClass.cs
- 판정: 정상
- 근거: `_changeDPFunc`/`_cardSourceCondition`, `SetUpChangeCardDPClass`, `GetDP(int, CardSource)`(null·CardCondition 가드, Permanent판과 달리 TopCard 가드 없음 — AS-IS도 없음), `CardCondition`/`IsUpDown`/`IsMinusDP` 1:1. `IChangeCardDPEffect` 유지. substrate 차이만.

### 45. Script/CardEffectCommons/KeyWordEffects/Jamming.cs
- 판정: 정상
- 근거: `GainJamming`의 4중 가드(target null/battle-area/activateClass null/EffectSourceCard null), `PermanentCondition`/`CanUseCondition`(CanNotBeAffected(activateClass) 반전), `CardEffectFactory.JammingStaticEffect(...)`, `AddEffectToPermanent(...timing: EffectTiming.None)` 1:1. IEnumerator→Task, 말미 `CreateBuffEffect` VFX만 제거. 분기 누락 없음.

### 46. Script/CardEffectCommons/KeyWordEffects/Scapegoat.cs
- 판정: 정상
- 근거: `CanActivateScapegoat`(IsPermanentExistsOnBattleArea && HasMatchConditionPermanent) 대응(스코프용 permanent.TopCard 추가·null 가드). `ScapegoatProcess`의 초기 가드(permanent/TopCard null), HasMatchConditionPermanent 게이트, SelectPermanentEffect.SetUp(maxCount:1, canNoSelect:false, selectPlayer=owner), SetUpCustomMessage("Select 1 Digimon to delete."), DeletePeremanentAndProcessAccordingToResult(...successProcess: willBeRemoveField=false) 대응. `HideDeleteEffect()` UI substrate 제거. AS-IS `owner=permanent.TopCard.Owner` 별도계산 vs TO-BE 직접 전달 동일 값. 등가.

### 47. Script/CardEffectFactory/CanNoReturnToDeck.cs
- 판정: 정상
- 근거: `CannotReturnToDeckStaticEffect` 6파라미터, `new CannotReturnToLibraryClass()`, `SetUpICardEffect(effectName,...)`/`SetUpCannotReturnToLibraryClass`, isInheritedEffect 분기, 로컬 `CanUseCondition`/`PermanentCondition`(IsPermanentExistsOnBattleArea && !CanNotBeAffected(cannotReturnToLibraryClass) && permanentCondition)/`CardEffectCondition` 1:1. 헤더 InstanceId 언급 있으나 본문은 AS-IS와 동일 효과 인스턴스 전달.

### 48. Script/MainPhaseAction/PlayCardAction.cs
- 판정: 정상
- 근거: 필드 5종(CardIndex/TargetFrameID/JogressEvoRootsFrameIDs/BurstTamerFrameID/AppFusionFrameIDs)+값-생성자, `Execute`가 `stateMachine.SetPlayCard(CardIndex, TargetFrameID, JogressEvoRootsFrameIDs, BurstTamerFrameID, AppFusionFrameIDs)` 호출 1:1. 제거된 byte[] 생성자·Deserialize·Serialize는 Photon/IGamePacket 네트워크 transport로, base `MainPhaseAction`가 AS-IS IGamePacket+abstract Serialize/Deserialize를 체계적으로 벗기고(TO-BE base는 abstract Task Execute만) 헤드리스 transport=TurnFlowDriver로 대체됨을 base 파일 실독 확인 — 계층 일관 substrate 제거. Execute void→Task도 base 시그니처 부합.

### 49. Script/CardEffectCommons/CanUseEffects/PermanentEnterField/PermanentEnterField.cs
- 판정: 정상
- 근거: `CanTriggerOnEnterField`(→`CanTriggerOnPermanentEnterField`에 permanent.cardSources.Contains(card) 위임), `CanTriggerOnPermanentEnterField`의 IsEvolution==isEvolution 게이트, GetHashtablesFromHashtable 순회, GetPermanentFromHashtable/permanentCondition/GetRootFromHashtable, `rootCondition==null || rootCondition(root) || root==SelectCardEffect.Root.None` 3중 OR 문자 단위 동일. substrate(using/namespace)만.

### 50. Script/CardEffectCommons/KeyWordEffects/Reboot.cs
- 판정: 정상
- 근거: `GainReboot`의 4중 가드, `PermanentCondition`/`CanUseCondition`, `CardEffectFactory.RebootStaticEffect(...)`, `AddEffectToPermanent(...EffectTiming.None)`가 Jamming과 동형 1:1. IEnumerator→Task, 말미 `CreateBuffEffect` VFX만 제거. 분기 누락 없음.

### 51. Script/CardEffectFactory/CanNotBeTrashedByEffect.cs
- 판정: 정상
- 근거: `CanNotBeTrashedBySkillStaticEffect`, `new ImmuneStackTrashingClass()`, `SetUpICardEffect`/`SetUpImmuneFromStackTrashingClass(PermanentCondition, CardEffectCondition)`, isInheritedEffect 분기, 로컬 세 함수 1:1. `PermanentCondition`이 IsPermanentExistsOnField(다른 파일의 OnBattleArea 아님 — AS-IS도 OnField) 사용까지 동일. 본문 CanNotBeAffected(canNotBeTrashedBySkillClass)도 AS-IS 그대로.

### 52. Script/CardEffectFactory/CanNotReturnToHand.cs
- 판정: 정상
- 근거: `CannotReturnToHandStaticEffect`, `new CannotReturnToHandClass()`, `SetUpICardEffect`/`SetUpCannotReturnToHandClass`, isInheritedEffect 분기, `PermanentCondition`(IsPermanentExistsOnBattleArea && !CanNotBeAffected(cannotReturnToHandClass) && permanentCondition)/`CardEffectCondition` 1:1. #47/#51과 동형이며 본문 AS-IS 그대로(헤더 InstanceId 언급과 무관하게 효과 인스턴스 전달).

### 53. Script/CardEffectFactory/ImmuneFromDPMinus.cs
- 판정: 정상
- 근거: `ImmuneFromDPMinusStaticEffect` 7파라미터, `SetUpICardEffect`/`SetUpImmuneFromDPMinusClass`/`SetIsInheritedEffect(true)` 순서 동일. `CanUseCondition`(condition null-단락), `PermanentCondition`(IsPermanentExistsOnBattleArea→CanNotBeAffected→permanentCondition 3중), `CardEffectCondition` 대응. 헤더 주석은 InstanceId 적응 예고하나 실제 코드(:37)는 AS-IS(:30)와 동일하게 객체 immuneFromDPMinusClass 전달.

### 54. Script/CardEffects/CanSuspendByDigisorptionClass.cs
- 판정: 정상
- 근거: 인터페이스 `ICardEffect, ICanSuspendByDigisorptionEffect`, 필드 3(PermanentCondition/CardEffectCondition/_CheckAvailability), `SetUpCanSuspendByDigisorptionClass`, `canSuspendDigisorption`의 5중 중첩 null·조건 게이트, `isCheckAvailability` 라인 단위 동일(주석 제외 바이트 동일).

### 55. Script/CardEffectCommons/GiveEffect/GiveEffectToPlayer/ChangeCardDP.cs
- 판정: 정상
- 근거: AS-IS `IEnumerator ChangeSecurityDigimonCardDPPlayerEffect`(guard: activateClass/EffectSourceCard/changeValue==0)를 TO-BE는 async Task 오버로드 + ...Impl로 분해. guard 3·isUpValue/effectName 문자열·Condition(true)·CardCondition(cardCondition null-단락)·ChangeSecurityDigimonCardDPStaticEffect 6인자·AddEffectToPlayer(None) 보존. IEnumerator→Task는 실제 yield 대기 없는 동기 코루틴이라 등가.

### 56. Script/CardEffectCommons/MinMax_DP_Cost_Level/Cost/IsMaxCost.cs
- 판정: 정상
- 근거: `IsMaxCost` 가드 6(null, TopCard, Owner!=owner, 존재, Digimon||Tamer, HasPlayCost)+IsDigimonOnly 분기, cost Max 비교(costs.Count>=1 && GetCostItself==costs.Max()) 대응. `GetNonMaxCostPermanents`의 maxCost(HasPlayCost?GetCostItself:-1)·`!HasPlayCost || <maxCost` 필터 동일. 차이: AS-IS `IsPermanentExistsOnOwnerBattleArea(permanent, TopCard)`→TO-BE `IsPermanentExistsOnBattleArea(permanent)`이나 직전 OwnerId==owner 선검증으로 등가; Player owner→HeadlessPlayerId+GetCards(owner,BattleArea) substrate. AS-IS `.Where(TopCard!=null)`는 헤드리스 Permanent가 항상 TopCard 해석되어 생략 무해.

### 57. Script/CardEffectCommons/KeyWordEffects/Ascension.cs
- 판정: 정상
- 근거: `CanTriggerAscension`/`CanTriggerPermanentAscension`/`CanActivateAscension` 3위임(OnDeletion/OnPermanentDeleted 게이트) 동일. `AscensionProcess` 코루틴→Task: 메시지 2문자열·SelectionElement Yes/No(spriteIndex 0/1)·SetBoolSelection·WaitForEndSelect·SelectedBoolValue·AddSecurityCard(card,true) 대응. 적응: AS-IS `card.Owner.CanAddSecurity(activateClass)`→`new Player(...).CanAddSecurity(activateClass?.EffectSourceCard?.InstanceId)` substrate CanNotBeAffected 패턴 일치.

### 58. Script/CardEffectFactory/CanNotBeAttacked.cs
- 판정: 정상
- 근거: `CanNotBeAttackedSelfStaticEffect` 5파라미터, `CanUseCondition`(IsExistOnBattleAreaDigimon→condition), `DefenderCondition`(IsPermanentExistsOnBattleArea→attacker==PermanentOfThisCard()), 종단 `CanNotAttackStaticEffect` 6인자 동일. 유일 차이 `PermanentOfThisCard()`→`ResolvePermanentOfThisCard(card)`.

### 59. Script/CardEffectFactory/CanNotBeBlocked.cs
- 판정: 정상
- 근거: `CanNotBeBlockedStaticSelfEffect` 5파라미터, `CanUseCondition`·`AttackerCondition`(IsPermanentExistsOnBattleArea→==PermanentOfThisCard())·종단 `CanNotBlockStaticEffect`(attackerCondition=로컬, defenderCondition=인자 그대로) 동일. `PermanentOfThisCard()`→`ResolvePermanentOfThisCard(card)` 적응만.

### 60. Script/CardEffects/ChangeLinkCostClass.cs
- 판정: 정상
- 근거: 인터페이스·필드 5(_changeCostFunc/_cardSourceCondition/_permanentCondition/_rootCondition/_isUpDown)·`GetCost`의 6항 AND 게이트(cardSource!=null && CardCondition && PermanentCondition && changeCostFunc!=null && rootCondition!=null && rootCondition(root))·`CardCondition`/`PermanentCondition`/`IsUpDown` 바이트 동일(주석 제외).

### 61. Script/CardEffectCommons/CanUseEffects/WhenPermanentWouldDigivolve.cs
- 판정: 정상
- 근거: `CanTriggerWhenPermanentWouldDigivolveOfCard`의 로컬 `PermanentCondition`(permanent==PermanentOfThisCard()→ResolvePermanentOfThisCard(card) 적응)·`CanTriggerWhenPermanentWouldDigivolve`의 IsEvolution 게이트→GetCardFromHashtable→cardCondition→GetPermanentsFromHashtable→Filter(null·TopCard)·Some(permanentCondition) 라인 단위 동일.

### 62. Script/ShowPhaseObject.cs
- 판정: 정상 (완전 스텁, 순수 UI)
- 근거: AS-IS는 MonoBehaviour 순수 UI로 게임 상태 미변경·표시만 — OnSprite/OffSprite 로드, Update 프레임 스로틀(count/UpdateFrame), ShowPhase(turnStateMachine.DoneStartGame 읽어 SetActive), PhaseIcon.SetUpPhaseIcon(phase 일치 시 image.sprite·isYou 분기·SetActive) 전부 Sprite/Image/gameObject 프레젠테이션. 규칙·상태변이 0이라 미러 대상 부재→스텁 정당. 로직 소실 아님.

### 63. Script/CardEffectFactory/KeyWordEffects/Save.cs
- 판정: 정상
- 근거: `SaveEffect`의 `new ActivateClass()`·`SetUpICardEffect("Save",...)`·`SetUpActivateClass(...,-1,true,DataBase.SaveEffectDiscription())`·로컬 `CanSelectPermanentCondition`(IsPermanentExistsOnOwnerBattleArea→IsTamer→!IsToken)·`CanUseCondition`(CanTriggerOnDeletion)·ActivateCoroutine(SaveProcess 위임) 동일. `CanActivateCondition`이 AS-IS 2인자→TO-BE 3인자(card 추가)는 미러 HasMatchConditionPermanent 스코프-스캔 오버로드가 CardSource 컨텍스트 요구해 threaded(Commons/KeyWordEffects/Save.cs 헤더 문서화·대응 정의 실존 확인). 코루틴→Task 순수 위임.

### 64. Script/CardEffectFactory/CanNotBeDeletedByBattle.cs
- 판정: 정상
- 근거: `CanNotBeDestroyedByBattleStaticEffect` 시그니처(canNotBeDestroyedByBattleCondition Func 4-arg, permanentCondition, isInheritedEffect, card, condition, effectName, isLinkedEffect=false)·`SetUpCanNotBeDestroyedByBattleClass`·SetIsInheritedEffect/SetIsLinkedEffect 분기·CanUseCondition·PermanentCondition(IsPermanentExistsOnBattleArea→!CanNotBeAffected→permanentCondition) 동일. 코드는 AS-IS(:35)와 동일 객체 전달.

### 65. Script/CardEffects/CanNotBeDestroyedByBattleClass.cs
- 판정: 정상
- 근거: 인터페이스·`SetUpCanNotBeDestroyedByBattleClass`·필드 2·`CanNotBeDestroyedByBattle`(condition!=null→IsPermanentExistsOnBattleArea→PermanentCondition→4-arg condition)·`PermanentCondition`(_permanentCondition!=null→존재→평가) 바이트 동일.

### 66. Script/CardEffectCommons/CanUseEffects/WhenRemoveField.cs
- 판정: 정상
- 근거: `CanTriggerWhenRemoveField`(cardSources.Contains(card) 람다)·`CanTriggerWhenPermanentRemoveField`(GetPermanentsFromHashtable→Count(null·TopCard·condition)>=1)·`CanTriggerWhenTopCardTrashed`(GetCardSourcesFromHashtable→Count>=1) 3종 바이트 동일.

### 67. Script/CardEffectCommons/MinMax_DP_Cost_Level/Level/IsMinLevel.cs
- 판정: 정상
- 근거: `IsMinLevel` 가드(null/TopCard/Owner!=owner/존재-Digimon/HasLevel)+Levels Min 비교(>=1 && Level==Min), `IsMinLevelBoard`(양 플레이어 Flat→HasLevel→Level→Min) 대응. AS-IS `Players.Map(GetBattleAreaDigimons).Flat()`→TO-BE `context.TurnController.Current.PlayerOrder` 순회+GetCards substrate. `IsPermanentExistsOnOwnerBattleAreaDigimon`→`IsPermanentExistsOnBattleAreaDigimon` 명칭 차이는 직전 OwnerId==owner 선검증으로 등가; `.Where(IsDigimon && HasLevel)`의 IsDigimon는 GetBattleAreaDigimons가 이미 digimon만 반환하던 것의 명시화라 등가.

### 68. Script/CardEffectCommons/GiveEffect/GiveEffectToPlayer/ChangeDigivolutionCost.cs
- 판정: 정상
- 근거: `ChangeDigivolutionCostPlayerEffect` 6파라미터, guard 2(activateClass/EffectSourceCard null→null 반환), 로컬 Condition/PermanentCondition/CardCondition/RootCondition(각 null-단락), `ChangeDigivolutionCostStaticEffect` 8인자(changeValue/permanentCondition/cardCondition/rootCondition/isInheritedEffect=false/card/condition/setFixedCost), 종단 GetCardEffectByEffectTiming(timing: EffectTiming.None, ...) 동일. 팩토리 제네릭 `ChangeDigivolutionCostStaticEffect<int>` 호출이나 인자·타이밍·반환 1:1 보존이라 팩토리 substrate 차이.

### 69. Script/CardEffects/CannotReduceCostClass.cs
- 판정: 정상
- 근거: 인터페이스·`SetUpCannotReduceCostClass`·필드 3(_playerCondition/_targetPermanentsCondition/_cardCondition)·`CannotReduceCost`의 7중 중첩 null·조건 게이트(playerCondition→player!=null→평가→targetPermanentsCondition→평가→cardSource!=null→cardCondition→평가) 바이트 동일. Player 타입 미러 Player 유지.

### 72. Script/CardEffectFactory/CanNotSuspend.cs
- 판정: 정상
- 근거: `CantSuspendStaticEffect(Func<Permanent,bool>, bool, CardSource, Func<bool>, string)` 시그니처, `SetUpICardEffect`/`SetUpCanNotSuspendClass(PermanentCondition:)`, isInheritedEffect→SetIsInheritedEffect(true) 분기, 로컬 `CanUseCondition`(condition==null||condition()) 및 `PermanentCondition`(IsPermanentExistsOnBattleArea→!TopCard.CanNotBeAffected(canNotUnsuspendClass)→permanentCondition 3중) 라인 단위 동일. 헤더는 InstanceId 적응 언급하나 실제 코드(:39)는 AS-IS와 동일 클래스 인스턴스 전달. substrate=namespace/using만.

### 73. Script/CardEffectFactory/CanNotUnsuspend.cs
- 판정: 정상
- 근거: `CantUnsuspendStaticEffect` 1:1. AS-IS 특이점(파일명 Unsuspend인데 셋업 메서드 `SetUpCanNotUntapClass`)까지 TO-BE(:22) 그대로 보존. `CanNotUnsuspendClass` 반환, 중첩 조건 동일.

### 74. Script/CardEffectFactory/KeyWordEffects/Training.cs
- 판정: 정상
- 근거: `TrainingEffect(CardSource)`의 `SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, DataBase.TrainingEffectDiscription())`, `CanActivateSuspendCostEffect(card, true)`, Suspend 후 라이브러리 top카드 facedown 바텀 부여 보존. substrate: (1) IEnumerator→async Task, (2) `SuspendPermanentsClass(list, CardEffectHashtable(activateClass)).Tap()`→`(list, activateClass, isBlock:false).Tap()` — AS-IS Tap()이 hashtable에서 IsBlock 파생하는데 CardEffectHashtable(activateClass)엔 block 키 없어 false이므로 isBlock:false 충실(CardController.cs 대조). (3) `card.Owner.LibraryCards`→`new Player(...).LibraryCards` 스냅샷. (4) AddDigivolutionCardsBottom(...,activateClass,...)→...?.EffectSourceCard?.InstanceId. **AS-IS의 `LibraryCards.Count > 0` 가드 보존.** (주의: 이 팩토리 AS-IS에는 가드가 존재하며, 대응하는 #86 TrashClass AS-IS에는 없음 — #86 참조.)

### 76. Script/CardEffectFactory/CanNotBeDeleted.cs
- 판정: 정상
- 근거: `CanNotBeDestroyedStaticEffect(...)` 1:1. `SetUpCanNotBeDestroyedClass(permanentCondition: PermanentCondition)`(소문자 명명 인자까지), `!TopCard.CanNotBeAffected(canNotBeDestroyedClass)` 중첩, isInheritedEffect 분기 라인 단위 동일. 반환형 `CanNotBeDestroyedClass`.

### 77. Script/CardEffectFactory/CanNotBeRemoved.cs
- 판정: 정상
- 근거: `CanNotBeRemovedStaticEffect(...)` 1:1. `SetUpCanNotBeRemovedClass(permanentCondition:)`, `CanNotBeRemovedClass` 반환, 중첩 PermanentCondition/CanUseCondition, isInheritedEffect 분기 동일. #76과 대칭 구조.

### 78. Script/CardEffectCommons/CanUseEffects/OnAddDigivolutionCards.cs
- 판정: 정상
- 근거: `CanTriggerOnAddDigivolutionCard(Hashtable, Func<Permanent,bool>, Func<ICardEffect,bool>, Func<CardSource,bool>)` 본문 바이트-동일: hashtable null→GetPermanentFromHashtable→TopCard→permanentCondition→GetCardEffectFromHashtable→cardEffectCondition→GetCardSourcesFromHashtable→Count(...)>=1 5중 중첩 동일. 유일 차이=`public partial`→`public static partial`(다른 Commons와 일관, substrate).

### 79. Script/CardEffectCommons/CanUseEffects/OnAttack.cs
- 판정: 정상
- 근거: `CanTriggerOnAttack(Hashtable, CardSource)`=`CanTriggerOnPermanentAttack(hashtable, permanent => permanent.cardSources.Contains(card))` 위임 동일, `CanTriggerOnPermanentAttack`의 "AttackingPermanent" 키·is Permanent 캐스트·TopCard·permanentCondition 중첩 동일. static partial 차이만.

### 80. Script/CardEffectCommons/CanUseEffects/WhenUseOption.cs
- 판정: 정상
- 근거: `CanTriggerWhenOwnerUseOption`의 로컬 CardCondition(cardSource.Owner==card.Owner && cardCondition 위임)→CanTriggerWhenUseOption 위임, `CanTriggerWhenUseOption`의 GetCardFromHashtable→cardCondition→"Cost" 키·is int 캐스트→constCondition 라인 단위 동일.

### 81. Script/CardEffects/ImmuneFromStackTrashingClass.cs
- 판정: 정상
- 근거: `ImmuneStackTrashingClass : ICardEffect, IImmuneFromStackTrashingEffect`, PermanentCondition/EffectCondition 프로퍼티, `SetUpImmuneFromStackTrashingClass`, `ImmuneStackTrashing(permanent, effect)`의 permanent/TopCard null→EffectCondition 반증→PermanentCondition 반증→return true 동일. 차이=미사용 using(UnityEngine/Photon/Photon.Pun) 제거만.

### 82. Script/CardEffects/InvertSAttackClass.cs
- 판정: 정상
- 근거: `InvertSAttackClass : ICardEffect, IInvertSAttackEffect`, `SetUpChangeSAttackClass(Func<Permanent,int,int>, Func<Permanent,bool>)`, 필드 _changeInvertFunc/_permanentCondition, `InversionValue`(PermanentCondition 시 _changeInvertFunc(permanent, invertValue)), `PermanentCondition` 3중 null 동일. 차이=UnityEngine/Unity.Mathematics using 제거만. (주: 클래스 자체는 존재하나, 이를 player-scope로 부착하는 grant 래퍼는 #13에서 소실.)

### 83. Script/CardEffectFactory/KeyWordEffects/ArmorPurge.cs
- 판정: 정상
- 근거: `ArmorPurgeEffect(CardSource)`의 `SetUpActivateClass(..., -1, true, DataBase.ArmorPurgeEffectDiscription())`, `CanUseCondition`(IsExistOnBattleArea && CanTriggerWhenRemoveField), `CanActivateArmorPurge(card)`, ActivateCoroutine=ArmorPurgeProcess(card) 위임 동일. 적응: (1) coroutine→비-async Task(순수 위임 반환형 스왑), (2) `SetHashString($"ArmorPurge_{card.CardID}")`→`{card.CardNumber}` — AS-IS CardSource.CardID(_cEntity_Base.CardID)는 콜렉터 카드번호 문자열("BT1-054" 등)이고 TO-BE CardSource엔 CardID 프로퍼티 없이 동일 값을 CardNumber(Definition.CardNumber)가 보유→해시 문자열 바이트-동일. 정당한 프로퍼티명 번역.

### 84. Script/CardEffectFactory/TreatAsDigimon.cs
- 판정: 정상
- 근거: `TreatAsDigimonStaticEffect(...)`의 `effectName="Also treat as Digimon"`, `SetUpTreatAsDigimonClass(permanentCondition:)`, isInheritedEffect 분기 동일. 특기: 이 PermanentCondition은 다른 static 이펙트(#72/#76/#77)와 달리 CanNotBeAffected 체크가 없고 IsPermanentExistsOnBattleArea && permanentCondition만 검사 — TO-BE가 이 AS-IS 차이를 그대로 보존(누락/추가 없음).

### 85. Script/CardEffects/AddJogressConditionClass.cs
- 판정: 정상
- 근거: `AddJogressConditionClass : ICardEffect, IAddJogressConditionEffect`, _getJogressCondition 프로퍼티+`SetUpAddJogressConditionClass`, `GetJogressCondition`의 cardSource/_getJogressCondition/jogressCondition/elements null체크 후 .Map으로 JogressConditionElement 재생성(로컬 EvoRootCondition=IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, cardSource) && element.EvoRootCondition 위임, selectMessage: element.SelectMessage), `new JogressCondition(newElements, jogressCondition.cost)` 반환 라인 단위 동일.

---

## 참고: substrate 번역 패턴(정상 판정에 반복 적용, AS-IS 근거 확인됨)
- `IEnumerator`/`StartCoroutine`/`yield` → `async Task`/`await`(yield 대기 없는 동기 코루틴은 반환형 스왑만).
- `Mathf.Abs`/`Unity.Mathematics` → `System.Math`(값 등가).
- `card.PermanentOfThisCard()` → `ICardEffect.ResolvePermanentOfThisCard(card)`(PermanentView 해석 브릿지).
- HashString/CanNotBeAffected 등 identity 인자: `CardID`/live ref → `CardNumber`/`InstanceId`(동일 값·동일 대상, 다수 파일 대조로 실증).
- `card.Owner.Xxx`(live Player) → `new Player(context, ownerId).Xxx`(스냅샷).
- UI/VFX 사이드이펙트(`CreateBuffEffect`/`CreateDebuffEffect`/`HideDeleteEffect`/gameObject 토글) 드롭 — 게임 상태 변경 아님.
- Photon/IGamePacket Serialize/Deserialize transport 제거 — base 계층에서 TurnFlowDriver로 일관 대체.
- `partial`→`static partial`(Commons 파일 일관).
- 다수 factory 파일 헤더 주석의 "CanNotBeAffected→InstanceId 어댑테이션" 언급은 stale — 실제 코드는 AS-IS와 동일하게 효과 인스턴스 전달(코드가 주석보다 충실).
