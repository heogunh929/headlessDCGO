# 커버리지 정본(exemplar) 감사 — 포팅 재개 1단계 (2026-07-18)

- 목적: 강모델 정본 카드 포팅(⑥)의 선정 입력 — 프리미티브/키워드/창-타이밍/Select*/특수플레이 5축의 **기포팅 코퍼스 커버리지 행렬**과 **미커버 축 set-cover 후보**.
- 방법(read-only): AS-IS 카드 소스 3,918파일 토큰 스캔(+IR DB `docs/porting/card_ir.sqlite` 룰텍스트 조인) × TO-BE 등록 코퍼스 209장(reflection dispatch=CEntity_Effect 서브클래스). 코드/빌드 무변경.
- 산출 스크립트: `.tmp/coverage_audit/analyze.py` / `.tmp/coverage_audit/gen_doc.py` (재실행 가능, stdlib 전용).

## 0. 코퍼스 기준선

| 항목 | 수 |
|---|---:|
| AS-IS 카드 효과 소스 | 3917 (파일 3,918 — `BT25_002` Yellow/Blue 중복 스템 1) |
| TO-BE 등록(포팅) | 209 |
| STOP 마커 파일 | 29 + 잠복-STOP 1(`AD1_025`, AddAssemblyConditionClass→RD-P6C1-5) = 30 |
| **클린 코퍼스(커버리지 기준)** | **179** |

- 커버리지 판정 = **클린 179장의 AS-IS 소스가 그 축 요소를 실호출**하는가(포팅은 1:1 미러 원칙이므로 AS-IS 호출 프로파일=TO-BE 호출 프로파일). STOP 30장만 호출하는 요소는 `stop-only`(클린 정본 없음)로 별도 분류.
- `∅ 카드-표면 없음` = 어떤 AS-IS 카드 소스도 그 심볼을 직접 참조하지 않음(엔진 내부/간접 경유) — 정본 카드로 직접 덮을 수 없는 요소.

## 1. 축별 커버리지 요약

| 축 | 총 | ✅커버 | 🟡STOP만 | ❌미커버 | ∅표면없음 | 커버율(표면 있는 요소 기준) |
|---|---:|---:|---:|---:|---:|---:|
| ① 프리미티브(정의서 182→고유 179) | 179 | 18 | 7 | 102 | 52 | 14% |
| ② 키워드 25(재하우징18+충실7) | 25 | 11 | 3 | 11 | 0 | 44% |
| ②′ 키워드 잔여 8(진화/특수계) | 33 | 12 | 6 | 13 | 2 | 39% |
| ③ 창-타이밍(AS-IS EffectTiming 60) | 60 | 27 | 6 | 17 | 10 | 54% |
| ④ Select* 모드 11 | 11 | 3 | 0 | 4 | 4 | 43% |
| ⑤ 특수플레이/비용·진화 변형 14 | 14 | 5 | 3 | 6 | 0 | 36% |

- ②′ 행은 ②와 합산치(33종)에서 25종을 뺀 나머지 8종(Barrier·ArmorPurge·Ascension·Progress·Link·ArtsDigivolve·BlastDigivolution·BlastDNADigivolution)을 포함한 전체 33 기준 표기.
- **미커버(+stop-only) 요소 합계 = 164** → greedy set-cover 87장이 162개를 덮음. 잔여 2: P:CanNotTrashFromDigivolutionCardsClass, T:OnLeaveFieldAnyone (후자=클린으로 덮을 카드 후보가 소진된 요소; §5 참조).

## 2. 축 정의 확정

### ① 프리미티브 — 정의서 `asis_tobe_primitive_mapping.md` 182 심볼

AS-IS 실소스에서 재추출해 정의서 집계(41+22+18+6+95=182)와 일치 확인. 고유명 기준 179(그룹 간 중복 3). 그룹:
- **A.CardEffectFactory**: 41종
- **B.Commons(non-token)**: 22종
- **C.Commons(token)**: 18종
- **D.PermanentEffectFactory**: 6종
- **E.EffectClass**: 95종

**유효성 재확인(2026-07-18)**: 정의서 판정은 2026-07-08~09 시점. 이후 ⑴ 창엔진 SkillInfo 컷오버+A군 키워드 재하우징 18/18(발명 게이트 발화-half 은퇴, GainK→AS-IS 창 경로), ⑵ R4 페이즈 모델 화해가 진행 — 아래 표의 `정의서 판정`은 그 시점 기준이며, 키워드-계 심볼(플래그 표기)은 재하우징 이후 실상태가 개선됐을 수 있음. 타이밍-클래스 19종은 "결과동일 FAIL(폴딩)"로 분류됐던 것 — 창 컷오버 후 uniform ActivateClass가 AS-IS 정본 경로임이 확정되어 **은퇴가 아니라 표현 방식 확정**으로 재해석(정본 카드가 해당 타이밍 창을 실발화하면 커버로 침).

### ② 키워드 25종(+잔여 8)

- 재하우징 18(A군 완료): Vortex·Overclock·Execute·Training·MaterialSave·Raid·Alliance·Blitz·Retaliation·Pierce·Evade·Decode·Decoy·Fragment·Partition·Fortitude·Scapegoat·Save
- 충실 7(AS-IS 자체가 전용 읽기): Rush·Iceclad·Blocker·Jamming·Collision·Reboot·MindLink
- 잔여 8(진화/특수 계열, ⑤와 교차): Barrier·ArmorPurge·Ascension·Progress·Link·ArtsDigivolve·BlastDigivolution·BlastDNADigivolution
- 검출 = AS-IS `KeyWordEffects/<K>.cs`(Factory+Commons)에서 추출한 API 토큰(GainK/K*Effect/CanActivateK 등) ∪ 룰텍스트 `<K …>` 매치(IR DB descriptions).

### ③ 창-타이밍 — AS-IS `EffectTiming` enum 60값(None 제외)

TO-BE enum은 AS-IS 60값 전체를 포함(+substrate 전용 소수). 커버 판정=카드 소스의 `EffectTiming.X` 직접 참조 기준 — 카드가 참조하지 않는 값(엔진 방출 전용) 10종은 ∅.

### ④ Select*/choice 모드 11종

SelectPermanentEffect·SelectHandEffect·SelectCardEffect·SelectAttackEffect·SelectAppFusionEffect·SelectBurstDigivolutionEffect·SelectDigiXrosClass·SelectAssemblyClass·SelectJogressEffect·SelectDNACondition·SelectCountEffect (TO-BE 미러 파일 11/11 존재). AppFusion/Assembly/Burst/Jogress 계 Select는 카드가 직접 참조하지 않고 `Add*ConditionClass` 부여를 통해 엔진이 개방(∅) — 정본은 해당 ConditionClass 카드로 덮는다(⑤).

### ⑤ 특수플레이/비용·진화 변형 14종

Jogress/DNA·DigiXros·Assembly·AppFusion·BurstDigivolution·DigiBurst·TokenPlay·DelayOption·HatchDigiEgg·Digisorption·AceOverflow·ExecutingAreaDigivolve·IgnoreRequirement·CostModification (토큰-패턴 검출).

## 3. 커버리지 행렬

### ② 키워드 25

| 요소 | 상태 | 클린 커버(예시) | STOP-예시 | AS-IS 보유 카드수 |
|---|---|---|---|---:|
| Vortex | 🟡 | — | EX8_074 | 12 |
| Overclock | ❌ | — | — | 12 |
| Execute | ❌ | — | — | 8 |
| Training | ❌ | — | — | 28 |
| MaterialSave | ❌ | — | — | 13 |
| Raid | 🟡 | — | AD1_025 | 80 |
| Alliance | 🟡 | — | BT22_035, EX8_074 | 80 |
| Blitz | ❌ | — | — | 24 |
| Retaliation | ✅ | BT2_074 | BT2_080, ST6_12 | 89 |
| Pierce | ✅ | BT1_022, BT1_026, BT1_081 | BT24_018 | 160 |
| Evade | ✅ | BT13_023 | — | 19 |
| Decode | ✅ | BT19_024 | — | 16 |
| Decoy | ❌ | — | — | 14 |
| Fragment | ✅ | EX8_051 | — | 7 |
| Partition | ✅ | BT16_025 | AD1_025 | 13 |
| Fortitude | ❌ | — | — | 29 |
| Scapegoat | ✅ | EX8_061 | — | 17 |
| Save | ❌ | — | — | 80 |
| Rush | ❌ | — | — | 98 |
| Iceclad | ❌ | — | — | 12 |
| Blocker | ✅ | BT19_071, BT1_023, BT1_031 | AD1_025, BT24_018 | 406 |
| Jamming | ✅ | BT1_016, BT1_098, BT2_057 | BT2_026 | 96 |
| Collision | ✅ | EX8_051 | — | 36 |
| Reboot | ✅ | BT2_055, BT2_063, BT2_065 | — | 121 |
| MindLink | ❌ | — | — | 11 |

### ②′ 키워드 잔여 8

| 요소 | 상태 | 클린 커버(예시) | STOP-예시 | AS-IS 보유 카드수 |
|---|---|---|---|---:|
| Barrier | ✅ | BT14_035 | BT15_037 | 56 |
| ArmorPurge | 🟡 | — | BT24_018 | 45 |
| Ascension | ❌ | — | — | 2 |
| Progress | 🟡 | — | BT24_018 | 7 |
| Link | 🟡 | — | BT22_035 | 73 |
| ArtsDigivolve | ❌ | — | — | 6 |
| BlastDigivolution | ∅ | — | — | 0 |
| BlastDNADigivolution | ∅ | — | — | 0 |

### ③ 창-타이밍 60

| 요소 | 상태 | 클린 커버(예시) | STOP-예시 | AS-IS 보유 카드수 |
|---|---|---|---|---:|
| OnUseOption | ❌ | — | — | 30 |
| OnDeclaration | ✅ | BT1_088, BT1_089, BT22_044 | BT22_035 | 298 |
| OnEnterFieldAnyone | ✅ | BT16_025, BT19_024, BT19_071 | AD1_025, BT15_083 | 2042 |
| OnGetDamage | ∅ | — | — | 0 |
| OptionSkill | ✅ | BT1_091, BT1_092, BT1_093 | BT1_090, BT1_104 | 522 |
| OnDestroyedAnyone | ✅ | BT1_030, BT1_035, BT1_049 | BT2_040, BT2_080 | 623 |
| WhenDigisorption | 🟡 | — | BT2_045, BT2_050 | 10 |
| WhenRemoveField | ✅ | BT16_025, BT19_024 | AD1_025, BT24_018 | 164 |
| WhenPermanentWouldBeDeleted | ✅ | BT13_023, BT14_035, EX8_051 | BT15_037, BT24_018 | 206 |
| WhenReturntoLibraryAnyone | ❌ | — | — | 9 |
| WhenReturntoHandAnyone | ❌ | — | — | 9 |
| WhenUntapAnyone | ❌ | — | — | 1 |
| OnEndAttackPhase | ∅ | — | — | 0 |
| OnEndTurn | ✅ | BT1_040 | BT1_021, BT1_090 | 249 |
| OnStartTurn | ✅ | BT1_085, BT1_086, BT1_087 | — | 120 |
| OnEndMainPhase | ∅ | — | — | 0 |
| OnDraw | ∅ | — | — | 0 |
| OnAddHand | ✅ | BT9_021 | BT15_083 | 21 |
| OnLoseSecurity | 🟡 | — | BT15_037, BT24_018 | 73 |
| OnAddSecurity | ✅ | BT8_090 | — | 14 |
| OnUseDigiburst | ❌ | — | — | 1 |
| OnDiscardHand | ✅ | ST16_14 | — | 34 |
| OnDiscardSecurity | 🟡 | — | BT15_037 | 14 |
| OnDiscardLibrary | ✅ | BT19_071 | — | 20 |
| OnKnockOut | ∅ | — | — | 0 |
| OnMove | ✅ | BT8_092 | — | 30 |
| OnEndCoinToss | ∅ | — | — | 0 |
| OnUseAttack | ∅ | — | — | 0 |
| OnTappedAnyone | ✅ | ST4_14 | — | 139 |
| OnUnTappedAnyone | ✅ | BT2_002, BT8_057 | — | 29 |
| OnAddDigivolutionCards | ✅ | BT22_044, EX6_001 | — | 50 |
| OnAllyAttack | ✅ | BT13_023, BT16_025, BT1_001 | AD1_025, BT1_021 | 944 |
| OnCounterTiming | ✅ | EX8_051 | — | 111 |
| OnBlockAnyone | ✅ | BT1_012, BT1_022, ST1_09 | — | 6 |
| OnSecurityCheck | ❌ | — | — | 9 |
| OnAttackTargetChanged | ✅ | EX10_002, ST15_02 | — | 31 |
| OnEndBlockDesignation | ∅ | — | — | 0 |
| SecuritySkill | ✅ | BT1_085, BT1_086, BT1_087 | BT15_083, BT2_084 | 894 |
| OnStartMainPhase | ✅ | ST15_02 | — | 222 |
| OnStartBattle | ∅ | — | — | 0 |
| OnEndBattle | ✅ | BT1_077, BT1_112, ST4_11 | — | 84 |
| OnDetermineDoSecurityCheck | ✅ | BT1_022, BT1_026, BT1_081 | BT24_018 | 119 |
| OnEndAttack | ✅ | BT19_024, BT1_081, BT9_062 | BT9_043 | 80 |
| BeforePayCost | 🟡 | — | BT2_045, BT2_050 | 141 |
| AfterPayCost | 🟡 | — | BT1_109 | 7 |
| OnDigivolutionCardDiscarded | ✅ | BT2_085, EX8_051 | — | 53 |
| OnDigivolutionCardReturnToDeckBottom | ❌ | — | — | 3 |
| OnReturnCardsToLibraryFromTrash | ❌ | — | — | 1 |
| OnPermamemtReturnedToHand | ❌ | — | — | 2 |
| OnReturnCardsToHandFromTrash | ❌ | — | — | 2 |
| AfterEffectsActivate | ❌ | — | — | 2 |
| WhenWouldDigivolutionCardDiscarded | ❌ | — | — | 1 |
| WhenWouldLink | ❌ | — | — | 2 |
| WhenLinked | ✅ | BT22_003 | BT22_035 | 64 |
| WhenTopCardTrashed | ❌ | — | — | 3 |
| RulesTiming | ∅ | — | — | 0 |
| OnRemovedField | ❌ | — | — | 2 |
| OnLinkCardDiscarded | ❌ | — | — | 7 |
| OnFaceUpSecurityIncreased | ❌ | — | — | 1 |
| OnLeaveFieldAnyone | 🟡 | — | AD1_025 | 1 |

### ④ Select* 11

| 요소 | 상태 | 클린 커버(예시) | STOP-예시 | AS-IS 보유 카드수 |
|---|---|---|---|---:|
| SelectPermanentEffect | ✅ | BT13_023, BT16_025, BT19_024 | AD1_025, BT1_104 | 2128 |
| SelectHandEffect | ✅ | BT19_024, BT1_039, BT1_056 | BT22_035, BT9_109 | 943 |
| SelectCardEffect | ✅ | BT19_024, BT1_010, BT1_011 | BT15_037, BT15_083 | 1749 |
| SelectAttackEffect | ❌ | — | — | 121 |
| SelectAppFusionEffect | ∅ | — | — | 0 |
| SelectBurstDigivolutionEffect | ∅ | — | — | 0 |
| SelectDigiXrosClass | ❌ | — | — | 5 |
| SelectAssemblyClass | ∅ | — | — | 0 |
| SelectJogressEffect | ∅ | — | — | 0 |
| SelectDNACondition | ❌ | — | — | 1 |
| SelectCountEffect | ❌ | — | — | 11 |

### ⑤ 특수플레이 14

| 요소 | 상태 | 클린 커버(예시) | STOP-예시 | AS-IS 보유 카드수 |
|---|---|---|---|---:|
| Jogress/DNA | ✅ | BT16_025 | AD1_025 | 123 |
| DigiXros | ❌ | — | — | 101 |
| Assembly | 🟡 | — | AD1_025 | 15 |
| AppFusion | ❌ | — | — | 10 |
| BurstDigivolution | ❌ | — | — | 5 |
| DigiBurst | ✅ | ST4_13 | — | 39 |
| TokenPlay | ✅ | BT8_092 | BT9_109 | 186 |
| DelayOption | ❌ | — | — | 148 |
| HatchDigiEgg | ✅ | BT1_089 | — | 10 |
| Digisorption | 🟡 | — | BT2_045, BT2_050 | 19 |
| AceOverflow | ❌ | — | — | 3 |
| ExecutingAreaDigivolve | ❌ | — | — | 15 |
| IgnoreRequirement | 🟡 | — | BT9_109 | 164 |
| CostModification | ✅ | BT2_023, BT2_099 | BT1_109, BT2_045 | 237 |

### ① 프리미티브 179 (정의서 판정 병기)

#### A.CardEffectFactory (41)

| 심볼 | 정의서 판정(07-08/09) | 커버리지 | 보정 플래그 |
|---|---|---|---|
| ActivateClass | PASS | ✅ 141장 (BT13_023, BT16_025) | — |
| ActivateClassesForSharedEffects | FAIL | ❌ 미커버 (AS-IS 84장) | — |
| ActivateMainOptionSecurityEffect | PARTIAL | ∅ 카드-표면 없음 | — |
| AddDetailClass | PARTIAL | ❌ 미커버 (AS-IS 8장) | — |
| AllTurnsClass | FAIL | ∅ 카드-표면 없음 | 타이밍-폴딩(uniform ActivateClass, 결과동일) |
| CounterClass | FAIL | ∅ 카드-표면 없음 | 타이밍-폴딩(uniform ActivateClass, 결과동일) |
| DigiXrosEffectFromNames | PARTIAL | ❌ 미커버 (AS-IS 1장) | — |
| EndOfAllTurnsClass | FAIL | ∅ 카드-표면 없음 | 타이밍-폴딩(uniform ActivateClass, 결과동일) |
| EndOfAttackClass | FAIL | ∅ 카드-표면 없음 | 타이밍-폴딩(uniform ActivateClass, 결과동일) |
| EndOfYourOpponentsTurnClass | FAIL | ∅ 카드-표면 없음 | 타이밍-폴딩(uniform ActivateClass, 결과동일) |
| EndOfYourTurnClass | FAIL | ∅ 카드-표면 없음 | 타이밍-폴딩(uniform ActivateClass, 결과동일) |
| EoTLose3Memory | PARTIAL | ✅ 1장 (BT1_040) | — |
| Gain1MemoryTamerOpponentDigimonEffect | PARTIAL | ❌ 미커버 (AS-IS 17장) | — |
| Gain1MemoryTamerOwnerDigimonConditionalEffect | PARTIAL | ❌ 미커버 (AS-IS 3장) | — |
| Gain2MemoryOptionDelayEffect | 상환 | ❌ 미커버 (AS-IS 13장) | — |
| GetJogressConditionClass | PASS | 🟡 STOP만 (AD1_025) | — |
| OnDeletionClass | FAIL | ❌ 미커버 (AS-IS 1장) | 타이밍-폴딩(uniform ActivateClass, 결과동일) |
| OnPlayClass | FAIL | ∅ 카드-표면 없음 | 타이밍-폴딩(uniform ActivateClass, 결과동일) |
| OpponentsTurnClass | FAIL | ∅ 카드-표면 없음 | 타이밍-폴딩(uniform ActivateClass, 결과동일) |
| PlaceSelfDelayOptionSecurityEffect | PARTIAL | ❌ 미커버 (AS-IS 54장) | — |
| PlaceToSecurityEffect | FAIL | ∅ 카드-표면 없음 | — |
| PlayMindLinkTamerFromDigivolutionCards | 상환 | ❌ 미커버 (AS-IS 1장) | — |
| PlaySelfDigimonAfterBattleSecurityEffect | 상환 | ❌ 미커버 (AS-IS 35장) | — |
| PlaySelfTamerSecurityEffect | PARTIAL | ✅ 15장 (BT1_085, BT1_086) | — |
| ReplaceBottomSecurityWithFaceUpOptionEffect | (정의서 내 미기재/집계행) | ❌ 미커버 (AS-IS 7장) | — |
| ReplaceBottomSecurityWithFaceUpOptionMainEffect | (정의서 내 미기재/집계행) | ❌ 미커버 (AS-IS 7장) | — |
| ReplaceTopSecurityWithFaceUpOptionEffect | (정의서 내 미기재/집계행) | ∅ 카드-표면 없음 | — |
| ReplaceTopSecurityWithFaceUpOptionMainEffect | (정의서 내 미기재/집계행) | ❌ 미커버 (AS-IS 1장) | — |
| SecurityClass | FAIL | ∅ 카드-표면 없음 | 타이밍-폴딩(uniform ActivateClass, 결과동일) |
| SetMemoryTo3TamerEffect | PARTIAL | ✅ 7장 (BT1_085, BT1_086) | — |
| StartOfOpponentsTurnClass | FAIL | ∅ 카드-표면 없음 | 타이밍-폴딩(uniform ActivateClass, 결과동일) |
| StartOfYourMainPhaseClass | 상환 | ❌ 미커버 (AS-IS 1장) | 타이밍-폴딩(uniform ActivateClass, 결과동일) |
| StartOfYourOpponentsMainPhaseClass | FAIL | ∅ 카드-표면 없음 | 타이밍-폴딩(uniform ActivateClass, 결과동일) |
| StartOfYourTurnClass | FAIL | ❌ 미커버 (AS-IS 1장) | 타이밍-폴딩(uniform ActivateClass, 결과동일) |
| TurnTimingClass | FAIL | ∅ 카드-표면 없음 | 타이밍-폴딩(uniform ActivateClass, 결과동일) |
| UseRequirements | PASS | ❌ 미커버 (AS-IS 16장) | — |
| WhenAttackingClass | FAIL | ∅ 카드-표면 없음 | 타이밍-폴딩(uniform ActivateClass, 결과동일) |
| WhenDigivolvingClass | FAIL | ❌ 미커버 (AS-IS 1장) | 타이밍-폴딩(uniform ActivateClass, 결과동일) |
| WhenLinkingClass | FAIL | ∅ 카드-표면 없음 | 타이밍-폴딩(uniform ActivateClass, 결과동일) |
| WhenMovingClass | FAIL | ❌ 미커버 (AS-IS 1장) | 타이밍-폴딩(uniform ActivateClass, 결과동일) |
| YourTurnClass | FAIL | ∅ 카드-표면 없음 | 타이밍-폴딩(uniform ActivateClass, 결과동일) |

#### B.Commons(non-token) (22)

| 심볼 | 정의서 판정(07-08/09) | 커버리지 | 보정 플래그 |
|---|---|---|---|
| ActivateMainOfOptionSide | PARTIAL | ❌ 미커버 (AS-IS 1장) | — |
| AddActivateMainOptionSecurityEffect | PARTIAL | ✅ 16장 (BT1_094, BT1_101) | — |
| AddThisCardToHand | PASS | ✅ 10장 (BT1_093, BT1_096) | — |
| BouncePeremanentAndProcessAccordingToResult | PASS | ❌ 미커버 (AS-IS 22장) | — |
| DeckBouncePeremanentAndProcessAccordingToResult | PASS | ❌ 미커버 (AS-IS 15장) | — |
| DeletePeremanentAndProcessAccordingToResult | PASS | 🟡 STOP만 (BT24_018) | — |
| DigivolveIntoExcecutingAreaCard | FAIL | ❌ 미커버 (AS-IS 1장) | — |
| DigivolveIntoHandOrTrashCard | FAIL | ❌ 미커버 (AS-IS 311장) | — |
| DrawAndDiscardCards | PARTIAL | ❌ 미커버 (AS-IS 3장) | — |
| GetCardEffectByEffectTiming | FAIL | ∅ 카드-표면 없음 | — |
| OptionMainEffect | 상환 | ❌ 미커버 (AS-IS 5장) | — |
| OptionSecurityEffect | FAIL | ❌ 미커버 (AS-IS 2장) | — |
| PlaceDelayOptionCards | PASS | ❌ 미커버 (AS-IS 148장) | — |
| PlacePermanentInSecurityAndProcessAccordingToResult | 상환 | ❌ 미커버 (AS-IS 6장) | — |
| PlayOptionCards | PARTIAL | ❌ 미커버 (AS-IS 34장) | — |
| PlayPermanentCards | PARTIAL | ✅ 6장 (BT19_024, BT1_044) | — |
| SuspendPeremanentAndProcessAccordingToResult | PASS | ❌ 미커버 (AS-IS 7장) | — |
| TrashDigivolutionCardsAndProcessAccordingToResult | FAIL | ❌ 미커버 (AS-IS 8장) | — |
| TrashDigivolutionCardsFromTopOrBottom | PARTIAL | ✅ 9장 (BT13_023, BT1_043) | — |
| TrashHandAndProcessAccordingToResult | (정의서 내 미기재/집계행) | ❌ 미커버 (AS-IS 1장) | — |
| TrashLinkCardsAndProcessAccordingToResult | (정의서 내 미기재/집계행) | ❌ 미커버 (AS-IS 11장) | — |
| TrashSecurityAndProcessAccordingToResult | (정의서 내 미기재/집계행) | ❌ 미커버 (AS-IS 5장) | — |

#### C.Commons(token) (18)

| 심볼 | 정의서 판정(07-08/09) | 커버리지 | 보정 플래그 |
|---|---|---|---|
| PlayAmonToken | (정의서 내 미기재/집계행) | ❌ 미커버 (AS-IS 1장) | — |
| PlayAthoRenePorToken | (정의서 내 미기재/집계행) | ❌ 미커버 (AS-IS 2장) | — |
| PlayDiaboromonToken | (정의서 내 미기재/집계행) | ❌ 미커버 (AS-IS 16장) | — |
| PlayFamiliarToken | (정의서 내 미기재/집계행) | ❌ 미커버 (AS-IS 5장) | — |
| PlayFujitsumonToken | (정의서 내 미기재/집계행) | ❌ 미커버 (AS-IS 1장) | — |
| PlayGyuukimonToken | (정의서 내 미기재/집계행) | ❌ 미커버 (AS-IS 1장) | — |
| PlayHinukamuyToken | (정의서 내 미기재/집계행) | ❌ 미커버 (AS-IS 1장) | — |
| PlayKoHagurumonToken | (정의서 내 미기재/집계행) | ❌ 미커버 (AS-IS 1장) | — |
| PlayPetrificationToken | (정의서 내 미기재/집계행) | ❌ 미커버 (AS-IS 3장) | — |
| PlayPipeFox | (정의서 내 미기재/집계행) | ❌ 미커버 (AS-IS 3장) | — |
| PlayRapidmonToken | (정의서 내 미기재/집계행) | ❌ 미커버 (AS-IS 1장) | — |
| PlaySelfDeleteFamiliarToken | (정의서 내 미기재/집계행) | ❌ 미커버 (AS-IS 1장) | — |
| PlayTaomonToken | (정의서 내 미기재/집계행) | ❌ 미커버 (AS-IS 1장) | — |
| PlayToken | 상환 | ∅ 카드-표면 없음 | — |
| PlayUkaNoMitama | (정의서 내 미기재/집계행) | ❌ 미커버 (AS-IS 1장) | — |
| PlayUmonToken | (정의서 내 미기재/집계행) | ❌ 미커버 (AS-IS 1장) | — |
| PlayVoleeZerdrucken | (정의서 내 미기재/집계행) | ❌ 미커버 (AS-IS 1장) | — |
| PlayWarGrowlmonToken | (정의서 내 미기재/집계행) | ❌ 미커버 (AS-IS 1장) | — |

#### D.PermanentEffectFactory (6)

| 심볼 | 정의서 판정(07-08/09) | 커버리지 | 보정 플래그 |
|---|---|---|---|
| AddDetailClass | PARTIAL | ❌ 미커버 (AS-IS 8장) | — |
| CanNotSwitchAttackTargetEffect | (정의서 내 미기재/집계행) | ❌ 미커버 (AS-IS 1장) | — |
| CollisionEffect | 상환 | ∅ 카드-표면 없음 | — |
| DeleteSelfEffect | LIVE(오분류정정) | ∅ 카드-표면 없음 | — |
| DigimonEffectImmunity | 상환 | ❌ 미커버 (AS-IS 8장) | — |
| OptionEffectImmunity | 상환 | ❌ 미커버 (AS-IS 1장) | — |

#### E.EffectClass (95)

| 심볼 | 정의서 판정(07-08/09) | 커버리지 | 보정 플래그 |
|---|---|---|---|
| AceOverflowClass | PASS | ❌ 미커버 (AS-IS 3장) | — |
| ActivateClass | PASS | ✅ 141장 (BT13_023, BT16_025) | — |
| AddAppFusionConditionClass | PASS | ❌ 미커버 (AS-IS 4장) | — |
| AddAssemblyConditionClass | PASS | 🟡 STOP만 (AD1_025) | — |
| AddBurstDigivolutionConditionClass | PARTIAL | ❌ 미커버 (AS-IS 5장) | — |
| AddDetailClass | PARTIAL | ❌ 미커버 (AS-IS 8장) | — |
| AddDigiXrosConditionClass | PARTIAL | ❌ 미커버 (AS-IS 79장) | — |
| AddDigivolutionRequirementClass | FAIL | ❌ 미커버 (AS-IS 3장) | — |
| AddJogressConditionClass | PARTIAL | ✅ 1장 (BT16_025) | — |
| AddJogressLevelsClass | PARTIAL | ❌ 미커버 (AS-IS 4장) | — |
| AddLinkConditionClass | PASS | ❌ 미커버 (AS-IS 1장) | — |
| AddMaxTrashCountDigiXrosClass | PASS | ❌ 미커버 (AS-IS 9장) | — |
| AddMaxUnderTamerCountDigiXrosClass | PASS | ❌ 미커버 (AS-IS 8장) | — |
| AddSkillClass | FAIL | 🟡 STOP만 (BT1_104) | — |
| ArmorPurgeClass | PASS | ∅ 카드-표면 없음 | — |
| BlockerClass | PASS | ∅ 카드-표면 없음 | A군 창-재하우징(18/18) 이후 상태 — 정의서 판정은 스테일 가능 |
| CanAttackTargetDefendingPermanentClass | MISSING | ❌ 미커버 (AS-IS 20장) | — |
| CanNotAffectedClass | PARTIAL | ❌ 미커버 (AS-IS 40장) | — |
| CanNotAttackTargetDefendingPermanentClass | PARTIAL | ∅ 카드-표면 없음 | — |
| CanNotBeDestroyedByBattleClass | PASS | ∅ 카드-표면 없음 | — |
| CanNotBeDestroyedBySkillClass | 상환 | ∅ 카드-표면 없음 | — |
| CanNotBeDestroyedClass | PASS | ∅ 카드-표면 없음 | — |
| CanNotBeRemovedClass | 상환 | ❌ 미커버 (AS-IS 2장) | — |
| CanNotDigivolveClass | FAIL | ❌ 미커버 (AS-IS 15장) | — |
| CanNotMoveClass | 상환 | ❌ 미커버 (AS-IS 1장) | — |
| CanNotPlayClass | MISSING | ✅ 2장 (BT8_057, EX1_072) | — |
| CanNotPutFieldClass | MISSING | ❌ 미커버 (AS-IS 7장) | — |
| CanNotSelectBySkillClass | 상환 | ∅ 카드-표면 없음 | — |
| CanNotSuspendClass | PASS | ❌ 미커버 (AS-IS 33장) | — |
| CanNotSwitchAttackTargetClass | PASS | ❌ 미커버 (AS-IS 13장) | — |
| CanNotTrashFromDigivolutionCardsClass | FAIL | 🟡 STOP만 (BT9_109) | — |
| CanNotUnsuspendClass | PASS | ❌ 미커버 (AS-IS 3장) | — |
| CanSelectAssemblyClass | MISSING | ∅ 카드-표면 없음 | — |
| CanSelectDigiXrosClass | MISSING | ❌ 미커버 (AS-IS 1장) | — |
| CanSuspendByDigisorptionClass | MISSING | ❌ 미커버 (AS-IS 1장) | — |
| CannotAddMemoryClass | PARTIAL | ❌ 미커버 (AS-IS 6장) | — |
| CannotAddSecurityClass | PARTIAL | ❌ 미커버 (AS-IS 1장) | — |
| CannotBlockClass | PARTIAL | ∅ 카드-표면 없음 | — |
| CannotIgnoreDigivolutionConditionClass | 상환 | ❌ 미커버 (AS-IS 1장) | — |
| CannotReduceCostClass | 상환 | ❌ 미커버 (AS-IS 5장) | — |
| CannotReturnToHandClass | PASS | ∅ 카드-표면 없음 | — |
| CannotReturnToLibraryClass | 상환 | ∅ 카드-표면 없음 | — |
| ChangeBaseCardColorClass | PASS | ❌ 미커버 (AS-IS 3장) | — |
| ChangeBaseCardNameClass | 상환 | ❌ 미커버 (AS-IS 2장) | — |
| ChangeBaseDPClass | PARTIAL | ❌ 미커버 (AS-IS 13장) | — |
| ChangeCardColorClass | PASS | ❌ 미커버 (AS-IS 14장) | — |
| ChangeCardDPClass | PARTIAL | ∅ 카드-표면 없음 | — |
| ChangeCardLevelClass | PASS | ❌ 미커버 (AS-IS 1장) | — |
| ChangeCardLevelForAssemblyClass | MISSING | ❌ 미커버 (AS-IS 1장) | — |
| ChangeCardNamesClass | PARTIAL | ❌ 미커버 (AS-IS 63장) | — |
| ChangeCardNamesForDigiXrosClass | MISSING | ❌ 미커버 (AS-IS 8장) | — |
| ChangeCostClass | PARTIAL | ✅ 2장 (BT2_023, BT2_099) | — |
| ChangeDPClass | PARTIAL | ❌ 미커버 (AS-IS 2장) | — |
| ChangeDPDeleteEffectMaxDPClass | PARTIAL | ❌ 미커버 (AS-IS 11장) | — |
| ChangeEndTurnMinMemoryClass | 상환 | ❌ 미커버 (AS-IS 2장) | — |
| ChangeLinkCostClass | PARTIAL | ∅ 카드-표면 없음 | — |
| ChangeLinkMaxClass | FAIL | ∅ 카드-표면 없음 | — |
| ChangePermanentLevelClass | PASS | ❌ 미커버 (AS-IS 6장) | — |
| ChangeSAttackClass | FAIL | ∅ 카드-표면 없음 | — |
| ChangeTraitsClass | PASS | ❌ 미커버 (AS-IS 1장) | — |
| CollisionClass | FAIL | ∅ 카드-표면 없음 | A군 창-재하우징(18/18) 이후 상태 — 정의서 판정은 스테일 가능 |
| DeckBottomBounceClass | PARTIAL | 🟡 STOP만 (AD1_025) | — |
| DeckTopBounceClass | MISSING | ∅ 카드-표면 없음 | — |
| DestroyPermanentsClass | PARTIAL | ✅ 2장 (BT1_084, BT9_081) | — |
| DisableEffectClass | FAIL | ✅ 1장 (BT1_025) | — |
| DontBattleSecurityDigimonClass | 상환 | ❌ 미커버 (AS-IS 5장) | — |
| DontHaveDPClass | 상환 | ❌ 미커버 (AS-IS 6장) | — |
| DrawClass | PARTIAL | ✅ 18장 (BT1_003, BT1_006) | — |
| EmptyEffectClass | PASS | ∅ 카드-표면 없음 | — |
| HatchDigiEggClass | PARTIAL | ✅ 1장 (BT1_089) | — |
| IcecladClass | PASS | ∅ 카드-표면 없음 | A군 창-재하우징(18/18) 이후 상태 — 정의서 판정은 스테일 가능 |
| IgnoreColorConditionClass | PASS | 🟡 STOP만 (BT9_109) | — |
| ImmuneFromDPMinusClass | 상환 | ∅ 카드-표면 없음 | — |
| ImmuneFromDeDigivolveClass | 상환 | ❌ 미커버 (AS-IS 11장) | — |
| ImmuneStackTrashingClass | PASS | ❌ 미커버 (AS-IS 2장) | — |
| InvertSAttackClass | 상환 | ∅ 카드-표면 없음 | — |
| MindLinkClass | PASS | ❌ 미커버 (AS-IS 11장) | A군 창-재하우징(18/18) 이후 상태 — 정의서 판정은 스테일 가능 |
| OptionResolutionClass | PARTIAL | ∅ 카드-표면 없음 | — |
| PartitionClass | PASS | ∅ 카드-표면 없음 | A군 창-재하우징(18/18) 이후 상태 — 정의서 판정은 스테일 가능 |
| PlayCardClass | PARTIAL | ✅ 1장 (BT1_078) | — |
| PlayPermanentClass | PARTIAL | ∅ 카드-표면 없음 | — |
| RebootClass | PASS | ∅ 카드-표면 없음 | A군 창-재하우징(18/18) 이후 상태 — 정의서 판정은 스테일 가능 |
| ReturnToLibraryBottomDigivolutionCardsClass | 상환 | ❌ 미커버 (AS-IS 14장) | — |
| RevealLibraryClass | PARTIAL | ❌ 미커버 (AS-IS 2장) | — |
| RushClass | PARTIAL | ❌ 미커버 (AS-IS 1장) | A군 창-재하우징(18/18) 이후 상태 — 정의서 판정은 스테일 가능 |
| ScapegoatClass | LIVE(오분류정정) | ∅ 카드-표면 없음 | A군 창-재하우징(18/18) 이후 상태 — 정의서 판정은 스테일 가능 |
| SelectAssemblyClass | PARTIAL | ∅ 카드-표면 없음 | — |
| SelectCardConditionClass | FAIL | ❌ 미커버 (AS-IS 25장) | — |
| SelectDigiXrosClass | FAIL | ❌ 미커버 (AS-IS 5장) | — |
| SimplifiedSelectCardConditionClass | PARTIAL | ✅ 9장 (BT1_010, BT1_048) | — |
| SuspendPermanentsClass | PARTIAL | ✅ 10장 (BT16_025, BT1_086) | — |
| TrainingClass | PARTIAL | ∅ 카드-표면 없음 | A군 창-재하우징(18/18) 이후 상태 — 정의서 판정은 스테일 가능 |
| TreatAsDigimonClass | FAIL | ❌ 미커버 (AS-IS 7장) | A군 창-재하우징(18/18) 이후 상태 — 정의서 판정은 스테일 가능 |
| UseOptionClass | PARTIAL | ∅ 카드-표면 없음 | — |
| VortexCanAttackPlayersClass | FAIL | ∅ 카드-표면 없음 | A군 창-재하우징(18/18) 이후 상태 — 정의서 판정은 스테일 가능 |

## 4. set-cover 후보 (사용자 witness 선정용)

greedy 최소집합: 미커버(+stop-only) 164개 요소를 **87장**으로 커버(카드당 신규 커버 요소수 내림차순). 상위 30장:

| # | 카드 | 신규커버 | 덮는 축 요소 | STOP-예상(RD-*) |
|---|---|---:|---|---|
| 1 | BT25_104(ShineGreymon: Burst Mode·D) | 14 | K:ArtsDigivolve, K:Raid, K:Rush, P:ActivateClassesForSharedEffects, P:ActivateMainOfOptionSide, P:AddBurstDigivolutionConditionClass, P:ChangeBaseDPClass, P:RushClass, P:StartOfYourTurnClass, P:TreatAsDigimonClass, P:UseRequirements, P:WhenDigivolvingClass, P:WhenMovingClass, X:BurstDigivolution | AddBurstDigivolutionConditionClass→RD-P6C1-6; ActivateClassesForSharedEffects→공유; BurstDigivolution→RD-P6C1-6 |
| 2 | LM_054(Treadmill Training·O) | 7 | K:Training, P:DeletePeremanentAndProcessAccordingToResult, P:DigivolveIntoHandOrTrashCard, P:IgnoreColorConditionClass, P:PlaceDelayOptionCards, X:DelayOption, X:IgnoreRequirement | — |
| 3 | BT21_030(Shoutmon X7: Superior Mode·D) | 7 | P:AddDigiXrosConditionClass, P:AddMaxTrashCountDigiXrosClass, P:DeckBottomBounceClass, P:SelectDigiXrosClass, S:SelectDigiXrosClass, T:BeforePayCost, X:DigiXros | DigiXros→RD-P6C1-5/RD-R5-04; SelectDigiXrosClass→RD-P6C1-5 |
| 4 | BT19_091(Trinity Burst!·O) | 5 | K:Alliance, P:PlayRapidmonToken, P:PlayTaomonToken, P:PlayWarGrowlmonToken, S:SelectAttackEffect | — |
| 5 | EX11_070(Unchained·T) | 5 | K:MindLink, P:ChangeDPClass, P:ImmuneStackTrashingClass, P:MindLinkClass, P:PlayMindLinkTamerFromDigivolutionCards | — |
| 6 | BT17_026(Beowolfmon·D) | 4 | P:CanNotSuspendClass, P:ChangeCardColorClass, P:ChangePermanentLevelClass, P:DontHaveDPClass | — |
| 7 | EX10_029(Warpmon·D) | 4 | K:Link, P:ImmuneFromDeDigivolveClass, P:PlaySelfDigimonAfterBattleSecurityEffect, P:TrashLinkCardsAndProcessAccordingToResult | — |
| 8 | P_223(Kuzuhamon·D) | 4 | P:ChangeCardNamesClass, P:PlayOptionCards, P:PlayPipeFox, T:OnUseOption | — |
| 9 | BT25_040(MagnaAngemon·D) | 4 | K:Ascension, P:TrashSecurityAndProcessAccordingToResult, T:OnDiscardSecurity, T:OnLoseSecurity | Ascension→RD-3A-01/RD-P6C3-A3; ActivateClassesForSharedEffects→공유 |
| 10 | BT24_062(MasterBlimpmon·D) | 4 | K:ArmorPurge, P:AddAssemblyConditionClass, P:CanNotSwitchAttackTargetClass, X:Assembly | Assembly→RD-P6C1-5; AddAssemblyConditionClass→RD-P6C1-5 |
| 11 | BT5_086(Omnimon·D) | 3 | K:Blitz, T:WhenReturntoHandAnyone, T:WhenReturntoLibraryAnyone | — |
| 12 | EX10_010(BlackWarGreymon·D) | 3 | P:CanNotAffectedClass, T:OnRemovedField, T:WhenTopCardTrashed | — |
| 13 | LM_047(Chartreuse Memory Boost!·O) | 3 | P:Gain2MemoryOptionDelayEffect, P:PlaceSelfDelayOptionSecurityEffect, P:SelectCardConditionClass | SelectCardConditionClass→고급 |
| 14 | BT25_089(Kazuki & Itsuki·T) | 3 | P:Gain1MemoryTamerOpponentDigimonEffect, P:SuspendPeremanentAndProcessAccordingToResult, X:AppFusion | AppFusion→RD-P6C1-6 |
| 15 | EX7_072(Seventh Fascination·O) | 3 | P:AddDetailClass, P:AddSkillClass, P:OptionMainEffect | AddSkillClass→nested-grant |
| 16 | BT3_056(Ceresmon·D) | 3 | P:CanSuspendByDigisorptionClass, T:WhenDigisorption, X:Digisorption | CanSuspendByDigisorptionClass→G11; Digisorption→G11 |
| 17 | BT18_042(MagnaGarurumon·D) | 2 | P:AceOverflowClass, X:AceOverflow | — |
| 18 | ST17_13(Magnamon·D) | 2 | P:DigivolveIntoExcecutingAreaCard, X:ExecutingAreaDigivolve | — |
| 19 | AD1_011(Paildramon·D) | 2 | P:CanNotSwitchAttackTargetEffect, P:GetJogressConditionClass | — |
| 20 | BT20_017(Jesmon·D) | 2 | K:Decoy, P:PlayAthoRenePorToken | — |
| 21 | BT14_030(MarineAngemon·D) | 2 | P:BouncePeremanentAndProcessAccordingToResult, T:OnPermamemtReturnedToHand | — |
| 22 | BT14_097(Suka's Curse·O) | 2 | P:ChangeBaseCardColorClass, P:ChangeBaseCardNameClass | — |
| 23 | BT14_018(Goldramon·D) | 2 | P:PlayAmonToken, P:PlayUmonToken | — |
| 24 | BT22_040(Cendrillmon·D) | 2 | K:Overclock, P:PlayFamiliarToken | — |
| 25 | BT21_029(Medusamon·D) | 2 | K:Progress, P:PlayPetrificationToken | — |
| 26 | EX5_055(HeavyLeomon·D) | 2 | K:Fortitude, P:DeckBouncePeremanentAndProcessAccordingToResult | — |
| 27 | EX5_053(Baihumon·D) | 2 | P:DontBattleSecurityDigimonClass, T:OnSecurityCheck | — |
| 28 | EX11_074(Vortexdramon·D) | 2 | K:Vortex, P:DigimonEffectImmunity | — |
| 29 | EX7_014(Volcanicdramon·D) | 2 | P:CanNotMoveClass, P:CanNotPutFieldClass | CanNotPutFieldClass→MISSING |
| 30 | EX10_045(Tuwarmon·D) | 2 | K:Save, P:TrashDigivolutionCardsAndProcessAccordingToResult | DigiXros→RD-P6C1-5/RD-R5-04 |

나머지 후보(31~87위)는 신규커버 1~2 요소의 롱테일 — 선정 시 `.tmp/coverage_audit/result.json`의 `greedy` 전체 리스트 참조.

## 5. 미커버 축 상세 — AS-IS 호출 카드

| 요소 | 상태 | AS-IS 카드수 | 호출 카드(선두 8) |
|---|---|---:|---|
| K:Alliance | 🟡stop-only | 80 | AD1_009, AD1_012, AD1_016, BT14_087, BT15_087, BT16_079, BT17_049, BT17_050 |
| K:ArmorPurge | 🟡stop-only | 45 | BT10_012, BT10_015, BT10_026, BT10_074, BT11_030, BT12_084, BT14_039, BT16_009 |
| K:ArtsDigivolve | ❌ | 6 | BT25_043, BT25_057, BT25_085, BT25_104, ST23_09, ST24_07 |
| K:Ascension | ❌ | 2 | BT25_034, BT25_040 |
| K:Blitz | ❌ | 24 | BT10_014, BT10_070, BT10_112, BT11_017, BT14_017, BT16_015, BT18_016, BT5_009 |
| K:Decoy | ❌ | 14 | BT11_082, BT16_052, BT16_052_token, BT19_031, BT20_017, BT20_017_token, BT6_059, BT6_064 |
| K:Execute | ❌ | 8 | BT20_072, BT20_079, BT23_069, BT23_071, BT24_081, EX11_051, EX11_068, P_208 |
| K:Fortitude | ❌ | 29 | BT20_034, BT20_035, BT22_051, BT23_046, BT24_038, BT24_049, BT25_015, BT25_058 |
| K:Iceclad | ❌ | 12 | BT18_026, BT22_077, BT25_103, EX11_016, EX11_017, EX7_017, EX7_021, EX7_023 |
| K:Link | 🟡stop-only | 73 | BT21_009, BT21_018, BT21_023, BT21_041, BT21_043, BT21_047, BT21_053, BT21_054 |
| K:MaterialSave | ❌ | 13 | BT10_009, BT10_013, BT10_024, BT10_111, BT11_009, BT11_012, BT11_018, BT11_019 |
| K:MindLink | ❌ | 11 | BT14_086, BT14_087, BT15_086, BT15_087, BT16_086, BT16_087, BT17_086, BT17_091 |
| K:Overclock | ❌ | 12 | BT19_101, BT22_036, BT22_040, BT22_042, BT24_065, BT24_079, EX11_024, EX11_060 |
| K:Progress | 🟡stop-only | 7 | BT21_025, BT21_029, BT24_017, BT24_018, EX11_012, EX11_054, P_189 |
| K:Raid | 🟡stop-only | 80 | AD1_001, AD1_003, AD1_004, AD1_007, AD1_008, AD1_025, BT11_010, BT11_014 |
| K:Rush | ❌ | 98 | AD1_002, AD1_008, AD1_021, BT10_008, BT10_024, BT10_070, BT11_019, BT11_054 |
| K:Save | ❌ | 80 | BT10_008, BT10_019, BT10_020, BT10_021, BT10_029, BT10_034, BT10_049, BT10_060 |
| K:Training | ❌ | 28 | EX9_008, EX9_009, EX9_010, EX9_015, EX9_016, EX9_017, EX9_022, EX9_025 |
| K:Vortex | 🟡stop-only | 12 | BT20_101, BT21_095, BT25_053, EX11_035, EX11_036, EX11_074, EX7_034, EX7_036 |
| P:AceOverflowClass | ❌ | 3 | BT17_098, BT18_042, BT24_093 |
| P:ActivateClassesForSharedEffects | ❌ | 84 | AD1_002, AD1_015, BT15_066, BT22_063, BT25_008, BT25_011, BT25_012, BT25_013 |
| P:ActivateMainOfOptionSide | ❌ | 1 | BT25_104 |
| P:AddAppFusionConditionClass | ❌ | 4 | BT21_059, BT23_021, BT23_022, BT23_024 |
| P:AddAssemblyConditionClass | 🟡stop-only | 14 | AD1_009, AD1_012, AD1_025, BT22_078, BT24_062, BT24_081, EX11_036, EX11_045 |
| P:AddBurstDigivolutionConditionClass | ❌ | 5 | BT13_020, BT13_033, BT13_060, BT13_092, BT25_104 |
| P:AddDetailClass | ❌ | 8 | BT11_087, BT14_044, BT15_078, BT20_059, BT25_054, EX1_068, EX6_057, EX7_072 |
| P:AddDigiXrosConditionClass | ❌ | 79 | BT10_009, BT10_012, BT10_013, BT10_015, BT10_024, BT10_026, BT10_061, BT10_063 |
| P:AddDigivolutionRequirementClass | ❌ | 3 | BT13_028, BT13_055, BT7_112 |
| P:AddJogressLevelsClass | ❌ | 4 | BT20_025, BT20_042, EX3_020, EX3_041 |
| P:AddLinkConditionClass | ❌ | 1 | ST22_08 |
| P:AddMaxTrashCountDigiXrosClass | ❌ | 9 | BT10_104, BT11_086, BT12_112, BT17_057, BT18_065, BT19_087, BT21_030, EX10_064 |
| P:AddMaxUnderTamerCountDigiXrosClass | ❌ | 8 | BT10_087, BT10_088, BT11_095, BT19_079, BT19_081, BT19_087, EX10_064, EX4_062 |
| P:AddSkillClass | 🟡stop-only | 42 | BT10_011, BT10_056, BT11_083, BT11_103, BT12_072, BT15_039, BT15_078, BT16_014 |
| P:BouncePeremanentAndProcessAccordingToResult | ❌ | 22 | BT13_010, BT14_030, BT15_082, BT16_084, BT16_085, BT16_088, BT17_025, BT17_039 |
| P:CanAttackTargetDefendingPermanentClass | ❌ | 20 | BT10_016, BT17_060, BT20_019, BT21_096, BT2_051, BT4_090, BT5_017, BT6_093 |
| P:CanNotAffectedClass | ❌ | 40 | AD1_008, BT11_093, BT13_077, BT13_088, BT13_108, BT15_047, BT15_049, BT15_053 |
| P:CanNotBeRemovedClass | ❌ | 2 | BT16_051, EX6_044 |
| P:CanNotDigivolveClass | ❌ | 15 | BT19_073, BT22_062, BT23_017, BT23_037, BT23_048, BT25_072, BT25_074, EX11_045 |
| P:CanNotMoveClass | ❌ | 1 | EX7_014 |
| P:CanNotPutFieldClass | ❌ | 7 | BT14_017, BT20_020, BT23_014, BT8_097, BT9_033, EX3_012, EX7_014 |
| P:CanNotSuspendClass | ❌ | 33 | AD1_014, BT15_026, BT15_101, BT16_069, BT17_026, BT17_027, BT18_025, BT19_101 |
| P:CanNotSwitchAttackTargetClass | ❌ | 13 | AD1_012, BT13_029, BT19_023, BT20_026, BT20_052, BT21_038, BT24_062, BT25_026 |
| P:CanNotSwitchAttackTargetEffect | ❌ | 1 | AD1_011 |
| P:CanNotTrashFromDigivolutionCardsClass | 🟡stop-only | 1 | BT9_109 |
| P:CanNotUnsuspendClass | ❌ | 3 | BT24_050, BT25_061, ST24_10 |
| P:CanSelectDigiXrosClass | ❌ | 1 | BT10_111 |
| P:CanSuspendByDigisorptionClass | ❌ | 1 | BT3_056 |
| P:CannotAddMemoryClass | ❌ | 6 | BT18_009, BT18_059, BT25_079, BT3_046, EX8_030, ST21_02 |
| P:CannotAddSecurityClass | ❌ | 1 | BT9_103 |
| P:CannotIgnoreDigivolutionConditionClass | ❌ | 1 | BT8_059 |
| P:CannotReduceCostClass | ❌ | 5 | BT5_008, BT5_021, BT8_071, EX7_015, ST20_07 |
| P:ChangeBaseCardColorClass | ❌ | 3 | BT11_043, BT14_097, BT18_078 |
| P:ChangeBaseCardNameClass | ❌ | 2 | BT11_043, BT14_097 |
| P:ChangeBaseDPClass | ❌ | 13 | BT10_086, BT12_031, BT13_007, BT16_095, BT17_078, BT24_102, BT25_104, BT3_014 |
| P:ChangeCardColorClass | ❌ | 14 | BT12_015, BT17_014, BT17_026, BT3_014, BT3_040, BT4_017, BT6_013, BT6_061 |
| P:ChangeCardLevelClass | ❌ | 1 | BT17_068 |
| P:ChangeCardLevelForAssemblyClass | ❌ | 1 | EX9_062 |
| P:ChangeCardNamesClass | ❌ | 63 | AD1_020, AD1_021, AD1_023, BT10_061, BT10_111, BT11_009, BT11_018, BT11_030 |
| P:ChangeCardNamesForDigiXrosClass | ❌ | 8 | BT11_015, BT19_012, BT19_035, BT19_038, BT19_051, BT19_061, BT21_021, BT21_027 |
| P:ChangeDPClass | ❌ | 2 | BT5_056, EX11_070 |
| P:ChangeDPDeleteEffectMaxDPClass | ❌ | 11 | BT12_001, BT17_008, BT17_010, BT19_007, BT19_009, BT19_011, BT9_009, BT9_011 |
| P:ChangeEndTurnMinMemoryClass | ❌ | 2 | BT14_081, BT17_069 |
| P:ChangePermanentLevelClass | ❌ | 6 | BT12_015, BT17_014, BT17_026, BT4_011, BT7_085, BT7_087 |
| P:ChangeTraitsClass | ❌ | 1 | EX7_010 |
| P:DeckBottomBounceClass | 🟡stop-only | 23 | AD1_025, BT10_086, BT11_072, BT12_031, BT12_112, BT15_029, BT16_095, BT17_078 |
| P:DeckBouncePeremanentAndProcessAccordingToResult | ❌ | 15 | BT17_077, BT22_088, BT22_089, BT22_094, BT23_080, BT23_087, BT24_030, BT24_082 |
| P:DeletePeremanentAndProcessAccordingToResult | 🟡stop-only | 279 | BT10_097, BT10_100, BT11_012, BT11_018, BT11_040, BT11_041, BT11_043, BT11_076 |
| P:DigiXrosEffectFromNames | ❌ | 1 | AD1_006 |
| P:DigimonEffectImmunity | ❌ | 8 | AD1_009, AD1_018, BT16_063, BT25_019, BT25_042, BT25_060, EX11_074, ST23_09 |
| P:DigivolveIntoExcecutingAreaCard | ❌ | 1 | ST17_13 |
| P:DigivolveIntoHandOrTrashCard | ❌ | 311 | AD1_001, AD1_010, AD1_011, AD1_021, AD1_022, BT10_041, BT10_067, BT10_080 |
| P:DontBattleSecurityDigimonClass | ❌ | 5 | BT24_039, BT5_112, EX2_054, EX4_013, EX5_053 |
| P:DontHaveDPClass | ❌ | 6 | BT12_015, BT17_014, BT17_026, BT4_011, BT7_085, BT7_087 |
| P:DrawAndDiscardCards | ❌ | 3 | P_198, P_205, P_212 |
| P:Gain1MemoryTamerOpponentDigimonEffect | ❌ | 17 | AD1_019, AD1_022, BT22_087, BT22_090, BT22_102, BT23_078, BT23_079, BT23_080 |
| P:Gain1MemoryTamerOwnerDigimonConditionalEffect | ❌ | 3 | BT23_081, BT23_083, BT23_084 |
| P:Gain2MemoryOptionDelayEffect | ❌ | 13 | BT22_099, BT24_100, LM_045, LM_046, LM_047, LM_048, LM_049, LM_050 |
| P:GetJogressConditionClass | 🟡stop-only | 3 | AD1_011, AD1_025, BT25_103 |
| P:IgnoreColorConditionClass | 🟡stop-only | 144 | BT10_039, BT10_041, BT10_101, BT10_104, BT10_105, BT10_109, BT10_110, BT12_103 |
| P:ImmuneFromDeDigivolveClass | ❌ | 11 | BT11_069, BT16_055, BT21_074, BT22_060, BT23_033, BT24_055, EX10_029, EX10_031 |
| P:ImmuneStackTrashingClass | ❌ | 2 | BT21_060, EX11_070 |
| P:MindLinkClass | ❌ | 11 | BT14_086, BT14_087, BT15_086, BT15_087, BT16_086, BT16_087, BT17_086, BT17_091 |
| P:OnDeletionClass | ❌ | 1 | BT25_039 |
| P:OptionEffectImmunity | ❌ | 1 | BT25_019 |
| P:OptionMainEffect | ❌ | 5 | BT13_106, BT23_097, BT24_096, EX7_072, EX8_072 |
| P:OptionSecurityEffect | ❌ | 2 | BT15_092, BT18_098 |
| P:PlaceDelayOptionCards | ❌ | 148 | BT10_097, BT10_100, BT13_110, BT15_096, BT15_098, BT16_094, BT16_096, BT16_099 |
| P:PlacePermanentInSecurityAndProcessAccordingToResult | ❌ | 6 | BT18_034, BT24_040, BT25_044, LM_020, P_187, ST22_06 |
| P:PlaceSelfDelayOptionSecurityEffect | ❌ | 54 | BT10_097, BT10_100, BT13_110, BT15_098, BT18_100, BT19_097, BT19_099, BT21_097 |
| P:PlayAmonToken | ❌ | 1 | BT14_018 |
| P:PlayAthoRenePorToken | ❌ | 2 | BT20_017, BT23_013 |
| P:PlayDiaboromonToken | ❌ | 16 | BT17_053, BT17_059, BT17_100, BT22_059, BT22_064, BT24_052, BT2_082, BT5_067 |
| P:PlayFamiliarToken | ❌ | 5 | BT22_040, EX11_019, EX11_024, EX7_030, ST19_12 |
| P:PlayFujitsumonToken | ❌ | 1 | EX5_058 |
| P:PlayGyuukimonToken | ❌ | 1 | LM_018 |
| P:PlayHinukamuyToken | ❌ | 1 | BT23_057 |
| P:PlayKoHagurumonToken | ❌ | 1 | BT16_052 |
| P:PlayMindLinkTamerFromDigivolutionCards | ❌ | 1 | EX11_070 |
| P:PlayOptionCards | ❌ | 34 | BT10_039, BT10_041, BT16_014, BT17_035, BT17_038, BT19_037, BT19_040, BT21_062 |
| P:PlayPetrificationToken | ❌ | 3 | BT21_029, BT24_017, EX11_012 |
| P:PlayPipeFox | ❌ | 3 | BT19_040, P_223, ST22_05 |
| P:PlayRapidmonToken | ❌ | 1 | BT19_091 |
| P:PlaySelfDeleteFamiliarToken | ❌ | 1 | P_165 |
| P:PlaySelfDigimonAfterBattleSecurityEffect | ❌ | 35 | BT14_034, BT17_047, BT18_035, BT21_015, BT21_041, BT21_043, BT21_067, BT21_069 |
| P:PlayTaomonToken | ❌ | 1 | BT19_091 |
| P:PlayUkaNoMitama | ❌ | 1 | EX8_037 |
| P:PlayUmonToken | ❌ | 1 | BT14_018 |
| P:PlayVoleeZerdrucken | ❌ | 1 | EX7_058 |
| P:PlayWarGrowlmonToken | ❌ | 1 | BT19_091 |
| P:ReplaceBottomSecurityWithFaceUpOptionEffect | ❌ | 7 | BT24_090, BT24_094, BT25_094, BT25_095, BT25_097, BT25_099, BT25_102 |
| P:ReplaceBottomSecurityWithFaceUpOptionMainEffect | ❌ | 7 | BT21_095, BT22_100, EX8_068, EX8_069, EX8_071, EX9_072, ST21_15 |
| P:ReplaceTopSecurityWithFaceUpOptionMainEffect | ❌ | 1 | ST20_15 |
| P:ReturnToLibraryBottomDigivolutionCardsClass | ❌ | 14 | BT11_062, BT11_064, BT11_070, BT11_111, BT13_075, BT18_092, BT21_060, BT21_062 |
| P:RevealLibraryClass | ❌ | 2 | EX2_072, P_070 |
| P:RushClass | ❌ | 1 | BT25_104 |
| P:SelectCardConditionClass | ❌ | 25 | BT10_096, BT10_097, BT16_082, BT16_094, BT19_008, EX6_025, EX8_050, EX8_053 |
| P:SelectDigiXrosClass | ❌ | 5 | BT10_093, BT12_112, BT15_102, BT21_030, EX10_061 |
| P:StartOfYourMainPhaseClass | ❌ | 1 | BT25_092 |
| P:StartOfYourTurnClass | ❌ | 1 | BT25_104 |
| P:SuspendPeremanentAndProcessAccordingToResult | ❌ | 7 | AD1_019, BT25_086, BT25_087, BT25_088, BT25_089, BT25_090, BT25_092 |
| P:TrashDigivolutionCardsAndProcessAccordingToResult | ❌ | 8 | BT25_029, BT25_096, EX10_032, EX10_034, EX10_045, EX10_055, EX10_056, EX10_058 |
| P:TrashHandAndProcessAccordingToResult | ❌ | 1 | BT25_101 |
| P:TrashLinkCardsAndProcessAccordingToResult | ❌ | 11 | BT25_073, BT25_101, EX10_014, EX10_016, EX10_017, EX10_019, EX10_024, EX10_029 |
| P:TrashSecurityAndProcessAccordingToResult | ❌ | 5 | BT25_040, BT25_042, BT25_043, EX10_041, ST23_05 |
| P:TreatAsDigimonClass | ❌ | 7 | BT12_015, BT17_014, BT17_026, BT25_104, BT4_011, BT7_085, BT7_087 |
| P:UseRequirements | ❌ | 16 | BT25_043, BT25_057, BT25_085, BT25_093, BT25_098, BT25_100, BT25_101, BT25_104 |
| P:WhenDigivolvingClass | ❌ | 1 | BT25_104 |
| P:WhenMovingClass | ❌ | 1 | BT25_104 |
| S:SelectAttackEffect | ❌ | 121 | AD1_004, AD1_007, AD1_008, AD1_020, AD1_021, BT11_104, BT11_107, BT12_055 |
| S:SelectCountEffect | ❌ | 11 | BT10_081, BT18_072, BT2_066, BT3_100, BT5_025, BT5_032, BT5_088, EX10_035 |
| S:SelectDNACondition | ❌ | 1 | BT17_095 |
| S:SelectDigiXrosClass | ❌ | 5 | BT10_093, BT12_112, BT15_102, BT21_030, EX10_061 |
| T:AfterEffectsActivate | ❌ | 2 | BT12_044, BT16_015 |
| T:AfterPayCost | 🟡stop-only | 7 | BT1_109, BT3_103, BT5_109, EX1_033, EX1_071, EX5_029, ST12_15 |
| T:BeforePayCost | 🟡stop-only | 141 | BT10_052, BT10_087, BT10_088, BT10_093, BT11_091, BT11_095, BT12_022, BT12_050 |
| T:OnDigivolutionCardReturnToDeckBottom | ❌ | 3 | BT11_065, BT18_065, BT21_058 |
| T:OnDiscardSecurity | 🟡stop-only | 14 | BT13_098, BT13_106, BT15_037, BT15_084, BT15_092, BT17_034, BT17_036, BT18_098 |
| T:OnFaceUpSecurityIncreased | ❌ | 1 | EX11_004 |
| T:OnLeaveFieldAnyone | 🟡stop-only | 1 | AD1_025 |
| T:OnLinkCardDiscarded | ❌ | 7 | EX10_001, EX10_030, EX10_043, EX10_062, EX10_070, EX10_073, P_234 |
| T:OnLoseSecurity | 🟡stop-only | 73 | AD1_017, BT11_016, BT11_045, BT13_003, BT13_036, BT13_044, BT14_001, BT14_082 |
| T:OnPermamemtReturnedToHand | ❌ | 2 | BT14_030, BT17_099 |
| T:OnRemovedField | ❌ | 2 | BT22_007, EX10_010 |
| T:OnReturnCardsToHandFromTrash | ❌ | 2 | BT15_082, BT16_011 |
| T:OnReturnCardsToLibraryFromTrash | ❌ | 1 | P_048 |
| T:OnSecurityCheck | ❌ | 9 | BT12_088, BT16_033, BT20_005, BT20_052, BT20_055, BT22_080, EX11_041, EX11_043 |
| T:OnUseDigiburst | ❌ | 1 | BT5_056 |
| T:OnUseOption | ❌ | 30 | BT10_032, BT12_044, BT17_031, BT17_032, BT17_038, BT19_030, BT19_034, BT19_040 |
| T:WhenDigisorption | 🟡stop-only | 10 | BT10_052, BT2_045, BT2_047, BT2_050, BT3_054, BT3_056, BT5_058, BT8_054 |
| T:WhenReturntoHandAnyone | ❌ | 9 | BT11_062, BT11_064, BT20_074, BT5_086, BT9_012, EX3_013, EX4_021, EX6_031 |
| T:WhenReturntoLibraryAnyone | ❌ | 9 | BT11_062, BT11_064, BT20_074, BT5_086, BT9_012, EX3_013, EX4_021, EX6_031 |
| T:WhenTopCardTrashed | ❌ | 3 | BT21_094, BT8_110, EX10_010 |
| T:WhenUntapAnyone | ❌ | 1 | BT7_055 |
| T:WhenWouldDigivolutionCardDiscarded | ❌ | 1 | BT10_084 |
| T:WhenWouldLink | ❌ | 2 | BT25_004, BT25_045 |
| X:AceOverflow | ❌ | 3 | BT17_098, BT18_042, BT24_093 |
| X:AppFusion | ❌ | 10 | BT21_059, BT21_084, BT22_087, BT23_021, BT23_022, BT23_024, BT23_079, BT24_087 |
| X:Assembly | 🟡stop-only | 15 | AD1_009, AD1_012, AD1_025, BT22_078, BT24_062, BT24_081, EX11_036, EX11_045 |
| X:BurstDigivolution | ❌ | 5 | BT13_020, BT13_033, BT13_060, BT13_092, BT25_104 |
| X:DelayOption | ❌ | 148 | BT10_097, BT10_100, BT13_110, BT15_096, BT15_098, BT16_094, BT16_096, BT16_099 |
| X:DigiXros | ❌ | 101 | AD1_006, AD1_013, BT10_009, BT10_012, BT10_013, BT10_015, BT10_024, BT10_026 |
| X:Digisorption | 🟡stop-only | 19 | BT10_052, BT11_091, BT2_045, BT2_047, BT2_050, BT2_088, BT3_054, BT3_056 |
| X:ExecutingAreaDigivolve | ❌ | 15 | BT6_056, EX1_027, EX1_065, EX4_013, EX8_035, P_066, P_067, P_068 |
| X:IgnoreRequirement | 🟡stop-only | 164 | BT10_039, BT10_041, BT10_101, BT10_104, BT10_105, BT10_109, BT10_110, BT12_089 |

## 6. STOP 예상 종합 — 정본 패스 중 인프라 갭 수확 목록

greedy 87장 중 **STOP-리스크 보유 21장**. 리스크 컬럼은 카드의 **전체** 리스크 프로파일(신규커버 요소 외 참조 포함). 클러스터별:

| 클러스터 | RD-* / 근거 | 걸리는 후보(대표) | 성격 |
|---|---|---|---|
| Assembly 인터랙티브 pre-play | RD-P6C1-5 (AD1_025 실측 throw) | BT24_062, (AD1_025 재활) | 정본 포팅=throw 재현 → 인프라 골 필요 |
| DigiXros 인터랙티브 pre-play | RD-P6C1-5 / RD-R5-04 | BT21_030, BT10_111, EX10_045(병존) | 동일 클러스터 |
| Burst/AppFusion select 컴포넌트 | RD-P6C1-6 (+RD-P6C1-1 field-frame) | BT25_104, BT25_089 | GManager select* 미이관 |
| BlastDNA 손패 픽 | RD-P6C1-7 (SelectHandEffect BlastDNA 경로) | (BlastDNA 후보 롱테일) | — |
| Execute 발화 | RD-R2-01 (ExecuteProcess STOP) | BT20_079 | 게이트 잔존 정당(창 STOP) |
| Ascension | RD-3A-01 / RD-P6C3-A3 | BT25_040 | sink 미충전 writer 필요 |
| AddSkillClass(중첩 부여) | nested-grant STOP boundary | EX7_072, (BT1_104 기존 live STOP) | AddSkillClass만 STOP 원칙 |
| Digisorption | G11 연계(CanSuspendByDigisorptionClass MISSING) | BT3_056 | — |
| 고급 select 술어 | SelectCardConditionClass 미평가(저장만) | LM_047 | fidelity-over-coverage: 뭉개면 FAIL |
| 필드 배치 제약 | CanNotPutFieldClass MISSING | EX7_014 | — |
| 공유 once-per-turn | ActivateClassesForSharedEffects(P2-9) | BT25_104, BT25_040 | 공유 hashValue 모델 |
| digi-source 전역 보호 | CanNotTrashFromDigivolutionCardsClass(딥 잔여) | BT9_109 재활만 가능(universe=1) | 전역 source-protection 스캔 인프라 |

- **잔여 2요소는 신규 카드로 못 덮음**: `P:CanNotTrashFromDigivolutionCardsClass`·`T:OnLeaveFieldAnyone` — 각각 유일 호출 카드(BT9_109, AD1_025)가 이미 STOP-포팅됨. 커버=해당 STOP 상환(재활)로만 가능.
- Retaliation/Pierce **발화** 경로는 RD-CBTL-01(IBattle 이관) STOP이나, 미러 수동블록이 행동을 담당하므로 카드 포팅 자체는 막히지 않음(클린 커버 존재).

## 7. 판정·다음 단계

### IR DB 사용 가능 판정

**사용 가능**. `docs/porting/card_ir.sqlite`(card 3,918행 + card_primitive) — timings/commons/keywords(*Class)/descriptions 신호 건재, 본 감사의 룰텍스트 키워드 검출에 사용. 단 `port_status` 컬럼은 스테일(ported 57 vs 실제 209; 2026-07-05 빌드) — 필요 시 `python tools/porting/build_card_db.py` 재실행으로 갱신 가능(본 감사는 TO-BE 파일 스캔으로 대체).

### 정의서 판정 스테일 실증 (커버리지-역방향 증거)

정의서(07-08/09)의 MISSING/FAIL 판정인데 클린 카드가 이미 커버 중인 사례 = 이후 상환이 정의서에 미반영된 것: `CanNotPlayClass`(MISSING 표기 ↔ BT8_057·EX1_072 클린 — E-3 ICanNotPlay 연속스캔 상환), `DisableEffectClass`(FAIL 표기 ↔ BT1_025 클린) 등. **①축 판정 컬럼은 "당시 판정"으로만 읽고, 실상태는 커버리지 컬럼과 후속 골 기록(A군 재하우징 18/18·FAILa~d 상환)을 우선할 것.**

### 2단계(선정) 안내

1. **사용자 witness 선정**: §4 상위 후보에서 카드 선택(카드당 덮는 축 목록 병기됨). 권장 프레임:
   - **클린 레인(STOP-리스크 무)**: LM_054·BT19_091·EX11_070·BT17_026·EX10_029·P_223·BT5_086·EX10_010·BT18_042·ST17_13·AD1_011·BT20_017·BT14_030·BT14_097·BT14_018·BT22_040·BT21_029·EX5_055·EX5_053·EX11_074·EX8_028(Iceclad) 등 — 강모델 정본 포팅 즉시 가능, 참조 코퍼스 순증.
   - **수확 레인(STOP-리스크 유)**: BT25_104·BT21_030·BT24_062·BT25_040·BT25_089·EX7_072·BT3_056·LM_047·EX7_014·BT20_079 — 포팅 시도=정직 STOP 수확(인프라 갭 원장 등재), Opus 프리미티브 선행 개발 트리거([[primitive-predevelopment-role]]).
2. **포팅 규약**: goal-witness 운용 모드(엔진 골+witness 2~3장), 카드별 AS-IS 1:1(단순화 불허), STOP은 runtime throw 아닌 정직 STOP 마커.
3. **커버리지 재측정**: 각 배치 후 `.tmp/coverage_audit/analyze.py` 재실행 — 커버율 변동은 구조 지표(green 수 아님)로 보고.

## 8. 실측 정정 (트랜치1~3 수확, 2026-07-18) — §6 STOP-예상 지도의 교체

정본 30장(클린 20+수확 10) 실포팅 결과, §6의 12 클러스터 예상 중 **적중 ~2.5개**. "미커버(❌)"의 다수는 "미구축"이 아니라 "클린 카드 부재로 미검증"이었음이 실증됨. 격파된 예상: Ascension(RD-P6C2-1 기상환)·공유 once-per-turn(순수 디스패처)·AddSkillClass nested-grant(플레이어-레벨 LIVE 스캔 실존)·고급 SelectCardCondition 술어(RevealLibrary에서 실평가)·Save·ArmorPurge·DeckBottomBounce·TrashDigivolutionCards-연쇄 등.

**실측 후 잔여 인프라 골 5개 (요구사항 명세=트랜치3A/3B 보고, 원장 RD-EXT3-*):**
| # | 골 | 실체 | 게이팅 물량 |
|---|---|---|---|
| G-Link | Link 서브시스템 | ILinkCard 타입·LinkCard() 흐름(WhenWouldLink 창·링크코스트 지불)·GetChangedLinkCost (RD-P6C2-7+C2-02) | K:Link **73장** |
| G-AppF | AppFusion | CanAppFusionFromTargetPermanent 요구/코스트+PermanentFrame.FrameID 절반 (RD-P6C1-2+P6C3-D1) | 10장 |
| G-Field | 필드-배치 제약 배선 | PlayCardAction.Validate에 CanEnterField/ICanNotPutField 스캔 호출 추가만(생산자·스캔 실존) (RD-EXT3-03/3A) | 7장 |
| G-Burst | Burst execution | SelectBurstDigivolutionEffect+burst-play 몸통(+CannotReturnToHand aggregate·활성화-시각 클럭 미소 표면) (RD-P6C1-6/RD-EXT3-04) | 5장 |
| G-Xros | DigiXros/Assembly 소비 파이프 | 등록·비용-머신 완료 — SelectDigiXros 소비(CanSubstituteForDigiXrosCondition+SelectHand 스텁)·SelectAssembly 형상 반전 (RD-EXT3-01/02=RD-P6C1-5·RD-R5-04) | Xros/Assembly 계열 |
| (별도) | Digisorption 진입 | Player.CanTapWhenAbsorbEvolution+_CheckAvailability 2종만 (RD-EXT3-03/3B) | 소형 |
| (별도) | Arts resolution | CanPlayCardTargetFrame/Hashtable-ctor PlayCardClass (RD-P6C2-10, 등재 클린·해소만 지연-STOP) | 6장 |

커버리지 계기판(정본 패스 후): 포팅 209→**239장**, 미커버 164요소 중 정본 30장이 ~90요소 커버(정확 수치는 재감사 시), 잔여=롱테일 57장+위 골 게이팅분.
