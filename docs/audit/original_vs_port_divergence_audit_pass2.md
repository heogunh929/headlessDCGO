# 원본 DCGO ↔ 헤드리스 포팅 2차 대조 감사 (체크리스트)

- 작성일: 2026-06-27
- 요청: 1차 감사(D-1~D-6 수정 완료) 이후 **다시 한번 원본과 대조, 소스 수정 없이 체크리스트만**.
- 방법: 1차에서 덜 다룬 엔진 서브시스템(게임 셋업, 존 이동 메커니즘, 서스펜드/리부트, 공격/블록 엣지, 지속/대체/제한 효과 배선)을 5개 병렬 에이전트로 대조 → **작성자가 상위 항목 직접 스폿체크**.
- 분류: 🔴HIGH / 🟠MED / 🟡LOW / ⚪경계(out-of-scope 인접)
- **소스 수정 없음.** 본 문서는 발견 목록일 뿐.
- 1차 문서: [original_vs_port_divergence_audit.md](original_vs_port_divergence_audit.md) / 타이밍: [timing_emission_gaps.md](timing_emission_gaps.md) / GPT: [gpt_review_followups.md](gpt_review_followups.md)

---

## 🔴 신규 — HIGH (확정·작성자 검증)

### N-1. ~~소환 멀미(summoning sickness) 규칙이 dead code~~ → ✅ **수정(2026-06-27)**
- **원본**: `Permanent.CanAttackTargetDigimon`(`Permanent.cs:2244-2250`) — `EnterFieldTurnCount == TurnCount`면 Rush 없는 한 공격 불가. 신규 플레이 permanent만 `EnterFieldTurnCount = TurnCount`(`CardController.cs:1386`); 진화는 같은 permanent 유지(계승); Jogress/hatch는 `-1`(면제, 기본값도 `-1`).
- **(과거)포팅 갭**: `AttackPermanentAction.cs:228`이 `enteredThisTurn`를 **읽지만 세팅하는 엔진 코드가 0건**이라 갓 낸 디지몬이 즉시 공격 가능했음.
- **수정**: ① `PlayCardAction`이 필드 진입 시 `enteredThisTurn=true` 세팅, ② `DigivolveAction`이 밑 디지몬의 `enteredThisTurn`를 **계승**(원본의 같은-permanent 의미), ③ `HeadlessEarlyPhaseFlow` Unsuspend 스텝이 턴플레이어 permanent의 플래그를 **클리어**. Rush는 기존 소비측에서 우회. 브리딩 이동(Hatch/MoveBreedingToBattle)은 플래그 미세팅 → 면제(원본 `-1`과 일치).
- **테스트**: `tests/G3.5-N1.SummoningSickness` 5/5 (플레이=멀미, Rush 우회, 다음턴 클리어, 진화 계승 not-sick/sick).

### N-2. 지속/대체 효과 서브시스템 배선 — ◑ **소비측 대부분 완료(2026-06-27)** (DP 재계산·DP면역·삭제방지·제한 슬라이스 연결; D-A5/A6·생산측은 잔여)
- **원본**: `Permanent.DP/BaseDP/GetDP`(`Permanent.cs:193-668`)가 **접근할 때마다** 전 필드+공개 시큐리티+플레이어 효과를 스캔해 DP 재계산. `CanBeDestroyed()`/`CanBeDestroyedByBattle()`(`:3186-3305`)도 타 카드의 `CanNotBeDestroyed(ByBattle)` 지속효과 스캔. `ImmuneFromDPMinus`도 DP 누적에서 적용.
- **포팅**: `ContinuousEffectEvaluator`/`ReplacementHelpers`/`ModifierHelpers`는 존재하나 **`ContinuousRestrictionGate`만 호출**하고 그것도 **`.Restrictions`만** 사용(`.Modifiers`/`.Replacements` 폐기). 라이브 소비처는 `ContinuousRestrictionGate`/`AttackPermanentAction`/`BlockTiming`뿐.
  - **D-A1** ~~배틀 DP가 타 카드 지속 DP효과 무시~~ → ✅ **수정(2026-06-27)**: 신규 `ContinuousDpGate`가 `BattleResolver`(필드 양측)·`GameFlowProcessor.HasLethalDp`(DP≤0 삭제)의 DP 계산에서 연속 DP modifier를 static DP 위에 적용.
  - **D-A2** ~~시큐리티 디지몬 배틀 DP 동일~~ → ✅ **수정(2026-06-27)**: `SecurityResolver`의 공격자·시큐리티 디지몬 DP도 `ContinuousDpGate` 경유.
  - **D-A3** ~~`ImmuneFromDpReduction`(DP-마이너스 면역) DP 경로 미적용~~ → ✅ **수정(2026-06-27)**: DP-마이너스 면역은 `InvertDelta` modifier가 아니라 **DpReduction/Immune REPLACEMENT**(`ReplacementHelpers.ImmuneFromDpReduction`, key `immuneFromDpMinus`)로 모델링됨(이전 주석이 InvertDelta로 오기재 — 수정). `ContinuousDpGate`가 카드에 해당 면역 replacement가 있으면 **음수 `Dp` Add modifier를 제거**한 뒤 resolve → 감소는 차단되고 양수 buff는 유지. (참고: `ModifierHelpers`의 `InvertDelta`는 SecurityAttack 부호 반전 전용이며 `FinalValue` 미반영은 의도된 동작 — DP 면역과 무관.) (`tests/G3.5-N2` 7/7)
  - **D-A4** ~~배틀/효과 삭제가 타 카드의 `CanNotBeDestroyed(ByBattle)` 지속효과 무시(자기 플래그만)~~ → ✅ **소비측 배선(2026-06-27)**: `BattleDeletionGate`가 `BattleResolver`·`SecurityResolver`의 삭제 결정에서 연속 `Delete/Prevent` replacement 조회. (`tests/G3.5-R2-1` 3/3) *남음: 생산측(키워드→연속 replacement 등록) Phase 4.*
  - **D-A5** 디지볼브 합법성이 "cannot digivolve" 지속 제한 미확인(`CannotRestrictionKind`에 Digivolve 멤버 자체 없음).
  - **D-A6** 공격 타깃 제한(`CanNotAttackTargetDefendingPermanent`)을 타깃 열거 시 미확인(`EvaluateAttack` 호출에 defenderId 누락).
- **소비측 현황(2026-06-27)**: "다른 디지몬에 +/−DP"(D-A1/A2, `ContinuousDpGate`)·"DP-마이너스 면역"(D-A3, `ContinuousDpGate`+`ImmuneFromDpReduction`)·"내 디지몬 파괴 불가"(D-A4, `BattleDeletionGate`)·"공격/블록 불가"(X-04, `ContinuousRestrictionGate`)가 라이브 전투/삭제/합법성 경로에서 연속 레지스트리를 조회. **잔여**: D-A5 디지볼브 제한(`CannotRestrictionKind`에 Digivolve 부재), D-A6 공격-타깃 제한(`EvaluateAttack` defenderId 전달), 그리고 **생산측**(카드 키워드→연속 effect 등록)은 Phase 4.
- **테스트**: `tests/G3.5-N2.ContinuousBattleDp` 7/7(필드 buff/debuff·DP≤0 경로·시큐리티 공격자 buff·시큐리티 디지몬 debuff·DP면역 감소차단·면역하 buff유지). 게이트는 연속 effect 미등록 시 no-op(base DP 반환).
- **심각도 HIGH→소비측 해소 / 확신 HIGH** · **성격: Phase-4 결합** — 생산측(효과 본문이 연속 effect emit/등록)이 오면 즉시 실효.

---

## 🟠 신규 — MED

### N-3. ~~시큐리티 삽입이 top이 아니라 bottom~~ → ✅ **수정(2026-06-27)**
- **원본**: `AddSecurityCard(toTop:true)` 기본 → `SecurityCards.Insert(0,…)`(`CardObjectController.cs:993`). 회수/딜 카드가 **맨 위(index 0)**. 셋업 딜(`AddSecurity`, `CardController.cs:2052-2066`)도 덱-top을 매번 `Insert(0)` → 마지막 딜 카드가 top.
- **(과거)포팅 갭**: `AddToSecurityAsync`/`AddSecurityFromLibraryAsync`가 **bottom append** → index 0부터 소비되는 시큐리티에서 회수 카드가 맨 나중 체크 + 셋업 스택이 원본과 역순.
- **수정**: ① `IZoneMover.AddToSecurityAsync`에 **`bool toTop = true`** 추가(원본 기본), 구현은 top 삽입; `MatchStateMutationSink`는 `toBottom` 플래그로 하단 지정 가능. ② `AddSecurityFromLibraryAsync`가 각 덱-top 카드를 **시큐리티 top에 Insert**(`MoveFromZoneTop`에 `insertTop` 추가) → 마지막 딜=top(원본 스택 순서 일치). 호출부(`MetadataActionProcessor`·`MatchStateMutationSink`)는 named ct로 갱신.
- **테스트**: `tests/G3.5-N3.SecurityInsertTop` 3/3(기본 top·toTop:false 하단·라이브러리 딜 스택).

### N-4. ~~덱 셔플이 셋업 기본값 OFF~~ → ✅ **수정(2026-06-27, 충실한 플립)**
- **원본**: 게임 시작 시 메인/디지타마 덱 **무조건 셔플**(`CardObjectController.CreatePlayerDecks` → `ShuffledDeckCards`).
- **(과거)포팅 갭**: `MatchSetupConfig.ShuffleDecks`/`ShuffleDigitamaDecks` 기본 **false** → 기본 게임이 덱리스트 순서로 결정적(RL 학습 다양성 위험).
- **수정**: `MatchSetupConfig`의 두 속성·`Create` 팩토리 기본값을 **true**로 플립(원본 always-shuffle 일치). 셔플은 시드 기반이라 동일 시드는 완전 재현 가능. **결정적 시나리오/단위 테스트는 `shuffleDecks:false, shuffleDigitamaDecks:false`로 opt-out**(23개 테스트 프로젝트의 `MatchSetupConfig.Create` 호출에 일괄 추가; 이들은 특정 카드 위치를 가정하므로 정당한 opt-out).
- **테스트**: 신규 `tests/G3.5-N4.DeckShuffleDefault` 4/4(기본 셔플·시드 결정성·다른 시드 분기·opt-out 순서 보존).

### N-5. ~~게임 시작 멀리건/리드로우 부재~~ → ✅ **수정(2026-06-27, 인터랙티브)**
- **원본**: 초기 5장 후 first player부터 멀리건(핸드 덱 하단 반환 → 셔플 → 5장 재드로우)을 **시큐리티 셋업 전**에 수행(`TurnStateMachine.cs:374-494`).
- **(과거)포팅 갭**: `MatchSetupFlow`에 멀리건 경로 0건.
- **수정**: ① `ChoiceType.Mulligan` + **`MulliganCoordinator`**(EngineContext 보유) — 플레이어별 멀리건을 `ChoiceController` 결정으로 노출(redraw 후보 선택=멀리건, skip=keep). pending choice는 turn-player 무시하고 소유자에게 디스패치되므로 first→second 순서가 자연스럽게 처리됨. ② `MatchSetupFlow`는 `EnableMulligan` 시 **시큐리티 딜을 멀리건 이후로 연기**(post-shuffle 덱에서 딜) + 첫 플레이어 결정 오픈. ③ `MetadataActionProcessor`가 `ChoiceType.Mulligan` 해소를 코디네이터로 라우팅(redraw=핸드→덱하단→셔플→재드로우, 마지막 결정 시 시큐리티 딜). 
- **게이트**: `MatchSetupConfig.EnableMulligan` **기본 false** — 기존 결정적 셋업(Setup→Main 직진하는 ~40 테스트)은 무영향, RL/충실 게임 경로가 opt-in(strict 프로파일과 동일 패턴).
- **테스트**: 신규 `tests/G3.5-N5.OpeningMulligan` 5/5(기본 무멀리건·순서 결정·합법액션 keep/redraw·redraw 핸드 변경·keep 불변).

---

## 🟡 신규 — LOW / ⚪ 경계

- **N-6 🟡 trash 삽입 순서 역순**: 원본 `TrashCards.Insert(0)`(최근=top) vs 포팅 append. 원본도 trash는 대부분 술어 선택이라 영향 낮음(파일 top 스프라이트=표시용). "trash 맨 위" 효과 포팅 시만 문제.
- **N-7 ⚪ 셔플 RNG 1스텝 차이**: 원본 Fisher-Yates가 n=0에서 no-op draw 1회 추가, 포팅은 i>0에서 정지. **순열은 동일**하나 RNG 스트림이 셔플 후 1스텝 어긋남 → 원본과의 cross-engine 결정성만 영향(포팅 자체 결정성은 유지). GPT "randomSeed" 인접.
- **N-8 ⚪ 필드 이탈 시 인스턴스 상태 미정리**: `CardIdentityAdapter.MoveCard`가 face state만 변경, suspended/SourceIds/modifiers 미클리어. 원본은 필드 이탈 시 `RemoveCardSource`+`Init()`로 해체. permanent-teardown 영역(OnDeletion-scope 인접). 효과 포팅 시 점검.
- **N-9 ⚪ 브리딩 unsuspend가 canUnsuspend 게이트 적용**: 원본 브리딩 루프는 `CanUnsuspend` 무시(무조건 unsuspend), 포팅은 `TryUnsuspend`가 게이트. 단 `CanNotUnsuspend`는 필드만 대상이라 디지타마엔 사실상 적용 안 됨 — 이론적. 

---

## 검증된 MATCH (이번 패스 — 오탐 방지용)
- 시작 핸드 5장·시큐리티 5장·첫 턴 드로우 스킵·first-player 결정/셋업 순서·시큐리티 face-down·디지타마 별도 라이브러리 배치.
- 덱 top 드로우·draw-from-empty 부분드로우·덱 top/bottom 단일삽입·AddToHand append.
- 턴 시작 unsuspend(필드/브리딩/Reboot)·공격 시 attacker 서스펜드·공격 자격(턴플레이어·디지몬·미서스펜드·can-suspend·battle area)·suspended-타깃 규칙·단일 블로커(maxCount=1)·collision 강제 블록·빈 보드 처리·블록 합법성 지속 제한 게이트(X-04)·attacker-global "cannot attack" 제한.

---

## 우선순위 권고 (수정 재개 시)
1. 🔴 **N-1 소환 멀미** — 작고 명확, 게임플레이 정확성 직결. 단독 수정 가능(엔진).
2. 🔴 **N-2 지속/대체 배선** — 가장 큰 구조적 갭. **Phase 4와 동반**(효과 본문이 지속효과를 emit/등록하면 DP/삭제 평가가 레지스트리 경유). DP≤0 삭제(D-2)·배틀 DP도 이때 함께 지속화.
3. 🟠 **N-3 시큐리티 top 삽입** — 작음, 규칙 정확성(회수·셋업 순서).
4. 🟠 **N-4 셔플 기본 / N-5 멀리건** — 셋업 완성 시(RL 학습 다양성·시작 결정).
5. 🟡/⚪ N-6~N-9 — 해당 효과군 포팅 시 점검.
