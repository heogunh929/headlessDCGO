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
| `[All Turns]` 스탯/키워드 (timing==None) | 해당 `*SelfStaticEffect` / `*SelfEffect` (연속형, 미러 OK) |
| 대체 진화원 | `AddSelfDigivolutionRequirementStaticEffect(permanentCondition, ...)` |

> **판정법**: 효과가 `timing == None`의 **연속/키워드/스탯 팩토리**면 **구문 미러 OK**(카탈로그에서 찾아 그대로). 효과가 **코루틴(`IEnumerator`/`.Draw()/.Tap()/.Destroy()`)**이면 → **의도를 읽어 대응 팩토리가 카탈로그에 있으면 사용, 없으면 STOP**(강모델). 코루틴을 억지로 옮기지 말 것.

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
