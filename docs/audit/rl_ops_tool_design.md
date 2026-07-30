# RL 운영 도구 설계 — **보류** (요구사항 정의서 확정 전 착수 금지)

> 정본은 `rl_ops_tool_requirements.md`(요구사항 정의서)다. 이 문서는 성급하게 작성된
> 초안으로, "실측된 현황" 절만 사실 기록으로 유효하고 마일스톤·구조는 전부 재검토 대상.

사용자 요구(2026-07-30): **① 판 들여다보기 ② 학습 운영 ③ 런 이력·비교 ④ 결함 감시**,
형태 = **명령 실행 가능한 웹 서버**. 기존 `rl/dashboard`는 "원하는 게 거의 없음" 판정 —
골격(stdlib http + 화이트리스트 런처)만 참고하고 표면은 새로 설계한다.

## 실측된 현황 (2026-07-30)

- `rl/dashboard/server.py`(323줄): runs/league/results API + replay/train 서브프로세스 런처.
  인터프리터 경로 `rl/.venv`는 실재(uv 관리). **표면이 요구와 불일치.**
- `replay.py`: 구세대 FactoredActionSchema 기준 — 현행 238레인 계약과 어긋남. **정비 필요.**
- 호스트(`RlBridgeHost/Program.cs`)는 `--log-level`/`--event-log`를 **파싱하지 않음** —
  train.py가 넘기는 두 플래그는 죽은 매개변수. 판 단위 이벤트 로그는 미구현.
- PlayLog: AS-IS가 판 내내 `PlayLog.OnAddLog`로 사람용 로그를 발화, 헤드리스에서도
  `_logList`에 축적됨(GManager.Awake→playLog.Init 배선 실측). **아무도 내보내지 않음.**
  캡 11,000자(AS-IS UI 제약) — 캡 전 구독 수집이 정본.
- 결함 표면: host stderr에 `[abort] tick=… <스택>` + `[coroutine-exception] …`(삼킴) 실동,
  result 메시지엔 사유 문자열. `DCGO_HOST_STDERR`로 파일 추기 가능(bridge.py).

## 마일스톤

### M1 — 런 산출물 표준화 (서버 없이도 가치, 본학습 안전망) ✅ 2026-07-30
1. **호스트 플레이 로그 캡처**: `--play-log` 시 매치별 `PlayLog.OnAddLog` 구독 수집(캡 없음),
   result-log jsonl 라인에 `playLog` 배열로 동봉. teardown 시 구독 해제(정적 이벤트 누수 계보).
2. **train.py 체크포인트**: SB3 CheckpointCallback → `out/checkpoints/` (l0-main-3 전량 유실 재발 방지).
3. **train.py 메타 2단계**: 시작 시 `meta.json` 선기록(config·git sha·시드·시작시각·상태=running),
   종료 시 지표·census(사유 분포+swallowed) 병합. 결함 엔진산 정책 오식별 방지(git sha가 판정 근거).
4. **호스트 stderr 상시 수집**: train.py가 `DCGO_HOST_STDERR=out/host-stderr.log`를 기본 설정.

### M2 — 서버 (명령 실행 표면)
- 새 `rl/opsd/` (stdlib 전용 유지): 런 조회/비교 API + 런처(uv run 경유, 화이트리스트)
  + 정지 + 실시간 진행(로그 tail·RSS) + 결함 census API.
- 기존 server.py의 보안 골격(127.0.0.1·경로 트래버설 차단·인자 화이트리스트) 승계.

### M3 — 판 들여다보기
- M1의 playLog + result를 판 단위로 서빙, replay.py를 현행 스키마로 정비해 argmax/정책 대전
  재생을 서버에서 실행. 상태 채널 스냅샷(관측 디코딩) 동봉.

### M4 — UI (탭 4: 운영 / 런 비교 / 판 뷰어 / 결함)

## 원칙
- stdlib 전용(서버), uv 경유 실행, 127.0.0.1 바인드.
- census는 "덮어쓰기"가 아니라 런 산출물로 영속 — 결함 발견물은 로그와 함께 증발하면 안 된다.
