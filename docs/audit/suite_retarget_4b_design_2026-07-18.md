# 4b 설계서 — OLD 스캐폴드 물리 삭제 = 스위트 재조준 골 (2026-07-18, 사용자 확인 대기)

Base: main `61c81af7`. Read-only 분석. 선행=`r4_tsm_s1_design_2026-07-16.md`(S3c-d 은퇴 원장 §78-84)·`r4_tsm_investigation_2026-07-16.md`.
분류 데이터=본 문서 작성 시점 grep 기계 스캔(테스트 프로젝트 470개, 코디네이터 base fail-set 스냅샷 97 = `jobs/dae5cd41/tmp/base_failset_97.txt` 교차). **코드 0.**

## 배경·종점
R4 컷오버(결정 3=B)로 `DcgoMatch.CreatePumpDriven` 펌프가 게임 로직의 정본 드라이버가 됐고, OLD 스텝-케이던스 드라이버(HeadlessEarlyPhaseFlow·HeadlessMainPhaseFlow invented eval·MetadataActionProcessor AdvancePhase/EndTurn body·throw-기록-재생 choice 계약·`EndOfTurnDrainedTurn` 마커·HeadlessGameLoop RunToStable 스텝 경로)는 **LEGACY TEST SCAFFOLD**로 강등됨(S3c-d 은퇴 원장 즉시-삭제 0항 = 단계적 물리 은퇴). 유일 소비자 = 기존 테스트 코퍼스.

**이 골(4b)의 종점 = 소비자 0 확인 후 물리 삭제.** 원칙(불변):
1. **재조준 우선** — 단언 보존, 구동만 OLD→펌프로 교체.
2. **은퇴는 검증 대상 소멸분만** — 부수 행동을 단언하는 테스트는 그 단언을 건져 재조준. 검증 대상 자체가 발명물(삭제 예정)인 것만 삭제.
3. **삭제는 마지막** — 소비자 0 판정(green 게이트) 도달 후에만 표면 물리 삭제. B군 registry 물리삭제 게이트 패턴 재사용.

---

# 1부: 전수 분류

## 1.1 구동 방식 기계 분류 (상호배타 우선순위 캐스케이드)

우선순위: ④정적(파일명) → ⑤발명물(파일명) → ①펌프(`CreatePumpDriven`) → ②a OLD-매치(`DcgoMatch` 잔여 참조) → ②b synth-머신러리(`AutoProcessing.`/`RunToStableAsync`/`ResumeSuspendedWindowsAsync`/`EarlyPhaseFlow.`/`HeadlessMainPhaseFlow.`) → ③bare.

| # | 카테고리 | 구동 시그니처 | 프로젝트 | base-red(97 중) | 4b 처분 |
|---|---------|--------------|---------:|----------------:|---------|
| ① | 펌프-네이티브 | `CreatePumpDriven` + `GetLegalActions`/`ApplyActionAsync` | **13** | 0 | 무변(정본 패턴) |
| ②a | OLD 풀-매치 구동 | 잔여 `DcgoMatch` 참조(펌프 아님, 기본 ctor=`new()`/`new DcgoMatch(`) | **102** | 24 | 재조준 |
| ②b | synth OLD-머신러리 | 매치 없이 AutoProcessing/RunToStable/Resume/*PhaseFlow 직구동 | **64** | 15 | 재조준(단언 건짐)+일부 은퇴 |
| ③ | bare 직접-드라이브 | 매치·OLD-머신 무 — 컨트롤러/이펙트/Sink 직접 호출 | **274** | 55 | **무관**(비-블로커; red는 직교 포팅부채) |
| ④ | 빌드-전용/정적 검사 | G0-*·Forbidden.dependency·Assembly·exclusion·Seeded.random | **9** | 2 | 무변(재조준 불요) |
| ⑤ | 발명물-표면 검증 | EffectRegistry.contract·*.binding·Hashtable.replacement·DeadTimingInfra·ActivatedBridge | **8** | 1 | **삭제 대상**(검증 대상 소멸)·일부 B군 트랙 이관 |
| | **합계** | | **470** | **97** | |

**핵심 판정: 삭제-블로커 모집단 = ②a+②b = 166개** (OLD 풀-매치 루프 또는 synth OLD-머신러리를 소비). ③(274)·④(9)는 페이즈-드라이버를 소비하지 않으므로 삭제를 **막지 않는다**(그 red 55+2는 포팅부채·STOP 스텁 등 R4-직교 = 4b가 건드리지 않음). ⑤(8)은 검증 대상이 발명물이라 재조준이 아니라 **소멸**한다.

### 교차-절단 시그니처 (②a+②b 내부 정밀)
- **AdvancePhase/EndTurn 스텝-액션 소비자 = 69** (`HeadlessActionTypes.AdvancePhase|.EndTurn`) — OLD 발명 페이즈 분절 액션. 결정 3=B에서 이 분절이 은퇴 → 이 69개가 재조준 최우선(액션 통화 자체가 바뀜: AdvancePhase/EndTurn → 펌프 자동흐름+Pass).
- **throw-계약 소비자 = 20** (`WindowChoicePendingException|DeferredChoicePendingException|ResumeSuspendedWindowsAsync`) — S3c-d 항8. throw-기록-재생 계약을 직접 구동. 컷오버 후 펌프는 await-모드라 이 계약 은퇴 → 20개가 계약 은퇴 게이트.

## 1.2 base fail-set 97 교차 (트라젝토리 입력)

| 카테고리 | red 수 | 성격 |
|---------|-------:|------|
| ②a OLD 풀-매치 | 24 | OLD 드라이버 S2/S3 반절단으로 red화된 것 + 직교부채 혼재 → 재조준 시 일부 **green 복원**(fail-set 수축) 후보 |
| ②b synth-머신러리 | 15 | 동상 |
| ③ bare | 55 | **직교**(포팅부채·STOP·프리미티브 미개발) — 4b 무접촉, red 유지 |
| ④ 정적 | 2 | 직교(정책 게이트) |
| ⑤ 발명물 | 1 | 삭제 시 fail-set에서 **제거** |

→ 4b가 접촉하는 red = ②a24+②b15+⑤1 = **40**. 나머지 57(③55+④2)은 4b 스코프 밖(별도 골). **fail-set 97은 4b 종료 시 이 40의 처분(green 복원 or 삭제-제거)만큼 순감**, 하한은 직교 57.

## 1.3 OLD 엔진 표면 전수 (삭제 대상 파일/멤버 + 소비자 수) — S3c-d 은퇴 원장 §9 갱신

경로 접두 `src/HeadlessDCGO.Engine/Headless/Runtime/`(별도 표기 외).

| 항 | 표면 (파일:멤버) | 줄 | 테스트 소비자(프로젝트) | 판정 |
|----|------------------|----:|------------------------|------|
| 1 | `HeadlessGameLoop.cs` 전체(OLD 스텝 루프+`RunToStableAsync`) | 414 | 직접 참조 5 · `RunToStableAsync` 43 · 기본-ctor 매치 103 | G1 근본 게이트(플립 후 삭제) |
| 1부속 | `HeadlessEarlyPhaseFlow.cs`의 `ResolveBreedingAsync` | (동파일 내) | **0** (死코드, 엔진 내 호출부 0) | **경량 즉시 삭제 가능** |
| 2 | `MetadataActionProcessor.cs` `AdvancePhaseAsync`(:969)·`EndTurnAsync`(:1012) | 1531(파일) | AdvancePhase/EndTurn 액션 69 · 직접 26 | G1 연동(디스패처 발행 중단 후) |
| 3 | `EndTurnAsync` drain(=항2 부분) + 항4 마커 동시 삭제 | — | (항2 포함) | 항4와 원자 |
| 4 | `WindowResolutionController.cs:25` `EndOfTurnDrainedTurn` 마커 | 1 | **0** (엔진 내부 전용) | 항3 은퇴와 동시(NEW=per-effect 캡) |
| 5 | `HeadlessMainPhaseFlow.cs` invented eval + `ResolveTurnEndMinMemory` 사본 | 318 | 직접 3 | 항2 은퇴 시 원본(AutoProcessing.TurnEndMinMemory) 정본 승격 |
| 6 | `HeadlessEarlyPhaseFlow.cs` Unsuspend/Draw/Breeding 블록 + supply OnEnterField/WhenDigivolving 변환 | 277 | 직접 5 · W2-SkillWindowSupply 재조준 | DORMANT(다운스트림 소비 카드 0); PlayCardAction/DigivolveAction enriched emit 제거 동반 |
| 7 | `CardEffectCommons/PlayCardsBridge.cs:464` `CanEnterFieldByEffect` 브리지 사본 | — | 내부 2호출부·생산자 0 | **경량 즉시 가능**(실물 `CardSource.CanEnterField` 재배선; ICanNotPutFieldEffect 생산자 0=동작 no-op) |
| 8 | throw-계약(`WindowChoicePendingException` 재생 + `ResumeSuspendedWindowsAsync`) | (AutoProcessing 계열) | throw-계약 20 | 컷오버 후 펌프 await-모드가 대체; 20개 재조준/은퇴 후 삭제 |
| 9 | 진화 legality 이중석 | — | (상환 완료, RD-R3-01) | 상환됨. 공유 데이터층(`ReadRequirements`)=존치. 이중 좌석 병합만 잔여 |
| 11 | `HeadlessLegalActionDispatcher.cs` AdvancePhase/EndTurn 페이즈 표 | 379 | 직접 5 | S3c-b에서 펌프 분기 완료; OLD arm만 삭제 |

**즉시-삭제 무료 2건**(소비자 0, 지금 삭제 가능): 항1부속 `ResolveBreedingAsync`·항7 `CanEnterFieldByEffect` 브리지.

---

# 2부: 재조준 표준 패턴

## 2.0 정본 목표 패턴 (EXEMPLAR-T1~T3B/GLINK)

`tests/EXEMPLAR-T1.Witness.Tests/Program.cs` = 후속 트랜치 복사 정본. 골격:

```
var policy = new PolicyChoiceProvider();
EngineContext ctx = ContextFactory.CreateWithProvider(policy, seed);
CardBaseEntityLoader.LoadInto((CardDatabase)ctx.CardRepository);
MatchSetupConfig setup = MatchSetupConfig.Create(decks, firstPlayerId: P1, ..., enableMulligan: false);
MatchConfig config = MatchConfig.Create(new[]{P1,P2}, randomSeed: seed, setup: setup);
DcgoMatch match = DcgoMatch.CreatePumpDriven(ctx, new EngineTrace());   // ← OLD 기본 ctor 대체
await match.InitializeAsync(config);
// 구동: 리걸 테이블에서 액션 선택 → ApplyActionAsync (StepOnce/DriveUntil)
LegalAction a = RequireLane(match, P1, HeadlessActionTypes.ActivateOption, subject, "...");
// 효과-내부 Select*/Optional = policy 좌석(에이전트 좌석 = 스크립트 답)
```

- **구동 프리미티브**: `match.GetLegalActions(pid)`(AmbientMatchContext.Enter 스코프 내)·`match.ApplyActionAsync`·`DriveUntil(m => AtMainWaitOf(m,P1))`·`HasPendingChoice()`/`ResolvePending`·`IsTerminal()`.
- **choice 응답**: `PolicyChoiceProvider`의 술어-매칭 답(스크립트 답 = 에이전트 좌석의 답) — R4RL-01 `ScriptedChoiceProvider` 관례를 일반화. throw-재생 없음.
- **금지**: OLD-cadence 직접 컨트롤러 호출·스텝 액션(`AdvancePhase`/`EndTurn`).

## 2.1 OLD-스텝 전형 → 펌프 치환표

| # | OLD 전형 패턴 | 대표 스위트 | 펌프 재조준 치환 | 단언 보존 방식 |
|---|--------------|-------------|-----------------|---------------|
| P-A | **페이즈 진행 단언** — `StepAsync(AdvancePhase)` 반복으로 Active→Draw→Breeding→Main 도달을 스텝별로 확인 | G2A-006·G2E-*·G3.5-N9(BreedingUnsuspend) | `DriveUntil(AtMainWaitOf)` 자동흐름 도달. 스텝 카운트 단언 → **도달-상태 단언**(phase/cursor/player)으로 치환 | 스텝 수 자체는 OLD 발명물(비-단언). 도달 상태·부수효과(언탭·드로·메모리)는 그대로 단언 |
| P-B | **스텝 경계 단언** — 특정 스텝 후 리걸 테이블=`{AdvancePhase}`, 다음 스텝 후 변화 | G2A-006 dispatch hook·G3.5-RL-A1/A3 | 리걸 테이블 단언을 펌프 정지-seam 리걸(Main 대기의 Pass/PlayCard/Digivolve/Attack)로 치환 | 액션 통화가 바뀜(A3 FactoredActionSpace=관측 shape 재정의 동반, S2 결정 A 판례). RL 스위트는 스키마 재조준 필수 |
| P-C | **choice 재실행(throw-재생) 단언** — `try{await ...}catch(WindowChoicePendingException)` + `ResumeSuspendedWindowsAsync` 루프로 창 재개 | W1b-SkillWindowResume·C-EoT2·C-Del-3C2B·GR-006 | 펌프 await-모드: choice가 `HasPendingChoice()`로 표면화 → `ResolvePending`(policy 답) → 제자리 재개 | 창 멤버십·park/resume 순서·drain 순서 단언은 보존; throw 포착 자체(재생 계약)는 **검증 대상 소멸**(항8) → 단언에서 제거, 결과(창 발화 횟수·순서)는 펌프에서 재단언 |
| P-D | **EndTurn 액션 seam** — `StepAsync(EndTurn)`이 창 드레인→flip을 트리거, 재-EndTurn으로 flip 완료 | RD6-EndTurnSequence·GR-001(MemoryTurnEnd)·GR-006 | EndTurn 액션 → **Pass 라우팅**(AS-IS PassTurn) 또는 임계-자동 턴종료(펌프 내 EndTurnCheck). "재-EndTurn으로 flip"=OLD 이중스텝 → 펌프 단일 자동흐름 | [End of Your Turn] 효과의 **pre-flip drain** 단언(RD6의 핵심)은 보존 — 펌프에서 EoT 창이 flip 전 발화하는지 재단언. `EndOfTurnDrainedTurn` once-마커 의존 단언은 제거(AS-IS=효과별 캡) |
| P-E | **synth 머신러리 직구동** — 매치 없이 `AutoProcessing.GetSkillInfos`/`RunToStableAsync`/`GameFlowProcessor` 직접 호출로 창/삭제 파이프 검증 | C-Del-*·C-Atk-*·F1-M*·D1-BatchId | 두 갈래: (a) query 표면(GetSkillInfos 등)=**존치**(retained substrate) → 무변 (b) 드라이버 표면(RunToStable/Resume)=펌프 매치 스캐폴드로 감싸 실구동 | 결정론 픽스처(단일 카드+합성 상태)를 펌프 매치 최소 스캐폴드에 이식; 부수-행동 단언(삭제 순서·batch id·스캔) 보존 |

## 2.2 false-green 함정 체크리스트 (단언 약화 없이 옮기기)

1. **스텝-카운트를 도달-상태로 강등하며 도달 자체를 안 세면 no-op green** — `DriveUntil`은 조건 미달 시 throw해야(EXEMPLAR `DriveUntilAsync` 96-iter 후 상세 throw 판례). 조건 함수가 항상-true면 false-green.
2. **throw 포착 제거 시 "창이 열렸다"만 확인하고 "무엇을 골랐고 결과가 무엇인지"를 안 세면 계약만 사라지고 검증도 사라짐** — P-C 재조준은 반드시 policy 답의 **효과 결과**(카드 이동·메모리·삭제)를 재단언.
3. **synth 픽스처를 펌프로 감쌀 때 리걸-게이트가 픽스처를 막으면 액션이 Illegal로 조용히 스킵** — RD-R3-02 판례(RemoveField 직호출 픽스처의 green=픽스처 통과이지 계약 통과 아님). 펌프 감쌈 후 액션이 실제 리걸 테이블에 뜨는지 먼저 단언.
4. **PolicyChoiceProvider Fallback이 의도와 다른 답을 내면 우발 green** — 술어-매칭 답을 명시(`req.Candidates.Any(c=>c.Id==x) ? Select(x) : Fallback`), fallback 경로 진입은 실패로 간주하거나 명시 단언.
5. **관측/리걸-테이블 shape 변경(RL 스위트)을 "통과"로만 보고 정보-보존을 안 세면 cardinality 손실 은폐** — S2 판례(6 one-hot+커서 정보-보존 단언). A3/A4/B1 재조준은 factored 마스크 정보량 단언 필수.
6. **EndTurn→Pass 라우팅에서 임계-자동종료와 명시-Pass를 혼동** — 메모리 임계 미달 시 Pass가 턴을 안 넘김(AS-IS EndTurnCheck). 픽스처 메모리 상태를 명시해 어느 경로인지 확정.
7. **shadow/witness 테스트를 재조준 대상으로 오인** — R4S3c-ShadowOldNew·R4P4-ShadowRun은 OLD-vs-NEW/OLD-vs-OLD 비교가 존재이유 → **재조준 아님, 컷오버 완료 시 은퇴**. (2a에 분류되나 처분=삭제.)

## 2.3 bare 직접-드라이브(③)의 처리

③은 페이즈-드라이버를 소비하지 않으므로 **원칙상 4b 무접촉**. 단 일부 ③ 픽스처가 매치-컨텍스트 부재로 삭제 후 배선이 끊길 수 있음(예: G2G 계열이 AttackPipeline을 매치 없이 구동). 그 경우 기반 상환 배치 판례 적용:
- **G3.5-D1L 판례**(LinkSubsystem 최소 스캐폴드)·**C2-Witness 수리 판례**: 매치-컨텍스트 최소 스캐폴드(`CreatePumpDriven` + InitializeAsync + 단일-카드 덱)로 감싸 AmbientMatchContext 확보 후 직접-드라이브 유지. 단언 무변.
- 판정 기준: ③ 테스트가 삭제될 OLD 표면을 **간접 참조**(예: 기본 ctor 경유 RunToStable)하면 ②로 재분류해 재조준; 순수 컨트롤러/Sink 단위면 무변.

---

# 3부: 배치 계획

## 3.1 배치 분할 (카테고리 × 규모, 병렬 가능 단위)

| 배치 | 스코프 | 규모 | 병렬 | 게이트 |
|------|--------|-----:|------|--------|
| **B0** | 즉시-삭제 무료 2건(항1부속 ResolveBreedingAsync·항7 CanEnterFieldByEffect) + ⑤ 발명물 은퇴(검증 대상 소멸 확인) — **실행결과: 7 은퇴 + 1 보류(PRIM.TriggeredActivatedBridge→②b 재분류)**, §3.1a | 표면 2 + 스위트 7(은퇴)/1(보류) | — | 삭제 후 전체 스위트 fail-set = base−(삭제된 ⑤ live red) |
| **B1** | ②b throw-계약 20 중 P-C/P-E(C-Del-*·C-Atk-*·W1b·C-EoT2·GR-006·F1-M*) 재조준 | ~20 | 카드계열별 2~3 병렬 | 재조준분 red→green **불변 증명**(재조준 전후 동일 판정) |
| **B2** | ②a/②b P-A/P-B 페이즈-진행·스텝-경계(G2A-006·G2E-*·G3.5-N9·비-RL) 재조준 | ~35 | phase-계열별 병렬 | 동상 |
| **B3** | RL 스위트(G3.5-RL-A1/A3/A4b·B1·B2B3·C1/C2·R4RL-03) 관측/액션 스키마 재조준 | ~12 | 직렬(스키마 공유) | 정보-보존 단언 게이트 |
| **B4** | P-D EndTurn seam(RD6·GR-001·GR-006 잔여) 재조준 | ~6 | 직렬 | pre-flip drain 단언 보존 증명 |
| **B5** | ③ 재분류분(간접 OLD 참조) 최소 스캐폴드 감쌈 | 소수(감사 후 확정) | — | 매치 스캐폴드 후 리걸-등재 단언 |
| **B6-Da** | **소비자 0 판정** — OLD 표면(항1/2/3/4/5/6/8/11) grep 소비자 재확인 | — | — | 소비자 0 도달 = 삭제 게이트 |
| **B6-Db** | OLD 표면 물리 삭제(HeadlessGameLoop·MetadataActionProcessor AdvancePhase/EndTurn body·*PhaseFlow·throw 계약·마커·디스패처 OLD arm) + 기본 ctor→펌프 플립 | 표면 ~2,900줄 | — | 전체 스위트 = base fail-set(직교 57)만 잔존 |

### 3.1a B0 실행 기록 (2026-07-18, main HEAD=91e1c337, 메인 워킹트리·미커밋)

**즉시-삭제 무료 2건 (소비자 0 재확증 후 처분):**
- **항7 CanEnterFieldByEffect** (`PlayCardsBridge.cs`) — 재grep으로 소비자 확인: 내부 2호출부만(PlayPermanentCards의 `Where` 절 :53·PlaceDelayOptionCards 가드 :212), 외부·테스트 소비자 0, ICanNotPutFieldEffect 생산자 0. **처분=완료**: 2호출부를 AS-IS 위치 멤버 `cardSource.CanEnterField(...)`/`card.CanEnterField(...)` 직호출로 재배선(양측 이미 non-null 가드 보유 → 행동 항등), 래퍼 메서드+doc 삭제, 잔여 doc 참조(PlayCardsBridge 헤더·PlaceDelayOptionCards doc·CardSource.cs:417·PlayPermanentCards doc의 `<see cref>`) 갱신. 엔진 재빌드 0오류·경고 1566 무증가·CS1574 0·dangling 참조 0.
- **항1부속 ResolveBreedingAsync** (`HeadlessEarlyPhaseFlow.cs`) — **선-완료 확인**: S3c-d 커밋에서 이미 물리 삭제(BreedingPhaseResult record 동반), 現상태=툼스톤 주석만 잔존. 4b B0 재grep: .cs 소비자 0(주석 1건만). 추가 조치 불요.

**⑤ 발명물-표면 스위트 (8 분류 중 7 은퇴 + 1 보류):**
- **은퇴 7** — 각 은퇴 근거(무엇을 검증했는가·왜 대상 소멸인가):
  1. `G1F-005.EffectRegistry.contract.Tests` (live, 삭제) — 검증 대상=발명 `InMemoryEffectRegistry`/`EffectBinding`/`IEffectQueryService` 계약(Register/GetEffects/GetKeywordEffects/입력검증) + CSV goal-row 메타데이터 + 소스 sniff(no-TODO/no-Unity). **전량 발명물 표면**(레지스트리=B군 청산 대상); 게임-행동 단언 0 → 건질 것 없음.
  2. `G2D-001.Card.identity.binding.Tests` (live, 삭제) — 검증 대상=발명 `CardIdentityAdapter`/`CardIdentitySnapshot`/`InMemoryCardInstanceRepository` 메커닉(create/move/reveal/suspend/attach-source) + CSV 메타 + AS-IS 파일 sniff. 실제 이동/리빌/서스펜드 게임-행동은 AS-IS-미러 witness(G2D-002/003/004)가 별도 커버 → 발명 어댑터 메커닉은 중복; 건질 것 없음.
  3. `G3B-001.Hashtable.replacement.adapter.Tests` (live, 삭제) — 검증 대상=발명 `EffectContextHashtableAdapter`/`EffectContextAdapterKeys` 번역 메커닉 + CSV 메타 + AS-IS sniff. 매치-경로 게임-행동 0 → 건질 것 없음.
  4-7. `G3J-001.CardEffectFactory.binding.Tests`·`G3J-002.PermanentEffectFactory.binding.Tests`·`F1-DeadTimingInfra.Tests`·`Stage5-ActivatedBridge.Tests` — **선-은퇴 확인**: 소스가 이미 선행 커밋에서 삭제됨(git-tracked 0; G3J-001=3068b19d·G3J-002=0feee8df·F1-DeadTimingInfra/Stage5-ActivatedBridge=91cbb178). 잔존=디스크상 stale bin/obj 뿐 → 물리 디렉터리 정리만 수행(runner는 `*.Tests.csproj`로 발견 → 이미 비가시).
- **보류 1 (판단 분기 → 삭제 안 함, 보고):**
  - `PRIM.TriggeredActivatedBridge.Tests` (live, **보류**) — 파일명 기계분류가 "ActivatedBridge" 시그니처로 ⑤에 넣었으나, **검증 대상이 발명물뿐이 아님**: 트리거→activated 자동해소 **실게임 행동 10건**(OnAllyAttack [When Attacking] draw·subject-스코프·[End of Your Turn] once/owner-only·OnDeclaration·unsuspend once-per-turn 캡·OnDigivolutionCardDiscarded 방송+게이트-스코프·OnEndBattle winner+비-winner 스코프·OnTappedAnyone opp-suspend 메모리)을 단언. 구동=`GameFlowProcessor.RunToStableAsync`(②b synth 머신러리 드라이버 표면) + 엔진-상주 `Tfx*` 픽스처(`src/.../CardEffect/TestFixtures/`, `TfxEndTurnDraw`는 RD6-EndTurnSequence도 소비). src에 `TriggeredActivatedBridge` 명명 삭제-예정 표면 0(grep). ⇒ 이는 ⑤ 소멸분이 아니라 **②b P-E 재조준 대상**(RunToStable 드라이버를 펌프 매치 스캐폴드로 감싸 실구동, B1/B5). 부수-행동 단언 10건의 기존 펌프-witness 이식처 부재 + "신설 금지" ⇒ 보수 원칙(판단 분기=삭제 보류)에 따라 **B0 미삭제, ②b로 재분류 권고**.

**B0 처분 요약**: 표면 2(항7 재배선-삭제·항1부속 선-완료) + 스위트 은퇴 7(live 3 삭제 + 선-은퇴 stale 4 정리) + 보류 1(PRIM→②b 재분류). **건진 단언·이식처=없음**(은퇴 7 전량 발명-표면; 이식 대상 게임-행동 단언 0). fail-set 영향: 삭제된 ⑤ live 3 중 base-red 해당분만 제거(트라젝토리 96 목표는 PRIM 보류로 -1 대신 삭제분 실제 red만큼; green-count 미보고 규약).

## 3.2 배치 게이트 방식

- **재조준분 red→green 불변 증명**: 각 재조준 테스트는 재조준 **직전** 판정(red/green)을 스냅샷 → 재조준 **직후** 동일 판정이어야 통과(단언 강도 무변 = green→green, 직교 red는 red 유지 or **의도된 green 복원 시 사유 기록**). 코디네이터 base fail-set 재사용(전체 스위트는 예외 시 동기 1회, 배치=관련 스위트만 — batch-suite-policy 준수).
- **삭제 게이트(소비자 0 판정)**: `grep -rl --binary-files=text <표면 시그니처> tests` = 0 (재조준·삭제 완료분 제외) → 물리 삭제 승인. B군 registry 물리삭제 게이트 패턴.
- **DCGO/ 심링크**: 워크트리 테스트 시 필수(memory: worktree-needs-dcgo-symlink); 현재 Haiku 파일럿 워크트리 병행 중 → 4b 실행은 메인 폴더 선호(work-in-main-folder).

## 3.3 fail-set 97 트라젝토리 예상

```
base 97 = ②a24 + ②b15 + ③55 + ④2 + ⑤1
B0 후:  ≤97 (은퇴 7 중 base-red 해당분만 제거; PRIM 보류로 ⑤ red가 PRIM이었다면 유지→②b 재조준 시 처분. green-count 미보고)
B1 후:  ≤96 (②b throw-계약 재조준 red 중 OLD-절단분 green 복원 가능 — 순감, 직교분 유지)
B2 후:  ≤    (②a/②b P-A/P-B 재조준분 동상)
B3~B4 후: RL/EndTurn 재조준분 green 복원 가능분 순감
B6 후:  ≈57 하한 (③55 + ④2 = R4-직교 포팅부채, 4b 무접촉)
```
**단언**: 4b는 fail-set을 **최대 40 감소**(40=②a24+②b15+⑤1), 하한 57. 실제 감소폭은 재조준 red 중 "OLD 반절단 원인" 대 "직교 포팅부채" 비율에 의존(배치별 red 사인 분석으로 확정 — green count 보고 금지, 구조 지표=삭제 표면 줄수·소비자 잔수로 진척 계측).

## 3.4 리뷰 지점 (adversarial-review-before-cutover)

- **리뷰1**(B1 후): throw-계약 은퇴 재조준의 false-green 감사(§2.2 체크리스트 2·3 집중) — 창 발화 결과 재단언 실재 확인.
- **리뷰2**(B3 후): RL 스키마 재조준의 정보-보존 감사(cardinality 손실 은폐 여부).
- **리뷰3**(B6-Da 전, 컷오버): 소비자 0 판정 독립 검증 + 기본 ctor→펌프 플립의 프로덕션 표면 영향(DcgoMatch 시그니처 불변 확인) — R4 flip 직전 사용자 체크포인트(r4-careful-mode).

---

# 산출물 요약

- **문서**: `docs/audit/suite_retarget_4b_design_2026-07-18.md` (본 파일)
- **분류 카운트**(전수 470): ①펌프 **13** · ②a OLD-매치 **102** · ②b synth-머신 **64** · ③bare **274** · ④정적 **9** · ⑤발명물 **8**. **삭제-블로커 = ②a+②b = 166**; AdvancePhase/EndTurn 액션 소비자 **69**, throw-계약 소비자 **20**.
- **삭제 대상 표면 수**: 파일 6 + 멤버 표면 ~5(항2 AdvancePhase/EndTurn body·항4 마커·항7 브리지·항1부속·디스패처 OLD arm), 총 ~2,900줄. 즉시-삭제 무료 2건(소비자 0).
- **배치 수**: 7 (B0 즉시삭제/발명물 · B1 throw-계약 · B2 페이즈-진행 · B3 RL스키마 · B4 EndTurn-seam · B5 bare-재분류 · B6 판정+물리삭제).
- **예상 소요**: 재조준 ~166 스위트 = 카드계열/페이즈계열 병렬로 5~6 배치, 각 관련-스위트 게이트 1회 + 컷오버 전체 스위트 게이트. 신중 모드(리뷰 3지점) 기준 중대형.
- **리스크 상위 3**:
  1. **RL 인터페이스 굴곡**(B3): 액션 통화·관측 shape 재정의(S2 결정 A 판례)가 rl-env-parallel-track과 결합 — 정보-보존 단언 없이 재조준하면 cardinality 손실 은폐(false-green 함정 5). rl-env 트랙 본격화 전 저비용 창구.
  2. **synth 픽스처의 리걸-게이트 조용한 스킵**(B1/B5): RemoveField-직호출류 픽스처를 펌프로 감쌀 때 액션이 Illegal 스킵 → green이 계약 통과 아님(RD-R3-02 판례). 감쌈 후 리걸-등재 선단언 필수.
  3. **삭제-순서 원자성**(B6): 항2(AdvancePhase/EndTurn body)·항3(drain)·항4(마커)는 상호의존 원자 삭제 + 기본 ctor→펌프 플립(G1 근본 게이트)이 동시 — 부분 삭제 시 프로덕션 기본 매치 파손. shadow 테스트(R4S3c/R4P4)는 이 시점 은퇴(재조준 아님).

## §3.1b B1 실행 기록 1부 — 분석·분류 (2026-07-18, 코드 미착수)
**baseline**: throw-계약 소비자 20 확정(설계 일치) — 19 GREEN/1 RED(G7-005: DeferredChoicePendingException을 리졸버가 더는 안 던짐 = 계약 반쯤 은퇴의 실측 증거; 나머지 19 green은 OLD 스캐폴드가 B6 삭제 전이라 작동 중이기 때문).
**군 분류**: α=P-E synth OnEndTurn 4(GR-006·C-EoT2·W-EoTFIX·A4-Execute — 펌프 EndPhase drain으로 이식, GetSkillInfos 질의 단언은 retained substrate 무변) / β=삭제 파이프 9(C-Del 5·G3.5-F68·C-Atk 3 — 리걸-게이트 조용한 스킵 함정 주의, RD-R3-02 판례) / γ=activated-effect 3(B1-OncePerTurn·G7-005·E1-Parity(a)만 — suspended 단언→HasPendingChoice 번역+효과 결과 재이식 필수; P1r=하이브리드 승격) / δ=순수 계약-메커닉(G3.5-W7 ProviderReplaysAnswer 1테스트=부분 은퇴·W1b=이식 우선 검토·R4RL-03=B3 이관 정정).
**블로커 실측**: ST1_16 CardBaseEntity JSON 스켈레톤(cost/color 무) — 펌프-플레이 재조준은 카드 데이터 포팅부채에 게이트, 파일별 "OLD 반절단 vs 직교 부채" 판별 필요. G7-005 green 복원은 이 해소에 종속(미해소 시 직교 부채 재분류, 강제 green 금지).
**실행 순서**: α 클러스터 → 리뷰 게이트(창 발화 결과 재단언 실재) → β → γ → δ 은퇴-마킹.

## §3.1c B1-β 코디네이터 판정 (2026-07-19) — 스코프 축소 재분류
**적재 질문**: out-of-pump promote/park(throw-unwind 삭제-교체 파킹)가 컷오버 후 live인가? **판정=retained substrate(존치)**. 근거: ①리뷰3 P2-③ 실측 — 펌프 매치의 액션-후 드레인=GameFlowProcessor.RunToStableAsync가 수행, 그 중 개설 창=out-of-pump throw 경로(펌프-블로커 witness가 AS-IS-정합 고정) ②본 설계 삭제 표면 6파일에 GameFlowProcessor·DeferredChoiceProvider throw 경로·sink promote/park 미포함.
**귀결**: β 9 중 — C-Del-3C1/3C1B/3C1C/3C2B/POST·F68 효과부·C-Atk 3종의 synth 구동=retained-substrate 유닛 테스트로 **존치(무접촉·B1 제외)**, promote/park 마커 단언 보존. B1 throw-심볼 grep이 과포함(심볼 소비≠삭제-표면 소비). 잔여 진짜 블로커: G3.5-F68의 `new DcgoMatch(ctx)` OLD-ctor 드라이버 부분만(②a — B2로 이관). B1 잔여=γ(3: B1-OncePerTurn·G7-005·E1-Parity(a))+δ(은퇴-마킹: G3.5-W7 1테스트·W1b 검토). 삭제-블로커 카운트 166→재산정 필요(B2에서: "OLD 6파일 표면 직접 소비" 기준으로 재grep).

## §3.1d B1-γδ 실행 + 삭제-블로커 재산정 (2026-07-19, main HEAD=c71eab63, 메인 워킹트리·미커밋)

### (1) γ군 재판정 — §3.1c 기준(삭제-표면 6파일 직접 소비) 적용
6파일 실-소비 grep(주석·Path.Combine 소스-sniff 문자열 제외): **γ 3종 전부 삭제-표면 소비 0** → β C-Del/C-Atk와 동일한 retained-substrate 유닛(ActivatedEffectResolver + DeferredChoiceProvider + AutoProcessing). 처분:
- **B1-OncePerTurnInteractiveResume (4/4 green)** — retained-substrate·삭제-블로커 아님. 픽스처=Tfx 합성 카드(직접-배치, cost/def 데이터 없음)라 OnEnterFieldAnyone 펌프-플레이 불가. **존치**(펌프 재조준 불요·불가). 무변.
- **E1-Parity (3/3 green)** — (a) peek=`RevealSelectThenPlaySelectedEffect` 직구성 + DeferredChoiceProvider 순수 효과-유닛. 삭제-표면 0. **존치**. 무변.
- **G7-005.ActivatedDeferredChoice (RED→GREEN 복원)** — §3.1b "ST1_16 데이터 스켈레톤 블로커" **판정 오류 2건 정정**: ①ST1_16 실데이터는 로더 코퍼스 `cards.json`에 실존(`cardType:Option, color:Red, playCost:8, effect:"[Main] Delete 1 …", effectClass:ST1_16`) — §3.1b는 per-card 스텁 `ST1_16.json`(`todo:Skeleton only`)을 봤으나 로더(`CardBaseEntityLoader`)는 cards.json을 읽음. ②실제 red 원인은 데이터가 아니라 **DoneStartGame 게이트**(`ICardEffect.CanTrigger` :391-393가 게임 시작 전 활성효과 전량 스킵) — 픽스처가 Initialize만 하고 Setup을 넘지 않아 옵션 [Main]이 활성화 안 됨(resolver 0 반환, suspend 없음). **수정=`SetPhase(HeadlessPhase.Main)`** 1줄(W1b 확립 관용구, 설계 §5.5 F4 DoneStartGame-gate 소견). 단언 6개 무변(red→green, 강도 동일). 재조준 아님(삭제-표면 미소비)·retained ActivatedEffectResolver suspend/resume 구동 유지.

### (2) δ 은퇴-마킹 재판정 — §3.1c retained-substrate 기준
- **G3.5-W7.DeferredChoice `ProviderReplaysAnswer` (5/5 green)** — 검증 대상=`DeferredChoiceProvider.ChooseAsync` throw(무답)→pending 등록 + `BeginResolution`+재-ChooseAsync 답 재생. **이 out-of-pump throw+재생이 바로 §3.1c가 retained로 판정한 기제**(펌프 액션-후 드레인=GameFlowProcessor.RunToStableAsync가 소비). 순수 발명-계약 부분 없음. → **존치(은퇴 아님)**. 스위트 5테스트 전부 DeferredChoiceProvider+EffectScheduler suspend/resume=retained.
- **W1b-SkillWindowResume (all green)** — 검증 대상=`SkillWindowContinuation` 커서 + `MultipleSkills.ResumeAsync` + `AutoProcessing.ResumeSuspendedWindowsAsync` 창-루프 resume. 삭제-표면 6파일 미포함(AutoProcessing=AS-IS 미러 창 머신러리, 존치). §1.3 항8이 "throw-계약"으로 은퇴 예정 표기했으나 §3.1c가 throw 경로를 retained로 재프레임 → **존치(은퇴 아님)**. 무변.

### (3) 삭제-블로커 재산정 (166 → **73**)
기준=삭제-표면 6파일 직접 소비(주석/소스-sniff 문자열 제외), 테스트 프로젝트 단위 union:
| 삭제-표면 | 정밀 소비 시그니처 | 프로젝트 수 |
|-----------|-------------------|-----------:|
| MetadataActionProcessor AdvancePhase/EndTurn body | `HeadlessActionTypes.(AdvancePhase\|EndTurn)` 액션 통화 | **69** |
| HeadlessGameLoop OLD ctor/step | `new HeadlessGameLoop(` | 1 (G3.5-004) |
| HeadlessMainPhaseFlow invented eval | `new HeadlessMainPhaseFlow(`·`.ResolveTurnEndMinMemory`/`.DefaultMemoryPassValue` | (69에 포함 or) FAILd-07 1건 초과 |
| HeadlessEarlyPhaseFlow Unsuspend/Draw/Breeding | `new HeadlessEarlyPhaseFlow(` | E3-Witness 1건 초과 |
| 디스패처 OLD arm | `new HeadlessLegalActionDispatcher(` | FAILd-03 1건 초과 |
| EndOfTurnDrainedTurn 마커 | `EndOfTurnDrainedTurn` | **0** (엔진 내부 전용, 설계 §1.3 항4 일치) |
| **union(중복 제거)** | | **73** (69 액션통화 + 페이즈플로우-직접 4: E3-Witness·FAILd-03·FAILd-07·G3.5-004) |

**166→73 감소 근거**: 원 166(②a102+②b64)이 `RunToStableAsync`(43)·`AutoProcessing`·`ResumeSuspendedWindowsAsync` synth 머신러리를 블로커로 계상했으나, `RunToStableAsync`는 **`GameFlowProcessor`에만 정의**(HeadlessGameLoop 아님) = §3.1c retained substrate → 삭제-표면 아님. `HeadlessGameLoop` 타입 참조는 5프로젝트뿐이고 그중 2(G2A-006·G2E-005)는 Path.Combine 소스-sniff 문자열, 2(GR-001·R4P4)는 주석뿐, 실-ctor 소비는 1(G3.5-004). ⇒ **삭제-블로커 실체 = AdvancePhase/EndTurn 액션 통화 69 + 페이즈플로우-직접 4 = 73**. α 재조준 4종(GR-006·C-EoT2·A4-Execute·W-EoTFIX)은 union 부재(Pass로 재조준 완료 확인).

### (4) §3.3 트라젝토리 갱신 초안
- 삭제-블로커 모집단: ~~166(②a+②b)~~ → **73**(AdvancePhase/EndTurn 액션통화 69 + *PhaseFlow/GameLoop-ctor 직접 4). RunToStable/AutoProcessing/Resume 소비(≈93)=retained substrate 재분류(무접촉).
- fail-set 영향(본 배치): G7-005 base-red 1건 green 복원(DoneStartGame 픽스처 정합) → 순감 1; γ 잔여(B1·E1)·δ(W7·W1b)=green 존치 무변. 하한 트라젝토리(§3.3) 불변.
- 잔여 배치 재조준 규모 하향: B2(페이즈-진행)·B4(EndTurn-seam)가 이 73을 소진(≈AdvancePhase/EndTurn 통화 69가 주력); B1-γδ는 삭제-블로커 0 소진(전부 retained 존치, G7-005만 직교 픽스처 수리).

**리스크(본 배치 발견)**: C5-SecurityPreWindow 4테스트가 현재 HEAD에서 RED(직교; 마지막 테스트-커밋 e11acaba에선 408/408 green이었으므로 이후 키워드 창-재하우징/sink 변경으로 회귀 — 보안-witness 도메인=병행 Sonnet 트랙 인접). 본 배치(G7-005 retained 픽스처 1줄) 무관: 스태시 대조로 前/後 동일 red 실증. "+secwin" 게이트가 현재 red임을 코디네이터 인지 필요.

## §3.1e B2-c 총괄 — 액션통화 소비자 73 전원 처분표 (2026-07-19, main HEAD=4c928050 파생, 메인 워킹트리·미커밋)

c1(red 20 실측)·c2(green 전반 21)·**c3(green 후반 풀)** 를 합산해 삭제-블로커 73(§3.1d) 전원의 **terminal 처분**을 확정한다. 이 표가 B6-Da 소비자-0 게이트의 잔여 경로를 정의한다: 재조준 완료분만 통화 제거(currency=0 실증), 나머지는 각 배치가 소진할 때까지 통화 존치(=B6 블로킹 유지).

### (1) c3 실행 요약 (green 후반 풀)
- **처분 규칙**: c2 판례 사전판별(파일 읽기) — clean="drive-to-main+observe"만 F62 레시피(`CreatePumpDriven`+`DriveUntil(AtMainWaitOf)`) 재조준; 3배제 패턴(단일-step apply / Pass-통화 / 이중-전투해소 / 합성-hand 픽스처)은 시도 없이 or red화 즉시 B5.
- **재조준 완료 12** (통화 1→0·단언 수 전후 동일·green→green 실증): `C-Del-3C2BP-Witness · C1-DecodePartitionPre · C1-Witness · G12-004 · G2G-001 · G3.5-DA56 · G3.5-F68R · G3.5-S1 · G3.5-W6 · P1r · P1w · PRIM-P0.WouldBeDeletedWindow`. (전형=`AdvanceToMainAsync` preamble → observe; W6만 단언 11→10 = OLD `AssertEqual(1, advance.Length,"advance phase count")` 통화-불변식 은퇴 = §2.1 P-A 정당, "advance to main" 도달단언은 DriveUntil throw+잔존 AssertEqual로 보존).
- **재조준 시도→red 원복 14 → B5** (전부 합성-hand/단일-step/이중해소 개막-상이 실증, c2 판례 재확인): `G2E-001`(memory -3 vs 0=단일-step apply)·`G2E-002/G3.5-B1b/G3E-002`("card not in Hand"=합성-hand 픽스처가 펌프 auto-draw로 소실=RD-R3-02 조용한-스킵)·`G2E-003`(memory 단일-step)·`G2E-004`(pending-attack 통화)·`G2E-005`(Pass phase 통화)·`G3.5-005/008/C12/R2-1/W5/G9-062`(전투해소 삭제-outcome 상이=이중해소)·`G12-002`(deferred-choice pending 상이). 원복=`git checkout HEAD --`(HEAD green 복원)·B5 per-fixture 정합 몫.
- **사전판별 no-attempt 마킹 15**:
  - Pass-통화/멀티-fixture → B5: `G3.5-D6`(AdvancePhase를 breeding "decline" 통화로 **단언** — cutover 후 ResolveChoice(skip), 통화가 곧 검증대상 → B5 재작성 몫)·`G3.5-F68`(oldctor=3 멀티-fixture, DeletionReplacement 이중해소; §3.1c가 OLD-ctor 드라이버부만 이미 이관).
  - 페이즈플로우-직접 4: `FAILd-07`(HeadlessMainPhaseFlow.ResolveTurnEndMinMemory=EndTurn-min-memory seam → **B4**; §1.3 항5 AutoProcessing.TurnEndMinMemory 승격 후 re-point)·`FAILd-03`(new HeadlessLegalActionDispatcher 직접 = OLD arm 단위, §1.3 항11 → **B5** 펌프-dispatch re-point)·`G3.5-004`(대부분 RunToStable=retained 존치 + `new HeadlessGameLoop(context)` 단일 subtest만 삭제-bound → **B5** subtest은퇴/re-point)·`E3-Witness`(new HeadlessEarlyPhaseFlow.AdvanceAsync=DORMANT 항6, baseline-RED → **B5**).
  - 선재-red(HEAD RED, 직교 포팅/키워드 회귀 — 재조준 불가, B6까지 통화 존치) → B5: `G2G-002 · G2G-003 · G2G-004 · G3.5-C910 · R2-DeletionPipeline` (전투/보안 witness, C5 회귀와 동일 도메인=병행 Sonnet 인접)·`PRIM-P0.NewTimingsFire`(task 지정 red).

### (2) 73 전원 terminal 처분표 (69 액션통화 + 4 페이즈플로우)
| 처분 | 수 | 프로젝트 | 통화 상태 |
|------|---:|---------|-----------|
| **재조준 완료** | **21** | c3(12, 위 목록) + c2 clean 8(G3.5-C13/C14/C16/C46/C4D/C57/C821·G9-069) + c1 F62 | **currency=0** (B6 비블로킹) |
| **B6-은퇴** | 2 | G2A-006(OLD 디스패처 시퀀스=검증대상 소멸)·R4S3c-ShadowOldNew(shadow=존재이유 소멸, §2.2#7; 現 green) | 통화 존치→B6 삭제 |
| **B4 (EndTurn seam)** | 4 | G3.5-N1·GR-001·RD6-EndTurnSequence·FAILd-07 | 통화 존치→B4 |
| **B3 (RL 스키마)** | 9 | G11-002·G3.5-RL-A1/A3/A4b/B1/B2B3/C1/C2·R4RL-03 | 통화 존치→B3 |
| **B5 (per-fixture)** | 37 | c3-원복 14 + c3-마킹(D6·F68·FAILd-03·G3.5-004·E3-Witness·G2G-002/003/004·C910·R2-Del·NewTimingsFire) + c2-B5(G3.5-C2·N9·GR-004[B3/B5]) + c1/설계 red-기지 9(C5-SecurityPreWindow·C5-Witness·G3.5-007·A3·D1·D2·D3·N2·W4) | 통화 존치→B5 |
| **합계** | **73** | 69 액션통화 + 4 페이즈플로우(FAILd-03·FAILd-07·G3.5-004·E3-Witness) | — |

**B6-Da 귀결**: 재조준 완료 21만 통화 소멸. **잔여 52(B6-은퇴 2 + B4 4 + B3 9 + B5 37)가 소비자-0 게이트를 계속 블로킹** — 각 배치(B3/B4/B5) 소진 or B6 은퇴로 통화 제거될 때까지 물리 삭제 불가. c3는 소비자-0 경로를 **완전 열거**했다(불명 잔여 0).

**표 밖 각주 — `PRIM-P0.TriggerGrantSetSplice`**: task c3 목록에 있었으나 grep 실측 AdvancePhase/EndTurn 통화 0·OLD-ctor 0 → **삭제-블로커 아님**(73 미포함). baseline RED은 직교 포팅/키워드 부채(TriggerGrant 도메인)로 B5-인접 별도 추적, B6 비블로킹.

### (3) 게이트 실측
- **build**: 엔진 0오류. 워킹트리 수정=**재조준 12 테스트파일 한정**(엔진/src·CardEffect/·PILOT-* 무접촉 — 병행 Sonnet 트랙 격리 확인).
- **재조준 12 green** (전후 표): 전원 baseline green → 재조준 후 green, currency=0, 단언 수 동일(W6만 -1=OLD 통화-불변식 은퇴).
- **회귀 27/27 green**: c2 clean 8 · α4(A4-Execute·C-EoT2·GR-006·W-EoTFIX) · F62 · G7-005 · EXEMPLAR-T1/T2A/T2B/T3A/T3B/GLINK · PILOT-S1~S4 · R4S3a/b · R4R3-01/02.
- **shadow**: R4P4-ShadowRun **bit-identical**(2 OLD-vs-OLD)·R4S3c-ShadowOldNew OLD-vs-NEW **2/2 IDENTICAL** + **secwin IDENTICAL**(seed 404 winner 1/1·sec 0/0·동일 digest). (R4S3c 자체=green; §3.1d "+secwin red" 경고는 별개 프로젝트 C5-SecurityPreWindow에 국한, R4S3c 내부 secwin subtest는 통과.)

### (4) 남는 리스크
1. **B5 수율 부담 상향**: c3 green-후반 풀 clean 수율 = 12/26 ≈ 46%(c2 38%와 정합). 원복 14 + 마킹 23 = **B5 규모 37**로 확정 상향 — per-fixture 개막-정합(합성-hand→펌프 실-draw 정합·전투 이중해소→단일 자동흐름·Pass/EndTurn 통화 재작성)이 4b 최대 잔여 노동. B6은 B5 완주에 강하게 종속.
2. **선재-red 6(G2G-002/003/004·C910·R2-Del·E3-Witness) 도메인 = 병행 Sonnet 보안/전투 witness 인접**: 이들의 red 원인이 c3 통화와 무관(직교 회귀)이나 B5에서 통화 제거하려면 먼저 red 원인(키워드 창/sink 회귀) 해소 필요 → B5가 포팅부채 상환과 얽힘. C5 회귀와 공통 근원 가능성(코디네이터 교차 확인 권고).
3. **FAILd-07 B4 재분류**: EndTurn-min-memory를 B4 seam으로 넘겼으나, HeadlessMainPhaseFlow.ResolveTurnEndMinMemory → AutoProcessing.TurnEndMinMemory 승격(항5)이 B4 전에 완료돼야 re-point 가능 — B4·항5 삭제 순서 의존.

## §3.4a 리뷰 지점 1 결과 (2026-07-19, B2 재조준 21건 적대 감사): GO-with-P1
**P0 0**(단언 감소 4건 전부 커밋 명시 — 은폐 없음). 렌즈 5종: 은닉 실룰 소실 없음·번역 충실(음성 단언 비-vacuous 확인)·항진 probe 미발견·픽스처 전제 이상 2건만·처분 오분류 미발견(양방향).
**P1-1(B4 편입)**: W-EoTFIX(a)/A4의 once-per-turn 재발화 가드가 행동검증→구조검증 강등 — 단일 펌프 turn-end는 재수집이 없어 캡 분기 미실행(커밋의 등가 근거 일부 부정확). 상환=B4에서 **2-턴 재발화 witness** 신설(P1 연속 두 turn-end로 캡 E2E 복원)+해당 주석 정정.
**P2**: ①F62 잔존 dormant-guard=준-항진(기록) ②α4 수집-카운트의 BT1_028-inert 숨은 전제(주석 권고) ③P1r legality 검증기 소실 미기재(무해·기록).

## §3.1f B5-c4 실행 기록 — 라이브-전투 재구축 실증 + ④ bare + ⑤ 판별 (B5 마감, 2026-07-19, main HEAD=52169578, 메인 워킹트리·미커밋)

스코프: ③ 이월 6(G3.5-005·C12·R2-1·W5·G9-062·F68) + ④ bare 7(FAILd-03·G3.5-004·G3.5-A3·G3.5-D6·G2G-002/003/004) + ⑤ 잔여 판별 11(E3-Witness·C910·R2-DeletionPipeline·NewTimingsFire·C5-SecurityPreWindow·C5-Witness·G3.5-007·W4·D1-BatchId·D2-Witness+N2) + D3 라벨 미스터리. **코드 변경 = 0**(라이브-재구축 실증은 G3.5-005에서 시도→red→`git checkout HEAD --` 원복; 순 워킹트리 무변). **flip = 0.**

### (0) 선행 조사 — AttackProcess 선언-경계 force-end 정확한 게이트 (c3 권고 완료)
- 게이트: `AttackProcess.cs:246`(및 :316 else-arm) `Commons.IsPermanentExistsOnBattleAreaDigimon(attacker)` = false → **"Attacker not a battle-area Digimon at declaration."** force-end(Resolved 직행).
- 요건 분해: `CardEffectCommons.cs:3712` `IsPermanentExistsOnBattleAreaDigimon = IsPermanentExistsOnBattleArea(p) && p.IsDigimon`. `IsPermanentExistsOnBattleArea`(`:3627-3642`) = **`ZoneMover.GetCards(OwnerId, BattleArea).Contains(InstanceId)`**(SnapshotZone override 외) ∧ `p.IsDigimon`(TopCard.CardType=="Digimon"). ⇒ 합성-metadata permanent(`new Permanent` metadata만·존 미등록 or 비-Digimon def)는 선언 즉시 force-end.
- **라이브-Permanent 최소 레시피**(F68 `PlaceRealCard`, tests/G3.5-F68…:435): 실 인스턴스 Upsert(DefinitionId=Digimon def) → `ZoneMover.MoveAsync(→BattleArea)`(존 등록) → (키워드 수집 시) `CardEffectRegistrar.RegisterCard`. 펌프에선 hand 0(`NormalizeForPump`)이므로 `None→BattleArea` 스테이징(G3.5-008 c3 판례).

### (1) ③ 이월 6 — 라이브-전투 재구축 실증 결과: **6 전원 R1/R6-게이트 확정(재조준 불가)**
- **실증(G3.5-005 대표)**: F68/G3.5-008 판례대로 `new(context)`→`CreatePumpDriven`+`DriveUntil(AtMainWaitOf)`, 공격자/방어자/블로커를 실-Digimon 인스턴스로 `None→BattleArea` 스테이징, 펌프 legal `DeclareAttack` 레인으로 선언. **선언 성공(DeclareAttack 레인 비-vacuous 확증)** — force-end은 해소됨. 그러나 펌프 드라이브의 **전투 결과가 재현 안 됨**: (a) target attack — attack은 clear(Phase→None)되나 defender가 Trash 미착지(삭제 outcome 상이), (b) direct attack — 시큐리티 Trash 미착지, (c/d) blocked/skipped — **블록 창 자체가 pending으로 안 뜸**(HasPendingChoice=false). 4/7 red(선언은 뜸=RD-R3-02 조용한-스킵 아님, 실제 outcome 갭).
- **근원**: 전투 DP는 `BattleResolver.cs:759` = `Permanent(...).DP`(printed BaseDP+연속 IChangeDPEffect fold, `Permanent.cs:376`); metadata `dp`는 **presence-gate만**(`:750` 읽고 `:758` `_ = baseDp;`로 폐기). 펌프 캐이던스 하 DP-fold(`GameContext.Players_ForTurnPlayer` 스캔·AmbientMatchContext 스코프)가 OLD 직구동과 상이 outcome을 냄 + 블록 후보 창은 metadata `HasBlockerKey`가 아닌 실-Blocker 키워드를 요구. ⇒ **c3 "이중해소 = 삭제-outcome 상이" 독립 재확인**. metadata-라이브만으론 불충분; 실-printed-DP def + 실-키워드(R6 카드층) + 연속-DP fold 펌프-정합(R1)이 선행돼야 함.
- **처분**: 6 전원 **OLD-ctor 통화 존치 → B6-블로킹, R1/R6까지 재조준 불가**. `red화=원복` 규율대로 G3.5-005 원복(7/7 green 복원). C12·R2-1·W5·G9-062·F68은 동일 근원(전투/시큐리티 outcome이 metadata 아닌 Permanent.DP/키워드 fold에 종속)이라 재시도=동일-결론 churn으로 판단, 미시도 마킹.

### (2) ④ bare 7 — 처분
| 픽스처 | 現 | 통화 | 처분 |
|--------|----|------|------|
| FAILd-03.CanNotMove | green | `new HeadlessLegalActionDispatcher()`(OLD arm, §1.3 항11) | 존치·B6 펌프-dispatch re-point(red 아님) |
| G3.5-004.GameFlowProcessor | green(6) | RunToStable(retained)+`new HeadlessGameLoop(context)` 1 subtest | **GameLoop subtest B6-삭제대상 검증=은퇴-마킹**(나머지 RunToStable subtest 존치·retained) |
| G3.5-A3.BlockerSuspend | **red**(NRE) | `new(context)`+AdvancePhase | 실부채: bare BlockTiming resolve NRE(블록-창 회귀, C5/Sonnet 도메인 인접). 재조준 불가·정밀마킹 |
| G3.5-D6.BreedingChoice | green(3) | `AdvancePhase`를 breeding-decline로 **단언**(:45) | 통화=검증대상 → B5 assertion 재작성 몫(cutover 후 `ResolveChoice(skip)`); 코드 재작성은 펌프 breeding-decline 표면 확정 후 |
| G2G-002/003/004 | **red** | `new(context)`+AdvancePhase | ⑤ 교집합 실부채(block NRE·battle DP delete·strike count). 구조 무변·red판별 기록(선재 회귀) |

### (3) ⑤ 잔여 판별 11(+N2) — red 원인 판별(OLD-artifact→flip vs 실부채→정밀마킹)
전원 **실부채(엔진 갭)** — 펌프 재구축이 same-engine이라 driver-swap으로 안 사라짐(G3.5-005 실증이 전투/DP-fold 갭을 독립 입증). flip 가능분 = **0**.
| 픽스처 | 現 | red 근원(무엇이 고쳐져야 green) |
|--------|----|-------------------------------|
| E3-Witness | red(1) | `HeadlessEarlyPhaseFlow.AdvanceAsync`(항6 DORMANT)의 natural-unsuspend가 opp-top-security-trash 효과 미발화(2→1 안 됨). EarlyPhaseFlow=B6-삭제대상 → 효과 타처 커버 시 B6-은퇴 후보, 아니면 unsuspend 트리거 배선 |
| C910(ExecuteCollision) | red(2) | Collision 키워드-granted 강제블록 NRE + per-defender CanNotAffected 가드(plain defender 강제 안 됨). Collision 키워드 창 회귀 |
| R2-DeletionPipeline | red(2) | 2-카드 배치삭제 PRE 창(A) 미개설·batch-defer 파킹 갭(field-seam 삭제-PRE) |
| PRIM-P0.NewTimingsFire | red(3) | OnEndBattle·WhenRemoveField·OnDigivolutionCardDiscarded 신 timing 미배선(효과 0 memory). task-지정 red |
| C5-SecurityPreWindow | red(3+) | 시큐리티 would-be-deleted PRE 창 미개설(§3.1d 회귀 실증: 키워드창-재하우징/sink 변경) |
| C5-Witness | red | Barrier/Evade grant가 시큐리티-전투 패배 시 미발동(C5 동일 도메인) |
| G3.5-007 | red(NRE) | continuous cannot-block이 블로커 후보 제거 시 NRE(연속-제한 블록 도메인, A3와 동종) |
| G3.5-W4(SecurityEffectWiring) | red(2) | OnSecurityCheck 창이 revealed 카드에 미발화(시큐리티 창 회귀, C5 도메인) |
| N2(ContinuousBattleDp) | red(3) | 연속 +/−DP가 전투 outcome 미반전(field-seam continuous-DP fold 갭 — c3 판별 재확인·G3.5-005 실증 corroborate) |
| **D1-BatchId** | **green** | RunToStable-only = **retained substrate, 삭제-블로커 아님**(6-표면 소비 0) → red-기지 오분류·통화=0 |
| **D2-Witness** | **green** | 동상 RunToStable-only(8×) = **삭제-블로커 아님**·통화=0 |

**'D3' 라벨 미스터리 해소**: D-정상화 시리즈는 D1-BatchId/D2-Witness **2개뿐**(D-3 witness 부재 — memory: D-1 delete batch-id·D-2 leave-field collapse만 존재). §3.1e(2) red-기지 9의 "D3"는 **팬텀 라벨**; 유일 D3-명 픽스처 = `G3.5-D3.TriggerOrdering`(G3.5 시리즈, 트리거 순서). 이것은 `new(context)`+AdvancePhase 통화 소비 = **실 B5 블로커**(red: turn-player-first + mandatory-before-optional 순서 미구현/회귀). ⇒ **c1 "실디렉터리 미발견"의 정체 = red-기지 9가 D1·D2(green 非-블로커) 2 + D3(팬텀→G3.5-D3.TriggerOrdering) 로 오구성됨**. 실 red 블로커 red-기지 = 7(C5-SecurityPreWindow·C5-Witness·G3.5-007·A3·G3.5-D3·N2·W4), 非-블로커 2(D1·D2).

### (4) 37 전원 terminal 처분 갱신 (c4 반영) — B6-Da 입력
| 처분 | c3(§3.1e) | **c4 갱신** | 통화 상태 |
|------|----------:|-----------|-----------|
| 재조준 완료(currency=0) | 21 | 21 (c4 flip 0) | B6 비블로킹 |
| **비-블로커 재분류(RunToStable-only)** | — | **+2**(D1-BatchId·D2-Witness) | 애초 통화 0(retained substrate)=B6 비블로킹 |
| B6-은퇴 | 2 | 2 (+G3.5-004 GameLoop subtest·E3-Witness EarlyPhaseFlow=은퇴 후보) | 존치→B6 |
| B4(EndTurn seam) | 4 | 4 | 존치→B4 |
| B3(RL 스키마) | 9 | 9 | 존치→B3 |
| B5(per-fixture) | 37 | **35 실블로커**(37 − D1·D2 비블로커 2) 중: **R1/R6-게이트 combat 6**(005·C12·R2-1·W5·G9-062·F68) + **실부채 red**(A3·G2G-002/003/004·C910·R2-Del·NewTimingsFire·C5-SecurityPreWindow·C5-Witness·G3.5-007·W4·N2·G3.5-D3·E3) + **기계적**(FAILd-03 re-point·G3.5-004 subtest·D6 재작성) + c3-원복 잔여(G2E계열·G3.5-B1b·G3E-002·G12-002)+c2-B5(C2·N9·GR-004) | 존치→B5(단, combat 6은 R1/R6 선행) |

**B6-Da 귀결(c4)**: c4는 **통화 소진 0**(flip 0). 그러나 **삭제-블로커 모집단 −2**(D1·D2 = RunToStable-only 非-블로커로 확정). **결정적 발견 = B5 잔여의 재조준 불가성**: ③ combat 6은 R1(연속-DP fold 펌프-정합)/R6(실-DP·실-키워드 카드) 선행 없이 펌프 재조준 불가(라이브-Permanent metadata만으론 outcome 미재현 실증); ⑤ red 12는 전부 실 엔진 갭(block/security/키워드 창·field-seam DP-fold·신 timing 배선)로 병행 키워드/보안 트랙(C5 회귀 동근원) 상환에 종속. ⇒ **B6 물리삭제는 driver-swap 재조준만으론 도달 불가** — combat 6은 R1/R6 게이트, red 12는 포팅/키워드 부채 게이트. 근-미래 통화 감소분 = D1/D2(비블로커 확정)·G3.5-004 GameLoop subtest 은퇴·FAILd-03/D6 기계적 re-point 뿐.

### (5) 게이트 실측
- **build**: 엔진 0오류(경고 1771 무증가). 워킹트리 순변경 0(G3.5-005 실증 원복 완료·`git checkout HEAD --`).
- **회귀 재확인**: G3.5-005 원복 후 7/7 green. c1~c3 재조준 대표(§3.1e c3 회귀 27 중 EXEMPLAR-T1/GLINK 등) 무영향(코드 무변).
- **리스크 갱신**: (a) B5 combat 6 = R1/R6 강결합(4b driver-swap 스코프 밖) — B6 게이트가 B5 완주가 아니라 R1/R6 완료에 종속(트라젝토리 §3.3 하한 재고 필요). (b) ⑤ red 12 = C5-SecurityPreWindow 회귀와 공통 근원(키워드 창-재하우징/sink)일 가능성 — 병행 Sonnet 보안/전투 트랙과 교차 상환 권고. (c) '이월 6' 라이브-재구축은 F68 관용구로 **선언은 뚫리나 outcome 미재현**이 확정 — 향후 시도는 R1 DP-fold 펌프-정합 실증을 먼저 게이트로.

## §3.4b 리뷰 지점 2 결과 (2026-07-19): GO-with-P1 — c4 구조 판정 반증(P0)
**B3/B4=clean GO**(A1 probe 존치·GR-001 등가 번역·RD6 정직 마킹 전부 타당). B5 재조준 역학=건전. **반증된 것=c4의 로드맵 추론**:
- **P0**: "combat 6=R1/R6 게이트·재조준 불가"는 오진 — Permanent.HasDP(:142)는 printed-DP 미참조(Digimon-타입이면 true), BaseCardDP(:1005)는 인스턴스 metadata dp 우선 → 합성-def 전투가 실 BattleResolver로 정상 해소(증거 3: 현행 green G3.5-005 자체·F68 PlaceRealCard 합성-def 풀전투 판례·BlockTiming:271 metadata 키). c4 실패는 driver-mechanical이지 DP/코퍼스 의존 아님. §3.1f 해당 결론 철회, F68 관용구 재실험이 결정 실험.
- **P1**: ⑤ NRE 클러스터 6(A3·C910·007·G2G×3)의 "실부채 확정"은 반증된 방법 상속 — "미해결(F68 differential 대기)"로 강등. 창/배선 계열(C5×2·N2·W4·NewTimings·R2-Del·E3)은 부채 유지(단 미분화 주기).
- **P2**: B5 누적 단언 net -11의 개별 정당성은 확인되나 총계 원장 미비 — per-file 제거→대체 원장 요구; RD6 t1/t2 서술 긴장 1건.
**B6 도달 경로(확정)**: ①G3.5-005를 F68 관용구로 재실험(최저비용 결정 실험) ②NRE 6 differential ③기계 잔여(D1/D2 재분류·G3.5-004 subtest 은퇴·FAILd-03/D6 re-point) ④잔여 진짜 부채=키워드/보안 트랙 몫(그것이 실 B6 게이트).

## §3.4c B5-c5 실행 기록 — 결정 실험 GO + NRE differential 판별 (2026-07-19, main HEAD=b84c5ce0 파생, 메인 워킹트리·미커밋)

**엔진/src 변경 = 0**(전부 테스트-파일 재조준). build 엔진 0오류. shadow 무영향 실증(아래).

### (1) 결정 실험 — G3.5-005 target-attack을 F68 관용구로: **재현 성공 → GO**
§3.4b P0 재실험. `CreatePumpDriven` + F68 `PlaceRealCard` 관용구(합성 Digimon def의 def-레벨 dp·CardType:Digimon·인스턴스 dp·None→BattleArea·`CardEffectRegistrar.RegisterCard`) + **펌프 DeclareAttack 리걸 레인**(직접 `AttackController.DeclareAttack` 아님)으로 재조준. **결과: 패자(defender 7000)가 실 Trash 착지 + `BattleResolver.DeletedByBattleKey=true`**. c4 실패의 정체 = **driver-mechanical**: c4는 직접-컨트롤러 seam(또는 mis-staging)으로 구동했고, 펌프 리걸-레인 seam이 정답. §3.1f "combat 6 = R1/R6 게이트·재조준 불가" 결론 **철회 확정**(P0 반증 실측).
- **false-green 자가감사(비-vacuous 증명)**: 삭제-가능 control 신설 — 약공격자(5000) vs 강 suspended target(9000)은 **공격자가 죽고 target 생존·Trash 미착지**. 펌프 전투가 실제 DP를 읽음을 실증(항진 아님). + `DeletedByBattleKey` 메타(삭제=전투에 의함, rule-sweep 아님).

### (2) G3.5-005 전체 재조준 완료 (8/8 green, 통화=0)
4 combat subtest(target/direct/blocked/skipped) 전원 OLD-ctor(`new DcgoMatch`)+`AdvanceToMainAsync`(AdvancePhase 통화) → `CreatePumpDriven`+펌프 main-대기 도달+펌프 DeclareAttack 레인으로 재조준. **AdvancePhase/EndTurn 액션 통화 제거(currency=0)**. 단언 전량 보존 + control subtest 1 신설. 잔여 `AttackController.DeclareAttack` 1건은 `AttackPipelineAdvancesPhaseByPhase`(bare AttackPipeline 유닛 테스트 = ③, 액션-통화 비소비)뿐 — 삭제-블로커 아님. 발견: 펌프 StartGame이 시큐리티 5장을 자체 딜(NormalizeForPump 무관, EXEMPLAR 판례 재확인) → direct-attack 픽스처는 딜된 시큐리티 clear 후 staged 1장만 남겨야 정확.

### (3) NRE 6 differential 판별표 (artifact→flip vs 실부채)
동일 관용구/원인 렌즈 = `DcgoMatch.StepAsync`(:227-235) 문서화된 NRE: bare 소비자가 `BlockTiming.GetBlockerCandidates → Permanent.HasCollision → …GManager.instance`를 **ambient scope 밖**에서 호출 → null NRE. 처방 = 호출부를 `AmbientMatchContext.Enter`로 감쌈(펌프는 StepAsync가 self-scope). §3.4b P1 실증 확정:

| 픽스처 | base | 실패 성격 | **판별** | 1줄 처방 |
|--------|------|----------|---------|---------|
| **G3.5-A3** | red 4/4 | NRE(block-timing) | **artifact→FLIP** (실증: 4/4 green, **완전 재조준 유지**=base-red green 복원) | bare BlockTiming 호출부를 `AmbientMatchContext.Enter`로 감쌈 |
| **G2G-002** | red 6 | NRE(block-timing)×6 | **artifact→FLIP** (probe 실증: 1 subtest wrap→green, 원복) | 동상 ambient wrap |
| **G3.5-007** | red 1 | NRE(GetBlockerCandidates) | **artifact→FLIP** (probe 실증: wrap→7/7 green, 원복) | 동상 ambient wrap |
| **G2G-003** | red 1 | NRE(block-timing) | **artifact→FLIP** (probe 실증: wrap→10/10 green, 원복) | 동상 ambient wrap |
| **G3.5-C910** | red 2 | NRE(S4 keyword-collision) + **logic assert(K3)** | **MIXED=실부채(Collision 키워드)**: K3 subtest는 **이미 ambient wrap 보유**(:213-216)에도 "plain defender 강제 실패" — keyword-granted Collision(new-model `CollisionClass`, EffectRegistry 브릿지 無=stage-B RED)이 강제블록 미발동 | NRE 반쪽은 wrap로 flip 가능하나 load-bearing 단언은 **Collision 키워드 rehousing 실상환** 필요(driver-swap 무효) |
| **G2G-004** | red 2 | **strike-count logic(비-NRE)** | **실부채(SecurityResolver)**: `StrikeKey` 메타 무시하고 항상 1장 체크(strike:2→1·strike:0→1). block/collision NRE 아님 | SecurityResolver가 piercing/strike 수(StrikeKey 또는 live SecurityAttack 키워드)를 존중하도록 **실상환** |

**differential 귀결**: NRE 6 중 **4(A3·G2G-002·007·G2G-003) = ambient-scope artifact→FLIP**(§3.1f "6 전원 실부채" 반증 — §3.4b P1 확정), **2(C910 Collision·G2G-004 strike) = 실부채**(둘 다 키워드/보안 창 도메인 = §3.4b P1 "창/배선 부채 유지"와 정합). A3는 완전 재조준 유지(green 복원); G2G-002/007/G2G-003는 probe 후 원복(HEAD 유지); C910/G2G-004 무접촉(실부채 마킹).

### (4) ③ 잔여 combat 5 처분
- **G3.5-005 = 완전 재조준(위 (2))**. ③ combat 재조준 가능성의 정본 증명.
- **C12·R2-1(G3.5-R2-1)·W5(G3.5-W5)·G9-062 = 재조준-가능·미접촉(green-on-OLD-ctor 유지)**: 전부 통화=`new DcgoMatch`+AdvancePhase이나 **manual `BattleResolver.ResolveAsync` 직호출 패턴**(blocker로 attack pending 유지 후 수동 해소) — 펌프 auto-drive와 단언-스타일(result-object vs zone) 충돌 → "단언 보존" 원칙상 zone-단언 재작성 없이는 churn. G3.5-005(펌프 auto-drive 완주형)와 NRE-substrate flip(block/battle/security가 펌프-ambient 하 정상)이 이미 재조준 가능성을 실증하므로, 이들은 **currency-only 기계 follow-on(NOT R1/R6-게이트)**로 마킹. red화=원복 준수(무접촉=green 유지).

### (5) 기계 잔여 (Step 3)
- **G3.5-004 GameLoop subtest 은퇴 완료**: `GameLoopDrainsThroughFlowProcessor`(유일 live `new HeadlessGameLoop(` 소비, §3.1d) 물리 삭제 — RunToStable subtest(EmptyContext/LoopResolves/LoopPauses)가 flow-drain 커버 유지. **5/5 green**. `grep -rl "new HeadlessGameLoop(" tests` = **0**(HeadlessGameLoop ctor B6 delete-gate 청산).
- **D1-BatchId·D2-Witness = 비블로커 재확정**(§3.1f(3) 재확인): RunToStable-only 소비 = retained substrate, 삭제-표면 6파일 소비 0 → **통화=0(애초 비블로커)**. §3.1e(2) red-기지 오분류 정정 유지.
- **FAILd-03(new HeadlessLegalActionDispatcher OLD arm)·G3.5-D6(AdvancePhase breeding-decline 단언)** = B6 펌프-dispatch re-point / cutover 후 `ResolveChoice(skip)` 재작성 몫으로 존치(펌프 breeding-decline 표면 확정 후) — 이번 배치 미변경.

### (6) 게이트 실측
- **build**: 엔진 0오류(src 무변경). 워킹트리 수정 = 테스트 3파일(G3.5-005 재조준·A3 green복원·G3.5-004 subtest 은퇴)만.
- **회귀**: G3.5-005 8/8·A3 4/4·G3.5-004 5/5·C1-Witness 4/4·G2G-001 10/10·G3.5-W6 4/4·P1r(양)·EXEMPLAR-T1 18/18·EXEMPLAR-GLINK 5/5 = 전원 green.
- **shadow**: R4P4-ShadowRun **bit-identical**(2 OLD-vs-OLD)·R4S3c-ShadowOldNew **2/2 IDENTICAL**·**secwin IDENTICAL**(seed 404 winner 1/1·sec 0/0·동일 digest). src 무변경이므로 엔진-행동 shadow 자명 무영향 실증.

## §3.1g B5 P2 상환 — per-file 제거 단언→대체 관측 원장 (리뷰2 P2)

리뷰2 P2 요구(B5 누적 단언 net 총계 감사 가능화). c1~c4 개별 정당성은 각 커밋/기록에서 확인(§3.4b P2: net -11, 개별 정당). c5(본 배치) per-file 원장 + c1~c4 요약:

| 배치 | 파일 | 제거 단언 | 대체 관측 | net | 근거 |
|------|------|----------|----------|----:|------|
| c5 | G3.5-005 | (0 제거) | +3 신설(control: 약공격자 死·강target 生·target Trash 미착지) + DeletedByBattleKey(기존) | **+3** | false-green guard 강화; 4 combat 단언 전량 보존 |
| c5 | G3.5-A3 | (0 제거) | 0(ambient wrap만; 4 subtest 단언 무변) | **0** | base-red green 복원 |
| c5 | G3.5-004 | -3(GameLoopDrains: HadPendingEffects·ResolvedEffectCount·ResolveCalls) | RunToStable subtest 3종이 flow-drain 등가 커버(EmptyContext/LoopResolves/LoopPauses) | **-3** | 검증대상=HeadlessGameLoop ctor(B6 삭제-표면) 소멸 → 은퇴; 관측은 retained RunToStable에 존치 |
| **c5 소계** | | -3 | +3 | **0** | 제거=은퇴(대체 커버 실재)·추가=guard |
| c1~c4 | (기록: §3.1b~§3.1f) | (누적 net −11) | 각 커밋 명시(W6 −1 통화-불변식 은퇴·재조준 12 단언보존·c4 순변경 0 등) | **−11** | §3.4a 리뷰1 P0=0(은폐 없음)·§3.4b P2 개별 정당 확인 |
| **B5 합계** | | | | **−11** | c5 net 0 → 총계 불변; 제거분 전량 "검증대상 소멸(은퇴)+대체 커버" 또는 "통화-불변식 은퇴" |

**감사 결론**: B5 전체 단언 net −11의 제거분은 (a) 발명물/OLD-표면 검증대상 소멸(은퇴, 대체 witness 실재) 또는 (b) OLD 통화-불변식(AdvancePhase count 등) 은퇴 — 실룰 검증 소실 0. c5는 오히려 +3/-3 = net 0(guard 강화·은퇴 상쇄).

## §3.4d 배치 A-1 실행 기록 — 기계 잔여 소탕 (2026-07-19, main HEAD=99fb71e4 파생, 메인 워킹트리·미커밋)

**엔진/src 변경 = 0**(전부 테스트-파일 재조준·re-point). build 엔진 0오류(경고 무증가). shadow 무영향(src 무변경). c5의 결정 실험 판례(§3.4c: G3.5-005 펌프 legal-lane)를 남은 combat·NRE·기계 좌석에 확산.

### (1) ③ 잔여 4 재조준 완료 — c5 G3.5-005 판례 적용, 통화=0
c5가 "manual BattleResolver/SecurityResolver.ResolveAsync 패턴·result-object 대 zone 충돌"로 미룬 4종을, F68 관용구(`CreatePumpDriven`+`StagePermanent`(합성 Digimon def·인스턴스 dp·None→BattleArea·`CardEffectRegistrar.RegisterCard`)+펌프 DeclareAttack legal-lane)로 완전 재조준. **전원 green→green**(base green 유지), OLD-ctor(`new DcgoMatch`)·AdvancePhase 통화 제거.
| 픽스처 | 前 | 後 | 재조준 방식 | 단언 처리 |
|--------|----|----|-------------|-----------|
| **G3.5-C12**(Iceclad) | green 5/5 | **green 5/5** | 펌프 target-attack auto-drive; StagePermanent에 `SourceIdsKey`·`HasIcecladKey` 확장 | `BattleResolutionResult.AttackerDeleted/DefenderDeleted` → **동일 단언 텍스트 보존**, 부울 소스만 result-object→zone(패자 Trash 착지) 재조준. Iceclad source-count 비교가 펌프 하 재현 확증 |
| **G3.5-R2-1** | green 3/3 | **green 3/3** | field=펌프 target-attack, security=펌프 direct-attack; `ContinuousRestrictionGate` prevent-deletion 등록 무변 | zone 단언(BattleArea/Trash) 이미 관측형 → **전량 보존 verbatim** |
| **G3.5-W5** | green 7/7 | **green 7/7** | 펌프 direct-attack; 딜된 security clear 후 N장 스테이징(top-down) | `SecurityResolutionResult.*` 진단 result-object 은퇴 → zone/metadata 관측 재소싱(각 inline 명시). strike-stop·non-Digimon-no-battle·sources-trashed·Jamming 생존 전원 펌프 재현 |
| **G9-062** | green 3/3 | **green 3/3** | battle 2 subtest=펌프 target-attack(삭제 outcome-or-OnDestroyedAnyone 창까지 드라이브); sweep subtest=RunToStable(retained 존치·무변) | zone/DP-scan/창 단언 verbatim; Ascension 창 pending 관측 위해 pending-or-cleared 드라이브 |

**결정 실험 판례 확대 검증**: c5는 G3.5-005 1종으로 combat 재조준 가능성을 실증했으나, A-1은 **field battle·security battle·continuous-DP fold(G9-062 buff-drop)·Iceclad source-count·prevent-deletion replacement·strike-stop·비-Digimon security** 전 축을 펌프 하 재현 — §3.1f "combat 6 = R1/R6-게이트" 결론이 **driver-mechanical 오진이었음을 6종째 독립 확증**(F68 6번째 confirm).

### (2) NRE wrap 3 착지 — base-red flip(A3 판례), 통화 존치
c5가 probe 후 원복한 3종의 `AmbientMatchContext.Enter` wrap을 실제 착지. bare BlockTiming/GetBlockerCandidates가 `Permanent.HasCollision → GManager.instance`를 ambient scope 밖 호출 → NRE, wrap이 해소. **단언 무변(A3 판례)·통화 존치**(BlockTiming/BattleResolver API 단위 테스트 = ③-형, AdvancePhase-to-main preamble 유지).
| 픽스처 | 前 | 後 | flip |
|--------|----|----|------|
| **G2G-002** | red(6 NRE)/4 green | **green 10/10** | +6 base-red subtest flip |
| **G2G-003** | red(1 NRE)/9 green | **green 10/10** | +1 |
| **G3.5-007** | red(1 NRE)/6 green | **green 7/7** | +1 |

**잔여 red 정밀 마킹**: 세 스위트 모두 **잔여 red 0**(wrap이 전량 해소). G2G-002 collision subtest는 metadata `HasCollisionKey`(BlockTiming:271) 소비 = wrap만으로 green(§3.4c C910 K3의 keyword-granted Collision 실부채와 별개 — metadata 경로는 정상). base fail-set: **3 프로젝트 red→green**(G2G-002·G2G-003·G3.5-007).

### (3) FAILd-03·G3.5-D6 re-point — 펌프-dispatch 좌석, 통화=0
c5가 "펌프 breeding-decline 표면 확정 후"로 미룬 2종을, 확정된 펌프 breeding 좌석(TurnStateMachine.BreedingPhaseAsync:333 `ChoiceType.BreedingDecision` choice-pause, minCount0/maxCount1/canSkip)으로 re-point.
- **G3.5-D6**(green 3/3 → **green 3/3**): CreateValidated+AdvancePhase → CreatePumpDriven; breeding=BreedingDecision 펌프 choice. **"AdvancePhase=decline" 검증대상 단언 = 번역(검증 의도 보존)**: decline은 이제 choice의 SKIP 레인(canSkip), hatch는 `breeding:act` ResolveChoice candidate. 핵심 검증("breeding은 auto-hatch 아닌 player DECISION")은 BreedingDecision pending choice로 정확 보존. AdvancePhase 통화 제거.
- **FAILd-03**(green 2/2 → **green 2/2**): `new HeadlessLegalActionDispatcher()`(디스패처 OLD arm) → 펌프. Mulligan pause에서 B(movable breeding Digimon) 스테이징 후, breeding 점유(CanHatch=false)이므로 BreedingDecision은 MOVE 합법 시에만 개설 → "move offered"=BreedingDecision candidate label "move" 개설, "move NOT offered"=펌프가 breeding 자동통과해 main-wait 도달. CanNotMove 게이트는 펌프 `Player.CanMove → Permanent.CanMove`(AS-IS ICanNotMoveEffect scan)가 그대로 존중. **디스패처 OLD arm 소비자 1→0(청산)**.

### (4) §3.1e 총괄표 최종 갱신 — A-1 후 통화 소비자 잔존
A-1은 재조준 완료 21(§3.1e) 위에 **+6 통화 소비자 제거**(currency=0 도달): C12·R2-1·W5·G9-062·D6(AdvancePhase 5) + FAILd-03(디스패처 OLD arm 1). 통화 소비자 잔존 = **재조준 완료 27**(21 + A-1 6). B6-은퇴 2·B4 4·B3 9·B5 잔여는 §3.1e(2) 처분 유지(단, B5에서 A-1 6 차감). **G2G-002·G2G-003·G3.5-007은 green이나 통화 존치**(wrap-only, ③-형 API 단위 테스트) — B5 잔여 중 "green·통화 존치" 하위군으로 재분류(red 부채 아님, 그러나 소비자-0 게이트 계속 블로킹).

### (5) B6-부분 삭제 입력 — 6 삭제-표면 소비자-0 최종 판정표
grep 소비자(`tests/**/Program.cs`, 주석/소스-sniff 문자열 제외, 워크트리 제외):
| # | 삭제-표면 파일 | grep 시그니처 | A-1 前 | A-1 後 | 소비자-0? |
|---|----------------|--------------|-------:|-------:|-----------|
| 1 | `HeadlessGameLoop.cs` OLD ctor/step | `new HeadlessGameLoop(` | 0 | **0** | ✅ **YES** (c5 §3.4c 청산 유지) |
| 2 | `MetadataActionProcessor` AdvancePhase/EndTurn body | `HeadlessActionTypes.(AdvancePhase\|EndTurn)` | 26 | **21** | ❌ NO (A-1 −5: C12·R2-1·W5·G9-062·D6) |
| 3 | `HeadlessMainPhaseFlow` invented eval | `new HeadlessMainPhaseFlow(`·`.ResolveTurnEndMinMemory`·`.DefaultMemoryPassValue` | 2 | **2** | ❌ NO (FAILd-07=B4·G2E-005=DefaultMemoryPassValue 상수 read; §1.3 항5 승격 후 처리) |
| 4 | `HeadlessEarlyPhaseFlow` Unsuspend/Draw/Breeding | `new HeadlessEarlyPhaseFlow(` | 1 | **1** | ❌ NO (E3-Witness=DORMANT 항6·baseline-RED; B5/은퇴 후보) |
| 5 | 디스패처 OLD arm | `new HeadlessLegalActionDispatcher(` | 1 | **0** | ✅ **YES** (A-1 청산 — FAILd-03 펌프 re-point) |
| 6 | `EndOfTurnDrainedTurn` 마커 | `EndOfTurnDrainedTurn` | 0 | **0** | ✅ **YES** (엔진 내부 전용, 설계 §1.3 항4) |

**B6-Da 귀결(A-1)**: 6 삭제-표면 중 **3(항1·항5·항6)이 소비자-0 도달** — 이 셋은 B6 물리삭제 가능(항5=디스패처 OLD arm은 A-1 신규 청산). 잔여 블로킹 3(항2·항3·항4)은 실부채/타-배치 게이트에 종속:
- **항2(AdvancePhase/EndTurn body) 21 잔존**: 진짜 부채 게이트 소속 검증 — 21 = **B6-은퇴 2**(G2A-006·R4S3c-ShadowOldNew=존재이유 소멸) + **green·통화 존치 4**(G2G-002/003·G3.5-007·A3=③-형 wrap-only 단위 테스트) + **B4/B3/B5 미스코프 6**(G12-002·G3.5-C2·N9·D1·D2·F68) + **실부채 red 9**(C5-SecurityPreWindow·C5-Witness·G3.5-C910·W4·N2·D3·NewTimingsFire·R2-Del·G2G-004 = 키워드/보안 창·신timing 부채 = §3.4b P1 "창/배선 부채 유지"·병행 Sonnet 트랙 몫). ⇒ **21 중 재조준-가능 잔여 0**: green 4는 API-단위(재조준 불가·존치), 실부채 9는 엔진 갭(driver-swap 무효), 나머지는 B3/B4/은퇴 배치 몫. A-1 스코프의 "기계 잔여"는 소진.
- **항3·항4(EndTurn drain·마커)**: 항2와 원자(§3.3 리스크 3) — 항2 잔존이 이 둘을 동반 블로킹.

## §3.1g A-1 P2 상환 — per-file 제거 단언→대체 관측 원장 (리뷰2 P2 연장)
A-1 재조준·re-point per-file 원장(§3.1g c1~c5 연장):
| 배치 | 파일 | 제거 단언 | 대체 관측 | net | 근거 |
|------|------|----------|----------|----:|------|
| A-1 | G3.5-C12 | (0 제거) | 0(단언 텍스트 verbatim; result-object 부울 소스만 zone 재소싱) | **0** | 10 combat 단언 전량 보존; 패자 Trash 착지=would-be-deleted flag보다 강한 관측 |
| A-1 | G3.5-R2-1 | (0 제거) | 0(zone 단언 이미 관측형·verbatim) | **0** | field/security 재조준·단언 무변 |
| A-1 | G3.5-W5 | -9(SecurityResolutionResult 진단 read: IsSuccess·SecurityDigimonBattles·CheckedCardIds.Count·AttackerDeletedBySecurity) | AttackCleared(펌프 None 도달)·security card Trash·attacker BattleArea/Trash·2nd-security Security·DeletedByBattleKey — 제거된 result-object 진단마다 동치(실삭제) zone/metadata 관측 명시 | **-9** | 진단 result-object 은퇴(실삭제 zone이 대체); **영속 룰 검증 소실 0**(would-be-deleted → 실제 zone 착지) |
| A-1 | G9-062 | (0 제거) | 0(zone/DP-scan/창 단언 verbatim) | **0** | battle 2=펌프 드라이브·단언 무변; sweep=retained 무변 |
| A-1 | G3.5-D6 | -3(OLD 액션-통화 assert: types.Contains(HatchDigitama)·Contains(AdvancePhase)·!Contains(MoveBreedingToBattle)) | +7(펌프 choice-model: IsPending·Type==BreedingDecision·PlayerId·hatch-ResolveChoice·CanSkip·skip-lane·candidate=="hatch") | **+4** | "AdvancePhase=decline" 검증대상 번역(canSkip으로); "breeding=DECISION" 검증 의도 보존·강화 |
| A-1 | FAILd-03 | (0 제거) | 0(2 test 부울 true/false shape 보존; 내부 관측만 dispatcher→펌프 BreedingDecision) | **0** | CanNotMove 게이트 검증 동일; 관측 좌석만 펌프-dispatch로 이동 |
| **A-1 소계** | | -12 | +11 (+ result-object→zone 등가 재소싱) | **-5** | 제거 12 전량 (a)result-object 진단 은퇴(동치 zone 대체) 또는 (b)OLD 액션-통화 assert 은퇴(펌프 choice-model 번역); 실룰 검증 소실 0 |

**A-1 감사 결론**: net −5의 제거분(W5 −9 result-object 진단 + D6 −3 OLD 액션-통화)은 전량 **검증대상(발명 result-object 진단·OLD 통화)이 펌프 등가 관측(실 zone 착지·choice-model)으로 대체**된 것 — c5 원칙(제거=은퇴+대체 커버 실재) 준수, 영속 게임-룰 검증 소실 0. C12/R2-1/G9-062/FAILd-03은 net 0(단언 verbatim); D6는 +4(choice-model 강화).

## §3.4e A-2 결과 (2026-07-19): 삭제 0 — 소비자-0 판정표 정정(직접-시그니처 grep 아티팩트)
6표면 전원 라이브-도달 재확증(삭제 시 build 깨짐/라이브-green 회귀): OLD ctor=17파일 라이브(GR-002/GR-003 등 green 포함, CreatePumpDriven이 파라미터 ctor 내부 의존)·GameLoop=DcgoMatch:52 전 매치 인스턴스화(펌프-공유)·디스패처 OLD arm=GetLegalActions 경유 라이브(GR-002 실증)·EoT 마커=항2 body 소비(원자)·shadow 2=OLD 존속 중 차분 가드 유효(조기 은퇴=secwin 유일-witness 소실 위험)·G2A-006=검증대상 존속. **결론: 4b 물리 삭제는 부분-불가, 원자적 종점** — 선행=잔여 OLD-ctor/통화 소비자(green 포함 ~25±) 재조준·은퇴 + 실부채 9 상환. 안전-삭제 가능 표면=∅. §3.4d(5) 판정표 폐기, 본 절이 대체.

## §3.4f A′-2 실행 기록 + A′ 총괄 — B6 사전 조건 확정판 (2026-07-19, main HEAD=7e139321 파생, 메인 워킹트리·미커밋)

**엔진/src 변경 = 0**(전부 테스트-파일). build 엔진 0오류(경고 1771 무증가). shadow 무영향(src 무변경). A′-1(7e139321) 위에 펌프-네이티브 재작성 2 + 계약-테스트 군 6 판정 + L4-001 진단.

### (1) 항목 1 — 펌프-네이티브 재작성 2 (전후 표, 단언 의도 전량 보존)

**G2G-003.Battle.DP.deletion (10/10 green→green, currency=0·OLD-ctor=0·SetPhase=0)**: 배틀-outcome 4종(HigherAttacker/HigherDefender/Equal/Blocked)은 `new DcgoMatch`+AdvanceToMain(AdvancePhase 통화)+수동 `new BattleResolver().ResolveAsync` result-object 진단 → `CreatePumpDriven`+F68 `StagePermanent`(합성 Digimon def·인스턴스 dp·None→BattleArea·`CardEffectRegistrar.RegisterCard`)+펌프 DeclareAttack legal-lane **자동해소**로 재작성. result-object → **zone 등가 재소싱**(A-1 C12/W5 판례):

| subtest | OLD result-object 단언 | 펌프 zone/메타 재소싱 |
|---------|------------------------|----------------------|
| HigherAttacker(9000v7000) | IsSuccess·AttackerDp·DefenderDp·!AttackerDeleted·DefenderDeleted·DeletedCardIds=[Tgt]·attack Resolved | Tgt→Trash·Atk→BattleArea·**!Atk in Trash(非-vacuous)**·DeletedByBattleKey(Tgt)·DpBeforeBattleKey=7000·AttackPhase.None |
| HigherDefender(5000v8000) | AttackerDeleted·!DefenderDeleted·DeletedCardIds=[Atk] | Atk→Trash·Tgt→BattleArea·!Tgt in Trash·DeletedByBattleKey(Atk)·DpBeforeBattleKey=5000 (=HigherAttacker의 非-vacuous 대조쌍) |
| Equal(6000v6000) | AttackerDeleted·DefenderDeleted·MovementResults.Count=2 | Atk·Tgt 둘 다 Trash |
| Blocked(atk9000·tgt3000·blk12000) | (수동 BlockTiming)+result AttackerDeleted·!DefenderDeleted·target unaffected | 펌프 target-attack→**block 창 pending**(AttackPhase.Blocking)→ExpResolveSelecting(blk)→Atk→Trash·blk→BattleArea·tgt→BattleArea. (원본=direct-attack+수동 BlockTiming; G3.5-005 판례로 펌프 block-창 target-attack 경유 — "선택 blocker가 전투 인수" 의도 보존) |

BattleResolver **방어-가드/결정론 4종**(DirectAttack-rejected·MissingDp-rejected·NonDigimon-rejected·Deterministic+source-sniff)은 **retained substrate 직접-호출 유지**(BattleResolver=삭제-표면 아님, 펌프 자체가 호출; 펌프는 direct-attack을 security로 라우팅해 rejection 경로를 절대 resolver에 안 태움 → 펌프 auto-해소 등가 부재). 단, 매치=CreatePumpDriven·선언=retained `AttackController.DeclareAttack` 직접(AdvancePhase 통화 0) → 가드 계약 verbatim 보존 + B6 통화 제거. 단언 net 0(제거 0·재소싱만).

**GR-002.BreedingMove (2/2 green→green, currency=0·OLD-ctor=0·SetPhase=0·MoveBreedingToBattle=0)**: `new DcgoMatch`+`TurnController.SetPhase(Breeding)` 강제+discrete `MoveBreedingToBattle` 액션 → `CreatePumpDriven`+**자연 순환**(Active→Draw→Breeding auto-flow)로 breeding 도달(GR-004 c2 `:breeding:act` 판례). 펌프는 breeding-move를 **BreedingDecision 창의 `:breeding:act` candidate**로 표면화(movable Digimon일 때만 개설=GR-002 gate):

| subtest | OLD 단언 | 펌프 재소싱 |
|---------|---------|-----------|
| DigimonCanMove(Lv3·dp3000) | MoveBreedingToBattle 리걸·apply·BreedingArea→BattleArea | 자연 순환→P1 차턴 breeding→`:breeding:act` candidate **개설(=리걸-등재)**·resolve→Rookie BreedingArea→BattleArea·no DigiEgg in battle |
| DigiEggCannotMove(Lv2·dp0·음성) | !MoveBreedingToBattle·!Hatch(occupied) | 자연 순환→비-movable egg이라 펌프가 **breeding move 미개설·auto-pass to main**(FAILd-03 판례: BreedingDecision은 MOVE 합법시만 개설)·moveOffered=false·egg는 BreedingArea 잔류·BattleArea=∅ (gate held) |

### (2) 항목 2 — 계약-테스트 군 6 판정 (멤버-단위 삭제-표면 vs 존치-표면)

기준(task ①): default-ctor의 `actionProcessor:null→MetadataActionProcessor` **폴백 구동**이 삭제-표면; 파라미터 ctor·CreateValidated·명시 actionProcessor 주입·비-구동 property-read는 존치. 실측: `RequestChoice`/`ClearChoice`/`"Choice resolve failed."`/`ResolveChoiceAsync`는 **MetadataActionProcessor 전용**(TurnFlowDriver=0); `TerminalActionProcessor`=G1A-002 내부 테스트-더블(주입).

| 계약 테스트 | 검증 대상 | 삭제 vs 존치 | 처분 |
|------------|----------|-------------|------|
| **G3.5-GPT4**(unguarded profile) | `EnforcesActionLegality`(GPT-#4 legality boundary)·CreateValidated·strict effect-gate profile(신1) | **전량 존치**(OLD 구동 0·property-read only; "unguarded"=actionLegality:null 존치 property, OLD actionProcessor 아님) | **retained 재분류**(D1/D2 판례·B6 비블로커·무접촉) |
| **G1C-001**(null-guard) | 파라미터-ctor `ThrowIfNull(context)`·EngineContext service-locator/CurrentMatch attach·track·clear·observation propagation | **존치**(파라미터-ctor 가드 + EngineContext 추적 계약=driver-agnostic; `new DcgoMatch(context)`+단일 StepAsync는 incidental·통화 0) | **retained 재분류**(B6서 default→pump ctor-swap 사소·단언 무영향) |
| **G1A-004**(inert-init) | ObservationSnapshot·ActionMask·GetLegalActions query 계약(init inert, StepAsync 없음→OLD processor **미발화**) | **존치**(parameterless `new DcgoMatch()`는 inert 컨테이너뿐·OLD 구동 0) | **retained 재분류**(B6 ctor-swap 사소) |
| **G1A-002**(lifecycle) | Initialize/Reset/Step/Result 순서·상태전이 계약 | **혼합**: 2 subtest=명시 `TerminalActionProcessor` 주입(존치)·`RejectsLifecycleApisBeforeInitialize`=pre-init 가드 driver-agnostic(존치); **`InitializeEstablishesFirstStepSnapshot`=OLD-driver 최소 step 시맨틱(init 이벤트·StepIndex non-auto-flow) 결합** | 대부분 retained; 1 subtest=**B6-동시 re-pin 초안**(펌프 등가 존재—first-step 이벤트 단언을 펌프 lifecycle 이벤트로 적응) |
| **M2-001**(real-match subtest) | 대부분 SyntheticSnapshot 관측-인코딩(존치); **1 실-매치 subtest**=`new DcgoMatch(…,LegalActionSetValidator)` 랜덤 legal-action 루프(AdvancePhase 통화)로 factored-action-schema↔observation-slot **정렬** 검증 | synth=존치; 실-매치 subtest=삭제-표면 소비(통화) | synth=**retained 재분류**; 실-매치 subtest=**B6-동시 re-pin 초안**(B3-인접: 펌프 매치 랜덤-드라이브로 동일 slot-정렬 계약) |
| **G1E-005**(bare pause) | pending-choice pause/resume 계약을 `RequestChoice`/`ResolveChoice`/`ClearChoice` **agent-action**(MetadataActionProcessor 처리)+StepAsync로 구동; +직접 `InMemoryHeadlessChoiceController` subtest(존치)·MetadataActionProcessor.cs sniff(삭제-파일 결합) | pending-choice **STATE** 계약=존치(펌프 ChoiceController 동일 기제); RequestChoice-agent-action 구동=삭제-표면 | ④ **throw-계약 은퇴 계열 대조 처분**: RequestChoice-agent-pause = 은퇴된 throw-replay 계약(§3.1b B1)의 **await-mode 대응물**(둘 다 OLD-driver choice-injection affordance)→MetadataActionProcessor와 함께 **B6-동시 은퇴**. STATE 계약은 펌프 choice 테스트(EXEMPLAR/W1b/DeferredChoice)+retained 직접 ChoiceController subtest가 커버. **실부채 아님** |

**계약군 귀결**: 6 중 **실부채 0**(전부 발명물/OLD-affordance 검증 or driver-agnostic 존치). 3(G3.5-GPT4·G1C-001·G1A-004)=완전 retained 재분류(무접촉); 3(G1A-002·M2-001·G1E-005)=혼합(retained 다수 + B6-동시 re-pin/은퇴 소수, 펌프 등가 실재).

### (3) 항목 3 — L4-001 단독-hang 진단

**증상 확정**: `L4-001.MatchEventLog` 단독 실행 = **무한 hang**(60s timeout kill). 프로세스 상태 R(running)·main utime 10.3 CPU-s/10s = **busy CPU 스핀**(deadlock 아님). 계측(스텝별 로그): OLD-driver(`new DcgoMatch(context, LegalActionSetValidator, eventLog)`) 랜덤-정책 풀-매치가 mid/late-board(turn ~19–25)에서 단일 ApplyAction/StepAsync 내부 무한루프.

**근원 특정**: 스텝-드레인 상위 루프(`GameFlowProcessor.RunToStableAsync`)는 `MaxIterations=256` 캡 보유(초과시 throw) → hang은 그 **아래 inner 무한 sub-루프**(단일 `ProcessNextState`/트리거-창/select iteration이 종료조건 미충족). hang 지점이 프로세스마다 상이(step 46/140/191·PlayCard/DeclareAttack 교차) = **프로세스-비결정**: .NET per-process 문자열-해시 랜덤화가 엔진 legal-action **순서**를 재배열 → 고정 정책-seed(`new Random(41)`)가 매 프로세스 다른 액션 선택 → 다른 trajectory → 일부가 루프 상태 도달. **이것이 "단독/스위트 green 차이"의 기제**(스위트 프로세스는 루프 trajectory 회피·단독은 적중; task 힌트 확증). MatchEventLog 자체는 순수 관측자(bounded 루프·상태 무변경)—무관 확인.

**처분**: **정밀 마킹**(수리 아님). 이유: (a) 深-엔진 트리거/전투/effect 드레인의 unbounded inner 루프=소형 수정 아님; (b) L4-001=OLD MetadataActionProcessor(B6 삭제-표면) 풀-랜덤 소비자 → B6서 CreatePumpDriven re-pin 대상; 펌프 캐이던스 하 동일 루프 재현 여부는 미검(펌프 캡/흐름이 회피 가능성 or retained 드레인 공유부채). **원장 등재**: `RD-R4A′-01` — OLD-driver 풀-랜덤 late-board hang(inner-drain unbounded loop, 프로세스-비결정 trajectory 노출; RunToStable MaxIterations 아래 sub-루프). L4-001 재-핀 시 펌프 하 재검 게이트. (L4 계측은 원복 완료·워킹트리 무변.)

### (4) A′ 총괄 — 통화·OLD-default 소비자 최종 잔존표 = **B6 사전 조건 확정판**

grep 실측(현 HEAD 파생 워킹트리, `tests/*/Program.cs`, 주석/sniff 제외): **AdvancePhase/EndTurn 통화 소비자 = 17**(A-1 21 −A′-1 3[A3·G2G-002·G3.5-007] −A′-2 1[G2G-003]); **OLD `new DcgoMatch(` 소비자 = 12**(§3.4e ~17 −A′-2 GR-002·G2G-003 등, 나머지 재조준분 CreatePumpDriven 전환).

**17 통화 소비자 3분류**:
| 분류 | 수 | 프로젝트 | B6 처분 |
|------|---:|---------|---------|
| **실부채 red(엔진 갭)** | **9** | C5-SecurityPreWindow·C5-Witness·G2G-004·G3.5-C910·G3.5-N2·G3.5-W4·PRIM-P0.NewTimingsFire·R2-DeletionPipeline·G3.5-D3(TriggerOrdering) | **키워드/보안 창·신-timing·트리거-순서 상환**(병행 Sonnet 트랙·driver-swap 무효)=**진짜 B6 게이트** |
| **B6-은퇴** | 2 | G2A-006(OLD 디스패처 시퀀스)·R4S3c-ShadowOldNew(shadow) | 검증대상 소멸→삭제 |
| **타-배치 re-point(통화 존치)** | 6 | G12-002·G3.5-C2·G3.5-N9·G3.5-F68·G3.5-D1(Piercing)·G3.5-D2(DpZero) | B3/B4/B5 per-fixture 펌프 재조준(펌프 등가 실재) |

**12 OLD-ctor 소비자 4분류**:
| 분류 | 프로젝트 | B6 처분 |
|------|---------|---------|
| **retained 재분류(무접촉)** | G3.5-GPT4·G1C-001·G1A-004 (+ G1A-002·M2-001·G1E-005 존치부) | B6 비블로커; default→pump ctor-swap 사소(단언 driver-agnostic) |
| **B6-동시 re-pin/은퇴** | G1A-002(1 subtest)·M2-001(실-매치 subtest, B3-인접)·G1E-005(RequestChoice-pause+MetaAP sniff, 은퇴) | 펌프 등가 실재; 실부채 아님 |
| **RL(B3)** | R4RL-02·R4RL-03·R4RL-04(FactoredSchemaV2) | B3 스키마 재조준 |
| **shadow/은퇴 + L4** | R4P4-ShadowRun(B6-은퇴)·L4-001(B6 re-pin + RD-R4A′-01 latent hang) | shadow 은퇴·L4 펌프 재-핀 |

**B6 사전 조건 확정판**: 물리 삭제(default→pump flip + 표면 삭제) 게이트 = **① 실부채 9 상환**(키워드/보안 창·신-timing·트리거-순서 = 병행 Sonnet 트랙 = 실 B6 게이트) + **② 은퇴 4**(G2A-006·R4S3c·R4P4·G1E-005 RequestChoice-pause/MetaAP-sniff) + **③ 타-배치 re-point 소진**(통화 6 = B3/B4/B5·RL 3 = B3·M2/G1A 혼합 subtest) + **④ retained 재분류 무접촉**(G3.5-GPT4·G1C-001·G1A-004 등=비블로커, 삭제와 무관). **retained 재분류·B6-동시 re-pin·은퇴는 실부채 아님**(펌프 등가 or 검증대상 소멸); **유일 실-잔부채=①의 9**(+ latent RD-R4A′-01은 L4 재-핀 게이트, B6 직결 아님). §3.4e 원자적-종점 결론 불변: 안전-삭제 표면 여전히 ∅, 종점=①9 + ②–④ 소진 후 원자적 flip.

### (5) 게이트 실측
- **build**: 엔진 0오류(경고 1771 무증가·src 무변경). 워킹트리 수정=**테스트 2파일**(G2G-003·GR-002)만.
- **재작성 2 green**: G2G-003 10/10·GR-002 2/2(전후 단언 의도 보존, 위 표).
- **회귀 green**: A′-1 5(G3.5-A3 4·G2G-002 10·G3.5-007 7·GR-003 pass·G1C-002 6) + A-1 대표(G3.5-C12 5·W5 7·G3.5-005 8) + EXEMPLAR-T1 18·GLINK 5 = 전원 green.
- **shadow**: R4P4-ShadowRun **bit-identical**(2 OLD-vs-OLD)·R4S3c-ShadowOldNew **2/2 IDENTICAL** + **secwin IDENTICAL**(seed 404 winner 1/1·sec 0/0·동일 digest). src 무변경→자명 무영향 실증.

### (6) 남는 리스크
1. **실부채 9가 유일 실-B6 게이트**: 전부 키워드/보안 창-재하우징·신-timing·트리거-순서 = 병행 Sonnet 트랙 몫(§3.4b/§3.4d P1 정합). 4b driver-swap 스코프 밖 확정—B6는 이 트랙 완료에 종속.
2. **RD-R4A′-01(L4 latent hang)**: OLD-driver 풀-랜덤 late-board inner-drain unbounded 루프. 펌프 하 재현 여부 미검=retained 드레인 공유부채일 위험. L4-001 B6 re-pin 시 펌프 풀-랜덤 스모크로 재검 게이트 권고(현재 B6 비블로킹—OLD-only 노출).
3. **계약군 B6-동시 subtest 3(G1A-002·M2-001·G1E-005)**: 펌프 등가 실재하나 first-step 이벤트/slot-정렬/choice-model 적응 필요—B6-Db 컷오버 배치에 편입(재조준 아님·기계적).

## §3.4g B6-Db 실행 기록 — 컷오버 준비 배치(잔여 소비자 최종 소진) + B6 원자 삭제 직전 상태 (2026-07-19, main HEAD=9966c6ec 파생, 메인 워킹트리·미커밋)

엔진/src 변경 = **1 파일 신설**(`TfxArmorPurgeWouldBeDeleted.cs` 테스트 픽스처, 실플레이 inert). build 엔진 0오류. shadow 무영향 실증(아래 (4)). 수리-1/2 판례(ecf18f0d·9966c6ec) 위에 §3.4f B6 사전조건의 ③타-배치 re-point·②은퇴 이식·항목3 재핀을 소진.

### (1) 항목 2 — 은퇴 준비(이식만; 실은퇴는 B6)
- **secwin 이식 완료**: R4S3c `[secwin]` 시큐리티-0 승리 종국 witness(§A-2 판정=유일-witness)를 **펌프-단독** witness로 R4S3b에 이식(`SecurityWinPumpWitness`). FixtureSecurityRaceDecks(P1=50×BT4_065 6000DP·P2=50×BT1_028 3000DP·무-digitama)를 `NewPumpMatchWithDecksAsync`+attack-first 드라이브로 종국까지 구동. 단언 보존: **승자 마킹·loser 시큐리티 0·loser 라이브러리>0(비-덱아웃)**. R4S3b **14/14 green**. ⇒ R4S3c(shadow 2)를 B6-은퇴해도 시큐리티-승 종국 seam 유실 없음(비-vacuous: 약공격 대조 없이도 zone-종국 3중 단언).
- **R4P4 결정론 sanity 판정=중복(이식 불요)**: R4P4의 OLD-vs-OLD "펌프에도 유효한 단언"=엔진 결정론(동seed→동trajectory). **RLB1-01(ParallelDeterminism) 헤더가 명시적으로 "the pump-era replacement"**(dormant OLD-cadence 결정론 verifier 대체)이며 serial+parallel 동seed digest 항등 + cross-seed 발산(discriminative)을 **펌프 드라이버**에서 단언 = R4P4 OLD-sanity의 상위집합. ⇒ 펌프-유효 단언은 RLB1-01이 커버(초과). 이식 불요, 판정만.

### (2) 항목 3 — R2-Del 발명-표현 3 재조준 + P1-2 판별 (코디네이터 승인, 수리-2 C5 판례)
`R2-DeletionPipeline.Tests` (base 4-red → **1-red**):
- **P1-1 ×2 (Evade) = 재핀 GREEN**: 철거된 `HasEvadeKey` 메타 게이트키 → card-registered OPTIONAL `[WhenPermanentWouldBeDeleted]`(`TfxWouldBeDeletedInteractive`, C5 판례). 창 타입 `DeletionReplacement`→`OptionalEffect`, `#evade` id→AcceptWindow(holder-id candidate), Evade suspend-cost 단언 폐기(발명-키워드 표현)→pendingDeletion-cleared 생존 단언으로 대체. **배치-원자 defer(mate B park)·단일-reactor collapse(-1)** load-bearing 룰 전량 보존. **하네스 정합 필수 발견**: 순수 sink 하네스가 (a)ambient scope 미진입 (b)`deferredChoice` 미설정 → 인터랙티브 PRE cut-in 미개설. C-Del-3C1 substrate 패턴대로 `AmbientMatchContext.Enter`+`CreateDefault(deferredChoice:true)` 배선 후 flip(HasEvadeKey 원본도 red였음=계약 미완결 상태를 재핀으로 완결).
- **P1-4 (Armor Purge) = 재핀 GREEN**: `HasArmorPurgeKey` → **신설 `TfxArmorPurgeWouldBeDeleted`**(OPTIONAL `[WhenPermanentWouldBeDeleted]`가 실 프리미티브 `DeDigivolveHelpers.ArmorPurgeTopAsync` 합성: top-trash+source-promote+willBeRemoveField=false). 창 개설(OptionalEffect)·top A→Trash·source→BattleArea·**leave-reactor 미발화(memory 0)** 전량 보존(비-vacuous: reactor 상주하나 top-swap=비-departure라 미발화). 프리미티브 신설 아님(ArmorPurgeTopAsync 기존)·실플레이 inert.
- **P1-2 (실갭) = 마킹 유지 (RD-R4B6-P1-2)**: 엔진은 배틀-id를 이미 스탬프(SecurityResolver.cs:822, 첫 단언 통과)하나, **시큐리티-전투 finisher의 departure가 RunToStable의 OnLeaveFieldAnyone 수집에 sink/field-battle finisher처럼 급전되지 않아** uncapped leave reactor 미발화. ambient wrap로도 미해소=하네스 아닌 실-엔진 갭. SecurityResolver finisher↔BattleResolver 트리거-큐 AS-IS 대조 필요=소형 재핀 초과 → 마킹(수리 아님).

### (3) 항목 1 — 타-배치 re-point 재실측 + 소진
- **G3.5-D2 (DpZero) = RED→GREEN 완전 재조준 (10/10)**: red 근원 재판별 = **죽은 표현 2중**. ① DP-fold: `dpModifiers`(`DpModifier.Relative`) 메타는 CardObservation만 읽고 `Permanent.DP`(DP<=0 sweep가 읽는 좌석, GameFlowProcessor.cs:671)가 미-fold → **N2 판례**로 live `CardEffectCommons.ChangeDigimonDP`(IChangeDPEffect) 재배선(sweep가 effective DP<=0 인식→삭제). ② Evade 창: DpZeroOpensPreWindow의 `HasEvadeKey` → **C5 판례** `TfxWouldBeDeletedInteractive`+OptionalEffect(deferredChoice+ambient). ⇒ strike-시드/Collision/N2 수리 이후 재실측 결과 D2는 **엔진 갭 아닌 죽은-표현 재타깃**(N2 동종)으로 확정, flip.
- **G3.5-D1 (Piercing) = W4 stale-probe 확정(재타깃 pending)**: 2 red subtest 전부 발명 `EffectRegistry.Register(new EffectBinding(RecordingFakeEffect))` 소비 — 라이브 security-check 창은 `AutoProcessing.GetSkillInfos`(card-registered)만 읽고 EffectRegistry 바인딩 미참조(W4 판례 동일). `PiercingFiresSecurityEffect`(OnSecurityCheck)→live `TfxOnSecurityCheckDraw` 필드-reactor로 재타깃 가능(단 생존 P2 필드-reactor staging 필요); `TriggerKillsAttackerBeforePiercing`(OnKnockOut)→live OnKnockOut-delete 픽스처 신설 필요(부재). 성격=W4 stale-probe 확정, 이번 배치 미실행(픽스처 staging 미완, 보수 원칙).
- **currency-4 (G12-002·G3.5-C2·G3.5-N9·G3.5-F68) = green·통화 존치(드레인 미완)**: 전원 green(재실측 무변)이나 `AdvanceToMainAsync`의 `AdvancePhase` 통화 보유. **결정적 발견**: D2에서 실증 — 삭제-파이프/would-be-deleted 창은 **Main 페이즈 도달을 요구**(AdvanceToMain 제거 시 sweep 회귀 5-red). ⇒ AdvancePhase 통화의 사소-제거 불가; 드레인=`CreatePumpDriven`+`DriveUntil(AtMainWait)` reach-Main 재핀(=B3/B5 per-fixture 펌프-하네스 재구축)에 게이트. §3.4e "원자적 종점" 재확증.
- **RL 3 (R4RL-02/03/04) = OLD-ctor 존치(드레인 미완)**: `PendingHandChoiceMatchAsync`의 `new DcgoMatch(...actionLegality:LegalActionSetValidator)` → `CreatePumpDriven` 스왑은 **비-사소**: CreatePumpDriven이 딜을 StartGame으로 정규화(hand 0 until StepAsync)→헬퍼의 `hand.Count>=3` 즉시-단언 회귀. 펌프-딜 드라이브(StepAsync+mulligan) 배선=B3 스키마 재조준. 계약(관측 인코딩) 무변 확인·미실행.
- **mixed-3 (G1A-002·M2-001·G1E-005) = B6-동시 처분 마킹**(§3.4f 일치): G1A-002 first-step·M2-001 실-매치 slot-정렬=B6-동시 re-pin(펌프 lifecycle 이벤트/랜덤-드라이브 적응, 기계적); G1E-005 RequestChoice-pause+MetaAP-sniff=B6-동시 은퇴. 실행 아닌 마킹(실은퇴/re-pin은 B6 원자 배치).

### (4) B6 원자 삭제 직전 상태 최종표 (삭제-표면별 잔존 소비자)
grep(`tests/**/Program.cs`, 근사; 주석/sniff 포함 상한):
| # | 삭제-표면 | 시그니처 | 소비자 | 소비자-0? | B6-동시 처분 |
|---|-----------|----------|-------:|-----------|--------------|
| 1 | `HeadlessGameLoop` OLD ctor/step | `new HeadlessGameLoop(` | **0** | ✅ | 삭제 가능 |
| 4 | `EndOfTurnDrainedTurn` 마커 | `EndOfTurnDrainedTurn` | **0** | ✅ | 삭제 가능(항2 원자) |
| 5 | 디스패처 OLD arm | `new HeadlessLegalActionDispatcher(` | **0** | ✅ | 삭제 가능(A-1 청산 유지) |
| 2 | `MetadataActionProcessor` AdvancePhase/EndTurn body | `HeadlessActionTypes.(AdvancePhase\|EndTurn)` | **16** | ❌ | 아래 분해 |
| 3 | `HeadlessMainPhaseFlow` invented eval | `new HeadlessMainPhaseFlow(` | **1** | ❌ | FAILd-07=B4(항5 승격 선행) |
| 6 | `HeadlessEarlyPhaseFlow` Unsuspend/Draw/Breeding | `new HeadlessEarlyPhaseFlow(` | **1** | ❌ | E3-Witness=B5/은퇴(DORMANT) |

**항2 잔존 16 분해** (B6-동시 처분 목록):
- **B6-은퇴 2**: G2A-006(OLD 디스패처 시퀀스=검증대상 소멸)·R4S3c-ShadowOldNew(shadow; **secwin 이식 완료로 유일-witness 유실 위험 해소** → 은퇴 안전).
- **green·통화-preamble 존치 13**(드레인=reach-Main 펌프 재핀): currency-4(G12-002·C2·N9·F68) + 수리-flip 유지 9(C5-SecurityPreWindow·C5-Witness·G2G-004·C910·**D2(본 배치 flip)**·N2·W4·PRIM-P0.NewTimings·R2-Del[7/8]). 전원 green/부분-green이나 `AdvanceToMain` preamble 통화 보유. 실룰 검증은 완결; 통화만 잔존.
- **실부채 red 1**: G3.5-D1(W4 stale-probe, 재타깃 pending).

**실부채 잔여(§3.4f ①9 대비 갱신)**: 본 배치가 D2 flip·R2-Del P1-1/P1-4 재핀으로 **red 감소**(D2 5-red·R2-Del P1-1×2+P1-4=3-red 해소); 잔여 실부채 = **RD-R4B6-P1-2**(신규 등재: 시큐리티 finisher departure 트리거 미급전) + **D1 W4-probe**(재타깃 pending) + §3.4f ①의 키워드/보안 창 red(병행 Sonnet 트랙, 무변). latent RD-R4A′-01(L4 hang)=B6 비블로킹 유지.

**B6-Da 귀결(B6-Db)**: 삭제-표면 6 중 **3(항1·4·5)=소비자-0 확정**(원자 삭제 가능). **잔여 블로킹 3**(항2=16·항3=1·항6=1)의 실체 = ① reach-Main preamble 통화 13(green, 드레인=B3/B5 펌프-하네스 재핀) ② B6-은퇴 2(secwin 이식으로 안전) ③ 실부채 red 1(D1)+FAILd-07(B4)+E3(B5) + P1-2 실갭. **§3.4e 원자적-종점 결론 불변**: 안전-삭제 표면 여전히 ∅; 종점 = preamble-통화 13 펌프 reach-Main 재핀 + 은퇴 2 + 실부채(D1·P1-2·§3.4f①9) 상환 후 원자 flip.

### (5) 게이트 실측 (코디네이터 detached 게이트용)
- **build**: 엔진 0오류(픽스처 1 신설 반영). 워킹트리 수정 = 테스트 3파일(R4S3b·R2-Del·G3.5-D2) + 엔진 픽스처 1(TfxArmorPurgeWouldBeDeleted).
- **실행분 green/판별**: R4S3b **14/14**(secwin 이식 포함)·R2-Del **7/8**(P1-2 마킹 red)·G3.5-D2 **10/10**.
- **회귀 green**: 수리-2 flip 5(C5-SecurityPreWindow 5·W4 5·C910 7·D3 2·N2 7) · A′ 대표(C12 5·W5 7·G3.5-005 8·G2G-003 10·GR-002 2) · EXEMPLAR-T1 18·GLINK 5 · currency-4(G12-002·C2 6·N9 2·F68 13) 전원 green.
- **shadow(엔진 픽스처 신설 무영향 실증)**: R4S3c-ShadowOldNew OLD-vs-NEW **2/2 IDENTICAL** + **secwin IDENTICAL**(seed 404 winner 1·sec 0/0·동일 digest)·R4P4-ShadowRun **bit-identical**(2 OLD-vs-OLD). (RLB2-01 profile 게이트는 장시간 실행; 픽스처 inert·shadow bit-identical이므로 엔진-행동 무영향 자명.)

### (6) 남는 리스크
1. **preamble-통화 13 드레인 = B3/B5 펌프-하네스 재구축**: 본 배치의 핵심 발견 — currency 소진은 sink/sweep 픽스처의 reach-Main을 `CreatePumpDriven`+`DriveUntil(AtMainWait)`로 재핀해야 하며(D2 실증: Main-도달 필수), per-fixture 노동. B6은 이 13(+RL 3)의 펌프 reach-Main 재핀에 강결합.
2. **RD-R4B6-P1-2 실갭**: 시큐리티-전투 finisher departure 트리거 미급전(배틀-id는 스탬프됨). SecurityResolver↔BattleResolver 트리거-큐 대조 = 병행 보안 트랙 인접, 소형-초과.
3. **D1 W4-probe 재타깃 미완**: `PiercingFiresSecurityEffect`=TfxOnSecurityCheckDraw 필드-reactor staging 필요; `TriggerKillsAttackerBeforePiercing`=OnKnockOut-delete 픽스처 부재(신설 필요). 성격 확정, 실행 이월.

## §3.4h B6-Dc 실행 기록 — preamble-통화 reach-Main 재핀 + D1 재타깃 + B6 원자 삭제 최종 전제표 (2026-07-19, main HEAD=6048ed05 파생, 메인 워킹트리·미커밋)

엔진/src 변경 = **1 파일 신설**(`TfxOnKnockOutDeleteOpponent.cs` 테스트 픽스처, 실플레이 inert). build 엔진 0오류. shadow 무영향 실증(아래 (5)). §3.4g B6-Db 판례 위에 항목3 preamble-통화 드레인·항목2 D1 재타깃을 소진하고, §3.4g가 "reach-Main 펌프 재핀만 필요"로 투영한 13을 **실측으로 정정**한다.

### (1) 항목 1 — preamble-통화 13 reach-Main 재핀 (실측: 5 드레인 / 8 B6-동시 이월)
레시피(수십 회 검증, F62/EXEMPLAR-T1): `new(context)`→`DcgoMatch.CreatePumpDriven(context)` + OLD `AdvanceToMainAsync`(AdvancePhase 루프)→`StepOnceAsync`+`DriveUntil(AtMainWaitOf)` + 펌프-드라이브 헬퍼 4 추가. 본문 단언 무변.

- **드레인 완료 5 (통화 1→0·green 무변)**: `G3.5-C910`(7/7)·`G3.5-D2`(10/10)·`G3.5-F68`(13/13)·`G3.5-W4`(5/5)·`R2-DeletionPipeline`(6/1 = P1-2 마킹 red 무변). 공통 성격 = **순수 sink/sweep/window 본문**(합성 카드 직배치 + `sink.Apply`/직접 `BattleResolver`·`AttackPipeline`). reach-Main은 페이즈-상태만 확보, 본문 기제는 펌프 무관. (C5-Witness의 OLD `AssertEqual(1, advance.Length,"advance phase count")` 통화-불변식은 이 배치 대상 아님 — 해당 파일은 아래 (2)에서 이월.)
- **preamble-only 스왑 회귀 → 원복 + B6-동시 이월 8**: `C5-SecurityPreWindow`·`C5-Witness`·`G12-002`·`G2G-004`·`G3.5-C2`·`G3.5-N2`·`G3.5-N9`·`PRIM-P0.NewTimingsFire`. **핵심 발견(§3.4g 투영 정정)**: 이들의 **본문이 `match.ApplyActionAsync(DeclareAttack/gameplay-action)`(또는 env-스텝·Setup-페이즈 staging)로 구동** → `CreatePumpDriven`의 `TurnFlowDriver`가 그 액션을 MainPhaseAction 패킷으로 지연-라우팅하므로, 본문의 단일 `StepAsync`+외부 `GameFlowProcessor.RunToStableAsync`가 효과를 완결 못 함 → green subtest 회귀(실증: PRIM-P0 5→2, C5-Witness 4→2, N2 7→1, G2G-004 9→6, C2 6→2, C5-SecurityPreWindow 5→0, G12-002 env-드라이브 1→0). ⇒ 이들의 통화 드레인 = **본문 펌프-하네스 재구축(B3/B5)** 필요이고, 그 재구축은 B6 원자 배치에서 OLD AdvancePhase/EndTurn body 삭제와 동시에 펌프 legal-lane으로 re-pin된다. `G3.5-N9`는 별종(Setup-페이즈에 suspend 카드 staging 후 Unsuspend-페이즈 도달을 요구 — 펌프 딜-정규화가 Setup-staging을 파괴; 브릿지 자체는 現 HEAD green 2/2 = 수리들이 해소). ⇒ **preamble-only-drainable ≠ 13; 실측 5**.
- **RL 3(R4RL-02/03/04) = 별개 축**: AdvancePhase 통화 아님(`new DcgoMatch(…actionLegality:…)` OLD-ctor 소비 = 기본-ctor→펌프 flip 축, B3 pump-딜 스키마 재조준). 통화-16 grep에 부재 확인. 본 배치 무접촉(§3.4g 일치).

### (2) 항목 2 — D1(Piercing) 재타깃 (RED→GREEN 완료, 3/2 → 5/5)
발명 `EffectRegistry.Register(new EffectBinding(RecordingFakeEffect/AttackerKillingEffect))` 프로브(W4 stale-probe) 2건을 live 카드-등록 표면으로 재타깃(기존 프리미티브 합성, 로직 발명 0):
- **PiercingFiresSecurityEffect** → 生存 P2 필드-reactor `TfxOnSecurityCheckDraw`(기존 픽스처, W4.RegisterReactor 관용구): 피어싱이 P2 시큐리티 체크 시 owner-scoped `[When your security is checked] Draw 1` 발화 → **P2 hand +1** 관측 단언(fake ResolveCalls 대체).
- **TriggerKillsAttackerBeforePiercing** → **신설 `TfxOnKnockOutDeleteOpponent`**(OnKnockOut ActivateClass가 `new Player(ctx,owner).Enemy!.GetBattleAreaDigimons().Filter(…)`+`DestroyPermanentsClass(...).Destroy()` — BT9_111/081 프리미티브 그대로): KO 카드(target)에 등록, BattleResolver KO 창(:248 stack)이 shared main-loop AutoProcessCheck에서 PierceProcess의 DoSecurityCheck flip **이전**에 드레인(:262-267) → 공격자 삭제·피어싱 취소. 단언 = 공격자 Trash + 시큐리티 unchanged(=KO 트리거 pre-check 드레인 witness). §3.4g "OnKnockOut 픽스처 부재 → 실행 이월" 해소; 엔진 갭 아님(창 배선 존재, 픽스처만 부재였음)을 실증. `AttackerKillingEffect`/`RecordingFakeEffect` 프로브 클래스 은퇴(D1 EffectRegistry.Register live 소비 0).

### (3) B6 원자 삭제 최종 전제표 (삭제-표면 6 × 소비자 최종)
grep(`tests/**/Program.cs`, 주석/sniff 포함 상한, 본 배치 후):
| # | 삭제-표면 | 시그니처 | 소비자 | 소비자-0? | B6-동시 처분 |
|---|-----------|----------|-------:|-----------|--------------|
| 1 | `HeadlessGameLoop` OLD ctor/step | `new HeadlessGameLoop(` | **0** | ✅ | (원자 flip; §3.4e 라이브-도달) |
| 4 | `EndOfTurnDrainedTurn` 마커 | `EndOfTurnDrainedTurn` | **0** | ✅ | 항2 원자 |
| 5 | 디스패처 OLD arm | `new HeadlessLegalActionDispatcher(` | **0** | ✅ | 원자 flip |
| 2 | `MetadataActionProcessor` AdvancePhase/EndTurn body | `HeadlessActionTypes.(AdvancePhase\|EndTurn)` | **11**(16→11) | ❌ | 아래 분해 |
| 3 | `HeadlessMainPhaseFlow` invented eval | `new HeadlessMainPhaseFlow(` | **1** | ❌ | FAILd-07 = B4(항5 승격 선행) |
| 6 | `HeadlessEarlyPhaseFlow` Unsuspend/Draw/Breeding | `new HeadlessEarlyPhaseFlow(` | **1** | ❌ | E3-Witness = B5/은퇴(DORMANT) |

**항2 잔존 11 분해** (16 − 드레인 5):
- **B6-은퇴 2**: `G2A-006`(OLD 디스패처 시퀀스=검증대상 소멸)·`R4S3c-ShadowOldNew`(shadow; secwin 이식 완료로 유일-witness 유실 없음).
- **B6-동시 re-pin 9**(본문 펌프-하네스 재구축 = B3/B5, 원자 flip 시 legal-lane 재조준): `C5-SecurityPreWindow`·`C5-Witness`·`G12-002`·`G2G-004`·`G3.5-C2`·`G3.5-D1`·`G3.5-N2`·`G3.5-N9`·`PRIM-P0.NewTimingsFire`. 전원 現-green/부분-green이나 본문이 `ApplyActionAsync`-구동(또는 env/Setup-staging)이라 preamble-only 드레인 불가.
- **실부채 red 0**(항2 내): D1 재타깃 green化로 §3.4g의 "실부채 red 1(D1)" 해소.

**§3.4e 원자적-종점 결론 불변**: 안전-독립-삭제 표면 = ∅(1/4/5도 CreatePumpDriven 파라미터-ctor 경유 라이브-도달 = 기본ctor→펌프 flip과 원자). 종점 = **항2 re-pin 9 + 은퇴 2**(원자 flip 시 동시 처분) + **항3 FAILd-07(B4)** + **항6 E3(B5)** + **RL 3(B3)** 소진.

### (4) B6 원자 배치 작업 목록 초안
- **삭제 파일**: `HeadlessGameLoop.cs`(전체)·`HeadlessMainPhaseFlow.cs`(invented eval; 항5 `AutoProcessing.TurnEndMinMemory` 정본 승격 후)·`HeadlessEarlyPhaseFlow.cs`(Unsuspend/Draw/Breeding 블록; DORMANT).
- **삭제 멤버**: `MetadataActionProcessor.AdvancePhaseAsync`(:969)/`EndTurnAsync`(:1012) body + drain·`WindowResolutionController.EndOfTurnDrainedTurn`(:25)·`HeadlessLegalActionDispatcher` OLD arm.
- **플립**: 기본 `new DcgoMatch(` 소비자 12(RL 3 포함)·잔여 OLD-ctor 파라미터 소비자 → `CreatePumpDriven`. G1 근본 게이트.
- **은퇴 스위트**: `G2A-006`·`R4S3c-ShadowOldNew`·`R4P4-ShadowRun`(shadow 존재이유 소멸)·`G1E-005`(RequestChoice-pause/MetaAP-sniff).
- **B6-동시 re-pin subtest**: 항2 9(위) + 항3 `FAILd-07`(B4 EndTurn-min-memory seam) + 항6 `E3-Witness`(B5) + RL 3 `R4RL-02/03/04`(B3 pump-딜 스키마) + `G1A-002`/`M2-001`(§3.4g mixed, 펌프 lifecycle/slot 재핀).
- **원장 폐쇄**: `RD-R4B6-P1-2`(잔존 실갭, 아래 (6))·`RD-R4A′-01`(L4 hang, B6 비블로킹 유지).

### (5) 게이트 실측
- **build**: 엔진 0오류(픽스처 1 신설 반영). 워킹트리 수정 = 테스트 6(C910·D1·D2·F68·W4·R2-Del) + 엔진 픽스처 1(TfxOnKnockOutDeleteOpponent).
- **실행분 green/전후 표**: 드레인 5(C910 7/7·D2 10/10·F68 13/13·W4 5/5·R2-Del 6/1[P1-2 무변]) 통화 0·단언 무변; D1 **5/5**(3/2→flip, EffectRegistry.Register live 0).
- **회귀 green**: B6-Db 실행분(R4S3b **14/14** secwin 포함·R2-Del **7/8**·D2 **10/10**) · 수리-flip 대표(C910 7·W4 5·D3 2·C12 5·W5 7·005 8·G2G-003 10·GR-002 2) · EXEMPLAR-T1·GLINK · currency-4 잔여(G12-002 1·C2 6·N9 2·F68 13) · 원복 8 전원 baseline green(C5-SecurityPreWindow 5·G2G-004 9·N2 7 등).
- **shadow(엔진 픽스처 신설 무영향 실증)**: `R4S3c-ShadowOldNew` **PASS**(OLD-vs-NEW IDENTICAL) + `R4P4-ShadowRun` **PASS**(bit-identical) → TfxOnKnockOutDeleteOpponent inert 확증. `RLB2-01` **PASS**(profile 게이트).

### (6) 잔여 실부채 = B6 비블로킹 근거 (통화 소비 여부 확정)
- **RD-R4B6-P1-2**(R2-Del): **통화 비소비 확인** — R2-Del 통화 0으로 드레인(본 배치). red 본문(시큐리티-finisher departure 트리거 미급전)은 존치하나 삭제-표면 소비 0 ⇒ **B6 비블로킹**(원자 삭제 후에도 동일 red, currency 무관).
- **C5-Witness 8·NewTimings 2**: **통화 소비 잔존**(preamble-only 드레인 회귀 → 원복). 이들은 항2 통화를 보유하므로 **currency-비소비로는 B6-비블로킹 불성립** — 대신 **B6-동시 re-pin**(본문 펌프-하네스 재구축)으로 원자 배치가 흡수(=通貨는 body 삭제와 동시 소멸). ⇒ B6 블로킹 아님(재핀 대상)이나 근거는 "통화 비소비"가 아니라 "B6-동시 처분 등재". §3.4g 투영 대비 **정정 사항**.
- **구조골 2(FAILd-07·E3)**: 항3/항6 = B4/B5 소진 예정, 통화 축(항2) 무관.

### (7) 남는 리스크
1. **preamble-only-drainable 실측 5 (§3.4g 투영 13 정정)**: 통화 드레인의 게이트 = 본문 구동 방식. `ApplyActionAsync`-gameplay/env/Setup-staging 본문 8은 B6 원자 flip에서 legal-lane 재핀과 동시에만 드레인. 사전-드레인 가능분은 sink/sweep 5로 소진 완료.
2. **RD-R4B6-P1-2 실갭**: SecurityResolver↔BattleResolver finisher departure 트리거-큐 대조 = 병행 보안 트랙, 소형-초과(B6 비블로킹).
3. **§3.4f ①9 키워드/보안 창 red**: 병행 Sonnet 트랙 = 실 B6 게이트, 본 배치 무접촉·무변.

## §3.4i B6 최종 cut-plan (정본 — 코디네이터 확정, pre-flight 2회 반영)
**판정**: (a) DcgoMatch 기본 ctor+`?? new MetadataActionProcessor()` 폴백=**존치** — AdvancePhase/EndTurn body 삭제 후 MetaAP=순수 retained substrate(OLD-성은 body와 함께 소멸), 턴-흐름 구동 소비자 12만 flip. (b) DefaultMemoryPassValue=**MetaAP 잔류**(클래스 존치, AS-IS 잠프-3 미러 참조 주석).
**멤버-단위 삭제 지도**(소스 검증 완료): MetaAP=AdvancePhase/EndTurn body(:969-1155)+dispatch arm(:66-67→Illegal)+memory-arm eval 호출(:1170/1194/1229)+EoT-마커 write — 클래스·RequestChoice/ClearChoice·memory arm 자체·시스템/존 arm=존치 / MainPhaseFlow.cs 318L 전체(참조 소멸 경로: GameLoop:107 !pump 블록·OLD PassAction:19·MetaAP :989/:1080/:1100/:1170/:1194/:1228) / EarlyPhaseFlow.cs 277L 전체(MetaAP:977+E3) / OLD PassAction.cs+MetaAP:27 dispatch / GameLoop !pump 블록(:93-109)+EoT 마커(WindowResolutionController:25/29) / 디스패처 OLD arm(:70-104+BuildBreedingActions — 펌프 분기 :53-68 존치).
**소비자 최종**: 항2 통화 4(G2A-006·C2·G12-002·R4S3c=은퇴/재핀 대상)·E3 2·G2E-005 const 1(잔류로 해소)·basic-ctor flip 12(R4RL-02/03/04·G1E-005·R4P4·M2-001·L4-001·G1A-004·GPT4·G12-002·G1A-002·G1C-001 — 계약 테스트는 존치 ctor라 flip 불요 재확인, 턴-흐름 구동분만).
**witness 클러스터 4 처분 기준**: 발명-단언(memory-pass phase·EndTurn 제안·스텝 경계·flow 소스-sniff)=은퇴 / 실룰 기커버 대조(TurnEndMinMemory=FAILd-07·memory-cross=GR-001·breeding=GR-002/D6·unsuspend=N9·sickness=N1) / 미커버 실룰만 이식 — 단언별 3분류 표 필수.

## §3.4j B6-최종2 실행 기록 — 확장 스코프 원자 삭제 착지 (2026-07-19, main HEAD=7f20f763 파생, 메인 워킹트리·미커밋)

§3.4i 정본 cut-plan대로 원자 실행 완료. **OLD 스텝-케이던스 드라이버 = 물리 소멸.** build 클린(엔진 0오류·0경고 + 전 테스트 프로젝트 454/454 빌드 클린).

### (1) 물리 삭제 목록 (파일·멤버·줄수)
| 표면 | 처분 | 줄수 |
|------|------|-----:|
| `Headless/Runtime/HeadlessMainPhaseFlow.cs` | 파일 삭제(invented eval 전체 + MainPhaseMemoryResult) | −318 |
| `Headless/Runtime/HeadlessEarlyPhaseFlow.cs` | 파일 삭제(Unsuspend/Draw/Breeding 블록 + PhaseTransitionResult) | −277 |
| `Headless/Runtime/PassAction.cs` (OLD) | 파일 삭제(GR-001 재조준 실증: 정본=TurnFlowDriver→미러 PassTurn→EndTurnProcess). 동거 `CheatActionGuard`(retained)는 `CheatActionGuard.cs`로 verbatim 재홈(+56) | −134 |
| `MetadataActionProcessor.cs` | AdvancePhaseAsync/EndTurnAsync body+drain(:969-1155)+EoT-마커 write+MetadataWithTurn/MetadataWithPhaseTransition/AddMainPhaseMetadata 삭제; AdvancePhase/EndTurn/Pass arm→Illegal; SetMemory/AddMemory/PayMemory arm=존치 substrate poke(`EvaluateAfterMemoryMutation` 호출 제거); `DefaultMemoryPassValue` 상수 재홈(§3.4i (b)); 클래스=순수 retained | −263/+26 |
| `HeadlessLegalActionDispatcher.cs` | OLD (phase,cursor) 표+BuildBreedingActions+IsMovableBreedingDigimon+ReadDp 삭제 — 펌프 분기=유일 표(비-펌프=∅) | −157/+18 |
| `HeadlessGameLoop.cs` | !pump memory-pass 블록(:93-109, EvaluateAfterMemoryMutation 좌석) 삭제 — GameLoop 본체=펌프-공유 존치(§3.4i) | −17/+3 |
| `WindowResolutionController.cs` | `EndOfTurnDrainedTurn` 마커 삭제(AS-IS=효과별 캡; 셸=EngineContext 플럼빙 존치) | −17/+9 |
| **src 합계** | | **−1,211/+140 (순 −1,071)** |

기본 `DcgoMatch` ctor+`?? new MetadataActionProcessor()` 폴백=**존치**(§3.4i (a) — body 삭제 후 MetaAP=순수 retained substrate; 계약 테스트 flip 불요 실증: G1A-002/G1A-004/G1C-001/GPT4/G2A-001/G2A-002 전원 무접촉 green).

### (2) 스위트 은퇴 (디렉터리 4 + subtest)
`G2A-006`(OLD 디스패처 시퀀스=검증대상 소멸)·`R4S3c-ShadowOldNew`(shadow, secwin은 R4S3b 이식 완료분이 계승)·`R4P4-ShadowRun`(OLD-vs-OLD sanity=RLB1-01 상위집합)·`G2A-003`·`G2A-004`(아래 처분 표) + `G1E-005` RequestChoice-pause 5 subtest/goal-row/sniff(直 ChoiceController 계약 1건만 존치). tests 순변경 −3,796/+768.

### (3) witness 클러스터 4 처분 표 (단언별 3분류 — §3.4i 기준)
**G2A-004 (11) — 전량 은퇴/기커버, 프로젝트 삭제**: goal-row·AS-IS sniff·TODO sniff=은퇴(발명 test-infra) / AdvancePhase→Main 스텝 단언=은퇴(펌프 자동흐름·도달은 EXEMPLAR DriveUntil 계열 커버) / ExplicitPass 메모리-패스 phase+메타=은퇴, 실룰 "pass=상대 3"=**기커버 G3.5-S1.MemoryEquivalence 재핀**(P1/P2 대칭) / EndTurn 핸드오프 +3=기커버 GR-001+MemoryEquivalence / Pay·Set 임계=기커버 GR-001(auto-flip) / AddMemory 양수 유지=기커버 MemoryEquivalence(spend-to-0·partial) / Pass 위상·소유 가드=기커버 G2E-005 재핀(펌프 legality 경계 InvalidAction).
**G2A-003 (11) — 은퇴 8 + 이식 3, 프로젝트 삭제**: goal-row·sniff·스텝-순서·비-턴-AdvancePhase 가드=은퇴(검증대상=OLD 스텝) / breeding hatch·move=기커버 GR-002·G3.5-D6 / deck-out=기커버 R4P2a DrawDeckOut·RL-C1 / **이식 3(→R4P2a-PhaseBodies, 펌프 미러 유닛)**: 첫턴 드로 스킵(AS-IS :669)·후속턴 1드로(:682)·**상대 <Reboot> 언서스펜드 행동**(:226, 실카드 BT2_063 등록 + plain 음성 대조 — T2A는 등록만 커버했던 미커버 실룰).
**G2A-005 (9) — 은퇴 4 + 재핀 5(파일 존치)**: goal-row·AS-IS sniff·TODO sniff=은퇴 / memory-pass-EndTurn 변형=기커버(펌프 단일 무조건 cleanup 좌석 TurnStateMachine :670 + R4P2a EndResetList) / **cleanup 스코핑 매트릭스 5(each/owner/opponent/persistent/hand-untouched/attack-reset)=retained-substrate 직접 `HeadlessEndTurnCleanupFlow.Cleanup` 유닛 재핀**(C-Del 판례; OLD 액션-메타 읽기→동일 관측 EndTurnCleanupResult 읽기, 5/5 green).
**G3.5-S1.MemoryEquivalence (7) — 전량 펌프 재핀(파일 존치, 7/7 green)**: GR-001 패턴(실코스트 플레이+Pass 레인+auto-flip). 실룰 전량 보존: overshoot K 핸드오프·**2nd-플레이어 대칭**·voluntary pass=3(양측)·spend-to-0 유지·partial 유지·multi-turn 체인 — GR-001 미커버분(대칭·pass=3·체인)의 유일 witness로 존속.

### (4) 선행 소진·재핀 결과 (전후 verdict)
| 스위트 | 처분 | 결과 |
|--------|------|------|
| G3.5-C2 | memory-pass 3 은퇴 + main 3 재핀(#6=비-자기-Main 수동공격 거부로 번역) | 3/3 green |
| G12-002 | G12-004 패턴 재핀 + TfxMultiSelect를 uniform ActivateClass(ST1_16 관용구)로 재표현(실 UseOptionClass 경로 구동) + 생존자 대조 C 신설 | 1/1 green(단언 강화) |
| E3-Witness (5a/5b) | EarlyPhaseFlow 드라이브→펌프 미러 ActivePhaseAsync 좌석 재핀 | base-red(5a)→**green**(OLD-artifact red: OLD 플로우가 창 수집 우회; 펌프 IUnsuspendPermanents 좌석=AS-IS 발화) |
| 미등재 클러스터 3 | G3.5-F4(EndTurn→substrate OnceFlags.ResetForTurn)·G3.5-OPT2(AdvancePhase preamble→SetPhase(Main))·G3.5-W1b(EndTurn 액션→펌프 Pass 턴-경계, delta 단언) | F4/OPT2=**verdict 항등 실증**(diff 동일; 잔red=선재 stale registry-probe 직교부채)·W1b 5/5 green |
| G2E-005 | DefaultMemoryPassValue→MetaAP 상수 re-point + PassAction.Process 가드 2건→펌프 legality 경계 재핀 + sniff 목록 갱신 | 10/10 green |
| M2-001 | 실-매치 slot-정렬 subtest 펌프 재핀(무-레인 스텝-포워드; hand 292·attack 189 체크 실측) + strip probe 펌프 정합 | 11/11 green |
| L4-001 | CreatePumpDriven 재핀 + **RD-R4A′-01 펌프 재검**: 풀-랜덤 5매치×2회 완주, hang 비재현 | 4/5(잔red 1=RD-P6C1-8 STOP 정직 red — failed-play hand restore 미포팅, 트래젝토리가 STOP 경계 도달) |
| R4RL-03 | Phase-2 풀게임 워크=턴-흐름 구동 적발(미등재) → PendingHandChoicePumpMatchAsync 재핀 | 12/12 green(seed-replay 지문 포함) |
| G1A-002/G1A-004/G1C-001/GPT4/G2A-001/G2A-002/RL-A1/RL-A2/RL-A4a | 존치-ctor 계약=무접촉 재확인(§3.4i) | 전원 green |

### (5) 게이트 실측
- **build**: 엔진 0오류·0경고(풀 리빌드) + **테스트 454 프로젝트 전부 빌드 클린**(삭제 표면 잔존 참조 0 실증).
- **회귀 대표 60+**: EXEMPLAR-T1/T2A/T2B/T3A/T3B·GLINK·PILOT-S1~S4·R4S3a·R4S3b(**secwin 이식분 포함**)·R4R3-01/02·R4RL-01/02/03/04·RLB1-01·F62·GR-001/002/004/006·C-EoT2·W-EoTFIX·A4·G7-005·G12-004·N1·N9·D6·FAILd-03/07·전투/보안 클러스터(G2G-001~004·G3.5-005/007/A3/C910/C12/D1/D2/D3/N2/W4/W5/F68·G9-062·C5-SecurityPreWindow) 전원 green.
- **RLB2-01 다이제스트**: PASS(profile 게이트 완주).
- **red-보존(선재 직교부채, verdict 항등 실증)**: C5-Witness 8(diff 항등 확인)·PRIM-P0.NewTimings 2·R2-Del 1(RD-R4B6-P1-2)·RD6 2(RD-R4B4-RD6a/b 정직 red)·F4 1·OPT2 3(stale registry-probe).
- **줄수**: 전체 **−5,007/+908 (순 −4,099)**; src 순 −1,071.

### (6) 원장 폐쇄
- **S3c-d 은퇴 원장 전 항 폐쇄**(r4_tsm_s1_design §S3c-d 갱신): 항1(GameLoop=펌프-공유 존치·!pump 블록만)·항2/3(MetaAP body)·항4(마커)·항5(TurnEndMinMemory=AutoProcessing 정본 승격 확정)·항6(EarlyPhaseFlow)·항8(OLD choice-injection affordance=G1E-005 은퇴; out-of-pump throw 경로=§3.1c retained 확정 유지)·항11(디스패처 OLD arm) — **물리 소멸 완료**.
- **RD-S3C-01**(OLD [When Digivolving] 이중발화)·**RD-S3C-02**(OLD ST1_15 throw): OLD 드라이버 물리 삭제로 **소멸 확정 폐쇄**.
- **P2-⑥**(수동 Install 풋건): 폐쇄 — 프로덕션 경로 전부 Reinstall(DcgoMatch)·수동 Install 잔존=R4S3a/S3b 하네스 3사이트(단발 Install·Reset 미사용=교착 전제조건 무), grep 실증.
- **RD-R4A′-01**(L4 latent hang): 폐쇄 — OLD-only 노출 표면 소멸 + 펌프 풀-랜덤 재검 비재현(L4 재핀 게이트 통과).
- **RD-R4B6-P1-2**·**RD-P6C1-8**·키워드/보안 창 직교부채(C5-Witness 8·NewTimings 2)·stale registry-probe(F4/OPT2)=**존치 원장**(B6 스코프 밖, 병행 트랙 몫).

## §3.5 완료 선언 — 4b 골 종점 도달 (2026-07-19)

**소비자 0 → 물리 삭제 → 검증 green: 4b의 종점 조건 충족.** OLD 스텝-케이던스 드라이버(발명물)는 엔진에서 물리적으로 소멸했고, 턴 케이던스의 유일 소유자는 AS-IS 미러 TurnFlowPump(TurnStateMachine 페이즈 바디)다. AdvancePhase/EndTurn 액션 통화 소비자 = 0(RL-A1의 펌프-illegal 거부 단언만 잔존 = 정당 keeper). 기본 ctor=존치 스크립팅 프로파일(MetaAP=순수 retained substrate). 구조 지표: 발명 드라이버 파일 3 삭제·발명 페이즈 표/마커/메모리-패스 모델 0·미러 위반 신규 0.
**남는 리스크**: ①키워드/보안 창 직교부채(C5-Witness 8 등)=병행 Sonnet 트랙 ②RD-P6C1-8(L4 잔red 1) ③RD-R4B6-P1-2 ④metadata `hasReboot` 소비자=OLD-전용이었으므로 사망(GR-007 sink 계약은 존치 green; 라이브 Reboot=R4P2a 신설 witness가 커버) — 키워드 grant 경로가 metadata 카서를 쓸 경우 라이브-클래스 grant로의 재하우징은 키워드 트랙 몫.

## §3.4k B6-fix (최종 게이트 여파 4건, 2026-07-19)
G2Z-001=G2A-006 은퇴 재핀(RetiredTestProject 역단언, G3Z 판례)·RL-A5/RL-V=누락 legacy-ctor 소비자 2 펌프 flip(재핀 불요 — 펌프가 단언 전량 재현)·G8-006=**선재 갭 노출**(RD-R4B6-P2-1: 펌프 디스패처 SpecialPlay 미제안(STOP RD-P6C1-5/R5-04) vs validator AgentFacingTypes 등재 불일치 — pre-B6에도 펌프-매치엔 존재, 비-펌프 폴백이 가림막이었음; SpecialPlay 클러스터 포팅 시 동시 해소)·L4-001=정직 red 확인(RD-P6C1-8 failed-play hand restore). 최종 모수: E3 flip-out, G8-006·L4-001 정직-red 등재.

## §3.6 최종 적대 리뷰(리뷰 지점 3, 2026-07-19): GO — 4b 완주 확정
P0/P1 0. 렌즈 5 전부 확인: 삭제 잔재 0(라이브 참조 전량 묘비/부정단언/미러-sniff)·과잉 삭제 없음(EvaluateAfterMemoryMutation=발명 확증, AS-IS 실기제 EndTurnCheck는 펌프 전 페이즈 배선 실증·CheatActionGuard verbatim)·처분 정직(표본 10+ 실룰 소실 0, E3/S1 등은 오히려 강화)·정직-red 2=커밋-계보로 선재 실증·flip 표본 5 전부 실재조준. P2 3(문서 정밀도): ①§3.5 keeper 집합에 R4S3b 부재-단언 추가 기술 ②fail-set 107 수치=코디네이터 게이트 실측으로 확인됨(리뷰는 read-only 한계) ③MetaAP :995 주석 보편 주장 완화 — poke-트리거 의존 witness 0이라 행동 무노출.
**4b 종점 도달**: 발명 턴 드라이버 물리 소멸·소비자 0·원장 폐쇄 전항·인계 원장(수리 잔여/B군/Sonnet 트랙) 정합.
