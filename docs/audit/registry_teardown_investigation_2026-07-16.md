# B군 사전조사 — R6-D(구모델 청산) + R3-W3(레지스트리 삭제) (2026-07-16)

Base: `595e0649`(A군 마감 병합). 조사 전용(무수정). 경로=`src/HeadlessDCGO.Engine/` 상대.

## 핵심 발견
1. **R6-D(activated)와 R3-W3(registry)는 구조적으로 분리** — `CardEffectRegistrar.cs:222-225`가 activated(`IActivatedCardEffect or ActivateICardEffect`)를 등록에서 명시 skip. 구모델 activated는 창(live EffectList 스캔)+resolver legacy switch로 해소 → **레지스트리 삭제를 막지 않음**(순서 자유·병렬 가능, 단 파일 공유 제약은 §F).
2. **"구모델 79→15" 계수는 과소** — 인라인 커스텀 클래스 카드만 셌음. 공유 factory 프리미티브(Select*/Buff*/Draw*) 경유 소비 포함 실잔량: **구모델 activated 실카드 39**(직접-`new` 6: BT1_078/084/087/109·ST4_15·EX8_074, 나머지 factory 경유·34/39는 신모델 병용 mixed) + **Tfx 18**. 구모델 continuous 직접 소비 실카드 = **0**(stage B에서 전량 flip 완료).
3. **창 컷오버의 "registry 트리거-읽기 0" 확증**: 잔여 트리거-판독 3파일 전부 사문 — `AutoProcessingTriggerCollector.cs:54`(유일 호출자 `SecurityDelayedTriggerHook.cs:144`=참조 0)·`TimingWindowResolver.cs:28`(호출자 0). 즉시 삭제 가능.

## §A. 구모델 corpus (삭제 종점 대상)
- `ActivatedEffects.cs` ~52 클래스(ToBinding 보유) · `ActivatedEffect.cs` 15 IEffectBody+uniform ActivatedEffect(:581) · `ContinuousAndRestrictionEffects.cs` **24** continuous/restriction(레지스트리 등록형) · `TriggeredEffects.cs` StartOfMainAttack(:19)+PlaySelfAtEndOfBattleTrigger(:82=RD-P6C3-B2) · KeywordBaseBatch1/2(889줄=RD-GC2-01) · LegacyActivatedBridge/ActivatedHashtableBridge · CardEffectFactory의 old-model 생산 ~33(activated)+31 construction(continuous).

## §B. 레지스트리 지도
**live 등록 생산자(전부 continuous/keyword/replacement 계열, 트리거/activated 없음)**: CardEffectRegistrar:237(enter-play non-activated) · CardEffectCommons grant 9곳(:95,1495,1747,1800,2827,2883,2961,3093,3546) · ActivatedEffects granted-continuous 6곳(:769,878,972,1050,2445,2562) · ActivatedEffect:339/ActivatedEffectResolver:690 · GiveEffectToPermanentOrPlayer:52 · KeywordBaseBatch1:321/2:368 · ProgressImmunity:63 · CardEffectFactoryBinding:323,434.
**live 판독 소비자**: 연속효과 게이트 union legacy-half(ContinuousKeywordGate:95,119,231·ContinuousScopeEvaluation:47,285·ContinuousImmunityGate·RestrictionScan·TrashProtectionScan·CanNotPlayOptionScan·DeletionReplacementGate:151 presence) + 룰(SecurityResolver:846·EffectDurationExpiry:24-63·EffectInvalidation·CardLeavePlayCleanup). 판독 call-site ~60(GetContinuousEffects 18·GetEffects 11·RemoveWhere 13 등).
**dead 판독자**: AutoProcessingTriggerCollector·SecurityDelayedTriggerHook·TimingWindowResolver(3파일 사문).
**CustomWouldBeDeletedOption**(DeletionReplacementTiming:38,252 + sink:1173): 유일 잔존 PRE 레지스트리 브릿지 — R3-W3c에서 잔존-필수 재판정.

## §C. 의존 그래프
R3-W3 선행조건 = 등록 생산자 전멸: (a) factory continuous 31 construction→신모델 kind-class (b) Commons grant 9곳→신모델 버킷(A군 패턴) (c) KeywordBaseBatch1/2(RD-GC2-01)+Kind-dispatch 해체 (d) ProgressImmunity (e) ActivatedEffects granted-continuous (f) CardEffectFactoryBinding → 이후 게이트 판독-half 은퇴(스캔 단독)→레지스트리/EffectBinding/LegacyBridge/registrar 삭제.
R6-D는 corpus 파일 자기-삭제만 관여(레지스트리 무관). 부분-삭제 즉시 가능=트리거-읽기 3파일.

## §D. 동승 판정
RD-GC2-01=R3-W3b 필수 · PlaySelfAtEndOfBattle(RD-P6C3-B2)=R6-Db 동승(선행조건 재판정 동반) · TimingWindowTrigger 통화-재배치·collector 상수 16종·구 SkillInfo record trim=R3-W3(배치0/c) 동승.

## §E. 검증 표면
- 빈손 검증법: union legacy-half를 계측 무력화(registry 강제 empty)→witness green이면 실증(게이트별 수행).
- witness: en-masse flip=대표 6종(continuous 3·activated 3), 인라인 6장=각자, Tfx 18=신모델 대체/은퇴.
- 레지스트리-단언 dedicated 테스트 ~11(G1F-004/005/006·G3J-001·G6-001·G7-001·G8-002/003·G2F-004·G3G-001/002) 재조준/은퇴. 나머지 ~170은 substrate 참조뿐.
- 계기판: ①registry.Register live 호출 0 ②판독 call-site 0 ③IActivatedCardEffect/IEffectBody/IHeadlessCardEffect/EffectBinding/ToBinding 참조 0 ④구모델 실카드 39→0·Tfx 18→0 ⑤파일 삭제 11종.

## §F. 배치 계획 (확정)
0. **배치 0 — dead-code 은퇴**(즉시·무위험): 트리거-판독 3파일+GetEffectsForTiming API+collector 상수, G2F-004/G1F-004 은퇴.
1. **R3-W3a — continuous factory flip**(최대 페이오프): factory 31 construction→신모델, 인쇄 카드 en-masse 자동 전환. union이라 게이트 무영향.
2. **R3-W3b — grant/keyword 등록 청산**: Commons grant 9곳+KeywordBaseBatch(RD-GC2-01)+ProgressImmunity+FactoryBinding→신모델 버킷.
3. **R3-W3c — 게이트 판독-half 은퇴+레지스트리 물리 삭제**: 생산자 0 확인 후 일괄 flip, 적대리뷰 필수(union 제거=최대 위험).
4. **R6-Da — activated factory flip**: 33 메서드→신모델, mixed 34장 자동 정리. CardEffectFactory.cs 공유로 R3-W3a와 순차.
5. **R6-Db — 인라인 6장 re-port+Tfx 18 은퇴+corpus 삭제**: EX8_074=비용 파이프라인 STOP 유지, PlaySelfAtEndOfBattle 재판정 동반.
소유권: 배치0 독립 / a·Da=CardEffectFactory.cs 순차 / b=Commons·KeyWordEffects(a와 서로소 가능) / c·Db=최종 일괄.

## §G. 배치0·W3a·W3b 실측 후 개정 계획 (2026-07-16, 코디네이터 확정)

**실측이 §F 계획을 정정한 것**: ①§B "dead 판독자 즉시 삭제"는 프로덕션-only 감사 — collector/GetEffectsForTiming은 테스트 13스위트가 live 소비(배치0 STOP, W3c-final 이월) ②§F W3a의 "union이라 flip 안전" 전제 붕괴 — union은 특정 kind만 배선, 31건 중 실질 안전 1메서드(DontHaveDP)뿐이고 CanNotAffected는 flip 후 게이트가 REVERT 강제(registry-half가 live 행동 소비자: GainCanNotBeDeletedByBattle+BlocksOpponentEffect 사이트, RD-W3A-01). 교훈: **flippability 판정은 tests/ 포함 전수 grep + 소비자 판독-half의 실배선 확인이 게이트**.

**착지 성과**: 배치0(−1,355줄 사문 은퇴)·W3b(등록 6사이트 제거+CardEffectFactoryBinding 495줄 삭제+KeywordBaseBatch 등록=생산-사문 판명·제거+**Progress 완성**(RD-R2-01 스테일 STOP 은퇴, witness 3/3)+GiveEffect 과도기 분기 제거=버킷 단독 AS-IS 복원)·W3a(DontHaveDP flip+FAILd-04 해소). 계기판: Register live 24→18. 구조 발견: **DP-delta grant의 registry 소비자(ResolveDp)=사문** — 등록이 이미 발화하지 않음(28 live 카드), 버킷 전환=사문→발화 행동 변경이라 AS-IS 기대 도출 동반 필요.

**개정 잔여 배치 (W3c 시리즈 — 소비자-측 AS-IS 재하우징 → 대응 flip 원자 묶음):**
1. **W3c-1 면역/joint**: ContinuousImmunityGate.BlocksOpponentEffect 사이트(Sink:527/1926/1944·BlockTiming:284·CardController×4)를 AS-IS `TopCard.CanNotBeAffected` 직독 이관 + GainCanNotBeDeletedByBattle + ProgressImmunity:63 + CanNotAffected flip 재실행 + G9-054/057/P0R 재조준. joint-술어 kind-class 모델 교정(CanNotSelectBySkill/CanNotMove/CannotIgnoreDigivolutionCondition — 단일-Func joint 규약) 동승.
2. **W3c-2 expiry 모델 flip**: EffectDurationExpiry(registry 만료 sweep)→버킷 만료(AS-IS 리셋 사이트) 일괄 + :95 GainCanNotBeDeletedByBattle 전환(RD-W3B-BATTLEDEL-TESTWELD, G9-054 재조준).
3. **W3c-3 DP-delta 판정·전환**: :1747/:1800 — AS-IS에서 이 grant들의 기대 발화 도출(현행 미러=무발화 발명 상태 추정) 후 버킷 전환 + 28 live 카드 대표 witness. 행동-변경 배치라 별도 적대리뷰 권장.
4. **W3c-4 키/스캔 소비자 재하우징**: TrashProtectionScan(BT9_109)·CanNotPlayOptionScan(BT8_057)·SecurityResolver:530 구-타입·HeadlessMainPhaseFlow·CardSource.CardNames·Sink CanNotAddSecurity/Memory·DeDigivolveHelpers·ImmuneStackTrashing — 각 AS-IS EffectList is-스캔 이관+대응 flip+실카드 witness. (:2961/:3093 restriction 코어·:1495/:2827 트리거형·:2883 포함)
5. **W3c-final**: 게이트 판독-half 은퇴+EffectRegistry/EffectBinding/LegacyBridge/registrar 물리 삭제+collector/GetEffectsForTiming 테스트 재조준(배치0 이월)+레지스트리-단언 스위트 정리 — 적대리뷰 필수.
특수플레이 마커 5건=R6-Db 결합 유지. R6-Da/Db는 §F 그대로.

## §H. B군 1라운드 마감 (2026-07-16)

**통합 적대리뷰(14커밋 전수, 6렌즈)**: 초판 **NO-GO(P0-1)** — 생산자-선행 flip(Progress→버킷·CanNotAffected→kind-class)이 sink/Commons의 registry-단독 소비자를 사문화시켜 일반 면역 집행이 main 대비 소실(sink-직행 mutation 경로), 기존 witness 전량이 직독-단언이라 미검출. **같은 라운드 상환(36010cdb)**: 소비 7사이트(리뷰 6+누락 1=Commons:359 C-arm) live 재하우징 → `BlocksOpponentEffect` production 호출 0, sink-경로 실구동 witness 7/7(Ba-P0-1), P1-1(sink ApplyMemory gainer-키잉 부호-도출 교정), P2(RD-BCE-01 기록). 스위트 328/108/436, fail-set 전 구간 연속 동일. **리뷰 GO 조건 충족.** 그 외 flip 14패밀리=전수 반증 실패(확인됨), union 위험 조합 0.

**1라운드 결산**: registry 판독 call-site 수십 곳 → AS-IS live 스캔(면역 계열 8+7·CanAdd 2·DeDigivolve·StackTrashing 9·CanNotPlay·TrashProtection·DontBattleSecurityDigimon·EndTurnMinMemory·joint 2·DP/SAttack-delta 5함수). 발명 파일 삭제 6(FactoryBinding·TrashProtectionScan·사문 훅/resolver·게이트 파일들 부분). 침묵 버그 실수정 3계열(BT9_109 트래시보호·DP-delta 무발화 28+장·P0-1 면역 사문화). Register 생산 24→14.

**잔여 지도(2라운드)**: ①**R6-Da' 독립 region-골 재스코프**(activated 표현형 이관 — 구모델 ActivatedEffect의 cap-파티션/환불/executed 의미론은 AS-IS ActivateClass 미표현, 단순 flip=회계 소실; 창 컷오버 동형 설계 골) ②잔여 생산자: C-게이트 grant 코어 일부·E(RD-3C2B-02 EffectMutation live-effect 스레딩)·F(RD-P6C3-C1)·A2(player-bucket expiry)·ActivatedEffects 내 granted-continuous 6곳(corpus 삭제와 동승) ③ContinuousImmunityGate=compile-only 스텁(테스트 2종 재조준 후 삭제) ④registry 물리 삭제=①② 소멸 후. **R2-C ①②③이 ContinuousModifierGate 비용 3키를 걷음** — 다음 골로 확정.
