# R4(턴흐름 TSM 재하우징) 사전조사 (2026-07-16)

Base: b1-registry-integration(5e4639d9 시점). 조사 전용. 상세 근거는 조사 에이전트 보고 원문(세션 기록) — 본 문서는 판정·계획 요지.

## 핵심 판정
1. **실이관 규모는 3,373줄이 아니라 ~1,000-1,400줄** — TurnStateMachine.cs:1354-2872(~1,500줄)는 SetMainPhase() 순수 Unity UI(클릭/드래그/패널)로 headless 대응물이 없어 **비-스코프**. 룰 출구는 Set*/QueueMainPhaseAction 표면뿐(액션 모델이 이미 커버).
2. **정의 리스크 = 제어-역전(control-inversion)**: AS-IS는 "결정을 당기는 코루틴"(WaitUntil(HasPlayerSelection)), 미러는 "환경이 액션을 미는 RL step-머신"(DcgoMatch.StepAsync 공개 표면). while(true) 코루틴 verbatim은 step API와 공존 불가 — **S1 설계 결정이 모든 배치의 게이트**. (권고 방향: 확립된 ADAPTATION 관례대로 — phase body를 AS-IS region 1:1 async 메서드로, 인터랙티브 정지 3곳(멀리건 :405/:446·브리딩 :723/:788·메인 대기 :972)은 park/resume+액션큐 seam으로, step API는 substrate 드라이버로 유지.)
3. **페이즈 모델 발산(load-bearing)**: 미러 HeadlessPhase 9값(발명 Unsuspend·MemoryPass 분할) vs AS-IS 6값 — S2 화해 필수(GameContext.TurnPhase가 현재 collapse로 가림).
4. **원장 정정**: "RD6 red들" 특성화는 **stale** — RD6-EndTurnSequence(4테스트)·W-EoTFIX 등 턴-흐름 스위트는 전부 active-green, red/skip 0. 발산 원인 지점은 (a)OnEndTurn 해소가 EndTurnAsync에 거주 (b)memory-pass 분할 페이즈 (c)GameContext의 IsSecurityLooking/Memory/TurnCount 미노출.
5. **의존 판정**: R2-C 접점 최소(카드별 비용 지불은 이미 액션들로 위임; R4-소유 접점=턴경계 ExpireFixedCostCalc:1050+memory-pass 기제) · MainProcessingEffect=누수 아님(AS-IS verbatim, AutoProcessing 미러 소유 — R4 비동승) · R5 잔여=R4 무관(per-Permanent 상태 멤버 부재가 blocker).

## AS-IS 지도 요지
- 척추 GameStateMachine(:301-334) while-루프: SwitchTurnPlayer→Active→Draw→Breeding→Main→End.
- region: StartGame(:341-504, 멀리건·시큐리티 5장) / ActivePhase(:530-648, OnStartTurn 창·공격 펌프·EndTurnCheck·언탭+Reboot 게이트·버킷 리셋 2종) / DrawPhase(:652-697, 덱아웃 패배) / BreedingPhase(:701-837, 부화/이동 인터랙티브) / MainPhase(:877-1351, repeat-until-select 펌프+액션 디스패치 3종) / EndPhase(:3151-3210, EoT 버킷 리셋=HeadlessEndTurnCleanupFlow가 기미러) / EndGame·PassTurn.
- 턴-말 seam: AutoProcessing.EndTurnCheck(:631)/TurnEndMinMemory(:645)/EndTurnProcess(:675)는 MultipleSkills 창과 불가분(AutoProcessing.cs:45-51) — **R3 창 seam과의 결합이 최난점, 직렬·리뷰-게이트 단계**.

## 미러 해체 대상 vs 존치
해체(발명 하우징): HeadlessGameLoop(358)·HeadlessEarlyPhaseFlow(306)·HeadlessMainPhaseFlow(303, 단 AS-IS-literal fold 포함)·MetadataActionProcessor의 AdvancePhaseAsync/EndTurnAsync(:903/:946-1060)·HeadlessLegalActionDispatcher 페이즈 표. 존치(substrate): InMemoryHeadlessTurnController+HeadlessTurnState(상태 저장소)·DcgoMatch 표면(시그니처 보존 필수). 기정위치: HeadlessEndTurnCleanupFlow(EndPhase body로 re-point만).

## 배치 계획
- **S1 설계 결정**(코루틴↔step 화해 + 9→6 페이즈 방침) — 선행 게이트, 코드 0.
- 병렬 휴면: P1 GameContext 갭필(~60줄) / P2 phase-body 휴면 조립(:341-837 region별, ~400-600줄) / P3 EndPhase re-point(~50줄) / P4 witness·shadow-driver 하네스(23스위트 재조준 준비).
- 직렬: S2 페이즈-모델 화해(~120-200줄, 전 페이즈 참조와 직렬) → **S3 드라이버 flip**(shadow-run으로 HeadlessTurnState per-step 동일성 실증 후 컷오버, 적대리뷰 필수; R3 창 seam 조율 동반).
- 검증: 곡선 스위트 23종 목록 확보(원문 보고), full-turn 순서 witness+TfxEndTurnLose3Memory 프레임 단언.
- 계기판: Headless 턴-룰 잔량(라인) → 0 / 미러 TSM region 패리티.
