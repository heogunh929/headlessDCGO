# 카드 포팅 정본 지침서 (단일 양성) — 2026-07-23

> **이 문서의 지위**: 대량 카드 포팅(Haiku 파일럿)의 **유일한 양성 정본**이다. Haiku 시스템 프롬프트에
> 주입되는 문서이며, 약한 모델이 카드 한 장을 처음부터 끝까지 "따라 쓰는" 대체물이다.
> 직전 감사에서 은퇴한 구 지침 2종(`docs/audit/card_porting_recipe.md`,
> `docs/porting/porting_translation_cheatsheet.md` — 둘 다 SUPERSEDED, 레지스트리-시대 아키텍처를 가르침)을
> **대체**한다. 그 두 문서는 절대 참조하지 말 것.
>
> **모든 시그니처·경로·심볼은 HEAD `5e314380` 실소스에서 발췌·검증했다**(2026-07-23, 문서·기억 불신 원칙).
>
> **연결 문서(정본, 함께 읽을 것)**:
> - `docs/audit/card_porting_standard.md` — 상위 원칙(구조 동일). **원칙=정본**(§0 상태 배너 참조).
> - `docs/audit/asis_tobe_primitive_mapping.md` — **R4 정본 idiom 색인**(축별 정본 예제 카드), R3 스테일-치환표.
>   프리미티브 축을 어느 실카드가 커버하는지 찾을 때 이 문서의 R4.1~R4.3 표를 쓴다.
> - `docs/porting/symbol_map_guide.md` + `docs/porting/symbol_map.csv` — **AS-IS 심볼 → 미러 심볼 lookup 표**.
>   심볼이 "없다"고 STOP 하기 전 **반드시** 이 표를 조회(grep-검증된 미러 surface).
> - `docs/audit/coverage_exemplar_audit_2026-07-18.md` §3·§4 — 커버 카드 행렬·greedy set-cover.
> - `docs/audit/freeze_evidence_2026-07-23.md` §9 — 소프트 동결 규약: **카드 포팅=additive 자유, 코어 수정 금지**.
> - `tests/EXEMPLAR-T1~T3B.Witness.Tests/` — **테스트 정본 템플릿**(복사 대상).

---

## 0. 한 장을 포팅하는 절차 (요약 — 상세는 각 §)

1. **AS-IS 원본을 연다**: `DCGO/Assets/Scripts/CardEffect/<SET>/<Color>/<ID>.cs`. 룰텍스트는
   `DCGO/.../DataBase.cs`(또는 `cards.json`). 카드가 *무엇을* 하는지 정확히 읽는다. **추측 금지.**
2. **미러 파일을 연다**: `src/HeadlessDCGO.Engine/Assets/Scripts/CardEffect/<SET>/<Color>/<ID>.cs`
   (동일 경로. 대개 빈 셸 또는 STOP-스텁이 이미 존재). §1 스켈레톤을 채운다.
3. **AS-IS의 각 효과(region)를 1:1로 옮긴다**: 팩토리 호출·`ActivateClass`·술어를 **이름·인자 순서 그대로**.
   심볼이 미러에 있는지 `symbol_map.csv` → grep으로 확인(§7). 있으면 쓴다.
4. **substrate 번역만 적용**(§9): `IEnumerator`→`async Task`, `GManager`/`Player`/`PermanentOfThisCard` 브릿지.
   **그 외 어떤 로직 변경도 금지**(§6 단순화 철칙).
5. **진짜 AS-IS 갭에만 STOP**(§8): 발명·단순화·우회 절대 금지. `throw new NotSupportedException("design item …")`.
6. **행동 witness 3종을 쓴다**(§10): `tests/EXEMPLAR-T1` 템플릿 복사. 펌프-드리븐 매치로 효과가 실발화함을 측정.

---

## 1. 카드 파일 정본 스켈레톤

### 1.1 두 레이어 (미러 대상)

| 레이어 | 원본 (DCGO/) | 미러 (src/) | 미러 수준 |
|---|---|---|---|
| **카드** | `Assets/Scripts/CardEffect/<Set>/<Color>/<Id>.cs` | **동일 경로** `src/HeadlessDCGO.Engine/Assets/Scripts/CardEffect/<Set>/<Color>/<Id>.cs` | **구조+행동 1:1** |
| **엔진/프리미티브** | `Assets/Scripts/Script/...` (`CardEffectFactory`, `CardEffectCommons`, `KeyWordEffects/*` 등) | `src/HeadlessDCGO.Engine/Assets/Scripts/Script/...` (동일 파일 레이아웃) | **파일·이름 1:1 / 런타임만 번역** |

> **경로 규약 (실측)**: 카드는 `src/HeadlessDCGO.Engine/Assets/Scripts/CardEffect/<SET>/<Color>/<ID>.cs`.
> `<Color>` = `Black/Blue/Green/Purple/Red/White/Yellow` 중 하나(디렉터리로 실재). 예:
> `.../CardEffect/BT6/Black/BT6_106.cs`, `.../CardEffect/EX9/White/EX9_074.cs`, `.../CardEffect/BT25/Green/BT25_004.cs`.
> **주의**: 경로에 `Script/`가 **들어가지 않는다**(`Script/`는 엔진 레이어 전용). 카드는 `Scripts/CardEffect/…`.

### 1.2 빈 스켈레톤 (이 뼈대에서 시작)

```csharp
// Source: DCGO/Assets/Scripts/CardEffect/<SET>/<Color>/<ID>.cs (<N> lines) — TRUE AS-IS 1:1 re-port.
//   <효과 1줄 요약 per region: [Main]/[On Play]/[Security] …>
// ② 프리미티브 매핑: <각 region이 쓰는 팩토리/kind-class 이름 + AS-IS 라인 앵커>
// ③ 배선 관례 근거: <타이밍 키 선택 근거 — trigger-wiring-porting-rules 인용>
// 치환(substrate translations only): <IEnumerator→Task, Player/PermanentOfThisCard/GManager 브릿지 등>
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.<SET>.<Color>;

using System.Collections;                 // Hashtable
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;   // CardEffectCommons, ActivateClass helpers
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;         // ActivateClass, DestroyPermanentsClass, kind-classes
using HeadlessDCGO.Engine.Headless.Services;                         // GManager, SelectCardEffect 등 substrate

public sealed class <ID> : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // region 마다: if (timing == EffectTiming.<창>) { … cardEffects.Add(effect); }

        return cardEffects;
    }
}
```

- **베이스 클래스는 항상 `CEntity_Effect`**(정의: `Script/CardEffectInterfaces.cs:671`,
  `public abstract class CEntity_Effect`, `public virtual List<ICardEffect> CardEffects(EffectTiming, CardSource)`).
- 클래스 이름 = 파일 이름 = 카드 ID. `public sealed class <ID> : CEntity_Effect`.
- **반환형은 `List<ICardEffect>`**. 카드는 효과를 **만들어 리스트에 담아 반환**할 뿐이다 —
  등록/소비/만료는 전부 substrate가 처리한다(카드가 등록하지 않는다).

### 1.3 헤더 주석 관례 (강제 — 실카드 3장 모두 이 형식)

모든 미러 카드 파일 상단에 아래 3~4블록을 단다(실카드 `BT6_106`·`EX9_074`·`BT25_004` 관례):

1. **`// Source:`** — AS-IS 파일 경로 + 줄 수 + `— TRUE AS-IS 1:1 re-port.` / region별 효과 1줄 요약.
2. **`// ② 프리미티브 매핑:`** — 각 region이 호출하는 팩토리/kind-class 이름 + **AS-IS 라인 앵커**(`AS-IS :57-63`).
3. **`// ③ 배선 관례 근거:`** — 타이밍 키를 왜 그렇게 골랐는지(§2.3 trigger-wiring 인용).
4. **`// 치환(substrate translations only):`** — 적용한 substrate 번역 쌍만 열거(§9). **로직 변경은 여기 없어야 정상.**

> 이 주석은 장식이 아니다. 다음 감사자가 "이 카드가 AS-IS와 1:1인지"를 **소스만 보고** 판정하는 근거다.
> AS-IS 라인 앵커가 없으면 리뷰에서 반려된다.

---

## 2. 실전 정본 예제 3장 (실소스 발췌 + 부위 해설)

### 2.1 예제 A — `BT25_004` (단순: 그랜트 1개, `ActivateClass` + 팩토리)

`src/HeadlessDCGO.Engine/Assets/Scripts/CardEffect/BT25/Green/BT25_004.cs` (전문):

```csharp
public sealed class BT25_004 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Shared Conditions
        bool CardCondition(CardSource cardSource)
        {
            return cardSource.EqualsTraits("Social") || cardSource.EqualsTraits("Tool") || cardSource.EqualsTraits("Game");
        }
        bool PermanentCondition(Permanent permanent) => permanent == ICardEffect.ResolvePermanentOfThisCard(card);
        bool RootCondition(SelectCardEffect.Root root) => true;
        #endregion

        #region Reduce Link Cost
        if (timing == EffectTiming.WhenWouldLink)
        {
            ActivateClass activateClass = new();
            activateClass.SetUpICardEffect("May reduce Link cost by 1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, true, EffectDiscription());
            activateClass.SetIsInheritedEffect(true);
            activateClass.SetHashString("BT25_004_YT");
            cardEffects.Add(activateClass);

            string EffectDiscription() =>
                "[Your Turn] [Once Per Turn] When a [Social], [Tool] or [Game] trait card would link to this Digimon, you may reduce the cost by 1.";

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.CanTriggerWhenWouldLink(hashtable, CardCondition, PermanentCondition)
                    && CardEffectCommons.IsOwnerTurn(card);
            }

            bool CanActivateCondition(Hashtable hashtable) => CardEffectCommons.IsExistOnBattleArea(card);

            Task ActivateCoroutine(Hashtable hashtable)
            {
                new Player(card.Context, card.Owner).UntilCalculateFixedCostEffect.Add((_) =>
                    CardEffectFactory.GrantedReduceLinkCostClass(
                        card: card, reducedCost: 1,
                        cardSourceCondition: CardCondition, permanentCondition: PermanentCondition,
                        rootCondition: RootCondition));
                return Task.CompletedTask;
            }
        }
        #endregion

        return cardEffects;
    }
}
```

**부위 해설**:
- `#region Shared Conditions` — region 간 공유하는 술어는 메서드 위쪽에 로컬 함수로 둔다. **술어는 `Func<Permanent,bool>` /
  `Func<CardSource,bool>` 로 도메인 객체를 직독**(§4).
- `PermanentCondition ... == ICardEffect.ResolvePermanentOfThisCard(card)` — AS-IS `permanent == card.PermanentOfThisCard()`의
  substrate 번역(§9). 값-동등 비교라 `Resolve…`가 필요.
- `if (timing == EffectTiming.WhenWouldLink)` — AS-IS 타이밍 키 그대로(§2.3).
- `new ActivateClass()` + `SetUpICardEffect` + `SetUpActivateClass` — uniform 진입점(§3).
- `SetIsInheritedEffect(true)`(진화원 상속) · `SetHashString("BT25_004_YT")`(once-per-turn 키) — AS-IS `SetIsInheritedEffect`/`HashString` 미러.
- `ActivateCoroutine`가 `await` 없이 끝나면 **`Task` 반환 non-async** + `return Task.CompletedTask`(AS-IS `yield return null` 번역).

### 2.2 예제 B — `BT6_106` (중간: `ActivateClass` + `DestroyPermanentsClass`, 평가되는 술어)

`src/HeadlessDCGO.Engine/Assets/Scripts/CardEffect/BT6/Black/BT6_106.cs` (핵심 발췌):

```csharp
if (timing == EffectTiming.OptionSkill)
{
    ActivateClass activateClass = new ActivateClass();
    activateClass.SetUpICardEffect(card.BaseENGCardNameFromEntity, CanUseCondition, card);
    activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
    cardEffects.Add(activateClass);

    string EffectDiscription() => "[Main] Delete all of your opponent's Digimon with the highest play cost.";

    bool PermanentCondition(Permanent permanent)
    {
        if (CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card))
            if (CardEffectCommons.IsMaxCost(permanent, CardEffectCommons.OpponentOf(card), true))
                return true;
        return false;
    }

    bool CanUseCondition(Hashtable hashtable) => CardEffectCommons.CanTriggerOptionMainEffect(hashtable, card);

    async Task ActivateCoroutine(Hashtable _hashtable)
    {
        List<Permanent> destroyTargetPermanents =
            new Player(card.Context, card.Owner).Enemy!.GetBattleAreaDigimons().Filter(PermanentCondition);
        await new DestroyPermanentsClass(destroyTargetPermanents, CardEffectCommons.CardEffectHashtable(activateClass)).Destroy();
    }
}

if (timing == EffectTiming.SecuritySkill)
{
    CardEffectCommons.AddActivateMainOptionSecurityEffect(
        card: card, cardEffects: ref cardEffects,
        effectName: $"Delete opponent's all Digimon with the highest play cost");
}
```

**부위 해설**:
- **`SetUpActivateClass(canActivate, coroutine, maxCountPerTurn, isOptional, description)`** — 5인자(§3.1). 여기서 `canActivate=null`(무조건),
  `maxCountPerTurn=-1`(무제한), `isOptional=false`(강제).
- **술어는 평가된다, 뭉개지 않는다**: `PermanentCondition`이 `IsPermanentExistsOnOpponentBattleAreaDigimon` 가드 + `IsMaxCost` 술어를
  **그대로 유지**. AS-IS가 술어를 평가하면 미러도 평가(§6). `.Filter(PermanentCondition)`로 대상 산출.
- `new Player(card.Context, card.Owner).Enemy!` — AS-IS `card.Owner.Enemy`의 substrate 번역(§9).
- `[Main]` 효과를 `[Security]`에서도 재사용: `CardEffectCommons.AddActivateMainOptionSecurityEffect(..., ref cardEffects, ...)` —
  `ref cardEffects`로 리스트에 파생 효과를 밀어넣는 정본 관용.

### 2.3 예제 C — `EX9_074` (복합: 6 region, Select* 브릿지, AS-IS quirk 보존)

`src/HeadlessDCGO.Engine/Assets/Scripts/CardEffect/EX9/White/EX9_074.cs` (구조만 발췌 — 전문은 실파일 참조):

- **6개 region**을 `#region … #endregion`으로 구획, 각기 `if (timing == …)` 진입:
  Assembly(`None`) / Sec+1(`None`) / Rush(`None`) / On Play(`OnEnterFieldAnyone`) / When Digivolving(`OnEnterFieldAnyone`) / All Turns(`None`).
- **연속·정적 효과는 `EffectTiming.None` region**에서 팩토리를 `Add`: 예
  `cardEffects.Add(CardEffectFactory.ChangeSelfSAttackStaticEffect(changeValue: 1, isInheritedEffect: false, card: card, condition: null));`
  `cardEffects.Add(CardEffactFactory.RushSelfStaticEffect(isInheritedEffect: false, card: card, condition: null));`
- **인터랙티브 선택은 GManager 브릿지**(§9):
  ```csharp
  SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();
  selectPermanentEffect.SetUp(selectPlayer: card.Owner, canTargetCondition: id => …, /* AS-IS SetUp 인자 전량 */,
      mode: SelectPermanentEffect.Mode.Destroy, cardEffect: activateClass);
  await selectPermanentEffect.Activate();
  ```
- **AS-IS 술어를 엔티티-id 술어로 재구성**(전역-스캔 헬퍼용): 로컬 `Permanent? PermanentOf(HeadlessEntityId id) => …`를 두고
  `id => PermanentOf(id) is { } p && CanSelectPermanentCondition(p, colours)` 형태로 넘긴다(BT9_062 idiom).
- **AS-IS quirk는 verbatim 보존**: EX9_074는 6+색 분기에서 **fresh 빈 `_hashtable`을 `DestroyPermanentsClass`에 넘기고
  "CardEffect"는 원래 `hashtable`에 넣는** 버그성 quirk가 있다. **고치지 않는다** — 주석으로 명시하고 그대로 옮긴다:
  ```csharp
  // AS-IS quirk kept verbatim (EX9_074.cs:494-497)
  Hashtable _hashtable = new Hashtable();
  hashtable.Add("CardEffect", activateClass);
  await new DestroyPermanentsClass(permanentToDelete, _hashtable).Destroy();
  ```

> **교훈**: 복합 카드도 규칙은 같다 — region별 timing 게이트, 팩토리/kind-class 호출, 술어 직독, substrate 번역만.
> AS-IS가 이상해 보여도 **그대로** 옮긴다. "개선"은 fidelity 위반이다.

---

## 3. uniform ActivateClass 체계 (정본 진입점)

전 타이밍-클래스(OnPlay/OnDeletion/Counter/When\*/StartOf\*/EndOf\*/YourTurn/Security 등 19종)의 **단일 정본 진입점**은
`new ActivateClass()`다. AS-IS가 `OnPlayClass`·`CounterClass` 같은 per-timing 팩토리를 쓰더라도, 미러는 **uniform `ActivateClass` +
timing 게이트(`CanTrigger*`)로 카드별 인라인**하는 것이 **정본으로 확정**됐다(mapping R2; 창엔진 컷오버 후). 이는 갭이 아니다.

### 3.1 시그니처 (실측 — `Script/CardEffects/ActivateClass.cs`, `Script/ICardEffect.cs`)

```csharp
// (1) 소스 카드·표시 이름·창 게이트 결선 (ICardEffect.cs:55)
void SetUpICardEffect(string effectName, Func<Hashtable, bool> canUseCondition, CardSource card);

// (2) 본체 결선 (ActivateClass.cs:33)
void SetUpActivateClass(Func<Hashtable, bool> canActivateCondition,   // 발동 가능 게이트(없으면 null)
                        Func<Hashtable, Task> activateCoroutine,      // 효과 본체(AS-IS coroutine → async Task)
                        int maxCountPerTurn,                          // once-per-turn=1, 무제한=-1
                        bool isOptional,                              // "you may…"=true, 강제=false
                        string effectDiscription);                    // 룰텍스트(AS-IS 원문 그대로)

// (3) 선택적 플래그 (ICardEffect.cs)
void SetIsInheritedEffect(bool);   // :634  진화원 상속 효과면 true
void SetHashString(string);        // :260  once-per-turn/공유-hash 키
void SetNotShowUI(bool);           // :796  UI 비표시(내부 조건 등재용)
```

- **`CanUseCondition(Hashtable hashtable)` 게이트**: 이 창(timing)에서 효과가 발화할지 판정. 본문은 거의 항상
  `CardEffectCommons.CanTrigger<창>(hashtable, …)` + `CardEffectCommons.IsExistOnBattleArea(card)` +
  (해당 시) `IsOwnerTurn(card)` 조합. `Hashtable`은 창이 실어 보내는 이벤트 컨텍스트다(substrate가 채움).
- **`ActivateCoroutine(Hashtable)`**: 효과 본체. `await`가 있으면 `async Task`, 없으면 `Task` + `return Task.CompletedTask`.

### 3.2 창-타이밍 명명 (실측 `EffectTiming` enum + `CanTrigger*` 헬퍼)

`EffectTiming` 값(실존): `None`, `OnEnterFieldAnyone`, `OptionSkill`, `SecuritySkill`, `OnEndTurn`, `OnStartTurn`,
`OnStartMainPhase`, `WhenDigivolving`, `OnDeclaration`, `WhenWouldLink`, `WhenLinked`, `OnAllyAttack`, `OnEndBattle`,
`OnEndAttack`, `OnDestroyedAnyone` 등.

**trigger-wiring 관례**(실카드·`CardEffectCommons/CanUseEffects/*` 확인):
- `[On Play]` → `timing == EffectTiming.OnEnterFieldAnyone` + `CardEffectCommons.CanTriggerOnPlay(hashtable, card)`.
- `[When Digivolving]` → **두-방언 이음새 주의 (2026-07-23 파일럿에서 실측 — 신규 포팅 규칙은 아래 한 줄)**:
  AS-IS 원문은 On Play와 같은 `OnEnterFieldAnyone` 창을 공유하고 `CanTriggerWhenDigivolving`로 구분한다.
  미러 실행기는 진화 플레이 시 **두 창(OnEnterFieldAnyone + WhenDigivolving)을 같은 hashtable로 모두 연다**
  (`CardController.cs:4243-4297`, DISPATCH-REMAP BRIDGE — 코퍼스 실측: 전용-키 54파일 / AS-IS-리터럴 20파일 병존, 둘 다 발화).
  단 **같은 효과를 두 키에 이중 등재하면 실행기가 STOP**한다(double-key 가드, 리뷰3 P2-②).
  **신규 포팅 규칙: `timing == EffectTiming.WhenDigivolving` + `CanTriggerWhenDigivolving(hashtable, card)` 전용 키를 쓴다**
  (코퍼스 다수 방언·실행기 :4293 보증 경로). AS-IS-리터럴 20파일의 단일-키 재수렴은 코드 주석에 명기된
  이연 캠페인(P6A-HT-ENTERFIELD 완성 후)이며 신규 카드가 선택할 사안이 아니다.
- `[Main]`(옵션) → `EffectTiming.OptionSkill` + `CanTriggerOptionMainEffect(hashtable, card)`.
- `[Security]` → `EffectTiming.SecuritySkill` + `CanTriggerSecurityEffect(…)` 또는 `AddActivateMainOptionSecurityEffect`로 [Main] 파생.
- `[Your Turn]` 링크 감소 → `EffectTiming.WhenWouldLink` + `CanTriggerWhenWouldLink(hashtable, cardCondition, permanentCondition)`.
- 연속·정적(스탯 변경·키워드 grant 등) → `EffectTiming.None` region에서 팩토리를 그냥 `Add`.

**`CanTrigger*` 헬퍼 census**(51종, `CardEffectCommons/CanUseEffects/` 및 `CanUseEffectHelpers.cs`): `CanTriggerOnPlay`,
`CanTriggerWhenDigivolving`, `CanTriggerOnDeletion`, `CanTriggerOnAttack`, `CanTriggerOnEndAttack`, `CanTriggerOnMove`,
`CanTriggerWhenLinked`, `CanTriggerWhenLinking`, `CanTriggerWhenWouldLink`, `CanTriggerOptionMainEffect`,
`CanTriggerSecurityEffect`, `CanTriggerOnTrash{Hand,Security,LinkCard,SelfDigivolutionCard,…}`,
`CanTriggerWhen{AddHand,AddSecurity,LoseSecurity,DeleteOpponentDigimon,OwnerUseOption,DiscardLibrary}`, `CanTriggerPierce`,
`CanTriggerEvade`, `CanTriggerAscension`, `CanTriggerFortitude`, `CanTriggerPartition` … (전량은 `grep -rhoE "public static bool CanTrigger[A-Za-z]+"
src/.../CardEffectCommons/`로 조회). **AS-IS가 쓰는 이름을 그대로** 찾아 쓴다.

> ⚠️ **타이밍 키를 임의 확장하지 말 것**: "모든 Anyone 타이밍" 같은 광범위 확장은 회귀를 낸다
> (card_porting_standard §3). AS-IS가 지정한 정확한 창만 쓴다.

---

## 4. 술어 규약 (정본 vs 금지)

- **정본**: 술어는 `Func<Permanent, bool>` 또는 `Func<CardSource, bool>`로 **도메인 객체를 직접 읽는다**.
  예: `bool Cond(Permanent p) => p.IsSuspended && p.Level >= 5;` / `bool Cond(CardSource c) => c.IsDigimon && c.HasDMTraits;`
- **금지**: 술어를 id-형 commons 라우팅으로 낮추기(구 레지스트리-시대 관용) — **하지 말 것**. Permanent/CardSource 도메인을 직독한다.
- **예외(허용되는 유일한 재구성)**: 전역-스캔 헬퍼(`HasMatchConditionOpponentsPermanent`,
  `MatchConditionPermanentCount` 등)가 `Func<HeadlessEntityId,bool>`을 받을 때만, 로컬
  `Permanent? PermanentOf(HeadlessEntityId id)`를 두고 `id => PermanentOf(id) is { } p && Cond(p)`로 감싼다(EX9_074 §2.3).
  이는 substrate 번역이지 술어 뭉갬이 아니다 — **술어 `Cond(p)`는 그대로 평가된다**.
- **철칙**(fidelity-over-coverage): 술어를 받는 팩토리는 **술어를 평가해야** 한다. `null`로 넘기거나 하드코딩·평면화하면 **FAIL**.
  AS-IS가 넘기는 값을 그대로 넘겨라.

---

## 5. Permanent accessor 검증 목록 (실측 census — `Script/Permanent.cs`, `public sealed class Permanent` @46)

카드 술어가 실사용하는 `Permanent` public 프로퍼티/메서드(실존 확인·시그니처 포함). **이 목록에 있는 것만 쓴다.**
없는 걸 지어내면 컴파일 실패 또는 fidelity 위반.

| 멤버 | 시그니처 | 용도 |
|---|---|---|
| `TopCard` | `CardSource TopCard` (get) | 스택 최상단 카드 뷰. `p.TopCard.CardColors`, `p.TopCard.CanNotBeAffected(...)` 등 |
| `DigivolutionCards` | `IReadOnlyList<CardSource> DigivolutionCards` (@1855) | 진화원 카드들. `.Where(x => !x.IsFlipped)` 등 |
| `DigivolutionCardsColors` | `List<CardColor> DigivolutionCardsColors` | 진화원 색 집합 |
| `HasNoDigivolutionCards` | `bool HasNoDigivolutionCards` | 진화원 0장 여부 |
| `HasFaceDownDigivolutionCards` | `bool HasFaceDownDigivolutionCards` | 뒤집힌 진화원 존재 |
| `IsSuspended` | `bool IsSuspended` (@1649) | 서스펜드(탭) 상태 |
| `CanSuspend` | `bool CanSuspend` (@1824) | 서스펜드 가능 여부 |
| `Level` | `int Level` (@565) | 레벨 |
| `IsDigimon` / `IsTamer` | `bool IsDigimon` (@625) / `bool IsTamer` (@703) | 카드 타입 |
| `DP` / `BaseDP` / `LinkedDP` / `GetDP(...)` | `int DP` (@381) / `int BaseDP` (@2235) / `int LinkedDP` (@2371) / `int GetDP(Permanent ignorePermanent = null)` (@198) | DP 값들 |
| `HasDP` / `IsDpDefined` | `bool HasDP` (@142) / `bool IsDpDefined` (@186) | DP 정의 여부 |
| `OwnerId` | `HeadlessPlayerId OwnerId` (get) | 소유자 |
| `LinkedCards` / `HasNoLinkCards` | `List<CardSource> LinkedCards` (@2812) / `bool HasNoLinkCards` (@2837) | 링크 카드 |
| `CanSelectBySkill(...)` | `bool CanSelectBySkill(ICardEffect skill)` (@2923) | 스킬 대상 선택 가능(untargetability) |
| `ImmuneFromDPMinus(...)` | `bool ImmuneFromDPMinus(ICardEffect cardEffect)` (@2433) | DP-감소 면역 |
| `IsPlaceToTrashDueToNotHavingDP` | `bool` (@2846) | DP 0 → 트래시 |
| remove-field 스냅샷 | `DPJustBeforeRemoveField`(@1769) / `LevelJustBeforeRemoveField`(@1777) / `CostJustBeforeRemoveField`(@1785) / `CardNamesJustBeforeRemoveField`(@1793) / `CardTraitsJustBeforeRemoveField`(@1801) | 필드 이탈 직전 값(리액션 술어용) |
| played/digivolved 스냅샷 | `LevelJustAfterPlayed`(@4398) / `PlayCostJustAfterPlayed`(@4405) / `CardNamesJustAfterPlayed`(@4412) / `CardNamesJustAfterDigivolved`(@4419) / `TraitsJustAfterPlayed`(@4426) | 플레이/진화 직후 값 |

**census 요약**: `Permanent`(@46–4615)에 public 멤버 **129개**. 위 표는 카드 술어가 실제로 읽는 상용 부분집합.
특이사항: (1) `DigivolutionCards`가 파일 내 **두 번** 나타남 — @31은 별개 타입 `PermanentView`(@19)의 것(`IReadOnlyList<StackedCard>`),
카드가 쓰는 진짜 `Permanent`의 것은 @1855(`IReadOnlyList<CardSource>`). 혼동 주의. (2) 값-동등 비교
(`permanent == 이 카드의 permanent`)에는 `ICardEffect.ResolvePermanentOfThisCard(card)`를 쓴다(§9).

### 5.1 CardSource accessor (부수 census — `Script/CardSource.cs`; `Func<CardSource,bool>` 술어용)

| 멤버 | 시그니처 |
|---|---|
| `IsDigimon`/`IsTamer`/`IsOption` | `bool` (@1170/1171/1172) — `Definition.IsCardType(...)` |
| `Level` / `HasLevel` / `IsLevel(int)` | `int Level`(@1118) / `bool HasLevel`(@1188) / `bool IsLevel(int level)`(@1637) |
| `Level_Assembly` | `List<int> Level_Assembly` (@1145) — Assembly 레벨-치환 체인 read-side |
| `CardNames` / `CardNames_DigiXros` | `IReadOnlyList<string>` (@1055/1075) |
| `CardColors` | `IReadOnlyList<string>` (@262) — enum 필요 시 `CardSource.ToCardColorList(...)`로 폴드 |
| `EqualsTraits(string)` / `HasDMTraits` | `bool EqualsTraits(string trait)`(@1641, 대소문자 무시) / `bool HasDMTraits`(@1652) |
| `IsFlipped` | `bool` (@1180) |
| `Owner` | `HeadlessPlayerId Owner` (@48) |
| `BaseENGCardNameFromEntity` | `string` (@970) — 카드 영문명 |

---

## 6. 단순화 금지 철칙 (최우선 규칙)

- **어떤 이유로도 AS-IS 로직을 단순화하지 않는다.** 결과-등가여도 구조가 다르면 FAIL(mapping LENS1).
- **드물다고 누락 금지**: 빈도로 합리화하지 않는다. AS-IS의 가드·조건·임계값·분기를 **전부** 옮긴다.
- **호출부가 없다고 배선 생략 금지**(no-call-site-is-not-a-skip): 오버로드·분기·타이밍을 균일하게 배선.
- **AS-IS가 이상해 보여도 그대로**: quirk(EX9_074 §2.3의 빈 hashtable)도 verbatim 보존 + 주석.
- **엔진에 이미 메커니즘이 있는지 먼저 probe**(card_porting_standard §4): 대개 엔진은 완성돼 있고 카드-facing 팩토리만 호출하면 된다.
  없다고 지레 STOP 하기 전 `symbol_map.csv` + grep으로 미러 surface를 확인(약한 모델이 실재 surface를 "없다"고 오판하는 것이 최대 함정).

---

## 7. factory 시그니처 정본 소스 (PRIMITIVE-CATALOG 현행성 판정)

### 7.1 판정: `docs/porting/PRIMITIVE-CATALOG.md` = **스테일. 단독 정본으로 쓰지 말 것.**

- 체크인된 카탈로그는 **2026-07-07** 최종수정, "154종"을 주장 — 이는 **07-14 모놀리스 분해 이전**이다.
  현행 `CardEffectFactory`는 `CardEffectFactory.cs`(64 메서드) + `CardEffectFactory/*.cs` **partial-class 60파일**(106 메서드)로
  분산돼 **실측 ~170 메서드**다(둘 다 `namespace …CardEffectCommons; public partial class CardEffectFactory`).
- **재생성 스크립트도 깨져 있다**: `python3 scripts/generate-primitive-catalog.py`는 partial-class 디렉터리를 스캔하지 못해
  **"4 factories"만 검출**한다(regex가 구 모놀리스 전용). → 카탈로그는 재생성으로도 못 고친다. **카탈로그를 신뢰하지 말 것.**

### 7.2 정본 소스 = **실소스 grep** (아래 파일들의 현행 public 시그니처)

팩토리를 쓸 때는 **AS-IS 이름 그대로** 미러에서 찾는다(이름 동일이 원칙). lookup 순서:

1. `docs/porting/symbol_map.csv` — AS-IS 심볼 → 미러 심볼/경로/시그니처-델타 표(grep-검증). **1차 조회.**
2. 없거나 불확실하면 실소스 grep(정본):
   - `src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectFactory.cs` (64 메서드)
   - `src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectFactory/*.cs` (partial, 106 메서드)
   - `src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/*.cs` (키워드 grant)
   - `src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs` (253 public static) + `CardEffectCommons/*.cs`(하위 디렉터리 678 public static: `CanTrigger*`, `Match*`, `Play*`, `Trash*`, `DigivolveInto*`, `MinMax*` 등)
   - `src/HeadlessDCGO.Engine/Assets/Scripts/Script/PermanentEffectFactory.cs` (아래 §7.3)
   - grep 예: `grep -rn --binary-files=text "public static .* GrantedReduceLinkCostClass" src/.../CardEffectFactory*`

### 7.3 `PermanentEffectFactory` 현행 public 시그니처 (실측, 전량 — `Script/PermanentEffectFactory.cs`)

```csharp
public static class PermanentEffectFactory
{
    CanNotSwitchAttackTargetClass CanNotSwitchAttackTargetEffect( … );          // :22
    CanNotAffectedClass           DigimonEffectImmunity(CardEffectCommons.Permanent permanent);   // :53
    CanNotAffectedClass           OptionEffectImmunity(CardEffectCommons.Permanent permanent);    // :86
    ICardEffect                   CollisionEffect( … );                          // :121
    ActivateClass                 DeleteSelfEffect( … );                         // :153
    AddDetailClass                AddDetailClass( … );                           // :206
}
```

> **주의**: `PermanentEffectFactoryBinding`는 **삭제됨**(§11). Immunity/Collision/DeleteSelf/AddDetail은 전부
> 위 `PermanentEffectFactory` **본체 오버로드**로 live. 발명 string-key binding 폼을 찾으면 스테일 문서를 베낀 것이다.

---

## 8. STOP 규약 (진짜 AS-IS 갭 전용)

**갭 발견 시 발명·단순화·우회 절대 금지 → 정직 STOP 마커 내고 보고.**

- **문법**: `throw new NotSupportedException("design item RD-x-NN: <AS-IS 라인 근거 + 왜 충실 번역이 불가한지>");`
  - `design item RD-x-NN` 형식의 원장 id를 반드시 붙인다.
  - **소스에 리터럴 `TODO` 금지**(lint-guard가 잡는다). "design item RD-x-NN"만 쓴다.
- **정본 예(실소스 `CardEffectCommons/TrashLinkedCards.cs:72`, RD-SKEL-01)**: AS-IS 내부 비대칭(LinkedCards 풀 vs
  DigivolutionCards.Count 예산 불일치)이 충실 headless 번역 시 비종결 루프를 낳는 진짜 AS-IS 한계 → 얕은 뭉갬/발명 가드
  회피 위해 STOP-가드. **파일 상단 주석에 AS-IS 라인 증거**를 상세히 남기고 throw.
- **언제 STOP인가**: mapping R5.4의 인프라 갭(Assembly/DigiXros 인터랙티브 pre-play, Burst/AppFusion select 컴포넌트,
  Execute 발화, Ascension writer, AddSkillClass 중첩부여(nested-grant), Digisorption 진입, 고급 SelectCardCondition 술어,
  CanNotPutField 필드제약, 전역 digi-source 보호)를 **새 카드가 실호출**할 때. 이땐 **runtime throw가 아니라 정직 STOP 마커**로
  원장 등재 후 **Opus 프리미티브 선행 개발**을 기다린다(약한 모델은 프리미티브를 만들지 않는다 — 배선만).
- **STOP은 최후 수단**: §6대로 먼저 엔진 probe + symbol_map 조회. 실재하는 surface를 "없다"고 STOP 하는 것이 최대 오류.
- **카드 레이어의 live throw는 사실상 0**: 현재 `src/.../CardEffect/`에 실 `throw new NotSupportedException`은 없다(과거 STOP은
  전부 상환됨; 남은 `NotSupportedException` 문자열은 "구 STOP 주석 정정" 코멘트뿐). 새 STOP은 진짜 신규 갭일 때만.

---

## 9. substrate 번역 규칙 (허용되는 유일한 차이)

런타임 메커니즘만 번역한다. **로직은 verbatim.** 카드 헤더 `// 치환:` 블록에 적용한 쌍만 열거한다.

| AS-IS (Unity/coroutine) | 미러 (headless) | 근거/실카드 |
|---|---|---|
| `IEnumerator` 코루틴 본체 | `async Task` (또는 `Task`+`return Task.CompletedTask`) | ActivateClass.cs; 전 카드 |
| `yield return StartCoroutine(X)` / `ContinuousController.instance.StartCoroutine(X)` | `await X` | BT6_106, EX9_074 |
| `yield return null` (마지막 단독) | `return Task.CompletedTask` (non-async `Task` 메서드) | BT25_004 |
| `card.Owner` (Player 객체) | `new Player(card.Context, card.Owner)` | BT25_004, BT6_106 |
| `card.Owner.Enemy` | `new Player(card.Context, card.Owner).Enemy!` | BT6_106 |
| `card.PermanentOfThisCard()` (값/멤버 접근·값-동등) | `ICardEffect.ResolvePermanentOfThisCard(card)` (`internal static Permanent`, ICardEffect.cs:537) | BT25_004, EX9_074 |
| `GManager.instance.GetComponent<Select*Effect>()` + AS-IS `SetUp(...)` | 동일 호출 그대로(브릿지 W4) — `GManagerBridge`(Headless/Bridge/GManagerBridge.cs)가 뒤를 받음 | EX9_074 |
| 인터랙티브 선택 응답(사람/UI) | `ChoiceProvider` 좌석(테스트에선 `PolicyChoiceProvider`가 답) | EXEMPLAR 테스트 |
| `cardSource.IsLevel4` (프로퍼티) | `cardSource.IsLevel(4)` (메서드) | EX9_074 |
| `List<CardColor> CardColors` | `IReadOnlyList<string>` + `CardSource.ToCardColorList(...)`로 enum 폴드 | EX9_074 |
| `.Filter(pred)` (AS-IS List 확장) | 그대로 사용 가능(미러도 `.Filter` 확장 제공); IReadOnlyList엔 LINQ `.Where` | EX9_074, BT6_106 |
| UnityEngine/Photon 타입·`MonoBehaviour` 콜백 | 제거(순수 로직만 남김) | 전 파일 |

> 상세 심볼 번역은 `docs/porting/symbol_map_guide.md` + `symbol_map.csv`(§2 규칙: Player-is-PlayerId, value-equality→Resolve 등).

### 9.1 번역 등재제 (2026-07-23 id-표면 flip 캠페인에서 확정)

**위 표(+아래 등재 목록)에 없는 substrate 번역은 이탈로 판정한다.** 새 번역이 필요하면 "왜 AS-IS shape이 headless에서 불가능한지" 사유와 함께 이 표에 등재하는 것이 선행 조건이다 — 등재 없는 편의 번역이 id-술어 껍데기 238파일 전염의 근본 원인이었다(관할-갭 복기 참조).

**등재된 id-핸들 어휘** (flip 캠페인 census에서 검증 — 이것만 허용):
| 어휘 | 등재 사유 |
|---|---|
| `zones.GetCards(...)` → `IReadOnlyList<HeadlessEntityId>` | zone reader 반환형 — AS-IS `List<Card>`의 headless 핸들 미러 |
| `MatchStateMutationSink`/`ReturnToDeckBottom` 등 sink API의 id 인자·스테이징 `List<HeadlessEntityId>` | mutation sink 어휘 — 술어 평가는 반드시 Permanent 위에서 하고 id는 운반만 |
| `permanent.TopInstanceId`·`permanent.InstanceId`·cause-id ctor 파라미터(`HeadlessEntityId?`) | Permanent/효과 원인 식별자 — AS-IS `Card` 참조-동일성의 headless 번역 |
| `CurrentBattleOpponent(card)` 등 commons 핸들 조회 후 `new Permanent(context, id)` 구체화 | 핸들→도메인 구체화 idiom (2-arg ctor가 owner를 리포지토리 해석) |
| 테스트 픽스처의 `new HeadlessEntityId(...)`·`result.SelectedIds` | choice-result/픽스처 배관 (AS-IS 대응 없음 — 테스트 전용) |

**금지 재확인**: 카드 파일 내 `Func<HeadlessEntityId,bool>` 술어·`PermanentOf` 해소 껍데기·`...ById` 어댑터 = **0 유지** (2026-07-23 flip 캠페인이 표면 자체를 삭제했으므로 이런 코드는 컴파일되지 않는다 — 보이면 stale 참조를 베낀 것).

**계기판 지표(강제)**: `grep -rn "Func<HeadlessEntityId" src/HeadlessDCGO.Engine/Assets/Scripts --include=*.cs | grep -v ":[0-9]*: *//"` = **0**. 대량 포팅 트랜치마다 게이트에 포함할 것.

---

## 10. 테스트 관례 (행동 witness — 대량 포팅 가드레일)

**정본 템플릿**: `tests/EXEMPLAR-T1.Witness.Tests/Program.cs` (T1~T3B·GLINK 동형). 새 트랜치는 이걸 **복사**해 시작.

### 10.1 witness 3종 세트 (카드당)
- AS-IS 각 주요 효과/축마다 **양성(효과 발화·상태 변화 측정) + 경계/음성(게이트 OFF·잘못된 창·타깃 부재)** 을 짝지어 3개.
  예(T1): `W1 양성 발화` / `W2 경계 게이트 OFF` / `W3 다른 창/ESS 대조`.

### 10.2 펌프-드라이브 매치 게이트 (강제 — false-green 방지)
- 매치 생성: `DcgoMatch.CreatePumpDriven(context, new EngineTrace())` (OLD-cadence 직접 컨트롤러 호출·스텝 액션 **금지**).
- **정본 헬퍼**(T1 Program.cs 하단 복사):
  ```csharp
  var policy = new PolicyChoiceProvider();
  EngineContext context = ContextFactory.CreateWithProvider(policy, seed);
  DcgoMatch match = DcgoMatch.CreatePumpDriven(context, new EngineTrace());
  await match.InitializeAsync(config);
  // 메인 대기까지 구동(=DoneStartGame 통과, phase==Main, 내 턴, pending 없음):
  await ReachMainWaitAsync(match);   // StepOnce + DriveUntil(AtMainWaitOf(P1))
  ```
- **게이트 패턴**: 창(window)은 `DoneStartGame`(Setup 통과) + `phase==Main` 이후에만 열린다. 구식 스위트는
  `context.TurnController.SetPhase(HeadlessPhase.Main)`로 직접 진입(예 `tests/G9-034.GainMemory.Tests`), EXEMPLAR 스위트는
  `ReachMainWaitAsync`(펌프-드라이브)로 진입한다. **신규 witness는 EXEMPLAR 방식(펌프-드라이브)을 쓴다.**
- **효과-내부 Select\*/Optional 프롬프트**는 `policy.On(req => …, req => ChoiceResult.Select(…))` 좌석으로 응답(에이전트 좌석 = 스크립트 답).

### 10.3 펌프-표면 witness 강제
- witness는 **리걸 액션 테이블에서 액션을 골라 `ApplyAsync`** 하는 방식으로 효과가 **실 발화(inert 아님)** 함을 측정한다.
  단위 단언만으로는 부족(false-green). `RequireLane(...)`로 리걸 레인 존재를 단언 → `ApplyAsync` → 상태 diff `AssertEqual`.
- **완료 정의**: witness green + 회귀(인접 스위트 green) + `RuleAudit 0` + 적대 리뷰(goal-witness 모드).

---

## 11. 금지 목록 (삭제된 발명 심볼 — "보이면 잘못된 참조를 베낀 것")

아래 심볼은 2026-07-23 소프트 동결에서 **물리 삭제**됐다(`grep --binary-files=text` 실측 = **live 참조 0**).
이 이름을 **쓰거나 import 하면 스테일 문서/구 코드를 베낀 것**이다. 컴파일도 안 되고, 봤다면 즉시 의심하라.

| 금지 심볼 | 대신 쓸 정본 경로 |
|---|---|
| `EffectRegistry` / `EffectRegistry.Register` | 없음. 카드는 팩토리/`ActivateClass`만 반환; 등록은 substrate가 함 |
| `EffectBinding` / `.ToBinding()` | 없음. binding 개념 폐기 |
| `IActivatedCardEffect` (마커) | `ICardEffect` + `ActivateClass`(ActivateICardEffect 계약) |
| `LegacyBindingBridge` | 없음 |
| `PermanentEffectFactoryBinding` (string-key 오버로드) | `PermanentEffectFactory` 본체 오버로드(§7.3) |
| `CardPortingFramework` (**타입**) | **타입 없음**. `CardEffectFactory.<Name>` / `CardEffectCommons.<Name>` 직접 호출 |
| `ContinuousKeywordGate`/`ContinuousDpGate` 등을 **카드가 직접 등록/질의** | 카드는 안 건드림; substrate 스캔이 처리 |

> ⚠️ **`CardPortingFramework` 함정 (해소됨)**: 파일 `…/CardEffectCommons/CardPortingFramework.cs`는 **물리 삭제
> 완료**(flip 캠페인 마지막 조각, 2026-07-23) — 마지막 남은 거주자였던 `BlastDNACondition`을 AS-IS 정위치 미러 파일
> `…/CardEffectFactory/KeyWordEffects/BlastDNADigivolution.cs`로 이주(AS-IS와 동일 파일-내 위치: `CardEffectFactory`
> 파셜 선언 직전)한 뒤 빈 파일을 삭제했다. **`CardPortingFramework`라는 타입은 애초에 없었다**. 옛 매핑 문서의
> "CPF:5786" 같은 주소는 **역사적 별칭(스테일)** 이다 — 그 메서드들은 지금 `CardEffectFactory.cs`/`CardEffectCommons.cs`(및
> 분해된 partial 파일)에 산다. 코드에 `CardPortingFramework.무엇` 이라고 절대 쓰지 말 것.
>
> ⚠️ **구 지침 2종**(`card_porting_recipe.md`, `porting_translation_cheatsheet.md`)은 SUPERSEDED. `.ToBinding()`/
> `EffectRegistry.Register`/`KeywordBaseBatch1`을 가르치는 모든 서술은 스테일이다. **이 문서(정본)만 따른다.**

---

## 부록 A. 체크리스트 (카드 1장 완료 판정)

- [ ] 미러 경로 정확(`…/CardEffect/<SET>/<Color>/<ID>.cs`, `Script/` 없음), 클래스 `: CEntity_Effect`.
- [ ] 헤더 4블록(Source/②프리미티브 매핑+AS-IS 라인 앵커/③배선 근거/치환) 존재.
- [ ] 각 AS-IS region이 `if (timing == EffectTiming.<창>)`로 1:1 대응, 팩토리/kind-class **AS-IS 이름 그대로**.
- [ ] 술어 `Func<Permanent,bool>`/`Func<CardSource,bool>` 직독, 평가됨(뭉갬·null·하드코딩 없음).
- [ ] substrate 번역만 적용(§9), 로직 변경 0. AS-IS quirk는 verbatim + 주석.
- [ ] 갭 있으면 `throw new NotSupportedException("design item RD-…")`(리터럴 TODO 없음), 아니면 완주.
- [ ] 금지 심볼(§11) 미사용.
- [ ] witness 3종(양성+경계) 펌프-드라이브, green + 인접 회귀 green + RuleAudit 0.
