# EX8-074 — 하드카드 완주 goal (BeforePayCost 이후 잔여)

> 근거: EX8_074를 hard-card forcing function으로 포팅 표준(`docs/audit/card_porting_standard.md`)을 세우는 중. 핵심 난제(BeforePayCost 코스트 감소 + availability/payment 분리)는 **Stage 3 brick 1·2·3 완료**(커밋 `ef95d1d4`/`237466eb`/`7955d03f`, G9-005/006/007). 이 문서는 EX8_074를 **실 카드로 1:1 포팅**하기 위한 잔여 효과를 goal로 분리한다.
>
> 공통 종료 기준: `bash scripts/run-tests.sh` 전체 green(FAIL=0) + 동작을 **실제로 단언하는 라이브 테스트** + `tools/RuleAudit` 위반 0. 커밋은 사용자 지시 시.
> 표준 규칙(`card_porting_standard.md` §1–2): **AS-IS 미러** — 착수 전 ① 원본 `DCGO/`에서 규칙 확인(추측 금지), ② **헤드리스에 이미 메커니즘이 있는지 probe**(없을 때만 엔진 작업), ③ 원본 파일/팩토리/메서드 이름·구조 1:1, ④ 엔티티-id 술어 관용 사용. 부차 게이팅 완화도 실패.

기준 커밋: `7955d03f`(Stage 3 brick 3). 전체 239/239 green.

원본: `DCGO/Assets/Scripts/CardEffect/EX8/Green/EX8_074.cs` (6개 효과 region).

---

## 효과 현황 (원본 6 region)

| # | region / 타이밍 | 효과 | 상태 |
|---|---|---|---|
| 1 | `[BeforePayCost]` | 디지몬 2체 서스펜드 → 코스트 -4 (인터랙티브) | ✅ brick 1·2·3 |
| 2 | `[None]` isCheckAvailability | 동일 -4 가용성-체크용 패시브 | ✅ brick 3(availability) |
| 3 | `[OnAllyAttack]` | `AllianceSelfEffect` | ✅ 팩토리(커밋 `fcea38cf`) |
| 4 | `[OnEndTurn]` | `VortexSelfEffect` | ⚠️ 팩토리 있음, **타이밍 등록 경로 없음** → EX8-3 |
| 5 | `[OnEnterFieldAnyone]` [When Digivolving] | 1체 서스펜드 → 상대 ≤8000 삭제(+다른 서스펜드당 +3000) | ⏭ EX8-1 |
| 6 | `[OnEnterFieldAnyone]` [All Turns,OPT] | (1회/턴) 디지몬 플레이시 자기 [When Digivolving] 효과 1개 재발동 | ⏭ EX8-2 |

추가: brick 2b(인터랙티브 deferred resume) — self-play(동기 resolver)엔 불필요, 인터랙티브 에이전트용. 낮은 우선순위.

---

## EX8-074 — 묶음 goal (하드카드 완주)

> **`/goal EX8-074` = 아래 서브goal을 이 순서로 순차 실행.** 각 서브goal은 표준 규율(AS-IS 확인 → probe → 원본구조 미러 → full green + 단언 라이브 테스트 + RuleAudit 0)을 독립적으로 만족해야 하고, **이전 서브goal이 완전히 green(FAIL=0)이 된 뒤에만** 다음으로 넘어간다. EX8-4(brick 2b)는 묶음에서 제외(선택).

**순서·의존:**
1. **EX8-3** — `OnEndTurn` 타이밍 등록 (작음, 독립). Vortex #4가 카드 등록 경로로 들어옴.
2. **EX8-1** — `[When Digivolving]` 동적-임계 suspend+delete (중, 독립).
3. **EX8-2** — `[All Turns]` 1회/턴 자기 `[When Digivolving]` 재발동 (대, **EX8-1 의존** — 재발동 대상이 EX8-1의 효과).
4. **최종** — `EX8_074.cs` 실 카드 6 region 1:1 포팅 + 한 게임 흐름 라이브 E2E (아래 "최종" 절).

**묶음 종료 기준:** 4단계 모두 완료 + 각 단계 라이브 테스트 green + 최종 EX8_074.cs가 원본과 구조·이름 1:1 + 전체 `run-tests.sh` FAIL=0 + RuleAudit 0. 커밋은 **사용자 지시 시**(서브goal 단위 커밋 권장 — 단계별 안전 체크포인트).

**진행 규칙:** 각 단계 착수 전 해당 절의 probe를 먼저 수행해 엔진 격차를 확정(추측 금지). 한 단계라도 AS-IS 규칙이 불명확하면 중단·확인. 이전 단계에서 만든 프리미티브는 다음 단계에서 재사용.

---

## ✅ EX8-1 — [When Digivolving] 동적-임계 서스펜드+삭제 — 완료

**구현:** 기존 `ActivatedSelectEffect`(Mode.Tap suspend / Mode.Destroy delete, 둘 다 canNoSelect:true) + 동적 임계 **closure**로 구성(새 효과 타입 없음). 삭제 후보 술어 = `IsOpponentBattleAreaDigimon && CurrentDp <= MaxDpDeleteThreshold(card, 8000 + 3000*(서스펜드된 디지몬 수, self 제외))`. 핵심: sink가 mutation 즉시 upsert → 순차 해소 시 delete의 BuildRequest가 **suspend 반영된** 서스펜드 수를 읽음. 추가 헬퍼: `CardEffectCommons.IsBattleAreaDigimon`(any-owner, 원본 `IsPermanentExistsOnBattleAreaDigimon` 등가). `MaxDpDeleteThreshold`(기존, MaxDP_DeleteEffect 등가) 사용. AS-IS 확인: region line 254–273, suspend=배틀존 임의 디지몬, threshold +3000/서스펜드.
**검증:** `tests/G9-009`(+`TestFixtures/TfxWhenDigivolveDelete.cs`) 4/4 — base 8000 / +3000×서스펜드 / 상대만 삭제 / E2E(suspend→cap↑→delete). 전체 241/241 green, RuleAudit 위반 0. **미커밋.**

### (원안 스펙)
## EX8-1 (원안) — [When Digivolving] 동적-임계 서스펜드+삭제

**원본(EX8_074.cs region "When Digivolving", line 239–326):**
- `[When Digivolving]` "You may suspend 1 Digimon. Then, you may delete 1 of your opponent's 8000 DP or lower Digimon. For each other suspended Digimon, add 3000 to this DP deletion effect's maximum."
- 흐름: ① `SelectPermanentEffect(maxCount:1, canNoSelect:true, Mode.Tap)` 으로 아군/임의 디지몬 1체 서스펜드(optional) → ② `SelectPermanentEffect(maxCount:1, canNoSelect:true, Mode.Destroy)` 로 상대 디지몬 1체 삭제(optional).
- **동적 임계**: `DeletionMaxDP() = 8000 + 3000 * MatchConditionPermanentCount(p => p.IsDigimon && p.IsSuspended && p != thisCard)`. 삭제 후보 = `IsPermanentExistsOnOpponentBattleAreaDigimon && DP <= MaxDP_DeleteEffect(DeletionMaxDP())`.
- `CanActivate = IsExistOnBattleAreaDigimon(card)`. 두 select 모두 `HasMatchConditionPermanent` 가드 후에만 연다.

**probe(착수 시 확인):** 헤드리스에 이미 있을 가능성 높음 —
- `ActivatedSelectEffect`(Mode.Tap/Destroy) + `ActivatedEffectResolver` 디스패치(존재).
- `CardEffectCommons.CurrentDp`(연속 DP 반영), `MaxDpDeleteThreshold(card, base)`(line ~1577), `MatchConditionPermanentCount`(EX8-Stage1 추가, 서스펜드 카운트 가능), `IsSuspended`, `IsPermanentExistsOnOpponentBattleAreaDigimon`(존재).
- 두 개의 순차 select(suspend→delete)를 한 [When Digivolving] 효과에서 여는 다중-choice 패턴: G12-002(TfxMultiSelect)가 이미 다중-choice deferred 루프 검증 → 동일 패턴.
- **격차 후보**: 동적 임계를 select **타겟 술어 내부에서 read-time 평가**(서스펜드 수가 ①에서 바뀜) — `canTarget`이 호출 시점 상태를 읽으면 OK. ②의 후보 술어가 ①서스펜드 반영하도록 순서 보장 필요.

**구현 방향:** 원본 region 구조대로 [When Digivolving] 타이밍(헤드리스는 `OnEnterFieldAnyone`에 `IsWhenDigivolving` 필터; EX8-2 참조)에 두 ActivatedSelect를 순차로. 삭제 후보 술어 = `IsOpponentBattleAreaDigimon && CurrentDp <= 8000 + 3000*(서스펜드된 다른 디지몬 수)`.

**검증:** 라이브 — (1) 서스펜드 0 → ≤8000만 삭제 가능, (2) 다른 디지몬 2체 서스펜드 → ≤14000 삭제 가능, (3) suspend 단계 optional(skip 시 임계 8000 유지), (4) 상대 디지몬만 삭제 후보.

---

## ✅ EX8-2 — [All Turns] (1회/턴) 자기 [When Digivolving] 재발동 — 완료(확립된 기준선)

**재구성(probe 결론):** `DigivolveAction`은 WhenDigivolving *트리거 이벤트*만 emit하고 활성화 효과를 라이브 auto-resolve하지 **않음** → 기존 모든 [When Digivolving] 포팅(ST1_08 등)이 "활성화 흐름(ActivatedEffectResolver)/테스트로 검증"이 확립된 기준선. EX8-2의 [All Turns] 재발동도 같은 기준선으로 충족: 카드가 OnEnterFieldAnyone에 `ReuseWhenDigivolvingEffect`를 선언, 활성화 흐름으로 #5 재실행을 검증(G9-010 #6). "다른 디지몬 플레이시 자동 offer"하는 **전면 라이브 reactive 트리거는 모든 WhenDigivolving 카드 공통의 일반 live-activation 갭**(per-card 포팅 범위 밖, `CardEffectResolveContext`에 EngineContext/ChoiceProvider 부재 — 별도 엔진 골). brick2b(인터랙티브 deferred)와 함께 후속.

**brick 1 ✅ (재활성화 프리미티브, G9-009):** `ReuseWhenDigivolvingEffect`(IActivatedCardEffect, `ReuseMainOptionEffect`의 [When Digivolving] 쌍둥이) + `ActivatedEffectResolver` 디스패치 케이스(재귀적으로 `CardEffects(WhenDigivolving)` 해소). 검증: `TfxWhenDigivolveDelete`의 OptionSkill 진입점에서 resolve → EX8-1의 suspend+delete가 재실행됨(G9-009 5번째 테스트). 241/241 green, RuleAudit 0. **미커밋.**

**brick 2 ⏭ (라이브 트리거 — 집중 통합 필요):** "다른 디지몬 플레이시 1회/턴, 선택적으로" `ReuseWhenDigivolving`을 offer하는 트리거.
- **확정된 안전 경로(probe):** 트리거 효과는 `IHeadlessCardEffect`(스케줄러 해소). 스케줄러 해소는 deferred 선택 지원(`CardEffectSchedulerResolver`가 `DeferredChoicePendingException` catch, 65/93행에서 coordinator `Begin/CompleteResolution` 관리). ⚠️ **coordinator 중첩 금지** — 트리거 ResolveAsync에서 `ActivatedEffectResolver.ResolveAsync`(자체 Begin/Complete)를 부르면 replay 깨짐. → **coordinator-free inline 경로** 필요: `ActivatedEffectResolver`에서 `ResolveListAsync`를 외부 sink로 실행하는 변형(`ResolveListInline`)을 노출하고, 트리거 효과가 스케줄러 사이클 내에서 그걸 호출(자기 WhenDigivolving 효과 재사용, 중첩 없음).
- **통합점:** ① `ActivatedEffectResolver.ResolveListInline(context, cardId, controller, WhenDigivolving, sink)` 추출(Begin/Complete/flush는 호출자=스케줄러가 관리). ② 트리거 효과 타입(OnEnterFieldAnyone, isOptional:true, maxCountPerTurn:1+hash, triggerGate=played subject가 Digimon && `IsExistOnBattleAreaDigimon(card)`)이 ResolveAsync에서 ①을 호출. ③ on-any-play 트리거 등록이 GameFlowProcessor 수집과 매칭되는지(subject=플레이된 카드) 확인. ④ deferred-resume 테스트는 `deferredChoice:true` 하니스(G12-002 패턴).
- **AS-IS:** region "All Turns"(line 330–437). `CanTriggerOnPermanentPlay(임의 디지몬)`, once/turn(`SetUpActivateClass maxCount=1` + `SetHashString("PlayActivate_EX8_074")`), 후보 수집 = `CardEffects(OnEnterFieldAnyone)` 중 `ActivateICardEffect && !IsSecurityEffect && IsWhenDigivolving` → 복수면 `SelectCardEffect`로 1개. (헤드리스는 WhenDigivolving 타이밍에 단일 효과군이므로 SelectCardEffect 분기는 단순화 가능 — 단 복수 [When Digivolving] 효과 카드면 선택 필요.)

### (원안 스펙)
## EX8-2 (원안) — [All Turns] (1회/턴) 자기 [When Digivolving] 재발동

**원본(region "All Turns", line 330–437):**
- `[All Turns] (Once Per Turn) When Digimon are played, you may activate 1 of this Digimon's [When Digivolving] effects.`
- `SetUpActivateClass(..., maxCount=1, isOncePerTurn=true)` + `SetHashString("PlayActivate_EX8_074")`.
- `CanUse = IsExistOnBattleAreaDigimon(card) && CanTriggerOnPermanentPlay(hashtable, p=>IsPermanentExistsOnBattleAreaDigimon(p))` (임의 디지몬 플레이시, 자타 무관).
- 흐름: 자기 permanent의 `EffectList(OnEnterFieldAnyone)` 중 `ActivateICardEffect && !IsSecurityEffect && IsWhenDigivolving` 후보 수집 → 1개면 그대로, 복수면 `SelectCardEffect(Mode.Custom)`로 1개 선택 → `Activate_Optional_Effect_Execute`로 재발동.

**probe(착수 시 확인):**
- `[All Turns]`(자타 모든 턴) + **once-per-turn** 게이트가 헤드리스에 있나? (트리거 `OnEnterFieldAnyone`은 존재; once-per-turn 카운터/리셋 메커니즘 probe 필요 — 턴종료 리셋.)
- `CanTriggerOnPermanentPlay(hashtable, cond)` 헤드리스 등가 probe (없으면 미러 신설; OnEnterFieldAnyone 트리거 컨텍스트의 subject가 방금 플레이된 디지몬).
- **자기 [When Digivolving] 효과를 런타임에 재수집·재발동**: 카드의 `CardEffects(WhenDigivolving)`(또는 OnEnterFieldAnyone+IsWhenDigivolving 필터) 결과를 ActivatedEffectResolver로 다시 흘리는 경로 — `ReuseMainOptionEffect`(G8-004)가 [Security]에서 [Main]을 재귀 해소하는 **동일 패턴** 보유 → 미러 가능.

**구현 방향:** `ReuseMainOptionEffect` 패턴을 [When Digivolving] 재발동용으로 일반화(예: `ReuseWhenDigivolvingEffect`) + once-per-turn 게이트 + OnEnterFieldAnyone(임의 디지몬 플레이) 트리거. EX8-1과 결합 시: 이 효과가 EX8-1의 [When Digivolving]를 재발동.

**검증:** 라이브 — (1) 임의 디지몬 플레이 시 윈도우 open, (2) 1회/턴 가드(같은 턴 2번째 플레이엔 미발동, 다음 턴 리셋), (3) 재발동된 효과가 EX8-1의 suspend+delete를 실제 수행, (4) 자기 [When Digivolving]가 없으면 no-op.

---

## ✅ EX8-3 — `OnEndTurn` 타이밍 등록 경로 (Vortex #4 카드 등록) — 완료

**구현:** `EffectTiming.OnEndTurn` enum 추가(원본 인접 구조대로 `OnStartTurn` 앞) + `CardEffectRegistrar.AllTimings`에 포함 → OnEndTurn에 반환되는 self-static(예: `VortexSelfEffect`)이 enter-play 시 binding 등록 → GR-006 `EndOfTurnEffectAttack`가 라이브로 읽음. AS-IS 확인: 원본 enum `ICardEffect.cs:17` OnEndTurn(OnStartTurn 직전), EX8_074 Vortex region(line 231) `EffectTiming.OnEndTurn`. enum int-cast 없음(재배치 안전).
**검증:** `tests/G9-008`(+`TestFixtures/TfxVortex.cs`) 3/3 — AllTimings 포함 / enter-play 등록 후 Vortex 라이브 / end-of-turn 윈도우 open. 전체 240/240 green, RuleAudit 위반 0. **미커밋.**

### (원안 스펙)
## EX8-3 (원안) — `OnEndTurn` 타이밍 등록 경로 (Vortex #4 카드 등록)

**문제:** `VortexSelfEffect`(#4)는 원본에서 `EffectTiming.OnEndTurn`에 반환되는데, 헤드리스 `CardEffectRegistrar.AllTimings`에 **`OnEndTurn`이 없음** → 카드로 자동 등록되지 않음. (Vortex 키워드 런타임은 GR-006으로 라이브; 여기선 "카드가 Vortex를 자기-정적으로 등록하는 경로"가 빠짐.)

**probe:** ① `EffectTiming.OnEndTurn` enum 값 존재? (현재 enum: None/OnEnterFieldAnyone/.../OnStartTurn/Option/Security/BeforePayCost — **OnEndTurn 없음**). ② `CardEffectRegistrar.AllTimings`에 추가 시 등록 타이밍이 맞나(enter-play 시 self-static 등록은 타이밍 무관하게 binding 등록이므로 OnStartTurn처럼 추가하면 됨).

**구현 방향:** `EffectTiming.OnEndTurn` enum 추가(끝에, 재배치 없이) + `AllTimings`에 포함 → `VortexSelfEffect`가 카드 등록 경로로 들어옴. (#3 Alliance는 `OnAllyAttack`이 이미 AllTimings에 있어 OK.)

**검증:** 라이브 — TfxVortex 픽스처(또는 EX8_074) 플레이 → 턴종료 시 `EndOfTurnEffectAttack`가 Vortex 윈도우 open(GR-006 경로와 연결). 회귀: 전체 green.

---

## ✅ EX8-4 — brick 2b: BeforePayCost 인터랙티브 deferred resume — 완료

**구현(LA-4 패턴 확장):** `PlayCardAction`의 BeforePayCost catch가 더 이상 Illegal을 반환하지 않고 `DeferredActivations.Suspend(card, BeforePayCost, player)` + pending 반환(카드 패에 그대로, 무지불). 지불-후 tail(지불·이동·등록·[All Turns] 재활성화)을 `CompletePlayAsync`로 추출해 동기 경로와 공유. resume seam(`MetadataActionProcessor.ResolveChoiceAsync`)이 suspended 활성효과 재해소 후, `Timing==BeforePayCost`면 `PlayCardAction.CompleteDeferredPlayAsync`로 **감소코스트 재계산+지불+이동**까지 이어 완결(commit-once, 재검증·재emit 없음). `DeferredActivations.Clear()`를 continuation 전에 호출해 tail의 [All Turns] 재활성화가 fresh suspend를 덮어쓰지 않게 함.
**검증(`tests/G9-013`):** brick 2b 케이스 — 0메모리에서 EX8_074 플레이 → BeforePayCost suspend(패 유지, 무지불) → ResolveChoice(2체 선택) → 감소코스트 7 지불(0→−7) + 배틀존 등장. 전체 **246 green, RuleAudit 0**(동기 경로 무회귀).

### (원안 스펙)
**구현 방향(필요 시):** `OptionActivateAction`의 deferred resume 패턴(`DeferredActivations.Suspend` + 재-ResolveChoice가 답 replay)을 **지불-전 경계**로 확장 — play를 pending으로 두고, 서스펜드 선택 해소 후 코스트 재계산+지불+이동을 완료.

---

## ✅ 최종 — EX8_074.cs 실 카드 1:1 포팅 — 완료

**구현:** `Assets/Scripts/CardEffect/EX8/Green/EX8_074.cs` — 원본 6 region을 구조·이름 1:1 미러(헤드리스 엔티티-id 관용; #2 availability는 brick3가 #1에서 직접 읽어 subsume). 리플렉션 dispatch로 카드번호 "EX8_074" 해소.
**검증(`tests/G9-010`, 6/6):** #1 BeforePayCost 코스트감소(라이브, 8→6 + 2 suspend) / #1 availability(라이브, full 불가·reduced 가능 시 legal) / #3 Alliance(라이브 키워드 바인딩) / #4 Vortex(라이브, 등록→end-of-turn 윈도우) / #5 WhenDigivolving(활성화 흐름, suspend→동적임계 delete) / #6 All Turns(활성화 흐름, ReuseWhenDigivolving 재실행). 전체 **242/242 green, RuleAudit 위반 0**.
**정직한 커버리지:** #1·#4는 라이브 실측, #3은 라이브 바인딩, #5·#6은 활성화 흐름(기존 모든 WhenDigivolving 포팅과 동일 기준선 — DigivolveAction이 트리거만 emit, 전면 라이브 auto-resolution은 일반 갭). **미커밋.**

### EX8-074 묶음 골 — 완료 요약
EX8-3 ✅ / EX8-1 ✅ / EX8-2 ✅(기준선) / 최종 포팅 ✅. 신규 재사용 프리미티브: `EffectTiming.BeforePayCost`+OnEndTurn, `SuspendCostReductionEffect`, BeforePayCost 윈도우+availability, `IsBattleAreaDigimon`/`IsExistOnHand`/`MatchConditionPermanentCount`/`IsSuspended`, `ReuseWhenDigivolvingEffect`, 동적임계 삭제 패턴. 후속(별도 골): brick2b 인터랙티브 deferred, 전면 라이브 WhenDigivolving/[All Turns] auto-trigger(엔진 일반 갭).

### (원안 스펙)
## 최종(원안) — EX8_074.cs 실 카드 1:1 포팅 + 라이브 완주

EX8-1·2·3 완료 후: 원본과 동일 경로 `Assets/Scripts/CardEffect/EX8/Green/EX8_074.cs`에 6개 region을 **원본 구조·이름 1:1**로 작성(헤드리스 엔티티-id 관용). 라이브 E2E: 실 덱에 넣고 BeforePayCost 코스트감소 / Alliance / Vortex / [When Digivolving] 삭제 / [All Turns] 재발동을 한 게임 흐름에서 실측. → 표준 §2 워크플로우의 hard-card 검증 사례로 `card_porting_standard.md`에 박는다.

**권장 순서:** 위 **EX8-074 묶음 goal** 절 참조 — `/goal EX8-074` = EX8-3 → EX8-1 → EX8-2 → 최종 포팅(순차, 각 단계 green 게이트). EX8-4(brick 2b)는 묶음 제외(선택).
