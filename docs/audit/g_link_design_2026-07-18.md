# G-Link 설계서 — Link 서브시스템 (K:Link 73장 게이팅)

- 작성일: 2026-07-18
- 성격: 인프라 골 설계서 (read-only 분석 기반, 코드/빌드/테스트 미변경)
- 요구사항 명세: `docs/audit/coverage_exemplar_audit_2026-07-18.md` §8, 원장 RD-P6C2-7 · RD-EXT3-01/3A · C2-02(MIG5-CANLINK-PAYCOST)
- 게이팅 물량: K:Link **73장**(팩토리 경로 70 + 직접호출 경로 8, 중복 포함)

> 규약: AS-IS 1:1 미러(동일 경로·동일 파일명·동일 로직), substrate 번역만 허용. 단순화·발명 파일 금지.
> 경로 표기: AS-IS = `DCGO/Assets/...`, 미러 = `src/HeadlessDCGO.Engine/...`.

---

## 1부: AS-IS Link 전체 해부 (수명 다이어그램)

Link 플레이의 전 수명은 **선언 → 코스트 산정 → WhenWouldLink 창 → 코스트 지불 → 배치 → 링크 상태 → 해제**로 흐른다.

```
[선언/합법성]                         [코스트]                    [해소 흐름 = ILinkCard.LinkCard()]
LinkEffect (factory)  ─┐            GetChangedLinkCost ──┐      1) root 존 판정(Hand/Trash/Digi/Linked/None)
  또는                 ├─ 합법 ─▶   (linkCondition.cost   │  ──▶ 2) WhenWouldLink 창(autoProcessing_CutIn)
new ILinkCard(...)   ─┘             + IChangeLinkCostEffect │      3) if payCost: Owner.AddMemory(-Cost)
                                     fold: perm/player/self)│      4) 배치:
CanLink / CanLinkToTargetPermanent ──┘  (Max(0,Cost))           root==None ─▶ IPlacePermanentToLinkCards
                                                                 else       ─▶ Permanent.AddLinkCard
                                                                            5) WasLinked 확인 + FixedCost 초기화
                                                                                    │
[링크 상태 소비]                                        [해제/정리]                  ▼
LinkedDP(→DP계산) · IsLinkedEffect(계승형 게이팅)        RemoveLinkedCard / ITrashLinkCards / AddLinkCard
LinkedMax(IChangeLinkMaxEffect fold)                    상태기반: DigimonLackLinkCondition/LinkMaxCount (AutoProcessing)
                                                        WhenLinked 창 / OnLinkCardDiscarded 창
```

### 1.1 선언 진입 조건 (손패 또는 필드 디지몬)

- **키워드 팩토리** `CardEffectFactory.LinkEffect(card, condition)` — `DCGO/Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/Link.cs:19-104`.
  - 진입 게이트(:21-24): `card != null`, `IsOwnerTurn`, `IsExistOnHand || IsExistOnBattleAreaDigimon`, `HasMatchConditionPermanent`.
  - `CanUseCondition`(:49-63): `IsExistOnHand || (IsExistOnBattleAreaDigimon && !card.IsLinked)` + 매칭 대상 존재 + optional `condition()`.
  - `CanSelectPermanentCondition`(:30-47): 소유자 배틀에어리어, 자기 자신 permanent 아님, `IsDigimon`, `linkCondition == null || linkCondition.digimonCondition(permanent)`.
  - `ActivateCoroutine`(:65-101): `SelectPermanentEffect`(maxCount=1)로 대상 1장 선택 → `new ILinkCard(true, card, selectedPermanent, activateClass).LinkCard()`.
- **합법성 술어** (효과·AI가 참조):
  - `CardSource.CanLink(bool PayCost, bool allowBreeding=false)` — `DCGO/Assets/Scripts/Script/CardSource.cs:3140-3205`. `linkCondition` 존재 시 소유자 배틀에어리어(또는 breeding 시 필드) 디지몬 중 `digimonCondition` 만족분을 스캔; `PayCost`면 `Owner.MaxMemoryCost >= GetChangedLinkCost(digimon, root)` 검사(root는 손패면 Hand, 아니면 None).
  - `CardSource.CanLinkToTargetPermanent(target, PayCost, allowBreeding)` — `CardSource.cs:3337-3372`. 대상 특정판: 토큰 아님, breeding 제외(불허 시), `CanLink(false)`, `digimonCondition(target)`, `PayCost`면 코스트 검사.

### 1.2 링크 코스트 산정

- `CardSource.GetChangedLinkCost(targetPermanent, root)` — `CardSource.cs:3267-3331`.
  - 기저 `linkCondition.cost`(:3271)에서 `IChangeLinkCostEffect` 3원천을 순차 fold:
    1. **permanents**(:3277-3288): `Players_ForTurnPlayer`의 필드 permanent(자기 permanent 제외) EffectList,
    2. **players**(:3294-3302): 플레이어 EffectList,
    3. **self**(:3308-3314): 자신 EffectList.
  - 필터: `is IChangeLinkCostEffect && CanUse(null) && CardCondition(this) && PermanentCondition(targetPermanent)`.
  - `IsUpDown()==false` 그룹 먼저, `IsUpDown()==true` 그룹 나중 적용(:3318-3328) → `Math.Max(0, Cost)`(:3330).
- **코스트 감소 생산자**: `GrantedReduceLinkCostClass` / `ReduceLinkCostClass` / `ChangeLinkCostClass` — `Link.cs:108-153` (모두 `ChangeLinkCostClass : IChangeLinkCostEffect` 생성).

### 1.3 해소 흐름 — `ILinkCard.LinkCard()`

- 타입 `public class ILinkCard` — `DCGO/Assets/Scripts/Script/CardController.cs:3440-3498`. 생성자 인자 `(bool payCost, CardSource card, Permanent permanent, ICardEffect cardEffect)`.
- `LinkCard()`(:3456-3497) 단계:
  1. null 가드(:3458-3460).
  2. **root 존 판정**(:3463-3473): Hand / Trash / DigivolutionCards / LinkedCards / None (링크 카드의 현재 위치).
  3. **WhenWouldLink 창**(:3475-3479): `autoProcessing_CutIn.StackSkillInfos(CardEffectCommons.WouldLinkHashtable(...), EffectTiming.WhenWouldLink)` + `TriggeredSkillProcess(false,null)` — 지불 **전** 링크-방지/반응 창.
  4. **코스트 지불**(:3482-3487): `if (_payCost)` → `Cost = GetChangedLinkCost(permanent, root)` → `Owner.AddMemory(-1*Cost, cardEffect)`.
  5. **배치**(:3489-3492): `root==None` → `new IPlacePermanentToLinkCards({perm-of-linkcard, permanent}, cardEffect).PlacePermanentToLinkCards()` (필드 permanent를 링크 카드로 전환); `else` → `permanent.AddLinkCard(linkCard, cardEffect)` (손패/트래시/진화원의 카드를 붙임).
  6. `WasLinked = permanent.LinkedCards.Contains(linkCard)`(:3494) + `UntilCalculateFixedCostEffect` 초기화(:3496).
- 직접호출 카드는 팩토리를 우회하여 `new ILinkCard(true/false, ...).LinkCard()`를 자체 몸통에서 호출(1.7 참조).

### 1.4 배치 — 링크 카드가 permanent 아래 붙는 방식

- **손패/트래시/진화원 → 링크**: `Permanent.AddLinkCard(addedLinkCard, cardEffect)` — `DCGO/Assets/Scripts/Script/Permanent.cs:1237-1293`.
  - 초과분 선정리(:1251-1257): `LinkedCards.Count >= LinkedMax`면 `RemoveLinkedCard`(Max>1이면 초과 개수 선택 트래시, Max==1이면 `LinkedCards[0]` 트래시).
  - `RemoveFromAllArea`(:1259) 후 토큰 아니면 `LinkedCards.Insert(0, ...)`(최신 우선), `LinkedDP += LinkDP`(:1264), `cardSources.Insert(1, ...)`(:1266).
  - **WhenLinked 창**(:1281-1290): `{Permanent, CardEffect, Card, isFromDigimon}` 해시테이블로 `autoProcessing.StackSkillInfos(..., EffectTiming.WhenLinked)`.
- **필드 permanent → 링크**: `IPlacePermanentToLinkCards.PlacePermanentToLinkCards()` — `CardController.cs:3141-3435`.
  - 대상쌍 `[LinkedPermanent(제거될 것), getLinkPermanent(붙일 곳)]`; `willBeRemoveField=true` 마킹(:3200-3203).
  - **WhenRemoveField 컷인**(:3210-3343): `WhenPermanentWouldRemoveFieldCheckHashtable`로 필드/플레이어 효과 수집 → cut-in.
  - 실제 이동(:3347-3401): `OnLeaveFieldAnyone` 창(:3360-3367) → `DiscardEvoRoots`(:3371) → `PlaceOtherPermanentEffect` 기록(:3381) → `CardObjectController.RemoveField(ignoreOverflow:true)`(:3387) → `getLinkPermanent.AddLinkCard(cardSource, cardEffect)`(:3391) → `InitUseCountThisTurn`.
- **상태 멤버**:
  - `Permanent.LinkedCards` (List) — `Permanent.cs:1041`; `LinkedMax` (기저 1 + `IChangeLinkMaxEffect` fold) — `Permanent.cs:896-...`; `LinkedDP` (auto-prop) — `Permanent.cs:670`; `DigivolutionCards`/`StackCards`는 `!LinkedCards.Contains` 필터(:884/888).
  - `CardSource.IsLinked => PermanentOfThisCard().LinkedCards.Contains(this)` — `CardSource.cs:2947`; `CardSource.LinkDP` — `CardSource.cs:2449`.
  - `LinkCondition`(digimonCondition + cost) — `CardSource.cs:4286-4296`; producer `AddLinkConditionClass : IAddLinkConditionEffect`.

### 1.5 링크 상태의 효과 (소비자)

- **DP 기여**: `LinkedDP`가 permanent DP 계산에 가산(AS-IS DP getter; 미러 `Permanent.cs:312/488`).
- **계승형(linked) 효과 게이팅**: `ICardEffect.IsLinkedEffect` — `DCGO/Assets/Scripts/Script/ICardEffect.cs:551-559`. 가용성 판정(:386-415)은 `IsInheritedEffect || IsLinkedEffect`일 때 top card 아님·`!IsFlipped`·permanent가 digimon을 요구하고, `IsLinkedEffect && !LinkedCards.Contains(EffectSourceCard)`면 비활성. `CheckEffectDisabledClass`(:120/145)도 `IsLinkedEffect` 분기.
- **트리거 창 소비**: `WhenLinked`(64장), `OnLinkCardDiscarded`(7장), `WhenWouldLink`(2장: BT25_004·BT25_045) — CanTrigger는 `CardEffectCommons.WhenLinked/OnTrashLinkCard/OnTrashLinkedCard/WhenWouldLink`.
- **공격/배틀에서의 링크**: 엔진 레벨의 링크→추가 시큐리티공격 기제는 **없음**(AttackProcess.cs에 LinkedCards 참조 없음). "시큐리티 추가공격" 류는 카드효과 레벨에서 `IsLinked`/`HasNoLinkCards` 술어와 `LinkedDP`로 표현. 따라서 G-Link 스코프는 **선언/코스트/배치/상태/해제**이며, 배틀 연동은 개별 카드 몸통 소관.

### 1.6 링크 해제 / leave-field 처리

- `Permanent.RemoveLinkedCard(cardSource, removeCount, trashCard)` — `Permanent.cs:1306-1348`: 개별 제거(`LinkedDP -=`, `LinkedCards.Remove`, `AddTrashCard`) 또는 개수 지정 시 `SelectCardEffect`(root=Custom, customRootCardList=LinkedCards)로 선택 트래시.
- `ITrashLinkCards.TrashLinkCards()` — `CardController.cs:5242`; wrapper `CardEffectCommons.TrashLinkCardsAndProcessAccordingToResult` — `CardEffectCommons.cs:567`.
- **상태기반(state-based) 자동정리** — `DCGO/Assets/Scripts/Script/AutoProcessing.cs`:
  - `IsDigimonLackLinkCondition`(:234-248) → `DigimonLackLinkConditionProcess`(:502-520): `LinkedCards` 중 `!CanLinkToTargetPermanent(perm,false)`인 것을 `ITrashLinkCards`로 트래시.
  - `IsDigimonLackLinkCount`(:250-264) → `DigimonLackLinkMaxCountProcess`(:524-537): `LinkedCards.Count > LinkedMax` 초과분을 `RemoveLinkedCard(null, 초과수)`로 정리.
- **leave-field**: 필드→링크 전환(1.4)은 `OnLeaveFieldAnyone` + `WhenRemoveField` 창을 발화; 링크 카드 자체의 트래시는 `OnLinkCardDiscarded`/`OnTrashLinkCard` 창을 발화.

### 1.7 직접호출 경로 (팩토리 우회, 8장)

`new ILinkCard(...).LinkCard()`를 카드 몸통에서 직접 호출 — 컴파일 의존(타입 `ILinkCard` 필요):
`BT25_070·BT25_072·BT25_075·BT25_052·BT25_056·BT25_089`(BT25), `P_234`, `ST22_12`.
- payCost 인자: 7장이 `true`, 1장(`BT25_075`)이 `false`. 대상은 `permanent` 또는 `card.PermanentOfThisCard()`.

---

## 2부: 미러 현황 대조 (있음 / 스텁 / 부재)

### 2.1 있음 (1:1 이관 완료)

| AS-IS 표면 | 미러 위치 | 비고 |
|---|---|---|
| LinkEffect **합법성 게이트** (CanUseCondition·CanSelectPermanentCondition) | `Assets/.../KeyWordEffects/Link.cs:31-75` | 몸통(ActivateCoroutine)만 STOP |
| `CanLink(false)` / `CanLinkToTargetPermanent(false)` | `Assets/.../CardSource.cs:1418 / 1612` | 코스트-무 합법성 |
| `GrantedReduceLinkCostClass`·`ChangeLinkCostClass`·`ReduceLinkCostClass` | `Link.cs:94-140` | 코스트감소 **생산자** 존재 |
| `ChangeLinkCostClass`·`ChangeLinkMaxClass` 효과 + `IChangeLinkCostEffect`·`IChangeLinkMaxEffect` | `Assets/.../CardEffects/*.cs`, `CardEffectInterfaces.cs` | fold 대상 인터페이스 준비됨 |
| `AddLinkConditionClass`(linkCondition 생산자) / `LinkConditionOf()` | `CardEffects/AddLinkConditionClass.cs`, `CardSource.cs:1522` | |
| `Permanent.AddLinkCard`·`RemoveLinkedCard`·`LinkedCards`·`LinkedMax`·`LinkedDP`·`IsLinked` | `CardSource.cs:1186`, `Permanent.cs:2236/2644/2666/3583/3791` | substrate=`LinkHelpers` |
| `LinkHelpers`(attach/detach/DP/max, off-field 메타 저장) | `Headless/Runtime/LinkHelpers.cs` (312줄) | `AddLinkCardAsync`가 WhenLinked emit |
| `ITrashLinkCards` / `TrashLinkedCards` / `OnTrashLinkCard`·`OnTrashLinkedCard` CanUse | `CardController.cs:1287`, `CardEffectCommons/TrashLinkedCards.cs`, `.../CanUseEffects/OnTrashLink*.cs` | |
| AutoProcessing **상태기반 정리** (LackLinkCondition/LackLinkMaxCount) | `Assets/.../AutoProcessing.cs:256-283, 611-...` | 1:1 이관 |
| `WhenLinked` 창 emit + supply | `LinkHelpers`, `SkillWindowSupply.cs:279/436 TryBuildWhenLinked` (RDW-02) | |
| 타이밍 상수 `WhenWouldLink`·`WhenLinked`·`OnLinkCardDiscarded` + `WhenWouldLink` CanUse + `WouldLinkHashtable` | `TriggerTimings.cs:115-117`, `EffectTiming.cs:85/86/152`, `CanUseEffects/WhenWouldLink.cs`, `HashtableSetting.cs:305` | 소비자측 준비, **emit는 부재** |
| 링크 회귀 테스트 | `tests/G3.5-D1L.LinkSubsystem`, `G9-031.LinkSecurity`, `C2-Witness`, `F1-Tier2-WhenLinked`, `G3.5-B10.MaterialLinkTrash` | 상태모델 커버 |

### 2.2 스텁/STOP (throw 좌석)

| AS-IS | 미러 STOP | 원장 |
|---|---|---|
| `LinkEffect.ActivateCoroutine` 전체 해소 | `Link.cs:77-87` `NotSupportedException` | **RD-P6C2-7** |
| `CanLink(payCost:true)` | `CardSource.cs:1429` throw | **C2-02 / MIG5-CANLINK-PAYCOST** |
| `CanLinkToTargetPermanent(payCost:true)` | `CardSource.cs:1654` 주석 좌석(payCost 분기 미구현) | C2-02 |

### 2.3 부재 (미러에 타입/함수 없음)

- **`ILinkCard` 클래스** — 미러 `CardController.cs`에 없음. 직접호출 8장의 **컴파일 블로커**.
- **`IPlacePermanentToLinkCards` 클래스** — 없음. `root==None`(필드 permanent→링크) 경로 부재.
- **`CardSource.GetChangedLinkCost` 프리미티브** — 없음(4개 참조 전부 주석/STOP 메시지). 코스트 fold 미구현.
- **`WhenWouldLink` emit 지점 + supply 빌더** — 상수·CanUse·`WouldLinkHashtable`는 있으나 발화 site 없음, `SkillWindowSupply.TryBuildWhenWouldLink` 부재.

### 2.4 트랜치가 남긴 STOP 좌석 (카드)

- **팩토리 경로 STOP: 70장** (K:Link 키워드, `LinkEffect.ActivateCoroutine` RD-P6C2-7).
- **직접호출 STOP/컴파일블록: 8장** (`ILinkCard` 타입 부재): BT25_070/072/075/052/056/089·P_234·ST22_12.
- 트리 내 대표 좌석:
  - `EX10_029`(Warpmon): 선언=팩토리 STOP; `[When Linking]` 몸통(ImmuneFromDeDigivolve·TrashLinkCards)은 클린. — `.../CardEffect/EX10/Black/EX10_029.cs:13-20`.
  - `BT25_089`(Kazuki & Itsuki): `[Main]` suspend-cost(SuspendPeremanentAndProcessAccordingToResult) 클린 + link 실행 절반 STOP(직접 `ILinkCard` + `CanLink(payCost)`). — `.../CardEffect/BT25/Green/BT25_089.cs:99-111`.

---

## 3부: 구현 설계

### ① 타입/흐름의 미러 착지 (동일 경로·동일 파일)

1. `CardSource.GetChangedLinkCost(targetPermanent, root)` → 미러 `Assets/.../CardSource.cs`에 1:1 신설. permanents/players/self 3원천을 미러 `EffectList`(NewModelContinuousScan)로 스캔, `IChangeLinkCostEffect` fold, NotUpDown→UpDown 순, `Max(0,Cost)`. → `CanLink(payCost)`·`CanLinkToTargetPermanent(payCost)`의 throw 해제.
2. `ILinkCard` → 미러 `Assets/.../CardController.cs`에 동일 클래스 신설. `LinkCard()`는 `async Task`(coroutine→Task 관례). 5단계 그대로.
3. `IPlacePermanentToLinkCards` → 미러 `CardController.cs`에 동일 클래스 신설. WhenRemoveField 컷인·OnLeaveFieldAnyone·RemoveField·AddLinkCard를 기존 substrate로 배선.
4. `LinkEffect.ActivateCoroutine`(`Link.cs:77-87`) → STOP 제거, `SelectPermanentEffect` 대상선택 후 `new ILinkCard(true, card, selectedPermanent, activateClass).LinkCard()` 배선(AS-IS 몸통 복원).
5. 직접호출 8장 → `ILinkCard` 타입 착지 즉시 컴파일 해제; 각 몸통의 STOP 마커를 AS-IS 원문으로 복원.

### ② substrate 번역 지점

- **대상 선택** = 기존 choice 펌프. `SelectPermanentEffect`(maxCount=1) → `RequestChoice`/`ResolveChoice`. `RemoveLinkedCard`의 개수-선택도 동일(이미 `SelectCardEffect` 이관됨).
- **WhenWouldLink 창** = game-event queue emit + `SkillWindowSupply.TryBuildWhenWouldLink` 신설(미러 `WouldLinkHashtable` 재사용, `WhenLinked` RDW-02 패턴 그대로). 지불 **전** 발화 순서 유지.
- **코스트 지불** = 기존 `Player.AddMemory(-Cost)` substrate.
- **attach** = 기존 `LinkHelpers.AddLinkCardAsync`(WhenLinked emit 포함) 경유 — `ILinkCard`가 `Permanent.AddLinkCard`를 호출하면 자동으로 그 경로를 탐.
- **cut-in / triggered process** = 기존 `autoProcessing_CutIn.TriggeredSkillProcess` 펌프.
- **필드→링크 제거** = 기존 삭제/leave-field substrate(`CardObjectController.RemoveField`, `DiscardEvoRoots`, `DeletionReplacementGate` 정합) 재사용.

### ③ RL 표면 영향 판정

**판정: 신규 액션 타입 불요, factored 스키마 v2 슬롯 불변.**

- Link 선언은 활성화형 카드효과(`ActivateClass`)이며, 상위 액션(`PlayCard`/`Digivolve`/`DeclareAttack`/`SpecialPlay`)이 아니라 **스킬-활성 창 → choice 펌프**(`RequestChoice`/`ResolveChoice`)로 노출된다. `HeadlessActionTypes`에 별도 `Link` 없음이 정합(추가 불요).
- factored 레인(`FactoredActionEncoder`: PlayCard/Digivolve/DeclareAttack/breeding/SpecialPlay/Confirm/ResolveChoice — `FactoredActionSchema.Version=2`)에 Link 레인 없음; 링크 대상선택·링크카드선택은 `ResolveChoice` 후보 슬롯으로 표현. → **버전 범프·오프셋 변동 없음**.
- 유일 관측 변화: STOP였던 링크 에피소드가 **도달 가능**해져 `ResolveChoice` 세션이 늘어남(마스크 밀도↑). 스키마 형상은 불변.
- 코스트 지불(`AddMemory`)은 엔진 내부 mutation(에이전트 액션 아님).

### ④ 배치 분할 (R4 careful-mode: 3배치, 배치별 전체 스위트)

- **배치 1 — 코스트/합법성 뼈대**: `GetChangedLinkCost` 프리미티브 신설 → `CanLink(payCost)`·`CanLinkToTargetPermanent(payCost)` throw 해제. 작고 독립적, 흐름과 무관하게 합법성만 정상화. 회귀=`C2-Witness`.
- **배치 2 — 해소 흐름**: `ILinkCard` + `IPlacePermanentToLinkCards` 클래스 착지, `WhenWouldLink` emit/supply 배선, `LinkEffect.ActivateCoroutine` flip. 컴파일러가 8장 직접호출을 해제하는 지점이므로, 이 배치에서 그 8장의 STOP 마커도 복원.
- **배치 3 — 소비자 flip/행동검증**: 팩토리 70장·직접호출 8장의 행동테스트, `BT25_089 [Main]` 팔·링크-배틀 상호작용, `OnLinkCardDiscarded`/leave-field 상호작용 검증.
- (배치 1은 소형이라 2와 병합 가능하나, R4 careful-mode에 따라 분리 유지 권장.)

### ⑤ witness 계획 (게이팅 대표)

- **EX10_029**(Warpmon): 팩토리 선언 경로 + `WhenLinked` 몸통(ImmuneFromDeDigivolve) + `TrashLinkCards` — 선언→attach→WhenLinked→링크-트래시 전 수명 커버.
- **BT25_089**([Main]): 직접 `new ILinkCard(true,...)` + `CanLink(payCost)` + suspend-cost 상호작용 + 링크-배틀 맥락.
- **WhenWouldLink 카드 1장**(BT25_004 또는 BT25_045): 지불-전 prevent-link 창 발화 순서 검증.
- 각 witness: green + 단언 행동테스트 + RuleAudit 0; 배치별 전체 스위트 + shadow-run N판.

### ⑥ 리스크 (상위)

1. **WhenLinked 기존 창과의 정합**: `AddLinkCard`는 이미 `LinkHelpers`가 `WhenLinked`를 emit. 신규 `ILinkCard` 흐름은 반드시 **동일 `LinkHelpers` 경로**로 attach해야 함(WhenLinked 이중발화/우회 금지). 신규 `WhenWouldLink` emit이 기존 `WhenLinked` 순서를 교란하지 않아야 함(지불-전/지불-후 경계 엄수).
2. **IPlacePermanentToLinkCards ↔ DeDigivolve/leave-field**: 필드 permanent를 링크카드로 전환할 때 `OnLeaveFieldAnyone`+`WhenRemoveField`+`DiscardEvoRoots`가 발화 — 기존 제거/삭제 substrate(`DeletionReplacementGate` 등)와 정확히 재사용해 이중 삭제창·shadow 이탈을 방지. `EX10_029`의 De-Digivolve 면역과 교차 검증.
3. **shadow 무변 증명 범위**: STOP flip은 도달 가능 행동을 바꾸므로 OLD-vs-OLD shadow는 **비-링크 에피소드**에서만 bit-identical을 증명 가능(링크 에피소드는 flip 전 throw). 따라서 증명은 (a) 비-링크 코퍼스 무변 + (b) 링크 에피소드 신규 행동테스트(before=throw, after=resolve)로 분리 설계.
   - 부수 리스크: `GetChangedLinkCost`의 **player-원천** fold는 미러 player-EffectList가 아직 `GiveEffectToPlayer` 미-flip(`CardSource.cs:1808` P6A-PLAYER-EFFECTLIST)이라 latent일 수 있음 — permanent/self 원천은 즉시 유효, player-부여 코스트감소는 그 골 착지까지 기여 0(원장 등재 필요).

---

## 요약

- **AS-IS 수명**: 선언(LinkEffect 팩토리 / 직접 `new ILinkCard`) → 합법성(`CanLink`/`CanLinkToTargetPermanent`) → 코스트 fold(`GetChangedLinkCost`, IChangeLinkCostEffect 3원천) → `ILinkCard.LinkCard()`(WhenWouldLink 창 → AddMemory 지불 → root별 배치) → 링크상태(LinkedDP·IsLinkedEffect·LinkedMax) → 해제(RemoveLinkedCard/ITrashLinkCards + AutoProcessing 상태기반 정리). 배틀 연동은 카드효과 레벨.
- **미러 갭**: 있음 ~15면(상태모델·코스트생산자·attach substrate·WhenLinked·상태기반정리·테스트) / 스텁(STOP) 3(LinkEffect 몸통·CanLink(payCost)·CanLinkToTargetPermanent(payCost)) / **부재 4**(`ILinkCard`·`IPlacePermanentToLinkCards`·`GetChangedLinkCost`·WhenWouldLink emit+supply). STOP 카드 좌석 78(팩토리 70 + 직접호출 8).
- **배치 계획**: 3배치 — (1) GetChangedLinkCost+합법성 해제, (2) ILinkCard/IPlacePermanent/WhenWouldLink/LinkEffect flip, (3) 78장 소비자 행동검증. witness=EX10_029·BT25_089·WhenWouldLink 1장.
- **RL 스키마 영향**: 없음 — Link은 choice 펌프로 노출되는 활성화 효과, factored 스키마 v2 슬롯/버전 불변, 신규 HeadlessActionType 불요(도달 가능 ResolveChoice 세션만 증가).
- **리스크 상위 3**: (1) WhenLinked 기존 창과 이중발화·순서 정합, (2) IPlacePermanentToLinkCards의 leave-field/DeDigivolve 이중삭제 위험, (3) shadow 무변은 비-링크 코퍼스로 한정(+ player-EffectList latent fold 원장화).

## 적대 리뷰 결과 (2026-07-18, 독립 — 커밋 19742e51·a03072ee·f9217ec6): GO
P0/P1 0. 6렌즈 전부 확인(반증 실패): AS-IS 라인 대조 verbatim(showEffect 게이트=UI만·TriggeredSkillProcess 무조건 호출 정합)·WhenWouldLink=창 후 코스트 재산정 정합·재검사-부재 quirk 보존(bug-for-bug)·DP ambient=중첩 복원 안전+관측 인코더는 별도 DpCalculator 경로라 무염·BareCauseEffect 전제 성립(AS-IS 비-효과 링크 경로 없음, 포팅 WhenLinked 4개 전수 sourceCondition=null)·BT25_089 store-backed grant 정합.
**P2 원장 4건(소형 원장 배치 이월)**: ①WhenLinked cause가 AddLinkCard 경계(Permanent.cs:3982 `_ = causeEffectSourceId` 폐기)에서 절단 — source-gated WhenLinked 카드 포팅 전 마지막 hop 배선 필요(RD-C1 갱신) ②GetChangedLinkCost LATENT 주석 과대 — UntilCalculateFixedCostEffect는 live(witness가 실증), 진짜 latent=GiveEffectToPlayer 서브경로만: 주석 정정 ③ResolveLinkCost의 legacy linkCostDelta 선-fold=AS-IS 미존재 union 비계(RD-P6B-16) — 엔드포인트 철거 대상, 그전까지 GrantedReduceLinkCost가 legacy 델타 미등록 회귀 고정 ④WhenWouldLink 코스트-변조·인터리빙 witness 커버리지 갭 — BT25_004/045(실제 prevent-link) 착지 시 추가.
