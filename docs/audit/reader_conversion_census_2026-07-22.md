# Reader 전환 캠페인 census — EffectRegistry 판독-half 사문 판정 (2026-07-22)

Base: `10cfc98d`(W3c-final 4차). 방법: 코디네이터 직접 실측 + Opus census 2기(생산자-측 ToBinding 전수 / 판독-측 29좌석 질의-종류 해석) 조인. 규약: [[dead-judgment-needs-asis]] — 모든 사문 판정에 AS-IS 발화 경로 확인 동반.

---

## §1. 생산자-측 최종 실측 (registry 유입 전수)

`EffectRegistry.Register` src call-site = **정확히 5** (SpecialPlayRecipeRegistry는 별개 레지스트리):

| 좌석 | 실카드 feed | 판정 | emit 프로파일 |
|---|---|---|---|
| Commons:3004 `GainRestrictionToPermanent` | CanNotAttack 6장·CanNotBlock 4장·CanNotUnsuspend 2장 | **LIVE** | role=Continuous·scope=`"ContinuousRecalculation"`·restriction key+joint 술어·duration 태그·effect=null |
| Commons:3137 `GainToPlayerScope` | CanNotAttackPlayer 2장·CanNotUnsuspendPlayer 1장·ChangeSecurityDigimonCardDPPlayerEffect 2장(ST1_14·ST3_13, Modifier scope=동일 리터럴) | **LIVE** | 동상 + `SecurityCardDpDeltaKey`; keyword 경로(Alliance)=실카드 0 |
| Commons:1520 `StartOfMainAttack` | 호출자 0 (src+tests 전수) | **사문** (AS-IS home 파일=스켈레톤) | — |
| Commons:2912 `AddSelfRemovalEffectToPermanent` | src 0·tests 1(PRIM-P0.GrantTriggeredToPermanent) | **테스트-전용** | — |
| Registrar:236 enter-play 하강 | 실카드 통과 클래스 **0** | **테스트-전용** | 브릿지 통과 가능=연속 8종+TfxTriggeredMemoryEffect뿐, 전원 테스트/픽스처 전용 |

registrar 브릿지 소거 근거(Opus census): ActivatedEffects 18종 ToBinding=전부 `throw`이고 `IActivatedCardEffect`는 :221에서 선차단. KeywordBaseBatch 인스턴스/정적 ToBinding·SkillInfo:87=시그니처 불일치로 브릿지 도달 불가, 생산 호출자=테스트 전용(RegisterBaseBatch1/2는 기삭제). 연속 8종(ContinuousSelfRestriction/PlayerScopeRestriction/SelfKeywordByName/PlayerScopeKeyword/PlayerScopeTriggerGrant/PlayerScopeModifier/SpecialPlayRecipeMarker/JointRestriction)의 생성 사이트=전부 테스트/Tfx (실카드 0).

**결론: 실카드가 registry에 넣는 것 = role Continuous + `"ContinuousRecalculation"` scope 술어 바인딩(restriction/joint/SecurityDP)뿐.** keyword·trigger-timing·effect-payload·타 scope·타 role 바인딩의 실카드 생산 = 0.

## §2. 판독-half 판정표 (29+α좌석)

리터럴: RestrictionGate.Scope=ModifierGate.Scope=EffectInvalidation.Scope=`"ContinuousRecalculation"`, CanNotPlayOptionScan.Scope=`"CanNotPlayOption"`, MaxDpDeleteScope=`"DeleteThreshold"`.

### (A) 사문-즉시 (생산자 0 + 호출자 0 — delete-first)
| 좌석 | 근거 | 테스트-핀 |
|---|---|---|
| InheritedGrantedSecurityHelpers.Query :185/:403/:409 + role-switch :211-218 | 외부 호출자 0 (GetModifierEffects 전 코드베이스 유일 소비자 포함) | G3L-002 |
| ReplacementHelpers.QueryReplacements:395 | 호출자 0 | G3I-001/002(클래스 참조) |
| RestrictionHelpers.QueryRestrictions:351 | 호출자 0 | G3H-002 등(클래스 참조) |
| ContinuousKeywordGate.KeywordGrantAcceptsSubject:209 | 호출자 0 | GR-007(클래스 참조) |
| EffectDurationExpiry.ExpireUnsuspend:54 | src 호출자 0 | G3.5-CVA1:103-104 |
| EffectRegistry.HasEffect / 외부 .Clear | 소비자 0 | — |

### (B) 사문-확정 registry-half (생산자 0, 소비 코드는 live — half 절제)
| 좌석 | 생산자-0 근거 | AS-IS live 경로(형제) | 테스트-핀 |
|---|---|---|---|
| CanNotPlayOptionScan:109 region-② + :150 region-③ | 코드-내 사문 선언(이연④-f 생산자 전 flip) | region-① `EffectList(None)`→`ICanNotPlayCardEffect` | E3-Witness |
| ExpireFixedCostCalc:66 + 6 call-site(TurnFlowPump:316·CardController:3639·액션 4종) | `UntilCalculateFixedCost` 바인딩 생산 0 (cost=순수 AS-IS `Player.UntilCalculateFixedCostEffect` 버킷, W3c-final 4차) | 버킷 clear는 각 choke에 기존재 | G3.5-F17(기-재조준)·CVA1 |
| MaxDpDeleteThreshold:4647 + Scope/DeltaKey | `MaxDpDeleteDeltaKey` 생산 0 | **AS-IS 정본 실재**: `Player.MaxDP_DeleteEffect`(Player.cs:425, IChangeDPDeleteEffectMaxDPEffect live 스캔) — BT9_009·BT20_017·EX8_074은 이미 정본 사용. **재조준 대상 4**: BT2_013·BT2_091·ST1_15·TfxWhenDigivolveDelete (AS-IS 원문도 `card.Owner.MaxDP_DeleteEffect(N, activateClass)` — 재조준=fidelity 회복) | CardEffect.ST1.Red·G9-009 |
| EffectInvalidation 전체(:38 card-targeted·:50 player-scope) + 소비 2좌석(ContinuousScopeEvaluation:99·ActivatedEffectResolver:167) | `DisableEffectsKey` 생산 0·포팅 corpus 사용 0 | AS-IS 기제=`DisableEffectClass`/`CheckEffectDisabledClass`/`IDisableCardEffect`(미포팅 — 발명 registry 모델과 이형). 은퇴+design item(RD-RC-01) | G3.5-D7 |
| DeletionReplacementTiming GetEffects :60/:80/:256 (PRE HasPreOption-half) | `WhenPermanentWouldBeDeleted` 타이밍 바인딩 생산 0 — 실카드(EX8_028 등)=new-model로 등록 없음, Tfx would-be-deleted 픽스처=주석으로 "no EffectRegistry binding" 명시 | 창 수집(GetSkillInfos→DeletionReplacementGate 창) | PRIM-P0.WouldBeDeletedWindow·C-Del-PRE·G3.5-F68 |
| keyword-half: KeywordGate:119 registry arm·:239 overload·CardLeavePlayCleanup:132(Partition)/:147(Decode) snapshot | keyword 바인딩 실카드 생산 0 (배치 클래스=생산-사문·Alliance 실카드 0) | `NewModelContinuousScan.HasKeyword` + 창 수집(Decode/Partition=ActivateClass) | GR-005·G9-062·G9-028/032/060·R2-DeletionPipeline |
| trigger-half: SchedulerResolver:30 Find·GameFlowProcessor:1146 Find·AutoProcessingTriggerCollector(GetEffectsForTiming) | trigger 바인딩 실카드 생산 0; 유일 feed=TfxOnDelete/OnPlayGainMemory→TfxTriggeredMemoryEffect(픽스처 2). collector=프로덕션 배선 0(테스트 13스위트 직생성) | 트리거 파이프라인=new-model 수집 | collector 13스위트+Tfx 2픽스처 스위트 |

### (C) joint2-의존 (이 캠페인 스코프 밖 — 재개지점 ②에서 재하우징)
ContinuousScopeEvaluation :49/:66/:307/:324 · PlayerScopeContinuousHelpers:78 · ContinuousEffectEvaluator:161 · RestrictionScan:48/49 · Sink:1756(fallback) · SecurityResolver:890(SecurityDP — ST1_14/ST3_13 실feed) · 만료 sweep TurnEnd:27/BattleEnd:40/AttackEnd:47 · cleanup RemoveWhere 6좌석(Sink×3·LeavePlay:53·registrar:128·MindLink:120).

## §3. 배치 설계 (delete-first 컴파일러-구동, 통합 배치)

| 배치 | 내용 | 게이트 |
|---|---|---|
| RC-1 | (A) 전량 원자 전삭 + G3L-002·CVA1(:103-104)·해당 참조 테스트 재조준 | build+관련 스위트 |
| RC-2 | MaxDpDelete inert-소생: 호출 4좌석 AS-IS `Player.MaxDP_DeleteEffect` 재조준(+causing activateClass 복원)→발명 reader+const 삭제 | BT2/ST1 스위트+행동 witness |
| RC-3 | EffectInvalidation 은퇴(+RD-RC-01 design item)+소비 2좌석 절제+G3.5-D7 재조준 | 관련 스위트 |
| RC-4 | CanNotPlayOption registry-half(:109 region-②·:150 region-③)+PRE GetEffects-half(:60/:80/:256) 절제+핀 재조준 | E3·PRE 스위트 |
| RC-5 | keyword-half 절제(KeywordGate:119 arm·:239·snapshot :132/:147)+배치-클래스 핀 재조준 | GR/G9 keyword 스위트 |
| RC-6 | trigger-half: Tfx 2픽스처 new-model 재조준→collector 13스위트 재조준→Find 2좌석+collector 절제 | 해당 스위트 |
| RC-7 | 통합 게이트: 전체 스위트 1회(base 재사용 비교)+다이제스트 스팟(1000/1001/1002)+적대리뷰 | T2 |

주의: (C)는 삭제 금지 — ②joint 재하우징 전까지 실카드 live. `.Register` 5좌석 자체도 이 캠페인에서 불변(생산자 청산은 ②③).
