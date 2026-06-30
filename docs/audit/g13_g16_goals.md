# G13–G16 목표 스펙 (대량 포팅 진입 전 마지막 정비 + 대표 카드 검증)

> 목적: 로컬 LLM이 **카드를 대량 포팅**하기 직전, (1) 엔진을 change-control로 안정화하고, (2) 포팅 계약 문서를 단일화하며, (3) 고난도 대표 카드로 엔진 커버리지를 실증하고, (4) 재사용 helper·카드별 테스트 템플릿을 확정한다.
>
> 공통 종료 기준(모든 goal): `bash scripts/run-tests.sh` **전체 green(FAIL=0)** + 해당 goal의 동작을 **실제로 단언하는 테스트**(또는 문서 goal의 경우 검증 가능한 산출물) 추가. 커밋은 사용자 지시 시에만.
>
> 표준 규칙 유지: 엄격 1:1(원본 가드 누락 = 실패, 빈도/추측 금지), AS-IS 미러(카드-facing 로직은 원본 파일 구조 1:1, 엔진 plumbing은 `Headless/`), DCGO/·.dotnet/ 커밋 금지.

기준 커밋: `40388596` (G12 + follow-up), 225/225.

---

## G13 — 엔진 change-control 선언 + 포팅 계약 단일화 + random self-play smoke를 게이트에 편입

**프레이밍(확정): hard freeze가 아니라 change-control(소프트 프리즈).**
엔진 코드 수정은 *"포팅 중 실제로 없는 primitive를 증명"할 때만* 허용하고, 수정마다 사유를 로그에 남긴다. 고난도 카드(G15)가 빠진 primitive를 요구하는 것은 정상 경로이므로, 하드 프리즈 대신 "정당화된 변경만"으로 통제한다.

### G13-001 — change-control 규칙 문서
- 산출물: `docs/audit/engine_change_control.md`
  - "무엇이 freeze인가": 카드-facing(`Assets/Scripts/CardEffect/**`)는 자유, 엔진(`Headless/**`)은 통제.
  - 허용 조건: ① 포팅 대상 카드가 해당 primitive 없이는 1:1 불가함을 코드로 증명, ② 변경 전 회귀 게이트 green, ③ `docs/audit/engine_change_log.md`에 (카드번호 · 없는 primitive · 추가/수정한 엔진 심볼 · 회귀결과) 한 줄 기록.
  - 금지: "있으면 편해서" 식 선제 추가, 가드 완화로 우회.
- DoD: 문서 존재 + `engine_change_log.md` 빈 템플릿(헤더 행) 생성.

### G13-002 — 포팅 계약 단일화 + recipe 갱신(stale 제거)
- `docs/audit/card_porting_recipe.md`가 현재 **stale**: §5.1(활성효과 자동활성화)·§6(런타임 자동등록 디스패치)은 이미 해소됨(G9-001 effectClass 디스패치, G6 auto-register, G11-002 활성화 풀 루프, G12-004 시큐리티 deferred). 이를 "완료"로 갱신하고, 남은 진짜 갭만 §5에 남긴다.
- 산출물: recipe를 **단일 권위 문서("포팅 계약")**로 정리 — 절차(1카드) · 매핑 치트시트 · 검증예시 · 현재 커버 패턴(연속/트리거/활성/시큐리티/once-per-turn/cross-card 브로드캐스트) · **남은 갭** · 모델용 매핑 CSV로의 포인터.
- DoD: recipe의 §5/§6이 현 코드 상태와 일치(해소된 항목은 "✅ 해소(Gxx)"로, 미해소만 잔존). 문서 내 참조 심볼(`CardEffectDispatch`, `ActivatedEffectResolver`, `DeferredActivationController`, `TriggerTimings.IsBroadcast` 등)이 실재함을 확인.

### G13-003 — random legal self-play smoke를 회귀 게이트에 편입 (프리즈 *전제*)
- 근거: 무작위-합법 self-play는 엔진 루프 불안정(데드락·불법액션 생성·턴 비진행·무한루프)을 털어내는 도구 → 대량 포팅 전 통과해야 의미. 신규 구축이 아니라 **기존 `HeadlessPolicyEpisodeRunner`/`HeadlessActionPolicy`/`HeadlessGameLoop`/`HeadlessSmokeSuite` 재활용**.
- 산출물: `tests/G13-003.RandomSelfPlaySmoke.Tests/`
  - 고정 시드 집합(예: 8개 시드)으로 **양 플레이어 random-legal 정책** full-game을 돌린다.
  - 단언: ① 각 게임이 턴 상한(예 ≤ 200 의사결정 스텝) 내 **종료 또는 정상 진행**, ② 매 스텝 `GetLegalActions`가 비어있지 않으면 그 중 하나가 **실제로 적용 가능**(불법액션 생성 0), ③ 예외/데드락 0, ④ 결정성(같은 시드 → 같은 결과).
  - run-tests.sh가 `tests/` 전 프로젝트를 자동 수집하므로 게이트에 자동 편입.
- DoD: 스모크 프로젝트가 게이트에서 green. (불안정 발견 시 → G13 change-control 첫 적용 사례로 엔진 수정 + 로그.)

**G13 완료 = engine_change_control.md + 갱신된 recipe + 게이트에 도는 random self-play smoke, 전체 225+green.**

---

## G14 — 고난도 대표 카드 20장 선정표 (서브시스템 태그) + Batch 1 5장 지정

- 목적: 대량 포팅 전, 엔진이 **고난도 패턴을 실제로 커버**하는지 대표 표본으로 실증. 20장은 풀, Batch 1(5장)은 그 중 **서브시스템 다양성 최대** 5장 — 빠진 primitive가 1~5장째에 드러나게.
- 산출물: `docs/audit/g14_representative_cards.md` 표
  - 열: `카드번호 | 세트/색 | 난이도 사유(핵심 기믹) | 자극하는 엔진 서브시스템(태그) | 예상 갭/리스크 | Batch`
  - 서브시스템 태그 예: deferred-activation, security-skill, digivolution-source(under-card), DigiXros/DNA, special-play, multi-target choice, delayed/duration effect, cross-card trigger, replacement(would-be-deleted), link, raid/alliance, memory-swing.
  - 20장은 **태그 중복 최소화**로 선정(엔진 커버리지 맵). 원본 `DCGO/`에서 후보를 식별(AS-IS 참조).
  - Batch 1 5장 = 서로 다른 5개 서브시스템을 덮는 5장으로 명시 + 각 카드의 1:1 포인트 메모.
- DoD: 표 20행 + Batch 1 5장 태그가 상호 배타적(5개 서브시스템). 각 카드번호가 원본에 실재함을 확인. (테스트 코드 없음 → 산출물은 문서; run-tests green 유지만.)

---

## G15 — Batch 1 5장 포팅 (엄격 1:1)

- 작업: G14 Batch 1의 5장을 AS-IS 미러로 포팅. `Assets/Scripts/CardEffect/<Set>/<Color>/<번호>.cs` + 필요한 엔진 primitive는 **G13 change-control 절차**로만 추가(사유 로그 필수).
- 각 카드:
  - 원본 가드/전제 **전부** 복원(엄격 PASS: 누락 시 실패). 빈도/추측 금지.
  - cards.json `effectClass` 별칭 해당 시 디스패치 경유.
  - 동작을 **실제로 단언하는 테스트** 추가(가능하면 라이브 루프 e2e: 활성→suspend→ResolveChoice / 트리거 라이브 발동 등; 단위 검증만 가능한 부분은 명시).
- DoD: 5장 × 테스트 green, 전체 게이트 green. 엔진을 건드렸다면 `engine_change_log.md`에 5건 이하의 정당화 기록. 각 카드가 원본과 1:1임을 코드로 대조한 메모를 테스트 주석 또는 `docs/audit/g15_batch1_notes.md`에 남김.

---

## G16 — Batch 1 공통 helper 정리 + 카드별 테스트 템플릿 확정

- 목적: Batch 1에서 반복 등장한 패턴을 **재사용 helper**로 승격하고, 로컬 LLM이 카드마다 동일하게 찍어낼 **테스트 템플릿**을 확정 → 대량 포팅 산출물이 자가검증되게.
- 작업:
  1. G15 5장(및 기존 35장)에서 중복된 카드-facing 패턴을 `CardEffectFactory`/`CardEffectCommons`의 helper로 추출(중복 제거, 동작 불변). 추출 후 **기존 테스트 전부 green 유지**로 무회귀 증명.
  2. **카드별 테스트 템플릿** 산출물: `docs/audit/card_test_template.md` + 예시 스켈레톤 — (a) 등록/배치 헬퍼, (b) 가드별 양성/음성 단언 슬롯, (c) 라이브 루프 e2e 슬롯, (d) run-tests 편입 방법. LLM이 이 템플릿에 카드별 값만 채우면 되도록.
- DoD: helper 추출 후 전체 게이트 green(무회귀), 템플릿 문서 + 컴파일되는 예시 테스트 1개. recipe(G13-002)에서 템플릿을 참조하도록 링크.

---

## 시퀀스 요약

| Goal | 한 줄 | 산출물 핵심 | 테스트/게이트 |
|---|---|---|---|
| G13 | change-control 선언 + recipe 단일화 + self-play smoke 편입 | engine_change_control.md / 갱신 recipe / RandomSelfPlaySmoke | smoke green + 225 유지 |
| G14 | 고난도 20장 선정표(서브시스템 태그) + Batch 1 5장 | g14_representative_cards.md | 문서(게이트 유지) |
| G15 | Batch 1 5장 포팅(엄격 1:1) | 5 × CardEffect + 테스트 | 5 테스트 green |
| G16 | 공통 helper + 카드별 테스트 템플릿 | helper 추출 + card_test_template.md | 무회귀 green |

> 비고: 원래 제안의 "G17 random self-play smoke(마지막)"는 **G13-003으로 앞당겨 편입**(프리즈 전제). "G13 freeze"는 **change-control**로 확정.
