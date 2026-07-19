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
