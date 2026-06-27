namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

public sealed class HeadlessGameLoop(
    EngineContext context,
    ITraceSink? traceSink = null,
    IActionProcessor? actionProcessor = null,
    GameFlowProcessor? gameFlowProcessor = null)
{
    private readonly HeadlessActionQueue _actionQueue = new();
    private readonly IActionProcessor _actionProcessor = actionProcessor ?? new MetadataActionProcessor();
    private readonly HeadlessLegalActionDispatcher _legalActionDispatcher = new();
    private readonly ITraceSink _traceSink = traceSink ?? new NullTraceSink();
    // R2-3: the flow processor is injectable (default new()) so tests can drive a non-converging loop
    // and verify the iteration-cap flag propagates through StepResult / RlStepResult.
    private readonly GameFlowProcessor _gameFlowProcessor = gameFlowProcessor ?? new();
    private ActionProcessResult? _lastActionResult;
    private LegalAction? _lastAction;
    private long _stepIndex;
    private static readonly ChoiceZone[] ObservableZones =
    {
        ChoiceZone.Library,
        ChoiceZone.Hand,
        ChoiceZone.Security,
        ChoiceZone.Trash,
        ChoiceZone.Clock,
        ChoiceZone.Recollection,
        ChoiceZone.Execution,
        ChoiceZone.DigivolutionCards,
        ChoiceZone.LinkedCards,
        ChoiceZone.BattleArea,
        ChoiceZone.BreedingArea,
        ChoiceZone.DigitamaLibrary
    };

    public EngineContext Context { get; } = context;

    public async Task<HeadlessGameLoopStep> StepAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await Context.TaskRunner.StepAsync(cancellationToken).ConfigureAwait(false);

        LegalAction? consumedAction = null;
        ActionProcessResult? actionResult = null;
        if (_actionQueue.TryDequeue(out LegalAction? action))
        {
            consumedAction = action;
            Context.LogSink.Info($"Action consumed: {action.ActionType}");
            _traceSink.Record(
                "action",
                $"Action consumed: {action.ActionType}",
                new Dictionary<string, object?>
                {
                    ["actionId"] = action.Id.Value,
                    ["playerId"] = action.PlayerId.Value
                });

            actionResult = await _actionProcessor
                .ProcessAsync(action, Context, cancellationToken)
                .ConfigureAwait(false);

            if (actionResult.IsSuccess &&
                Context.RuleQueryService is IHeadlessLegalActionController legalActionController)
            {
                legalActionController.RemoveLegalAction(action.Id);
            }

            _lastAction = action;
            _lastActionResult = actionResult;
            Context.LogSink.Info(actionResult.Message);
            _traceSink.Record(
                "action",
                actionResult.Message,
                MergeMetadata(actionResult.Metadata, new Dictionary<string, object?>
                {
                    ["success"] = actionResult.IsSuccess
                }));
        }

        bool hadPendingEffects = Context.EffectScheduler.HasPendingEffects;
        FlowProcessResult flow = await _gameFlowProcessor
            .RunToStableAsync(Context, cancellationToken)
            .ConfigureAwait(false);
        int resolvedEffectCount = flow.ResolvedEffectCount;

        bool isTerminal = Context.RuleQueryService.IsTerminal();
        _stepIndex++;
        List<string> messages = new();

        if (resolvedEffectCount > 0)
        {
            messages.Add($"Resolved effects: {resolvedEffectCount}");
            _traceSink.Record(
                "effects",
                "Resolved pending effects.",
                new Dictionary<string, object?>
                {
                    ["count"] = resolvedEffectCount,
                    ["flowIterations"] = flow.Iterations
                });
        }

        if (flow.PausedForChoice)
        {
            messages.Add("Flow paused for pending choice.");
            _traceSink.Record("runtime", "Flow processor paused for pending choice.");
        }

        if (flow.IsMaxIterationsExceeded)
        {
            // GPT-#3: surface a runaway/iteration-cap stop distinctly from a genuine stable fixpoint.
            messages.Add("Flow hit the iteration cap without stabilizing.");
            _traceSink.Record(
                "runtime",
                "Flow processor exceeded MaxIterations without reaching a stable state.",
                new Dictionary<string, object?> { ["flowIterations"] = flow.Iterations });
        }

        if (isTerminal)
        {
            messages.Add("Rule query reported terminal state.");
            _traceSink.Record("runtime", "Rule query reported terminal state.");
        }

        return new HeadlessGameLoopStep(
            _stepIndex,
            isTerminal,
            consumedAction,
            actionResult,
            _actionQueue.Count,
            hadPendingEffects,
            resolvedEffectCount,
            messages,
            flow.IsMaxIterationsExceeded);
    }

    public void EnqueueAction(LegalAction action)
    {
        _actionQueue.Enqueue(action);
    }

    public void Reset()
    {
        _actionQueue.Clear();
        Context.ResetMatchState();
        Context.EffectScheduler.Clear();
        Context.TaskRunner.Clear();
        _lastAction = null;
        _lastActionResult = null;
        _stepIndex = 0;
    }

    public IReadOnlyList<LegalAction> PendingActions()
    {
        return _actionQueue.Snapshot();
    }

    public IReadOnlyList<LegalAction> GetLegalActions(HeadlessPlayerId playerId)
    {
        return MergeLegalActions(
            Context.RuleQueryService.GetLegalActions(playerId),
            _legalActionDispatcher.GetLegalActions(Context, playerId));
    }

    public ObservationSnapshot GetObservation(
        bool isTerminal,
        IEnumerable<HeadlessPlayerId>? playerIds = null,
        HeadlessPlayerId? perspectivePlayerId = null)
    {
        return new ObservationSnapshot(
            _stepIndex,
            isTerminal,
            _actionQueue.Count,
            Context.EffectScheduler.HasPendingEffects,
            Context.CardInstanceRepository.Snapshot().Count,
            (Context.RandomSource as IRandomStateReader)?.CurrentSeed,
            _lastAction?.ActionType,
            _lastActionResult?.IsSuccess,
            _lastActionResult?.Message,
            Context.TurnController.Current,
            Context.ChoiceController.Current,
            Context.AttackController.Current,
            new HeadlessEffectState(
                Context.EffectScheduler.PendingCount,
                Context.EffectScheduler.TotalEnqueuedCount,
                Context.EffectScheduler.TotalResolvedCount,
                Context.EffectScheduler.LastResolvedCount,
                Context.EffectScheduler.TotalUnboundCount),
            Context.MemoryController.Current,
            BuildPlayerObservations(playerIds ?? Array.Empty<HeadlessPlayerId>(), perspectivePlayerId));
    }

    public ActionMask GetActionMask(IEnumerable<HeadlessPlayerId> playerIds)
    {
        ArgumentNullException.ThrowIfNull(playerIds);

        LegalAction[] legalActions = playerIds
            .SelectMany(GetLegalActions)
            .ToArray();

        return new ActionMask(legalActions);
    }

    private static IReadOnlyList<LegalAction> MergeLegalActions(
        IEnumerable<LegalAction> seededActions,
        IEnumerable<LegalAction> dispatchedActions)
    {
        Dictionary<string, LegalAction> merged = new(StringComparer.Ordinal);
        foreach (LegalAction action in seededActions
                     .Concat(dispatchedActions)
                     .Where(action => !CheatActionGuard.IsCheatOrDebugAction(action.ActionType)))
        {
            string key = $"{action.PlayerId.Value}:{action.Id.Value}";
            merged[key] = action;
        }

        return merged.Values.ToArray();
    }

    private static IReadOnlyDictionary<string, object?> MergeMetadata(
        IReadOnlyDictionary<string, object?> source,
        IReadOnlyDictionary<string, object?> additionalValues)
    {
        Dictionary<string, object?> merged = new(source);

        foreach (KeyValuePair<string, object?> pair in additionalValues)
        {
            merged[pair.Key] = pair.Value;
        }

        return merged;
    }

    private IReadOnlyList<PlayerObservation> BuildPlayerObservations(
        IEnumerable<HeadlessPlayerId> playerIds,
        HeadlessPlayerId? perspectivePlayerId)
    {
        HeadlessPlayerId[] distinctPlayerIds = playerIds.Distinct().ToArray();
        if (distinctPlayerIds.Length == 0)
        {
            return Array.Empty<PlayerObservation>();
        }

        IZoneStateReader? zoneReader = Context.ZoneMover as IZoneStateReader;

        return distinctPlayerIds
            .Select(playerId => new PlayerObservation(
                playerId,
                BuildZoneObservations(zoneReader, playerId, perspectivePlayerId)))
            .ToArray();
    }

    // G3.5-RL-A4: when a perspective viewer is supplied, hidden zones (Library/Hand/Security/
    // DigitamaLibrary) of players other than the viewer are exposed as count-only — the card ids
    // are withheld so a self-play agent cannot read its opponent's private information.
    // A null perspective preserves the full ("god's-eye") view for debugging and legacy callers.
    // G3.5-RL-A4b: visible cards additionally carry typed per-card features (DP/level/cost/...).
    private IReadOnlyList<ZoneObservation> BuildZoneObservations(
        IZoneStateReader? zoneReader,
        HeadlessPlayerId playerId,
        HeadlessPlayerId? perspectivePlayerId)
    {
        IReadOnlyDictionary<ChoiceZone, IReadOnlyList<HeadlessEntityId>> snapshot =
            zoneReader?.Snapshot(playerId) ??
            new Dictionary<ChoiceZone, IReadOnlyList<HeadlessEntityId>>();

        return ObservableZones
            .Select(zone =>
            {
                HeadlessEntityId[] cardIds = snapshot.TryGetValue(zone, out IReadOnlyList<HeadlessEntityId>? cards)
                    ? cards.ToArray()
                    : Array.Empty<HeadlessEntityId>();

                bool hiddenFromViewer =
                    perspectivePlayerId is { } viewer &&
                    viewer != playerId &&
                    ZoneState.DefaultVisibility(zone) == ZoneVisibility.Hidden;

                // Count is always preserved; only the card identities are withheld when hidden.
                if (hiddenFromViewer)
                {
                    return new ZoneObservation(zone, cardIds.Length, Array.Empty<HeadlessEntityId>());
                }

                CardObservation[] cardObservations = cardIds.Select(BuildCardObservation).ToArray();
                return new ZoneObservation(zone, cardIds.Length, cardIds, cardObservations);
            })
            .ToArray();
    }

    private CardObservation BuildCardObservation(HeadlessEntityId instanceId)
    {
        if (!Context.CardInstanceRepository.TryGetInstance(instanceId, out CardInstanceRecord? instance) ||
            instance is null)
        {
            return new CardObservation(instanceId, string.Empty, "Unknown", 0, 0, 0, 0, false, false, 0);
        }

        Context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? definition);
        return CardObservationView.Build(instance, definition);
    }
}

public sealed record HeadlessGameLoopStep(
    long StepIndex,
    bool IsTerminal,
    LegalAction? ConsumedAction,
    ActionProcessResult? ActionResult,
    int PendingActionCount,
    bool HadPendingEffects,
    int ResolvedEffectCount,
    IReadOnlyList<string> Messages,
    // R2-3: the flow processor stopped at the iteration cap WITHOUT reaching a stable fixpoint. Carried
    // as a typed flag (not just a log message) so step/RL consumers can detect a runaway loop directly.
    bool FlowExceededIterationCap = false);
