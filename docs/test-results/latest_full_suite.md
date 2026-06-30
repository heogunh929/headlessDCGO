# 최신 전체 스위트 결과

회귀 게이트(`bash scripts/run-tests.sh`)의 최신 전체 실행 스냅샷. push 전후로 갱신.

| 항목 | 값 |
|---|---|
| 날짜 | 2026-06-30 |
| 기준 커밋 | `21fd93e5` (G11) + G12 미커밋 |
| 결과 | `SUMMARY: PASS=225 FAIL=0 TOTAL=225  (jobs=10 build_jobs=6)` |
| 러너 | `scripts/run-tests.sh` (빌드 6-병렬 / 실행 `--no-build` 10-병렬, 2단계) |

## 비고
- 225 = 테스트 **프로젝트** 수(케이스 아님). 각 프로젝트가 다수 sub-test 포함.
- CI(Actions)는 컴파일-only이므로 이 로컬 전체 실행이 **정식 회귀 게이트**(docs/audit/ci_check_procedure.md).
- 이 파일은 커밋 시점의 green을 기록하는 스냅샷 — 새 작업 후 갱신.

## 직전 마일스톤
- G7(통합 정밀화 7) → 커밋 `e1ba54f6`, 212/212
- G8(대량 포팅 선결 8) → 커밋 `5345e894`/`610258ae`, 217/217
- 카드 포팅 ST2/ST3 + effectClass 별칭 → 커밋 `d4caa021`, 220/220
- G10(충실도 부채 11장 상환, ST1/ST2/ST3 진짜 1:1 35/35) → 커밋 `cd2f1acf`
- G11(검증 + 활성화 풀 루프 deferred e2e) → 커밋 `21fd93e5`, **222/222**
- G12(전체결과 갱신 + multi-choice/cross-card/Security deferred e2e) → 미커밋, **225/225**
  - G12-002 multi-choice 활성화 deferred 재개(2-라운드, 비용 1회)
  - G12-003 ST3_01/ST3_04 라이브 cross-card 트리거(0DP 삭제 → "Anyone" 타이밍 subject 브로드캐스트)
  - G12-004 [Security] 효과 deferred suspend/resume(SecurityResolver → DeferredActivations → ResolveChoice)
