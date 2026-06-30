---
description: 골 실행 — docs/audit/*_goals.md의 해당 스펙대로 (AS-IS 1:1 확인 → 구현 → green+단언테스트+RuleAudit 0)
argument-hint: <골ID 예: GR-006 / EX8-3>
---

$ARGUMENTS 진행. `docs/audit/*_goals.md`의 "$ARGUMENTS" 스펙대로.
구현 전 원본 `DCGO/`에서 해당 규칙(대상·선결·타이밍 등)을 1:1 확인(추측 금지).
종료 시 `bash scripts/run-tests.sh` 전체 green + 발동/동작을 단언하는 테스트 + `tools/RuleAudit` 위반 0. 커밋은 내가 지시할 때.
