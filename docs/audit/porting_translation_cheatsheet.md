# 포팅 번역 치트시트 — AS-IS(Unity) 도메인 패턴 → 헤드리스(.NET)

- 작성일: 2026-07-05. 근거: 파일럿(BT1 exact) 실패 카드의 CS1061(도메인 멤버 환각) 전수 분석.
- 목적: 심볼 표면(팩토리·커먼즈 시그니처)으로 안 잡히는 **의미 번역 계층**을 규칙화한다. 파일럿이 확정:
  통과율은 심볼 표면으로 ~50%까지 오르고, 나머지는 전부 이 계층. 이 치트시트가 그 벽을 여는 유일한 레버.
- 사용: `tools/porting/pilot/port_with_sonnet.py`의 심볼 표면에 이 규칙을 부록으로 주입(자동 로드). 살아있는
  문서 — 파일럿 실패에서 새 패턴이 나오면 여기 추가하고 재측정.

## 0. 핵심 구조 규칙 (가장 중요)

AS-IS 조건 술어는 **`Func<Permanent, bool>`** 이고 `permanent.속성`으로 상태를 읽는다.
헤드리스 조건 술어는 **`Func<HeadlessEntityId, bool>`** 이고 `CardEffectCommons.술어(card, id)`로 읽는다.

```
// AS-IS
bool CanSelect(Permanent permanent) => permanent.HasNoDigivolutionCards && permanent.IsDigimon;
// 헤드리스 (id를 받아 커먼즈 질의로)
bool CanSelect(HeadlessEntityId id) =>
    CardEffectCommons.HasNoDigivolutionCards(card, id) && CardEffectCommons.IsBattleAreaDigimon(card, id);
```

**규칙: `permanent.X` → `CardEffectCommons.X(card, id)` (같은 이름의 커먼즈 술어가 대개 존재).**
도메인 객체(`PermanentView`/`HeadlessEntityId`/`HeadlessPlayerId`)에는 게임-상태 속성이 없다
(`HeadlessEntityId`/`HeadlessPlayerId`는 `Value`·`IsEmpty`뿐). 상태 질의는 **전부 커먼즈 경유.**

## 1. Permanent 속성 → 커먼즈 술어 (id 받는 형태)

| AS-IS (`permanent.X`) | 헤드리스 |
|---|---|
| `permanent.HasNoDigivolutionCards` | `CardEffectCommons.HasNoDigivolutionCards(card, id)` |
| `permanent.IsDigimon` | `CardEffectCommons.IsBattleAreaDigimon(card, id)` (배틀존 디지몬 여부) |
| `permanent.IsSuspended` | `CardEffectCommons.IsSuspended(card, id)` |
| `permanent`(상대 소유 디지몬인가) | `CardEffectCommons.IsOpponentBattleAreaDigimon(card, id)` |
| `permanent`(내 소유 디지몬인가) | `CardEffectCommons.IsOwnerBattleAreaDigimon(card, id)` |
| `permanent.Level` (정수 레벨) | `CardEffectCommons.LevelOf(card, id)` → int. 비교는 `LevelOf(card,id) >= N`. min/max는 `IsMinLevel`/`IsMaxLevel`. |
| `permanent.HasPierce` / `HasBlocker` 등 키워드 보유 질의 | **프리미티브 갭(확정)** — 헤드리스에 키워드-보유 질의 술어 없음(`Gain*`은 부여용). 조건으로 필요하면 프리미티브 부채 또는 STOP. |

## 2. Owner/Enemy 탐색 → 스코프 커먼즈

| AS-IS | 헤드리스 |
|---|---|
| `card.Owner.Enemy.GetBattleAreaDigimons().Any(p => COND)` | `CardEffectCommons.HasMatchConditionOpponentsPermanent(card, id => COND(id))` |
| `card.Owner.Enemy...Count(p => COND) >= N` | `CardEffectCommons.MatchConditionPermanentCount(card, id => COND(id)) >= N` (상대/자기 스코프는 COND의 커먼즈로) |
| `card.Owner.SecurityCards.Count` | `CardEffectCommons.SecurityCount(card)` |
| `card.Owner.MemoryForPlayer >= N` (메모리 값 조건) | **프리미티브 갭(확정)** — 메모리 값을 조건으로 읽는 질의 커먼즈 없음. 효과는 `GainMemory*`/`SetMemoryTo` 팩토리로 표현. 조건 질의가 필요하면 프리미티브 부채. |
| `CardEffectCommons.IsOwnerTurn(card)` / `IsOpponentTurn(card)` | 동일(이미 커먼즈, 그대로) |
| `CardEffectCommons.IsExistOnBattleArea(card)` | 동일(자기 배틀존 존재 — 활성 가드) |

## 3. 트리거 가드 (타이밍 커먼즈)

AS-IS의 `CanUseCondition(Hashtable)`은 헤드리스에서 타이밍별 트리거 술어로:

| AS-IS | 헤드리스 타이밍 / 가드 |
|---|---|
| `CanTriggerOnAttack(hashtable, card)` | 타이밍 `OnAllyAttack` + `CardEffectCommons.CanTriggerOnAttack(...)` |
| `CanTriggerWhenDigivolving(hashtable, card)` | 타이밍 `WhenDigivolving`(번역됨 — AS-IS는 `OnEnterFieldAnyone` 게이팅) |
| `CanTriggerOptionMainEffect(hashtable, card)` | 타이밍 `OptionSkill`/`SecuritySkill` |

## 4. 확정된 프리미티브 갭 (치트시트로 못 여는 계층 — 프리미티브 개발 필요)

파일럿 실측 결과, 잔여 실패는 "프롬프트 정보 부족"이 아니라 **헤드리스에 질의 술어가 없는 진짜 갭**:
- **키워드 보유 질의**(`HasPierce`/`HasBlocker` …): 조건으로 "특정 키워드 보유" 판정 술어 부재. `Gain*`은 부여용.
- **메모리 값 조건**(`MemoryForPlayer >= N`): 메모리 값을 조건으로 읽는 질의 술어 부재.

이 둘은 [primitive_backlog.md]/[fidelity_debt.md] 영역 — 강모델이 질의 프리미티브를 선행 개발해야 열린다
(카드 포팅 중 per-card 이연 금지 원칙). 치트시트로 커버 불가.

## 6. 신규 EFFECT 프리미티브 번역 (2026-07-05 — P0 Build Order 전량 구현)

AS-IS의 STOP-표면(동적/코루틴 흐름)을 헤드리스 카드-facing 팩토리로 미러. **§4의 갭은 조건 질의(상태 읽기) 계층이고,
아래는 effect 계층 — 이제 STOP 아님.** 시그니처 전수 = `PRIMITIVE-CATALOG.md`(147종).

| AS-IS 패턴 | 헤드리스 |
|---|---|
| `UserSelectionManager` Set/IntSelection 메뉴(모드 선택) | `CardEffectFactory.SelectModeEffect(card, "설명", new ModeChoiceEffect.Mode("라벨", 가용성술어_Func<bool>?, 분기효과_ICardEffect), ...)` — 가용성술어 false면 그 모드 생략 |
| `SelectCardEffect`로 존에서 골라 손패로 | `CardEffectFactory.SelectAndAddToHandFromZoneEffect(card, fromZone, canTarget, maxCount, canEndNotMax, "설명")` |
| …골라 trash / 덱복귀 / 시큐리티 | `SelectAndTrashFromZoneEffect` / `SelectAndReturnToDeckEffect` / `SelectAndPutSecurityEffect` (동일 시그니처 형태) |
| …골라 디지볼브(진화) | `SelectAndDigivolveEffect(card, fromZone, canTarget, DigivolveCost, ...)` |
| `ChangeCostClass`(BeforePayCost 감액) | `CardEffectFactory.BeforePayCostReductionEffect(card, amount, condition, "설명")` — play·digivolve 양쪽 델타 등록, 카드가 어느 액션인지로 적용. play 전용이면 `if (timing == EffectTiming.BeforePayCost && CardEffectCommons.IsPayCostRoot(card, PayCostRoot.Play))` 게이트 |
| `CannotAddSecurityClass.SetUp(PlayerCondition, CardEffectCondition)` | `CanNotAddSecurityStaticEffect(scopePlayer, isInherited, card, condition, causingEffectPredicate)` — **CardEffectCondition을 causingEffectPredicate로 넘겨라**(뭉개면 과차단) |
| `CannotAddMemoryClass` | `CanNotAddMemoryStaticEffect(scopePlayer, isInherited, card, condition, causingEffectPredicate)` |
| `CannotReduceCostClass` | `CanNotReduceCostStaticEffect(permanentCondition, isInherited, card, condition)` |
| `CardEffectCommons.PlayOptionCards`(존에서 옵션 플레이) | `CardEffectFactory.PlayOptionCardEffect(card, sourceZone, optionPredicate, maxCount, canEndNotMax, "설명")` — 옵션 [Main] 자동 해소 후 trash |
| `CardEffectCommons.AddEffectToPlayer(duration, card, effect, timing)`(지연 플레이어 효과) | 동일 이름 그대로. nested가 timing T에 1회 발화 후 self-remove(fire-then-clear) |
| `CardEffectCommons.AddEffectToPermanent(perm, duration, card, effect, timing)` | 동일 이름. nested는 **대상 permanent의 CardSource**로 만들어라(소스=대상). |
| …단, 대상 자신의 `[On Deletion]`을 부여(대상 삭제 시 발화) | `CardEffectCommons.AddSelfRemovalEffectToPermanent(perm, duration, card, nested, timing)` — leave-play cleanup 면제. nested는 `triggerGate: rc => rc.EffectContext.TriggerEntityId == 대상` self-gate |
| `AddSkillClass`로 "네 디지몬이 \<키워드\> 획득" | player-scope `<키워드>StaticEffect(permanentCondition, isInherited, card, condition)` — cardSourceCondition을 permanentCondition으로. 라이브 세트(늦게 입장한 카드도 획득). Piercing/Blitz/Retaliation/Scapegoat/Decoy/Barrier/Alliance/Rush/Reboot/Jamming/Blocker |
| `AddSkillClass`로 "네 디지몬이 \<triggered 효과\> 획득"(BT8_031류) | `CardEffectFactory.GrantTriggeredEffectToScopedSet(card, scopePlayer, nested)`. nested는 `TriggerEntityId`(실제 트리거한 카드)를 읽고 cardSourceCondition을 그 카드에 적용하도록 구성 |

## 7. 트리거된 activated 효과 (2026-07-05 브릿지 v1)

`[When Attacking]`(OnAllyAttack)·`[On Deletion]`(OnDestroyedAnyone) 타이밍에서 **해소가 필요한 액션**(draw/trash/
delete/select 등)은 이제 그 타이밍에 **activated 팩토리를 그대로 반환**하면 auto-processing 브릿지가 해소한다.

```
// [When Attacking] draw 1
if (timing == EffectTiming.OnAllyAttack)
    effects.Add(CardEffectFactory.DrawCardsEffect(card, 1));
// [On Deletion] delete 1 opp Digimon (activated select)
if (timing == EffectTiming.OnDestroyedAnyone)
    effects.Add(CardEffectFactory.SelectAndDestroyEffect(card, canTarget, 1, false, "..."));
```

**v2 추가**: 경계 타이밍 `[End of Turn]`(OnEndTurn)·`[Start of Turn]`(OnStartTurn)·`[Start of Main Phase]`
(OnStartMainPhase)도 브릿지됨 — subject 없는 턴 경계라 전체 배틀존 스캔, 턴당 1회. `[End of YOUR Turn]`은 카드가
`CardEffectCommons.IsOwnerTurn(card)`로 게이트.

```
if (timing == EffectTiming.OnEndTurn && CardEffectCommons.IsOwnerTurn(card))
    effects.Add(CardEffectFactory.SelectAndDestroyEffect(card, ...));  // [End of Your Turn] delete 등
```

**v3 추가**: `[on unsuspend]`(OnUnTappedAnyone) — subject-스코프, 턴 내 다중발화. 브릿지가 **once-per-turn 캡**을
자동 적용(OnceFlags, 턴엔드 리셋). 즉 카드는 `[Once Per Turn]` 명시 없이 activated 팩토리만 반환하면 됨(재-언서스펜드
시 재발화 안 됨). memory/DP/recovery/unsuspend는 여전히 기존 triggered 팩토리(scheduler-캡) 사용 — 브릿지 대상 아님.

브릿지 커버: **[When Attacking]·[On Deletion]·[End/Start of Turn]·[Start of Main Phase]·[on unsuspend]**에서
draw/trash/delete/select 등 activated 팩토리를 그 타이밍에 반환하면 해소됨.

## 5. 파일럿 실측 (BT1 exact 15장, Sonnet 4.6)

| 라운드 | 프롬프트 | 컴파일 통과 |
|---|---|---|
| 1 | 없음 | (환각 다수) |
| 2 | 팩토리+타이밍 심볼 표면 | 8/15 (53%) |
| 3 | +커먼즈 클래스 구분 | 7/15 (수확 체감 — 잔여가 도메인 번역) |
| 4 | **+이 치트시트** | **12/15 (80%)** |

- 라운드3→4 상승분(+5장)이 곧 이 치트시트의 커버리지. 남은 3장은 모두 §4 프리미티브 갭.
- 결론: **심볼 표면 + 이 치트시트로 exact 카드의 ~80%가 컴파일 통과**. 나머지는 프리미티브 갭(프롬프트로
  못 여는 별도 트랙). 살아있는 문서 — 다른 세트에서 새 도메인 패턴이 나오면 §1~2에 추가.
