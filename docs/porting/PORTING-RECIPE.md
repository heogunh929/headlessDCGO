# 카드 포팅 레시피 (로컬모델용 · 결정적 절차)

> 대상: opencode + 로컬모델(gemma3 / qwen3-coder). **추론 최소화, 조회 최대화.** 아래 절차를 **그대로** 따른다.
> 프리미티브(카드가 부르는 팩토리)는 **전부 선행개발 완료**. 포팅 = **원본을 미러 + 파라미터 채우기**. **새 프리미티브를 만들지 않는다.**

---

## 핵심 규칙 (어기지 말 것)
1. **AS-IS 1:1 미러**: 헤드리스 카드 파일은 원본과 **같은 타이밍 분기 · 같은 `CardEffectFactory.<이름>(...)` 호출**을 그대로 옮긴다. 로직을 바꾸거나 단순화하지 않는다.
2. **프리미티브 개발 금지**: 원본이 부르는 팩토리는 [PRIMITIVE-CATALOG.md](PRIMITIVE-CATALOG.md)에 있어야 한다. **없으면 만들지 말고 STOP**(→ §5 에스컬레이션).
3. **`DCGO/`는 읽기 전용**(원본 참조). 절대 수정/커밋하지 않는다. `bin/`·`obj/`도 건드리지 않는다.
4. **한 카드 = 한 사이클**: 미러 → 테스트 추가 → `bash scripts/run-tests.sh` green → 다음 카드.

---

## 절차 (카드 1장)

### 1) 원본 읽기
`DCGO/Assets/Scripts/CardEffect/<SET>/<COLOR>/<ID>.cs`
- 확인할 것: `CardEffects(EffectTiming timing, ...)` 안의 **각 `if (timing == EffectTiming.X)` 분기**와 그 안의 `CardEffectFactory.<Method>(...)` 호출.

### 2) 각 팩토리 호출을 카탈로그에서 조회
- 호출된 `<Method>` 이름을 [PRIMITIVE-CATALOG.md](PRIMITIVE-CATALOG.md) **알파벳 마스터**에서 찾는다.
  - **있으면** → 그 헤드리스 시그니처로 인자를 맞춘다(이름 동일이 원칙).
  - **없으면** → **STOP**. §5로.

### 3) 헤드리스 미러 파일 작성
경로: `src/HeadlessDCGO.Engine/Assets/Scripts/CardEffect/<SET>/<COLOR>/<ID>.cs`
(스켈레톤이 이미 있으면 본문만 교체.) 아래 템플릿을 채운다:

```csharp
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.<SET>.<COLOR>;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class <ID> : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // 원본의 각 타이밍 분기를 그대로 옮긴다:
        if (timing == EffectTiming.<X>)
            cardEffects.Add(CardEffectFactory.<Method>(/* 인자: 원본과 동일, 카탈로그 시그니처 순서 */));

        return cardEffects;
    }
}
```
- 인자 관용: `card: card`, `isInheritedEffect: false`(진화원 상속 아닐 때), `condition: null`(원본에 조건 없으면). 원본에 조건/값이 있으면 그 값을 그대로 넣는다.
- 타이밍 `EffectTiming.X`가 없다는 컴파일 에러가 나면 → **STOP**(§5, 타이밍 신설은 강모델 몫).

### 4) 테스트 추가 (새 프로젝트 만들지 않음)
- 그 카드가 속한 **그룹 테스트 프로젝트** `tests/CardEffect.<SET>.<COLOR>.Tests/`에 sub-test 추가.
- 최소 단언: 카드 등록 후 효과가 **라이브**인지.
  - 연속 DP/SA: `ContinuousModifierGate.ResolveDp/ResolveSecurityAttack(...)` 값 변화.
  - 키워드: `ContinuousKeywordGate.HasKeyword(context, id, "<Keyword>")` == true.
  - 제약: `ContinuousRestrictionGate.EvaluateAttack/Block/...(...).IsRestricted` == true.
  - (조회 방법은 [PRIMITIVE-CATALOG.md](PRIMITIVE-CATALOG.md) 각 항목 참고, 기존 `tests/G9-0xx` 테스트 패턴 복사.)

### 5) 게이트
```bash
bash scripts/run-tests.sh          # SUMMARY: PASS=N FAIL=0 여야 함
```
- FAIL이면 원본과 미러를 다시 대조(로직 누락/인자 오류). green이면 이 카드 완료.

---

## 4-b. 코루틴 효과 = 구문 미러 아님, **의도→팩토리 번역**
원본의 **활성/트리거 효과는 코루틴 빌더**입니다(`ActivateClass` + `IEnumerator ActivateCoroutine` 안에서 `new DrawClass(...).Draw()` / `CardEffectCommons.ChangeDigimonDP(...)` / `.Tap()` / `.Destroy()`). 헤드리스는 **선언형**(ICardEffect + mutation)이라 이 코루틴을 **그대로 옮기지 못합니다.** 대신 **의도를 읽어 대응 팩토리**를 고른다.

| 원본 코루틴 의도 | 헤드리스 팩토리 |
|---|---|
| `new DrawClass(owner, N, ..).Draw()` | `CardEffectFactory.DrawCardsEffect(card, N)` |
| `owner.AddMemory(±N)` / 메모리 증감 | `AddMemoryTriggerEffect(timing, ±N, ...)` (트리거형) |
| `new SuspendPermanentsClass(perms,..).Tap()` (선택-서스펜드) | `SelectAndSuspendEffect(card, canTarget, maxCount, canEndNotMax, desc)` |
| 선택-언서스펜드 / 선택-바운스 | `SelectAndUnsuspendEffect(...)` / `SelectAndBounceEffect(...)` |
| `new DestroyPermanentsClass(perms,..).Destroy()` (선택-파괴) | `SelectAndDestroyEffect(card, canTarget, maxCount, canEndNotMax, desc)` |
| `CardEffectCommons.ChangeDigimonDP(target, ±N, dur, ..)` (선택-DP) | `SelectAndBuffDpEffect(card, canTarget, maxCount, ±N, duration, desc)` |
| `[All Turns]` 옵션/시큐리티 스탯 버프(플레이어 스코프) | `PlayerScopeBuffSAttackEffect` / `PlayerScopeBuffSecurityDpEffect` |
| `CardEffectCommons.PlayPermanentCards(sel, .., root)` (존에서 select-and-play) | `SelectAndPlayFromZoneEffect(card, fromZone, canTarget, maxCount, canEndNotMax, desc)` (root→fromZone: Trash/Hand) |
| `CardEffectCommons.ChangeDigimonSAttack(target, ±N, dur, ..)` (선택-SA) | `SelectAndBuffSAttackEffect(card, canTarget, maxCount, ±N, duration, desc)` |
| `CardEffectCommons.AddThisCardToHand(..)` | `AddThisCardToHandEffect(card)` |
| `new IgnoreColorConditionClass(cardCondition)` | `UseRequirements(card, cardCondition)` |
| `new CanNotSuspendClass(PermanentCondition)` (self) | `CantSuspendStaticEffect(permanentCondition, false, card, condition)` |
| `new CanNotBeDestroyedClass(..)` / `CanNotBeDeleted` (self) | `CanNotBeDestroyedStaticEffect(permanentCondition, false, card, condition, name)` |
| `new ChangeCostClass()` / 자기 플레이코스트 증감 | `ChangePlayCostStaticEffect(...)` (연속) |
| `CardEffectCommons.DigivolveIntoHandOrTrashCard(..)` (선택-디제너) | `SelectAndDeDigivolveEffect(card, canTarget, maxCount, count, canEndNotMax, desc)` |
| `CardEffectCommons.SimplifiedRevealDeckTopCardsAndSelect(..)` | `SimplifiedRevealDeckTopCardsAndSelect(card, revealCount, conditions, remainingTo, desc)` |
| `new CanNotAffectedClass()` (효과 면역) | `CanNotAffectedStaticEffect(permanentCondition, false, card, condition)` |
| `new ChangeCardNamesClass()` (이름 추가) | `ChangeCardNamesStaticEffect(addedName, false, card, condition)` |
| `CardEffectFactory.BlastDigivolveEffect(card, cond)` | `BlastDigivolveEffect(card, condition)` (레시피 선언, 엔진이 실행) |
| `CardEffectFactory.DigiXrosEffectFromNames(card, cr, .., names)` | `DigiXrosEffectFromNames(card, costReduction, canTarget, names)` |
| `new AddDigiXrosConditionClass(); SetUp(GetDigiXros)` — 재료가 **이름만** | `DigiXrosEffectFromNames(card, cr, null, names…)` |
| `new AddDigiXrosConditionClass(); SetUp(GetDigiXros)` — 재료에 **임의 조건**(`CanSelectCardCondition`) | `DigiXrosEffect(card, cr, new SpecialPlayMaterial(cs => …원본 술어 1:1…, "label"), …)` — **술어를 뭉개지 말 것** |
| `CardEffectFactory.BlastDNADigivolveEffect(card, conds, cond)` | `BlastDNADigivolveEffect(card, blastDNAConditions, condition)` |
| `new AddJogressConditionClass(); SetUp(GetJogress)` (DNA/Jogress) | `JogressEffectFromNames(card, condition, names…)` (GetJogress 재료 이름 번역) |
| `partitionConditions.Add(new PartitionCondition(4, CardColor.Red)); …` (Partition 색 그룹) | 같은 이름 그대로: `new PartitionCondition(4, "Red")` 2개 배열 → `PartitionSelfEffect(false, card, cond, new[]{c0, c1})` — **조건을 빼먹지 말 것**(색 그룹이 메커니즘의 정의) |
| `new MindLinkClass(tamer, digimonCondition, activateClass).MindLink()` (Mind Link) | `new MindLinkClass(new Permanent(ctx, tamerId, owner), digimonCondition, null)` → `BuildRequest()`/`MindLink(선택Id)` — 키워드 grant(`MindLinkSelfEffect`)는 표시용일 뿐 메커니즘이 아님 |
| `new ChangeCardLevelClass(); SetUpChangeCardLevelClass(GetLevel)` (레벨/색/특성 변경) | 같은 클래스명 그대로 미러: `SetUpICardEffect(설명, CanUse, card)` + `SetUpChange...Class(원본 변환 Func 1:1)` — 색/특성은 `List<string>` |
| `CardEffectCommons.RevealDeckTopCardsAndSelect(revealCount, 조건, remainingPlace, ..)` (단일 조건) | `RevealAndSelect.RequestChoice(ctx, player, revealCount, maxSelect, selectedTo, remainingTo, selectCondition, isOpponentDeck)` |
| `RevealDeckTopCardsAndProcessForAll(...)` (선택 없음, 전 매칭 처리) | `RevealAndSelect.RevealAndProcessAllAsync(...)` — **초이스로 바꾸지 말 것**(mandatory) |
| `RevealDeckTopCardsAndSelect(revealCount, SelectCardConditionClass[]{...}, ...)` (다중 조건, BT10-096형) | `RevealAndSelect.RequestMultiChoice(ctx, player, revealCount, new[]{ new RevealSelectPass(조건, max, 목적지, 메시지, canNoSelect, canEndNotMax), ... }, remainingTo)` — 원본 `Mode.Custom` = `RevealDestination.Custom`(카드 스크립트가 `RevealFlowState.TakeCustomSelections()`로 회수해 후속 처리) |
| `SelectPermanentEffect` Attack/Degenerate 모드 | `SetUp(..., Mode.Attack/Degenerate, ...)` + `SetAttackOptions(canAttackPlayer, defenderCondition)`(원본 ctor 인자 그대로) / `SetDegenerationCount(n)`; Attack 실행은 `TryOpenAttack(ctx, selected)` — 다중 공격자는 자동 순차 |
| `canEndSelectCondition`(선택 조합 제약, 예: "서로 다른 색 2장") | `SetCanEndSelectCondition(집합술어)` — resolve가 불법 조합을 중앙 거부 |
| `activateClass.SetIsLinkedEffect(true)` (링크 상태 효과) | 해당 팩토리의 `isLinkedEffect: true` — 링크 중일 때만 활성(라이브 게이트) |
| dual 카드(Digimon이자 Option) | 정의 메타 `"cardTypes"`에 추가 종류 배열 (`CardRecord.AdditionalCardTypesKey`) |
| 카운터 타이밍의 진짜 [Counter] 효과 | binding values `AutoProcessingTriggerCollector.IsCounterEffectKey = true` (2-pass 순서: 비-[Counter] 먼저) |

> **STOP-목록(강모델 전용)**: `new AddSkillClass()`(효과 동적 부여)·`CardEffectCommons.AddEffectToPlayer(..)`(플레이어 딜레이)·`CardEffectCommons.PlayOptionCards(..)`·`AddSelfLinkConditionStaticEffect`(대체 링크원)·`AddMaxTrashCountDigiXrosClass`(DigiXros 트래시-보정)·중첩 커스텀 coroutine(예: removal-prevent, `ChangeEndTurnMinMemoryClass`). 이들은 STOP 후 강모델로.
> **특수플레이 DigiXros/Blast/DNA/Jogress는 이제 위 팩토리로 선언 = 로컬모델 가능**(재료 이름/조건만 config).
| `[All Turns]` 스탯/키워드 (timing==None) | 해당 `*SelfStaticEffect` / `*SelfEffect` (연속형, 미러 OK) |
| 대체 진화원 | `AddSelfDigivolutionRequirementStaticEffect(permanentCondition, ...)` |

> **판정법**: 효과가 `timing == None`의 **연속/키워드/스탯 팩토리**면 **구문 미러 OK**(카탈로그에서 찾아 그대로). 효과가 **코루틴(`IEnumerator`/`.Draw()/.Tap()/.Destroy()`)**이면 → **의도를 읽어 대응 팩토리가 카탈로그에 있으면 사용, 없으면 STOP**(강모델). 코루틴을 억지로 옮기지 말 것.

## 4-c. 부분 포팅 (PARTIAL) — 분기별로 처리
한 카드는 **타이밍 분기별로 독립**(`if (timing == ...)`)입니다. 어떤 분기는 매핑되고 어떤 분기는 STOP일 수 있다 → **매핑되는 분기는 포팅하고, STOP 분기만 남긴다.** 카드를 통째로 STOP하지 말 것.
- 매핑되는 분기: 정상 미러/팩토리로 채운다.
- STOP 분기: 그 분기 자리에 주석 `// STOP: <이유> — 강모델` 을 남기고 `cardEffects.Add` 를 생략(또는 `DeferredCardEffect`). 카드 id + STOP 분기를 기록해 강모델에 넘긴다.
- 결과: 카드는 부분 동작(포팅된 분기만)하고 컴파일/테스트는 통과. 강모델이 나중에 STOP 분기를 채운다.

## 5. STOP — 강모델 에스컬레이션 조건
아래 중 하나면 **직접 해결하지 말고** 카드 id + 이유를 기록하고 넘어간다:
- 원본이 부르는 팩토리가 **카탈로그에 없음**.
- 원본에 있는 **`EffectTiming`이 헤드리스에 없음**(컴파일 에러).
- 원본이 카드 내부 **nested 클래스/커스텀 로직**(단순 팩토리 호출이 아님)을 씀.
- 특수플레이(DigiXros/DNA/Blast 등) **레시피 데이터**가 필요함.

기록 형식(한 줄): `<ID> | 이유 | 원본이 부른 심볼`

---

## 6. 검증된 예시 — ST7_10
원본이 `ChangeSelfSAttackStaticEffect`(None 타이밍) + `PierceSelfEffect`(OnDetermineDoSecurityCheck)를 부름 → 그대로 미러:
```csharp
public sealed class ST7_10 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();
        if (timing == EffectTiming.None)
            cardEffects.Add(CardEffectFactory.ChangeSelfSAttackStaticEffect(changeValue: 1, isInheritedEffect: false, card: card, condition: null));
        if (timing == EffectTiming.OnDetermineDoSecurityCheck)
            cardEffects.Add(CardEffectFactory.PierceSelfEffect(isInheritedEffect: false, card: card, condition: null));
        return cardEffects;
    }
}
```
테스트: 등록 후 `ResolveSecurityAttack(context, card, 1) == 2`, `GetKeywordEffects("Piercing").Count >= 1`.

---

## 그룹 반복 (세트/색상 배치)
여러 장을 돌릴 때:
1. 대상 목록 확정: `DCGO/.../CardEffect/<SET>/<COLOR>/*.cs` 파일명 = 카드 id.
2. **한 장씩** §절차 1~5 반복. 각 장 green 후 다음(배치로 몰아서 하지 않는다 — 실패 격리).
3. STOP된 카드는 목록에 모아 강모델에 넘긴다.
4. 세트 끝나면 `bash scripts/run-tests.sh` 전체 green + 진행 기록.
