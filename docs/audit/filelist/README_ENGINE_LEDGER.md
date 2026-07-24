# 엔진 개발 원장 (관리 정본) — 2026-07-24 확정

**정본 = `merged_files_no_cards.csv`** (사용자 확정: "지금부터 이게 엔진 개발 원장임").
카드층(CardEffect/) 제외한 엔진+substrate 전 파일(650행)의 AS-IS↔TO-BE 대조 관리 대장.

## 컬럼
`asis_path | asis_filename | tobe_path | tobe_filename | 진행상태 | 결함여부 | 삭제대상여부 | 비고1 | 비고2 | 비고3 | 비고4 | 비고5`
- asis/tobe 경로·파일명: 상대경로(Assets/Scripts/ 또는 Headless/ 기준) 일치 시 한 행, 한쪽만 있으면 반대쪽 공백.
- 진행상태/결함여부/삭제대상여부/비고: 판정·관리 기록(자동 무죄·라벨 주입 금지 — 실측/사용자 판정만).

## 현황(생성 시점)
- 650행: 양쪽 344 · ASIS만 92 · TOBE만 214(미러층 발명 5 + Headless substrate 209).
- 결함여부=Y·삭제대상여부=Y: 미러층 발명 5(NewModelContinuousScan·EffectChoiceHelpers·RestrictionHelpers·ReplacementHelpers·RestrictionCarriers).

## 지위
- 이전 `docs/audit/ENGINE_DEFECT_LEDGER_2026-07-24.md`(DEF-A~S/C/D)를 대체하는 관리 정본. DEF 원장은 결함 상세 참조용으로 존속(상태 추적은 본 CSV).
- 원칙: 판정 라벨·자동 무죄·요약 서열을 원장에 주입 금지(사용자 재교정 반복). 컬럼은 실측 또는 사용자 판정만.
