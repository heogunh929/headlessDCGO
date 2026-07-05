# Select-Follow-Up (Mode.Custom) — 스코핑 (PRIM-P0-flow Build Order 3)

- 작성일: 2026-07-05. 근거: AS-IS `Mode.Custom` 3형제 + 헤드리스 기존 select 프리미티브 전수 조사.
- 대상: ALL_CARD_PRIMITIVE_BACKLOG P0 Build Order 3 (보고 2,806장). "대상 선택 후 후속 동작"의 typed 프리미티브.

## 0. 핵심 결론 — 갭은 "2,806장 신규"가 아님

**대부분의 단일-동사 프리미티브는 이미 존재.** AS-IS `Mode.Custom`은 "술어로 N개 선택 후 임의 코루틴 실행"의
탈출구지만, 헤드리스는 typed Mode/mutation으로 이미 대부분 커버. 진짜 갭은 **소수 누락 동사 + 2개 횡단 구조**:
1. **compound/sequence 조건부 체인**(successProcess) — 다만 *무조건* 체인은 이미 리스트 순차로 동작(§2 검증).
2. **select-then-digivolve**(~310장) — 유일한 대형 신규 동사(flow 필요).

## 1. AS-IS 형태

3형제, 각 `Mode` enum의 `Custom` 값이 임의 후속 코루틴 탈출구:
- `SelectPermanentEffect`(필드 permanent, `SelectPermanentEffect.cs:126`) — Custom 2086 call-site.
- `SelectCardEffect`(존 카드: Library/Trash/Security/Hand…, `SelectCardEffect.cs:182`) — Custom 1136.
- `SelectHandEffect`(손패, `SelectHandEffect.cs:125`) — Custom 762.
합계 3984 call-site / ~2106 파일. 후속 동작: play 778 · DP버프 496 · draw 451 · delete 355 · digivolve 310 ·
suspend 299 · source-move 203 · trash 196 · unsuspend 195 · add-hand 190 · SAttack 134 · recovery 98 · bounce · 등.
**체이닝이 표준**(`…AndProcessAccordingToResult` = 주동작 후 successProcess): +play 768 · +DP 416 · +digivolve 212 · +draw 188.

## 2. 헤드리스 기존 (재사용 — 재구축 금지)

`ActivatedSelectEffect`(`CardPortingFramework.cs:1881`)가 `SelectPermanentEffect` 래핑. **헤드리스엔
Mode.Custom 탈출구 없음** — 모든 후속은 typed Mode/mutation(`SelectPermanentEffect.cs:189` dispatch).

| 동사 | 기존 프리미티브 | 상태 |
|---|---|---|
| suspend | `SelectAndSuspendEffect` (:4807) | ✅ |
| unsuspend | `SelectAndUnsuspendEffect` (:4813) | ✅ |
| delete | `SelectAndDestroyEffect` (:4791) | ✅ |
| bounce→hand | `SelectAndBounceEffect` (:4819) | ✅ |
| DP버프 | `SelectAndBuffDpEffect` (:4975) | ✅ |
| SAttack버프 | `SelectAndBuffSAttackEffect` (:5035) | ✅ |
| source trash | `SelectAndTrashDigivolutionEffect` (:4995) | ✅ |
| de-digivolve | `SelectAndDeDigivolveEffect` (:4839) | ✅ |
| play(존→필드) | `SelectAndPlayFromZoneEffect` (:4827) | ⚠️ 부분(토큰/옵션·digivolve-into 미포함) |
| restrict | `SelectAndRestrictEffect` (:5018) | ✅ |
| reveal-select | `SimplifiedReveal…`/`RevealMulti…` (:4846/4857) | ✅ |
| **return-to-deck(permanent)** | Mode.PutLibraryTop/Bottom dispatch됨, **팩토리 없음** | ⚠️ 팩토리 one-liner |
| **put-security(permanent)** | Mode.PutSecurityTop/Bottom dispatch됨, **팩토리 없음** | ⚠️ 팩토리 one-liner |
| **add-to-hand(존→손)** | `SelectCardEffect.Mode.AddHand` + ReturnToHandKind 존재, **미배선** | ❌ 래퍼 |
| **trash-from-zone** | `SelectCardEffect.Mode.Discard` + TrashCardKind 존재, **미배선** | ❌ 래퍼 |
| **return-sources** | ReturnDigivolutionCardsKind 존재, 술어-select 팩토리 없음 | ❌ 팩토리 |
| **select-then-digivolve** | 없음(flow 필요) | ❌ 대형 신규 |

## 3. 재-스코핑 & 계획

**§2 무조건 체인 검증**: `ActivatedEffectResolver.ResolveListAsync`가 카드의 `CardEffects()` 반환 리스트를
순서대로 해소(같은 sink 공유). 따라서 카드가 `[SelectAndDestroy, Draw]`를 반환하면 두 스텝이 이미 순차 실행됨
→ **무조건 후속(select-then-draw 등)은 이미 포팅 가능** (배치A에서 테스트로 확정). 조건부(successProcess만)만 갭.

### 배치 A (저위험, 기존 Mode/mutation 재사용)
- 팩토리 one-liner: `SelectAndReturnToDeckEffect`(PutLibrary*), `SelectAndPutSecurityEffect`(PutSecurity*),
  `SelectAndReturnSourcesEffect`(ReturnDigivolutionCardsKind).
- 무조건-순차 체인 동작 테스트로 확정(리스트 순차).

### 배치 B (SelectCardEffect 배선)
- 존-카드 select 래퍼 `ActivatedSelectCardEffect`(기존 `SelectCardEffect` 구동) + 팩토리
  `SelectAndAddToHandFromZoneEffect`(AddHand) / `SelectAndTrashFromZoneEffect`(Discard). add-hand 190 + trash 196.

### 배치 C (대형 신규)
- **select-then-digivolve**(~310): digivolve flow 프리미티브(비용/요구 해소) — Mode 재사용 불가, 실 엔진 작업.
- **조건부 체인(successProcess)**: 주동작 성공 시에만 후속 — 필요 시 `…AndProcess` 계열 확장 또는 조건부 래퍼.

## 4. 우선순위 (카드 수 기준 최대 언블록)
1. 배치 A(팩토리 + 순차 검증) — 즉시, 저위험.
2. 배치 B(SelectCardEffect 배선) — add-hand/trash-from-zone ~400장.
3. 배치 C(digivolve ~310, 조건부 체인) — 대형.

## 5. 관련
- [ALL_CARD_PRIMITIVE_BACKLOG.md](ALL_CARD_PRIMITIVE_BACKLOG.md) P0 Build Order 3.
- 재사용: `ActivatedSelectEffect`/`SelectPermanentEffect.Mode`, `SelectCardEffect`(미배선),
  `ActivatedEffectResolver.ResolveListAsync`(리스트 순차).
