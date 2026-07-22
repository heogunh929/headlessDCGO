# SW-A / SW-C / SW-E 충실도 감사 스위프 (2026-07-22)

> 명세: [mirror_correctness_axes_2026-07-22.md](mirror_correctness_axes_2026-07-22.md) §15 "미실시 감사 스위프".
> Base: `git HEAD 2a69cf38` (branch `main`). **read-only 감사** — 엔진 소스 무변경(엔진 파일 0 diff).
> AS-IS grep 전량 `--binary-files=text`. 경로: 미러 = `src/HeadlessDCGO.Engine/…`, AS-IS = `DCGO/Assets/Scripts/…`.
> 방법: 축별 sample-verify로 AS-IS 1:1 주장을 원본 대조. 분류 = DEFECT-P0(실플레이 파괴) / DEFECT-P1(잠재·조건부, 트리거 시나리오 명시) / NOTE-P2(cosmetic·dead·구조).

## 집계 요약

| 축 | 대상 규모 | P0 | P1 | P2 | 판정 |
|---|---|---:|---:|---:|---|
| **SW-A** 카드 corpus 발명 클래스 전수 | 3,918 파일 | 0 | 0 | 0 | **PASS** — 발명 클래스 0·경로이탈 0 |
| **SW-C** kind-class 도달성 전수 | 74 인터페이스 | 0 | 0 | 4 | **PASS(notes)** — 전 인터페이스 1:1·미도달분은 cosmetic/dead/기지채무/dormant |
| **SW-E** 타이밍 emit 좌석 ↔ 미러 배선 | 63 timing / 68 AS-IS 좌석 | 0 | 3 | 1 | **PASS(ledgered)** — enum 63/63·잔 P1 3건 전부 in-code 기등재 design item |

**신규 미등재 결함 = 0.** P1 3건은 전부 소스 주석에 이미 핀된 design item(MIG3-CUTIN-WOULDDISCARD / MIG3-CUTIN-WHENUNTAP / R2-P2-2)의 감사-재확인. 클리어컷 기계 결함 없음 → **인라인 수리 0 → 엔진 diff 0 → DIGEST 불변**(자명).

---

## SW-A — 카드 corpus 파일 내부 발명 클래스 전수

**방법**: 미러 `Assets/Scripts/CardEffect/` 전 .cs(TestFixtures 제외)에 대해 (1) 동일 상대경로 AS-IS 파일 존재 여부, (2) 파일 내부 `class/struct/enum/record` 선언 집합을 AS-IS 동일-경로 파일과 `comm -23` diff — 미러에만 있는 클래스명 = 발명 후보.

**결과** (3,918 파일 스캔):
- **AS-IS 대응 파일 부재 = 0** (전 미러 카드 파일이 동일-경로 AS-IS 원본 보유).
- **미러-전용 클래스 선언 = 0** (진성). 기계 스캔이 4파일 flag했으나 전부 주석 오탐(`"same class as"`, `"enum members"`, `"base-class helper"`, `"the kind-class in the OWNING"` 등 자연어 문구가 `class|enum <word>` 정규식에 매칭).

| flag 파일 | flag 토큰 | 실체 |
|---|---|---|
| BT2/Black/BT2_063.cs:10 | `class as` | 주석 "same class as BT2_002/010" |
| ST2/Blue/ST2_15.cs:16 | `class members` | 주석 "enum members kept" |
| ST2/Blue/ST2_12.cs:10 | `class helper` | 주석 "base-class helper" |
| EX1/White/EX1_072.cs:12 | `class in` | 주석 "kind-class in the OWNING" |

**판정: PASS**. §7.1 census가 미실시로 남긴 "미러 카드 파일 내부 발명 클래스" 축을 전수 — corpus 층에 구조적 발명물 0. (census §7.1 한계 종결.)

---

## SW-C — kind-class 도달성 전수 (74 IXxxEffect)

**방법**: 미러 `Script/CardEffectInterfaces.cs` 74 인터페이스 각각에 대해 (i)AS-IS 대응 인터페이스 실존 (ii)구현 클래스 존재 (iii)미러 chokepoint(scan 판독원) 실존 — chokepoint 무-scan인데 구현자 존재하면 inert 후보. scan=0 후보를 AS-IS chokepoint와 개별 대조.

### C.0 인터페이스 집합 1:1
`CardEffectInterfaces.cs` 미러 74 vs AS-IS 74 — **mirror-only 0·AS-IS-only 0** (`comm` 무차). 인터페이스 어휘 완전 일치.

### C.1 scan=0 후보 개별 판정 (chokepoint 무-scan 인터페이스)

| # | 인터페이스 | 미러 chokepoint | AS-IS chokepoint | 판정 |
|---|---|---|---|---|
| RD-SW-C-01 | `IChangeCardLevelForAssemblyEffect` | **없음** (미러 `CardSource`에 `Level_Assembly` getter 부재) | `CardSource.cs:2225-2226` (`Level_Assembly` fold) | **P2 dormant** |
| RD-SW-C-02 | `IAddDetailEffect` | 없음 (미러 `PermanentDetail.cs` = 7줄 스텁) | `PermanentDetail.cs:293-338` (detail 표시 문자열) | **P2 cosmetic** |
| RD-SW-C-03 | `IAddDNANamesEffect` | 없음 (미러 `Permanent.cs` DNA-name fold 미배선) | `Permanent.cs:3628-3649` | **P2 dead-in-AS-IS** |
| RD-SW-C-04 | `IScapegoatEffect` | 없음 (라이브 키워드 미인식) | 없음 (AS-IS도 인터페이스 scan 무 — 키워드 경로) | **P2 기지채무** |

**RD-SW-C-01 (IChangeCardLevelForAssemblyEffect) — P2 dormant**
- 생산자: `EX9_062`(SkullGreymon "Kimeramon 어셈블리에서 Lv.4 취급") — 미러 1:1 포팅 확인(`ChangeCardLevelForAssemblyClass` 생산). kind-class 자체는 미러 `Script/CardEffects/ChangeCardLevelForAssemblyClass.cs`에 1:1 실존.
- 소비자(chokepoint): AS-IS `CardSource.Level_Assembly` getter가 `IChangeCardLevelForAssemblyEffect`를 fold. 소비 카드 = **EX9_074**(Kimeramon), **P_220**(Kimeramon P) 뿐.
- 미러 상태: `CardSource`에 `Level_Assembly` 프로퍼티 자체가 부재 → EX9_062의 어셈블리-레벨 효과 inert.
- **그러나 유일 소비자 EX9_074·P_220이 둘 다 미포팅 스켈레톤 스텁**(`// Decision: PORT … Skeleton only`, 각 ~300B). 양단 dormant → **현 미러에서 실플레이 발현 불가**(P0/P1 아님).
- 해소 의존성: EX9_074/P_220 포팅 시 `CardSource.Level_Assembly` fold(`IChangeCardLevelForAssemblyEffect` 스캔)를 **동반 복원**해야 EX9_062 효과 활성. 미복원 시 조용한 inert 회귀 위험 → 포팅 착수 시 witness로 EX9_062+Kimeramon 어셈블리 조합 강제.

**RD-SW-C-02 (IAddDetailEffect) — P2 cosmetic**: AS-IS `PermanentDetail.cs`가 `IAddDetailEffect.GetDetail()`로 **표시용 detail 문자열**(`effectString +=`)만 구성. 실플레이 룰 무영향(UI 텍스트). 미러 `PermanentDetail.cs`가 7줄 스텁으로 축약한 것은 여타 UI-스트립과 동일 관례. 무결함.

**RD-SW-C-03 (IAddDNANamesEffect) — P2 dead-in-AS-IS**: AS-IS `Permanent.cs:3628-3649`가 스캔하나 **AS-IS·미러 양쪽 구현 클래스 0**(전 저장소 grep — 생산 카드 없음). AS-IS에서도 dead scan(추가되는 DNA 이름 0). 미러가 fold 블록을 생략해도 행동 차이 0. 구조적 note.

**RD-SW-C-04 (IScapegoatEffect) — P2 기지채무**: AS-IS도 인터페이스 scan 무(Scapegoat는 키워드 삭제-치환 경로). session_fidelity_checklist 부록의 **7종 SEAL 키워드**(Evade·Barrier·Save·Fortitude·Ascension·**Scapegoat**·Fragment) 중 하나 — 라이브 키워드 미인식 seal = [fidelity_master_goals.md](fidelity_master_goals.md) 기등재 채무. 신규 아님.

### C.2 라이브 확인분 (scan grep 오탐 교정)
자동 scan-count가 0으로 오검했으나 실측 라이브인 것:
- `IDontBattleSecurityDigimonEffect` — 미러 `Headless/Runtime/SecurityResolver.cs:556-573` 라이브(FQN 참조라 grep 누락). AS-IS `CardController.cs:4138-4162` 대응. **1:1**.
- `IOptionResolutionEffect` — `CardController`·`ActivatedEffectResolver`·`CardEffectFactory` 소비. 라이브.
- `IDontHaveDPEffect` — `Permanent.cs`·`RestrictionHelpers.cs` fold. 라이브.

**판정: PASS(notes)**. 74 인터페이스 전부 AS-IS 1:1, 도달성 결함(P0/P1) 0. 미도달 4건은 dormant/cosmetic/dead/기지채무 — 실플레이 발현 불가 또는 기등재.

**방법론 한계**: scan chokepoint의 **판독 충실도**(base vs fold된 값 판독 — 축 D)는 본 스위프 범위 밖(scan 실존만 검증). D축은 `permanent_fidelity_audit`·수리④-d에서 별도 진행.

---

## SW-E — AS-IS 타이밍 emit 좌석 ↔ 미러 배선 전수

**방법**: (1)`EffectTiming` enum 미러 vs AS-IS diff. (2)미러 corpus가 반응(`timing == EffectTiming.X`)하는 timing 집합 추출 → 각 timing의 미러 emit 좌석(비-corpus·비-enum·비-test) 실존 검사 → emit=0인 timing을 AS-IS emit 좌석 및 반응 카드 포팅상태와 대조.

### E.0 EffectTiming enum 1:1
AS-IS 63 값 전부 미러 enum 존재 (`comm -23` 무차). 미러-전용 잉여 값 없음(잔여 diff는 주석어 오탐). **완전 일치.**

### E.1 emit-coverage 결손 판정

corpus가 반응하는 timing 중 미러 emit 좌석 0인 8건 개별 판정:

| # | timing | corpus 반응 카드 | 카드 포팅 | AS-IS emit | 미러 emit | 판정 |
|---|---|---|---|---|---|---|
| — | `WhenDigisorption` | BT3_056 | 포팅(533줄) | 카드 인라인 `PutStackedSkill` (BT3_056.cs:137,151) | **카드 인라인** (BT3_056.cs:181,195) | **1:1 무결함** |
| RD-SW-E-01 | `WhenWouldDigivolutionCardDiscarded` | BT10_084 | 포팅(258줄) | `CardController.cs:5171-5192` LIVE cut-in | **미배선 (inert)** | **P1 latent** (=MIG3-CUTIN-WOULDDISCARD) |
| RD-SW-E-02 | `WhenUntapAnyone` | BT7_055 | 포팅(231줄) | `CardController.cs:5682-5720` manual cut-in | **미배선 (inert)** | **P1 latent** (=MIG3-CUTIN-WHENUNTAP) |
| — | `OnGetDamage` | TfxDeadTimingDraw만 | (test fixture) | 없음(전 repo 0) | 없음 | dead 양측 — 무결함 |
| — | `OnEndCoinToss` | TfxDeadTimingDraw만 | (test fixture) | 없음 | 없음 | dead 양측 — 무결함 |
| — | `OnEndBlockDesignation` | TfxDeadTimingDraw만 | (test fixture) | 없음 | 없음 | dead 양측 — 무결함 |
| — | `OnEndMainPhase` | TfxDeadTimingDraw만 | (test fixture) | 없음 | 없음 | dead 양측 — 무결함 |
| — | `OnEndAttackPhase` | TfxDeadTimingDraw만 | (test fixture) | 없음 | 없음 | dead 양측 — 무결함 |

**RD-SW-E-01 (WhenWouldDigivolutionCardDiscarded) — P1 latent, 기등재**
- 미러 `CardController.cs:1176-1183`가 명시: *"Headless has no such timing/gate/producer yet … design item **MIG3-CUTIN-WOULDDISCARD**. Nothing clears willBeRemoveSources today — the round-trip below is wired for when the cut-in lands."*
- AS-IS `CardController.cs:5171-5192`: 진화원 트래시 직전 "[When digivolution cards would be trashed]" cut-in을 StackSkillInfos→동기 drain. 미러는 `willBeRemoveSources` 마크/refilter를 no-op으로 통과(아무도 clear 안 함).
- 반응 카드 BT10_084(포팅, :170 반응 분기)의 이 timing 효과는 **inert**.
- **트리거 시나리오**: BT10_084가 필드에 있고 자신/타 효과로 진화원 트래시가 발생할 때 — AS-IS는 트래시 확정 전 cut-in으로 대상 리스트를 바꿀 수 있으나 미러는 발동 없이 원본 리스트대로 트래시.
- 수리: cut-in PRE 창 + 동기 drive 인프라 신설 = **design judgment**(원자 신설). 인라인 기계 수리 아님 → ledger only. 기존 in-code 핀 유지.

**RD-SW-E-02 (WhenUntapAnyone) — P1 latent, 기등재**
- 미러 `CardController.cs:1985-1990`가 명시: *"AS-IS :5682-5720 … the MANUAL-push variant … No headless timing/producer/drive point yet: design item **MIG3-CUTIN-WHENUNTAP**. Inert today."*
- AS-IS: 언서스펜드 직전 "[When permanents would unsuspend]" cut-in(GetSkillInfos+수동 PutStackedSkill+동기 drain)으로 대상 언서스펜드를 취소/변경 가능. 미러는 PRE cut-in 미발동 → 언서스펜드 그대로 진행(POST `OnUnTappedAnyone`만 라이브 = `TriggerEventEmitter.Emit` :2015 + StackSkillInfos :2022).
- 반응 카드 BT7_055(포팅, :142 반응 분기) inert.
- **트리거 시나리오**: BT7_055 필드 상태에서 상대/자신 언서스펜드 효과 발동 시 — AS-IS는 언서스펜드를 사전 차단할 수 있으나 미러는 무조건 언서스펜드 후 사후 창만 개방.
- 수리: PRE cut-in 인프라 = design judgment → ledger only.

**참고 (기등재) — `WhenRemoveField` / `OnRemovedField` PRE-vs-POST (R2-P2-2)**
미러 `TriggerTimingMap.cs`(DeriveZoneTransition)가 명시하는 **design item R2-P2-2**: AS-IS `WhenRemoveField`는 이동 확정 前 PRE cut-in(`DestroyPermanentsClass.Destroy` CardController.cs:3699), 미러는 POST-move 파생. bound self-scoped `WhenRemoveField` 등록 카드가 pre-trash 필드 상태를 읽는 witness가 아직 미포팅 → 현 unobservable(P1 latent, resume 조건 명시됨). SW-E 재확인: 여전히 latent, 신규 아님.

**판정: PASS(ledgered)**. enum 63/63, 이동/공격/시큐리티 파생 타이밍(TriggerTimingMap)은 포괄적. emit 결손 3건(WOULDDISCARD/WHENUNTAP/REMOVEFIELD)은 전부 **소스 주석에 id 핀된 기존 design item** — 신규 미배선 timing 0. 5 dead phase timing은 AS-IS도 dead(오검 아님).

---

## 게이트

| 게이트 | 상태 | 근거 |
|---|---|---|
| 엔진 빌드 0 에러 | N/A (자명 GREEN) | 인라인 수리 0 — 엔진 소스 무변경(`git status` 엔진 diff 0) |
| 각 수리에 witness | N/A | 수리 0 |
| DIGEST bit-identical (1000/1001/1002) | **불변(자명)** | 엔진 파일 0 diff → 결정론 산출 불변, baseline 그대로 |
| 전체 스위트 vs 36-fail base | **불변(자명)** | 코드 무변경 → fail-set = baseline, 신규 fail 0 |

> 클리어컷 기계 결함이 발견되지 않아(SW-A clean·SW-C notes·SW-E 기등재 latent) 인라인 수리·회귀 witness 미발생. 따라서 빌드/digest/스위트 델타는 구조적으로 불가능(무변경). 산출 = 본 원장.

## 산출물 위치
`docs/audit/sw_ace_audit_2026-07-22.md` (본 문서). 엔진 소스 변경 없음.
