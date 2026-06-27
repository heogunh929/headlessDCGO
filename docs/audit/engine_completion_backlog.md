# 기본 엔진 완성 백로그 (A~D 전체 작업 리스트업)

- 작성일: 2026-06-27
- 정의: **A + B + C + D 전부 완료 = "기본 엔진 완성"**. 아래는 각 항목을 구체적 구현 단위로 쪼갠 체크리스트(백로그). ID로 참조/체크오프.
- 상위 문서: [engine_completion_checklist.md](engine_completion_checklist.md) (갭 근거·숫자), [timing_emission_gaps.md](timing_emission_gaps.md)
- 표기: `[ ]` 미착수 · `[~]` 부분 · `[x]` 완료. 각 항목은 (구현 + 단위테스트) 1세트.

---

## A. 기반 프레임워크

### F-1. EffectDuration 시스템 🔴
- [x] F-1.1 `EffectDuration` enum 8종 — `Headless/Effects/EffectDuration.cs` (CV-A1, 2026-06-27)
- [x] F-1.2 duration 태깅 — `EffectBinding.Duration`(연속 effect binding에 보존) + `EffectRegistry.RemoveWhere`
- [x] F-1.3 만료 훅: 턴종료(each/owner/opp) — `HeadlessEndTurnCleanupFlow`→`EffectDurationExpiry.ExpireTurnEnd`
- [x] F-1.4 만료 훅: 전투 끝 — `BattleResolver`→`ExpireBattleEnd`
- [x] F-1.5 만료 훅: 공격 끝 — `AttackPipeline.AdvanceEndAttack`→`ExpireAttackEnd`(공격 Completed 전환 직전). 테스트: `tests/G3.5-F15.AttackEndDurationExpiry` 1/1
- [x] F-1.6 만료 훅: 언탭/액티브페이즈 — `HeadlessEarlyPhaseFlow`(Unsuspend)→`ExpireUnsuspend`
- [ ] F-1.7 만료 훅: UntilCalculateFixedCost — `ExpireFixedCostCalc` 존재하나 **보류**: 비용 헬퍼(`PlayCostHelpers`/`DigivolutionCostHelpers.TryResolveCost`)는 순수 read-side로 legal-action 열거마다 반복 호출됨 → 거기서 expire 호출 시 매 쿼리마다 레지스트리 변형(버그). 전용 "비용 재계산" 변형 지점이 생길 때 배선
- 테스트: `tests/G3.5-CVA1.EffectDuration` 6/6 (enum·each/owner/opp 턴종료·battle/attack·언탭·permanent 생존)

### F-2. 선택→연산 프레임워크 🔴 (CV-A2 진행, 2026-06-27)
- [x] F-2.1 "조건 매칭 대상 N개 선택" permanent 빌더 — `SelectPermanentEffect.BuildRequest` (라이브 보드 열거+predicate 필터 → Permanent ChoiceRequest). **card/hand 빌더는 F-2.4와 함께 잔여**
- [x] F-2.2 Root 존 추상화: Hand/Library/Trash/Security/Clock/Execution/DigivolutionCards/LinkedCards — `SelectCardEffect.Root` enum(1:1) + `RootZone` 매핑 + `BuildRequest`가 해당 존 열거 (B-5)
- [~] F-2.3 SelectPermanent 모드→뮤테이션 매핑 — `SelectPermanentEffect.Mode`(11종 1:1) + BuildMutations: Tap/UnTap/Destroy/Bounce/PutLibrary(top·bottom)/PutSecurity(top·bottom) 매핑 완료. **Degenerate→D-4 미구현(NotSupported), Attack/Custom→무뮤테이션(콜백)**
- [~] F-2.4 SelectCard/Hand 모드 매핑 — `SelectCardEffect.Mode`(AddHand/Discard/PlayForFree/PlayForCost/Custom, 원본 1:1) + BuildMutations: AddHand=ReturnToHand·Discard=TrashCard 완료. **PlayForFree/PlayForCost=effect-Play(F-3.7) 잔여(NotSupported)**. 테스트 `tests/G3.5-B5.SelectCardEffect` 6/6
- [x] F-2.5 선택 결과→대상별 연산 — `SelectPermanentEffect.Apply(sink, selected)` 대상별 뮤테이션 적용(선택순). **after-select 콜백은 잔여**
- [x] F-2.6 강제/선택(canNoSelect)·부분선택 종료(canEndNotMax) 규칙 — BuildRequest의 min/max/canSkip 산출에 반영
- 구현: `Assets/Scripts/Script/SelectPermanentEffect.cs`(AS-IS 미러). 테스트: `tests/G3.5-CVA2.SelectPermanentEffect` 7/7 (predicate 필터·exact/canNoSelect/canEndNotMax 카운트규칙·Destroy e2e·Tap·Bounce·Mode 매핑)

### F-3. 뮤테이션 vocabulary 확장 🔴 (B와 결합) — 다수 kind는 `MatchStateMutationSink`에 이미 존재
- [x] F-3.1 **Delete**(effect 삭제) 뮤테이션 kind + 게이트 존중(BattleDeletionGate/연속 prevent) — CV-B1
- [x] F-3.2 Bounce(→hand)=`ReturnToHandKind` / DeckReturn=`ReturnToDeckTopKind`·`ReturnToDeckBottomKind` (CV-A2 SelectPermanent에서 결합·검증)
- [x] F-3.3 Suspend/Unsuspend=`SuspendKind`·`UnsuspendKind` (CV-A2 Tap/UnTap 결합, CV-A4 emit)
- [x] F-3.4 Discard(hand→trash)/DeckTrash=`TrashCardKind`(존 무관 trash) + OnDiscardHand/Library 타이밍(F-6.5)
- [~] F-3.5 TrashSecurity=`TrashCardKind`(security→trash)+OnDiscardSecurity / Recovery(덱→시큐리티)=`AddToSecurityKind`(library 출발). 전용 헬퍼 래핑은 B-6에서
- [ ] F-3.6 Reveal(덱 top N) + 결과 노출 🔴 (신규 kind 필요)
- [~] F-3.7 effect-Play 뮤테이션 — **PlayForFree 완료**: `MatchStateMutationSink.PlayCardKind`(소스존→BattleArea face up + enteredThisTurn 소환멀미) + `SelectCardEffect.PlayForFree` 결합. **PlayForCost 잔여**(D-8 비용 파이프라인 필요, NotSupported). 테스트 `tests/G3.5-B5.SelectCardEffect` 7/7
- [ ] F-3.8 Token 생성 / 소재·링크 trash / 트래시→hand·deck 🟡 (트래시복귀는 ReturnToHand/Deck kind로 가능, Token/소재trash는 신규)

### F-4. once-per-turn 자동 게이팅 🟠 — 완료 (2026-06-28)
- [x] F-4.1 effect 정의 `MaxCountPerTurn` 필드(기존) + use-count 추적 — `OnceFlagController`(`OnceFlagHelpers` 데이터 위 mutable 보유자), EngineContext에 `OnceFlags` 등록
- [x] F-4.2 트리거 활성 판정 자동 반영 — `GameFlowProcessor` 수집 루프에서 `Find(effectId).Effect.Definition.MaxCountPerTurn` 조회 → `OnceFlags.TryActivate`로 게이트(원본 `isOverMaxCountPerTurn`+카운트 증가)
- [x] F-4.3 턴 시작 use-count 리셋 — `MetadataActionProcessor.EndTurn`→`OnceFlags.ResetForTurn`(원본 `InitUseCountThisTurn`)
- 테스트: `tests/G3.5-F4.OncePerTurnGating` 5/5 (uncapped·cap1·cap2·reset·루프 e2e 턴내 1회+다음턴 재발동)

### F-5. 플레이어 스코프 연속효과 🟠 — 대부분 완료 (2026-06-28)
- [x] F-5.1 "플레이어의 조건매칭 전 permanent에 적용" 연속 스코프 — `PlayerScopeContinuousHelpers`(마커 `playerScope`+`scopePlayerId`+조건 `scopeCardType`/`scopeMeta*`). ±DP·cannot-* 검증. (±sAttack/keyword grant는 동일 패턴으로 자동 적용 — 같은 effect Values 경유)
- [x] F-5.2 게이트 확장 — `ContinuousScopeEvaluation.EvaluateForCard`(카드타깃 ∪ 플레이어스코프 결합 평가)를 `ContinuousDpGate`·`ContinuousRestrictionGate` 양쪽이 호출
- [ ] F-5.3 IgnoreDigivolutionRequirement player effect — 진화요건 무시 player-scope(진화 검증 경로 연계 필요, 별도)
- 테스트: `tests/G3.5-F5.PlayerScopeContinuous` 4/4 (소유자 스코프·CardType 조건·cannot-attack 제한·ConditionMatches)

### F-6. 타이밍 emit 중앙화 + 누락분 🟠 (CV-A4 진행, 2026-06-27)
- [~] F-6.1 존-이동/뮤테이션 공통 레이어 자동 emit — 존이동은 `TriggerTimingMap.Derive`(CardMoved→타이밍 파생)로 중앙화됨. 비-이동 상태변화(suspend 등)는 `MatchStateMutationSink.EmitTiming`로 emit
- [x] F-6.2 페이즈계: **OnStartMainPhase**(`AdvancePhaseAsync` 메인 진입, `MainPhaseEntered` 신호) + **OnEndMainPhase/OnEndAttackPhase**(`PassAction` Main→MemoryPass 핸드오버 시, 메인 종료 플레이어 대상). 원본은 OnStartMainPhase만 실발화·나머지는 enum 선언만 → 전진 호환 배선. 테스트: `tests/G3.5-F62.PhaseTimingEmission` 5/5
- [~] F-6.3 전투계: **OnEndBattle 완료**(`BattleResolver` 해결 후 emit). **OnStartBattle 잔여**(DP비교 전 동기 해결 윈도우 필요 — 단순 emit 시 부정확). OnEndAttack/OnAttackTargetChanged/OnEndBlockDesignation/OnDetermineDoSecurityCheck/OnGetDamage/OnKnockOut 잔여
- [x] F-6.4 상태계: OnTappedAnyone/OnUnTappedAnyone(sink Suspend/Unsuspend, CV-A4), OnMove(육성→배틀 승격 파생, CV-A4), **OnAddDigivolutionCards**(`DigivolveAction` 소재 부착 후, 받는 카드 스코프), **OnFaceUpSecurityIncreased**(sink AddToSecurity faceUp 시, 글로벌). 테스트: `tests/G3.5-F64.DigivolveSecurityTimingEmission` 4/4
- CV-A4 구현: `TriggerTimings`(OnTapped/OnUntapped/OnMove/OnEndBattle 상수) + `TriggerTimingMap`(OnMove 파생) + `MatchStateMutationSink`(GameEventQueue 주입, EmitTiming) + `BattleResolver`(OnEndBattle) + `EngineContext`(큐 호이스트). 테스트: `tests/G3.5-CVA4.TriggerTimingEmission` 7/7
- [~] F-6.5 카드이동계: **OnDiscardHand/Security/Library**(non-field zone→Trash 파생), **OnReturnCardsToHand/LibraryFromTrash**(Trash→Hand/Library 파생), **OnPermamemtReturnedToHand**(field→Hand 파생), **OnRemovedField**(field-leave 파생) 완료 — `TriggerTimingMap`. **잔여**: WhenTopCardTrashed/OnDigivolutionCardDiscarded/OnDigivolutionCardReturnToDeckBottom(top/소재 stack 컨텍스트 필요, 존파생 불가). 테스트: `tests/G3.5-F65.CardMovementTimingEmission` 9/9
- [~] F-6.6 액션계: **OnUseOption** 완료(`OptionActivateAction`). **잔여**: OnUseDigiburst(디지버스트 미구현), OnAllyAttack(아군 공격 로직 필요), OnDeclaration(선언 시점 구분 필요). 테스트: `tests/G3.5-F67.ActionCostTimingEmission`
- [x] F-6.7 코스트계: **BeforePayCost/AfterPayCost** — PlayCard/Digivolve/OptionActivate 3개 코스트 지점에서 지불 전후 emit(subject=카드). 테스트: `tests/G3.5-F67.ActionCostTimingEmission` 4/4
- [ ] F-6.8 would계(예측 트리거): WhenWouldLink, WhenWouldDigivolutionCardDiscarded, WhenPermanentWouldBeDeleted; AfterEffectsActivate
- [ ] F-6.9 링크계: WhenLinked, OnLinkCardDiscarded (D-1 동반)

### F-7. 계승효과(inherited) 활성 모델 🟡 — 완료 (2026-06-28)
- [x] F-7.1 IsInheritedEffect 활성규칙 — `InheritedEffectHelpers.IsInheritedEffectActive`(소재가 under-card·flip 안됨·permanent Digimon) + `IsMainEffectActive`(top일 때만). 원본 `ICardEffect.CanUse` inherited 분기 미러
- [x] F-7.2 소재 stack→top 부여 경로 — `InheritedEffectHelpers.ActiveInheritedSources`(DigivolutionStack의 non-flipped under-card 열거, DigivolutionStackReader 연계)
- 테스트: `tests/G3.5-F7.InheritedEffectActivation` 9/9 (under-card 활성·top 제외·flip 차단·non-Digimon 차단·off-stack·main 효과·ActiveInheritedSources 3종)
- 참고: 규칙/경로 헬퍼 제공 완료. 라이브 트리거 게이트에 자동 연동(stack을 트리거 수집에 주입)은 카드 포팅 시 author가 호출(빌딩블록 제공 방식)

### F-8. 조건/쿼리 헬퍼 커버리지 🟡 — 대부분 기존 이동 헬퍼로 충족 (2026-06-28 재조정)
- [~] F-8.1 Min/Max — `MinMaxRequirementHelpers` IsMin/MaxDP·Cost·Level **완료**(기존). **DigivolutionCards 메트릭만 잔여**(enum에 DP/PlayCost/Level만 → 소재수 메트릭 추가 필요)
- [~] F-8.2 매칭 — `CardRequirementHelpers` HasName/HasColor/HasAllColors/HasTrait/HasGroupedTrait **완료**(기존), level은 MinMax로. type 매칭 술어만 명시 보강 여지
- [x] F-8.3 존 카운트·존재 — `ZoneQueryHelpers` Library/Trash/Security/Sources/Query **완료**(기존)
- [x] F-8.4 턴/소유 체크 — `TurnOwnershipHelpers.IsOwnerTurn/IsOpponentTurn/IsOwner/IsOpponent` (신규). 테스트: `tests/G3.5-F84.TurnOwnershipHelpers` 10/10
- [ ] F-8.5 특수: IsJogress/IsDiXros/IsDPZeroDelete/IsTopCardInTrash 등 (고급, D-5/D-2 연계 시)

---

## B. 공통 게임 연산 (고빈도)

- [~] B-1 **디지몬 삭제(effect Delete)** — `MatchStateMutationSink.Delete` kind: trash + `deletedByEffect`, 정적 `cannotBeDeleted` + 연속 Delete/Prevent 존중(sink에 registry 주입). `tests/G3.5-CVB1` 3/3. 대상 선택=CV-A2(SelectPermanentEffect.Mode.Destroy) 결합 완료, OnDeletion emit=CV-A4(field→Trash 파생) 확인 완료. **남음**: 소재 스택 처리 🔴
- [x] B-2 **±DP / ±Security Attack / ±cost (지속)** — ±DP=`ContinuousDpGate`(기존). ±Security Attack/±PlayCost/±DigivolutionCost=`ContinuousModifierGate`(신규, DpGate 패턴). 카드타깃+player-scope(F-5)+duration(F-1) 자동. 테스트: `tests/G3.5-B2.ContinuousModifierGate` 5/5 🔴
- [x] B-3 바운스(→hand) / 덱(top·bottom) 되돌리기 — `ReturnToHand`/`ReturnToDeckTop/Bottom` kind + `SelectPermanentEffect`(Bounce/PutLibrary, CV-A2) 결합 완료
- [x] B-4 Suspend/Unsuspend (effect, 대상선택) — `Suspend`/`Unsuspend` kind + `SelectPermanentEffect`(Tap/UnTap, CV-A2) + emit(CV-A4) 완료
- [~] B-5 Draw / discard(손버림) / deck-trash — `DrawCards` kind(기존) + **`SelectCardEffect`(신규)**: Discard(손버림=Hand root, mill=Library root)·AddHand(트래시복귀). 테스트 `tests/G3.5-B5.SelectCardEffect` 6/6. **잔여**: Draw 선택 UI 결합(드로우 자체는 kind로 가능)
- [ ] B-6 시큐리티 trash / Recovery(덱→시큐리티) 🟠
- [ ] B-7 덱 top N 공개 + 선택 처리(reveal & select) 🟠
- [~] B-8 effect로 카드 플레이 — **무료(PlayForFree) 완료**(PlayCardKind, F-3.7; SelectCardEffect로 손/트래시/시큐 등 Root에서 플레이). **코스트 변형 잔여**(D-8) 🟠
- [ ] B-9 토큰 생성/플레이 🟡
- [ ] B-10 소재(디지볼브 카드)/링크 카드 trash·복귀 🟡
- [ ] B-11 트래시→손/덱 복귀 🟡

---

## C. 키워드 (각 = 연속 등록 + 전투/공격 소비 훅 + 테스트)

### 이미 실효 (참고)
- [x] Blocker · [x] Jamming · [x] Reboot · [x] Piercing

### C-그룹1: 기본 전투 🟠
- [~] C-1 Rush (플래그+N-1 일부) — effect grant + 소환턴 공격 완성
- [~] C-2 Blitz (플래그) — 상대 턴 공격
- [ ] C-3 Raid — 시큐리티 직접공격(+공격중 효과) *(D-3과 동일)*

### C-그룹2: 방어 🟠
- [ ] C-4 Decoy (강제 블록 유도)
- [ ] C-5 Barrier (첫 효과/공격 무효)
- [ ] C-6 Fortitude (언탭 시 재블록/생존)
- [ ] C-7 Evade (턴1 공격 회피)

### C-그룹3: 반격/처형 🟡
- [~] C-8 Retaliation (플래그) — 블록 시 반격 완성
- [ ] C-9 Execute (블록 성공 시 파괴)
- [ ] C-10 Collision (블록 상호파괴)

### C-그룹4: 자원/조건 🔵
- [ ] C-11 Fragment (플레이 시 트래시) · C-12 Iceclad (공격 시 트래시)
- [ ] C-13 Decode · C-14 Partition · C-15 Progress · C-16 Overclock (trait 조건/버프)
- [ ] C-17 Ascension · C-18 Alliance (드로우) · C-19 Scapegoat (다중대상 면역) · C-20 Vortex (끌어오기/플레이어공격)

### C-그룹5: 기타 🔵
- [~] C-21 ArmorPurge (플래그) · [ ] C-22 Save · [ ] C-23 Material Save · [ ] C-24 Training

---

## D. 대형 서브시스템 (각각 독립 구축)

- [ ] D-1 **Link** — 링크 카드 부착/해제 + 링크코스트·max 관리 + WhenLinked/OnLinkCardDiscarded(F-6.9)
- [ ] D-2 **Appfuse(앱퓨전)** — 이름/조건 기반 다카드 융합
- [ ] D-3 **Raid** — 시큐리티 직접공격 + 공격중 효과 *(C-3)*
- [ ] D-4 **De-Digivolve(진화퇴화)** — 소재 N장 제거(stack 분리)
- [ ] D-5 **DNA Digivolve(Jogress) / DigiXros** — 다permanent/다카드 융합 진화
- [ ] D-6 **Blast / Arts Digivolve** — 코스트리스/특수 진화 경로
- [ ] D-7 **효과 무효화(invalidation)** — 연속효과를 끄는 메커니즘
- [ ] D-8 **코스트 감소 파이프라인** — BeforePayCost 단계 + "감소 불가" replacement
- [ ] D-9 **Recovery / Token / Mind Link / Delay Option** — B-6/B-9 연계

---

## 진행 순서 (권장)
1. **A 기반** F-1 → F-2 → F-3 → F-6 → F-4 → F-5 → F-7 → F-8
2. **B 공통** B-1(Delete) → B-2 → B-3~B-11
3. **수직 슬라이스** 대표 3~5장 → 정규 템플릿/헬퍼 확정
4. **C 키워드** 그룹1→5
5. **D 서브시스템** 세트별
6. → 로컬 LLM에 per-card 인계

## 카운트
- A: 8개 영역 / 세부 ~35
- B: 11
- C: 24 (실효 4 제외 시 ~20)
- D: 9
- **총 구현 단위 ≈ 70+ (테스트 동반)**
