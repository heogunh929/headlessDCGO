# 카드 포팅 레시피 (Phase 1 산출물)

- 작성일: 2026-06-29
- 목적: 원본 DCGO 카드효과(`DCGO/Assets/Scripts/CardEffect/<set>/<color>/<id>.cs`)를 헤드리스 엔진으로 **1:1 미러 포팅**하는 반복 절차. 로컬 LLM이 그대로 따라 할 수 있게 작성.
- 검증된 첫 사례: **ST7_10** (security attack +1 연속 + Piercing). 테스트 `tests/P1-ST710.Port.Tests` 3/3 green, 전체 200/200 무회귀.

---

## 0. 큰 그림 (왜 이 형태인가)
원본은 카드마다 `public class <Id> : CEntity_Effect`가 `CardEffects(EffectTiming timing, CardSource card)`를 오버라이드해 그 타이밍에 활성인 `ICardEffect`들을 반환한다. 헤드리스는 Unity 타입이 없으므로, **저작 표면을 1:1로 미러**한 프레임워크를 둔다(`Assets/Scripts/Script/CardEffectCommons/CardPortingFramework.cs`):
`CEntity_Effect` · `CardSource` · `EffectTiming` · `ICardEffect` · `CardEffectFactory`(메서드명 원본과 동일).

각 `ICardEffect`는 `ToBinding()`으로 **`EffectBinding`** 으로 낮춰지고, 이미 LIVE인 게이트가 소비한다:
- 연속 수치(DP/시큐어택/코스트) → `ContinuousDpGate` / `ContinuousModifierGate` (`Context.Values`의 `dpDelta`/`securityAttackDelta`/`playCostDelta` 등 키 + `EffectQueryRole.Continuous` + scope).
- 키워드(Blocker/Jamming/Reboot/Piercing) → 기존 `KeywordBaseBatch1` 해소/게이트 재사용.
- 트리거(OnPlay 등) → `TriggerEventEmitter`→`AutoProcessingTriggerCollector`→`EffectScheduler`→`CardEffectSchedulerResolver`→`IHeadlessCardEffect.ResolveAsync`→`MatchStateMutationSink`(처리 kind: AddMemory/DrawCards/CreateToken/PlayCard/Trash…/Return…/Suspend/AddDpModifier 등).

> 등록 시임: `CardEffectRegistrar.RegisterOnEnterPlay(context, effect, cardNumber, card)` — 카드가 장에 들어올 때 그 카드의 바인딩을 EffectRegistry에 등록. (현재 테스트/명시 호출용. PlayCardAction 자동 호출 배선은 후속 — §6.)

---

## 1. 절차 (한 카드)
1. **원본 읽기**: `DCGO/Assets/Scripts/CardEffect/<set>/<color>/<id>.cs`.
2. **헤드리스 미러 파일 작성**: `src/HeadlessDCGO.Engine/Assets/Scripts/CardEffect/<set>/<color>/<id>.cs` (스켈레톤 헤더를 본문으로 교체).
   - `namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.<set>.<color>;`
   - `using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;`
   - `public sealed class <Id> : CEntity_Effect` + `CardEffects(EffectTiming, CardSource)` 오버라이드.
   - **본문은 원본을 그대로** 옮긴다(같은 `if (timing == …)` 분기, 같은 `CardEffectFactory.<Method>(...)` 호출). 헤드리스 `CardEffectFactory`에 해당 메서드가 있으면 그대로 컴파일된다.
3. **팩토리 메서드 없으면 추가**: `CardPortingFramework.cs`의 `CardEffectFactory`에 원본과 **동일한 이름**으로 메서드 추가 → 적절한 `ICardEffect`(연속/키워드/트리거) 반환.
4. **타이밍 매핑**: 원본 `EffectTiming.X`가 헤드리스 enum에 없으면 `EffectTiming`에 추가(+필요 시 `TriggerTimings` 상수와 연결).
5. **테스트**: 그룹 기준에 따라 **그 카드가 속한 그룹의 `tests/CardEffect.<Set>.<Color>.Tests/` 프로젝트에 sub-test 추가**(카드마다 새 프로젝트 만들지 않음 — [card_group_standard.md](card_group_standard.md)). 등록 후 게이트/싱크로 결과 단언.
6. **게이트**: `bash scripts/run-tests.sh` 전체 green 확인 → 백로그 갱신.

---

## 2. 매핑 치트시트 (원본 → 헤드리스)
| 원본 `CardEffectFactory.X` | 헤드리스 처리 | 인코딩 |
|---|---|---|
| `ChangeSelfDPStaticEffect(changeValue,…)` | 연속 DP | `Context.Values["dpDelta"]=v`, role Continuous, scope `ContinuousDpGate.Scope`, target=self |
| `ChangeSelfSAttackStaticEffect(changeValue,…)` | 연속 시큐어택 | `["securityAttackDelta"]=v` (동 scope) |
| `BlockerSelfStaticEffect` / `JammingSelfStaticEffect` / `PierceSelfEffect` | 키워드 | `KeywordBaseBatch1Kind.{Blocker,Jamming,Piercing}` 재사용 |
| (코스트 증감) | 연속 코스트 | `["playCostDelta"]`/`["digivolutionCostDelta"]` |

키 상수: `ModifierHelpers.DpDeltaKey/SecurityAttackDeltaKey/PlayCostDeltaKey/DigivolutionCostDeltaKey`.

---

## 3. 검증된 예시 — ST7_10
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
테스트 핵심: `CardEffectRegistrar.RegisterOnEnterPlay(...)` → `ContinuousModifierGate.ResolveSecurityAttack(context, card, 1) == 2`, `EffectRegistry.GetKeywordEffects("Piercing").Count >= 1`.

---

## 4. 현재 커버되는 패턴 (연속효과군 — 완성)
- **연속 자버프**(DP/시큐어택/코스트) — 무조건 + **조건부**(`condition` 람다, read-time 평가) + **상속**(`isInheritedEffect:true`, 소재→top 자동 부여) + **동적값**(`changeValue: () => count()`, read-time `Func<int>` 평가).
- **플레이어-광역 연속**(`ChangeDPStaticEffect`, "your Digimon +X DP") — 소유자+카드타입 스코프(`PlayerScopeContinuousHelpers`).
- 자기 대상 **키워드 부여**(Blocker/Jamming/Reboot/Piercing).
- 조건/술어: `CardEffectCommons.IsOwnerTurn(card)`, `IsExistOnBattleArea(card)`, `IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card)`, `card.PermanentOfThisCard().DigivolutionCards.Count`.
- 검증(`tests/P1-ST1.RedWave1.Tests`, 5/5): ST7_10(메인 SA/Pierce)·ST1_07(상속 SA)·ST1_03(상속+턴)·ST1_01(상속+소재수)·ST1_11(동적값)·ST1_12(광역 DP).

> 작동 원리: 조건/동적값은 `EngineContext`를 `CardSource`에 실어 read-time에 `Func<bool>`/`Func<int>` 평가(`ContinuousScopeEvaluation`). 상속은 top 평가 시 active 소재(`InheritedEffectHelpers.ActiveInheritedSources`)의 inherited 연속효과를 fold-in. 광역은 `PlayerScope*` 마커로 소유자 카드에 적용.

## 4-b. 트리거/활성 효과 (추가 커버)
- **트리거-메모리**(ActivateClass "gain/lose N memory"): `TriggeredMemoryEffect`(timing 발화→AddMemory emit)+`AddMemoryTriggerEffect`. 검증 ST1_06/09(`tests/P1-ST1.RedTriggers.Tests` 4/4) — 본체를 실 sink로 해소.
- **활성 선택+삭제**(Option [Main] delete): `ActivatedSelectEffect`(SelectPermanentEffect 래핑, Mode.Destroy)+`SelectAndDestroyEffect`. 대상 술어 `CardEffectCommons.IsOpponentBattleAreaDigimon`/`CurrentDp`. 검증 ST1_16/15(`tests/P1-ST1.RedActivated.Tests` 3/3) — 명령형(BuildRequest→scripted answer→Apply), CVA2 패턴.

> 1:1 완화: 트리거/활성 효과는 원본 코루틴 `ActivateClass`/`SelectPermanentEffect` 인라인을 헤드리스 헬퍼로 표현(코루틴→뮤테이션/요청). 카드 본문은 술어·설명까지 유지.

## 4-c. 지속시간 / 존-스코프 활성 버프 (추가 커버)
- **선택+지속 DP**(`SelectAndBuffDpEffect`→`ActivatedTargetBuffEffect`): 선택 대상에 `EffectDuration` 태그 연속 modifier 등록(예 ST1_13 [Main], ST1_08 When Digivolving).
- **광역+지속**(`PlayerScopeBuffSAttackEffect`/`PlayerScopeBuffSecurityDpEffect`→`ActivatedPlayerScopeBuffEffect`): 소유자(+카드타입/존) 스코프 지속 버프(예 ST1_13 [Security], ST1_14 시큐리티 디지몬 DP).
- **존-스코프**: `PlayerScopeContinuousHelpers.ScopeZoneKey` + `ContinuousScopeEvaluation.ResolveZoneName`(BattleArea/Security) → "your Security Digimon" 류.
- 만료: `EffectDurationExpiry.ExpireTurnEnd/...`. 검증 `tests/P1-ST1.RedTimedBuff.Tests` 5/5.
- **`IActivatedCardEffect` 마커**: 활성/선택/지속버프/Deferred 효과는 `CardEffectRegistrar.AllTimings` 자동등록에서 스킵(명령형 활성화 경로로 해소).

> **ST1(스타터1 적색) 12/12 전부 포팅 완료** — 연속(상속/조건/동적/광역/존)·트리거-메모리·활성선택+삭제·지속버프 전 패턴 커버.

## 5. 아직 갭 (다음)
1. **활성효과 자동 활성화 배선**(최우선): `IHeadlessCardEffect.ResolveAsync`에 **choice provider 없음** → 옵션/시큐리티 액션이 효과 본체를 대화형으로 해소하는 풀-루프 미배선. 현재 활성효과(선택/삭제/버프)는 **로직만 명령형 검증**(BuildRequest→answer→Apply). RL 실전 연결의 핵심.
2. **시큐리티 카드 DP 게이트 실전 호출**: ST1_14 류 버프가 시큐리티 체크 전투에서 실제 적용되려면 시큐리티 카드 DP 해소가 `ContinuousDpGate`를 호출해야 함(단위 검증은 됨).
3. **트리거 타이밍 emit**: 일부(OnAllyAttack 등) emit-only 미배선.
4. **DigiXros/DNA/Blast 특수 플레이**: D-5/D-6 헬퍼 있으나 플레이-액션 배선 없음(예 BT10_012). 강모델 작업.

> `EffectTiming.OptionSkill`/`SecuritySkill`은 `CardEffectRegistrar.AllTimings`에서 제외(자동등록 안 됨). 미포팅 활성효과는 `DeferredCardEffect`로 1:1 컴파일만 유지.

## 6. 런타임 자동 등록 배선 (TODO, 강모델)
지금은 `CardEffectRegistrar.RegisterOnEnterPlay`를 명시 호출. 완전 자동화하려면:
- `cardNumber → CEntity_Effect` 디스패치 테이블,
- `PlayCardAction`(및 디지볼브/장 진입)이 카드 진입 후 디스패치를 통해 등록,
- 장 이탈 시 해당 바인딩 제거(EffectRegistry.RemoveWhere)와 수명(EffectDuration) 정합.
디스패치에 등록된 카드만 영향받게 가드 → 미포팅 카드 무영향.
