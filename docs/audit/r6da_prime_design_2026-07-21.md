# R6-Da′ 설계 — activated corpus 회계 모델 화해 (2026-07-21)

Base: `791e9207`(main). **설계 전용 — 코드 무수정.** 경로=`src/HeadlessDCGO.Engine/` 상대(별도 표기 없으면).
정본 재료: `registry_teardown_investigation_2026-07-16.md §H`(1라운드 마감) · `registry_probe_census_2026-07-20.md`(좌표) ·
커밋 `7fcda8dd`(수리-3c, CEntityUseCycle 구축).

---

## §0. 핵심 판정 (설계의 중심 질문에 대한 답)

**1라운드 재스코프 판정은 CEntityUseCycle 부재 상태의 것이다.** §H①은 "발명 `ActivatedEffect`의 cap-파티션/환불/
executed 의미론을 AS-IS `ActivateClass`가 미표현 → 단순 flip = 회계 소실"이라 했다. 그 판정 당시(2026-07-16)엔
포팅-카드 [Once Per Turn] 캡을 AS-IS-형으로 담을 resume-저널이 없었다. **수리-3c(7fcda8dd, 2026-07-20)가
`CEntityUseCycle`을 `CEntity_EffectController`에 구축**하면서 상황이 뒤집혔다:

- AS-IS `ActivateClass`(ActivateICardEffect)의 캡 회계 = `CEntity_EffectController.UseEffectsThisTurn` +
  `isOverMaxCountPerTurn(this, MaxCountPerTurn)`(ICardEffect.cs:405/433) + `RegisterUseEffectThisTurn`/
  `RemoveUseEffectThisTurn`(:1201/1211 AddUse/RemoveUse) + `IsSameEffect`/`HashString` 파티션(:981-1030).
- 이 AS-IS 캡 경로는 **이미 신모델 경로에서 load-bearing**이다 — resolver의 `case ActivateICardEffect`가
  `ce.EffectSourceCard.cEntity_EffectController.RegisterUseEffectThisTurn(ce)`(ActivatedEffectResolver.cs:611/618)로
  등록하고, 그 재검을 suspend/resume-안전하게 만드는 `CEntityUseCycle`이 `ResolveWithinCycleAsync`(:513)에서
  `OnceFlags`와 **lockstep으로 구동**된다.

**결론: 발명 corpus의 회계 4종(cap·환불·executed·resume)은 전부 AS-IS 기제로 전단사(bijection) 표현 가능하다.**
따라서 R6-Da′는 "창 컷오버 동형 재설계 골"이 아니라 **"병렬 발명 캡 기제(uniform `ActivatedEffect` +
`OnceFlagController`의 캡-절반)를 AS-IS `ActivateClass`+`CEntity_EffectController` 경로로 flip하는 청산 골"**이다.
회계 소실 위험은 flip의 **파티션 충실도**(OnceFlags 문자열-키 ↔ AS-IS `IsSameEffect`)에만 국한되며, 발명 캡의
`capHash` 주석(ActivatedEffect.cs:611-621)이 이미 그 파티션을 AS-IS `IsSameEffect`/`SetHashString`과 **동형으로
설계**해 두어 digest-중립 flip이 구조적으로 가능하다.

단, **`OnceFlagController`는 깨끗이 은퇴하지 않는다**(§3d): 그 3책무 중 캡-파티션만 발명이고, resolution-cycle
transaction + sink mutation-replay journal은 **전 resolver 경로 공유 substrate**다. 이 분리가 본 골의 최대 난점.

---

## §1. 발명 corpus 회계 의미론 전수표

각 의미론이 표현하는 것 + 라인 앵커. (좌표: census §2/§3, `IEffectBody`69·`IActivatedCardEffect`56·producer 8.)

| # | 회계 의미론 | 발명 좌석(앵커) | 무엇을 표현하나 |
|---|---|---|---|
| S1 | **cap 파티션** | `ActivatedEffect` ctor `EffectId` (ActivatedEffect.cs:618-621): `{card}:ae` 또는 `{card}:ae:{capHash}` | 턴당 사용횟수 카운트의 키. capHash 무 → 같은 소스카드 전 효과 1카운트 공유(timing/body-blind); capHash 유 → 분리 파티션 |
| S2 | **cap 게이트** | `OnceFlags.CanActivate(request, MaxCountPerTurn)` (OnceFlagController.cs:261-271); resolver 소비 :200/264/327/976 | `GetUseCount(key) + CycleExtra < max`. maxCount null → 항상 통과 |
| S3 | **cap 소비(register-before-body)** | `OnceFlags.Consume` (:277-314); resolver :996(declarative)·:1008(standard) | 본체 전 use 1 등록. AS-IS register-before-body 순서 |
| S4 | **비용 환불(RemoveUse)** | `ActivatedEffect.RefundWhenNotExecuted`(:657) + `OnceFlags.Refund`(:320-345); resolver :1017-1020 | per-card opt-in: 본체가 아무것도 안 하면 use 반환(AS-IS `if(!executed) RemoveUse()`, ~38장) |
| S5 | **executed 상태** | `ActivatedEffect.ExecutedPredicate`(:663) + `ResolveBodyAsync` 반환(:696-726) | 본체 실행 여부 판정. 기본 "선택 skip 아님", 카드별 composite 재정의(AD1_024 3-branch OR, BT14_029 board pred) |
| S6 | **resume 저널(캡)** | `OnceFlagController` uniform-cycle: `BeginUniformCycle`/`Suspend`/`Complete`/`Abort`(:73-202) + `_pending`/`_replayCursor` | deferred-choice REPLAY-resume 시 staged consume 재생·중복등록/자기-capped-out 방지 |
| S7 | **resume 저널(sink mutation)** | `BeginMutationApply`/`RecordFreshMutation`(:114-142) + `_mutationJournal` | suspend 가로질러 immediate mutation(memory/DP/flag) 중복적용 방지. **sink가 소비**(MatchStateMutationSink.cs:492) |
| S8 | **body 조립(composable)** | `IEffectBody` 15종(ActivatedEffect.cs:36-575: Draw/Memory/Select/Composite/…) | AS-IS `ActivateCoroutine`의 조립형 미러(주석 명시) |
| S9 | **granted-continuous 등록** | `GrantContinuousBody`(:311-341)·`GrantPlayerScopeRestrictionBody`(:351-370) + ActivatedEffects.cs 6 producer(:779/889/983/1061/2456/2573) | activated 해소 시 continuous/restriction/buff/cost-mod을 `EffectRegistry.Register(EffectBinding)`로 등록 |
| S10 | **specialized dispatch** | resolver `ResolveListAsync` switch 56 subtype(:560-1160) | `IActivatedCardEffect` 하위형별 자기-구동 해소(DigiBurst/RevealMulti/DnaFromHandOrTrash/…) |

---

## §2. AS-IS 정본 대응표

| # | AS-IS 기제 | 좌석 | 판정 |
|---|---|---|---|
| S1 | `IsSameEffect` 파티션(소스카드 + `HashString`) | ICardEffect.cs:981-1030; `SetHashString`(:260) | **번역** — `{card}:ae`=빈 HashString, `{card}:ae:{capHash}`=SetHashString. 전단사(발명 주석이 이미 이 동형을 명기) |
| S2 | `isOverMaxCountPerTurn(this, MaxCountPerTurn)` | CEntity_EffectController.cs:285-288; CanTrigger:405·CanActivate:433 | **번역** — 신모델 경로 이미 사용 중 |
| S3 | `RegisterUseEffectThisTurn`(AddUse) | CEntity_EffectController.cs:295-306; ICardEffect.cs:1211 | **번역** — resolver 신모델 case 이미 호출(:611/618); AS-IS 등록점=`SetOnProcessCallbuck`(:616)/declarative(TurnStateMachine:1183) |
| S4 | `RemoveUseEffectThisTurn`(RemoveUse) | CEntity_EffectController.cs:313-323; ICardEffect.cs:1201 | **번역** — 카드 `ActivateCoroutine` 안의 `if(!executed) RemoveUse()`로 이동 |
| S5 | 카드-정의 `executed` composite | 각 카드 `ActivateCoroutine` 본문 | **번역** — 발명은 이를 델리게이트로 외부화; AS-IS는 코루틴 내부. flip = 카드 재-포팅 시 코루틴 안으로 흡수 |
| S6 | (AS-IS 부재 — 단일 코루틴은 mid-body suspend, 재검 없음) | — | **substrate-필수(비-발명)** — `CEntityUseCycle`이 이미 이 역할(수리-3c). AS-IS엔 없으나 headless resume 모델의 번역-필수 |
| S7 | (AS-IS 부재 — 동상) | — | **substrate-필수(공유)** — 전 resolver 경로가 소비. 은퇴 아님(§3d) |
| S8 | `ActivateClass._activateCoroutine`(카드 공급 클로저) | CardEffects/ActivateClass.cs:31-52 | **번역** — 조립형 body → 카드별 `Func<Hashtable,Task>` 코루틴. flip=재-포팅 |
| S9 | `AddEffectToPermanent`/`AddEffectToPlayer` 지속-버킷 | GiveEffectToPermanentOrPlayer.cs:28/101; surface=`Permanent.EffectList_Added`/`Player.EffectList` | **번역** — AS-IS 라이브 grant store 실재. **registry 브릿지는 이미 inert**(신모델 ActivateClass는 ToBinding 무 → 버킷 경로 단독, 헤더 주석 명기). RD-P6C3-C1 RESOLVED |
| S10 | `ActivateClass` 단일 kind + 카드 코루틴 (AS-IS는 subtype 폭발 없음) | ActivateClass.cs; AutoProcessing 수집 filter `is ActivateICardEffect` | **발명(은퇴)** — 56-subtype switch는 조합폭발 발명. flip 후 resolver의 `case ActivateICardEffect activate` 단일 case(:564)로 붕괴 |

> **AS-IS 부재 확증(dead-judgment-needs-AS-IS)**: census §3 각주대로 `IEffectBody`·`EffectBinding`·
> `IHeadlessCardEffect`·`IActivatedCardEffect` 4종 전부 `DCGO/` 원본 grep(`--binary-files=text`) hit 0 = 재구축 발명물.
> 재하우징 타깃 AS-IS 기제는 실재: `ActivateClass`·`CEntity_EffectController`·`AddEffectToPermanent`·
> `EffectList` live-scan.

---

## §3. 화해 설계

### 3a. cap flip — OnceFlags 캡-절반 → CEntity_EffectController (S1~S5)

**목표**: uniform `ActivatedEffect`를 AS-IS `ActivateClass`로 flip. 그 즉시 캡 회계가 OnceFlags(문자열-키
`OnceFlagState`)에서 CEntity(`UseEffectsThisTurn` + `IsSameEffect`)로 이관된다 — **신모델 카드가 이미 밟는 경로**이므로
새 substrate 불요.

- **S1 파티션 전단사**: 각 uniform `ActivatedEffect`의 `capHash` → 그 카드 `ActivateClass.SetHashString(capHash ?? "")`.
  capHash 무 = 빈 HashString = 소스카드 1카운트(AS-IS 기본). 이것이 **digest-중립의 열쇠**(§6): 파티션이 어긋나면
  캡 결정이 바뀌어 RLB 다이제스트가 깨진다.
- **S3 등록점**: resolver 신모델 case가 이미 declarative(TurnStateMachine:1183 등가, :609-613)와 standard
  (`SetOnProcessCallbuck`, :616-620) 두 등록점을 미러. uniform→ActivateClass flip이면 이 코드가 그대로 캡을 집행.
- **S4 환불**: `RefundWhenNotExecuted=true`였던 카드(~38장)는 코루틴 말미에 `if(!executed) this.RemoveUse()` 추가.
- **S5 executed**: `ExecutedPredicate` 보유 카드는 그 술어를 코루틴 안 `executed` 지역변수 계산으로 인라인.
- **S6 resume(캡)**: `CEntityUseCycle`이 이미 담당(수리-3c). flip 후 OnceFlags 캡-cycle은 무소비 → 정리 대상(§3d).

### 3b. granted-continuous 6 rehome — EffectRegistry → AS-IS 지속-버킷 (S9)

6 producer(census §2)를 **발명 `EffectRegistry.Register(EffectBinding)` → AS-IS `AddEffectToPermanent`/
`AddEffectToPlayer` 지속-버킷**으로 재하우징. 대상 store는 **이미 존재**(GiveEffectToPermanentOrPlayer.cs, 신모델
버킷 경로 단독·registry 브릿지 inert). 각 producer가 등록하는 continuous를 신모델 `ActivateClass`/kind-class continuous로
바꿔 버킷에 넣으면, 라이브 게이트가 `NewModelContinuousScan`/`Continuous*Gate` union으로 읽는다(W3c 시리즈가 이미 이
union 소비자 절반을 건설).

| producer | 좌석 | 등록물 | rehome 타깃 |
|---|---|---|---|
| `BeforePayCostReductionEffect` | :779 | before-pay cost-mod continuous | player/permanent 버킷 cost-mod |
| `SuspendCostReductionEffect` | :889 | cost-reduction continuous | 동상 |
| `ActivatedTargetBuffEffect` | :983 | DP/SA buff continuous(target) | `AddEffectToPermanent` buff |
| `ActivatedPlayerScopeBuffEffect` | :1061 | player-scope buff continuous | `AddEffectToPlayer` buff |
| `ActivatedTargetRestrictionEffect` | :2456 | can't-attack/block restriction(target) | `AddEffectToPermanent` restriction |
| `PlaySelfAtEndOfBattleSecurityEffect` | :2573 | **트리거**(PlaySelfAtEndOfBattleTriggerEffect=RD-P6C3-B2) | **동승 재판정** — 이건 continuous 아닌 지연-트리거. R6-Db PlaySelfAtEndOfBattle 재판정과 결합 |

또한 body 두 개(`GrantContinuousBody`:339·`GrantPlayerScopeRestrictionBody`:368)는 flip 시 카드 코루틴이 직접
`AddEffectToPermanent`/`Gain*PlayerEffect`(라이브 self-register)를 호출하는 형태로 흡수 — GrantContinuousBody의
STOP(RD-P6C3-C1)은 버킷 경로가 열려 이미 RESOLVED.

**RD-P6B-6 동승 지점(DigiBurst grant)**: resolver DigiBurst case(:691-707)의 `if (burst.InnerEffect is
IActivatedCardEffect or ActivateICardEffect) resolve now / else register`는 keyword self-static grant(예
`PierceSelfEffect`, `ActivateClass` 구현)를 "즉시 활성"으로 오배선(no-op 코루틴 실행). flip이 이 case 자체를 없앤다
(§3c) — DigiBurst의 continuous-inner는 버킷 grant로, activated-inner는 카드 코루틴으로 각각 명시 분기. **hasReboot
carrier 동승**: sink `KindToFlag` metadata carrier(MatchStateMutationSink.cs:251-253)의 정본 이동(원장 잔여 #5)은
이 keyword-grant 재하우징과 같은 파일권에서 처리(라이브 Reboot은 `Permanent.HasReboot`로 이미 재하우징 완료, 잔여만).

### 3c. resolver switch 붕괴 (S10)

56-subtype switch(ActivatedEffectResolver.cs:560-1160)는 발명 dispatch. 각 subtype이 AS-IS `ActivateClass` +
카드 코루틴으로 flip되면 그 case가 하나씩 소멸하고, 최종적으로 `case ActivateICardEffect activate`(:564, AS-IS
`AutoProcessing.ActivateEffectProcess` 미러) **단일 case로 붕괴**. self-driving 특수 case(RevealMulti·DnaFromHandOrTrash
등)는 그 `ResolveAsync(sink,…)` 본문을 카드 코루틴으로 옮긴다(코루틴이 ChoiceProvider를 직접 구동하는 것은
AS-IS와 동형). resolver 파일 자체는 최종 W3c-final에서 얇아진 dispatch만 남거나 창-루프에 흡수.

### 3d. `OnceFlagController` 부분-은퇴 (S6·S7 — 본 골 최대 난점)

**`OnceFlagController`는 3책무를 겸한다** — 발명 판정이 갈린다:

1. **캡-파티션**(`OnceFlagState`·`CanActivate`/`Consume`/`Refund`/`ForRequest`·`ResetForTurn`/`ResetForCard`) —
   uniform `ActivatedEffect` 전용. flip 후 무소비 → **은퇴**. 단 `ResetForCard`(AS-IS `CardSource.Init` 캡 리셋)의
   호출부(fusion/digivolve: MindLink·Permanent:3927/4176·DNA)는 CEntity 캡 리셋으로 이관 필요 —
   `CEntity_EffectControllerStore.ResetUseCountsForTurn`은 턴-단위만 있고 **per-card 리셋 미구축**(신설 필요, §5 D3).
2. **resolution-cycle transaction**(`BeginUniformCycle`/`Suspend`/`Complete`/`Abort`) — `ResolveWithinCycleAsync`가
   `CEntityUseCycle`과 **lockstep 구동**(:508↔513). **전 resolver 경로 공유**(신모델 포함). `CEntityUseCycle`과
   기능 중복 → **통합 대상**(은퇴 아님).
3. **sink mutation-replay journal**(`BeginMutationApply`/`RecordFreshMutation`/`_mutationJournal`) —
   `MatchStateMutationSink.cs:492`·`CardController.cs:630`가 소비, **모델 무관 전 해소 공유**. `CEntityUseCycle`엔
   이 저널이 **없다**. → **존치(재하우징)** 필수.

즉 `OnceFlagController` 파일 삭제 = **불가**. 캡-파티션만 도려내고 (2)(3)은 substrate로 남긴다. 권고: (2)(3)을
`CEntityUseCycle`(또는 신규 substrate `ResolutionCycle`)로 흡수해 OnceFlagController를 완전 소멸시키는 통합.
이것이 §5 결정지점 D1.

### flip 순서 (소비자→생산자, P0-1 교훈)

1라운드 P0-1(생산자-선행 flip이 registry-단독 소비자를 사문화 → 행동 소실, 직독-witness가 미검출)의 교훈:
**소비자 재하우징 ↔ producer 청산을 원자로 묶고, 소비 경로를 실구동 witness로 검증**. R6-Da′ 순서:

1. cap 소비자(resolver 게이트 6좌석) 먼저 CEntity 경로로 배선 검증(신모델 case 이미 존재) → 카드 flip.
2. granted-continuous: 버킷 소비자(union 게이트) 실배선 확인 → producer 6 rehome을 원자로.
3. resolver switch는 subtype이 0 될 때만 case 삭제(중간 상태에서 절대 소비자-먼저 삭제 금지).

---

## §4. 배치 계획 (A3 실행 순서)

census §6 위상 안에서의 R6-Da′ 내부 트랜치. **R6-Da′는 registry 물리삭제(W3c-final)의 하드 선행**이며 W3c
재하우징과 파일 서로소(R6-Da′=ActivatedEffect(s)/factory activated-half, W3c=Commons/게이트/Expiry)로 **병렬 가능**.

| 트랜치 | 내용 | 파일권 | 게이트 |
|---|---|---|---|
| **Da′-0 배선검증** | resolver 캡 6좌석이 CEntity 경로 등가임을 계측 확증(신모델 case 이미 밟음). OnceFlags 캡-cycle과 CEntity-cycle 이중구동을 witness로 대조 | resolver(무flip, 계측) | 이중구동 결과 동일 |
| **Da′-1 factory flip** | `CardEffectFactory` uniform 생산 메서드(:41 `new ActivatedEffect`) → `new ActivateClass`+`SetHashString(capHash)`. 인쇄 카드 en-masse 전환 | CardEffectFactory.cs | 대표 witness activated 3종(capped·refund·executed 각 1) |
| **Da′-2 직접-new 11장** | `new ActivatedEffect(` 직접 카드 11장 각자 re-port(코루틴화·executed 인라인·refund 말미) | CardEffect/*/*.cs | 카드별 행동 witness |
| **Da′-3 granted-continuous 6** | producer 6 → 버킷 rehome(§3b). :2573은 R6-Db PlaySelfAtEndOfBattle과 결합 | ActivatedEffects.cs + GiveEffect | union 게이트 실구동 witness(buff/restriction/cost 각) |
| **Da′-4 RD-P6B-6 + DigiBurst** | resolver DigiBurst case 분기 명시(continuous→버킷/activated→코루틴), hasReboot carrier 정본이동 동승 | resolver + MatchStateMutationSink | PRIM.DigiBurst red→green |
| **Da′-5 switch 붕괴** | subtype 0 확인 후 56-case → 단일 `ActivateICardEffect` case. 특수 self-driving case를 코루틴 이관 | resolver | 회귀 전량 |
| **Da′-6 OnceFlags 캡-도려내기** | 캡-파티션 소멸 + (2)(3) 통합/존치 결정 집행(§5 D1) + per-card 리셋 신설(§5 D3) | OnceFlagController + CEntityUseCycle | 적대리뷰 필수(공유 substrate 손상 위험) |

이후 **corpus 삭제(R6-Db 동승)**: `IEffectBody`69·`IActivatedCardEffect`56·`ToBinding`96·corpus `EffectBinding`
74좌석 소멸(census §6-3). 인라인 특수플레이 마커 5·Tfx 18 은퇴. **W3c-final registry 물리삭제**는 producer 0 도달
후(R6-Da′ + W3c 재하우징 합류).

---

## §5. 사용자 결정 지점 (옵션·권고)

### D1. `OnceFlagController`의 공유 substrate(cycle+mutation journal) 처리
발명 캡-파티션 은퇴 후 남는 (2)resolution-cycle·(3)mutation-journal의 거처.
- **옵션 A(통합)**: (2)(3)을 `CEntityUseCycle`에 흡수(mutation journal 필드 추가), OnceFlagController 파일 완전 소멸.
  cycle 이중구동(:508↔513)이 단일화되어 코드 감축·개념 1개.
- **옵션 B(존치·개명)**: 캡-파티션만 삭제, (2)(3)을 substrate `ResolutionCycle`로 개명 존치. CEntityUseCycle과
  2-cycle 병존 유지(현행 구조 최소변경).
- **권고: A** — 이중 cycle의 lockstep 구동은 순수 중복이고, mutation journal은 sink 1좌석·CardController 1좌석만
  소비하므로 이관 표면이 작다. 단 A는 mutation-journal 이관이 digest에 닿으므로 shadow-run N판 강화 필수(§6).

### D2. granted-continuous 저장 모델(6 producer)
- **옵션 A(AS-IS 버킷 단독)**: 전부 `AddEffectToPermanent`/`AddEffectToPlayer` duration-bucket으로. AS-IS 1:1,
  registry 브릿지 완전 이탈. buff/restriction/cost-mod을 신모델 continuous kind-class로 표현.
- **옵션 B(permanent-grant store 신설)**: 별도 신규 저장소 신설(census §4b 표현 "A2 player-bucket 만료 모델 필요").
- **권고: A** — AS-IS 라이브 store가 실재하고 registry 브릿지가 이미 inert(GiveEffect 헤더 명기)이므로 신설은
  발명 재도입. 단 **만료 모델**(EffectDurationExpiry registry sweep → 버킷 만료)은 W3c-2 소관이라 R6-Da′와 **합류
  타이밍 확인 필요**(buff에 duration이 실리므로 버킷 만료 경로가 W3c-2에서 먼저 서 있어야 Da′-3가 닫힌다).

### D3. per-card 캡 리셋 신설(ResetForCard 이관)
`OnceFlags.ResetForCard`(AS-IS `CardSource.Init` 캡 리셋; fusion/digivolve/re-stack 호출부 4곳)의 CEntity 대응.
- **옵션 A**: `CEntity_EffectControllerStore.ResetUseCountForCard(context, instanceId)` 신설(턴-리셋 :362의 카드-판).
- **옵션 B**: fusion helper가 CEntity 컨트롤러 직접 `InitUseCountThisTurn()` 호출.
- **권고: A** — 턴-리셋과 대칭·store 캡슐화 유지. AS-IS `CardSource.Init`의 단일-카드 리셋과 1:1.

### D4. :2573 PlaySelfAtEndOfBattle(RD-P6C3-B2) 결합
granted-continuous 6 중 유일 트리거-등록. R6-Db의 PlaySelfAtEndOfBattle 재판정과 동승(§H §D)이 확정 방침.
- **결정 필요**: Da′-3에서 함께 rehome할지, R6-Db로 완전 이월할지. **권고: R6-Db 이월**(continuous 아닌 지연-트리거라
  버킷 모델과 별개 경로; corpus 삭제 시점에 특수플레이 마커 5와 묶는 편이 원자적).

### D5. flip 단위(카드 witness 선정)
[witness-selection-card-level] 규약상 트랜치 착수 전 카드 리스트 제시·카드 단위 사용자 선정 강제.
- **결정 필요**: Da′-1(factory) 대표 3종 + Da′-2(직접-new 11장)의 witness 카드 명단을 사용자가 선정. 특히 refund
  카드(~38장 중)와 executed-composite 카드(AD1_024·BT14_029)는 적대 선정 권고(회계 3의미론이 동시에 걸리는 표본).

---

## §6. 리스크

| 리스크 | 성격 | 완화 |
|---|---|---|
| **R1. cap 파티션 어긋남** | S1 flip에서 `capHash`↔`HashString` 매핑 오류 → 캡 결정 변동 → **RLB 다이제스트 붕괴** | 발명 주석이 이미 동형 명기(ActivatedEffect.cs:611-621). 파티션 전수 대조표 + shadow-run bit-identical 게이트(수리-3c가 RLB2 다이제스트 bit-identical 확인한 선례) |
| **R2. 공유 substrate 손상(D1-A)** | mutation-journal 이관이 sink immediate/deferred 재생을 미묘히 바꾸면 **모든** resolver 경로의 resume가 깨짐(모델 무관) | Da′-6 격리·적대리뷰 필수. shadow-run N판 강화(deferred-choice suspend 경유 카드 표본 포함) |
| **R3. granted-continuous 만료 미착지** | Da′-3가 버킷 만료(W3c-2) 선행에 의존 — 순서 위반 시 duration buff가 안 걷힘(1라운드 P0-1 동류의 사문화) | D2 권고대로 W3c-2 합류 타이밍 확인 후 Da′-3 착수. union 게이트 **실구동** witness(직독-단언 금지, P0-1 교훈) |
| **R4. DigiBurst 분기 오배선 잔존** | RD-P6B-6 미해소 시 keyword-inner가 no-op 코루틴으로 계속 소실 | Da′-4에서 PRIM.DigiBurst red→green 게이트. permanent-grant store(D2-A) 선행 |
| **R5. resolver switch 중간붕괴** | subtype 잔존 중 case 삭제 시 그 카드 무해소 | Da′-5는 subtype 0 계측 확인 후에만. 계기판: `IActivatedCardEffect`/`IEffectBody` 참조 0 |
| **R6. ExecutedPredicate 인라인 누락** | S5를 코루틴에 인라인할 때 카드-정의 composite(3-branch OR 등) 축약 → [fidelity-over-coverage] 위반 | refund·executed 카드 적대 witness 선정(D5). "뭉개면 FAIL" 규약 |

**RLB 다이제스트 영향 예상**: 캡 flip(S1~S3)은 파티션 충실 시 **결정 동일 → digest 중립**이 목표. mutation-journal
이관(D1-A)이 유일한 실질 digest-touch 지점. 수리-3c가 동종 변경에서 bit-identical을 확보한 선례가 있어 **달성 가능**
하나, R6-Da′는 [r4-careful-mode]에 준하는 shadow-run N판 강화 대상으로 취급 권고.

---

## §7. 흡수/신설/은퇴 3분류 규모

| 분류 | 대상 | 규모 |
|---|---|---|
| **흡수(기존 AS-IS/substrate가 처리)** | S1~S5 cap/refund/executed → CEntity_EffectController(신모델 이미 사용); S6 resume → CEntityUseCycle(수리-3c 완비); S7 mutation journal → 공유 존치; S9 granted-continuous → AddEffectToPermanent/Player 버킷(실재·inert-registry) | 회계 4종 + grant store **전량 흡수**(신 substrate 0) |
| **신설(진짜 구축)** | per-card 캡 리셋(D3, `ResetForCard` 대응); [D2-A 채택 시] buff/restriction/cost-mod 신모델 continuous kind-class 표현(카드별); [D1-A 채택 시] CEntityUseCycle에 mutation-journal 필드 | **substrate 신설 ≈ 1**(per-card 리셋), 나머지는 카드-표현·통합 |
| **은퇴(발명 삭제)** | uniform `ActivatedEffect`·`IEffectBody` 15종(69 좌석); `IActivatedCardEffect` 56 subtype + resolver switch; OnceFlagController **캡-파티션 절반**; `EffectBinding`/`ToBinding`/registry(corpus 74좌석, W3c-final 합류); `LegacyActivatedBridge`/`ActivatedHashtableBridge` | corpus `IEffectBody`69 + `IActivatedCardEffect`56 + producer 8 + OnceFlags 캡-절반 |

**핵심**: 1라운드 "회계 소실" 우려는 CEntityUseCycle 등장으로 무효화됐다. R6-Da′는 발명물 청산 골이며, 신 substrate는
per-card 리셋 1건뿐. 최대 난점은 회계가 아니라 `OnceFlagController`의 **공유 substrate 분리**(D1)와 granted-continuous의
**W3c-2 만료 모델 합류 타이밍**(D2·R3)이다.

---

## §8. 사용자 결정 확정 (2026-07-21)
- **D1 = A**: OnceFlagController 완전소멸 — cycle+journal을 CEntityUseCycle로 통합 이관. shadow-run N판 강화 + Da′-6 적대리뷰 필수(R2 완화책 그대로).
- **D2 = A**: granted-continuous는 AS-IS AddEffectToPermanent/Player 버킷 단독. Da′-3는 A1b 버킷-모델 골의 만료 경로 합류 확인 후 착수(R3 완화).
- **D3+D4 = 승인("AS-IS와 둘 다 동일하게")**: ResetUseCountForCard 신설=AS-IS ResetForCard의 substrate 번역으로, :2573=R6-Db 이월하되 처리 시 AS-IS 1:1로.
- **D5 = 적대 권고 표본 채택(선정 위임)**: executed-composite AD1_024·BT14_029 + refund 대표 1~2장(~38장 중 적대 선정). Da′-2 직접-new 11장은 카드별 행동 witness.

실행 순서(확정): A1b(버킷 모델, 실행 중) → Da′-0 배선검증 → Da′-1 factory flip → Da′-2 직접-new 11장 → Da′-3 granted-continuous(버킷 합류 확인 후) → Da′-4 DigiBurst/hasReboot → Da′-5 switch 붕괴 → Da′-6 OnceFlags 통합-소멸(적대리뷰) → corpus 삭제(R6-Db 동승) → W3c-final.

---

## §9. R6-Da′-1 집행 원장 (2026-07-21 — 재스코프 확정분)

### 화이트박스 처분
- **G9-045.SelectActions.Tests = 은퇴(디렉터리 삭제)**. 전 단언(3건)이 발명 Body-표면(`ActivatedEffect.Body.Apply` 직접 호출) 검증이었음. **실룰 커버 증빙**: suspend/unsuspend/bounce의 실제 룰 표면은 `SelectPermanentEffect` Mode.Tap/UnTap/Bounce의 AS-IS 배치(`SuspendPermanentsClass.Tap()`/`IUnsuspendPermanents`/sink bounce)이며, (a) 이관된 `TfxSelectFollowUp` "seq"(Mode.Tap 인라인)를 PRIM-P0가 behavioral로 검증, (b) 인쇄-카드 스위트(ST4_15=Tap, BT2_095=Bounce, ST2_11=UnTap 계열)와 G9-009(Mode.Tap+Destroy resolver-driven E2E)가 동일 배치를 커버.
- **G9-046.SelectAndPlay.Tests = 존치+SelectAndPlay 케이스 3건만 제거**(PlayFrom(Trash)/PlayFrom(Hand)/CandidateFilter — 삭제된 `ActivatedSelectAndPlayEffect` body 표면). 커버 증빙은 테스트 파일 헤더에 기재(BT9_081/BT2_090/BT1_044 잔존 케이스). DeDigivolve(Da′-5 대기)·BT1_044(stale red, A6 처분 대기) 케이스 유지.

### 소비 스위트 하네스 교정 (단언 무변경)
- PRIM-P0: `SetPhase(HeadlessPhase.Main)` 추가 — 신모델 `ICardEffect.CanTrigger`의 AS-IS DoneStartGame 게이트(구모델 헬퍼는 미소비) 통과용. G9-009 F4-companion 판례 그대로.
- G12-004: P1 두 번째 디지몬 추가 — AS-IS forced-selection(정확-max 풀은 무선택 자동확정, SelectPermanentEffect.Activate)이 단일 후보에서 choice를 생략하므로, deferred 경로 검증에 실제 choice가 필요.

### 이월(이번 삭제 제외) + 은퇴-가드
F1/F2/F3 갈림길 결정(2026-07-21 코디네이터 확정)에 따라 아래는 존치·`[Obsolete]`(RD-RETIRE-DA1, warning) 부착·G1R-001 원장 핀:
| 심볼 | 이월 사유 | 소속 배치 |
|---|---|---|
| `AsUniformActivated` | 버프 6좌석 등 잔여 factory 소비 | Da′-3/6 |
| `ActivatedSelectEffect` | EX8_074 RD-R6-07 STOP + 픽스처 3 + ActivatedEffects 내부 사용 | Da′-5/corpus 삭제 |
| `ActivatedSelectBounceAndDiscardSourcesEffect` | C3-Witness 케이스(9) green 소비 — corpus 삭제 시 재조준 필요 | corpus 삭제(R6-Db) |
| `ActivatedSelectTrashDigivolutionEffect` | ST2.Blue stale 캐스트 3좌석 — ST2.Blue 처분과 동시 삭제 | A6 |
| `SelectAndDeDigivolveEffect`(헬퍼) | G9-046 DeDigivolve 케이스 소비 — body와 동시 소멸 | Da′-5 |
| `ActivatedSelectAndDeDigivolveEffect`(body) | resolver switch case(:928) 보유 | Da′-5 |
