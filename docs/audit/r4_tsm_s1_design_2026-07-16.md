# R4 S1 설계서 — 턴흐름 TSM 재하우징 (2026-07-16, 사용자 확인 대기)

상세=설계 에이전트 보고 원문(세션 기록). 본 문서=결정·계획 요지. 선행=r4_tsm_investigation_2026-07-16.md.

## 선(先)발견: 제어-역전은 드라이버층에서 이미 화해돼 있음
AS-IS 인터랙티브 정지 3곳이 미러에서 전부 외부화 기완료 — 멀리건=MulliganCoordinator+ChoiceType.Mulligan choice-pause·브리딩=디스패치 액션·메인 대기=액션큐(DequeueMainPhaseAction≈ProcessAsync). **R4 실작업=제어모델 신설이 아니라 흩어진 phase-BODY 로직의 AS-IS region 1:1 재하우징.** 발산 위험=body 충실도.

## 결정 1 (권고 확정): 후보 (a) — region 1:1 async + 기존 park/resume·액션큐 seam
- (b) 멱등-pass 커서 확대는 기각: AS-IS TSM은 멱등이 아님(TurnCount++:550·버킷리셋:536·언탭:590 등 body-중간 부수효과) — 커서 재진입=guard 대량 삽입=구조 발산.
- phase-body 시그니처: StartGame/Active/Draw/Breeding/Main/EndPhaseAsync ← AS-IS TSM region별(:341-504/:530-648/:652-697/:701-837/:877-1351 펌프만/:3151-3210). SetMainPhase UI(:1354-2872)=non-scope. 재개기제=기존 substrate 소비만(신설 0).
- 잔존 substrate: DcgoMatch 표면·HeadlessGameLoop 펌프·액션큐·TurnController·choice-pause 계열·RunToStable. 은퇴: EarlyPhaseFlow Unsuspend/Draw/Breeding 블록·MainPhaseFlow invented eval·MetadataActionProcessor AdvancePhase/EndTurn body(시그니처 불변)·StepAsync inline 메모리 재확인. re-point: HeadlessEndTurnCleanupFlow→EndPhaseAsync.

## 결정 2 (사용자 판정 필요): 페이즈 모델 9 vs 6
- 미러 HeadlessPhase=9값(발명 Setup/Unsuspend/MemoryPass) vs AS-IS 6값. **카드-가시 표면(GameContext.TurnPhase)은 이미 6으로 collapse**(13 콜사이트 전부 6만 봄) — 9값 소비자는 substrate(디스패처·매핑·RL 관측 one-hot 9)와 스위트뿐.
- 옵션 A(6 fold): enum 충실 최고. 비용=디스패처 재작성·**RL 관측 cardinality 9→6**·23스위트 재작성·다중-phase atomic Step.
- 옵션 B(9 substrate 유지+collapse 경화): 카드-가시 발산 0·RL 관측 안정·스위트 무변. 경화=HeadlessPhase 직접 read 카드 0 감사+ADAPTATION 문서화.
- **권고=B**. 단 "enum 값 자체를 게임로직으로 볼 것인가"는 fidelity 바 판정=사용자 몫.

## 배치 계획 (신중 모드: 소단위·배치별 전체 스위트·리뷰 3지점)
P1 GameContext 갭필(~60줄) → P2a phase-body 휴면 조립(~400-600줄, live 호출 0) → P3 EndPhase re-point(~50줄) → P4 shadow-run 하네스(OLD-vs-OLD sanity) → **리뷰1(휴면 body 충실도)** → S2 페이즈-모델 경화(결정 2 확정 후) → **리뷰2** → P2b 턴-말 seam(EndTurnCheck/TurnEndMinMemory/EndTurnProcess — MultipleSkills 불가분, 창 seam 정합 재검증 동반) → S3 드라이버 flip(단일 원자) → **shadow-run OLD-vs-NEW 게이트(무작위 self-play N판 스텝별 HeadlessTurnState 동일성) → 사용자 컷오버 승인 → 리뷰3**.

## shadow-run 설계
동일 MatchConfig(RandomSeed·UseDeterministicChoices)로 DcgoMatch 2인스턴스(ctor가 processor 주입 지원 — 구/신 드라이버), 시드 결정론 self-play N=200-1000판, per-step HeadlessTurnState 값-동등+상태 다이제스트 비교, 최초 발산=실패 리포트. 한계=희소 엣지(EoT 인터랙티브·브리딩-이동·덱아웃) 미커버 가능→witness 보완 필수.

## 리스크 잔여
①결정 2 사용자 판정 ②DoneStartGame 게이트 정확 지점(StartGameAsync :503 대응) ③P2b의 창 seam 정합(현 EndTurnAsync의 drain이 컷오버 후 신 창엔진과 어떤 관계인지 P2b 착수 시 재검증) ④SetMainPhase 게임룰-누수 spot-audit 미완(V-item) ⑤shadow-run 커버리지 공백=witness 보완.

## 결정 2 확정 (2026-07-16, 사용자 판정): 옵션 A — AS-IS 6값 enum + substrate sub-커서
**근거(사용자)**: DCGO2 원본이 계속 업데이트됨 — enum 발산 시 페이즈-관련 업스트림 변경마다 영구 번역세(로컬LLM 불가 판단). 일회성 churn > 영구세. (프로젝트 근본 명제=기계-diff 추적성과 정합.)
**정제 설계**: 페이즈 enum=AS-IS 6값(게임 모델, 업스트림 1:1) + 스텝 위치=`HeadlessTurnState`의 명시적 substrate sub-커서(게임 페이즈 명명 금지 — resume 커서와 동류). 합법 액션 표 키=페이즈+커서. RL 관측=페이즈 one-hot 6+커서 feature(업스트림 페이즈 추가 시 enum·관측이 함께 의미 있게 확장). S2 스코프 갱신=HeadlessPhase 9→6+커서 도입+디스패처 재키잉+스위트 23종 재조준(일회성 비용 수용). P1/P2a는 결정-무관이라 선행 가능.

## 리뷰지점 1 결과 (2026-07-17): GO — P0/P1 0
6 body+EndGame region 대조 전부 확인됨(순서·경계값·EndPhase 리셋 :3170-3208 전량·winner→loser 방향), ADAPTATION 전부 순수 substrate 번역 판정, P1 판정 3종(Memory view 충실·TurnCount wrong-host·IsSecurityLooking=카드-WRITE 플래그라 view 불가) 전부 정당. **P2-1(P2b 필수 하위작업)**: MainPhaseAsync가 AS-IS :880 진입 EndTurnCheck+:882 goto 가드 누락 — P2b에서 OnStartMainPhase 창(:905) 앞에 진입 가드를 구조 삽입해야 함(단순 주석 아님). minor 전방주의: AutoProcessing 미러 시 gameContext.Memory 직접-write(:685/:690)는 MemoryController 경유 필수.

## P3+P4 착지 (2026-07-17)
P3(35dd7d44)=EndPhase→Cleanup re-point(AS-IS :3170-3208 전량 커버 대조, junction 3종 소유 판정)+TurnCount 정위치(P2a dormant 필드의 latent 0-반환 버그 교정→substrate 위임 view). P4(ff9f58f4)=shadow-run 하네스 — OLD-vs-OLD **bit-identical**(자연 종결 223/188스텝), 결정론 갭 0, S3 주입 seam 준비. **하네스 퍼징이 latent 엔진 버그 2건 표면화**: RD-R4P4-01=`DcgoMatch.StepAsync`가 ambient scope 미자체설정→block-with-collision NRE(기존 CEntity:88 계열의 정확한 위치 — 프로덕션 DcgoMatch 소비자 노출=S3 리뷰 대상·RL 환경 트랙 필수 수정)·RD-R4P4-02=ST1_15 자동선택 validator 위반. 둘 다 양측 동일 발생=결정론(발산 아님).

## 결정 3 (S3 착수 전 사용자 판정 필요): 스텝 케이던스 — OLD 유지 vs AS-IS 복원
S3 사전조사(드라이버 층 정독)에서 S1 설계 내부의 **비정합** 발견 — 다음 세 가지가 동시에 성립 불가:
①결정 1(멱등-pass 커서 재진입 기각 = body는 비-재진입 통짜), ②S1 shadow 게이트 스펙("스텝별 HeadlessTurnState 동일성" = OLD 케이던스 보존 전제), ③body의 AS-IS 스팬이 OLD 스텝 경계와 어긋남.
**어긋남의 실체**: OLD 드라이버는 턴을 에이전트 스텝으로 분절 — EndTurn 액션[EoT창→flip→(Active,PhaseStart)+OnStartTurn 드레인] → AdvancePhase[(Active,Unsuspending)=언탭 블록] → AdvancePhase[(Draw)=드로] → AdvancePhase[(Breeding)] → AdvancePhase[(Main)+OnStartMainPhase]. 반면 미러 `ActivePhaseAsync`는 OnStartTurn 창+언탭을 **한 몸**으로 실행(AS-IS :530-648) — OLD 스텝 2개(E 후반+A1)에 걸침. 비-재진입 body를 어느 스텝에 호출해도 스텝별 다이제스트 동일성은 **구조적으로 불가**(창-언탭 타이밍이 한 스텝 안으로 붕괴).
- **옵션 A (OLD 케이던스 유지)**: 드라이버가 커서 세그먼트별로 body를 조각 호출. 필요조건=body 슬라이싱(재진입 지점 파라미터) — **결정 1이 기각한 구조**. 채택 시 body가 AS-IS 통짜 미러가 아니게 되고, 턴-흐름 업스트림 변경마다 세그먼트 경계 재판정 = 결정 2에서 사용자가 기각한 **영구 번역세**의 드라이버판.
- **옵션 B (AS-IS 케이던스 복원, 권고)**: TSM 펌프가 AS-IS처럼 연속 실행(StartGame→{Active→Draw→Breeding→Main→End} 루프), 정지=이미 외부화된 인터랙티브 지점만(멀리건 choice·브리딩 디스패치·메인 액션큐·창 choice-pause — S1 선발견 그대로). 귀결: 조기-페이즈 AdvancePhase 에이전트 액션 은퇴, EndTurn 액션→Pass 라우팅(AS-IS PassTurn :3364)으로 대체, 임계-도달 턴종료는 펌프 내 EndTurnCheck 자동(AS-IS :880 계열), (Main,AwaitingMemoryPassEnd) 대기 스텝 소멸. 비용(일회성): RL 액션표/관측 케이던스 재정의·스텝-키 스위트 재조준·**shadow 게이트 경계 재정의**(스텝별→인터랙티브-정지 경계별 상태 동일성+최종 궤적 동등). 편익: 발명 드라이버 0·업스트림 턴-흐름 변경=body만 diff(프로젝트 근본 명제 정합)·결정 1/2와 일관.
- 참고: S2의 커서-키 액션표("합법 액션 표 키=페이즈+커서")는 옵션 A를 전제한 서술이었음 — B 채택 시 커서는 관측 feature+인터랙티브 정지 식별로 축소(Starting/Unsuspending 은퇴 후보, AwaitingMemoryPassEnd 소멸).
**권고=B**. 근거: 결정 1(비-재진입)과 결정 2(영구 번역세 거부)의 논리적 귀결이며, 인터랙티브 정지 외부화가 기완료라 구현 가능성은 S1 선발견이 이미 보증. 단 RL 인터페이스 굴곡이 결정 2보다 큼(액션 공간 자체가 변함) — rl-env-parallel-track이 R4 flip 후 본격화라 지금이 마지막 저비용 창구.

**결정 3 확정 (2026-07-17, 사용자 판정): 옵션 B — AS-IS 케이던스 복원.** 근거(사용자 확인): B=AS-IS 원본 구조(연속 코루틴 펌프+인터랙티브 지점만 정지), OLD 스텝 분절(AdvancePhase 액션)=헤드리스 초기 발명물 — 원본에 없는 분절을 살리려 원본에 없는 세그먼트 경계를 body에 새기는 A는 프로젝트 원칙 역방향. 에이전트 노출 결정=원본 플레이어 결정과 동일해짐(부화/플레이/패스). shadow 게이트 경계=인터랙티브-정지 경계별 상태 동일성+최종 궤적 동등으로 재정의.

## S3 실행 설계 (2026-07-17, 결정 3=B 후속) — 사용자 확인 대기
**펌프 구조**: `TurnFlowPump`(신규, substrate) — StartGameAsync 후 `{Active→Draw→Breeding→Main→End→flip}` 루프를 미러 body 호출로 연속 실행하는 단일 async 태스크. 정지=**await-게이트**(TCS 인라인-연속, 단일 스레드 결정론): 파크 지점에서 조건 미충족 시 제어가 동기적으로 호스트에 반환, 에이전트 액션이 조건을 채우면 게이트 완성→body가 **제자리 재개**(C# async 연속 = AS-IS 코루틴 프레임 보존의 정확한 등가). 기존 EngineTaskRunner(IEnumerator 전용)는 부적합 판정 — async Task body를 스텝할 수 없음; TCS-게이트가 WaitUntil의 async 번역.
**정지 지점 4종**(전부 기존 외부화의 재소비): ①멀리건=MulliganCoordinator choice ②창 choice=**await-모드 choice 포트**(현행 throw 계약(WindowChoicePendingException+continuation 기록)은 RunToStable 구동용 — 펌프 구동에선 포트가 choice 개설 후 게이트 await, MultipleSkills body 무변경, 해소 시 제자리 재개; continuation 기록·ResumeSuspendedWindowsAsync는 컷오버 시 은퇴 후보) ③브리딩 결정=디스패치 액션 ④메인 선택 대기=AS-IS :971-1253 디스패치 영역 미러(인텐트=LegalAction, 실행=기존 액션 클래스).
**배치 분할(신중 모드 소단위 유지)**: S3a=펌프 기반시설(TurnFlowPump+await-게이트+await-모드 포트)+StartGame/멀리건+조기 페이즈 연속 실행 → S3b=MainPhase 디스패치 영역(:971-1253) 미러+Pass→EndTurnProcess 라우팅 → S3c=shadow OLD-vs-NEW(경계=인터랙티브-정지+최종 궤적)+**사용자 컷오버 승인**+은퇴(EarlyPhaseFlow 블록·MainPhaseFlow invented eval·AdvancePhase/EndTurn body·EndOfTurnDrainedTurn 마커·TurnEndMinMemory flow 사본)+리뷰3. 각 배치=전체 스위트 게이트. NEW 드라이버는 S3c 승인 전까지 주입식(기본값=OLD 무변).
**리스크 ② 착지점**: DoneStartGame=멀리건 choice 해소+시큐리티 배분 완료 후 펌프가 루프 진입하는 지점(AS-IS :503 대응) — S3a에서 확정.

## 리뷰3 결과 (2026-07-17, 독립 적대 리뷰 — R4 전체 아크 fc3cf677..1faa823a): GO-with-P1-상환
**P0 0건.** witness 3스위트 재실행 green(R4S3a 7/7·R4S3b 10/10·R4S3c 셰도 경계-동일). 렌즈 7종 중 5종 확인됨(펌프 동시성·choice 이중-모드·실행자 라인 대조 :1150-1902 전량·턴-말 의미론·컷오버 표면), 2종 반증(→P1). RD-S3C-01은 리뷰3이 재확증(NEW 1회 발화=AS-IS 정본).
**P1-1 (RD-R3-01): 진화-비용 이중석의 "동일 데이터" 전제 붕괴 — 51장 발산.** 미러 EvoCosts는 S3c-c에서 EvolutionCondition 토큰으로 교정됐으나, 합법-액션 표를 만드는 DigivolveAction 좌석은 여전히 top-level EvolutionCost 단일값: 로더는 구조화 조건을 메타키 `evolutionConditions`로 저장(CardBaseEntityLoader.cs:88-91)하는데 DigivolutionCostHelpers.ReadRequirementsFromMetadata(:441)는 {digivolutionCosts,evolutionCosts,evoCosts}만 스캔→Any(EvolutionCost) 폴백. 재현: BT10_026(Blue@4:4, Blue@5:2) — Lv.5 위 진화 시 정본 비용 2 vs 표 4 → 메모리 2~3 보유 시 합법 진화가 액션 표 누락(RL 액션 공간 오류), 4+ 보유 시 선언 4/청구 2 불일치. 동형 51장. **셰도 게이트 구조적 맹점**(OLD/NEW가 같은 좌석 공유). 은퇴 원장 항9의 "동일 데이터" 전제가 거짓 → 항9=정합 결함 상환으로 승격.
**P1-2 (RD-R3-02): PermanentBookkeepingStore 수명 계약 누수 — 메인라인 경로가 CREATE/DIE Reset 좌석 전부 우회.** Reset 좌석 2곳(CardObjectController :134/:181)은 레거시 경로 전용 — 효과-플레이(MatchStateMutationSink.ApplyPlayCard :1873-1923, raw zone move)·삭제 종결(sink :1489-1502, GameFlowProcessor :406-408/:640-642 field→Trash 직행)·바운스/덱복귀(sink :592-663) 전부 미경유 → 재플레이 카드가 전생 북키핑을 읽음(AS-IS는 새 Permanent 객체=기본값). ReKey는 old-top 엔트리 부재 시 침묵 no-op(:90-103) — 엔트리 없는 아래카드 위 진화 시 새 top의 stale 엔트리 잔존. 독자 표면 활성(EX8_074 DigivolvingEffect, CardEffectCommons.cs:2130). 현행 S3b DIE witness는 RemoveField 직호출 픽스처라 green=픽스처 통과이지 계약 통과 아님. 권고: Reset 좌석을 공용 withdraw(RemoveFromAllArea/sink 진입·이탈)로 이전+수명 witness.
**P2 8건**: ①Set(|m|) 재부호 오표기(TurnFlowPump.cs:313-316·HeadlessMainPhaseFlow.cs:205-207 — 진짜 재부호=Set(-m); m≤0에서만 등가, TurnEndMinMemory<0 효과 시 발산; negation 교정 권고) ②WhenDigivolving 브릿지 이중발화 무가드(현 코퍼스 0장=안전; 브릿지 은퇴 전 단언 권고) ③**펌프 매치 미등재 이중-드라이버**: GameFlowProcessor.RunToStableAsync(스텝 3 attack advance+RuleProcess)가 펌프 매치 무게이트 상주 — 공격-내 choice 해소 직후 flow가 잔여 공격 스테이지를 throw-계약으로 완주, 펌프 공격 루프는 idle no-op 기상(크래시 없음·발산 입증 실패이나 블로커 경로 셰도 미커버 → 펌프-블로커 witness 권고) ④TurnFlowDriver DeclareAttack 무효-표적→시큐리티 공격 침묵 폴백(:94-98; Failure 통일 권고) ⑤MainPhaseAction 패킷 턴-경계 잔존(큐 미청소 — AS-IS는 UI가 생산 게이트; EndMainPhase 드레인 권고) ⑥수동 Install+Reset 풋건(호스트 생존+태스크 사멸=교착; CreatePumpDriven은 Reinstall로 안전) ⑦시큐리티-0 승리=MarkLose 경유 한 패스 지연(발산 미발견; 시큐리티-승 종국 다이제스트 witness 권고) ⑧S1 리스크 ④ spot-audit 문서 불일치.
**상환 계획**: P1-1 배치(RD-R3-01: ReadRequirements의 evolutionConditions 소비 갭필 or 항9 좌석 병합+diff witness) → P1-2 배치(RD-R3-02: Reset 좌석 이전+수명 witness) → P2-1 negation+P2-3/4 소배치. 각 배치=전체 스위트.

### 리뷰3 상환 착지 (2026-07-17) — P1 2건+P2 3건 전량 상환, R4 마감
**P1-1(RD-R3-01) 상환**(3db10c4f): 정본 토큰 파서를 DigivolutionCostHelpers.ParseEvolutionCondition 단일 보유(미러 PrintedEvoCosts가 소비 — 이중 파서 소멸), ReadRequirements=토큰 우선→메타키(evolutionConditions 추가)→Any 폴백, DigivolveAction.TryGetDeclaredEvolutionCost로 액션 표·Validate·before-pay 3좌석 단일화. witness R4R3-01: 전-코퍼스 8143장 파싱 대조+42,412건 이중석 행동 대조 불일치 0, BT10_026 행동 단언. Any-폴백 결함 의존 픽스처 2건 교정(G3E-002·G9-071). 게이트 335/107=base 동일. 이월: 효과-구동 좌석(ActivatedEffects:37/146/165 등)의 ignore/added 경로 편입 별도 판단; helper 색 비교 IgnoreCase vs 미러 Ordinal(코퍼스 발산 0 실증).
**P1-2(RD-R3-02) 상환**(a69965d6): CREATE/DIE Reset을 유일 존-변이 지점 InMemoryZoneMover.MoveCard로 중앙화(필드-존 소속 변화 시 Reset — 전 우회 경로 커버·신규 경로 자동 포함), permanent-연속 top-스왑은 ContinuityMove 마커 7좌석 전수(누락=리셋 방향 fail-safe), ReKey no-op 누수 차단. witness R4R3-02 6/6(실 sink 재플레이 기본값·바운스·지연-finalize·ReKey 보존·stale 차단·브리딩 승격). 게이트 336/107=base 동일. 이월: FuseAsync 무-ReKey 선재 갭(관측 무변; design item 후보), 필드-이탈 후 사후-독자 생기면 JustBefore* 스냅샷 계열로 흡수.
**P2-1 상환**: 두 좌석(TurnFlowPump·HeadlessMainPhaseFlow) 무조건 `Set(-m)` 교정+근거 주석; Set=순수 clamp라 m==0 신규 호출 부작용-무. **P2-4 상환**: DeclareAttack 무효-표적→Failure 통일(침묵 시큐리티-공격 변환 제거). **P2-3 상환**: 펌프-블로커 witness(R4S3b 11번째, PumpBlockerBattle) — Blocker choice 방어측 개설·Blocking park·해소 후 suspend/치환/시큐리티-무변/공격자 트래시·펌프 idle 기상 후 재park·턴 flip 라이브니스 전부 단언, **발산 미적발(첫 실행 green — 이중-드라이버 인터리빙=AS-IS 기대 일치, P1 재분류 불요)**. shadow 경계-동일 유지(P2-1 좌석 변경 후).
**잔여 P2(5건, 전부 현행 무해·원장 유지)**: ②브릿지 이중발화 가드(브릿지 은퇴 시 소멸) ⑤패킷 턴-경계 잔존 ⑥수동 Install 풋건 ⑦시큐리티-승 다이제스트 witness ⑧S1 리스크④ 문서 정합. **R4 마감.**

## S3c-d 은퇴 원장 (2026-07-17, 사용자 컷오버 승인 후 — 소비자 전수 감사 완료)
**총판정: 즉시 삭제 0항** — 물리 은퇴는 단계적(B군 registry 물리삭제 게이트 패턴).
- **근본 게이트 G1(항1~5·8 연동)**: HeadlessGameLoop 기본 프로세서(MetadataActionProcessor)를 pump로 플립+디스패처의 AdvancePhase/EndTurn 발행 중단 — 그 전까지 프로덕션 기본 매치가 소비. 차단=OLD 스텝-액션 위 테스트 코퍼스(항3 실질 광범위·항8 throw 계약 15스위트: W1b-Resume·C-Del 계열·C-Atk 계열·C-EoT2·A4·GR-006·G3.5-F68·P1r-Sec·W-EoTFIX).
- **경량 즉시 가능 2건**: 항7 CanEnterFieldByEffect 브리지 사본(내부 2호출부→실물 CanEnterField 재배선; ICanNotPutFieldEffect 생산자 0=동작 no-op) · 항1 부속 ResolveBreedingAsync(호출부 0=死코드).
- **독립 재배선 2건**: 항6 supply OnEnterField/WhenDigivolving 변환(다운스트림 소비 카드 0=DORMANT; 삭제=PlayCardAction/DigivolveAction enriched emit 제거+W2-SkillWindowSupply 재조준) · 항9 진화 legality 이중석(A=DigivolveAction/DigivolutionCostHelpers 소비자 — BT1_078·ActivatedEffects·CardEffectCommons 코스트 게이트 — 를 B=미러 CanEvolve/EvoCosts로 이관 후 병합; B의 ReadRequirements 재사용은 공유 데이터층이라 잔존).
- 항4 EoT 마커=항3 EndTurnAsync 은퇴와 동시 삭제(NEW 대체=per-effect 캡). 항5=항2 은퇴 시 원본(AutoProcessing.TurnEndMinMemory) 정본 승격.
**S3c-d 실행 스코프(이번 배치)**: 펌프 승격(RL/신규 표면)+legacy 강등 문서화+경량 2건, 물리 삭제=후속 골("OLD 스텝-코퍼스 재조준") 등재.

## S3c-c 착지 (2026-07-17) — shadow 확대·발산 판정 (9a6df8e2, 컷오버 승인 대기)
**기본 정책 21게임**(5+16시드, 캡 140): 전부 경계-동일, 16게임 자연 종국(81턴 덱아웃) 도달 — **종국 승자 판정까지 OLD=NEW**.
**확장 정책(진화+옵션)이 결함 3건 적발** — 게이트의 존재 이유 실증:
1. **NEW 버그(수정)**: EvoCosts printed 투영이 요건 리스트만 소비 → 과허용. 진짜 캐리어=`CardRecord.EvolutionCondition` 토큰("Color@Level(:Cost)", OLD MatchesEvolutionCondition과 동일 게이트). 부수: :813 진화 재검증 게이트 STOP 실체화(소유자+CanEvolve 환원).
2. **코퍼스-방언 갭(브릿지)**: 코퍼스가 [When Digivolving]을 전용 키로 리맵 등록(배치-2 관례) → AS-IS-충실 실행자(OnEnterFieldAnyone+게이트)에서 침묵. 실행자에 DISPATCH-REMAP BRIDGE(진화 시 WhenDigivolving 동시 개설). 코퍼스 재키잉 시 브릿지 은퇴.
3. **OLD latent 원장 2건**: **RD-S3C-01**=OLD [When Digivolving] **이중발화**(전용 emit+공급 변환 이중 창 혐의; 산술 증거="2장 트래시" 효과가 OLD에서 3장=2+1회. NEW 1회=AS-IS 정본 — 컷오버로 자연 소멸, 리뷰3 확증 대상) · **RD-S3C-02**=OLD ActivateOption의 ST1_15 조합-검증 throw(은퇴 경로 엣지, 리뷰3 판정).
**게이트 의미론 확정**: 발산=판정 REPORT(원장 대조), [OLD]-측 오류=원장, 스위트 실패=NEW-측/일반 오류만. **커버리지**: 플레이·진화(연쇄 3단)·옵션·공격·시큐리티 배틀·부화·패스·EoT창·자동 턴종료·덱아웃 종국 / 미커버=특수플레이(RD-P6C1-5/RD-R5-04 STOP 등재)·블록(방어측 블로커 없는 정책 — witness별도 커버)·본선 카드풀 밖 효과.
**전체 스위트**: 333/107, fail-set=base 바이트 동일.

## S3c-a 착지 (2026-07-17) — shadow OLD-vs-NEW 하네스 + 5시드 경계-동일
**재정의 프로토콜(결정 3)**: 두 드라이버는 액션 통화가 달라 P4 lockstep이 정의 불가 — ①단일 결정론 정책(멀리건 keep·부화·공격-우선/플레이/패스)이 게임-수준 결정을 내리고 각 측이 자기 통화로 번역(OLD=legal 테이블 선택+AdvancePhase/EndTurn 스텝 구동, NEW=펌프 정지 seam) ②비교 경계=양 모델이 공유하는 인터랙티브-정지 — **턴별 메인-진입**(OLD=(Main,PhaseStart)+무choice, NEW=펌프 메인 park)+종국 ③RNG 패리티 전제(셔플=공용 setup·드로=RNG 미소비) 게임별 검증 내장 ④발산=자동판정 아닌 REPORT(경계·diff·결정 트레일)=S3c-c 분석 입력.
**결과: 5/5 게임 경계-동일(41턴 캡, 실플레이·공격·부화·시큐리티 배틀 포함)**. 캡 도달=정책 교착(종국 미검증) — S3c-c에서 N·캡 확대+종국 정책 보강. 하네스 교훈: 공격/저비용 플레이는 같은 턴 메인으로 복귀 — 루프=결정 반복+턴 전환 시 비교(턴당 1결정 구조는 오판).

## S3c-b 착지 (2026-07-17) — legal-action 표 재키잉 (RL 액션 공간 확정)
**디스패처 펌프 분기**: TurnFlowPumpHost 존재 시 — pending choice=기존 ResolveChoice 분기 공용(브리딩 결정 포함) / 그 외 유일 표면=**(Main, PhaseStart) 메인 대기**: Pass+PlayCard+Digivolve+ActivateOption+MainSkillActivate+DeclareAttack(기존 빌더 재사용; 파라미터는 TurnFlowDriver가 패킷 변환). AdvancePhase/EndTurn/브리딩 액션/memory-pass 대기=전부 은퇴(자동 흐름). SpecialPlay=생략(RD-P6C1-5/RD-R5-04 컴포넌트 STOP까지).
**TurnFlowDriver 보강**: ActivateOption→PlayCard 패킷(AS-IS에서 옵션 플레이=카드 플레이)·SpecialPlay=Illegal(정직 거부).
**witness**: R4S3b 8/8(신규: 메인 대기 표면·자동-흐름 공집합·choice-단독·비-턴 플레이어 공집합·스텝 액션 부재).

## S3b-2 몸통 착지 (2026-07-17) — PlayPermanentClass/UseOptionClass 1:1 (RD-P6C1-4 해소)
**PlayPermanentClass(:1150-1703)**: ctor/Set* 6종/필드/isJogress·isAppFusion verbatim. PlayPermanent 본문 —
DigiXros Select 1:1(HasDigiXros 도달 시 내부 STOP=RD-R5-04)·Assembly Select=STOP RD-P6C1-5(PlayCardClass 판례)·
프레임 표적(:1290-1340)=**배치가능성 판정으로 환원**(브리딩=빈 브리딩+CanEnterField(PayCost:false 어댑테이션)·배틀=용량 생략 RD-P6C1-2, 룰 게이트=:1350 CanPlayAsNewPermanent(미러 무-isBreedingArea 파라미터 주석))·
진화 arm=S3b-2① AddCardSource(뷰 재바인딩)·신규 arm=CreateNewPermanent+EnterFieldTurnCount 1:1·
jogress arm 완전 이식(루트=필드-리스트 인덱스 어댑테이션, DiscardEvoRoots/RemoveField/링크 트래시/AddDigivolutionCardsTop/InitUseCountThisTurn)·
시큐리티-루트=IReduceSecurity(null-ref emit-now 분기)·:1526-1529=OnDigivolveCompletedAsync(검증 op)·
버스트 턴엔드 트래시=STOP RD-P6C1-6(도달 불가: 상류 STOP)·북키핑 9필드 기록(②store)·
"move permanents(hybrid)"=순수 캔버스 UI strip·AddDigivolutiuonCards* 4호출=**빈-상태 no-op 증명+가드**(상태 생산자 0; 미래 생산자는 STOP 발화, 침묵 불가)·
꼬리=CardEffectCondition verbatim+OnEnterFieldHashtable(기존 빌더)+**인라인 OnEnterFieldAnyone**(설계 옵션 B — zone 진입 무-메타로 supply GAP-drop, 단일 개설자).
**UseOptionClass(:1704-1902)**: Execution 존 파킹(③op)·OnUseOption 창+배경효과·OptionSkill 발동 루프·OptionResolution 3스캔+해소 루프 verbatim·**다중-해소 픽**(:1855-1880 selectCardPanel)=ChoiceProvider.ChooseAsync(펌프-await/RunToStable-throw 공용 계약)·트래시 꼬리.
**핸드오프 STOP 교체**: PlayCardClass.PlayCard 꼬리(:868-960)=실제 핸드오프(SetJogress/SetBurstDigivolved/SetAppFusion/SetIsBreedingArea+PlayPermanent→UseOption). 부수: Permanent.StackCards(:884) 미러.
**witness 7/7**: PlayCard 실단언(비용·진입·북키핑 스탬프·임계 자동 턴종료)+**플레이-에이전트 풀게임**(공격-우선+보드캡 4 — 용량-생략 어댑테이션에서 play-first는 O(보드²) 성장으로 비종결, 공격-우선으로 시큐리티 소모 종결; 실 ST1/ST2 [On Play] 창·공격·배틀 전부 펌프 스택, 동일-시드 2게임 종국 다이제스트 동일).

## S3b-2② 착지 (2026-07-17) — Just-After 북키핑 store
**AS-IS 표면 전수 확정**: 쓰기=실행자 단독(PlayPermanentClass :1535-1569)·읽기=미포팅 카드 코퍼스(EX3/EX4/EX5/LM/ST10/BT11의 LevelJustAfterPlayed/PlayCost/Traits/CardNamesJustAfterDigivolved 스캔)+IsDigivolvedByTheEffect 커먼즈(DigivolvingEffect)·리셋=없음(수명=AS-IS Permanent 객체 수명=필드 체류).
**store 설계**: `PermanentBookkeepingStore`(신규, CardEffectCommons/) — **repository-키** ConditionalWeakTable(재키 소유 op 2곳이 EngineContext 없이 repository만 보유), 엔트리=AS-IS 9필드 verbatim(이름·기본값), live ICardEffect 참조=A1 인메모리 판례. **수명 매핑 CREATE/PERSIST/DIE**: ①CREATE=CreateNewPermanent에서 Reset(재플레이 카드가 전생 북키핑을 보면 안 됨) ②PERSIST=top 교체 소유 op 2곳에서 ReKey — DigivolveAction.AttachTargetAsSource(자연 진화·미러 AddCardSource 공용)+DeDigivolveHelpers(DeDigivolveAsync promote·ArmorPurgeTopAsync) ③DIE=RemoveField에서 Reset. Permanent에 9 프로퍼티 표면(AS-IS :3686-3941 선언 미러).
**동반 착지**: `CardEffectCommons.IsDigivolvedByTheEffect` 스켈레톤→1:1 실포팅(DigivolvingEffect 착지로 unblock; 뷰 참조-동등=InstanceId 동등 ADAPTATION).
**witness**: R4S3b 6/6 — 신규 수명 테스트(뷰-안정 쓰기→top 교체 후 생존(재키)→필드 이탈 후 사망(리셋)→재플레이 신생 기본값).

## S3b-2① 착지 (2026-07-17) — 영구물 생성/스택 substrate 매핑
**설계 판정 3건**:
1. **창 개설 좌석=옵션 B(실행자 인라인)**: SkillWindowSupply의 OnEnterField 변환은 zone-move 메타의 키 유무로 게이트("presence == carries the params") — 실행자의 zone 진입은 메타 無로 이동시켜 supply GAP-drop, AS-IS :1694 StackSkillInfos(OnEnterFieldAnyone)를 인라인 그대로 유지(C1 inline-insert 관례). supply의 OnEnterField 항은 OLD 경로(PlayCardAction/DigivolveAction) 전용으로 잔존, S3c 은퇴 후보.
2. **생성 op**: AS-IS 2단(`new Permanent(cards){IsSuspended}`+`CreateNewPermanent(permanent, frameID)` :479-510)은 뷰-모델에서 단일 op로 붕괴 — `CardObjectController.CreateNewPermanent(card, isSuspended, isBreedingArea)`: RemoveFromAllArea(:485 1:1)→zone 진입(프레임 슬롯 :488-491→BattleArea/Breeding append, 무프레임 ADAPTATION)→메타 init(isSuspended·EnteredThisTurnKey)→**G6-001 RegisterCard**(AS-IS 정적 효과 상주의 미러 등가)→뷰 반환. UI(:493-508) strip.
3. **스택 op**: AS-IS `AddCardSource`(:1045, Insert(0)=top 교체)는 "영구물 identity=top의 존 상주"라서 — old top 존 이탈(**plain Move→None**=DigivolveAction targetRemoval의 검증 형태, leave-창 비발화)→새 카드 동존 진입(메타 無)→**AttachTargetAsSource 재사용**(sourceIds+N-1 병증 상속)→RegisterCard→**갱신 뷰 반환**(뷰 키=top 인스턴스라 in-place 변이→뷰 교체 ADAPTATION, 호출부 재바인딩 `permanent = await permanent.AddCardSource(card)`).
**부수 착지**: `Permanent.EnterFieldTurnCount` 쓰기 표면(AS-IS 가변 int :1387 `=TurnCount`/:1500 jogress `=-1` → enteredThisTurn bool 캐리어 위 세터: value==현재턴⇒true·그 외⇒false; getter=비교형 재유도).
**witness 5/5**(R4S3b): 생성 op(존 진입·IsSuspended·병증 스탬프·jogress -1 클리어·뷰 identity)+스택 op(top 교체·단일 영구물 유지·sourceIds 스레딩·병증 상속). **잔여=②Just-After 북키핑 store ③AddExecutingCard → 실행자 몸통 포팅.**

## S3b-2 부분 착지 (2026-07-17) — CanEvolve 엔진(RD-P6C1-2 read측 해소) + 공격 디스패치 실단언 flip
**진화 비용 엔진 1:1**: CardSource에 `EvoCosts(:534-611)/CostList(:617-627)/CanEvolve(:1263-1285)` — added-requirement 3스캔(AS-IS 지연 Func 형태 그대로; AddedDigivolutionCosts(RD-P6B-15)와 같은 fold의 AS-IS-위치 판)+printed 요건(캐리어=`DigivolutionCostHelpers.ReadRequirements` — {TargetColor,TargetLevel,MemoryCost}=AS-IS EvoCost; null=Any는 substrate 확립 의미론)+ignore/색/레벨 게이트 중첩 verbatim. **STOP 스텁 2개 은퇴**: CanEvolve 확장(CardController)·CostList R2-C 스텁. DigivolveAction의 자체 legality 경로=컷오버까지 병행 이중석(동일 데이터, 문서화).
**witness flip**: 보드-디지몬 CanSelect STOP 핀 → **공격 디스패치 실단언**으로 교체: staged 공격자의 시큐리티 공격이 펌프 스택에서 전 파이프라인(선언→카운터→블록→배틀→시큐리티 체크) 완주+시큐리티 1장 소비. choice-pause seam(WaitPendingChoiceUnderPump) 실구동 검증.
**S3b-2 잔여(다음 배치, 실행자 본체)**: PlayPermanentClass(:1150-1703)+UseOptionClass(:1704-1902) 1:1 — 의존 감사 결과 대부분 기존재(CanPlayAsNewPermanent·RemoveFromAllArea/RemoveField/AddTrashCard·OnEnterFieldHashtable(Params)·IReduceSecurity·DiscardEvoRoots/AddDigivolutionCardsTop·OptionResolutionClass·SelectDigiXros/Assembly·ActivateBackgroundEffects). **미존재=설계 필요 3계열**: ①"영구물 생성/스택" substrate 매핑(AS-IS `new Permanent(cards)`/`AddCardSource`/`CreateNewPermanent` ↔ ZoneMover 배치+진화 스택 op — DigivolveAction의 검증된 op 재사용 판정) ②**Just-After 북키핑 store**(PlayingEffect/DigivolvingEffect/Level·PlayCost·CardNames·TraitsJustAfterPlayed/Digivolved/IsBurstDigivolved/IsAppFusion — 미러 Permanent는 view라 match-scoped store 필요, 키잉=진화 시 top 교체 문제 포함) ③AddExecutingCard(Execution 존 op). 프레임 항(PreferredFrame/frameId)=기존 zone-list ADAPTATION으로 환원.

## S3b 착지 (2026-07-17) — 메인 디스패치 seam + STOP 경계 2건 적발 (주입식 유지)
**CanSelect 술어 완성**: CardSource에 AS-IS-위치 4종 — `CanDeclareSkillList/CanDeclareSkill`(:1041-1054 verbatim)·`CanNotPlayThisOption`(:184-249, ICanNotPlayCardEffect 3스캔+색 요건)·`CanEnterField`(:1210-1258, ICanNotPutFieldEffect 3스캔; PlayCardsBridge 사본과 한시 이중=컷오버 dedup 후보)·`CanPlayJogress`(:2747-2792, 순서쌍 로컬 열거=SelectPermanentEffect 판례)·`CanPlayFromHandDuringMainPhase`(:139-178, **프레임 ADAPTATION**: digimon-arm=CanEvolve 직결·CanPutField=비용+CanEnterField, 용량 반쪽=RD-P6C1-2 기존 등재). TSM CanSelect()=AS-IS 5항 완전 미러.
**인텐트 seam**: MainPhaseAction 미러 6클래스(스켈레톤 착지; Photon strip, Execute=Task 번역, CheatAction=비스코프)+Player 큐(:166-190, match-scoped store)+TSM 세터 4종(:3050-3148, SetAttackingPermaent 오타 보존)+PassTurn(:3364, fire-and-forget→인라인 await ADAPTATION).
**선택대기+디스패치**: MainPhaseAsync에 AS-IS :971-1170 대기 루프(park=HasMainPhaseAction; AI/오토파일럿 분기=원본의 에이전트 대체물이라 strip — 헤드리스 에이전트가 그 자리)+:1176-1252 디스패치 arm 3종(ActivateEffectProcess·PlayCardClass(TargetFrameID=필드 리스트 인덱스 ADAPTATION)·공격=RD-9 chokepoint Declare). **공격 파이프라인 choice-pause seam**: 6개 공격 펌프 루프에 WaitPendingChoiceUnderPump(AS-IS 코루틴-내 선택 대기의 번역; Blocker/DeletionReplacement plain-pending을 펌프가 idle).
**TurnFlowDriver**(신규): 펌프 매치의 IActionProcessor — 메인 액션(Pass/PlayCard/Digivolve/DeclareAttack/ActivateMain)→미러 패킷 변환(엔티티id→AS-IS 인덱스), AdvancePhase/EndTurn=Illegal(결정 3), 나머지=Metadata 위임. HeadlessGameLoop의 GR-001 루프-레벨 메모리 평가=펌프 매치 스킵(OLD 발명 좌석; 펌프는 AS-IS EndTurnCheck 보유).
**STOP 경계 2건 적발(정직 게이트, witness로 핀)**: ①PlayCard arm→`PlayCardClass.PlayCard`의 PlayPermanentClass/UseOptionClass 핸드오프(**RD-P6C1-4**, AS-IS :1150-1902 ~750줄 미포팅 실행자) ②보드에 디지몬 존재 시 CanSelect digimon-arm→`CanEvolve`(**RD-P6C1-2**, 진화 요건/비용 엔진 미포팅). **→ S3b-2 배치 = 이 실행자 클러스터 포팅**(PlayPermanentClass·UseOptionClass·CanEvolve 엔진) — 이후 공격/플레이 witness가 실단언으로 flip.
**witness**: R4S3b-MainDispatch 3/3(STOP 핀 2+pass-표면 liveness 풀게임)·R4S3a 7/7 갱신(메인 대기+명시 패스 체인: 드라이버→패킷→PassTurn→EndTurnProcess 전 구간 실증, 풀게임 덱아웃·동일-시드 다이제스트 동일 유지).
**게이트**: 전체 스위트 — 미러 동일명 클래스(PassAction/PlayCardAction/AttackPermanentAction)가 양 네임스페이스 import 테스트 11건에서 CS0104 모호성 유발 → Runtime 별칭 재조준(단언 무변). 정합 fail-set=**base 107 동일**(재조준 후 7 green 복원+4=base 기존 red), R4 스위트 4종 전부 green(R4P4-ShadowRun 포함=live 경로 무영향).

## S3a 착지 (2026-07-17) — TurnFlowPump 기반시설 + 조기 페이즈 연속 실행 (주입식, 기본값 OLD 무변)
**신규 substrate** `Headless/Runtime/TurnFlowPump.cs`: ①`TurnFlowGate`=AS-IS WaitUntil의 async 등가(단일 park 슬롯, TCS 인라인-연속 — body 스택이 park를 가로질러 생존=코루틴 프레임 보존의 정확한 등가, 재진입/슬라이싱/기록-재생 전부 불필요) ②`TurnFlowPumpTask : IEngineTask`=기존 EngineTaskRunner에 등록되어 HeadlessGameLoop.StepAsync의 기존 TaskRunner 펌프가 스텝(신규 스텝 배관 0; ambient scope 자체 진입; 비-동기 await 탈출=fail-fast 가드) ③`TurnFlowPumpHost`=컨텍스트 서비스(Install/Find/FindExecuting·IsPumpExecuting 마커·펌프-소유 choice 예치 슬롯) ④`TurnFlowPump.RunAsync`=AS-IS 드라이버 체인(StartGame→멀리건 게이트→{Active→Draw→Breeding→Main→End→flip} 루프)+턴 경계 블록(OLD flip 블록 등가: OnceFlags/PlayerTurnCounters/OnPlayReactivation/ExpireFixedCostCalc+메모리 재부호 Set(|m|)=turn-relative 좌표 번역, CEntity 리셋은 EndPhaseAsync가 AS-IS-위치 소유라 제외).
**await-모드 choice 파이프라인**: 펌프-실행 중(IsPumpExecuting) ①DeferredChoiceProvider.ChooseAsync=throw+재실행 계약 대신 게이트 await(효과 body 제자리 재개 — 재실행 0, replay 프레임 무접촉) ②AgentSkillWindowChoicePort.ChooseOrderAsync=continuation 기록 없이 예치 답을 인덱스로 직해 ③MetadataActionProcessor.ResolveChoiceAsync에 예치 분기(펌프-소유 choice는 legacy resume 라우팅 전부 우회 — 언와인드된 적 없는 body의 이중-구동 방지). RunToStable-구동 창은 기존 throw 계약 그대로(마커 false).
**body live seam 2개 활성화**: ①BreedingPhaseAsync :719-816 결정 블록(ChoiceType.BreedingDecision 신설 — AS-IS bool ValueSelection 외부화; hatch-우선 quirk :804 보존; ZoneMover Hatch/MoveBreedingToBattle=P2a 매핑) ②MainPhaseAsync :971 선택 대기=park(디스패치 body=S3b, RD-S3B-01). Player.CanHatch(:1168 verbatim)/CanMove(:1172, 프레임 용량 반쪽=RD-P6C1-2 기존 ADAPTATION)/DigitamaLibraryCards 포팅.
**witness 7/7**(R4S3a-PumpFlow.Tests): StartGame 5/5+멀리건 → keep/keep→시큐리티 5/5→브리딩 정지 → **빈 메인 자동-패스 실증**(CanSelect() false→:960 EndTurnProcess: 패스 점프 Set(-3)→임계→End→flip — AS-IS 의미론이 스텁 상태에서 그대로 발현) → 풀 게임 덱아웃 종국+승자 마킹 → 동일-시드 2매치 종국 다이제스트 동일 → 미설치 매치=OLD 무변(펌프 태스크 0).
**게이트**: 전체 스위트 — fail-set 차이=G1E-001 단건(ChoiceType 스키마 열거=BreedingDecision 등재로 재조준, 단언 약화 0), 그 외 base 107 동일·R4P4-ShadowRun green(live 경로 무영향). 스위트 439프로젝트(신규 1).
**S3b로**: 메인 디스패치 영역(:971-1253) 미러+Pass→EndTurnProcess 라우팅+legal-action 표 재키잉. **S3b 리스크 신규 등재**: 공격 파이프라인 choice(BlockTiming 등)가 펌프-스택에서 열릴 때의 throw 경로 — await-모드 편입 또는 예치 라우팅 필요(S3a에선 미노출: 조기 페이즈 무공격).

## P2b 착지 (2026-07-17) — 턴-말 seam 이관 + P2-1 + 창 seam 재검증
**턴-말 트리오 미러**: `AutoProcessing.EndTurnCheck/TurnEndMinMemory/EndTurnProcess`(AS-IS :630-727) AS-IS-위치 1:1(IEnumerator→Task). 번역 4건: ①`Passed`=미러 TSM에 AS-IS :3150 신설(match-scoped box, isExecuting 동형) ②:683-693 좌석-절대 게이지 쓰기(PlayerID 0/1 분기)→턴-상대 좌표 `Set(-3)` 단일 환원(양팔 동치 증명 주석; live PassTurn과 동일값) ③:694 memoryObject.SetMemory=UI strip(Player.cs:559 판례) ④:722 SetMainPhase=UI 재장전 strip(룰 내용="Main 유지·펌프 계속"). TurnEndMinMemory는 live HeadlessMainPhaseFlow.ResolveTurnEndMinMemory(W3c-4b B2)와 한시 이중 보유 — S3에서 flow 사본 은퇴.
**S2-cursor 스캐폴드 해소**: `GameContext.TurnPhase`에 AS-IS(:126 가변 필드) SETTER 신설(TurnController.SetPhase 위임, 커서=PhaseStart 리셋) — dormant body의 per-method `currentPhase` 로컬 전량 제거, 전 body가 AS-IS처럼 gameContext.TurnPhase 직접 읽기/쓰기(:554/:666/:715/:897/:3162). seam 호출 8지점 활성화(:579/:641/:655/:696/:704/:831/:880/:950).
**P2-1 이행**: MainPhaseAsync에 :880 진입 EndTurnCheck+:882-885 `goto EndMainPhase` 가드 구조 삽입, AS-IS EndMainPhase 라벨(:1256) 실재화(:966 goto도 1:1 복원). 브리딩 중 임계 도달 시 OnStartMainPhase 창 미발화 경로 확보.
**리뷰2 P2-② 이행**: `HeadlessTurnState.IsMainPhase` dead 접근자 제거(소비자 0 grep 확증; dormant body는 IsMainPlayPhase/직접 Phase 읽기만).
**창 seam 재검증(리스크 ③ 해소)**: live EndTurnAsync의 Emit(OnEndTurn)→SkillWindowSupply(**null payload 재구성=RDW-07 CLOSED**)→AutoProcessAsync→미러 StackSkillInfos/AutoProcessCheck — 즉 live drain은 이미 미러 창 유닛을 구동. 미러 EndTurnProcess의 `StackSkillInfos(null, OnEndTurn)`+AutoProcessCheck는 **같은 기제의 인라인 진입**이므로 S3 flip은 수집 진입점 교체일 뿐 창 기제 불변. 멤버십 동일(null payload)·park/resume 동일(WindowChoicePendingException→choice-pause→ResumeSuspendedWindowsAsync). 유일 의미 차: live의 `EndOfTurnDrainedTurn` once-per-turn 마커는 발명물 — AS-IS는 EndTurnProcess마다 창 재실행(효과별 once-per-turn 캡이 상한). S3에서 마커 은퇴=AS-IS 의미론 복원(S3 리뷰 항목).
**게이트**: 전체 스위트 331/107/438(base 329/107/436+R4 테스트 2프로젝트; **신규 FAIL 0** — RD6-EndTurnSequence red는 stash 대조로 base 기존 red 확증), R4P2a-PhaseBodies·R4P4-ShadowRun green(OLD-vs-OLD bit-identical 유지), FAILd-07(ChangeEndTurnMinMemory) green. fail-set 스냅샷=세션 tmp p2b_failset.txt.
**S3 이월**: ①MainPhase 디스패치 영역 자체 EndTurnProcess 사이트(:1149 pass-command/:1158 auto-pass)=외부화된 Pass 액션이 AutoProcessing.EndTurnProcess로 라우팅돼야 함 ②TurnEndMinMemory flow 사본 은퇴 ③EndOfTurnDrainedTurn 마커 은퇴 ④HeadlessMainPhaseFlow invented eval 은퇴.

## S2 착지 + 리뷰지점 2 GO (2026-07-17)
S2(통합 2a8015e8): HeadlessPhase 6값+TurnStepCursor{PhaseStart,Starting,Unsuspending,AwaitingMemoryPassEnd}, 재키잉 9쌍 전단사, 소비자 전수 이관, 관측=6 one-hot+커서(정보-보존 단언), 재조준 15파일(단언 약화 0 — "23종"은 상한이었음), shadow 4판 bit-identical, fail-set 동일. **리뷰2 GO(P0/P1 0)**: 전단사·직독 0·계약 불변·DoneStartGame 진리표 동일 확인. P2 3건: ①커밋 메시지 "값·순서 1:1" 과장(None=0은 기존 관례, 이름-매핑이라 무해) ②`HeadlessTurnState.IsMainPhase`=dead 접근자(memory-pass 오포함 함정 — P2b/S3에서 dormant body가 IsMainPlayPhase 사용 확인+제거 권고) ③관측 shape 변경=결정 A 수용 비용(다운스트림 체크포인트 비호환 플래그).
