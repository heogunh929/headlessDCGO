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
