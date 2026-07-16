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
