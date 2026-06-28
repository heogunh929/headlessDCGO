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
- [x] F-3.5 TrashSecurity=`TrashCardKind`(security→trash)+OnDiscardSecurity / Recovery(덱→시큐리티)=`AddToSecurityKind`(library 출발). **전용 배치 뮤테이션(`RecoverKind`/`TrashSecurityKind`)은 B-6에서 완료**
- [x] F-3.6 Reveal(덱 top N) + 결과 노출 — **B-7에서 완료**: `RevealAndSelect`가 라이브러리 top N을 choice 후보로 노출(reveal) + 선택/분배. 별도 sink kind 대신 choice-흐름(`ChoiceType.RevealSelect`)으로 구현.
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
- [~] F-6.8 would계(예측 트리거): **WhenPermanentWouldBeDeleted 부분 토대** — `DeletionReplacementGate`(전투+효과 삭제 직전 대체 Evade/Barrier, 적-효과 리다이렉트 Decoy, OnDestroyed replay Fortitude). 잔여: WhenWouldLink, WhenWouldDigivolutionCardDiscarded; AfterEffectsActivate
- [ ] F-6.9 링크계: WhenLinked, OnLinkCardDiscarded (D-1 동반)

### F-7. 계승효과(inherited) 활성 모델 🟡 — 완료 (2026-06-28)
- [x] F-7.1 IsInheritedEffect 활성규칙 — `InheritedEffectHelpers.IsInheritedEffectActive`(소재가 under-card·flip 안됨·permanent Digimon) + `IsMainEffectActive`(top일 때만). 원본 `ICardEffect.CanUse` inherited 분기 미러
- [x] F-7.2 소재 stack→top 부여 경로 — `InheritedEffectHelpers.ActiveInheritedSources`(DigivolutionStack의 non-flipped under-card 열거, DigivolutionStackReader 연계)
- 테스트: `tests/G3.5-F7.InheritedEffectActivation` 9/9 (under-card 활성·top 제외·flip 차단·non-Digimon 차단·off-stack·main 효과·ActiveInheritedSources 3종)
- 참고: 규칙/경로 헬퍼 제공 완료. 라이브 트리거 게이트에 자동 연동(stack을 트리거 수집에 주입)은 카드 포팅 시 author가 호출(빌딩블록 제공 방식)

### F-8. 조건/쿼리 헬퍼 커버리지 🟡 — 대부분 기존 이동 헬퍼로 충족 (2026-06-28 재조정)
- [x] F-8.1 Min/Max — `MinMaxRequirementHelpers` IsMin/MaxDP·Cost·Level·**DigivolutionCards**(소재수 메트릭, 2026-06-29 복원) 전부 완료. 테스트 `tests/G3.5-F81.DigivolutionCardsMinMax` 4/4
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
- [x] B-6 시큐리티 trash / Recovery(덱→시큐리티) — **effect 레벨 완료**: `MatchStateMutationSink`에 플레이어-스코프 뮤테이션 `RecoverKind`(top N library→security, AS-IS `IRecovery`/`IAddSecurityFromLibrary`, faceUp 옵션)·`TrashSecurityKind`(N security→trash, AS-IS `IDestroySecurity`, fromTop 옵션, **`OnDiscardSecurity` emit**) 추가 — 배치 프리미티브(`IZoneMover.AddSecurityFromLibraryAsync`/`TrashSecurityAsync`, 기존)를 effect 어휘로 노출(액션 레벨은 이미 존재). 테스트: `tests/G3.5-B6.SecurityRecovery` 4/4(recover·trash·라이브러리 cap·player 누락 unsupported). (2026-06-28)
- [x] B-7 덱 top N 공개 + 선택 처리(reveal & select) — **완료**: 신규 `RevealAndSelect`(AS-IS `RevealLibrary.RevealDeckTopCardsAndSelect`) — 라이브러리 top N peek → `ChoiceType.RevealSelect` agent 선택(up to K) → 선택분=목적지A·나머지=목적지B 분배(`RevealDestination` Hand/DeckTop/DeckBottom/Trash, 요청 id 인코딩) + `MetadataActionProcessor` 라우팅. F-3.6도 이로 충족(reveal+노출은 choice 후보로). 테스트: `tests/G3.5-B7.RevealSelect` 4/4(오픈·선택→hand/나머지→bottom·skip·빈덱). (2026-06-28)
- [~] B-8 effect로 카드 플레이 — **무료(PlayForFree) 완료**(PlayCardKind, F-3.7; SelectCardEffect로 손/트래시/시큐 등 Root에서 플레이). **코스트 변형 잔여**(D-8) 🟠
- [x] B-9 토큰 생성/플레이 — **완료**: `MatchStateMutationSink.CreateTokenKind`(플레이어-스코프, AS-IS `CardEffectCommons.PlayToken`) — 토큰 정의 id + base instance id + count로 `IsToken` 인스턴스 N개 생성(소환멀미, tokenTapped 옵션 서스펜드) + 배틀존 배치(None→BattleArea). 토큰 카드 정의는 포팅 레이어가 등록. 테스트: `tests/G3.5-B9.Token` 4/4(단일·다수·tapped·정의누락 unsupported). (2026-06-28)
- [ ] B-10 소재(디지볼브 카드)/링크 카드 trash·복귀 🟡
- [x] B-11 트래시→손/덱 복귀 — **완료(검증)**: 기존 `ReturnToHand`/`ReturnToDeckTop`/`ReturnToDeckBottom` 뮤테이션이 출발존 무관(`MoveCardToSingleZone`)이라 트래시 출발도 그대로 동작하고, `TriggerTimingMap`이 Trash→Hand/Library 시 `OnReturnCardsTo{Hand,Library}FromTrash`를 파생함. 엔진 변경 없이 트래시-출발 동작을 e2e 검증. 테스트: `tests/G3.5-B11.TrashReturn` 3/3(hand·deck top·deck bottom). (2026-06-28)

---

## C. 키워드 (각 = 연속 등록 + 전투/공격 소비 훅 + 테스트)

> ✅ **충실도 마이그레이션 완료 ([f68_deletion_replacement_window_design.md](f68_deletion_replacement_window_design.md), [asis_fidelity_audit.md](asis_fidelity_audit.md))**: 삭제-대체/전환 계열 9종(Evade·Barrier·Decoy·ArmorPurge·Raid·Fragment·Scapegoat·Ascension·Save)이 자동해소(룰 변경)에서 **F-6.8 재진입 윈도우 agent 선택**으로 전환됨 — 효과/전투/post 전 경로. 테스트 `tests/G3.5-F68`(14)·`tests/G3.5-F68B`(4)·`tests/G3.5-C3`(7). 아래 [x]/[~]는 이제 룰-충실. Retaliation 유발 삭제도 상대 윈도우 재오픈(Evade/Barrier 가능). **end-attack optional 트리거**도 `EndAttackTriggerHook` 재분류(`Definition.IsOptional`)+`OptionalPromptQueue` 라우팅으로 agent 선택화(구 LIMITATION #6 해소, 증분 6). **카드별 후보 조건 훅**(`IDeletionReplacementCandidateConditions` + Gate 열거자 optional 술어)도 선행 배선 — AS-IS `permanentCondition` 미러, 기본 리졸버로 거동 동일, 카드 포팅 시 등록만으로 주입(구 LIMITATION #3 해소, 증분 7, 테스트 `tests/G3.5-F68D` 8). (잔여 LIMITATION 경미: Fragment cost>1 단일선택 N회=결과동일; grant 트리거 클래스는 포팅 시; 동시 다중삭제 Decoy.)
> - 아직 자동(룰-변경)인 키워드의 아래 [x]/[~]는 "거동 근사"이지 "룰-충실" 아님.

### 이미 실효 (참고)
- [x] Blocker · [x] Jamming · [x] Reboot · [x] Piercing

### C-그룹1: 기본 전투 🟠
- [x] C-1 Rush — grant=`KeywordBaseBatch2`(GrantRush→`hasRush`, G3G-002) + 소비=`AttackPermanentAction`(소환멀미 우회, 232행). e2e 검증: `tests/G3.5-N1.SummoningSickness`(RushBypassesSickness: 플레이→같은 턴 공격 가능, 비-Rush는 불가). (2026-06-28)
- [x] C-2 Blitz — "상대 메모리 ≥1일 때 공격". 윈도우=`MemoryPass` 페이즈(메모리 음수, 턴 미전환). 소비 신규: `AttackPermanentAction.Validate` 페이즈 게이트(Main 전용 + MemoryPass는 `hasBlitz`만) + `HeadlessLegalActionDispatcher` MemoryPass 분기에 공격 액션 노출(비-Blitz는 게이트가 걸러 EndTurn만 유지). grant=`KeywordBaseBatch2`(RequestBlitzAttack→`hasBlitz`). 테스트: `tests/G3.5-C2.BlitzMemoryPassAttack` 6/6 (MemoryPass 공격가능/비-Blitz 불가/EndTurn 공존/Main 회귀×2/Process 수락). (2026-06-28)
- [~] C-3 Raid — **정정**: 원본은 "시큐리티 직접공격"이 아니라 **"공격 시, 공격 대상을 상대의 최고 DP(언서스펜드, 현 방어자 제외) 디지몬으로 전환(SwitchDefender)"**(RaidProcess). D-3와 무관. **엔진 소비 완료**: `RaidAttackSwitch.TryApply`(공격자 hasRaid → 최고DP 언서스펜드 적 디지몬으로 전환) + `IHeadlessAttackController.SwitchDefender` 신규 + `AttackPipeline.AdvanceBlockTiming` 블록 전 호출. grant `GrantRaid→hasRaid`. **잔여**: grant 트리거 클래스(포팅 시). 테스트: `tests/G3.5-C3.RaidAttackSwitch` 6/6. (2026-06-28)

### C-그룹2: 방어 🟠
> 원본 DCGO 소스(`DCGO/Assets/Scripts/Script/{CardEffectCommons,CardEffectFactory}/KeyWordEffects/`)가 이 머신에 **존재** → 1:1 미러 가능. 실제 의미는 원본 기준(백로그 한 줄 설명과 다소 상이).
- [~] C-4 Decoy — 원본: "다른 아군 디지몬이 **적 효과**로 필드를 떠날 때, 이 디지몬을 대신 삭제(희생)해 막는다". **엔진 소비 완료**: `DeletionReplacementGate.FindDecoyRedirect`(효과-삭제 경로 `ApplyDelete`에서 적-소유 deleter일 때 소유자의 다른 hasDecoy 디지몬을 희생, 원 타깃 생존). 효과-삭제 전용(원본 IsByEffect). grant `GrantDecoy→hasDecoy`. **잔여**: grant 트리거 클래스(포팅 시). 테스트: `tests/G3.5-C46.DecoyFortitude`. (2026-06-28)
- [~] C-5 Barrier — 원본: "**전투로** 삭제될 때, 시큐리티 top 1장 trash해 생존"(조건 시큐리티≥1). **엔진 소비 완료**: `DeletionReplacementGate.TryBarrierAsync`(BattleResolver 삭제 직전 호출, top 시큐리티 trash+생존). grant 플래그 `GrantBarrier→hasBarrier`. **잔여**: 키워드 grant 트리거 클래스(KeywordBaseBatch3, 포팅 시). 테스트: `tests/G3.5-C57.DefenseDeletionReplacement`. (2026-06-28)
- [~] C-6 Fortitude — 원본: "삭제될 때(소재≥1) 트래시에서 코스트 없이 다시 플레이"(=replay, `OnDestroyed`=삭제 **후**). **엔진 소비 완료**: `DeletionReplacementGate.TryFortitudeReplayAsync`(전투+효과 삭제 후 트래시→배틀존 무료 재플레이, 소재/삭제마커 클리어+소환멀미). 소재 카운트=인스턴스 `sourceIds`. grant `GrantFortitude→hasFortitude`. **잔여**: grant 트리거 클래스(포팅 시). 테스트: `tests/G3.5-C46.DecoyFortitude`. (2026-06-28)
- [~] C-7 Evade — 원본: "필드를 떠나려 할 때(전투/효과 무관) 자신을 서스펜드해 생존"(조건 언서스펜드=코스트 지불 가능). **엔진 소비 완료**: `DeletionReplacementGate.TryEvade` — 전투(BattleResolver) + 효과-삭제(`MatchStateMutationSink.ApplyDelete`) **양 경로**. grant 플래그 `GrantEvade→hasEvade`. **잔여**: 키워드 grant 트리거 클래스(포팅 시). 테스트: `tests/G3.5-C57.DefenseDeletionReplacement`. (2026-06-28)
- 신규 엔진 plumbing: `Headless/Runtime/DeletionReplacementGate.cs`(WhenPermanentWouldBeDeleted 리플레이스먼트 + Decoy 리다이렉트 + Fortitude OnDestroyed replay, F-6.8 부분 토대). LIMITATION: optional "you may"·"select 1"을 agent 선택으로 노출하지 않고 affordable 시 자동 적용/첫 후보 선택(기존 optional 트리거 자동해소와 동일 정책).

### C-그룹3: 반격/처형 🟡
- [x] C-8 Retaliation — 원본: "**전투로** 삭제될 때, 전투 상대(승자)도 삭제"(무승부 시 이미 양쪽). grant=`KeywordBaseBatch2`(DeleteRetaliationTarget→`hasRetaliation`) + **소비 신규**: `BattleResolver`(삭제 확정 후, holder 사망·상대 생존 시 상대 삭제; 상대는 Evade/Barrier 기회 보유). 테스트: `tests/G3.5-C821.RetaliationArmorPurge`. (2026-06-28)
- [~] C-9 Execute — **정정**: 원본은 "이 디지몬이 언서스펜드 디지몬도 공격 가능하게 1회 공격 후, 공격 종료 시 자신을 삭제". **엔진 소비 완료**: `canAttackUnsuspendedDigimon`(기존, `AttackPermanentAction`) + **신규** `deleteSelfAtEndOfAttack`(`AttackPipeline.AdvanceEndAttackAsync`에서 공격 종료 시 공격자 trash). grant `GrantAttackUnsuspended→canAttackUnsuspendedDigimon`·`GrantDeleteSelfAtEndOfAttack→deleteSelfAtEndOfAttack`. **잔여**: 효과가 공격을 개시·번들링하는 grant 트리거 클래스(포팅 시; 헤드리스는 공격을 agent legal-action으로 선언). 테스트: `tests/G3.5-C910.ExecuteCollision`. (2026-06-28)
- [x] C-10 Collision — **정정**: 원본은 "공격 시 상대가 **강제로 블록**(아무 디지몬이나 블록 가능, 스킵 불가)". 소비는 `BlockTiming`에 이미 존재(`hasCollision`→아무 블로커 허용+`CanSkipBlock` false)·`tests/G2G-002` 검증됨. **신규**: grant `GrantCollision→hasCollision` + grant→consume e2e 테스트 `tests/G3.5-C910.ExecuteCollision`. (2026-06-28)

### C-그룹4: 자원/조건 🔵
> 삭제-계열 3종(C-11/17/19)은 `DeletionReplacementGate`에 배선 완료. 나머지 7종 선결 서브시스템 분석: **[cgroup4_subsystem_analysis.md](cgroup4_subsystem_analysis.md)**. 재분류 결과 **C-12 Iceclad·C-13 Decode·C-18 Alliance는 서브시스템 없이 구현 가능**(아래 표시), C-14/15/16/20은 서브시스템(스택분할·효과무효·trait·효과구동공격) 선결.
- [~] C-11 Fragment — 원본: "삭제될 때 디지볼루션 소재 N장(`fragmentCost`, 기본 1) trash하고 생존(top 유지)". **엔진 소비 완료**: `DeletionReplacementGate.CanFragment`/`ApplyFragmentAsync`/`TryFragmentAsync`(전투+효과, skip+가장 깊은 N 소재 trash). grant `GrantFragment→hasFragment`. 잔여 grant 트리거 클래스(포팅). 테스트: `tests/G3.5-C4D.ResourceDeletionKeywords`. (2026-06-28)
- [x] C-12 Iceclad — 원본: 전투 시 둘 중 하나라도 Iceclad면 **DP 대신 디지볼루션 소재 수로 승부 비교**(`CardController.CompareStats`, clamp[-1,1]). **엔진 소비 완료**: `BattleResolver.CompareBattleStats`(어느 쪽이든 `hasIceclad`면 `sourceIds` 카운트 비교, 아니면 DP) + grant `GrantIceclad→hasIceclad`(`MatchStateMutationSink.KindToFlag`). 신규 서브시스템 불요. **잔여**: grant 트리거 클래스(포팅 시). 테스트: `tests/G3.5-C12.IcecladBattle` 5/5(소재로 역전·동수 무승부·소재 적으면 패·非Iceclad는 DP). (2026-06-28)
- [x] C-13 Decode — 원본: "이 디지몬이 **전투 외**로 필드를 떠날 때(`WhenRemoveField && !IsByBattle`), 색 조건 맞는 소재 1장을 **무료 플레이**(새 permanent)". **엔진 소비 완료**: F-6.8 **POST 윈도우**에 `DecodeOption` 추가(ArmorPurge/Save 동형 — 소재는 `ChoiceZone.None`에 남아 trashed 카드 `sourceIds`로 참조) + `DeletionReplacementGate.TryDecodePlaySourceAsync`(선택 소재 None→배틀존 무료, 소환멀미·sourceIds 분리·`decoded` 1회 가드) + grant `GrantDecode→hasDecode`. 2단계 agent 선택(발동?→어느 소재). 색 조건=`IDeletionReplacementCandidateConditions`(#3 seam, 기본=아무 Digimon 소재). **스켈레톤 1:1**: `KeywordBaseBatch2`에 `Decode` kind 추가(`CardEffectCommons/KeyWordEffects/Decode.cs` 파티얼 = `CanResolveDecode`, factory wrapper). **잔여**: grant 트리거 클래스(카드 포팅 시 바인딩); DP≤0 sweep/바운스 등 비-effect 이탈은 POST 미발화(ArmorPurge류와 동일 한계). 테스트: `tests/G3.5-C13.Decode` 5/5. (2026-06-28)
- [~] C-14 Partition — **정정**: 원본(PartitionProcess)은 스택 전체 분할이 아니라 **색 그룹별 소재 1장씩 총 2장을 무료 플레이**(payCost:false). 트리거 `WhenRemoveField && !IsByBattle && !IsByOwnerEffect`, 조건 `DigivolutionCards.Count≥2`. **S4 + 엔진 소비 완료**: F-6.8 **POST 윈도우**에 `PartitionOption`(effect-deletion·≥2 소재·`partitioned` 1회 가드) — Decode의 무료-플레이 프리미티브(`PlaySourceForFreeAsync` 공유 코어 추출, `TryPartitionPlaySourceAsync`) + Fragment의 **반복 단일선택**(2회, remaining 카운터 공유) 재사용. grant `GrantPartition→hasPartition`. **스켈레톤 1:1**: `KeywordBaseBatch2`에 `Partition` kind(`CanResolvePartition` 파티얼/factory). **잔여**: grant 트리거(포팅); 색 그룹 2분할 조건은 `IDeletionReplacementCandidateConditions` seam 기본=아무 Digimon 소재(카드 포팅 시 색 주입); 스택 전체 분할형(미완 소재 스택)은 본 키워드 범위 밖. 테스트: `tests/G3.5-C14.Partition` 4/4. (2026-06-28)
- [~] C-15 Progress — 원본: "공격 중(UntilEndAttack) 이 디지몬이 **상대 효과의 영향을 받지 않음**"(ProgressStaticEffect=CanNotAffectedClass, SkillCondition=IsOpponentEffect; **패시브 정적효과**). **S2 + 엔진 소비 완료**: 신규 `ContinuousImmunityGate`(registry+repo만으로 동작 — sink가 EngineContext 미보유; opponent-only immunity를 **출처-상대성**으로 판정: 출처 소유자=대상 소유자면 통과(자기/아군), 상대면 차단) + `MatchStateMutationSink.Apply` 타겟-뮤테이션 직전 게이팅(immunity 시 skip). `ProgressImmunity`가 공격 선언 시(AttackPipeline) opponent-only continuous immunity(UntilEndAttack)를 공격자에 **자동 등록**(선택 없음 — 정적효과). grant `GrantProgress→hasProgress`. **스켈레톤 1:1**: `KeywordBaseBatch2`에 `Progress` kind(非optional, `None` 타이밍, `CanResolveProgress` 파티얼/factory). **현재 immunity 등록 카드 없음→무회귀**. **잔여**: grant 트리거(포팅); AS-IS의 ~20개 효과-적용 지점은 sink 중앙 게이트로 수렴(개별 비-sink 효과 적용 경로는 포팅 시 점검). 테스트: `tests/G3.5-C15.Progress` 5/5. (2026-06-28)
- [~] C-16 Overclock — 원본: "**턴 종료 시**(OnEndTurn), **토큰 또는 해당 trait** 아군(≠자신) 1장 삭제(옵션) → 삭제 시 이 디지몬이 **언탭·플레이어 한정** 공격". **S3 trait + S1 + 엔진 소비 완료**: 신규 `OverclockEffect`(trait-아군 후보 `GetTraitAllyCandidates`[토큰 `IsToken` OR trait 매칭 — S3는 `trait`/`traits`/`cardTraits` 키 직접 읽기] + optional `ChoiceType.OverclockTarget` 선택 → `DeletionReplacementGate.SacrificeAsync`[Decoy/Scapegoat와 동일 직접 희생] → `EffectDrivenAttack`[S1] 언탭·플레이어-한정 공격 오픈) + `MetadataActionProcessor` 라우팅 + grant `GrantOverclock→hasOverclock`. **스켈레톤 1:1**: `KeywordBaseBatch2`에 `Overclock` kind(`OnEndTurn`/`CanResolveOverclock` 파티얼/factory). **잔여**: 카드별 발동 트리거(포팅); 희생 아군의 PRE 삭제-대체(Evade 등) 우회=Decoy/Scapegoat와 동일 한계(삭제 항상 성립→공격 항상 후속). 테스트: `tests/G3.5-C16.Overclock` 5/5. (2026-06-28)
- [~] C-17 Ascension — 원본: "삭제된 **후** 그 카드를 시큐리티에 놓는다(옵션)". **엔진 소비 완료**: `DeletionReplacementGate.TryAscensionAsync`(전투+효과 삭제 후 trash→Security, Fortitude류 훅). grant `GrantAscension→hasAscension`. 잔여 grant 트리거 클래스(포팅). 테스트: `tests/G3.5-C4D.ResourceDeletionKeywords`. (2026-06-28)
- [x] C-18 Alliance — 원본: "공격 시(`OnAllyAttack`), **다른 아군 1장 서스펜드**(코스트) → 이 디지몬 +DP(=그 아군 DP)·+1 SecAtk(UntilEndAttack)". **엔진 소비 완료**: `AllianceAttackBoost`(RaidAttackSwitch 동형 — `AttackPipeline.AdvanceBlockTiming`에서 Raid 다음, 블록/전투 전에 optional 선택 오픈 → 선택 아군 서스펜드 + 공격자에 `dpDelta`/`sAttackDelta` continuous 모디파이어 바인딩 `UntilEndAttack` 등록; `EffectDurationExpiry`가 공격 종료 시 제거) + `ChoiceType.AllianceTarget` + `MetadataActionProcessor` 라우팅 + grant `GrantAlliance→hasAlliance`. **SA 배선 보강**: `SecurityResolver.ReadStrike`/`AttackPipeline.ReadStrike`가 `ContinuousModifierGate.ResolveSecurityAttack`를 fold(이전엔 SA 모디파이어가 시큐리티 체크 수에 미반영 — Alliance +1 SA가 실제 1장 더 체크). **스켈레톤 1:1**: `KeywordBaseBatch2`에 `Alliance` kind(`OnAllyAttack`/Continuous/`CanResolveAlliance` 파티얼/factory). 단일 optional select(=AS-IS canNoSelect). **잔여**: grant 트리거 클래스(카드 포팅 시 바인딩). 테스트: `tests/G3.5-C18.Alliance` 7/7(오퍼·無후보·디클라인·+DP/+SA 버프·만료·전투역전·파이프라인). (2026-06-28)
- [~] C-19 Scapegoat — 원본: "삭제될 때 **다른 아군** 디지몬을 대신 삭제하고 자신은 생존"(Decoy 역방향). **엔진 소비 완료**: `DeletionReplacementGate.FindScapegoatSacrifice` + `SacrificeAsync`(전투+효과, 아군 희생+holder skip). grant `GrantScapegoat→hasScapegoat`. 잔여 grant 트리거 클래스(포팅). 테스트: `tests/G3.5-C4D.ResourceDeletionKeywords`. (2026-06-28)
- [~] C-20 Vortex — 원본: "이 디지몬이 **효과로 추가 공격**(디지몬+플레이어, 언서스펜드 대상 가능; `isVortex`)". **S1 서브시스템 + 엔진 소비 완료**: 신규 `EffectDrivenAttack`(S1 허브 — `GetTargets`/`Initiate`/`RequestChoice`/`ResolveChoice`; `DeclareAttack`만 호출하면 기존 `AttackPipeline`이 구동, 새 상태기계 불요). 대상은 **agent 선택**(`ChoiceType.EffectAttack`, 룰-충실; 설계의 자동해소 대신) + `MetadataActionProcessor` 라우팅. 옵션 `EffectAttackOptions`(WithoutTap/AllowPlayer/AllowDigimon/TargetUnsuspended). grant `GrantVortex→hasVortex`. **스켈레톤 1:1**: `KeywordBaseBatch2`에 `Vortex` kind(`CanResolveVortex` 파티얼/factory; 라이브=EffectDrivenAttack). **잔여**: 카드별 발동 트리거 바인딩(포팅 시 EffectDrivenAttack 호출). 테스트: `tests/G3.5-S1.EffectDrivenAttack` 8/8. (2026-06-28)

### C-그룹5: 기타 🔵
> 3종 공통 토대 = AS-IS `Permanent.AddDigivolutionCardsBottom` → 신규 `Headless/Runtime/DigivolutionStackHelpers`(소재 스택 맨 아래 추가/이동/덱→스택).
- [~] C-22 Save — 원본: "삭제된 **후** 그 카드를 다른 (타머) permanent의 디지볼루션 스택 맨 아래에 부착". **엔진 소비 완료**: `DeletionReplacementGate.TrySaveAsync`(전투+효과 삭제 후, 소유자의 다른 battle-area permanent에 부착). grant `GrantSave→hasSave`. 잔여 grant 트리거 클래스(포팅). 테스트: `tests/G3.5-C5.SaveMaterialTraining`. (2026-06-28)
- [~] C-23 Material Save — 원본: "이 디지몬의 소재 N장을 다른 permanent 스택 맨 아래로 이동"(활성 효과). **엔진 프리미티브 제공**: `DigivolutionStackHelpers.MoveSourcesBottom`(소재 재배치). 패시브 훅 없음(활성 효과) → 카드-facing 발동은 포팅 시. 테스트: `tests/G3.5-C5.SaveMaterialTraining`. (2026-06-28)
- [~] C-24 Training — 원본: "자신 서스펜드(코스트) 후 덱 top을 자기 스택 맨 아래에 추가(face down)"(활성 효과). **엔진 프리미티브 제공**: `DigivolutionStackHelpers.TrainAsync`(서스펜드+library top→자기 스택). 패시브 훅 없음 → 발동은 포팅 시. 테스트: `tests/G3.5-C5.SaveMaterialTraining`. (2026-06-28)
- [x] C-21 ArmorPurge — 원본: "삭제될 때(소재≥1) top 카드를 trash하고 직속 소재가 새 top이 되어 생존(하위 형태)". grant=`KeywordBaseBatch2`(ApplyArmorPurge→`hasArmorPurge`) + **소비 신규**: `DeletionReplacementGate.TryArmorPurgeAsync`(삭제 후 top→trash 확정 + `sourceIds[0]`을 None→BattleArea 승격, 잔여 소재/탭상태 이관, Fortitude와 동일 훅으로 전투+효과 양 경로). 테스트: `tests/G3.5-C821.RetaliationArmorPurge`. (2026-06-28) · [ ] C-22 Save · [ ] C-23 Material Save · [ ] C-24 Training

---

## D. 대형 서브시스템 (각각 독립 구축)

- [ ] D-1 **Link** — 링크 카드 부착/해제 + 링크코스트·max 관리 + WhenLinked/OnLinkCardDiscarded(F-6.9)
- [ ] D-2 **Appfuse(앱퓨전)** — 이름/조건 기반 다카드 융합
- [x] D-3 ~~Raid~~ — **정정/해소**: Raid는 시큐리티 직접공격 서브시스템이 아니라 공격 대상 전환 키워드였음(C-3에서 완료). 별도 D-3 서브시스템 불필요.
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
