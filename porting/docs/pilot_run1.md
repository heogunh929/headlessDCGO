# 로컬모델 포팅 파일럿 #1 — **보류** (2026-07-02, 로컬 LLM 환경 미세팅으로 실행 취소)

> **상태**: 준비 완료·실행 보류. 로컬 LLM 개발환경(qwen3-coder 등) 세팅 후 아래 그대로 실행하면 됨.
> 첫 시도(opencode 무료 모델 대체 실행)는 카드 파일 생성 전에 중단 — 트리 오염 없음.

- **목적**: 포팅 스킬(AGENTS.md + /port-card + 카탈로그/레시피)로 로컬-급 모델이 per-card 포팅을 수행할 수 있는지 검증. **조건부 라틴트를 건드리는 카드를 의도적으로 포함** — 성공 경로와 STOP 경로 양쪽을 시험.
- **모델**: (로컬 LLM 세팅 후 지정 — 원계획 qwen3-coder)
- **실행**: `opencode run --command port-card -m <provider/model> "<ID>"` (저장소 루트, 순차)
- **선행 준비**: 그룹 테스트 프로젝트 6종 템플릿 생성(`tests/CardEffect.<SET>.<COLOR>.Tests`), P4 풀버전 팩토리 `RevealDeckTopCardsAndSelect`(=`RevealMultiSelectEffect`) 신설 — 카탈로그 122종 재생성.

## 카드 선정 (6장)

| # | 카드 | 터치하는 표면 | 기대 결과 |
|---|---|---|---|
| 1 | **BT20_079** (Purple) | Execute-1(턴종료 창), SAttack, delete-lowest·select-and-play 코루틴 번역 | 성공 (MEDIUM) |
| 2 | **EX11_051** (Purple) | Execute-1 + Pierce + select-and-play/free-digivolve 번역 | 성공 (MEDIUM) |
| 3 | **ST17_11** (Green) | **P4 다중조건 reveal 팩토리(신설)** + suspend-N + delay 분기 | 부분 성공 예상 — delay(`PlaceDelayOptionCards`) 분기는 STOP 가능 |
| 4 | **BT22_016** (Blue) | **isLinkedEffect(C9)** + Link 서브시스템 + SimplifiedReveal + `AddSelfLinkConditionStaticEffect`(STOP-목록) | 부분 성공 — 링크-조건 분기 STOP 기대 |
| 5 | **BT9_103** (Black) | **CanAddSecurity 라틴트** — `CannotAddSecurityClass`(미모델 제한) | **STOP 프로브** — `<ID> \| 이유 \| 심볼` 기록이 나오는지 검증 |
| 6 | **BT14_086** (Black) | **MindLinkClass(K5, 클래스 직접 생성 표면)** + 테이머 시큐리티 + Jamming/Reboot inherited | 시험 — 카탈로그의 클래스-표면 문서만으로 표현 가능한지 |

제외: AD1_011/AD1_025 — Assembly/IDestroySecurity/DeckBottomBounce 등 진짜 미개발 프리미티브 다수(파일럿 소음; 선행개발 백로그로).

## 판정 기준 (카드당)

1. 미러 파일이 원본 타이밍 분기 구조와 일치하는가 (분기 누락/발명 없음)
2. 팩토리 인자를 **뭉개지 않았는가** (술어/값 1:1 — 카탈로그 경고 준수)
3. STOP 계약 준수 — 미표현 분기를 임의 구현하지 않고 `// STOP:` + 기록을 남겼는가
4. 그룹 테스트 sub-test 추가 + `run-tests.sh` green
5. 금지사항 — DCGO/ 수정 없음, 커밋 없음, 프리미티브 신설 없음

## 결과

| 카드 | 결과 | 판정 1~5 | 비고 |
|---|---|---|---|
| BT20_079 | 보류 | | |
| EX11_051 | 보류 | | |
| ST17_11 | 보류 | | |
| BT22_016 | 보류 | | |
| BT9_103 | 보류 | | |
| BT14_086 | 보류 | | |

## 발견 사항 / 스킬 개선점

(실행 후 기록. 준비 단계에서 이미 발견·해소한 것: ① 다중조건 reveal이 카드-표현 불가였음 → 풀버전 동명 팩토리 `RevealDeckTopCardsAndSelect` 신설(카탈로그 122종), ② 신규 세트 그룹 테스트 프로젝트 부재 → 6종 템플릿 선생성.)
