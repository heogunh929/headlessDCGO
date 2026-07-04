# IR 파이프라인 설계 (pipeline-v3)

> 상태: **설계 합의본 r2 — 구현 전.** 2026-07-04 (r1 초안 → 설계 대화로 미결 4건 확정).
> 목적: 카드 포팅을 "LLM 1회 번역"에서 **컴파일러형 파이프라인**으로 재구조화.
> LLM은 suggestion/review만, 최종 승인권은 validator. 진실 원천 = Canonical IR (§9 참조).

```
 1. 수집        DCGO C# + cards.json ─────────────┐ (결정론)
 2. Source IR   Roslyn syntax → 무손실 JSON        │ (결정론)
 3. 로워링      expression_map/의도표/심볼표 적용    │ (결정론, 테이블 주도)
 4. 제안        미매핑 조각만 LLM suggestion        │ (LLM, 비구속)
 5. Canonical IR 병합 → 닫힌 어휘 IR 확정           │ (결정론)
 6. Ledger      분기별 커버리지 원장                │ (결정론)
 7. Validator   schema/type/coverage 승인          │ (결정론, 최종 승인권)
 8. Scenario    행동 테스트 생성/승인               │ (템플릿 + LLM 제안)
 9. Codegen     IR → C# 미러 방출                  │ (결정론, 템플릿)
10. Simulation  시나리오 실행 + 빌드/바인딩 게이트    │ (결정론)
11. Registry    RL 학습 카드풀 매니페스트            │ (결정론)
```

LLM이 개입하는 곳은 **4와 8뿐**이고, 두 곳 모두 산출물이 스키마 구속 JSON이라
validator가 기계적으로 수용/반려한다. 나머지 9단계는 전부 결정론적 도구다.

---

## 단계별 명세

### 1. 수집 (ingest)

- **입력**: `DCGO/Assets/Scripts/CardEffect/<SET>/<COLOR>/*.cs`(읽기전용),
  `cards.json`(룰텍스트·스탯, 8187항목).
- **출력**: 카드 레코드 `{id, set, color, ruleText, srcPath, srcHash}`.
- **판정**: 원본 효과 클래스 없음 → **Tier 0 (바닐라)** 확정, 2~10 건너뛰고 11에서 자동 포함.
- **소유**: 결정론 도구. srcHash는 증분 재빌드 키(원본 불변이므로 사실상 1회).

### 2. Source IR 생성 (Roslyn)

- **입력**: 카드 원본 1파일.
- **도구**: `porting/tools/CardIr.Extract/` (C# 콘솔). `CSharpSyntaxTree.ParseText`만 사용 —
  DCGO는 Unity/Photon 참조가 없어 컴파일 불가하지만 **semantic model 불필요**
  (심볼 해석은 3의 테이블이 담당). syntax 수준에서 코루틴·local function·
  named argument·중첩 람다 전부 파싱 가능.
- **출력**: **무손실** Source IR JSON — DCGO 어휘 그대로:
  ```json
  { "card": "BT1_029",
    "branches": [
      { "timing": "OnEnterFieldAnyone",
        "effects": [
          { "kind": "activate",
            "label": "Draw 1",
            "canUse":      { "call": "CardEffectCommons.CanTriggerOnPlay" },
            "canActivate": { "and": [
              { "call": "CardEffectCommons.IsExistOnBattleArea", "args": ["card"] },
              { "cmp": ">=", "lhs": { "member": "card.Owner.LibraryCards.Count" }, "rhs": 1 } ] },
            "coroutine": [
              { "ctor": "DrawClass", "args": ["card.Owner", 1], "invoke": "Draw" } ] } ] } ] }
  ```
- **원칙**: 여기서는 아무것도 번역/판단하지 않는다. 못 알아보는 구문은
  `{"kind":"opaque","syntax":"<원문>"}`로 보존(로워링에서 tier-3 판정 재료).
- 정규식은 파일명/card_id/로그 보조로만. 효과 본문은 전부 Roslyn.

### 3. 로워링 — expression_map 적용

- **입력**: Source IR + 3종 테이블.
- **테이블** (전부 기계가독형으로 재정비, 강모델이 유지보수):
  - **심볼표**: 카탈로그(128 팩토리 + 275 커먼즈)에서 자동 생성 — 이름→시그니처.
    `porting/scripts/generate-primitive-catalog.py`가 md와 함께 `porting/data/symbols.json`도 방출하게 확장.
  - **의도표**: 코루틴 ctor/호출 패턴 → Canonical op (PORTING-RECIPE §4-b의 기계화).
  - **expression_map**: Source 술어 표현 → **predicate AST atom** 매핑.
    ⚠ EXPRESSION-MAP.md는 실행 문법이 아니라 **Source IR → Canonical IR 매핑 테이블**이다.
- **출력**: 부분 로워링된 IR + **미매핑 조각 목록**(fragment 단위, 분기 좌표 포함).
- **소유**: 결정론. 테이블에 없으면 절대 추측하지 않고 조각을 4로 넘긴다.

### 4. LLM suggestion (미매핑 조각만)

- **입력**: 미매핑 조각 1개 + 해당 분기의 Source IR 문맥 + 관련 카탈로그 슬라이스
  (v2 브리프 생성 로직 재활용 — 조각당 1~3KB).
- **출력**: `suggestions/<ID>.<fragmentId>.json`:
  ```json
  { "fragment": "BT1_029/branch0/canActivate/cmp0",
    "proposal": { "atom": "LibraryCardsCount", "subject": "self", "cmp": ">=", "value": 1 },
    "evidence": ["catalog:LibraryCardsCount(row 412)", "expression_map:row 12"],
    "confidence": "high|low" }
  ```
- **규율**: **파일에 쓰지 않는다. 제안 큐에만 쓴다.** evidence가 실재하지 않으면
  validator가 자동 반려(인용 검증). 반려/무제안 = typed STOP.
- **모델**: Tier 2 조각 = 로컬 추론모델(gemma4:31b 캘리브레이션 결과 반영),
  Tier 3 경계 조각 = 강모델. 조각 단위라 컨텍스트 문제 소멸.

### 5. Canonical IR 확정

- **입력**: 3의 로워링 결과 + 4의 **승인된** 제안.
- **출력**: `porting/data/ir/<SET>.<COLOR>/<ID>.json` — **닫힌 어휘**:
  - `op`: 카탈로그 심볼표에 존재하는 것만.
  - `predicate`: 화이트리스트 atom의 AST 조합 (`and`/`or`/`not` + atom).
    atom은 **subject 타입**(`self`|`permanent`|`card`)을 갖고, op의 술어 슬롯과
    타입이 일치해야 한다 (`condition`←self, `permanentCondition`/`defenderCondition`←permanent).
  - 로워링 불가 분기는 **typed STOP 노드**로 IR 안에 남는다(조용한 드롭 불가):
    ```json
    { "timing": "OnAllyAttack", "stop": { "type": "missing-op", "symbol": "IUnsuspendPermanents" } }
    ```
- **atom 화이트리스트 v0**: 발명하지 않는다 — 기존 라이브 미러 44장 + ST1~3에서
  실사용 술어를 채굴해 초기 집합을 만든다.

#### predicate atom 3층 구조 (확정)

| 층 | 규율 | 어휘 팽창 압력의 처리 |
|---|---|---|
| **base** | 닫힌 집합(예상 30~50개). 각 atom은 엔진에 실증된 조회 헬퍼(CardEffectCommons)와 **1:1 + 테스트 보유** 필수. 추가 권한은 강모델만 — "헬퍼 구현+테스트+등재"가 한 세트 | 팽창 금지 |
| **macro** | base의 매개변수화 합성(`and`/`or`/`not` 조합)을 **선언적 매크로 테이블**로 정의. validator는 검사 전 base 형태로 전개. **semantic hash는 전개형 기준** — 매크로 리팩토링이 의미 해시를 안 바꿈 | 팽창을 여기로 흡수 (안전 — 어차피 base로 전개) |
| **candidate** | LLM 제안 산물, **실행 불가** — Canonical IR 진입 금지, ledger에 stop(stage: `missing-rule`)으로만 존재. 빈도 집계 = base/macro 승격 백로그 | 미지 어휘의 격리 구역 |

### 6. Coverage ledger

- **출력**: `porting/data/ledger/<SET>.<COLOR>.json` — 카드×분기×조각 단위 원장:
  `lowered | suggested-accepted | stop:missing-op | stop:missing-rule | stop:tier-3 | pending-suggestion`
- **의미**: 손으로 쓰는 `porting/stop/*.md`를 **기계 생성 진단**으로 대체.
  `stop:missing-op` 집계 = 강모델 프리미티브 선행개발 큐가 자동으로 나온다.
  `stop:missing-rule` 집계 = 테이블 보강 백로그.
- 사람이 읽는 요약 md는 ledger에서 렌더링(원장이 진실).

### 7. Validator (최종 승인권)

카드가 codegen 대상이 되기 위한 관문. 전부 결정론:

| 검사 | 내용 | 죽이는 실패모드 (실증됨) |
|---|---|---|
| schema | Canonical IR JSON Schema 적합 | 자유 문자열 expr 침투 |
| symbol | 모든 op가 심볼표에 존재 + 인자 수/타입 일치 | 팩토리 발명 (qwen/gemma BT1_100) |
| predicate typing | atom subject ↔ 술어 슬롯 타입 일치 | 잘못된 수신자 호출 (BT1_044) |
| timing | EffectTiming 열거형 유효성 | 존재하지 않는 타이밍 |
| **coverage** | Source IR의 모든 분기가 lowered 또는 typed STOP | **효과 조용한 드롭 (BT1_040)** |
| evidence | 4의 제안이 인용한 근거 실재 | 그럴듯한 발명 제안 |

- 통과 → `validated`. 실패 → ledger에 사유 기록, codegen 불가.
- LLM 산출물이든 테이블 산출물이든 **동일하게** 검사한다(경로 무관 동일 관문).

### 8. Scenario test 생성/승인

- **목적**: 컴파일·바인딩(구조 검증)을 넘어 **행동 검증** — RL 카드풀 포함의 근거.
- **생성 2경로**:
  - **템플릿 자동생성** (결정론): 시나리오 템플릿이 있는 op는 IR에서 직접 방출.
    예: `DrawCardsEffect(n:1)` → `{setup: 기본반, trigger: OnPlay, expect: hand +1, deck -1}`,
    `AddMemoryTriggerEffect(+3)` → `expect: memory +3`. 공통 op 상위 ~20개면 커버리지 대부분.
  - **LLM 제안** (비구속): 템플릿 없는 복합 카드만. 스키마 구속 시나리오 JSON +
    룰텍스트 인용. 승인은 validator(스키마·상태식 유효성) + 필요시 강모델 검토.
- **출력**: `porting/data/scenarios/<SET>.<COLOR>/<ID>.json` (카드당 0..n개; 0개 = 구조검증만으로 포함 판단).
- **⑤ 확장(리뷰어)**: 같은 단계에서 LLM이 Canonical IR ↔ 공식 룰텍스트 대조 리뷰
  리포트(비구속)를 생성 — DCGO 구현 자체의 버그 탐지 보너스.

#### 승인 정책 (확정): 초기 자동 승인 금지 → family 단계 승격

- 템플릿 시나리오도 **초기엔 전수 강모델 검토**. 승격은 템플릿 family 단위:
  `scenario-families.json`에 family별 상태 `manual → auto-tier01 → auto-all`.
- **승격 조건(정량)**: 서로 다른 카드 ≥10장에서 해당 family 생성 시나리오가
  강모델 **무수정 승인** + 시뮬레이션 실패 0. 이후 수정 사례 1건 발생 시 **자동 강등**.
- 자동 승인돼도 **⑩ 시뮬레이션은 전수 실행** — 승인은 검토 생략이지 신뢰 부여가 아니다.

### 9. Codegen — IR → C# 미러

- **입력**: validated Canonical IR + 심볼표 시그니처 + 미러 뼈대 템플릿.
- **출력**: `src/.../CardEffect/<SET>/<COLOR>/<ID>.cs` — 순수 템플릿 확장.
  namespace/sealed/IReadOnlyList/접두/시그니처 오류가 **원리적으로 불가능**.
  STOP 노드 → `// STOP: <type> <symbol> — 강모델` 주석 방출(현행 게이트 규약 유지).
- **규율(신규)**: **미러 수기 편집 금지** — 미러는 빌드 산출물. 수정은 IR 또는
  테이블에서 하고 재생성한다. 파일 헤더에 `// GENERATED FROM porting/data/ir/... — DO NOT EDIT` 명시.
- **이행기**: 기존 라이브 44장은 당분간 수기 원본 유지(레지스트리에 `provenance: handwritten`).
  추후 2단계 도구를 헤드리스 미러에도 돌려 역파싱 → IR화 → 재생성 일치 확인으로 흡수.

### 10. Scenario simulation + 게이트

- 빌드 green + `CardEffect.Binding.Auto`(현행: 발견/inert/namespace 대조) — 유지.
- **신규**: `porting/binding-test/` 옆 `CardEffect.Scenario.Tests/` — 시나리오 JSON을 읽어
  `EngineContext.CreateDefault` 위에서 setup→trigger→expect 단언. 데이터 주도 하네스 1개.
- 실패는 ledger로 환류(`sim-failed` 상태) — 포함 불가 + 원인 조각 좌표.

### 11. Registry — RL 학습 카드풀

- **출력**: `porting/data/cardpool.json` (결정론 재생성, 버전 스탬프):
  ```json
  { "BT1_029": { "tier": 2, "provenance": "ir", "irHash": "…",
                 "validator": "pass", "scenarios": "3/3", "gate": "green",
                 "included": true },
    "BT1_115": { "included": false, "reason": "stop:missing-op IUnsuspendPermanents" },
    "BT1_009": { "tier": 0, "included": true, "reason": "vanilla" } }
  ```
- **포함 규칙(초안)**: Tier 0 자동 포함 / validated + gate green + 시나리오 전승(있는 경우) → 포함 /
  STOP 잔존 분기 카드 → **불포함**(부분 효과 카드로 학습하면 RL이 왜곡된 가치를 배움 —
  전분기 green이어야 포함. 이 규칙이 맞는지는 미결 §참조).
- RL 학습기는 registry만 소비. 사람은 registry를 손대지 않는다.

---

## Tier 분류 (2~3 산출물로 자동 판정)

| Tier | 기준 (기계 판정) | 경로 |
|---|---|---|
| 0 | 원본 효과 클래스 없음 (바닐라) | 11 직행, 자동 포함 |
| 1 | 코루틴 0 + 전 심볼 로워링 성공 | 3→5→7→9 전자동 (LLM 0) |
| 2 | 코루틴 있음 + 의도표/제안으로 로워링 가능 | 4 경유 (조각만 LLM) |
| 3 | opaque 구문 / 특수플레이 레시피 / 중첩 커스텀 | 강모델 큐 (ledger 집계) |

## STOP 분류 — 2축 모델 (확정)

하나의 STOP 레코드에 직교하는 두 축을 싣는다:

```json
{ "fragment": "BT1_115/branch0",
  "stage": "lowering:missing-op",          // 파이프라인 축 — 기계 판정 (정확)
  "code": "STOP_MISSING_PRIMITIVE",        // 도메인 축 — 규칙→LLM→사람 순 폴백 분류
  "symbol": "IUnsuspendPermanents",
  "confidence": "high" }
```

**stage 축** (어느 단계가 왜 반려했나 — 기계 100% 판정):
`lowering:missing-op` · `lowering:missing-rule` · `lowering:tier-3` · `validator:<검사명>` · `suggestion-rejected` · `sim-failed`

**code 축** (게임 메커니즘상 무엇이 문제인가 — 10코드 + confidence 플래그):

| code | 설명 | 판정 주체 |
|---|---|---|
| `STOP_MISSING_PRIMITIVE` | RL 엔진 primitive로 표현 불가 | 기계 (심볼 조회 실패) |
| `STOP_REPLACEMENT_EFFECT` | 삭제/진화/배틀 "대신" 대체 효과 | 기계 (DCGO 패턴 시그니처) |
| `STOP_IMMUNITY_OR_PREVENTION` | 효과 면역·삭제 방지 등 지속 룰 | 기계 (패턴) |
| `STOP_SEARCH_REVEAL_SHUFFLE` | reveal/search/choose/shuffle 흐름 복잡 | 기계 (패턴) |
| `STOP_PRIVATE_ZONE` | 덱/시큐리티/손패 비공개 영역 처리 | 기계 (존 심볼 접근) |
| `STOP_COMPLEX_TIMING` | 트리거 큐·동시 발동·우선권·pending | 기계 부분 + LLM |
| `STOP_SPECIAL_PLAY` | DigiXros/DNA/Jogress/Assembly 레시피 | 기계 (명명된 범주) |
| `STOP_MULTI_STEP_OPTIONAL` | may/if you do/then 복합 연결 | LLM 리뷰어(⑧) |
| `STOP_RULE_AMBIGUOUS` | 카드 텍스트/룰 해석 애매 | LLM 리뷰어(⑧) |
| `STOP_SOURCE_INCONSISTENCY` | 카드텍스트↔DCGO↔룰DB 불일치 | LLM 리뷰어(⑧) |
| `STOP_ENGINE_ARCHITECTURE` | 엔진 구조 변경 필요 | **강모델 제안 → 사용자 승인** |
| (플래그) `confidence: low` | 모델 확신 낮음/리뷰 모델 간 불일치 — 코드가 아니라 모든 코드에 얹히는 플래그 | 메타 신호 |

**라우팅 테이블** (code → 소비 큐):

| code | 큐 / 처리 |
|---|---|
| MISSING_PRIMITIVE, IMMUNITY, SEARCH_REVEAL, PRIVATE_ZONE | 강모델 프리미티브 선행개발 — ledger **빈도 가중 정렬** ("이 심볼이 N장 막음") |
| COMPLEX_TIMING, REPLACEMENT, SPECIAL_PLAY, ENGINE_ARCHITECTURE | 엔진 로드맵 — 카드 단위 아닌 **묶음 설계** 대상 |
| RULE_AMBIGUOUS, SOURCE_INCONSISTENCY | **사람(사용자) 판정 큐** — 정본 행동 결정 필요 |
| confidence: low | 상위 모델 재리뷰 자동 에스컬레이션 |

**포함 정책과의 관계 (확정)**: STOP 잔존 카드는 **code 불문 카드풀 제외**.
code의 가치는 부분 포함 허용이 아니라 해소 작업의 데이터 기반 우선순위화다
("MISSING_PRIMITIVE 상위 3심볼 구현 → 41장 진입" 식 의사결정이 ledger에서 직접 도출).

## 기존 자산 재배치

| 기존 | v3에서 |
|---|---|
| `porting/scripts/make-card-brief.py` | 2+3+4-문맥생성으로 흡수 (폐기 예정) |
| `PRIMITIVE-CATALOG.md` | 심볼표의 렌더링 뷰 (`porting/data/symbols.json`이 원본이 됨) |
| `EXPRESSION-MAP.md` | 로워링 테이블 (기계가독 재포맷) |
| `PORTING-RECIPE.md` §4-b | 의도표 (기계가독 재포맷) |
| `CardEffect.Binding.Auto` | 10의 구조 게이트로 존속 |
| `porting/scripts/port-batch.sh` / opencode porter | 4의 제안 드라이버로 개조 (파일 쓰기 권한 회수) |
| `porting/stop/*.md` | ledger 렌더링 뷰로 대체 |

## 구축 순서 (MVP 우선 — 전 단계 동시 구축 금지)

- **Phase A (골격 증명)**: 2, 3, 5, 7, 9 최소판 — Tier 1 카드만, LLM 없음.
  검증: BT1 Blue의 정적 카드들이 IR 경유로 재생성되어 현행 게이트 green +
  기존 수기 미러와 의미 동일 diff.
- **Phase B (원장·풀)**: 6, 11 — ledger/registry 생성, stop md를 뷰로 전환.
- **Phase C (LLM 조각 제안)**: 4 — Tier 2 개방. gemma4 캘리브레이션 프로토콜 재사용.
- **Phase D (행동 검증)**: 8, 10 — 시나리오 템플릿 상위 op부터.

각 Phase 종료 = 게이트 green + 이전 Phase 산출물 회귀 없음.

## 버저닝·재현성 (확정)

- **IR 스키마 = semver**: major = IR 형태 파괴(**마이그레이터 필수**), minor = 가산적
  (신규 base atom·선택 필드), patch = 메타/문서.
- **semantic hash**: 정규화(매크로 전개 + 키 정렬)된 Canonical IR에 대한 해시 = `irHash`.
  registry가 카드별로 기록.
- **generated drift check**: CI가 코드젠을 재실행해 커밋된 미러와 diff — 불일치 = FAIL.
  부수 효과로 **"미러 수기 편집 금지" 규율이 기계적으로 강제**된다(사람이 미러를
  고치면 drift가 잡힘).
- **의미 변경은 in-place 금지**: predicate/카탈로그 심볼의 의미가 바뀌면
  **새 심볼 신설(V2) → IR 명시 마이그레이션 → 구심볼 deprecated 플래그(validator
  통과+경고) → 전량 이전 후 제거**. 기존 카탈로그의 AS-IS 이름 보존 관행과 일관.
- **테이블 버저닝**: 로워링 테이블(expression_map/의도표/심볼표)에도 버전 스탬프.
  ledger·registry 레코드는 `{irSchemaVer, tableVer, irHash}`를 기록 — "이 카드가 왜
  저번과 다르게 로워링됐나"를 항상 추적 가능(RL 카드풀 감사 가능성).

## 확정된 설계 결정 요약 (r2)

1. **포함 정책**: STOP 잔존 카드는 code 불문 제외. 전분기 green만 카드풀 진입.
2. **predicate atom**: base(닫힘·엔진 실증 1:1) / macro(선언적 합성, 전개 검증) /
   candidate(실행 불가, 승격 백로그) 3층.
3. **시나리오 승인**: 초기 전수 강모델 검토 → family 정량 조건(≥10장 무수정 승인 +
   sim 실패 0) 충족 시 Tier 0/1부터 제한 자동 승인, 수정 1건 발생 시 자동 강등.
4. **버저닝**: semver + migrator + semantic hash + generated drift check.
   의미 변경 = 새 심볼 + migration.
5. **STOP**: stage×code 2축, 10코드 + confidence 플래그, 라우팅 테이블.
   ENGINE_ARCHITECTURE 판정은 강모델 제안 → 사용자 승인.
6. **진실 원천 = Canonical IR**, 미러는 생성물(drift check로 강제).
   LLM은 ④제안·⑧리뷰에서만, 항상 비구속 + evidence 인용 의무.
