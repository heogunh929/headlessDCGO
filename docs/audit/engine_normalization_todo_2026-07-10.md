# 엔진 정상화 TODO (2026-07-10 기준)

Stage 5 창-루프 컷오버 **전체 완료**(PR#9, 391/391·RuleAudit 0) 시점의 잔여 정상화 항목.
출처: `rule_deficiency_remediation_design_2026-07-09.md`(이연 L1~L8 · P1 레지스터 · VR 재검수) + Stage 5 3b-iii 적대검수 신규 발견.
원칙: [[check-asis-before-implementing]] · [[result-equivalence-not-completion]] · [[adversarial-review-before-cutover]] · [[fidelity-over-coverage]].

**현행 상태(2026-07-11 갱신, main `812d7966`)**: **A군 전량(A-1~A-4) + 정밀 debt(RDx-A3·A-2 task6/7) ✅** · **B-1(P1-3)·B-2(P1-5)·B-3(P1-6)·B-4(P1-7) ✅ 완료**(회귀 395/395·RuleAudit 0). **잔여 B**: **B-5=대형 uniform 이관(별도 집중 세션, 설계 `uniform_activated_primitive_design.md`)**. **다음 = C군 또는 B-5(별도)**. A-1의 5개 컷인 창 배선은 debt 아닌 별도 포팅 태스크(창 미존재).
> **C/D/E 재검증(2026-07-11)**: "포팅 時/인프라 대기/blocked"로 미룬 C-1·C-2·C-5·D-2·E-1·E-2·E-3 전량 AS-IS 조사 결과 **genuinely blocked 0건** — 정의·호출부·witness 실재 + 헤드리스 부분 scaffolding. E-1(reveal peek)·E-2(AddTrashTopCardAtTurnEnd)는 **원 전제가 거짓**(발산 없음/정의 실재)이라 패리티 테스트로 재분류. 착수 우선순위(가치·명확발산): **C-5(시큐리티 PRE, witness ~86장) > C-2 TODO-98(라우팅 2건) > E-3(producer 2장)**.
> **BT24_049 실 카드 witness 보류(2026-07-11 지시)**: B-3 회귀는 fixture(`TfxOncePerTurnInteractiveTrash`)로 완결. 실 카드 witness로 BT24_049(②Fortitude+⑤[Once Per Turn] security-trash) 포팅을 시도했으나, 효과 ①(`AddSelfDigivolutionRequirementStaticEffect`)은 포팅된 caller 0개→미러 부재, ③④(compound suspend+min-DP 조건부 bounce)는 compound 프리미티브 자체 부재. "프리미티브 미러가 다 없으면 실 카드 테스트 보류" 지시로 **BT24_049 스켈레톤 유지**. 재개 조건=①의 alt-digivolve 미러 확보 + ③④ compound 프리미티브 구축.

---

## ✅ Stage 5로 해소된 항목 (재작업 불요)

| 항목 | 해소 내용 |
|------|-----------|
| **L4 / P1-2** RD-12 수집-소모 실피해 3종 | 소모를 수집→commit(실행 직전)으로 이동(`SchedulerCommit`) |
| **L5** RD-13 트리거 경로 optional | 창 인라인 yes/no(`ConfirmOptionalAsync`→WindowChoice)로 통합 |
| **P1-1** 같은-창 재평가 소실 | `GateLive`가 매 pass 스택 전체 재평가(re-entrant) |
| **VR-1**(부분) fizzle 소모 롤백 | 소모-시점 이동으로 fizzle 前 미소모(commit-recheck fizzle=미소모) |

---

## A. Stage 5 직후 마무리 (창-루프가 열어둔 잔여) — **✅ 전량 완료**(A-1~A-4 + 정밀 debt, main b0569133)

- [x] ~~**A-1 · P1-4** 컷인 창 same-effect dedup 미러 부재~~ — **완료**(커밋 1525b2e6, opt-in 인프라 선행)
  - AS-IS `HasExecutedSameEffect` skipCondition(AutoProcessing.cs:623-627)은 **컷인 창 5곳에만** 전달(CardController.cs:727·990·5189·5301·5709=SelectCount/TrashDigivolutionCards/TrashLinkCards/Unsuspend), 메인 트리거 창(AutoProcessing.cs:137)은 `null` → **dedup은 컷인 창 한정**. 헤드리스는 현재 메인 루프 창만 WindowResolver로 구동(=AS-IS 메인=dedup 없음)이라 라이브 발산 아님(latent).
  - 구축: `WindowResolverDeps.SkipCondition`(opt-in) + `WindowContinuation.Resolved`(=skillInfos_used, commit 누적) + `WindowResolverWiring.HasExecutedSameEffect`(IsSameEffect→EffectId 동치). `BuildSchedulerDeps`(컷인 창)가 전달, 메인 루프는 null(과다억제=발산 방지). 5개 컷인 창이 WindowResolver로 배선될 때 skipCondition 전달만 하면 됨.
  - (결정) AS-IS `IsCutInEffectUsedMaxCount`(:1095-1098)는 `count<ChainActivations`를 skip 조건으로 써 ChainActivations>0 효과가 **영구 skip되는 역-부호 死경로**(+`IsCutInEffectHasUsed` 하드 false) → **미러 안 함**; `ChainLimit`은 runaway 안전바운드만.
- [x] ~~**A-2 · L8 / RD-6** 턴 종료 시퀀스 정합~~ — **완료**(part1 커밋 ee1c255b + part2; 적대검수 clean·REAL BUG 없음)
  - **라이브 버그 상환**: BT1_021(EoTLose3Memory=TriggeredGainMemoryEffect)이 flip 後 방출돼 새-턴 프레임서 `TurnPlayerId==Owner` 가드에 걸려 **완전 no-op**(메모리 -3 상실). EndTurn→EndTurnAsync 재구조화로 OnEndTurn 창을 flip **前** 구 프레임서 동기 drain(DrainEndOfTurnWindowAsync=AutoProcessAsync 루프, RunToStable 재진입 없음). 인터랙티브/다중-트리거 EoT는 A-4식 loud-guard(latent — 등장 시 재적용+fired-marker).
  - **선재 이중수집 fix**(part1): OnEndTurn scheduler 효과가 scheduler 바인딩+activated 마커 양쪽 수집→order-choice(post-flip no-op에 가려짐, pre-flip drain이 노출). resolver 도메인=IActivatedCardEffect이므로 `HasEffectsAt`→`HasActivatedEffectsAt` 교체(scheduler-only 제외). 적대검수: 전 40+ resolver case가 IActivatedCardEffect·이중구현 없음 확인.
  - **턴지속 재검**(part2, AS-IS EndTurnProcess:714): drain 후 `memory<=-threshold` 재검(=`NonTurnPlayer.MemoryForPlayer>=TurnEndMinMemory` 미러); 미달 시 MemoryPass→Main 복귀(턴 지속, flip/cleanup 없음). uncapped gain 무한루프는 AS-IS 공유(실카드=loss라 무해).
  - **#67 양측 스캔**(part2): `ResolveTurnEndMinMemory`를 turn-player→양측 스캔+GetMinMemory fold(last-write-wins, SET 카드 정합). 현 producer 0(BT14_081/BT17_069 스켈레톤)이라 latent 인프라.
  - 테스트 `RD6-EndTurnSequence`(BT1_021 pre-flip 상대+6 · EoT gain 턴지속). 회귀 392/392·RuleAudit 0. GR-006 셋업을 현실적 MemoryPass(memory≤-threshold)로 정정.
  - ~~인터랙티브/다중-트리거 EoT 재적용 경로(fired-marker)~~ — **✅ 해소**(residual-debt-cleanup, task 6): loud-guard(NotSupportedException)→attack-창식 early-return + `WindowResolutionController.EndOfTurnDrainedTurn` per-turn 마커(emit-once). drain suspend 시 창 park→에이전트 resolve(WindowChoice resume, 구 프레임)→EndTurn 재적용(재-emit 없이 재검+flip). continue/flip서 마커 클리어. 테스트 RD6-EndTurnSequence(2× TfxEndTurnDraw order-choice→suspend→재적용→flip).
  - ~~**잔여 sub-order(latent, task 7)**: AS-IS는 [End of Turn] 창(:699)이 어택 루프(:705)보다 先인데 헤드리스는 attack TryOpen 先~~ — **✅ 해소**(residual-debt-cleanup, task 7): `EndOfTurnEffectAttack.TryOpen`을 OnEndTurn drain **後**로 이동(AS-IS window-then-attack). 공통 케이스 무영향(빈 drain 후 offer). 테스트 RD6-EndTurnSequence(BT1_021 -3이 Vortex 어택 offer 前 적용→memory -6). **A-2 잔여 전량 해소.**
- [x] ~~**A-3** (신규 latent) `HasEffectsAt` collect-time 비대칭~~ — **재평가·정정 완료**(적대검수 2026-07-10, 커밋과 동승 주석 정정)
  - **원 전제("collect 1회 필터→per-pass 재스캔 필요")는 기각**: `HasEffectsAt`는 효과-존재(`CardEffects(timing).Count>0`)이고 AS-IS도 존재를 **collect 1회**만 포착(GetSkillInfos/EffectList, AutoProcessing.cs:770-857); 루프는 이미-스택된 항목의 `CanActivate`만 per-pass 재검(MultipleSkills.cs:122/164-165), 원 timing 존재를 재수집 안 함. 따라서 **collect-time 필터가 AS-IS 정합**이고, per-pass로 옮기면 AS-IS가 수집 안 하는 항목을 admit해 **오히려 발산**. board-의존 효과-리스트는 AS-IS도 못 잡는 공유 한계.
  - ~~**진짜 발산(RDx-A3 debt, latent)**: `MarkerGate`가 활성 마커의 CanActivate/CanResolve를 per-pass 재검 안 함~~ — **✅ 해소**(residual-debt-cleanup): resolver uniform 게이트의 resolveCtx 구성을 공용 `BuildUniformResolveContext`로 추출, `CanActivateAt(card,timing,drivingEvent)`을 resolver·MarkerGate 양측서 호출(공용 재사용→over/under-게이팅 없음). MarkerGate가 이제 per-pass CanResolve 재검(SchedulerGate·AS-IS MultipleSkills:122/164-165 대칭). uniform=자체 CanResolve, 비-uniform=활성 가능. 테스트 Stage5-ActivatedBridge #6(존재 vs 조건 distinction) + cap 테스트 실카드화(TfxUnsuspendDraw/TfxWinBattleDraw).
- [x] ~~**A-4** (신규 문서화됨) scheduler-body-suspend 인터랙티브 리액터~~ — **완료**(F3 불변식 강제 추가)
  - 인터랙티브 bound 트리거 리액터: `ResolveBodyLiveAsync`가 scheduler-suspend를 `NotSupportedException`으로 하드-강제(기존 :142-148) — 인터랙티브 리액터는 activated effect여야(SuspendedExternally, 이미 동작).
  - F3 `RuleProcessAsync` mid-window 비-인터랙티브 불변식: 기존엔 주석만 → **loud throw로 강제**(WindowChoicePendingException/DeferredChoicePendingException catch→NotSupportedException). silent-drop 대신 가시적 실패.

---

## B. RD-12/13 소모 정합 잔여 (선언형 / 재-스택 / 환불)

- [x] ~~**B-1 · P1-3** Consume 재실행 계약 위반(latent P0)~~ — **✅ 완료**(residual-debt-cleanup)
  - `ActivatedEffectResolver` uniform case가 `OnceFlags.Consume`를 body **前**에 실행 → capped **인터랙티브** body가 suspend 시 cap 소모됐는데 resume(ResolveAsync 재invoke)서 CanActivate 재검이 소모된 cap을 false로 읽어 효과 증발+use 소진. **fix: Consume을 body 完走 後로 이동**(window의 SchedulerCommit=F5와 별개; suspend 시 미소모+un-flushed sink 폐기, resume 완주서 1회 소모). 테스트 B1-OncePerTurnInteractiveResume(suspend 미소모→resume 트래시→재-resolve no-op) + 픽스처 TfxOncePerTurnInteractiveTrash. 회귀 392/392·RuleAudit 0.
- [x] ~~**B-2 · P1-5** 선언형 메인 활성화 = 선언 시점 소모~~ — **✅ 완료(선행구축, 2026-07-11)**: [Main] 스킬 선언 ACTION 서브시스템 신규 구축
  - AS-IS: `Permanent.CanDeclareSkillList`(Permanent.cs:1618) → 각 배틀 퍼머넌트 `EffectList(OnDeclaration)`를 `ActivateICardEffect`·`CanUse` 필터(295 카드 사용). `SetActSkill`(TurnStateMachine.cs:3061)로 1개 선택 → 메인루프(1174-1195): `SetIsDeclarative(true)` 後 `MaxCountPerTurn<100`이면 body 前 `RegisterUseEffectThisTurn`. `CanUse=CanTrigger&&CanActivate`, CanActivate가 cap(isOverMaxCountPerTurn) 포함.
  - **선행구축 사유**: 이미 포팅된 BT1_088/089가 이 액션 부재로 [Main] 효과 미등록(STOP). [[no-callsite-not-skip-reason]]/[[strong-model-prebuild-latent-infra]] 원칙 → 사용자 지시로 지금 구축.
  - **구현**: 신규 `ActivateMain` 액션(타입/팩토리/`MainSkillActivateActionPayload`/`MainSkillActivateValidation`) + `MainSkillActivateAction`(GetLegalActions=배틀 퍼머넌트별 `ActivatedEffectResolver.CanDeclareAt(OnDeclaration)` 게이트; ProcessAsync=`ResolveAsync(OnDeclaration)` + interactive suspend/resume via `DeferredActivations.Suspend`). 배선: `HeadlessLegalActionDispatcher`(Main 페이즈) + `MetadataActionProcessor` 디스패치 + `LegalActionSetValidator` 허용집합.
  - **소모 로직 = 변경 불필요(result-equivalent)**: AS-IS register-before-body + body의 `if(!executed)RemoveUse()`는 resolver의 consume-after-if-executed(B-1+B-4)와 등가. consume-after가 헤드리스 resume 모델에서 유일하게 안전(consume-before는 재개 시 RD-12 게이트가 소모된 cap을 capped-out으로 읽어 body 스킵 = B-1 버그 재유발).
  - **legal-move 게이트 = 새 `CanDeclareAt`**: AS-IS `CanUse`(cap 포함) 미러 — uniform은 `CanResolve`+`OnceFlags.CanActivate`(cap), **DigiBurst는 `TrashableDigivolutionCount≥Count`**(AS-IS CanDigiBurst, 지불불가 phantom offer 방지, 적대리뷰 Finding 2).
  - **attack-proxy 제거**: `AttackPermanentAction`의 OnDeclaration emit(공격선언 stopgap) 삭제 — AS-IS 공격은 OnDeclaration 미emit, 이제 실 액션 존재 → ST4_13 [Main] Digi-Burst 이중발화 방지. OnDeclaration 브릿지 분류는 유지(직접 emit 경로).
  - **테스트**: `B2-MainSkillDeclare.Tests`(offer→resolve+consume→capped-out+illegal재처리→reset→own-scope→unpayable-DigiBurst 미offer, 4/4) + `TfxMainDeclareDraw`/`TfxMainDigiBurstDraw` fixture + `PRIM-P0.NewTimingsFire` 회귀가드(공격≠OnDeclaration). 회귀 395/395·RuleAudit 0. 적대리뷰 2건(회귀·DigiBurst게이트) 상환.
  - **잔여(design item B2-05)**: per-skill-index 선택 미구현(퍼머넌트당 1 액션, OnDeclaration 스킬 전부 resolve) — 다중 [Main] 스킬 카드 포팅 시 필요(resolver의 per-index resolve도 함께). 현 포팅 풀 전량 단일 스킬이라 무영향.
- [x] ~~**B-3 · P1-6** 재-스택 use 리셋 부재~~ — **✅ 완료**(residual-debt-cleanup)
  - AS-IS `CardSource.Init()`(CardSource.cs:345-347)이 `InitUseCountThisTurn`(UseEffectsThisTurn 클리어)를 새 CardSource(=enter-play/이동) 시 호출. 헤드리스는 카드 use를 인스턴스로 키잉해 재-진입서 stale use 잔존. fix: enter-play 훅 `CardEffectRegistrar.RegisterCard`가 `OnceFlags.ResetForCard(owner, instanceId)` 호출(재-플레이/de-digivolve/re-stack 포괄, 첫 플레이는 0 제거=no-op). `OnceFlagHelpers.ResetForCard`(키 `{owner}:{source}:` prefix 매칭, per-card). 테스트 B3-RestackUseReset(re-enter→해당 카드만 리셋). 회귀 검증 중.
- [x] ~~**B-4 · P1-7** RemoveUse 환불 프리미티브 부재~~ — **✅ 완료**(residual-debt-cleanup)
  - AS-IS 10+장(AD1_024:265·BT14_029:114)이 `if (!executed) RemoveUse()`로 body 미실행 시 캡 환불. fix: `ActivatedEffect.ResolveBodyAsync`가 `bool executed` 반환(인터랙티브 선택 IsSkipped→false), resolver uniform case가 **executed일 때만 Consume**(B-1 consume-after-body와 결합). 현 헤드리스 body는 전부 canSkip:false라 latent이나 skippable body 포팅 시 발화. 테스트 B1-OncePerTurnInteractiveResume(#2: skip→환불→재발화) + 픽스처 TfxOncePerTurnOptionalTrash(canSkip:true).
- [ ] **B-5 · P1-8** per-shape optional/cap 우회 — **별도 집중 세션 예정**(대형)
  - IsOptional/MaxCountPerTurn이 uniform ActivatedEffect 전용 — resolver ~12 per-shape 케이스(ActivatedSelect·TargetBuff·SelectFromZone …)는 캡·yes/no 없음(IActivatedCardEffect 인터페이스 비어있음). uniform 프리미티브(`ActivatedEffect`)는 이미 존재 → **각 per-shape를 IEffectBody로 이관 + 그 shape 사용 카드 갱신**(다-shape·다-카드 마이그레이션). 설계 `uniform_activated_primitive_design.md`, [[asis-uniform-activateclass]].

---

## C. RD-4 삭제 / 진화원 트래시 잔여

> **AS-IS 실태 검증(2026-07-11, 3-way 병렬 조사)**: C/D/E "잔여" 대부분은 **genuinely blocked 아님** — AS-IS 정의·호출부·witness 카드 실재 + 헤드리스 부분 scaffolding 존재. "포팅 時/인프라 대기" 프레이밍은 과보수적이었음. 아래 각 항목에 witness·호출부·정정 판정 반영. E-1·E-2는 **원 전제가 사실과 달라**(발산 없음/정의 실재) 재분류.

- [ ] **C-1 · L6 잔여 / TODO-96** Decode/Partition PRE-이동 — **지금 구축가능**(POST→PRE 타이밍 이동)
  - AS-IS: would-be-deleted 창(`CardController.cs:3696` WhenPermanentWouldBeDeleted, `:3705` WhenRemoveField 前)서 자기 스택 진화원을 무료 플레이 → 그 뒤 `DiscardEvoRoots`(`Permanent.cs:106`)가 잔여 트래시. `Decode.cs:27/104`(WhenRemoveField 등록), `Partition.cs:43/71`, `Permanent.HasPartition`(`Permanent.cs:3113`).
  - 헤드리스: **POST로 구현됨**(`DeletionReplacementTiming.cs:64-66` Decode/PartitionOption=effect-deletion POST), `DeletionSourceTrash.cs:16-18`가 TODO-96(PRE 재구조화)로 명시. → POST→PRE 이동 + 잔여 소스 무조건 트래시.
  - witness: Decode ≈16(BT19_024·BT22_021·P_214)·Partition ≈13(AD1_011·BT16_012·BT16_036). Save/Fortitude는 P0-3서 상환済.
- [ ] **C-2 · L7 / TODO-98** ACE-소스 Overflow · LinkedCards 트래시 — **지금 구축가능**(좁은 라우팅 2건)
  - AS-IS: `AceOverflowClass.Overflow()`(`CardController.cs:5836`)=이탈 un-flip ACE당 소유자 메모리 -OverflowMemory. `DiscardEvoRoots`(`Permanent.cs:106-134`)가 evoRoots+linkRoots 둘 다 Overflow 후 evo=AddTrashCard·link=RemoveLinkedCard. `IsACE=>OverflowMemory>=1`(`CEntity_Base.cs:74`).
  - 헤드리스: **top-card overflow·Link 모델 완료**(`AceOverflowGate.cs`, `LinkHelpers.cs`, 테스트 G9-042/G9-056). **소스-경로만 갭**: `DeletionSourceTrash.cs:17-18`가 "이탈 ACE 소스 overflow(TODO-98) + host LinkedCards trash 미처리" 명시. → delete-path 소스를 `AceOverflowGate.OverflowFor`로 라우팅 + delete 시 `LinkHelpers.RemoveLinkCardAsync`.
  - witness: Link ≈71(AD1_005·BT22_075); ACE는 데이터플래그(isAce/overflowMemory 메타, G9-042 합성 선례).
- [ ] **C-3 · P1-9** 보호필터 밀수(latent): `CanNotTrashFromDigivolutionCards`가 DiscardEvoRoots에 혼입 — 보호 키워드 producer 포팅 前 전용 무필터 경로 분리.
- [ ] **C-4 · P1-10** battle knock-out 창이 트래시 前 해소 — AS-IS는 소스+톱 트래시 後 해소. RD-4 전체 시퀀스 재설계 時.
- [ ] **C-5 · VR-6 (=RD-7 Part B)** 시큐리티 배틀 PRE would-be-deleted 창 — **지금 구축가능**(시큐리티 loss를 BattleResolver로 통합)
  - AS-IS: 시큐리티 배틀도 field와 **동일** 경로 — `IBattle.Battle()`→`DestroyPermanentsClass.Destroy()`(`CardController.cs:4165→4705→3696`)가 PRE 창(WhenPermanentWouldBeDeleted) 개방. Evade/Barrier/Fragment/Scapegoat가 이 타이밍에 등록+`willBeRemoveField=false`(`Evade.cs:43/77`·`Barrier.cs:37/99`·`Fragment.cs:67`·`Scapegoat.cs:57`).
  - 헤드리스: field 배틀은 `BattleResolver`(DeletionReplacement, `BattleResolver.cs:109-273`)로 **완료**. **시큐리티만 미경유** — `SecurityResolver.cs:397-401`이 "Evade 공격자가 시큐리티전서 죽음(field전선 생존), RD-7/VR-6서 BattleResolver 통합 시 해소" 명시. 로직 재구축 아님, 경로 통합.
  - witness 최다: Evade ≈13(BT13_023·BT14_021)·Barrier ≈56·Scapegoat ≈17·Fragment 2(EX10_034·EX11_044). 명확 발산=Evade 공격자가 tie/loss인 시큐리티 디지몬에 공격 시 AS-IS 생존 vs 헤드리스 삭제.

---

## D. 삭제 배치 정밀화 (under-fire 엣지)

- [ ] **D-1 · VR-8 / F1(b)** 같은 pass 독립 2 delete-process under-fire(AS-IS 2회, 여기 1회). emission 時 delete-process batch-id 스탬프. (공통 0-DP스윕/보드와이프=단일 process는 정확.)
- [ ] **D-2 · VR-9** `OnLeaveFieldAnyone` 배치 dedup — **지금 검증가능**(witness AD1_025)
  - AS-IS: 동시 이탈 퍼머넌트 전체를 `OnDeletionHashtable`(`HashtableSetting.cs:85-131`)로 1개 payload에 묶어 `StackSkillInfos(..., OnLeaveFieldAnyone)`(`CardController.cs:3748-3756`) 1회 stack → 리액터는 **배치당 1회** 발화(N 이탈 = 1 이벤트). 호출부 다수(`:2380/2546/2711/3367/3605/3756`).
  - 헤드리스: **타이밍 emit 완료**(`TriggerTimings.cs:15` OnLeaveField, `TriggerTimingMap.cs:64-70`), per-event EffectId dedup 존재(`AutoProcessingTriggerCollector.cs:161/186`). 단 **N 동시삭제→1 배치 collapse** 시맨틱은 GameFlowProcessor서 확인 필요(현재 per-CardMoved 발화일 수 있음).
  - witness: **AD1_025(옴니몬)** 유일 — `[All Turns] 상대 디지몬 이탈 시 옵션+톱시큐리티 트래시`(`AD1/Red/AD1_025.cs:153-211`, 유일 `CanTriggerOnPermanentLeave`). self-leave(WhenRemoveField)는 별개 ≈191장.

---

## E. 트리거 잔여 (2026-07-11 AS-IS 검증 — 대부분 전제 정정됨)

- [ ] **E-1 · L1 / RD-1** 효과-구동 free-digivolve reveal — ⚠️**원 전제 거짓, 발산 없을 가능성**(패리티 테스트로 종결)
  - ~~"AS-IS는 revealed 카드를 executing-존으로 이동"~~ = **거짓**. AS-IS도 **peek만**: `RevealLibrary.RevealLibrary()`(`RevealLibrary.cs:749-790`)가 top-N을 읽되 카드는 물리적으로 라이브러리 top 유지, `IsBeingRevealed=true` 플래그만 세팅(`:789`). 선택 카드는 `PlayCardClass(root:Library)`로 라이브러리서 직접 플레이(BT1_078.cs:101-108).
  - 헤드리스: **이미 동일 peek 모델**(`RevealAndSelect.cs:74-75/137` "reveal only peeks; cards move when choice resolves"). Executing-존/TODO-68·83 대기 불필요. → BT1_078로 reveal→free-digivolve 순서 + 진화후 +1드로우(`CardController.cs:1529`) 패리티 확인 테스트, 통과 시 종결.
  - witness: BT1_078 등 ≈385(SimplifiedReveal…: BT9_020·BT9_064).
- [ ] **E-2 · L2 / RD-3** 버스트 재-진화 엣지 — ⚠️**"정의 export 부재" 거짓, 정의·구현 실재**(엣지 테스트만)
  - ~~"AS-IS `AddTrashTopCardAtTurnEnd` 정의 부재"~~ = **거짓**(이전 grep의 일본어 주석 mojibake 오판). 실제 정의 `SelectBurstDigivolutionEffect.cs:249-344`(hash "TrashBurstDigivolution", `UntilEachTurnEndEffects`, OnEndTurn 발화). 호출부 **단일** `CardController.cs:1537`(`isEvolution && _burstDigivolved && TopCard!=null` 시 버스트 top 턴종료 자가트래시). 진화 +1드로우는 `:1529`.
  - 헤드리스: **이미 구현** — `SpecialPlayAction.cs:390`(BurstTrashAtTurnEndKey), `HeadlessEndTurnCleanupFlow.cs:82-86`(due 스윕), `GameFlowProcessor.cs:289-295`(RD-3 미러 문서화).
  - 잔여 = **재-진화 엣지 테스트**(버스트 후 턴 내 재진화 시 어느 top이 스탬프/트래시되는지). witness: BT13_033·BT13_020·BT13_060(≈5장 burst).
- [ ] **E-3 · L3 / RD-2** ICanNotPlayCardEffect 연속 스캔 — **지금 구축가능**(producer 2장 실재)
  - AS-IS: 인터페이스 `CardEffectInterfaces.cs:20-23`, 구현 `CanNotPlayClass.cs:6-28`, 연속 스캔 `CardSource.CanNotPlayThisOption`(`CardSource.cs:184-248`: 전 플레이어·전 필드·자기 효과의 ICanNotPlayCardEffect 순회). 호출부 `CardSource.cs:158`(CanBePlayed)·`TurnStateMachine.cs:2076/2533`.
  - 헤드리스: **스켈레톤 스텁**(`CanNotPlayClass.cs` 본문 없음), 보드-스캔 부재. 삽입점(`OptionActivateAction`/IsOptionLocked=정적 메타플래그만) + 색요구 절반은 포팅됨(`OptionColorRequirement`). → CanNotPlayClass 구현 + 옵션/플레이 legality에 보드-스캔 + 2장 포팅.
  - producer: **BT8_057**(상대 옵션 사용불가), **EX1_072**(상대 옵션 플레이불가). 헤드리스 "producer 0"은 미포팅 탓, AS-IS엔 2장.

---

## F. 별도 대형 엔진 골 (Stage 5 밖)

- [ ] **F-1** Triggered→activated 브릿지 확산(~660 카드) — EVENT-BROADCAST 카테고리 driving-event 전달. 이름-커버리지 ≠ 완성([[triggered-activated-bridge]]).
- [ ] **F-2** 프리미티브 잔여: G11 Digisorption · OnAddDigivolutionCards 방출 · per-card 트래시보호([[bt2-bt3-primitive-dev]]).
- [x] ~~**F-3** continuous-DP P0 2건(0-clamp·isUpDown 순서)~~ — **상환済**(커밋 7cbf4fe5, DpCalculator 0-clamp 확인).

---

## 권고 착수 순서

1. ~~**A군**(Stage 5 직후 마무리)~~ — **✅ 완료**(A-1~A-4 + 정밀 debt RDx-A3·task6/7, main b0569133; 라이브 버그 A-2/BT1_021 상환).
2. **B-1(P1-3)** ← **현 최우선** — 첫 인터랙티브 capped 카드가 이걸 밟기 前 필수(latent P0).
3. 나머지 B~E는 각 명시된 카드/기능 착수 前 선행 구축([[strong-model-prebuild-latent-infra]], OPUS-only).
4. **F-1/F-2** 대형 골은 별도 트랙.
