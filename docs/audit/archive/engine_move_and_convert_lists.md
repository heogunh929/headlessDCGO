# 기본 엔진: MOVE 리스트 + AS-IS→TO-BE 변환 리스트 (A·C·D)

- 작성일: 2026-06-27
- 두 종류로 구분:
  - **MOVE** = 이미 `Headless/`에 구현돼 있고 **AS-IS 미러로 위치만 옮길 것** (키워드 이동과 동일 패턴: 이동→namespace→소비처 using→빌드→풀스위트).
  - **CONVERT (AS-IS→TO-BE)** = 아직 없어서 **원본에서 변환·신규 구축할 것** (원본 경로 → 헤드리스 목표 경로).
- 규칙: 카드-대면 레이어 → `[HL-Assets]=src/HeadlessDCGO.Engine/Assets/Scripts/Script/`(원본 동일 경로). 엔진 플럼빙(스케줄러/게이트/뮤테이션싱크/레지스트리/RL) → `[HL]=Headless/` 유지. ([asis-structure-mirror-rule], [engine_completion_file_map.md](engine_completion_file_map.md))
- ⚠️ **MOVE 주의**: 헬퍼 파일들은 엔진 타입을 **번들**(NumericModifier·CannotRestriction·ReplacementEffect 등)하므로, 옮기면 그 타입을 쓰는 게이트/평가기/테스트까지 namespace ripple. 키워드보다 ripple 큼 → 파일 단위로 신중히.

---

## PART 1 — MOVE 리스트 (Headless/ → AS-IS 미러)

### 이미 이동 완료 ✅
| 항목 | 현 위치 | 비고 |
|------|---------|------|
| 키워드 8종 + 공유 | `[HL-Assets]CardEffectCommons/KeyWordEffects/{Blocker,Jamming,Reboot,Pierce,Rush,Blitz,Retaliation,ArmorPurge,KeywordBaseBatch1,KeywordBaseBatch2}.cs` | per-keyword partial 분리 완료 |

### A 프레임워크 — MOVE 대상 (카드-대면 헬퍼, Headless/Effects → CardEffectCommons)
| ID | Headless 현 위치 | AS-IS 미러 목표 | 번들 타입(ripple) |
|----|------------------|-----------------|-------------------|
| MV-1 | `[HL]Effects/ModifierHelpers.cs` | `[HL-Assets]CardEffectCommons/ModifierHelpers.cs` (facade `ModifierHelperFactory` 이미 존재 → 병합) | `NumericModifier/Metric/Mode/Result/Request` — **광범위** |
| MV-2 | `[HL]Effects/RestrictionHelpers.cs` | `[HL-Assets]CardEffectCommons/RestrictionHelpers.cs` (facade 존재) | `CannotRestriction/Kind/Result` |
| MV-3 | `[HL]Effects/ReplacementHelpers.cs` | `[HL-Assets]CardEffectCommons/ReplacementHelpers.cs` (facade 존재) | `ReplacementEffect/EventKind/ActionKind` |
| MV-4 | `[HL]Effects/ContinuousEffectEvaluator.cs` | `[HL-Assets]CardEffectCommons/ContinuousEffectEvaluator.cs` (구현본 존재 — 중복 정리 필요) | `ContinuousEvaluationResult/Request` |
| MV-5 | `[HL]Effects/CanUseEffectHelpers.cs`·`TriggerConditionHelpers.cs` | `[HL-Assets]CardEffectCommons/CanUseEffects/` (스켈레톤 41개 채움) | 트리거 조건 |
| MV-6 | `[HL]Effects/MinMaxRequirementHelpers.cs` | `[HL-Assets]CardEffectCommons/MinMax_DP_Cost_Level/` (스켈레톤 7개) | Min/Max 술어 |
| MV-7 | `[HL]Effects/TargetFilterHelpers.cs`·`ZoneQueryHelpers.cs`·`CardRequirementHelpers.cs` | `[HL-Assets]CardEffectCommons/` | 타깃/존/요건 술어 |
| MV-8 | `[HL]Effects/PlayCostHelpers.cs`·`DigivolutionCostHelpers.cs` | `[HL-Assets]CardEffectCommons/` (cost) | cost 계산 |
| MV-9 | `[HL]Effects/OnceFlagHelpers.cs`·`TimingPriorityHelpers.cs`·`InheritedGrantedSecurityHelpers.cs`·`EffectChoiceHelpers.cs` | 동명 미러 **이미 존재** → 구현본 통합/중복 제거 | — |
| MV-10 | `[HL]Effects/CardEffectFactoryBinding.cs`·`PermanentEffectFactoryBinding.cs` | `[HL-Assets]CardEffectFactory/` (일부 미러 존재) | 바인딩 |

### C 키워드 — MOVE: 없음 (구현된 8종 이미 이동 완료; 나머지는 CONVERT)

### D 서브시스템 — MOVE 대상 (부분 구현분만)
| ID | Headless 현 위치 | AS-IS 미러 목표 | 비고 |
|----|------------------|-----------------|------|
| MV-11 | Link 플래그 흔적(`KeywordBaseBatch*`/binding) | `[HL-Assets]CardEffects/AddLinkConditionClass.cs` 등 | 대부분 CONVERT (아래) |

> **STAY (이동 안 함, 엔진 플럼빙)**: EffectScheduler·EffectResolutionQueue·PendingEffect·EffectRegistry·EffectContext/Request/Result·CardEffectSchedulerResolver·MatchStateMutationSink·MandatoryEffectOrdering·AutoProcessingTriggerCollector·TriggerEventEmitter·TriggerTimingMap·TriggerTimings·OptionalPromptQueue·*TriggerHook·SkillInfo·HeadlessCardEffectContract + Runtime 전체(게이트·리졸버·컨트롤러·RL·액션·플로우).

---

## PART 2 — AS-IS→TO-BE 변환 리스트 (미구현 신규 구축)

### A 프레임워크 — CONVERT (원본엔 있고 헤드리스 미구현/부분)
| ID | TO-BE 헤드리스 위치 | AS-IS 원본 |
|----|---------------------|-----------|
| CV-A1 EffectDuration 8종+만료훅 | `[HL]Effects/EffectDuration.cs` + 만료지점(EndTurnCleanup·Battle·Attack·Untap) | `[ASIS]ICardEffect.cs`(EffectDuration) + `TurnStateMachine.cs`·`AttackProcess.cs` |
| CV-A2 선택→연산 모드/Root | `[HL-Assets]SelectPermanentEffect.cs`·`SelectCardEffect.cs`·`SelectHandEffect.cs`·`SelectCountEffect.cs`(스켈레톤 채움) + `[HL]Runtime` choice 배관 | `[ASIS]Select*Effect.cs` |
| CV-A3 player-scope 연속효과 | `[HL]Runtime/ContinuousDpGate.cs`·`ContinuousRestrictionGate.cs`(확장) | `[ASIS]Permanent.cs`(GetDP 스캔)·`CardEffectCommons/GiveEffect/GiveEffectToPlayer/*` |
| CV-A4 타이밍 emit 누락 ~45 | `[HL]Effects/TriggerTimings.cs` + 중앙 emit(`InMemoryZoneMover`·`MatchStateMutationSink`·flows) | `[ASIS]TurnStateMachine.cs`·`AttackProcess.cs`·`CardController.cs`·`CardObjectController.cs` |
| CV-A5 inherited 활성모델 | `[HL]State/DigivolutionStackReader.cs` + 활성판정 | `[ASIS]ICardEffect.cs`(IsInheritedEffect) |
| CV-A6 once-per-turn 자동게이팅 | `[HL]Effects/OnceFlagHelpers.cs`(통합) | `[ASIS]CEntity_EffectController.cs` |

### B 공통연산 — CONVERT (대부분 신규 뮤테이션/존 op)
| ID | TO-BE | AS-IS |
|----|-------|-------|
| CV-B1 Delete | `[HL]Effects/MatchStateMutationSink.cs`(+Delete) | `[ASIS]CardEffectCommons.cs`(DeletePermanent…)·`Permanent.cs` |
| CV-B2~B11 | `[HL]Effects/MatchStateMutationSink.cs`·`[HL]Services/InMemoryZoneMover.cs` | `[ASIS]CardEffectCommons.cs`·`CardObjectController.cs`·`CardController.cs` |
| (B-2 ±DP지속) | ModifierHelpers + CV-A1 | `[ASIS]CardEffectCommons.cs`(ChangeDigimonDP) |
| (Recovery·Token·Reveal·effect-Play) | InMemoryZoneMover·신규 | `[ASIS]CardObjectController.cs`·`CardController.cs` |

### C 키워드 — CONVERT (미구현 ~20)
TO-BE: `[HL-Assets]CardEffectCommons/KeyWordEffects/<Name>.cs` ← AS-IS: `[ASIS]CardEffectCommons/KeyWordEffects/<Name>.cs` (+ 소비 훅 `[HL]Runtime/{BattleResolver,AttackPipeline,BlockTiming}`)

| 그룹 | 키워드(파일) |
|------|------|
| 방어 | Decoy · Barrier · Fortitude · Evade |
| 반격 | Execute · Collision |
| 진화/공격 | Raid(=D-3) |
| 자원/조건 | Fragment · Iceclad · Decode · Partition · Progress · Overclock · Ascension · Alliance · Scapegoat · Vortex |
| 기타 | Save · MaterialSave · Training · MindLink |

### D 서브시스템 — CONVERT (전부 신규)
| ID | TO-BE 헤드리스 | AS-IS 원본 |
|----|----------------|-----------|
| CV-D1 Link | 신규 `[HL]Runtime/LinkController.cs` + linked-card 존 + 미러 `[HL-Assets]CardEffects/{AddLinkConditionClass,ChangeLinkCostClass,ChangeLinkMaxClass}.cs`·`CardEffectFactory/{AddLinkRequirement,ChangeLinkMax,KeyWordEffects/Link}.cs` | `[ASIS]CardEffects/AddLinkConditionClass.cs` 외, `CardEffectFactory/AddLinkRequirement.cs`·`KeyWordEffects/Link.cs`·`CardEffectCommons/TrashLinkedCards.cs`·`CanUseEffects/{WhenLinked,WhenWouldLink,OnTrashLink*}.cs` |
| CV-D2 Appfuse | `[HL]Runtime/AppfuseAction.cs` + 미러 `[HL-Assets]SelectAppFusionEffect.cs`·`CardEffects/AddAppFusionConditionClass.cs`·`CardEffectFactory/AddAppfusionMethod.cs` | `[ASIS]SelectAppFusionEffect.cs`·`CardEffects/AddAppFusionConditionClass.cs`·`CardEffectFactory/AddAppfusionMethod.cs` |
| CV-D3 Raid | `[HL]Runtime/AttackPipeline.cs`(시큐직접공격) + 미러 키워드 | `[ASIS]CardEffectCommons/KeyWordEffects/Raid.cs`·`CardEffectFactory/KeyWordEffects/Raid.cs` |
| CV-D4 De-Digivolve | `[HL]Runtime/DigivolutionSourceStackPort.cs`(소재 N detach) + 미러 `[HL-Assets]CardEffects/ImmuneFromDeDigivolveClass.cs` | `[ASIS]SelectPermanentEffect.cs`(Degenerate)·`CardEffects/ImmuneFromDeDigivolveClass.cs` |
| CV-D5 DNA/Jogress·DigiXros | `[HL]Runtime/DigivolveAction.cs`(확장) + 미러 `[HL-Assets]SelectJogressEffect.cs` | `[ASIS]SelectJogressEffect.cs`·`CardEffectFactory`(Jogress/DigiXros)·`DigiXrosEffectObject.cs` |
| CV-D6 Blast/Arts Digivolve | `[HL]Runtime/DigivolveAction.cs` + 미러 `[HL-Assets]SelectBurstDigivolutionEffect.cs` | `[ASIS]SelectBurstDigivolutionEffect.cs`·`CardEffectFactory` |
| CV-D7 효과 무효화 | 신규 `[HL]Effects/EffectInvalidation.cs` | `[ASIS]CheckEffectDisabledClass.cs`·`ICardEffect.cs`(IsDisabled) |
| CV-D8 코스트감소 파이프라인 | `[HL]Effects/PlayCostHelpers.cs`·`DigivolutionCostHelpers.cs`(확장) + Before/AfterPayCost emit | `[ASIS]CardController.cs`(지불)·`CardEffects/Change*CostClass.cs` |
| CV-D9 Recovery/Token/MindLink/Delay | `[HL]Services/InMemoryZoneMover.cs`·신규 + 미러 `[HL-Assets]CardEffectCommons/KeyWordEffects/MindLink.cs` | `[ASIS]CardObjectController.cs`·`CardController.cs`·`KeyWordEffects/MindLink.cs` |

---

## 권장 순서
1. **MOVE 먼저(저위험 우선)**: MV-9(이미 미러 동명 존재 — 중복 정리) → MV-5/MV-6/MV-7(조건/술어 헬퍼) → MV-2/MV-3(restriction/replacement) → MV-1(ModifierHelpers, 번들 ripple 큼 마지막) → MV-4(중복정리) → MV-10.
2. **CONVERT A 프레임워크**: CV-A1(Duration) → CV-A2(선택) → CV-B1(Delete) → CV-A4(타이밍) → CV-A3/A5/A6.
3. **CONVERT C 키워드** 그룹별, **CONVERT D 서브시스템** 세트별.
> MOVE는 위치 정리(저위험·기계적), CONVERT는 실제 기능 구축. 둘은 병행 가능하나, A 프레임워크(CV-A1·CV-A2·CV-B1)가 깔려야 C·D·카드본문이 쉬워짐.
