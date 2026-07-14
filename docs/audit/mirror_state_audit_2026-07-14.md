# 미러 상태 감사 — "AS-IS 동일 파일·동일 로직" & "Headless=substrate만" 검증 (2026-07-14)

작업 보류 중 수행한 읽기전용 감사. 질문 3개에 대한 측정 결과.

## Q1. 제대로 개발되고 있는가 — **층에 따라 갈림**

| 층 | 상태 | 근거 |
|---|---|---|
| 효과종류층 `Script/CardEffects/` | ✅ 1:1 | 73/73 파일, 스텁 0, 미러 3017줄(AS-IS 2567+substrate주석) |
| 팩토리층 `Script/CardEffectFactory/` | ✅ 1:1 | 27(+분할2)/27, 6822줄(AS-IS 5242) |
| 효과 베이스 `ICardEffect.cs` | ✅ 1:1 | 1252 vs 1291줄 |
| `AutoProcessing.cs`·`AttackProcess.cs` | ✅ 실질 | 1218/1106·990/628 (마이그 골 1·2 재하우징) |
| 카드층 `CardEffect/**` | 🟡 진행 | 신모델 재포팅 진행(P5+BT1 12), 구모델 잔여 ~39실카드+27Tfx |
| **상태/룰층** `Permanent`·`CardSource`·`CardController`·`TurnStateMachine` | ❌ **껍데기+위임** | 아래 Q2 |

## Q2. AS-IS 동일 파일이 동일 로직을 갖는가 — **핵심 상태층은 아니오**

| 파일 | AS-IS | 미러 | 판정 |
|---|---|---|---|
| Permanent.cs | 4187줄 | 864줄 | ❌ DP getter가 발명물 `ContinuousDpGate.ResolveDp` 위임(:136). AS-IS DP(:499-668)의 3-스코프 스캔·ImmuneFromDPMinus·isUpDown·LinkedDP·Boosts **부재**(LinkedDP/Boosts/ImmuneFromDPMinus/BaseCardDP 멤버 0회). `Level`(:146)은 올바른 직접-스캔 선례 존재 |
| CardSource.cs | 4357줄 | 1221줄 | ❌ 28%, Headless 위임 14회 |
| CardController.cs | 5988줄 | 2022줄 | ❌ 34%, 위임 29회. 내부클래스 PlayCardClass/IBattle이 별도 파일로 분리(경로 이탈) |
| TurnStateMachine.cs | 3373줄 | **57줄** | ❌ gameContext 접근용 껍데기, 턴흐름은 `GameFlowProcessor`("temporary home" 자인) |
| GameContext.cs | 186줄 | 107줄 | 🟡 접근자 미러(실로직 위임) |

- top-level 156 중 미러 78. **누락 81개는 전부 UI/로비/사운드**(BGMObject·Draggable·LobbyManager…) → substrate-strip **정당**.
- `Script/`의 발명 파일 3: `IBattle.cs`·`PlayCardClass.cs`(AS-IS는 CardController.cs 내부 클래스 — 분리=경로 이탈)·`OnEnterFieldHashtableParams.cs`.

## Q3. Headless/는 "기존 의존성 대체 기능만"인가 — **아니오, 게임 룰 대량 잠복**

총 221파일 ~42k줄. 분류:

**정당한 substrate** (~17k줄): Services(존저장소·리포지토리 34), State(13), Choices(9), Coroutines(4), DataLoading(8), Diagnostics(7), Bridge(6), Runtime 중 Headless* 액션큐/합법성/페이즈 컨트롤러/RL 관측(≈40파일) — 코루틴→async·GManager→EngineContext·Photon스텁·ChoiceProvider·존저장소 취지에 부합.

**게임 룰(AS-IS Script/* 소속이어야 함)** (~25k줄 추정, Runtime 106중 ~60 + Effects 33중 ~25):
| Headless 파일 | AS-IS 정위치 |
|---|---|
| Continuous{Dp,Keyword,Modifier,Restriction,Immunity}Gate·ContinuousScopeEvaluation·RestrictionScan·TrashProtectionScan·NewModelContinuousScan(Script측 발명) | `Permanent.cs`/`CardSource.cs` getter들 (DP·키워드·CanNot*) |
| MatchStateMutationSink(1792)·DeletionReplacement{Gate,Timing,CandidateConditions}·BattleDeletionGate·DeletionSourceTrash·DpZeroDeletionHelpers·CardLeavePlayCleanup | 삭제/치환 룰 — AS-IS `CardController`·키워드 Process들 |
| SecurityResolver(881)·BattleResolver(686)·BlockTiming·AttackPipeline | `AttackProcess.cs`(부분 재하우징됨, resolver 잔존) |
| DigivolveAction(1168)·DigivolutionStackHelpers·DigivolutionCostHelpers·Free/FusionDigivolveHelpers·PlayCardAction·PlayCostHelpers·SpecialPlayAction | `CardController.cs`(PlayCardClass 등) |
| GameFlowProcessor(930)·EndTurnCleanup·MainPhaseFlow | `TurnStateMachine.cs` |
| WindowResolver(+Wiring 1650)·EffectScheduler·EffectRegistry·TriggerTimingMap·AutoProcessingTriggerCollector | `AutoProcessing.cs`/`MultipleSkills`/`CutInProcess` (AS-IS에 registry 없음) |
| OverclockEffect·AllianceAttackBoost·RaidAttackSwitch·ProgressImmunity·EndOfTurnEffectAttack·LinkHelpers·RevealAndSelect | AS-IS 키워드 Process/Select* 컴포넌트 |

## 종합 판정

**효과모델 리빌드(P1~P6)는 효과층에선 1:1 달성, 그러나 상태/룰층은 여전히 Headless에 로직이 있고 미러 파일은 껍데기/위임.** "Headless=substrate만" 원칙(asis-mirror-migration-decision) 위반 상태이며, union·게이트는 이 위반의 증상(로직이 발명 파일에 있으니 이중 표현을 잇는 다리가 필요했던 것).

**"제대로"의 정의** (mirror-into-asis-file-not-invented): AS-IS 로직이 동일 경로·동일 파일·동일 로직으로 존재. 즉:
1. `Permanent.DP`/`BaseDP`/`HasDP` 등 getter를 AS-IS 그대로 미러(Level 선례) → Continuous*Gate/Scan 해체
2. 삭제/치환/보호 룰 → CardController·키워드 Process 미러로
3. 턴흐름 → TurnStateMachine 미러로
4. 트리거 창 → AutoProcessing/MultipleSkills 미러로 (EffectRegistry 삭제)
5. Headless는 substrate 목록(존이동·리포지토리·ChoiceProvider·async실행기·액션큐/RL관측)만 잔류

규모: 게임룰 ~25k줄의 재하우징 + 상태층 미러 ~10k줄 채움. 마이그 골1~7이 이미 이 방식(AttackProcess 950줄 재하우징)의 실증 선례.
