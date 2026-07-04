# RL 학습환경 개발 로드맵

- 작성일: 2026-07-03
- 기준: [rl_training_environment_design.md](rl_training_environment_design.md) **v0.2** (아키텍처 확정 8건) + 설계 리뷰에서 확인된 계약 공백 3건·전략 공백 2건을 마일스톤에 배치.
- 최종 목표: ① **내부망 상호학습 환경**(여러 모델이 대전하며 학습) ② **Digimon AI Arena**(여러 사람의 AI가 레이팅 경쟁).

## 현재 위치
- 엔진 RL 인터페이스는 **완성·검증됨**: `HeadlessRlEnvironment`(reset/step/observe), `StepByFactoredIndexAsync`, factored 마스크, 결정론 — `G13-003` 실카드 셀프플레이 스모크 green.
- 설계 v0.2 확정: seat 단위 매치 프로토콜(학습·아레나 공용), transport-agnostic, 정보집합 관측(룰 충실), 카드-ID 임베딩, 지식추적 레이어, 어댑터 별도 어셈블리.
- **남은 것 = 연결 작업**: 어댑터 분리 → 관측 확장 → seat 프로토콜 브리지 → 학습 → 내부망 → 아레나.
- 병행 의존: **카드 포팅**(기존 [development_roadmap.md](development_roadmap.md) Phase 1~3)은 학습 "콘텐츠" 트랙. RL 트랙(M1~M4)은 스타터덱+현재 포팅분으로 독립 진행 가능하나, "효과를 활용하는 정책"의 품질은 포팅 진척에 의존.

---

## M1 — RL 어댑터 어셈블리 분리 (설계 §4) ✅ 완료 (2026-07-04)
**목표**: 엔진/어댑터 경계를 물리적으로 고정 — 이후 모든 RL 확장이 "엔진 코어 변경"이 아니게 됨.
- 신규 `src/HeadlessDCGO.Rl/HeadlessDCGO.Rl.csproj`(엔진 ProjectReference). 설계 §4 이동 목록대로 RL 파일 이동(`HeadlessRlEnvironment`, `Rl*`, `ObservationEncoder`, `ActionEncoder`, `FactoredActionEncoder`, `HeadlessActionPolicy`, `*EpisodeRunner` 등). **네임스페이스 유지**(using 편집 없이 참조만 추가).
- 경계 누수 정리: `DcgoMatch.Encode*` 3메서드를 Rl 어셈블리로 이동(src 사용처 1곳뿐 — 실측).
- RL 타입 참조 테스트 21개 프로젝트에 ProjectReference 추가.
- **종료조건**: `bash scripts/run-tests.sh` 전체 green(회귀 0) + `RuleAudit` 위반 0.
- **실행 기록**: RL 16파일 + **검증 하네스 8파일**(Scenario/Smoke/Determinism — RL 환경을 구동하는
  스캐폴딩이라 함께 이동, 역참조 폐쇄 확인) = 24파일 `git mv`. `DcgoMatch.Encode*` →
  `DcgoMatchEncodingExtensions`(확장 메서드, 호출 문법 무변경). 참조 추가 = 테스트 **22개**(+하네스
  사용분) + `RuleAudit`. CI에 Rl 빌드 스텝 추가. 게이트: run-tests **312/312 PASS** + RuleAudit 위반 0.

## M2 — 관측 확장: 정보집합 + 카드-ID (설계 §5) ✅ 완료 (2026-07-04)
**목표**: 현재 벡터(BattleArea 스탯만)로는 학습 불가 → **정보집합의 충분통계**로 확장. 룰상 모르는 정보(본인 시큐리티/덱)는 count-only 유지(아레나 안티치트 겸용).
- (엔진 스냅샷, 가산적 3건) ① `HeadlessChoiceState`에 choice 후보 카드 id + GameLoop 배선 ② `DigivolutionStackReader`를 `CardObservationView`에 배선(진화 소재 정체) ③ `CardObservation`에 InstanceId(attack 슬롯 매칭).
- (어댑터) 정보집합 인코딩: 본인 손패/공개존 per-card 정체 + `CardVocabulary`(CardDatabase 기반 카드번호→정수), 본인 시큐리티/덱은 인코딩 단에서 명시 제외. factored 스키마 용량 튜너블 주입 + `Unmapped` 카운트 노출.
- **계약(리뷰 🔴2)**: **관측 슬롯 ↔ 액션 레인 정렬 불변식** 명문화 + 검증 테스트(관측 hand slot i의 cardId == factored PlayCard lane slot i의 대상).
- **종료조건**: 정렬 불변식 등 신규 계약 테스트 green + 기존 무회귀.
- **실행 기록**: InstanceId는 기존재(③ 무작업). 엔진 가산 = `HeadlessChoiceState.CandidateIds`(+컨트롤러
  배선 + GameLoop **비선택자 시점 후보 strip**, count 유지) + `CardObservation.UnderCards`(DigivolutionStackReader
  배선, sourceIds 보유 카드만). 어댑터 = `CardVocabulary`(C#, Python `dcgo_rl.cards`와 canonical 규칙 동일·
  append-only) + `ObservationEncodingOptions.InformationSet` 프리셋(손패/필드 용량=factored 스키마 정렬,
  본인 시큐리티/덱 per-card 인코딩 즉시 실패 가드, choice 후보·attack 슬롯·진화 소재, identityOverflow).
  기본 옵션은 무변경(기존 벡터 무회귀). 게이트: `M2-001.InfoSetObservation.Tests` 11/11(정렬 불변식
  라이브 매치 hand 166·attack 151회 검증) + run-tests **313/313** + RuleAudit 0.

## M3 — 지식 추적 인프라 (설계 §5.1) 🟠 M2 후 착수, 카드 포팅과 병행 배선
**목표**: "효과로 정당하게 알게 된 히든존 카드"(reveal로 본 덱 top, 덱 위에 놓기, 정렬)를 관측에 노출하는 per-player 지식 스토어.
- 스토어 + populate/invalidate: populate에 **공개 대상(owner-only/chooser-only/both) 포함 — public reveal 채널을 별도 메커니즘 없이 여기로 통합**(리뷰 🔴3). invalidate = 셔플/드로우/위치이동. hook = `MatchStateMutationSink` + `RevealAndSelect`.
- 어댑터 인코딩: `deck.top.knownCardId`, `security.slot.knownCardId` 류 — 아는 것만 정체, 모르는 건 count-only.
- **종료조건**: 무효화 규칙 유닛 테스트 green. 실전 배선은 reveal/arrange 계열 카드 포팅 시점에 카드별 테스트와 함께.

## M4 — seat 매치 프로토콜 + stdio 호스트 + 로컬 학습 슬라이스 (설계 §6, §9-B) ✅ 완료 (2026-07-04)
**목표**: "루프가 실제로 학습된다"를 증명하는 완결 수직 슬라이스. 프로토콜은 처음부터 아레나 공용 계약으로.
- **프로토콜 스펙 v1 문서**(선행): 메시지 스키마(JSON-lines), seat claim 핸드셰이크(연결이 좌석 1..N claim), **좌석별 보상 귀속 규칙**(승자 좌석 +1/패자 −1/무 0 — 리뷰 🔴1), **좌석별 이벤트 가시성 필터**, protocol/obs/action schema **version** 명시, 에피소드/seat 순차계약(Mlp↔LSTM 겸용).
- `tools/RlBridgeHost`(stdio 전송) + `tests/` 프로토콜 스모크(합법 0-위반·수렴·결정론·차원 일치·보상 귀속).
- Python `rl/`: `DcgoStdioEnv`(gymnasium, `action_masks()`), 카드-ID 임베딩 features extractor, `train.py`(MaskablePPO, 체크포인트/텐서보드/eval).
- **처리량 벤치마크 게이트(steps/sec) 포함**(리뷰 🟡5) — 직렬화 병목 조기 측정.
- **종료조건**: 스모크 학습 무크래시 + eval 승률이 랜덤(≈50%) 대비 유의미 상승(희박 terminal 보상 감안한 기대치 — 리뷰 🟡6) + steps/sec 측정치 기록 + run-tests green.
- **실행 기록**: 스펙 v1 = [rl_seat_protocol_v1.md](rl_seat_protocol_v1.md)(보상 귀속 🔴1·좌석별 정보집합
  관측·합법성 경계·결정론·describe 포함). 호스트 = `tools/RlBridgeHost`(`SeatMatchHost` 전송 무관 상태기계
  \+ stdio 래퍼). 계약 테스트 = `M4-001.SeatProtocol.Tests` 9/9(결정론 리플레이·보상 귀속·불법 액션
  무변경 거부·레시피 공급 통로·미지원 카드 명시 실패). Python = `rl/dcgo_rl/`(bridge·DcgoSeatEnv·카드-ID
  임베딩 extractor) + train/evaluate. **vocab 해시 C#↔Python 완전 일치 실증**(4206 canonical, sha256).
  게이트 실측: 30k 스텝 무크래시, **eval vs 랜덤 97.5%**(40매치, ST1vST2·좌석 교대),
  **94.6 steps/sec**(4 env DummyVec, JSON+stdio; 단일 env 랜덤 143), run-tests **314/314** + RuleAudit 0.
  벤치마크 판단: L1~L2 규모까지 stdio+JSON으로 충분 — 직렬화 승급은 요구사항 §9 보류 유지.

## M5 — TCP 전송 + 내부망 상호학습 (설계 §9-C) 🟡 목표 ①
**목표**: 같은 메시지 스키마를 TCP/WebSocket로 — 다중 머신 학습기가 각각 접속(1연결=1좌석)해 대전 학습.
- 전송 계층 추가(스키마 무변경), 다중 매치 병렬 호스팅(기존 `HeadlessEpisodeBatchRunner` 패턴 참조).
- **리그 방식을 기본 전략으로**(리뷰 🟡4): 학습기 vs frozen 스냅샷 풀, 주기적 승급/스왑. live-vs-live 동시 학습은 실험 옵션(independent PPO 비정상성 리스크).
- 스케일 경로 평가: 바이너리 인코딩 승급 또는 **C# in-process 롤아웃 + ONNX 정책**(직렬화 제거) — M4 벤치마크 수치로 판단.
- **종료조건**: 내부망 2대+ 학습기가 각각 접속해 리그 학습이 지속 동작, 스냅샷 승급 사이클 검증.

## M6 — Digimon AI Arena (설계 §9-D) 🟢 목표 ② 최종
**목표**: 여러 사람의 AI가 원격 접속해 레이팅 경쟁하는 서비스. 매치 코어는 M4~M5 프로토콜 그대로.
- 아레나 서비스 레이어(엔진 저장소 밖): 매치메이커, 레이팅(Elo/Glicko/TrueSkill 선정), 제출/접속 관리(원격 접속 = 서버가 제출 코드 미실행, 보안 핵심 이점), 시간제한/타임아웃 패배(`MatchResult` reason 확장), 리플레이 저장(trace/fingerprint 포맷 승급) + 뷰어, 덱 규정(스타터 고정 → 덱빌딩, 카드 포팅 연동).
- 전제: M4 프로토콜 버저닝이 서드파티 AI 저자와의 계약으로 안정화.
- **종료조건**: 외부 AI 2개가 원격 접속해 레이팅 매치를 완주하고 레이팅이 갱신됨.

---

## 병행 트랙 / 의존 관계
```
M1 ─▶ M2 ─▶ M4 ─▶ M5 ─▶ M6
        └▶ M3 (병행; 실배선은 카드 포팅 진척에 연동)
카드 포팅(기존 로드맵 Phase 1~3) ──────▶ 학습 콘텐츠 품질·M3 실배선·M6 덱 규정
보상 shaping(G3.5-RL-D) — M4 스모크 이후 별도 goal
```

## 교차 원칙 (전 마일스톤 공통)
- **엔진 코어 터치 최소화**: M2·M3에 명시된 스냅샷/지식 확장만. 그 외 전부 `HeadlessDCGO.Rl` 어셈블리.
- **매 마일스톤 게이트**: `bash scripts/run-tests.sh` 전체 green + `RuleAudit` 0.
- **계약 우선**: 프로토콜/스키마는 버전 명시(handshake), 변경 시 버전 증가. 보상 귀속·슬롯 정렬 같은 계약은 테스트로 고정.
- **안티치트 = 구조로**: 정보집합 관측 + 합법성 경계 + 좌석별 이벤트 필터 — 서버가 애초에 히든 정보를 안 보냄.

## 권장 즉시 다음 액션
**M1(어댑터 분리)** — 기계적이고 회귀 리스크 낮으며 이후 모든 작업의 전제. 병행으로 **M4의 프로토콜 스펙 v1 문서**를 먼저 작성해 계약 공백 3건(보상 귀속·정렬 불변식·지식-reveal 통합)을 구현 전에 고정하는 것도 유효.
