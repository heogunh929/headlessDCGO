# fire-and-forget `StartCoroutine` 전수 census — 로드맵 2.7 / R4 종결

2026-07-29. 문장형(핸들 버림) `StartCoroutine(...)` 전수: AS-IS 트리 130곳, **게임 범위 96곳**
(로비·덱에디터·타이틀 등 범위 밖 34곳 제외 — docs/out_of_scope.md).

## 드라이버의 현행 처리 (이미 흡수됨)

`CoroutineDriver.AttachToStartCoroutine` → `MonoBehaviour.Started` 구독 → Adopt: 핸들 버려진 루틴을
독립 루틴으로 **병행 구동**한다. 구노선 메모(RunDetached 인라인+파킹 시 throw)는 폐기된 상태 기술.
실증: 모든 정체 덤프에 `LoadingObject.SetLoadingText`(while(true) 루프 2개)가 병행 생존.

## Unity 의미론과의 차이 — 1건, 등재

Unity의 `StartCoroutine`은 **첫 yield까지 즉시 인라인 실행**, 드라이버는 **다음 틱부터** 시작한다
(CoroutineDriver.Start 주석 명시). 즉 첫-yield-이전 구간이 1틱 늦다. 아래 분류상 이 지연이 룰에
닿는 사이트는 없다 — 유일한 룰-흐름 병행부(TSM:969)는 대기 루프가 SetMainPhase가 아니라 행동
큐 플래그만 읽으므로 무해 [판단+정황]. 새 사이트가 생기면 이 차이를 먼저 의심할 것.

## 96곳 분류

| 부류 | 곳 | 대표 | 판정 |
|---|---|---|---|
| **연출·표시·사운드** | ~60 | `Effects.DeleteCoroutine` ×22(이펙트 소멸), ShrinkUpUseHandCard, BreakGlass, TargetArrow, Loading/PlayLog/ShowXxx close, BGM FadeOut(TSM:3353), `SetHandCardPlayablity`(:2084 — 시각 하이라이트, 룰 판독 0) | 무해 |
| **핸드오프(연쇄 이양)** | ~20 | GameStateMachine(:291)·turnStateMachine.Init·AwakeCoroutine·SetRandomCoroutine·EndBattleCoroutine·SetMainPhase(:969,:1965,:2748)·PassTurn→EndTurnProcess(:3369)·AutoProcessing:723 | 시작 직후 호출자 종료/대기 — 순차와 등가. :969만 병행이나 위 판정 |
| **UI 클릭 배선 내부** | ~12 | AddClickTarget 람다 속 SC(OnClick_Select/SelectDefender/SetMainPhase), Draggable 드롭 경로의 SelectWheterToJogress/Burst/AppFusion·SelectJogressTarget(:2196-2427) | 사람 클릭 전용 — headless **도달 불가** (정책은 QueueMainPhaseAction 직행) |
| **개발자 치트** | 7 | CheatAction.cs:49-67 (DrawCard·TrashCard·PlaceInSecurity·AlterMemory fire-and-forget) | CheatAction을 큐잉하는 주체 없음 — **도달 불가**. 유일하게 룰 상태를 병행 변이하는 모양이므로, 치트를 쓰게 된다면 재분류 필수 |
| **패널 콜백** | 2 | SelectCardPanel:623,:660 OnClickButtonActionCoroutine | 선택 채널이 클릭한 콜백의 실행체 — 순차 |

## 결론

**병행성이 룰 상태에 닿는 도달-가능 사이트 0** [실측 96/96 분류 + 의심 5곳 정독].
R4는 "설계 필요"가 아니라 "드라이버 Adopt 설계로 흡수 완료 + 의미론 차이 1건 등재"로 종결.
게이트(단계 2·3, 96/96 다이제스트 일치)가 경험적 뒷받침.
