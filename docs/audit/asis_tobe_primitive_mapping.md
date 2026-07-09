# AS-IS ↔ TO-BE 프리미티브 매핑 정의서 (전수 감사)

- 🔜 **진행 예정 대상 워크리스트**: [primitive_remaining_worklist.md](primitive_remaining_worklist.md)
- 작성: 2026-07-08. 방법: **문서·카탈로그 불신 — AS-IS(`DCGO/`)와 TO-BE(`src/`) 실소스를 심볼별로 직접 대조**(병렬 감사 에이전트 11, 코드에서 직접 리스트업).
- 목적: 개발된 프리미티브가 (1) AS-IS를 **1:1 미러**했는지, (2) **호출부가 없다는 이유로 스킵**한 부분이 있는지를 전수 판정하고, AS-IS 함수 ↔ TO-BE 함수 매핑을 확정한다.
- 파일 경로 약칭: **CPF** = `src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons/CardPortingFramework.cs`. AS-IS 파일은 전부 `DCGO/Assets/Scripts/Script/` 하위.

## 감사 기준 (두 렌즈)

- **LENS1 (1:1 미러)**: 구조 + 동작 재현. 결과-동일이라도 **구조가 다르면 FAIL**. 술어/파라미터를 AS-IS는 평가하나 TO-BE가 무시·하드코딩·평면화 = FAIL. 가드/조건/임계값/분기 누락 = FAIL.
- **LENS2 (호출부-없음 스킵)**: 분기/파라미터/오버로드/타이밍 누락, "현재 포팅 카드에만" 축소, `NotSupported`/스텁/seal(등록되나 소비자 미배선), 무시된 파라미터. **호출부 부재는 스킵 사유가 아님.**

판정: **PASS**(1:1) · **PARTIAL**(일부 미러/일부 이탈) · **FAIL**(구조·동작 상이) · **MISSING**(TO-BE 부재/스텁).

## 요약 (인벤토리 182 심볼, 코드 직접 추출)

| 원본 그룹 | 파일 | 수 | PASS | PARTIAL | FAIL(MISSING 포함) |
|---|---|---:|---:|---:|---:|
| CardEffectFactory | `CardEffectFactory.cs` | 41 | 2 | 14 | 25 |
| CardEffectCommons (비-토큰) | `CardEffectCommons.cs` | 22 | 6 | 9 | 7 |
| CardEffectCommons (토큰 플레이) | `CardEffectCommons.cs` | 18 | 15 | 2 | 1 |
| PermanentEffectFactory | `PermanentEffectFactory.cs` | 6 | 0 | 2 | 4 |
| 효과 본체 `*Class` | `Script/**` | 95 | 27 | 31 | 37 |
| **합계** | | **182** | **50** | **58** | **74** |

**진짜 1:1 = 50/182 (27%).** 그중 **완전 MISSING(어디에도 포트 없음/스텁+소비자 없음) ≈ 17종**, 나머지 FAIL은 대부분 술어 평면화·구조 이탈·미배선 seal.

> ⚠️ 판정 주의: 감사는 "구조 다르면 FAIL" 엄격 기준. FAIL 중 다수는 **결과-동일 modeling**(예: 타이밍-클래스 팩토리 → uniform `ActivatedEffect`/`CardEffectDefinition` 폴딩)으로, 능력은 존재하나 1:1 심볼이 없음. 실동작 버그(면역 과다·트리거 미발화·dead code)와 구별해 상환 우선순위(§ 하단)를 매겼다. 또한 일부 "가드 드롭"(CanAddMemory/CanAddSecurity)은 엔진 clamp로 등가 강제될 수 있어 **재대조 필요**로 표기.

---

## A. CardEffectFactory (41)

### PASS
| AS-IS | AS-IS loc | TO-BE | 비고 |
|---|---|---|---|
| UseRequirements | CardEffectFactory.cs:722 | `UseRequirements` CPF:5786 | ignore-color self-scope + battle/breeding·Digimon/Tamer·TopCard 게이트 1:1 |
| GetJogressConditionClass | :752 | `JogressEffect` CPF:6551 | 술어쌍+owner/Digimon wrap+canUseCondition 미러; cost-drop은 AS-IS quirk 충실 |

### FAIL — 타이밍-클래스 계열 (1:1 팩토리 없음 → uniform `ActivatedEffect`/`CardEffectDefinition` 카드별 인라인)
`ActivateClass`(:910, PARTIAL: ActivatedEffect가 hashValue·isSecurity·isSkippable·isInherited·isLinked 드롭), **ActivateClassesForSharedEffects**(:828, FAIL: 공유 hashValue once-per-turn 재현 불가 → 각 트리거 독립 발동), OnPlayClass(:978), OnDeletionClass(:1078), CounterClass(:1206, isCounterEffect 플래그 없음), EndOfAttackClass(:1170), EndOfYourTurnClass(:1320), EndOfYourOpponentsTurnClass(:1392), EndOfAllTurnsClass(:1430), AllTurnsClass(:1447), OpponentsTurnClass(:1409), TurnTimingClass(:1248), StartOfYourTurnClass(:1286), StartOfYourMainPhaseClass(:1303, main-phase→OnStartTurn 붕괴 의심), StartOfYourOpponentsMainPhaseClass(:1375), StartOfOpponentsTurnClass(:1358), YourTurnClass(:1337), WhenAttackingClass(:1044), WhenDigivolvingClass(:1011), WhenLinkingClass(:1111), WhenMovingClass(:940). → 공통: IsExistOnBattleArea trigger/activate 2단계 + additionalUse/additionalActivate 분리 게이트 **미중앙화**.

### FAIL — 동작/구조 이탈
| AS-IS | AS-IS loc | TO-BE | 이탈 |
|---|---|---|---|
| ~~Gain2MemoryOptionDelayEffect~~ **✅상환** | :470 | TrashSelfThenGainMemoryDelayEffect CPF | ~~무조건 +2 at OnStartTurn(오동작)~~ → **해소(2026-07-08)**: [Main]<Delay> 활성 = 자기 permanent trash(`DeletePeremanentAndProcessAccordingToResult`) → **성공 시에만** +2(AddMemory). 배틀에리어 가드(=PermanentOfThisCard). 리졸버 case 추가. 13 카드. 테스트 FAILa-09(2건) |
| ~~PlayMindLinkTamerFromDigivolutionCards~~ **✅상환** | :196 | ActivatedPlayFromUnderEffect CPF:6070 | ~~scope 확대·optional→mandatory·guard 드롭~~ → **해소(2026-07-08)**: `ActivatedPlayFromUnderEffect`에 `selfStackOnly` 추가(THIS 카드 stack만, AS-IS `card.PermanentOfThisCard()`) + `isOptional:true`(canNoSelect) + 후보 필터에 `CanPlayAsNewPermanent` 추가. ST2_15(전-owner·mandatory)는 기본값 유지. 테스트 FAILa-11(2건) |
| ~~PlaySelfDigimonAfterBattleSecurityEffect~~ **✅상환** | :285 | PlaySelfAtEndOfBattleSecurityEffect CPF | ~~즉시 플레이·deleteDigimon 소실~~ → **해소(2026-07-08)**: [Security] 효과가 **OnEndBattle 트리거 등록**(`PlaySelfAtEndOfBattleTriggerEffect`, 즉시 아님)로 지연 플레이 + `deleteDigimon`(UntilOwner/Opponent/EachTurnEnd) → 턴종료 self-delete 마커(`AddSelfDeleteEffect` 재사용) 배선. 리졸버 case 추가. 35 카드 대상. 테스트 FAILa-10(3건), G9-031 갱신 |
| SecurityClass | :1146 | (없음) | 범용 [Security] ActivateClass 팩토리 미포팅; 카드별만 |
| PlaceToSecurityEffect | :1497 | (없음) | OptionResolutionClass "used Option→security 대신 trash" 전면 미포팅 |

### PARTIAL (요약)
AddDetailClass(:1523→DisplayDetailEffect CPF:6072, permanentCondition+triggerEffect 드롭) · ActivateMainOptionSecurityEffect(:551→AddActivateMainOptionSecurityEffect CPF:10891, effectDiscription+[Main]→[Security] derivation 드롭) · DigiXrosEffectFromNames(:784→SpecialPlayRecipe CPF:6429, canTargetCondition·costReduction 무시) · PlaceSelfDelayOptionSecurityEffect(:512→PlayThisCardToBattleEffect, isPlayOption delay 배치 대체) · EoTLose3Memory(:1467→CPF:5981, TurnPlayerId==Owner 게이트 AS-IS엔 없음=과다제약) · Gain1MemoryTamerOpponentDigimonEffect(:63→CPF:5703, CanAddMemory·battle-area 드롭, 타이밍 OnStartMainPhase→OnStartTurn) · Gain1MemoryTamerOwnerDigimonConditionalEffect(:115→CPF:5944, 술어 폴딩 good이나 CanAddMemory 드롭+owner-turn 게이트 추가) · PlaySelfTamerSecurityEffect(:148→PlayThisCardToBattleEffect CPF:6262, CanPlayAsNewPermanent 게이트 미적용) · ReplaceBottom/TopSecurityWithFaceUpOption(Main)Effect(:645/599/684/622→ReplaceBottomSecurityWithFaceUpEffect CPF:3485, `CanAddSecurity` 게이트 드롭 ×4) · SetMemoryTo3TamerEffect(:11→TriggeredSetMemoryEffect CPF:6224, `CanAddMemory` 드롭).

---

## B. CardEffectCommons — 비-토큰 (22)

### PASS
PlaceDelayOptionCards(:113→CPF:7098) · AddThisCardToHand(:424→CPF:8340) · SuspendPeremanentAndProcessAccordingToResult(:437→CPF:6823) · DeletePeremanentAndProcessAccordingToResult(:463→CPF:6764) · BouncePeremanentAndProcessAccordingToResult(:489→CPF:6863) · DeckBouncePeremanentAndProcessAccordingToResult(:515→CPF:6903).

### FAIL
| AS-IS | AS-IS loc | TO-BE | 이탈 |
|---|---|---|---|
| TrashDigivolutionCardsAndProcessAccordingToResult | :541 | CPF:6927 | 명시 List<CardSource> 타깃→(count,isFromTop) 평면화; success가 int(trashed 리스트 아님) |
| ~~PlacePermanentInSecurityAndProcessAccordingToResult~~ **✅상환** | :644 | CPF | ~~isFaceUp 하드코딩 false + CanAddSecurity 누락(K2)~~ → **해소(2026-07-08)**: `isFaceUp` 파라미터 복원 + ZoneMover 직접호출 → **sink `AddToSecurityKind` 경유**로 전환(CanAddSecurity 제약 게이트 + faceUp + OnFaceUpSecurityIncreased 타이밍 모두 배선). 테스트 FAILa-PPS(3건) |
| DigivolveIntoHandOrTrashCard | :756 | CPF:8439 | **enum 부분 상환(2026-07-08)**: ~~IgnoreRequirement enum→bool~~ → **해소** — TO-BE `IgnoreRequirement` enum 신설 + cost 파이프라인(`DigivolutionCostHelpers`/`TryGetEvolutionCost`)에 `ignoreLevel`/`ignoreColor` 관통 + CanSelect가 enum별 적격성(Level=색유지, Color=레벨유지, All=우회). 테스트 FAILa-06(5건). **잔여**: cost-effect 파이프라인 가시성(reduce/fixed grants가 다른 코스트 수정효과에 보이게) = #5와 묶음 |
| DigivolveIntoExcecutingAreaCard | :1106 | CPF:9251 | 위와 동일(enum 해소, cost-visibility 잔여) |
| OptionMainEffect | :711 | (없음) | [Main] discriminator 미모델 → 전 OptionSkill 균일 |
| OptionSecurityEffect | :717 | (없음) | [Security] discriminator 미모델 |
| GetCardEffectByEffectTiming | :1402 | (없음) | 타이밍-게이트 effect-getter 패턴 부재(piecemeal binding) |

### PARTIAL (요약)
PlayPermanentCards(:23→CPF:8360, CanPlayAsNewPermanent에 cardEffect:null·isBreedingArea 드롭 — 게이트에 root/isBreedingArea 파라미터 자체 없음) · PlayOptionCards(:59→PlayOptionCardEffect CPF:6324, payCost+setAddSecurityEndOption 창 생략) · TrashDigivolutionCardsFromTopOrBottom(:675→CPF:6953, cardCondition 술어 드롭) · TrashLinkCards~(:567→CPF:6964, success int) · TrashSecurity~(:593→CPF:7000, success int, 삭제된 시큐리티 카드 리스트 소실) · TrashHand~(:619→CPF:7034, success cardToTrash 인자 드롭 + IDiscardHands 경로 아닌 generic) · ActivateMainOfOptionSide(:733→CPF:9280, 전 OptionSkill 해소·afterMainEffect/asEffectOfThisDigimon 부재) · AddActivateMainOptionSecurityEffect(:723→CPF:10891, OptionMainEffect==null 조기반환 드롭) · DrawAndDiscardCards(:1408→CPF:9153, 4 파라미터 생략).

---

## C. CardEffectCommons — 토큰 플레이 (18)

**PASS 15** (스펙 = AS-IS ContinuousController.CreateTokenData 일치): PlayAmon/Umon/Fujitsumon/KoHagurumon/Familiar/SelfDeleteFamiliar/VoleeZerdrucken/UkaNoMitama/WarGrowlmon/Taomon/Rapidmon/PipeFox/AthoRenePor/Hinukamuy/PetrificationToken (CPF:8935~8995). — 단, 전부 PlayToken 코어 갭 상속.
**PARTIAL 2**: PlayDiaboromonToken(CPF:8931, Form=Mega/Attr=Unknown 드롭) · PlayGyuukimonToken(CPF:8947, Form=Ultimate/Attr=Virus 드롭).
**FAIL 1 — PlayToken 코어**(:140→CPF:8867): (1) empty-frame 임계 가드 드롭(자리 없어도 플레이) (2) CanPlayAsNewPermanent 게이트 드롭 (3) defMeta가 Form+Attribute 미방출(TokenSpec 필드 존재하나 미사용). 자기-인정 "field-size 미모델".

---

## D. PermanentEffectFactory (6) — 전부 미배선 static binding(zero callers) = 호출부-없음 스킵 전형

| AS-IS | AS-IS loc | TO-BE | 판정/이탈 |
|---|---|---|---|
| DeleteSelfEffect | :11 | Binding `DeleteSelf` (PermanentEffectFactoryBinding.cs:375) | FAIL — cardEffect+CanNotBeAffected, deleteOnOwnturn/OpponentsTurn 분기(한쪽만 삭제 불가), IsExistOnBattleArea, CanActivate 전부 드롭; 0 callers |
| ~~DigimonEffectImmunity~~ **✅상환** | :51 | `DigimonEffectImmunity(Permanent)` PermanentEffectFactory.cs:61 | **해소(2026-07-08)**: AS-IS 형태 오버로드 추가 — `ContinuousImmunityEffect`(TargetPredicate=this-permanent, SkillCondition=상대+Digimon)로 미러; 기존 flattened binding-rule은 별도 유지(G3J-002 테스트). FAILa-03(5건) |
| ~~OptionEffectImmunity~~ **✅상환** | :80 | `OptionEffectImmunity(Permanent)` PermanentEffectFactory.cs:78 | **해소**: SkillCondition=상대+Option(!Digimon&&!Tamer). FAILa-03 |
| ~~CollisionEffect~~ **✅상환** | :131 | `CollisionEffect(Permanent,ICardEffect)` PermanentEffectFactory.cs | **해소**: AS-IS 형태 오버로드 — `CollisionStaticEffect`에 permanentCondition=this-permanent 위임(1:1) |
| AddDetailClass | :146 | Binding `Detail` (:444) | PARTIAL — detail/triggerEffect 유지, CanUse+PermanentCondition+SourceCard 드롭(display-only 저영향) |
| CanNotSwitchAttackTargetEffect | :109 | PermanentEffectFactory.cs:36 | PARTIAL — 실미러이나 CanNotBeAffected 드롭, topCard 생성시점 캡처(진화 시 stale) |

---

## E. 효과 본체 `*Class` (95)

### PASS (27)
AceOverflowClass(→AceOverflowGate) · ActivateClass(→ActivatedEffect) · AddAppFusionConditionClass · AddAssemblyConditionClass · AddLinkConditionClass · AddMaxTrashCountDigiXrosClass · AddMaxUnderTamerCountDigiXrosClass · ArmorPurgeClass(→DeDigivolveHelpers.ArmorPurgeTopAsync) · BlockerClass · CanNotBeDestroyedByBattleClass(4-arg battle 술어 1:1) · CanNotBeDestroyedClass · CanNotSuspendClass · CanNotSwitchAttackTargetClass · CanNotUnsuspendClass · CannotReturnToHandClass · ChangeBaseCardColorClass · ChangeCardColorClass · ChangeCardLevelClass · ChangePermanentLevelClass · ChangeTraitsClass · EmptyEffectClass(de facto) · IcecladClass · IgnoreColorConditionClass · ImmuneStackTrashingClass · MindLinkClass · PartitionClass · RebootClass.

### FAIL — 실동작 버그 (과다면역 / dead / 미발화) ★우선 상환
| AS-IS | TO-BE | 버그 |
|---|---|---|
| ~~CanNotBeDestroyedBySkillClass~~ **✅상환** | CPF:5729/9712 | ~~cardEffectCondition 미전달→과다면역~~ → **해소(2026-07-08)**: 팩토리에 permanentCondition+cardEffectCondition 배선(CannotReturnToHand 패턴), GainRestrictionToPermanent에 causingEffectPredicate 추가, sink `IsDeletionPreventedByContinuous`가 DeleteBySkill을 `IsRestrictedFromCause`로 원인술어 평가. 테스트 FAILa-01(3건) |
| ~~ImmuneFromDPMinusClass~~ **✅상환** | ImmuneFromDPMinusStaticEffect CPF:5829 | ~~모든 DP-minus 면역~~ → **해소(2026-07-08)**: 팩토리에 cardEffectCondition 배선; `NumericModifier`에 `SourceEntityId` 추가(ModifierHelpers) → `ContinuousDpGate`가 각 DP-감소 모디파이어의 소스에 면역 술어 평가(per-causing-effect, null=전면). Gain(target/player) 변형도 causing 술어 전달. 테스트 FAILa-02(3건) |
| ~~**InvertSAttackClass**~~ **✅상환** | InvertSAttackStaticEffect CPF | ~~invertDelta 누적되나 소비자 없음(dead)~~ → **해소(2026-07-09)**: `ModifierHelpers.Evaluate`가 invert 모디파이어 합을 clamp[-1,1]해 각 SAttack 변경의 **방향을 flip**(-1=감소→증가, +1=증가→감소, AS-IS `ChangeSAttackClass.GetSAttack` 미러). 2-pass(invertValue 먼저 산출). 테스트 FAILb-01(5건) |
| ~~**ImmuneFromDeDigivolveClass**~~ **✅상환** | ImmuneFromDeDigivolveStaticEffect CPF | ~~producer 없음(READ만)~~ → **해소(2026-07-09)**: producer 신설(연속 제약 `CannotBeDeDigivolvedKey`, self/player-scope+술어) + sink `DeDigivolveKind` 핸들러 `IsRestrictedFromCause` 검사 + CPF 직접 호출부 2곳에 공유 헬퍼 `IsDeDigivolveImmune`(uniform). 테스트 FAILb-02(2건) |
| **CanNotTrashFromDigivolutionCardsClass** ⚠️딥 잔여 | IsTrashProtected(metadata) | AS-IS는 **모든 필드 permanent 효과를 전역 스캔**해 매칭 source 보호(source 술어+causing 조건). 헤드리스 per-card 연속 스코프와 구조 상이 → 얕은 metadata-stamp론 술어 재현 불가. **전역 source-protection 스캔 인프라 필요**(fidelity 위해 얕은 뭉갬 회피, 별도 진행) |
| ~~**ReturnToLibraryBottomDigivolutionCardsClass**~~ **✅상환** | CPF | ~~트리거 미발화 + CanNotBeAffected 드롭~~ → **해소(2026-07-09)**: (1) `OnDigivolutionCardReturnToDeckBottom` 타이밍 신설(EffectTiming enum + TriggerTimings string + BroadcastTimings + AllTimings + EventBroadcastActivatedTimings — OnDigivolutionCardDiscarded 미러) + `RemoveSourcesAsync`가 Library 목적지에서 방출(+`ReturnSourcesAsync`에 gameEventQueue 관통). (2) sink return 경로에 `ContinuousImmunityGate.BlocksOpponentEffect`(=CanNotBeAffected) 가드. 테스트 FAILc-01(2건) |
| ~~CannotReturnToLibraryClass~~ **✅상환** | CannotReturnToDeckStaticEffect CPF:6000 | ~~cardEffectCondition 드롭→무조건 발동~~ → **해소(2026-07-08)**: static에 cardEffectCondition 배선(hand판 미러); sink는 이미 `IsRestrictedFromCause`로 읽음. Gain(target/player) 변형도 배선. 테스트 FAILa-04(3건) |
| CannotReduceCostClass | CanNotReduceCostStaticEffect CPF | **핵심 상환(2026-07-08)**: ~~targetPermanentsCondition 드롭→play도 과다차단~~ → **해소** — `CostReductionScope`(Both/Play/Digivolve) 신설, cost-kind 키(ImmuneFromPlay/DigivolutionCostReductionKey)로 `ResolvePlayCost`(play)/`ResolveDigivolutionCost`(digivolve)가 각각 필터(BT5_021="opponent can't reduce DIGIVOLUTION costs"가 play는 안 막음). 테스트 FAILa-05(4건). **playerCondition 완료(2026-07-08)**: `ContinuousPlayerScopeRestrictionEffect`에 scopeAnyPlayer 추가 → permanentCondition이 payer(p.OwnerId)까지 결정(BT5_021 "상대만" 표현). FAILa-05 opponent-scope 케이스 검증. 잔여=arbitrary targetPermanentsCondition(비-count 술어)만 배선 시 |

### ~~FAIL — name-only seal~~ → **✅오분류 정정(2026-07-09): 전부 LIVE**
~~ScapegoatClass · TreatAsDigimonClass · VortexCanAttackPlayersClass — producer 스텁~~ → **코드 직접 검증 결과 3건 모두 end-to-end 작동**: producer가 `ContinuousPlayerScopeKeywordEffect`(키워드 등록) 반환, 소비자(`ContinuousKeywordGate.IsDigimon`/`HasKeyword`/`DeletionReplacementGate.FindScapegoatSacrifice`)가 읽음. 검증 테스트 FAILb-verify(before=false→after=true). **감사가 과대-플래그**(사용자 원칙: 데이터 불신·코드 검증). PermFactory DeleteSelf/Collision(binding-rule 폼)도 `PermanentEffectFactoryBinding` 소비 + G3J-002 테스트 존재 = LIVE.

### FAIL — 완전 MISSING (스켈레톤/미구현, 소비자 없음)
CanAttackTargetDefendingPermanentClass · ~~CanNotBeRemovedClass~~ **✅상환(2026-07-09, FAILd-02)** · ~~CanNotMoveClass~~ **✅상환(2026-07-09, FAILd-03)** · CanNotPlayClass · CanNotPutFieldClass · ~~CanNotSelectBySkillClass~~ **✅상환(2026-07-09, FAILd-01)** · CanSelectAssemblyClass · CanSelectDigiXrosClass · CanSuspendByDigisorptionClass(→G11 연계) · ~~CannotIgnoreDigivolutionConditionClass~~ **✅상환(2026-07-09, FAILd-06)** · ~~ChangeBaseCardNameClass~~ **✅상환(2026-07-09, FAILd-08)** · ChangeCardLevelForAssemblyClass · ChangeCardNamesForDigiXrosClass · ~~ChangeEndTurnMinMemoryClass~~ **✅상환(2026-07-09, FAILd-07)** · ~~DontBattleSecurityDigimonClass~~ **✅상환(2026-07-09, FAILd-05)** · ~~DontHaveDPClass~~✅ · DeckTopBounceClass(직접-리스트 미러 없음, select 경로만).

### FAIL — 구조/술어 평면화
AddDetailClass(CPF:6072, permanentCondition+triggerEffect 드롭) · AddDigivolutionRequirementClass(범용 GetEvoCost+ignore/isCheckAvailability 미포팅) · AddSkillClass(범용 getEffects 스플라이스+limitedTiming 게이트 미포팅, 라이브 STOP BT1_104) · CanNotDigivolveClass(into-card 술어 드롭) · ChangeLinkMaxClass · ChangeSAttackClass(Func<Permanent,int,int> 변환+invert/isUpDown 드롭) · ChangeSAttackClass · CollisionClass(predicate-form 미포팅, self-keyword만) · DisableEffectClass(per-effect 술어→whole-card boolean) · SelectCardConditionClass(고급 술어 저장만·미평가) · SelectDigiXrosClass(ByPreSelectedList+substitution 드롭).

### PARTIAL (술어 일부 평면화 / 위임 / 특정 분기 드롭)
AddBurstDigivolutionConditionClass · AddDigiXrosConditionClass(costReduction 무시) · AddJogressConditionClass(cost quirk) · AddJogressLevelsClass(permanent→self) · CanNotAffectedClass(ICardEffect→CardSource) · CanNotAttackTargetDefendingPermanentClass · CannotAddMemoryClass · CannotAddSecurityClass · CannotBlockClass(Func<P,P>→분리) · ChangeBaseDPClass · ChangeCardDPClass · ChangeCostClass · ChangeDPClass · ChangeDPDeleteEffectMaxDPClass(ICardEffect 스코프 드롭) · ChangeCardNamesClass(append-only) · ChangeLinkCostClass(reduction-only) · DeckBottomBounceClass(sink 위임) · DestroyPermanentsClass(**CanBeDestroyedBySkill 가드 드롭** — latent 면역홀) · DrawClass(**OnDraw 트리거 본체서 미발화** — sink 확인要) · HatchDigiEggClass(sink 위임) · OptionResolutionClass(카드별 해체) · PlayCardClass(구조 분리) · PlayPermanentClass(구조 분리) · RevealLibraryClass(**IsBeingRevealed 미모델 → discard 트리거 과발화**) · RushClass(predicate 오버로드 드롭) · SelectAssemblyClass(field-permanent substitution 드롭) · SimplifiedSelectCardConditionClass(Mode.Custom 코루틴 드롭) · SuspendPermanentsClass(**DPWhenSuspended 미기록 + CanNotBeAffected 드롭 + already-suspended 미필터→OnTapped 재발화**) · TrainingClass(**suspend-cost OnTapped 미발화 + isFacedown 미전달**) · UseOptionClass(OptionResolution 루프 미재현).

---

## 상환 우선순위

### P0 — 실동작 버그 (틀린 결과 산출; 카드 배선과 무관하게 지금 잘못됨)
1. **CanNotBeDestroyedBySkillClass / ImmuneFromDPMinusClass** — cardEffectCondition 미전달로 **자기 효과·전체 효과까지 과다면역**. `CausingEffectPredicateKey`(이미 존재) 배선.
2. **InvertSAttackClass** — invert가 **dead**(소비자 없음). ResolveSecurityAttack에 InvertDelta 폴딩(AS-IS InvertSecutiryValue clamp[-1,1] 미러).
3. **~~ReturnToLibraryBottomDigivolutionCardsClass~~ ✅상환(2026-07-09, FAILc-01)** / **잔여: TrainingClass · SuspendPermanentsClass** — Training/Suspend는 트리거 미발화(OnTappedAnyone) + SuspendPermanents는 DPWhenSuspended 스냅샷 누락·already-suspended 재발화. sink suspend 경로에 이벤트+가드 배선 필요(별도 진행).
4. **RevealLibraryClass** — IsBeingRevealed 미모델로 WhenDiscardLibrary 과발화. (풀정보 모델이라 reveal 자체는 무의미하나 discard-window 제외 가드는 필요.)
5. **DestroyPermanentsClass/PlacePermanentInSecurity/Replace*Security/SetMemoryTo3/Gain* — CanBeDestroyedBySkill·CanAddSecurity·CanAddMemory 가드 드롭**. ⚠️ **재대조**: 일부는 엔진 clamp(MoveFromZoneTop·MemoryController.Clamp)로 등가 강제될 수 있음(fidelity_debt.md 기존 주장) — 확인 후 진짜 갭만 상환.

### P1 — 완전 MISSING, 실카드 수요 큼
6. ~~**CanNotSelectBySkillClass**~~ **✅상환(2026-07-09)** (AS-IS 39 카드, 핵심 untargetability) — `CanNotSelectBySkillStaticEffect` producer 신설(후보-스코프 연속 제약 `CannotBeSelectedBySkillKey` + causingEffectPredicate + scopeAnyPlayer) → `SelectPermanentEffect.BuildRequest`(AS-IS와 동일 단일 chokepoint, `Permanent.CanSelectBySkill` 미러)가 후보 풀에서 배제. 전 SetUp 호출부 7곳에 context 배선. gated(상대만) 지원. 테스트 FAILd-01(4건).
7. ~~**CanNotBeRemovedClass**(EX6_044/BT16_051)~~ **✅상환(2026-07-09)**: `CanNotBeRemovedStaticEffect` producer(연속제약 `CannotBeRemovedKey` + causingEffectPredicate) → sink 3개 return chokepoint(ReturnToHand·DeckTop·DeckBottom)에 검사(바운스+덱바운스 차단, **삭제 예외**=AS-IS "except by deletion"). FAILd-02(6건). **잔여**: ~~DontHaveDPClass~~ **✅상환(2026-07-09, FAILd-04)**, **CanNotMove/Play/PutFieldClass**, **~~ChangeEndTurnMinMemoryClass~~ **✅상환(2026-07-09, FAILd-07)****, **~~CannotIgnoreDigivolutionConditionClass~~ **✅상환(2026-07-09, FAILd-06)****, **~~DontBattleSecurityDigimonClass~~ **✅상환(2026-07-09, FAILd-05)****, **ChangeBaseCardName/ChangeCardLevelForAssembly/ChangeCardNamesForDigiXros**.
8. **ScapegoatClass / TreatAsDigimonClass / VortexCanAttackPlayersClass** — 소비자 게이트는 있으나 **부여 producer 스텁**. 부여 팩토리 배선(술어 저장).

### P2 — 구조/능력 갭 (결과-동일이나 1:1 심볼·조합성 부재)
9. **ActivateClassesForSharedEffects 공유 hashValue once-per-turn** — 다중 트리거 공유 캡. 현재 각 트리거 독립 → 중복 발동 가능. 공유-hash 모델 필요.
10. **타이밍-클래스 계열 uniform 프리미티브** — TurnTiming/When*/StartOf*/EndOf* 2단계 게이트 중앙화(asis-uniform-activateclass 설계).
11. **IgnoreRequirement enum 복원**(DigivolveInto* bool→enum), **ChangeCost/ChangeDP/ChangeSAttack/ChangeLinkMax Func-transform + IsUpDown/IsMinusDP** 통합 프리미티브.
12. **Play*/Trash*ProcessAccordingToResult success 콜백에 실제 카드 리스트 복원**(현재 int).

### P3 — 특수플레이/선택 조합성
13. SelectDigiXros/SelectAssembly substitution 분기, Select(Simplified)CardCondition 고급 술어 실평가, DigiXros/Jogress cost, PlayToken empty-frame+Form/Attribute, UseOption의 OptionResolution 루프.

> **주의**: 위 상환은 AS-IS 미러가 기본. 구조를 AS-IS와 다르게 가야 유리한 지점(예: 타이밍 폴딩 유지 vs 팩토리 신설)은 **착수 전 확답** 후 진행([[check-asis-before-implementing]]).

---

## FAIL 74건 세분: 결과동일 FAIL vs 완전 FAIL

- **결과동일 FAIL (23)** = 구조는 AS-IS와 다르나(리네임·폴딩·delta 재구현), **현재 도달가능한 입력/카드에서 결과가 동일** — 지금 틀린 출력을 내지 않음. 상환은 "1:1 구조 복원" 성격(급하지 않음).
- **완전 FAIL (51)** = 아래 중 하나로 **실제 잘못됨**: (a) 도달가능 입력서 틀린 결과(과다면역·오동작·오타이밍), (b) dead/inert(효과 없음), (c) 트리거/이벤트 미발화, (d) MISSING/스텁 → 해당 AS-IS 카드 포팅 불가.

### 결과동일 FAIL (23) — 구조만 이탈, 현재 결과 동일
- **타이밍-클래스 폴딩 (19)**: OnPlayClass · OnDeletionClass · CounterClass · EndOfAttackClass · EndOfYourTurnClass · EndOfYourOpponentsTurnClass · EndOfAllTurnsClass · AllTurnsClass · OpponentsTurnClass · TurnTimingClass · StartOfYourTurnClass · StartOfOpponentsTurnClass · StartOfYourOpponentsMainPhaseClass · YourTurnClass · WhenAttackingClass · WhenDigivolvingClass · WhenLinkingClass · WhenMovingClass · SecurityClass. → 1:1 팩토리 없이 uniform `ActivatedEffect`+triggerGate로 카드별 표현; 능력 보존, 결과 동일. 부채=재사용 심볼·2단계 게이트 미중앙화.
- **delta 재구현 (2)**: ChangeLinkMaxClass(고정 ±N) · ChangeSAttackClass(delta+Func<int>). 현재 카드(고정/계산 델타)엔 동일 결과; arbitrary Func<Permanent,int,int>만 미표현.
- **cosmetic (1)**: AddDetailClass(표시 전용, permanentCondition/triggerEffect 드롭이 게임결과 무영향).
- **구조 대체 (1)**: DeckTopBounceClass(직접-리스트 미러 없으나 select 경로로 deck-top 반환 달성 — ⚠️추가 프롬프트 여부 verify).

### 완전 FAIL (51) — 실제 잘못됨

> **상환 완료(2026-07-08)**: (a) "틀린 결과" **15건 전부 상환 완료** — #1 CanNotBeDestroyedBySkill · #2 ImmuneFromDPMinus · #3 Digimon/OptionEffectImmunity · #4 CannotReturnToLibrary · #5 CannotReduceCost(cost-kind + playerCondition/scopeAnyPlayer 완전 배선) · #6/#7 DigivolveInto enum · #8 PlayToken(Form/Attr + empty-frame=UI-only 제외 확정) · #9 Gain2Memory Delay · #10 PlaySelfDigimonAfterBattle · #11 PlayMindLinkTamer · #12 StartOfYourMainPhase · #13 OptionMain/Security discriminator([Main]-태그 필터, 결과-등가 불허·AS-IS 구조 미러) · PlacePermanentInSecurity(isFaceUp + CanAddSecurity 게이트). 테스트 FAILa-01~13 + FAILa-PPS, 회귀 green + RuleAudit 0. → **(a) 그룹 완료.** 다음=(b) dead/inert · (c) 트리거 미발화 · (d) MISSING(CanNotSelectBySkill 등).

**(a) 틀린 결과 — 과다면역/오동작/오타이밍 (15)**
- ~~CanNotBeDestroyedBySkillClass~~ **✅상환(2026-07-08)** — causing-effect 술어 배선(FAILa-01).
- ~~ImmuneFromDPMinusClass~~ **✅상환(2026-07-08)** — DP 모디파이어 소스별 면역 술어 평가(FAILa-02). + Gain ReturnToHand/Deck의 causing 술어 드롭도 함께 배선.
- ~~DigimonEffectImmunity · OptionEffectImmunity(PermFactory)~~ **✅상환(2026-07-08)** — AS-IS 형태 오버로드로 상대+타입+this-permanent 면역 미러(FAILa-03). +CollisionEffect도 AS-IS 오버로드 추가. 잔여: DeleteSelfEffect(turn-branch 활성 자기삭제 프리미티브 필요) · AddDetailClass(cosmetic, 프레임워크 flatten 상속) 오버로드 미추가.
- ~~CannotReturnToLibraryClass~~ **✅상환(2026-07-08)** — cardEffectCondition 배선(FAILa-04).
- ~~CannotReduceCostClass~~ **✅상환(2026-07-08)**: cost-kind(play/digivolve) 구분 + playerCondition(scopeAnyPlayer, opponent-scope) 완전 배선(FAILa-05).
- ~~PlacePermanentInSecurity~~ **✅상환(2026-07-08)** — isFaceUp 복원 + sink 경유로 CanAddSecurity 게이트 배선(FAILa-PPS).
- DigivolveIntoHandOrTrashCard · DigivolveIntoExcecutingAreaCard — **enum 부분 상환(2026-07-08)**: IgnoreRequirement enum 복원 + ignoreLevel/ignoreColor 관통(FAILa-06). 잔여 cost-effect 파이프라인 가시성=#5 묶음.
- ~~PlayToken(core)~~ **✅상환(2026-07-08)**: (1) Form/Attribute 방출(defMeta forms/attributes, Diaboromon Mega/Unknown·Gyuukimon Ultimate/Virus; FAILa-08 3건). (2) **empty-frame 가드 = 의도적 미모델(확정)**: AS-IS `fieldCardFrames`는 **Unity UI GameObject**(Player.cs:15-52, `BattleAreaFrameParent.GetChild`·`GetComponent<Image>`)로 만든 **클라이언트 화면 슬롯**이며 Digimon TCG 규칙엔 배틀에리어 마릿수 제한이 없음 → **UI 아티팩트라 헤드리스(게임규칙 모델)에서 제외가 정답**(사용자 확정 2026-07-08). (3) CanPlayAsNewPermanent 게이트는 payCost:false에서 vacuous.
- ~~Gain2MemoryOptionDelayEffect~~ **✅상환(2026-07-08)** — 자기 trash→성공 시에만 +2(FAILa-09, 13카드).
- ~~PlaySelfDigimonAfterBattleSecurityEffect~~ **✅상환(2026-07-08)** — OnEndBattle 지연 플레이 + 턴종료 삭제 배선(FAILa-10, 35카드).
- ~~PlayMindLinkTamerFromDigivolutionCards~~ **✅상환(2026-07-08)** — selfStackOnly+optional+CanPlayAsNewPermanent(FAILa-11).
- ~~StartOfYourMainPhaseClass~~ **✅상환(2026-07-08)** — 검증 결과 실제 오타이밍이었음: `Gain1MemoryTamerOpponentDigimonEffect`·`Gain1MemoryTamerOwnerDigimonConditionalEffect`가 OnStartTurn 등록 → AS-IS "[Start of Your Main Phase]"(BT23_081/083)에 맞춰 `OnStartMainPhase`로 교정(방출·수집 존재 확인). 테스트 FAILa-12(2건).
- ~~OptionMainEffect · OptionSecurityEffect~~ **✅상환(2026-07-08)** — 사용자 지시(결과-등가 불허, AS-IS 구조 미러): `ActivatedEffectResolver.IsMainOptionEffect`(`ActivatedEffect`+"[Main]" description, AS-IS `is ActivateClass && Contains("[Main]")` 미러) 신설 + `ResolveAsync`에 effectFilter 추가 → `ActivateMainOfOptionSide`·`ReuseMainOptionEffect`(security 재사용)가 **[Main]-태그 효과만** 해소(전 OptionSkill 아님). 테스트 FAILa-13(픽스처 TfxMainDisc: [Main] gain vs non-[Main] draw → [Main]만 실행).

**(b) dead / inert — 효과 없음 (8) → 검증 후 실제 dead=2건(둘 다 ✅상환), 5건 오분류(LIVE), 1건 딥 잔여**
- ~~InvertSAttackClass~~ **✅상환(2026-07-09)** — invert가 SAttack 변경 방향 flip 적용(FAILb-01, 5건).
- ~~ImmuneFromDeDigivolveClass~~ **✅상환(2026-07-09)** — producer(연속 제약)+sink/호출부 면역 검사(FAILb-02, 2건).
- CanNotTrashFromDigivolutionCardsClass — ⚠️딥 잔여(전역 source-protection 스캔 인프라 필요).
- ~~ScapegoatClass · TreatAsDigimonClass · VortexCanAttackPlayersClass~~ **오분류 정정: LIVE**(FAILb-verify 검증, producer+소비자 작동).
- ~~DeleteSelfEffect · CollisionEffect(PermFactory)~~ **오분류 정정: LIVE**(binding-rule 시스템 소비 + G3J-002 테스트).

**(c) 트리거 미발화 (1) — ✅전량 상환(2026-07-09)**
- ~~ReturnToLibraryBottomDigivolutionCardsClass~~ **✅상환(2026-07-09)** — 타이밍 신설+방출 + CanNotBeAffected 가드(FAILc-01). ⚠️ 나머지 (c)로 분류됐던 TrainingClass·SuspendPermanentsClass는 §하단 3번 항목 참조(별도 잔여).

**(d) MISSING / 스텁 → AS-IS 카드 포팅 불가 (27)**
- PlaceToSecurityEffect · ActivateClassesForSharedEffects(공유 hash once-per-turn 다중발동) · GetCardEffectByEffectTiming · TrashDigivolutionCardsAndProcessAccordingToResult(계약 변경).
- AddDigivolutionRequirementClass · AddSkillClass(라이브 STOP BT1_104).
- CanAttackTargetDefendingPermanentClass · ~~CanNotBeRemovedClass~~✅ · ~~CanNotMoveClass~~ **✅상환(2026-07-09, FAILd-03)** · CanNotPlayClass · CanNotPutFieldClass · ~~CanNotSelectBySkillClass~~ **✅상환(FAILd-01)**.
- CanSelectAssemblyClass · CanSelectDigiXrosClass · CanSuspendByDigisorptionClass · ~~CannotIgnoreDigivolutionConditionClass~~ **✅상환(2026-07-09, FAILd-06)**.
- ~~ChangeBaseCardNameClass~~ **✅상환(2026-07-09, FAILd-08)** · ChangeCardLevelForAssemblyClass · ChangeCardNamesForDigiXrosClass · ~~ChangeEndTurnMinMemoryClass~~ **✅상환(2026-07-09, FAILd-07)**.
- ~~DontBattleSecurityDigimonClass~~ **✅상환(2026-07-09, FAILd-05)** · ~~DontHaveDPClass~~✅.
- CollisionClass(predicate-grant 형 부재) · DisableEffectClass(선택적 per-effect disable 부재) · SelectCardConditionClass(고급 술어 미평가) · SelectDigiXrosClass(ByPreSelectedList+substitution 드롭).

> 상환 순서: **완전 FAIL (a)(b)(c) 먼저**(지금 틀림) → **(d) 중 실카드 수요 큰 것**(CanNotSelectBySkill 등) → **결과동일 FAIL**(구조 복원, 저우선). ⚠️ 표기 verify 2건(StartOfYourMainPhase 타이밍, DeckTopBounce 프롬프트)은 상환 전 재대조.
