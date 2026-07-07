# Uniform Activated Primitive — design (mirror the AS-IS `ActivateClass`)

작성 2026-07-07. 근거: [primitive_gap_inventory.txt](primitive_gap_inventory.txt), 메모리 `asis-uniform-activateclass`.

## 1. 문제 — 파편화가 조합 폭발을 낳는다

AS-IS는 **모든 효과가 하나의 `ActivateClass`**:

```
SetUpICardEffect(name, CanUseCondition, card)                       // CanUse = 타이밍 게이트
SetUpActivateClass(CanActivateCondition, ActivateCoroutine,          // 전제조건 + 효과 본체
                   order, isOptional, description)                   // order = once-per-turn
```

= `(타이밍게이트, 전제조건, 효과본체, order, isOptional)`. 타이밍(OnPlay/OnAttack/Main…)은 CanUse 게이트로만
구분되고 **구조는 동일**. draw가 [On Play]든 [When Attacking]든 [Main]이든 같은 ActivateClass다.

헤드리스는 이걸 **(액션 × 타이밍)별 팩토리**로 쪼갬 → `DrawCardsEffect`(activated)는 있고 draw-트리거는 없음;
`AddMemoryTriggerEffect` + `GainMemoryActivatedEffect`는 같은 것 2벌. 매 조합마다 새 팩토리 = **끝없는 갭**.
실측: 1037장에서 29 갭 shape, 갭 675 card-hits 중 507(75%)이 trigger-class(= activated 액션을 트리거 타이밍에서).

## 2. 목표 — `ActivateClass`를 1:1 미러한 uniform 프리미티브

### 2.1 핵심 타입

```csharp
// 하나의 uniform 효과 = AS-IS ActivateClass. IActivatedCardEffect로 리졸버 경로에 태운다.
public sealed class ActivatedEffect : IActivatedCardEffect
{
    EffectTiming Timing;                              // 어느 타이밍 블록
    Func<CardEffectResolveContext, bool>? CanUse;     // 타이밍 게이트 (CanTriggerOnPlay/OnAttack/…)
    Func<bool>? CanActivate;                          // 전제조건 (라이브 상태 읽기)
    IEffectBody Body;                                 // 합성 가능한 효과 본체 (아래)
    int? MaxCountPerTurn;                             // order → once-per-turn
    bool IsOptional;                                  // "you may" 프롬프트
    string Description;
}

// 효과 본체 — AS-IS ActivateCoroutine에 해당. 비대화형은 Apply, 대화형은 BuildRequest+Apply.
public interface IEffectBody
{
    bool IsInteractive { get; }
    ChoiceRequest? BuildRequest(CardSource card, IEnumerable<HeadlessPlayerId> players);   // 대화형만
    void Apply(CardSource card, MatchStateMutationSink sink, IReadOnlyList<HeadlessEntityId> selected);
}
```

### 2.2 합성 본체 세트 (~15종 = 전체 액션 공간)

| body | 대응 액션 | 대화형 |
|---|---|---|
| `DrawBody(count)` | draw | 아니오 |
| `MemoryBody(amount)` | memory ± | 아니오 |
| `RecoveryBody(amount)` | recovery | 아니오 |
| `SelfDpBody(value, duration)` | 자기 DP | 아니오 |
| `SelfKeywordBody(keyword, duration)` | 자기 키워드 | 아니오 |
| `SelectBody(mode, canTarget, maxCount, canEndNotMax)` | select+destroy/suspend/unsuspend/bounce/hand/discard/custom | 예 |
| `SelectBuffBody(canTarget, maxCount, stat, value, duration)` | select+DP/SA 버프 | 예 |
| `SelectTrashDigivolutionBody(canTarget, maxCount, count, fromBottom)` | 진화원 트래시 | 예 |
| `SelectPlayBody(fromZone, canTarget, maxCount)` | 존에서 플레이 | 예 |
| `RevealSelectBody(count, condition, dest)` | 덱top 공개+선택 | 예 |
| `SelfToHandBody()` | 이 카드 핸드로 | 아니오 |
| `RestrictionBody(scope, kind, duration, predicate)` | CanNot… 부여 | 아니오 |
| `CostChangeBody(target, delta, duration)` | 코스트 증감 | 아니오 |
| `PlayerScopeBuffBody(stat, value, duration, scope)` | 플레이어스코프 버프 | 아니오 |
| `ModeChoiceBody(modes[])` | 모드 선택 메뉴 | 예 |

**1 래퍼(`ActivatedEffect`) × ~15 본체 = 전체 프리미티브.** (액션 × 타이밍) 조합 폭발이 사라진다 —
타이밍은 `Timing`+`CanUse`로, order는 `MaxCountPerTurn`으로 파라미터화되므로 새 타이밍/조합에 새 타입이 불필요.

## 3. 해소 통일 — 모든 타이밍을 리졸버 하나로

현재 2경로(스케줄러 등록트리거 vs 리졸버 activated)를 **리졸버 하나로 수렴**. `ActivatedEffect`는
IActivatedCardEffect이므로 `ActivatedEffectResolver`가 처리하되, **게이트/전제/once를 리졸버가 확인**하도록 보강:

```
resolve ActivatedEffect e:
   if e.CanUse != null      && !e.CanUse(ctx):       skip     // 타이밍/subject 게이트 (self-scope)
   if e.CanActivate != null && !e.CanActivate():      skip     // 전제조건
   if e.MaxCountPerTurn set  && !OnceFlags.TryActivate: skip    // once-per-turn (게이트 통과 후에만 소모)
   if e.Body.IsInteractive:  choice = Choose(Body.BuildRequest(...)); if skipped return; Body.Apply(card,sink,choice)
   else:                     Body.Apply(card, sink, [])
```

타이밍별 진입:
- **트리거**(OnAllyAttack/OnDeletion/OnBlockAnyone/…): 브릿지가 리졸버로 라우팅. **브릿지 allow-list를 전 트리거
  타이밍으로 확장**(현재 4종 → 전체). subject-scope는 브릿지가 이벤트 subject의 효과만 돌려 자연 확보.
- **activated**(OptionSkill/SecuritySkill): 기존 action-wiring → 리졸버.
- **onplay**(OnEnterFieldAnyone): PlayCardAction → 리졸버(이번 세션 배선 완료).
- **digivolve**(WhenDigivolving): DigivolveAction → 리졸버.

비대화형 본체(memory/dp)도 리졸버가 처리하므로 IHeadlessCardEffect 등록트리거 경로는 점진 폐기 가능
(당장은 공존; 등록트리거는 continuous/inert 판정용으로 잔존).

## 4. 마이그레이션 — per-shape 팩토리는 얇은 래퍼로

기존 팩토리는 **삭제하지 않고** `ActivatedEffect` 생성 래퍼로 재구현(하위호환·기존 테스트 유지):

```csharp
public static ICardEffect DrawCardsEffect(CardSource card, int count) =>
    new ActivatedEffect(card, EffectTiming.None, canUse: null, canActivate: null,
                        body: new DrawBody(count), maxCountPerTurn: null, isOptional: false, "Draw " + count);

public static ICardEffect AddMemoryTriggerEffect(EffectTiming timing, int amount, …, Func<ctx,bool>? triggerGate=null, int? maxCountPerTurn=null) =>
    new ActivatedEffect(card, timing, canUse: triggerGate, canActivate: condition,
                        body: new MemoryBody(amount), maxCountPerTurn, isOptional: amount>0, description);
```

포팅 카드는 **팩토리를 계속 쓰거나**, 직접 `ActivatedEffect(...)`로 임의 (타이밍×게이트×본체×order) 조합을 표현
가능 — draw@trigger, select_destroy@OnDeletion 등 지금 갭인 것들이 **새 타입 없이** 즉시 표현된다.

## 5. 단계

- **Phase A** — `IEffectBody` + ~15 본체 + `ActivatedEffect` 신설. 리졸버에 게이트/전제/once 확인 추가 + `ActivatedEffect` case. 단위테스트(본체별 Apply, 게이트 skip).
- **Phase B** — 브릿지 allow-list 전 트리거 타이밍 확장. 라이브 트리거 테스트(draw@OnDeletion 등 신규 갭 shape가 발동). 전체 스위트 회귀.
- **Phase C** — 기존 per-shape 팩토리를 ActivatedEffect 래퍼로 재구현(하위호환 확인). primitive_gap_inventory 재측정 → 갭 수렴 확인.
- **Phase D** — BT1 포팅 재개, 이번엔 갭 0로 흐르는지 실측.

## 6. 무엇이 붕괴하나

- trigger-class 갭 507 card-hits → 브릿지 확장 + uniform 본체로 대부분 흡수.
- onplay/activated 갭 141 hits → 이미 리졸버 경로 + 본체로 흡수.
- 남는 진짜 작업 = **~15 본체 구현**(대부분 기존 activated 효과에서 추출) + 브릿지 확장 + 리졸버 게이트. 유한.

**주의(fidelity)**: 본체는 AS-IS coroutine을 1:1 미러(뭉개기 금지). CanUse/CanActivate 게이트는 AS-IS
CanUseCondition/CanActivateCondition을 그대로 옮긴다([[check-asis-before-implementing]]). 정말 없는 것(§4 메모리값·
키워드보유 질의)은 여전히 STOP/문서화.
