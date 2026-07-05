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

## 관련
- [ALL_CARD_PRIMITIVE_BACKLOG.md](ALL_CARD_PRIMITIVE_BACKLOG.md) B.O.5.
- 재사용: `ActivatedSelectAndPlayEffect`/`ReuseMainOptionEffect`(재귀), `OptionActivateAction`(OnUseOption/ResolveOptionCost),
  `EffectRegistry.RemoveWhere`, `AutoProcessingTriggerCollector`(player-scope).
