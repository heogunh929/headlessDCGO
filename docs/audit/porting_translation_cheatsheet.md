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

## 4. 상태-읽기 질의 갭 (2026-07-06 정정 — 대부분 "이름 모름"이지 "부재" 아님)

**정정**: 초기 파일럿은 잔여 실패를 "질의 술어 부재(capability gap)"로 봤으나, BT2 재측정(§9)에서 실측하니
**키워드 보유 질의는 이미 존재**(`ContinuousKeywordGate.HasKeyword`)했고, 트래시 수·색·소유도 전부 실재였다. 즉
로컬모델이 **진짜 이름을 몰라 할루시네이션**한 naming/문서 갭이 대부분 → **§9 매핑으로 해소**(발명 아님).

**진짜 capability 갭(강모델 영역, 여전히 부재)**:
- **메모리 값 조건**(`MemoryForPlayer >= N`): 메모리 값을 조건으로 읽는 질의 술어 부재.
- **메인 페이즈 여부**(`IsMainPhase`): 카드-facing 술어 부재(§9 참조 — 대개 불요).

이 진짜 갭만 [primitive_backlog.md]/[fidelity_debt.md] 영역(강모델 선행개발, per-card 이연 금지). 나머지 상태-읽기
질의는 §9의 진짜 이름을 쓰면 열린다.

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

**v4 추가**: `[When Attacking]`의 **공격 선언 타이밍** `OnDeclaration`도 브릿지됨(subject=공격자, OnAllyAttack과
동일 방식으로 emit). AS-IS가 OnDeclaration에 선언한 activated 효과(**Digi-Burst body 포함** — BT6_028류가 여기서
발동)가 선언 시 해소됨. 298장 OnDeclaration 카드의 activated 계층이 열림.

브릿지 커버: **[When Attacking](OnAllyAttack·OnDeclaration)·[On Deletion]·[End/Start of Turn]·[Start of Main
Phase]·[on unsuspend]**에서 draw/trash/delete/select 등 activated 팩토리를 그 타이밍에 반환하면 해소됨.

## 8. 특수 플레이 프리미티브 (2026-07-05 — Special Mechanics STOP 전량 해소)

DigiXros/DNA/Blast 계열의 STOP-표면이 전부 헤드리스 팩토리로 열렸다. **이 메커니즘들은 더 이상 STOP이 아니다.**
헤드리스 특수플레이는 **auto-match 모델**(플레이어 인터랙티브 선택이 아니라, 조건 만족 재료를 엔진이 자동 매칭) —
카드는 조건(술어)만 선언하면 된다. 시그니처 전수 = `PRIMITIVE-CATALOG.md`.

| AS-IS 패턴 | 헤드리스 |
|---|---|
| `AddDigiXrosConditionClass`(기본 DigiXros) | `CardEffectFactory.DigiXrosEffect(card, costReduction, new SpecialPlayMaterial(술어, "라벨"), ...)` — 각 재료는 배틀존 후보를 매칭하는 술어 |
| `AddMaxTrashCountDigiXrosClass` / `maxTamerDigivolutionCardsCount`(재료를 트래시/테이머-진화원에서) | `CardEffectFactory.DigiXrosWithExtraMaterialsEffect(card, costReduction, maxTrashCount:Func<CardSource,int>?, maxUnderTamerCount:Func<CardSource,int>?, materials...)` — 재료 슬롯을 트래시 존/테이머 진화원 소스로 최대 N장 충족(getMaxTrashCount Func 그대로 스레드) |
| `AddJogressConditionClass`(DNA/Jogress) | `CardEffectFactory.JogressEffect(card, condition, new SpecialPlayMaterial(술어, "라벨"), ...)` 또는 이름 기반 `JogressEffectFromNames(card, condition, "이름1", "이름2")` |
| `AddJogressLevelsClass`("이 카드도 레벨 N으로 취급") | `CardEffectFactory.AddJogressLevelsEffect(card, getLevels:Func<CardSource,IReadOnlyList<int>>)` — getLevels는 진화 카드(jogressCard)를 받아 이 카드가 추가로 취급될 레벨 목록 반환. 레벨-기반 재료 술어는 `material.JogressLevelsAgainst(jogressCard).Contains(N)`로 판정 |
| `BurstDigivolutionCondition`(Burst Digivolve: 타겟 위로 진화 + 테이머 바운스) | `CardEffectFactory.BurstDigivolveEffect(card, digimonCondition:Func<CardSource,bool>, tamerCondition:Func<CardSource,bool>, cost)` — 타겟 Digimon 위로 무료진화 + 매칭 테이머 hand 바운스 + cost. 엔진이 타겟·테이머 자동 매칭 |
| `IDigiBurst`(`[Digi-Burst N] <효과>`: 진화원 N장 trash 비용) | `CardEffectFactory.DigiBurstEffect(card, count, innerEffect:ICardEffect, "설명")` — 자기 진화원 소스 N장 trash 후 innerEffect 발동. inner가 activated면 해소, **연속 grant(키워드/스탯)면 register**. ≥N trashable 소스일 때만 발동. 트리거 타이밍(OnDeclaration 등)에서 반환하면 브릿지가 해소 |
| `DNADigivolveWithHandOrTrashCardIntoHandOrTrash`(효과-구동 DNA: hand/trash 카드로 진화) | `CardEffectFactory.DnaDigivolveFromHandOrTrashEffect(card, intoCondition, permanentCondition, materialCondition, intoFromHand:bool, materialFromHand:bool)` — into-카드(hand/trash) 위로 필드 permanent + hand/trash 재료를 융합. 엔진이 자동 매칭 |
| `AddAssemblyConditionClass`(Assembly: 트래시에서 재료로 플레이) | 이미 배선됨 — 카드가 `AssemblyConditionOf`를 선언하면 `PlayCardAction`이 트래시 재료로 Assembly 플레이를 제안. 별도 팩토리 불요 |

**주의**: 위 재료 술어(`SpecialPlayMaterial`의 `Func<CardSource,bool>`, `digimonCondition`, `tamerCondition`)는
§0 규칙대로 **술어를 그대로 평가**하라(카드명 동등만 하지 말고 레벨/색/타입 등 원본 조건 미러). Digi-Burst inner가
"gain 키워드"류 **연속 효과**면 activated가 아니므로 register 경로로 자동 처리된다(카드는 inner를 그대로 넘기면 됨).

## 9. 상태-읽기 질의: 진짜 헤드리스 이름 (2026-07-06 BT2 재측정 할루시네이션 정정)

**이 질의들은 헤드리스에 이미 있다 — 존재하지 않는 이름을 지어내지 말고 아래 진짜 이름을 써라.** (BT2 재측정에서
로컬모델이 아래 왼쪽을 할루시네이션했으나, 전부 오른쪽으로 실재.) 카드 조건 술어 안에서 `card.Context`로 상태 접근.

| 지어낸 이름(쓰지 말 것) | 진짜 헤드리스 |
|---|---|
| `HasReboot(x)` / `HasBlocker(x)` 등 키워드 보유 | `HeadlessDCGO.Engine.Headless.Runtime.ContinuousKeywordGate.HasKeyword(card.Context, 대상_InstanceId, "Reboot")` — 모든 키워드 동일(Blocker/Rush/Jamming/…) |
| `GetTrashCount()` / `GetOpponentTrashCount()` 트래시 수 | `((IZoneStateReader)card.Context.ZoneMover).GetCards(플레이어, ChoiceZone.Trash).Count` — 자기=`card.Owner`, 상대=상대 playerId. 덱/시큐리티/hand도 존만 바꿔 동일 |
| `TopCardHasColor("Red")` 색 보유 | `card.HasCardColor("Red")` (또는 `card.CardColors` — 색변경 효과까지 반영) |
| `IsOwnerOwnedDigimon(x)` 소유+타입 | 조합: `x.Owner == card.Owner && x.IsDigimon` (`Owner`/`Controller`/`IsDigimon`/`IsTamer` 실재) |
| `card.PermanentId` | `card.InstanceId` (permanent id = 그 카드 InstanceId) |
| `card.CardNames`, `card.Level`, `card.HasCardColor`, `card.DP` | 실재 접근자 — 카드 조건은 이걸로 구성(레벨/색/이름/DP 질의 별도 함수 지어내지 말 것) |

**진짜 없는 것(문서화만, 발명·구현 보류)**:
- **메인 페이즈 여부 질의**(`IsMainPhase`): 카드-facing 술어 부재. 대개 activated 능력은 타이밍/컨텍스트가 이미
  메인페이즈를 강제하므로 별도 체크 불요 — 그 경우 조건에서 빼라. 정말 페이즈 값이 필요하면 **STOP**(강모델 영역).

### 팩토리 시그니처 — 인자 지어내지 말 것

컴파일 실패 다수가 **팩토리 인자 수/타입 할루시네이션**이다. 시그니처는 반드시 `PRIMITIVE-CATALOG.md`에서 확인하고
그대로 호출하라. 자주 틀린 것:

| 오호출 | 진짜 시그니처 |
|---|---|
| `SelectAndReturnToDeckEffect(...)` | `(CardSource card, Func<HeadlessEntityId,bool> canTarget, int maxCount, bool toTop, bool canEndNotMax, string description)` |
| `PlaceSelfDelayOptionSecurityEffect(card, condition)` | `(CardSource card)` — **인자 `card` 하나뿐. condition 오버로드 없다** |
| 술어 자리에 2-인자 람다 | 대부분 `Func<bool>`(조건) 또는 `Func<HeadlessEntityId,bool>`(canTarget). 인자 개수를 카탈로그로 확인 |

## 10. action_tag → 정규 팩토리 맵 (2026-07-06 — 레퍼런스 대체 계층)

**왜**: 카드 단위 레퍼런스는 60%가 signature 고유라 일반화 안 됨(시딩 무의미). 그러나 **action_tag는 83%가 공유** —
"카드는 다 달라도 무슨 동작을 하는가는 겹친다". 그래서 아래 **액션→팩토리** 맵이 레퍼런스를 대체하는 일반화 계층이다.
카드의 동작을 이 팩토리로 매핑하라(전부 실재 — 발명 아님). 하네스는 카드의 action_tags에 해당하는 줄만 프롬프트에
자동 주입한다(`action_map.json`, `gen_action_map.py`로 재생성). 시그니처는 `PRIMITIVE-CATALOG.md`에서 확인.

| action_tag | 정규 팩토리 (변형) | 비고 |
|---|---|---|
| `play` | `SelectAndPlayFromZoneEffect` / PlayOptionCardEffect | 존에서 골라 플레이 / 옵션 플레이 |
| `trash` | `SelectAndTrashFromZoneEffect` / SelectAndTrashDigivolutionEffect | 존에서 골라 trash / 진화원 trash |
| `once_per_turn` | *(modifier)* | 액션 아님 = [Once Per Turn] 캡. 다중발화 타이밍은 브릿지가 OnceFlags 자동 캡(§7 v3) |
| `security` | `SelectAndPutSecurityEffect` / ReplaceBottomSecurityWithFaceUpOptionEffect | 시큐리티에 놓기 |
| `memory` | `GainMemoryActivatedEffect` | 메모리 증감(+/-) |
| `digivolve` | `SelectAndDigivolveEffect` / BlastDigivolveEffect, BurstDigivolveEffect, ArtsDigivolveEffect | 골라 진화 / 특수진화 |
| `delete` | `SelectAndDestroyEffect` | 디지몬 골라 삭제(canTarget 술어로 제한) |
| `to_hand` | `SelectAndAddToHandFromZoneEffect` / SelectAndBounceEffect, AddThisCardToHandEffect | 존→손패 / 필드 바운스 / 자기 손패 |
| `deenergize` | `SelectAndDeDigivolveEffect` / SelectAndTrashDigivolutionEffect | 진화원 N장 trash |
| `draw` | `DrawCardsEffect` | N장 드로우 |
| `suspend` | `SelectAndSuspendEffect` | 골라 서스펜드(탭) |
| `bounce` | `SelectAndBounceEffect` / SelectAndReturnToDeckEffect | 손패 바운스 / 덱(toTop bool) |
| `cannot` | *(restriction-family)* `CanNot*StaticEffect` | 금지 대상별: Attack/BeDestroyed/AddSecurity/Digivolve/Block 등 |
| `unsuspend` | `SelectAndUnsuspendEffect` | 골라 언서스펜드 |
| `dp_minus` | `SelectAndBuffDpEffect`(음수) / ChangeDPStaticEffect | DP -N |
| `dp_plus` | `SelectAndBuffDpEffect` / PlayerScopeBuffDpEffect, ChangeSelfDPStaticEffect | DP +N (대상/스코프/자기) |
| `recovery` | `RecoveryTriggerEffect` | 시큐리티 회복 |
| `blocker` | `BlockerStaticEffect` / BlockerSelfStaticEffect | Blocker 부여(스코프/자기) |
| `piercing` | `PiercingStaticEffect` / PierceSelfEffect | Piercing 부여(스코프/자기) |

**주의**: 맵은 **개별 동작**을 정규 팩토리로 접지할 뿐이다. 여러 동작의 **합성**(조건·대상·타이밍 배선)과 술어의
**충실 평가**(§0)는 여전히 카드별로 해야 한다 — 태그로 뭉개지 말 것.

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
