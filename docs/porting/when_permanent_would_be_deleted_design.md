# WhenPermanentWouldBeDeleted — 설계 (PRIM-P0-timing 배치 4)

- 작성일: 2026-07-05. 근거: 삭제 파이프라인/대체 인프라 전수 조사.
- 대상: `EffectTiming.WhenPermanentWouldBeDeleted` (원본 카드 206장). ALL_CARD_PRIMITIVE_BACKLOG P0 타이밍
  표의 최다 항목이자 마지막 남은 고빈도 타이밍.
- 성격: **삭제 대체/예방 윈도우** — 사후 트리거가 아니라 삭제되기 직전 동기 개입점. 효과가 삭제를 막거나
  다른 것으로 대체할 수 있음(손으로 되돌림, 소스 트래시로 예방, 아군 희생 등).

## 0. 결론 (요약)

**동기 "삭제되려는" 윈도우는 이미 완성돼 있음.** 신규 파이프라인 개입점 불필요. 진짜 갭은 이 윈도우가
**고정 키워드 allow-list**로만 구동되고 카드용 `WhenPermanentWouldBeDeleted` 타이밍으로 안 열린다는 것.
채울 것 = (1) 타이밍 이름, (2) `HasPreOption`이 카드 등록 효과도 PRE 옵션으로 인식, (3) 카드 효과 →
기존 PRE 옵션 브릿지.

## 1. AS-IS 메커니즘 (충실도 앵커)

원본 `CardController.Destroy()` (`DCGO/Assets/Scripts/Script/CardController.cs:3667`):

1. `:3684` 삭제 대상마다 `permanent.willBeRemoveField = true` — **이 불리언 하나가 전체 예방/대체 게이트.**
2. `:3690` 컷인 스택 `StackSkillInfos(..., WhenPermanentWouldBeDeleted)` — 실제 이동 전 동기 해소.
3. `:3699` 형제 컷인 `WhenRemoveField`.
4. `:3718` `TriggeredSkillProcess(...)` 컷인 효과 해소. 각 본문이 `willBeRemoveField=false`로 생존 처리
   및/또는 대체(되돌림·소스트래시·소스플레이·대타삭제) 수행.
5. `:3729` `destroyTargetPermanents = filter(willBeRemoveField==true)` — 플래그 유지된 것만 실제 삭제.
6. `:3736`/`:3749` 사후 `OnDestroyedAnyone`/`OnLeaveFieldAnyone` — 확정 집합에만 발화.
7. `:3820~` 실제 트래시.

"삭제 vs 예방/대체"는 오직 컷인 윈도우에서 `willBeRemoveField`가 꺼졌는지로 결정. 예방 프리미티브 원형 =
`Evade.cs:45`(`EvadeProcess`): 자기 서스펜드 후 `willBeRemoveField=false`.

**카드 동작 유형(206장, 전부 optional·소유자 결정·비용 게이트):**
- 순수 예방(키워드): Evade, Barrier
- 비용 지불 예방: BT9_024 "같은 레벨 소스 2장 트래시로 예방"
- 대체/대타: Decoy/BT8_060 "이 디지몬을 대신 삭제"
- 아군 희생: Scapegoat/EX8_061
- 소스 플레이 대체: BT9_050 "이 디지몬의 진화원에서 Leomon 1장 코스트 없이 플레이"
- 손으로 되돌림 대체: 일반 "대신 손으로"

## 2. 헤드리스 인프라 (이미 구축됨)

**삭제 파이프라인 3종이 모두 sink로 수렴:**
- 효과 삭제: `MatchStateMutationSink.ApplyDelete` (`MatchStateMutationSink.cs:616`)
- DP제로/상태기반: `GameFlowProcessor.RuleProcessAsync` (`:233-244`) → 동일 sink(`DeleteKind`+`IsDpZeroKey`)
- 전투 삭제: `BattleResolver` (`:224` `PendingDeletionKey` → `AttackPhase.DeletionReplacement` → `:101` finalize)

**동기 "삭제되려는" 윈도우 = 지연 플래그 + 재진입 choice 루프 (완성):**
- 지연 결정: `MatchStateMutationSink.cs:635` `DeletionReplacementTiming.HasPreOption(...)` → PRE 옵션(또는 Decoy)
  있으면 `GameFlowProcessor.PendingDeletionKey=true`, 뮤테이션 skip, 카드 필드 유지.
- 윈도우 구동: `GameFlowProcessor.cs:83` `_deletionReplacement.RequestChoice(context)` → `ChoiceType.DeletionReplacement`
  choice 열고 루프 pause. sweep(`RuleProcessAsync`)은 `IsPreAwaiting` 중인 카드를 건너뜀(`:189`).
- 생존: `DeletionReplacementTiming.ClearDeletion` (`:876`) = `willBeRemoveField=false`의 대응. 거절 시
  `ReplacementDeclinedKey` → 다음 sweep이 삭제 완료.
- pause 넘는 정착: `SettleAwaitingSacrifices`(`:684`), `DeletionOutcomeWatcher.SettleAsync`(`:39`) — 둘 다
  `GameFlowProcessor.cs:56`/`72`에 배선.
- 이미 작동 키워드(`DeletionReplacementTiming.cs:41-51`): PRE(Evade·Barrier·ArmorPurge·Scapegoat·Fragment·Decoy),
  POST(Ascension·Save·Decode·Partition). 본문은 `DeletionReplacementGate.cs`. 테스트 `G3.5-C57`.
- 하드 예방(비-optional): `MatchStateMutationSink.cs:621` `CannotBeDeletedFlagKey`/`IsDeletionPreventedByContinuous`
  (별개 개념 — 완전 스킵).

**타이밍은 아직 없음:** `WhenPermanentWouldBeDeleted`는 src에서 `DeletionReplacementGate.cs:10` 주석 1건뿐.
`TriggerTimings`·`EffectTiming` enum 모두 멤버 없음 — 카드가 이 타이밍을 명명조차 못함.

## 3. 설계: 최소 갭 (전부 가산적, 신규 파이프라인 0)

### 단계 1 — 타이밍 이름 (사소, 배치2 형태)
- `EffectTiming.WhenPermanentWouldBeDeleted` enum 멤버 + `AllTimings` 등록.
- `TriggerTimings.WhenPermanentWouldBeDeleted = "WhenPermanentWouldBeDeleted"` 상수(WhenRemoveField 옆).

### 단계 2 — 지연 결정이 카드 등록 효과도 인식 (핵심, load-bearing)
- `DeletionReplacementTiming.HasPreOption`(consumed at `MatchStateMutationSink.cs:635`·`BattleResolver.cs:247`)이
  **이 타이밍에 등록된 live 효과가 있으면 PRE 옵션으로 카운트**하도록 확장. 키워드와 동일 취급.
- 없으면 카드가 지연조차 안 돼 윈도우 열리기 전에 트래시됨 → 반드시 필요.

### 단계 3 — 제네릭 "custom effect" PRE 옵션
- `RequestChoice`/`ResolveChoice`/`ApplyNoTarget` 스위치(`DeletionReplacementTiming.cs:316-320`·`740-768`)에
  `EvadeOption` 옆 신규 옵션 추가. 선택 시 카드 등록 효과 본문 실행 → 본문이 되돌림/소스트래시/소스플레이 후
  `ClearDeletion` 호출.
- 타깃/비용 픽이 필요한 효과는 기존 2단계 서브셀렉트(Priority 1, `:293-313`) 재사용.

### 경계
- 개별 카드 본문(BT9_024의 소스2장, BT9_050의 소스플레이 등)은 **per-card 포팅 몫** — 단 기존 게이트로 해소.
  이 설계는 "카드가 이 윈도우에 진입할 수 있게" 하는 인프라만.
- 3개 삭제 경로(효과·DP제로·전투)가 sink/윈도우로 수렴하므로, 단계 2·3은 세 경로 모두에 자동 적용.
- broadcast 여부: 원본 `StackSkillInfos`는 전역이나, 이 윈도우는 **삭제 대상 permanent 스코프**로 각 카드가
  자기 삭제에 반응(willBeRemoveField는 대상별). 대상=subject 스코프로 충분(broadcast 불요 예상, 구현 중 확인).

## 4. 구현 순서 & 검증
1. 단계1 → 빌드. (카드가 타이밍 명명 가능, 아직 지연 안 됨)
2. 단계2 → 이 타이밍 등록 효과가 있는 삭제가 **지연**되는지 테스트(sink·전투 경로 각각).
3. 단계3 → 프로브 효과가 윈도우에서 실행돼 `ClearDeletion`으로 **생존**하는지, 거절 시 **삭제 완료**되는지 테스트.
4. 각 단계 후 전체 스위트(316) 무회귀 확인. 삭제 경로가 민감하므로 배치 중 가장 신중히.

## 5. 관련
- [ALL_CARD_PRIMITIVE_BACKLOG.md](ALL_CARD_PRIMITIVE_BACKLOG.md) P0 타이밍 표.
- 기존 인프라: `DeletionReplacementTiming.cs`, `DeletionReplacementGate.cs`, `DeletionOutcomeWatcher.cs`,
  `tests/G3.5-C57.DefenseDeletionReplacement.Tests`.
