# 미러 정확성 계약 — 전수 축 명세 (R7 종점 감사 명세 겸용)

> 목적: "미러가 틀릴 수 있는 방식"의 축을 계약("경로 등가·발명물 0·미러 100%")에서 일괄 유도해 전수 프로그램을 **닫힌 목록**으로 만든다. 배경: 축이 사고 후에야 하나씩 추가되는 반응 루프(심볼→구조→충실도→가시성)를 사용자 지적으로 종결. 이후 새 축의 추가는 예외적 사건이며, 발생 시 이 문서를 갱신하고 그 축을 즉시 전수한다.

| 축 | 불변식 | 전수 방법 | 상태 (2026-07-22) |
|---|---|---|---|
| A. 구조 존재 | 모든 미러 파일·클래스가 AS-IS 대응을 가짐(substrate 예외는 명시 등재) | 경로-diff(파일)+클래스-diff(파일 내부) | 파일 ✅(structural_invention_census) / **카드 내부 클래스 미실시** → 감사 SW-A |
| B. 심볼 참조 | 발명 심볼 live 소비 0·단조 감소 | 다양화 grep+컴파일러 열거+G1R baseline 핀 | ✅ 상시 가드 |
| C. 생산-소비 도달성 | 모든 효과 산출물(kind-class 포함)이 그 룰의 집행 chokepoint에 가시(인터페이스 구현+chokepoint 판독원 정합) | 3열 판정: 인터페이스 구현?/chokepoint가 registry-only·live-only·union?/실생산자? | residue 한정 진행 중(④-d) / **kind-class 전반 미실시** → 감사 SW-C |
| D. 판독 충실도 | chokepoint·헬퍼가 base 아닌 fold된 값·전체 저장소를 읽음 | 게터-위임 검사(효과 접기) | Permanent 게터 ✅(permanent_fidelity_audit, 결함=LevelOf 1) / chokepoint 판독원은 SW-C에 포함 |
| E. 타이밍 창 배선 | AS-IS StackSkillInfos/컷인 전 좌석 ↔ 미러 배선(경로별 차이 보존) | AS-IS 타이밍 emit 좌석 전수 ↔ 미러 TriggerTimingMap/sink/인라인 대조 | 개별만(수리-3b OnDigivolutionCardDiscarded·수리-6 RDW-01·R2-P2-2 미결) → **감사 SW-E** |
| F. 만료/수명 | Until* 버킷 리셋 사이트 완비 | 리셋-사이트 전수 | ✅ (A1b) |
| G. 캡/회계 | 캡 파티션 전단사·staged 회계 등가 | 파티션 대조표 | ✅ (Da′-0, uniform 소멸로 단일화) |

## 미실시 감사 스위프 (R7 전 필수 — 이 3개가 끝나면 축 목록 완결)
- **SW-A**: 카드 corpus 파일 내부의 발명 클래스 전수(4,018장 — 헤더/클래스 선언 기계 스캔으로 축소 가능)
- **SW-C**: kind-class 전반의 도달성 전수 — 모든 IXxxEffect 인터페이스에 대해 (i)구현 클래스 목록 (ii)그 인터페이스를 스캔하는 chokepoint 실존·판독원 (iii)스캔 무-호출 인터페이스(=전 구현이 inert) 적발
- **SW-E**: AS-IS 타이밍 emit 좌석 전수표 ↔ 미러 배선 상태(배선/의도적 미배선(사유)/갭)

## 운용
- 캠페인·트랜치는 이 축 명세를 기준으로 검증 항목을 구성한다. R7 종점 감사 = A~G 전 축의 최종 재실행·채점.
