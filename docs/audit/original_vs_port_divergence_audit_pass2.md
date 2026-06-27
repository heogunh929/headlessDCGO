# 원본 DCGO ↔ 헤드리스 포팅 2차 대조 감사 (체크리스트)

- 작성일: 2026-06-27
- 요청: 1차 감사(D-1~D-6 수정 완료) 이후 **다시 한번 원본과 대조, 소스 수정 없이 체크리스트만**.
- 방법: 1차에서 덜 다룬 엔진 서브시스템(게임 셋업, 존 이동 메커니즘, 서스펜드/리부트, 공격/블록 엣지, 지속/대체/제한 효과 배선)을 5개 병렬 에이전트로 대조 → **작성자가 상위 항목 직접 스폿체크**.
- 분류: 🔴HIGH / 🟠MED / 🟡LOW / ⚪경계(out-of-scope 인접)
- **소스 수정 없음.** 본 문서는 발견 목록일 뿐.
- 1차 문서: [original_vs_port_divergence_audit.md](original_vs_port_divergence_audit.md) / 타이밍: [timing_emission_gaps.md](timing_emission_gaps.md) / GPT: [gpt_review_followups.md](gpt_review_followups.md)

---

## 🔴 신규 — HIGH (확정·작성자 검증)

### N-1. 소환 멀미(summoning sickness) 규칙이 dead code — 갓 낸 디지몬이 같은 턴 공격 가능
- **원본**: `Permanent.CanAttackTargetDigimon`(`Permanent.cs:2244-2250`) — `EnterFieldTurnCount == TurnCount`면 Rush 없는 한 공격 불가.
- **포팅**: `AttackPermanentAction.cs:228`이 `enteredThisTurn` 메타를 **읽지만**, 이 키를 **세팅하는 엔진 코드가 0건**(PlayCard/Digivolve/셋업/뮤테이션 어디서도 안 씀, 턴 시작 클리어도 없음). 세팅은 **테스트 2곳에서만**(검증됨).
- **영향**: 실전에서 갓 플레이/진화한 디지몬이 **즉시 공격 가능** — 핵심 규칙 누락. (게다가 만약 세팅되면 턴 시작 리셋이 없어 영구히 "멀미" 상태로 남는 2차 버그.)
- **심각도 HIGH · 확신 HIGH** · 수정방향: PlayCard/Digivolve가 `enteredThisTurn` 세팅 + 턴 시작(또는 진화 계승 규칙) 클리어. 단 Rush/진화-계승 규칙 정확히.

### N-2. 지속/대체 효과 서브시스템이 라이브 엔진에 거의 미배선 (제한-슬라이스만 연결)
- **원본**: `Permanent.DP/BaseDP/GetDP`(`Permanent.cs:193-668`)가 **접근할 때마다** 전 필드+공개 시큐리티+플레이어 효과를 스캔해 DP 재계산. `CanBeDestroyed()`/`CanBeDestroyedByBattle()`(`:3186-3305`)도 타 카드의 `CanNotBeDestroyed(ByBattle)` 지속효과 스캔. `ImmuneFromDPMinus`도 DP 누적에서 적용.
- **포팅**: `ContinuousEffectEvaluator`/`ReplacementHelpers`/`ModifierHelpers`는 존재하나 **`ContinuousRestrictionGate`만 호출**하고 그것도 **`.Restrictions`만** 사용(`.Modifiers`/`.Replacements` 폐기). 라이브 소비처는 `ContinuousRestrictionGate`/`AttackPermanentAction`/`BlockTiming`뿐.
  - **D-A1** 배틀 DP가 타 카드 지속 DP효과 무시 (`BattleResolver`는 static `dp`+per-instance `dpModifiers`만).
  - **D-A2** 시큐리티 디지몬 배틀 DP 동일.
  - **D-A3** `ImmuneFromDpReduction`(헬퍼 존재) DP 경로에서 미적용.
  - **D-A4** ~~배틀/효과 삭제가 타 카드의 `CanNotBeDestroyed(ByBattle)` 지속효과 무시(자기 플래그만)~~ → ✅ **소비측 배선(2026-06-27)**: `BattleDeletionGate`가 `BattleResolver`·`SecurityResolver`의 삭제 결정에서 연속 `Delete/Prevent` replacement 조회. (`tests/G3.5-R2-1` 3/3) *남음: 생산측(키워드→연속 replacement 등록) Phase 4.*
  - **D-A5** 디지볼브 합법성이 "cannot digivolve" 지속 제한 미확인(`CannotRestrictionKind`에 Digivolve 멤버 자체 없음).
  - **D-A6** 공격 타깃 제한(`CanNotAttackTargetDefendingPermanent`)을 타깃 열거 시 미확인(`EvaluateAttack` 호출에 defenderId 누락).
- **영향**: "다른 디지몬에 +/−DP", "내 디지몬 파괴 불가", "DP-마이너스 면역", "진화/특정 타깃 공격 불가" 류의 **지속/대체 효과가 게임에 반영 안 됨**. DP 재계산·삭제 방지가 static 메타에만 의존.
- **심각도 HIGH · 확신 HIGH** · **성격: Phase-4 결합** — 지속효과는 카드 본문과 함께 오므로, **DP/삭제 평가를 매번 지속 레지스트리 경유하도록 재배선**하는 것이 Phase 4 효과 작업의 핵심 전제. (1차 D-2의 DP≤0 삭제도 같은 한계 — static dpModifiers만 봄.)

---

## 🟠 신규 — MED

### N-3. 시큐리티 삽입이 top이 아니라 bottom에 들어감 (회수·셋업 순서 영향)
- **원본**: `AddSecurityCard(toTop:true)` 기본 → `SecurityCards.Insert(0,…)`(`CardObjectController.cs:993`). 덱→시큐리티 회수 카드가 **맨 위(index 0, 다음에 깨질 자리)**.
- **포팅**: `AddToSecurityAsync`/`AddSecurityFromLibraryAsync` → `MoveCardToSingleZone(... Security)` 기본 `insertTop:false` = **bottom append**(`InMemoryZoneMover.cs:77-83,117-131,230-`). 시큐리티는 index 0부터 소비(`SecurityResolver`)되므로 회수 카드가 **맨 나중에** 체크됨.
- **추가**: 셋업 시 `AddSecurityFromLibrary` 5장도 같은 이유로 **스택 순서가 원본과 역순**(멀티셋은 동일, 공개 순서만 다름).
- **영향**: 시큐리티 회수 효과 + 게임 시작 시큐리티 공개 순서가 원본과 다름. top 지정 옵션 자체가 인터페이스에 없음.
- **심각도 MED · 확신 HIGH** · 수정방향: 시큐리티 삽입 기본을 top으로, 또는 `IZoneMover.AddToSecurity`에 위치 인자 추가.

### N-4. 덱 셔플이 셋업 기본값 OFF
- **원본**: 게임 시작 시 메인/디지타마 덱 **무조건 셔플**(`CardObjectController.CreatePlayerDecks` → `ShuffledDeckCards`).
- **포팅**: `MatchSetupConfig.ShuffleDecks`/`ShuffleDigitamaDecks` 기본 **false**(`MatchSetupFlow.cs:202-223`). 호출자가 명시 안 하면 덱리스트 순서대로 시드(셔플 없음). `HeadlessScenarioSetup` 경로는 아예 셔플 없음.
- **영향**: 기본 호출 시 시작 핸드/시큐리티가 덱리스트 순서로 결정적 — RL 학습에서 매 게임 동일 배치 위험. (시드 기반 결정성과 별개로, 원본의 always-shuffle 규칙과 불일치.)
- **심각도 MED · 확신 MED**(프로덕션 호출자가 전부 `true`를 넘기면 무해 — 미확인) · 수정방향: 기본 true 또는 셋업 경로에서 시드 셔플 강제.

### N-5. 게임 시작 멀리건/리드로우 부재
- **원본**: 초기 5장 후 first player부터 멀리건(핸드 덱 하단 반환 → 셔플 → 5장 재드로우)(`TurnStateMachine.cs:374-494`).
- **포팅**: `MatchSetupFlow.ApplyAsync`에 멀리건 결정/재드로우 경로 없음. 엔진 전체 `mulligan` 심볼 0건.
- **영향**: 필수 시작 결정 누락 — RL에서 합법 결정 1개 상실, 오프닝 상태공간 축소.
- **심각도 MED · 확신 HIGH** · 성격: 미포팅(의도 가능, `HeadlessScenarioSetup` TODO). 수정방향: 셋업에 멀리건 결정 단계 추가(에이전트 선택).

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
