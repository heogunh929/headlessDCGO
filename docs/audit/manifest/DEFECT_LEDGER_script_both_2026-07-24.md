# Script BOTH 전수 감사 결함 원장 — 2026-07-24 (HEAD e5ea69d7)

전수: 344쌍 (MATCH-1:1 108 / MATCH-TRANSLATED 178 / BLOCKED 48 / **DEVIATION 10**).
병합 원장=`verdicts_script_both_ALL.csv`. 아래는 DEVIATION 10파일의 **파일별·항목별** 결함 전수(뭉뚱그림 없음).
각 항목: 종류 · AS-IS 앵커 · TO-BE 상태 · 소비자(live/latent) · 심각도.

---

## D1. `Script/CardEffectCommons.cs` — 행동차이 1 + GAP 1 + sig 1

| # | 종류 | AS-IS | TO-BE | 소비/심각도 |
|---|---|---|---|---|
| D1-a | **행동차이** | `DigivolveIntoHandOrTrashCard` 후보필터=`CanPlayCardTargetFrame(PayCost)` 코스트-지불가능성 포함 (:795-821) | `DigivolveIntoZoneCoreAsync.CanSelect`(:1921)는 진화요건만 확인, 코스트는 `MemoryController.CanPay`(:1997)로 지연 | **지불불가-합법 카드가 TO-BE에선 선택지 제시**(AS-IS는 필터). Execution 변형(:2705) 동일. **live 가능·최고 심각** |
| D1-b | GAP | `OptionSecurityEffect(card)` getter (:717) | commons 무대응(OptionMainEffect만 폴드 :4407) | BT18_098이 로컬 `OptionSecurityEffectOf`로 재구현("no mirror bridge"). 미재하우징 |
| D1-c | sig drift | `AddActivateMainOptionSecurityEffect`가 `effectDiscription` param 보유 (:723) | commons 시그니처(:4135)에서 param 드롭 | factory(:914)가 기본값 채움·전 live 콜러 생략 → **behavior-neutral**, 단 1:1 아님 |

## D2. `Script/Permanent.cs` — GAP 4 (전부 잠복)

| # | 종류 | AS-IS | TO-BE | 소비 |
|---|---|---|---|---|
| D2-a | GAP | `HandBounceEffect` ICardEffect 필드 (:3678) — 형제 Playing/Digivolving/PlaceOther는 포팅됨 | Permanent에 접근자 전무 | 실카드 4장(BT11_072·P_024 등) read-back — **전부 미포팅 스텁(잠복)** |
| D2-b | GAP | `LibraryBounceEffect` (:3682, 같은 bookkeeping 클러스터) | 접근자 전무 | 실카드 9장(BT22_007·EX10_052/055·BT11_072·P_214·P_024·EX11_031·BT23_043·ST22_10)+CardController DeckBounce writer — **잠복** |
| D2-c | GAP | `DPWhenSuspended` public int 필드 default 114514 (:1958) | **write-only** — SuspendPermanentsClass가 `DpWhenSuspendedKey` 쓰지만(CardController:1867) Permanent에 getter 없고 키를 아무도 안 읽음 | BT9_018 소비(미포팅 스텁) — **써놓고 못 읽음** |
| D2-d | GAP | `DigivolutionOrLinkCards` = `cardSources.Filter(c=>c!=TopCard)` (:892) | src 트리 전역 부재(0 occurrence) | BT25_085 소비(미포팅 스텁) |

## D3. `Script/CardSource.cs` — 미등재패턴 1 + design-item GAP 4

| # | 종류 | AS-IS | TO-BE | 비고 |
|---|---|---|---|---|
| D3-a | **미등재 substrate 패턴**(id-껍데기류) | 파생 boolean 편의 게터 ~90개: `Has*Traits`(Bird/Beast/Angel/Dragon/Appmon/CS/WG… L1713-4180)·`Has*Name`(Greymon/Garurumon/Impmon/Dramon/XAntiBody L1595-1673)·키워드flag(Blocker/DigiBurst/Blitz/Fortitude/Retaliation L2533-2683)·효과존재flag(OnPlay/OnDeletion/WhenDigivolving/Inherited L2608-2703) | CardSource에 멤버 전무(일부 keyword flag만 Permanent로 재하우징). 카드가 호출부에서 `EqualsTraits/ContainsTraits` **인라인**(BT22_035·BT23_081 등 수십장 주석) | 행동 각 사이트 정확하나 **AS-IS-이름 멤버 표면 부재·canonical §9.1 미등재** → 등재제에 올리면 정당화 |
| D3-b | design-item GAP | `ChangedLocationTime`/`SetChangedLocationTime` (MIG3-LOCATIONTIME) | headless analog 없음 | AS-IS live(BT25_104·ArmorPurge) — BT25_104 미러가 timestamp 비교 미포팅 명시 |
| D3-c | **행동차이(잠재)** | `GetCostItself`(:769) cost-fold 적용 값 | TO-BE(:1346)는 unfolded `BasePlayCostFromEntity` 읽음 | 자체문서화 "documented reduction" — 현재 folded 소비자 0이라 behavior-neutral, 단 named 멤버 **비등가** |
| D3-d | design-item GAP | `IsBeingRevealed`(RD-P6C3-A2) writer | 미포팅(getter default false) | 자체문서화 :2267 |
| D3-e | design-item GAP | `PermanentJustBeforeRemoveField`(RD-P6C3-A3) writer | 미포팅(getter default null) | 자체문서화 :2290 |

## D4. `Script/CardController.cs` — 구조 GAP 6 (전부 재하우징 캐리어 확인)

| # | 종류 | AS-IS 클래스 | 재하우징 캐리어(문서화) |
|---|---|---|---|
| D4-a | 구조 GAP | `HatchDigiEggClass` (:1056-1099) | TurnStateMachine `ZoneMover.HatchDigitamaAsync`(:365) "P2a mapping" |
| D4-b | 구조 GAP | `DeckBottomBounceClass` (:2271-2436) | SelectPermanentEffect `Mode.PutLibraryBottom` sink(:542) |
| D4-c | 구조 GAP | `DeckTopBounceClass` (:2437-2602) | SelectPermanentEffect `Mode.PutLibraryTop` sink |
| D4-d | 구조 GAP | `HandBounceClaass` (:2603-2837) | SelectPermanentEffect `Mode.Bounce` sink |
| D4-e | 구조 GAP | `IPutSecurityPermanent` (:3503-3647) | SelectPermanentEffect `Mode.PutSecurityBottom/Top` sink(:580) |
| D4-f | 구조 GAP | `ISecurityCheck` (:3880-4234) | Headless/Runtime/SecurityResolver.cs(858줄·16테스트) — mig-goal3 "임시 거처"(2026-07-13 사용자 승인) = **게임로직이 Assets 미러가 아닌 Headless substrate에 거주** |
| — | 참고(非결함) | 29/35 클래스 존재·고충실 MATCH-TRANSLATED(PlayCard/PlayPermanent/IBattle/DestroyPermanents 등, quirk 보존) | 非-canonical adaptation 4종은 파일-scope 문서화(emission-ownership seam·AI브랜치 strip·jogress frame·IMassDegen null-guard) |

## D5. `Script/SelectCardEffect.cs` — 발명 잔재 1 (dead)

| # | 종류 | 위치 | 상태 |
|---|---|---|---|
| D5-a | 발명(dead) | TOBE:52-200 `BuildRequest/BuildMutations/Apply/BuildMutation/Mutation/PlayMutation/RootZone` | AS-IS 무대응·레포 전역 호출 0(완전 dead). 라이브 경로(SetUp/Activate:202-936)는 EffectMutation 인라인 생성(537/599/753)으로 이 블록 미경유. **구 F-2.2/F-2.4 id-flip 잔존** = flip 캠페인이 놓친 사문 |
| D5-b | stale 문서 | 343-349 주석 "RD-W4-1 PlayForCost STOP" | Activate(646-729)가 reduce/fixed-cost 완전 구현 — 문서-코드 불일치(기능결함 아님) |

## D6. `Script/SelectPermanentEffect.cs` — 발명 잔재 1 (dead)

| # | 종류 | 위치 | 상태 |
|---|---|---|---|
| D6-a | 발명(dead) | TOBE:117-123 `IsValidSelection`·129-160 `BuildRequest(IZoneStateReader,players)` | AS-IS 무대응·호출 0(완전 dead). D5와 동형 구 CV-A2/F-2 id-flip 잔존. **주의: 같은 파일 BuildMutations/Apply/Mutation/SecurityMutation(184-292)은 ApplyAsIsModeBatchAsync가 실호출하는 라이브 — IsValidSelection/BuildRequest만 dead** |

## D7. `Script/CardEffectCommons/GameContextDeterminarion.cs` — GAP 1 + minor 1

| # | 종류 | AS-IS | TO-BE | 소비 |
|---|---|---|---|---|
| D7-a | GAP | `OwnerHas1OrLessTamers`(:858, static bool, owner 배틀에어리어 Tamer≤1) | src 전역 정의 0 | 라이브 AS-IS 콜러 4장(BT22_030·EX10_017·BT23_016·BT23_042) — **전부 미포팅(잠복)**. 형제(HasNoElement·MatchConditionPermanentCount)는 선제 재하우징됐는데 이것만 드롭 |
| D7-b | minor | `TurnOwnershipHelpers.IsOwner/IsOpponent`(TO-BE, AS-IS 앵커 없는 2메서드) | 콜러 0 | 이전 fold의 잔존(git상 TOBE_ONLY 커밋에 이미 존재) — 신규 발명 아님 |

## D8. `Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/ChangeSAttack.cs` — GAP 2 (계통)

| # | 종류 | AS-IS | TO-BE |
|---|---|---|---|
| D8-a | GAP | `InverteDigimonSAttack`(:124) | 전역 무대응 |
| D8-b | GAP | `InvertDigimonSAttack`(:176) | 전역 무대응 |
| — | 참고 | ChangeDigimonSAttack 2오버로드는 충실 브릿지 | |

## D9. `Script/CardEffectCommons/GiveEffect/GiveEffectToPlayer/ChangeSAttack.cs` — GAP 1 (계통)

| # | 종류 | AS-IS | TO-BE |
|---|---|---|---|
| D9-a | GAP | `InvertDigimonSAttackPlayerEffect` region(:73-131) | 미포팅(commons 래퍼 없음·factory `InvertSAttackStaticEffect`만 존재). TO-BE 자인 "left for a later batch" |
| — | 참고 | ChangeDigimonSAttackPlayerEffect만 포팅(→CardEffectCommons.cs:3058, 1:1) | |

> **D8+D9 = Invert-SAttack 계열 3메서드 계통적 미포팅**(permanent 2 + player 1).

## D10. `Script/CardEffectCommons/KeyWordEffects/Training.cs` — 행동차이 1 (저위험)

| # | 종류 | AS-IS | TO-BE | 심각도 |
|---|---|---|---|---|
| D10-a | 행동차이 | `card.Owner.LibraryCards[0]` 가드 없이 인덱싱(빈 라이브러리 시 IndexOutOfRange throw) | (:53-57) `if(Count>0)` 방어가드로 no-op | **저위험**: 양쪽 0-caller latent 래퍼(live=factory Training.cs), 헤더에 가드 명시 |

---

## 상환 분류 요약 (심각도순)

- **행동차이 live 가능(우선)**: D1-a(코스트필터)
- **행동차이 잠재/저위험**: D3-c(GetCostItself)·D10-a(Training 가드)
- **발명 잔재(dead) — 즉시삭제 가능**: D5-a·D6-a (flip 캠페인 놓침), D1-c/D5-b/D3-c sig·문서
- **GAP 잠복(⑦ 대량포팅 지뢰 — 소비 카드 미포팅)**: D1-b·D2-a/b/c/d·D3-b/d/e·D7-a·D8-a/b·D9-a
- **구조 GAP(재하우징 캐리어 존재·문서화)**: D4-a~f
- **미등재 패턴(등재제 편입 대상)**: D3-a (~90 게터), D4의 non-canonical adaptation, GameRandom raw-draw 폭(파트11)

> 대부분 잠복 = 소비 카드가 미포팅이라 스위트·퍼징·다이제스트 침묵. ⑦ 대량 포팅 착수 전 상환 필요.
