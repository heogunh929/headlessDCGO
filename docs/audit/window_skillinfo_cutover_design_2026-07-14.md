# 창엔진 SkillInfo 표현형 이관 설계 (RD-R3W1b-01 해소 골)

2026-07-14/15. 상위 설계=`bigbang_redesign_2026-07-14.md` R3. 2회 독립 STOP(R3-B RD-R3-01, R3-W1b RD-R3W1b-01)으로 확정된 "창 컷오버 독립 region-골"의 실행 설계.
케이던스: 본 설계문서 → 배치 구현(opus/sonnet 서브에이전트, 코디네이터 AS-IS 라인 대조) → 전체 diff + 적대리뷰.

## 0. 골 정의

**현행(발명물)**: 창 통화=`TimingWindowTrigger`(registry EffectId 키) · 루프=`WindowResolver` 프레임 스택 · 수집=`AutoProcessingTriggerCollector`(EffectRegistry 읽기)+`CollectActivatedBridgeTriggers`(zone 스캔) · 컷인=depth 프레임.

**종점(AS-IS 1:1 + 승인 ADAPTATION)**:
- 창 통화 = 미러 `SkillInfo`(live `ICardEffect`+`Hashtable`+`EffectTiming`) — `Assets/Scripts/Script/SkillInfo.cs`(이미 1:1 존재)
- 수집 = `AutoProcessing.GetSkillInfos`/`GetSkillInfosOfCards` **live 5-영역 재열거**(player효과/필드/트래시/핸드/앞면시큐리티, `is ActivateICardEffect`+`CanTrigger` 필터) — 휴면 1:1 포트 활성화
- 스택 = `AutoProcessing.StackedSkillInfos`+`PutStackedSkill`(디지몬/테이머 플래그, PermanentWhenTriggered/TopCardWhenTriggered 스탬프)
- 루프 = `MultipleSkills.ActivateMultipleSkills(_OnePlayer)` 1:1 실장(현행 STOP 스텁 대체): 턴→비턴 2-pass, `while(true)` 재-CanActivate, Blast/일반 순서선택, 해소마다 `RuleProcess`→`TriggeredSkillProcess` 재귀
- 해소 = `AutoProcessing.ActivateEffectProcess`→`Activate_Optional_Effect_Execute`(→`ActivatedEffectResolver.ResolveWithinCycleAsync`, 394a8402에서 이 목적으로 추출됨)
- 구조 = `autoProcessing`+`autoProcessing_CutIn` **2-인스턴스** + `multipleSkills` 컴포넌트 풀(available/executing) — 포트의 단일-리졸버+프레임 붕괴를 AS-IS 구조로 복원
- 컷인 계정 = `MainProcessingEffect`/`_usedCutinEffects`/`AddCutinEffect`/`IsCutInEffectUsedMaxCount`, **`IsCutInEffectHasUsed`의 AS-IS `return false` TODO까지 verbatim**
- 창 경로의 EffectRegistry 읽기 = **0** (registry 물리 삭제는 R3-W3 별도 골)

**구조 계기판(이 골의 진척 지표)**: ①창-구동 경로의 registry 참조 0 ②`IHeadlessCardEffect` 창-발화 모집단 0 ③미러 `TriggeredSkillProcess`의 NotSupportedException(P6A-STACKED-DRAIN) 제거 ④R6P-EOT-PLAYER-EFFECTLIST 해소 ⑤`WindowResolver` 창 루프 참조 0 ⑥Triggered\* 발명 프리미티브 7종 참조 0.

**비-스코프(명시)**: EffectRegistry/EffectBinding 물리 삭제(R3-W3 — 연속효과 게이트 소비자 잔존), `ISecurityCheck`/`IBattle` 본체의 CardController 재하우징(별도 골), 연속/치환 효과 경로, 기존 self-flagged 발산(RD9-87 탭 미-sink, 시큐리티 다중효과 선택루프, IDontBattle player-scope 절반, MIG1-KEYWORD-RELOCATE 등 — §6 design item 승계), EX8_074(비용 파이프라인 RD-R6-07 STOP 유지).

## 1. 조사 확정 사실 (설계 근거)

### 1.1 seam 7개 (컷오버 대상 전수)
| # | 위치 | 성격 |
|---|------|------|
| 1 | `GameFlowProcessor.AutoProcessAsync` (GameFlowProcessor.cs:628-657) | 메인 루프 시드+park (resumable) |
| 2 | `MetadataActionProcessor.ResolveChoiceAsync` WindowChoice (:507-516) | 창 choice 응답 resume |
| 3 | `MetadataActionProcessor` 링크-트림 F3 분기 (:555-562) | between-picks 룰 choice resume |
| 4 | `MetadataActionProcessor` deferred-activation resume (:736-745) | ExternallySuspended resume |
| 5 | `BattleResolver.ResolveKnockOutWindowAsync` (:447-449) | KO 동기창 (FIFO) |
| 6 | `BattleResolver.ResolveStartBattleWindowAsync` (:469-471) | 전투개시 동기창 (FIFO) |
| 7 | `SecurityResolver.ResolveSecurityCheckWindowAsync` (:380) | 시큐리티체크 동기창 (FIFO, unified seed) |

### 1.2 위험 모집단 (컷오버 게이트 = `is ActivateICardEffect`)
- **이중모델 프리미티브 7종**(TriggeredEffects.cs: TriggeredMemory/UnsuspendSelf/SetMemory/GainMemory/SelfDpBuff/RecoverTrigger/PlaySelfAtEndOfBattle — ICardEffect이지만 ActivateICardEffect 아님, 소비 카드 수십 장. **AS-IS 대조 확정: AS-IS CardEffectFactory 동명 헬퍼는 `new ActivateClass()`+SetUpICardEffect+SetUpActivateClass 반환**(CardEffectFactory.cs:63 Gain1MemoryTamerOpponentDigimonEffect 실측) → 팩토리 레벨 1:1 재작성으로 카드 무수정 fold 가능)
- **binding-전용 3종**: StartOfMainAttackEffect(AS-IS GiveEffectToPermanent/StartOfMainAttack.cs), KeywordBaseBatch1Effect(Blocker/Jamming/Reboot/Pierce — 대부분 연속/치환이라 창 비경유), KeywordBaseBatch2Effect(Rush/Blitz/Retaliation/ArmorPurge/Decode/Alliance/Vortex/Overclock/Partition/Progress — **창 경유 트리거 타이밍만 위험**: OnEnterFieldAnyone·OnDestroyedAnyone·OnEndTurn(Vortex)·OnAllyAttack(Alliance)·WhenRemoveField)
- **구모델 15파일**: 실카드 5(BT1_021 OnAllyAttack+EoT반전 / BT1_090·BT1_109 OptionSkill / BT9_043 OnEndAttack / EX8_074 STOP유지) + Tfx 10(8=구모델 리졸버 겨냥→컷오버 시 은퇴, 2=공유 인프라→재포팅)
- **DelayedOneShot player-scope**: 미러 4-arg `AddEffectToPlayer`(CardEffectCommons.cs:2935)가 registry binding으로 lower — **AS-IS 대조 확정: AS-IS AddEffectToPlayer(GiveEffectToPermanentOrPlayer.cs:57)는 player 버킷에 `Func<EffectTiming,ICardEffect>` 저장, registry 없음** → 버킷 저장 1:1 전환(=R6P-EOT-PLAYER-EFFECTLIST 해소)

### 1.3 수집 실행가능성 (확정)
- `CEntity_EffectControllerStore.Create`(CEntity_EffectController.cs:323-347)가 생성 시 `CardEffectDispatch.TryCreateForCard`로 `cEntity_Effect` 부착(AS-IS setup-attach 미러, EmptyEffectClass 폴백 포함) → **모든 존의 카드가 `EffectList(timing)` live 열거 가능**. GetSkillInfos 5-영역 스캔 급전 성립.
- `Player.EffectList(timing)`(Player.cs:241)는 R6-P 버킷 8종 병합 완료 — F3 버킷 전환만 하면 player 섹션 성립.
- **fold 산출물(ActivateClass)은 컷오버 전에도 기존 activated-bridge half(`CollectActivatedBridgeTriggers` zone 스캔)가 수집** → F-군 배치가 컷오버 전에 랜딩해도 발화 공백 없음(등록 registrar는 ActivateICardEffect를 binding-lower에서 skip: CardEffectRegistrar.cs:222). **예외 = player-scope 버킷**(activated-bridge가 player 버킷을 스캔하지 않음) → F3 본체는 컷오버 배치(C)에 동승.

### 1.4 pause/resume 기제 (승인 ADAPTATION의 현행 형태)
- 창: `WindowResolutionController`(단일 Pending continuation + choice-답변 `_answers` 재생) / 본체: `DeferredChoiceProvider`(answers+cursor 재생, 효과 재실행) / 메인루프 pause: `ChoiceController.Current.IsPending`.
- AS-IS 등가물 = 코루틴 `WaitUntil(player.HasPlayerSelection())`+`DequeuePlayerSelection`(MultipleSkills.cs:326-329, C# 스택이 지속성). continuation 외부화는 기승인 substrate ADAPTATION — **내용물만 SkillInfo로 교체**한다.

## 2. 표현형 매핑 (구 → 신)

| 구(발명물) | 신(AS-IS 정위치) | 판정 |
|---|---|---|
| TimingWindowTrigger(Request.EffectId) | SkillInfo(live ICardEffect, Hashtable, Timing) | 1:1 |
| WindowFrame.Stack | MultipleSkills.StackedSkillInfos | 1:1 |
| WindowFrame.Resolved(pop 시 폐기) | MultipleSkills.SkillInfos_used(인스턴스별, 종료 시 클리어) | 1:1 |
| 프레임 depth 재귀 | autoProcessing_CutIn 인스턴스+MultipleSkills 풀+TriggeredSkillProcess 재귀 | 1:1 (2-인스턴스 복원) |
| GateLive(재평가) | 루프 head의 CanActivate 재평가+skipCondition+ChainActivations+IsCutinEffect 판정 | 1:1 |
| Commit(OnceFlags.Consume(Request)) | SetOnProcessCallbuck(SkillInfos_used.Add+RegisterUseEffectThisTurn) — once-use 소비는 ResolveWithinCycleAsync의 OnceFlags 트랜잭션이 담당(기존 검증 경로) | 1:1+기존 substrate |
| ResolveBodyLiveAsync | ActivateEffectProcess→Activate_Optional_Effect_Execute(→ResolveWithinCycleAsync) | 1:1 |
| DrainNewTriggers/DrainCutInInto | MultipleSkills.cs:405-415 TriggeredSkillProcess 재귀(CheckNewTriggredSkill_mainStack 분기) | 1:1 |
| ChooseOrderAsync/ConfirmOptionalAsync | 순서선택(Blast=SelectHandEffect 경로/일반=selectCardPanel 경로)+optional 확인 → **ChoicePort ADAPTATION 유지**(A2) | ADAPTATION(기승인) |
| WindowContinuation(트리거 스택) | SkillInfo 스택을 담는 신형 continuation(A1) | ADAPTATION(기승인 연장) |
| FilterToMinimumBatch(BatchId 축) | **공급층 격리**(A3): event→stack 공급 시 최소 batch만 투입, 루프 본체 무-batch(AS-IS와 동형) | ADAPTATION(D-1 전례) |
| CollectUnifiedSeed(event→trigger) | event→(Hashtable, EffectTiming) 변환기+`StackSkillInfos(hashtable, timing)` 호출(A4: GameEventQueue 잔류 기승인) | ADAPTATION(기승인) |
| CollectActivatedBridgeTriggers | GetSkillInfos 5-영역 스캔이 대체(브릿지 마커 소멸) | 1:1 |
| activated-bridge 마커(Request.Context) | SkillInfo.Hashtable(AS-IS 페이로드 그대로) | 1:1 |
| HasExecutedSameEffect(SourceEntityId 비교) | AutoProcessing.HasExecutedSameEffect(IsSameEffect) | 1:1 |
| InFlightPick body-replay | 신 continuation의 in-flight SkillInfo replay(본체 재실행은 DeferredChoiceProvider 기존 의미론) | ADAPTATION(기승인) |

## 3. 배치 계획

병렬 규율: 파일-서로소 배치만 병렬, 공유 파일은 단일 배치 소유(GManager=W1, CardEffectFactory=F1). 각 배치 완료 시 코디네이터 AS-IS 라인 대조 → 커밋 → 중간보고.

### 1차 병렬 (F1 · F2 · F4 · W1)
- **F1 (opus)** — Triggered\* 프리미티브 7종 fold: 미러 `CardEffectFactory`의 해당 헬퍼들을 AS-IS CardEffectFactory 동명 헬퍼 1:1(ActivateClass 반환)로 재작성. 카드 파일 무수정. Triggered\* 7종 참조 0 확인 후 삭제. 소유: CardEffectFactory.cs(미러), TriggeredEffects.cs.
- **F2 (opus)** — 키워드 창-트리거 fold: KeywordBaseBatch1/2 중 **창 경유 타이밍만** AS-IS 키워드 파일(Vortex.cs/Alliance.cs/Retaliation.cs/Rush.cs/ArmorPurge.cs/Decode.cs/StartOfMainAttack.cs 등) 정독 후 해당 미러 파일에 1:1 재하우징(ActivateICardEffect-가시), 생성 사이트 재배선. 연속/치환 half 잔류. 소유: KeyWordEffects/\*, GiveEffect/\*.
- **F4 (sonnet, 카드 2장씩 2기 병렬 가능)** — 구모델 재포팅: BT1_021/BT1_090/BT1_109/BT9_043 + 공유-인프라 Tfx 2장을 확립된 ActivateClass 인라인 레시피로. BT9_043은 RD-R6-03(sec→hand 캐리어) 해소 여부 착수 시 확인, 미해소면 정직 STOP. EX8_074 불변(STOP). 소유: 해당 카드/Tfx 파일만.
- **W1 (opus)** — 신 창엔진 휴면 구축: MultipleSkills 1:1 실장(STOP 스텁 대체), AutoProcessing 휴면 절반 활성(P6A-STACKED-DRAIN throw 제거, availableMultipleSkills/executingMultipleSkills/skillInfos_used/컷인 계정), GManager에 autoProcessing_CutIn+multipleSkills 풀 배선, SkillInfo-통화 continuation 신형(Headless substrate, 구 WindowContinuation 불변). **live 호출자 없음(휴면)**. 소유: MultipleSkills.cs, AutoProcessing.cs, GManager, 신 continuation 파일.

### 2차 (W2 · W3 · F1b, 1차 완료 후 — 파일-서로소 병렬)
- **W2 (opus)** — 공급층: GameEventQueue event→(Hashtable, EffectTiming) 변환기(AS-IS emit 지점별 Hashtable 페이로드 byte-동형 대조표 필수), 배치 순차화(최소-batch 공급, A3), choice-답변 replay 키의 SkillInfo 등가(A2: source card InstanceId+effect ordinal). 소유: 공급층 신파일+Wiring 인접부.
- **W3 (opus, F2r 진실표로 신설)** — permanent 부여 저장층 1:1: 미러 Permanent에 duration 버킷 5종(UntilOwnerTurnEnd/UntilOpponentTurnEnd/UntilEachTurnEnd/UntilEndAttack/UntilNextUntap, `Func<EffectTiming,ICardEffect>` 저장) + `EffectList_Added`(AS-IS Permanent.cs:1380-1492) 실장(P6A-PERMANENT-EFFECTLIST-ADDED 해소) + `AddEffectToPermanent`를 AS-IS GiveEffectToPermanentOrPlayer.cs:11-51 버킷 저장으로 전환(RD-P6C3-C1 해소 — 구모델 잔존 경로는 기존 registry-lowering 보존 판단 포함). 소유: Permanent.cs, CardEffectCommons.cs(AddEffectToPermanent), GiveEffect/GiveEffectToPermanentOrPlayer.cs.
- **F1b (sonnet)** — F1 잔여: AS-IS `Player.SetFixedMemory` 1:1 신설 + `SetMemoryTo3TamerEffect` fold + `PlaySelfAtEndOfBattleTriggerEffect` fold(생성 사이트 ActivatedEffects.cs:2587). 소유: Player.cs, CardEffectFactory.cs(해당 헬퍼), ActivatedEffects.cs, TriggeredEffects.cs.

### 3차 (C, 단일 opus 배치 — 부분 컷오버 금지)
- seam 7개 일괄 전환(§1.1): 1→미러 AutoProcessCheck/TriggeredSkillProcess 구동, 2-4→신 continuation resume, 5-7→신 엔진 동기 구동(battle/security 동반).
- F3 본체 동승: 4-arg AddEffectToPlayer→AS-IS 버킷 저장 1:1(GiveEffectToPermanentOrPlayer.cs 정위치), DelayedOneShot registry-lowering 제거.
- registrar TryToBinding의 창-트리거 lowering 중지(창 경로 registry 빈손화), 구 창 루프 경로(WindowResolver 창 구동/Collector 창 수집/Wiring 창 deps) 제거, 구모델 Tfx 8 은퇴.
- 컷오버 불가 판명 시 **무수정 STOP**(전례 2회 작동한 안전장치).

### 4차 (V — 검증)
- 전체 diff 적대리뷰(독립 reviewer, "REFUTE하라" 프롬프트+AS-IS file:line 대조 요구).
- witness(적대 선정): BT1_021(EoT player-scope 반전), Vortex/Alliance/Retaliation(키워드 창-트리거), BT9_043(OnEndAttack), 기존 시큐리티 witness(BT14_035/BT13_023/EX8_051/EX8_061), 컷인 재귀, 순서선택 suspend/resume replay, KO/StartBattle 동기창(AD1_025 계열).
- 구조 계기판 보고(§0) + RuleAudit/스위트는 최종 게이트로만.

## 4. ADAPTATION 결정 목록 (전부 기승인 계열, 신규 발명 없음)
- **A1** continuation 외부화 유지+내용물 SkillInfo화 — 기승인(RD-R3-01 STOP note "continuation/ChoicePort retained as substrate ADAPTATION").
- **A2** 순서선택/optional의 ChoicePort화 + 답변 replay 키=(source InstanceId, effect ordinal, 제시 순서) — 기존 effect-id 키의 SkillInfo 등가 번역.
- **A3** cross-batch 삭제 순차화를 공급층으로 격리(루프 본체 무-batch) — D-1 골 전례, AS-IS에선 코루틴 순차성이 보장하던 것의 substrate 번역.
- **A4** GameEventQueue emit-전달 잔류 — bigbang_redesign §R3 명시 승인.
- **A5** 컷오버 시 구모델-리졸버 겨냥 Tfx 8 은퇴, EX8_074 authorized red — R6-D 계획 승계.

## 5. 리스크
- unified security seed(OnSecurityCheck+OnLoseSecurity 병합)의 AS-IS 대응 = ISecurityCheck의 IReduceSecurity ref-merge(CardController.cs:3982-3985) — seam 7 전환 시 수집 형태를 이 AS-IS 모양으로(C 배치에서 대조 필수).
- EndAttackTriggerHook의 off-window 직행(F1-ENDATTACK-HOOK) — 컷오버 후 OnEndAttack 수집이 이중화되지 않는지 C 배치에서 검증.
- F1/F2 fold 후 activated-bridge half의 수집 결과가 기존과 동일한지(전환 직후 기존 witness green 유지 — fold 배치별 검증 항목).
- 신 continuation의 choice-답변 replay가 pass 재실행 의미론에서 순서 안정적인지(W2 검증 항목).

## 5.5 배치 판정 기록
- **F2 = 무수정 종결(2026-07-14/15)**: KeywordBaseBatch1/2의 14 kind 전수 분류 결과 **live 창-발화 0** — Definition.Timing이 창 타이밍을 명명하나 해당 binding은 live 미등록. live 발화는 ①grant 경로=`GainKeywordToPermanent`(CardEffectCommons.cs:1747-1752)가 timing "Continuous"·role Continuous·**effect:null** 마커 등록(창 구조적 비수집, 코디네이터 실측 확인) ②인쇄 키워드=R1 Permanent getter(HasRetaliation:1133/HasBlitz:1229/HasAlliance:1346)+전용 게이트(AllianceAttackBoost/DeletionReplacementGate/EndOfTurnEffectAttack/BattleResolver). `KeywordBaseBatch*Effect`/`SelfKeywordBatch*Effect`/`BindKeywordBaseBatch1/2` 생성·등록은 전부 테스트-전용(코디네이터 실측: new Self\*=G3.5-C14 테스트만) → **live-dead, 물리 삭제는 R3-W3 승계**. §1.2의 키워드 위험 모집단 추정은 과대였음(선행 R1/R2/GR-006이 이미 중화).

- **F1 = 부분 fold 종결(2026-07-14/15, 코디네이터 AS-IS :63/:115 라인 대조 통과)**: ①`Gain1MemoryTamerOpponentDigimonEffect`·`Gain1MemoryTamerOwnerDigimonConditionalEffect` → AS-IS 1:1 ActivateClass fold(desc byte-동일, 게이트 순서 동일; Enemy null-가드·nullable 술어 가드·empty-desc 기본값=기존 미러 시그니처 ADAPTATION 유지) ②발명 wrapper 3종(UnsuspendSelfTrigger/SelfDpBuffTrigger/RecoveryTrigger)+클래스 3종(TriggeredUnsuspendSelf/TriggeredSelfDpBuff/RecoverTrigger) 삭제(참조 0 실증). **F1 잔여 STOP 5건**:
  - `SetMemoryTo3TamerEffect`: 미러 `Player.SetFixedMemory` 부재(AS-IS Player 멤버) → **F1b 배치**(2차)로 재스코프
  - `PlaySelfAtEndOfBattleTriggerEffect`: 생성 사이트가 ActivatedEffects.cs:2587(F1 비소유) → **F1b 동승**
  - `EoTLose3Memory`(BT1_040)·`TriggeredMemory/SetMemory/GainMemory`+DelayedOneShot(ActivatedEffect.cs:470, Tfx 2): 4-arg AddEffectToPlayer(F3) 결합 → **C 배치 동승**
  - `StartOfMainAttackEffect`: 미러 GiveEffect/StartOfMainAttack.cs가 **skeleton 스텁**(§1.2 인벤토리의 "동일 경로 포팅 완료" 판정은 오류) + Permanent-레벨 UntilOwnerTurnEndEffects 버킷·SelectAttackEffect Execute(RD-R2-01) 의존 → STOP 유지, **C 배치 사전조건: 이 grant의 live 소비 카드 존재 여부 확인**(무소비면 안전)
  - `TriggeredGainMemoryEffect` 클래스: EoTLose3Memory+테스트 5파일 잔존으로 삭제 불가 — C 배치에서 청산

- **F2 무수정 종결 판정 철회(2026-07-15 사용자 지적)**: F2의 "창-비경유" 분류가 **TO-BE 배선만 근거**로 이뤄짐 — 판정 기준은 AS-IS여야 함. 코디네이터 AS-IS 실측: `GainRetaliation`(Retaliation.cs:158-160)=ActivateClass를 `AddEffectToPermanent(OnDestroyedAnyone)`, `GainVortex`(Vortex.cs:96-108)=ActivateClass를 `AddEffectToPermanent(OnEndTurn)` — **AS-IS 부여형 키워드는 창-발화 효과**. 포트의 continuous 마커(effect:null)+전용 게이트는 구조 발산(발명물). → **F2r 재감사**(AS-IS 키워드 전수 진실표: 인쇄/부여/발화기제/미러 현행/발산 판정) 가동. 결과에 따라 키워드 재하우징 배치를 이 골 또는 R2 승계로 스코프 판정. §5.5 F2 항목의 "재하우징 불필요"는 **컷오버 시점의 행동-드랍 없음**으로만 유효(현행 발화가 게이트 경유이므로), 구조 종점 판정으로는 무효.
- **W1 = 검수 통과(2026-07-15, 코디네이터 AS-IS 전문 대조)**: MultipleSkills 1:1 실장(게이트 순서·post-filter·bounds quirk `Count < _skillIndex`·컷인/메인 재귀 분기·SetOnProcessCallbuck verbatim), AutoProcessing 휴면 절반 활성(TriggeredSkillProcess drain 1:1+AfterEffectsActivate 재스택·skillInfos_used 집계·HasAwaitingActivateEffects·AutoProcessCheck·컷인 계정 `return false` quirk verbatim), SkillWindowContinuation/AgentSkillWindowChoicePort 신설(휴면, 구 기제 무수정). **신 창에 ConfirmOptionalAsync 부재는 의도**(AS-IS는 optional 확인이 Activate_Optional_Effect_Execute 내부 — 구 창보다 1:1). **적대리뷰 재검 표적**: ①풀 grow-on-demand(AS-IS는 scene-직렬화 고정 풀, 고갈 시 null→배치 드랍 vs 미러는 무한 성장) ②port decline null→-1 매핑 ③endGame→RuleQueryService.IsTerminal() 번역.

- **F2r = AS-IS 키워드 진실표 완성(2026-07-15)**: 전수 판정=`keyword_firing_truth_table_2026-07-15.md`. 창-발화 키워드 **18종이 재하우징 대상**(포트는 전용 게이트 우회 — 삭제치환/EoT공격/전투플래그/공격선언/activated헬퍼 5클러스터), 충실 7종(Rush/Iceclad/Blocker/Jamming/Collision/Reboot/MindLink)+Progress 혼합. 컷오버 저장층 봉쇄 2건 확인(P6A-PERMANENT-EFFECTLIST-ADDED·RD-P6C3-C1) → **W3 배치 신설**(이 골), 키워드 18종 재하우징=R2 승계 5골. **collect-before-removal 패턴**(삭제창 수집이 제거 전, SkillInfo heap 참조로 트래시 해소) = C 배치 수집-시점 충실 요건으로 등재.

- **F4 = 부분 종결(2026-07-15, 코디네이터 BT9_043 AS-IS :84-136 verbatim 대조 통과)**: BT9_043 재포팅(RD-R6-03 해소 — AddHandCards+IReduceSecurity+IUnsuspendPermanents 3-call AS-IS 리터럴, 복합 body 발명 청산)·TfxWhenDigivolveDelete 재포팅(+G9-009 white-box 캐스트 교체). STOP: BT1_021/BT1_090(R6P-EOT-PLAYER-EFFECTLIST — C 배치 동승으로 재스코프)·BT1_109(아래 시스템 발견으로 revert)·TfxBeforePayCost(PlayCardAction:425-427이 구모델 SuspendCostReductionEffect 하드캐스트 — EX8_074/RD-R6-07 결합, C 배치 판정 필요).
- **시스템 발견(F4, 컷오버 전체 위험 항목)**: 신모델 `ICardEffect.CanTrigger` 1차 게이트=`DoneStartGame`(ICardEffect.cs:385-397, Setup phase 통과 필요) — **bare-Initialize 테스트 하네스에서 신모델 효과가 조용히 미발화**(구모델 ActivatedEffect는 이 게이트 미경유라 기존 테스트가 통과해 왔음). BT1_109 재포팅이 이 때문에 red→revert. 함의: ①구모델→신모델 전환 카드의 기존 테스트는 하네스가 phase를 Main으로 전진시키는지 먼저 확인 ②C 배치 컷오버 후 witness 전면에 동일 리스크 — **V 배치 검증 항목으로 등재**(하네스 phase-전진 감사).

## 6. 승계 design item (이 골에서 손대지 않음, 명기만)
RD9-87(탭 미-sink) · 시큐리티 다중효과 선택루프 부재 · IDontBattle player-scope 절반 · MIG1-KEYWORD-RELOCATE · MIG1-EXECUTE-RELOCATE · MIG1-BEFOREONATTACK · F1-ENDATTACK-LIVENESS · RD-R5-01/02/03/04 · RD-R6-07(EX8_074) · **키워드 창-재하우징 18종(진실표, R2 5클러스터 골)**.
