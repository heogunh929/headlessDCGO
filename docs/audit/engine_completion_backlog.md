# 기본 엔진 완성 백로그 (A~D 전체 작업 리스트업)

- 작성일: 2026-06-27
- 정의: **A + B + C + D 전부 완료 = "기본 엔진 완성"**. 아래는 각 항목을 구체적 구현 단위로 쪼갠 체크리스트(백로그). ID로 참조/체크오프.
- 상위 문서: [engine_completion_checklist.md](engine_completion_checklist.md) (갭 근거·숫자), [timing_emission_gaps.md](timing_emission_gaps.md)
- 표기: `[ ]` 미착수 · `[~]` 부분 · `[x]` 완료. 각 항목은 (구현 + 단위테스트) 1세트.

---

## A. 기반 프레임워크

### F-1. EffectDuration 시스템 🔴
- [ ] F-1.1 `EffectDuration` enum 8종: UntilEachTurnEnd, UntilOwnerTurnEnd, UntilOpponentTurnEnd, UntilEndAttack, UntilEndBattle, UntilOwnerActivePhase, UntilNextUntap, UntilCalculateFixedCost
- [ ] F-1.2 modifier/플래그에 duration 태깅(메타 + 연속 effect 등록 시 duration 보존)
- [ ] F-1.3 만료 정리 훅: 턴종료(each/owner/opp) — 기존 `HeadlessEndTurnCleanupFlow` 확장
- [ ] F-1.4 만료 훅: 전투 끝(UntilEndBattle) — BattleResolver/SecurityResolver 종료점
- [ ] F-1.5 만료 훅: 공격 끝(UntilEndAttack) — AttackPipeline end-attack
- [ ] F-1.6 만료 훅: 다음 언탭(UntilNextUntap)·오너 액티브페이즈(UntilOwnerActivePhase)
- [ ] F-1.7 만료 훅: UntilCalculateFixedCost (코스트 계산 시점)

### F-2. 선택→연산 프레임워크 🔴
- [ ] F-2.1 "조건 매칭 대상 N개 선택" 공통 빌더(permanent/card/hand) — minCount/maxCount/canSkip/canEndNotMax + target predicate
- [ ] F-2.2 Root 존 추상화: Hand/Library/Trash/Security/Clock/Execution/DigivolutionCards/LinkedCards
- [ ] F-2.3 SelectPermanent 모드→뮤테이션 매핑: Tap/UnTap/Destroy/Bounce/PutLibrary(top·bottom)/PutSecurity(top·bottom)/Degenerate/Custom
- [ ] F-2.4 SelectCard/Hand 모드 매핑: AddHand/Discard/PutLibrary/PutSecurity/PlayForFree/PlayForCost/Custom
- [ ] F-2.5 선택 결과→대상별 연산 콜백(원본 selectXxxCoroutine 대응) + after-select 콜백
- [ ] F-2.6 강제/선택(canNoSelect)·부분선택 종료(canEndNotMax) 규칙

### F-3. 뮤테이션 vocabulary 확장 🔴 (B와 결합)
- [ ] F-3.1 **Delete**(effect 삭제) 뮤테이션 kind + 게이트 존중(BattleDeletionGate/연속 prevent)
- [ ] F-3.2 Bounce(→hand) / DeckReturn(top·bottom) effect 뮤테이션
- [ ] F-3.3 Suspend/Unsuspend effect 뮤테이션(이미 kind 있음 → 선택 결합)
- [ ] F-3.4 Discard(hand→trash) / DeckTrash(덱→trash)
- [ ] F-3.5 TrashSecurity / Recovery(덱→시큐리티) 뮤테이션
- [ ] F-3.6 Reveal(덱 top N) + 결과 노출
- [ ] F-3.7 effect-Play(free/cost) 뮤테이션
- [ ] F-3.8 Token 생성 / 소재·링크 trash / 트래시→hand·deck

### F-4. once-per-turn 자동 게이팅 🟠
- [ ] F-4.1 effect 정의에 maxCountPerTurn(once-per-turn) 필드 + use-count 추적
- [ ] F-4.2 트리거/활성 판정에 자동 반영(원본 `isOverMaxCountPerTurn`)
- [ ] F-4.3 턴 시작 시 use-count 리셋(원본 `InitUseCountThisTurn`)

### F-5. 플레이어 스코프 연속효과 🟠
- [ ] F-5.1 "플레이어의 조건매칭 전 permanent에 적용" 연속 스코프(±DP/sAttack/cannot-*/keyword grant)
- [ ] F-5.2 게이트(DpGate/RestrictionGate)가 player-scope effect도 조회하도록 확장
- [ ] F-5.3 IgnoreDigivolutionRequirement player effect

### F-6. 타이밍 emit 중앙화 + 누락분 🟠
- [ ] F-6.1 존-이동/뮤테이션 공통 레이어에서 자동 emit(카드별 배선 최소화) 설계
- [ ] F-6.2 페이즈계: OnStartMainPhase, OnEndMainPhase, OnEndAttackPhase
- [ ] F-6.3 전투계: OnStartBattle/OnEndBattle(순서주의: DP비교 전 해결), OnEndAttack, OnAttackTargetChanged, OnEndBlockDesignation, OnDetermineDoSecurityCheck, OnGetDamage, OnKnockOut
- [ ] F-6.4 상태계: OnTappedAnyone/OnUnTappedAnyone, OnMove, OnAddDigivolutionCards, OnFaceUpSecurityIncreased
- [ ] F-6.5 카드이동계: OnDiscardHand, OnDiscardSecurity, OnDiscardLibrary, OnReturnCardsToHand/LibraryFromTrash, OnPermamemtReturnedToHand, WhenTopCardTrashed, OnDigivolutionCardDiscarded, OnDigivolutionCardReturnToDeckBottom, OnRemovedField
- [ ] F-6.6 액션계: OnUseOption, OnUseDigiburst, OnAllyAttack, OnDeclaration
- [ ] F-6.7 코스트계: BeforePayCost, AfterPayCost
- [ ] F-6.8 would계(예측 트리거): WhenWouldLink, WhenWouldDigivolutionCardDiscarded, WhenPermanentWouldBeDeleted; AfterEffectsActivate
- [ ] F-6.9 링크계: WhenLinked, OnLinkCardDiscarded (D-1 동반)

### F-7. 계승효과(inherited) 활성 모델 🟡
- [ ] F-7.1 IsInheritedEffect 활성규칙(소재가 top 아닐 때만, flip 시 차단, Digimon 한정)
- [ ] F-7.2 소재 stack의 효과가 top 카드에 부여되는 경로(DigivolutionStackReader 연계)

### F-8. 조건/쿼리 헬퍼 커버리지 🟡
- [ ] F-8.1 Min/Max: DP·Level·Cost·DigivolutionCards (IsMin/MaxXxx)
- [ ] F-8.2 매칭: trait/name/color/level/type 술어
- [ ] F-8.3 존 카운트·존재(hand/trash/security/field/breeding) 술어
- [ ] F-8.4 턴/소유 체크(IsOwnerTurn/IsOpponentTurn/IsOwner/Opponent Effect·Permanent)
- [ ] F-8.5 특수: IsJogress/IsDiXros/IsDPZeroDelete/IsTopCardInTrash 등

---

## B. 공통 게임 연산 (고빈도)

- [ ] B-1 **디지몬 삭제(effect Delete)** — 대상선택 → 삭제 (F-3.1 + F-2) 🔴
- [ ] B-2 **±DP / ±Security Attack / ±cost (지속)** — modifier + duration(F-1) 🔴
- [ ] B-3 바운스(→hand) / 덱(top·bottom) 되돌리기 🟠
- [ ] B-4 Suspend/Unsuspend (effect, 대상선택) 🟠
- [ ] B-5 Draw / discard(손버림) / deck-trash 🟠
- [ ] B-6 시큐리티 trash / Recovery(덱→시큐리티) 🟠
- [ ] B-7 덱 top N 공개 + 선택 처리(reveal & select) 🟠
- [ ] B-8 effect로 카드 플레이(무료/코스트, 손/트래시/시큐/소재) 🟠
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
