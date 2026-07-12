# AS-IS 미러 마이그레이션 설계 (2026-07-12)

## 0. 결정 및 배경

**2026-07-12 사용자 확정**: 마이그레이션이 M2 잔여작업보다 우선. 어떤 이유로도(witness 부재, 읽는 카드 미포팅, 결과 등가, sink 강제 등) AS-IS 로직의 단순화를 허가하지 않음 — 대부분의 카드 포팅을 로컬 LLM이 수행하므로, 로컬이 인지·수정할 수 없는 비-명시적 인프라 갭을 강모델이 남겨서는 안 됨.

**문제**: 최초 설계는 DCGO2 원본 `Assets/Scripts/Script/` 경로를 `src/HeadlessDCGO.Engine/Assets/Scripts/Script/`에 1:1 미러하는 것(분류 헤더 포함 스켈레톤 74개 사전 생성됨). 실제 진행은 코어 엔진을 `Headless/Runtime·Effects`(139파일)에 자유 재작성 — 미러 직하 74개 중 70개가 7줄 스켈레톤 방치(AttackProcess 628→7줄, CardController 5988→7, Permanent 4187→7). 자유 재작성이 만든 AS-IS 발산이 실증됨: OnAddDigivolutionCards emit 반전, OnBlockAnyone subject 반전, WhenLinked emit-before-trim, UntilEndAttack 만료 시점 오류, IsEndAttack/SwitchDefender 효과-표면 누락.

## 1. 아키텍처 경계 (사용자 정밀화)

- **미러 층 `Assets/Scripts/Script/` = 게임 로직.** AS-IS 파일과 같은 파일이 같은 역할 — 같은 클래스/메서드/제어흐름/상태. upstream DCGO2 코어 변경이 이 층에 기계적 diff-패치로 적용됨.
- **`Headless/` = substrate만.** 유니티 의존 공통기능의 대체물: 코루틴 런타임→async 실행기, GManager/ContinuousController→EngineContext·서비스, Photon→스텁, UI 선택창→ChoiceProvider, 존/상태 저장소, 이벤트 큐. **게임 규칙 로직 금지.**

판정 기준표:

| Headless 파일 | 판정 |
|---|---|
| EngineContext, GameEventQueue, ChoiceProvider/Choices, ZoneMover/저장소(CardRepository·CardInstanceRepository), EffectScheduler(실행기), TurnController(상태 저장), MemoryController, OnceFlagController, GameRandom | **substrate — 유지** |
| AttackPipeline(상태머신), BattleResolver/SecurityResolver(규칙 — 단 이들은 CardController IBattle/ISecurityCheck의 미러이므로 "미러의 임시 거처"), BlockTiming, DeletionReplacement*(삭제 규칙), DigivolutionStackHelpers/LinkHelpers(Permanent 로직), RaidAttackSwitch/AllianceAttackBoost/ProgressImmunity(키워드 효과의 오배치), EndAttackTriggerHook | **게임 로직 — 미러 층으로 이관 대상** |
| MatchStateMutationSink | 혼재 — mutation 적용=substrate, 규칙성 게이트=이관 |
| WindowResolver/WindowResolverWiring | MultipleSkills 미러(Stage5 완료) — 장기적으로 미러 층 MultipleSkills.cs로 이동, 당분간 유지 |
| GameFlowProcessor | TurnStateMachine 구동 루프의 미러 — TurnStateMachine.cs 이관 시 흡수 |

**미러 층에 이미 있는 것**: `CardEffectCommons/CardPortingFramework.cs`(13,292줄 — CanTrigger 게이트·Permanent/CardSource 뷰·프리미티브), `SelectPermanentEffect.cs`(293)·`SelectCardEffect.cs`(217)·`SelectAssemblyClass.cs`(169)·`PermanentEffectFactory.cs`(170), CardEffect/(카드 전체). 즉 상태층 **뷰**와 선택 로직은 이미 미러 층에 살고 있음 — 코어 상태기계/규칙 파일만 Headless에 오배치됨.

## 2. 인벤토리 요약 (상세는 조사 기록)

- 최상위 `Script/*.cs` 145파일 64,164줄. **코어 이관 총규모 ≈ 64k줄**: 최상위 코어 31.1k(GameState 11.5k + BattleLogic 8.4k + CoreRule 4.8k + CardEffect 프레임워크 6.0k) + 효과 하위 19.8k + 선택 로직 6.5k + 혼재 추출 6.5k.
- UI/연출 ~60파일·네트워킹·ProfanityFilter=스트립. 데이터로딩=Headless CardDatabase 등으로 이미 대응.
- 미러 스켈레톤에 분류 헤더 존재(`Decision: PORT / Category / Priority`) — 최초 설계의 분류 체계 활용.
- **의존성 순서(leaf→root)**: GameRandom·GameContext·CEntity_Base → 상태층(CardSource 4.4k·Permanent 4.2k·Player 1.7k) → 효과 프레임워크(ICardEffect·CardEffectCommons·CardEffectFactory·MultipleSkills) → AttackProcess → AutoProcessing → CardController → TurnStateMachine.
- 단 **AttackProcess는 먼저 착수 가능**: 필요한 상태 접근이 CardPortingFramework의 Permanent/CardSource 뷰(미러 층 기존)로 충족되고, callee(BlockTiming/BattleResolver/SecurityResolver)가 이미 해당 AS-IS 클래스의 미러.

## 3. substrate 번역 규칙집 (선례 기반 — 로컬 LLM 규칙집의 코어-파일 층)

카드층 규칙은 `porting_translation_cheatsheet.md`·`card_porting_standard.md` 기존 문서. 코어 파일 층:

| AS-IS 패턴 | 헤드리스 패턴 |
|---|---|
| `IEnumerator Foo()` | `async Task FooAsync(CancellationToken ct)` (네이티브 async — CoroutineAdapter 미사용) |
| `yield return ContinuousController.instance.StartCoroutine(Bar())` | `await BarAsync(ct).ConfigureAwait(false)` |
| `yield break` / `yield return null`(프레임 양보) | `return` / 삭제(선택 대기였다면 ChoiceRequest) |
| 유저 선택 대기(IsSelecting 폴링) | `ChoiceRequest`→`await context.ChoiceProvider.ChooseAsync` (Deferred면 예외-서스펜드 규약) |
| 선택-블로킹 상태기계 | phase enum + 단일-스텝 Advance + choiceRequested 파킹, 공용 루프 재진입 |
| `GManager.instance.turnStateMachine` | `context.TurnController` (+gameContext.TurnPlayer→Current.TurnPlayerId) |
| `GManager.instance.autoProcessing.StackSkillInfos(ht, timing)` | `TriggerEventEmitter.Emit(context.GameEventQueue, TriggerTimings.X, actor, subject, extraMetadata)` — payload는 Hashtable 내용 전부(**축소 금지**, no-simplification) |
| `TriggeredSkillProcess` | 직접 해소 금지 — 방출 후 파킹, 공용 루프(GameFlowProcessor.AutoProcessAsync→CollectUnifiedSeed→WindowResolver.DriveAsync)가 소유 |
| 배치 의미론(1 StackSkillInfos=N장) | batch-id 발급(EngineContext.Next*BatchId) + CardMoved 스탬프 |
| 즉시 상태변경(Destroy/Suspend/AddMemory) | MatchStateMutationSink mutation 스테이징→일괄 flush |
| `GetComponent<SelectXxxEffect>()` | `new SelectXxxEffect()`+SetUp (미러 층 클래스) |
| `Hashtable` 수신 게이트 | `CardEffectResolveContext` + `event.<key>` enrich, `GetXxxFromHashtable(ctx, card)` 동명 미러 |
| `Player`/`Permanent`/`CardSource` 참조 | `HeadlessPlayerId` 값 / CardPortingFramework의 Permanent·CardSource 뷰(context+id) |
| Unity null(파괴 체크) | `TryGetInstance(id, out rec) && rec is not null` |
| UI/연출/Photon(RPC·오디오·화살표·패널) | **무언 삭제**(UnityNullObjectPolicy; UnityEngine 문자열 자체가 테스트 게이트로 금지). AS-IS 게임로직 앵커만 file:line 주석 |
| 주석 "TODO" 리터럴 | 금지("design item RDx-NN" 사용 — lint-guard) |

## 3.5 범위 명시 — 하위 디렉토리 전체 포함 (2026-07-13 사용자 확인)

이관 범위는 `Script/` 최상위 145파일만이 아니라 **모든 하위 디렉토리의 파일 전부**:
- `CardEffectCommons/`(AS-IS 121: CanUseEffects 41·KeyWordEffects 29·GiveEffect·MinMax_DP_Cost_Level·직하) — 미러 실물 37/143
- `CardEffectFactory/`(59) — 실물 17 | `CardEffects/`(73) — 실물 11
- `MainPhaseAction/`(7)·`PlayerSelection/`(4)·`AutomaticOrder/`(1) — 전부 스켈레톤
- (Networking/·ProfanityFilter/·Hypertext/ = 스트립)

**구조 부채 — CardPortingFramework.cs 모놀리스**: 미러의 `CardEffectCommons/CardPortingFramework.cs`(13,292줄)는 AS-IS에 없는 단일 파일로, AS-IS가 121개 파일에 나눠 담은 로직(게이트·해시테이블·키워드·뷰)을 흡수했다. 파일 1:1 원칙 위반 — upstream이 `CanUseEffects/OnAttack.cs`를 고치면 대응 파일이 없음. **골 7.5 = CardPortingFramework 분해**: 내용을 AS-IS 파일 배치대로 각 미러 파일(CanUseEffects/*.cs·GetFromHashtable.cs·HashtableSetting.cs·KeyWordEffects/*.cs·CardSource/Permanent 뷰는 각각 CardSource.cs/Permanent.cs 미러로)로 재배치하고 CardPortingFramework는 삭제(또는 순수 re-export shim). partial class 분할이 아니라 **AS-IS 파일명·배치 그대로**.

## 4. 이관 골 목록 (순서)

**골 1 — AttackProcess.cs** (628줄, 이번 착수): §5 상세.
**골 2 — AutoProcessing.cs** (1,106): StackSkillInfos/TriggeredSkillProcess/AutoProcessCheck의 미러 본체화, WindowResolverWiring과 접속.
**골 3 — CardController.cs** (5,988, 분할 골): ISecurityCheck·IBattle·삭제 프로세스·존 이동·I*Class 프리미티브군 — SecurityResolver/BattleResolver/DeletionReplacement*/MatchStateMutationSink 규칙부 흡수. (M2 잔여 OnSecurityCheck가 여기서 AS-IS 그대로 해소.)
**골 4 — Permanent.cs** (4,187, 분할 골): EffectList/스택/링크/DigiXros — DigivolutionStackHelpers/LinkHelpers/Continuous*Gate 흡수.
**골 5 — CardSource.cs** (4,357, 분할 골) + Player.cs(1,675) + GameContext/CEntity_Base.
**골 6 — TurnStateMachine.cs** (3,373): GameFlowProcessor 흡수.
**골 7 — MultipleSkills.cs**(437)·CardEffectCommons.cs·CardEffectFactory.cs 본체화 + 잔여 Select* 채우기.
각 골 = AS-IS 1:1 확인 → 미러 본체 → shim 위임 → 기존 테스트 green + RuleAudit 0 + 적대 리뷰.

## 5. 골 1: AttackProcess.cs 상세 설계

### 5.1 미러 본체
`src/HeadlessDCGO.Engine/Assets/Scripts/Script/AttackProcess.cs`에 AS-IS 1:1 구조:
- `AttackState` enum 6값 그대로(None/Counter/Block/Battle/End/CleanUp — **Counter 명시 복원**).
- 인스턴스 필드 전부: AttackingPermanent/DefendingPermanent/HasDefender/AttackCount/IsAttacking/IsBlocking/SecurityDigimon/DoSecurityCheck/**IsEndAttack**/EffectHashtable(→선언 시점 메타)/**CounterEffectHashtable(선언 시점 cardSources 스냅샷)**.
- 메서드 1:1: `ActiveAttack()`, `ProcessNextState()`(switch), `Attack()`(재진입 가드·리셋·존재 게이트·suspend·beforeOnAttackCoroutine 훅·OnAttack/OnAllyAttack 방출·IsEndAttack 분기·else→End), `CounterTiming()`(2-pass + 경계 가드 3종: IsEndAttack·TopCard==null·!IsDigimon), `BlockTiming()`(SelectPermanentEffect 미러 경유), `DetermineAttackOutcome()`(직접공격 EndGame·배틀 IBattle·시큐리티 ISecurityCheck), `EndAttack()`(**public — 효과-대면 강제종료 표면**), `Cleanup()`(**UntilEndAttackEffects 리셋을 여기서** — 현재 위치 오류 교정), `SwitchDefender()`(**효과-대면 풀 시퀀스: 가드→재타게팅→블록 suspend+OnBlockAnyone→사망 시 IsBlocking 해제→OnAttackTargetChanged emit 중앙집중** — F1-ATC-EMIT-CENTRALIZE 해소, 카드 ~30+장 경로 개통).
- callee는 기존 미러 사용: BlockTiming(=AS-IS SelectPermanentEffect 블로커 선택부), BattleResolver(=IBattle), SecurityResolver(=ISecurityCheck), CardEffectCommons 게이트/해시테이블 빌더.
- UI(아웃라인·breakGlass·타겟화살표·로그) 무언 삭제.

### 5.2 상태 소유 + write-through
AttackProcess 인스턴스가 상태 소유(AS-IS와 동일). `IHeadlessAttackController`는 write-through 뷰로 유지(선언·블로커·페이즈 반영) — 관찰/RL 인코더·MetadataActionProcessor·LegalActionDispatcher 무변경. IsEndAttack/SecurityDigimon/카운터 스냅샷은 AttackProcess 전용 필드.

### 5.3 shim + 은퇴
`AttackPipeline.AdvanceAsync`(virtual 유지 — GPT-#3 override 의존)를 `attackProcess.ProcessNextState()` 1스텝 위임으로 교체, before/after로 `AttackAdvanceResult` 합성. park-phase(Blocking/DeletionReplacement/PiercingSecurity)는 AttackState의 서브-park로 매핑(코루틴 서스펜션의 headless 표현 — 매핑 함수 1개). 테스트 14개 프로젝트는 시그니처 유지로 대부분 무변경; AttackPipeline 내부 구현(카운터 플래그·Raid/Alliance 마커)을 단언하는 G3.5-W6/C3/C18은 단언 대상 이동.

**은퇴(로직 흡수 후)**: AttackPipeline(→shim→은퇴), AttackPhase(→AttackState+park 서브상태), RaidAttackSwitch/AllianceAttackBoost/ProgressImmunity(→CardEffectCommons Raid/Alliance/Progress 키워드 미러로 복귀), EndAttackTriggerHook(→EndAttack()의 방출+공용 루프), AttackDeclarationCommons(→Attack() 서두), Execute self-delete(→ExecuteProcess 미러).
**유지(callee)**: BlockTiming·BattleResolver·SecurityResolver·AttackTargetSwitchGate·InMemoryHeadlessAttackController(substrate 뷰)·GameFlowProcessor(골 6까지)·EffectDrivenAttack(Attack() 경유 재배선).

### 5.4 이 골이 해소하는 기존 갭
- **IsEndAttack 강제종료**(카드 4+장 attackProcess.EndAttack() 직접 호출 — 현재 포팅 불가) → public EndAttack()으로 개통.
- **SwitchDefender 효과-대면 래퍼**(리다이렉트 카드 ~30+장) + **OnAttackTargetChanged emit 중앙집중**(F1-ATC-EMIT-CENTRALIZE).
- **UntilEndAttack 만료 시점**(현재 OnEndAttack 해소 전 만료 → CleanUp으로 이동).
- **CounterEffectHashtable 스냅샷 의미론**.
- **카운터 경계 가드 3종**.
- M2 잔여 OnBlockAnyone(subject=attacker emit이 SwitchDefender 시퀀스 안에서 AS-IS 위치로)·OnCounterTiming 2-pass(CounterTiming() 인라인 — IsCounterEffect 필터를 AS-IS filter 인자 그대로).
- F1-ENDATTACK-LIVENESS(게이트 liveness)·RD9-87(suspend sink화)은 골 내 상환 후보.

### 5.5 검증
기존 전체 스위트(420+) green + RuleAudit 0 + 이번 세션 F1-Tier2 스위트들 green + 신규: IsEndAttack 강제종료 witness(BT25_103류 1장 포팅) + SwitchDefender 리다이렉트 witness(BT9_044류 1장) + UntilEndAttack-만료×OnEndAttack 상호작용 테스트 + 적대 리뷰(AS-IS diff 렌즈: 미러 파일과 AS-IS 파일의 구조 비교가 이제 직접 가능).

## 6. 리스크

- 미러 본체와 기존 Headless 로직의 이중 기간 — shim으로 단일 경로 보장(파이프라인 내부 로직은 위임 즉시 삭제, 두 경로 공존 금지).
- 소스-스캔 테스트가 Headless 경로/문구를 단언(G3.5-005:154-169 등) — 아키텍처 위치 단언은 마이그레이션의 일부로 갱신(행동 단언은 불변).
- Raid/Alliance 키워드 복귀는 파이프라인 하드코딩 제거와 동시 — 골 1 범위에 포함하되 별 커밋 단위.
