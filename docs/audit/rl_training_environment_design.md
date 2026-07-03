# headlessDCGO — RL 학습환경 설계안 (v0.1)

> **상태**: v0.1 초안 (living document). 2026-07-03.
> **성격**: 구현 착수 전 아키텍처 설계안. 코드 조사로 확인한 사실 + 엔진/어댑터 경계 + 확정 결정 +
> 미확정/후속을 정리. 상세 스키마·프로토콜·코드는 후속 버전에서 구체화.
> **선행 문서**: `archive/rl_gap_remediation_design.md`(RL 인터페이스 갭 조치 완료), `development_roadmap.md`(Phase 5 = 통합/RL).
>
> **범위 원칙**: 엔진/카드/프리미티브 코드는 변경하지 않는 설계 단계 문서. 실제 구현은 후속.

## 0. 한눈에 — 레이어 스택

```
┌───────────────────────────────────────────────────────────────┐
│ (예정) 트레이너      Python rl/  — MaskablePPO, 카드-ID 임베딩,   │  ← 학습 알고리즘
│                      셀프플레이, 네트워크                          │
├───────────────────────────────────────────────────────────────┤
│ (예정) 브리지        tools/RlBridgeHost — stdio JSON-lines,        │  ← 프로세스 경계
│                      벡터/마스크/보상 직렬화                        │
╞═══════════════════════════════════════════════════════════════╡
│ RL 어댑터            HeadlessRlEnvironment, ObservationEncoder,    │  ← 도메인객체→텐서
│                      FactoredActionEncoder, RlRewardCalculator,    │     gym reset/step/observe
│                      RlStepResult, PolicyEpisodeRunner, Dataset     │     보상/discount/시점결정
╞═══════════════════════════════════════════════════════════════╡  ◀── 경계선(BOUNDARY)
│ 엔진 파사드          DcgoMatch, HeadlessGameLoop                    │  ← 매치 생명주기
│                      Init/Reset/ApplyAction/Step, GetObservation    │     구조화 스냅샷/합법행동/결과
│                      (perspective 시점필터), GetLegalActions, Result │     ⚠ Encode* 편의메서드 누수
├───────────────────────────────────────────────────────────────┤
│ 엔진 코어            State/Services/Effects/Choices/Rules/          │  ← 게임 규칙·상태·전이
│                      Coroutines/DataLoading                         │     효과해소·합법행동·승패·결정론
│                      (RL을 전혀 모름; 도메인 객체만 출력)             │     가시성 규칙(DefaultVisibility)
└───────────────────────────────────────────────────────────────┘
   의존 방향: 위 → 아래 (단방향). 코어는 RL을 역참조하지 않음.
   물리적 분리: 없음 — 전부 1개 어셈블리(HeadlessDCGO.Engine.csproj),
   RL 파일도 코어와 같은 Headless/Runtime/ 폴더 + 같은 네임스페이스.
```

## 1. 누가 무엇을 하나 (책임 표)

| 관심사 | 엔진 코어/파사드 | RL 어댑터 | 근거 |
|---|---|---|---|
| 게임 규칙·상태 전이 | ✅ | | GameFlowProcessor, AttackPipeline |
| 합법행동 산출(`LegalAction`) | ✅ | | HeadlessLegalActionDispatcher |
| 승패 판정(`MatchResult`) | ✅ | | TerminalEvaluator |
| 결정론(시드) | ✅ | | EngineContext.CreateDefault(seed) |
| 시점 필터(상대 히든존 count-only) | ✅ 스냅샷 생성 시 | (perspective 지정) | HeadlessGameLoop.BuildZoneObservations |
| 구조화 관측(`ObservationSnapshot`) | ✅ | | HeadlessGameLoop.GetObservation |
| 관측 → **벡터** 인코딩 | ⚠ 누수(DcgoMatch.Encode*) | ✅ | ObservationEncoder |
| factored 행동공간/마스크 | | ✅ | FactoredActionEncoder |
| 보상/discount | | ✅ | IRlRewardCalculator |
| gym reset/step/observe | | ✅ | HeadlessRlEnvironment |
| 에피소드 롤아웃/데이터셋 | | ✅ | PolicyEpisodeRunner, RlTrainingDataset |
| 프로세스 경계(stdio/JSON) | | 브리지(예정) | tools/RlBridgeHost |
| 학습 알고리즘/네트워크 | | 트레이너(예정) | Python rl/ |

**핵심 규칙**: 엔진은 **구조화 도메인 객체**(LegalAction, ObservationSnapshot, MatchResult)까지만 만든다.
**텐서(벡터/마스크/보상)로 바꾸는 건 RL 어댑터**의 몫. 유일한 경계 누수 = `DcgoMatch.Encode*`
(`DcgoMatch.cs:349–370`)가 인코더를 직접 부르는 것 → 분리 시 어댑터로 이동 대상.

## 2. 지금까지 확인한 사실 (조사 결론)

### 2.1 행동공간
- action id는 **카드/효과별로 안 생김**. 제네릭 타입(PlayCard/Digivolve/…) × 보드 위치. 효과는 행동이
  아니라 결과로 자동 발동. 효과가 요구하는 선택만 제네릭 `ResolveChoice`(후보 slot별).
- factored 마스크 = 고정길이 `double[]`(합법 1/불법 0), `FactoredActionSchema.TotalSize`. 기본 599
  (손패16/필드16/선택16). 용량 초과·충돌 행동은 `Unmapped`로 표면화되나 런타임 소비처 없음 → factored
  정책엔 안 보임(손패 17장째부터 잘림). 용량은 스키마 주입으로 조절(미확정, 튜너블).

### 2.2 관측 — 두 레벨(중요)
- **스냅샷(rich)**: 본인 손패/시큐리티/덱 카드 정체까지 담김(ZoneObservation.Cards). 상대 히든존은 count-only.
- **인코더 벡터(lossy, 기본)**: BattleArea 8슬롯 per-card 스탯만. 그 외 전부 count. → **본인 손패 정체,
  choice 후보 id, attack의 attacker/target/blocker id(known 불리언만), 진화 스택 소재 정체, 효과 큐 상세,
  다스텝 이력이 벡터에서 빠짐.** = 현재 벡터는 가시상태의 충분통계도 아님.
- 카드 정체(CardNumber)가 벡터에 없음 → 스탯 같은 카드 구분 불가. **카드-ID 임베딩 필요**(확정).

### 2.3 은닉정보 / reveal (POMDP)
- 가시성 = 정적 존 속성(Hand/Library/Security/DigitamaLibrary=Hidden, 그 외 Public). `VisibilityView`는
  호출처 0 = 죽은 코드. 실제 필터는 GameLoop에서 DefaultVisibility로 직접.
- reveal은 카드를 안 옮김(Library 잔류) + 후보를 **chooser의 legal action에만** 노출. `HeadlessChoiceState`엔
  후보 id 필드 자체가 없음 → 후보 정체는 관측(스냅샷/벡터)에 없음.
- **public reveal(양쪽 공개)은 상대 시점 관측에 반영되는 채널이 아예 없음.** 공개→다시 히든 전이의 기억도
  관측에 없음(Markov 현재상태만) → 밑장/과거 reveal 의존 최적정책은 히스토리(RNN/프레임스택) 필요.

### 2.4 아키텍처 분리
- 1개 어셈블리, RL 파일이 코어와 같은 폴더/네임스페이스. 의존은 단방향(코어는 RL 무지).
- 권장: RL 어댑터를 **별도 `HeadlessDCGO.Rl` 어셈블리**로 물리 분리 → 경계 명확 + 관측/인코딩 확장이
  "엔진 코어 변경"이 아니게 됨(AGENTS.md 충돌 해소). 단 스냅샷 데이터 부재(choice 후보·진화 소재)는
  엔진 스냅샷을 확장해야 함(작고 가산적).

## 3. 확인된 갭 → 책임 귀속 (무엇을 어디서 고치나)

| 갭 | 고치는 레이어 |
|---|---|
| 카드-ID 임베딩(관측 벡터) | **RL 어댑터** (vocab는 엔진 CardDatabase 읽기) |
| 본인 손패 정체가 벡터에 없음 | **RL 어댑터** (CardFeatureZones에 Hand 추가 / 스냅샷 직렬화) |
| choice 후보 id가 관측에 없음 | **엔진** (HeadlessChoiceState + GameLoop 스냅샷 확장) |
| 진화 스택 소재 정체 없음 | **엔진** (DigivolutionStackReader를 관측에 배선) |
| attack id ↔ 보드 슬롯 매칭 | 엔진 스냅샷(InstanceId) 또는 어댑터 인코딩 |
| public reveal 상대 노출 | **엔진**(새 채널) — 카드 미포팅이라 후속 |
| POMDP 기억(reveal/밑장) | **트레이너**(RecurrentPPO) 또는 어댑터/엔진(belief 피처) |
| factored 용량(손패/필드) | **RL 어댑터**(스키마 주입, 튜너블) |
| Unmapped 커버리지 노출 | 어댑터/브리지 |

## 4. 어댑터 분리 — **확정: 별도 어셈블리로 물리 분리**

신규 `src/HeadlessDCGO.Rl/HeadlessDCGO.Rl.csproj` (엔진을 ProjectReference). 파일 이동:

**→ Rl 어셈블리로 이동 (인코딩·보상·gym·롤아웃 = 어댑터 관심사):**
`HeadlessRlEnvironment`, `RlStepResult`, `RlRewardCalculator`, `RlActionOutcome`, `RlTransition(Sample)`,
`RlTrainingDataset(+JsonlExporter)`, `RlVectorSchema`, `ObservationEncoder`(+옵션/EncodedObservation/Feature),
`ActionEncoder`(+EncodedActionMask/EncodedAction), `FactoredActionEncoder`(+Schema/Mask/PositionContext),
`HeadlessActionPolicy`, `HeadlessPolicyEpisodeRunner`, `HeadlessEpisodeBatchRunner`, `HeadlessEpisodeFingerprint`.

**엔진에 남김 (도메인·구조화 스냅샷):**
`DcgoMatch`(파사드), `HeadlessGameLoop`, `GameFlowProcessor`, `AttackPipeline` 등 코어;
구조화 타입 `ObservationSnapshot`, `CardObservation`(뷰), `HeadlessChoiceState`, `HeadlessTurnState`,
`HeadlessMemoryState`, `LegalAction`, `MatchResult`, `MatchConfig`, `EngineContext`.

**경계 누수 정리 (거의 무료)**: `DcgoMatch.EncodeObservation/EncodeActionMask/EncodeFactoredActionMask`를
Rl 어셈블리로 이동(확장 메서드 또는 어댑터로). 실측: **src에서 이걸 쓰는 곳은 `HeadlessRlEnvironment` 하나뿐**
(테스트 포함 총 5곳) → 이동 비용 미미.

**리팩토링 범위(실측)**: RL 타입을 참조하는 **테스트 프로젝트 21개** — 각 csproj에 `HeadlessDCGO.Rl`
ProjectReference 추가(+네임스페이스 새로 쓰면 `using` 갱신). 기계적이지만 넓음. 네임스페이스를 유지하면
`using` 편집 없이 참조만 추가하면 됨(권장 최소 변경).

**여전히 엔진 코어를 건드려야 하는 것(어댑터로 못 뺌, §3 스냅샷 데이터 부재)** — 작고 가산적:
`HeadlessChoiceState`에 후보 id 필드 추가 + GameLoop 배선; `DigivolutionStackReader`를 `CardObservationView`에
배선(소재 정체); (선택) `CardObservation`에 InstanceId(attack 슬롯 매칭).

## 5. 관측 확장 — **확정: 전체 충분통계 목표** (단, "정보집합"의 충분통계)

⚠️ **핵심 정정**: "충분통계"는 *raw 가시상태*가 아니라 **에이전트의 정보집합(information set)**의 충분통계여야
한다. 현재 스냅샷은 **본인 시점에 자기 히든존 전체(손패·시큐리티·덱) 카드 정체를 노출**하는데, 디지몬 룰상
**플레이어는 자기 시큐리티·덱의 정체/순서를 모른다**(게임 시작 시 덱 top에서 face-down 배치, 열람 안 함).
따라서 그대로 인코딩하면 **룰 위반 + 실제 대전에서 불가능한 우위**를 학습하게 된다.

**정보집합 = 인코딩 대상:**
- 포함: 본인 **손패** 정체, **양쪽 필드(BattleArea)·브리딩·트래시** 등 공개존 정체, 메모리, 턴/페이즈,
  (본인 차례의) **choice 후보 정체**, **attack 참여 카드**(공격자/타깃/블록커) 슬롯 매칭.
- 제외(count-only 유지): **본인 시큐리티/덱 정체·순서**, 상대 모든 히든존. ← 룰상 모르는 정보.
- **효과로 알게 된 카드**(reveal로 본 덱 top, 밑장 보낸 카드 등)는 belief/history 문제 → §5.1 지식추적으로 처리
  (또는 RecurrentPPO). raw 노출 금지.

**필요 작업(전체 충분통계):**
1. (어댑터) 관측 인코더가 **정보집합만** 벡터화 — 본인 손패/공개존 per-card 정체 + 카드-ID 임베딩,
   본인 시큐리티/덱은 count-only. `ObservationEncodingOptions`로 존별 정책 지정.
2. (엔진 스냅샷) `HeadlessChoiceState`에 **후보 카드 id** 필드 추가 + GameLoop 배선(choice 정체 노출).
3. (엔진 스냅샷) `DigivolutionStackReader`를 `CardObservationView`에 배선 — **진화 스택 소재 정체**.
4. (엔진 스냅샷) `CardObservation`에 **InstanceId** 추가 → attack의 attacker/target/blocker를 보드 슬롯과 매칭.
5. (어댑터/엔진) 시점 필터가 **본인 시큐리티/덱도 count-only**가 되도록 인코딩 단에서 보장(스냅샷은
   자기 것을 노출하므로 어댑터가 명시적으로 제외).

### 5.1 지식 추적(known-info) — **확정: 룰 충실 기본 + 효과 획득 지식 노출** (신규 엔진 레이어)

방침: 기본은 본인 시큐리티/덱 count-only(룰 충실). **단, 효과가 특정 카드를 특정 위치에 두거나 공개해서
플레이어가 정당하게 알게 된 정보**(reveal로 본 덱 top, "덱 맨 위에 놓기", 정렬 등)는 관측에 노출되도록
**per-player 지식 스토어**를 둔다. 이것이 앞서의 "reveal 후 밑장" 기억 문제(§2.3)를 RNN에 떠넘기지 않고
엔진이 ground-truth로 푸는 방식.

현황: **지식 추적 개념은 엔진에 전무(greenfield)**. 단 원시연산은 존재 — `ZoneState.InsertTop/InsertAt`,
`RevealAndSelect`(DeckTop/DeckBottom/DeckTopOrBottom + 순서). 모든 상태변경이 지나는 단일
`MatchStateMutationSink` = 자연스러운 hook 지점.

설계(신규, 엔진 레벨 = who-knows-what는 게임 상태 진실):
- **스토어**: `(player, hiddenZone, position) → knownCardId` (또는 `(player, cardInstanceId) → knownBy set`).
- **기록(populate)**: 지식 부여 효과가 발동할 때 — reveal(본 카드), place-on-top/arrange(놓은 카드),
  look-at-top 등. hook = `RevealAndSelect` + `MatchStateMutationSink`.
- **무효화(invalidate)**: 셔플, 해당 카드 드로우(소비), 위쪽 삽입으로 위치 이동 시 갱신/삭제. 스토어가
  zone 변경에 반응.
- **관측 노출(어댑터)**: 시점 플레이어의 "아는" 히든존 카드만 cardId 피처로(예: `deck.top.knownCardId`,
  `security.slot.knownCardId`), 모르는 건 count-only. = 룰 충실한 정보집합.
- **레이어 귀속**: 지식 추적 = **엔진**(상태 진실). 지식의 관측 인코딩 = **어댑터**.

현실 스코프: 카드 효과 대부분 미포팅 → 지식 부여 효과가 아직 안 fire. 따라서 (a) 지식 인프라는 지금 설계,
(b) reveal/arrange 효과 포팅(Phase 4) 시 배선, (c) 수직 슬라이스는 기본 count-only로 시작하고 인프라는
점진 채움. 명시적 지식 밖의 잔여 불확실성은 RecurrentPPO로 보완.

## 6. 정책 형태 — **확정: 브리지 순차계약 + 두 정책 모두 지원**

정책(신경망) 형태는 **트레이너단 결정**(엔진/어댑터 무관). 결정: 처음부터 **feed-forward↔RecurrentPPO 교체
가능**하게 간다. 유일한 요구는 **브리지가 에피소드 순차성·reset 신호를 항상 보존**하는 계약(LSTM hidden
state가 에피소드마다 리셋되도록). 처음엔 MlpPolicy로 검증, 필요 시 LSTM으로 **비파괴 승급**.

**브리지 순차계약(설계 고정):**
- reset/step 응답에 에피소드 식별 + `terminal`/`reset` 신호 명확히. 트랜잭션을 에피소드 경계 넘어 섞지 않음.
- 벡터화 시 각 env 스트림이 자기 순서 유지(병렬 env여도 스트림 내 순차성 보존).
- 관측/지식스토어/인코딩은 정책 형태와 무관(policy-agnostic) — 위 계약만 지키면 정책 교체가 엔진·어댑터를
  안 건드림.

## 7. 확정 요약 (아키텍처 결정 총괄)
1. 트레이너 = Python + sb3-contrib MaskablePPO.
2. 브리지 = stdio JSON-lines + **에피소드 순차계약**(Mlp↔LSTM 교체 지원).
3. 카드 구별 = **카드-ID 임베딩**(트레이너 네트워크).
4. 어댑터 = **별도 어셈블리 `HeadlessDCGO.Rl`로 물리 분리**(§4).
5. 관측 = **정보집합의 충분통계**(룰 충실: 본인 시큐리티/덱 count-only)(§5).
6. 지식 = **룰 충실 기본 + 효과 획득 지식 스토어**(신규 엔진 레이어)(§5.1).
7. 정책 = **양쪽 지원**, 브리지 순차계약(§6).

## 8. 남은 미확정 / 후속
- factored 스키마 용량값(튜너블 — 데이터로 조정).
- 지식스토어·choice후보·진화소재 스냅샷 확장의 상세 스키마.
- 브리지/트레이너 수직 슬라이스 구체화(프로토콜 필드, 네트워크 구조 코드).
- 보상 shaping(G3.5-RL-D), 상대 풀 셀프플레이, public reveal 상대 노출 — 카드 포팅 진척 의존 후속.
