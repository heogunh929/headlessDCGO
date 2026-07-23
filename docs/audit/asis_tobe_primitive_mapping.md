# AS-IS ↔ TO-BE 프리미티브 매핑 정의서 (전수 감사)

- 🔜 **진행 예정 대상 워크리스트**: [primitive_remaining_worklist.md](primitive_remaining_worklist.md)
- 작성: 2026-07-08. 방법: **문서·카탈로그 불신 — AS-IS(`DCGO/`)와 TO-BE(`src/`) 실소스를 심볼별로 직접 대조**(병렬 감사 에이전트 11, 코드에서 직접 리스트업).
- 목적: 개발된 프리미티브가 (1) AS-IS를 **1:1 미러**했는지, (2) **호출부가 없다는 이유로 스킵**한 부분이 있는지를 전수 판정하고, AS-IS 함수 ↔ TO-BE 함수 매핑을 확정한다.
- 파일 경로 약칭: **CPF** = `src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons/CardPortingFramework.cs`. AS-IS 파일은 전부 `DCGO/Assets/Scripts/Script/` 하위.

---

# 2026-07-23 개정 (소프트 동결 시점 · HEAD 5e314380)

> **읽는 법**: 이 문서의 §감사기준 이하 본문은 **2026-07-08 작성 + 07-09까지의 상환 주석**이 누적된 역사 기록이다(각 심볼 행에 `~~취소선~~ ✅상환` 형태로 07-08/09 시점 판정과 그 상환이 병기됨). 아래 개정 블록은 **07-09 이후 소프트 동결(2026-07-23)까지의 구조 변화**를 반영해, 대량 카드 포팅(Haiku 파일럿)이 참조할 **현재 실상태**를 상단에 확정한다. **본문의 07-08 판정은 그대로 두되(`[07-08 판정]` 태그로 읽을 것), 아래 개정과 충돌하면 개정이 우선한다.** 판정 근거는 전부 HEAD `5e314380` 실소스 grep(`--binary-files=text`)·census 07-22 대조로 재검증했다(문서·기억 불신 원칙).

## R0. 동결 계기판 (07-23 실측)

- **발명물 물리 삭제 확정**: `EffectRegistry`·`EffectBinding`·`LegacyBindingBridge`·`IActivatedCardEffect`·`ToBinding`·`PermanentEffectFactoryBinding` 전부 **클래스/인터페이스 정의 0 · live 참조 0**(grep 실측). 07-08 본문이 이들을 "seal/binding-rule 소비"로 판정한 항목(§D DeleteSelf/Collision, §E name-only seal 3종)은 전부 **AS-IS 정본 경로로 재배선 후 발명물 청산 완료**로 갱신 — 아래 R3 참조.
- **live NotSupportedException = 4좌석**(전수 원장-매핑, 동결 증거 §7과 동기):
  1. `GManager.cs:198` — RD-W4-3(브릿지 W4 미지원 컴포넌트 타입, **조건부** — 새 컴포넌트 요청 시에만 좌석화, 심층-불가 아님)
  2. `CardController.cs:4283` — 리뷰3 P2-② 코퍼스 **중복-키 방어가드**(fidelity 갭 아님 — DISPATCH-REMAP 방언이 효과당 1키 강제; 잘못 배선한 카드를 잡는 가드)
  3. `Permanent.cs:4549` — MIG4-DETACH-LIVE-TOP 직접-라이브-탑 가드
  4. `CardEffectCommons/TrashLinkedCards.cs:72` — RD-SKEL-01 **dead-가드**(AS-IS 내부 비대칭 = LinkedCards 풀 vs DigivolutionCards.Count 예산 불일치 → 충실 headless ChoiceProvider 번역 시 비종결 루프. 얕은 뭉갬/발명 가드 회피 위해 STOP-가드. **진짜 AS-IS 한계**)
- **게이트**: 전체 스위트 425/425 green · 확장 다이제스트 10시드 + behavior 5시드 bit-identical · 500판 strict 퍼징 PASS(결함 0) · 동결-게이트 적대리뷰 GO(엔진 P0/P1 0).

## R1. 요약표 — 신구 대비 (07-08 판정 vs 07-23 실상태)

**주의**: 두 시점은 **카운트 단위가 다르다**. 07-08 감사 = "public 팩토리 메서드/함수" 182심볼(허브 파일 내 메서드 단위). census 07-22/07-23 = "파일/인터페이스/kind-class" 340유닛(B군/R시리즈가 허브 파일을 파일-당-프리미티브로 재분해). **두 숫자는 직접 비교 불가** — 아래 표는 (A) 07-08 렌즈의 상환 진척, (B) 07-23 census 렌즈의 구조 상태를 **각각** 제시한다.

### (A) 07-08 렌즈 — 182 심볼의 상환 진척 (본문 주석 집계)

| 원본 그룹 | 수 | [07-08] PASS | [07-08] PARTIAL | [07-08] FAIL(+MISSING) | → [07-23] 실동작-버그(a/b/c) 잔존 | → [07-23] 구조-only FAIL(결과동일·폴딩) |
|---|---:|---:|---:|---:|---:|---:|
| CardEffectFactory | 41 | 2 | 14 | 25 | **0**(타이밍-폴딩 19 = 정본 확정) | 타이밍-클래스 19 (uniform ActivateClass) + PlaceToSecurity·ActivateClassesForShared 등 |
| CardEffectCommons(비-토큰) | 22 | 6 | 9 | 7 | **0** | success-list 콜백 int화 등 계약 잔여 |
| CardEffectCommons(토큰) | 18 | 15 | 2 | 1 | **0**(PlayToken Form/Attr 상환·empty-frame=UI 제외 확정) | — |
| PermanentEffectFactory | 6 | 0 | 2 | 4 | **0**(Immunity/Collision AS-IS 오버로드 상환·발명 binding 청산) | AddDetail(cosmetic)·CanNotSwitchAttackTarget(stale-capture) |
| 효과 본체 `*Class` | 95 | 27 | 31 | 37 | **0**(FAILa~d 전량 상환·오분류 5 LIVE 정정) | 타이밍-폴딩·Change* Func-transform·Select* 고급술어 등 |
| **합계** | **182** | **50** | **58** | **74** | **실동작-버그 0** | 구조-only 잔여만 |

- **07-08의 "완전 FAIL 51" → 07-23 실동작-버그 잔존 = 0.** (a)틀린결과 15 + (b)dead 2 + (c)트리거미발화 1 = **18건 전량 상환**(FAILa-01~13, FAILb-01/02, FAILc-01, FAILd-01~08 등). (b)그룹 중 5건(Scapegoat·TreatAsDigimon·Vortex·DeleteSelf·Collision)은 **오분류 정정=LIVE**(코드 검증). (d)MISSING 27 중 실카드 수요 항목 상환 완료.
- **딥 잔여 1건**: `CanNotTrashFromDigivolutionCardsClass`(전역 source-protection 스캔) — 07-23 현재 kind-class 미러 파일 존재(`CardEffects/CanNotTrashFromDigivolutionCardsClass.cs`, 40줄) + 소비자 배선(MatchStateMutationSink·DeletionSourceTrash·DigivolutionStackHelpers). census는 FULL 계수. **전역-스캔 심층 충실성은 유일 호출카드 BT9_109가 STOP-포팅 상태라 정본 카드로 재검증 불가** — 포팅 재개 시 BT9_109 재활로만 확인 가능(coverage 감사 §6 `딥 잔여`).

### (B) 07-23 census 렌즈 — 340 유닛의 구조 상태 (파일/인터페이스/kind-class)

| 카테고리 | 유닛 | [census 07-22] FULL/SKEL/UNP/DIV | → [07-23 HEAD 재검증] |
|---|---:|---|---|
| A. GiveEffect | 33 | 28 / 4 / 1 / 0 | **33 / 0 / 0 / 0** — SKEL 4(ChangeLinkMax·StartOfMainAttack·ChangeDigivolutionCost·IgnoreDigivolutionRequirement) **전부 본체 충전**(스켈레톤-보일러 0줄 → 34~109줄) · UNP `CanNotBeDeletedByBattle`(permanent-scope) **충전(94줄, 비대칭 갭 해소)** |
| B. CardEffectFactory(top27+KeyWord32) | 59 | 57 / 0 / 0 / 2 | **59 / 0 / 0 / 0** — DIV 2(BlastDigivolution·BlastDNADigivolution) **전부 포팅**(field-frame W4 인덱스 적응; Blast=07-22 STOP-stale 실증·verbatim 미러, BlastDNA=07-23 잔여블로커 전폐쇄) |
| C. CardEffects/*.cs (kind-class) | 73 | 73 / 0 / 0 / 0 | **73 / 0 / 0 / 0** (무변) |
| D. CardEffectInterfaces.cs | 74 | 74 / 0 / 0 / 0 | **74 / 0 / 0 / 0** (무변) |
| E. KeyWordEffects 본체 | 29 | 28 / 1 / 0 / 0 | **29 / 0 / 0 / 0** — SKEL 1(`KeyWordEffects/Training.cs`) **본체 충전**(59줄) |
| F. Commons(top11+CanUse37+MinMax7) | 55 | 47 / 8 / 0 / 0 | **54 / 0 / 0 / 1** — SKEL 8 중 7(MinMax_DP_Cost_Level IsMax/Min×Cost/DP/Level/DigivolutionCards) **전부 충전**, 1(`TrashLinkedCards`)은 **DIVERGENT로 이동**(RD-SKEL-01 dead-가드, AS-IS 한계) |
| G. Select*/choice | 17 | 10 / 3 / 4 / 0 | **10 / 2 / 4 / 0**(엔진 프리미티브 기준) — SKEL `SelectJogressEffect`는 **UI-플로우 오케스트레이터로 재분류·의도적 bodiless**(substrate DNA 경로가 대체). SelectCardPanel/SelectDeck(SKEL 2)·SelectBattleDeck/Mode/Command/CommandPanel(UNP 4) = **UI 패널, 엔진 스코프 밖**(동결 계약 제외 대상) |
| **합계** | **340** | **317 / 16 / 5 / 2** | **엔진-프리미티브 잔여 비-FULL: DIVERGENT 3(TrashLinkedCards + Blast 2는 해소) → 실질 1(TrashLinkedCards) + UI 6(스코프 밖)** |

**핵심 델타**: census 07-22의 SKELETON 16·UNPORTED 5·DIVERGENT 2 (총 23 비-FULL) 중, **엔진-프리미티브 실 갭은 07-23 HEAD에서 1건(TrashLinkedCards RD-SKEL-01, AS-IS 한계 dead-가드)으로 수렴**. 나머지는 (i)스켈레톤 소진 아크에서 본체 충전(13+1+7=글자 그대로 채워짐), (ii)Blast 2종 포팅, (iii)UI 패널 6종(엔진 스코프 밖)으로 정리됨.

## R2. 구조 재해석 — 타이밍-클래스 "결과동일 FAIL"의 종결

07-08 본문이 최대 FAIL 원인으로 지목한 **타이밍-클래스 폴딩 19종**(OnPlay/OnDeletion/Counter/EndOf*/StartOf*/When*/YourTurn/OpponentsTurn/SecurityClass 등 → uniform `ActivatedEffect`+triggerGate 카드별 인라인)은, **창엔진 SkillWindowSupply 컷오버 + A군 키워드 재하우징 18/18** 이후 **uniform ActivateClass가 AS-IS 정본 경로임이 확정**됐다(coverage_exemplar_audit §2 "은퇴가 아니라 표현 방식 확정"). → 이 19종은 **"1:1 팩토리 심볼 부재"를 갭으로 계상하지 않는다**. 정본 카드가 해당 타이밍 창을 실발화하면 커버로 친다. 잔여 부채는 `ActivateClassesForSharedEffects`의 **공유 hashValue once-per-turn** 캡뿐인데, 07-18 실측(§8)에서 "순수 디스패처(공유 캡 불요)"로 격파 — 다중발동 위험 실증 없음.

## R3. 07-08 본문 참조 중 스테일이 된 인프라 (개정 필독)

07-08 본문(특히 §D·§E)이 **살아있는 것으로 서술한 발명 클러스터는 전부 물리 삭제**됐다. 본문을 읽을 때 아래 치환을 적용하라:

| 07-08 본문 서술 | 07-23 실상태 |
|---|---|
| §D `Binding DeleteSelf (PermanentEffectFactoryBinding.cs:375)` · `Binding Detail (:444)` | **PermanentEffectFactoryBinding 삭제.** DeleteSelf/AddDetailClass는 **AS-IS `PermanentEffectFactory.cs` 본체**로 live(발명 string-key binding-rule 오버로드 = G-clean 배치서 제거) |
| §E "FAIL — name-only seal (Scapegoat·TreatAsDigimon·Vortex)" | **오분류 정정 = 전부 LIVE**(07-09 검증). producer=`ContinuousPlayerScopeKeywordEffect`, 소비자=`ContinuousKeywordGate`/`DeletionReplacementGate` |
| §D DeleteSelfEffect/CollisionEffect "0 callers, binding-rule 폼" | `PermanentEffectFactory.cs`에 **AS-IS 형태 오버로드**로 live(DigimonEffectImmunity:53·OptionEffectImmunity:86·CollisionEffect:121) + G3J-002 테스트 |
| "EffectRegistry/EffectBinding/ToBinding/IActivatedCardEffect 경유" 일반 | **전부 삭제.** live 경로 = AS-IS EffectList 버킷 스캔(NewModelContinuousScan·Permanent.CanX getter) |

---

## 감사 기준 (두 렌즈)

- **LENS1 (1:1 미러)**: 구조 + 동작 재현. 결과-동일이라도 **구조가 다르면 FAIL**. 술어/파라미터를 AS-IS는 평가하나 TO-BE가 무시·하드코딩·평면화 = FAIL. 가드/조건/임계값/분기 누락 = FAIL.
- **LENS2 (호출부-없음 스킵)**: 분기/파라미터/오버로드/타이밍 누락, "현재 포팅 카드에만" 축소, `NotSupported`/스텁/seal(등록되나 소비자 미배선), 무시된 파라미터. **호출부 부재는 스킵 사유가 아님.**

판정: **PASS**(1:1) · **PARTIAL**(일부 미러/일부 이탈) · **FAIL**(구조·동작 상이) · **MISSING**(TO-BE 부재/스텁).

## 요약 (인벤토리 182 심볼, 코드 직접 추출) — [07-08 판정 · 역사 보존]

> 이 표는 **2026-07-08 시점 판정**이다. 상환 진척·신구 대비는 상단 **R1 요약표**를 우선하라.

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

> **[07-23 개정]** 아래 표의 `Binding …(PermanentEffectFactoryBinding.cs:…)` 서술은 **스테일**. `PermanentEffectFactoryBinding` 클러스터는 **물리 삭제**됐고, DeleteSelf·AddDetailClass·Immunity·Collision은 전부 **AS-IS `PermanentEffectFactory.cs` 본체 오버로드**로 live(R3 표 참조). DigimonEffectImmunity/OptionEffectImmunity/CollisionEffect는 07-08 상환 완료(FAILa-03). 잔여 실-갭 없음(DeleteSelf turn-branch·AddDetail cosmetic만 07-08 판정대로 저영향).

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

---

# R4. 현재 정본 idiom 색인 (대량 포팅 참조 · 2026-07-23)

> **용도**: Haiku 파일럿이 프리미티브를 호출할 때 참조할 **정본 예제 카드**(포팅 코퍼스 339장 중, 미러 파일 경로 병기)와 **호출 관용구**. 카드 경로 규약 = `src/HeadlessDCGO.Engine/Assets/Scripts/CardEffect/<SET>/<Color>/<ID>.cs` (예: `.../CardEffect/BT13/Blue/BT13_023.cs`). 예제 선정은 coverage_exemplar_audit_2026-07-18.md §3 커버리지 행렬의 **클린 커버(✅) 카드**와 연결 — 아래는 그 EXEMPLAR 앵커의 프리미티브-축 발췌. `∅`/`❌`(정본 부재) 축은 §R5 잔여 리스트로 넘긴다.

## R4.1 프리미티브 축 — 정본 앵커 (coverage §3 ✅ 발췌)

| 프리미티브/축 | 정본 예제 카드 | 미러 경로(색상 세트) | 관용구 메모 |
|---|---|---|---|
| ActivateClass(uniform 타이밍-창) | BT13_023, BT16_025 | BT13/Blue, BT16/? | 전 타이밍-클래스(OnPlay/When*/EndOf*/StartOf* 등 19종)의 **정본 진입점** — triggerGate로 창 지정(R2) |
| SelectPermanentEffect | BT13_023, BT16_025, BT19_024 | — | 후보-스코프 연속 제약(CanNotSelectBySkill 등)이 BuildRequest 단일 chokepoint에서 배제 |
| SelectHandEffect | BT19_024, BT1_039, BT1_056 | — | 손패 선택 정본 |
| SelectCardEffect | BT19_024, BT1_010, BT1_011 | — | 범용 카드 선택 |
| DrawClass | BT1_003, BT1_006 (18장) | BT1/? | OnDraw 트리거 sink 배선 확인됨 |
| DestroyPermanentsClass | BT1_084, BT9_081 | — | CanBeDestroyedBySkill 가드 경유 |
| SuspendPermanentsClass | BT16_025, BT1_086 (10장) | — | already-suspended 필터·DPWhenSuspended 스냅샷 확인 |
| SimplifiedSelectCardConditionClass | BT1_010, BT1_048 (9장) | — | Mode 기반 술어 |
| TrashDigivolutionCardsFromTopOrBottom | BT13_023, BT1_043 (9장) | — | (count,isFromTop) 계약 |
| DisableEffectClass | BT1_025 | — | (07-08 FAIL 표기 ↔ 실 커버; E-3 상환 반영) |
| PlayCardClass | BT1_078 | — | 구조 분리 폼 |
| HatchDigiEggClass | BT1_089 | — | sink 위임 |
| ChangeCostClass | BT2_023, BT2_099 | BT2/? | CostModification 정본 |
| CanNotPlayClass | BT8_057, EX1_072 | — | ICanNotPlay 연속스캔(E-3 상환) |
| AddJogressConditionClass / Jogress·DNA | BT16_025 | BT16/? | 특수플레이 정본; DNA는 substrate ChoiceProvider 경로 |
| PlaySelfTamerSecurityEffect / SetMemoryTo3 | BT1_085, BT1_086, BT1_087 | BT1/? | [Security] 타이머 정본 |
| AddActivateMainOptionSecurityEffect | BT1_094, BT1_101 | BT1/? | [Main]→[Security] 파생 |
| AddThisCardToHand | BT1_093, BT1_096 | BT1/? | — |
| PlayPermanentCards | BT19_024, BT1_044 | — | CanPlayAsNewPermanent 게이트 |

## R4.2 키워드/특수플레이 축 — 정본 앵커 (coverage §3 ②·⑤ ✅)

| 축 | 정본 예제 | 메모 |
|---|---|---|
| Retaliation | BT2_074 | — |
| Pierce | BT1_022, BT1_026, BT1_081 | OnDetermineDoSecurityCheck 연동 |
| Blocker | BT19_071, BT1_023, BT1_031 | A군 재하우징 후 정본 |
| Jamming | BT1_016, BT1_098, BT2_057 | — |
| Reboot | BT2_055, BT2_063, BT2_065 | — |
| Collision / Fragment | EX8_051 | OnCounterTiming 동반 |
| Scapegoat | EX8_061 | LIVE(오분류 정정) |
| Barrier | BT14_035 | WhenPermanentWouldBeDeleted 창 |
| Evade / Decode / Partition | BT13_023 / BT19_024 / BT16_025 | — |
| DigiBurst | ST4_13 | — |
| TokenPlay | BT8_092 | Form/Attribute 방출 상환(FAILa-08) |
| CostModification | BT2_023, BT2_099 | — |

## R4.3 창-타이밍 축 — 정본 앵커 (coverage §3 ③ ✅ 발췌)

OnDeclaration=BT1_088/089 · OnEnterFieldAnyone=BT16_025/BT19_024 · OptionSkill=BT1_091/092/093 · OnDestroyedAnyone=BT1_030/035/049 · WhenRemoveField=BT16_025 · WhenPermanentWouldBeDeleted=BT13_023/BT14_035/EX8_051 · OnEndTurn=BT1_040 · OnStartTurn=BT1_085/086/087 · OnStartMainPhase=ST15_02 · OnEndBattle=BT1_077/112/ST4_11 · OnEndAttack=BT19_024/BT1_081 · OnDigivolutionCardDiscarded=BT2_085/EX8_051 · WhenLinked=BT22_003 · OnTappedAnyone=ST4_14 · OnUnTappedAnyone=BT2_002/BT8_057 · OnAddDigivolutionCards=BT22_044/EX6_001 · OnAllyAttack=BT13_023/BT16_025 · OnCounterTiming=EX8_051 · SecuritySkill=BT1_085/086/087.

> **선정 확장**: coverage §4 greedy set-cover 상위 30장(BT25_104·LM_054·BT21_030 …)은 **미커버 축을 새로 덮는 witness 후보**다(정본 idiom이 아직 없는 축). 신규 축 정본을 만들 때 그 표를 witness 선정 입력으로 쓰라 — 단, STOP-리스크 21장(coverage §6)은 Opus 프리미티브 선행 개발 트리거([[primitive-predevelopment-role]]).

---

# R5. 잔여 비-1:1 항목 전수 리스트 (2026-07-23 HEAD 실측)

> 실동작-버그(틀린 결과 산출)는 **0**(R1-A). 아래는 **구조-only 이탈**(결과동일이나 1:1 심볼/조합성 부재) + **진짜 AS-IS 한계** + **UI 스코프-밖**의 전수 분류. 빈도 가중 없음(전수 분류만).

## R5.1 진짜 AS-IS 한계 / 인프라 갭 (live STOP·dead-가드)

| 항목 | 좌석/원장 | 성격 |
|---|---|---|
| TrashLinkedCards 선택 루프 | `TrashLinkedCards.cs:72` (RD-SKEL-01) | dead-가드. AS-IS 비대칭(LinkedCards 풀 vs DigivolutionCards.Count 예산)이 충실 headless 번역 시 비종결 루프 → 얕은 뭉갬/발명 가드 회피 위해 STOP. **AS-IS 한계** |
| W4 브릿지 미지원 컴포넌트 | `GManager.cs:198` (RD-W4-3) | **조건부** — 새 컴포넌트 타입 요청 시에만 좌석화(심층-불가 아님) |
| 직접-라이브-탑 detach 가드 | `Permanent.cs:4549` (MIG4-DETACH-LIVE-TOP) | latent(호출자 0) |
| 코퍼스 중복-키 방어 | `CardController.cs:4283` (리뷰3 P2-②) | **fidelity 갭 아님** — 잘못 배선한 카드를 잡는 방어가드 |
| field-frame 슬롯 WRITE 모델 | (Blast 2종 W4-적응으로 소비자 해소) | 인프라 latent — 현재 소비자(Blast) 포팅됨. 새 frame-WRITE 요구 카드 등장 시 재점화 가능 |
| CanNotTrashFromDigivolutionCards 전역-스캔 심층 | kind-class FULL, BT9_109 STOP | 미러+소비자 존재. 전역 source-protection 심층 충실성은 유일카드 STOP 상태라 재검증 불가(딥 잔여) |

## R5.2 구조-only 이탈 (결과동일 FAIL — 07-08 렌즈 잔여, 저우선)

- **타이밍-클래스 uniform 폴딩 (19)**: OnPlay·OnDeletion·Counter·EndOfAttack·EndOfYourTurn·EndOfYourOpponentsTurn·EndOfAllTurns·AllTurns·OpponentsTurn·TurnTiming·StartOfYourTurn·StartOfOpponentsTurn·StartOfYourOpponentsMainPhase·YourTurn·WhenAttacking·WhenDigivolving·WhenLinking·WhenMoving·SecurityClass. → **R2에서 정본 확정**(uniform ActivateClass = AS-IS 정본 경로). "1:1 팩토리 심볼 부재"는 **갭으로 계상 안 함**.
- **공유 once-per-turn**: ActivateClassesForSharedEffects(공유 hashValue 캡). 07-18 실측서 "순수 디스패처(캡 불요)"로 격파 — 다중발동 위험 실증 0. 잔여=심볼 조합성뿐.
- **delta 재구현 (2)**: ChangeLinkMaxClass(고정 ±N) · ChangeSAttackClass(delta+Func<int>). 현 카드 동일결과; arbitrary `Func<Permanent,int,int>`만 미표현.
- **success 콜백 계약 (Trash*/Play*ProcessAccordingToResult)**: success가 int(삭제 리스트 아님). 현 카드 동일결과, 계약 복원은 저우선.
- **cosmetic (2)**: AddDetailClass(표시 전용) · CanNotSwitchAttackTargetEffect(topCard stale-capture, 진화 시).
- **구조 대체 (1)**: DeckTopBounceClass(직접-리스트 미러 없이 select 경로로 달성 — ⚠️프롬프트 추가여부 verify 미해소).

## R5.3 UI 스코프-밖 (엔진 동결 계약 제외 대상)

- **UNPORTED UI 패널 (4)**: SelectBattleDeck·SelectBattleMode·SelectCommand·SelectCommandPanel — 배틀덱/모드/커맨드 선택 **화면**. 카드-효과 프리미티브 아님.
- **near-empty UI 스텁 (2)**: SelectCardPanel·SelectDeck.
- **재분류 bodiless (1)**: SelectJogressEffect — UI-플로우 오케스트레이터, substrate DNA 경로(DNADigivolvePermanentsIntoHandOrTrashCard + SpecialPlayRecipeRegistry)가 게임 결정을 담당. 엔진-프리미티브 본체 없음(의도적).

## R5.4 포팅 중 정직-STOP 예상 (coverage §6 인프라 갭 — 신규 카드가 건드리면 수확)

Assembly/DigiXros 인터랙티브 pre-play(RD-P6C1-5) · Burst/AppFusion select 컴포넌트(RD-P6C1-6) · Execute 발화(RD-R2-01) · Ascension writer(RD-3A-01) · AddSkillClass 중첩부여(nested-grant 원칙 STOP) · Digisorption 진입(G11) · 고급 SelectCardCondition 술어(뭉개면 FAIL) · CanNotPutField 필드제약 · 전역 digi-source 보호(딥 잔여). → 정본 포팅이 이들을 실호출하면 **runtime throw 아닌 정직 STOP 마커**로 원장 등재 후 Opus 프리미티브 선행([[primitive-predevelopment-role]]).
