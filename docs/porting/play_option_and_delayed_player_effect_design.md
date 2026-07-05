# PlayOptionCards & AddEffectToPlayer(delayed) — 설계 (PRIM-P0 B.O.5)

- 작성일: 2026-07-05. 근거: AS-IS + 헤드리스 재사용 지점 조사.
- 대상: PlayOptionCards(34), AddEffectToPlayer 지연(30). 둘 다 기존 인프라 위 래퍼.

## Primitive 1 — PlayOptionCards (저위험, 먼저)

AS-IS `CardEffectCommons.PlayOptionCards(cardSources, activateClass, payCost, root, ...)` — caller가 미리 고른
옵션 카드를 `PlayCardClass.PlayCard()→UseOptionClass.UseOption()`로 재-진입(비용창 → OnUseOption → 옵션 [Main]
해소 → trash). 34장 표본 전부 `payCost:false`(코스트프리). 존은 임의(덱top/hand/security/trash).

### 헤드리스 재사용
옵션 파이프라인은 `OptionActivateAction.ProcessAsync`에 인라인(추출 코어 없음)이나, 핵심 조각 재사용:
`ZoneMover.MoveAsync`(trash), `TriggerTimings.OnUseOption` emit, `ActivatedEffectResolver.ResolveListAsync`(재귀).

### 설계
`PlayOptionCardEffect : IActivatedCardEffect(card, sourceZone, optionPredicate, maxCount, costFree, desc)`:
- BuildRequest: 후보 = `GetCards(owner, sourceZone).Where(optionPredicate)`. (ActivatedSelectAndPlayEffect 미러.)
- 리졸버 case(신규): 선택 옵션마다 → (costFree 아니면 ResolveOptionCost+Pay) → **trash-before-resolve**
  (sourceZone→Trash, 기존 헤드리스 OptionActivate 순서 일치) → `Emit(OnUseOption, subject:option)` →
  옵션 def→`CardEffectDispatch.TryCreateForCard`→`CardSource` → **같은 sink로** `ResolveListAsync(option
  .CardEffects(OptionSkill, optCard))`. ReuseMainOptionEffect와 동일 재귀(중요: `ResolveAsync` 쓰면 중첩 sink/
  BeginResolution 사이클로 deferred-choice 손상 → `ResolveListAsync` 사용).
- ToBinding throw(활성 흐름 전용).

### 리스크: LOW
OptionActivateAction의 공유 비용/검증 핸들러 무변경(B.O.4와 다름). 순수 가산(클래스+case). v1은 costFree
(34장 표본), paid/setAddSecurityEndOption은 후속/debt.

## Primitive 2 — AddEffectToPlayer 지연 (중위험, 후속)

AS-IS `AddEffectToPlayer(duration, card, cardEffect, timing)` — 미래 timing T에 **1회** 발화하는 nested 효과를
플레이어 Until…리스트에 등록, 발화 후 clear. 예 BT1_090: [Main] +2메모리 + "턴종료 시 -2메모리"(OnEndTurn 1회).

### ⚠️ 위험(핵심 발견): 헤드리스 turn-end는 CLEAR-then-FIRE(AS-IS는 FIRE-then-CLEAR)
`MetadataActionProcessor.ProcessEndTurn`: `:802 ExpireTurnEnd`(UntilEachTurnEnd 제거) → `:814 Emit(OnEndTurn)`.
즉 `timing=OnEndTurn + duration=UntilEachTurnEnd`로 등록하면 **발화 전에 만료** → never fires(기존 shim
`CardPortingFramework.cs:8031`이 timing을 버리고 이 함정에 빠짐). `duration=null`이면 매턴 재발화(1회 아님).

### 설계: self-removing one-shot(duration=null + consume-on-resolve)
기존 shim 수정: (1) timing T 존중(binding.Timing=trigger(T), player-scope=card.Owner, source=원본카드로 trash
생존), duration=null. (2) `delayedOneShot` 마커 + 트리거 해소 후 `EffectRegistry.RemoveWhere(effectId)`로 자기제거.
AS-IS fire-then-clear 일치, turn-flow 재정렬 불요(공유 핸들러 무변경). 재사용: TriggeredMemoryEffect 본문,
RemoveWhere, collector player-scope 필터.

### 리스크: MEDIUM
turn-end 만료/발화 순서 함정 이해 필요. turn-flow 재정렬은 금지(공유 핸들러 = B.O.4급 위험). null-duration
self-remove로 우회.

## 검증
- P1: 옵션을 존에서 플레이 → 옵션 [Main] 발화(관측) + 옵션 trash. 같은 sink/deferred-choice 무손상.
- P2: OnEndTurn 지연효과가 1회 발화 후 재발화 안 함(다음 턴 무발화).
- 각 단계 전체 스위트 무회귀.

## Primitive 3 — temp AddEffectToPermanent (triggered) — 실증 결과 (2026-07-05)

조사(nested-effect-grant 서브시스템)와 **실증 테스트**로 확정한 실제 상태:

### tractable subset — 오늘 동작 (엔진 변경 불요)
target이 **생존하는** 타이밍의 triggered grant(예 "[End of Your Turn] +2까지", "[When Attacking] ...")는
기존 `CardEffectCommons.AddEffectToPermanent`가 그대로 처리한다. nested를 target CardSource로 만들면
SourceEntityId=target·Timing=nestedTiming binding이 등록되고, 기존 collect→gate→fire 경로가 발화, duration
만료도 정상. 검증: `PRIM-P0.GrantTriggeredToPermanent.Tests`(발화 + 만료).

### ✅ self-[On Deletion] grant — 해결됨 (leave-play cleanup 면제 메커니즘 구현)
초기 실증에서 "발화 못함"으로 보였으나, 두 원인은 **테스트 셋업 버그**였음: (1) dp를 카드 정의 메타에 넣어
`SweepAsync`(인스턴스 메타 읽음)가 target을 삭제조차 안 함, (2) `AddMemoryTriggerEffect`의 `isOptional` 기본이
amount>0→true라 에이전트 없는 테스트에서 자동 해소 안 됨. 셋업 정정 후 **면제 메커니즘이 정상 동작**:
- **마커** `AutoProcessingTriggerCollector.SurviveOwnLeaveKey` — self-[On Deletion] grant에 스탬프.
- **두 cleanup 지점 면제**: `CardLeavePlayCleanup.OnLeftPlay` + `MatchStateMutationSink`(DeleteKind, :731)의
  `RemoveWhere(SourceEntityId==card)`가 마커 있는 binding을 건너뜀 → grant가 target 삭제를 살아남아 OnDeletion
  해소 때 존재.
- **fire-then-clear**: 동시에 `DelayedOneShotKey` 스탬프 → 발화 후 GameFlowProcessor가 제거(B.O.5 재사용).
  duration이 비-삭제 이탈(바운스)의 백스톱.
- **팩토리** `CardEffectCommons.AddSelfRemovalEffectToPermanent(target, duration, card, nested, timing)` —
  기존 AddEffectToPermanent 미러 + 두 마커 스탬프. nested는 target CardSource + self-gate(`TriggerEntityId==target`).
- 검증: 실제 0-DP sweep 삭제로 발화(+2)·target-scoped·self-remove·만료. 전체 스위트 327 PASS(공유 삭제 경로 무회귀).

부수 확인: broadcast 효과의 self-scope는 collector가 아니라 효과 CanResolve의 self-gate(`TriggerEntityId==subject`,
GameFlowProcessor가 실제 삭제 경로에서 string subject로 enrich)로 함.

### AddSkillClass — 키워드 부여는 해결(기존 player-scope 키워드 grant), 조사 반증
조사는 "쿼리-타임 set-splice 신규 훅 필요"라 했으나 **틀렸음**: 41장의 getEffects가 부여하는 것은 거의 전부
`XxxSelfEffect` **연속 키워드**(Piercing 7·Scapegoat 3·Retaliation 3·Alliance 3·Blitz 2·Decoy·Barrier·
ChangeSAttack)이지 triggered 효과가 아님. "your Digimon gain <keyword>"는 **기존 `ContinuousPlayerScopeKeywordEffect`**
(라이브 세트 재평가)로 표현됨 — 신규 훅 불요. **실증**: grant 후 입장한 Digimon도 키워드 획득·owner-scoped·per-card
술어 준수(PRIM-P0.AddSkillLiveSet.Tests).

port 대상 = player-scope `XxxStaticEffect(permanentCondition, isInherited, card, condition)` 팩토리. 기존:
Alliance/Rush/Reboot/Jamming/Collision/Vortex/TreatAsDigimon/Blocker. 신규 추가(이번): Piercing/Blitz/Retaliation/
Scapegoat/Decoy/Barrier. ChangeSAttack은 `ChangeSAttackStaticEffect`(기존 player-scope modifier).

잔여 STOP(소수): getEffects가 **triggered activated 효과**(키워드 아님)를 라이브 세트에 부여하는 드문 경우(BT8_031류
ActivateClass@OnAllyAttack) — 이것만 쿼리-타임 triggered set-splice 훅이 필요. 키워드 부여(대다수)는 완료.

## 관련
- [ALL_CARD_PRIMITIVE_BACKLOG.md](ALL_CARD_PRIMITIVE_BACKLOG.md) B.O.5.
- 재사용: `ActivatedSelectAndPlayEffect`/`ReuseMainOptionEffect`(재귀), `OptionActivateAction`(OnUseOption/ResolveOptionCost),
  `EffectRegistry.RemoveWhere`, `AutoProcessingTriggerCollector`(player-scope).
