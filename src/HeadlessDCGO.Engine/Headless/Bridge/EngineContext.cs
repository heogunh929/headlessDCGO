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
        DeferredActivations = new HeadlessDCGO.Engine.Headless.Runtime.DeferredActivationController();
        PlayerStatusController = playerStatusController ?? new InMemoryHeadlessPlayerStatusController();
        ContinuousContext = continuousContext ?? ContinuousContext.Create(
            Array.Empty<HeadlessPlayerId>(),
            randomSeed: randomSource is IRandomStateReader randomStateReader ? randomStateReader.CurrentSeed : 0);

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

    /// <summary>(G11-002) Holds an activation suspended mid-resolution waiting for an agent choice, so the
    /// next ResolveChoice resumes it without re-running the originating action (no re-pay).</summary>
    public HeadlessDCGO.Engine.Headless.Runtime.DeferredActivationController DeferredActivations { get; }

    public IHeadlessPlayerStatusController PlayerStatusController { get; }

    public ContinuousContext ContinuousContext { get; }

    public DcgoMatch? CurrentMatch { get; private set; }

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
        DeferredActivations.ResetMatchState();
        ResetIfSupported(PlayerStatusController);
        CurrentState = ObservationSnapshot.Empty;
    }

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
        // (G8-002) Captured here, assigned to the constructed context below, so an effect-driven PlayCard
        // (MatchStateMutationSink) can auto-register the played card's effects via CardEffectRegistrar —
        // the same enter-play semantics as PlayCardAction. Sinks are created lazily (after construction),
        // so selfRef is set by the time the hook fires.
        EngineContext? selfRef = null;
        var effectScheduler = new EffectScheduler(
            new EffectResolutionQueue(),
            CardEffectSchedulerResolver.Create(
                effectRegistry,
                sinkFactory: _ => new MatchStateMutationSink(
                    cardInstanceRepository, logSink, zoneMover, memoryController, effectRegistry, gameEventQueue,
                    onCardEnteredPlay: (id, controller) =>
                    {
                        if (selfRef is not null)
                        {
                            HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.CardEffectRegistrar.RegisterCard(selfRef, id, controller);
                        }
                    },
                    // (PRIM-W4 AceOverflow) turn-relative memory sign for a leaving ACE's overflow penalty.
                    currentTurnPlayer: () => selfRef?.TurnController.Current.TurnPlayerId),
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
