# 단계 4 · RL 인터페이스 설계 — 학습 루프 실동

2026-07-29 수립. 전제: 단계 2·3 게이트 통과(자기대전 seam + 결정성 다이제스트 96/96).

## 기존 자산 실측 (4.5의 대상)

| 자산 | 위치 | 재사용 판정 |
|---|---|---|
| **seat 매치 프로토콜 v1** | `docs/audit/rl_seat_protocol_v1.md` | **그대로 계약으로 채택.** JSON-lines stdio·hello/welcome(스키마 해시)·claim·reset/turn/action/result·infoset 관측·factored 마스크·보상 ±1·결정론 NFR-3 — 전부 현 엔진과 정합 |
| 구 RL 런타임 (ObservationEncoder·FactoredActionEncoder·CardVocabulary·SeatMatchHost 등 ~30파일) | 백업 `substrate/HeadlessDCGO.Rl`·`RlBridgeHost` | **구현 재사용 불가** — 구 엔진 API에 결박. 개념·스키마 구조만 이식, AS-IS 타입으로 재작성 (old-TO-BE 참조 금지 원칙과도 정합: 계약 문서가 정본, 코드가 아님) |
| 트레이너(python) | **미발견** | 브리지(M4)까지 만들어지고 트레이너는 없는 것으로 보임 — **사용자 확인 항목**: 별도 저장소 존재? 신규 작성? |
| RL 설계 문서군 9건 | docs/audit/rl_*·internal_rl_* | 프로토콜의 근거 설계로 참조(관측 InformationSet·factored 헤드 정의 등) |

## 이 엔진이 강제하는 제약 [실측]

1. **동시 매치 = 프로세스 단위.** 엔진 상태가 프로세스-전역 싱글턴(`GManager.instance` 등)이라
   한 프로세스에 한 매치. 벡터화(4.4)는 **프로세스 6개**(물리 코어 6, RL 워커=6 규약과 일치)로 한다.
   프로세스 내 순차 매치는 teardown이 보증(시드1 단독 재현 일치로 실증).
2. **결정 지점 seam은 완성돼 있다.** 오늘까지의 선택 채널 census가 곧 행동 표면의 전수다:
   메인페이즈 행동 5종(QueueMainPhaseAction) + 선택 질문 12사이트(SetXxx) + 클릭형(hatch/이동/패널/커맨드).
   RL 브리지는 **`VirtualPlayer` 서브클래스**다 — RandomVirtualPlayer가 rng로 답하던 자리를
   프로토콜 왕복으로 답한다. 새 seam을 만들지 않는다.
3. **결정론 수단 확보**: `MatchSeed.TryPin` + 트래젝토리 다이제스트(`--digest`) → NFR-3
   ("같은 seed+같은 action 열 = 같은 관측 열") 검증기가 이미 있다.

## 4.1 관측 — infoset-v1

좌석 시점 정보집합만(상대 손패·시큐리티 내용 미포함 — 프로토콜의 안티치트 구조 그대로):
- 자기 손패 cardId×maxHand · 양측 필드(탑 cardId·레벨·DP·서스펜드·진화원 수)×maxField ·
  양측 브리딩 · 시큐리티/트래시/덱 **수** + 트래시 공개 내용(축약) · 메모리(좌석 시점 부호 정규화) ·
  페이즈 · 턴 · 자기 시큐리티는 비공개(수만)
- vocab = 로더의 canonical CardID 전집, `vocabHash` 계약 준수. 피처명 목록 → `obsSchemaHash`
- **스키마 상수는 실측 후 확정**: maxHand/maxField 16은 구 설계값 — E-01 슬롯 64와 실전 분포
  (스타터 무작위 대전 필드 최대치)를 재보고 정한다. 관측 잘림은 소리 없이 하지 않는다(초과 시 로그)

## 4.2 행동 — factored-v1

결정 지점 타입별 헤드(구 설계의 factored 구조 계승, 표면은 현 census로 재정의):
- **MainPhase**: Pass / PlayCard(hand 슬롯×타겟 프레임) / Attack(내 퍼머넌트×대상[시큐리티|상대 퍼머넌트]) /
  ActivateCard / ActivatePermanent
- **Selection**: 채널별 — 카드 다중선택(SelectCard/Hand/Permanent: 후보×선택/종료) · 카운트 ·
  영역(DigiXros 0-4) · 예/아니오(Optional/Redraw/Breeding) · 커맨드 버튼 인덱스 · 스킬 인덱스
- **마스크 생성기 = 술어 재사용**: 메인페이즈는 RandomVirtualPlayer가 이미 쓰는 AS-IS AI 동일 술어
  (`CanAttack`·`CanPlayFromHandDuringMainPhase`·`CanPlayCardTargetFrame`)를 열거기로 승격.
  선택 질문은 각 선택기의 `_canTargetCondition`/`_maxCount`/`_canNoSelect`를 읽어 후보 집합 구성
  — **선택기별 합법 집합 노출 census가 구현 1순위 항목** (현 채널은 최소 응답만 보내서 안 읽던 것)
- 합법성 2중 방어: 마스크(1차) + 프로토콜 §5 illegal_action 재발행(2차)

## 4.3 step/reset — RlMatchHost

MatchSmoke.RunToCompletion의 일반화(같은 뼈대: Build→SupplyGameData(양덱)→RunLifecycle→driver→
TryPin→틱 루프→Teardown):
- `reset{seed,decks}` → 매치 기동, 첫 결정 지점까지 틱 → `turn{seat,observation,actionMask}`
- `action{seat,index}` → 디코드 → seam 호출(QueueMainPhaseAction/SetXxx/클릭) → 다음 결정
  지점까지 틱 → `turn` 또는 `result`
- PolicyVirtualPlayer: Answer()/Act()에서 즉답 대신 "결정 지점 도달"을 호스트에 노출하고 응답을
  기다리는 형태(단일 스레드: 호스트 루프가 틱과 프로토콜 IO를 소유)
- 정체(무진행 1500틱)·미처리 예외 → `result{reason:aborted, 보상 0}` (프로토콜 §5)
- maxSteps 캡 → 무승부 0.0 (§4)

## 4.4 벡터화 — 프로세스 6

트레이너가 호스트 프로세스 6개에 각각 stdio 연결(셀프플레이=연결당 2좌석 claim). 배치는
트레이너단 조립. 프로세스 수준 격리라 한 매치의 크래시가 다른 워커를 못 건드림 — OOM 사고
이후의 운영 규율(MemoryMax 상한)도 워커 단위로 그대로 적용.

## 작업 분해 (착수 순서)

1. **선택기 합법 집합 노출 census** — 12사이트 각각 `_canTargetCondition` 등 내부 상태를 읽어
   후보를 열거할 수 있는지 실측(리플렉션 접근은 기존 채널 방식 그대로). 마스크 생성기 사양 확정
2. 관측 인코더 + CardVocabulary (AS-IS 타입 직결, 스키마/vocab 해시)
3. FactoredActionEncoder(디코드→seam 호출 표) — 스키마 상수 실측 확정 포함
4. PolicyVirtualPlayer + RlMatchHost(stdio, 프로토콜 v1 전문 준수) — 진입점은 driverprobe와 같은
   형태의 `tools/` 러너 또는 Engine 내 Host 클래스+얇은 exe
5. 랜덤-정책 클라이언트(파이썬 수십 줄)로 자기검증: N판 완주 + NFR-3(같은 시드·액션열 재생 =
   다이제스트 일치) + illegal_action 경로
6. 게이트: "학습 루프가 돌고 eval이 나온다" — 트레이너 확인 항목 해소 후 접속
   (없으면 최소 PPO 트레이너 신규가 4.5의 실체가 됨 — 사용자 결정)

## 착수 조사 결과 (2026-07-29 밤, 작업 1 수행분)

**트레이너 실물 확인 — 미결 해소**: `rl/`가 현 저장소에 생존 — `dcgo_rl` 패키지(BridgeClient·
DcgoSeatEnv·CardEmbeddingExtractor·덱 프로바이더) + train/evaluate/replay/train_league +
MaskablePPO(sb3_contrib). **스키마-동적**("호스트 describe가 진실 — 이중 구현 금지",
train.py:53) — 구 상수(obs 3088·action 599·vocab 4206) 복제 의무 없음, 프로토콜 v1만 지키면 됨.
호스트 기대 경로 `tools/RlBridgeHost/RlBridgeHost.csproj`(train.py:3) — **여기 재건이 4.5의 실체**.
vocab canonical 규칙은 python `dcgo_rl.cards.CardIndex`와 정렬 필수(구현 시 대조).

**마스크 생성기 원리 확정 [실측]**: AS-IS는 질문 시 합법 후보를 항상 **실체화**한다 — 합법
퍼머넌트/손패에 `AddClickTarget` 배선(SelectPermanentEffect:414·SelectHandEffect:261·
SelectCardPanel:324·SelectAttackEffect:266,283), 커맨드 버튼 생성(UserSelectionManager·DigiXros
영역), `SelectCountEffect._candidates` 리스트. 따라서 **마스크 = "지금 클릭 가능한 것의 census"**
(OnClickAction != null 스캔 + 버튼 스캔 + _candidates) — 룰 술어 복제 0, 구성상 합법. 구엔진의
병목(결정점당 합법표 재열거 17.4ms)도 구조적으로 회피(배선은 이미 만들어져 있음).

**구설계 채택 결정 2건** (rl_env_design_2026-07-17 §D 유효): ① 다중 선택 = **순차 부분-선택
액션화**(후보 토글+Confirm — AS-IS 싱글-픽 루프와 동형) ② **관측 슬롯↔액션 레인 정렬 불변식**
(hand/field 슬롯 순서 = 액션 레인 순서) 계약 유지.

**스키마 상수** (실측 + 사용자 교정 2026-07-29 밤): 손 최대 실측 45 → **maxHand=50**(자기 덱 구조
상한; 상대 카드 유입류 효과가 있으면 이론상 초과 가능 — Encode가 초과를 로그, 마스크도 같은 캡이므로
로그 발생 시 재검토). 필드는 처음에 16으로 잡았다가 **사용자 지적으로 교정**: 씬의 구조 상한은
`HeadlessScene.BattleAreaSlots(64)+브리딩 1 = 65`이고, 16은 여유가 아니라 **마스크 열거기가 17번째
이후 퍼머넌트를 소리 없이 떨어뜨리는 결함**이었다. 수정: `MaxField = BattleAreaSlots + 1`로
**소스 상수에서 파생** — 소스가 변하면 스키마·해시가 따라 변해 트레이너가 알아챈다. 결과:
**maxChoice도 같은 교정**(사용자 지적 2연타): 구값 16은 "덱 서치/트래시 열람" 공개-집합 선택의
후보(최대 한 덱 55장)를 17장째부터 무음 절사하는 결함 → **55 = 메인 50+디지타마 5 구조 상한**,
포획 3지점(패널·카운트·커맨드)에 overflow 로그 신설. 최종: obs **788피처(cardId 237채널)**·행동
**238레인**(NULL+YES+hand50+myField65+foeField65+foePlayer+choice55). 트레이너는 스키마-동적이라
크기 변화 무비용.

**멀티선택 순차 진행의 조임 (2026-07-29 밤, 선반영)**: 부분-선택마다 재포획되는 순차 구조 위에
① `choice.selectedCount` 피처(선택기 자신의 부분 리스트 판독 — 패널 `_preSelectedHandCardList`,
SelectHandEffect `_targetCards`, SelectPermanentEffect `_targetPermanents`) ② 패널 YES/NULL 합법성을
**버튼 activeSelf에서 판독**(EndSelectButton:506=CanEndSelection·NoSelectButton:232=_canNoSelect —
AS-IS 자체 게이트, 룰 복제 0) ③ 배선형 선택은 부분-선택 후 빈-답 금지(내부 상태 desync 차단)
④ 빈 마스크 방지(패널 과도기 틱 재포획). 최종 obs **789피처**.

## 잔여 확인 항목

- ActivateCard/ActivatePermanent의 인자 공간 정독(효과 발동 대상 지정 방식)
- 보상: 프로토콜 §4 순수 승패 ±1 유지(셰이핑은 트레이너 몫) — 변경 없음 제안
- `dcgo_rl.cards.CardIndex`의 vocab canonical 규칙 대조(구현 시)
