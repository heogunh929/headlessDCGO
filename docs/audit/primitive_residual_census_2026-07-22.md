# AS-IS 프리미티브 잔여량 전수표 (엔진 동결 계약 범위 산정)

- 작성: 2026-07-22, HEAD `b7b63213`. READ-ONLY census — 빌드/테스트 미실행.
- 목적: AS-IS(`DCGO/Assets/Scripts/Script/`)의 엔진-프리미티브 표면 전체를 열거하고, TO-BE 미러(`src/HeadlessDCGO.Engine/`)의 상태를 4분류(FULL/SKELETON/UNPORTED/DIVERGENT)로 판정한다. "엔진 동결 계약"의 잔여 스코프를 정하기 위한 기준 자료.
- 선행 자료: `docs/audit/asis_tobe_primitive_mapping.md`(2026-07-08, 182 심볼 카운트·PASS/PARTIAL/FAIL 3분류). **이 문서와 카운트 방법이 다르다** — 2026-07-08 감사는 "public 팩토리 메서드/함수" 단위로 세었고(예: `CardEffectFactory.cs` 한 파일 안의 41개 메서드), 이 문서는 과제 지시대로 **파일/인터페이스/kind-class 단위**로 센다(2026-07-08 이후 B군/R 시리즈로 단일-파일-다중-메서드couplings가 파일당 1-primitive 구조로 대부분 재편됨 — 아래 §5 참고). 두 숫자는 직접 비교 불가.
- **SPEC 변경 반영**: 최초 지시에 있던 "AS-IS 카드층 호출 수요 카운트"(§METHOD 4)는 코디네이터 지시로 제거됨 — 전 프리미티브가 전수 포팅 대상이라 빈도가 판단 신호가 아니기 때문. §2/§3은 경로 알파벳순으로만 정렬.

## 방법

1. AS-IS 측 카테고리 A~G 전 파일/인터페이스/kind-class를 `find`/`grep`으로 열거.
2. TO-BE 대응을 **동일 경로·동일 파일명** 우선으로 확인(미러 규약), 없으면 심볼 grep으로 재하우징 여부 확인.
3. 판정 근거: 파일 헤더 `// TODO: Skeleton only.` 패턴(순수 보일러플레이트, 본문 없음) → SKELETON; 파일/심볼 자체 부재 → UNPORTED; `NotSupportedException`으로 핵심 동작이 종단 차단되거나 은퇴/재하우징 주석이 있는 경우 → DIVERGENT; 그 외(실 로직 존재, AS-IS 대응 확인) → FULL.
4. FULL 판정이라도 파일 내부에 국소적 STOP(`NotSupportedException`, 특정 파라미터 조건부)이 있는 경우는 각주로 표기(파일 전체를 대표하는 차단이 아니므로 DIVERGENT로 격상하지 않음).

---

## §1 요약 카운트 표

| 카테고리 | AS-IS 유닛 | FULL | SKELETON | UNPORTED | DIVERGENT |
|---|---:|---:|---:|---:|---:|
| A. GiveEffect (GiveEffectToPermanent/GiveEffectToPlayer/…OrPlayer) | 33 | 28 | 4 | 1 | 0 |
| B. CardEffectFactory (top-level 27 + KeyWordEffects 32) | 59 | 57 | 0 | 0 | 2 |
| C. CardEffects/*.cs (kind-class) | 73 | 73 | 0 | 0 | 0 |
| D. CardEffectInterfaces.cs (인터페이스) | 74 | 74 | 0 | 0 | 0 |
| E. CardEffectCommons/KeyWordEffects/*.cs (키워드 본체) | 29 | 28 | 1 | 0 | 0 |
| F. CardEffectCommons 커먼즈 (top-level 11 + CanUseEffects 37 + MinMax_DP_Cost_Level 7) | 55 | 47 | 8 | 0 | 0 |
| G. Select*/choice 계열 | 17 | 10 | 3 | 4 | 0 |
| **합계** | **340** | **317 (93.2%)** | **16 (4.7%)** | **5 (1.5%)** | **2 (0.6%)** |

세부 하위분류:
- A는 `GiveEffectToPermanent`(18) + `GiveEffectToPlayer`(14) + `GiveEffectToPermanentOrPlayer.cs`(1).
- F는 `CardEffectCommons/*.cs` top-level 11개 + `CardEffectCommons/CanUseEffects/**`(트리거-가능 술어 계열, top-level 34 + `PermanentEnterField/` 3) 37개 + `CardEffectCommons/MinMax_DP_Cost_Level/**`(Cost/DP/Level/DigivolutionCards 4개 하위폴더) 7개. **주의**: `CanUseEffects`와 `MinMax_DP_Cost_Level`은 과제 원 카테고리 정의(F="최상위 커먼즈 파일들")에 명시되지 않았으나, `CardEffectCommons/` 직속 서브그룹으로서 실측 중 발견되어 F에 편입함 — 전수성을 위해 포함.
- G는 과제 지시대로 "이미 미러된 대형은 존재 확인만" 원칙을 적용했으나, 기계적 4분류를 위해 전 17개 파일에 동일 잣대(FULL/SKELETON/UNPORTED)를 적용했다. 그중 `SelectBattleDeck/SelectBattleMode/SelectCommand/SelectCommandPanel`(UNPORTED 4개)과 `SelectCardPanel/SelectDeck`(SKELETON 2개 중 2개)는 **UI 패널·게임-플로우 선택 화면**이지 카드-효과 프리미티브가 아니다 — §5에서 동결-계약 관점 재해석.

---

## §2 SKELETON 전수 리스트 (16, 경로 알파벳순)

모든 항목은 TO-BE 파일이 존재하나 본문이 `// TODO: Skeleton only. Port or implement deterministic .NET logic later.` 보일러플레이트뿐이다(실 로직 0줄).

| # | 경로 (src/HeadlessDCGO.Engine/Assets/Scripts/Script/ 기준) | 카테고리 |
|---|---|---|
| 1 | `CardEffectCommons/GiveEffect/GiveEffectToPermanent/ChangeLinkMax.cs` | A |
| 2 | `CardEffectCommons/GiveEffect/GiveEffectToPermanent/StartOfMainAttack.cs` | A |
| 3 | `CardEffectCommons/GiveEffect/GiveEffectToPlayer/ChangeDigivolutionCost.cs` | A |
| 4 | `CardEffectCommons/GiveEffect/GiveEffectToPlayer/IgnoreDigivolutionRequirement.cs` | A |
| 5 | `CardEffectCommons/KeyWordEffects/Training.cs` | E |
| 6 | `CardEffectCommons/MinMax_DP_Cost_Level/Cost/IsMaxCost.cs` | F |
| 7 | `CardEffectCommons/MinMax_DP_Cost_Level/Cost/IsMinCost.cs` | F |
| 8 | `CardEffectCommons/MinMax_DP_Cost_Level/DP/IsMaxDP.cs` | F |
| 9 | `CardEffectCommons/MinMax_DP_Cost_Level/DP/IsMinDP.cs` | F |
| 10 | `CardEffectCommons/MinMax_DP_Cost_Level/DigivolutionCards/IsMinDigivolutionCards.cs` | F |
| 11 | `CardEffectCommons/MinMax_DP_Cost_Level/Level/IsMaxLevel.cs` | F |
| 12 | `CardEffectCommons/MinMax_DP_Cost_Level/Level/IsMinLevel.cs` | F |
| 13 | `CardEffectCommons/TrashLinkedCards.cs` | F |
| 14 | `SelectCardPanel.cs` | G (UI 패널) |
| 15 | `SelectDeck.cs` | G (UI 패널) |
| 16 | `SelectJogressEffect.cs` | G (**효과-선택**, UI 아님) |

비고:
- #5 `KeyWordEffects/Training.cs`(E)는 B 카테고리의 `CardEffectFactory/KeyWordEffects/Training.cs`(FULL, 56줄, 팩토리 배선)와 짝인데, 실제 키워드 프로세스 본체(자기-suspend + 라이브러리 최하단 카드 이면 add)가 스켈레톤이라 **배선은 있으나 발동 시 빈 동작**이 될 가능성. 확인 필요.
- #16 `SelectJogressEffect.cs`는 이름이 "Select"지만 UI 패널이 아니라 조그레스 진화 시 "digivolve 대신 jogress를 택할지" 플레이어 선택 게이트(AS-IS `MonoBehaviour`, `SetUp_SelectWheterToJogress`) — 게임 로직 프리미티브. G의 다른 UNPORTED/SKELETON UI 항목과 성격이 다르다.

---

## §3 UNPORTED 전수 리스트 (5, 경로 알파벳순)

| # | 경로 | 카테고리 | 비고 |
|---|---|---|---|
| 1 | `CardEffectCommons/GiveEffect/GiveEffectToPermanent/CanNotBeDeletedByBattle.cs` | A | AS-IS `GainCanNotBeDeletedByBattle`(대상 1-permanent grant, `CanNotBeDestroyedByBattleStaticEffect` 위임) — TO-BE에 파일도 심볼도 없음. 재하우징 흔적 없음(grep 0건). player-scope 자매 함수(`GiveEffectToPlayer/CanNotBeDeletedByBattle.cs`)는 FULL이라 **한쪽만 빠진 비대칭 갭**. |
| 2 | `SelectBattleDeck.cs` | G | UI 패널(배틀덱 선택 화면) — 카드-효과 프리미티브 아님 |
| 3 | `SelectBattleMode.cs` | G | UI 패널(배틀모드 선택 화면) — 카드-효과 프리미티브 아님 |
| 4 | `SelectCommand.cs` | G | UI 패널(커맨드 선택) — 카드-효과 프리미티브 아님 |
| 5 | `SelectCommandPanel.cs` | G | UI 패널 — 카드-효과 프리미티브 아님 |

---

## §4 DIVERGENT 리스트 (2)

| # | 경로 | 사유 |
|---|---|---|
| 1 | `CardEffectFactory/KeyWordEffects/BlastDigivolution.cs` | STOP 가드(design item RD-P6C2-11). `CanSelectPermanentCondition`/`CanActivateCondition`/`ActivateCoroutine` 3개 멤버 전부 `NotSupportedException` — AS-IS `CardSource.CanPlayCardTargetFrame`/`Permanent.PermanentFrame`(field-frame 슬롯 모델)이 미포팅이라 활성화 자체가 도달 불가. 팩토리 등록(`ActivateClass` 셋업)은 존재하나 실동작 0. |
| 2 | `CardEffectFactory/KeyWordEffects/BlastDNADigivolution.cs` | STOP 가드(design item RD-P6C1-1/-8). 307줄 중 대부분 로직은 살아있으나(RD-P6C1-2/-7은 close됨, 주석에 명시) jogress-FRAME play 실행 직전 종단에서 `NotSupportedException` — 동일 field-frame 슬롯 모델 결여가 원인. #1과 같은 인프라 갭의 다른 얼굴. |

두 항목 모두 **동일 인프라 갭**(field-frame 슬롯 모델: `Player.fieldCardFrames`/`PreferredFrame`/frame-indexed `CreateNewPermanent`)에서 파생 — 별개로 상환할 두 개가 아니라 인프라 1건 + 소비자 2개로 봐야 한다.

---

## §5 판정 노트

### 2026-07-08 감사 대비 정성 변화

- **구조 자체가 바뀌었다.** 2026-07-08 감사는 "카드 효과 로직이 소수의 대형 허브 파일(`CardEffectFactory.cs`/`CardEffectCommons.cs`, 각 수천 줄) 안에 메서드 단위로 뭉쳐 있고, TO-BE는 `ActivatedEffect`/`CardEffectDefinition` 같은 uniform 프리미티브로 폴딩되어 1:1 팩토리 심볼이 없다"는 구조 불일치를 FAIL의 최대 원인으로 지목했다(타이밍-클래스 계열 20종 등). 이번 실측에서는 **AS-IS 파일 구조 자체가 파일-당-프리미티브로 이미 분해되어 있고**(`CardEffectFactory/*.cs` 27개, kind-class `CardEffects/*.cs` 73개 등), TO-BE도 **동일 경로·동일 파일명 1:1 미러**로 존재한다 — 즉 B군/R 시리즈 작업이 "허브 파일 재분해 + 미러 파일 신설"을 통해 2026-07-08 시점 FAIL의 구조적 원인(비-1:1 심볼) 상당수를 물리적으로 해소한 것으로 보인다. 다만 이 문서는 **파일 존재+본문 유무만** 판정하므로, 2026-07-08 감사가 지적한 "술어 평면화/게이트 드롭"류 **fidelity(내용) 갭이 남아있는지는 별도 확인 필요** — 이 census의 FULL은 "구조적으로 자리가 있고 실 로직이 있다"는 뜻이지 "AS-IS와 동작이 완전히 등가"라는 보증은 아니다.
- **잔여 갭이 3개 층으로 국소화됐다.** SKELETON 16개 대부분이 (a) `MinMax_DP_Cost_Level/**` 서브트리(7개, IsMax/IsMinCost·DP·Level·DigivolutionCards — 술어 유틸리티 한 무리가 통째로 미포팅), (b) `GiveEffect/**`의 개별 grant 4종, (c) 키워드 1종(Training) + 커먼즈 1종(TrashLinkedCards)으로 쏠려 있다. 2026-07-08 당시처럼 "구조 자체가 없다"가 아니라 "자리는 파여 있는데 안 채워졌다"는 흔적(TODO 보일러플레이트)이 남아 있어, 상환 작업량 산정이 훨씬 명확해졌다.
- **DIVERGENT는 진짜 인프라 블로커 1건뿐.** field-frame 슬롯 모델(`Player.fieldCardFrames`) 미포팅이 BlastDigivolution/BlastDNADigivolution 2개 키워드를 종단 차단한다 — 2026-07-08 감사의 "SecurityClass/PlaceToSecurityEffect 전면 미포팅"류 대형 갭과 달리, 이번엔 스코프가 좁고(키워드 2개) 원인이 단일하다.

### 동결-계약 관점 특기 사항 (빈도와 무관)

- **A의 비대칭**: `CanNotBeDeletedByBattle`이 player-scope(FULL)엔 있고 permanent-scope(UNPORTED)엔 없다 — 같은 grant 계열 안에서 한쪽만 빠진 패턴이라 놓치기 쉽다. 동결 전에 A 전체를 이런 "짝 비교"로 한 번 더 훑을 가치가 있다.
- **MinMax_DP_Cost_Level 서브트리는 전멸(7/7 SKELETON)**: 부분 포팅이 아니라 이 작은 유틸리티 그룹 전체가 손대지지 않았다 — 단일 트랜치로 묶어 처리 가능한 후보.
- **인터페이스(D)·kind-class(C)는 구조적으로 완결**: 74/74, 73/73 모두 FULL. 이 두 계층은 "형태"의 문제가 아니라(2026-07-08 감사가 우려했던 지점) 이미 해소되어 있으므로, 동결 계약 심사에서 D/C는 낮은 리스크로 분류 가능.
- **G의 UNPORTED 4개는 엔진 프리미티브가 아니라 UI 화면**(배틀덱/배틀모드/커맨드 선택 패널) — "엔진 동결 계약" 스코프(게임 로직)에서는 애초에 제외 대상일 가능성이 높다. 기계적 4분류표에는 넣었으나, 계약 심사 시 A~F(순수 카드-효과 프리미티브)와 G의 UI-형 UNPORTED는 **분리해서 판단**해야 한다 — 합치면 UNPORTED 비율이 실제보다 과장된다(G 제외 시 UNPORTED는 323개 중 1개, 0.3%).
- **SelectJogressEffect(SKELETON)는 이름 때문에 UI로 오분류하기 쉬우나 실제로는 게임-로직 선택 게이트** — G 리스트를 UI로 일괄 치부하지 않도록 주의.
- **FULL 판정 파일 내부의 국소 STOP** 3건은 파일 전체를 대표하지 않아 4분류표에서 DIVERGENT로 세지 않았으나 동결 심사 시 존재를 인지해야 한다: `CardEffectCommons/DNADigivolveEffects.cs`(`DNADigivolveWithHandOrTrashCardIntoHandOrTrash` — transient-permanent 서브스트레이트 부재, RD-W3-6/7 계열), `CardEffectCommons/KeyWordEffects/Blitz.cs`(`beforeOnAttackCoroutine` 파라미터가 non-null일 때만 STOP, 유일 호출부 ST13_06), `SelectCardEffect.cs`(`UntilCalculateFixedCostEffect` transient cost-registration 훅 부재).
