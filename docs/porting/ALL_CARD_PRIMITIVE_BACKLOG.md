# All Card Primitive Backlog

Scope: original Unity card scripts under `DCGO/Assets/Scripts/CardEffect` across all set folders.

Scan date: 2026-07-04.

This is a static source scan for primitive planning. It does not change card behavior and does not modify `DCGO/`.

> **검증(2026-07-05):** 이 스캔의 핵심 주장을 헤드리스 코드베이스로 실측 확인함. 헤드리스 `EffectTiming` enum은
> 14종뿐이며 `OnDeclaration`(295장)·`OnStartMainPhase`(220장)·`WhenPermanentWouldBeDeleted`(199장) 등은
> 부재 확인. `Mode.Custom`/코루틴 흐름 사용 카드 2,806장(72%)은 실측치와 정확히 일치. 타이밍 갭 1,484장(38%),
> 둘 중 하나라도 해당하는 갭 카드 3,122장(80%). **갭 없이 포팅 가능한 카드는 796장(20%)뿐.** 이전
> CardIrExtractor 결론("갭 1종, 프리미티브 완료")은 팩토리 이름 커버리지만 본 오판이었음.

## Scan Summary

- Set directories scanned: 64
- Non-empty set directories: 63
- Card effect files scanned: 3,918
- Empty set directory: `EX12`
- Current comparison targets:
  - `docs/porting/PRIMITIVE-CATALOG.md`
  - `src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectFactory*`
  - `src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons*`
  - `src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons/CardPortingFramework.cs`

The broad result is similar to the BT20+ scan: most direct `CardEffectFactory.*` calls already have a headless surface. The real blockers are missing card-facing timings, coroutine-shaped choice/follow-up flows, temporary effect grants, option play, cost-modification windows, and several special mechanics.

## ✅ P0 해결 상태 (2026-07-05) — 아래 STOP/갭은 전부 구현 완료

이 세션에서 P0 Build Order 전부 구현·검증(전체 스위트 329 PASS). **아래 갭들은 더 이상 STOP이 아니다** — 지정된
헤드리스 팩토리/타이밍/메커니즘으로 포팅하라. 상세 번역은 `docs/audit/porting_translation_cheatsheet.md` §6, 시그니처는
`PRIMITIVE-CATALOG.md`(147종).

| AS-IS 갭 | 헤드리스 해결 (팩토리/메커니즘) |
|---|---|
| 타이밍 부재(OnDeclaration/OnStartMainPhase/WhenPermanentWouldBeDeleted/OnEndAttack/OnDigivolutionCardDiscarded/OnAttackTargetChanged 등 32종) | `EffectTiming` enum에 추가·발화됨. 타이밍 심볼 그대로 사용. |
| mode-choice (`Mode.Custom` 메뉴, 366장) | `CardEffectFactory.SelectModeEffect(card, desc, params ModeChoiceEffect.Mode[])` — 각 모드는 라벨+가용성술어+분기효과 |
| typed select-follow-up (존에서 골라 hand/trash/deck/security/디지볼브, 2,806장 흐름) | `SelectAndAddToHandFromZoneEffect` / `SelectAndTrashFromZoneEffect` / `SelectAndReturnToDeckEffect` / `SelectAndPutSecurityEffect` / `SelectAndDigivolveEffect` |
| cost-modification 윈도우 (`BeforePayCost`, 134장) | `CardEffectFactory.BeforePayCostReductionEffect(card, amount, condition, desc)` + 액션-루트 게이팅(`CardEffectCommons.IsPayCostRoot`) |
| Cannot add security (104) | `CardEffectFactory.CanNotAddSecurityStaticEffect(scopePlayer, isInherited, card, condition, causingEffectPredicate?)` |
| Cannot add memory (6) | `CardEffectFactory.CanNotAddMemoryStaticEffect(scopePlayer, isInherited, card, condition, causingEffectPredicate?)` |
| Cannot reduce cost (5) | `CardEffectFactory.CanNotReduceCostStaticEffect(permanentCondition, isInherited, card, condition)` |
| `PlayOptionCards` (34) | `CardEffectFactory.PlayOptionCardEffect(card, sourceZone, optionPredicate, maxCount, canEndNotMax, desc)` |
| `AddEffectToPlayer` 지연 one-shot (30) | `CardEffectCommons.AddEffectToPlayer(duration, card, nestedEffect, timing)` — nested가 timing T에 1회 발화 후 self-remove |
| temp `AddEffectToPermanent` (26) | `CardEffectCommons.AddEffectToPermanent(perm, duration, card, nested, timing)`; **자기-[On Deletion]**은 `AddSelfRemovalEffectToPermanent(...)`(leave-play cleanup 면제 + fire-then-clear) |
| `AddSkillClass` 키워드 부여 (대다수) | player-scope `XxxStaticEffect(permanentCondition, isInherited, card, condition)` — Piercing/Blitz/Retaliation/Scapegoat/Decoy/Barrier/Alliance/Rush/Reboot/Jamming/Blocker/ChangeSAttack |
| `AddSkillClass` triggered 부여 (BT8_031류) | `CardEffectFactory.GrantTriggeredEffectToScopedSet(card, scopePlayer, nestedTriggeredEffect)` — nested는 `TriggerEntityId`(트리거한 카드)를 읽고 per-card 술어 적용 |

여전히 STOP인 것(소수): STOP 동적 표면 중 nested 임의효과(일부), DNA 임시재료, max-trash DigiXros 등 — 아래 표 유지. 위 표 밖의 갭만 STOP.

## P0 Timing Surface

These `EffectTiming.*` values appear in original card scripts but are missing from the current headless `EffectTiming` enum.

| Timing | Cards | Priority note |
| --- | ---: | --- |
| `OnDeclaration` | 298 | Very high impact: attack declaration and declaration-time modifiers. |
| `OnStartMainPhase` | 222 | High impact: common modern start-main triggers. |
| `WhenPermanentWouldBeDeleted` | 206 | High impact: deletion prevention/replacement windows. |
| `WhenRemoveField` | 164 | High impact: leave-field triggers. |
| `OnTappedAnyone` | 139 | Suspension trigger family. |
| `OnCounterTiming` | 111 | Counter/ACE/blast timing family. |
| `OnEndBattle` | 84 | Engine trigger exists, card-facing enum is missing. |
| `OnEndAttack` | 80 | End-of-attack trigger family. |
| `OnLoseSecurity` | 73 | Security-loss trigger family. |
| `WhenLinked` | 64 | Link trigger family. |
| `OnDigivolutionCardDiscarded` | 53 | Source-trash trigger. |
| `OnAddDigivolutionCards` | 50 | Source-add trigger. |
| `OnDiscardHand` | 34 | Hand discard trigger. |
| `OnAttackTargetChanged` | 31 | Raid/block/switch-target trigger family. |
| `OnUseOption` | 30 | Option-use trigger. |
| `OnUnTappedAnyone` | 29 | Unsuspend trigger. |
| `OnAddHand` | 21 | Add-to-hand trigger. |
| `OnDiscardLibrary` | 20 | Deck-trash trigger. |
| `OnAddSecurity` | 14 | Security-add trigger. |
| `OnDiscardSecurity` | 14 | Security-discard trigger. |
| `WhenDigisorption` | 10 | Digisorption window. |
| `OnSecurityCheck` | 9 | Security-check trigger. |
| `WhenReturntoHandAnyone` | 9 | Return-to-hand trigger. |
| `WhenReturntoLibraryAnyone` | 9 | Return-to-deck trigger. |
| `AfterPayCost` | 7 | Post-payment window. |
| `OnLinkCardDiscarded` | 7 | Link-card trash trigger. |
| `OnDigivolutionCardReturnToDeckBottom` | 3 | Source bottom-deck trigger. |
| `WhenTopCardTrashed` | 3 | Deck-top trash trigger. |
| `AfterEffectsActivate` | 2 | Post-effect activation window. |
| `OnPermamemtReturnedToHand` | 2 | Original typo spelling appears in source. |
| `OnRemovedField` | 2 | Alternate leave-field spelling. |
| `OnReturnCardsToHandFromTrash` | 2 | Trash-to-hand trigger. |
| `WhenWouldLink` | 2 | Pre-link window. |
| `OnFaceUpSecurityIncreased` | 1 | Face-up security trigger. |
| `OnLeaveFieldAnyone` | 1 | Alternate leave-field trigger. |
| `OnReturnCardsToLibraryFromTrash` | 1 | Trash-to-deck trigger. |
| `OnUseDigiburst` | 1 | Digi-Burst trigger. |
| `WhenUntapAnyone` | 1 | Alternate unsuspend spelling. |
| `WhenWouldDigivolutionCardDiscarded` | 1 | Pre-source-trash window. |

## P0 Flow Primitives

These are the largest practical blockers for broad per-card porting.

| Gap | Cards | Why it matters |
| --- | ---: | --- |
| Custom select follow-up flow | 2,806 | Original cards often use `Mode.Custom`, `SelectPermanentCoroutine`, or `SelectCardCoroutine`. Many individual cases can map to existing select primitives, but broad porting needs typed "select then follow-up" primitives for draw, suspend, unsuspend, delete, bounce, DP/SAttack buffs, play, digivolve, source movement, and trash. |
| Mode choice / multi-mode option flow | 366 | Original cards frequently present a mode menu and then run different target/action branches. Current card-facing select primitives do not represent this as one atomic effect. |
| Cost modification / `ChangeCostClass` flow | 229 | Static cost modifiers exist, but original scripts include one-shot before-pay reductions, digivolution/play reductions, `ShowReducedCost`, and effect self-removal after payment. |
| Dynamic skill grant / `AddSkillClass` | 42 | Explicit STOP class. Needed for cards that grant arbitrary nested effects. |
| `PlayOptionCards` | 34 | Explicit STOP commons. Needed for option-from-zone execution. |
| `AddEffectToPlayer` delayed player effects | 30 | Low-level commons exists, but recipe marks this as strong-model territory. Needs explicit card-facing wrappers. |
| Mandatory reveal/process-all | 29 | Engine helper exists; card-facing wrapper/factory policy is unclear. Avoid turning mandatory processing into a player choice. |
| Temporary `AddEffectToPermanent` nested grants | 26 | Low-level commons exists, but common cases need safe wrappers for temporary granted triggers. |

## P0/P1 Special Mechanics

| Mechanic / class | Cards | Notes |
| --- | ---: | --- |
| Cannot add security | 104 | Scan catches `CannotAddSecurity` / `CanAddSecurity` family. `BT9_103` was already a known STOP probe. Needs a modeled security-add restriction gate. |
| `AddDigiXrosConditionClass` | 79 | Name-only DigiXros is partially covered, but arbitrary material predicates and related variants need audit. |
| `AddJogressConditionClass` | 59 | Predicate/name Jogress support exists in places; source usage is broad enough to warrant a parity pass. |
| Digi-Burst | 39 | Digi-Burst gates and payment windows remain a known difficult area. |
| Assembly | 14 | Some Assembly support exists, but field substitution/complex materials need careful coverage. |
| Mind Link process | 11 | `MindLinkClass` appears outside BT20+. Some docs exist, but needs card-facing process consistency. |
| Max-trash-count DigiXros | 9 | Already STOP-listed. |
| Max-under-Tamer DigiXros | 8 | Separate DigiXros material-count variant. |
| Burst Digivolution | 5 | Appears in BT13 and BT25. |
| App Fusion by condition | 4 | Name-based app fusion is easier; condition-based app fusion needs parity. |
| Jogress by levels | 4 | `AddJogressLevelsClass` appears separately from other Jogress helpers. |
| ACE overflow card-facing class | 3 | Engine has overflow support, but `AceOverflowClass` source usage needs card-facing mirror. |
| DNA digivolve with hand/trash temporary materials | 2 | Current commons explicitly throws STOP for this shape. |

## P1 Restriction / Replacement Primitives

| Restriction / replacement | Cards | Notes |
| --- | ---: | --- |
| Cannot switch attack target | 13 | `CanNotSwitchAttackTargetClass`. |
| Immune from de-digivolve | 11 | `ImmuneFromDeDigivolveClass`. |
| Cannot put in field | 7 | `CanNotPutFieldClass`. |
| Cannot add memory | 6 | `CannotAddMemoryClass`. |
| Cannot reduce cost | 5 | `CannotReduceCostClass`. |
| Do not battle security Digimon | 5 | `DontBattleSecurityDigimonClass`. |
| Change end-turn minimum memory | 2 | `ChangeEndTurnMinMemoryClass`; already named in STOP examples. |

## P2 Static / Continuous Class Parity

These are not all blockers, because some already have equivalent factory or commons surfaces. They should be audited when the relevant cards are ported.

| Source class | Cards | Notes |
| --- | ---: | --- |
| `CanNotAffectedClass` | 40 | Current `CanNotAffectedStaticEffect` exists; verify all predicates map cleanly. |
| `CanNotSuspendClass` | 33 | Current `CantSuspendStaticEffect` exists; verify player/permanent scopes. |
| `PlayCardClass` | 32 | Simple no-cost play exists for some cases; target/root/tapped/cost branches need coverage. |
| `SelectCardConditionClass` | 25 | Usually maps into reveal/select passes; custom follow-up still matters. |
| `DeckBottomBounceClass` | 23 | Current direct/selected bounce coverage should be audited. |
| `CanAttackTargetDefendingPermanentClass` | 20 | Attack-target restriction/permission family. |
| `CanNotDigivolveClass` | 14 | Static no-digivolve coverage exists; verify all scope variants. |
| `ChangeCardColorClass` | 14 | Color-changing surface should be checked against current card-source transforms. |
| `ReturnToLibraryBottomDigivolutionCardsClass` | 14 | Source-bottom-deck movement. |
| `ChangeBaseDPClass` | 12 | Base-DP changes exist in limited form; verify permanent/card scope variants. |
| `ChangeDPDeleteEffectMaxDPClass` | 11 | DP deletion threshold modifier. |
| `HatchDigiEggClass` | 10 | `HatchDigiEggEffect` exists; use it instead of STOP when only hatch is needed. |
| `ChangeCardNamesForDigiXrosClass` | 8 | DigiXros-specific name treatment. |
| `ChangePermanentLevelClass` | 6 | Permanent level transform. |
| `DontHaveDPClass` | 6 | "No DP" / treated-as-Digimon style support. |
| `TreatAsDigimonClass` | 6 | Current `TreatAsDigimonStaticEffect` exists; verify all variants. |
| `CanNotUnsuspendClass` | 3 | Current cannot-unsuspend surfaces exist; verify duration/scope variants. |
| `AddDigivolutionRequirementClass` | 3 | Compare against static self digivolution requirement helpers. |
| `CanNotBeRemovedClass` | 2 | Removal restriction. |
| `CanNotPlayClass` | 2 | Play restriction. |
| `ImmuneStackTrashingClass` | 2 | Existing factory exists; verify coverage. |

## P2 Commons / Glue Gaps

These missing `CardEffectCommons.*` names are often event-payload or UI-era helpers rather than new gameplay primitives. They still need a porting rule or card-facing equivalent to avoid repeated STOPs.

| Missing helper | Cards | Likely handling |
| --- | ---: | --- |
| `CardEffectHashtable` | 421 | Original coroutine payload builder. Usually replaced by `CardEffectResolveContext`/effect source metadata, not a gameplay primitive. |
| `ShowReducedCost` | 132 | UI helper around cost reduction. Need headless semantic/no-op rule tied to cost-modification primitives. |
| `HasNoElement` | 76 | Card trait/element helper. Add equivalent predicate helper. |
| `GetHashtablesFromHashtable` | 28 | Event payload extraction helper. Replace with context helper. |
| `customPermanentMessageArray_ChangeDP` | 23 | UI message helper; likely no-op or metadata only. |
| `WhenDigivolvingCheckHashtableOfCard` | 18 | Event payload gate helper. |
| `GetPlayCardClassFromHashtable` | 16 | Play-event payload helper. |
| `OnPlayCheckHashtableOfCard` | 10 | On-play payload gate helper. |
| `IsOnly1CardPlayed` | 9 | Play-count event helper. |
| `OptionMainCheckHashtable` | 7 | Option-main wrapper/gate helper. |
| `OptionMainEffect` | 5 | Option-main wrapper helper. |
| `OwnerHas1OrLessTamers` | 4 | Board-state helper. |
| `OnDeletionHashtable` | 3 | Deletion payload helper. |
| `OptionSecurityEffect` | 2 | Security option wrapper helper. |
| `customPermanentMessageArray_ChangeOriginDP` | 2 | UI message helper. |
| `customPermanentMessageArray_ChangeSAttack` | 1 | UI message helper. |

## Direct Factory Name Gaps

Only a small number of direct `CardEffectFactory.*` names are missing from the current catalog.

| Factory name | Cards | Recommendation |
| --- | ---: | --- |
| `ActivateClassesForSharedEffects` | 84 | Recipe already says to expand enabled timings into normal timing branches. Probably do not implement as a primitive unless porting ergonomics demand it. |
| `OnDeletionClass` | 1 | Tiny wrapper candidate. |
| `StartOfYourMainPhaseClass` | 1 | Tiny wrapper candidate once `OnStartMainPhase` exists. |
| `StartOfYourTurnClass` | 1 | Tiny wrapper candidate. |
| `WhenDigivolvingClass` | 1 | Tiny wrapper candidate. |
| `WhenMovingClass` | 1 | Tiny wrapper candidate. |

## Suggested Build Order

1. Add missing high-volume `EffectTiming` values and verify trigger emission/collection: start with `OnDeclaration`, `OnStartMainPhase`, `WhenPermanentWouldBeDeleted`, `WhenRemoveField`, `OnTappedAnyone`, `OnCounterTiming`, `OnEndBattle`, and `OnEndAttack`.
2. Build a generic atomic mode-choice activated effect primitive.
3. Build typed select-follow-up primitives for the common `Mode.Custom` shapes.
4. Normalize cost-modification primitives: before-pay, after-pay, one-shot, digivolution cost, play cost, `ShowReducedCost`, and self-removal.
5. Promote STOP-listed dynamic surfaces into explicit card-facing primitives: `AddSkillClass`, `AddEffectToPlayer`, `PlayOptionCards`, and common temporary `AddEffectToPermanent` grants.
6. Implement security/memory restriction gates: cannot add security, cannot add memory, cannot reduce cost.
7. Fill special mechanics: Digi-Burst, DigiXros variants, Jogress variants, Assembly, Mind Link process, App Fusion by condition, Burst Digivolution, ACE overflow, DNA with hand/trash temporary materials.
8. Add context helper replacements for missing hashtable-era commons so card ports can copy original gates without ad hoc code.

## Related Documents

- `docs/porting/BT20_PLUS_PRIMITIVE_BACKLOG.md` narrows this same analysis to `BT20` through `BT25`.
- `docs/porting/PRIMITIVE-CATALOG.md` remains the source of truth for primitives that are already available to card ports.
