# AS-IS↔TO-BE 매칭 검증 — 파트 9/13 (32파일)

대상: `docs/audit/manifest/both_part_09.txt`. AS-IS=`DCGO/Assets/Scripts/<relpath>`, TO-BE=`src/HeadlessDCGO.Engine/Assets/Scripts/<relpath>`. 양측 전문 실독 기준, 실소스 관찰만 근거로 사용.

## 요약

32파일 중 **문제 3건**(진짜 결함 2건 + 미포팅 CoreRule 1건), **경미 소견 1건**(무근거 스켈레톤), 나머지 28파일은 AS-IS와 정합.

---

## 문제 발견

### P1. `Script/CardEffectFactory/PermanentEffectFactory.cs` — `CollisionEffect`의 면역 게이트 소실 (진짜 결함)

AS-IS `PermanentEffectFactory.CollisionEffect` (DCGO PermanentEffectFactory.cs:131-143)의 `CanUseCondition`:
```csharp
return CardEffectCommons.IsPermanentExistsOnBattleArea(targetPermanent)
    && !targetPermanent.TopCard.CanNotBeAffected(activateClass);
```
TO-BE (`src/.../Script/PermanentEffectFactory.cs:121-132`)는 `activateClass`를 `_ = activateClass;`로 폐기하고 `CanNotBeAffected` 검사를 완전히 빼버림:
```csharp
condition: () => CardEffectCommons.CardEffectCommons.IsPermanentExistsOnBattleArea(targetPermanent)
```
주석 근거: "activateClass ... 자기-부여 전용이라 vacuous". **이 주장은 실소스로 반증됨.** `CollisionEffect`의 유일한 호출 경로는 `CardEffectCommons.GainCollision`(`Script/CardEffectCommons/KeyWordEffects/Collision.cs`)이고, 그 실제 AS-IS 호출부(`grep GainCollision(` 결과):
- `EX8_070.cs:74` — `GainCollision(selectedPermanent, ...)`: `selectedPermanent`는 별도 셀렉트 코루틴(`SelectTrashDigivolutionCards`)이 고른 아군 퍼머넌트로, 발동원 카드 자신이 아닐 수 있음.
- `BT21_077.cs`, `EX11_063.cs`, `EX10_032.cs`, `EX10_008.cs` — 동일 패턴(선택된 타 퍼머넌트에게 부여).

즉 "매 호출자가 self-grant"라는 전제가 거짓이며, `CanNotBeAffected(activateClass)`는 대상이 발동 카드 효과에 면역인 경우 Collision 부여를 무효화하는 실질 게이트다(AS-IS는 효과 자체는 항상 등록하되 `CanUseCondition`으로 라이브 무효화). TO-BE `Collision.cs`(`GainCollision`)도 이 재확인을 보완하지 않음(단지 VFX 분기만 스트립). 결과: TO-BE에서는 면역 대상에게도 기능하는 Collision이 부여될 수 있음 — AS-IS 대비 행동 불일치.

대조: 같은 파일의 `AddDetailClass`는 동일한 `!targetPermanent.TopCard.CanNotBeAffected(activateClass)` 게이트를 정확히 보존하고 있어, `CollisionEffect`/`CanNotSwitchAttackTargetEffect`만 선택적으로 누락됨. `CanNotSwitchAttackTargetEffect`는 AS-IS 유일 호출부(`AD1_011.cs:112`)가 진짜 self-grant(`card.PermanentOfThisCard()` + 자신의 `activateClass`)라서 논리상 무해하지만, `CollisionEffect`는 그렇지 않다.

### P2. `Script/CardEffectCommons/KeyWordEffects/Decode.cs` — `DecodeProcess`가 `PlayPermanentCards`의 `CanEnterField` 게이트를 건너뜀 (진짜 결함)

AS-IS `DecodeProcess`(KeyWordEffects/Decode.cs:65-72)는 `PlayPermanentCards(cardSources, activateClass, payCost:false, isTapped:false, root: SelectCardEffect.Root.DigivolutionCards, activateETB:true)`를 호출 — AS-IS 시그니처와 동일한 `ICardEffect activateClass` 오버로드.

TO-BE는 그 오버로드를 쓰지 않고 저수준 `sourceCard`-오버로드를 직접 호출:
```csharp
await PlayPermanentCards(
    cardSources: selectedCards,
    sourceCard: cardSource,
    payCost: false, isTapped: false,
    root: ChoiceZone.DigivolutionCards,
    activateETB: true).ConfigureAwait(false);
```
`CardEffectCommons.cs`의 두 오버로드를 대조하면:
- `activateClass`-오버로드(`CardEffectCommons.cs:4935`)는 재생 직전 `CanPlayAsNewPermanent(...) && cardSource.CanEnterField(activateClass)`로 다시 필터링한 뒤 저수준 오버로드에 위임.
- `sourceCard`-저수준 오버로드(`CardEffectCommons.cs:1795`)는 `CanPlayAsNewPermanent(cs, payCost, cardEffect: **null**, ...)`만 재확인 — `CanEnterField`는 아예 호출 안 함.

`CardSource.CanEnterField(ICardEffect?)`(`CardSource.cs:419`)는 `ICanNotPutFieldEffect`(예: "상대 디지몬은 필드에 낼 수 없다") 스캔 — 실질 게임 룰 게이트다. 선택 단계 조건 `CanSelectDecodeSourceCardCondition`(Decode.cs 자체)도 `CanEnterField`를 검사하지 않으므로, TO-BE `DecodeProcess`는 선택~재생 사이 어느 지점에서도 이 게이트를 통과시키지 않는다. 같은 파트의 `Partition.cs`/`BlastDigivolution.cs`는 `activateClass:`-오버로드(정확한 AS-IS 시그니처)를 그대로 쓰고 있어 Decode.cs만 예외적으로 이 게이트를 누락한 것으로 확인됨.

### P3. `Script/DeckBuildingRule.cs` — CoreRule/HIGH 태그인데 완전 미포팅

TO-BE는 7줄 "TODO: Skeleton only" 무근거 스텁(`Category: CoreRule`, `Priority: HIGH`). AS-IS(291줄)는 실제 덱 합법성 로직: `IsValidDeck`, `ModifiedDeckData`(밴리스트 정리), `MaxCount_BanList`, `CanAddCard`(장수 제한 + 밴 페어 검사), `BanList`/`Restrictions`/`Pair`/`CardRestriction`/`CardLimitCount`/`BannedPair` 데이터 홀더. 전부 순수 C# 로직(Unity 의존은 `ContinuousController.instance.useBanlist`/`BanList` 싱글턴 read뿐, 파라미터로 치환 가능).

`grep -rln "CardRestriction|BannedPair|CardLimitCount|IsValidDeck|CanAddCard" src/HeadlessDCGO.Engine/` → 0건. 엔진 어디에도 덱 구성 합법성(장당 매수 제한, 밴 페어) 검증이 존재하지 않음 — 같은 파트의 `SelectDeck.cs`(순수 Unity UI, 근거 있는 스킵 사유 보유)와 달리 `DeckBuildingRule.cs`는 포터블 로직이 있음에도 무근거로 방치됨. CoreRule/HIGH 태그 대비 실질 갭.

---

## 경미 소견

### M1. `Script/PermanentDetail.cs`, `Script/CheckCardPanel.cs`, `Script/Effect Examples/Link_Examples.cs` — 무근거 "TODO: Skeleton only" 스텁

세 파일 모두 실행 안 되는 raw TODO 스텁. 단, AS-IS 실독 결과 셋 다 안전하게 스킵 가능:
- `PermanentDetail.cs`/`CheckCardPanel.cs`: TextMeshPro/ScrollRect/DOTween/EventTrigger 기반 순수 Unity UI 패널(효과 텍스트 조립·카드 상세 오픈). 읽는 상태(`permanent.Has*`, `EffectList` 등)는 이미 엔진에 존재.
- `Link_Examples.cs`: `DCGO.CardEffects.Examples.Link_Examples` — 카드 저작자용 예시/템플릿 파일(`<author>Mike Bunch</author>` 주석, 실제 카드 번호 아님, DB 미등록).

같은 파트의 `SelectDeck.cs`는 동일 성격(순수 UI)임에도 근거 있는 판단 주석("(SKEL-Exhaust) RECLASSIFIED...")을 달았고, `SelectJogressEffect.cs`도 마찬가지. 이 세 파일만 근거 주석 없이 raw TODO로 방치된 것은 감사 절차상 일관성 결여이나, 내용 자체는 무해함(기능적 결함 아님).

---

## 정합 확인 파일 (문제 없음, 28건)

| # | 파일 | 판단 근거 |
|---|---|---|
| 2 | `Script/SelectJogressEffect.cs` | UI 플로우 오케스트레이터; jogress 조건 매칭 로직은 `DNADigivolvePermanentsIntoHandOrTrashCard`(CardEffectCommons)에 대체 이관 확인. 근거 있는 스킵. |
| 3 | `Script/SelectCountEffect.cs` | AS-IS `Activate` 후보 구성·단일후보 자동해소·ChoiceProvider 위임까지 verbatim 구조 매칭. |
| 5 | `Script/CheckEffectDisabledClass.cs` | 바이트 단위 verbatim(트리 평가 로직 100% 동일). |
| 6 | `Script/CEntity_Base.cs` | ScriptableObject 데이터 홀더; TO-BE는 필요분(`CardColor` enum)만 이식하는 "grown as ports require" 정책. `CardKind`는 `CardSource.IsDigimon/IsTamer/IsOption/IsDigiEgg` 불리언으로 등가 이식 확인(`CardSource.cs:326-332`). `EvoCost`는 `PrintedEvoCost` record로 재설계·문서화됨(`CardSource.cs:2497`). |
| 7 | `Script/UserSelectionManager.cs` | ChoiceProvider substrate 번역, 문서화 우수, 로직 등가. |
| 8 | `Script/PermanentEffectFactory.cs` | P1 결함 제외 나머지(`DigimonEffectImmunity`/`OptionEffectImmunity`/`DeleteSelfEffect`/`AddDetailClass`/`CanNotSwitchAttackTargetEffect`) 정합. |
| 9 | `Script/CardEffectCommons/KeyWordEffects/Partition.cs` | verbatim, `CanTriggerPartition`/`CanActivatePartition`/`PartitionClass` 로직 동일. |
| 10 | `Script/CardEffectCommons/KeyWordEffects/Retaliation.cs` | verbatim, null-safety만 추가. |
| 11 | `Script/CardEffectFactory/KeyWordEffects/Partition.cs` | verbatim(`PartitionCondition` 문자열-색상화는 프로젝트 전역 확립 컨벤션). |
| 12 | `Script/CardEffectFactory/ChangePlayCost.cs` | 바이트 단위 동일. |
| 13 | `Script/DeckInfoPanel.cs` | 순수 Unity UI(Animator/InputField/ScrollRect); M1 소견(무근거 주석)이나 무해. |
| 14 | `Script/GameContext.cs` | 카드-이펙트가 실제 참조하는 접근자만 노출(문서화된 스코프). 누락 멤버(`FirstPlayer`/`PlayerFromID`/`SwitchTurnPlayer`/`DoSwitchTurnPlayer`/`SetPlayerID`)는 AS-IS에서도 `TurnStateMachine.cs`/`CardObjectController.cs`(턴-엔진 내부)만 참조, 카드 이펙트 호출 0건 확인. |
| 15 | `Script/SelectDeck.cs` | 순수 Unity 덱-선택 화면; 근거 있는 스킵 주석. |
| 16 | `Script/CardEffectFactory/KeyWordEffects/Link.cs` | verbatim async 번역. |
| 17 | `Script/CardEffectCommons/KeyWordEffects/Execute.cs` | verbatim. |
| 18 | `Script/CardEffectCommons/KeyWordEffects/Overclock.cs` | verbatim. |
| 19 | `Script/CardEffectFactory/ChangeDP.cs` | verbatim(어댑테이션 문서화). |
| 21 | `Script/DeckBuildingRule.cs` | → P3 참조(문제). |
| 22 | `Script/CardEffectCommons/KeyWordEffects/Vortex.cs` | verbatim, `GameContext` substrate 경유 확인. |
| 23 | `Script/CardEffectFactory/ChangeLinkMax.cs` | verbatim. |
| 24 | `Script/CardEffectFactory/KeyWordEffects/Scapegoat.cs` | verbatim. |
| 25 | `Script/CardEffectFactory/ChangeDigivolutionCost.cs` | 바이트 단위 동일. |
| 26 | `Script/CardEffectFactory/KeyWordEffects/BlastDigivolution.cs` | verbatim, STOP 해소 이력 문서화(RD-P6C2-11). |
| 27 | `Script/CardEffectCommons/KeyWordEffects/Blitz.cs` | verbatim, 잔여 design item(RD-W3-7) 명시적 문서화. |
| 28 | `Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/CanNotUnsuspend.cs` | verbatim, 면역 게이트(`CanNotBeAffected`)를 read-time에 정확히 보존(P1과 대비되는 모범 사례). |
| 29 | `Script/CardEffectFactory/KeyWordEffects/Alliance.cs` | verbatim. |
| 30 | `Script/CardEffectCommons/KeyWordEffects/Decode.cs` | → P2 참조(문제); `CanActivateDecode`/`GainDecode`/게이트 구조 자체는 정합, `DecodeProcess`의 재생 호출 1건만 결함. |
| 31 | `Script/CardEffectCommons/TrashLinkedCards.cs` | AS-IS 자체에서도 dead(카드 DB 미참조, 실증적 grep 근거 제시)임을 재검증 후 STOP 가드 — 정당한 판단. |
| 32 | `Script/CardEffectCommons/CanUseEffects/WhenDeleteOpponentDigimon.cs` | 바이트 단위 완전 동일. |

(4/20 = `Script/Effect Examples/Link_Examples.cs` → M1 참조.)
