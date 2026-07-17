namespace HeadlessDCGO.Engine.Headless.Bridge;

using System.Collections.ObjectModel;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Coroutines;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class EngineContext
{
    private readonly Dictionary<Type, object> _services = new();

    public EngineContext(
        IChoiceProvider choiceProvider,
        IRandomSource randomSource,
        ICardRepository cardRepository,
        ICardInstanceRepository cardInstanceRepository,
        IZoneMover zoneMover,
        IRuleQueryService ruleQueryService,
        IHeadlessTurnController turnController,
        IHeadlessChoiceController choiceController,
        IHeadlessAttackController attackController,
        IHeadlessMemoryController memoryController,
        ILogSink logSink,
        EngineTaskRunner taskRunner,
        EffectScheduler effectScheduler,
        ContinuousContext? continuousContext = null,
        EffectRegistry? effectRegistry = null,
        GameEventQueue? gameEventQueue = null,
        IHeadlessPlayerStatusController? playerStatusController = null)
    {
        ChoiceProvider = choiceProvider ?? throw new ArgumentNullException(nameof(choiceProvider));
        RandomSource = randomSource ?? throw new ArgumentNullException(nameof(randomSource));
        CardRepository = cardRepository ?? throw new ArgumentNullException(nameof(cardRepository));
        CardInstanceRepository = cardInstanceRepository ?? throw new ArgumentNullException(nameof(cardInstanceRepository));
        ZoneMover = zoneMover ?? throw new ArgumentNullException(nameof(zoneMover));
        RuleQueryService = ruleQueryService ?? throw new ArgumentNullException(nameof(ruleQueryService));
        TurnController = turnController ?? throw new ArgumentNullException(nameof(turnController));
        ChoiceController = choiceController ?? throw new ArgumentNullException(nameof(choiceController));
        AttackController = attackController ?? throw new ArgumentNullException(nameof(attackController));
        MemoryController = memoryController ?? throw new ArgumentNullException(nameof(memoryController));
        LogSink = logSink ?? throw new ArgumentNullException(nameof(logSink));
        TaskRunner = taskRunner ?? throw new ArgumentNullException(nameof(taskRunner));
        EffectScheduler = effectScheduler ?? throw new ArgumentNullException(nameof(effectScheduler));
        EffectRegistry = effectRegistry ?? new InMemoryEffectRegistry();
        GameEventQueue = gameEventQueue ?? new GameEventQueue();
        OptionalPromptQueue = new OptionalPromptQueue();
        MulliganCoordinator = new MulliganCoordinator();
        OnceFlags = new OnceFlagController();
        PlayerTurnCounters = new HeadlessDCGO.Engine.Headless.Runtime.PlayerTurnCounterController();
        DeferredActivations = new HeadlessDCGO.Engine.Headless.Runtime.DeferredActivationController();
        WindowResolution = new HeadlessDCGO.Engine.Headless.Effects.WindowResolutionController();
        PlayerStatusController = playerStatusController ?? new InMemoryHeadlessPlayerStatusController();
        ContinuousContext = continuousContext ?? ContinuousContext.Create(
            Array.Empty<HeadlessPlayerId>(),
            randomSeed: randomSource is IRandomStateReader randomStateReader ? randomStateReader.CurrentSeed : 0);

        // (RD-R3-02) the zone mover IS the field-entry/leave chokepoint for the AS-IS Permanent-object
        // lifetime (PermanentBookkeepingStore CREATE/DIE) — key it with this match's instance repository.
        // Wired here (not in CreateDefault) so every context construction path gets the lifetime seat.
        if (zoneMover is InMemoryZoneMover inMemoryZoneMover)
        {
            inMemoryZoneMover.BookkeepingRepository ??= cardInstanceRepository;
        }

        RegisterCoreServices();
    }

    public IChoiceProvider ChoiceProvider { get; }

    public IRandomSource RandomSource { get; }

    public ICardRepository CardRepository { get; }

    public ICardInstanceRepository CardInstanceRepository { get; }

    public IZoneMover ZoneMover { get; }

    public IRuleQueryService RuleQueryService { get; }

    public IHeadlessTurnController TurnController { get; }

    public IHeadlessChoiceController ChoiceController { get; }

    public IHeadlessAttackController AttackController { get; }

    public IHeadlessMemoryController MemoryController { get; }

    public ILogSink LogSink { get; }

    public EngineTaskRunner TaskRunner { get; }

    public EffectScheduler EffectScheduler { get; }

    public EffectRegistry EffectRegistry { get; }

    public GameEventQueue GameEventQueue { get; }

    /// <summary>(#2) Pending optional ("you may") trigger prompts awaiting an agent decision. Persists
    /// across the loop's pause/resume so optional triggers are activated by choice, not auto-fired.</summary>
    public OptionalPromptQueue OptionalPromptQueue { get; }

    /// <summary>(N-5) Coordinates the opening-hand mulligan decisions during setup. Active only when the
    /// match setup enables mulligan; otherwise idle.</summary>
    public MulliganCoordinator MulliganCoordinator { get; }

    /// <summary>(F-4) Per-turn use-count tracking that gates once-per-turn / max-count-per-turn effects.</summary>
    public OnceFlagController OnceFlags { get; }

    /// <summary>Player-scoped, turn-scoped counters (AS-IS <c>Player.DigivolveCount_ThisTurn</c> etc.).</summary>
    public HeadlessDCGO.Engine.Headless.Runtime.PlayerTurnCounterController PlayerTurnCounters { get; }

    /// <summary>(G11-002) Holds an activation suspended mid-resolution waiting for an agent choice, so the
    /// next ResolveChoice resumes it without re-running the originating action (no re-pay).</summary>
    public HeadlessDCGO.Engine.Headless.Runtime.DeferredActivationController DeferredActivations { get; }

    /// <summary>(A-2 RD-6) Holds the per-match [End of Your Turn] window drain-once marker. (The old registry-currency
    /// window continuation this once parked was retired at the SkillInfo cutover; only the EoT drain state survives.)</summary>
    public HeadlessDCGO.Engine.Headless.Effects.WindowResolutionController WindowResolution { get; }

    public IHeadlessPlayerStatusController PlayerStatusController { get; }

    public ContinuousContext ContinuousContext { get; }

    public DcgoMatch? CurrentMatch { get; private set; }

    private long _deletionBatchSequence;

    /// <summary>(D-1 / VR-8 / F1(b)) Allocate a fresh monotonic delete-BATCH id. One id == one AS-IS
    /// <c>DestroyPermanentsClass.Destroy()</c> call == one logical simultaneous-deletion batch == one
    /// <c>StackSkillInfos(OnDeletion / OnLeaveFieldAnyone)</c>. Stamped (via <c>MatchStateMutationSink.DeletionBatchIdKey</c>)
    /// onto the CardMoved event of every card leaving in the same batch so the window collapse
    /// (<c>WindowResolverWiring.CollectUnifiedSeed</c> / <c>CollectActivatedBridgeTriggers</c>) fires the reactor once
    /// per batch yet fires an INDEPENDENT delete-process (a distinct id) separately. Ids start at 1; the sentinel 0
    /// (an unstamped CardMoved, e.g. a raw zone move) collapses all-together, preserving the pre-D1 whole-pass
    /// behaviour for paths that never carried a batch.</summary>
    public long NextDeletionBatchId() => Interlocked.Increment(ref _deletionBatchSequence);

    /// <summary>(F1-M1 P1-1) Allocate a fresh monotonic security-LOSS batch id. One id == one AS-IS
    /// <c>IReduceSecurity.ReduceSecurity()</c> call == one <c>StackSkillInfos(OnLoseSecurity)</c> broadcast
    /// (the effect-driven <c>IDestroySecurity</c> path calls it ONCE for the whole N-card trash). Stamped (via
    /// <c>MatchStateMutationSink.SecurityLossBatchIdKey</c>) onto every trashed security card's CardMoved so the
    /// OnLoseSecurity activated-bridge collapse (<c>WindowResolverWiring.CollectActivatedBridgeTriggers</c>) fires the
    /// reactor once per batch yet fires an INDEPENDENT security removal (a distinct id) separately. Shares the
    /// deletion counter so security-loss and deletion ids are GLOBALLY unique — a drain that mixes a leave batch and a
    /// security-loss batch never collides in the window's <c>BatchId</c> cross-batch ordering (temporal allocation
    /// order == resolve order, matching AS-IS resolving each StackSkillInfos window before the next). Ids start at 1;
    /// the sentinel 0 (an unstamped security move) collapses all-together.</summary>
    public long NextSecurityLossBatchId() => Interlocked.Increment(ref _deletionBatchSequence);

    /// <summary>(F1-Tier1 OnDiscard*) Allocate a fresh monotonic DISCARD batch id. One id == one AS-IS logical
    /// discard operation == one <c>StackSkillInfos(OnDiscardHand / OnDiscardLibrary)</c> broadcast — AS-IS collects
    /// the whole discarded LIST and fires the timing ONCE (<c>DiscardHands</c> → CardController.cs:48-56;
    /// <c>TrashDeckCards</c> → CardController.cs:5807-5816). Stamped (via <c>MatchStateMutationSink.DiscardBatchIdKey</c>)
    /// onto every discarded card's CardMoved (Hand/Library-&gt;Trash) so the OnDiscard* activated-bridge collapse
    /// (<c>WindowResolverWiring.CollectActivatedBridgeTriggers</c>) fires a reactor once per batch yet fires an
    /// INDEPENDENT discard operation (a distinct id) separately. Shares the deletion counter so discard, deletion and
    /// security-loss ids are GLOBALLY unique (a mixed drain never collides in the window's <c>BatchId</c> cross-batch
    /// ordering). Ids start at 1; the sentinel 0 (an unstamped move — e.g. a non-effect trash) collapses all-together
    /// and, for OnDiscardHand/Security, fails the CardEffect!=null gate (no cause-effect id either).</summary>
    public long NextDiscardBatchId() => Interlocked.Increment(ref _deletionBatchSequence);

    /// <summary>(F1-Tier1 OnAddHand) Allocate a fresh monotonic ADD-HAND batch id. One id == one AS-IS
    /// <c>CardObjectController.AddHandCards(list, isDraw, cardEffect)</c> call == one
    /// <c>StackSkillInfos(OnAddHand)</c> broadcast — AS-IS builds the payload {Players, CardEffect, CardSources =
    /// the WHOLE added LIST} ONCE and fires the timing ONCE for an N-card hand-add (CardObjectController.cs:559,616).
    /// Stamped (via <c>MatchStateMutationSink.AddHandBatchIdKey</c>) onto every added card's CardMoved (-&gt;Hand) so
    /// the OnAddHand activated-bridge collapse (<c>WindowResolverWiring.CollectActivatedBridgeTriggers</c>) fires a
    /// reactor once per batch yet fires an INDEPENDENT hand-add (a distinct id) separately. Shares the deletion
    /// counter so add-hand, discard, deletion and security-loss ids are GLOBALLY unique (a mixed drain never collides
    /// in the window's <c>BatchId</c> cross-batch ordering). Ids start at 1; the sentinel 0 (an unstamped move — a
    /// turn/mulligan/setup draw, which carries no cause either) collapses all-together and fails the OnAddHand
    /// CardEffect!=null gate (AS-IS turn draws pass cardEffect=null, so no OnAddHand fires).</summary>
    public long NextAddHandBatchId() => Interlocked.Increment(ref _deletionBatchSequence);

    /// <summary>(F1-Tier1 OnAddSecurity, design item F1-ADD-COUNTER P2-1) Allocate a fresh monotonic ADD-SECURITY
    /// batch id. Unlike add-hand/discard/deletion/security-loss, OnAddSecurity is NOT collapsed — AS-IS fires one
    /// <c>StackSkillInfos(OnAddSecurity)</c> per SINGLE added security card (per <c>IAddSecurity</c>,
    /// CardController.cs:5478), resolved SEQUENTIALLY. So EACH card gets its OWN id (this is called PER card, not
    /// cached per flush) — N recovered/added cards get N ascending ids == the AS-IS per-card sequential resolution
    /// order. Stamped (via <c>MatchStateMutationSink.AddSecurityBatchIdKey</c>) onto every added card's -&gt;Security
    /// CardMoved so the OnAddSecurity activated bridge (<c>WindowResolverWiring.CollectActivatedBridgeTriggers</c>)
    /// sequences the co-drained per-card triggers ASCENDING (one at a time, no spurious cross-fire order prompt).
    /// Shares the deletion counter so add-security, add-hand, discard, deletion and security-loss ids are GLOBALLY
    /// unique in ONE counter space — a mixed drain (e.g. an Ascension security-add co-draining with the deletion
    /// batch that produced it) never collides in the window's raw-<c>BatchId</c> cross-batch ordering
    /// (<c>WindowResolver.FilterToMinimumBatch</c>). Formerly OnAddSecurity stamped the DcgoMatch event
    /// <c>Sequence</c> (a DIFFERENT counter space), which broke that cross-timing ordering invariant. Ids start at 1.</summary>
    public long NextSecurityAddBatchId() => Interlocked.Increment(ref _deletionBatchSequence);

    // (RD-3C1-01) Trailing staged operations of an INTERACTIVE-PRE-promoted delete FLUSH — the sink ops staged
    // AFTER the delete thunk that promoted the batch to a deferred deletion (e.g. the DRAW of "delete an enemy
    // Digimon; then draw 1"). When an interactive would-be-deleted replacement (Evade/Barrier/ArmorPurge/Fragment)
    // pauses the cut-in, MatchStateMutationSink.FlushAsync SWALLOWS the pause and the enclosing effect body does
    // NOT re-run — so these already-staged ops would otherwise be LOST. They are stashed here (keyed by the promoted
    // batch id, in original flush order) and REPLAYED by StateBasedDeletionSweepAsync once that batch fully finalizes
    // (cut-in resolved, survivors spared / casualties trashed) — mirroring the AS-IS effect coroutine resuming its
    // trailing statements after `yield return Destroy()` returns.
    private readonly Dictionary<long, List<Func<CancellationToken, Task>>> _promotedBatchTrailingOps = new();

    /// <summary>(RD-3C1-01) Stash the trailing staged ops of a promoted delete batch (original flush order preserved,
    /// appended if the same batch id promotes more than once). No-op for the unstamped sentinel id 0.</summary>
    public void StashPromotedBatchTrailingOps(long batchId, IReadOnlyList<Func<CancellationToken, Task>> ops)
    {
        if (batchId == 0 || ops.Count == 0)
        {
            return;
        }

        if (!_promotedBatchTrailingOps.TryGetValue(batchId, out List<Func<CancellationToken, Task>>? list))
        {
            list = new List<Func<CancellationToken, Task>>();
            _promotedBatchTrailingOps[batchId] = list;
        }

        list.AddRange(ops);
    }

    /// <summary>(RD-3C1-01) Whether any promoted delete batch still has trailing ops awaiting replay.</summary>
    public bool HasPromotedBatchTrailingOps => _promotedBatchTrailingOps.Count > 0;

    /// <summary>(RD-3C1-01) The batch ids that currently hold stashed trailing ops (snapshot — safe to mutate the
    /// registry while iterating).</summary>
    public IReadOnlyList<long> PromotedBatchTrailingOpBatchIds => _promotedBatchTrailingOps.Keys.ToArray();

    /// <summary>(RD-3C1-01) Remove and return a promoted batch's stashed trailing ops (null if none).</summary>
    public IReadOnlyList<Func<CancellationToken, Task>>? TakePromotedBatchTrailingOps(long batchId)
    {
        if (_promotedBatchTrailingOps.TryGetValue(batchId, out List<Func<CancellationToken, Task>>? list))
        {
            _promotedBatchTrailingOps.Remove(batchId);
            return list;
        }

        return null;
    }

    /// <summary>(PRIM-P0 B.O.4 #1) The action whose cost is currently being paid, set by the play / digivolve /
    /// option action around its BeforePayCost window so a card's [BeforePayCost] effect can gate on WHICH cost
    /// it is (AS-IS ChangeCostClass rootCondition). <see cref="PayCostRoot.None"/> outside a pay window.</summary>
    public PayCostRoot CurrentPayCostRoot { get; set; } = PayCostRoot.None;

    public ObservationSnapshot CurrentState { get; private set; } = ObservationSnapshot.Empty;

    public IReadOnlyDictionary<Type, object> Services =>
        new ReadOnlyDictionary<Type, object>(new Dictionary<Type, object>(_services));

    public EngineContext AttachMatch(DcgoMatch match)
    {
        CurrentMatch = match ?? throw new ArgumentNullException(nameof(match));
        RegisterService(match);
        return this;
    }

    public EngineContext ClearCurrentMatch()
    {
        CurrentMatch = null;
        CurrentState = ObservationSnapshot.Empty;
        _services.Remove(typeof(DcgoMatch));
        return this;
    }

    public EngineContext UpdateCurrentState(ObservationSnapshot snapshot)
    {
        CurrentState = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        return this;
    }

    public TService GetService<TService>()
        where TService : class
    {
        if (TryGetService<TService>(out TService? service) && service is not null)
        {
            return service;
        }

        throw new InvalidOperationException($"Service '{typeof(TService).FullName}' is not registered.");
    }

    public object GetService(Type serviceType)
    {
        if (TryGetService(serviceType, out object? service) && service is not null)
        {
            return service;
        }

        throw new InvalidOperationException($"Service '{serviceType.FullName}' is not registered.");
    }

    public bool TryGetService<TService>(out TService? service)
        where TService : class
    {
        if (TryGetService(typeof(TService), out object? rawService) &&
            rawService is TService typedService)
        {
            service = typedService;
            return true;
        }

        service = null;
        return false;
    }

    public bool TryGetService(Type serviceType, out object? service)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return _services.TryGetValue(serviceType, out service);
    }

    public EngineContext RegisterService<TService>(TService service)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(service);
        RegisterService(typeof(TService), service);
        RegisterConcreteAlias(service);
        return this;
    }

    public EngineContext RegisterService(Type serviceType, object service)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentNullException.ThrowIfNull(service);

        if (!serviceType.IsInstanceOfType(service))
        {
            throw new ArgumentException(
                $"Service instance type '{service.GetType().FullName}' is not assignable to '{serviceType.FullName}'.",
                nameof(service));
        }

        _services[serviceType] = service;
        RegisterConcreteAlias(service);
        return this;
    }

    public void ResetMatchState()
    {
        ResetIfSupported(ZoneMover);
        ResetIfSupported(CardInstanceRepository);
        ResetIfSupported(RuleQueryService);
        ResetIfSupported(TurnController);
        ResetIfSupported(ChoiceController);
        ResetIfSupported(AttackController);
        ResetIfSupported(MemoryController);
        ResetIfSupported(GameEventQueue);
        OptionalPromptQueue.Clear();
        MulliganCoordinator.Clear();
        OnceFlags.ResetMatchState();
        PlayerTurnCounters.ResetMatchState();
        DeferredActivations.ResetMatchState();
        WindowResolution.ResetMatchState();
        ResetIfSupported(PlayerStatusController);
        Interlocked.Exchange(ref _deletionBatchSequence, 0);
        _promotedBatchTrailingOps.Clear();
        CurrentState = ObservationSnapshot.Empty;
    }

    /// <summary>Auto-register a card's ported continuous/passive-trigger effects as it enters play. This is
    /// the enter-play chokepoint every <see cref="MatchStateMutationSink"/> defaults to (a sink that plays a
    /// card via PlayCardKind / PlayDigivolutionAsDigimonKind invokes this) — mirroring AS-IS, where every play
    /// (action- or effect-driven) runs through the single <c>PlayCardClass.PlayCard()</c> path, so the entered
    /// card's <c>EffectList</c> is live regardless of who played it. No-op for un-ported cards;
    /// <c>RegisterOnEnterPlay</c> skips activated effects, so this never double-runs imperative activations.</summary>
    public void RegisterEnteredCardEffects(Services.HeadlessEntityId instanceId, Services.HeadlessPlayerId controller)
        => Assets.Scripts.Script.CardEffectCommons.CardEffectRegistrar.RegisterCard(this, instanceId, controller);

    /// <param name="randomSeed">Deterministic RNG seed.</param>
    /// <param name="strictUnbound">(GPT-#1 / 신1) When true, the effect scheduler treats a request with
    /// no bound effect body as a hard failure instead of a silent <c>Unbound</c> drain — a strict
    /// coverage gate for Phase 4 porting / tests. Production defaults to lenient (false).</param>
    public static EngineContext CreateDefault(int randomSeed = 0, bool strictUnbound = false, bool deferredChoice = false)
    {
        GameRandomSource randomSource = new(randomSeed);
        var cardInstanceRepository = new InMemoryCardInstanceRepository();
        var logSink = new NullLogSink();
        // Hoisted so the production mutation sink can apply zone moves / memory (W2-follow).
        var zoneMover = new InMemoryZoneMover(randomSource);
        var memoryController = new InMemoryHeadlessMemoryController();

        var effectRegistry = new InMemoryEffectRegistry();
        // Hoisted so the mutation sink can open non-zone-move timing windows (CV-A4: OnTapped/OnUntapped)
        // on the same queue the EngineContext exposes.
        var gameEventQueue = new GameEventQueue();
        // (G8-002) Captured here, assigned to the constructed context below. Sinks are created lazily (after
        // construction), so selfRef is set by the time a sink is built — passing `context: selfRef` gives the
        // scheduler sink the same enter-play registration every context-bearing sink defaults to
        // (MatchStateMutationSink → selfRef.RegisterEnteredCardEffects), the same semantics as PlayCardAction.
        EngineContext? selfRef = null;
        var effectScheduler = new EffectScheduler(
            new EffectResolutionQueue(),
            CardEffectSchedulerResolver.Create(
                effectRegistry,
                sinkFactory: _ => new MatchStateMutationSink(
                    cardInstanceRepository, logSink, zoneMover, memoryController, effectRegistry, gameEventQueue,
                    // (PRIM-W4 AceOverflow) turn-relative memory sign for a leaving ACE's overflow penalty.
                    currentTurnPlayer: () => selfRef?.TurnController.Current.TurnPlayerId,
                    // (FR-P3) EngineContext so restriction/immunity checks honour player-scope predicates AND the
                    // played card auto-registers on enter-play via the sink's default hook.
                    context: selfRef),
                strictUnbound: strictUnbound));

        // G7-005: opt into the interactive DeferredChoiceProvider (suspend/resume) instead of the default
        // immediate ScriptedChoiceProvider. The provider must share the context's choice controller.
        var choiceController = new InMemoryHeadlessChoiceController();
        IChoiceProvider choiceProvider = deferredChoice
            ? new HeadlessDCGO.Engine.Headless.Runtime.DeferredChoiceProvider(choiceController)
            : new ScriptedChoiceProvider();

        var context = new EngineContext(
            choiceProvider,
            randomSource,
            new CardDatabase(),
            cardInstanceRepository,
            zoneMover,
            new InMemoryRuleQueryService(),
            new InMemoryHeadlessTurnController(),
            choiceController,
            new InMemoryHeadlessAttackController(),
            memoryController,
            logSink,
            new EngineTaskRunner(),
            effectScheduler,
            effectRegistry: effectRegistry,
            gameEventQueue: gameEventQueue);
        selfRef = context;
        return context;
    }

    private void RegisterCoreServices()
    {
        RegisterService<IChoiceProvider>(ChoiceProvider);
        RegisterService<IRandomSource>(RandomSource);
        RegisterService<ICardRepository>(CardRepository);
        RegisterService<ICardInstanceRepository>(CardInstanceRepository);
        RegisterService<IZoneMover>(ZoneMover);
        RegisterService<IRuleQueryService>(RuleQueryService);
        RegisterService<IHeadlessTurnController>(TurnController);
        RegisterService<IHeadlessChoiceController>(ChoiceController);
        RegisterService<IHeadlessAttackController>(AttackController);
        RegisterService<IHeadlessMemoryController>(MemoryController);
        RegisterService<ILogSink>(LogSink);
        RegisterService(TaskRunner);
        RegisterService(EffectScheduler);
        RegisterService(EffectRegistry);
        RegisterService(GameEventQueue);
        RegisterService(OptionalPromptQueue);
        RegisterService(MulliganCoordinator);
        RegisterService(OnceFlags);
        RegisterService(DeferredActivations);
        RegisterService(WindowResolution);
        RegisterService<IHeadlessPlayerStatusController>(PlayerStatusController);
        RegisterService(ContinuousContext);
    }

    private void RegisterConcreteAlias(object service)
    {
        Type concreteType = service.GetType();
        if (!concreteType.IsInterface && !concreteType.IsAbstract)
        {
            _services[concreteType] = service;
        }
    }

    private static void ResetIfSupported(object service)
    {
        if (service is IHeadlessMatchStateResettable resettable)
        {
            resettable.ResetMatchState();
        }
    }
}
