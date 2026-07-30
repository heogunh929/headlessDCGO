# RL 관리 툴 — 1차 설계서 (v1, 2026-07-30)

정본 요구사항: `rl_ops_tool_requirements.md` v1.2 (전 항목 확정). 이 문서는 그 요구를 구조로
옮긴 것이며, 요구와 충돌하면 요구가 이긴다. 구(舊) `rl_ops_tool_design.md`는 폐기.

## 1. 컴포넌트 구성 (머신 무의존, 역할 기준)

```
[브라우저]──http──┐                      ┌──[에이전트(러너 CLI/스킬)]──ws──┐
                  ▼                      ▼                                │
            ┌──────────── opsd (aiohttp 단일 프로세스) ────────────┐      │
            │ 운영자 표면 │ 공유 표면 │ 아레나 표면(ws+페이지)      │◄─────┘
            │ sqlite DB(참가자·키·덱·시즌·레이팅·매치메타·설정·임계) │
            └───────────────┬────────────────────────────────────┘
                            │ http(공유 토큰)
            ┌───────────────▼───────────────┐
            │ runner (엔진 PC 상주, 1개↑)     │  잡 실행(uv run 화이트리스트)·정지(SIGINT)
            │ RSS/진행 감시·워커 상한 강제     │  runs/ 아티팩트 서빙(메타·판로그·stderr census)
            └───────┬───────────────────────┘
                    │ 서브프로세스(현행) / TCP 브리지(DGX 분리 시)
            ┌───────▼────────┐     ┌────────────────┐
            │ RlBridgeHost ×N │ ◄── │ 학습기 train.py │ (현 PC → DGX 이전 예정)
            └────────────────┘     └────────────────┘
```

- **opsd**: aiohttp 하나로 HTTP(페이지·API)+웹소켓(아레나) 동시 서빙. 배치 자유(§7).
- **runner**: 엔진 PC 상주 데몬. opsd에 자기등록(주소+토큰). 아레나 판도 runner가
  RlBridgeHost를 세워 돌린다 — 학습과 아레나가 같은 실행 표면을 공유.
- **뷰어**: opsd가 서빙하는 정적 페이지(바닐라 JS). `.jsonl.gz`를 API로 받아 클라이언트에서 해석.
- **SDK/러너 CLI**: 파이썬 패키지 1개(`dcgo-arena`) — 프로토콜 계층(SDK) + 참조 러너.
  OpenClaw 스킬은 러너 CLI를 감싸는 스킬 문서(Hermes는 OpenClaw 호환으로 커버).

## 2. 판 로그 스키마 (`<매치ID>.jsonl.gz`) — ①·⑤·아레나 공용

```jsonl
{"v":1,"type":"header","matchId":"...","ts":"KST ISO8601","engineSha":"...","seed":N,
 "players":[{"seat":1,"kind":"policy|random|arena","name":"...","deck":{"name":"...","cards":[...]}},...],
 "recordPolicy":"all|sample|accident"}
{"type":"step","i":0,"tick":N,"seat":1,"turn":1,"phase":"Main",
 "decision":{"kind":"MainPhase","legal":[...],"chosen":17},
 "state":{...전지적 스냅샷...}}
{"type":"event","afterStep":0,"cat":"phase|effect|battle|select","text":"PlayLog 원문"}
{"type":"result","reason":"game_end","winnerSeat":1,"steps":N,"turns":N,"census":{"swallowed":[...]}}
```

- **state**(스텝 앵커 전지적 스냅샷): 양측 `{memory, deckCount, hand:[카드], security:[카드],
  trash:[카드], breeding:[퍼머넌트], field:[{cards:[스택], level, dp, suspended, links}]}` +
  `turn/phase/activeSeat`. 카드 = `{id, name}` (1차 텍스트).
- 이 스키마가 **LLM 상태 표현의 정본** — 아레나 ws 메시지의 `state`도 동일 구조(단, 상대
  hand/security/deck 내용은 카드 수로 대체하는 **관측 필터**를 서버가 적용).
- 생성 위치: **RlBridgeHost 내 MatchRecorder** — 메모리 버퍼, 종료 시 모드 판정 후 gz 기록.
  PlayLog는 `OnAddLog` 구독(teardown 해제), 상태는 GManager 게임 상태 직렬화 덤퍼.

## 3. 프로토콜 3종

**3.1 runner API** (opsd↔runner, http+공유 토큰):
`POST /jobs {script(화이트리스트), args, record_mode}` · `POST /jobs/{id}/stop|kill` ·
`GET /jobs/{id}/status`(진행·RSS·census 실황) · `GET /runs`, `GET /runs/{run}/meta|matches|matches/{id}` ·
`GET/PUT /config`(워커 상한 등 — opsd 대시보드가 원격 관리) · 아레나용 `POST /arena/match`.

**3.2 아레나 ws** (에이전트↔opsd, `wss /arena?key=...`):
서버→ `queued | match_start{matchId, seat, yourDeck:[전문], opponent:{name,rating}} |
your_turn{state, legalActions, deadline} | match_end{result, ratingDelta} | error` ·
클라→ `enqueue{deckId?} | create_room{deckId?} | join_room{code, deckId?} | action{index} | resign`
(서버→ `room_created{code}` 추가 — 룸 매치, 2026-07-30 개정으로 challenge 대체).
착수당 타임아웃·끊김 유예(N초 재접속)는 opsd가 판정, 초과=타임아웃 패.
legalActions는 마스크가 아닌 **서술형 목록**(예: `{index, desc:"hand[3] ST1-07 플레이"}`) —
LLM 가독이 1차 요건.

**3.2.5 매치 브로커 좌석 라우팅**: 판 1개 = 엔진 호스트 1프로세스, 브로커(opsd)가 좌석별
결정 지점을 소비자에게 라우팅 — RL 좌석은 숫자 관측+마스크(기존 인코더), LLM 좌석은 텍스트
상태+서술형 합법 수. RL/LLM/랜덤 어느 조합이든 동일 경로. RL 참가자도 API 키 참가자
(kind=policy, 스냅샷별 등록 가능). 덱은 이원화: 학습 덱(레시피, 파이프라인 소유) ↔ 아레나 덱
(서버 정본) — 포맷·검증기 공용, 연결은 스냅샷 참가자 등록 게이트 하나.

**3.3 학습 브리지**: 현행 stdio JSON 라인 프로토콜을 그대로 **TCP로도** 여는 리스너를
RlBridgeHost에 추가(`--listen 포트`). DcgoSeatEnv에 `host="tcp://엔진PC:포트"` 옵션 —
DGX 학습기가 LAN 너머로 동일 프로토콜 사용. 워커 배분·수명은 runner가 관리.

## 4. DB (sqlite, opsd 소유)

`participants(id, handle, key_hash, status[pending|active|banned], created)` ·
`decks(id, owner, name, cards_json, active, valid, created)` ·
`matches(id, season, p1, p2, deck1_snapshot, deck2_snapshot, result, reason, rating_delta, log_path, ts)` —
**영구, 삭제 없음** · `ratings(participant, season, elo, games)` · `seasons(id, name, state)` ·
`thresholds(id, metric, limit, action[notify|notify+stop], enabled)` ·
`settings(key, value)` — 카드 풀, 보존 규칙, 자동승인 온오프, 워커 상한 기본값, SMTP.
런 메타는 DB가 아니라 `runs/<run>/meta.json`이 정본(runner 서빙, opsd는 캐시).

**sqlite 충분성 (2026-07-30 사용자 동의)**: 착수는 DB 미경유(ws 메모리+판 로그 파일),
DB 쓰기는 판 종료 1건+가입·덱 등록뿐이고 동시 판은 엔진 워커 상한이 제한 — 부하가 분당
몇 건 수준. 단 **WAL 모드 필수** + **DB 접근은 얇은 단일 계층으로 격리**(opsd 다중 프로세스가
필요해지는 규모가 오면 Postgres 이전 비용 최소화).

## 5. 보안 표면 구현 (§7 3분류)

- 운영자: `X-Ops-Token` 헤더(공유 토큰) — 대시보드·runner API·관리 페이지 전부.
- 아레나: API 키(발급 시 1회 표시, DB엔 해시). ws 접속·덱 API·본인 이력 페이지.
- 공유: 무인증 읽기 전용 — 가입 신청 `POST /signup{handle}`(rate limit)과 순위표
  `GET /rankings`(사전 생성 JSON)만. 판 로그 접근 경로 없음.

## 6. 학습 운영 결선 (②③④)

- train.py: `--checkpoint-every/--keep N`(SB3 콜백) · SIGINT→체크포인트 저장 후 종료 ·
  meta.json 2단계(시작: config·git sha·시드·상태=running / 종료: 지표·census 병합) ·
  stderr는 `runs/<run>/host-stderr.log`, census 파서가 abort/swallowed 집계.
- 대시보드 탭 5: **운영**(런처+실황) / **런 비교**(지표 8종 표+곡선) / **판 뷰어** /
  **결함**(census·임계 규칙 관리) / **아레나 관리**(가입 승인·카드 풀·시즌·보존 규칙).
- 알림: 임계 평가는 opsd 주기 폴링(runner status) → 화면 배지 + smtplib 메일, 규칙별
  자동 중단은 runner stop 호출.

## 7. 구현 마일스톤 (각각 독립 가치·게이트)

| M | 내용 | 게이트 |
|---|---|---|
| M1 ✅ | MatchRecorder(호스트) + train.py 결선(체크포인트·메타·census) | **통과 2026-07-30 [실측]**: 판로그 gz(sample:5+사고판)·메타 2단계(done/interrupted)·체크포인트 5개 보존·SIGTERM graceful(1525스텝 중단→정책+메타 확보)·stderr census. 구현 노트: 카드는 id만 기록(이름은 뷰어가 cards.json), 센티널 값 원기록, graceful은 명시적 signal 핸들러+저장 후 os._exit(close 블록 실측 회피) |
| M2 ✅ | runner 데몬 + opsd 골격 | **통과 2026-07-30 [실측]**: opsd 프록시 경유 기동(상한 거부 포함)→감시(RSS·진행)→graceful 정지(interrupted+정책 확보). runner=stdlib 전용(uv 래퍼 우회로 SIGTERM 직행), opsd=aiohttp+임계 평가 루프(배지·SMTP), 대시보드 3탭 |
| M3 ✅ | 판 뷰어(와이어프레임 구현) | **데이터 경로 통과 2026-07-30 [실측]**(gz 서빙·파싱·97스텝 판), UI는 브라우저 육안 검증 대기. 요약/Diff/JSON 탭·이벤트 검색/칩·타임라인 턴마커·배속 재생·숨김/디버그 토글 구현. 카드명 사전 부재(cards.json엔 번호·타입만) → ID 표시 1차, 이름 추출 후속. 상대 시트 180° 회전은 가독성 사유로 상하 대면 배치로 대체(원 와이어프레임과 차이 — 확인 필요) |
| M4 ✅ | 아레나 골격: DB·가입/승인·키·덱(빌더+검증기)·ws 대전·Elo/시즌·순위 공개 페이지 | **통과 2026-07-30 [실측]**: 러너 CLI 2개 래더 한 판 완주(Elo ±16)·지정 도전 완주(비대칭 Elo ±17.5)·타임아웃 몰수패·덱 검증 4종(50장·MaxCount·풀·실존) 거부·본인 판 로그 열람+타키 401·공개 순위/가입 무인증. 구현 노트: ① 호스트 `--describe`(턴에 상태 스냅샷+서술형 합법 수, 관측 필터는 opsd) + reset에 서버 matchId ② 패널 ask 좌석 귀속=후보 카드 소유자(멀리건이 P1로 쏠리던 것 교정) ③ runner 릴레이=stdlib 롱폴(/arena/match·turn·act·end, arena_cap 기본 2) ④ 검증기 데이터원=runner /cards-meta(cards.json+자산 MaxCountInDeck) ⑤ 대전 성립=래더+룸 매치(코드 발급·참가 — challenge 대체 개정) ⑥ UI 육안 검증 대기(rankings/arena/관리 탭/뷰어 arena 모드) |
| M5 | SDK 정리 + 참조 러너 공개품질 + OpenClaw 스킬 · TCP 브리지(`--listen`) | DGX 없는 상태로 TCP 루프백 학습 1런 완주 |

순서 근거: M1은 본학습 안전망이라 최우선(서버 없이도 가치). M4까지가 정의서 1차 범위,
M5의 TCP 브리지는 DGX 도착 전 루프백으로 선검증.

**배치 방침 (2026-07-30 사용자 확정)**: **환경 구축이 완료되기 전까지 Spark 작업은 하지 않고,
현 PC 한 대에 opsd+runner+엔진을 전부 올려 테스트로 진행**한다. 머신 무의존 설계 덕에
분리는 배치 변경만으로 가능 — DGX 이전은 M1~M5 완료 후의 별도 단계.

## 8. 리스크

- R-a: 스냅샷 덤퍼가 AS-IS 상태를 잘못 읽으면 뷰어가 거짓말 — 덤퍼는 reflection 판독 전용,
  기존 채널 인코더와 교차 대조 테스트(카드 수·메모리 일치)로 게이트.
- R-b: 아레나 동시 판 수 = 엔진 PC 워커 상한과 경쟁 — runner의 잡 우선순위(학습>아레나 또는
  관리자 설정)로 중재, 1차는 "학습 중 아레나 판 수 제한" 설정 하나로 시작.
- R-c: aiohttp 의존 1개 — uv로 고정(lock), 정의서 §8 예외 등재 완료.
