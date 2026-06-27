# 미구현·잔여 항목 마스터 리스트 (구현 대상 총정리)

- 작성일: 2026-06-27
- 목적: "미구현 항목이 있으면 안 된다 — 다 만들어야 한다"는 방침에 따라, 현재까지 감사·리뷰에서 드러난 **모든 잔여 항목**을 한 곳에 모아 **지금 만들 수 있는 것 / Phase 4(카드풀) 대기 / 결정 필요 / 운영**으로 분류한다.
- 출처: [original_vs_port_divergence_audit.md](original_vs_port_divergence_audit.md), [original_vs_port_divergence_audit_pass2.md](original_vs_port_divergence_audit_pass2.md), [timing_emission_gaps.md](timing_emission_gaps.md), [gpt_review_followups.md](gpt_review_followups.md), [remaining_rule_parity_followups.md](remaining_rule_parity_followups.md)
- 범례: ⬜ 미착수 · ◑ 소비측만 / 부분 · ✅ 완료(참고용, 본 리스트엔 미완만 상세)

> **중요한 구조적 사실**: 잔여의 상당수는 **"생산측(Phase 4 카드풀)"** 의존이다. 엔진의 소비측(읽어서 적용)을 다 배선해도, 실제 발동은 카드가 그 효과/타이밍을 **등록·바인딩**해야 일어난다. 따라서 "지금 코드로 완결 가능한 것"과 "카드 데이터가 와야 E2E로 완성되는 것"을 반드시 구분한다.

---

## Tier 1 — 지금 만들 수 있음 (엔진 소비측, 합성 등록으로 단위검증 가능)

> 이 묶음은 카드풀 없이 **지금 구현+테스트**할 수 있다. (R2-1/N-2 DP처럼 테스트에서 연속효과를 합성 등록해 검증.)
> **2026-06-27: D-A5·D-A6·N-6·N-9 구현 완료.** N-8·N-7은 검증 결과 Tier 1이 아니었음(아래 재분류).

| ID | 항목 | 작업 | 상태 |
|----|------|------|------|
| **D-A6** | 공격 타깃 제한 target-aware | [AttackPermanentAction.cs](../../src/HeadlessDCGO.Engine/Headless/Runtime/AttackPermanentAction.cs)가 `EvaluateAttack(context, attackerId, targetId)`로 호출 → defender-스코프(`restriction.SourceEntityId`) 제한이 해당 타깃에만 적용. `tests/G3.5-DA56` 2/4. | ✅ |
| **D-A5** | "cannot digivolve" 지속 제한 | `CannotRestrictionKind.Digivolve` + `CannotDigivolveKey`/헬퍼 + `ContinuousRestrictionGate.EvaluateDigivolve` + `DigivolveAction.Validate` 소비. `tests/G3.5-DA56` 2/4. | ✅ |
| **N-6** | trash 삽입 순서(top) | `InMemoryZoneMover.AddToZone`에서 Trash 목적지는 항상 index 0 삽입(원본 `TrashCards.Insert(0)`) — 모든 경로 통일. `tests/G3.5-N6` 3/3. | ✅ |
| **N-9** | 브리딩 unsuspend의 `canUnsuspend` 게이트 | `TryUnsuspend`에 `ignoreCanUnsuspend` 추가, 브리딩 루프만 게이트 무시(필드는 유지). `tests/G3.5-N9` 2/2. | ✅ |

---

## Tier 2 — Phase 4(카드풀) 대기: 소비측 ✅, 생산측 미존재

> 엔진 소비측은 배선됐으나 **카드가 효과/타이밍을 등록해야** 실효. 카드 데이터 없이 E2E 검증 불가 → 카드 포팅과 동반.

### 2-A. 연속/대체 효과 생산측 (N-2 나머지)
- **N-2 생산측**: 카드 키워드/본문이 연속효과를 레지스트리에 등록해야 함. 현재 소비 게이트는 모두 no-op 대기:
  - `ContinuousDpGate`(+/−DP, DP-마이너스 면역) ← 카드가 `dpDelta`/`immuneFromDpMinus` 연속효과 등록
  - `BattleDeletionGate`(파괴 불가) ← Jamming 등 키워드가 `Delete/Prevent` replacement 등록. **키 정렬 필요**: `PreventBattleDeletion`(emit) ↔ `preventDeletion`(read)
  - `ContinuousRestrictionGate`(공격/블록 불가, + D-A5/A6 추가분) ← 카드가 `cannotAttack/Block/Digivolve` 연속 제한 등록

### 2-B. 트리거 타이밍 emission 갭 (18종, [timing_emission_gaps.md](timing_emission_gaps.md))
> 엔진은 원본에서 발화하나 포팅이 아직 emit 안 함 → 해당 타이밍 바인딩 카드는 현재 dead. **카드군 포팅 시 W1-2 패턴으로 배선.**
- 🔴 高: `OnStartMainPhase`, `WhenLinked`, `OnStartBattle`/`OnEndBattle`(※OnStartBattle은 DP비교 전 emit+해결 순서 주의)
- 🟠 中: `OnTappedAnyone`/`OnUnTappedAnyone`, `OnAddDigivolutionCards`, `OnMove`, `OnUseOption`, `OnDiscardHand`
- 🟡 低: `OnFaceUpSecurityIncreased`, `OnDigivolutionCardDiscarded`, `OnLinkCardDiscarded`, `OnDigivolutionCardReturnToDeckBottom`, `WhenTopCardTrashed`, `OnReturnCardsToHandFromTrash`, `OnReturnCardsToLibraryFromTrash`, `OnDiscardSecurity`, `Before/AfterPayCost`, `OnUseDigiburst`

### 2-C. 기타 Phase 4 동반
- **D-3 한계**: optional end-attack 트리거의 **에이전트 결정 노출**(현재 안전망만, 실전 노출 미구현).
- **메모리 양면 표현**: "상대 메모리 −N" 류 효과는 현 단면 게이지로 표현 불가 → 해당 효과 포팅 시 양면 모델 필요.
- **N-8 (Tier 1→2 재분류, 2026-06-27)**: 필드 이탈 시 인스턴스 상태 teardown. **라이브 엔진에 ZoneMover↔인스턴스repo 중앙 훅이 없고**, 현재 유일한 필드-이탈 경로(배틀 삭제→trash)는 리포트 마커(`deletedByBattle`/`dpBeforeBattle`)를 보존해야 해 충돌. 실제 트리거(hand/deck 반환)는 Phase-4 효과 경로 → **Phase 4 동반**(존-이동↔상태 teardown 커플링 설계 시).

### 2-D. won't-fix / N/A
- **N-7 (Tier 1→N/A 재분류, 2026-06-27)**: 셔플 RNG 1스텝 차이. 목표였던 "원본과 cross-engine 셔플 일치"는 **포팅(xoshiro)과 원본(Unity Random)이 완전히 다른 PRNG**라 1스텝과 무관하게 **원천 불가능**. 포팅 자체 결정성은 이미 정상 → loop 변경은 가치 0 + 모든 셔플 출력 이동 위험. **수정 안 함.**

---

## Tier 3 — 결정/정합 필요 (구현 전 방향 합의)

| ID | 항목 | 쟁점 | 필요 결정 | 상태 |
|----|------|------|-----------|------|
| **C-1** | `DigivolveAction`의 EvolutionCondition 2차 게이트 | 포팅이 색/레벨/비용 외 `definition/번호/타입` 매칭 게이트를 **추가**(원본엔 없음) | 이 게이트 유지할지/원본대로 제거할지 | ⬜ |
| **신2** | `DigivolutionStack` = projection(저장 아님) | de-digivolve/소재 재정렬 본격 구현 시 병목 | 진짜 저장 모델로 승격 시점 | ⬜(기록) |

---

## Tier 4 — 운영 (코드 무관)

| ID | 항목 | 액션 | 상태 |
|----|------|------|------|
| **R2-5** | CI 가동 외부 확인 | 사람이 GitHub Actions UI에서 현재 HEAD 초록 체크 확인 (도구별 조회 편차 있음) | ◑ |

---

## 진행 현황 / 남은 순서
1. ✅ **D-A6·D-A5·N-6·N-9 완료(2026-06-27)** — 규칙 parity 소비측 + 존 메커닉 마감. (Tier 1에서 실현 가능했던 전부.)
2. **C-1 / 신2** 방향 결정 (Tier 3) — 사용자 결정 대기.
3. **Tier 2(생산측·타이밍·N-8)**: Phase 4 카드풀과 동반 — 카드군별로 (a) 필요한 타이밍 emit 배선 + (b) 연속효과 등록 + (c) E2E 테스트.
4. **R2-5**: CI UI 확인(운영). **N-7**: won't-fix(다른 PRNG).

> 한 줄 요약: **Tier 1에서 실제로 카드 없이 완결 가능했던 4건(D-A6·D-A5·N-6·N-9)은 완료.** N-8은 Phase-4 결합(중앙 훅 부재), N-7은 N/A(다른 PRNG). **Tier 2는 본질적으로 Phase 4 카드풀이 와야 완성**(엔진 소비측 준비 완료). **Tier 3은 결정 후**, **Tier 4는 운영.**
