# headlessDCGO 문서 인덱스 (현재 상태)

- 갱신: 2026-07-01
- 이 폴더는 headless 엔진 포팅 프로젝트의 작업 문서입니다. 아래는 **현재 authoritative** 문서만 정리한 인덱스이며, 완료된 엔진-완성기·감사·후속 goal 이력은 [`archive/`](archive/)로 이동했습니다.

---

## 1. 현재 위치 (2026-07-01)

> **엔진 코어 완성 + 프리미티브 선행개발 전 웨이브 완료(88종). 다음 = 로컬 모델의 per-card 포팅(config-only).**

| 단계 | 상태 |
|---|---|
| 엔진 코어 (A~D · F · G · X 시리즈) | ✅ 완료 (archive 참조) |
| 라이브 통합 / 실루프 배선 | ✅ 완료 |
| **프리미티브 선행개발 (BT-PRE-A · W1~W4)** | ✅ **88종 완료** |
| 카드 데이터 로더 | 🔴 대량 포팅 선결 |
| per-card 대량 포팅 (로컬 LLM) | 🟠 다음 본작업 |
| 통합 / RL | 🟢 마무리 |

- 전체 테스트: `bash scripts/run-tests.sh` — **274 green**, `tools/RuleAudit` 위반 0.

## 2. 로드맵 · 기준

- [**development_roadmap.md**](development_roadmap.md) — Phase 1~5 계획 (수직슬라이스 → 데이터로더 → 대량포팅 → 타이밍보강 → RL)
- [card_porting_standard.md](card_porting_standard.md) — 카드 포팅 표준(구조 1:1 미러 원칙)
- [card_porting_recipe.md](card_porting_recipe.md) — 포팅 레시피(반복 패턴·체크리스트)
- [card_group_standard.md](card_group_standard.md) — goal·테스트 단위 기준
- [ci_check_procedure.md](ci_check_procedure.md) — CI 확인 절차

## 3. 프리미티브 선행개발 (완료, 88종)

> 강모델이 카드-facing 프리미티브를 전부 선행개발 → 로컬모델 포팅은 파라미터화만. **카드 포팅 중 프리미티브 개발 없음.**

- [**primitive_backlog.md**](primitive_backlog.md) — 마스터 백로그(전 DCGO census)
- [primitive_w1_goals.md](primitive_w1_goals.md) — W1 진화 기반 (6)
- [primitive_w2_goals.md](primitive_w2_goals.md) — W2 고빈도 (20)
- [primitive_w3_goals.md](primitive_w3_goals.md) — W3 중빈도 (27)
- [primitive_w4_goals.md](primitive_w4_goals.md) — W4 저빈도 tail + 프레임워크/타이밍 (30)
- [ace_overflow_design.md](ace_overflow_design.md) — AceOverflow 중앙 규칙 설계
- [**fidelity_debt.md**](fidelity_debt.md) — 충실도 부채 레저 + preemptive-seal 목록
- [**fidelity_remediation_goals.md**](fidelity_remediation_goals.md) — permanentCondition 술어 무시 복원 goal (P1~P5) · 상세: [fidelity_remediation.md](fidelity_remediation.md)

## 4. 카드 포팅 goal (예정)

- [bt1_porting_goals.md](bt1_porting_goals.md) — BT1 포팅 goal
- [cards_st2_blue_goal.md](cards_st2_blue_goal.md) · [cards_st3_yellow_goal.md](cards_st3_yellow_goal.md) — ST 색상별 goal

## 5. 세션 연속성

- [session_handoff.md](session_handoff.md) — 다른 PC에서 이어서
- [memory_mirror.md](memory_mirror.md) — auto-memory ↔ repo 미러

---

## 아카이브 ([`archive/`](archive/))

완료된 이력 문서(37종): 엔진-완성기(engine_completion_*, phase3/4, rl_gap, prephase4_wiring), 감사(original_vs_port_*, asis_fidelity, phase3_parity), 후속 goal(g11~g16, gr_*, live_*, integration_*, fidelity_repayment, bt_pre_a 등), 서브시스템 설계(s1/s2_s4, f68, cgroup4). git 히스토리 + 이 폴더에 보존.
