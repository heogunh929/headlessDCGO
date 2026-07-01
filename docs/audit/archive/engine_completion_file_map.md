# 기본 엔진 백로그 — 생성 위치 ↔ AS-IS 원본 위치 매핑

- 작성일: 2026-06-27
- 목적: [engine_completion_backlog.md](engine_completion_backlog.md)의 각 항목에 대해 **헤드리스 생성/수정 위치**와 **AS-IS 원본 위치**를 나란히 둔다. 추후 원본↔포팅 대조 분석을 쉽게.
- 배치 컨벤션(기존 코드베이스 관행):
  - **AS-IS 카드-대면 레이어**(카드 파일이 호출: CardEffectCommons / CardEffectFactory / Select*Effect / ActivateClass / KeyWordEffects / CardEffect 본문) → **`src/HeadlessDCGO.Engine/Assets/Scripts/Script/<원본과 동일 상대경로>`** 에 1:1 미러 포팅. (이미 `Assets/Scripts/Script/CardEffectCommons.cs`, `.../CanUseEffects/*`, `CardEffect/*` 등이 이 방식.)
  - **엔진 내부 배관**(스케줄러/게이트/뮤테이션싱크/연속평가/존무버) → **`src/HeadlessDCGO.Engine/Headless/{Effects,Runtime,Services,State,Choices,Bridge}/`**. AS-IS 1:1 대응이 없으므로 "개념적 원본"을 표기.
- 경로 약어: `[HL-Assets]` = `src/HeadlessDCGO.Engine/Assets/Scripts/Script/` · `[HL]` = `src/HeadlessDCGO.Engine/Headless/` · `[ASIS]` = `DCGO/Assets/Scripts/Script/`

---

## A. 기반 프레임워크

| ID | 헤드리스 생성/수정 위치 | AS-IS 원본 위치 |
|----|------------------------|-----------------|
| **F-1 EffectDuration** enum | `[HL]Effects/EffectDuration.cs` (신규; TriggerTimings.cs와 동거) | `[ASIS]ICardEffect.cs` (EffectDuration enum) |
| F-1 만료 정리(턴종료) | `[HL]Runtime/HeadlessEndTurnCleanupFlow.cs` (확장) | `[ASIS]TurnStateMachine.cs` (EndPhase "Reset status until end of turn") |
| F-1 만료(전투/공격 끝) | `[HL]Runtime/BattleResolver.cs`·`SecurityResolver.cs`·`AttackPipeline.cs` | `[ASIS]AttackProcess.cs` (UntilEndAttack/UntilEndBattle) |
| **F-2 선택→연산** 포팅면 | `[HL-Assets]SelectPermanentEffect.cs`·`SelectCardEffect.cs`·`SelectHandEffect.cs`·`SelectCountEffect.cs` (미러 신규) | `[ASIS]SelectPermanentEffect.cs`·`SelectCardEffect.cs`·`SelectHandEffect.cs`·`SelectCountEffect.cs` |
| F-2 엔진 배관(choice) | `[HL]Runtime/DeferredChoiceProvider.cs`·`HeadlessLegalActionDispatcher.cs` (확장), `[HL]Choices/` | `[ASIS]CEntity_EffectController.cs`·`GManager.cs` (선택 코루틴 구동) |
| **F-3 뮤테이션 확장** | `[HL]Effects/MatchStateMutationSink.cs` (kind 추가) + `[HL]Services/InMemoryZoneMover.cs` | `[ASIS]CardEffectCommons.cs`(GiveEffect/Delete/Bounce 등) + `CardController.cs`·`CardObjectController.cs`·`Permanent.cs` |
| F-3 GiveEffect 포팅면 | `[HL-Assets]CardEffectCommons/GiveEffect/...` (미러 신규) | `[ASIS]CardEffectCommons/GiveEffect/GiveEffectToPermanent/*`·`GiveEffectToPlayer/*` |
| **F-4 once-per-turn** | `[HL]Effects/OnceFlagHelpers.cs` (게이트 통합) + 스케줄러/collector | `[ASIS]CEntity_EffectController.cs`(UseEffectsThisTurn/InitUseCountThisTurn) + `ICardEffect.cs`(MaxCountPerTurn) |
| **F-5 player-scope 연속** | `[HL]Runtime/ContinuousDpGate.cs`·`ContinuousRestrictionGate.cs` (확장) + `[HL]Effects/ContinuousEffectEvaluator.cs` | `[ASIS]Permanent.cs`(DP/GetDP 전 필드 스캔) + `CardEffectCommons/GiveEffect/GiveEffectToPlayer/*` |
| **F-6 타이밍 emit** 상수 | `[HL]Effects/TriggerTimings.cs`·`TriggerEventEmitter.cs`·`TriggerTimingMap.cs` | `[ASIS]ICardEffect.cs` (EffectTiming enum 63종) |
| F-6 emit 지점(중앙) | `[HL]Services/InMemoryZoneMover.cs`·`[HL]Effects/MatchStateMutationSink.cs` + `[HL]Runtime/{HeadlessMainPhaseFlow,AttackPipeline,BattleResolver,...}.cs` | `[ASIS]TurnStateMachine.cs`·`AttackProcess.cs`·`CardController.cs`·`CardObjectController.cs`·`AutoProcessing.cs` |
| **F-7 inherited 활성** | `[HL]State/DigivolutionStackReader.cs` + 효과 활성 판정 | `[ASIS]ICardEffect.cs`(IsInheritedEffect 활성규칙) |
| **F-8 조건/쿼리 헬퍼** | `[HL]Effects/{TargetFilterHelpers,ZoneQueryHelpers,CardRequirementHelpers,MinMaxRequirementHelpers,TriggerConditionHelpers}.cs` (확장) + `[HL-Assets]CardEffectCommons.cs`·`CardEffectCommons/CanUseEffects/*` | `[ASIS]CardEffectCommons.cs` + `CardEffectCommons/CanUseEffects/*` |

---

## B. 공통 게임 연산

공통: 헤드리스 `[HL]Effects/MatchStateMutationSink.cs`(뮤테이션 kind) + `[HL]Services/InMemoryZoneMover.cs`(존 op) + F-2 선택. AS-IS는 `[ASIS]CardEffectCommons.cs`의 `*AndProcessAccordingToResult` 계열 + 아래.

| ID | 헤드리스 위치 | AS-IS 원본 |
|----|--------------|-----------|
| B-1 Delete | `[HL]Effects/MatchStateMutationSink.cs`(+Delete kind)·`[HL]Runtime/BattleDeletionGate.cs`(존중) | `[ASIS]CardEffectCommons.cs`(DeletePermanent…)·`Permanent.cs`(CanBeDestroyed) |
| B-2 ±DP/SAttack/cost(지속) | `[HL]Effects/ModifierHelpers.cs`(+F-1 duration) | `[ASIS]CardEffectCommons.cs`(ChangeDigimonDP…) |
| B-3 바운스/덱복귀 | `[HL]Services/InMemoryZoneMover.cs`·`MatchStateMutationSink.cs` | `[ASIS]CardObjectController.cs`(ReturnToHand/Library) |
| B-4 Suspend/Unsuspend | `[HL]Effects/MatchStateMutationSink.cs`(kind 존재)+F-2 | `[ASIS]CardObjectController.cs`(Tap/UnTap) |
| B-5 Draw/discard/deck-trash | `[HL]Services/InMemoryZoneMover.cs` | `[ASIS]CardController.cs`(DrawClass)·`CardObjectController.cs` |
| B-6 시큐 trash/Recovery | `[HL]Services/InMemoryZoneMover.cs`(+Recovery) | `[ASIS]CardObjectController.cs`(AddSecurityCard/TrashSecurity) |
| B-7 reveal&select | `[HL]Services/InMemoryZoneMover.cs`+F-2 | `[ASIS]CardEffectCommons.cs`(RevealDeckTop…) |
| B-8 effect-Play | `[HL]Runtime/PlayCardAction.cs`(effect 경로)·`OptionActivateAction.cs` | `[ASIS]CardController.cs`(PlayPermanent/PlayOption) |
| B-9 토큰 | `[HL]Services/`·`Runtime/`(신규 TokenFactory) | `[ASIS]CardController.cs`(PlayToken*) |
| B-10 소재/링크 trash·복귀 | `[HL]Runtime/DigivolutionSourceStackPort.cs`(확장) | `[ASIS]CardEffectCommons.cs`(TrashDigivolutionCards)·`TrashLinkedCards.cs` |
| B-11 트래시→손/덱 | `[HL]Services/InMemoryZoneMover.cs` | `[ASIS]CardObjectController.cs`(ReturnCardsToHand/LibraryFromTrash) |

---

## C. 키워드

- **포팅면(미러)**: `[HL-Assets]CardEffectCommons/KeyWordEffects/<Name>.cs` ← `[ASIS]CardEffectCommons/KeyWordEffects/<Name>.cs`
- **팩토리 면**: `[HL-Assets]CardEffectFactory/KeyWordEffects/<Name>.cs` ← `[ASIS]CardEffectFactory/KeyWordEffects/<Name>.cs` (Link/Raid 등)
- **소비 훅(엔진)**: `[HL]Runtime/{BattleResolver,AttackPipeline,BlockTiming}.cs` + `[HL]Effects/KeywordBaseBatch*.cs`

| 키워드 | AS-IS 원본 파일 (`[ASIS]CardEffectCommons/KeyWordEffects/`) |
|--------|-----------|
| C-1 Rush | Rush.cs |
| C-2 Blitz | Blitz.cs |
| C-3 Raid | Raid.cs (+ `[ASIS]CardEffectFactory/KeyWordEffects/Raid.cs`) |
| C-4 Decoy | Decoy.cs |
| C-5 Barrier | Barrier.cs |
| C-6 Fortitude | Fortitude.cs |
| C-7 Evade | Evade.cs |
| C-8 Retaliation | Retaliation.cs |
| C-9 Execute | Execute.cs |
| C-10 Collision | Collision.cs |
| C-11 Fragment | Fragment.cs |
| C-12 Iceclad | Iceclad.cs |
| C-13 Decode | Decode.cs |
| C-14 Partition | Partition.cs |
| C-15 Progress | Progress.cs |
| C-16 Overclock | Overclock.cs |
| C-17 Ascension | Ascension.cs |
| C-18 Alliance | Alliance.cs |
| C-19 Scapegoat | Scapegoat.cs |
| C-20 Vortex | Vortex.cs |
| C-21 ArmorPurge | ArmorPurge.cs |
| C-22 Save | Save.cs |
| C-23 MaterialSave | MaterialSave.cs |
| C-24 Training | Training.cs |
| (참고) MindLink | MindLink.cs |
| (이미 실효) Blocker/Jamming/Pierce/Reboot | Blocker.cs/Jamming.cs/Pierce.cs/Reboot.cs |

> 헤드리스 현재 키워드 골격: `[HL]Effects/KeywordBaseBatch1.cs`·`KeywordBaseBatch2.cs` (Blocker/Jamming/Reboot/Piercing 실효 + Rush/Blitz/Retaliation/ArmorPurge 플래그).

---

## D. 대형 서브시스템

| ID | 헤드리스 생성/수정 위치 | AS-IS 원본 위치 |
|----|------------------------|-----------------|
| **D-1 Link** | 신규 `[HL]Runtime/LinkController.cs` + linked-card 존(`[HL]Services/InMemoryZoneMover.cs`) + 미러 `[HL-Assets]CardEffects/AddLinkConditionClass.cs`·`ChangeLinkCostClass.cs`·`ChangeLinkMaxClass.cs`, `[HL-Assets]CardEffectFactory/{AddLinkRequirement,ChangeLinkMax}.cs`·`KeyWordEffects/Link.cs` | `[ASIS]CardEffects/AddLinkConditionClass.cs`·`ChangeLinkCostClass.cs`·`ChangeLinkMaxClass.cs`; `CardEffectFactory/AddLinkRequirement.cs`·`ChangeLinkMax.cs`·`KeyWordEffects/Link.cs`; `CardEffectCommons/TrashLinkedCards.cs`; `CanUseEffects/{WhenLinked,WhenWouldLink,OnTrashLinkCard,OnTrashLinkedCard}.cs` |
| **D-2 Appfuse** | 신규 `[HL]Runtime/AppfuseAction.cs` + 미러 `[HL-Assets]SelectAppFusionEffect.cs`·`CardEffects/AddAppFusionConditionClass.cs`·`CardEffectFactory/AddAppfusionMethod.cs` | `[ASIS]SelectAppFusionEffect.cs`·`CardEffects/AddAppFusionConditionClass.cs`·`CardEffectFactory/AddAppfusionMethod.cs` |
| **D-3 Raid** | `[HL]Runtime/AttackPipeline.cs`(시큐 직접공격 훅) + 미러 키워드(C-3) | `[ASIS]CardEffectCommons/KeyWordEffects/Raid.cs`·`CardEffectFactory/KeyWordEffects/Raid.cs` |
| **D-4 De-Digivolve** | `[HL]Runtime/DigivolutionSourceStackPort.cs`(소재 N장 detach) + `[HL-Assets]CardEffects/ImmuneFromDeDigivolveClass.cs` 미러 | `[ASIS]SelectPermanentEffect.cs`(Degenerate 모드)·`CardEffects/ImmuneFromDeDigivolveClass.cs` |
| **D-5 DNA/Jogress·DigiXros** | `[HL]Runtime/DigivolveAction.cs`(확장) + 미러 `[HL-Assets]SelectJogressEffect.cs` | `[ASIS]SelectJogressEffect.cs`; `CardEffectFactory`(GetJogressConditionClass/DigiXrosEffectFromNames); `DigiXrosEffectObject.cs` |
| **D-6 Blast/Arts Digivolve** | `[HL]Runtime/DigivolveAction.cs`(경로 추가) + 미러 `[HL-Assets]SelectBurstDigivolutionEffect.cs` | `[ASIS]SelectBurstDigivolutionEffect.cs`; `CardEffectFactory`(Blast/Arts) |
| **D-7 효과 무효화** | 신규 `[HL]Effects/EffectInvalidation.cs` + 평가 통합 | `[ASIS]CheckEffectDisabledClass.cs`; `ICardEffect.cs`(IsDisabled) |
| **D-8 코스트 감소 파이프라인** | `[HL]Effects/PlayCostHelpers.cs`·`DigivolutionCostHelpers.cs`(확장) + BeforePayCost emit(F-6.7) | `[ASIS]CardController.cs`(지불 단계)·`CardEffects/Change*CostClass.cs` |
| **D-9 Recovery/Token/MindLink/Delay** | `[HL]Services/InMemoryZoneMover.cs`(Recovery) + B-9(Token) + 미러 `[HL-Assets]CardEffectCommons/KeyWordEffects/MindLink.cs` | `[ASIS]CardObjectController.cs`(AddSecurityCard); `CardController.cs`(PlayToken); `KeyWordEffects/MindLink.cs`; Delay Option(CardEffectFactory) |

---

## 요약 규칙 (신규 파일 만들 때)
1. **카드가 직접 호출하는 것**(Commons/Factory/Select/Keyword/CardEffect 본문) → `[HL-Assets]`에 **원본과 동일 상대경로**로 미러.
2. **엔진 내부 배관**(스케줄러/게이트/뮤테이션/존무버/평가) → `[HL]Effects` 또는 `[HL]Runtime`. 파일 상단 주석에 **대응 AS-IS 파일** 명시(이미 `ContinuousDpGate`/`BattleDeletionGate` 등이 이렇게 함).
3. **테스트** → `tests/G3.5-<ID>.<Name>.Tests/` (기존 관행).
