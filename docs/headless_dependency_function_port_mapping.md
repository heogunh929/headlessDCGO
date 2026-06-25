# Headless Dependency Function Port Mapping

## Purpose

This document defines the function-level mapping between Unity/DCGO dependency calls and the current HeadlessDCGO.Engine porting APIs.

It is a Phase 1 mapping document. It does not start Phase 2 gameplay/card-effect porting. It records where future porting work should land when an AS-IS dependency function is encountered.

## Source Documents

- `docs/headless_complete_dependency_replacement.csv`
- `docs/headless_source_origin_mapping.csv`
- `docs/dotnet_non_unity_dependency_replacement_plan.csv`
- `docs/headless_unity_dependent_functions.csv`
- `src/HeadlessDCGO.Engine/Headless`

## Mapping Status

| Status | Meaning |
|---|---|
| PORTED | A concrete Headless function/API exists and is the current target. |
| CONTRACT | A Headless contract exists, but later Phase work must fill full gameplay semantics. |
| BRIDGE | Temporary migration bridge; prefer direct Headless services in new code. |
| EXCLUDED | Visual/client-only dependency. It must not enter Headless runtime. |
| FUTURE | Known dependency family, but final implementation belongs to later Phase work. |

## Runtime And Lifecycle Mapping

| Existing dependency function/name | Existing location | Current porting function/name | Current location | Status | Notes |
|---|---|---|---|---|---|
| `GManager.Init` | `DCGO/Assets/Scripts/Script/GManager.cs` | `DcgoMatch.InitializeAsync(MatchConfig, CancellationToken)` | `src/HeadlessDCGO.Engine/Headless/Runtime/DcgoMatch.cs:39` | PORTED | Top-level match initialization entry point. |
| `TurnStateMachine.Init` | `DCGO/Assets/Scripts/Script/TurnStateMachine.cs` | `IHeadlessTurnController.Initialize(...)` / `InMemoryHeadlessTurnController.Initialize(...)` | `src/HeadlessDCGO.Engine/Headless/Runtime/IHeadlessTurnController.cs:10`, `src/HeadlessDCGO.Engine/Headless/Runtime/InMemoryHeadlessTurnController.cs:12` | CONTRACT | Initializes turn order and phase state without Unity lifecycle. |
| `GameStateMachine` loop | `DCGO/Assets/Scripts/Script/TurnStateMachine.cs` | `DcgoMatch.StepAsync(...)` | `src/HeadlessDCGO.Engine/Headless/Runtime/DcgoMatch.cs:103` | PORTED | Public one-step match progression API. |
| `Update` / frame loop | Unity `MonoBehaviour` methods | `HeadlessGameLoop.StepAsync(...)` | `src/HeadlessDCGO.Engine/Headless/Runtime/HeadlessGameLoop.cs:39` | PORTED | Explicit deterministic step replaces frame-driven update. |
| `Reset` / match reset callbacks | Unity `MonoBehaviour` and game scripts | `DcgoMatch.ResetAsync(...)` | `src/HeadlessDCGO.Engine/Headless/Runtime/DcgoMatch.cs:81` | PORTED | Resets match-scoped services and current state. |
| match-scoped service reset | `GameContext`, `Player`, `TurnStateMachine` reset flows | `EngineContext.ResetMatchState()` | `src/HeadlessDCGO.Engine/Headless/Bridge/EngineContext.cs:174` | PORTED | Calls resettable Headless services. |
| `EndGame`, `SetLose`, `Surrender` | `TurnStateMachine.cs`, `Player.cs` | `DcgoMatch.GetResult()` / `MatchResult` | `src/HeadlessDCGO.Engine/Headless/Runtime/DcgoMatch.cs:264`, `src/HeadlessDCGO.Engine/Headless/Runtime/MatchResult.cs` | CONTRACT | Result model is fixed; full win/loss rules expand later. |
| terminal state query | `TurnStateMachine.endGame` style checks | `DcgoMatch.IsTerminal()` / `IRuleQueryService.IsTerminal()` | `src/HeadlessDCGO.Engine/Headless/Runtime/DcgoMatch.cs:252`, `src/HeadlessDCGO.Engine/Headless/Services/IRuleQueryService.cs:10` | PORTED | Terminal query is service-based. |

## Singleton And Component Access Mapping

| Existing dependency function/name | Existing location | Current porting function/name | Current location | Status | Notes |
|---|---|---|---|---|---|
| `GManager.instance` | `DCGO/Assets/Scripts/Script/GManager.cs` | `EngineContext` | `src/HeadlessDCGO.Engine/Headless/Bridge/EngineContext.cs:15` | PORTED | Root injected dependency container. |
| `GManager.instance.turnStateMachine` | `GManager.cs`, `TurnStateMachine.cs` | `GManagerBridge.GetTurnStateMachine()` | `src/HeadlessDCGO.Engine/Headless/Bridge/GManagerBridge.cs:30` | BRIDGE | Temporary compatibility accessor. Prefer `EngineContext.TurnController`. |
| `GManager.instance.autoProcessing` | `GManager.cs`, `AutoProcessing.cs` | `GManagerBridge.GetAutoProcessing()` / `EngineContext.EffectScheduler` | `src/HeadlessDCGO.Engine/Headless/Bridge/GManagerBridge.cs:35`, `src/HeadlessDCGO.Engine/Headless/Bridge/EngineContext.cs:75` | BRIDGE | Maps auto-processing to effect scheduler contract. |
| `GManager.instance.attackProcess` | `GManager.cs`, `AttackProcess.cs` | `GManagerBridge.GetAttackProcess()` / `EngineContext.AttackController` | `src/HeadlessDCGO.Engine/Headless/Bridge/GManagerBridge.cs:40`, `src/HeadlessDCGO.Engine/Headless/Bridge/EngineContext.cs:67` | BRIDGE | Attack rules remain later Phase work; state contract exists. |
| `GetComponent<T>()` | Unity scene object scripts | `EngineContext.GetService<TService>()` | `src/HeadlessDCGO.Engine/Headless/Bridge/EngineContext.cs:107` | PORTED | Service lookup replaces scene component lookup. |
| dynamic service registration | Unity scene object composition | `EngineContext.RegisterService<TService>(...)` | `src/HeadlessDCGO.Engine/Headless/Bridge/EngineContext.cs:148` | PORTED | Allows explicit test/runner injection. |
| missing service probe | `GetComponent<T>() != null` style checks | `EngineContext.TryGetService<TService>(out ...)` | `src/HeadlessDCGO.Engine/Headless/Bridge/EngineContext.cs:128` | PORTED | No Unity null semantics. |
| `ContinuousController.instance` | `DCGO/Assets/Scripts/Script/ContinuousController.cs` | `ContinuousContext.Create(...)` / `ContinuousContext.FromMatchConfig(...)` | `src/HeadlessDCGO.Engine/Headless/Bridge/ContinuousContext.cs:98`, `src/HeadlessDCGO.Engine/Headless/Bridge/ContinuousContext.cs:127` | PORTED | Global runtime flags become immutable/context data. |
| `PlayerPrefs.Get/Set` for gameplay-affecting options | Unity preference usage | `MatchConfig` / `ContinuousContext.ToMatchConfig()` | `src/HeadlessDCGO.Engine/Headless/Bridge/ContinuousContext.cs:148` | CONTRACT | Hidden preferences must become explicit config. |

## Coroutine And Time Mapping

| Existing dependency function/name | Existing location | Current porting function/name | Current location | Status | Notes |
|---|---|---|---|---|---|
| `StartCoroutine(IEnumerator)` | `GManager.cs`, `ContinuousController.cs`, `TurnStateMachine.cs`, `CardObjectController.cs` | `EngineTaskRunner.Enqueue(IEngineTask)` | `src/HeadlessDCGO.Engine/Headless/Coroutines/EngineTaskRunner.cs:9` | PORTED | Queue task for deterministic execution. |
| immediate coroutine run | coroutine-heavy AS-IS flows | `EngineTaskRunner.RunAsync(IEngineTask, CancellationToken)` | `src/HeadlessDCGO.Engine/Headless/Coroutines/EngineTaskRunner.cs:15` | PORTED | Run one task through the runner. |
| one frame coroutine tick | Unity frame scheduler | `EngineTaskRunner.StepAsync(...)` | `src/HeadlessDCGO.Engine/Headless/Coroutines/EngineTaskRunner.cs:21` | PORTED | Explicit scheduler step. |
| run until coroutine queue drains | nested coroutine chains | `EngineTaskRunner.RunUntilIdleAsync(...)` | `src/HeadlessDCGO.Engine/Headless/Coroutines/EngineTaskRunner.cs:54` | PORTED | Deterministic equivalent for queue drain. |
| `StopAllCoroutines` / clear pending flows | coroutine owner cleanup | `EngineTaskRunner.Clear()` | `src/HeadlessDCGO.Engine/Headless/Coroutines/EngineTaskRunner.cs:69` | PORTED | Clears pending engine tasks. |
| `IEnumerator` effect/action methods | `CardController.cs`, `AutoProcessing.cs`, `AttackProcess.cs`, `TurnStateMachine.cs` | `CoroutineAdapter.FromEnumerator(IEnumerator)` | `src/HeadlessDCGO.Engine/Headless/Coroutines/CoroutineAdapter.cs:7` | BRIDGE | Temporary adapter while IEnumerator logic is converted. |
| `WaitForSeconds` | Unity coroutine waits | `EngineWaitCondition.Seconds(double)` / `Seconds(TimeSpan)` | `src/HeadlessDCGO.Engine/Headless/Coroutines/EngineWaitCondition.cs:25`, `src/HeadlessDCGO.Engine/Headless/Coroutines/EngineWaitCondition.cs:43` | PORTED | Time is explicit condition, not Unity wall-clock frame wait. |
| `WaitUntil` / `WaitWhile` | Unity coroutine waits | `EngineWaitCondition.Until(Func<bool>)` | `src/HeadlessDCGO.Engine/Headless/Coroutines/EngineWaitCondition.cs:56` | PORTED | Predicate waits are explicit and deterministic. |
| `Time.deltaTime`, `Time.time` gameplay sequencing | Unity frame-time use | `DcgoMatch.StepAsync(...)` / `HeadlessGameLoop.StepAsync(...)` | `src/HeadlessDCGO.Engine/Headless/Runtime/DcgoMatch.cs:103`, `src/HeadlessDCGO.Engine/Headless/Runtime/HeadlessGameLoop.cs:39` | CONTRACT | Gameplay progression is action/step based. |

## Action, Turn, Attack, Memory Mapping

| Existing dependency function/name | Existing location | Current porting function/name | Current location | Status | Notes |
|---|---|---|---|---|---|
| `QueueMainPhaseAction` | `TurnStateMachine.cs`, `Player.cs` | `HeadlessActionQueue.Enqueue(LegalAction)` | `src/HeadlessDCGO.Engine/Headless/Runtime/HeadlessActionQueue.cs:15` | PORTED | Queue legal action for later processing. |
| `DequeueMainPhaseAction` | `TurnStateMachine.cs`, `Player.cs` | `HeadlessActionQueue.TryDequeue(out LegalAction?)` | `src/HeadlessDCGO.Engine/Headless/Runtime/HeadlessActionQueue.cs:52` | PORTED | Dequeue next action. |
| main phase action construction | `TurnStateMachine.cs`, `Player.cs` | `HeadlessActionFactory.Create(...)` | `src/HeadlessDCGO.Engine/Headless/Runtime/HeadlessActionFactory.cs:9` | PORTED | Common LegalAction creation entry point. |
| pass/no-op action | main phase command placeholder | `HeadlessActionFactory.NoOp(...)`, `Pass(...)` | `src/HeadlessDCGO.Engine/Headless/Runtime/HeadlessActionFactory.cs:24`, `src/HeadlessDCGO.Engine/Headless/Runtime/HeadlessActionFactory.cs:29` | PORTED | Test and infrastructure actions. |
| terminal action/result payload | `EndGame`, `SetLose`, surrender flows | `HeadlessActionFactory.SetTerminalResult(...)` | `src/HeadlessDCGO.Engine/Headless/Runtime/HeadlessActionFactory.cs:39` | CONTRACT | Used by metadata processor and tests. |
| main phase action execution | `SetPlayCard`, `SetActSkill`, `SetAttackingPermaent`, `MainPhaseAction` | `IActionProcessor.ProcessAsync(...)` / `MetadataActionProcessor.ProcessAsync(...)` | `src/HeadlessDCGO.Engine/Headless/Runtime/IActionProcessor.cs`, `src/HeadlessDCGO.Engine/Headless/Runtime/MetadataActionProcessor.cs:10` | CONTRACT | Concrete gameplay handlers are later Phase work. |
| legal action query | `CanHatch`, `CanMove`, `CanPayCost`, main phase command availability | `IRuleQueryService.GetLegalActions(...)` | `src/HeadlessDCGO.Engine/Headless/Services/IRuleQueryService.cs:6` | CONTRACT | Rule availability surface is fixed. |
| add/remove legal action in tests/porting | action availability setup | `IHeadlessLegalActionController.AddLegalActions(...)` / `RemoveLegalAction(...)` / `ClearLegalActions()` | `src/HeadlessDCGO.Engine/Headless/Services/IHeadlessLegalActionController.cs:6`, `src/HeadlessDCGO.Engine/Headless/Services/IHeadlessLegalActionController.cs:8`, `src/HeadlessDCGO.Engine/Headless/Services/IHeadlessLegalActionController.cs:10` | PORTED | Deterministic legal-action seed/control surface. |
| phase advance | `SetMainPhase`, `EndPhase`, phase branch changes | `InMemoryHeadlessTurnController.AdvancePhase()` | `src/HeadlessDCGO.Engine/Headless/Runtime/InMemoryHeadlessTurnController.cs:34` | CONTRACT | Phase sequence contract exists. |
| end turn | `EndTurn` style flow | `InMemoryHeadlessTurnController.EndTurn()` | `src/HeadlessDCGO.Engine/Headless/Runtime/InMemoryHeadlessTurnController.cs:45` | CONTRACT | Turn rotation contract exists. |
| direct phase set | Unity state machine phase assignment | `InMemoryHeadlessTurnController.SetPhase(HeadlessPhase)` | `src/HeadlessDCGO.Engine/Headless/Runtime/InMemoryHeadlessTurnController.cs:61` | CONTRACT | For deterministic setup and transition tests. |
| attack declaration | `AttackProcess.cs`, attack command | `HeadlessActionFactory.DeclareAttack(...)` / `InMemoryHeadlessAttackController.DeclareAttack(...)` | `src/HeadlessDCGO.Engine/Headless/Runtime/HeadlessActionFactory.cs:197`, `src/HeadlessDCGO.Engine/Headless/Runtime/InMemoryHeadlessAttackController.cs:12` | CONTRACT | Full battle/security rules later. |
| attack resolution | `AttackProcess.cs` | `HeadlessActionFactory.ResolveAttack(...)` / `InMemoryHeadlessAttackController.ResolveAttack(...)` | `src/HeadlessDCGO.Engine/Headless/Runtime/HeadlessActionFactory.cs:216`, `src/HeadlessDCGO.Engine/Headless/Runtime/InMemoryHeadlessAttackController.cs:34` | CONTRACT | Attack result contract exists. |
| memory set/add/pay | memory gauge mutation in AS-IS runtime | `InMemoryHeadlessMemoryController.Set(...)`, `Add(...)`, `Pay(...)` | `src/HeadlessDCGO.Engine/Headless/Runtime/InMemoryHeadlessMemoryController.cs:26`, `src/HeadlessDCGO.Engine/Headless/Runtime/InMemoryHeadlessMemoryController.cs:32`, `src/HeadlessDCGO.Engine/Headless/Runtime/InMemoryHeadlessMemoryController.cs:42` | PORTED | Memory mutation is service controlled. |

## Choice And UI Selection Mapping

| Existing dependency function/name | Existing location | Current porting function/name | Current location | Status | Notes |
|---|---|---|---|---|---|
| `SelectCardEffect.SetUp(...)` and related setup fields | `DCGO/Assets/Scripts/Script/SelectCardEffect.cs` | `ChoiceRequest(...)` | `src/HeadlessDCGO.Engine/Headless/Choices/ChoiceRequest.cs:7` | PORTED | Captures type, player, min/max, skip, zone, candidates. |
| `SelectPermanentEffect.SetUp(...)` | `SelectPermanentEffect.cs` | `ChoiceRequest(...)` / `ChoiceCandidate(...)` | `src/HeadlessDCGO.Engine/Headless/Choices/ChoiceRequest.cs:7`, `src/HeadlessDCGO.Engine/Headless/Choices/ChoiceCandidate.cs:7` | PORTED | Permanent candidates become stable IDs. |
| `SelectCountEffect` selected count | `SelectCountEffect.cs` | `ChoiceResult.SelectCount(int)` | `src/HeadlessDCGO.Engine/Headless/Choices/ChoiceResult.cs:57` | PORTED | Count selection result. |
| `_targetCards`, `_targetPermanents` | `SelectCardEffect.cs`, `SelectPermanentEffect.cs` | `ChoiceResult.Select(...)` | `src/HeadlessDCGO.Engine/Headless/Choices/ChoiceResult.cs:46`, `src/HeadlessDCGO.Engine/Headless/Choices/ChoiceResult.cs:51` | PORTED | Selected target IDs. |
| no-selection / cancel | `canNoSelect`, skip button | `ChoiceResult.Skip()` | `src/HeadlessDCGO.Engine/Headless/Choices/ChoiceResult.cs:41` | PORTED | Explicit skip result. |
| choice validation | UI-side selectable checks | `ChoiceResult.Validate(ChoiceRequest)` / `ThrowIfInvalid(...)` | `src/HeadlessDCGO.Engine/Headless/Choices/ChoiceResult.cs:62`, `src/HeadlessDCGO.Engine/Headless/Choices/ChoiceResult.cs:88` | PORTED | Enforces selected IDs/count against request. |
| `Activate()`, `WaitForEndSelect()`, `OnClick`, `EndSelect_RPC` | `Select*Effect.cs`, `UserSelectionManager.cs` | `IChoiceProvider.ChooseAsync(...)` | `src/HeadlessDCGO.Engine/Headless/Choices/IChoiceProvider.cs` | PORTED | UI wait/click/RPC becomes deterministic provider call. |
| queued scripted selection | `QueuePlayerSelection`, `DequeuePlayerSelection` | `ScriptedChoiceProvider.Enqueue(...)` / `ChooseAsync(...)` | `src/HeadlessDCGO.Engine/Headless/Choices/ScriptedChoiceProvider.cs:25`, `src/HeadlessDCGO.Engine/Headless/Choices/ScriptedChoiceProvider.cs:36` | PORTED | Test/replay provider. |
| AI/policy choice | `IsAI`, AI action queue | `PolicyChoiceProvider.ChooseAsync(...)` | `src/HeadlessDCGO.Engine/Headless/Choices/PolicyChoiceProvider.cs:12` | CONTRACT | Policy hook exists; final AI/RL adapter can plug in. |
| choice pause | UI modal blocks gameplay | `InMemoryHeadlessChoiceController.RequestChoice(...)` | `src/HeadlessDCGO.Engine/Headless/Runtime/InMemoryHeadlessChoiceController.cs:12` | PORTED | Runtime pending choice state. |
| choice resume | selected result returns to effect/action | `InMemoryHeadlessChoiceController.ResolveChoice(...)` | `src/HeadlessDCGO.Engine/Headless/Runtime/InMemoryHeadlessChoiceController.cs:43` | PORTED | Runtime choice resolution state. |
| clear pending choice | UI selection closed/reset | `InMemoryHeadlessChoiceController.ClearChoice()` | `src/HeadlessDCGO.Engine/Headless/Runtime/InMemoryHeadlessChoiceController.cs:70` | PORTED | Explicit pending choice cleanup. |

## Zone, Card State, And GameObject Mapping

| Existing dependency function/name | Existing location | Current porting function/name | Current location | Status | Notes |
|---|---|---|---|---|---|
| `GameObject`, `Transform`, `Image`, `SetActive` visual-only calls | Unity object scripts | `UnityNullObjectPolicy.Evaluate(...)` / `ShouldExclude(...)` | `src/HeadlessDCGO.Engine/Headless/Bridge/UnityNullObjectPolicy.cs:7`, `src/HeadlessDCGO.Engine/Headless/Bridge/UnityNullObjectPolicy.cs:43` | EXCLUDED | Visual-only objects are no-op/excluded unless gameplay service replacement is required. |
| `SetParent`, sibling index/order | card object hierarchy | `ZoneState.InsertTop(...)`, `InsertBottom(...)`, `InsertAt(...)` | `src/HeadlessDCGO.Engine/Headless/State/ZoneState.cs:101`, `src/HeadlessDCGO.Engine/Headless/State/ZoneState.cs:108`, `src/HeadlessDCGO.Engine/Headless/State/ZoneState.cs:115` | PORTED | Ordered zone collection replaces transform hierarchy. |
| remove card object from area | `CardObjectController`, `Player` zones | `ZoneState.Remove(...)` | `src/HeadlessDCGO.Engine/Headless/State/ZoneState.cs:130` | PORTED | Pure data mutation. |
| move card between zones | `RemoveFromAllArea`, `MovePermanent`, zone object moves | `IZoneMover.MoveAsync(...)` / `InMemoryZoneMover.MoveAsync(...)` | `src/HeadlessDCGO.Engine/Headless/Services/IZoneMover.cs:9`, `src/HeadlessDCGO.Engine/Headless/Services/InMemoryZoneMover.cs:53` | PORTED | Main zone movement API. |
| `AddHandCard` | `Player.cs`, `CardObjectController.cs` | `InMemoryZoneMover.AddToHandAsync(...)` | `src/HeadlessDCGO.Engine/Headless/Services/InMemoryZoneMover.cs:61` | PORTED | Adds card to hand zone. |
| `AddTrashCard` | `Player.cs`, `CardObjectController.cs` | `InMemoryZoneMover.AddToTrashAsync(...)` | `src/HeadlessDCGO.Engine/Headless/Services/InMemoryZoneMover.cs:69` | PORTED | Adds card to trash zone. |
| `AddSecurityCard` | `Player.cs`, security flows | `InMemoryZoneMover.AddToSecurityAsync(...)` | `src/HeadlessDCGO.Engine/Headless/Services/InMemoryZoneMover.cs:77` | PORTED | Adds card to security zone. |
| draw from deck/library | `Draw`, library top moves | `InMemoryZoneMover.DrawAsync(...)` | `src/HeadlessDCGO.Engine/Headless/Services/InMemoryZoneMover.cs:101` | PORTED | Draws deterministic top cards. |
| add security from library | security setup/recovery flows | `InMemoryZoneMover.AddSecurityFromLibraryAsync(...)` | `src/HeadlessDCGO.Engine/Headless/Services/InMemoryZoneMover.cs:117` | PORTED | Moves from library to security. |
| trash security | security check/trash flows | `InMemoryZoneMover.TrashSecurityAsync(...)` | `src/HeadlessDCGO.Engine/Headless/Services/InMemoryZoneMover.cs:134` | PORTED | Moves security cards to trash. |
| hatch digitama | breeding area setup | `InMemoryZoneMover.HatchDigitamaAsync(...)` | `src/HeadlessDCGO.Engine/Headless/Services/InMemoryZoneMover.cs:163` | PORTED | Moves digitama to breeding. |
| move breeding to battle | raising phase movement | `InMemoryZoneMover.MoveBreedingToBattleAsync(...)` | `src/HeadlessDCGO.Engine/Headless/Services/InMemoryZoneMover.cs:181` | PORTED | Moves breeding card(s) to battle. |
| shuffle deck/library | `Shuffle`, `GameRandom`, deck randomization | `InMemoryZoneMover.ShuffleAsync(...)` / `GameRandomSource.Shuffle<T>(...)` | `src/HeadlessDCGO.Engine/Headless/Services/InMemoryZoneMover.cs:201`, `src/HeadlessDCGO.Engine/Headless/Services/GameRandomSource.cs:60` | PORTED | Seeded shuffle. |
| suspended/revealed/card flags | `CardController`, `CardObjectController`, `Permanent` state | `CardInstanceState.Suspend()`, `Unsuspend()`, `Reveal()`, `Hide()`, `SetFlag(...)` | `src/HeadlessDCGO.Engine/Headless/State/CardInstanceState.cs:75`, `src/HeadlessDCGO.Engine/Headless/State/CardInstanceState.cs:80`, `src/HeadlessDCGO.Engine/Headless/State/CardInstanceState.cs:85`, `src/HeadlessDCGO.Engine/Headless/State/CardInstanceState.cs:90`, `src/HeadlessDCGO.Engine/Headless/State/CardInstanceState.cs:153` | CONTRACT | Data state exists; full rule semantics later. |

## Data Loading Mapping

| Existing dependency function/name | Existing location | Current porting function/name | Current location | Status | Notes |
|---|---|---|---|---|---|
| `Resources.Load`, `Resources.LoadAll` for gameplay card data | Unity Resources usage | `CardAssetJsonLoader.LoadFile(...)` / `LoadDirectory(...)` | `src/HeadlessDCGO.Engine/Headless/DataLoading/CardAssetJsonLoader.cs:65`, `src/HeadlessDCGO.Engine/Headless/DataLoading/CardAssetJsonLoader.cs:106` | PORTED | Loads converted card JSON without Unity runtime. |
| async file load | future runner/data pipeline | `CardAssetJsonLoader.LoadFileAsync(...)` | `src/HeadlessDCGO.Engine/Headless/DataLoading/CardAssetJsonLoader.cs:84` | PORTED | Async loader contract. |
| load card directory into DB | Unity asset database replacement | `CardAssetJsonLoader.LoadDirectoryInto(...)` | `src/HeadlessDCGO.Engine/Headless/DataLoading/CardAssetJsonLoader.cs:127` | PORTED | Populates `CardDatabase`. |
| `CEntity_Base` / CardBaseEntity definition lookup | `DCGO/Assets/CardBaseEntity`, `CardObjectController.cs` | `CardDatabase.Upsert(...)`, `TryGetCard(...)`, `GetCard(...)`, `Query(...)` | `src/HeadlessDCGO.Engine/Headless/DataLoading/CardDatabase.cs:11`, `src/HeadlessDCGO.Engine/Headless/DataLoading/CardDatabase.cs:26`, `src/HeadlessDCGO.Engine/Headless/DataLoading/CardDatabase.cs:31`, `src/HeadlessDCGO.Engine/Headless/DataLoading/CardDatabase.cs:36` | PORTED | Queryable card definition source. |
| deck code/file load | `ContinuousController`, deck UI/load helpers | `DeckListLoader.LoadFile(...)`, `ParseCode(...)`, `LoadLines(...)` | `src/HeadlessDCGO.Engine/Headless/DataLoading/DeckListLoader.cs:7`, `src/HeadlessDCGO.Engine/Headless/DataLoading/DeckListLoader.cs:38`, `src/HeadlessDCGO.Engine/Headless/DataLoading/DeckListLoader.cs:44` | PORTED | Unity-free deck list loading. |
| banlist option/data load | `ContinuousController`, gameplay options | `BanlistLoader.LoadFile(...)`, `ParseCode(...)`, `LoadLines(...)` | `src/HeadlessDCGO.Engine/Headless/DataLoading/BanlistLoader.cs:7`, `src/HeadlessDCGO.Engine/Headless/DataLoading/BanlistLoader.cs:38`, `src/HeadlessDCGO.Engine/Headless/DataLoading/BanlistLoader.cs:44` | PORTED | Unity-free banlist loading. |
| deck legality checks | UI/deck validation flows | `DeckValidator.Validate(...)` | `src/HeadlessDCGO.Engine/Headless/DataLoading/DeckValidator.cs:10` | CONTRACT | Validation exists; game-format rules can expand later. |

## Effect, Timing, And Hashtable Mapping

| Existing dependency function/name | Existing location | Current porting function/name | Current location | Status | Notes |
|---|---|---|---|---|---|
| `Hashtable`, `GetFromHashtable`, `HashtableSetting` effect payloads | `CardEffectCommons.cs`, effect scripts | `EffectContext(...)` | `src/HeadlessDCGO.Engine/Headless/Effects/EffectContext.cs:8`, `src/HeadlessDCGO.Engine/Headless/Effects/EffectContext.cs:22` | PORTED | Typed context plus metadata dictionary. |
| `SkillInfo`, effect activation request | `AutoProcessing.cs`, `MultipleSkills.cs`, `CardEffectFactory.cs` | `EffectRequest(...)` | `src/HeadlessDCGO.Engine/Headless/Effects/EffectRequest.cs:7` | PORTED | One queued effect activation request. |
| `StackedSkillInfos`, `PutStackedSkill`, queued skills | `AutoProcessing.cs`, `MultipleSkills.cs` | `EffectResolutionQueue.Enqueue(...)`, `TryPeek(...)`, `TryDequeue(...)` | `src/HeadlessDCGO.Engine/Headless/Effects/EffectResolutionQueue.cs:9`, `src/HeadlessDCGO.Engine/Headless/Effects/EffectResolutionQueue.cs:15`, `src/HeadlessDCGO.Engine/Headless/Effects/EffectResolutionQueue.cs:27` | PORTED | Deterministic queue contract. |
| `AutoProcessCheck`, `RuleProcess`, `TriggeredSkillProcess`, `ActivateBackgroundEffects` | `AutoProcessing.cs` | `EffectScheduler.Enqueue(...)`, `ResolveNextAsync(...)`, `ResolveAllAsync(...)` | `src/HeadlessDCGO.Engine/Headless/Effects/EffectScheduler.cs:31`, `src/HeadlessDCGO.Engine/Headless/Effects/EffectScheduler.cs:53`, `src/HeadlessDCGO.Engine/Headless/Effects/EffectScheduler.cs:104` | CONTRACT | Queue and scheduling contract exists; real effect bodies later. |
| effect lookup by timing | `AutoProcessing.cs`, effect factory/discovery | `IEffectQueryService.GetEffectsForTiming(...)` / `InMemoryEffectQueryService.GetEffectsForTiming(...)` | `src/HeadlessDCGO.Engine/Headless/Services/IEffectQueryService.cs`, `src/HeadlessDCGO.Engine/Headless/Services/InMemoryEffectQueryService.cs:15` | PORTED | Timing-based effect query surface. |
| continuous effect query | `ContinuousController`, permanent effect lists | `InMemoryEffectQueryService.GetContinuousEffects(...)` | `src/HeadlessDCGO.Engine/Headless/Services/InMemoryEffectQueryService.cs:25` | PORTED | Role-specific query. |
| replacement effect query | replacement effects | `InMemoryEffectQueryService.GetReplacementEffects(...)` | `src/HeadlessDCGO.Engine/Headless/Services/InMemoryEffectQueryService.cs:31` | PORTED | Role-specific query. |
| modifier/restriction query | modifier/restriction effects | `InMemoryEffectQueryService.GetModifierEffects(...)`, `GetRestrictionEffects(...)` | `src/HeadlessDCGO.Engine/Headless/Services/InMemoryEffectQueryService.cs:37`, `src/HeadlessDCGO.Engine/Headless/Services/InMemoryEffectQueryService.cs:43` | PORTED | Role-specific query. |
| effect registry/factory lookup | `CardEffectFactory.cs`, `PermanentEffects`, `EffectList` | `EffectRegistry.Register(...)`, `GetEffects(...)`, `Find(...)` | `src/HeadlessDCGO.Engine/Headless/Effects/EffectRegistry.cs:24`, `src/HeadlessDCGO.Engine/Headless/Effects/EffectRegistry.cs:38`, `src/HeadlessDCGO.Engine/Headless/Effects/EffectRegistry.cs:103` | CONTRACT | Registry contract exists; actual card effect binding later. |
| timing window collection | trigger timing checks | `DefaultTimingWindowResolver.CollectTriggers(...)`, `OpenWindow(...)` | `src/HeadlessDCGO.Engine/Headless/Rules/TimingWindowResolver.cs:24`, `src/HeadlessDCGO.Engine/Headless/Rules/TimingWindowResolver.cs:57` | CONTRACT | Timing window contract exists. |

## Random, Logging, Diagnostics Mapping

| Existing dependency function/name | Existing location | Current porting function/name | Current location | Status | Notes |
|---|---|---|---|---|---|
| `UnityEngine.Random`, `GameRandom`, `IsSucceedProbability` | `GameRandom.cs`, `ContinuousController.cs` | `IRandomSource` / `GameRandomSource.NextInt(...)`, `NextDouble()`, `Shuffle<T>(...)` | `src/HeadlessDCGO.Engine/Headless/Services/IRandomSource.cs`, `src/HeadlessDCGO.Engine/Headless/Services/GameRandomSource.cs:36`, `src/HeadlessDCGO.Engine/Headless/Services/GameRandomSource.cs:55`, `src/HeadlessDCGO.Engine/Headless/Services/GameRandomSource.cs:60` | PORTED | Seeded deterministic random source. |
| random seed reset | `ContinuousController.SetRandom` style state | `GameRandomSource.ResetSeed(int)` | `src/HeadlessDCGO.Engine/Headless/Services/GameRandomSource.cs:20` | PORTED | Explicit seed control. |
| `Debug.Log`, `Debug.LogWarning`, `Debug.LogError` | Unity console calls | `ILogSink.Info(...)`, `Warn(...)`, `Error(...)` | `src/HeadlessDCGO.Engine/Headless/Services/ILogSink.cs` | PORTED | Plain log abstraction. |
| optional no-op logging | client-only log side effects | `NullLogSink.Info(...)`, `Warn(...)`, `Error(...)` | `src/HeadlessDCGO.Engine/Headless/Services/NullLogSink.cs:5`, `src/HeadlessDCGO.Engine/Headless/Services/NullLogSink.cs:9`, `src/HeadlessDCGO.Engine/Headless/Services/NullLogSink.cs:13` | PORTED | Removes log side effects. |
| capture logs for tests | Unity console / PlayLog output | `InMemoryLogSink.Info(...)`, `Snapshot()`, `Clear()` | `src/HeadlessDCGO.Engine/Headless/Services/InMemoryLogSink.cs:10`, `src/HeadlessDCGO.Engine/Headless/Services/InMemoryLogSink.cs:25`, `src/HeadlessDCGO.Engine/Headless/Services/InMemoryLogSink.cs:30` | PORTED | Testable log collection. |
| `PlayLog.OnAddLog` | `PlayLog.cs` | `EngineTrace.Record(...)` | `src/HeadlessDCGO.Engine/Headless/Diagnostics/EngineTrace.cs:22` | PORTED | Structured deterministic trace. |
| trace snapshot | UI log panel state | `EngineTrace.Snapshot()` | `src/HeadlessDCGO.Engine/Headless/Diagnostics/EngineTrace.cs:44` | PORTED | Replay/debug inspection. |
| deterministic trace hash | parity/debug comparison | `EngineTrace.Fingerprint()` | `src/HeadlessDCGO.Engine/Headless/Diagnostics/EngineTrace.cs:49` | PORTED | Stable comparison surface. |

## Network And Client-Only Mapping

| Existing dependency function/name | Existing location | Current porting function/name | Current location | Status | Notes |
|---|---|---|---|---|---|
| `PhotonNetwork`, `PhotonView`, `[PunRPC]`, `Room`, `Player` network callbacks | Photon/PUN usage under Unity client | `SessionContext`, `HeadlessActionQueue`, `GameEvent` | `src/HeadlessDCGO.Engine/Headless/Runtime/SessionContext.cs`, `src/HeadlessDCGO.Engine/Headless/Runtime/HeadlessActionQueue.cs`, `src/HeadlessDCGO.Engine/Headless/Runtime/GameEvent.cs` | CONTRACT | Local deterministic session/action/event model replaces network transport in core. |
| RPC action sync | Photon RPC methods | `HeadlessActionQueue.Enqueue(...)` / `ReplayActionRecord.Serialize()` | `src/HeadlessDCGO.Engine/Headless/Runtime/HeadlessActionQueue.cs:15`, `src/HeadlessDCGO.Engine/Headless/Runtime/HeadlessActionQueue.cs:133` | PORTED | Replay metadata replaces RPC dependency for deterministic runs. |
| `TextMeshPro`, `UnityEngine.UI`, `DOTween`, `Animator`, `AudioSource`, `Camera`, VFX calls | visual/client scripts | no gameplay replacement; use `UnityNullObjectPolicy` only when encountered during migration | `src/HeadlessDCGO.Engine/Headless/Bridge/UnityNullObjectPolicy.cs` | EXCLUDED | These must not become Headless runtime dependencies. |
| `WebSocketSharp` transport | network/client transport | none in core engine | N/A | EXCLUDED | Future remote runner, if needed, belongs outside core engine. |

## Lookup Rules For Future Porting

1. If AS-IS code reads `GManager.instance` or `ContinuousController.instance`, replace it with `EngineContext`, `GManagerBridge`, `ContinuousContext`, or explicit constructor parameters.
2. If AS-IS code waits for UI selection, replace the wait with `ChoiceRequest` plus `IChoiceProvider.ChooseAsync` and persist pending state through `IHeadlessChoiceController`.
3. If AS-IS code mutates card scene objects or transforms, map the gameplay meaning to `IZoneMover`, `ZoneState`, `CardInstanceState`, or repository state. Do not port visual transform code.
4. If AS-IS code uses `StartCoroutine` or `WaitForSeconds`, map it to `EngineTaskRunner` and `EngineWaitCondition`. Do not depend on Unity frame time.
5. If AS-IS code sends Photon RPC or reads room/player network state, map gameplay input to `HeadlessActionQueue`, `SessionContext`, `HeadlessPlayerId`, and `GameEvent`.
6. If AS-IS code loads card/deck/banlist gameplay data through Unity assets/resources, map it to `CardAssetJsonLoader`, `CardDatabase`, `DeckListLoader`, `BanlistLoader`, and `DeckValidator`.
7. If AS-IS code writes logs or PlayLog UI messages, map deterministic diagnostics to `EngineTrace` or `ILogSink`.
8. If AS-IS dependency is purely visual/audio/camera/editor, classify it as `EXCLUDED` and do not add a Headless runtime replacement.

## Maintenance Notes

- This document is the human-readable mapping. Existing CSV inventories remain the machine-readable source for broad scans.
- When a new porting API is added, update both this document and the relevant source-origin CSV if the mapping changes.
- Do not use this document as proof that full gameplay semantics are ported. Rows marked `CONTRACT` or `BRIDGE` are intentionally limited to Phase 1 replacement surfaces.
