# GR — 룰 정확성 수정 goal (G13 freeze 전제)

> 근거: `docs/audit/rule_audit_findings.md` (10개 룰 차원 감사, 6게임 635스텝). 깨진 곳은 **2개 서브시스템뿐**(육성, 메모리 음수 턴종료), 나머지 8개 차원은 정상. 이 둘을 고치고 **룰 불변식을 영구 게이트로** 편입한 뒤에야 G13 freeze가 의미를 가진다.
>
> 공통 종료 기준(모든 goal): `bash scripts/run-tests.sh` **전체 green(FAIL=0)** + 해당 룰을 **실제로 단언하는 회귀 테스트** 추가 + `tools/RuleAudit` 재실행 시 해당 위반 카운트 **0**. 커밋은 사용자 지시 시에만.
>
> 표준 규칙 유지: **AS-IS 미러**(룰 동작은 원본 `DCGO/` Unity 소스와 1:1로 대조 — 특히 육성 규칙은 추측 금지하고 원본에서 확인), 엄격 PASS(원본 가드 누락=실패), DCGO/·.dotnet/ 커밋 금지. 이 수정은 **엔진(`Headless/**`) 층 교정**이며 freeze 선언 전 정당한 변경이다.

기준 커밋: `40388596` + 미커밋(G13-003 스모크, tools/RuleAudit). 현재 게이트 226/226(룰 정확성은 미검증).

---

## GR-001 — 메모리 음수 시 턴 종료 강제

**증상(감사):** `MEM_TURN_NOT_ENDED` 34건. mem `0 → -2 → -5 → -8 → -10`까지 한 턴에 연속 플레이.

**근본 원인:** 턴종료 평가 `HeadlessMainPhaseFlow.EvaluateAfterMemoryMutation`(mem ≤ -1 → `MemoryPass` 전이)은 독립 메모리 액션(`SetMemory`/`AddMemory`/`PayMemory`)에서만 호출됨. 실제 플레이 경로 `PlayCardAction`·`DigivolveAction`·`OptionActivateAction`·`SpecialPlayAction`은 `MemoryController.Pay()`를 **직접 호출하고 평가를 안 탐**.

**작업:**
1. 네 액션이 메모리 지불 직후 `EvaluateAfterMemoryMutation`(또는 동등한 턴종료 평가)을 타도록 배선. 가능하면 "지불 + 턴종료 평가"를 한 곳(헬퍼)으로 묶어 중복/누락 방지.
2. 평가 결과 `MemoryPassTriggered`면 페이즈가 `MemoryPass`로 가고, 이후 합법액션이 `EndTurn`(+Blitz 공격)만 남는지 확인.
3. 핸드오버 시 메모리 부호가 상대에게 +|m|로 넘어가는 기존 동작 유지(감사에서 이미 정상).

**DoD:**
- 단언 테스트: 메모리 0에서 코스트≥1 카드를 플레이 → 즉시 `MemoryPass`(또는 턴 넘어감), **같은 플레이어의 추가 코스트-플레이 액션이 합법목록에 없음**, 상대가 +|m| 메모리로 시작.
- `tools/RuleAudit`의 `MEM_TURN_NOT_ENDED` = 0.
- 기존 Blitz/MemoryPass 관련 테스트 무회귀.

---

## GR-002 — 육성(Breeding) 룰 강제

**증상(감사):** `BREED_MOVE_NOT_DIGIMON` 30 + `BREED_HATCH_AND_MOVE_SAME_TURN` 25 + `EGG_OR_L2_IN_BATTLE` 532(하류 증상). 갓 부화한 lv2 디지에그가 배틀존으로 이동.

**근본 원인:** `HeadlessLegalActionDispatcher.BuildBreedingActions` 가 `MoveBreedingToBattle`을 **육성칸이 비어있지 않다는 이유만으로** 제시(레벨/횟수 게이트 없음). 처리부에도 거부 로직 없음.

**작업 — 단, 먼저 원본 룰 확인(AS-IS 미러):**
0. **원본 `DCGO/` 소스에서 육성 규칙을 확인** — 특히 (a) 이동 가능 대상(lv3+ 디지몬), (b) 부화/이동의 턴 제약(같은 턴 동시 가능 여부), (c) 육성칸 내 진화(lv2→lv3). 추측 금지.
1. `MoveBreedingToBattle` 합법성: 육성칸의 카드가 **lv3+ 디지몬**일 때만 제시(DigiEgg/lv2/유아기 제외). 합법액션 생성 + 처리부 검증 양쪽에 게이트(방어적 이중화).
2. 부화/이동 **턴 제약**을 원본대로 강제(once-per-turn / 동시 금지 등 — 0에서 확인한 규칙).
3. 육성칸 내 lv2→lv3 진화 경로가 정상 동작하는지 확인(없으면 정상 흐름 자체가 막힘).

**DoD:**
- 단언 테스트: (a) 갓 부화한 lv2는 이동 불가(합법목록에 없음), (b) 육성칸의 lv3 디지몬은 이동 가능, (c) 원본대로의 부화/이동 턴 제약, (d) 배틀존에 lv2/DigiEgg가 절대 안 생김.
- `tools/RuleAudit`의 `BREED_MOVE_NOT_DIGIMON`·`BREED_HATCH_AND_MOVE_SAME_TURN`·`EGG_OR_L2_IN_BATTLE` = 0.
- 원본 대조 메모를 테스트 주석 또는 `docs/audit/g_breeding_asis_notes.md`에 남김.

---

## GR-003 — 룰 불변식을 영구 회귀 게이트로 편입

**목적:** 이번처럼 "안정성은 통과인데 룰이 틀린" 상황을 영구 차단. `tools/RuleAudit`의 상태-점검 불변식을 **게이트 테스트**로 승격.

**작업:**
1. `tests/GR-003.RuleInvariants.Tests/`(가칭) 신설 — 실 ST1/ST2/ST3 덱으로 랜덤 self-play 소수 시드(예 4~6게임, 적당한 cap)를 돌리며 다음 불변식을 **위반 시 FAIL**:
   - 메모리 음수 후 추가 코스트-플레이 없음 (GR-001)
   - 육성 이동은 lv3+만, 배틀존에 lv2/DigiEgg 없음, 부화/이동 턴 제약 (GR-002)
   - 공격 소환멀미·서스펜드 공격금지·공격자 서스펜드·턴 소유권·옵션→트래시·메모리 범위·핸드오버 부호·디지에그 누출 없음 (현재 정상인 8개 — 회귀 방지)
2. **미확정 항목 확정:** 직접공격 보안 소진 47/55의 나머지 8건을 정밀 점검(보안 0=직접패배/블록이면 정상, 아니면 새 버그) → 결과를 불변식 또는 문서로 확정.
3. `run-tests.sh`가 자동 수집(빠르게 — 게이트 시간 영향 최소화). `tools/RuleAudit`은 더 넓은 진단용으로 유지.

**DoD:**
- GR-001/002 수정 후 새 게이트 테스트 green(위반 0), `run-tests.sh` 전체 green.
- 어떤 불변식을 강제하는지 테스트 상단 주석에 명시.
- 보안 47/55의 8건 정체 확정(정상/버그) 기록.

---

## 시퀀스 & freeze 관계

| Goal | 한 줄 | 게이트 |
|---|---|---|
| GR-001 | 메모리 음수 → 턴 종료 | 단언 테스트 + 감사 MEM_TURN_NOT_ENDED=0 |
| GR-002 | 육성 lv3+ 이동 + 부화/이동 제약(원본 대조) | 단언 테스트 + 감사 육성 위반=0 |
| GR-003 | 룰 불변식 영구 게이트 + 보안 8건 확정 | 게이트 테스트 green + 전체 green |

권장 순서: **GR-001 → GR-002 → GR-003**(작은→큰→고착화). GR-003 완료 시점이 **G13 freeze 선언의 실제 전제 충족**이다(룰 정확성이 게이트로 보장됨).
