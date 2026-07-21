# 구조적 발명물 전수 census — 심볼-도달성 아닌 AS-IS 경로-대조 (2026-07-21)

Base: `git HEAD 149851c7`(브랜치 `d1-r4-integration`). **read-only**(엔진 소스 무변경) — grep + 파일시스템만, 빌드/테스트 미실행. AS-IS grep 전량 `--binary-files=text`. 경로는 `src/HeadlessDCGO.Engine/` 상대(별도 표기 없으면), AS-IS는 `DCGO/Assets/…`.

## §0. 왜 이 census인가 (기존 심볼-census와의 차이)

기존 전수(`registry_probe_census_2026-07-20.md`·계기판)는 **심볼-기준** — 알려진 발명 타입(`EffectRegistry`·`EffectBinding`·`ToBinding`·`IEffectBody`·`IActivatedCardEffect`·`IHeadlessCardEffect`)을 sentinel 개명·참조 열거하는 방식. 이 방식은 **알려진 심볼을 참조하지 않는 발명물**을 구조적으로 놓친다. 본 census는 **파일-경로 대조**로 접근: mirror-into-asis-file 규약(미러 = 동일 경로·동일 파일명)을 판정 기준으로, **AS-IS에 대응 경로 파일이 없는 미러 파일/클래스는 정의상 substrate 아니면 발명물**.

자가검증 표적: `OnPlayReactivation`(발명 body 1개만 물고 Headless/Runtime에 독립 파일로 존재, 등록된 심볼 미참조) — 심볼-census가 놓친 것이 본 방법으로 잡히는가.

---

## §1. 층별 파일 수 집계

엔진 소스 .cs(= `bin/`·`obj/` 제외): **4,616**.

| 층 | 파일 수 | AS-IS 대응 | 성격 |
|---|---:|---|---|
| **미러 층** `Assets/Scripts/` (동일-경로 AS-IS 존재) | **4,261** | `DCGO/Assets/Scripts/` 동일 상대경로 | 게임 로직 미러(카드 corpus + Script 엔진층) |
| **미러-경로 이탈** `Assets/Scripts/` (AS-IS 경로에 동일-파일명 부재) | **140** | 부재(§2) | 판정 대상 A |
| **substrate 층** `Headless/` (AS-IS 대응 원래 없음) | **215** | 전무 | 판정 대상 B — 개별 substrate 후보 |

미러 층 대조: engine `Assets/Scripts` 4,401 vs AS-IS `Assets/Scripts` 4,354 → engine-only(경로-부재) = **140**(`comm -23`).

### 140 engine-only 내역
| 하위 | 수 | 분류 |
|---|---:|---|
| `CardEffect/TestFixtures/Tfx*.cs` | 100 | (T) 테스트 스캐폴딩 |
| `Script/CardEffectCommons/*.cs` | 37 | (I)+(S)-mirror 혼재 → §2 |
| `Script/DataTools/{FilterCardList,GameplayOption}.cs` | 2 | (S) 미러-경로 이탈(§2.3) |
| `Script/OnEnterFieldHashtableParams.cs` | 1 | (S) 미러-분할(§2.3) |

---

## §2. 판정 대상 A — `Assets/Scripts` engine-only 40파일(비-Tfx)

### 2.1 (I) 발명물 — registry/binding/activated/dispatch 계열 (전량 기(旣)등재)

이 12파일은 재구축 발명 effect-model의 corpus·인프라. **전부 `registry_probe_census_2026-07-20.md`가 심볼로 이미 열거**(등재됨, NEW 아님). AS-IS `DCGO/` grep hit 0 확인분 포함.

| 파일 | 역할 | 소비자(1-hop) | 등재 |
|---|---|---|---|
| `CardEffectCommons/CardEffectRegistrar.cs` | enter-play 효과 **바인딩 등록 관문**(`.Register` 단일 디스패치 :237, leave-play `RemoveWhere` :129) | EffectRegistry, PlayCardAction | 프로브 §2 producer/reader |
| `CardEffectCommons/ActivatedEffect.cs` | uniform-activated `IEffectBody`(AS-IS ActivateCoroutine 미러 주장) | ActivatedEffectResolver, corpus | 프로브 §3c (69좌석) |
| `CardEffectCommons/ActivatedEffects.cs` | activated corpus(`IActivatedCardEffect` 구현자 다수) | resolver, factory | 프로브 §3d (56좌석) |
| `CardEffectCommons/ActivatedEffectResolver.cs` | activated 효과 resolve + `.Register`(:706) | OnPlayReactivation, actions | 프로브 §2 producer |
| `CardEffectCommons/ActivatedHashtableBridge.cs` | 미러 이벤트→AS-IS `StackSkillInfos` 페이로드 재구성 브릿지 | corpus CanUse 경로 | 프로브(활성 브릿지) |
| `CardEffectCommons/LegacyActivatedBridge.cs` | **명시 "LEGACY-BRIDGE, retires with old-model corpus"** — `IActivatedCardEffect` abstract-class 부활 마커 | corpus | 프로브 §2/§4 (56좌석) |
| `CardEffectCommons/ContinuousAndRestrictionEffects.cs` | 연속 numeric 자기-수정 → `EffectBinding` lowering | ContinuousModifierGate 등 | 프로브 §3a (23좌석) |
| `CardEffectCommons/ContinuousEffectEvaluator.cs` | 연속/제약/교체 평가 허브 | BattleDeletionGate·ContinuousRestrictionGate | 프로브 §4(a) |
| `CardEffectCommons/NewModelContinuousScan.cs` | "P6 dispatch-flip STAGE B" 신모델 연속 인터페이스 스캔("NO registry in AS-IS") | 게이트층 | 프로브(연속 판독) |
| `CardEffectCommons/InheritedGrantedSecurityHelpers.cs` | `Create*Binding` 팩토리(7) — granted-security 바인딩 | registrar | 프로브 §3a |
| `CardEffectCommons/TriggeredEffects.cs` | 트리거드 효과 바인딩(`RemoveWhere` :109) | 트리거 루프 | 프로브 §2 reader |
| `CardEffectCommons/CardEffectDispatch.cs` (?) | 카드번호→효과 클래스 **리플렉션 맵**(G6-001, AS-IS CardEffectFactory 대체) | registrar, factory | 경계 — §4 (?) |

### 2.2 (S) 모놀리스-분할 미러 파티션 / 헬퍼 번역 (발명 아님 — 미러 내용, 파일명만 이탈)

이 25파일은 자기-서술상 **AS-IS 모놀리스(`CardEffectCommons.cs`·`ICardEffect.cs`·`Permanent.cs`)의 메서드를 분할·번역한 미러 파티션**(mig-goal7.5 monolith split의 산물). AS-IS에 **기제가 실재**하며 로직은 1:1 — 다만 동일-파일명 규약을 이탈(별도 파일로 재하우징). 발명물 아님.

`CanUseEffectHelpers` · `CardRequirementHelpers` · `Conditions` · `EffectChoiceHelpers` · `EffectTiming` · `InheritedEffectHelpers` · `MinMaxRequirementHelpers` · `ModifierHelpers`(AS-IS `CalculateOrder` 미러) · `OnceFlagHelpers` · `OptionMainEffect`(AS-IS `CardEffectCommons.OptionMainEffect` 1:1) · `ReplacementHelpers` · `RestrictionHelpers` · `SpecialConditionHelpers`("mirrored 1:1") · `TargetFilterHelpers` · `TimingPriorityHelpers` · `TriggerConditionHelpers` · `TurnOwnershipHelpers` · `ZoneQueryHelpers` · `CanUseEffectHelpers` · `DigivolveAndTrashBridge`·`PlayCardsBridge`·`ProcessAccordingToResultBridge`(모놀리스 mutation-helper의 AS-IS-signature Task 오버로드 shim) · `CardPortingFramework`(AS-IS `SimplifiedSelectCardConditionClass`/RevealLibrary.cs 미러) · `KeyWordEffects/KeywordBaseBatch1.cs`·`KeywordBaseBatch2.cs`(키워드 base 배치 번역) · `PermanentBookkeepingStore`(?)(AS-IS Permanent.cs:3686-3941 "just-after" 필드의 match-scoped 운반 — "Store" 명명이나 AS-IS 필드 carrier → §4 (?)).

### 2.3 (S) 미러-경로 이탈 3파일 (AS-IS 파일이 **다른 경로에** 실재)

| 파일 | AS-IS 실재 위치 | 판정 |
|---|---|---|
| `Script/DataTools/FilterCardList.cs` | `Script/FilterCardList.cs` | (S) 미러 — 경로만 `DataTools/`로 이동 |
| `Script/DataTools/GameplayOption.cs` | `Script/GameplayOption.cs` | (S) 미러 — 경로만 이동 |
| `Script/OnEnterFieldHashtableParams.cs` | `Script/CardEffectCommons/HashtableSetting.cs` 내부 클래스 | (S) 미러 — 파일 분할("ported verbatim") |

### 2.4 (T) 테스트 스캐폴딩 100파일 (`CardEffect/TestFixtures/Tfx*.cs`)

출하 게임로직 아님 — 행동테스트 픽스처. AS-IS 무대응은 정상(테스트 자산). 발명-census 대상 외(별도 버킷). 단, 프로브 §4(c)가 지적한 **구모델 단언-핀 Tfx 18장**은 corpus 삭제 시 은퇴 동승(기등재).

---

## §3. 판정 대상 B — `Headless/` 215파일 (substrate 층)

`Headless/`는 아키텍처 결정(`asis-mirror-migration-decision`)상 **substrate 전용 지정 층** — "미러 층=게임 로직, Headless/=substrate만". 따라서 이름-부재는 발명 판정 근거가 못 됨(전부 Headless-prefix 번역 배관이라 AS-IS 이름 grep 0은 정상). 판정 기준 = **게임 룰 판단을 담느냐(발명/룰-게이트) vs 순수 배관(substrate)**.

### 3.1 (S) 순수 substrate 클러스터 — 82파일 (개별 게임룰 무보유)

| 디렉터리 | 수 | 성격(substrate 근거) |
|---|---:|---|
| `Services/` | 34 | 엔티티 id·리포지토리·ZoneMover·랜덤·로그·쿼리 인터페이스 + InMemory 구현 — 배관/결정론 인프라 |
| `State/` | 13 | 상태 record·어댑터(PlayerState·ZoneState·DigivolutionStack·DpCalculator) — AS-IS 상태 번역 |
| `Choices/` | 10 | choice 파이프(Request/Result/Provider) — RL/에이전트 어댑터 |
| `DataLoading/` | 8 | 카드/덱 로더·밴리스트·검증 — 자산 적재 배관 |
| `Diagnostics/` | 7 | trace/log sink — 관측 배관 |
| `Bridge/` | 6 | EngineContext·Continuous/Ambient context·GManagerBridge·PayCostRoot·UnityNullObjectPolicy — 컨텍스트 어댑터 |
| `Coroutines/` | 4 | Task runner·wait condition — Unity 코루틴→async 번역(결정론) |
| `Rules/` | 1 | `TimingWindowTrigger` — 타이밍 창 배관 |

### 3.2 `Runtime/`(102) + `Effects/`(30) = 132 혼재층

**(a) 발명 effect-model 룰-게이트/인프라 — 기등재(프로브 census 소비자)**: registry 심볼(`EffectRegistry`·`EffectBinding`·`GetContinuousEffects` 등)을 참조하는 26파일 = 프로브 census가 이미 열거한 registry-소비 게이트/스캔/sink. 대표: `EffectRegistry`·`EffectDurationExpiry`·`MatchStateMutationSink`·`CardEffectSchedulerResolver`·`CardLeavePlayCleanup`·`ContinuousKeywordGate`·`ContinuousImmunityGate`·`ContinuousScopeEvaluation`·`RestrictionScan`·`CanNotPlayOptionScan`·`DeletionReplacementGate`·`DeletionReplacementTiming`·`EffectInvalidation`·`SecurityResolver`·`GameFlowProcessor`·`HeadlessCardEffectContract`·`SkillInfo` 등. **전부 등재**.

**(b) 발명 게이트 형제 — 기등재 계열(연속-slice, registry 경유)**: `BattleDeletionGate`·`ContinuousRestrictionGate`·`ContinuousModifierGate` — 자기-서술 "the registry is queried"(ContinuousEffectEvaluator 경유). registry-소비 게이트 계열로 프로브 §4(a) teardown 맵 소속.

**(c) (S) AS-IS-chokepoint 미러 룰-번역(registry 무경유, 이름된 AS-IS 클래스의 substrate 미러)**: 자기-서술이 명시 AS-IS 클래스를 미러. 발명 아님(번역).
- `AceOverflowGate`(AS-IS `AceOverflowClass.Overflow`) · `AttackTargetSwitchGate`(AS-IS `Permanent.CanSwitchAttackTarget` :3745) · `OverclockEffect`(AS-IS Overclock KeyWord) · `ProgressImmunity`(AS-IS `ProgressProcess`/Progress.cs:62) · `EffectDrivenAttack`(AS-IS `SelectAttackEffect`, "no new pipeline") · `BattleResolver`·`AttackPipeline`·`AttackPhase`(AS-IS AttackProcess 미러) · `DeDigivolveHelpers`·`DigivolveCommons`·`FreeDigivolveHelpers`·`FusionDigivolveHelpers`·`LinkHelpers`·`DpBoostHelpers`·`DpZeroDeletionHelpers`·`DigivolutionStackHelpers` 등.

**(d) (S) 엔진/RL substrate(순수 배관)**: `HeadlessAction*`(7) · `GameEvent*`(3) · `IHeadless*Controller`+`InMemoryHeadless*Controller`(10) · `HeadlessGameLoop`·`TurnFlowDriver`·`TurnStepCursor`·`StepResult`·`SessionContext`·`MatchConfig/Result`·`MatchSetupFlow`·`MulliganCoordinator`·`ActionMask`·`*ActionProcessor`·`LegalAction*`·`CardMovementPort`·`CardStateMutationPort`·`ObservationSnapshot`·`CardObservation`·`TerminalEvaluator`·`Headless*State`(Attack/Choice/Effect/Memory/Turn) 등. RL 환경/결정론 배관.

**(e) (S) effect-model 배관(발명 아님, 큐/컨텍스트/스케줄)**: `EffectContext`·`EffectContextAdapter`·`EffectRequest`·`EffectResult`·`EffectResolutionQueue`·`EffectResolutionMode`·`EffectScheduler`·`PendingEffect`·`OptionalPromptQueue`·`MandatoryEffectOrdering`·`TriggerEventEmitter`·`TriggerTimingMap`·`TriggerTimings`·`SkillWindowContinuation`·`SkillWindowSupply`·`WindowChoicePendingException`·`DeferredActivationController`·`DeferredChoiceProvider`.

**(f) (D) dead/은퇴 잔재**: `WindowResolutionController` — 자기-서술 **"Retired shell"**(SkillInfo 컷오버 C2/C2b에서 은퇴; 구 step-driver drain-once 잔재만). 참조-희박 은퇴 잔재.

**(g) (I) NEW — §4 헤드라인**: `OnPlayReactivation` (아래).

---

## §4. **NEW 발명물** (미등재 — 이 census의 헤드라인)

### NEW-01 · `Headless/Runtime/OnPlayReactivation.cs` — (I) 발명 드라이버

- **무엇**: EX8_074 region "All Turns"의 "[All Turns] (Once Per Turn) When Digimon are played, you may activate this Digimon's [When Digivolving] effects" **재활성 창을 전용 static 드라이버로 구현**. 카드가 play될 때마다 양 플레이어 battle-area를 스캔, `CardEffects(OnEnterFieldAnyone)`가 `ReuseWhenDigivolvingEffect`를 내는 홀더 중 once-per-turn 가드(`allTurnsReactivationUsed`)가 열린 것을 `ActivatedEffectResolver`로 재해소.
- **왜 발명물**: AS-IS `DCGO/`에 `OnPlayReactivation`·`ReuseWhenDigivolving`·`allTurnsReactivation` grep **hit 0**. AS-IS는 이 카드의 [All Turns] 반응을 **범용 효과-broadcast(OnEnterFieldAnyone + StackSkillInfos)** 경로로 처리 — 별도 드라이버 파일 없음. 미러는 카드 거동을 **범용 dispatch가 아닌 substrate-Runtime의 bespoke hook으로 승격**(전용 전달자). mirror-into-asis-file 규약 위반: substrate 층에 게임-로직 장치가 상주.
- **소비자(1-hop)**: `Headless/Runtime/TurnFlowPump.cs`, `Headless/Runtime/PlayCardAction.cs`(play 후 호출), `Assets/…/EX8/Green/EX8_074.cs`(홀더 마킹), `ActivatedEffectResolver`(해소 위임).
- **등재 여부**: **미등재**. `registry_probe_census`(심볼)·`RD-RETIRE`/`G1R-001` 은퇴 원장 어디에도 없음. 설계 워크시트(`window_cutover_worksheet`·`r4_tsm_s1_design`·`session_handoff`·`ex8_074_remaining_goals`)에 **설계-논의만** 존재 — 발명-원장 핀 없음.
- **소멸 경로 제안**: EX8_074의 [All Turns] 재활성을 **범용 broadcast 트리거로 환원** — `ReuseWhenDigivolvingEffect`를 카드의 `OnEnterFieldAnyone` 효과로 이식하고, once-per-turn은 기존 `OnceFlagController`(MaxCountPerTurn) 배관에 합류. 드라이버 파일·`TurnFlowPump`/`PlayCardAction`의 전용 호출 2좌석 삭제. AS-IS StackSkillInfos 반응 경로가 이미 존재하므로 원자 flip 가능. **적대리뷰 대상**(EX8_074 witness green 유지 확인).

### 방법론 자가검증 결과: **PASS**

`OnPlayReactivation`은 등록 심볼(`EffectRegistry`/`EffectBinding`/`IEffectBody`…)을 **일절 참조하지 않아** 심볼-census 6종 프로브에 걸리지 않았고, 실제 프로브 census 파일 목록에도 부재. 본 **경로-대조 census가 단독으로 포착** — 방법론이 심볼-census의 사각을 메움을 실증.

---

## §5. (?) 경계 목록 (뭉개기 금지 — 사유 명시, S 우세 판단)

| 파일 | 경계 사유 | 잠정 |
|---|---|---|
| `CardEffectCommons/CardEffectDispatch.cs` | 카드번호→효과 리플렉션 맵. AS-IS `CardEffectFactory`(팩토리 조회)의 substrate 번역이나 "no manual table" 자동성장은 AS-IS에 없는 접근 — dispatch 인프라(발명 vs 번역 경계) | (S) 우세(팩토리 조회 등가), G6-001 태그. registrar와 달리 registry-바인딩 미보유 |
| `CardEffectCommons/PermanentBookkeepingStore.cs` | "Store" 명명(발명-flag 단어)이나 AS-IS Permanent 필드(:3686-3941) match-scoped carrier — 미러 Permanent가 per-access view라 필요한 번역 | (S) 우세(AS-IS 필드 운반) |
| `Effects/OnceFlagController.cs` + `CardEffectCommons/OnceFlagHelpers.cs` | once/max-per-turn 활성 회계. AS-IS는 per-effect 플래그 — match-scoped holder로 집약(발명 회계 vs 번역 경계) | (S) 우세(AS-IS once 의미 번역) |
| `Effects/AutoProcessingTriggerCollector.cs` | AS-IS `AddEffectToPlayer`/temp `AddEffectToPermanent` 지연 one-shot 마킹 — PRIM-P0 태그. AS-IS 기제 실재하나 collector 형태는 substrate 발명 소지 | (S) 우세(명시 AS-IS 대응) |

이 4건은 모두 **명시된 AS-IS 대응 기제를 인용**하므로 S로 기울지만, "Store"/"Controller"/"Collector"/"Dispatch" 명명이 발명-flag 단어라 추가 화이트박스 확인 시 재분류 여지 — dead-judgment 규율상 (?) 유지.

---

## §6. 집계 요약

| 구분 | 수 |
|---|---:|
| 엔진 소스 .cs 총 | 4,616 |
| — 미러 층(동일-경로) | 4,261 |
| — 미러-경로 이탈 `Assets/Scripts`(engine-only) | 140 |
| — substrate 층 `Headless/` | 215 |
| **(S) substrate/미러-번역** | ≈ 28(CardEffectCommons 분할·경로이탈) + 82(Headless 순수 클러스터) + Runtime/Effects (c)(d)(e) 대다수 |
| **(I) 발명물 — 기등재** | 12(CardEffectCommons registry/activated 계열) + Runtime/Effects registry-소비 게이트/인프라 26+3(형제 게이트) = **프로브 census 열거분과 일치** |
| **(I) 발명물 — NEW(미등재)** | **1** (`OnPlayReactivation`) |
| **(D) dead/은퇴 잔재** | 1 (`WindowResolutionController`) + Tfx 구모델-핀 18(기등재 은퇴 예정) |
| **(T) 테스트 스캐폴딩** | 100 (`Tfx*`) |
| **(?) 경계** | 4 (§5) |

**헤드라인**: 경로-대조 전수 결과, 기존 심볼-census(registry/binding/activated/gate 계열)가 커버하지 못한 **NEW 발명물 = 1건(`OnPlayReactivation`)**. 그 외 (I)는 전부 프로브 census·RD-RETIRE/G1R 원장에 기등재. 즉 심볼-census의 사각은 **좁으나 비어있지 않음** — substrate-지정 층(`Headless/`)에 상주한 bespoke 카드-로직 드라이버 1건이 유일한 구조적 누락.

---

## §7. 방법론 한계

1. **클래스-단위 미전수**: 판정은 **파일 단위 우선**. 동일-경로 미러 파일 4,261장 내부에 AS-IS 원본에 없는 발명 클래스가 추가됐는지는 전수 안 함 — CardEffectCommons·Headless 계열만 대표 grep 표본. 미러 카드 corpus(4,018장) 내부 발명 클래스는 본 census 범위 밖(별도 corpus 감사 필요).
2. **Headless 클러스터-단위 집계**: `Headless/` 215장 중 순수-substrate 클러스터(Services/State/Choices/DataLoading/Diagnostics/Bridge/Coroutines/Rules 82장)와 Runtime/Effects (d)(e) 배관은 **디렉터리-성격 + 대표 헤더 표본**으로 판정 — 132장 Runtime/Effects 전장 화이트박스 미독. 추가 bespoke 드라이버(OnPlayReactivation류)가 이 안에 더 있을 잔여 가능성은 낮으나(card-id·"Reactivat"·bespoke-region grep로 스크리닝 완료: OnPlayReactivation만 유일 hit) 0 보장 아님.
3. **grep 이름-대조의 한계**: substrate 층 발명 판정을 이름-부재로 하지 않고(설계상 substrate 지정) 자기-서술·소비자·AS-IS-인용으로 판정 — 자기-서술이 부정확한 파일이 있으면 오분류 가능. §5 (?) 4건이 그 경계.
4. **빌드 미실행**: 소비자 수는 grep 1-hop(정적) — 프로브 census 같은 컴파일러-열거(cascade-suppress 보정)는 안 함. 정확 좌석 수는 프로브 census 참조.
