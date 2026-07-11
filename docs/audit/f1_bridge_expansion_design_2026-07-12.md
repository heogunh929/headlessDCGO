# F-1 설계: Triggered→Activated 브릿지 확산 (2026-07-12)

> 실전 최대 갭. 트리거 타이밍에서 카드의 **activated 효과**(draw/trash/delete/select/play — `IActivatedCardEffect`, resolver로 해소)를 발화시키는 브릿지를, 현재 배선된 ~13개 타이밍에서 미배선 타이밍 전반으로 확산.
> 원칙: [[triggered-activated-bridge]]("이름-커버리지 ≠ 완성") · [[check-asis-before-implementing]] · [[fidelity-over-coverage]] · [[no-callsite-not-skip-reason]] · [[adversarial-review-before-cutover]] · [[grep-binary-skip-pitfall]].
> 출처: 3-way 병렬 조사(AS-IS 타이밍 전수 카탈로그 · 미배선 카드 분포 · 헤드리스 인프라 진단, 2026-07-12).

---

## 0. 진단 요약 (3 조사 종합)

### 규모 (실측)
- **AS-IS EffectTiming = 60 멤버**, **헤드리스 enum = 46 멤버** → **14개 타이밍이 헤드리스 enum 자체에 부재**.
- 브릿지된 타이밍 = **13개**(SubjectScoped 4 + Boundary 3 + EventBroadcast 6): OnAllyAttack·OnDestroyedAnyone·OnUnTappedAnyone·OnDeclaration·OnEndTurn·OnStartTurn·OnStartMainPhase·OnDigivolutionCardDiscarded·OnDigivolutionCardReturnToDeckBottom·OnEndBattle·OnTappedAnyone·OnUseOption·OnLeaveFieldAnyone.
- **미배선 reactor 타이밍의 activated-body 카드 = 실측 657(distinct 603)**. "~660장"은 과장이 아니라 실측치. 이 중 실포팅 6장 → **651장이 확산 시 신규/본체 포팅 필요**.
- **DEAD 7종**(AS-IS 미발화·무카드): OnGetDamage·OnEndAttackPhase·OnEndMainPhase·OnKnockOut·OnEndCoinToss·OnUseAttack(→OnAllyAttack 대체)·OnEndBlockDesignation. **확산 대상 아님**(inert 유지).

### "이름-커버리지 ≠ 실-커버리지" — 두 종류의 갭 구분 (핵심)
| 갭 종류 | 정의 | 처리 |
|---|---|---|
| **브릿지-shape 갭** (F-1 본체) | 미배선 타이밍은 **리액터 shape 자체가 없음** — activated 효과가 발화될 경로가 없음. 인프라 확장(enum+세트+emit+scan-zone) 선행 필요. | F-1 확산 |
| **per-card 포팅 백로그** | ✅ 브릿지된 타이밍은 리액터가 **임의 ActivateClass 본체를 generically 해소** — OnDestroyedAnyone은 482장 중 12장만 실포팅이나, 나머지 470장은 "브릿지 갭이 아니라 각 카드 본체 번역만 남음". | 별도 포팅 트랙(F-1 아님) |

즉 F-1은 **인프라 확장**이고, 그 위에서 카드 본체 포팅은 별개 대량 작업. 이 문서는 인프라 확산을 다룬다.

### 핵심 AS-IS 메커니즘 (모든 타이밍 공통)
- 중앙 디스패치 `AutoProcessing.StackSkillInfos(hashtable, timing)`(AutoProcessing.cs:984) → `GetSkillInfos`(:770)가 **5존 균일 스캔**: player-scope · 필드 permanent · **트래시** · **핸드** · **앞면 시큐리티**. 각 후보에 `is ActivateICardEffect && CanTrigger(hashtable)`.
- **self/anyone 게이트 이원 구조**(F-1의 진짜 난제): 같은 타이밍이 self(SubjectScoped)와 anyone(EventBroadcast)를 동시 서빙. 게이트가 **driving event(subject 리스트+metadata)를 hashtable에서 읽어** self/other 자기-판정. 예: `CanTriggerWhenPermanentRemoveField(ht, cond) => GetPermanentsFromHashtable(ht).Any(cond)`.
- 카드-드리븐 cut-in(중앙 훅 없음): `WhenDigisorption` 등은 행동 수행 카드가 직접 `EffectList(timing)` 순회 → 엔진 훅 없이 카드마다 broadcast 재현 필요.

### 헤드리스 인프라 현황
- 단일 통합 창: `GameFlowProcessor.AutoProcessAsync` → `WindowResolverWiring.CollectUnifiedSeed`(스케줄러半 + activated半) → `WindowResolver.DriveAsync`. 확장 지점은 전부 activated半(`CollectActivatedBridgeTriggers`, WRW:655-798).
- **event-threading 실경로**: EventBroadcast는 `MakeActivatedBridgeTrigger(..drivingEvent..)` → resolve-context Values에 `ActivatedBridgeDrivingEventKey`로 GameEvent 저장 → `BuildUniformResolveContext`가 `TriggerEntityId = drivingEvent.Subject` + 이벤트 원시 메타를 `event.<key>` values로 스레드(**string/int/bool/long만** — list/Func 유실).
- **scan-zone 단일 지점** `ScanZones`(WRW:809-825): 현재 BattleArea+Trash+Hand. **Security(face-up)·player-scope 미포함** = AS-IS 5존 대비 갭.
- **카테고리 분기**: `CollectActivatedBridgeTriggers`가 `TriggerTimingMap.Derive` timing을 `Enum.TryParse<EffectTiming>` 후 세트 멤버십으로 SS→BD→EB else-if. **enum 부재 timing은 TryParse 실패→continue→activated 브릿지 원천 불가**(단 스케줄러半 memory/DP는 문자열 매칭이라 계속 발화 = "memory/DP는 되고 draw/trash는 안 됨" 갭).

---

## 1. 설계 원리

### R1. driving-event 전달 + 5존 스캔 = 한 쌍
AS-IS `GetSkillInfos`는 전 타이밍에서 5존을 스캔하며 hashtable(subject+metadata)을 전달한다. 헤드리스 브릿지는 **(a) driving event를 resolve-context에 스레드 (b) 스캔 존을 리스너가 사는 존까지 확장** — 이 둘을 항상 함께 배선해야 self/anyone 게이트가 AS-IS와 동일 동작. 한쪽만 하면 비대칭(리스너 못 찾거나 게이트 오판).

### R2. 카테고리 판정 결정 트리 (타이밍 하나를 분류할 때)
```
반응 효과가 이벤트 subject(그 카드 자신)에만 있나?
  ├─ 예 → SubjectScoped (drivingEvent=null, subject 1장, scan-zone 불요)
  │        └─ 턴 내 다발 가능(재-suspend 등)? → OncePerTurn 캡 추가
  └─ 아니오(cross-card 또는 전 필드) →
       subject 없는 턴 경계인가?
         ├─ 예 → Boundary (drivingEvent=null, 전 존 스캔, 자연 1회 캡)
         └─ 아니오(subject+metadata 있고 반응카드가 다름) →
              → EventBroadcast (drivingEvent 스레드, 전 존 스캔, MaxCountPerTurn 자기캡)
                 └─ 동시-배치 1회 시맨틱? → firedXxxBatch collapse + BatchId 스탬프
```
- self+anyone 겸용 타이밍(OnDestroyedAnyone·WhenRemoveField·OnUnTappedAnyone류)은 **양쪽 세트에 등록**(subject-scoped 발화 + broadcast 발화 공존, 헤드리스 이미 OnDestroyedAnyone·OnUnTappedAnyone가 그 형태).

### R3. self-scheduler는 브릿지 아님
self-scoped bound 효과(WhenRemoveField의 memory/DP)는 scheduler half가 per-card 처리 — activated半 브릿지 대상 아님. activated 본체(select/trash/prevention)를 가진 self-scoped 리액터만 SubjectScoped 브릿지(leave-hook seam) 필요.

---

## 2. 인프라 확장 (선행 작업, 확산 前)

### I1. enum 정합 (헤드리스 부재 14개 중 확산 대상만 추가)
헤드리스 EffectTiming enum **끝에 append**(ordinal 안정, 회귀지점 #6). 추가 대상 = 미배선 EB/SS 중 카드 있는 타이밍: OnDraw·OnAddHand·OnLoseSecurity·OnAddSecurity·OnDiscardHand·OnDiscardSecurity·OnDiscardLibrary·OnSecurityCheck·WhenReturntoHandAnyone·WhenReturntoLibraryAnyone·OnPermamemtReturnedToHand(enum 오타 그대로)·OnReturnCardsToHandFromTrash·OnReturnCardsToLibraryFromTrash·WhenWouldLink·OnFaceUpSecurityIncreased·WhenUntapAnyone·OnStartBattle·OnCounterTiming·OnBlockAnyone·OnAttackTargetChanged·WhenTopCardTrashed·WhenDigisorption·OnUseDigiburst·OnDetermineDoSecurityCheck·AfterEffectsActivate·RulesTiming·BeforePayCost·AfterPayCost·WhenWouldDigivolutionCardDiscarded. (이미 enum 있는 것 = OnMove·WhenLinked·WhenRemoveField·OnRemovedField·OnAddDigivolutionCards·WhenPermanentWouldBeDeleted·OnEndAttack·OnLinkCardDiscarded 등 — 세트 등록만.)
- **DEAD 7종은 추가 안 함**(inert).

### I2. scan-zone 5존 확장 (`ScanZones`)
BattleArea+Trash+Hand → **+ Security(face-up)** + (player-scope는 별도 축). face-up/face-down 모델 선확인. **행동-중립 보장**: 각 미러 카드가 자기 존 가드(IsExistOnBattleArea/Trash/Security), 비반응 카드는 `HasActivatedEffectsAt`가 필터. 존 확장은 순회 비용↑이나 기존 카드 회귀 없음(존-가드 누락 카드만 위험).

### I3. TriggerTimingMap emit 배선
각 타이밍의 driving event가 실제 emit되는지 확인. CardMoved 파생(OnDiscard*/OnAddHand/OnLoseSecurity/OnMove/OnAddSecurity 등)은 존-전이로 파생 가능. override-메타 필요분(battle/link/cost)은 해당 파이프라인이 메타 실어 emit. **원시 타입 평탄화**(winnerIds 등 List→평탄, 회귀지점 #7).

### I4. 카드-드리븐 cut-in (WhenDigisorption)
엔진 중앙 훅 없는 타이밍 — 행동 수행 지점(Digisorption 실행)에서 emit 배선하거나 per-card 재현. 단발(BT3_056 1장)이라 후순위.

---

## 3. 확산 배치 계획 (ROI 순 — goal+witness 단위)

각 타이밍(또는 타이밍 그룹)이 하나의 goal+witness 골: **인프라 확장(enum+세트+emit+scan) + witness 2~3장(적대 선정) + 적대 리뷰**. 카드 본체 대량 포팅은 별도 트랙.

### Tier 1 — 저위험 고ROI (단순 draw/gain/select, cross-card 메타 단순)
| 타이밍 | 카드 | witness 후보 | 근거 |
|---|---|---|---|
| **OnLoseSecurity** (73) | player-scope EB, 다수 단순 draw/gain | BT9_016·BT18_039·AD1_017 | 양 많고 payload 단순({Player}) |
| **OnMove** (30, median 16줄) | permanent EB/SS | BT6_088·ST24_04·BT8_092 | 최저 난이도, 존-전이 파생 자명 |
| **OnDiscardSecurity/Library/Hand** (14+20+34) | EB, DiscardedCards 읽기 | ST16_14·BT8_006·BT18_098 | 트래시 파생, 게이트 단순 |
| **OnAddHand/OnAddSecurity** (21+14) | EB | BT9_021·BT9_003 | 존-전이 파생 |

### Tier 2 — 중 (서브시스템 얽힘 or compound)
| 타이밍 | 카드 | witness | 근거 |
|---|---|---|---|
| **OnEndAttack** (77) | self attacker SS | BT9_043·EX8_025·BT18_079 | 양 최다급, self-scoped라 event 단순 |
| **WhenLinked** (63) | SS/EB, Link 서브시스템 | BT22_033·BT25_036·BT21_043 | Link 인프라(C-2 배선분) 재사용 |
| **OnAddDigivolutionCards** (50) | SS/EB, memory latent gap | BT9_066·BT8_066·LM_017 | [[bt2-bt3-primitive-dev]] 잔여 |
| **OnStartBattle/OnEndBlockDesignation류 전투** | OnStartBattle·OnCounterTiming·OnBlockAnyone·OnAttackTargetChanged·OnSecurityCheck | BT20_052·BT18_099 | 전투 파이프라인 메타 스레드 |

### Tier 3 — 난 (치환/방지 shape, median 100줄+)
| 타이밍 | 카드 | 근거 |
|---|---|---|
| **WhenRemoveField** (137, 최대) | self-scoped leave-hook 필요(반응=이탈 카드 자신), "시큐리티 trash로 이탈 방지"류 prevention | 최대 덩어리지만 broadcast 아닌 per-card leave-hook 설계. A-2 P1-1/C-4 P2(pre-trash WhenRemoveField cut-in)와 연동 |
| **WhenPermanentWouldBeDeleted** (41+save 206) | would-be 치환/방지, median 107줄 | C-5 PRE 창(DeletionReplacementTiming) 확장. Evade/Barrier류 이미 배선, activated save 확산 |
| **WhenReturnto{Hand,Library}Anyone** (각 9, median 131) | 복귀 예정 치환 | 난이도 최상위, 양 대비 비쌈 후순위 |

### 후순위/단발
OnPermamemtReturnedToHand(2)·OnReturnCards*(2+2)·AfterEffectsActivate(2)·WhenDigisorption(1)·OnFaceUpSecurityIncreased(1)·WhenTopCardTrashed(1)·WhenUntapAnyone(1)·OnUseDigiburst(1) — 단발 또는 소량, 개별 처리.

---

## 4. 회귀 방어 (확산이 건드리는 8 민감 지점)
1. **collapse/batch-id**(D-1/D-2): 새 EB 타이밍에 "동시 배치 1회" 시맨틱 잘못 부여/누락 시 N배 과다 또는 독립배치 미발화. **dedup 골은 uncapped 픽스처로만 실증**([[d2-goal-done]] 교훈).
2. **FilterToMinimumBatch**(D-1): BatchId 스탬프는 삭제/leave 유래에만·비-0 실제 id에만.
3. **cut-in 재귀**(RD-17): 새 broadcast 타이밍은 반드시 자기캡/자연캡(자기 방출→자기 재수집 무한루프 방지).
4. **loud-guard**(ResolveBodyLiveAsync): 바운드(memory/DP) 리액터를 인터랙티브하게 만들면 NotSupportedException — 인터랙티브 리액터는 반드시 activated(SuspendedExternally).
5. **HasExecutedSameEffect 파티션**(A-1): 메인루프에 dedup 주입 금지(과다억제).
6. **enum ordinal**: 새 멤버는 반드시 끝에 append.
7. **event value 타입 필터**: string/int/bool/long만 스레드 — list/Func 메타는 원시 평탄화 필요.
8. **CanCollectAt vs CanActivateAt 분리**(2차 리뷰 상환분): collect 1회(CanUse半) vs per-pass(CanActivate半+IsEffectsDisabled) 재뭉갬 금지.

추가: **scheduler-half vs activated-half 경계**(HasActivatedEffectsAt) — 같은 타이밍 memory+activated 공존 시 이중수집 방지 유지. **BroadcastTimings allow-list(스케줄러半)와 EventBroadcast 세트(activated半) 둘 다 갱신**(비대칭 방지).

---

## 5. 로드맵

1. **M0 — 인프라 선행**(1 골): enum 정합(부재 타이밍 append) + `ScanZones` 5존 확장 + event value 원시 평탄화 헬퍼 + `TriggerTimingMap` emit 매트릭스 감사. witness=기존 브릿지 카드 회귀 + 신규 타이밍 1개 스모크. **이게 확산의 토대**.
2. **M1 — Tier 1 확산**(타이밍별 골): OnLoseSecurity → OnMove → OnDiscard* → OnAdd*. 각 witness 2~3장 + 적대 리뷰. 저위험으로 브릿지 패턴·회귀 방어 검증.
3. **M2 — Tier 2**: OnEndAttack → WhenLinked → OnAddDigivolutionCards → 전투 계열.
4. **M3 — Tier 3**: WhenRemoveField(leave-hook 재설계) → WhenPermanentWouldBeDeleted(PRE 창 확장) → WhenReturnto*.
5. **M4 — 단발 정리** + per-card 포팅 백로그(별도 트랙)로 이행.

각 골: goal+witness + 독립 적대 리뷰(렌즈: 삽입점 전수·scan-zone·게이트 driving-event 정합·회귀 8지점). C군 2차 리뷰 교훈(골-스코프 리뷰 불충분·witness 토폴로지도 AS-IS 재도출) 상시 적용.

---

## 부록 A. AS-IS EffectTiming 60 전수 분류 (요약)
- **✅ 브릿지 13**: OnUseOption·OnDeclaration·OnDestroyedAnyone·OnEndTurn·OnStartTurn·OnTappedAnyone·OnUnTappedAnyone·OnStartMainPhase·OnEndBattle·OnDigivolutionCardDiscarded·OnDigivolutionCardReturnToDeckBottom·OnAllyAttack·OnLeaveFieldAnyone.
- **DEAD 7**: OnGetDamage·OnEndAttackPhase·OnEndMainPhase·OnKnockOut·OnEndCoinToss·OnUseAttack·OnEndBlockDesignation.
- **action-wired**: OnEnterFieldAnyone(PlayCard/Digivolve, cross-card 반응부만 EB)·OptionSkill·SecuritySkill.
- **Boundary 미배선**: AfterEffectsActivate·RulesTiming.
- **SubjectScoped/self-sched 미배선**: WhenRemoveField·WhenPermanentWouldBeDeleted·OnUseDigiburst·OnEndAttack·OnDetermineDoSecurityCheck.
- **EventBroadcast 미배선(핵심)**: OnCounterTiming·OnBlockAnyone·OnStartBattle·OnAttackTargetChanged·OnSecurityCheck·WhenReturntoLibraryAnyone·WhenReturntoHandAnyone·OnPermamemtReturnedToHand·OnRemovedField·WhenUntapAnyone·OnDiscardHand·OnDiscardSecurity·OnDiscardLibrary·OnReturnCardsToHandFromTrash·OnReturnCardsToLibraryFromTrash·WhenTopCardTrashed·OnAddDigivolutionCards·WhenLinked·WhenWouldLink·OnLinkCardDiscarded·WhenWouldDigivolutionCardDiscarded·WhenDigisorption·OnDraw·OnAddHand·OnLoseSecurity·OnAddSecurity·OnFaceUpSecurityIncreased·OnMove·BeforePayCost·AfterPayCost.

전수 표(발화지점·payload·게이트·DE 필요)는 조사 산출물 참조(이 문서 작성 근거).
