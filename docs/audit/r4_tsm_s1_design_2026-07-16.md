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

## S2 착지 + 리뷰지점 2 GO (2026-07-17)
S2(통합 2a8015e8): HeadlessPhase 6값+TurnStepCursor{PhaseStart,Starting,Unsuspending,AwaitingMemoryPassEnd}, 재키잉 9쌍 전단사, 소비자 전수 이관, 관측=6 one-hot+커서(정보-보존 단언), 재조준 15파일(단언 약화 0 — "23종"은 상한이었음), shadow 4판 bit-identical, fail-set 동일. **리뷰2 GO(P0/P1 0)**: 전단사·직독 0·계약 불변·DoneStartGame 진리표 동일 확인. P2 3건: ①커밋 메시지 "값·순서 1:1" 과장(None=0은 기존 관례, 이름-매핑이라 무해) ②`HeadlessTurnState.IsMainPhase`=dead 접근자(memory-pass 오포함 함정 — P2b/S3에서 dormant body가 IsMainPlayPhase 사용 확인+제거 권고) ③관측 shape 변경=결정 A 수용 비용(다운스트림 체크포인트 비호환 플래그).
