# LA — 라이브 활성화 자동화 goal (WhenDigivolving / On-Play / [All Turns])

> 근거: EX8-074 포팅 중 확정된 **엔진 일반 갭**. 카드의 활성화 효과(`IActivatedCardEffect` — select-and-act류)는 `ActivatedEffectResolver`(EngineContext/ChoiceProvider 완비)로 해소되지만, 현재 이를 **라이브 게임 루프에서 자동 호출하는 곳**이 [Main]/[Security]/BeforePayCost/deferred-resume뿐이다. **[When Digivolving] · 자기 On-Play · [All Turns] 활성화 효과는 트리거 이벤트만 emit될 뿐 자동 해소되지 않는다.** 그래서 EX8_074 #5/#6와 기존 모든 [When Digivolving] 카드(ST1_08, ST2_09, ST3_09 …)가 "활성화 흐름/테스트로만 검증"되는 기준선에 머문다. 이 골은 그 활성화들을 **self-play에서 자동 발동**시켜, EX8_074뿐 아니라 **활성화 효과 카드군 전체를 한 번에 라이브화**한다.
>
> 공통 종료 기준: `bash scripts/run-tests.sh` 전체 green(FAIL=0) + 동작을 **라이브 게임 루프로 단언하는 테스트**(`DcgoMatch`/`HeadlessRlEnvironment` E2E) + `tools/RuleAudit` 위반 0. 커밋은 사용자 지시 시.
> 표준 규칙: **AS-IS 미러**(원본 타이밍/선결 1:1, 추측 금지) + **probe-first**(기존 윈도우 패턴 재사용; 없을 때만 신설). `Headless/**`는 change-control. 부차 게이팅 완화도 실패.

기준 커밋: `7955d03f` 이후(EX8-074 묶음 = 미커밋). 전체 242/242 green.

---

## 확정된 현재 상태 (probe)

- `ActivatedEffectResolver.ResolveAsync(context, cardId, controller, timing)` — 카드의 `CardEffects(timing)` 활성화 효과를 ChoiceProvider로 해소. EngineContext 완비. **라이브 호출처**: `OptionActivateAction`([Main]), `SecurityResolver`([Security]), `PlayCardAction`(BeforePayCost, EX8 Stage3), `MetadataActionProcessor:608`(deferred-activation resume). → **WhenDigivolving / On-Play / [All Turns] 호출처 없음.**
- `DigivolveAction:128`·`SpecialPlayAction:194`·`FusionDigivolveHelpers`: `TriggerTimings.WhenDigivolving` **이벤트만 emit**, 활성화 효과 auto-resolve 안 함.
- `PlayCardAction`: 카드 enter 시 `CardEffectRegistrar.RegisterCard`만(자기 On-Play 활성화 효과 auto-resolve 안 함; 단 BeforePayCost는 Stage3로 라이브).
- 트리거 효과(`IHeadlessCardEffect`)는 스케줄러로 해소되나 `CardEffectResolveContext`에 **EngineContext/ChoiceProvider 부재** → 스케줄러-해소 트리거는 선택 구동 불가. `ActivatedEffectResolver`를 스케줄러 사이클 내에서 부르면 **coordinator 중첩**(`CardEffectSchedulerResolver` Begin/CompleteResolution)으로 deferred replay 깨짐.
- **재사용 가능한 라이브-윈도우 패턴**: GR-006 `EndOfTurnEffectAttack` — 턴종료 훅이 ChoiceController 윈도우를 열고, 해소는 액션 플로우로. deferred-activation resume = `DeferredActivations.Suspend` + `MetadataActionProcessor:608` 재해소(OptionActivate 패턴).

→ 결론: **스케줄러-해소가 아니라, 트리거 시점에 `ActivatedEffectResolver`를 직접 호출하는 라이브 윈도우**가 정답(EngineContext 완비, coordinator 중첩 없음). 동기 resolver(self-play)는 인라인, 인터랙티브는 deferred-resume.

---

## ✅ LA-1 — 라이브 [When Digivolving] 활성화 — 완료

**구현:** `DigivolveAction.ProcessAsync`에서 RegisterCard 직후 `ActivatedEffectResolver.ResolveAsync(context, payload.CardId, playerId, EffectTiming.WhenDigivolving, ct)` 호출(no-op for 미포팅). 인터랙티브 deferred는 `DeferredChoicePendingException` catch → `DeferredActivations.Suspend(cardId, WhenDigivolving, playerId)` + pending 반환(OptionActivate 패턴, MetadataActionProcessor:608 resume가 재해소). coordinator 중첩 없음(액션 레벨, 스케줄러 사이클 밖). **전체 WhenDigivolving 카드군(ST1_08 등 + EX8_074 #5)이 라이브화.**
**검증:** `tests/G9-011` 2/2 — EX8_074로 디지볼브 → WhenDigivolving suspend+delete 라이브 발동 / plain 디지볼브 no-op. 전체 243/243 green, RuleAudit 위반 0(self-play 균형 유지). **미커밋.**

### (원안 스펙)
## LA-1 (원안) — 라이브 [When Digivolving] 활성화

**목표:** 디지볼브가 완료되면(트리거 emit 직후), 디지볼브한 카드의 `CardEffects(WhenDigivolving)` 활성화 효과를 `ActivatedEffectResolver`로 자동 해소. EX8_074 #5 + ST1_08/ST2_09/ST3_09 등 전체 [When Digivolving] 카드가 라이브가 됨.

**AS-IS:** 원본은 디지볼브 시 `EffectTiming.OnEnterFieldAnyone`(CanTriggerWhenDigivolving 게이트)로 [When Digivolving] 효과를 발동. 헤드리스는 `WhenDigivolving` 타이밍.

**구현 방향:** `DigivolveAction.ProcessAsync`(및 `SpecialPlayAction`, `FusionDigivolveHelpers`)에서 WhenDigivolving 이벤트 emit 후 `await ActivatedEffectResolver.ResolveAsync(context, topCardId, controller, EffectTiming.WhenDigivolving, ct)`. 인터랙티브 deferred는 `DeferredChoicePendingException` catch → `DeferredActivations.Suspend(topCardId, WhenDigivolving, controller)` + pending 반환(OptionActivate 패턴). 동기 resolver는 인라인 완결.

**검증:** 라이브 E2E — ST1_08을 디지볼브 → +3000 버프 선택 자동 발동(현재는 미발동). EX8_074를 디지볼브 → suspend+delete 발동. 동기/deferred 양쪽.

---

## LA-2 — 라이브 자기 On-Play 활성화 (선택, 해당 카드 있을 때)

**목표:** "When this card is played" 자기 활성화 효과를 PlayCardAction enter 후 자동 해소. (EX8_074엔 해당 없음 — #1은 BeforePayCost로 이미 라이브. 다른 카드용 일반화.)

**구현 방향:** LA-1과 동일 패턴을 PlayCardAction enter 직후 자기 On-Play 활성화 타이밍에 적용. 우선순위는 해당 카드 포팅 시.

---

## ✅ LA-3 — 라이브 [All Turns] (1회/턴) 재트리거 — 완료

**구현:** `OnPlayReactivation`(EndOfTurnEffectAttack/GR-006 윈도우 패턴) — 디지몬이 플레이되면(PlayCardAction.ProcessAsync, RegisterCard 직후) **양 플레이어** 배틀존에서 [All Turns] 재활성화 홀더(CardEffects(OnEnterFieldAnyone)가 `ReuseWhenDigivolvingEffect` yield, once/turn 플래그 미사용, 방금 플레이된 카드 제외)를 찾아 `ActivatedEffectResolver.ResolveAsync(holder, OnEnterFieldAnyone)`로 해소(자기 WhenDigivolving 재실행). deferred는 holder suspend + pending 반환. per-instance `allTurnsReactivationUsed` 가드 + 턴종료 시 `ClearAll`(MetadataActionProcessor EndTurn). "you may" 선택성은 재실행되는 WhenDigivolving 선택이 skippable해서 보존.
**검증:** `tests/G9-012` 3/3 — 다른 디지몬 플레이 시 EX8_074 [All Turns] 발동(suspend+delete) / 1회/턴 가드 / ClearAll 리셋. 전체 244/244 green, RuleAudit 위반 0(모든 play 영향에도 회귀 0). **미커밋.**

### (원안 스펙)
## LA-3 (원안) — 라이브 [All Turns] (1회/턴) 재트리거

**목표:** "다른 디지몬이 플레이될 때마다(1회/턴) 선택적으로" 카드의 [All Turns] 활성화 효과(예: EX8_074 #6 `ReuseWhenDigivolvingEffect`)를 offer. EX8_074 #6 라이브화.

**AS-IS:** region "All Turns" — `CanTriggerOnPermanentPlay(임의 디지몬)`, `maxCount=1`(1회/턴)+`SetHashString`, 자기 [When Digivolving] 효과 재발동.

**구현 방향:** PlayCardAction/Digivolve enter 후, 배틀존의 [All Turns] 활성화 효과 보유 카드(once/turn 플래그 미사용)에 대해 윈도우 offer(EndOfTurnEffectAttack류 패턴) → opt-in 시 `ActivatedEffectResolver.ResolveAsync(holderCardId, <AllTurns 타이밍>)`. per-instance once/turn 플래그 + 턴종료 클리어. LA-1 의존(재발동 대상이 WhenDigivolving).

**검증:** 라이브 — EX8_074 보유 중 다른 디지몬 플레이 → [All Turns] 윈도우 open → opt-in 시 #5(suspend+delete) 실행. 1회/턴 가드(2번째 플레이 미발동, 다음 턴 리셋).

---

## LA-4 — 인터랙티브 deferred resume (= EX8 brick2b 일반화)

**목표:** LA-1~3의 새 윈도우들이 인터랙티브 `DeferredChoiceProvider`에서 `DeferredChoicePendingException` 시 안전하게 suspend/resume(지불·이동 경계 포함). 동기 resolver(self-play)엔 불필요하나 인터랙티브 에이전트용.

**구현 방향:** `DeferredActivations` + `MetadataActionProcessor:608` resume 경로를 새 윈도우 타이밍으로 확장. `deferredChoice:true` 하니스(G12 패턴)로 테스트.

---

**권장 순서:** LA-1(기반, 전체 WhenDigivolving 라이브화) → LA-3(EX8_074 #6 + [All Turns] 카드군) → LA-2(해당 카드 시) → LA-4(인터랙티브).

### 진행 요약
- ✅ **LA-1** 라이브 [When Digivolving] (G9-011) — 전체 WhenDigivolving 카드군 + EX8_074 #5 라이브
- ✅ **LA-3** 라이브 [All Turns] 1회/턴 재트리거 (G9-012) — EX8_074 #6 라이브
- ⏭ **LA-2** 자기 On-Play 활성화 — **선택**(EX8_074 무관; #1은 BeforePayCost로 이미 라이브). 해당 카드 포팅 시 LA-1 패턴 적용.
- ⏭ **LA-4** 인터랙티브 deferred resume — **선택**(self-play 동기 resolver엔 불필요; 인터랙티브 에이전트용. `deferredChoice:true` 하니스로 LA-1/3 윈도우 resume 검증).

**결과: EX8_074 6 region 전부 LIVE** — #1 BeforePayCost·#3 Alliance·#4 Vortex(라이브) + #5 WhenDigivolving(LA-1)·#6 [All Turns](LA-3). self-play에서 손 안 대고 전부 자동 발동. 244/244 green, RuleAudit 0.
