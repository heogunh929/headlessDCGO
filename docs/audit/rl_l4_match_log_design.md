# L4 — 매치 로그 인프라 설계 (FR-7)

- 작성일: 2026-07-04. 기준: [internal_rl_training_environment_dev_design.md](internal_rl_training_environment_dev_design.md) §8,
  요구사항 FR-7, 로드맵 L4.
- 상태: 구현 직전 확정 설계 (AS-IS 조사 완료).

## 0. 조사 결론 — 계측은 이미 존재한다 (설계 §8 가정의 상향 수정)

dev design §8은 "프리미티브 ~88 지점 + MatchStateMutationSink에 emit을 심는다"고 가정했으나, 실측 결과
**이벤트 생산은 이미 완비**되어 있다:

| 기존 자산 | 내용 |
|---|---|
| `GameEvent` | **육하원칙 필드 기존재**: Sequence/Type/Actor/Subject/Target/ZoneFrom/ZoneTo/Cause/Metadata (G3.5-RL-B2) |
| `GameEventType` | CardMoved·AttackDeclared/Resolved·SecurityCheck/Skill·Effect*·Choice*·Action*·GameEnded 등 19종 |
| `InMemoryZoneMover.Events` | **모든 존 이동**의 append-only 스트림 — CardMoved에 from/to/faceUp/operation까지 채워짐 |
| `DcgoMatch._pendingEvents` | 액션/어택/효과/종료 이벤트의 매치 깔때기(Apply/Step 결과로 배출) |
| `EngineTrace`(ITraceSink) | 엔진 내부 진단 채널(TRACE 후보), Enabled 게이트 기보유 |

따라서 **L4 = 새 계측 0, 소비자 1 추가**: 두 깔때기(존무버 스트림 + 매치 이벤트)를 레벨 게이트로 걸러
JSONL로 쓰는 `MatchEventLog` 하나를 붙인다. FR-7.2(카드 3918개 소급 불필요)는 자동 충족.

## 1. 컴포넌트

### 1.1 `MatchLogLevel` (engine `Headless/Diagnostics/`)
`OFF=0 · RESULT=1 · REPLAY=2 · ANALYSIS=3 · TRACE=4` (누적적). 기본 OFF.
RESULT 요약 1줄은 이미 브리지 호스트의 `--result-log`가 담당(L0 선반영) — 레벨 체계에 편입만 한다.

### 1.2 `MatchEventLog` (engine `Headless/Diagnostics/`)
- 생성: `(MatchLogLevel level, TextWriter sink, string matchId)`. 파일 열기/수명은 호출자(호스트) 소관.
- `Attach(EngineContext)`: 존무버 이벤트 커서 초기화 + 턴 상태 리더 확보.
- `LogStep(IReadOnlyList<GameEvent> matchEvents)`: DcgoMatch가 스텝 경계에서 호출.
  1) `Level < REPLAY`면 즉시 반환(**분기 1회** — FR-7.3; 이벤트 객체는 트리거용으로 어차피 생산되므로
     로깅 OFF의 추가 비용은 소비 생략뿐),
  2) 존무버 스트림을 커서로 drain(스텝 간 신규분만) + 매치 이벤트와 병합,
  3) 타입→레벨 분류로 필터, 4) turn/phase/matchId 스탬프 후 JSONL 1행씩 기록.

### 1.3 타입 → 레벨 분류 (FR-7.1 의미 그대로)
| 레벨 | GameEventType |
|---|---|
| REPLAY (사람이 알아볼 굵은 선) | CardMoved, ActionProcessed, AttackDeclared/Resolved, SecurityCheck, SecuritySkill, ChoiceResolved, GameEnded |
| ANALYSIS (효과 발동 등 잔 사건) | EffectQueued/Resolved, ChoiceRequested/Cleared, DelayedTrigger, StateChanged(타이밍 윈도), ActionQueued, AttackCleared |
| TRACE (그 외 전부 + 진단) | InvalidAction, Unknown, (후속) ITraceSink 브릿지 |

### 1.4 JSONL 스키마 (FR-7.4 + FR-7.5 자리)
```json
{"matchId":"m-77-0","seq":1287,"turn":6,"phase":"Main","type":"CardMoved",
 "actor":1,"subject":"card-…","target":null,"zoneFrom":"Hand","zoneTo":"BattleArea",
 "cause":"Move","tags":[]}
```
- `message`/`metadata`는 TRACE에서만 포함(부피 절약). `tags` = semantic 태깅 예약(비움).
- 배틀소멸 등 결과 사건은 CardMoved(BattleArea→Trash)로 나타난다 — cause 정밀화(예: "SecurityBattle")는
  후속 enrichment(§3).

### 1.5 배선
- `DcgoMatch` ctor에 `MatchEventLog? eventLog = null` **가산 파라미터**. 이벤트 배출 지점
  (`DrainStepResult`)에서 `_eventLog?.LogStep(events)` 1줄.
- 호스트(RlBridgeHost): `--log-level`, `--event-log <path>` CLI → reset마다 matchId 스탬프.
- Python: `BridgeClient(log_level=…, event_log=…)` → `DcgoSeatEnv`/`train*.py` 옵션 관통
  (config `log_level` 필드와 연결 — NFR-4).

## 2. 게이트 (로드맵 L4 종료조건 구체화)
1. OFF 오버헤드: stdio 랜덤 스모크 steps/sec — OFF vs 미주입 기준 무측정차(±5% 이내).
2. ANALYSIS 로그 소비 증명: JSONL 집계로 **"카드별 등장(플레이)시 승률" 1개 지표** 산출(FR-8.1 최소형) +
   지난 조사(배틀소멸)를 이벤트로 재확인(CardMoved BattleArea→Trash가 다이렉트 어택 직후 발생).
3. 무회귀: run-tests 전체 green + RuleAudit 0. 신규 계약 테스트(L4-001): 레벨 필터·스키마 필드·
   OFF 무출력·turn/phase 스탬프.

## 3. 명시적 후속 (v1 비범위)
- cause 정밀화(SecurityBattle/BattleLose/EffectDelete 구분) — 소멸 원인별 밸런스 분석이 필요해질 때.
- ITraceSink→TRACE 브릿지, 좌석 중계용 가시성 필터(이건 프로토콜 이벤트 스트림의 몫 — 혼동 금지),
  semantic `tags` 채우기(FR-7.5), MatchStateMutationSink 직결(현 깔때기로 커버되지 않는 뮤테이션이
  발견될 때만).
