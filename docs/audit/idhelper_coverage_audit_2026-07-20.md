# id-헬퍼 커버리지 전수 감사 — `permanent.XXX` 번역 병목 규명

- 날짜: 2026-07-20
- 범위: read-only 감사 (코드 수정 없음)
- 목적: 저가/로컬 모델 포팅에서 `permanent.XXX` 속성 읽기가 번역 실패하는 원인이 **substrate(id-헬퍼/미러 속성) 부재**인지 **치환표(`symbol_map.csv`) 커버리지 구멍**인지를 숫자로 규명.
- 실증 사례: BT2_109 — `permanent.Level`이 치환표에 없어 모델이 기존 `LevelOf(card, id)`를 못 찾고 `GetPermanentOf`를 자작.

## 데이터 소스

- AS-IS 수집: `DCGO/Assets/Scripts/CardEffect/` 전체의 `permanent\.[A-Za-z_]+` (grep `--binary-files=text`, 빈도순) → **고유 속성 65종**.
- commons id-헬퍼: `src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons.cs` (+ `CardEffectCommons/` 하위).
- 미러 Permanent 속성: `src/HeadlessDCGO.Engine/Assets/Scripts/Script/Permanent.cs` (4,519줄; 게임 로직 보유).
- 치환표: `docs/porting/symbol_map.csv` (443 distinct symbol) + `symbol_map_guide.md`.

## 번역 경로 2종

- (A) **commons id-헬퍼**: `CardEffectCommons.LevelOf(card, id)` 형태 static 헬퍼. `permanent.Level` → `LevelOf(card, id)`.
- (B) **미러 Permanent 경유**: `PermanentOf(card, id).XXX`. `PermanentOf`는 전역 헬퍼가 아니라 파일마다 인라인되는 id-adapter 람다(guide L90-106; Save.cs:34, ArtsDigivolve.cs:49, Link.cs:83 등에 반복 정의). 미러 Permanent에 속성이 있으면 경로 B로 항상 번역 가능.

## 제4축 — 충실도 (가장 중요한 판정 기준)

경로 A(id-헬퍼)는 substrate가 "존재"해도 구현이 **효과를 접는 미러 `Permanent.XXX`에 위임**하는지, 아니면 **원시 metadata/base를 직접 읽어 부여·지속 효과를 놓치는지**에 따라 충실도가 갈린다. 경로 B(미러 Permanent 직접)는 효과-접기 표면 그 자체이므로 본질적으로 충실 — 충실도 리스크는 **미러 속성을 우회하는 경로 A 헬퍼에만** 존재한다. 따라서 id-헬퍼를 실제로 열어 위임 대상을 판정:

- **분류3a (완비·충실)**: 헬퍼 존재 + 효과-접기 미러 속성에 위임(or 접을 효과가 없는 순수 상태/구조 읽기로 미러와 동일 substrate).
- **분류3b (존재하나 충실도 결함)**: 헬퍼 존재하나 **base 직접 읽기 → 효과 미접기**. "있는데 틀린" 함정 — 신설보다 **우선 수리** 대상.

id-헬퍼 5종 구현 실사 결과:

| id-헬퍼 | 대상 속성 | 구현 | 위임/직접 | 판정 |
|---|---|---|---|:---:|
| `CurrentDp(card,id)` | DP | `return new Permanent(ctx,id,owner).DP;` (:4567) | **미러 `Permanent.DP` 위임** (모든 IChangeDPEffect·LinkedDP·Boost 접음) | **3a** |
| `LevelOf(card,id)` | Level | `ReadLevel(instance.Metadata) ?? ReadLevel(def.Metadata)` (:4753) | **base metadata 직접** — `Permanent.Level` 우회, `IChangePermanentLevelEffect` 미접기 | **3b ⚠️** |
| `IsSuspended(card,id)` | IsSuspended | `instance.Metadata["isSuspended"] is true` (:4320) | 미러 `Permanent.IsSuspended`(:1642)와 **동일 substrate 직접 읽기** — 접을 효과 없는 순수 상태 | 3a |
| `HasNoDigivolutionCards(card,id)` | HasNoDigivolutionCards | `DigivolutionStackReader.Read(...).UnderCards.Count==0` (:4607) | 미러 `Stack.UnderCards`와 동일 구조 읽기 — 접을 효과 없음 | 3a |
| `HasCannotReturnToLibrary(ctx,id,id)` | CannotReturnToLibrary | 전 플레이어 EffectList의 `ICannotReturnToLibraryEffect` 스캔 (NewModelContinuousScan:1723) | **효과 스캔-접기** | 3a |

**분류3b(충실도 결함) = 1건: `LevelOf`.**

- `Permanent.Level`(:558)은 `TopCard.Level`을 seed로 모든 활성 `IChangePermanentLevelEffect`를 접는다. 그러나 `LevelOf`는 `instance.Metadata`의 `level`/`Level` 키(없으면 def.Metadata)만 읽어 **base 레벨만** 반환 → 레벨 변경 효과를 놓친다.
- **현재 미발화**(레벨 변경 카드 IChangePermanentLevelEffect 미포팅이라 base==수정치)이나 `LevelOf`는 이미 **ST2_03·BT1_068·ST4_01·BT2_095**가 target/count 술어에서 사용 중 → 레벨 변경 카드가 포팅되는 순간 잠복 버그 발화.
- 수리: `CurrentDp`처럼 `new Permanent(card.Context, id, owner).Level`로 re-point. **caveat**: `Permanent.Level`은 no-level에 -1 sentinel(consumer가 `HasLevel` 선가드), `LevelOf`는 unknown에 0 반환 + `TopCardHasLevel = LevelOf>0`. sentinel 화해 필요 → 강모델 수리(자명 re-point 아님).

---

## 핵심 발견 (요약)

1. **substrate는 거의 완비.** 65종 중 55종은 미러 Permanent 공개 속성/메서드(경로 B) 또는 전용 id-헬퍼(경로 A)가 이미 존재. substrate가 전무한 것은 **5종**(모두 빈도 ≤8), 그중 2종은 기존 미러 멤버로부터의 1줄 파생.
2. **치환표는 `permanent.XXX` 속성-읽기 어휘를 사실상 0건 등재.** 65종 중 `symbol_map.csv` 등재는 **0건**(유일한 `CanUnsuspend` 매치는 트리거 헬퍼 행이지 속성-읽기 행이 아님). 치환표의 asis_symbol 컬럼은 commons/actions/factory 심볼(`IsMinLevel`, `ChangeCardLevelClass`, `IsLevel4` 등)만 담고 있고 순수 필드 읽기(`Level`, `DP`, `IsDigimon`, `TopCard`)는 population에서 빠져 있음.
3. **따라서 `symbol_map_coverage.md`의 "frequency-weighted coverage 100.0%"는 포팅-기계화 관점에서 오도.** 그 100%는 속성-읽기 어휘를 제외한 symbol population에 대한 수치. BT2_109 실패는 substrate 부재가 아니라 **치환표가 속성-읽기 행을 아예 안 담는 구조적 사각지대**가 원인 — 분류1 실증.

---

## 분류 집계

| 분류 | 정의 | 개수 |
|---|---|---:|
| **분류1** — 치환표 보강만 | substrate(id-헬퍼 or 미러 Permanent 속성) 존재, `symbol_map.csv` 미등재 → 문서/치환표 보강만 | **55** |
| **분류2** — substrate 신설 | id-헬퍼도 없고 미러 Permanent 속성도 없음 → 강모델 헬퍼/속성 신설 후 등재 | **5** |
| **분류3** — 이미 완비 | 헬퍼 존재 + 치환표 등재 | **0** |
| (별도) **UI-stripped** — 포팅 대상 아님 | AS-IS에서 순수 Unity UI 부수효과(`ShowingPermanentCard.WillBe*Object.SetActive`), 헤드리스 no-op | **5** |

> 분류3 = 0의 의미: 치환표가 속성-읽기 행을 하나도 담지 않으므로, substrate가 있는 것은 전부 "치환표에 없음"(분류1)으로 떨어짐. 즉 substrate는 있는데 문서 색인만 비어 있는 상태.

**충실도 축 교차(제4축):** 위 map-등재 축과 직교. 경로 A id-헬퍼 5종 중 **3a(충실) 4종**(CurrentDp·IsSuspended·HasNoDigivolutionCards·HasCannotReturnToLibrary), **3b(충실도 결함) 1종**(LevelOf). 경로 B(미러 Permanent 직접) 번역은 효과-접기 표면을 그대로 쓰므로 충실. → 수리 우선순위: **3b(1건) > 분류1 치환표 보강 > 분류2 substrate 신설.** 3b는 "있는데 틀린" 함정이라 신설·색인보다 먼저 고쳐야 함.

---

## 분류2 (substrate 신설 필요 — 강모델 작업 대상 M = 5)

| 속성 | 빈도 | AS-IS 정의(Permanent.cs) | 미러 상태 | 성격 |
|---|---:|---|---|---|
| `HasFaceDownDigivolutionCards` | 8 | `=> DigivolutionCards.Any(x => x.IsFlipped)` (:3954) | 미러 Permanent에 명명 속성 없음 (단 `DigivolutionCards` 존재) | **자명 1줄 파생** — named getter 추가 or 인라인 |
| `HandBounceEffect` | 3 | `public ICardEffect HandBounceEffect { get; set; }` (:3678) | 미러 전무 (files=0) | 이펙트-상태 필드 신설 |
| `DigivolutionOrLinkCards` | 2 | `=> cardSources.Filter(cs => cs != TopCard)` (:892) | 미러 Permanent에 명명 속성 없음 (단 `cardSources`/`TopCard` 존재) | **자명 1줄 파생** |
| `LibraryBounceEffect` | 2 | `public ICardEffect LibraryBounceEffect { get; set; }` (:3682) | 미러 전무 (files=0) | 이펙트-상태 필드 신설 |
| `DPWhenSuspended` | 1 | `public int DPWhenSuspended = 114514;` (:1958) | 미러 Permanent 속성 아님; AttackProcess/CardController 주석 참조만 | 상태 필드 (tail; substrate 확정 필요) |

세부:
- **자명 파생 2종** (`HasFaceDownDigivolutionCards`, `DigivolutionOrLinkCards`): 기존 미러 멤버(`DigivolutionCards`, `cardSources`, `TopCard`)로부터 1줄. 사실상 배선 수준.
- **상태 필드 3종** (`HandBounceEffect`, `LibraryBounceEffect`, `DPWhenSuspended`): per-permanent 가변 상태 substrate가 필요. 모두 빈도 ≤3.
- 다섯 항목 전부 빈도 ≤8 → 포팅 기계화의 **주 병목이 아님**.

## UI-stripped (포팅 대상 아님 — 5종, 총 99회 출현)

| 속성 | 빈도 | AS-IS |
|---|---:|---|
| `ShowingPermanentCard` | 24 | `FieldPermanentCard ShowingPermanentCard { get; set; }` (:1644) — Unity UI 뷰 |
| `HideHandBounceEffect` | 20 | `void HideHandBounceEffect()` (:4068) — `...WillBeHandBounceObject.SetActive(false)` |
| `HideDeckBounceEffect` | 20 | `void HideDeckBounceEffect()` (:4032) — UI SetActive |
| `HideWillRemoveFieldEffect` | 18 | UI SetActive |
| `HideDeleteEffect` | 17 | `void HideDeleteEffect()` (:4104) — UI SetActive |

이들은 `permanent.HideXxx()` 메서드 호출/`ShowingPermanentCard` 참조로, AS-IS에서 순수 Unity GameObject 가시성 토글. 헤드리스에서 no-op(스트립). 번역 헬퍼 불필요 — 치환표에 "no-op/stripped"로 명시만 하면 됨.

---

## 전체 교차표 (65종, 빈도순)

범례: **id-헬퍼** = `(CardSource, HeadlessEntityId)` 전용 static 헬퍼명 / **미러속성** = 미러 `Permanent.cs` 공개 멤버(경로 B) / **치환표** = `symbol_map.csv` asis_symbol 등재 / **분류** = 1/2/3/UI

| 속성 | 빈도 | id-헬퍼(경로A) | 미러Permanent속성(경로B) | 치환표 | 분류 |
|---|---:|---|---|:---:|:---:|
| TopCard | 3757 | — (id가 곧 top card → `new CardSource(ctx,id,owner)`) | ✅ `TopCard` (:107) | ✗ | 1 |
| IsTamer | 733 | — | ✅ `IsTamer` (:696) | ✗ | 1 |
| IsDigimon | 733 | (위치조합: `IsBattleAreaDigimon` 등) | ✅ `IsDigimon` (:618) | ✗ | 1 |
| DP | 400 | ✅ `CurrentDp(card,id)` (:4561) | ✅ `DP` (:376) | ✗ | 1 |
| Level | 349 | ✅ `LevelOf(card,id)` (:4753) — **⚠️ base 직접, 효과 미접기 = 3b** | ✅ `Level` (:558, 효과 접음) | ✗ | **1 (BT2_109) + 3b 수리** |
| DigivolutionCards | 336 | — | ✅ `DigivolutionCards` (:31) | ✗ | 1 |
| IsSuspended | 251 | ✅ `IsSuspended(card,id)` (:4320) | ✅ `IsSuspended` (:1642) | ✗ | 1 |
| PermanentFrame | 132 | — | ✅ `PermanentFrame` (:118, `FieldCardFrame?`) | ✗ | 1 (frame 모델 adaptation RD-P6C) |
| Levels_ForJogress | 117 | — | ⚠️ 미러 `CardSource.Levels_ForJogress(CardSource)`로 이전 (CardSource.cs:955); Permanent엔 없음 | ✗ | 1 (relocated; 비자명) |
| IsToken | 104 | — | ✅ `IsToken` (:593) | ✗ | 1 |
| HasNoDigivolutionCards | 87 | ✅ `HasNoDigivolutionCards(card,id)` (:4607) | ✅ (:589) | ✗ | 1 |
| CanSelectBySkill | 75 | — | ✅ `CanSelectBySkill(ICardEffect)` (:2851) | ✗ | 1 |
| cardSources | 74 | — | ✅ `cardSources` (:1866) | ✗ | 1 |
| willBeRemoveField | 54 | — | ✅ `willBeRemoveField` (:1719) | ✗ | 1 |
| CanAttack | 44 | — | ✅ `CanAttack(...)` (:3157) | ✗ | 1 |
| HasDP | 40 | — | ✅ `HasDP` (:141) | ✗ | 1 |
| CanBeDestroyedBySkill | 33 | — | ✅ `CanBeDestroyedBySkill(ICardEffect)` (:3528) | ✗ | 1 |
| CanSuspend | 25 | — | ✅ `CanSuspend` (:1817) | ✗ | 1 |
| ShowingPermanentCard | 24 | — | ✗ (UI) | ✗ | **UI** |
| IsOption | 24 | — | ✅ `IsOption` (:719) | ✗ | 1 |
| HasBlocker | 23 | — | ✅ `HasBlocker` (:818) | ✗ | 1 |
| HideHandBounceEffect | 20 | — | ✗ (UI) | ✗ | **UI** |
| HideDeckBounceEffect | 20 | — | ✗ (UI) | ✗ | **UI** |
| HideWillRemoveFieldEffect | 18 | — | ✗ (UI) | ✗ | **UI** |
| HideDeleteEffect | 17 | — | ✗ (UI) | ✗ | **UI** |
| LinkedCards | 16 | — | ✅ `LinkedCards` (:2758) | ✗ | 1 |
| LevelJustAfterPlayed | 14 | — | ✅ (:4281) | ✗ | 1 |
| LevelJustBeforeRemoveField | 13 | — | ✅ (:1770) | ✗ | 1 |
| AddDigivolutionCardsBottom | 13 | — | ✅ `AddDigivolutionCardsBottom(...)` (:4135) | ✗ | 1 |
| CanUnsuspend | 11 | (commons `CanUnsuspend(Permanent)` — id 아님) | ✅ `CanUnsuspend` (:3022) | ⚠️ (트리거헬퍼 행만) | 1 |
| StackCards | 10 | — | ✅ `StackCards` (:4247) | ✗ | 1 |
| CannotReturnToLibrary | 10 | ✅ `NewModelContinuousScan.HasCannotReturnToLibrary(ctx,id,id)` (:1723) | ✗ (Permanent엔 미러 없음; scan 헬퍼로 대체) | ✗ | 1 (causing-source 인자 → 비자명) |
| CannotReturnToHand | 10 | — | ✅ `CannotReturnToHand(ICardEffect)` (:743) | ✗ | 1 |
| UntilOwnerTurnEndEffects | 9 | — | ✅ (:2033) | ✗ | 1 |
| HasFaceDownDigivolutionCards | 8 | — | ✗ | ✗ | **2 (자명파생)** |
| CanAttackTargetDigimon | 8 | — | ✅ `CanAttackTargetDigimon(...)` (:3285) | ✗ | 1 |
| HasSecurityAttackChanges | 7 | — | ✅ (:2562) | ✗ | 1 |
| EffectList | 6 | — | ✅ `EffectList(EffectTiming)` (:1886) | ✗ | 1 |
| HasRetaliation | 5 | — | ✅ (:1214) | ✗ | 1 |
| HasNoLinkCards | 4 | — | ✅ (:2783) | ✗ | 1 |
| DiscardEvoRoots | 4 | — | ✅ `DiscardEvoRoots(...)` (:3849) | ✗ | 1 |
| CanMove | 4 | — | ✅ `CanMove` (:3073) | ✗ | 1 |
| ImmuneFromStackTrashing | 3 | — | ✅ `ImmuneFromStackTrashing(ICardEffect)` (:2918) | ✗ | 1 |
| HandBounceEffect | 3 | — | ✗ | ✗ | **2 (상태필드)** |
| DestroyingEffect | 3 | — | ✅ `DestroyingEffect` (:1739) | ✗ | 1 |
| DPJustBeforeRemoveField | 3 | — | ✅ (:1762) | ✗ | 1 |
| oldIsTapped_playCard | 2 | — | ✅ (:1666) | ✗ | 1 |
| UntilOpponentTurnEndEffects | 2 | — | ✅ (:2054) | ✗ | 1 |
| RemoveBoost | 2 | (`DpBoostHelpers.RemoveBoost`) | ⚠️ DpBoostHelpers로 이전 | ✗ | 1 (relocated) |
| PlayCostJustAfterPlayed | 2 | — | ✅ (:4288) | ✗ | 1 |
| LibraryBounceEffect | 2 | — | ✗ | ✗ | **2 (상태필드)** |
| IsDestroyedByBattle | 2 | — | ✅ (:1694) | ✗ | 1 |
| HasReboot | 2 | — | ✅ (:1048) | ✗ | 1 |
| HasOnDeletionEffect | 2 | — | ✅ (:1610) | ✗ | 1 |
| DigivolutionOrLinkCards | 2 | — | ✗ | ✗ | **2 (자명파생)** |
| CardNamesJustAfterDigivolved | 2 | — | ✅ (:4302) | ✗ | 1 |
| AddBoost | 2 | (`DpBoostHelpers.AddBoost`) | ⚠️ DpBoostHelpers로 이전 | ✗ | 1 (relocated) |
| TraitsJustAfterPlayed | 1 | — | ✅ (:4309) | ✗ | 1 |
| ImmuneFromDeDigivolve | 1 | — | ✅ `ImmuneFromDeDigivolve()` (:2884) | ✗ | 1 |
| HasJamming | 1 | — | ✅ (:910) | ✗ | 1 |
| HasFortitude | 1 | — | ✅ (:1283) | ✗ | 1 |
| DPWhenSuspended | 1 | — | ✗ | ✗ | **2 (상태필드)** |
| CostJustBeforeRemoveField | 1 | — | ✅ (:1778) | ✗ | 1 |
| CardNamesJustBeforeRemoveField | 1 | — | ✅ (:1786) | ✗ | 1 |

---

## 결론

**기계화를 막는 실제 작업량 = 충실도 수리 1건(최우선) + 치환표 보강 55건 + substrate 신설 5건(그중 2건은 자명 1줄, 3건은 저빈도 상태필드).**

- **최우선 = 충실도 수리 1건(`LevelOf`, 분류3b).** substrate가 "있는데 틀린" 유일 항목. `Permanent.Level`(효과 접음)을 우회해 base metadata만 읽어 `IChangePermanentLevelEffect`를 놓침. 이미 ST2_03·BT1_068·ST4_01·BT2_095가 사용 중 → 레벨 변경 카드 포팅 시 잠복 발화. `new Permanent(ctx,id,owner).Level` re-point + no-level sentinel(-1 vs 0) 화해 필요(강모델). 나머지 4개 id-헬퍼(CurrentDp·IsSuspended·HasNoDigivolutionCards·HasCannotReturnToLibrary)는 충실(3a) — CurrentDp는 `Permanent.DP` 위임, 나머지는 접을 효과 없는 순수 상태/구조 읽기.

- **주 병목 = 치환표(symbol_map.csv) 커버리지 구멍.** `permanent.XXX` 속성-읽기 어휘 65종 중 55종(빈도 상위 전부 — TopCard 3757, DP 400, Level 349, IsSuspended 251 …)이 substrate 완비 상태인데 치환표에 색인만 없음. 나머지 5종은 UI no-op(스트립). substrate 부재는 5종·전부 빈도 ≤8.
- **BT2_109은 분류1 실증.** `permanent.Level`은 미러 `Permanent.Level`(:558)과 commons `LevelOf(card, id)`(:4753)가 **둘 다 이미 존재**했으나 `symbol_map.csv`에 `Level → LevelOf` 행이 없어 모델이 못 찾고 `GetPermanentOf`를 자작. substrate 문제가 아니라 치환표 색인 문제.
- **symbol_map "100% 커버리지"는 사각지대.** 치환표의 asis_symbol population이 commons/actions/factory 심볼만 담고 순수 필드-읽기(`Level`/`DP`/`IsDigimon`/`TopCard`)를 아예 제외 → 100%는 그 population 내부 수치일 뿐, 속성-읽기 어휘는 0% 색인. guide는 `PermanentOf(id)` id-adapter 패턴(L90-106)을 서술하나, 심볼 단위로 lookup하는 저가/기계 포터가 CSV에서 `permanent.Level`을 검색하면 행이 없어 실패.

### M — 강모델 substrate 신설 대상 (구체 목록, 5건)

1. `HandBounceEffect` (freq 3) — per-permanent `ICardEffect` get/set 상태 필드 신설.
2. `LibraryBounceEffect` (freq 2) — per-permanent `ICardEffect` get/set 상태 필드 신설.
3. `DPWhenSuspended` (freq 1) — per-permanent `int` 상태 필드 신설(현재 AttackProcess/CardController 주석 참조만; substrate 확정 필요).
4. `HasFaceDownDigivolutionCards` (freq 8) — 자명 파생: `DigivolutionCards.Any(x => x.IsFlipped)` named getter.
5. `DigivolutionOrLinkCards` (freq 2) — 자명 파생: `cardSources.Filter(cs => cs != TopCard)` named getter.

(4·5는 배선 수준. 1~3만 실질 상태 substrate.)

### 권고 (본 감사 범위 밖, 후속)

`symbol_map.csv`에 `permanent.XXX` 속성-읽기 행 65개를 추가하는 것이 최대 ROI: 각 행 mirror_symbol = `PermanentOf(card,id).XXX`(경로 B) 또는 전용 id-헬퍼(경로 A: `LevelOf`/`CurrentDp`/`IsSuspended`/`HasNoDigivolutionCards`/`HasCannotReturnToLibrary`), UI 5종은 `stripped(no-op)`로 명시. relocated 3종(`Levels_ForJogress`→CardSource, `AddBoost`/`RemoveBoost`→DpBoostHelpers)은 signature_delta에 이전처 기재.
