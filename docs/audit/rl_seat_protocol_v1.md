# seat 매치 프로토콜 스펙 v1

- 작성일: 2026-07-04 (M4 선행 산출물 — [rl_development_roadmap.md](rl_development_roadmap.md) M4)
- 근거 설계: [rl_training_environment_design.md](rl_training_environment_design.md) §6 (seat 단위·transport-agnostic),
  [internal_rl_training_environment_dev_design.md](internal_rl_training_environment_dev_design.md) §7.
- 지위: **학습 브리지와 아레나 매치 서버가 공유하는 계약.** 메시지 스키마는 전송(stdio/TCP/WebSocket)과
  독립이며, 변경 시 `protocol` 버전을 올린다.

## 1. 전송 / 프레이밍
- **JSON-lines**: 메시지 1건 = JSON 오브젝트 1개 = 1행(`\n` 종결, UTF-8, 행 내 개행 금지).
- v1 전송 = **stdio**(호스트 프로세스의 stdin/stdout). TCP/WebSocket은 M5에서 같은 스키마로 추가.
- 모든 메시지에 `type` 필드. 알 수 없는 `type`/필드 → `error` 응답(호스트 상태 무변경).

## 2. 세션 수립
```
C→H  {"type":"hello","protocol":1}
H→C  {"type":"welcome","protocol":1,
      "obsSchemaVersion":"infoset-v1","obsSize":<int>,"obsSchemaHash":"<sha256(피처명 결합)>",
      "actionSchemaVersion":"factored-v1","actionSize":<int>,
      "schema":{"maxHand":16,"maxField":16,"maxChoice":16},
      "vocabVersion":"v1","vocabSize":<int>,"vocabHash":"<sha256('번호:id' 오름차순 결합)>"}
C→H  {"type":"claim","seats":[1,2]}
H→C  {"type":"claimed","seats":[1,2]}
```
- (선택) `{"type":"describe"}` → `{"type":"schema","obsSchemaHash":"...","features":[<피처명>...]}` —
  트레이너가 `cardId` 채널 인덱스(임베딩 대상)를 식별하는 용도. `obsSchemaHash` = sha256(피처명 `\n` 결합).
- **버전 검증은 클라이언트 의무**: `obsSchemaHash`/`vocabHash`가 자기 것과 다르면 즉시 종료(조용한 진행 금지).
- **좌석 점유는 데이터**(설계 §6.1): 연결이 좌석 1..N개를 claim. stdio 셀프플레이 = 1연결 2좌석,
  이종 대전/아레나 = 1연결 1좌석(전송만 다르고 메시지는 동일).

## 3. 매치 루프
```
C→H  {"type":"reset","seed":<int>,"maxSteps":2000,
      "decks":{"1":<Recipe|"starter:ST1">,"2":<Recipe|"starter:ST2">}}
H→C  {"type":"turn","matchId":"m-<seed>-<n>","seat":<1|2>,"stepIndex":<long>,
      "observation":[<double>...obsSize],"actionMask":[<0|1>...actionSize],"legalCount":<int>}
C→H  {"type":"action","seat":<1|2>,"index":<int>}
      … turn/action 반복 …
H→C  {"type":"result","matchId":"...","rewards":{"1":1.0,"2":-1.0},
      "winnerSeat":<1|2|null>,"isDraw":<bool>,"reason":"...","steps":<int>,"turns":<int>}
```
- **Recipe** = 내부 표준 레시피 JSON(dev design §3.1: `main`/`digitama`의 `{card,count}` 목록,
  canonical 카드번호). `"starter:STn"` 은 엔진 내장 스타터 덱 축약.
- **관측 = 좌석 시점 정보집합**(설계 §5): `turn.observation`은 해당 좌석 perspective 스냅샷을
  InformationSet 옵션으로 인코딩한 것. 다른 좌석의 관측은 절대 전달되지 않는다(아레나 안티치트 구조).
- **마스크 = 좌석별**: 해당 좌석의 합법행동만 factored 인덱스에 1. `turn`은 **행동할 좌석에게만** 발행.
- **순차계약**(설계 §6.3): 한 좌석의 메시지 흐름은 자기 순서를 유지한다. `turn`에는 반드시 그
  `turn`에 대한 `action`(같은 seat)으로 응답한다. 에피소드 경계는 `result`로 명시되며 상태가 에피소드를
  넘어 이월되지 않는다(LSTM hidden 리셋 조건).

## 4. 보상 귀속 (계약 — 리뷰 🔴1)
- terminal 시 호스트가 계산해 `result.rewards`로 확정한다: **승자 좌석 +1.0 / 패자 −1.0 / 무승부·스텝캡 0.0**.
- 중간 보상 없음(C-4 순수 승패). 셰이핑은 트레이너단 변환의 몫 — 프로토콜은 결과 보상만 나른다.

## 5. 합법성 경계 / 오류
- `action.index`가 현재 `turn.actionMask`에서 0이거나 범위 밖 → 호스트는 **상태 무변경**으로
  `{"type":"error","code":"illegal_action",...}` 응답 후 **같은 `turn`을 재발행**한다(1차 방어선).
- 프로토콜 위반(순서 어긋난 seat, claim 안 된 좌석의 action, reset 전 action) → `error` (`code`:
  `protocol_violation`), 상태 무변경.
- 호스트 내부 오류 → `error` (`code`:`internal`) 후 해당 매치는 `result`(reason=`aborted`, 보상 0)로 종결.

## 6. 결정론 (NFR-3)
- 같은 `seed` + 같은 `decks` + 같은 action 인덱스 열 = 같은 관측 열·같은 `result`.
- `matchId`는 진단용 식별자일 뿐 상태에 영향 없음.

## 7. 로그 (L0 최소치)
- 호스트는 매치당 `result` 내용 + seed + 덱 이름을 **RESULT 요약 1줄(JSONL)**로 stderr 또는
  `--result-log <path>`에 기록한다(FR-7 본공사(L4) 전의 브리지단 선반영 — dev design §11 L0).

## 8. v1 명시적 비범위 (후속 버전에서)
- 이벤트 스트림 중계(좌석별 가시성 필터 — REPLAY/ANALYSIS 레벨, L4와 함께).
- 다중 동시 매치 / 연결 재접속 / 타임아웃 패배(M5·M6), 항복 액션, 관전 좌석.
- 바이너리 인코딩(벤치마크 임계 도달 시 — 요구사항 §9 보류 그대로).
