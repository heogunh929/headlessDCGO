# 기본 엔진 완성 체크리스트 — 카드 포팅을 위한 미구현 엔진 전수 조사

- 작성일: 2026-06-27
- 목적: **"저런 카드들을 포팅할 기본 엔진 완성"**이 목표. 원본 카드들이 호출하는 엔진 vocabulary 전체를 매핑하고, 헤드리스가 제공하는 것과 대조해 **미구현 부분을 전부** 나열한다. (카드 포팅 자체는 추후 로컬 LLM; 그 전에 강한 모델로 광범위 인프라를 깔아둔다.)
- 방법: 4개 병렬 매핑(원본 CardEffectFactory 152+메서드 / 원본 CardEffectCommons 160연산 / 원본 EffectTiming 63·EffectDuration 8·Select 모델 / 헤드리스 현재 제공범위) + 작성자 스폿검증.
- 범례: ❌ 전무 · ◑ 부분 · ✅ 있음(참고) · 🔴최우선 🟠높음 🟡중간 🔵낮음
- 관련: [remaining_items_master_list.md](remaining_items_master_list.md), [timing_emission_gaps.md](timing_emission_gaps.md), [original_vs_port_divergence_audit_pass2.md](original_vs_port_divergence_audit_pass2.md)

> **숫자 요약**: 원본 트리거 타이밍 **63종** vs 헤드리스 emit **~18종**. 원본 키워드 ~**30+종** vs 헤드리스 실효 ~**4종**(+플래그 5). 원본 effect 연산 ~**160종** vs 헤드리스 뮤테이션 ~**20종**. **effect-driven "디지몬 삭제"가 아예 없음**(최빈 효과). EffectDuration **8종 중 3종**(end-turn류)만 부분.

---

## A. 기반 프레임워크 (대부분의 카드가 의존 — 최우선)

이게 없으면 카드 본문을 깨끗하게 못 적는다. 로컬 LLM에 넘기기 전 **반드시 강한 모델로** 완성할 부분.

| ID | 항목 | 현재 | 작업 | 우선 |
|----|------|------|------|------|
| **F-1** | **EffectDuration 시스템** | ◑ end-turn 3종 메타키만(`untilEachTurnEnd/Owner/Opponent`); `UntilEndAttack/UntilEndBattle/UntilOwnerActivePhase/UntilNextUntap/UntilCalculateFixedCost` 없음 | 8종 duration enum + 각 만료 지점에서 정리(전투끝/액티브페이즈/다음언탭 등). "+DP 턴종료까지"류 거의 전부가 의존 | 🔴 |
| **F-2** | **선택→연산 프레임워크** | ◑ ChoiceType + DeferredChoiceProvider 존재하나, 원본 Select*Effect의 **모드(Destroy/Bounce/PutLibrary/PutSecurity/Tap/UnTap/Degenerate…)와 Root(Hand/Library/Trash/Security/Clock/Execution/DigivolutionCards/LinkedCards)**가 choice→mutation으로 안 묶임 | "대상 N개 선택 → 그 대상에 연산" 공통 파이프라인. 원본 SelectPermanent/Card/Hand 모드를 헤드리스 choice+mutation에 매핑 | 🔴 |
| **F-3** | **effect-driven 게임연산 뮤테이션 vocabulary 확장** | ◑ MatchStateMutationSink ~20종(DP/suspend/flag/zone일부/memory) | 아래 B의 연산들을 뮤테이션 kind로 추가(특히 **Delete**) | 🔴 |
| **F-4** | **once-per-turn 자동 게이팅** | ◑ `OnceFlagHelpers` 존재하나 효과 게이트에 자동 통합 안 됨(수동 호출) | `[Once Per Turn]`(maxCountPerTurn)·use-count를 트리거/활성 판정에 자동 반영. 원본 `InitUseCountThisTurn`/`isOverMaxCountPerTurn` 대응 | 🟠 |
| **F-5** | **플레이어 스코프 연속효과** | ◑ modifier는 per-target. "내 모든 디지몬 +X / 상대 전체 −X"류 player-effect 미흡 | 연속효과를 "플레이어의 조건 매칭 전 permanent"에 적용하는 스코프(원본 `*PlayerEffect` 다수) | 🟠 |
| **F-6** | **타이밍 emit 중앙화 + 누락 45종** | ◑ ~18종 emit, 원본 63종 | 존-이동/뮤테이션 공통 레이어에서 emit(카드별 배선 최소화). 누락분: OnStartMainPhase·OnStartBattle/OnEndBattle·OnTapped/OnUnTapped·OnMove·OnUseOption·OnDiscardHand·OnAddDigivolutionCards·WhenLinked·OnFaceUpSecurityIncreased·Before/AfterPayCost·OnUseDigiburst·WhenWould* 등 ([timing_emission_gaps.md](timing_emission_gaps.md)) | 🟠 |
| **F-7** | **계승효과(inherited) 활성 모델** | ◑ DigivolutionStackReader 있음. inherited-effect 활성규칙(소재가 top 아닐 때만, flip시 차단 등) 미구현 | 원본 `IsInheritedEffect` 활성 계약 포팅 | 🟡 |
| **F-8** | **조건/쿼리 헬퍼 커버리지** | ◑ TargetFilter/ZoneQuery/CardRequirement/MinMax 헬퍼 일부 | IsMin/MaxDP·Level·Cost, trait/name/color 매칭, 존 카운트, 턴 체크 등 원본 조건 헬퍼 전수 대조·보강 | 🟡 |

---

## B. 공통 게임 연산 (고빈도 — 수천 장이 사용)

뮤테이션 kind + (대상이 필요하면) F-2 선택과 결합. **effect-driven Delete가 최우선** (가장 흔한 효과인데 전무).

| ID | 연산 | 현재 | 우선 |
|----|------|------|------|
| **B-1** | **디지몬 삭제(effect Delete)** — 대상 선택 → 삭제 | ❌ **0** (배틀삭제만 있음, 효과삭제 뮤테이션 없음) | 🔴 |
| **B-2** | **±DP / ±Security Attack / ±cost (지속)** — 대상에 modifier + duration | ◑ modifier는 있음, duration 결합 필요(F-1) | 🔴 |
| **B-3** | **바운스(손으로)/덱(top·bottom)으로 되돌리기** | ◑ ZoneMover에 op 있음, effect→선택→이동 배선 필요 | 🟠 |
| **B-4** | **Suspend/Unsuspend (effect)** — 대상 선택 | ◑ 뮤테이션 있음(Suspend/Unsuspend), 선택 결합 | 🟠 |
| **B-5** | **Draw / discard(손버림) / trash from deck** | ◑ Draw 있음, discard·deck-trash effect 배선 | 🟠 |
| **B-6** | **시큐리티 trash / 시큐리티에 추가(Recovery: 덱→시큐리티)** | ◑ TrashSecurity 있음 / **Recovery ❌ 0** | 🟠 |
| **B-7** | **덱 top N장 공개 + 선택 처리(reveal & select)** | ◑ Reveal 흔적만(4) | 🟠 |
| **B-8** | **effect로 카드 플레이(무료/코스트)** — 손/트래시/시큐리티/소재에서 | ❌ 0 (PlayFromHand effect 없음) | 🟠 |
| **B-9** | **토큰 생성/플레이** | ❌ 토큰 팩토리 없음(`isToken` 플래그만) | 🟡 |
| **B-10** | **소재(디지볼브 카드)/링크 카드 trash·복귀** | ❌ (소재 stack은 attach만) | 🟡 |
| **B-11** | **트래시→손/덱 복귀** | ❌ | 🟡 |

---

## C. 키워드 (원본 ~30+종 / 헤드리스 실효 ~4종)

✅ 실효: **Blocker · Jamming · Reboot · Piercing**. ◑ 플래그만(자동화 없음): Rush · Blitz · Retaliation · ArmorPurge · Pierce.

| 그룹 | 키워드 | 현재 | 우선 |
|------|--------|------|------|
| 기본 전투 | **Rush**(소환턴 공격) | ◑ 플래그 — N-1 소환멀미와 연동돼 소비측 일부 있음 | 🟠 |
| 기본 전투 | **Blitz**(상대턴 공격) · **Piercing(Pierce)**(초과뎀 시큐리티) · **Jamming**(시큐 전투 파괴불가) | Blitz◑플래그 / Piercing✅ / Jamming✅ | 🟠 |
| 방어 | **Blocker**✅ · **Decoy**(강제 블록 유도) · **Barrier**(첫 효과/공격 무효) · **Fortitude**(언탭 시 재블록) · **Evade**(턴1 공격 회피) | Blocker만 ✅, 나머지 ❌ | 🟠 |
| 반격/처형 | **Retaliation**(블록 시 반격) · **Execute**(블록 성공 시 파괴) · **Collision**(블록 상호파괴) | Retaliation◑플래그, 나머지 ❌ | 🟡 |
| 진화/리셋 | **Reboot**✅ · **Raid**(시큐리티 직접공격) | Raid ❌ | 🟡 |
| 자원/조건 | **Fragment**(플레이시 트래시) · **Iceclad**(공격시 트래시) · **Decode**(trait 조건) · **Partition** · **Progress** · **Overclock**(trait 공격버프) · **Ascension** · **Alliance**(드로우) · **Scapegoat**(다중대상 면역) · **Vortex**(끌어오기/플레이어공격) | 전부 ❌ | 🔵 |
| 기타 | **ArmorPurge** · **Save** · **Material Save** · **Training** | ArmorPurge◑플래그 | 🔵 |

> 키워드는 대부분 (a) 연속효과 등록(생산측, C-그룹별) + (b) 전투/공격 로직의 소비 훅이 필요. Blocker/Jamming/Reboot/Piercing이 레퍼런스 패턴.

---

## D. 대형 서브시스템 (앱·고급 세트 — 전무, 각각 독립 구축)

| ID | 서브시스템 | 현재 | 비고 |
|----|-----------|------|------|
| **D-1** | **Link(링크)** | ◑ 흔적 7(플래그) | 링크 카드 부착/해제/링크코스트·max 관리. ST22 다수 |
| **D-2** | **Appfuse(앱퓨전)** | ❌ 0 | 이름/조건 기반 다카드 융합 |
| **D-3** | **Raid(레이드)** | ❌ 0 | 시큐리티 직접공격 + 공격중 효과 |
| **D-4** | **De-Digivolve(진화퇴화)** | ❌ 0 | 소재 N장 제거(stack 분리) |
| **D-5** | **DNA Digivolve(Jogress) / DigiXros** | ◑ ad-hoc(1차 audit) | 다permanent/다카드 융합 진화 |
| **D-6** | **Blast / Arts Digivolve** | ❌ | 코스트리스/특수 진화 경로 |
| **D-7** | **효과 무효화(invalidation)** | ❌ 0 | "[When Digivolving] 효과 무시" 류 — 연속효과를 끄는 메커니즘 |
| **D-8** | **코스트 감소 파이프라인(replacement)** | ◑ modifier만(2) | BeforePayCost 단계 + "코스트 감소 불가" 등 |
| **D-9** | **Recovery / Token 생성 / Mind Link / Delay Option** | ❌ | B-6/B-9와 연계 |

---

## E. 이미 구현됨 (참고 — 재사용 패턴)

- 트리거: 18종 emit + `AutoProcessingTriggerCollector`/`TriggerTimingMap`/`EffectScheduler`(mandatory→optional, 턴플레이어 우선) ; OptionalPromptQueue(선택발동).
- 연속/대체 소비측 게이트: `ContinuousRestrictionGate`(attack/block/digivolve) · `ContinuousDpGate`(±DP·DP면역) · `BattleDeletionGate`(파괴불가).
- modifier/restriction/replacement 헬퍼(±DP/cost/sAttack, cannot-*, prevent/immune).
- 뮤테이션: DP·suspend·flag·zone(trash/hand/deck/security)·memory·키워드 플래그 grant.
- 전투/시큐리티: BattleResolver·SecurityResolver(시큐 디지몬 전투·OnSecurityCheck)·AttackPipeline·BlockTiming·Counter.
- 셋업: 셔플·멀리건(N-5)·시큐리티 top 삽입(N-3)·소환멀미(N-1).
- RL: 관측/액션 인코딩·factored mask·strict+validated 프로파일·MaxIterations 노출.

---

## 권장 구축 순서 (로컬 LLM 인계 전 강한 모델로)

1. **A. 기반 프레임워크 먼저** — F-1(Duration) → F-2(선택→연산) → F-3(뮤테이션 확장, **Delete 포함**) → F-6(타이밍 중앙 emit) → F-4(once-per-turn) → F-5(player-scope). *이게 깔리면 카드 본문이 좁은 패턴 채우기가 됨.*
2. **B. 공통 연산** — B-1(Delete) → B-2(±DP duration) → B-3/B-4/B-5/B-6/B-7/B-8. 고빈도 순.
3. **수직 슬라이스 재시도** — A+B가 깔린 뒤 대표 카드 3~5장 실제 포팅 → **정규 템플릿 + 헬퍼 확정**.
4. **C. 키워드** — Blocker 패턴 따라 그룹별(전투→방어→반격→자원).
5. **D. 대형 서브시스템** — 해당 세트 포팅 시 각각.
6. **그 다음에야 로컬 LLM**에 per-card 채우기 인계.

> 한 줄: **A(기반 6) + B(공통 11) + 정규 템플릿**이 "기본 엔진 완성"의 핵심. 그게 되면 C(키워드)·D(서브시스템)는 패턴 반복 + 카드별 작업이고, 로컬 LLM이 감당할 좁은 일이 된다.
