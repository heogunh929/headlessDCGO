# AS-IS↔TO-BE 매칭 검증 — part 02

manifest: `docs/audit/manifest/both_part_02.txt`
AS-IS root: `DCGO/Assets/Scripts/<relpath>`
TO-BE root: `src/HeadlessDCGO.Engine/Assets/Scripts/<relpath>`

전문 실독(양측) + AS-IS 전체 public/private 멤버 261개 목록화 → TO-BE 161개 목록과 전수 대조. 판단 기준: AS-IS 심볼 소실/오배선/시맨틱 편차를 실소스 대조로 직접 확인(기존 코드주석·감사판정은 근거로 사용하지 않되, 코드주석이 가리키는 AS-IS 줄번호는 원문 재확인 용도로만 사용).

---

## 1. Script/CardSource.cs

- AS-IS: 4357줄. `MonoBehaviour`. 카드 인스턴스 1장의 색상/이름/특성/코스트/진화조건/키워드능력 판정을 담당하는 전 카드효과의 최상위 공용 API 표면. 하위에 `JogressCondition`/`JogressConditionElement`/`DigiXrosCondition`/`DigiXrosConditionElement`/`BurstDigivolutionCondition`/`LinkCondition`/`AppFusionCondition`/`AssemblyCondition`/`AssemblyConditionElement` 9개 보조 클래스 포함.
- TO-BE: 2839줄. `sealed class CardSource`(순수 C#, `EngineContext`/`HeadlessEntityId` 기반 view 객체 — 매 접근마다 재구성됨, `Equals`/`GetHashCode`/`==`로 인스턴스 동등성 별도 구현). 보조 클래스 9개 전부 `sealed class`로 구조 일치 존재 확인.

코스트 파이프라인(`PayingCost`/`GetPayingCostWithBaseCost`/`GetChangedCostItselef`/`GetChangedPayingCost`, DigiXros/Assembly 코스트 감면), 진화코스트 엔진(`EvoCosts`/`CostList`/`CanEvolve`/`AddedDigivolutionCosts`), 색상 폴드(`BaseCardColors`/`CardColors`/`BaseDualCardColors`/`DualCardColors`/`MatchColorRequirement`/`IgnoreColorConditionActive`), 필드배치 게이트(`CanPlayCardTargetFrame`/`CanEnterField`/`CanPlayFromHandDuringMainPhase`)는 AS-IS 줄번호를 인용한 상세 주석과 함께 라인 대 라인으로 대조했으며 로직 소실 없음을 확인(리네임/대체는 모두 실사용처 근거 확인, 아래 "정합" 항목 참조). 아래는 실질 결손으로 판단한 항목.

### 문제 1 — `HasCardColor` 기본(no-flag) 호출의 시맨틱 축소 (DUAL 카드에서 실동작 편차)

- AS-IS(:1565-1578):
  ```
  public bool HasCardColor(CardColor cardColor, bool isOptionOnly = false, bool isDigimonOnly = false)
  {
      if (isOptionOnly && isDigimonOnly) return false;
      if (isOptionOnly) return OptionCardColors.Contains(cardColor);
      else if (isDigimonOnly) return DigimonCardColors.Contains(cardColor);
      return AllCardColors.Contains(cardColor); //check entire card
  }
  ```
  `AllCardColors`(:1555-1562)는 `CardColors.Concat(DualCardColors).ToList()` — 즉 플래그 없이 호출하면 Digimon 면 색상과 Option(듀얼) 면 색상의 **합집합**을 검사한다.
- TO-BE(:1638): `public bool HasCardColor(string color) => CardColors.Any(c => string.Equals(c, color, StringComparison.OrdinalIgnoreCase));` — `CardColors`만 검사, `DualCardColors`는 검사하지 않는다. `AllCardColors`에 대응하는 프로퍼티/로직이 TO-BE `CardSource.cs`에 없음(`grep -rn "\.AllCardColors\b"` AS-IS/TO-BE 둘 다 외부 호출처 0건 — 오직 `HasCardColor` 내부에서만 쓰였던 값이라 대체 경로도 없음).
- `HasDigimonColor`/`HasOptionColor`(플래그가 있던 오버로드의 후신)는 TO-BE에 정확히 이식되어 있음(`HasOptionColor`가 듀얼카드에서 `DualCardColors`를 읽는 것까지 확인, :1914-1922) — 즉 "플래그 지정 호출"의 시맨틱은 보존됐지만, **기본(플래그 없음) 호출만** 합집합 검사에서 단일 리스트 검사로 축소됨.
- 실사용 규모: AS-IS `.HasCardColor(` 호출 590건 중 절대다수가 플래그 없는 기본 호출(`HasDigimonColor`/`HasOptionColor` 우회 경로가 아닌 직접 호출). 듀얼 카드(Digimon 겸 Option, 양면 색상이 다른 카드)를 대상으로 기본 `HasCardColor(색상)`을 호출하는 카드효과가 존재하면, AS-IS는 듀얼 면 색상 일치 시 true를 반환하지만 TO-BE는 false를 반환 — 조건 판정 오동작.
- TO-BE 코드에는 이 축소에 대한 AS-IS 줄번호 인용/설계 근거 주석이 전혀 없음(같은 파일의 다른 모든 편차는 예외 없이 인용 주석이 붙어 있는 것과 대조적) — 의도된 설계 결정이 아니라 이식 누락으로 판단.

### 문제 2 — CardSource-스코프 키워드/효과존재 판정 프로퍼티 다수가 이관 없이 소실 (실사용처 존재, 추적표시 없음)

AS-IS `CardSource.cs`는 "이 카드 객체 자신의 EffectList"를 스캔하는 스코프의 다음 프로퍼티들을 정의한다(`Permanent.cs`의 동명 프로퍼티와는 스코프가 다름 — Permanent 쪽은 TopCard/스택 전체를 다루는 별도 정의로 실제로 존재를 확인함). 아래는 TO-BE `CardSource.cs` 및 엔진 전체(`grep -rn`)에 **정의는 물론 문자열 언급도 0건**이며, AS-IS 실사용처가 있고 해당 소비 카드가 아직 미포팅(스켈레톤) 상태로 남아있어 향후 포팅 시 즉시 막히는 항목:

| 심볼 | AS-IS 위치 | 실사용 카드(직접 `cardSource.X`/`card.X` 호출) |
|---|---|---|
| `HasInheritedEffect` | :2685-2701 | BT18 시리즈(031/091/081/076/063/011/010/048/090/097/096/095), AD1(015/002), BT17(020/094) 등 15개+ 파일에서 반복 사용 |
| `HasUseCost` | :3500 (`_cEntity_Base.HasUseCost`) | BT6(065/017/112), EX4_030, BT25(049/090), EX7_013 등 20개 파일 |
| `HasDigisorption` | :2640-2645 | BT5(100/049) |
| `HasDigiBurst` | :2544-2562 | BT5(102/057), BT4(107/004/095/051) 등 8개 파일 |
| `HasBlocker`(CardSource 스코프) | :2535-2538 | `LM_014.cs:28` `if(cardSource.HasBlocker \|\| cardSource.IsTamer)` — 필드 밖(덱 안) 카드 판정이라 `Permanent.HasBlocker`로 대체 불가 |
| `HasBlitz`/`HasFortitude`/`HasRetaliation`(CardSource 스코프) | :2651-2680 | BT5_009, EX5(042/035), EX1_063 — `permanent.HasBlitz` 등과는 별개로 `cardSource.X` 직접 호출 |
| `HasXAntiBodyName` | :1671 | EX5(015/023) |
| `EqualsCardNameDigiXros` | :2243-2268 | BT20_058(x3), BT19(068/102 x2), BT21(027 x2/021) — DigiXros 소환조건 판정 |
| `HasLightFangNightClawTraits` / `HasLightFangOrNightClawTraits` | :2069-2093(둘 다 `ContainsTraits("Light Fang")\|\|ContainsTraits("Night Claw")`와 사실상 동일 로직의 중복 프로퍼티) | 전자 BT16_020, 후자 BT22 시리즈(077/072/069/073/102), P_191 등 11개 파일 |
| `HasPulsemonText` | :2187 (`HasText("Pulsemon")`) | BT20_081, BT17(034/086/098 x2), P_147, BT16_023 등 8개 파일 |

- 위 소비 카드 21개 전부 `src/HeadlessDCGO.Engine`에서 확인한 결과 **전부 7줄짜리 미포팅 스켈레톤**(`// TODO: Skeleton only.`)이라 현재 시점에 컴파일/동작 파괴는 없음. 그러나 앞서 part03 검증에서도 나타난 패턴과 동일하게, 트레이트 계열 프로퍼티(문제3)처럼 "미러 미존재" 주석과 함께 개별 인라인되는 대응조차 이 심볼들에는 전혀 없음(엔진 전체 grep 0건) — 즉 소실 사실 자체가 어디에도 기록되어 있지 않음. `HasBlocker`/`HasBlitz`/`HasFortitude`/`HasRetaliation`/`HasOnDeletionEffect`는 `Permanent.cs`에 동명이 존재하지만(스코프가 다름 — Permanent판은 "필드에 있는 퍼머넌트의 TopCard 기준", CardSource판은 "이 카드 객체 자신" 기준. AS-IS 두 정의를 나란히 읽고 스코프 차이를 확인함, Permanent.cs:2397/2778/2843/2867/3155 vs CardSource.cs:2535/2663/2674/2651/2610), LM_014처럼 필드 밖 카드에 호출하는 실사용처가 있어 Permanent판으로 대체 불가능.

### 문제 3 — 이름/특성 계열(trait-family) 공용 프로퍼티 약 50개가 CardSource에서 전삭, 산발적 인라인 재구현으로 대체

AS-IS `CardSource.cs`는 `HasXXXTraits`/`HasXXXName` 형태의 카드군(archetype) 판정 프로퍼티를 다수 정의한다(예: `HasRoyalKnightTraits`, `HasAdventureTraits`, `HasAquaTraits`, `HasBirdTraits`, `HasHybridTenWarriorsTraits` 등). 대표 표본의 AS-IS 실사용 파일 수:

| 심볼 | AS-IS 사용 파일 수 |
|---|---|
| `HasRoyalKnightTraits` | 43 |
| `HasAdventureTraits` | 33 |
| `HasAquaTraits` | 27 |
| `HasBirdTraits` | 15 |
| `HasHybridTenWarriorsTraits` | 15 |
| `HasSeekersTraits` | 14 |
| `HasPlantTraits` | 13 |
| `HasSocTraits` | 12 |
| `HasLiberatorTraits` | 12 |
| `HasBeastTraits` | 11 |
| `HasFairyTraits` | 10 |
| (그 외 `HasAngelTraits`/`HasAngelTraitRestrictive`/`HasDragonTraits`/`HasHudieTraits`/`HasRoyalBaseTraits`/`HasUndeadTraits`/`HasGhostTraits`/`HasDarkAnimalTraits` 등 약 40개 추가, 각 2~9개 파일) | |

이들 전부 TO-BE `CardSource.cs`에 대응 프로퍼티가 없음(정의 0건). 실사용처를 이미 포팅한 소수 사례를 직접 확인한 결과, 일관되게 "미러 CardSource엔 해당 표면 없음"을 헤더 주석에 명시하고 **호출 카드 파일 내부에 로컬 함수/인라인으로 AS-IS 프로퍼티 바디를 그대로 복제**하는 패턴을 취함:
- `BT15_082.cs:97` — `bool HasAvianBeastAnimalTraits(CardSource cardSource)` 로컬 함수로 AS-IS :1930-1948 바디 인라인.
- `BT25_039.cs:206-207` — `HasShamanTraits`/`HasIliadTraits` 둘 다 로컬 함수로 인라인(`EqualsTraits("Shaman")`/`EqualsTraits("Iliad")`).
- `BT25_043.cs`, `P_223.cs`, `P_198.cs`, `BT15_083.cs`(`HasGarurumonName`)도 동일 패턴.

기능적으로는 카드 1장 단위로는 등가(같은 `ContainsTraits`/`EqualsTraits` 원시 연산 재조립)이지만, **AS-IS에서 공용 프로퍼티 1개였던 것이 소비처마다 중복 재구현되는 구조적 편차**이며, 대표 심볼(`HasRoyalKnightTraits` 43곳, `HasAdventureTraits` 33곳 등)의 나머지 대다수 소비 카드는 아직 미포팅 스켈레톤이라 이 중복화가 앞으로 수십~수백 파일에 걸쳐 반복될 예정. `HasLightFangNightClawTraits`/`HasLightFangOrNightClawTraits`(문제2)처럼 인라인 시도조차 없는 경우도 섞여있어 일관성이 없음.

### 문제 4 — `ChangedLocationTime`/`SetChangedLocationTime` 소실 (연속효과 타임스탬프 게이트)

- AS-IS(:120-135): 카드가 필드 위치를 바꾼 시각(진화/탈진화로 TopCard가 바뀐 시점 등)을 기록하는 `DateTime` 필드+세터. `CardController.cs`(:4897/:5074/:5940) 등에서 TopCard가 바뀔 때마다 호출, `BT25_104.cs:230`에서 `changeBaseDPClass.SetActivatedTime(card.Owner.TurnStartTime, card.ChangedLocationTime)`으로 "이 턴 안에 이 카드가 위치를 바꾼 이후부터"라는 연속효과 시작조건에 사용.
- TO-BE: `CardSource.cs`에 대응 멤버 없음. `CardController.cs`(:884/:974/:1122/:2779/:2849) 5곳에서 "design item MIG3-LOCATIONTIME(no headless analog)"로 스코프 밖 선언하며 호출부를 스트립. `BT25_104.cs`(미포팅 스켈레톤)의 포팅 계획 주석에도 "두 타임스탬프 소스 멤버 미이관"이 명시되어 있음.
- 다른 소실 항목(문제2/3)과 달리 이 항목은 **design item으로 추적되고 있어 은닉된 결손은 아님**. 다만 실제 소비 카드(`BT25_104`)가 여전히 미포팅이라 언제 이 design item이 실제로 막히는지는 그 카드 포팅 시점에 결정됨 — 결손 사실 자체는 참고용으로 남김.

### 검증 완료 — 정합으로 판단한 리네임/구조변경 (근거 포함)

- `CardID`(:3454, `_cEntity_Base.CardID`) → `CardNumber`(:1166, `Definition?.CardNumber`): 엔진 전체 33개 파일이 `CardNumber`를 사용 중이며, `BT15_102.cs:24`/`BT21_030.cs:51` 등에서 "AS-IS `CardSource.CardID`(=`CEntity_Base.CardID`) → 미러 `CardNumber`"로 실제 AS-IS 줄번호를 인용해 명시적으로 매핑한 흔적을 확인 — 일관된 전역 리네임.
- `IsLevel2`/`IsLevel3`/`IsLevel4`/`IsLevel5`/`IsLevel6`(:2917-2946) → `IsLevel(int level) => Level == level`(:1637): 다수 소비처(`EX8_059.cs`, `EX9_074.cs`, `BT24_062.cs`, `BT23_021.cs` 등)에서 "`IsLevel3` → `.IsLevel(3)`(CardSource.cs:xxx)" 형태로 AS-IS 줄번호 인용 확인 — 의미 동일한 파라미터화, 소실 아님.
- `IsACE`(:3553)/`OverflowMemory`(:3559)가 CardSource 표면에서 빠지고 `AceOverflowGate.OverflowFor`(내부)로 흡수: AS-IS `.IsACE` 실사용처 전수 확인(`BT18_042`×2/`BT17_098`/`BT24_093`/`CardController.cs:5839`) 결과 **모든 호출이 예외 없이 `AceOverflowClass(...).Overflow()` 코루틴과 즉시 결합된 가드**이거나 `CardController.cs:5839`의 오버플로우 계산 필터 자체였음 — 별도 용도의 `IsACE` 단독 사용은 AS-IS에도 없어 내부 게이트로의 흡수가 행동 등가.
- `SetFace`/`SetReverse`/`IsFlipped`(:56-97): 뮤터블 필드가 `SecurityFaceState`(메타데이터 기반 저장소) + `SelectCardEffect.SetFaceMirror`(정적 헬퍼)로 재배치됨을 교차 파일 추적으로 확인 — Unity MonoBehaviour 필드에서 저장소 기반으로의 substrate 이전이며 판정 로직 자체는 동일.
- `IsToken`/`SetIsToken`(:2464-2466) → 메타데이터(`isToken`) 기반 게터(:1184-1185)로 재배치 — 세터는 카드/토큰 생성 시점(이 파일 범위 밖)에서 메타데이터를 쓰는 방식으로 이전된 것으로 판단(본 파일 범위 안에서는 게터 시맨틱만 확인 대상이며 일치).
- `PhotonView`/`SetUpCardIndex`/`CardIndex`/`ShowingHandCard`/`SetShowingHandCard`/`CardSprite`/`GetCardSprite`/`SetBaseData`/`Init()`: AS-IS 실사용처가 전부 Photon 네트워킹/Unity GameObject/UI 표시(`CardObjectController.cs`, `CardInfo.cs` 등)에 한정됨을 확인 — 프로젝트의 "Headless/=substrate만, 미러 층=게임 로직" 방침(AS-IS mirror migration decision)과 일치하는 정당한 배제.
- `InheritedEffectDiscription_ENG/_JPN`/`BaseJPNCardNameFromEntity`/`LinkEffectDiscription`/`CardEntityIndex`: AS-IS 실사용처 전수 확인 결과 UI 텍스트 표시(`CardInfo.cs`) 또는 카드데이터 로더(`OfficialCardListUtility.cs`)/UI 정렬(`DeckData.cs`) 전용 — 게임 로직 소비처 0건, 정당한 배제.
- `BaseDualCardColorsFromEntity`/`ContainsCardNameDigiXros`: AS-IS 자체에서도 `CardSource.cs` 내부 호출 외 외부 실사용처 0건(죽은 표면) — 배제로 인한 실질 손실 없음. 단 `BaseCardColorsFromEntity`(:356)는 UI 다수 소비처 외에 **`BT18_085.cs:86`(`cardColors.AddRange(cardSource.BaseCardColorsFromEntity)`) 게임 로직 소비처 1건**을 확인했으나, 해당 카드도 미포팅 스켈레톤이라 이관 시점에 재확인 필요(현재는 미영향, 참고 기록).
- `CardKinds`(:3547, `List<CardKind>`) → `IsDigimon`/`IsTamer`/`IsDigiEgg`/`IsOption` 개별 플래그(`CardRecord.IsCardType` 기반): `IsPermanent`(AS-IS `CEntity_Base.cs:238`, `cardKind.Contains(Digimon)||Contains(Tamer)||Contains(DigiEgg)`)의 TO-BE 인라인(`IsDigimon||IsTamer||IsDigiEgg`, :920)이 원 수식과 정확히 일치함을 원문 대조로 확인 — 표현형만 바뀐 정당한 변경.
- 색상 폴드(`BaseCardColors`/`CardColors`/`BaseDualCardColors`/`DualCardColors`): `List<CardColor>` enum → `IReadOnlyList<string>`로 표현형이 바뀌었으나 `ToCardColorList`/`ToColorNames` 무손실 변환 헬퍼로 왕복 가능함을 확인, AS-IS의 "자기 자신(필드 밖일 때만) → 전체 필드 퍼머넌트" 2단계 스캔 순서도 `FoldColorEffects`에서 동일하게 재현됨 — 정합.

**판정: 부분 정합 — 실질 결손 4건.** 그 중 문제1(`HasCardColor` 기본호출 합집합 소실)은 현재도 호출 가능한 표면의 시맨틱 버그로 가장 위험도가 높고, 문제2/3(키워드·이름·특성 프로퍼티 약 60개 소실)은 현재는 미영향(소비 카드 전부 미포팅)이나 향후 대량 포팅 시 매 카드마다 재발할 구조적 부채이며 절반 이상(문제2)은 추적 흔적조차 없음, 문제4는 design item으로 추적 중인 참고 항목.

---

## 요약

| 파일 | 판정 | 발견 |
|---|---|---|
| Script/CardSource.cs | 부분 정합 | (1) `HasCardColor(string)` 기본 호출이 AS-IS `AllCardColors`(CardColors∪DualCardColors) 대신 `CardColors`만 검사 — 듀얼카드 시맨틱 버그, 추적주석 없음. (2) `HasInheritedEffect`/`HasUseCost`/`HasDigisorption`/`HasDigiBurst`/CardSource-스코프 `HasBlocker`·`HasBlitz`·`HasFortitude`·`HasRetaliation`/`HasXAntiBodyName`/`EqualsCardNameDigiXros`/`HasLightFangNightClawTraits`·`HasLightFangOrNightClawTraits`/`HasPulsemonText` — 실사용 카드 21개+ 존재(전부 현재 미포팅 스켈레톤), 엔진 전체에 정의·인라인 대체 0건, 추적 표시 없음. (3) `HasRoyalKnightTraits`(43곳)·`HasAdventureTraits`(33곳)·`HasAquaTraits`(27곳) 등 이름/특성 계열 공용 프로퍼티 약 50개 전삭 — 이미 포팅된 소수 소비처는 카드별 로컬 인라인으로 개별 복제 중(기능 등가·구조 중복), 나머지 대다수는 미포팅이라 향후 반복 예정. (4) `ChangedLocationTime`/`SetChangedLocationTime` 소실 — design item MIG3-LOCATIONTIME으로 추적 중, 실소비카드(BT25_104) 미포팅이라 현재 미영향. |
