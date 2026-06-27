# 남은 규칙-parity 잔여 항목 (검증 문서)

- 작성일: 2026-06-27
- 배경: pass2 감사의 N-2(지속/대체 효과 배선) 소비측 작업(D-A1·D-A2·D-A3·D-A4) 완료 후, GPT 라운드-3 리뷰가 **아직 남은 3건**을 지적. 본 문서는 그 3건을 **소스에서 직접 검증**하고, 다음 규칙-parity 묶음의 착수 지점·수정 방향을 기록한다.
- 관련 문서: [original_vs_port_divergence_audit_pass2.md](original_vs_port_divergence_audit_pass2.md) (N-2 / D-A5·D-A6), [gpt_review_followups.md](gpt_review_followups.md) (라운드 3, R2-5)
- **본 문서는 발견·계획 기록일 뿐 — 소스 수정 없음.**

---

## 1. D-A5 — "cannot digivolve" 지속 제한 미소비 (확정)

### 증상
"이 디지몬은 진화할 수 없다"류 지속 제한이 디지볼브 합법성 판정에 반영되지 않는다.

### 검증 (소스)
- **`CannotRestrictionKind`에 Digivolve 멤버 부재** — [RestrictionHelpers.cs:7-15](../../src/HeadlessDCGO.Engine/Headless/Effects/RestrictionHelpers.cs)
  ```
  enum CannotRestrictionKind { Attack=0, Block=1, Delete=2, ReturnToHand=3, ReturnToDeck=4, Suspend=5 }
  ```
  → 진화 금지를 표현할 enum 값 자체가 없다.
- **`DigivolveAction.Validate`가 `ContinuousRestrictionGate`/`CannotRestriction`를 전혀 호출 안 함** — [DigivolveAction.cs](../../src/HeadlessDCGO.Engine/Headless/Runtime/DigivolveAction.cs) 내 `Restriction`/`ContinuousRestrictionGate` 심볼 0건. (Attack은 `AttackPermanentAction:222`에서 `EvaluateAttack`로 소비하지만 Digivolve엔 대응 게이트 없음.)

### 수정 방향 (다음 묶음)
1. `CannotRestrictionKind`에 `Digivolve` 추가 + `RestrictionHelpers`에 키(`cannotDigivolve`)·`CannotDigivolve(targetId, restrictions, …)` 헬퍼.
2. `ContinuousRestrictionGate.EvaluateDigivolve(context, cardId)` (Attack/Block 자매).
3. `DigivolveAction.Validate`에서 진화 대상(밑 디지몬) 및/또는 진화하는 카드에 대해 게이트 호출 → 제한 시 Illegal.
4. 테스트: 진화 금지 지속효과 등록 시 Digivolve 합법액션 제거 확인.

---

## 2. D-A6 — 공격 타깃 제한이 target-aware하지 않음 (확정)

### 증상
"이 디지몬은 특정 조건의 상대 디지몬을 공격할 수 없다"류 **타깃 한정** 공격 제한이 평가되지 않는다. (전역 "공격 불가"는 동작.)

### 검증 (소스)
- **호출부가 `attackerId`만 전달** — [AttackPermanentAction.cs:222](../../src/HeadlessDCGO.Engine/Headless/Runtime/AttackPermanentAction.cs)
  ```
  CannotRestrictionResult attackRestriction = ContinuousRestrictionGate.EvaluateAttack(context, attackerId);
  ```
  `defenderId`를 넘기지 않는다. 게다가 이 호출은 targetless/target 분기(`:233`) **이전**의 공용 검증부에 있어, 특정 타깃 후보에 대한 정보가 반영되지 않는다.
- **인프라는 defenderId를 지원** — `ContinuousRestrictionGate.EvaluateAttack(context, attackerId, defenderId=null)` / `RestrictionHelpers.CannotAttack(attackerId, restrictions, defenderId=null)`.
- **defenderId 없으면 타깃-한정 제한은 항상 스킵** — [RestrictionHelpers.cs:454](../../src/HeadlessDCGO.Engine/Headless/Effects/RestrictionHelpers.cs) `CanApply`:
  ```
  if (restriction.TargetEntityId is HeadlessEntityId target && target != request.TargetEntityId) return false;
  ```
  `request.TargetEntityId`(=defenderId)가 null이면 `TargetEntityId`를 가진 제한은 `target != null`로 항상 제외된다 → 타깃-한정 제한 무효.
- **원본 대조**: [Permanent.cs:2261-2288](../../DCGO/Assets/Scripts/Script/Permanent.cs) `ICanNotAttackTargetDefendingPermanentEffect.CanNotAttackTargetDefendingPermanent(this, Defender)` — 공격자+방어자 쌍으로 평가.

### 수정 방향 (다음 묶음)
1. `AttackPermanentAction.Validate`의 Digimon-target 분기에서 `targetId`를 알고 있으므로, 그 분기 안에서 `EvaluateAttack(context, attackerId, targetId)`를 **타깃별로** 호출(또는 후보 열거 시 per-target 평가). 전역 제한은 기존처럼 `defenderId=null` 호출 유지.
2. `GetAttackDeclarations`의 타깃 후보 필터에도 동일 적용 → 합법 타깃 목록에서 제외.
3. 테스트: 특정 defender만 공격 금지하는 지속효과 등록 시 해당 타깃 후보만 제거, 다른 타깃·직접공격은 유지.

---

## 3. CI 확인 상태 (R2-5)

### 현황
- `.github/workflows/ci.yml`(compile-only 게이트)은 origin/main에 푸시됨.
- **도구별 편차**: 이 세션의 unauthenticated REST 조회로는 최신 HEAD까지 workflow run이 `success`로 보였으나, **GPT 측 `fetch_commit_workflow_runs`/combined status 조회는 최신 HEAD에 대해 빈 결과**. 커넥터가 막 푸시된 run/현재 HEAD를 잡지 못하는 경우로 추정.
- 따라서 **단정 회피**. [gpt_review_followups.md](gpt_review_followups.md)의 R2-5를 ✅→◑로 하향하고 "GitHub UI에서 현재 HEAD 초록 체크 직접 확인 권장"으로 문구 조정 완료.

### 권장 액션
- 코드/엔진 변경 불필요(운영 건).
- **사람이 GitHub Actions UI에서 현재 HEAD의 CI 결과를 한 번 직접 확인**. 초록이면 R2-5 종결, 실패면 로그 확인 후 후속.

---

## 요약 (다음 규칙-parity 묶음 착수 순서)
1. **D-A6 공격 타깃 제한** — 인프라(defenderId) 이미 존재, 호출부만 target-aware하게 → 비교적 작음.
2. **D-A5 cannot-digivolve** — enum/헬퍼/게이트 신설 필요 → 중간.
3. (운영) **CI UI 확인** — 코드 무관.

> 비고: D-A5·D-A6 모두 **소비측** 배선이다. 실제 발동은 해당 지속효과를 등록하는 **생산측(Phase 4 카드풀)**이 와야 일어난다 — N-2의 다른 슬라이스(DP·삭제방지)와 동일한 성격.
