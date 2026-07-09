# 엔진·프리미티브 충실도 TODO (전면 점검, 2026-07-09)

목적: AS-IS(`DCGO/`)와 다르게 구현된 것 전수 조사 → 상환 TODO. 기준=[[result-equivalence-not-completion]](결과등가 불인정, 스캔 population·가드 유무/순서·tier 구조·게이트 조건 1:1, 순수 substrate 번역만 허용), [[no-callsite-not-skip-reason]](latent도 FAIL), [[strong-model-prebuild-latent-infra]](기각 기준=AS-IS 메커니즘 존재 여부).

**조사 진행 상태 — 전 서브시스템 완료 (2026-07-09)**:
- ✅ 트리거/효과 해소 시스템 (P0 8·P1 9·P2 3) — P0 전건 직접 검증
- ✅ restriction/immunity (인터페이스 29종 커버리지표 포함, P0 1·P1 13·P2 6)
- ✅ play/digivolve/턴 (P0 4·P1 9·P2 4) — P0 전건 직접 검증
- ✅ DP/수치/비용 (P0 2·P1 14·P2 7)
- ✅ attack/battle/security (P0 4·P1 18·P2 6)
- ✅ deletion/replacement+키워드 전수 (P0 2·P1 13·P2 다수) — P0 전건 직접 검증
- ✅ 프리미티브 팩토리 전수(AS-IS 182 심볼·CPF 464) + grant-factory 45메서드 + select-계열 + Commons GiveEffect 33종/CanTrigger 게이트/MinMax — 문서-vs-코드 mismatch 0
- ✅ 상환 재검증 3라운드(joint-scan 4건·FAILa 8건·d04-08+b01/02 6건) — CanNotBeRemoved PARTIAL(TODO-9)·DontBattleSecurityDigimon PARTIAL(TODO-88) 외 전건 CONFIRMED

**총계: TODO 113건** (P0 15건 내외·P1 다수·P2/정리). 최다 발산 근원 3곳: ①트리거 창(window) 재평가 루프 소실(TODO-1~8), ②삭제-확정 시퀀스(진화원/PRE창/치환, TODO-93~103), ③attack/security 창 순서·모집단(TODO-46~48·79~92).

**라이브 룰 결손 상세 명세**: `docs/audit/rule_deficiency_2026-07-09.md` — P0를 RD-1~17로 재구성(A 기본규칙 미집행 / B 순서·시점 발산 / C 트리거 시맨틱스), 항목별 룰 정의→AS-IS→헤드리스→관측 발산→상환 방향 + 5단계 상환 순서.

표기: [검증] = Opus가 양측 코드로 직접 확인. [보고] = 조사 에이전트 보고, 개별 spot-검증 미실시(착수 전 AS-IS 재확인 필수).

---

## P0 — 트리거/해소 코어 시맨틱스 (라이브 행동 발산)

공통 근원: AS-IS 해소 루프는 "스택 → **매 해소마다 재검사·재선택·중첩 재귀**"(MultipleSkills.cs)의 재진입 구조인데, 헤드리스는 "일괄 수집 → 고정 정렬 → FIFO 일괄 소진"(GameFlowProcessor/EffectScheduler)의 배치 구조. 창(window) 재평가 루프가 substrate 번역에서 소실됨. **개별 패치보다 창-루프 구조 자체의 미러를 우선 설계할 것.**

### TODO-1. [검증] 동시 트리거 "플레이어 처리순서 선택" 부재
- AS-IS: MultipleSkills.cs:180-336 — 한 플레이어의 활성 스킬 ≥2면 선택 패널로 **플레이어가 순서 결정**, 하나 해소 후 while 루프(76행)가 잔여 스택 재구성→재선택. (autoEffectOrder 옵션 시 AutomaticOrder 휴리스틱)
- 헤드리스: MandatoryEffectOrdering.cs:59-65 — OrderBy(PlayerOrder→Priority→Sequence→InputIndex) 고정 정렬 일괄 enqueue.
- TODO: 의무 트리거 동시 N건 시 플레이어 순서-선택 choice 도입(RL 액션 표면 포함). 창-루프 재설계(TODO-4)와 함께.

### TODO-2. [검증] 창 순서 구조 역전 — AS-IS "턴P (의무+선택) 전부 → 비턴P 전부" vs 헤드리스 "양측 의무 전부 → 선택"
- AS-IS: MultipleSkills.cs:55-56 — 턴 플레이어 창 완료 후 비턴 플레이어 창. 창 안에서 의무/선택 구분 없이 같은 선택 패널(의무-우선 강제 없음, 전원 skippable일 때만 "활성화 안 함").
- 헤드리스: GameFlowProcessor.cs:899-913 + MandatoryEffectOrdering.cs:40-44 — 양측 mandatory 전부 소진 후 optional 프롬프트. (a) 비턴P 의무가 턴P 선택보다 먼저 = AS-IS 역순, (b) 의무-우선 강제 = 헤드리스 발명.
- TODO: 창을 플레이어 단위로 분할(턴P 먼저), 플레이어 창 내 의무+선택 통합 제시.

### TODO-3. [검증] optional 프롬프트 1개 선택 시 잔여 optional 전량 소실
- AS-IS: MultipleSkills.cs:76-165 — 하나 해소 후 잔여 스택 **재제시**. 전량 소실은 전체-거절 시만.
- 헤드리스: OptionalPromptQueue.cs — maxCount:1(245행), ResolveChoice(109-122)가 1개 enqueue 후 프롬프트 무조건 Dequeue → 나머지 소멸(once-flag도 이미 소모, TODO-5 연동).
- TODO: 선택-해소 후 잔여 트리거 재제시 루프.

### TODO-4. [검증] 중첩(컷인) 해소 순서 역전 — 신규 트리거가 잔여 스택보다 뒤로
- AS-IS: MultipleSkills.cs:397-415 — 각 해소 직후 RuleProcess+TriggeredSkillProcess **재귀**("newly triggered effects resolve first"), ICardEffect.cs:1283-1285 매 효과 후 재스택; CanTrigger는 뮤테이션 순간 동기 평가.
- 헤드리스: GameFlowProcessor.cs:499-501 — ResolveAllAsync가 현행 큐 소진 후 신규 이벤트는 다음 RunToStable pass에서 수집(현행 배치 뒤). 조건 평가도 배치-후 상태로 지연.
- TODO: 해소 직후 재수집-재귀(또는 우선삽입) 구조로 창 루프 재설계. TODO-1~3과 통합 설계 권장.

### TODO-5. [검증] once-per-turn 소모 시점 — AS-IS "실행 시" vs 헤드리스 "수집 시"
- AS-IS: MultipleSkills.cs:358-362 → ICardEffect.cs:1118-1121 — OnProcess 콜백이 `UseOptional || !IsOptional` 확인 후에만 사용 등록. 거절/불발 시 미등록→같은 턴 재트리거 가능.
- 헤드리스: GameFlowProcessor.cs:479-483 — 수집 루프에서 OnceFlags.TryActivate. 이후 optional 거절/Failure여도 소모 유지.
- TODO: TryActivate를 해소 성공(및 optional 수락) 시점으로 이동. (G11-004의 게이트-먼저 평가와 별건)

### TODO-6. [검증] uniform ActivatedEffect.IsOptional 死필드 — 비대화형 optional 효과 무프롬프트 강제 실행
- AS-IS: OptionalSkill.cs:14-132 + ICardEffect.cs:1191-1201 — 모든 IsOptional 효과는 실행 직전 yes/no.
- 헤드리스: ActivatedEffect.cs:526/547 저장만; ActivatedEffectResolver가 IsOptional 미참조(확인: resolver 전문 grep 0건). 대화형 body는 skip으로 우회되나 비대화형 body("~해도 된다: 메모리+1")는 거절 기회 없음.
- TODO: ActivatedEffectResolver(및 브릿지 경로)에 IsOptional yes/no 게이트 삽입.

### TODO-7. [검증] 해소 시점 게이트 실패 = 스킵이 아니라 큐 영구 블로킹(wedge)
- AS-IS: MultipleSkills.cs:122-126 — CanActivate 실패 시 continue(skip).
- 헤드리스: HeadlessCardEffectContract.cs:274-282 Failure(Resolved=false) → EffectScheduler.cs:89-93 dequeue 안 함 → ResolveAllAsync break → 큐 헤드 영구 잔류, 후속 트리거 전부 미해소.
- TODO: 해소 시점 CanResolve 실패는 dequeue+skip으로(AS-IS fizzle 미러). ⚠️단, "head changed" 방어 로직과 리졸버-오류 Failure(진짜 오류)는 구분 유지.

### TODO-8. [검증] pass 내 다중 이벤트 EffectId dedupe — 동일 효과 다회 발화 소실
- AS-IS: 이벤트마다 StackSkillInfos — 같은 ICardEffect라도 이벤트별 SkillInfo(각자 hashtable)로 N회 해소(AutoProcessing.cs:984-989).
- 헤드리스: GameFlowProcessor.cs:438-451 — `seen` HashSet이 pendingEvents 전체에 걸쳐 EffectId dedupe → 1회만, 첫 이벤트 subject만 유지.
- TODO: dedupe 키를 (EffectId × GameEvent)로 — 단 동일 이벤트 내 중복 수집 방지 목적은 유지.

---

## P1 — 트리거 시스템 latent 구조 발산

### TODO-9. [검증] CanNotBeRemoved 4번째 chokepoint(place-to-security) 누락
- AS-IS: CardController.cs:3526 — PutSecurity가 `CanBeRemoved()` 게이트(hand/deck/security 3종 removal kind 커버).
- 헤드리스: MatchStateMutationSink — IsRemovalBlockedByScan은 ReturnToHand:338/ReturnToDeckTop:349/ReturnToDeckBottom:362 3곳만; AddToSecurityKind:372-389는 CannotAddSecurityKey만 게이트.
- TODO: AddToSecurityKind에(필드→시큐리티 이동일 때) IsRemovalBlockedByScan 게이트 추가. 부수: CanNotBeRemovedStaticEffect에 isInheritedEffect 파라미터 부재(AS-IS factory 보유) + AS-IS grant-embedded `!TopCard.CanNotBeAffected(self)` 면역 누락 — 함께 상환.

### TODO-10. [보고] 트리거 수집 population — AS-IS는 (플레이어→필드[브리딩 포함 16슬롯]→트래시→핸드→앞면 시큐리티) vs 헤드리스 존-무관 레지스트리/BattleArea-only 브릿지
- AS-IS: AutoProcessing.cs:770-885 GetSkillInfos.
- 헤드리스: EffectRegistry.GetEffectsForTiming(존 무관 — 존 이탈 시 바인딩 잔존하면 역방향 과발화 위험) + 브릿지 스캔 BattleArea만(GameFlowProcessor.cs:641,656; "KNOWN BOUNDARY" 주석 577-580).
- TODO: 타이밍별 스캔 population을 AS-IS 5-존으로 정렬(faceup 시큐리티는 SecurityFaceState 재사용 가능). 트래시/핸드 리스너 카드 포팅 전 선행 구축.

### TODO-11. [보고] `CanUseCondition == null → 트리거 불가` 규약 미반영 (기본값 정반대)
- AS-IS: ICardEffect.cs:350-353 — null이면 return false(절대 미발화).
- 헤드리스: ActivatedEffect.cs:554-561 — CanUse null이면 통과(always).
- TODO: uniform/브릿지 경로의 null-게이트 기본값을 AS-IS로. ⚠️기존 포팅 카드 중 null-게이트 의존 정상동작이 있는지 회귀 조사 필수(load-bearing 가능).

### TODO-12. [보고] 트리거-시점 스냅샷·소스 lapse 규칙 부재
- AS-IS: AutoProcessing.cs:108-115 PermanentWhenTriggered/TopCardWhenTriggered 스냅샷; CanActivate(ICardEffect.cs:386-452)가 해소 시점에 ①비계승: 소스가 여전히 TopCard ②계승/링크: 뒤집힘·비디지몬·이탈 불발 + Permanent 동일성 ③완전 이탈 시엔 통과(트래시서 해소).
- 헤드리스: EffectContext에 id만 — 해소 시점 재확인 없음.
- TODO: 트리거 수집 시 스냅샷 + 해소 시점 lapse 게이트(컷인 창 도입=TODO-4와 결합 시 필수).

### TODO-13. [보고] 효과 무효화(disable) — 평가 시점(수집 1회 vs 매 해소)·상호-무효화 트리 부재
- AS-IS: CanActivate 내부에서 매 해소 직전 CheckEffectDisabledClass.isDisabled(트리: disabler가 disabled면 대상 active, CheckEffectDisabledClass.cs:13-46).
- 헤드리스: GameFlowProcessor.cs:456-459 수집 시 1회, EffectInvalidation.cs:27-60 플랫.
- TODO: 해소 직전 재평가 + disabler-of-disabler 트리.

### TODO-14. [보고] once-per-turn 키 정밀도 — AS-IS IsSameEffect(소스카드+HashString+RootCardEffect) vs 헤드리스 키 충돌/분열
- 헤드리스: OnceFlagHelpers.cs:226-238 키=(EffectId,Source,Owner)인데 uniform EffectId=`{instance}:ae:{timing}:{bodyType}`(ActivatedEffect.cs:529-530) → 같은 카드·타이밍·body타입의 다른 효과가 캡 공유(과소발화) / 같은 효과가 등록-경로와 브릿지-경로에서 키 상이(과다발화).
- TODO: EffectId에 AS-IS HashString 대응(효과 서수/해시) 포함 + 경로간 키 통일.

### TODO-15. [보고] use-count 리셋 지점 누락 — 턴 경계 외
- AS-IS: 카드 이동 시 CardSource.Init()(CardSource.cs:345-350), 진화원 편입(CardController.cs:1511,3093), 링크 부착(3393), DigiXros(SelectDigiXrosClass.cs:923)에서 개별 리셋; 캡 없는 효과도 실행 시 무조건 RegisterUseEffectThisTurn(타 술어가 GetUseCountThisTurn 관찰).
- 헤드리스: OnceFlagController.ResetForTurn뿐; 캡 있는 효과만 등록.
- TODO: 존-이동/진화원-편입 시 per-card once-flag 리셋 + 무캡 효과도 사용 등록.

### TODO-16. [보고][P1-INV] IsBackgroundProcess 즉시-실행 경로 미구현 (enum만 존재)
- AS-IS: AutoProcessing.cs:889-981 ActivateBackgroundEffects — 스택 제외(781행), 창·순서·optional 없이 즉시 실행+사용 등록.
- 헤드리스: EffectResolutionMode.Background 매핑만, 분기 소비처 0.
- TODO: background 분기 구현(InvertSAttack 등 AS-IS SetIsBackgroundProcess 사용 효과 포팅 전 선행).

### TODO-17. [보고][P1-INV] AfterEffectsActivate / RulesTiming 창 미방출
- AS-IS: 매 효과 실행 후(ICardEffect.cs:1283) AfterEffectsActivate 스택, AutoProcessCheck마다 RulesTiming(AutoProcessing.cs:134).
- 헤드리스: 두 타이밍 문자열 grep 0건.
- TODO: 두 타이밍 방출 지점 추가(해당 타이밍 리스너 카드 포팅 전 선행).

### TODO-18. [보고] 컷인 전용 스택·ChainActivations 상한·skipCondition(HasExecutedSameEffect) 부재
- AS-IS: 별도 AutoProcessing 인스턴스(12개 호출부), IsCutinEffect 시 ChainActivations 상한(MultipleSkills.cs:138-158), 같은 창 내 동일효과 재발화 방지(AutoProcessing.cs:624-627).
- 헤드리스: 단일 EffectScheduler 큐, 대응물 없음.
- TODO: TODO-4 창 재설계에 컷인 스택 분리+상한+동일효과-스킵 포함.

---

## P2 — 트리거 시스템 minor

- TODO-19. [보고] 창 내 동률 순서 결정 기반: AS-IS 수집 순서(플레이어효과→슬롯順→트래시→핸드→시큐리티) vs 헤드리스 레지스트리 등록순+sequence. TODO-1 도입 시 대부분 가려짐.
- TODO-20. [보고] 플레이어 파티셔닝 키: AS-IS Owner(MultipleSkills.cs:42-50) vs 헤드리스 ControllerId(MandatoryEffectOrdering.cs:47) — 탈취류에서 갈림.
- TODO-21. [검증] 문서 정정: CardPortingFramework.cs:6470-6471(CanNotSelectBySkillStaticEffect)·6484-6485(CanNotBeRemovedStaticEffect) XML 주석이 존재하지 않는 causingEffectPredicate 파라미터 서술(구 split 설계 잔재; 코드는 joint 정상).

## P2/latent — 상환 재검증(8건 전건 CONFIRMED)에서 확인된 잔여 노트

8건 "✅상환 2026-07-08" 주장 재검증 결과 전건 유지(MISMATCH 0): CanNotBeDestroyedBySkill·CannotReturnToLibrary·CannotReduceCost(Both/Play/Digivolve 스코프 필터 정상, BT5_021 교차-차단 없음)·PlacePermanentInSecurity·OptionMain discriminator·ReturnToLibraryBottomDigivolutionCards(가드→방출→이동 순서 일치)·TrashSelfThenGainMemoryDelay·PlaySelfDigimonAfterBattleSecurity. 잔여(전부 latent, 문서 기왕 기록과 정합):

- TODO-22. `[Security]` 옵션 discriminator 미모델 — AS-IS CardEffectCommons.cs:717 OptionSecurityEffect(Contains("[Security]"), BT18_098.cs:39 사용) vs 헤드리스 discriminator 0건(BT18_098은 per-card 라우팅으로 포팅됨). 시큐리티-옵션 재사용류 카드 포팅 전 구축.
- TODO-23. CannotReduceCost의 임의 non-count `targetPermanentsCondition` 미모델(asis_tobe_primitive_mapping.md:114 기왕 기록) — count-외 술어 카드 포팅 전 구축.
- TODO-24. PlaySelfAfterBattle 삭제 예약 구조 상이 — AS-IS는 플레이된 permanent에 실제 [End of Turn] ActivateClass 등록(CardEffectFactory.cs:369-410) vs 헤드리스 DeleteAtTurnEndKey metadata 마커+HeadlessEndTurnCleanupFlow sweep(CPF:4677/9817). 행동 등가이나 효과-무효화·CanNotBeAffected 상호작용이 실 효과가 아닌 마커엔 못 닿음 — 구조 미러 검토.

## P1 — select-효과 계열 (AS-IS 4클래스 → 헤드리스 ChoiceRequest 축소에서 소실된 대화형 의미론; 전부 latent-표면, 해당 파라미터 사용 카드 포팅 시 발현)

### TODO-25. [보고] `canTargetCondition_ByPreSelecetedList` 표현 부재 (3클래스 공통)
- AS-IS: SelectPermanentEffect.cs:439-445/519-533·SelectHandEffect.cs:278-296/408-426 — **이미 선택된 집합의 함수로** 후보 적법성을 클릭마다 라이브 재평가.
- 헤드리스: ChoiceCandidate.IsSelectable이 빌드 시 1회 고정(ChoiceCandidate.cs:39). 집합-의존(pairwise) 제약은 resolve-시 whole-set SelectionValidator로만 — 단계별 차단 불가.
- TODO: ChoiceRequest에 per-step 재평가 술어(selected-set 인자) 도입 또는 반복-choice 프로토콜.

### TODO-26. [보고] `canEndSelectCondition` 의미론 — 종료버튼 라이브 게이트 + 실행가능성 pre-scan 소실
- AS-IS: SelectPermanentEffect.cs:201-217/221-239 — End 버튼 활성 여부 라이브 + 적법 종료집합 없으면 효과 자체 억제(pre-scan).
- 헤드리스: resolve-시 try-reject-retry(ChoiceRequest.cs:76-82, ChoiceResult.cs:86-93)만.
- TODO: 조기종료 가능 여부 노출 + 발동 전 feasibility pre-scan.

### TODO-27. [보고] `canNoSelect`가 AS-IS `Func<bool>`(지연 평가, SelectCardEffect.cs:14/366-369)인데 헤드리스는 정적 bool(CanSkip) 캡처.
### TODO-28. [보고] 비공개/뒷면 존 선택 플래그 부재 — AS-IS canLookReverseCard(SelectCardEffect.cs:24)·_allowFaceDown(:105)·IsFlipped 필터(:280-326) vs ChoiceRequest/Candidate에 가시성·뒷면 표현 0.
### TODO-29. [보고] `afterSelect*Coroutine`(전체-선택-후 1회) 의미론이 per-id 콜백으로 붕괴(ActivatedEffect.cs:452-497) — maxCount≤1일 때만 안전(주석 자인).
### TODO-30. [보고→일부 검증] SelectPermanentEffect.BuildMutation의 PutSecurity가 select-시 CanAddSecurity 가드 생략(:223-225) — 단 sink AddToSecurityKind가 apply-시 게이트(:375)하므로 위치 차이만; Degenerate→DeDigivolveKind가 AS-IS IDegeneration 전체 의미론 미커버. 위치-충실 여부 판정 후 처리.

## P1 — 값변경 grant-factory 계열 (전수 대조: AS-IS 최상위 45 메서드; 신규 F1~F8)

### TODO-31. [검증] F1: `Func<int>` 동적값이 비-self 스코프 값변경 7종에서 소실
- AS-IS: 전 값변경 팩토리가 `<T>`(int|Func<int>) 제네릭 — ChangeSAttack.cs:66·ChangeOriginDP.cs:36·ChangeLinkMax.cs:62·ChangeCardDP.cs:11·ChangeSAttack.cs:205(invert)·ChangeDP.cs:65·ChangeDigivolutionCost.cs:10.
- 헤드리스: self 오버로드만 Func<int>(CPF:5950/5964/5980), player-scope/global/target/invert/set은 int 전용(CPF:6258/6443/6565/6901/6450/7329/5991).
- TODO: PlayerScopeModifierEffect 등에 dynamicValue 플러밍 확장(ContinuousScopeEvaluation.ResolveDynamicValue 재사용).

### TODO-32. [검증] F2: isLinkedEffect·hashstring 파라미터 소실 — ChangeDP(ChangeDP.cs:15/47/72)·ChangeSAttack(:14/46/72+hashstring :59/72)·ChangeCardDP(:11, 추가로 포팅이 isInheritedEffect를 받고도 미전달) → 링크-부여 정리·해시 유일성 표현 불가.
### TODO-33. [검증] F3: MandatorySelfPlayCostReduction — AS-IS rootCondition(ChangePlayCost.cs:117-121) + PermanentsCondition(=타깃 퍼머넌트 0개일 때만 → **등장 결제 한정, 진화 결제 제외**, :158-161) 게이트가 포팅(ported ChangePlayCost.cs:81/89)에서 삭제·미문서화 → 진화 결제에도 감액될 위험.
### TODO-34. [보고] F4: ChangePlayCostStaticEffect 필드-효과가 self 모디파이어로 붕괴(+setFixedCost NotSupported) — 문서화된 debt(ported ChangePlayCost.cs:14-23)이나 상환 대상.
### TODO-35. [보고] F5: AddDigivolutionRequirementStaticEffect 일반형(AS-IS AddDigivolutionRequirement.cs:35 — 술어·비용식·레벨범위)이 포팅 동명(CPF:6016)에선 color@level 전용으로 축소; 일반형은 self 변형(CPF:6025)에만.
### TODO-36. [보고] F6: 동명 씬-래퍼 오버로드 7종 부재(ChangeTargetDP/TargetLinkMax/BaseDP(target)/TargetSAttack/InvertSelf·Target SAttack/VortexCanAttackPlayersSelf/AddLinkCondition[isInheritedEffect 미지원]) — 일반형으로 대체 가능하나 AS-IS 호출부 1:1 이식 시 재작성 필요. [P2 성격]
### TODO-37. [보고] F7: 체계적 — AS-IS 제로가드(`changeValue==0 → return null`, ChangeDP.cs:79 등 5파일)와 grant-내장 `!TopCard.CanNotBeAffected` 위치가 포팅(무조건 등록+중앙 게이트)과 상이 — "효과 존재 여부" 관찰·면역 상호작용 미세 편차. [P2 성격, 일괄 정책 결정 필요]
### TODO-38. [보고] F8: AddAppfuse linkCondition의 `source != card` 가드(AddAppfusionMethod.cs:31) 누락(CPF:6784).

## P1 — 프리미티브/sink 신규 (전수 감사 완료분; 문서-vs-코드 mismatch 0 = 기존 매핑 문서 상태는 신뢰 가능)

### TODO-39. [검증] SetMemory 경로가 CannotAddMemory 제약 우회
- AS-IS: CardEffectFactory.cs:35-48 — SetMemoryTo3의 CanActivate가 `Owner.CanAddMemory(activateClass)` 요구(memory≤2에서 set-to-3은 항상 증가=게이트 대상); Player.cs:1030-1055 CanAddMemory = ≥10캡 + ICannotAddMemoryEffect joint 순회-스캔.
- 헤드리스: MatchStateMutationSink.cs:500-508 — `!isSet && amount > 0`로 **SetMemory 면제**(주석이 AS-IS를 잘못 서술). TriggeredSetMemoryEffect(CPF:2636-2678)도 turn+threshold만.
- 발산: CannotAddMemory 연속효과 존재 시 AS-IS는 set-to-3 발동 불가, 헤드리스는 무조건 설정. SetMemoryTo3 카드 6장 기포팅(BT1_085/086/087/089/104·BT2_090), producer 카드 미포팅=latent.
- TODO: set-메모리 증가분(target>current)에 CannotAddMemory 게이트 + resolve-시 IsExistOnBattleAreaActivate 재검사.

### TODO-40. [검증] 효과-드리븐 드로우가 OnDraw 타이밍 미방출
- AS-IS: CardController.cs:1948-1960 — **모든** 드로우(count≥1, 효과 포함) 후 StackSkillInfos(OnDraw).
- 헤드리스: OnDraw 방출은 HeadlessEarlyPhaseFlow.cs:90-91(드로우 페이즈 1회)뿐; 효과 드로우(MatchStateMutationSink ApplyDraw:523-543)는 미방출.
- TODO: ApplyDraw에 OnDraw 방출 추가(드로우 플레이어 actor). OnDraw 소비 카드 미포팅=latent. 매핑 문서 DrawClass PARTIAL 항목 "sink 확인要"→"갭 확정" 갱신.

### TODO-41. [보고][P2] 정리 3건 — (a) SelectPermanentEffect.cs:223-225·DeletionReplacementGate.cs:340의 stale 부채 주석(CannotAddSecurity 게이트는 이미 sink:373-380+producer CPF:6376 실재 — 주석만 낡음), (b) AS-IS CanActivate 사전게이트 vs 헤드리스 mutation-시점 차단 구조 노트(AddMemory 계열 결과 등가이나 once-per-turn 소비 관측 차이 잠재 — 트리거 P0-5와 동근원), (c) 스켈레톤 미러 트리(src/…/Script/CardEffects/ 74파일 7줄 TODO) 존재 — 이름-grep 감사 위양성 주의.

---

# 서브시스템 감사 통합분 (restriction/immunity · play/digivolve/턴 · DP/수치/비용 · attack/battle/security)

중복 발견은 병합함(옵션 색요건·CanNotPlay/PutField·토큰 진화 가드·언서스펜드-공격 grant·faceup 과잉폴드·SAttack 면역 등은 복수 감사가 독립 재발견 = 신뢰도 상향).

## P0 — 라이브 룰 결손 (즉시 상환 대상)

### TODO-42. [검증] 옵션 색 요건(MatchColorRequirement) 게이트 전무 (restriction#1=play#2 동일 발견)
- AS-IS: CardSource.cs:184-249 CanNotPlayThisOption — ICanNotPlayCardEffect 3-region 스캔(플레이어→퍼머넌트→자기) 후 `!MatchColorRequirement → true`(:240-245); MatchColorRequirement(:255-321)=옵션 전 색이 소유자 필드(브리딩 포함) 톱카드 색에 존재, IIgnoreColorConditionEffect만 면제.
- 헤드리스: OptionActivateAction.Validate에 색 검사 0(전 src grep `MatchColorRequirement|colorRequirement` 0건); 정적 메타 플래그만(:300-306).
- TODO: MatchColorRequirement 미러(브리딩 포함 population + ignore-color 효과 게이트) + ICanNotPlayCardEffect 연속 스캔. **라이브 — 모든 옵션 합법성에 영향.**

### TODO-43. [검증] 진화 시 1드로우 전면 부재
- AS-IS: CardController.cs:1526-1529 — isEvolution이면 DigivolveCount++ 후 **Draw 1**(일반/조그레스/버스트/앱퓨전 공통).
- 헤드리스: DigivolveAction.cs:263-283 카운터만; Digivolve/SpecialPlay/FreeDigivolve/FusionDigivolve 4파일+런타임 전체 DrawAsync 0건(직접 grep 확인).
- TODO: 진화 성공 지점에 공통 드로우 삽입 + 테스트 단언. **라이브 — 모든 진화에서 매번.**

### TODO-44. [검증] [End of Turn] 창 시점 역전 + 턴종료 취소(Main 복귀) 부재 (+once 리셋 시점)
- AS-IS: AutoProcessing.cs:675-727 — (구 턴 컨텍스트) pass 메모리=3 → OnEndTurn 스택·해소 → **메모리 재검사, 미달 시 SetMainPhase() 복귀**; until-턴종료 정리·InitUseCountThisTurn은 그 뒤 EndPhase(TurnStateMachine.cs:3151-3210).
- 헤드리스: MetadataActionProcessor.cs:783-834(직접 확인) — cleanup→턴 전환→메모리 플립 **후** OnEndTurn 방출(해소는 새 턴 상태에서), OnceFlags.ResetForTurn도 방출 직후·해소 전; 재검사/복귀 없음.
- TODO: EndTurn을 "구 턴 컨텍스트에서 OnEndTurn 해소→재검사→(미달 시 Main 잔류)→cleanup→전환" 순서로 재배열. BT1_021(EoTLose3Memory) 기포팅이라 **라이브**.

### TODO-45. [검증] 버스트 진화 임시성 부재 — IsBurstDigivolved + 턴종료 톱카드 트래시
- AS-IS: CardController.cs:1526-1538(직접 확인) — 버스트 성공 시 IsBurstDigivolved=true + AddTrashTopCardAtTurnEnd.
- 헤드리스: SpecialPlayAction.cs:347-361 영구 진화; BurstDigivolved/TrashTopCardAtTurnEnd 전 src 0건(직접 grep). **라이브(버스트 실행 시마다).**

### TODO-46. [보고] 시큐리티 디지몬 배틀이 삭제 파이프라인 전체 우회 (attack#1)
- AS-IS: CardController.cs:4179 — 완전한 IBattle → DestroyPermanentsClass(:4705): would-be-deleted 치환창(Evade/Barrier)·삭제 트리거·leave-play 정리·OnStartBattle/OnEndBattle·배틀 해시테이블·Pierce 판정 전부 통과.
- 헤드리스: SecurityResolver.cs:350-400 — 인라인 DP 비교 후 Trash 직행(치환창·cleanup·트리거·Fortitude·결과값 전무).
- TODO: 시큐리티 배틀을 BattleResolver 공용 경로로 통합. **라이브(Evade/Barrier/삭제트리거 공격자 패배 시).**

### TODO-47. [보고] 시큐리티 체크 창 순서 역전 + OnSecurityCheck 모집단 축소 (attack#2·#3)
- AS-IS: CardController.cs:3954 OnSecurityCheck를 공개 前 **전역 스캔**으로 수집·보관 → [Security] 활성효과 먼저 해소(:3987-4103) → 보관분 스택(:4111-4114) → 시큐리티 배틀.
- 헤드리스: SecurityResolver.cs:138 OnSecurityCheck **먼저**+SourceEntityId=공개카드로 self-스코프 축소(collector:309-313 드롭) → :144 [Security] 나중.
- TODO: 순서 원복(수집→[Security]→스택 해소) + OnSecurityCheck 전역 브로드캐스트화.

### TODO-48. [보고] 효과-기인 공격이 [When Attacking] 창(OnAllyAttack) 미발화 (attack#4)
- AS-IS: 모든 공격이 AttackProcess.Attack() 단일 진입(:73, :197-199 OnAllyAttack 스택); SelectAttackEffect(효과 공격)도 동일 코루틴.
- 헤드리스: EffectDrivenAttack.Initiate(:184-211)는 DeclareAttack만 — OnAttack/OnAllyAttack 발화는 수동 선언 액션에만.
- TODO: 효과-공격 경로에 어택창 발화 통합(단일 chokepoint 미러). Vortex/Execute/EndOfTurn 공격 **라이브**.

## P1 — restriction/immunity (조사: 인터페이스 29종 커버리지)

### TODO-49. [보고] ICanNotPlayCardEffect·ICanNotPutFieldEffect 연속 스캔 인프라 전무 (restriction#2·#3=play#6) — 스켈레톤만; PlayCardAction.Validate에 restriction 스캔 0. "플레이/등장 불가" 카드 포팅 전 선행 구축.
### TODO-50. [보고] 공격/블록 게이트가 연속 CanSuspend 미참조 + SuspendAttacker sink 우회 (restriction#4) — AS-IS CanAttackTargetDigimon/CanBlock의 `if(!CanSuspend) return false`(Permanent.cs:2230/2140) vs 헤드리스 정적 메타만(AttackPermanentAction.cs:233, BlockTiming.cs:247); producer 기존재(CPF:6623)라 카드 1장 착지 시 P0화.
### TODO-51. [보고] 언서스펜드-대상-공격-허가 grant의 3-인자 joint 스캔→자기 플래그 평탄화 (restriction#5=attack#22) — AS-IS Permanent.cs:2316-2359 (attacker,defender,effect) joint grant vs canAttackUnsuspendedDigimon bool+Execute만. defender-조건부 grant 표현 불가.
### TODO-52. [보고] CanNotEvolve joint counterpart(진화 카드) 미전달 (restriction#6) — AS-IS CanNotEvolve(target, **this**) vs EvaluateDigivolve counterpart null(DigivolveAction.cs:474).
### TODO-53. [검증] 토큰 진화 금지 하드가드 부재 (restriction#7=play#7) — AS-IS CardSource.cs:1291-1301 무조건 가드 vs DigivolveAction IsToken 0건. 토큰 인프라는 live.
### TODO-54. [보고] Digisorption 게이트 부재 (restriction#8, 기지 G11 정밀화) — Player.cs:1180-1326 두 변형(+조기-false 비대칭 구조) 미구현.
### TODO-55. [보고] DigiXros/Assembly 소재 대체 grant(ICanSelectDigiXros/Assembly) 부재 (restriction#9) — Permanent.cs:3796-3886 joint grant 스캔 vs 스켈레톤.
### TODO-56. [검증] SAttack/LinkMax/Invert 폴드에 per-소스 CanNotBeAffected 면역 가드 부재 (restriction#10=DP#3) — AS-IS 수집마다 `!TopCard.CanNotBeAffected`(Permanent.cs:1752/1772/1841/1861·919/944/964·1696/1713, 직접 확인) vs ContinuousModifierGate.ResolveSecurityAttack·LinkHelpers 무필터(직접 확인 — DP만 P1-DP-3 적용됨). DP와 동일한 SourceEntityId 면역 드롭 확장.
### TODO-57. [보고] CanNotTrashFromDigivolutionCards per-card 스캔 구조 부재 (restriction#11, 기지 debt 정밀화) — 3-region+causing joint vs 스탬프 스텁.
### TODO-58. [보고][INV] BlockTiming 공격자-자신-counterpart 조기 컷 프로브 발명 (restriction#12) — BlockTiming.cs:41-45 2차 호출이 AS-IS에 없는 프로브; per-candidate 경로 존재하므로 제거 가능.
### TODO-59. [보고][INV] CannotReturnToHand/Library의 AS-IS 중첩 루프 quirk(필드 0 플레이어의 player-효과 비스캔, Permanent.cs:746-818) 미반영 — 헤드리스 과잉 보호. AS-IS quirk 미러 여부 사용자 결정 필요(의도 vs 버그 판단).
### TODO-60. [검증] faceup 시큐리티 소스 과잉 폴드 — **골 3 구현 정정 필요**
- AS-IS: faceup 시큐리티를 소스로 스캔하는 getter는 **특정 집합만** — DP(377/546), LinkMax(931), Has-키워드(2442 Blocker/2643 Reboot/2734 Rush/3005 Alliance/3068 Collision), CanBeDestroyedByBattle(3263). CanSuspend(3698-3742, 직접 확인: 필드+플레이어만)·BaseDP·Invert·Strike·cost·DontHaveDP 등은 **비스캔**.
- 헤드리스: 골 3의 CollectFaceUpSecuritySourced가 ApplicableEffects 단일 지점에 폴드 → 전 소비자(DP·SAttack·cost·면역·restriction·battle-deletion)에 균일 적용 = 과잉.
- TODO: faceup 폴드에 소비자-식별(metric/kind) 게이트를 달아 AS-IS getter 집합으로 제한. (골 3 커밋 전 수정 — 사용자 결정 "AS-IS와 동일 구조"의 정확한 이행.)
### TODO-61. [보고] ImmuneFromDeDigivolve/StackTrashing 모집단 확대(AS-IS 필드-퍼머넌트만 vs 레지스트리 player-scope 포함) + DeDigivolve는 causing 인자 자체 없음 (restriction#15).
### TODO-62. [보고][P2 묶음] restriction 잔여 — 블록의 공격자 IsDigimon 가드(도달 불가), IsSecurityLooking 게이트 어댑터 무배선(구현은 존재, chokepoint 0 — PG minor 정밀화), CanReduceCost 3-인자 joint의 스코프 평탄화, 이동 빈-프레임 요구 부재(프레임 모델 자체 부재), 스캔 순회 순서(turn-player-first vs ordinal — OR-스캔이라 결과 등가), sink 선두 균일 면역 게이트 층 구조.

## P1 — play/digivolve/턴

### TODO-63. [보고] DigivolveAction 이동→지불 순서 역전 — AS-IS 지불(:968-981)→AfterPayCost(:985-991)→이동(:1026) vs 헤드리스 이동(:161-174) 후 지불(:176-206); PlayCardAction은 올바름(액션 간 비균일). BeforePayCost 감액 효과가 "이미 스택 위" 상태로 평가됨.
### TODO-64. [보고] 효과-구동 ignore 진화가 CannotIgnore 부정 게이트 우회 (P1-DV-1 정밀 핀) — AS-IS CardSource.cs:593-604 모든 ignore가 `&& CanIgnoreDigivolutionRequirement`인데 헤드리스 효과-경로(CPF:9174-9183)는 IgnoreRequirement.All/Level/Color 무조건 통과; IsDigivolveIgnoreBlocked는 player-Validate 분기에만.
### TODO-65. [보고] OnEnterFieldAnyone 크로스카드 브로드캐스트 부재 (play#9, 브릿지 갭의 플레이-지점 특정) — AS-IS CardController.cs:1691-1694 전 필드+플레이어 창 vs 자기+OnPlayReactivation만(GameFlowProcessor.cs:527 allow-list 명시 제외).
### TODO-66. [보고] Main 외 페이즈 EndTurnCheck 부재 — AS-IS 페이즈 경계 전반(TurnStateMachine.cs:579-880) vs Phase!=Main이면 NotApplicable(HeadlessMainPhaseFlow.cs:108-116).
### TODO-67. [보고] TurnEndMinMemory 스캔 스코프 축소 — AS-IS 양 플레이어 player-효과+전 필드 GetMinMemory 체이닝(AutoProcessing.cs:645-671) vs 턴 플레이어 BattleArea만+SET(last-wins)(HeadlessMainPhaseFlow.cs:16-36).
### TODO-68. [보고] 옵션 해소 존 모델 — AS-IS Execution 림보(AddExecutingCard → 해소 → 잔존 시 배치, CardController.cs:1739-1798) vs Hand→Trash 즉시(OptionActivateAction.cs:84-95); 해소 중 트래시-쿼리 +1 관측. (시큐리티 카드도 동일 패턴 = attack#11과 동근원 — Executing 존 도입 일괄 검토.)
### TODO-69. [보고][P2 묶음] play 잔여 — 복수 진화코스트 플레이어 선택 붕괴(auto-min), 필드 프레임 용량 게이트 부재, 실패-플레이 롤백 의미론(UntilCalculateFixedCost 실패시 소거 포함) 미표현, 해치의 OnEnterField 창 부재.

## P1 — DP/수치/비용

### TODO-70. [검증] 비-DP 메트릭 per-스텝 0-클램프 (DP#1) — AS-IS는 스테이지 말단 1회 클램프(CardSource.cs:849-857/924-932, Strike 1942-1947) vs 헤드리스 Evaluate가 매 모디파이어마다 Math.Max(minimumValue=0)(ModifierHelpers Evaluate — 직접 읽음). 중간 음수 소실: cost 3−5+2 → AS-IS 0 vs 헤드리스 2. **라이브 후보.** ⚠️수정 시 DP 경로(intermediate clamp는 AS-IS 의도)와 구분.
### TODO-71. [보고] 시큐리티 디지몬 DP가 CardDP 메커니즘이 아닌 permanent-DP 폴드로 계산 (DP#2) — AS-IS CardDP(CardSource.cs:2382-2443)=IChangeCardDPEffect 전용(면역·DPBoost·LinkedDP 무관, 보안배틀 중 카드만) vs SecurityResolver.cs:406-457이 ContinuousDpGate.ResolveDp 전체 폴드. zone-무필터 dpDelta 스태틱이 보안 DP에 오적용. **라이브 가능.**
### TODO-72. [보고] ChangeBaseDPGlobalEffect가 SET→ADD로, NotIsUpDown→isUpDown 그룹으로 오배치 (DP#5) — AS-IS "Origin DP is X" SET(ChangeOriginDP.cs:1023-1043) vs BaseDpDeltaKey Add(CPF:6443); FixedBaseDpKey 미사용.
### TODO-73. [보고] cost 2-스테이지(GetChangedCostItselef/GetChangedPayingCost 분리, 스테이지 경계 클램프, GetCostItself 질의값) 연속경로 붕괴 (DP#6) — 메타데이터 경로는 보존, 레지스트리 경로 단일 폴드.
### TODO-74. [보고] LinkMax 폴드에 InvertSecutiryValue 미전달 (DP#7) + LinkCost target-permanent 게이트 부재 (DP#12) + 링크 초과 처리(플레이어 선택·add-前 제거 vs 자동-oldest·add-後) (DP#10) + OnLinkCardDiscarded 과잉 방출 (DP#11, INV).
### TODO-75. [보고] SecurityAttackChanges/HasSecurityAttackChanges 메커니즘 부재 (DP#8) + DPWhenSuspended 스냅샷 부재 (DP#9) — 소비 카드(EX6_031·BT10_042·BT12_039/044·BT9_018) 미포팅 latent.
### TODO-76. [보고] DigiXros/Assembly 감액이 CanReduceCost 게이트 없이·연속 폴드 후에 적용 (DP#13) — AS-IS는 게이트 통과 시·폴드 이전(CardSource.cs:676/711).
### TODO-77. [보고] 진화 요구조건 매칭이 정의 메타데이터 기준 (DP#16) — AS-IS는 live CardColors/TreatedLevel(색·레벨 변경 효과 반영, CardSource.cs:596-600/941-947) vs DigivolutionCostHelpers.Matches가 CardRecord만.
### TODO-78. [보고][P2 묶음] DP 잔여 — 그룹 내 순서 소스(스캔순 vs Id ordinal), **주 DP getter(:641)는 NotIsUpDown 무정렬**(ActivatedTime 정렬은 301/472만 — 골 2 구축이 641 비대칭 미반영, 재확인), GetDP(ignorePermanent) 변형(AS-IS도 dead), cost NotIsUpDown 그룹 Set/Add 3분할, 팩토리 제로가드/null 처리, MandatorySelfPlayCostReduction rootCondition(=TODO-33), LinkHelpers 소발명 2건(linkedMax 메타 오버라이드·LinkedDP 감산 클램프), setFixedCost throw(=TODO-34 관련 CPF측).

## P1 — attack/battle/security

### TODO-79. [보고] OnEndBattle↔UntilEndBattle·OnEndAttack↔UntilEndAttack 만료 순서 역전 + OnEndAttack 공격자 생존 게이트 부재 (attack#5·#6) — AS-IS 해소 후 만료 vs 헤드리스 만료 후 해소(BattleResolver.cs:212/227, AttackPipeline.cs:359-425).
### TODO-80. [보고] Retaliation 재모델 발산 (attack#7) — AS-IS는 트래시에서 발화하는 사후 **효과-삭제**(DestroyPermanentsClass, 효과 해시테이블) vs 헤드리스 배틀-삭제 편입(DeletedByBattleKey) — 보호 클래스 반전·결과값 오염·면역 미적용.
### TODO-81. [보고] 배틀 결과값 산정 불일치 (attack#8·#9) — AS-IS 타이=양측 Winner(비교 시점 확정, 클론 스냅샷·WasTie·LoserCard 운반) vs 헤드리스 finalize-시점 생존자=승자(치환 생존자가 승자로 오기록).
### TODO-82. [보고] 시큐리티 체크 루프 Strike/가용치 동결 (attack#10) — AS-IS 매회 라이브 재평가 vs 진입 전 1회 고정(SecurityResolver.cs:100-107).
### TODO-83. [보고] 공개 시큐리티 카드 Executing 림보 부재 (attack#11) — TODO-68과 동근원.
### TODO-84. [보고] OnStartBattle 창 구조(전역 1회·클론 해시테이블·IsWithoutAttack 조건 해소 vs subject-scoped 2회·DP게이트 전) (attack#12) + DP-센티널 배틀에서 OnEndBattle/만료/결과값 발생 발명 (attack#13).
### TODO-85. [보고] Blitz 기제 발명 (attack#14) — AS-IS [On Play]/[When Digivolving] 트리거-공격 vs MemoryPass 페이즈 선언 허용 하드코드.
### TODO-86. [보고] Raid/Alliance/Progress가 OnAllyAttack 창 밖 고정 스텝 (attack#15) — AS-IS는 창 내부에서 타 트리거와 플레이어 순서 선택으로 인터리브.
### TODO-87. [보고] 공격/블록 서스펜드가 SuspendPermanentsClass 우회 (attack#16) — OnTappedAnyone 미발화·탭 시점 재필터·DPWhenSuspended 기록 없음(메타 직접 기록).
### TODO-88. [검증] DontBattleSecurityDigimon 플레이어-스코프 스캔 누락 (attack#17 = d04-08 재검증 item2 PARTIAL 교차 확인) — AS-IS 이중 루프(자기 효과 + 양 플레이어 EffectList; EX5_053의 UntilSecurityCheckEndEffects 경로) vs 공개 카드 자신만(SecurityResolver.cs:276-296). 매핑 문서의 ✅상환 표기 과장 — 문서 정정 포함.
### TODO-89. [보고] [Security] 효과 다중 순서 선택·전량 루프·isFaceDown 스냅샷 부재 (attack#18) + UntilSecurityCheckEnd 지속기간 부재 (attack#19) + isSecurityCehck/SecurityDigimon 전역 상태 부재 (attack#28).
### TODO-90. [보고][INV] 공격 선언 시 OnUseAttack·OnDeclaration 발명 창 2개 (attack#20) — AS-IS OnUseAttack=死 타이밍, OnDeclaration=메인 스킬 선언 전용; 헤드리스 3중 발화(AttackPermanentAction.cs:146-149). OnDeclaration 오발화 위험.
### TODO-91. [보고] IsEndAttack(강제 공격 종료)·BattleWithoutDigimon 룰 처리 좌석 부재 (attack#21).
### TODO-92. [보고][P2 묶음] attack 잔여 — 카운터 cut-in 스택/클론 스냅샷(2패스 자체는 충실), OnBlockAnyone 해시테이블·중간 가드, 죽은 공격자에 블록 창 노출, Execute 자기-삭제 창 밖 동기 실행, EffectDrivenAttack 옵션 재파생.

# 서브시스템 감사: deletion/replacement + 키워드 전수

## P0

### TODO-93. [검증] 삭제 확정 시 진화원 트래시(DiscardEvoRoots) 누락
- AS-IS: CardController.cs:3846(직접 확인) — 톱카드 트래시 직전 `permanent.DiscardEvoRoots()`(Permanent.cs:106-142, evoRoots·linkRoots 전부 AddTrashCard).
- 헤드리스: MatchStateMutationSink.cs:790(직접 확인)·BattleResolver.cs:192-194 — 톱만 트래시, 소스들은 ChoiceZone.None 영구 잔류(DeletionReplacementGate.cs:571-573이 이를 전제로 설계 — POST Decode/Save/Partition용).
- TODO: 삭제 확정 경로에 소스 트래시 통합(POST 창이 소스를 쓴 뒤 잔여 트래시 = AS-IS 순서와 함께 재설계, TODO-96 연동). 트래시 매수·트래시-쿼리 술어·"트래시에 놓일 때" 트리거 모집단 전부 영향. **라이브(진화원 보유 스택 삭제 시 항상).**

### TODO-94. [검증] Scapegoat 희생 후보에 아군-**디지몬** 게이트 누락
- AS-IS: Scapegoat.cs:53 — `IsPermanentExistsOnOwnerBattleAreaDigimon && != holder`.
- 헤드리스: DeletionReplacementGate.cs:492-526(직접 확인) — battleArea 전 카드(!=holder만) → 테이머/옵션도 제물 가능. **라이브.**

## P1 — 삭제 파이프라인/치환 창

### TODO-95. [보고] 바운스(손/덱) 경로에 would-remove-field 컷인 창 전무 (deletion#4) — AS-IS CardController.cs:2311-2330/2638-2700: WhenReturnto*Anyone+WhenRemoveField 컷인 → willBeRemoveField 재확인(Armor Purge/Fragment/MaterialSave가 바운스 치환·취소 가능) vs sink ReturnTo* 즉시 이동. **라이브(Armor Purge 보유자 바운스 시).**
### TODO-96. [보고] Decode/Partition PRE(WhenRemoveField 컷인)→POST(트래시 후) 재배치 (deletion#11) + Decode/Fortitude의 CanPlayAsNewPermanent 게이트 누락(#12) + Partition 2장 동시 플레이 원자성(#13) + Fragment 원자적 지불(#10). TODO-93과 한 묶음(삭제-확정 시퀀스 재설계).
### TODO-97. [보고] WhenPermanentWouldBeDeleted 창에 WhenRemoveField-타이밍 카드효과 미브릿지 (deletion#3) — AS-IS는 두 창이 한 세트(CardController.cs:3690-3705).
### TODO-98. [보고] ACE Overflow가 스택 전체가 아닌 톱만 (deletion#2) — AS-IS AceOverflowClass(cardSources 전체)+DiscardEvoRoots 내 개별 Overflow+턴 플레이어 우선 정렬 vs ApplyAceOverflowOnLeave(record 1장).
### TODO-99. [보고] 삭제-직전 파라미터 스냅샷(DP/Level/Cost/CardNames/Traits/PermanentJustBeforeRemoveField) 인프라 부재 (deletion#5) — [On Deletion] 활성 게이트(OnDeletion.cs:113-140)가 이 동일성 판정 기반.
### TODO-100. [보고] Save 대상 기본 술어(비토큰 **테이머**) 누락 (deletion#14) — AS-IS Save.cs:16-30 vs SaveTargets=소유자 battleArea 전부+첫 후보 자동 부착.
### TODO-101. [보고] Fragment/Decoy **옵션 제공** 게이트의 CanBeDestroyedBySkill 스캔 누락 (deletion#9) — 실행은 sink 커버, 제공 여부 게이트만 미커버.
### TODO-102. [보고] Scapegoat 자기-효과-삭제 불발 게이트 누락 (deletion#8) — AS-IS IsByEffect(IsOwnerEffect)→false; sink가 기록하는 DeletedByOwnEffectKey를 Scapegoat 게이트가 미독(Partition만 읽음).
### TODO-103. [보고][INV] 룰(비-효과) 삭제에 과잉 가드 (deletion#6) — AS-IS는 cardEffect==null(DP0 등)이면 가드 전부 스킵 vs 헤드리스 rule:dp-zero에도 cannotBeDeleted/치환 적용.

## P1 — 키워드 grant→consume/기제

### TODO-104. [보고] Iceclad 소비자가 키워드 게이트 미독 (deletion#17) — BattleResolver.CompareBattleStats가 hasIceclad 메타만(ContinuousKeywordGate.Iceclad 선언돼 있으나 미소비) — GR-005 Blocker와 동형 재발.
### TODO-105. [보고] hasVortex(GrantVortex 뮤테이션 플래그) 판독자 0 (deletion#18) — EndOfTurnEffectAttack이 HasKeyword만 확인 → 뮤테이션 경유 부여 Vortex/Overclock류 종료턴 창 미개방 — GR-007 계열 재발.
### TODO-106. [보고] Overclock 아군 삭제가 풀 파이프라인 미경유 + 성공-시-공격 **강제** 누락 (deletion#19) — AS-IS DeletePermanentAndProcess…(치환 가능, 성공시에만·강제 공격) vs SacrificeAsync 직행+선택적 공격.
### TODO-107. [보고] 서스펜드-비용 공통 우회 (deletion#20 ⊃ attack#16=TODO-87) — Alliance/Evade/Training이 CanSuspend 제약·CanNotBeAffected·OnTappedAnyone·탭 실패 분기 없이 IsSuspended 직접 기록. sink SuspendKind 경유로 통일.
### TODO-108. [보고][P2 묶음] 키워드 잔여 — MaterialSave 창/선택/술어 스텁(deletion#23), Training face-down 부착·강제성(deletion#24), Raid 후보 정규화(현 방어자 제외 — AS-IS 내부 비대칭의 한쪽 정규화, deletion#22), **GainFortitude AS-IS 원문 死버그를 헤드리스가 '수정'** — 원문 보존 vs 개선 사용자 결정 필요(deletion#25), Ascension/Alliance 강제 트리거+내부 선택 구조 차이+CanAddSecurity 게이트(deletion#15).

# Cross-check: Commons GiveEffect/게이트/MinMax (추가 검증분)

### TODO-109. [보고] StartOfMainAttackEffect CanResolve에 CanNotBeAffected+CanAttack 게이트 미미러 (CPF:1035 vs AS-IS 2중 게이트).
### TODO-110. [보고] GainIgnoreDigivolutionRequirementPlayerEffect(술어형 일반 오버로드) 팩토리 부재 — 기저 메커니즘(AddedDigivolutionRequirementPredicateEffect CPF:1374)은 존재하나 미노출. (TODO-35와 동근원)
### TODO-111. [보고] CanTrigger* 게이트 6종 의미론 부재 — WhenDeleteOpponentDigimon(비-배틀 일반형)/CardsReturnToLibraryFromTrash(비-owner)/OwnerCardsReturnToHandFromTrash/OnTrashSelfLink(ed)Card/IsTopCardSamePermanent/CanActivatePermanentSuspendCostEffect.
### TODO-112. [보고] MinMax 계열 permanentCondition(부분집합 min/max: "네 녹색 중 최대 DP") 술어 전면 drop — tie 의미론 자체는 1:1 확인.
### TODO-113. [검증-교차] PlayerScopeModifierEffect(CPF:1839) 생성자에 dynamicValue·isInheritedEffect 부재 = TODO-31/32의 **공통 근원** — 이 생성자 확장 1건으로 값변경 5-7종 팩토리 일괄 상환 가능(ChangeDP/SAttack/BaseDPGlobal/InvertSA/SecurityDigimonCardDP). isInheritedEffect drop은 계승 게이트(ContinuousScopeEvaluation:50)가 아예 적용 안 됨을 의미.

## 참고 (검증 노트)
- joint-scan 4항목 재검증: CanNotSelectBySkill CONFIRMED(문서화된 over-scope latent 노트 유지), CanNotMove CONFIRMED, CannotIgnoreDigivolutionCondition CONFIRMED(단 시그니처 축소: AS-IS 3-arg `(Player,Permanent,CardSource)` → 헤드리스 2-arg, Player 인자 drop + 인자순 상이 — 미래 producer 포팅 시 어댑트 필요, latent 노트). 4항목 모두 현재 producer 0 = latent 인프라(AS-IS도 3종은 latent).
- AS-IS `ActivateClass.SetUpActivateClass` 시그니처의 once-per-turn은 `order` 파라미터가 아니라 `maxCountPerTurn`+IsSameEffect(HashString) — 기존 설계 문서 전제와 정합(TODO-14 참조).

## 우선순위 제안 (착수 순서)
1. **P0 라이브 룰 결손 즉시 상환**: TODO-43(진화 드로우)·42(옵션 색요건)·44(OnEndTurn 역전)·45(버스트)·93(진화원 트래시)·94(Scapegoat) — 각각 국소·검증 완료라 즉시 가능.
2. **트리거 창-루프 구조 재설계**(TODO-1~8+18): 개별 패치 불가 덩어리 — 별도 설계 문서 후 일괄. P0-7(fizzle wedge)·P0-8(dedupe)·P0-5(once 소모)는 구조 재설계 전 선행 hotfix 가능.
3. **attack/security 창 순서·시큐리티 배틀 통합**(TODO-46~48·79~92): BattleResolver 공용화 축.
4. **삭제-확정 시퀀스 재설계**(TODO-93·95~103): DiscardEvoRoots+PRE창+치환 한 묶음.
5. **공통-근원 일괄 상환**: PlayerScopeModifierEffect 생성자(TODO-113→31/32), 서스펜드 sink 통일(TODO-107), faceup 폴드 게이트(TODO-60 — 골 3 정정).
6. latent 팩토리/게이트 표면(TODO-25~41·49~78·95~112 잔여)은 트리거 카드 포팅 순서에 맞춰 선행 구축.
