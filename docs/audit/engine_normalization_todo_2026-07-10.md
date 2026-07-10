# 엔진 정상화 TODO (2026-07-10 기준)

Stage 5 창-루프 컷오버 **전체 완료**(PR#9, 391/391·RuleAudit 0) 시점의 잔여 정상화 항목.
출처: `rule_deficiency_remediation_design_2026-07-09.md`(이연 L1~L8 · P1 레지스터 · VR 재검수) + Stage 5 3b-iii 적대검수 신규 발견.
원칙: [[check-asis-before-implementing]] · [[result-equivalence-not-completion]] · [[adversarial-review-before-cutover]] · [[fidelity-over-coverage]].

**현행 상태(2026-07-11 갱신, main FF)**: **A군 전량(A-1~A-4) + 정밀 debt(RDx-A3·A-2 task6/7) ✅** · **B-1(P1-3)·B-2(P1-5)·B-3(P1-6)·B-4(P1-7) ✅ 완료**(회귀 395/395·RuleAudit 0). **잔여 B**: **B-5=대형 uniform 이관(별도 집중 세션, 설계 `uniform_activated_primitive_design.md`)**. **다음 = C군 또는 B-5(별도)**. A-1의 5개 컷인 창 배선은 debt 아닌 별도 포팅 태스크(창 미존재).
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

- [ ] **C-1 · L6 잔여** Decode/Partition PRE 이동 (TODO-96 전체 정합). Save/Fortitude는 P0-3서 상환済.
- [ ] **C-2 · L7** ACE-소스 Overflow(TODO-98) · LinkedCards 트래시 — ACE-소스/Link 카드 포팅 時.
- [ ] **C-3 · P1-9** 보호필터 밀수(latent): `CanNotTrashFromDigivolutionCards`가 DiscardEvoRoots에 혼입 — 보호 키워드 producer 포팅 前 전용 무필터 경로 분리.
- [ ] **C-4 · P1-10** battle knock-out 창이 트래시 前 해소 — AS-IS는 소스+톱 트래시 後 해소. RD-4 전체 시퀀스 재설계 時.
- [ ] **C-5 · VR-6 (=RD-7 Part B)** 시큐리티 배틀 PRE would-be-deleted 창(Evade/Barrier/Fragment/Scapegoat) 미개방 — SecurityResolver는 POST Fortitude만. RD-7 시큐리티 배틀 공용화 時.

---

## D. 삭제 배치 정밀화 (under-fire 엣지)

- [ ] **D-1 · VR-8 / F1(b)** 같은 pass 독립 2 delete-process under-fire(AS-IS 2회, 여기 1회). emission 時 delete-process batch-id 스탬프. (공통 0-DP스윕/보드와이프=단일 process는 정확.)
- [ ] **D-2 · VR-9** `OnLeaveFieldAnyone` 배치 dedup 미러(AS-IS CardController:3746 동일 배치) — board-wide leave-field 리액터 포팅 時(현재 BroadcastTimings 부재로 가려짐).

---

## E. 카드 포팅 시점 트리거 (인프라 대기)

- [ ] **E-1 · L1** RD-1 효과-구동 free-digivolve 드로우 — reveal이 peek만(미이동 카드 드로우 발산, BT1_078). Executing-존/reveal-제거 모델(TODO-68/83) 랜딩 時.
- [ ] **E-2 · L2** RD-3 버스트 재-진화 엣지 — AS-IS `AddTrashTopCardAtTurnEnd` 정의 export 부재. AS-IS 정의 확보 / 재-진화-후-버스트 카드 포팅 時.
- [ ] **E-3 · L3** RD-2 ICanNotPlayCardEffect 연속 스캔 — 스켈레톤, producer 0. CanNotPlay/PutField producer 카드 포팅 前(TODO-49).

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
