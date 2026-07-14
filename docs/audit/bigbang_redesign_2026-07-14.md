# 빅뱅 재설계 — 진짜 스코프: 상태/룰층 재하우징 + 발명물 해체 (2026-07-14)

전제 감사=`mirror_state_audit_2026-07-14.md`. 실패 복기=memory `bigbang-scope-failure-postmortem`.
설계까지만 — 구현은 별도 지시 후.

## 0. 종점 정의 (구조 기준 — green 아님)

리빌드 완성 = 아래 5가지가 **기계적으로 0/100%**:
1. **미러 로직 보유율**: AS-IS 게임로직 파일(UI 제외)의 미러가 동일 경로·동일 파일명·동일 로직 보유 — 100%
2. **Headless 게임룰 잔량**: Headless/에 게임 룰 0줄 (substrate 화이트리스트 외 파일 0)
3. **발명물 잔수**: Continuous*Gate·NewModelContinuousScan·EffectRegistry·EffectBinding·LegacyBindingBridge·ActivatedEffect(구모델)·IEffectBody — 참조 0, 파일 0
4. **union/다리 잔수**: 0 (로직이 정위치에 있으면 존재 이유 소멸)
5. **`Script/` 발명 파일**: 0 (PlayCardClass/IBattle → CardController.cs 내부로 복귀)

**green/RuleAudit은 최종 검증 게이트일 뿐**(모든 구조 기준 충족 후 행동 동일성 확인). 중간 진척 보고는 구조 지표로만.

## 1. 진척 계기판 (기계 측정 — 매 단위 보고)

| 지표 | 측정 | 시작값(2026-07-14) | 종점 |
|---|---|---|---|
| Headless 게임룰 파일 | 화이트리스트(§4) 외 `Headless/**/*.cs` 수 | ~85파일 ~25k줄 | 0 |
| 발명 게이트/스캔 참조 | `grep -r 'ContinuousDpGate\|ContinuousKeywordGate\|ContinuousModifierGate\|ContinuousRestrictionGate\|ContinuousImmunityGate\|NewModelContinuousScan\|ContinuousScopeEvaluation\|RestrictionScan\|TrashProtectionScan' src --include=*.cs -l` | ~40+ | 0 |
| EffectRegistry/Binding 참조 | 동일 grep | 33파일 | 0 |
| 구모델 잔재 | `ToBinding\|ActivatedEffect\b\|IEffectBody` 참조 | ~93파일 | 0 |
| 미러 4대 파일 로직 보유 | region 단위 체크리스트(§3) | Permanent 일부·CardSource 일부·CardController 일부·TSM 0 | 전 region |
| 컴파일 | error CS | 0 | 0 (매 커밋 유지) |

**RED 정책**: 행동 테스트 red는 종점까지 허용·**미계수**. 컴파일 green만 매 커밋 유지(작업 가능성). "N개 통과 회복" 보고 금지.

## 2. 스코프 — 무엇이 남았나

**완료(재설계 불필요)**: 효과종류층 `Script/CardEffects/` 73/73 · 팩토리 `Script/CardEffectFactory/` · `ICardEffect.cs` · 카드층 재포팅 방식(레시피 확립).

**본체 = 상태/룰층 재하우징** (AS-IS 동일 파일·동일 로직으로):

## 3. 재하우징 단위 (AS-IS 파일 기준 분해)

### R1. 연속효과 읽기층 — `Permanent.cs`(4187줄) + `CardSource.cs`(4357줄)
모든 것이 이 getter들을 읽으므로 **최우선**. AS-IS region 단위로 이관:
- Permanent: Level(선례 완료)·HasDP/BaseDP/DP(3-스코프 스캔+ImmuneFromDPMinus+isUpDown+LinkedDP+Boosts+clamp)·DP-minus면역·CanBeReturned(hand/deck)·ImmuneFrom(DeDigivolve/TrashingStack)·CardSources/Stacked/Digivolution/LinkedCards·시큐리티체크 매수·진화원증가효과·CanSuspend/CanMove/CanAttack계열·CanBeDestroyed(BySkill/ByBattle)·Strike/SecurityAttack·키워드 보유(HasBlocker 등)
- CardSource: CanPlay/CanPayCost·색요구(MatchColorRequirement+ignore-color)·BaseCardColors/CardColors·CardDP·CardNames/BaseCardName·EvoCosts/CostList·LinkCost·CanNotBe계열
- 필요 멤버 신설(AS-IS에 있고 미러에 없는 것): `LinkedDP`·`Boosts(DPBoost)`·`ImmuneFromDPMinus(cardEffect)`·`BaseCardDP`·`ActivatedTime` 등 — AS-IS 시그니처 그대로
- **동시 해체**: Continuous{Dp,Keyword,Modifier,Restriction,Immunity}Gate·NewModelContinuousScan·ContinuousScopeEvaluation·RestrictionScan·TrashProtectionScan — consumer를 `permanent.X`/`cardSource.X` 읽기로 재배선 후 파일 삭제. (스캔 코드 자체는 getter 안으로 흡수 — FoldDp 등에서 검증된 스캔 본문 재사용 가능하되 **위치·이름은 AS-IS**)

### R2. 룰/프로세스층 — `CardController.cs`(5988줄) + 키워드 Process
- CardController AS-IS region: Trash cards from hand·Play cards(PlayCardClass 내부복귀)·pay cost·cut-in(before/after)·select DigiXros/Assembly·play permanent·use option·Hatch DigiEgg·move permanents 등
- Headless 잠복분 이관: `PlayCardAction`·`DigivolveAction`·`DigivolutionStackHelpers`·`Free/FusionDigivolveHelpers`·`PlayCostHelpers`·`DigivolutionCostHelpers`·`SpecialPlayAction`
- 삭제/치환 룰: `MatchStateMutationSink`(1792줄)를 **분해** — 존이동 적용부=substrate 잔류, 룰 판정부(삭제 가능/치환/보호)=AS-IS 정위치(Permanent getter·키워드 Process)로. `DeletionReplacement{Gate,Timing,CandidateConditions}`·`BattleDeletionGate`·`DeletionSourceTrash`·`DpZeroDeletionHelpers`·`CardLeavePlayCleanup` → AS-IS 각 키워드 Process(`Decoy/Fragment/Barrier/ArmorPurge/Evade/Ascension/Fortitude/Partition/Retaliation…`)·`DestroyPermanentsClass` 미러로
- 키워드 상시 Process: `OverclockEffect`·`AllianceAttackBoost`·`RaidAttackSwitch`·`ProgressImmunity`·`EndOfTurnEffectAttack`·`LinkHelpers` → AS-IS 소재 파일 확인 후 그 파일로(다수는 CardEffectFactory/KeyWordEffects 및 전용 Process 클래스)

### R3. 트리거 창 — `AutoProcessing.cs` 완성 + `MultipleSkills`/`CutInProcess` 미러
- AutoProcessing 미러(1218줄)는 실질이나 `StackSkillInfos` 등 창 로직이 `WindowResolver(+Wiring 1650줄)`·`EffectScheduler`·`AutoProcessingTriggerCollector`에 있음 → AS-IS `AutoProcessing`/`MultipleSkills`/`CutInProcess` 파일로 이관
- **EffectRegistry·EffectBinding·PendingEffect 등 등록 모델 삭제** — AS-IS=매 패스 live 재열거(GetSkillInfos), 등록 없음
- `GameEventQueue`는 emit 전달 substrate로 잔류 가능(AS-IS Hashtable 창 구축에 공급)

### R4. 턴 흐름 — `TurnStateMachine.cs`(3373줄)
- 미러 57줄 껍데기 → AS-IS 상태머신(StartGame/각 Phase/EndTurn/EndGame) 이관, `GameFlowProcessor`(930줄)·`HeadlessEarlyPhase/MainPhase/EndTurnCleanupFlow` 해체(substrate 루프 구동부만 잔류)

### R5. Select*/상호작용 컴포넌트
- `SelectHandEffect`(942줄, 현재 7줄 스텁)·`SelectAttackEffect`·`SelectDigiXrosClass.Select`·`selectBurstDigivolution`/`selectAppFusion` — AS-IS 파일로 실구현(`RevealAndSelect` 등 Headless 잠복분 흡수)

### R6. 구모델 잔재 청산
- 카드: 순수 구모델 실카드 ~39(BT1 18·BT2 5·EX8 3·기타)·혼용 13·Tfx 27 → 신모델(레시피). **R1~R5 완료 후** (엔진 룰이 정확해진 뒤 — 사용자 지시 "엔진 먼저, 카드 bulk 무의미"에 따라 witness 필요분만 우선, 나머지는 카드 단계에서)
- `ActivatedEffect`·`ActivatedEffectResolver`·`IEffectBody` bodies·`LegacyBindingBridge`·`HashtableBridge` 잔재 삭제

### R7. 최종 검증
- 구조 계기판 전 지표 0/100% 확인 → **그 다음에야** 전체 스위트 + RuleAudit (행동 동일성 게이트)

## 4. Headless 잔류 화이트리스트 (이것만 남는다)

Services(존저장소·리포지토리·ZoneMover)·State·Choices(ChoiceProvider)·Coroutines(async 실행기)·DataLoading·Diagnostics·Bridge(EngineContext·AmbientMatchContext)·Runtime 중: Headless{Action*,GameLoop,Phase,TurnState,MemoryState,ChoiceState}·InMemory*Controller·LegalActionSetValidator·ActionMask·ObservationSnapshot·DcgoMatch·MatchConfig/Result·SessionContext·MulliganCoordinator(셋업 substrate)·GameEvent{,Queue,Type}·CardMovementPort류·SecurityFaceState(존 상태 플래그).
**기준**: "Unity/Photon/코루틴/저장소/RL-관측의 대체물인가?" 예→잔류, 아니오(게임 룰)→AS-IS 정위치로.

## 5. 실행 규율

- **단위=AS-IS 파일의 region** (파일 통짜 아님): 한 region의 AS-IS 로직 전체를 미러 getter/메서드로 이관 → consumer 재배선 → 대응 발명물 참조 제거. region 완료 = 계기판 갱신.
- **발명 금지 = STOP**: 이관 중 AS-IS에 없는 구조가 필요해지면(예: 코루틴 프레임 상태) substrate 화이트리스트로 해결 가능한지 확인, 아니면 STOP+design item. union/게이트/브릿지 신설 절대 금지.
- **substrate 번역만 허용**: IEnumerator→Task·StartCoroutine→await·GManager.instance→AmbientMatchContext·PermanentOfThisCard→Resolve…·UI/Photon strip. 로직 구조·순서·이름 불변.
- **순서 고정**: R1→R2→R3→R4→R5→R6→R7 (읽기층부터 — 모두가 의존). R2·R3은 부분 병행 가능하나 동일 파일 동시 수정 금지(단일스레드 원칙 — 이 세션 clobber 사고 2회).
- **보고 형식**: "R1 Permanent DP-region 완료 — 계기판: Headless 게임룰 x줄→y줄, 게이트 참조 n→m" (green 수 언급 금지).
- 커밋: 사용자 지시 시. 각 region 단위가 커밋 후보.

## 6. 규모 추정

이관 대상 ~25k줄(Headless 잠복) + 미러 상태층 신규 채움 ~10k줄(AS-IS 게임로직분). 선례: 골-1 AttackProcess 950줄 재하우징=1골. 단순 환산 R1~R5 ≈ 25~35 region-골. R6 카드 ~66파일. 세션 수는 배치 크기에 따름 — **속도보다 정확성**(사용자: "red 없애는 게 아니라 제대로 만드는 게 중요").

## 7. 이 설계가 이전 실패를 막는 지점

- 스코프에 상태/룰층 명시(오진 교정) · 이연 항목 전부 재개(R2~R5에 흡수) · "다리 신설=STOP" 규칙(union 재발 차단) · 진척=구조 계기판(green 은폐 차단) · 종점=발명물 0(하이브리드 잔존 차단)
