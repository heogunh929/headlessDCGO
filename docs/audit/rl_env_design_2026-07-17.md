# RL 환경 트랙 — 착수 감사 + 설계 초안 (2026-07-17, post-R4 flip)

Base: `0b339cfc`(main, RD-R4P4-01 가족 완결). 조사+설계 전용(엔진 무수정). 선행 문서:
[rl_development_roadmap.md](rl_development_roadmap.md)(M1·M2·M4 완료), [internal_rl_training_environment_roadmap.md](internal_rl_training_environment_roadmap.md)(L0·L1·L4 완료),
[rl_seat_protocol_v1.md](rl_seat_protocol_v1.md), [r4_tsm_s1_design_2026-07-16.md](r4_tsm_s1_design_2026-07-16.md).
이 문서의 지위: **R4 펌프 flip 이후 재착수 기준선** — 기존 M/L 트랙 자산의 pump-era 유효성 재감사 + 잔여 결정 지점 D1~D7 확정.

## 0. 핵심 발견 (요약)
1. **RL 스택은 신품이 아니라 재검증 대상** — 어댑터 26파일(M1)·정보집합 관측(M2)·seat 프로토콜+Python 학습 슬라이스(M4/L0/L1)가 기존재하고, 펌프 flip(S3c-d1)이 두 소비자(`HeadlessRlEnvironment`, `SeatMatchHost`) 모두에 이미 반영됨(§1). 금일 재실행: M4-001 **9/9 green**, R4RL-01 **6/6 green**.
2. **성능 실측이 추정을 뒤집음**: 랜덤 셀프플레이 10판(Release 호스트, ST1vST2) = **5.7 agent-steps/sec, 판당 중앙값 11.2초**(§7). 기존 추정 "판당 수십~수백 ms"·pre-R4 L0 실측 94.6 steps/sec 대비 판당 ~4배, 스텝당 ~18배 저하. 전송(JSON)이 아니라 엔진(펌프 드라이브)이 병목 — D3/D5/D7의 1급 결정 인자.
3. **다중-선택 교착 사각은 현 풀에서 비발화**: SelectionValidator 부착처 5곳 전수 — 포팅된 검증기 보유 카드 4장 전부 "비어있지 않으면 통과"형(싱글턴-세이프). 교착 성립 조건 (a)(b) 모두 현 209장 풀에서 도달 불가, 단 AS-IS에 합계-DP/합계-레벨형 조인트 술어가 실재(BT20_098 등) → **풀 확대 전 상환 필수**(§4, D1).
4. **stale 계측 3건 확정**: G13-003 잔여 red = `EffectResolved` 이벤트 계측이 OLD-드라이버 드레인 카운터에 결박(행동은 green); type-슬롯 `ActionEncoder` 기본 순서에 메인 카드액션 5종 전부 누락(UNKNOWN 붕괴); determinism verifier 2종 호출부 0(§1.3, §2).
5. **벡터화 전제(ambient 격리)는 구조적으로 충족**: `AmbientMatchContext`=AsyncLocal, 미러층 전역은 전부 `ConditionalWeakTable<EngineContext,…>` per-match. 유일 가변 static은 Tfx 테스트 픽스처(프로덕션 풀 무관). 단 in-process 병렬 실증 테스트는 사문(§5, D5).
6. **카드 풀 실측 = 209장**(~200 추정 확인), 그중 STOP 마커 29장 → 클린 ~180장. **주의: BT3=0**(이 트리에 BT3 포팅분 없음 — 메모리 기록과 상이, 착수 전 확인 필요). 카드 정의 DB 8,143종·vocab 4,206 canonical — id 공간 예약 문제는 이미 해결(§6).

---

# 1부. 현황 감사

## §1. RL 표면 인벤토리

### §1.1 HeadlessRlEnvironment (in-process 계약)
`src/HeadlessDCGO.Rl/Runtime/HeadlessRlEnvironment.cs`(397줄):
- **매치 생성**: 기본 매치 = `DcgoMatch.CreatePumpDriven`(:37-40, R4 S3c-d1 승격 반영) + `EnforceAgentActionLegality`(기본 true, A1 경계)·`StrictUnbound`(R2-4 프로파일) 옵션(:363-397).
- **reset/seed**: `InitializeAsync(MatchConfig)`(:43-50)·`ResetAsync`(:52-57) — seed는 `MatchConfig.RandomSeed`가 권위(`src/HeadlessDCGO.Engine/Headless/Runtime/DcgoMatch.cs:639-644`에서 컨텍스트 RNG reset). 양쪽 다 `DrivePumpToDecisionAsync`(:247-263, bound 128)로 첫 결정점(멀리건 choice)까지 자동 진행.
- **step**: `StepAsync(LegalAction)`(:160-207) = 마스크-멤버십 거부(옵션) → `ApplyActionAsync`(apply-시점 거부 = 무변이) → `StepAsync` + `DrivePumpAfterActionAsync`(:217-242; 액션이 풀어준 펌프 세그먼트는 다음 스텝에 돌므로 후속 1스텝 + quiescent-empty 루프). 인덱스 진입 4종: ActionId/EncodedKey/ActionIndex/**FactoredIndex**(:136-153).
- **관측/보상**: `Encode`(:265-298) — perspective 필터 스냅샷(A4; 고정 `PerspectivePlayerId` 우선, 아니면 턴 플레이어 시점 :303-312) + factored 마스크 동봉(A5) + `TerminalRlRewardCalculator`.

### §1.2 SeatMatchHost (프로세스 경계 계약, 프로토콜 v1)
`tools/RlBridgeHost/SeatMatchHost.cs`(636줄) — JSON-lines 상태기계(스펙: rl_seat_protocol_v1.md). 금일 핸드셰이크 실측:
**obsSize=3088**(infoset-v1), **actionSize=599**(factored-v1), **vocabSize=4206**.
- reset: `seed`·`maxSteps`(기본 2000)·`decks`(starter 쇼트핸드/레시피) → `EngineContext.CreateDefault(randomSeed: seed)`(:203) → **펌프 매치**(:263-267) + 펌프-era setup 계약 명시(손 0·시큐리티 0·setup-멀리건 off — StartGameAsync가 소유, :233-239).
- step: `action{seat,index}` → factored 마스크 조회 → 불법이면 상태 무변경 + 동일 turn 재발행(:296-299) → apply+step+`DrivePumpToDecisionAsync(afterAction:true)`(:309, 호스트 내 중복 구현 :316-339).
- 종결: `result{rewards(승 +1/패 −1/무·캡·중단 0), reason}`(:409-444). 예외 시 `internal` error + 보상 0 종결(:88-98). RESULT JSONL(:446-471)·L4 이벤트 로그(:243-259).
- 교착 표면화 기존재: `no_mappable_action`(:384)·`stalled`(:404)·`step_cap`(:354).

### §1.3 stale/OLD-표면 잔재 (인벤토리)
| 잔재 | 위치 | 상태 |
|---|---|---|
| type-슬롯 `ActionEncoder` 기본 순서에 `PlayCard/Digivolve/ActivateOption/ActivateMain/SpecialPlay` 부재 → 전부 UNKNOWN 슬롯 붕괴 | `src/HeadlessDCGO.Rl/Runtime/ActionEncoder.cs:109-139` | 학습은 factored로 대체됐으나 `StepByActionIndexAsync`/`ToMaskVector` 소비자에겐 퇴화 표면. 재조준 또는 은퇴 |
| factored 스키마의 `AdvancePhase`/`EndTurn` 레인(인덱스 2·3) | `FactoredActionEncoder.cs:36-37` | 펌프 매치에서 영구 마스크-0(사문 레인). **오프셋 안정성 위해 유지**(D1) |
| `HeadlessSmokeScenarios.AdvancePhaseToDraw` 등 OLD-cadence 시나리오·`HeadlessScenarioRunner`/`HeadlessSmokeSuite` | `HeadlessSmokeScenarios.cs:579`, `HeadlessSmokeSuite.cs:169` | pre-R4 스캐폴드(plain ctor). 펌프 재조준 대상 |
| `HeadlessDeterminismVerifier`·`HeadlessBatchParallelDeterminismVerifier` | 동 디렉터리 | **호출부 0**(사문 인프라). 배치 1에서 실행 witness로 승격 |
| G13-003이 legacy ctor(`new DcgoMatch(..., actionLegality:)`)로 구동 | `tests/G13-003.RandomSelfPlaySmoke.Tests/Program.cs` | 펌프 재조준 대상(§2) |
| A1/A2의 RL-e2e 테스트 "LEGACY TEST SCAFFOLD (R4 S3c-d1)" 주석 | `G3.5-RL-A1/Program.cs:184`, `G3.5-RL-A2/Program.cs:115` | green이지만 OLD cadence를 고정 — 펌프-cadence witness 부재 |
| `RlTrainingDataset`/`RlTransition*`/`JsonlExporter`/`HeadlessEpisodeFingerprint`(coarse 자인 :5) | 동 디렉터리 | OLD-era 수집 계열 — L 트랙이 JSONL/이벤트 로그로 대체. 용도 재판정 |

## §2. 검증 커버리지 매트릭스
(테스트는 전부 Exe 콘솔 하네스 — `[Fact]` 0개, `dotnet test` no-op, `dotnet run`으로 구동. 개수 = 테이블 엔트리.)

| 스위트 | 수 | 고정하는 계약 | 미커버/비고 |
|---|---|---|---|
| M2-001.InfoSetObservation | 11 | vocab canonical 규칙(Python 동일)·append-only·PAD=0; 히든존 per-card 인코딩 즉시 실패; choice 후보 정체(비선택자 strip); **관측 슬롯↔액션 레인 정렬 불변식**(라이브 400스텝, hand 검증 >0 강제) | 9/11이 합성 스냅샷; choice-레인 정렬은 라이브 미단언; ST1/ST2 한정 |
| M4-001.SeatProtocol | 9 | welcome 안정 해시; 0-거부 완주; 보상 귀속 +1/−1·step_cap 0; 동일 seed+액션열 결정론(관측 지문); 불법 액션 무변경+재발행; 레시피 공급·미지원 카드 명시 실패 | in-process 구동(실 stdio 미검증); 중도 reset·좌석 경합 없음. **금일 9/9 green** |
| G13-003.RandomSelfPlaySmoke | 10속성 | 실카드(ST1/2/3) 5매치업 랜덤 셀프플레이: invalid 0·flowCap 0·교착 0·자연 종결·플레이/공격 발생·seed-replay 지문 일치 | **9/10 green, red 1 = stale 계측 확정**: `Program.cs:72`의 `EffectResolved>0` — 이벤트 발행이 `DcgoMatch.cs:245-252`에서 OLD `GameFlowProcessor` 드레인 카운터에만 결박, 창-경로 해소는 계수 안 됨(전 판 효과 0으로 표시되나 시큐리티 소진 승부 발생 = 행동 정상). `r4_tsm_s1_design_2026-07-16.md:71` 판정과 일치. 부수: `SecurityCheck` 이벤트는 발행처 자체가 0(무단언이라 무해) |
| G3.5-RL-A1.ActionLegality | 8 | 단일 권위 합법성 경계: 위조 SpecialPlay 포함 불법 액션 apply-거부(상태 완전 무변이 증명), `LegalActionSetValidator.AgentFacingTypes` 12종 | RL-e2e 테스트가 OLD cadence(LEGACY SCAFFOLD :184) — 펌프판 witness 이연 |
| G3.5-RL-A2.ChoiceAsAction | 6 | choice=액션: 후보별 ResolveChoice+skip, 소유자에게만 노출, 선택 반영·skip 기록, 무-choice ResolveChoice 거부 | 동일 LEGACY SCAFFOLD(:115); 펌프 choice 경로는 S3a/S3b 스위트에 위임 |
| G3.5-RL-A3.FactoredActionSpace | 9 | 레인 연속성·슬롯 구별성·직접공격 슬롯·overflow=Unmapped 표면화·실매치 왕복 | **다중-선택 부분집합 열거 미구현·미테스트**(이연 실체는 §4) |
| G3.5-RL-A4/A4a/A4b | 4/6/5 | strictUnbound 하드 실패+포렌식 마커; perspective 필터(타인 히든존 id 0·자기존 유지·count 보존); per-card 피처(DP=B1 계산기 경유)·고정 슬롯 | A4b가 카드당 10피처 폭을 하드코딩(스키마 성장 시 red); 합성 픽스처 위주 |
| G3.5-RL-A5.FactoredMaskInStepResult | 5 | 매 RlStepResult에 factored 마스크 동봉 = 단독 인코딩과 동일(0/1·비트↔액션 전단사) | reset+1스텝만; choice-레인 내용 미검증 |
| G11-002.RlDeferredChoiceE2E | 1 | 이연 choice 서스펜드→resume에서 **비용 1회 지불**(메모리 5→2 유지) e2e | 단일-선택·합성 ST2_16 재현 1장; OLD AdvancePhase 스캐폴드 |
| R4RL-01.RolloutBlockers | 6 | RD-S3D-01(디스패처 표=검증기-통과 싱글턴만)·RD-R4P4-02(auto-select 검증기 존중, AS-IS 재시도)·RD-R4P4-01(bare 소비자 ambient NRE 해소) | **금일 6/6 green**. 잔여 미상환 = §4 교착 본체 |

**커버리지 공백 종합**: ① 펌프-cadence에서의 A1/A2/G11-002/G13-003급 witness(전부 OLD 스캐폴드) ② 다중-선택 ③ in-process 병렬 결정론 ④ 실 stdio 전송 ⑤ 처리량 베이스라인(수치 고정 테스트 없음).

## §3. 액션/choice 표면 전수

### §3.1 디스패처가 내는 액션 (펌프 매치)
`src/HeadlessDCGO.Engine/Headless/Runtime/HeadlessLegalActionDispatcher.cs`:
- 우선순위: terminal→빈 표(:16-19) ≺ **pending choice(소유자만, 타자는 빈 표)**(:23-34) ≺ 비턴 플레이어 빈 표 ≺ 펌프 분기(:53-68).
- 펌프 분기 = `(Main, PhaseStart)`에서만 표 생성, **6레인**: `Pass`·`PlayCard`·`Digivolve`·`ActivateOption`·`ActivateMain`·`DeclareAttack`(:60-65). **`SpecialPlay`는 펌프에서 의도적 제외**(:47-52 — DigiXros/Assembly STOP 클러스터 대기; legacy 분기 :83-91에는 있음). `AdvancePhase`/`EndTurn`은 펌프에서 불법(cadence를 펌프가 소유).
- 멀리건 = 액션이 아니라 `ChoiceType.Mulligan` choice(`MulliganCoordinator.cs:113-133`, redraw 후보 `mulligan:redraw`); 브리딩 = `ChoiceType.BreedingDecision` choice(`TurnStateMachine.cs:330-358`, min0/max1/skip=사양, hatch>move 우선 :371) — 둘 다 ResolveChoice로 표면화.
- ResolveChoice 열거(:221-267): Count형=n별 SelectCount; `MinCount<=1<=MaxCount`=후보별 size-1(**`SelectionValidator({id})` 필터**, RD-S3D-01 :245-249); CanSkip=Skip 1개. **MinCount>1 부분집합 열거는 명시 이연**(:214-220).
- 액션 종류 총어휘: 문자열 상수 34종(`HeadlessActionTypes.cs:3-38`), 에이전트-대면 부분집합은 `LegalActionSetValidator.AgentFacingTypes` **12종**(`LegalActionSetValidator.cs:23-41`).

### §3.2 LegalAction·ActionMask 구조
- `LegalAction`(`Services/LegalAction.cs:6-52`) = `Id`(합성 `{player}:{type}[:{key}]`, `HeadlessActionFactory.cs:480-495`) + `PlayerId` + `ActionType`(문자열) + `Parameters`(dict). 실사용 키(`HeadlessActionParameterKeys.cs`): PlayCard=`cardId/memoryCost/fromZone/toZone`; Digivolve=`cardId/targetCardId/memoryCost`; ActivateOption=`cardId/effectId/memoryCost/skillIndex`; ActivateMain=`cardId/effectId/skillIndex`; DeclareAttack=`attackerId/defendingPlayerId/attackTargetId/isDirectAttack`; ResolveChoice=`choiceSkipped|choiceSelectedCount|choiceSelectedIds(HeadlessEntityId[])` 중 1.
- `ActionMask`(`Runtime/ActionMask.cs:5-58`) = 고정 벡터가 아니라 **LegalAction 리스트 record**(양 플레이어 concat, `HeadlessGameLoop.cs:218-227`).
- 고정-벡터 표현은 RL 어셈블리의 factored 스키마: 레인 배열 NoOp1·Pass1·AdvancePhase1·EndTurn1·PlayCard16·ActivateOption16·Digivolve16×16·DeclareAttack16×17(+1=플레이어 직공 슬롯)·ResolveChoice16+1(skip)·Hatch1·MoveBreeding1·SpecialPlay16 = **599**(`FactoredActionEncoder.cs:33-49`). overflow/미배치/충돌 = `Unmapped`로 표면화(:224-229). **ResolveChoice 레인은 첫 선택 id만 인코딩(:355-364) — 구조적 단일-선택**.

## §4. 다중-선택 교착 사각 (이월 설계 항목 정밀화)
- **빈 표 성립 조건**: (a) `MinCount>1 && !CanSkip` → 항상 빈 표(A3 이연 본체). 도달 모양 = batch select `canNoSelect:false && canEndNotMax:false && maxCount>=2`(`SelectHandEffect.cs:432` 등) — **현 포팅 코퍼스에 이 모양 0장**(grep). (b) `MinCount<=1` + 검증기가 모든 싱글턴 거부 + `CanSkip=false` → 빈 표.
- **SelectionValidator 보유 choice 전수**: 부착처 5곳/ChoiceType 2종 — `SelectCardEffect.cs:743-753`(Card)·`SelectHandEffect.cs:433-443`(Card)·`SelectPermanentEffect.cs:191-194,697-708`(Permanent)·`RevealLibrary.cs:533-545`(Card). Mulligan/Blocker/WindowChoice/BreedingDecision 등 나머지 16 ChoiceType는 무부착. incremental(byPreSelectedList) 경로는 싱글-픽 루프라 교착-세이프.
- **현 풀 실태**: 검증기 보유 포팅 카드 4장(BT2_092:76-84, BT2_095:74-82, BT2_080:131-139, ST1_15:89-97) 전부 `HasNoElement→false`형 — 싱글턴 항상 통과 → **(b) 현재 도달 불가**. (ST1_15의 seed-303 정지는 auto-select 좌석 건으로 RD-R4P4-02에서 상환 완료.)
- **AS-IS 잠복(포팅 시 발화)**: `DCGO/Assets/Scripts/CardEffect/BT20/Purple/BT20_098.cs:87-90`(합계 레벨=목표 — 싱글턴 전멸형, skip 탈출은 있음; 포팅측은 7줄 skeleton), LM_021(합계 DP), EX7_047(합계 코스트), BT17_051·BT11_107(누적 합), ST24_10·ST24_06·BT25_035(Count==2형), BT9_012·BT22_072/073·EX5_073(distinct-레벨). 다수가 `canNoSelect:true`(skip 탈출)라 하드 교착은 "조인트 게이트 + canNoSelect:false" 결합 시.
- **표현 계층의 결핍**: 디스패처가 열거 못 하고(§3.1), factored 레인이 표현 못 하고(§3.2), A1 경계(`LegalActionSetValidator`)가 표-밖 합성 액션을 거부 — 3층이 함께 막혀 있음. 해소는 D1에 종속.

## §5. 결정론 / seed
- **단일 RNG per match**: `EngineContext.CreateDefault(randomSeed)` → `GameRandomSource`(xoshiro256**+SplitMix64, `Services/GameRandomSource.cs:13-34`). `MatchConfig.RandomSeed`가 initialize/reset 시 권위로 재주입(`DcgoMatch.cs:149,197→639-644`).
- **소비처 전수**: 셋업 덱 셔플(`MatchSetupFlow.cs:140`)·선공 결정(FirstPlayerId 미지정 시 :146-159)·인게임 라이브러리/시큐리티 셔플(`InMemoryZoneMover.cs:351,371`)·`ZoneState.Shuffle`(:162-168). AS-IS AI의 UnityEngine.Random은 결정론 ChoiceProvider로 의도적 대체(RD-W5-2).
- **보장 범위**: 동일 config+액션열 → 동일 궤적. 실증 — M4-001 결정론 테스트(관측 지문), G13-003 seed-replay, R4 shadow: `R4P4-ShadowRun`(OLD-vs-OLD **bit-identical** per-step 전체 상태 비교)·`R4S3c-ShadowOldNew`(OLD-vs-펌프, 공유 interactive-stop 경계 digest). 금일 프로브 부수 실증: Debug/Release 호스트 간 10판 스텝열 완전 일치(§7).
- **잠복 비결정성 1건**: 토큰/부여-지속효과 엔티티 id가 `Guid.NewGuid()`(`CardEffectCommons.cs:2493,2893,2949,2964,3183`) — 궤적 순서는 무영향이나 **id 문자열을 해시하는 미래 digest는 seed-무관 발산**. → 설계 항목 RD-RLENV-04.
- verifier 2종은 사문(§1.3) — 배치 1에서 승격.

## §6. 카드 풀 실측
- **포팅(행동 가능) = 209 카드 id**(reflection dispatch 기준: 카드번호=클래스명 `CEntity_Effect` 서브클래스, `CardEffectDispatch.cs:17-42`; skeleton은 클래스 부재로 자연 제외. 계수법 2종 일치, Tfx 93 제외).
- 세트 분포: BT1=86·BT2=46·ST1=12·ST2=11·ST3=11·ST4=10·BT9=5·{BT8,BT22,EX8,ST5}=3·{BT15,BT19}=2·단건 12세트. **STOP 마커 잔존 29파일**(부분 포트) → 클린 ~180. **BT3=0(전량 skeleton — "BT2/BT3 완료" 메모리 기록과 불일치, 브랜치/트리 확인 필요)**.
- 정의 DB: `cards.json` 8,187엔트리/8,143 distinct(전 카드 인쇄본) — vocab은 canonical 붕괴 후 **4,206**(변형 통합). vocab이 전체 DB 기반이므로 **풀 확대 시 id 공간 예약 불필요**, `CardVocabulary.Extend` append-only 기존재(`CardVocabulary.cs:90-109`).
- 부트스트랩 덱 재료: 스타터 ST1~ST4 4종 완비(각 12/11/11/10) + BT1/BT2 풀 — 제한-풀 컨스트럭티드 레시피 구성 가능.

## §7. 성능 실측 (금일, 신규)
프로브: seat 프로토콜 경유 랜덤(마스크-합법) 셀프플레이 10판, ST1vST2, seed 1000~1009, maxSteps 2000.

| 빌드 | steps/sec | 판당 ms (med/min/max) | 판당 agent-steps | 종결 |
|---|---|---|---|---|
| Release | **5.7** | **11,221** / 3,947 / 19,404 | 32~83 (전판 자연 종결) | 승부 10/10, cap 0 |
| Debug | 2.7 | 23,761 / 8,654 / 38,023 | 동일 스텝열(결정론 방증) | 동일 |

- 비교: pre-R4 L0 실측 94.6 steps/sec(4 env)·단일 env 랜덤 143(OLD 드라이버, roadmap M4 기록) — 스텝 정의가 다르나(OLD=AdvancePhase 마이크로스텝 포함, 펌프=결정점만) **판당 wall 기준 ~4배, 결정점당 ~175ms**. 메모리의 "판당 수십~수백 ms" 추정은 **기각**(펌프 경로 실측으로 치환).
- 병목 소재(1차 심증, 미프로파일): 전송 아님(스텝당 JSON ~30KB) — `DrivePumpToDecisionAsync` 루프가 반복마다 `GetActionMask()`(=양 플레이어 legal 전열거, 연속효과 라이브 스캔 포함)를 재계산 + 펌프 파크 세그먼트당 TaskRunner 스텝. → RD-RLENV-03(프로파일링은 배치 2).
- 함의: 30k-스텝 스모크 학습 ≈ 87분(직렬) — L0 재검증은 가능하되 고통, 대규모 학습은 최적화/벡터화 없이 불가.

### §7.1 B1 재실측 (2026-07-17 — B2 프로파일링 "before" 고정값)
하네스: `tests/RLB1-02.ThroughputBaseline.Tests`(신설). 측정 조건: **in-process**(`HeadlessRlEnvironment` + `DcgoMatch.CreatePumpDriven`, seat/JSON 전송 없음), Release 빌드, ST1vST2 스타터, env seed=policy seed=1000~1009 10판, maxSteps 2000, JIT 워밍업 1판 제외, 동일 개발기(Linux 6.17).

| 지표 | 값 |
|---|---|
| steps/sec (aggregate) | **5.7** |
| 판당 ms (med/min/max) | **11,790** / 2,799 / 24,850 |
| 판당 agent-steps | 45~98 (총 698), 자연 종결 10/10·cap 0 |

- §7 seat-프로토콜 실측(5.7 steps/sec, med 11,221ms)과 사실상 동일 — **전송(JSON/stdio)을 제거해도 처리량 불변 = 병목은 엔진 결정점 드라이브라는 §7 심증을 in-process에서 재확증**(D3 판단 유지, RD-RLENV-03이 B2 프로파일링 대상). B2의 before 수치는 이 표로 고정.

### §7.2 B2 엔진 프로파일 실측 (2026-07-17 — RD-RLENV-03 상환)
방법: dotnet-trace CPU 샘플링(RLB1-02 10판, seed 1000~1009, Release) + speedscope 귀속 스크립트 + 일회성 프로브 `tests/RLB2-01.EngineProfile.Tests`(엔진 소스 무계측 — 공개 표면 스톱워치 + 전-궤적 다이제스트 witness). 스텝 유형별 분해(before): choice-해소 스텝 평균 **96ms**, 메인 액션 스텝 평균 **183ms**(DeclareAttack 277 · Digivolve 189 · PlayCard 151 · Pass 140 · ActivateOption 126ms); 결정점당 양측 합법표 재열거 17.4ms; env.Observe(관측+마스크 인코딩) **0.1ms**.

**핫스팟 순위표 (before, 전체 CPU 대비 %)**:

| # | 함수/서브시스템 | incl | excl | 층 |
|---|---|---|---|---|
| 1 | `DigivolutionStack..ctor` (LINQ 검증: ToArray+Any+Distinct/HashSet) | 51.2% | **49.4%** | substrate |
| 2 | `DigivolutionStackReader.Read` (스택 재구축; ①③④ 포함 계열이 전 샘플의 **~86%** 경로상) | 68.3% | ~14% | substrate |
| 3 | `CardSource.PermanentOfThisCard()` (보드 스캔×호출빈도) | 59.7% | 17.4% | 미러 |
| 4 | `ReadSourceIds` LINQ Where/Select/ToArray (LargeArrayBuilder) | — | 14.5% | substrate |
| 5 | `CEntity_EffectController.GetCardEffects` (효과-스캔 진입점) | 79.1% | ~5% | 미러 |
| 6 | `Permanent.get_DP` (연속효과 폴드) | 42.6% | — | 미러 |
| 7 | `CardEffectCommons.IsExistInSecurity` (GetCards 복사+Contains) | 4.8% | 4.8% | 미러(비용은 substrate GetCards) |
| 8 | `InMemoryZoneMover.GetCards` 호출당 ToArray 복사 | — | (④·⑦에 분산) | substrate |

루트-우선 분해: 펌프 세그먼트(TaskRunner/TurnFlowPump) 43.9% · GetActionMask 재열거 25.1% · RunToStable 24.9% · apply-합법성 5.0% · 관측 스냅샷+인코딩 **0.14%**. **가설 평결**: "관측 3,088피처 재계산" **기각**(0.1%), "GetActionMask 재열거"·"RunToStable 반복" = 루트로는 실재하나 그 아래 실비용의 ~85%가 **DigivolutionStack 읽기-모델 재구축**(매 효과-스캔이 카드별 스택을 풀 재구축 — 진짜 병목은 존 스캔 자체가 아니라 스캔당 스택 재구축·재파스·재검증), "CWT/dict 재생성" = 방향 반대(CWT **추가**가 해법이었음).

### §7.3 (a) substrate-전용 개선 적용분 + after 수치
적용 3건(전부 `Headless/` substrate, 미러 무접촉):
1. `Headless/State/DigivolutionStack.cs` — ctor 검증 무할당 재작성(동일 검사·순서·예외) + `UnderCards` 지연 캐시(불변 레코드).
2. `Headless/State/DigivolutionStackReader.cs` — **순수-함수 메모이제이션**: CWT<top `CardInstanceRecord`, entry>, 히트 시 under-카드 레코드 **전수 ref-검증**(Upsert=새 레코드 불변식 → 값-동일성 증명 가능) + `ReadSourceIds` string[] 수동 파스 + 중간 컬렉션 제거.
3. `Headless/Services/InMemoryZoneMover.cs` — `GetCards` (player,zone) 스냅샷 캐시. 무효화 초크 = `GetZone`(가변 리스트의 유일 반출구, 뮤테이션 전 무효화) + `RemoveFromAllZones`(유일 우회 경로) + `ResetMatchState`. 반환 배열은 불변-스냅샷 의미 유지.

| 지표 (RLB1-02, seed 1000~1009) | before | after | 배수 |
|---|---|---|---|
| steps/sec (in-process 단일) | 5.6~5.7 | **13.5** | **2.4×** |
| 판당 ms (med/min/max) | 11,909 / 2,861 / 25,582 | **4,998 / 1,453 / 9,233** | 2.4× |

의미론-불변 실증: RLB2-01 전-궤적 다이제스트(스텝별 양측 합법표 전체+관측 3,088벡터 전체+factored 마스크+턴/메모리+종결/존카운트, SHA256) **10/10 bit-identical**(개선 3건 각각의 단계에서도 동일) + RLB1-01 3/3(병렬 결정론) + R4S3c shadow **2/2 IDENTICAL + secwin IDENTICAL** + R4S3a 7/7 · R4S3b 13/13 · M2-001 11/11 · M4-001 9/9 · R4RL-01 6/6 · G13-003 green.

after 잔여 핫스팟(=(b) 원장, RD-RLENV-06): LINQ ToArray 계열 24.0%(`Player.SecurityCards` 8.3 · `GetZonePermanents` 6.6 · `GameContext.OrderedFrom` 4.8 등) · List AddRange/Resize 13.0%(`CanNotBeAffected` 4.3 포함) · 문자열 해시(레포 dict) 5.6% · `Permanent.HasDP` 4.6% · `PermanentOfThisCard` 본문 3.2% — 전부 미러층 `EffectList` 재-스캔 아키텍처 비용. `IsExistInSecurity`는 존-캐시 후 <2%로 소멸.

### §7.4 벡터화 러너 실측 (신설 `tools/RlVectorHost`)
단일 드라이버·2모드(마스크-랜덤 셀프플레이, seat 프로토콜 v1, 부모가 seed 공유큐 분배 + 결과 JSONL 수집): `procs` = RlBridgeHost 자식 N프로세스(stdio, 크래시 격리 — D5 1차안 그대로), `tasks` = in-process SeatMatchHost N워커(RLB1-01이 안전성 witness). 동일 48게임(seed 1000~1047), 6C/12T(Ryzen 5600GT):

| 모드 | workers | steps/sec | 동일-작업 1w 대비 |
|---|---|---|---|
| procs | 1 | 11.5~11.6 | 1× |
| procs | 8 | **50.1~51.7** | **4.3~4.5×** |
| procs | 12 | 50.2 | 4.3× |
| tasks (기본 workstation GC) | 8 | 31.0~31.7 | 2.7× |
| tasks (**DOTNET_gcServer=1**) | 1 / 8 | 11.4 / **51.9** | **4.55×** |

- **게이트(≥5× 단일) 판정: 이 개발기에서는 4.3~4.55×로 미달** — 두 전송·GC 구성 모두 ~51 steps/sec에서 동일 플래토(8w=12w), 즉 러너가 아니라 **하드웨어 천장**(물리 6코어+SMT·올코어 클럭). 물리 ≥8코어 장비에서 5× 기대는 유지(러너측 병목 증거 없음). 자식 프로세스 server GC는 **역효과**(28.2), tasks 모드는 server GC **필수**(31.7→51.9).
- 종합: B2-before 단일 5.7 steps/sec 대비 **aggregate 51.9 = 9.1×** (엔진 (a) 2.4× × 벡터화 4.5×). 30k-스텝 스모크: 87분 → **~10분**.

---

# 2부. 설계 초안 (결정 지점 D1~D7)

## D1. 행동 공간 표현 — **권고: 현행 고정 factored 599 유지 + 다중-선택은 "순차 부분-선택 액션화"**
- 고정 이산(MaskablePPO/AlphaZero류) vs 후보-리스트 포인터: 이 게임은 존 캡이 작고(손/필드 ≤16캡, 관측 슬롯↔액션 레인 정렬 불변식이 M2에서 이미 계약화) 조합 폭발이 Digivolve(256)·Attack(272) 수준에서 멈춤 — **고정 공간이 성립하고, L0/L1에서 학습까지 실증됨**. 포인터류로의 전환은 관측·프로토콜·트레이너 전면 재작업 대비 이득 불명 → 기각(대형 풀에서 후보-포인터 재평가 여지만 기록).
- 스키마 v1 동결: 사문 레인 2개(AdvancePhase/EndTurn)는 **제거하지 않고 유지**(오프셋 안정성 — 스냅샷/리그 자산과의 호환; 항상 마스크 0이라 무해).
- **다중-선택 해소 — 3안 대조**: ① 조합 열거 = C(16,8)=12,870 레인 폭발 + 스키마 비안정 → 기각. ② **순차 부분-선택 액션화(권고)** = 기존 ResolveChoice 후보 레인(17슬롯)을 "토글/선택" 의미로 재사용 + Confirm 액션 1레인 — AS-IS의 incremental(byPreSelectedList) 싱글-픽 루프와 **동형**(미러-정합), 스키마 증분 +1~2 슬롯, 정책은 부분-선택 누적을 관측(`choice.selectedIds.count` 기존재, `ObservationEncoder.cs:146`)으로 추적. ③ 후보별 토글+확정 = ②의 변형(토글 해제 허용) — ②에 흡수.
- **엔진측 소요(수정 금지 — 등재)**: RD-RLENV-01 — 디스패처가 `MinCount>1` 또는 조인트-검증기 choice에서 부분-선택 세션을 열거(선택된 부분집합을 choice 상태에 누적, Confirm 시 `SelectionValidator(set)` 최종 심판 = 기존 `ChoiceResult.Validate` 재사용 `ChoiceResult.cs:86-91`), A1 경계가 세션 액션을 표-멤버십으로 수용. 검증기-전멸(모든 확장이 불가한) 세션의 탈출 = AS-IS 재시도 한계(ScriptedChoiceProvider 200-cap과 동일 사상)로 Skip-승격 또는 명시 실패.
- **시급도**: 현 209장 풀 비발화(§4) — 부트스트랩 비차단. **풀 확대 게이트에 결박**(배치 5).

## D2. 관측 인코딩 — **권고: infoset-v1(3088) 유지, 재편은 학습 병목 실증 후**
- 현행 = 이름-값 flat 벡터: 카드 슬롯당 스탯 10 + `cardId`(vocab 인덱스, 트레이너측 임베딩 — describe로 채널 식별) + 진화스택 6×3, 존 커버 = 본인 손 16·양측 필드 16·브리딩 2·트래시 16, 히든존 count-only(+인코딩 시도 즉시 실패 가드), choice 후보 16·attack 슬롯 매칭(`ObservationEncoder.cs:224-338`, `InformationSet` 프리셋 :493-522). perspective 필터는 A4/A4a로 계약화 완료.
- **부족분(우선순위순)**: ① M3 지식추적 미배선 — reveal/덱-top 지식이 관측 밖(reveal류 카드 포팅과 연동, 기존 로드맵 유지) ② 트래시 16캡 overflow(장기전에서 정체 손실 — `identityOverflow` 카운트만) ③ 상대 손패 "알려진 장수" 이상의 추적 없음(정보집합상 정당한 통계 — 후속) ④ flat 벡터라 카드-슬롯 구조를 트레이너가 재구성(현 extractor가 처리 — 유지 비용으로 수용).
- 카드풀-확장 호환은 이미 해결(§6): vocab=전체 DB, obs 스키마는 존-캡 함수(풀 무관). **스키마 해시가 계약**(welcome `obsSchemaHash`) — 변경 시 버전 증가 규약 준수만 강제.
- 텐서 shape 후보(장기): `[slot, feature]` 구조화 + set-transformer류는 **학습이 병목을 실증한 뒤** — 지금 바꾸면 L0/L1 자산·스냅샷 리그 전량 무효화.

## D3. 브릿지 — **권고: (a) SeatMatchHost stdio+JSON 유지, 벡터화는 다중 프로세스로**
- 금일 실측이 결정적: 스텝당 ~175ms(엔진) vs JSON 직렬화(~30KB/스텝, ms-급) — **전송 승급(gRPC/바이너리)은 현 병목에 무의미**. (c) TorchSharp 네이티브는 학습 생태계(sb3-contrib/MaskablePPO 기실증) 포기 비용이 압도적 → 기각.
- 기존 자산 재사용: 프로토콜 v1 계약 테스트 9종·Python `BridgeClient`/`DcgoSeatEnv`/extractor/리그(L1) 전부 무변경 호환(금일 M4-001 green + 프로브가 실증).
- 벡터화 경로 = 프로세스당 host 1 + `SubprocVecEnv` N(§D5). 재평가 트리거 명시: 엔진 최적화로 스텝 <10ms 도달 시 stdio 오버헤드 재측정 → 그때 (b) 소켓 벡터 서버(1프로세스 N매치) 검토(M5 TCP 설계 기존재).

## D4. 보상/종결 — **권고: 현행 유지(순수 승패 ±1, 셰이핑 없음)**
- 이미 구현·계약화: 승 +1/패 −1/무·step_cap·중단 0(프로토콜 §4 + `TerminalRlRewardCalculator`, M4-001 witness). Python측 step_cap=truncated(bootstrap) 처리 기존재(`envs.py:94-95`).
- 셰이핑 반대 근거: 시큐리티 차/보드 우위 등 중간 신호는 이 게임의 역전 메커니즘(시큐리티 효과·카운터)과 상충하는 편향 위험 + L0에서 희박 보상만으로 vs-랜덤 97.5% 기실증. 유일 허용 예외 = `FlowExceededIterationCap` 페널티(진단용 옵션 기존재, `RlStepResult.cs:17-19` — 기본 off 유지).
- 종결 분류는 D6 퍼징 계측과 공유: 자연 종결(reason=lose 사유) vs 인프라 종결(`step_cap/stalled/no_mappable_action/aborted`) — 후자는 보상 0 + 수확 대상.

## D5. 벡터화 — **권고: 1차 = 다중 프로세스(SubprocVecEnv×N host); in-process 병렬은 witness 승격 후 2차**
- 구조 감사 결과 in-process N매치는 **설계상 안전**: `AmbientMatchContext`=AsyncLocal(`AmbientMatchContext.cs:16-18`, nest-safe Enter/Scope) + RD-R4P4-01로 5개 읽기 표면 균일 self-scope(`DcgoMatch.cs:235,347,409-438`); 미러층 전역은 전부 `ConditionalWeakTable<EngineContext,…>`(Player.cs:167 외) 또는 불변 Lazy(CardEffectDispatch.cs:25). 유일 가변 static = Tfx 픽스처(`TfxCannotAddSecurity.cs:14` 등) — 프로덕션 풀 미사용, 병렬 테스트에서만 배제 규약.
- 그러나 **실증이 사문**: `HeadlessBatchParallelDeterminismVerifier`(직렬 vs 병렬 지문 diff, `HeadlessEpisodeBatchRunner`=Task.WhenAll+Semaphore)가 호출부 0. → 배치 1에서 실행 테스트로 승격 후에만 in-process 병렬을 신뢰.
- 1차 권고가 다중 프로세스인 실리: Python GIL 회피·크래시 격리(퍼징 겸용 D6와 시너지 — 한 매치의 엔진 사고가 배치 전체를 죽이지 않음)·기존 BridgeClient 무변경. 코어 수 N에서 기대 처리량 ≈ 5.7×N steps/sec(선형 가정, 배치 2에서 실측).

## D6. 퍼징 겸용 — **권고: 브릿지단 "수확 계층" — 엔진 무수정으로 4종 신호 포집**
롤아웃 = 퍼징 검증층(확정 전략). 잡을 것과 잡는 곳:
| 신호 | 현재 표면 | 보강(RL층) |
|---|---|---|
| 엔진 throw | `SeatMatchHost.HandleLineAsync` catch → `internal` error+aborted result(:88-98) — **예외 타입/메시지만, 스택·매치 컨텍스트 유실** | result JSONL에 `exception{type,message,stack}`+seed+스텝 수+마지막 액션 첨부(호스트 필드 추가 = 도구층, 엔진 아님) |
| STOP/포팅 갭 | `StrictUnbound` 프로파일 = unbound 효과 하드 실패+포렌식 마커(A4 witness); Sonnet-포팅 STOP=runtime throw 관례 | 퍼징 런은 **strict+validated 프로파일 고정**, STOP throw를 위 예외 채널로 수확·카드 id별 집계 |
| 교착 | `stalled`/`no_mappable_action`/`step_cap` reason + `FlowExceededIterationCap` | reason별 카운터 + 발생 seed 재현 커맨드 자동 기록(seed+덱+액션열 리플레이 — M4 결정론 계약이 재현성 보증) |
| 결정론 드리프트 | M4-001 지문 테스트(스팟) | 캠페인 중 k판마다 seed-replay 이중 실행 지문 대조(기존 verifier 승격분 재사용) |
- 수확물 처리 규약: 크래시/STOP/교착 = **학습 파이프라인에선 보상 0 종결(기존재)로 계속**, 수확 JSONL은 주기적으로 원장(RD 항목) 회부 — "학습-크래시가 아니라 수확물" 요건 충족.

## D7. 단계 분할 — witness-게이트 구현 배치 5개

| # | 배치 | 산출물 | witness 게이트 |
|---|---|---|---|
| B1 | **계측 정합 회복** (RL층+테스트만) | G13-003 펌프 재조준(legacy ctor→CreatePumpDriven, stale `EffectResolved` 속성→행동 지표[플레이/공격/시큐리티 소진] 또는 창-경로 계측으로 교체 — 엔진 이벤트 재배선은 RD-RLENV-02로 등재만); A1/A2 LEGACY SCAFFOLD의 펌프판 witness 추가; determinism/병렬 verifier 실행 테스트 승격; 처리량 베이스라인 하네스(steps/sec·ms/판 수치 고정) | G13-003 10/10 green(펌프) + 병렬 결정론 witness green + 베이스라인 수치 기록 |
| B2 | **처리량** | 엔진 프로파일(판당 11s 소재 확정 — RD-RLENV-03; 엔진 수정은 프로파일 결과로 항목화해 사용자 확인 후 별도 배치); RL측 다중 프로세스 벡터화(`SubprocVecEnv`×N + host 풀) | N-프로세스 aggregate steps/sec 실측 ≥ 5×단일(선형성) + 프로파일 보고서(핫스팟 상위 5) |
| B3 | **학습 루프 재검증(pump-era L0/L1)** | 209장 풀 레시피 2~4종 확정(witness 카드 포함, ST 스타터+BT1/BT2); MaskablePPO 스모크 재실행; L1 리그 재가동(스냅샷 신규 — 구 스냅샷은 OLD-cadence 궤적이라 폐기) | 스모크 무크래시 + eval vs 랜덤 유의미 우위 + 관측/마스크 차원 계약 무변(해시 일치) **[B3 상환(2026-07-17) — 3부 §B3.1~.5: eval 99.2% CI95=[95.4%,99.9%], 게이트 전부 PASS]** |
| B4 | **퍼징 수확 계층** | D6 표의 보강 4종(호스트 result 확장·strict 프로파일 캠페인 러너·reason 집계·seed-replay 게이트); 대량 랜덤/약정책 롤아웃 캠페인(209장 풀 전 카드 노출 커버리지 리포트) | 캠페인 ≥10⁴판 완주(크래시=수확·파이프라인 무중단) + 카드 노출 커버리지 리포트 + 수확 원장 회부 ≥1회전 |
| B5 | **다중-선택 표면 + 풀 확대 게이트** (엔진 공사 포함 — 사용자 승인 선행) | RD-RLENV-01(순차 부분-선택: 디스패처 세션 열거+Confirm 레인+A1 경계 수용) + factored 스키마 v2(+1~2슬롯, 버전 증가) + A3 잔여 witness(조인트-검증기 카드 = BT20_098 포팅과 동승) | 조인트-검증기 witness green(교착 0) + 기존 스위트 무회귀 + 스키마 버전 핸드셰이크 검증 |

순서 근거: B1은 신뢰 기반(red/사문 정리) 없이는 이후 게이트 판정 불가. B2가 B3 앞 — 현 5.7 steps/sec로는 L0 재검증이 87분/30k라 가능은 하나, 리그(B3)와 캠페인(B4)은 처리량 없이 무의미. B5는 풀 확대 전 필수이나 현 풀 비발화라 최후순.

## 설계 항목 원장 (엔진측 필요 변경 — 이번 트랙에서 수정 금지, 등재만)
- **RD-RLENV-01**: 다중-선택 choice의 디스패처 표면(순차 부분-선택 세션 + Confirm; `SelectionValidator(set)` 최종 심판 재사용) — D1/B5. 근거: `HeadlessLegalActionDispatcher.cs:214-220`.
- **RD-RLENV-02**: `EffectResolved` 이벤트 발행이 OLD `GameFlowProcessor` 드레인 카운터에 결박(`DcgoMatch.cs:245-252`) — 창-경로 해소 계수로 재배선 또는 이벤트 은퇴. 부수: `SecurityCheck` 이벤트 발행처 0.
- **RD-RLENV-03**: 펌프 결정점당 ~175ms — `DrivePumpToDecision` 루프의 반복 `GetActionMask()` 전열거(연속효과 라이브 스캔 포함) 재계산 의심. 프로파일 후 항목 분해(B2). **[B2 상환(2026-07-17)]** 실측으로 심증 교정: 실비용의 ~86%는 마스크/관측이 아니라 **효과-스캔당 DigivolutionStack 읽기-모델 풀 재구축**(§7.2). substrate-전용 (a) 3건 적용(§7.3, 다이제스트 10/10 동일 실증) → 2.4×. 잔여는 RD-RLENV-06(미러층, STOP)·RD-RLENV-07(구조)로 분해.
- **RD-RLENV-06** (b — 미러 수정 필요, **STOP·구현 금지**): (a) 상환 후 잔여 핫스팟이 전부 미러층 `EffectList` 재-스캔 아키텍처에 귀속(§7.3 after 잔여표): ① `Player.get_SecurityCards`/`GetZonePermanents`/`GameContext.OrderedFrom`의 접근당 LINQ Select/ToArray(합산 ~24%) ② `CardSource.PermanentOfThisCard()` 본문의 호출당 보드 스캔+`UnderCards.Any` 클로저(11% incl) ③ `CEntity_EffectController.GetCardEffects`의 호출당 리스트 재구축+필드/시큐리티/플레이어 3중 AddSkill 스캔(38% incl) ④ `CardSource.CanNotBeAffected` AddRange churn ⑤ `Permanent.DP/HasDP` 연속효과 폴드 재계산(39%/11% incl). 전부 AS-IS 1:1 미러 소스 — 개선하려면 AS-IS-동형 캐시/인덱스 구조 설계가 선행돼야 하며 이 트랙에서는 등재만.
- **RD-RLENV-07** (c — 구조 변경 설계 항목): 결정점당 미러 효과-스캔의 **무효화-epoch 기반 상위 캐싱**(효과-리스트/연속효과 폴드 결과를 상태-뮤테이션 epoch에 결박, 미러 코드 무접촉으로 substrate 경계에서 재사용) — RD-RLENV-06을 미러 수정 없이 우회할 수 있는 유일 후보이나, 뮤테이션 초크포인트 전수 식별(효과 부여/만료·bookkeeping 리셋 포함)이 선행 조건. 관측 증분화는 **불요 판정**(관측+인코딩 실측 0.14%, §7.2 가설 기각).
- **운영 노트(B2)**: in-process 병렬(tasks 모드·학습 벡터화)은 `DOTNET_gcServer=1` 필수(§7.4: 31.7→51.9 steps/sec); 다중 프로세스 자식에는 server GC 금지(역효과 28.2).
- **RD-RLENV-04**: 토큰/부여효과 id의 `Guid.NewGuid()`(`CardEffectCommons.cs:2493` 외 4곳) — seed-무관 id 발산. 상태-digest 기반 검증 도입 시 결정론 id 채번으로 교체 필요.
- **RD-RLENV-05**: `SpecialPlay` 펌프 분기 제외(`HeadlessLegalActionDispatcher.cs:47-52`) — DigiXros/Assembly STOP 클러스터 상환 시 펌프 표에 복귀(RD-P6C1-5/RD-R5-04 후속). 그때까지 해당 카드는 RL 관점에서 플레이 불가 레인.
- **확인 항목**: BT3 포팅분 부재(§6) — 메모리 기록("BT2/BT3 완료")과 트리 불일치. 브랜치 유실인지 기록 오류인지 착수 전 판정.
  - **사용자 확정(2026-07-17): 실측 209장이 정본, 메모리 기록(BT3 포팅)이 stale** — 재작업으로 소실된 것이 아니라 기록이 구식. 조사 불요, **종결**.

---

# 3부. B3 실행 기록 (2026-07-17) — 학습 루프 재검증(pump-era L0/L1)

## §B3.1 L0/L1 재조준 (변경 목록 — 전부 RL층/학습 스크립트, 엔진 무접촉)
기존 Python 학습 슬라이스 위치: `rl/train.py`(L0 MaskablePPO+카드임베딩), `rl/train_league.py`(L1 리그/Elo/스냅샷), `rl/dcgo_rl/`(bridge/envs/cards/decks/league/policy), venv=`rl/.venv`(torch 2.12.1, sb3-contrib 2.9.0, gymnasium 1.3.0 — 신규 의존성 없음).

| 파일 | 재조준 내용 |
|---|---|
| `rl/dcgo_rl/bridge.py` | 호스트 DLL Debug 하드코딩 → **Release 우선, Debug 폴백**(§7 실측: 구성 간 스텝열 동일·속도 2배) |
| `rl/dcgo_rl/envs.py` | `DcgoSeatEnv`에 `deck_provider` 추가 — 매 reset마다 `FixedPoolProvider`가 match_seed-파생 rng로 매치업 샘플(NFR-3 유지). 기존 고정-덱 경로 무변 |
| `rl/train.py` | `--recipes`(레시피 풀 학습), 기본 `--n-envs 8`, eval 기본 120판+Wilson CI, meta에 obs/action 크기·eval 전적 기록, 기본 out=`../runs/l0-pump` |
| `rl/evaluate.py` | 평가 리포트 확장: 승/패/절단 수 + **Wilson 95% CI**; `--recipes`(학습과 동일 풀 평가) |
| `rl/train_league.py` | `--init` 기본 → `../runs/l0-pump/policy.zip`(pump-era 재생성 경로), `--recipes` + 스냅샷 meta의 deck_context/card_pool 갱신 |
| `rl/configs/l0_fixed_pair.yaml` | 실존하지 않던 레시피 참조 → `build_recipes.py` 산출 4종으로 교체 |
| `rl/build_recipes.py` | **신설**: 클린 풀 스캔(엔진 dispatch 규약 미러) + 덱 레시피 생성기(재생성 경로) |
| `runs/l0-smoke`·`runs/l0-300k`·`runs/league-l1` | **폐기(삭제)** — OLD-cadence 궤적 체크포인트/리그 스냅샷(설계 결정). 재생성 경로 = §B3.4 |

주: RlVectorHost는 마스크-랜덤 롤아웃/퍼징 러너(정책 인터페이스 없음). PPO는 on-policy라 오프라인 랜덤 데이터로 학습 불가 — 학습용 벡터화 데이터 생성은 **같은 토폴로지**(RlBridgeHost 자식 N프로세스 = D5 1차안, §7.4 procs 모드가 실증)를 `SubprocVecEnv×N`으로 구동한다.

## §B3.2 덱 레시피 (클린 179장 풀, `rl/decks/*.json`)
- 풀 산출: 포팅 209(카드번호=클래스명 dispatch 미러) − 파일 STOP 마커 29 − **잠복-STOP 1**(`AD1_025`: 파일에 STOP 마커 없으나 `AddAssemblyConditionClass` 부여로 CardController Assembly play arm의 RD-P6C1-5 throw 격발 — B3 검증 롤아웃에서 실측 수확) = **클린 179**.
- AS-IS 덱 규칙 확인(`DCGO/Assets/Scripts/Script/DeckBuildingRule.cs`·`EditDeck.cs` "50+5"): **메인 정확히 50장, 카드당 최대 4장, 디지타마 ≤5장** — 전 레시피 준수.
- 구성(모노컬러 4종 = 학습 풀, ST 스타터 코어 + 동색 BT1/BT2 필; witness 카드 ST1_15 포함):

| 레시피 | 색/전략 | 구성 |
|---|---|---|
| `red_st1_bt` | 적 — ST1 어그로 코어 + BT1/BT2 적 | 50+5 (15 distinct) |
| `blue_st2_bt` | 청 — ST2 컨트롤 코어 + BT 청 | 50+5 (15 distinct) |
| `yellow_st3_bt` | 황 — ST3 시큐리티 코어 + BT 황 | 50+5 (15 distinct) |
| `green_st4_bt` | 녹 — ST4 코어(엔진 StarterDecks 미수록, 포팅분 4장씩) + BT 녹 | 50+5 (13 distinct) |
| `coverage_rest_1..5` | 잔여 커버리지(자/백/흑/혼색 포함, 2장씩) — B4 퍼징 캠페인용 | 각 50+4 |

- 커버리지: **179/179 클린 카드가 최소 1덱에 등장**(생성기 출력이 검증). 결정적 생성 — `cd rl && python build_recipes.py`.
- 엔진 수용 검증: 9덱 × (미러+체인) 18쌍 전판 랜덤 롤아웃 — **18/18 자연 종결, 셋업 거부 0, 크래시 0, step_cap 0**(maxSteps 500).

## §B3.3 L0 스모크 + 평가 (게이트 실측)
조건: 30,000 agent-steps, `SubprocVecEnv×8`(RlBridgeHost Release 자식 8프로세스), seed 42, 모노 4레시피 풀, MaskablePPO(MlpPolicy+CardEmbeddingExtractor — L0 알고리즘 그대로, 신규 발명 0).

| 게이트 | 판정 | 실측 |
|---|---|---|
| ① 스모크 전 구간 무크래시 | **PASS** | 학습 1,441판 + 평가 120판 전판 자연 종결(step_cap/stalled/aborted/예외 0) |
| ② 학습 정책 vs 랜덤 통계적 우위 | **PASS** | **99.2% (119W/1L/120판, 절단 0), Wilson CI95=[95.4%, 99.9%]** — 50% 대비 압도적 |
| ③ 관측/마스크 차원 계약 무변 | **PASS** | obs 3088·action 599·vocab 4206, obsSchemaHash `b09a5218…` 학습 전/후·검증 프로브 동일 |

- 학습 곡선(학습-중 탐색 포함 winrate, 에피소드 4분위): **Q1 0.69 → Q2 0.84 → Q3 0.93 → Q4 0.94** — 단조 상승.
- 처리량: **21.3 agent-steps/sec**(학습기 gradient 연산과 8워커가 6C/12T를 공유 — §7.4 랜덤-롤아웃 51 steps/sec 대비 학습 포함 실효치). 30k 스모크 벽시계 = 23.5분 + 평가 ~9분.
- L1 리그 재가동(8,000스텝, freeze 2,000, DummyVecEnv×4, 동일 레시피 풀, init=l0-pump): 스냅샷 4개 신규 편입(`st-league-s001..s004`), **게이트 3/3 — 레이팅 상승(learner 1315), weakness 샘플링 발동(64회), 시드 재현 대전 일치**. 12.0 steps/sec.
- 잠복-STOP 수확 1건: `AD1_025`(§B3.2) — B4 수확 계층의 선행 실증(레시피 검증 롤아웃이 잡음).

## §B3.4 재현 절차 (명령 시퀀스)
```bash
# 0) 호스트 빌드 (1회)
dotnet build tools/RlBridgeHost/RlBridgeHost.csproj -c Release

# 1) 덱 레시피 생성 (결정적 — 산출물 rl/decks/*.json)
cd rl && .venv/bin/python build_recipes.py

# 2) 셀프플레이 데이터 생성 + L0 학습 + 평가 + 체크포인트 (일체형)
.venv/bin/python train.py --steps 30000 --n-envs 8 --vec subproc --seed 42 \
  --eval-matches 120 \
  --recipes decks/red_st1_bt.json decks/blue_st2_bt.json \
            decks/yellow_st3_bt.json decks/green_st4_bt.json \
  --out ../runs/l0-pump
# 산출: ../runs/l0-pump/policy.zip + meta.json(계약 해시·eval 전적·CI) + results-env*.jsonl

# 3) 평가만 재실행(체크포인트 관리 — 임의 시점 재평가)
.venv/bin/python evaluate.py ../runs/l0-pump/policy.zip --matches 120 --seed 819 \
  --recipes decks/red_st1_bt.json decks/blue_st2_bt.json \
            decks/yellow_st3_bt.json decks/green_st4_bt.json

# 4) L1 리그 재가동 (스냅샷 신규 — freeze 주기마다 runs/league-l1-pump/snapshots/에 편입)
.venv/bin/python train_league.py --steps 8000 --freeze-every 2000 --n-envs 4 \
  --recipes decks/red_st1_bt.json decks/blue_st2_bt.json \
            decks/yellow_st3_bt.json decks/green_st4_bt.json \
  --out ../runs/league-l1-pump
```
체크포인트 관리 규약: `runs/<실험>/policy.zip`+`meta.json`(obs_schema_hash·vocab_version 동봉 — 로드 시 대조로 OLD-cadence/스키마 드리프트 차단), 리그 스냅샷=`runs/<실험>/snapshots/` + `ratings.json`·`matchup.sqlite`. OLD-cadence 산출물은 2026-07-17 전량 삭제됨 — 위 시퀀스가 유일 재생성 경로.

## §B3.5 실행 로그 (게이트 판정·회귀)
- RL 스위트(학습과 병행 실행, Release): **M2-001 11/11 · M4-001 9/9 · G13-003 PASS · R4RL-01 6/6 · RLB1-01 3/3** — 전부 green, 무회귀. rl/ Python 유닛 **41/41**.
- B3 게이트 종합 판정: **GO** (§B3.3 표 3항목 전부 PASS + L1 리그 게이트 3/3).
- 남는 리스크(원장):
  1. 잠복-STOP은 정적 마커 스캔으로 전수 식별 불가 — `AD1_025`형(인프라 throw 격발)이 더 있을 수 있음. 완화: 레시피 검증 롤아웃(§B3.2) + B4 캠페인이 전수 노출. 학습 파이프라인은 예외=보상 0 종결이라 크래시가 아니라 수확(D6).
  2. 학습 실효 처리량 21.3 steps/sec — 학습기(torch)와 env 워커의 코어 경합. 대규모(≥10⁶ 스텝) 캠페인은 물리 ≥8코어 장비 또는 학습기/롤아웃 분리 필요(성능 작업은 사용자 동결 지시로 미착수).
  3. 평가·리그 게이트는 vs-랜덤/자기-스냅샷 기준 — 절대 실력 척도 아님(L2+ 몫). 스모크 게이트로는 충분.
  4. coverage_rest 덱은 전략적 정합성이 낮은 커버리지 파일 — 학습 풀엔 모노 4종만, coverage는 B4 퍼징 캠페인 전용.
