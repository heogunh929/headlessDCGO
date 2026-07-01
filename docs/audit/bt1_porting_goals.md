# BT1 포팅 goal — Booster Set 1 (88 카드, 색상별)

> **목표:** 원본 DCGO BT1 카드 효과 88장을 헤드리스로 **AS-IS 1:1 포팅**. 현재 88장 전부 7줄 스켈레톤 스텁(`// TODO: Skeleton only`) + 카드 데이터(JSON)·dispatch 배선은 존재 → per-card 효과 로직만 채우면 됨. EX8_074에서 확립한 표준(`docs/audit/card_porting_standard.md`)과 이번까지 쌓인 프리미티브를 재사용한다.
>
> **scope (원본 `DCGO/Assets/Scripts/CardEffect/BT1/` 기준):** Red 22 · Blue 22 · Yellow 20 · Green 23 · White 1 = **88장**. 난이도(원본 줄 수): trivial(<80줄) 49 · medium(80–199줄) 39 · heavy 0. (EX8보다 평이 — 400줄급 괴물 없음.)

## 공통 종료 기준 (모든 서브goal 공통)
- `bash scripts/run-tests.sh` 전체 green(FAIL=0) + **발동/동작을 단언하는 테스트** + `tools/RuleAudit` 위반 0.
- **AS-IS 미러**: 원본 `Script/`의 파일 위치·팩토리/메서드 이름·시그니처·논리 분해까지 1:1(행동만 아님). 카드-facing은 엔티티-id 술어 관용. 가드 완화·추측 = FAIL(엄격 PASS, `card_porting_standard.md` §2).
- **probe-first**: 새 프리미티브 전 엔진에 이미 메커니즘 있는지 확인("엔진 있음 / 등록 경로 없음" 패턴 반복). `Headless/**`는 change-control.
- **순차 실행**: 아래 서브goal을 순서대로, **이전 서브goal이 완전히 green이 된 뒤에만** 다음으로. `/goal BT1-<ID>`로 실행.

## 기준 / 현황
- 기준 HEAD: `d8b9daba`(+ 미커밋 EX8_074 owner-scope fix · brick 2b). 전체 **246 프로젝트 green, RuleAudit 0**.
- BT1 미러: `src/HeadlessDCGO.Engine/Assets/Scripts/CardEffect/BT1/<Color>/BT1_*.cs` (88 스텁).
- BT1 데이터: `Assets/CardBaseEntity/BT1/**` + `cards.json` 존재. 리플렉션 dispatch는 카드번호 `BT1_xxx`로 해소.

---

## 프리미티브 재사용 매핑 (probe 결과)

| 원본 심볼 (BT1 사용) | 헤드리스 대응 | 상태 |
|---|---|---|
| `ActivateClass` (86회) | `ActivatedSelectEffect` / `ActivatedEffectResolver` | ✅ 있음 |
| `ChangeSelfDPStaticEffect` (11) / `ChangeSelfSAttackStaticEffect` (4) | self-static 키워드 팩토리 | ✅ 있음 |
| `PlaySelfTamerSecurityEffect` (5) | `PlayThisCardToBattleEffect` (G10-003) | ✅ 있음 |
| `SuspendPermanentsClass` (4) | `SelectPermanentEffect.Mode.Tap` | ✅ 있음 |
| `PierceSelfEffect` (4) / `JammingSelfStaticEffect` / `BlockerSelfStaticEffect` | 키워드 self-static 팩토리 | ✅ 있음 |
| `DrawClass` (13) | 엔진 `DrawCards` 액션은 있으나 **카드-facing `DrawEffect` 없음** | ❌ 신설 |
| `SetMemoryTo` (4) | `MemoryController.Set` 있으나 카드-facing 효과 없음 | ❌ 신설 |
| `EoTLose` (2) | `EffectTiming.OnEndTurn` 있으나 메모리-손실 효과 없음 | ❌ 신설 |
| `CanNotBeBlockedStaticSelfEffect` (1) | 키워드 게이트 미확인 | ❌ 신설/매핑 |
| `SimplifiedSelectCardConditionClass` (6) | 존 선택 술어 래퍼 없음 | ❌ 신설 |

→ **5개 신설 프리미티브**가 색상 포팅의 선결. 대부분 "엔진 메커니즘 있음 / 카드-facing 등록 경로 없음" — 신설 비용 낮음.

---

## BT1-0 — 선결 프리미티브 → **`bt1_3_prereq_primitives_goals.md`로 통합**

BT1의 선결 프리미티브(Draw·SetMemoryTo·EoTLose·CanNotBeBlocked·SimplifiedSelect)는 BT2·BT3와 **공유**되므로, 세트별로 만들지 않고 **BT1–3 합집합 선결 goal**(`docs/audit/bt1_3_prereq_primitives_goals.md`, 16종 신설)에서 한 번에 처리한다. → 색상 포팅(BT1-RED 이하) 전에 `BT-PRE-A/B/C`가 게이트.

---

## BT1-RED — 22장 (trivial 14 · medium 8)

trivial 먼저, 그다음 medium. 각 카드 원본 `DCGO/.../BT1/Red/BT1_xxx.cs` 1:1.
- trivial: `BT1_001 002 010 012 015 016 018 021 022 026 085 090 091 092`
- medium: `BT1_011 017 023 025 093 094 095 114`
- 종료: 전체 green + 대표 카드 라이브 단언 테스트(예: Pierce/Draw/DP 버프 각 1) + RuleAudit 0.

## BT1-BLUE — 22장 (trivial 15 · medium 7)
- trivial: `BT1_003 004 029 030 031 033 034 035 036 039 040 097 099 101 115`
- medium: `BT1_041 043 044 086 096 098 100`

## BT1-YELLOW — 20장 (trivial 9 · medium 11)
- trivial: `BT1_005 006 046 048 049 102 105 106 107`
- medium: `BT1_053 054 055 056 060 061 062 063 087 103 104`

## BT1-GREEN — 23장 (trivial 11 · medium 12)
- trivial: `BT1_007 008 068 070 073 076 077 081 083 088 089`
- medium: `BT1_066 067 074 078 079 082 108 109 110 111 112 113`

## BT1-WHITE — 1장 (medium)
- `BT1_084`

---

## 진행 요약 (체크리스트)
- [ ] **선결**: `BT-PRE-A/B/C` (공유, `bt1_3_prereq_primitives_goals.md`)
- [ ] **BT1-RED** 22
- [ ] **BT1-BLUE** 22
- [ ] **BT1-YELLOW** 20
- [ ] **BT1-GREEN** 23
- [ ] **BT1-WHITE** 1
- 누적: 0 / 88 포팅 · 부채 0 (레저 `fidelity_debt.md`에 BT1 섹션 추가하며 "진짜 1:1 N / 부채 M" 보고)

## /goal 연동
`/goal BT1-0` → `/goal BT1-RED` → … 순차. 각 goal은 이 문서의 해당 절 스펙대로(AS-IS 1:1 확인 → probe → 미러 → green + 단언 테스트 + RuleAudit 0). 색상 goal이 크면 trivial-batch/medium-batch로 쪼개 각각 green 게이트.
