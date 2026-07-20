# B군 2라운드 프로브 census — EffectRegistry 일회용 삭제 + 컴파일러 소비자 전량 열거 (2026-07-20)

Base: `c4205b2a`(수리 원장 아크 마감). **일회용 워크트리 `probe/registry-census`에서만 삭제·빌드 수행 → census 후 완전 폐기.** main 소스 무변경. 커밋 없음. 경로=`src/HeadlessDCGO.Engine/` 상대(별도 표기 없으면).

---

## §0. 방법론 (중요 — 프로브 신호의 성격)

프로브는 각 대상 **타입 선언 식별자를 sentinel(`_PROBEDEL`)로 개명** → 모든 외부 참조를 `CS0246`(형식 없음)으로 파열시켜 소비자를 열거하는 방식(파일 물리삭제와 등가이나, 공유 파일 안의 형제 타입을 함께 죽이지 않아 단계 분리가 가능). `EffectBinding`은 `EffectRegistry.cs`(:145)에 **공유** 거주하므로 파일삭제로는 Stage 1/2 분리 불가 → 개명 프로브가 필수.

**핵심 계측 성질**: Roslyn은 오류-형(error-typed) 수신자에 대한 멤버 접근을 **연쇄억제(cascade-suppress)**한다. 즉 `EngineContext.EffectRegistry`(property) 경유의 `.GetContinuousEffects()`·`.Register()` 호출은 property 형이 깨져도 **각각 오류를 내지 않는다**. 따라서:
- **타입-이름 프로브(Stage 1/2 빌드 오류)** = 그 타입을 **형(型)으로 명명**하는 좌석만 포착(파라미터/필드/기저/생성자).
- **판독·생산 call-site(§H의 "판독 ~30·생산자 14")** = property 경유 **메서드 호출**이라 빌드 오류에 안 뜸 → **인터페이스 멤버 이름 grep**(`.Register(`·`.GetContinuousEffects(` 등)으로 별도 회수. 본 census는 두 신호를 모두 싣는다.
- **테스트 좌표**: 엔진이 의도적으로 파열된 상태에선 test 프로젝트가 엔진-컴파일 단계에서 상류 실패 → 파일별 좌표 산출 불가. 따라서 **test-pin census는 정적 grep**(빌드 아님). 규율상 test 실행·test 빌드는 하지 않음(엔진만 빌드).

빌드 로그 원문: `/home/hg/.claude/jobs/dae5cd41/tmp/stage1_errors.txt`·`stage2_errors.txt`·`cs0246_table.txt`.

---

## §1. 단계별 오류 통계

| 단계 | 프로브(개명) | 엔진 빌드 오류 | 비고 |
|---|---|---|---|
| **baseline** | 없음 | **0** | 워크트리 그린 확증(경고 1777) |
| **Stage 1** | `EffectRegistry`(interface :6) | **18** (전부 CS0246) | 타입-이름 좌석만. property 판독은 억제됨 |
| **Stage 2**(누적) | +`EffectBinding`(:145)·`IEffectBody`(ActivatedEffect.cs:15)·`IActivatedCardEffect`(LegacyActivatedBridge.cs:36, **abstract class**)·`IHeadlessCardEffect`(HeadlessCardEffectContract.cs:6) | **288** (CS0246 255·CS0538 31·CS0234 1·CS1520 1) | 개명-타입별 귀속은 §3 |

CS0246 개명-타입 귀속(Stage 2): `EffectBinding` 103 · `IEffectBody` 69 · `IActivatedCardEffect` 56 · `EffectRegistry` 18 · `IHeadlessCardEffect` 9. CS0538 31 = `ActivatedEffects.cs`에서 개명된 인터페이스를 명시-구현하던 좌석(구현자 신호).

---

## §2. Stage 1 — `EffectRegistry` **타입-이름** 소비자 census (18좌석)

전부 **substrate/룰 층**(`Headless/Effects`·`Headless/Runtime`) + producer 좌석. **카드 corpus는 이 타입을 형으로 명명하지 않음**(오직 `.Register` producer로만 접촉) — 타입-결합은 룰층 단독이라는 것이 핵심 발견.

| 파일:라인 | 성격 |
|---|---|
| `Headless/Bridge/EngineContext.cs`(30,9)(97,12) | **producer 좌석**(property 선언 + 생성 `new InMemoryEffectRegistry()` 배선; :47·:382) |
| `Headless/Effects/EffectDurationExpiry.cs`(24,37)(37,39)(44,39)(51,39)(63,43) | 만료 sweep 5메서드 `Expire*(EffectRegistry, …)` |
| `Headless/Effects/MatchStateMutationSink.cs`(300,22)(403,9) | sink 파라미터 |
| `Headless/Effects/CardEffectSchedulerResolver.cs`(13,9) | 스케줄러 필드 |
| `Headless/Runtime/CardLeavePlayCleanup.cs`(23,9)(44,35)(93,9) | leave-play cleanup |
| `Headless/Runtime/ContinuousKeywordGate.cs`(196,9)(231,35) | 키워드 게이트 |
| `Headless/Runtime/DeletionReplacementGate.cs`(151,112) | 삭제-교체 presence |
| `Headless/Runtime/DeletionReplacementTiming.cs`(53,154)(69,139) | PRE 옵션 |

**producer(생산자) 실 call-site = 14 (live)** — property 억제로 빌드엔 안 뜨므로 `\.Register(` grep으로 회수. §H "Register 생산 24→14"와 **정확히 일치**:

| producer 좌석(`.Register`) | 클러스터 |
|---|---|
| `CardEffectRegistrar.cs:237` | enter-play non-activated 등록 디스패치(단일 관문) |
| `CardEffectCommons.cs:1520,2912,2968,3048,3181` (5) | grant 코어(GiveEffect 계열) |
| `ActivatedEffects.cs:779,889,983,1061,2456,2573` (6) | **granted-continuous**(R6-Da′ corpus 동승) |
| `ActivatedEffect.cs:339` | uniform activated 등록 |
| `ActivatedEffectResolver.cs:706` | resolver 등록 |
| (`TestFixtures/TfxOnKnockOutDeleteOpponent.cs:5` = **주석 참조**, live 아님) | — |

**reader(판독) 실 call-site = 29** — §H "판독 ~30사이트"와 일치(§B 원판 ~60 → W3c 시리즈로 반감 확인):

| 메서드 | n | 좌표(요약) |
|---|---|---|
| `GetContinuousEffects` | 9 | ContinuousScopeEvaluation:49,307 · RestrictionScan:45 · CanNotPlayOptionScan:106 · ContinuousImmunityGate:56 · EffectInvalidation:38 · SecurityResolver:867 · MatchStateMutationSink:1749 · CardEffectCommons:4638 |
| `RemoveWhere` | 9 | EffectDurationExpiry:27,40,47,54,66 · CardLeavePlayCleanup:53 · CardEffectRegistrar:129 · TriggeredEffects:109 · MindLink:120 |
| `GetKeywordEffects` | 5 | ContinuousKeywordGate:209,239 · CardLeavePlayCleanup:132,147 |
| `GetEffects` | 3 | DeletionReplacementTiming:60,80,256 |
| `Find` | 2 | CardEffectSchedulerResolver:30 · GameFlowProcessor:1146 |
| `GetRestrictionEffects` | 1 | RestrictionScan:46 |

---

## §3. Stage 2 — 타입-참조 census (§B 계기판 ③ "참조 0" 대상의 실좌표)

§H가 정성 기술만 했던 것을 **실 파일-분포**로 확정. 괄호 = 정적 grep hit(src) 대비.

### 3a. `EffectBinding` — **103** 컴파일-좌석 (grep 173 src / 131 tests)
발명 registry **substrate 타입**(record). 분포:
- corpus: `ActivatedEffects.cs` 51 · `ContinuousAndRestrictionEffects.cs` 23 · `ActivatedEffect.cs` 1
- registry 인프라: `EffectRegistry.cs` 12 · `InheritedGrantedSecurityHelpers.cs` 7(Create*Binding 팩토리) · `KeywordBaseBatch1/2` 2 · `SkillInfo.cs` 1 · `TriggeredEffects.cs` 1 · `LegacyActivatedBridge.cs` 1 · `CardEffectRegistrar.cs` 1 · `TfxTriggeredMemoryEffect.cs` 1

### 3b. `ToBinding`(메서드, 타입 아님) — **96** src call/decl (grep 134 src / 121 tests)
`EffectBinding` 생산 메서드(각 corpus 효과 클래스가 보유). `SkillInfo.cs:87` + corpus 클래스 다수 + `LegacyActivatedBridge.TryToBinding` 브릿지. EffectBinding·registrar와 **동시 소멸**.

### 3c. `IEffectBody` — **69** 컴파일-좌석 (grep 93 src) ✓ §H "93" 정합
- `ActivatedEffects.cs` 43 · `ActivatedEffect.cs` 21 · `CardEffectFactory.cs` 1 · `CardEffectCommons.cs` 1 · Tfx 3
- **성격**: uniform-activated 모델의 "composable body"(주석상 AS-IS ActivateCoroutine 미러). §H 지적 = cap-파티션/환불/executed 의미론이 AS-IS ActivateClass에 미표현 → **R6-Da′ 동형 재설계 대상**(단순 은퇴 불가).

### 3d. `IActivatedCardEffect` — **56** 컴파일-좌석 (grep 75 src) ✓ §H "75" 정합
- `ActivatedEffects.cs` 50 · `CardEffectFactory.cs` 5 · `ActivatedEffect.cs` 1
- **성격**: 구모델 activated 마커(**abstract class**, `: ICardEffect`). R6-Da′/corpus 동승. CS0538 31좌석(ActivatedEffects.cs)이 이 계열 구현자.

### 3e. `IHeadlessCardEffect` — **9** 컴파일-좌석 (grep 16 src) ✓ §H "16" 정합
- `HeadlessCardEffectContract.cs` 2 · `EffectRegistry.cs` 2 · `SkillInfo.cs` 1 · `TriggeredEffects.cs` 1 · `KeywordBaseBatch1/2` 2 · `TfxTriggeredMemoryEffect.cs` 1
- **성격**: EffectBinding이 실어 나르는 effect 페이로드 인터페이스 → registry 인프라 타입.

> **AS-IS 부재 확인(dead-judgment-needs-AS-IS)**: `IEffectBody`·`EffectBinding`·`IHeadlessCardEffect`·`IActivatedCardEffect` 4종 모두 `DCGO/` 원본 트리에 grep(`--binary-files=text`) hit **0** — 전부 재구축 발명물(Gate/Registry/Binding 계열). 재하우징 타깃 AS-IS 기제는 실재: `ActivateClass`(3682파일)·`EffectList` live-scan(85)·`CanNotBeAffected` 직독(392).

---

## §4. 클러스터 분류 (원자-flip 배치 가능성 + 의존 순서)

### (a) 발명물 소비자 — 은퇴 대상 (AS-IS 대응 기제 부재)
| 클러스터 | 좌석 | 원자-flip 닫힘? | 순서 |
|---|---|---|---|
| **registry substrate**: `EffectRegistry`(interface/InMemory)·`EffectBinding`·`ToBinding`·`IHeadlessCardEffect` + `InheritedGrantedSecurityHelpers` Create*Binding | Stage1 18 + `EffectBinding`103 + `ToBinding`96 + `IHeadlessCardEffect`9 | **아니오** — 생산자(14) 전멸 **후**에만 delete-and-rewire 원자 닫힘 | **최종**(W3c-final) |
| **판독-half union 게이트**(ContinuousScopeEvaluation·RestrictionScan·CanNotPlayOptionScan·EffectInvalidation·SecurityResolver:867·MatchStateMutationSink:1749·ContinuousKeywordGate) | reader 20좌석 | 생산자 0 확인 후 컴파일러-열거 일괄 | W3c-final 직전 |
| **ContinuousImmunityGate**(:56) | reader 1 | compile-only 스텁화 → 테스트 2종 재조준 후 삭제 | ②소멸 후(§H③) |

### (b) AS-IS 좌석의 registry-백킹 — 재하우징 대상
| 클러스터 | 좌석 | live 정본 게터 존재? | target |
|---|---|---|---|
| **만료 sweep**: `EffectDurationExpiry`(타입 5 + RemoveWhere 5) | 10 | **아니오** → A2 player-bucket 만료 모델 필요 | AS-IS 리셋-사이트 버킷 만료(§G W3c-2) |
| **grant 코어**: `CardEffectCommons.cs:1520,2912,2968,3048,3181` (.Register 5) | 5 producer | 부분 — C-게이트 grant 코어 일부 잔존 | 신모델 버킷(A군 패턴, §H② C) |
| **leave-play/keyword cleanup**: `CardLeavePlayCleanup`·`ContinuousKeywordGate`·`DeletionReplacementGate/Timing` | 타입 8 + reader | 예 — AS-IS EffectList/CanNot* 직독 이관 경로 존재 | EffectList live-scan |
| **enter-play 등록 관문**: `CardEffectRegistrar.cs:237`(.Register) + :129(RemoveWhere) | 2 | 예 — non-activated 버킷 전환 | 신모델 버킷 |
| **DeletionReplacement PRE**(`CustomWouldBeDeletedOption`) | Timing:53,69 GetEffects 3 | 재판정 필요(유일 잔존 PRE 브릿지) | §B §17 잔존-필수 재판정 |

### (b′) **R6-Da′ 선행 의존**(activated 표현형 — 설계先行 필수)
| 클러스터 | 좌석 | 왜 단순 flip 불가 |
|---|---|---|
| **activated corpus**: `IEffectBody`(69)·`IActivatedCardEffect`(56, CS0538 31 구현자) + `ActivatedEffect.cs:339`·`ActivatedEffectResolver.cs:706`·`ActivatedEffects.cs` granted-continuous 6 producer | 69+56+8 | 구모델 `ActivatedEffect`의 **cap-파티션/환불/executed 의미론이 AS-IS `ActivateClass`에 미표현** — 단순 flip = 회계 소실(§H①). **창 컷오버 동형 설계 골(R6-Da′)로 선(先)해소**해야 corpus 삭제 및 상기 6 producer 청산 가능 |

### (c) 테스트-핀 (구모델 단언 스위트 — 재조준/은퇴)
정적 grep(빌드 아님, §0):
- `new InMemoryEffectRegistry` **직접 생성 26 스위트**(하드 컴파일-핀): A4-Execute·C-EoT2·EXEMPLAR-{GLINK,T1,T2A,T2B,T3A,T3B}·G1F-006·G3.5-001·G3.5-RL-{A4,B2B3}·G3.5-W7·G3H-002·G3I-{001,002}·G3L-002·GR-006·PILOT-S1~S6·RD-BATCH7B·W-EoTFIX
- `EffectBinding` 명명 **67 파일** · `.Register(` **56 파일** · `IHeadlessCardEffect` **28 파일**
- §E 지정 **레지스트리-단언 dedicated ~11**(G1F-004/005/006·G3J-001·G6-001·G7-001·G8-002/003·G2F-004·G3G-001/002) = 재조준/은퇴 우선. 나머지(~170)는 substrate 참조뿐 → 개명-따라가기(mechanical rename)로 닫힘.

---

## §5. §H 지도와의 diff

| 항목 | §H(1라운드 마감) | 본 census(2026-07-20 실측) | 판정 |
|---|---|---|---|
| Register 생산 | "24→**14**" | **14 live**(+주석 1) | ✓ 일치 확증 |
| 판독 call-site | §B "~60" → W3c로 감축(수치 미기재) | **29** | 신규 확정(§B 원판 대비 −반) |
| **타입-결합 좌석** | 미분리(정성) | **EffectRegistry 형-명명 18** (전부 룰층; corpus는 형-명명 0) | **신규 발견** — 타입-결합과 행동-결합(call-site) 분리 계측 |
| `IEffectBody`/`IActivatedCardEffect`/`IHeadlessCardEffect` 참조 | "93/75/16"(정적 주장) | 컴파일-좌석 **69/56/9**, 정적 grep **93/75/16** | ✓ grep 주장 정합; **컴파일-실결합은 더 작음**(주석·xmldoc·선언 제외) |
| `EffectBinding`/`ToBinding` | (§H 미수치) | **103/96** 컴파일 · **173/134** grep src | 신규 수치화 |
| R6-Da′ | "독립 region-골 재스코프"(정성) | 좌석 확정: `IEffectBody`69+`IActivatedCardEffect`56+producer 8 | **좌표화 완료** |
| registry 물리 삭제 게이트 | "①② 소멸 후" | 확증: 생산자 14 + R6-Da′ corpus 미해소로 **아직 원자 닫힘 불가** | ✓ |

**§H 대비 실질 신규**: (1) EffectRegistry 타입-결합은 **룰층 18좌석 단독**(corpus는 producer로만 접촉) — 타입 삭제와 producer 청산이 **분리 배치 가능**함을 실증. (2) 판독 29·생산 14의 정확 좌표. (3) Stage-2 5타입의 파일별 분포표(§H가 파일명만 나열했던 것을 좌석 수로).

---

## §6. 권장 배치 순서 (R6-Da′ 위치 명시)

컴파일러-열거 원자성 기준 의존 위상:

1. **W3c 잔여(b 재하우징)** — `EffectDurationExpiry` 만료 모델(A2)·`CardEffectCommons` grant 5·`CardEffectRegistrar:237`·leave-play cleanup을 AS-IS 버킷/EffectList로 이관 + 대응 게이트-half flip. 각 원자(소비자 재하우징 ↔ producer 청산 동시). → **Register 14 → activated-only 잔여**.
2. **R6-Da′(선행 설계 골)** — activated 표현형 동형 이관(cap-파티션/환불/executed → AS-IS ActivateClass 표현 도출). 완료 시 `IEffectBody`69·`IActivatedCardEffect`56·CS0538 31 구현자·producer 6(granted-continuous)·`ActivatedEffect:339`·`resolver:706` 청산 가능. **(a)registry 삭제의 하드 선행 — 이 골 전엔 producer 0 도달 불가.**
3. **corpus 삭제(R6-Db 동승)** — activated/continuous corpus 파일 자기-삭제 + `ToBinding`96·`EffectBinding` corpus 74좌석 소멸. 인라인 6장 re-port·Tfx 18 은퇴·특수플레이 마커 5 동승.
4. **W3c-final = registry 물리 삭제(원자, 컴파일러-열거)** — 생산자 0 확인 후: 판독-half union 게이트(reader 20) 스캔-단독 전환 → `EffectRegistry`(interface/InMemory)·`EffectBinding`·`ToBinding`·`IHeadlessCardEffect`·`InheritedGrantedSecurityHelpers`·`LegacyActivatedBridge`·`registrar` 삭제. **ContinuousImmunityGate 스텁 + 테스트 2종 재조준** 동봉. test-pin 26 하드생성 + ~170 substrate 참조 = delete-and-rewire 기계 개명. **적대리뷰 필수**(union 제거=최대 위험).

핵심: **R6-Da′는 4(registry 삭제)의 하드 선행**이며 1(W3c 재하우징)과는 병렬 가능(파일 서로소: R6-Da′=ActivatedEffect(s)/factory activated-half, W3c=Commons/게이트/Expiry). 순서 위반 시 producer가 0에 못 가 registry 원자 flip이 열리지 않음.

---

## §7. 워크트리 폐기

프로브 워크트리 `probe/registry-census`(경로 `/home/hg/.claude/jobs/dae5cd41/tmp/probe-census`)는 census 종료 후 `git worktree remove --force` + `git branch -D`로 완전 폐기. main 워킹트리 변경 = 본 문서 1파일뿐(§Report 참조). 커밋 없음.
