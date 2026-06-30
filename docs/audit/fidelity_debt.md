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
| **PASS (1:1)** | **35** | 100% |
| PARTIAL | 0 | 0% |
| FAIL | 0 | 0% |
| STUB | 0 | 0% |

**진짜 1:1 = 35/35 (100%).** 부채 = **0** — G10 전부 상환.
**복원 진행**: G10-001(ST2_11 once-per-turn) · G10-002(ST3_01·ST3_04 0DP-격파 전제+once) · G10-003(테이머 [Security] 플레이) · G10-004(protected-source) · G10-005(동적 삭제 임계값) · G10-006(배틀-페어링) · G10-007(ST2_15 play-from-under `PlayDigivolutionAsDigimon`) 완료. ✅ ST1/ST2/ST3 진짜 1:1 35/35.
별칭(파일 없음): ST2_07·ST3_07 → `ST1_06`(PASS) 재사용 — dispatch 배선(G9-001) 전제.

---

## 카드별 레저

### ST1/Red (12 PASS · 0 PARTIAL · 0 FAIL)
| 카드 | 판정 | 카드-코드 부채 | 엔진통합 노트 |
|---|---|---|---|
| ST1_01·03·07·11 | PASS | — | — |
| ST1_06 | PASS | — | OnAllyAttack 메모리 트리거 plumbing 완화(recipe §5) |
| ST1_08·13·14·16 | PASS | — | 활성/선택 imperative 해소(미배선) |
| ST1_09 | PASS | — | OnBlockAnyone 트리거 plumbing |
| ST1_12 | **PASS** | (G10-003) [Security] `PlaySelfTamerSecurityEffect` 실구현 → 테이머를 배틀에리어로 플레이(+효과 자동등록) | — |
| ST1_15 | **PASS** | (G10-005) 동적 삭제 임계값 `MaxDpDeleteThreshold(card, 4000)`(기본+상승 누적) 복원 | 선택→삭제 imperative |

### ST2/Blue (11 PASS · 0 PARTIAL · 0 FAIL · 0 STUB) + 별칭 ST2_07
| 카드 | 판정 | 카드-코드 부채 | 엔진통합 노트 |
|---|---|---|---|
| ST2_03 | **PASS** | (G10-004) protected-source 가드 복원: `HasTrashableDigivolutionCards`(보호 안 된 전위카드 ≥1) + `TopCardHasLevel` | 활성 imperative |
| ST2_06 | PASS | — (원본 select=상대 디지몬만, 포팅 일치) | 활성 imperative |
| ST2_09 | **PASS** | (G10-004) protected-source 가드 복원: `HasTrashableDigivolutionCards` | 활성 imperative |
| ST2_08 | PASS | — | — |
| ST2_13·14·16 | PASS | — | 활성/시큐리티 imperative |
| ST2_07 | PASS(별칭) | — (→ST1_06) | effectClass dispatch 전제 |
| ST2_01 | **PASS** | (G10-006) 배틀-페어링 복원: `CurrentBattleOpponent`(AttackController.Current)로 싸우는 특정 적의 무전위 판정 | — |
| ST2_11 | **PASS** | (G10-001) once-per-turn 복원: `maxCountPerTurn=1`+`hash="Unsuspend_ST2_11"` → 라이브 트리거 루프 `OnceFlagController` 게이트가 강제 | — |
| ST2_12 | **PASS** | (G10-003) [Security] 테이머 플레이 실구현 (OnStartTurn 메모리도 1:1) | — |
| ST2_15 | **PASS** | (G10-007) play-from-under 실구현: 밑 Digimon 전위카드를 배틀에리어로 cost-free 플레이(`ActivatedPlayFromUnderEffect`+`PlayDigivolutionAsDigimon` 뮤테이션) | 활성 imperative |

### ST3/Yellow (11 PASS · 0 PARTIAL · 0 FAIL) + 별칭 ST3_07
| 카드 | 판정 | 카드-코드 부채 | 엔진통합 노트 |
|---|---|---|---|
| ST3_05 | PASS | `CanAddMemory` 누락이나 **메모리 Clamp 상한이 엔진에서 강제됨(검증)** → 동일 동작 | — |
| ST3_08·11 | PASS | — | 활성 imperative |
| ST3_09 | PASS | `Library>=1`/`CanAddSecurity` 누락이나 **`MoveFromZoneTop` 가용분 클램프=빈 덱 no-op(검증)** → 동일 동작 | — |
| ST3_13·14·15·16 | PASS | — | 활성/시큐리티 imperative |
| ST3_07 | PASS(별칭) | — (→ST1_06) | effectClass dispatch 전제 |
| ST3_01 | **PASS** | (G10-002) 0DP-격파 전제(`IsDPZeroDelete`+`CanTriggerOnPermanentDeleted` 상대디지몬) + once-per-turn(`maxCountPerTurn=1`+hash) 복원 | 라이브 OnDestroyedAnyone이 삭제 subject를 cross-card 리스너에 전달 + once-flag를 매칭 시에만 소비하는 부분은 엔진-통합 트랙 |
| ST3_04 | **PASS** | (G10-002) ST3_01과 동일 게이트 복원(메모리+1판) | 위와 동일 |
| ST3_12 | **PASS** | (G10-003) [Security] 테이머 플레이 실구현 ([All Turns] 시큐리티존 +2000도 1:1) | — |

### ST7/Red
| 카드 | 판정 | 부채 |
|---|---|---|
| ST7_10 | PASS | — (원본과 동일) |

---

## 부채 — 갚는 법 (유형별)

| 유형 | 카드 | 필요 작업 | 난이도 |
|---|---|---|---|
| **once-per-turn** | ~~ST2_11, ST3_01, ST3_04~~ **전부 완료**(G10-001/002) | `CardEffectDefinition.maxCountPerTurn`+hash → 라이브 `OnceFlagController` 게이트 | 낮음 |
| **트리거 전제(0DP격파·상대디지몬)** | ~~ST3_01, ST3_04~~ **완료**(G10-002) | `IsDPZeroDelete`(DPZero 마커)/`CanTriggerOnPermanentDeleted`(TriggerEntityId) 술어 복원 | 중 |
| **배틀-페어링** | ~~ST2_01~~ **완료**(G10-006) | `PermanentView.TopInstanceId`+`CurrentBattleOpponent`(AttackController.Current) | 중~상 |
| **동적 삭제 임계값** | ~~ST1_15~~ **완료**(G10-005) | `MaxDpDeleteScope`+`MaxDpDeleteThreshold`(상승 바인딩 합산) | 중 |
| **보호 전위카드(protected source)** | ~~ST2_03, ST2_09~~ **완료**(G10-004) | `TrashProtectedKey` 메타 + `TrashableDigivolutionCount`/`HasTrashableDigivolutionCards`/`TopCardHasLevel` | 중 |
| **테이머 시큐리티 플레이** | ~~ST1_12, ST2_12, ST3_12~~ **완료**(G10-003) | `PlayThisCardToBattleEffect`(PlayCard 뮤테이션)+ActivatedEffectResolver 디스패치 | 중~상 |
| **play-from-under** | ~~ST2_15~~ **완료**(G10-007) | `DigivolutionStackHelpers.PlaySpecificSourceAsync`+`PlayDigivolutionAsDigimonKind`+`ActivatedPlayFromUnderEffect` | 상 |

## 엔진 통합 갭 (카드-코드 실패 아님, 별도 트랙)
- **활성화 풀 루프 — 대부분 해소(G11-002)**: 옵션-활성 deferred 경로가 RL env에서 풀 루프로 발동(활성→suspend→ResolveChoice→재개, 재지불 없음, commit-once; `DeferredActivationController`+`ResolveChoiceAsync` 재호출). 잔여: 시큐리티-스킬 deferred 재개·다중-choice e2e.
- **트리거 plumbing(G11-002/004)**: `GameFlowProcessor`가 트리거 request에 이벤트 subject(TriggerEntityId) enrich + 게이트-우선 once-소비. 잔여: "Anyone" 트리거의 cross-card subject 전달(`MatchesEvent` sourceEntityId 필터가 self-scoped만 통과 → ST3_01/04 라이브 cross-card 발동).

## 권장 처리 순서 (수요 기반)
1. **once-per-turn 3장** — `OnceFlagHelpers` 연결, 가장 싸고 "실제보다 강함" 버그 제거.
2. **ST3_01/04 트리거 전제** — 0DP격파 컨텍스트(같은 작업으로 2장).
3. **테이머 시큐리티 3장** — `PlaySelfTamerSecurityEffect` 한 번 구현 → 3장 동시 해결.
4. **ST1_15 임계값 / ST2_01 배틀페어링** — 해당 메커니즘 실제 필요 시.
5. **ST2_15 play-from-under** — 특수플레이 서브시스템 착수 시.
