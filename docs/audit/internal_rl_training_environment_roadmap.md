# 내부 RL 학습환경 개발 로드맵 (L 트랙)

- 작성일: 2026-07-04
- 기준: [internal_rl_training_environment_requirements.md](internal_rl_training_environment_requirements.md)(FR/NFR/C-1~C-8) +
  [internal_rl_training_environment_dev_design.md](internal_rl_training_environment_dev_design.md)(컴포넌트 설계, §11 L 마일스톤의 상세판).
- 관계: [rl_development_roadmap.md](rl_development_roadmap.md)의 **M 트랙**(M1~M6)은 인프라(어댑터·관측·프로토콜·전송·아레나),
  이 문서의 **L 트랙**(L0~L6)은 그 위의 학습환경 시스템(리그·Base·파인튜닝·GA·로그·분석). **M4까지가 L 트랙의 전제.**
- 폴더 배치: 설계 §0.1 — L 트랙 산출물은 `rl/`(Python)과 `runs/`(산출물, gitignore), 엔진 공사는 L4(로그 계측)뿐.

## 현재 위치
- M 트랙: **M1·M2·M4 완료**(2026-07-04). M3(지식 추적)는 카드 포팅 진척 연동 병행, M5(TCP)는 필요 시.
- L 트랙: **L0 완료**(2026-07-04) — 학습환경 수직 슬라이스 관통:
  - Python `rl/` 패키지(레시피 로더·vocab·provider·config·시드 규약, 유닛 32건 green)
  - seat 프로토콜 v1 + `RlBridgeHost`(stdio) + 계약 테스트 9/9 (M4와 공유)
  - `DcgoSeatEnv`(gymnasium, 좌석 교대, 랜덤 상대) + 카드-ID 임베딩 extractor + MaskablePPO train/evaluate
  - vocab 해시 C#↔Python 일치(4206 canonical), RESULT JSONL 로그(브리지단 선반영)
  - **게이트 실측**: 30k 스텝 무크래시 · eval vs 랜덤 **97.5%**(40매치) · **94.6 steps/sec**(4 env) ·
    run-tests 314/314 · RuleAudit 0. 스냅샷 메타 최소형(runs/l0-smoke/meta.json)이 §5.1 포맷 선반영.
- L 트랙 추가: **L1 완료**(2026-07-04) — 리그 인프라(스냅샷·Elo·80/20 샘플러·매치업 매트릭스) 가동,
  게이트 3종 PASS. **다음 = L2(Base 정책)** — RandomDeckProvider + 대규모 런. 단, 랜덤 덱의 의미는
  카드 포팅 폭에 비례하므로 착수 시점은 카드풀 상황과 함께 판단.
- L 트랙 추가: **L4 완료**(2026-07-04) — 매치 사건 로그(OFF~TRACE, JSONL 육하원칙). 남은 L 트랙 =
  **L2(Base)·L3(파인튜닝)·L5(GA)·L6(분석)** — L2/L5/L6은 카드 포팅 진척과 연동 판단.
- 부속 도구: **로컬 GUI 대시보드**(`rl/dashboard/` — stdlib 서버 + 단일 HTML). 리그 현황(레이팅 곡선·
  매치업 히트맵·스냅샷), 리플레이 뷰어(`rl/replay.py` 승격판), 학습 런처(시작/중지/로그 tail).
  실행: `cd rl && .venv/bin/python dashboard/server.py` → http://127.0.0.1:8787 (로컬 전용 바인드).
- L 트랙이 M에 거는 의존: L0 = M4의 Python 최소분(`DcgoStdioEnv`·임베딩 extractor·train.py)에서 시작.
  M5(TCP)는 L 트랙과 독립적으로 언제든 얹을 수 있음(스키마 무변경 전송 승급).
- 콘텐츠 의존: 카드 포팅 진척(기존 development_roadmap Phase 1~3)은 "정책 품질·메타 의미"를 좌우하지만,
  L0~L4는 현재 포팅분(스타터+BT1~3 일부)으로 독립 진행 가능(A-1, A-2).

---

## L0 — 최소 학습 파이프라인 (리그 없음) ✅ 완료 (2026-07-04, M4와 동시)
**목표**: "던진 레시피로 학습이 붙는다" — 이후 모든 단계의 기반 검증.
- `rl/` 패키지 골격 + `pyproject.toml` + 실험 config 로더(설계 §2 스키마).
- 레시피 정규화 로더(설계 §3.2 — 6단계 순서 고정, 미지원 카드 명시 실패) + `FixedPoolProvider`.
- Mode A 최소형: 고정 덱 2개 미러/교차전, MaskablePPO + 카드-ID 임베딩 extractor, terminal ±1.
- RESULT 로그 최소치(승패 요약 JSONL)는 §8 엔진 공사 없이 **브리지단 기록**으로 선반영.
- 시드 유도 규약 확정: `match_seed = H(experiment_seed, match_index)` (FR-6.2).
- **종료조건**: M4 종료조건 공유(스모크 학습 무크래시 + eval 승률 랜덤 대비 유의미 상승 + steps/sec 기록)
  \+ 레시피 JSON 교체만으로 재실험 가능(FR-3.1).

## L1 — 리그 가동 (FR-2 전체) ✅ 완료 (2026-07-04) — C-6 "리그 안정화" 판정 지점
**목표**: 스냅샷 리그가 지속 동작 — 이후 Base·GA·아레나가 소비할 스냅샷 생태계 확립.
- `SnapshotStore`(설계 §5.1 메타 스키마: 레이팅+글로벌스텝 좌표, 덱/카드풀/스키마·vocab 버전 태깅).
- 얼리기: `every_steps` 기본 + 실력 게이트 옵션(FR-2.2).
- 온라인 Elo(`rating.update` 인터페이스 고정 — 후속 교체 가능) + **매치업 매트릭스**(SQLite, (i,j) 누적 —
  FR-2.4, 밸런스 진단 1급 자료).
- 상대 샘플러: 80% 레이팅 근처 + 20% 약점 우선, 표본 미달 시 랜덤 폴백(FR-2.3 콜드스타트).
- 평가 러너 정례화(설계 §7): 고정 매치업×고정 시드 승률 + 리그 레이팅 리포트.
- **종료조건**: 스냅샷 승급 사이클 지속 동작(레이팅 단조 추세) + 매트릭스 표본 축적 후 약점 샘플링 자동 전환
  확인 + 임의 스냅샷 재현 대전(시드) 일치.
- **실행 기록**: `rl/dcgo_rl/league/`(EloBook·MatchupMatrix(SQLite 대칭 기록)·OpponentSampler(80/20+폴백)·
  SnapshotStore(§5.1 메타)·LeagueOpponentPool(매치 단위 배정)) + `train_league.py`(freeze 사이클 콜백 +
  게이트 자동 판정). 유닛 테스트 41건 green. 스모크(60k, L0-300k에서 이어서, freeze 10k):
  스냅샷 6개 편입, 학습기 Elo 1200→**1347**, 샘플러 모드 bootstrap 246→near 1213/weakness **265**/폴백 7
  (자동 전환 관측), vs 스냅샷 승률 47~63%(리그 특성 — 상대 동반 상승), 결정론 재현 대전 일치.
  **GATE1·2·3 전부 PASS.** 처리량 84.8 steps/sec(4 env Dummy + 상대 추론 포함).

## L2 — Base 정책 (FR-1b) 🟠 파인튜닝 부모 = 중추 자산
**목표**: 덱·상대 무관 제너럴리스트 — 모든 후속 실험의 파인튜닝 시작점(C-8).
- `RandomDeckProvider`: 카드풀 제약 내 합법 랜덤 덱 생성(색/매수 규정 준수).
- Base 학습 런: 랜덤 덱 + 리그 80/20 + terminal ±1 + 대규모 스텝(FR-1b.1 "넓고 얕게").
- `base-v1` 버저닝: `card_pool`·`engine_version`·`vocab_version` 메타 기록(FR-1b.2).
- 평가 리포트에 "vs Base 제로샷" 컬럼 고정(FR-1b.4 — 기준선 역할 개시).
- **종료조건**: Base가 랜덤 정책·초기 스냅샷을 안정적으로 이김 + 학습에 안 쓴 신규 레시피 제로샷이
  vs 랜덤 유의미 우위(정성 확인 병행).

## L3 — 파인튜닝 / 집중 학습 모드 (FR-5) 🟠 신세트 연구 파이프라인
**목표**: "제로샷 티어 vs 학습후 티어" 이중 측정 파이프라인 — 임의 덱의 최대 실력 산출.
- vocab append 절차(FR-5.3): 새 슬롯 추가 + 기존 임베딩 보존, vocab 버전 증가 + 핸드셰이크 검증.
- Mode E(FR-5.5): 특정 덱 고정 + 리그 상대 고정 + Base(또는 기존 스냅샷)에서 파인튜닝,
  기존 덱 소량 혼합(기본 신규 80/기존 20 — FR-5.2 망각 방지).
- 이중 측정(FR-5.1): 같은 평가 프로토콜을 파인튜닝 전(제로샷)/후(학습후) 두 번 — 차이 = 덱 스킬천장 지표.
- **종료조건**: 신규 덱 1종에 대해 제로샷/학습후 티어 리포트 산출 + 파인튜닝 후 기존 덱 승률 회귀 없음
  (망각 방지 검증).

## L4 — 로그 인프라 본공사 (FR-7) ✅ 완료 (2026-07-04) — 엔진측 유일 공사
**목표**: OFF~TRACE 레벨 계측 — GA 기여도(L5)·밸런스 분석(L6)의 데이터 공급원.
- `MatchLogLevel`(OFF/RESULT/REPLAY/ANALYSIS/TRACE 누적) + `MatchConfig` 주입, 기본 OFF.
- 계측: `MatchStateMutationSink` 자동 emit + 프리미티브 지점(≈88) — 카드 소급 없음(FR-7.2).
- 레벨 가드 규율: 이벤트 객체 생성/포매팅은 가드 뒤(FR-7.3 — 위반은 리뷰 반려).
- 육하원칙 이벤트 스키마(설계 §8.3) + `tags` 예약 필드(semantic 태깅 자리만 — FR-7.5).
- **종료조건**: OFF 오버헤드 무측정차(벤치마크) + ANALYSIS 레벨 로그로 §9 지표 산출 가능 확인 +
  `bash scripts/run-tests.sh` green + RuleAudit 0.
- **실행 기록**: 상세 설계 = [rl_l4_match_log_design.md](rl_l4_match_log_design.md). **AS-IS 조사로 설계
  가정 상향**: GameEvent 육하원칙 필드·존무버 이벤트 스트림·매치 깔때기가 기존재 → **새 계측 0,
  소비자 1**(`MatchEventLog` — 레벨 분류 + turn/phase/matchId 스탬프 + JSONL). DcgoMatch 가산 파라미터
  1개 + 훅 1줄, 호스트 `--log-level`/`--event-log`, BridgeClient 관통. 게이트: OFF 181.6 vs ANALYSIS
  기록 중 219.5 steps/sec(무측정차), `L4-001` 계약 5/5(배틀소멸=CardMoved BattleArea→Trash 관측 포함),
  ANALYSIS 20판 13,606줄 → 카드별 플레이 시 승률 지표 산출(FR-8.1 최소형), run-tests **315/315** +
  RuleAudit 0. cause 정밀화·TRACE 브릿지·semantic tags는 설계 §3 후속.

## L5 — 덱 진화 GA (FR-4) 🟡 활성 게이트 = 운영자 판단 + 승률 수렴
**목표**: 바깥 루프 가동 — 창발 메타 관찰 개시.
- **활성 게이트**(요구사항 결정 §8): 초기 메타가 예상 티어와 유사(운영자 판단) + 승률 수렴(최신 정책이
  과거 스냅샷을 안정적으로 이기다 정체). 둘이 같이 서기 전 착수 금지(A-4).
- `GaProvider`(§3.3 시그니처 준수 — config 한 줄 전환): population·교배(매수 교환)·변이(카드 치환)·도태.
- 기여도 유도 변이(FR-4.2): L4 ANALYSIS 로그의 카드별 사용/미사용 승률차로 치환 후보 가중.
- 반사실 검증(FR-4.3): 상위 기여 카드 제외 변종을 population에 강제 주입(GA 통로 재사용).
- population 이력 전량 저장(FR-4.4 — 세대별 레시피+적합도 JSONL).
- **종료조건**: population 승률 분포가 세대에 따라 개선 + 창발 레시피 조회/집계 가능 + 반사실 검증 1건 수행.

## L6 — 밸런스 분석 (FR-8) 🟢 최종 — 메타 진화 시뮬레이터 완성
**목표**: 로그 → 밸런스 인사이트. 카드 포팅 진척에 비례해 가치 성장(NFR-5).
- 집계 파이프라인(DuckDB/pandas): 채용률·사용/미사용 승률차·색/타입 밸런스·선후공·결착 턴·메타 다양성.
- 통계 규율(FR-8.4): 표본수·신뢰구간 필수, 인과 주장은 반사실 대전(L5 통로)으로만.
- 발매순 개방 실험 1건: 실제 역사(너프/메타)와 대조(FR-8.3).
- **종료조건**: 지표 대시보드/리포트 산출 + 실제 역사 대조 리포트 1건.

---

## 통합 타임라인 (M 트랙 + L 트랙)

```
M1 ─▶ M2 ─▶ M4 ═ L0 ─▶ L1 ─▶ L2 ─▶ L3 ─▶ L5 ─▶ L6
        └▶ M3 (지식추적 — 카드 포팅 연동)   └▶ L4 (병행; RESULT 최소치는 L0에 선반영)
M5(TCP/내부망) — L1 이후 아무 때나(전송 승급, 스키마 무변경). 대규모 Base 런(L2)을 다중 머신으로 돌리려면 선행 권장.
M6(아레나)    — L1 스냅샷 스토어를 봇 공급원으로 재사용(C-7). L 트랙 완주와 독립.
카드 포팅(Phase 1~3) ──▶ 정책 품질·M3 실배선·L5/L6 의미(A-1) — 전 구간 병행.
```

## 교차 원칙 (전 마일스톤 공통)
- **매 마일스톤 게이트**: C# 변경분은 `bash scripts/run-tests.sh` green + RuleAudit 0. Python은 자체 테스트
  (정규화·샘플러·매트릭스·vocab append 유닛) green.
- **모드 = config**: 새 실험이 코드 분기를 낳으면 설계 위반(NFR-4). provider/훅 인터페이스로 흡수.
- **버전 계약**: obs/action 스키마·vocab 버전은 핸드셰이크 검증 — 불일치 즉시 실패, 조용한 진행 금지.
- **결정론**: 모든 리포트에 experiment_seed 명기 — config+seed로 재현 불가한 결과는 결과가 아님(NFR-3).
- **YAGNI 경계**(NFR-6): 셰이핑(FR-1.3 훅)·semantic 태깅(`tags`)·ONNX export는 자리만 — 트리거 조건
  (학습 불착/분석 니즈/벤치마크 임계) 발생 전 구현 금지.

## 권장 즉시 다음 액션
L 트랙은 M4까지를 전제하므로 순서는 기존 권고 그대로 — **M1(어댑터 분리)** 착수. L 트랙에서 선행 가능한 것:
1. **레시피 정규화 로더(§3.2) + 유닛 테스트** — 브리지 없이 순수 Python, 지금 바로 개발·검증 가능.
2. **실험 config 스키마(§2) 확정** — M4 프로토콜 스펙 v1 문서와 함께 계약을 코드 전에 고정.
