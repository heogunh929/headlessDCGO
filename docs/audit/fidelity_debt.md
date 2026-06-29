# 카드 포팅 충실도 부채 레저 (fidelity debt)

- 작성일: 2026-06-30
- 방법: **원본 `DCGO/.../<id>.cs` ↔ 포팅 `src/.../<id>.cs` 카드별 코드 대조** (추정 없음, 병렬 감사 에이전트 3 + 직접 1). 기준: [[asis-structure-mirror-rule]] = 카드-facing 로직 **1:1**.
- 판정:
  - **PASS** — 원본과 1:1 (형태 매핑 차이는 허용: `Func<Permanent,bool>`→`Func<HeadlessEntityId,bool>`, inline ActivateClass→팩토리 헬퍼).
  - **PARTIAL** — 일부 타이밍 분기는 1:1, 다른 분기는 미구현(주로 테이머 [Security] = `PlaySelfTamerSecurityEffect` stub).
  - **FAIL** — 카드 코드 자체가 원본과 다름(게이트/조건/임계값 누락·단순화).
  - **STUB** — 카드 전체가 무동작(`DeferredCardEffect`).
- **구분**: "활성효과가 실루프에서 자동 발동 안 됨(imperative 검증만)"은 **엔진 통합 갭**이지 카드-코드 실패가 아님 → 별도 열.
- **PASS 기준 (엄격)**: 원본 가드를 뺐으면 **발동 빈도·중복성·"희귀 엣지"는 PASS 근거가 아님 = FAIL.** 단, 그 가드와 **동일한 동작을 엔진이 다른 방식으로 강제함을 *검증*한 경우에 한해** PASS(추측 금지). 예: ST3_05 `CanAddMemory` → `InMemoryHeadlessMemoryController.Clamp`(상한 강제, 검증); ST3_09 `Library>=1` → `MoveFromZoneTop`이 가용분 클램프(빈 덱 no-op, 검증).

## 요약 (포팅 파일 35장)

| 판정 | 장수 | 비율 |
|---|---|---|
| **PASS (1:1)** | **24** | 69% |
| PARTIAL | 3 | 9% |
| FAIL | 7 | 20% |
| STUB | 1 | 3% |

**진짜 1:1 = 24/35 (69%).** 부채 = **11장**(PARTIAL 3 + FAIL 7 + STUB 1).
별칭(파일 없음): ST2_07·ST3_07 → `ST1_06`(PASS) 재사용 — dispatch 배선(G9-001) 전제.

---

## 카드별 레저

### ST1/Red (10 PASS · 1 PARTIAL · 1 FAIL)
| 카드 | 판정 | 카드-코드 부채 | 엔진통합 노트 |
|---|---|---|---|
| ST1_01·03·07·11 | PASS | — | — |
| ST1_06 | PASS | — | OnAllyAttack 메모리 트리거 plumbing 완화(recipe §5) |
| ST1_08·13·14·16 | PASS | — | 활성/선택 imperative 해소(미배선) |
| ST1_09 | PASS | — | OnBlockAnyone 트리거 plumbing |
| **ST1_12** | **PARTIAL** | [Security] `PlaySelfTamerSecurityEffect` = stub (메인 player-DP는 1:1) | 테이머 시큐리티 플레이 미배선 |
| **ST1_15** | **FAIL** | 삭제 임계값 `card.Owner.MaxDP_DeleteEffect(4000)`(동적·상승가능) → 고정 `<=4000` | 선택→삭제 imperative |

### ST2/Blue (5 PASS · 1 PARTIAL · 4 FAIL · 1 STUB) + 별칭 ST2_07
| 카드 | 판정 | 카드-코드 부채 | 엔진통합 노트 |
|---|---|---|---|
| **ST2_03** | **FAIL** | 대상 가드 누락: `DigivolutionCards.Count(!CanNotTrashFromDigivolutionCards)>=1`(보호 안 된 전위카드만) → 포팅 "전위카드 있음"; `TopCard.HasLevel` 누락. (엔진에 protected-source 개념 없음 = 진짜 부재, 빈도 무관) | 활성 imperative |
| ST2_06 | PASS | — (원본 select=상대 디지몬만, 포팅 일치) | 활성 imperative |
| **ST2_09** | **FAIL** | 대상 가드 누락: `!CanNotTrashFromDigivolutionCards>=1`(보호 안 된 전위카드만) → 포팅 "전위카드 있음" | 활성 imperative |
| ST2_08 | PASS | — | — |
| ST2_13·14·16 | PASS | — | 활성/시큐리티 imperative |
| ST2_07 | PASS(별칭) | — (→ST1_06) | effectClass dispatch 전제 |
| **ST2_01** | **FAIL** | 배틀-페어링 누락: 원본은 *이 카드가 싸우는 특정 적*(`PermanentOfThisCard().battle.enemyPermanent`) 무전위 판정 → 포팅은 "무전위 상대 아무나" | — |
| **ST2_11** | **FAIL** | once-per-turn 누락(`SetHashString`+order=1) → 매 공격 발동 | once-flag 미배선 |
| **ST2_12** | **PARTIAL** | [Security] `PlaySelfTamerSecurityEffect` stub (OnStartTurn 메모리는 1:1) | 테이머 시큐리티 미배선 |
| **ST2_15** | **STUB** | [Main] play-from-under 전체 `DeferredCardEffect`(선택→카드선택→플레이 체인 전무); [Security]는 Main 재사용이라 동반 무동작 | 특수플레이 미배선 |

### ST3/Yellow (8 PASS · 1 PARTIAL · 2 FAIL) + 별칭 ST3_07
| 카드 | 판정 | 카드-코드 부채 | 엔진통합 노트 |
|---|---|---|---|
| ST3_05 | PASS | `CanAddMemory` 누락이나 **메모리 Clamp 상한이 엔진에서 강제됨(검증)** → 동일 동작 | — |
| ST3_08·11 | PASS | — | 활성 imperative |
| ST3_09 | PASS | `Library>=1`/`CanAddSecurity` 누락이나 **`MoveFromZoneTop` 가용분 클램프=빈 덱 no-op(검증)** → 동일 동작 | — |
| ST3_13·14·15·16 | PASS | — | 활성/시큐리티 imperative |
| ST3_07 | PASS(별칭) | — (→ST1_06) | effectClass dispatch 전제 |
| **ST3_01** | **FAIL** | 트리거 전제 `CanTriggerOnPermanentDeleted`(상대 디지몬) + `IsDPZeroDelete`(0DP격파) 누락, **및** once-per-turn 누락 → 아무 격파에 무제한 발동 | — |
| **ST3_04** | **FAIL** | ST3_01과 동일(메모리+1판): `IsDPZeroDelete`·`CanTriggerOnPermanentDeleted`·once-per-turn 누락 | — |
| **ST3_12** | **PARTIAL** | [Security] `PlaySelfTamerSecurityEffect` stub ([All Turns] 시큐리티존 +2000은 1:1) | 테이머 시큐리티 미배선 |

### ST7/Red
| 카드 | 판정 | 부채 |
|---|---|---|
| ST7_10 | PASS | — (원본과 동일) |

---

## 부채 9장 — 갚는 법 (유형별)

| 유형 | 카드 | 필요 작업 | 난이도 |
|---|---|---|---|
| **once-per-turn** | ST2_11, ST3_01, ST3_04 | `OnceFlagHelpers`(엔진에 존재) 연결 — 트리거 효과에 once-flag 게이트 | 낮음 |
| **트리거 전제(0DP격파·상대디지몬)** | ST3_01, ST3_04 | OnDestroyedAnyone emit에 delete-context(원인·대상소유자) 실어 `IsDPZeroDelete`/`CanTriggerOnPermanentDeleted` 복원 | 중 |
| **배틀-페어링** | ST2_01 | 연속 평가에 "이 카드가 싸우는 적" 컨텍스트 필요 | 중~상 |
| **동적 삭제 임계값** | ST1_15 | `MaxDP_DeleteEffect` 상승-가능 임계값 서브시스템 | 중 |
| **보호 전위카드(protected source)** | ST2_03, ST2_09 | `CanNotTrashFromDigivolutionCards` 개념 도입 후 대상 가드를 "보호 안 된 카드 ≥1"로 복원 (+ST2_03 `TopCard.HasLevel`) | 중 |
| **테이머 시큐리티 플레이** | ST1_12, ST2_12, ST3_12 | `PlaySelfTamerSecurityEffect` 실구현(시큐리티에서 테이머 플레이 플로우) | 중~상 |
| **play-from-under** | ST2_15 | 밑카드→다른 Digimon 코스트없이 플레이(특수플레이) 서브시스템 | 상 |

## 엔진 통합 갭 (카드-코드 실패 아님, 별도 트랙)
- **활성화 풀 루프**: 옵션/시큐리티/선택 효과(~15장)가 `IHeadlessCardEffect.ResolveAsync`에 choice provider 미주입이라 실게임 루프에서 자동 발동 안 됨(현재 imperative 검증만). 카드 코드는 1:1.
- 트리거 plumbing 일부 완화(recipe §5).

## 권장 처리 순서 (수요 기반)
1. **once-per-turn 3장** — `OnceFlagHelpers` 연결, 가장 싸고 "실제보다 강함" 버그 제거.
2. **ST3_01/04 트리거 전제** — 0DP격파 컨텍스트(같은 작업으로 2장).
3. **테이머 시큐리티 3장** — `PlaySelfTamerSecurityEffect` 한 번 구현 → 3장 동시 해결.
4. **ST1_15 임계값 / ST2_01 배틀페어링** — 해당 메커니즘 실제 필요 시.
5. **ST2_15 play-from-under** — 특수플레이 서브시스템 착수 시.
