# 내부 RL 학습환경 — 개발 설계안 (draft v0.1)

- 작성일: 2026-07-04
- 상태: **draft v0.1** — 구현 착수 전 상세 설계. living document.
- 기준 문서:
  - [internal_rl_training_environment_requirements.md](internal_rl_training_environment_requirements.md) — **무엇을**(FR/NFR/확정 제약 C-1~C-8). 이 설계안은 그 요구사항의 "어떻게".
  - [rl_training_environment_design.md](rl_training_environment_design.md) v0.2 — 엔진/어댑터 경계, 관측(정보집합), seat 매치 프로토콜. **이 설계안은 그 위 레이어**(트레이너측 학습환경 시스템)를 다루며, 아키텍처 확정 8건을 전제로 재론하지 않는다.
  - [rl_development_roadmap.md](rl_development_roadmap.md) M1~M6 — 이 설계안의 컴포넌트는 M4(로컬 학습 슬라이스) 이후에 얹히는 **L 마일스톤**(§11)으로 배치.
  - [internal_rl_training_environment_roadmap.md](internal_rl_training_environment_roadmap.md) — §11 L 마일스톤의 독립 로드맵(상세 목표/작업/종료조건).
- 범위 원칙: 설계 단계 문서 — 코드 변경 없음. 엔진 코어 신규 공사는 **로그 인프라(§8) 하나뿐**이며 나머지는 전부 Python `rl/` 또는 `HeadlessDCGO.Rl` 어댑터 위.

## 0. 한눈에 — 이중 루프의 구현 형상

요구사항 §3의 이중 루프(바깥=덱 공급원, 안쪽=플레이 정책)를 컴포넌트로 옮기면:

```
┌────────────────────────── Python rl/ (트레이너측, 신규) ──────────────────────────┐
│  experiments/   실험 모드 진입점(§2)  — config 로 모드 조합(NFR-4)                  │
│  decks/         덱 공급원(§3)        — RecipeLoader · DeckProvider(fixed/random/GA)│
│  policy/        정책(§4)             — CardVocab·임베딩 extractor·Mlp↔LSTM 팩토리   │
│  league/        리그(§5)             — SnapshotStore · Elo · 80/20 샘플러 · 매치업M │
│  runners/       실행(§7)             — DcgoSeatEnv(gymnasium) · VecEnv · 평가 러너  │
│  analysis/      분석(§9, 후속)       — 로그 집계                                    │
├──────────────────────── seat 매치 프로토콜 (설계 v0.2 §6, M4) ─────────────────────┤
│  tools/RlBridgeHost (stdio → TCP)  — 좌석별 관측/마스크/보상, 이벤트 가시성 필터      │
├──────────────────────── HeadlessDCGO.Rl 어댑터 (M1~M2) ───────────────────────────┤
│  ObservationEncoder(정보집합) · FactoredActionEncoder · EpisodeBatchRunner          │
├──────────────────────── HeadlessDCGO.Engine 코어 ─────────────────────────────────┤
│  규칙·상태·결정론 + (신규 §8) MatchLogLevel 계측: MatchStateMutationSink·프리미티브  │
└────────────────────────────────────────────────────────────────────────────────┘
```

- **스냅샷 = 공통 화폐**(C-7): `league/snapshots`의 산출물이 리그 상대·아레나 봇·파인튜닝 부모로 그대로 쓰인다.
- **한 대전 로그, 세 소비자**: `runners/`가 남긴 매치 로그를 RL(승패)·GA(카드 기여)·analysis(밸런스)가 공유(§8).

### 0.1 저장소 폴더 배치 — Python 학습환경은 최상위 독립 폴더

경계가 코드 참조가 아니라 **seat 프로토콜(JSON)** 이므로, Python 학습환경은 C# 빌드와 의존이 전혀 없다
(vocab조차 핸드셰이크 교환 — §4.1). 따라서 최상위 독립 폴더로 격리한다:

```
headlessDCGO/
├── src/
│   ├── HeadlessDCGO.Engine/    ← 기존 엔진 (M2/M3 스냅샷·지식 확장 + §8 로그 계측만 가산)
│   └── HeadlessDCGO.Rl/        ← C# 어댑터 (M1, 별도 어셈블리 — 솔루션 안 유지: ProjectReference·테스트 21개)
├── tools/
│   └── RlBridgeHost/           ← 브리지 호스트 (M4 — C#이므로 솔루션 안)
├── rl/                         ← ★ Python 학습환경 전체 (이 문서의 §3~§7·§9, 완전 독립)
│   ├── pyproject.toml          ← 자체 의존성 관리 (sb3-contrib 등, C# 빌드와 무관)
│   ├── configs/                ← 실험 YAML (§2)
│   └── dcgo_rl/
│       ├── decks/  policy/  league/  runners/  analysis/  experiments/
└── runs/                       ← 실험 산출물: 스냅샷·매치 로그·매치업 매트릭스 (gitignore — 코드 아님)
```

- 구분 3계층: ① `rl/` = 완전 분리(빌드 무의존) ② `HeadlessDCGO.Rl`·`RlBridgeHost` = 폴더는 나뉘나 C#
  솔루션 안 ③ 엔진 코어 가산 변경(M2/M3·§8) = 분리 대상 아님.
- **저장소 분리 시점**: M4 기간에는 프로토콜 스키마가 브리지와 함께 진화하므로 같은 저장소가 실용적.
  프로토콜 버전이 안정화되는 M5~M6에 `rl/`을 별도 저장소로 분리 가능(경계가 프로토콜이라 이사 비용 낮음).
- `runs/`(스냅샷 스토어 §5.1의 물리 위치·로그 §8.3 출력)는 저장소 추적 밖 — `.gitignore` 등록.

## 1. 설계 원칙 (요구사항 제약의 구현 귀결)

1. **모드 = 데이터, 코드 아님**(C-5, NFR-4): 덱 공급원×정책 학습여부×보상×로그레벨은 전부 실험 config(YAML)의
   필드. 새 실험 = 새 config 파일. 코드 분기 최소.
2. **덱 공급원 단일 인터페이스**(FR-3.4): 손으로 던진 레시피든 GA 산출이든 `DeckProvider.next_matchup()` 하나로
   들어온다. GA는 "레시피를 생성하는 또 하나의 provider"일 뿐.
3. **vocab 단일 진실**(FR-3.3, FR-5.3): 카드번호→정수 매핑은 `CardVocabulary` 하나. 레시피 정규화기·임베딩
   테이블·스냅샷 메타가 전부 이 vocab 버전을 참조. 확장은 append-only.
4. **스냅샷 좌표 = 레이팅 + 글로벌 스텝**(FR-2.2, 결정 §8): 세대 번호는 저장하되 정렬 축으로 쓰지 않는다.
5. **엔진 코어 터치 최소**(로드맵 교차 원칙): 이 문서에서 엔진에 넣는 것은 로그 계측(§8)뿐. 나머지는 위 레이어.

## 2. 실험 모드 매트릭스 (C-5 토글의 구체화)

| 모드 | 덱 공급원 | 안쪽 정책 | 대응 요구사항 | 시점 |
|---|---|---|---|---|
| **A. 리그 학습** (시작점) | 고정 레시피 풀 | SB3 학습 + 리그 | C-6 전반, FR-1.1, FR-2 | L0~L1 |
| **B. Base 학습** | 최대 랜덤화 | SB3 대규모 학습 | FR-1b | L2 |
| **C. 평가/수집** | 고정 | 고정 스냅샷 | FR-5.1 제로샷, FR-8 데이터 수집 | L1 |
| **D. 덱 진화** | **GA** | 고정 스냅샷(또는 저LR 파인튜닝) | FR-4, A-4 | L5 |
| **E. 집중 학습** | 특정 덱 고정 | 파인튜닝(리그 상대 고정) | FR-5.5 | L3 |

config 스키마(초안):

```yaml
experiment: bt1-league-01
seed: 42
deck_source: { type: fixed, recipes: [decks/st1.json, decks/bt1_red.json] }  # fixed|random|ga
policy:
  arch: mlp            # mlp | recurrent (FR-1.2)
  init: base-v1        # null(밑바닥) | 스냅샷 id | base 버전 (FR-1b, FR-5.5)
  learn: true          # false = 고정 스냅샷 평가 모드
reward: { type: terminal }   # terminal(±1 기본, C-4) | 교체형(FR-1.3)
league:
  enabled: true
  freeze: { every_steps: 2_000_000, skill_gate: null }   # FR-2.2
  sampling: { near_rating: 0.8, weakness: 0.2, weakness_min_games: 200 }  # FR-2.3
log_level: RESULT      # OFF|RESULT|REPLAY|ANALYSIS|TRACE (FR-7.1)
parallel: { n_envs: 16 }
```

## 3. 덱 공급 (FR-3)

### 3.1 레시피 포맷 (정규화 후 내부 표준)
```json
{
  "name": "bt1_red_aggro",
  "source": "operator",              // operator | ga | random
  "vocab_version": "v1",
  "main":     [ {"card": "BT1-020", "count": 4}, ... ],
  "digitama": [ {"card": "BT1-001", "count": 4}, ... ]
}
```

### 3.2 정규화 파이프라인 (FR-3.2 — 단계 고정, 순서 의미 있음)
1. 헤더/주석 행 제거 → 2. 구분자 정규화(하이픈→언더스코어 등 표기 편차) → 3. 일러 변형 collapse(`_P1` 류 접미
   제거) → 4. 중복 행=매수 집계 → 5. `CardDatabase` 조회로 카드 타입 확인 → **메인/디지타마 분리** →
   6. **미지원 카드 = 명시 실패**(에러에 카드번호 나열; 조용한 skip 금지 — 충실도 원칙).
- P·LM 등 자체 효과 카드 특별 취급 없음(일반 조회).
- 구현 위치: Python `rl/decks/recipe.py`. C# 쪽과의 카드 존재 대조는 CardDatabase가 읽는 카드 데이터 JSON을
  같이 읽거나, 브리지 핸드셰이크의 vocab 교환(§4.1)으로 검증 — **이중 구현 금지**, vocab 교환 방식을 기본으로.

### 3.3 DeckProvider 인터페이스 (FR-3.4)
```python
class DeckProvider(Protocol):
    def next_matchup(self, rng) -> tuple[Recipe, Recipe]: ...
    def report_result(self, matchup_id, result): ...   # GA만 소비, 나머지 no-op
```
- `FixedPoolProvider`(균등/가중 샘플), `RandomDeckProvider`(Base용 §4.3 — 카드풀 제약 내 합법 랜덤 덱),
  `GaProvider`(§6, 후속). 셋 다 같은 시그니처 = GA on·off가 config 한 줄.

## 4. 정책 (FR-1, FR-1b)

### 4.1 덱-조건부 관측 (C-2)
- 기반: M2 정보집합 벡터(본인 손패/공개존 per-card 정체, 본인 시큐리티/덱 count-only).
- **카드-ID 채널**: features extractor가 카드ID → 공유 임베딩 테이블 → 존별 pooling(순서 없는 존은 sum/mean,
  슬롯 존은 per-slot concat).
- **덱 정체 채널**: 자기 덱 리스트(레시피의 카드ID 멀티셋)를 임베딩 합으로 인코딩해 관측에 상시 포함 —
  "게임이 진행돼야 덱을 아는" 지연 없이 **처음부터 덱-조건부**.
- **vocab 핸드셰이크**: 브리지 접속 시 obs/action 스키마 버전과 함께 vocab 버전 교환. 불일치 = 즉시 실패
  (조용한 매핑 어긋남 방지).

### 4.2 정책 팩토리 (FR-1.2) / 보상 (FR-1.3)
- `policy/factory.py`: config `arch:` 하나로 MaskablePPO(Mlp) ↔ RecurrentPPO 생성. 순차계약(설계 v0.2 §6.3)은
  브리지가 보장하므로 교체가 환경/어댑터를 안 건드림.
- 보상: 좌석별 귀속(승자 +1/패자 −1/무 0)은 **브리지 계약**(M4 프로토콜 스펙 🔴1)이 원천. 트레이너단
  `RewardTransform` 훅은 그 위의 선택적 변환(기본 identity). 셰이핑 실험은 이 훅에만 — 엔진/브리지 무변경.

### 4.3 Base 정책 파이프라인 (FR-1b)
- **학습 레시피**(FR-1b.1): `RandomDeckProvider`(카드풀 전체에서 합법 덱 랜덤 생성 — 색/매수 규정 준수) +
  리그 80/20 상대 + terminal ±1 + 대규모 스텝. "넓고 얕게".
- **버저닝**(FR-1b.2): `base-v{N}` 이름 + 스냅샷 메타에 `card_pool`, `engine_version`, `vocab_version` 기록.
  갱신 트리거 = 카드풀 대확장 or 엔진 룰 변경(시즌 경계 정렬). 갱신 방식 = 재학습 or 대규모 파인튜닝
  (vocab append 후 이어서) — 실측 비교 후 결정.
- **호환 규칙**(FR-1b.3, FR-5.3): vocab append-only + 기존 임베딩 행 보존. 신카드 행 초기화는 랜덤(기본) —
  유사카드 평균 초기화는 후속 실험.
- **기준선 역할**(FR-1b.4): 모든 평가 리포트에 "vs Base 제로샷" 컬럼 고정 — 덱 복잡도/스킬천장 지표의 분모.

## 5. 리그 / 스냅샷 (FR-2)

### 5.1 SnapshotStore
```
runs/snapshots/                # 물리 위치 = §0.1 runs/ (gitignore)
  base-v1/                     # Base도 스냅샷의 일종 (lineage 루트)
  {lineage}/{snapshot_id}/
    policy.zip                 # SB3 저장 포맷 (후속: ONNX export 병행)
    meta.json
```
`meta.json` (FR-2.2 좌표화 + FR-2.5 태깅 + FR-2.6 아레나 배포 호환):
```json
{
  "snapshot_id": "bt1red-s0042",
  "lineage": "bt1red", "parent": "base-v1",
  "global_step": 24_000_000, "generation": 42,
  "rating": 1543.2, "rating_games": 812,
  "frozen_at": "2026-07-04T…", "freeze_reason": "steps|skill_gate",
  "deck_context": ["bt1_red_aggro"], "card_pool": "ST1-3+BT1",
  "obs_schema_version": "…", "action_schema_version": "…", "vocab_version": "v1",
  "arch": "mlp"
}
```
- 얼리기(FR-2.1~2.2): 기본 = `every_steps` 주기. 옵션 = 실력 게이트(최근 eval 구간 승률 임계 통과 시만).
  정책(lineage)마다 주기 달라도 됨 — 편입 시 레이팅으로 정렬되므로 무관(결정 §8).

### 5.2 레이팅 + 매치업 매트릭스 (FR-2.3~2.4)
- **레이팅**: 온라인 Elo로 시작(구현 단순, 리그 난이도 조절 용도로 충분). Glicko/TrueSkill 승급은 아레나(M6)
  결정과 함께 — 인터페이스(`rating.update(a, b, result)`)만 고정해 교체 가능하게.
- **매치업 매트릭스**: `(i, j) → {wins, losses, draws}` 누적 저장(SQLite 단일 파일 — 동시 기록·집계 쿼리·
  파일 하나 백업의 균형). 파생 조회: 승률, 표본수, **내 최저 승률 상대 top-k**(약점 샘플링), 비이행 사이클
  탐지(밸런스 진단 — FR-2.4 후단).
- **상대 샘플러**(FR-2.3): `0.8 × (|Δrating| ≤ W 균등)` + `0.2 × 약점 우선`. 약점 축은 상대별 표본
  `weakness_min_games` 미달이면 랜덤으로 폴백(초반 콜드스타트 — 결정 §8 그대로). W·비율은 config 튜너블.

### 5.3 스냅샷 풀 상한 (보류 §10)
- v0.1에서는 상한 없음(초기 풀이 작음). 스토어에 `retired: bool` 필드만 예약 — 솎아내기 정책(시대별 대표
  보존)은 풀이 커진 뒤 별도 결정. 지금 만들지 않음(NFR-6).

## 6. 덱 진화 GA (FR-4 — 후속, 인터페이스만 고정)

- `GaProvider`가 §3.3 시그니처로 편입: population의 레시피를 `next_matchup`으로 배급, `report_result`로 적합도
  누적. **활성 게이트 = 결정 §8**(운영자 판단 + 승률 수렴) — 코드가 아니라 운영 절차.
- 적합도 = 리그 고정 스냅샷 상대 승률(안쪽 정책 고정 = A-4 순서 준수). 교배=카드 매수 교환, 변이=카드 치환.
- **기여도 유도 변이**(FR-4.2): §8 ANALYSIS 로그의 카드별 (사용시 승률 − 미사용시 승률) 통계로 치환 후보
  가중. **반사실 검증**(FR-4.3): 상위 기여 카드 제외 변종을 population에 강제 주입해 대전 — 별도 장치가
  아니라 GA 배급 통로 재사용.
- population 이력 전체를 저장(FR-4.4 창발 메타 관찰) — 세대별 레시피+적합도 JSONL.

## 7. 실행 / 병렬 (FR-6)

- **토폴로지(1차, M4 형상)**: 브리지 호스트 프로세스 N개(stdio) × SB3 `VecEnv` 래핑. 공유정책 셀프플레이는
  1연결=2좌석 모드(샘플효율↑), 리그 대전은 학습기 1좌석 + 스냅샷 좌석은 호스트측/별도 프로세스 추론.
- **시드**(FR-6.2, NFR-3): `match_seed = H(experiment_seed, match_index)` 유도 → `EngineContext.CreateDefault(seed)`.
  실험 config + match_index만으로 어떤 매치든 재현. 평가 러너는 시드 집합 고정(대조군 간 공정 비교).
- **평가 러너**: (a) 고정 매치업×고정 시드 집합 승률 (b) vs Base 제로샷 (c) 리그 레이팅 — 세 지표 정례 리포트.
- **승급 경로**(FR-6.3, 보류 §9): M4 steps/sec 벤치마크 수치가 기준. 임계 미달 시 바이너리 직렬화 → 그래도
  부족하면 C# 인프로세스 롤아웃 + ONNX(스냅샷 스토어에 ONNX export를 그때 추가). 지금은 측정 지점만 확보.

## 8. 로그 인프라 (FR-7) — 엔진측 유일한 신규 공사

### 8.1 레벨과 가드
- `MatchLogLevel { OFF=0, RESULT=1, REPLAY=2, ANALYSIS=3, TRACE=4 }` (누적적). `MatchConfig`에 주입,
  기본 OFF(FR-7.1 — 대량 학습 오버헤드 0).
- **가드 패턴**(FR-7.3): emit 지점은 `if (logger.Level < ANALYSIS) return;` 뒤에서만 이벤트 객체 생성 —
  비활성 비용 = 분기 1회. 문자열 포매팅/할당이 가드 앞에 오는 구현은 리뷰에서 반려.

### 8.2 계측 지점 (FR-7.2 — 카드 소급 없음)
- **단일 뮤테이션 sink**: `MatchStateMutationSink`(실재 확인) — 모든 상태변경이 지나는 지점에서 존 이동·
  스탯 변경 이벤트 자동 emit. 카드 3918개 개별 계측 불필요.
- **프리미티브 지점**(≈88): 효과 발동/해소, RevealAndSelect, 어택 파이프라인 단계 등 — ANALYSIS 레벨.
- REPLAY 레벨 = 그중 "사람이 알아볼 굵은 선"(플레이/진화/어택/시큐리티 개봉/승패)만 통과시키는 필터.

### 8.3 이벤트 스키마 (FR-7.4 육하원칙)
```json
{ "seq": 1287, "turn": 6, "phase": "Main",
  "type": "ZoneMove", "actor": "P1",
  "subject": {"cardId": "BT1-020", "instanceId": 314},
  "target": null, "zoneFrom": "Hand", "zoneTo": "BattleArea",
  "cause": {"kind": "PlayCard", "sourceCardId": null},
  "tags": [] }
```
- `tags` = semantic 태깅 예약 필드(FR-7.5 — 자리만, 채우지 않음).
- 출력: 매치당 JSONL 1파일(RESULT는 요약 1줄 — 시드·덱·승패·턴수·매치메타). 세 소비자(RL/GA/분석)가
  같은 파일을 각자 필터로 소비.
- **주의**: 이 로그는 서버측 전량 기록(분석용)이고, **좌석에 중계되는 이벤트는 별도로 가시성 필터를 거친다**
  (설계 v0.2 §3 갭 — 혼동 금지).

## 9. 밸런스 분석 (FR-8 — 후속, 스키마만 이 문서에서 고정)

- §8.3 스키마가 산출 가능해야 하는 지표: 카드 채용률·사용률, 사용시/미사용시 승률 차(기여도 1차 근사),
  색·타입별 승률, 선후공 승률, 평균 결착 턴, 메타 다양성(population 엔트로피).
- 집계 구현(DuckDB/pandas)·대시보드·실제 역사 대조(FR-8.3)는 후속. 상관≠인과 규율: 기여도 주장에는 표본수·
  신뢰구간 필수, 인과 주장은 반사실 대전(§6)으로만.

## 10. FR 커버리지 매핑

| 요구사항 | 담당 컴포넌트 (§) | 단계 |
|---|---|---|
| FR-1.1~1.3 정책 학습 | policy/ (§4.1~4.2) | L0 |
| FR-1b Base | policy/ + RandomDeckProvider (§4.3) | L2 |
| FR-2 리그/스냅샷 | league/ (§5) | L1 |
| FR-3 덱 로더 | decks/recipe·providers (§3) | L0 |
| FR-4 GA | decks/ga (§6) | L5 |
| FR-5.1~5.4 신규 편입 | vocab append(§4.3) + 평가 러너(§7) | L3 |
| FR-5.5 집중 학습 | 모드 E(§2) = 기존 부품 조합, 신규 코드 최소 | L3 |
| FR-6 병렬/결정론 | runners/ (§7) | L0(기본)~ |
| FR-7 로그 | 엔진 계측(§8) | L4(RESULT는 L0) |
| FR-8 분석 | analysis/ (§9) | L6 |
| NFR-1 조각 재사용 | 스냅샷 메타 버전들 + seat 프로토콜 공용(v0.2) | 상시 |
| NFR-4 유연 구성 | config 스키마(§2) | L0 |

## 11. 개발 순서 — L 마일스톤 (M 로드맵과의 정렬)

전제: **M1(어댑터 분리) → M2(관측 확장) → M4(seat 프로토콜 + stdio + 학습 슬라이스)**. M4의 Python 최소분
(`DcgoStdioEnv`, 임베딩 extractor, train.py)이 L0의 시작점이다.

- **L0 — 최소 리그 없는 학습** (M4 종료조건과 사실상 동일 + 레시피 로더): 정규화 로더(§3.2) + 고정 덱 2개
  + Mode A(리그 없이 미러전) + RESULT 로그 최소치(승패 요약 — §8 전체 공사 없이 브리지단 기록으로 시작 가능).
  게이트: M4 종료조건(랜덤 대비 유의미 승률 + steps/sec 기록).
- **L1 — 리그 가동**: SnapshotStore + Elo + 80/20 샘플러 + 매치업 매트릭스(§5). 게이트: 스냅샷 승급 사이클이
  지속 동작(레이팅 단조 추세), 매트릭스 조회로 약점 샘플링 전환 확인. ← **C-6 "리그 안정화"의 판정 지점**.
- **L2 — Base 정책**: RandomDeckProvider + 대규모 런(§4.3). 게이트: Base가 랜덤 정책·초기 스냅샷을 안정적으로
  이기고, 임의 신규 레시피 제로샷이 "말이 되는" 수준(정성 + vs 랜덤 승률).
- **L3 — 파인튜닝/집중 학습**: 모드 E + 제로샷/학습후 이중 측정(FR-5.1) + vocab append 절차 검증(FR-5.3).
  게이트: 신규 덱 1종에 대해 "제로샷 티어 vs 학습후 티어" 리포트 산출.
- **L4 — 로그 인프라 본공사**(엔진, L1~L3과 병행 가능): §8 레벨/계측/스키마. 게이트: OFF 오버헤드 무측정차
  + ANALYSIS 레벨에서 §9 지표 산출 가능한 로그 생성 + run-tests green·RuleAudit 0.
- **L5 — GA**: §6. 활성 게이트 = 결정 §8(운영자 판단 + 승률 수렴). 게이트: population 승률 분포가 세대에
  따라 개선 + 창발 레시피 조회 가능.
- **L6 — 분석**: §9 집계 + 실제 역사 대조 1건.

```
M1 ─▶ M2 ─▶ M4 ═ L0 ─▶ L1 ─▶ L2 ─▶ L3 ─▶ L5 ─▶ L6
                          └▶ L4 (병행; RESULT 최소치는 L0에 선반영)
M5(TCP/내부망)는 L1 이후 아무 때나(전송 승급 — 스키마 무변경), M6(아레나)는 스냅샷 스토어(§5.1) 재사용.
```

## 12. 미확정 / 후속 결정 (요구사항 §9~10 승계 + 이 설계에서 추가)

- 레이팅 근처 폭 W·80/20 비율·`weakness_min_games` 구체값 — L1 실측으로.
- 스냅샷 풀 상한/솎아내기 — 풀 성장 후(§5.3).
- 직렬화 병목 임계/ONNX 전환 — M4 벤치마크 후(요구사항 §9 그대로).
- 신카드 임베딩 초기화(랜덤 vs 유사카드 평균) — L3 실험.
- Base 갱신 방식(재학습 vs 대규모 파인튜닝) — L2 이후 비교.
- 최소 셰이핑 형태·감쇠(sparse 보상 실패 시 비상구) — L0/L1에서 학습이 아예 안 붙을 때만 개봉(FR-1.3 훅 위).
