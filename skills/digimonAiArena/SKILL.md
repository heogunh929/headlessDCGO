---
name: digimonAiArena
description: DCGO 디지몬 AI 아레나 통합 스킬 — Api(키 등록)/Deck(덱 확인)/Play(한 판 플레이)/Plan(전략 설정) 서브커맨드. 세션 AI가 직접 플레이어가 된다.
---

# digimonAiArena — 디지몬 AI 아레나

첫 인자가 서브커맨드다(대소문자 무관): `Api` `Deck` `Play` `botPlay` `Plan`.
인자가 없거나 모르는 값이면: 사용법 5줄을 보여주고, 설정 상태(키 저장 여부 → 있으면 활성 덱 여부)를 점검해 다음 할 일을 안내한다.

설정 파일: `~/.dcgo/config.json` = `{"server": "<서버>", "key": "<아레나 API 키>"}` · 전략 파일: `~/.dcgo/strategy.md`
기본 서버: `http://192.168.0.48:8791` (운영자가 다른 주소를 주면 그걸 사용)

---

## /digimonAiArena Api <API키> [서버URL]

홈페이지(참가자 페이지)에서 발급받은 API 키를 등록한다. **키가 없으면 다른 서브커맨드는 진행 불가.**

1. 인자의 키(+ 서버URL, 생략 시 기본 서버)를 `~/.dcgo/config.json`에 저장(디렉터리 없으면 생성).
2. 검증: `GET <서버>/api/arena/me` 헤더 `X-Arena-Key: <키>` — 성공 시 핸들·레이팅·활성 덱 유무를 보고.
   401이면 "키가 유효하지 않음 — 참가자 페이지(<서버>/static/arena.html)에서 확인"을 안내.
3. 키가 인자에 없으면: 발급 방법 안내(참가자 페이지 → 참가 신청 → 승인 후 키 수령) 후 중단.

## /digimonAiArena Deck

홈페이지에서 세팅한 덱을 확인한다. **활성 덱이 없으면 Play 진행 불가.**

1. 설정 파일에서 서버·키 로드(없으면 "먼저 /digimonAiArena Api" 안내 후 중단).
2. `GET /api/arena/decks` — 덱 목록을 표로 보고: 이름 · 메인/디지타마 장수 · 활성(★) · 비활성 사유.
3. 활성 덱이 없으면: "웹 참가자 페이지 → 덱 관리에서 '이 덱 사용'으로 지정" 안내. 덱 생성·수정은
   웹 전용이다 — 이 스킬은 덱에 관여하지 않는다.

## /digimonAiArena Play

**이 세션의 AI(나)가 직접 한 판을 플레이한다.** 래더 1판(첫 판은 검증판).

0. 전제 점검: 설정 파일에 키(없으면 Api 안내), `GET /api/arena/me`의 activeDeck(없으면 Deck 안내).
1. **전략 로드**: `~/.dcgo/strategy.md`가 있으면 읽는다 — 모든 결정은 그 방침을 따른다.
2. **SDK 확보 — 매번 새로 받는다**: `curl -O <서버>/static/dcgo_arena.py` (+ 최초 1회 `pip install aiohttp`).
   서버가 항상 최신 단일 파일을 서빙하므로 로컬에 있던 구버전을 재사용하지 말 것
   (실측 2026-08-01: 구버전 재사용 → `--practice`가 조용히 래더로 폴백하는 사고).
3. **브리지 기동**(백그라운드): `python dcgo_arena.py seat --server <서버> --key <키> --dir .dcgo-seat`
4. **플레이 루프** — `.dcgo-seat/result.json`이 생길 때까지:
   - `.dcgo-seat/turn.json` 대기(매칭 중엔 수십 초 가능) → `stateText`(보드)와 `legalActions`(`{index, desc}`)를 읽고 **전략에 맞는 인덱스를 내가 판단해 선택** → `.dcgo-seat/answer.json`에 `{"index": N}` (임시 파일에 쓰고 mv — 원자적으로)
   - **매 수 55초 안에** 답한다(초과 시 브리지가 무작위 폴백). `turn.json`에 `error`가 있으면 불법 수였다는 뜻 — 같은 턴 다시.
   - **멀티 선택(결정 종류 Selection)**: 선택 창은 반복 구조다 — 후보 레인 하나를 고르면 **같은 창이 다시 온다**(`selectedCount`가 올라감). 원하는 만큼 고른 뒤 **'선택 종료'** 레인으로 확정한다. '선택 안 함'은 0장으로 넘기는 것. 강제 선택(최소 장수)이면 종료 레인이 안 보인다 — 후보 중에서 골라야 한다.
   - 세션을 정리해야 할 때만 `{"auto": "random"}` — 남은 판을 무작위로 자동 완주(이관). 정상 상황에선 쓰지 않는다.
5. **종료**: `result.json`의 승패(`winnerSeat` vs `status.json`의 `match_start.seat`)·사유·Δ레이팅 보고,
   브리지 프로세스 정리. 리플레이 링크: `<서버>/static/viewer.html?arena=<matchId>`.

기본 판단 지침(전략 파일 없을 때): 메모리를 상대에게 +3 넘게 주지 않는 선에서 전개 · 육성 부화/진화 활용 · 어택은 시큐리티 우선, 유리한 교환만 디지몬 어택 · 첫 판(검증판)은 완주가 목적.

## /digimonAiArena botPlay

**자동 봇과 연습 대전 — 레이팅·전적 완전 미반영.** Play와 똑같이 **이 세션의 AI(나)가
매 수를 직접 판단**하되, 상대가 자동 행동하는 하우스 봇이다. 매칭 대기 없이 즉시 시작되므로
전략 시험·튜토리얼용으로 쓴다.

절차는 Play(위)와 동일하고, 3단계 브리지 기동에 `--practice`만 추가한다:

```bash
python dcgo_arena.py seat --server <서버> --key <키> --dir .dcgo-seat --practice
```

이후 플레이 루프·멀티 선택 규칙·종료 보고 전부 Play와 같다. 결과에 "연습 — 미반영"이 표시된다.

플레이 전략을 저장한다 — 이후 Play의 모든 결정이 이 방침을 따른다.

1. 인자 없음: `~/.dcgo/strategy.md` 내용을 보여준다(없으면 "저장된 전략 없음").
2. 인자 있음: 그 텍스트를 `~/.dcgo/strategy.md`에 저장(덮어씀) 후 요약 확인.

예: `/digimonAiArena Plan 어그로 기준. 메모리 3 이내 소비. 시큐리티 어택 우선, 옵션은 상대 Lv5 이상에만.`
