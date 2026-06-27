# 트리거 타이밍 emission 갭 — Phase 4 배선 체크리스트

- 작성일: 2026-06-27
- 출처: 원본↔포팅 자체 감사(`original_vs_port_divergence_audit.md` 🔵항목)의 정량화.
- 성격: **엔진이 원본에서 발화하지만 포팅이 emit하지 않는 타이밍.** 이 타이밍에 바인딩된 카드 본문은 현재 dead(발동 자리 없음).
- 처리 방침: **지금 일괄 emit하지 않음.** 각 타이밍은 (1) 대응 엔진 연산이 실제 일어나는 지점에서 emit해야 하고, (2) 바인딩 효과가 있어야 E2E 검증이 가능하므로 — **해당 카드군 포팅 시점에 배선**한다(W1-2 패턴: `TriggerEventEmitter.Emit` + `TriggerTimings` 상수 추가, subject 있으면 스코프).
- 이미 emit되는 타이밍: OnPlay/OnEnterField/OnDeletion(필드한정, D-5)/OnLeaveField/OnAddHand/OnReturnToHand/OnReturnToLibrary/OnAddSecurity/OnLoseSecurity/OnAttack/OnCounter(W6)/OnBlock(A3)/OnSecurityCheck(W4)/OnStartTurn/OnEndTurn/WhenDigivolving/OnDraw.

## 미발행 타이밍 (빈도/우선순위순)

| 우선 | 타이밍(원본 EffectTiming) | 원본 발화 지점 | 포팅 emit 추가 위치(제안) |
|------|--------------------------|----------------|---------------------------|
| 🔴高 | `OnStartMainPhase` | `TurnStateMachine.cs:905` | 메인 페이즈 진입 시(`HeadlessMainPhaseFlow`/페이즈 전이) actor=턴플레이어 |
| 🔴高 | `WhenLinked` | `Permanent.cs:1290` | 링크 부착 연산 구현 시, subject=링크된 카드 |
| 🔴高 | `OnStartBattle` / `OnEndBattle` | `CardController.cs:4557,4718` | `BattleResolver.ResolveAsync` 비교 전/삭제 후. **OnStartBattle은 DP 비교 전 emit+해결**해야 "전투 시작시 +DP"가 반영됨(주의) |
| 🟠中 | `OnTappedAnyone`/`OnUnTappedAnyone` | `CardController.cs:5648,5754` | suspend/unsuspend mutation 적용 지점, subject=대상 |
| 🟠中 | `OnAddDigivolutionCards` | `Permanent.cs:1119,1223` | 소재 부착(`DigivolutionSourceStackPort`/digivolve) 시, subject=대상 |
| 🟠中 | `OnMove`(WhenMoving) | `CardObjectController.cs:1111` | 이동 연산 시(존 이동 일반), subject=이동 카드 |
| 🟠中 | `OnUseOption` | `CardController.cs:1765` | 옵션 발동(`OptionActivateAction`) 시 |
| 🟠中 | `OnDiscardHand` | `CardController.cs:56` | 핸드 버림(Hand→Trash) 시, subject=버린 카드 (D-5로 OnDeletion과 분리됨) |
| 🟡低 | `OnFaceUpSecurityIncreased` | `CardController.cs:5506,5548` | 공개 시큐리티 증가 연산 시 |
| 🟡低 | `OnDigivolutionCardDiscarded` | `CardController.cs:5215` | 소재 trash 시, subject=소재 |
| 🟡低 | `OnLinkCardDiscarded` | `CardController.cs:5327` | 링크 카드 trash 시 |
| 🟡低 | `OnDigivolutionCardReturnToDeckBottom` | `CardController.cs:5400` | 소재 덱하단 복귀 시 |
| 🟡低 | `WhenTopCardTrashed` | `CardController.cs:4915,5092,5958` | 최상단 카드 trash 시 |
| 🟡低 | `OnReturnCardsToHandFromTrash` | `CardObjectController.cs:578` | trash→hand 시 |
| 🟡低 | `OnReturnCardsToLibraryFromTrash` | `CardObjectController.cs:800,882` | trash→library 시 |
| 🟡低 | `OnDiscardSecurity` | `CardController.cs:4377` | 시큐리티 trash 시(체크와 구분) |
| 🟡低 | `Before/AfterPayCost` | `CardController.cs:985` | 비용 지불 전/후 |
| 🟡低 | `OnUseDigiburst` | `CardController.cs:2228` | 디지버스트 연산 구현 시 |

## 참고
- 일부(`OnStartBattle` 등)는 단순 emit이 아니라 **해결 순서**가 중요(전투 비교 전에 DP 변경 효과가 해결돼야 함) → 해당 시점에 emit 후 즉시 스케줄러 drain 또는 별도 윈도우 처리 설계 필요.
- emit 추가 시 W1-2/W4 패턴 준수: 카드 한정 타이밍은 `subject`로 스코프, 전역 타이밍은 subject 없이.
- 본 갭은 카드가 실제로 해당 타이밍에 바인딩될 때 비로소 의미가 생기므로, **Phase 4 카드군별로 "이 카드군이 쓰는 타이밍이 emit되는가" 확인 후 배선**.
