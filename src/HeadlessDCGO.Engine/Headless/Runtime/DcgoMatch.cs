namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class DcgoMatch
{
    private readonly List<GameEvent> _pendingEvents = new();
    private readonly ITraceSink _traceSink;
    private readonly HeadlessGameLoop _gameLoop;
    private long _eventSequence;
    private MatchConfig _config = new();
    private MatchResult _result = new();
    private bool _isInitialized;
    private bool _isTerminal;

    public DcgoMatch()
        : this(EngineContext.CreateDefault(), new EngineTrace())
    {
    }

    public DcgoMatch(
        EngineContext context,
        ITraceSink? traceSink = null,
        IActionProcessor? actionProcessor = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        Context = context;
        Context.AttachMatch(this);
        _traceSink = traceSink ?? new NullTraceSink();
        _gameLoop = new HeadlessGameLoop(context, _traceSink, actionProcessor);
    }

    public EngineContext Context { get; }

    public bool IsInitialized => _isInitialized;

    public async Task InitializeAsync(MatchConfig config, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        cancellationToken.ThrowIfCancellationRequested();

        _config = config.Validate();
        _isInitialized = true;
        _isTerminal = false;
        _result = new MatchResult();
        _pendingEvents.Clear();
        _eventSequence = 0;
        _gameLoop.Reset();
        ApplyRandomSeed(config.RandomSeed);
        MatchSetupResult? setupResult = await ApplySetupAsync(_config, cancellationToken).ConfigureAwait(false);
        Context.TurnController.Initialize(config.PlayerIds, setupResult?.FirstPlayerId);
        Context.MemoryController.Initialize(
            config.InitialMemory,
            config.MinimumMemory,
            config.MaximumMemory);

        RecordEvent(
            GameEventType.StateChanged,
            "Match initialized.",
            new Dictionary<string, object?>
            {
                ["playerCount"] = config.PlayerIds.Count,
                ["randomSeed"] = config.RandomSeed,
                ["deterministicChoices"] = config.UseDeterministicChoices,
                ["initialMemory"] = config.InitialMemory,
                ["setupApplied"] = setupResult is not null,
                ["firstPlayerId"] = setupResult?.FirstPlayerId.Value
            });

        _traceSink.Record(
            "runtime",
            "Match initialized.",
            new Dictionary<string, object?>
            {
                ["playerCount"] = config.PlayerIds.Count,
                ["randomSeed"] = config.RandomSeed
            });

    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        cancellationToken.ThrowIfCancellationRequested();

        _isTerminal = false;
        _result = new MatchResult();
        _pendingEvents.Clear();
        _eventSequence = 0;
        _gameLoop.Reset();
        ApplyRandomSeed(_config.RandomSeed);
        MatchSetupResult? setupResult = await ApplySetupAsync(_config, cancellationToken).ConfigureAwait(false);
        Context.TurnController.Initialize(_config.PlayerIds, setupResult?.FirstPlayerId);
        Context.MemoryController.Initialize(
            _config.InitialMemory,
            _config.MinimumMemory,
            _config.MaximumMemory);

        RecordEvent(
            GameEventType.StateChanged,
            "Match reset.",
            new Dictionary<string, object?>
            {
                ["setupApplied"] = setupResult is not null,
                ["firstPlayerId"] = setupResult?.FirstPlayerId.Value
            });
        _traceSink.Record("runtime", "Match reset.");
    }

    public async Task<StepResult> StepAsync(CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        cancellationToken.ThrowIfCancellationRequested();
        if (_isTerminal)
        {
            return DrainStepResult();
        }

        bool wasTerminal = _isTerminal;
        HeadlessGameLoopStep loopStep = await _gameLoop.StepAsync(cancellationToken).ConfigureAwait(false);
        _isTerminal = loopStep.IsTerminal;

        if (loopStep.ResolvedEffectCount > 0)
        {
            RecordEvent(
                GameEventType.EffectResolved,
                "Resolved pending effects.",
                new Dictionary<string, object?> { ["count"] = loopStep.ResolvedEffectCount });
        }

        if (loopStep.ConsumedAction is not null)
        {
            RecordEvent(
                GameEventType.StateChanged,
                $"Action consumed: {loopStep.ConsumedAction.ActionType}",
                new Dictionary<string, object?>
                {
                    ["actionId"] = loopStep.ConsumedAction.Id.Value,
                    ["playerId"] = loopStep.ConsumedAction.PlayerId.Value,
                    ["pendingActions"] = loopStep.PendingActionCount
                });
        }

        if (loopStep.ActionResult is not null)
        {
            if (loopStep.ActionResult.IsSuccess)
            {
                _isTerminal |= ApplyTerminalResultMetadata(loopStep.ActionResult.Metadata);
                RecordActionSpecificEvent(loopStep);
            }

            RecordEvent(
                GameEventType.ActionProcessed,
                loopStep.ActionResult.Message,
                MergeMetadata(loopStep.ActionResult.Metadata, new Dictionary<string, object?>
                {
                    ["success"] = loopStep.ActionResult.IsSuccess
                }));
        }

        foreach (string message in loopStep.Messages)
        {
            RecordEvent(GameEventType.StateChanged, message);
        }

        if (_isTerminal)
        {
            // (X-02) When terminal is reported by the rule query (e.g. EndTurnCheck) rather than via action
            // metadata, lift the stored winner/reason outcome into the public match result.
            if (_result.WinnerId is null
                && !_result.IsDraw
                && !_result.IsSurrender
                && string.IsNullOrEmpty(_result.Reason)
                && Context.RuleQueryService is ITerminalOutcomeSink outcomeSink
                && outcomeSink.TryGetTerminalOutcome(out TerminalOutcome? outcome)
                && outcome is not null)
            {
                _result = new MatchResult(
                    outcome.WinnerPlayerId,
                    outcome.IsDraw,
                    IsSurrender: false,
                    outcome.Reason);
            }

            _result = _result with
            {
                Reason = string.IsNullOrWhiteSpace(_result.Reason)
                    ? "Terminal state reported by rule query."
                    : _result.Reason
            };

            if (!wasTerminal)
            {
                RecordEvent(
                    GameEventType.GameEnded,
                    _result.Reason,
                    new Dictionary<string, object?>
                    {
                        ["winnerPlayerId"] = _result.WinnerId?.Value,
                        ["isDraw"] = _result.IsDraw,
                        ["isSurrender"] = _result.IsSurrender
                    });
            }
        }

        return DrainStepResult();
    }

    public Task<StepResult> ApplyActionAsync(LegalAction action, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();
        if (_isTerminal)
        {
            throw new InvalidOperationException("Cannot apply actions after the match is terminal.");
        }

        _gameLoop.EnqueueAction(action);
        RecordEvent(
            GameEventType.ActionQueued,
            $"Action queued: {action.ActionType}",
            new Dictionary<string, object?>
            {
                ["actionId"] = action.Id.Value,
                ["playerId"] = action.PlayerId.Value
            });

        Context.LogSink.Info($"Action queued: {action.ActionType}");
        _traceSink.Record(
            "action",
            $"Action queued: {action.ActionType}",
            new Dictionary<string, object?>
            {
                ["actionId"] = action.Id.Value,
                ["playerId"] = action.PlayerId.Value
            });

        return Task.FromResult(DrainStepResult());
    }

    public IReadOnlyList<LegalAction> GetLegalActions(HeadlessPlayerId playerId)
    {
        EnsureInitialized();
        return _gameLoop.GetLegalActions(playerId);
    }

    public IReadOnlyList<LegalAction> PendingActions()
    {
        EnsureInitialized();
        return _gameLoop.PendingActions();
    }

    public ObservationSnapshot GetObservation()
    {
        EnsureInitialized();
        return _gameLoop.GetObservation(_isTerminal, _config.PlayerIds);
    }

    public EncodedObservation EncodeObservation(ObservationEncodingOptions? options = null)
    {
        return new ObservationEncoder(options).Encode(GetObservation());
    }

    public ActionMask GetActionMask()
    {
        EnsureInitialized();
        return _gameLoop.GetActionMask(_config.PlayerIds);
    }

    public EncodedActionMask EncodeActionMask(ActionEncodingOptions? options = null)
    {
        return new ActionEncoder(options).Encode(GetActionMask());
    }

    public bool IsTerminal()
    {
        EnsureInitialized();
        return _isTerminal;
    }

    public bool HasPendingChoice()
    {
        EnsureInitialized();
        return Context.ChoiceController.Current.IsPending;
    }

    public MatchResult GetResult()
    {
        EnsureInitialized();
        return _result;
    }

    private void RecordEvent(
        GameEventType type,
        string message,
        IReadOnlyDictionary<string, object?>? metadata = null)
    {
        _pendingEvents.Add(new GameEvent(
            ++_eventSequence,
            type,
            message,
            metadata ?? new Dictionary<string, object?>()));
    }

    private StepResult DrainStepResult()
    {
        GameEvent[] events = _pendingEvents.ToArray();
        _pendingEvents.Clear();
        ObservationSnapshot observation = GetObservation();
        Context.UpdateCurrentState(observation);
        return new StepResult(
            _isTerminal,
            Context.ChoiceController.Current.IsPending,
            events,
            observation,
            GetActionMask());
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

    private void RecordActionSpecificEvent(HeadlessGameLoopStep loopStep)
    {
        if (loopStep.ConsumedAction is null || loopStep.ActionResult is null)
        {
            return;
        }

        string normalizedActionType = HeadlessActionTypes.Normalize(loopStep.ConsumedAction.ActionType);
        if (normalizedActionType == HeadlessActionTypes.NormalizedDeclareAttack)
        {
            RecordEvent(
                GameEventType.AttackDeclared,
                loopStep.ActionResult.Message,
                loopStep.ActionResult.Metadata);
        }
        else if (normalizedActionType == HeadlessActionTypes.NormalizedResolveAttack)
        {
            RecordEvent(
                GameEventType.AttackResolved,
                loopStep.ActionResult.Message,
                loopStep.ActionResult.Metadata);
        }
        else if (normalizedActionType == HeadlessActionTypes.NormalizedClearAttack)
        {
            RecordEvent(
                GameEventType.AttackCleared,
                loopStep.ActionResult.Message,
                loopStep.ActionResult.Metadata);
        }
        else if (normalizedActionType == HeadlessActionTypes.NormalizedRequestChoice)
        {
            RecordEvent(
                GameEventType.ChoiceRequested,
                loopStep.ActionResult.Message,
                loopStep.ActionResult.Metadata);
        }
        else if (normalizedActionType == HeadlessActionTypes.NormalizedResolveChoice)
        {
            RecordEvent(
                GameEventType.ChoiceResolved,
                loopStep.ActionResult.Message,
                loopStep.ActionResult.Metadata);
        }
        else if (normalizedActionType == HeadlessActionTypes.NormalizedClearChoice)
        {
            RecordEvent(
                GameEventType.ChoiceCleared,
                loopStep.ActionResult.Message,
                loopStep.ActionResult.Metadata);
        }
    }

    private bool ApplyTerminalResultMetadata(IReadOnlyDictionary<string, object?> metadata)
    {
        if (!TryReadBool(metadata, HeadlessActionParameterKeys.IsTerminal, out bool isTerminal))
        {
            return false;
        }

        if (!isTerminal)
        {
            _result = new MatchResult();
            return false;
        }

        _result = new MatchResult(
            WinnerId: ReadOptionalPlayerId(metadata, HeadlessActionParameterKeys.WinnerPlayerId),
            IsDraw: ReadBoolOrDefault(metadata, HeadlessActionParameterKeys.IsDraw, defaultValue: false),
            IsSurrender: ReadBoolOrDefault(metadata, HeadlessActionParameterKeys.IsSurrender, defaultValue: false),
            Reason: ReadStringOrDefault(metadata, HeadlessActionParameterKeys.Reason, string.Empty));
        return true;
    }

    private static bool TryReadBool(
        IReadOnlyDictionary<string, object?> metadata,
        string key,
        out bool value)
    {
        if (!metadata.TryGetValue(key, out object? rawValue) || rawValue is null)
        {
            value = default;
            return false;
        }

        if (rawValue is bool boolValue)
        {
            value = boolValue;
            return true;
        }

        if (rawValue is string stringValue && bool.TryParse(stringValue, out bool parsedValue))
        {
            value = parsedValue;
            return true;
        }

        value = default;
        return false;
    }

    private static bool ReadBoolOrDefault(
        IReadOnlyDictionary<string, object?> metadata,
        string key,
        bool defaultValue)
    {
        return TryReadBool(metadata, key, out bool value)
            ? value
            : defaultValue;
    }

    private static HeadlessPlayerId? ReadOptionalPlayerId(
        IReadOnlyDictionary<string, object?> metadata,
        string key)
    {
        if (!metadata.TryGetValue(key, out object? rawValue) || rawValue is null)
        {
            return null;
        }

        if (rawValue is HeadlessPlayerId playerId)
        {
            return playerId;
        }

        if (rawValue is int intValue)
        {
            return new HeadlessPlayerId(intValue);
        }

        if (rawValue is string stringValue && int.TryParse(stringValue, out int parsedValue))
        {
            return new HeadlessPlayerId(parsedValue);
        }

        return null;
    }

    private void ApplyRandomSeed(int seed)
    {
        if (Context.RandomSource is IRandomSeedController seedController)
        {
            seedController.ResetSeed(seed);
        }
    }

    private async Task<MatchSetupResult?> ApplySetupAsync(
        MatchConfig config,
        CancellationToken cancellationToken)
    {
        if (config.Setup is null)
        {
            return null;
        }

        MatchSetupResult result = await new MatchSetupFlow()
            .ApplyAsync(Context, config.PlayerIds, config.Setup, cancellationToken)
            .ConfigureAwait(false);

        _traceSink.Record(
            "setup",
            "Match setup applied.",
            new Dictionary<string, object?>
            {
                ["firstPlayerId"] = result.FirstPlayerId.Value,
                ["setupTurnPlayerId"] = result.SetupTurnPlayerId.Value,
                ["playerCount"] = result.Players.Count
            });

        return result;
    }

    private void EnsureInitialized()
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("The match must be initialized before using lifecycle APIs.");
        }
    }

    private static string ReadStringOrDefault(
        IReadOnlyDictionary<string, object?> metadata,
        string key,
        string defaultValue)
    {
        return metadata.TryGetValue(key, out object? rawValue) && rawValue is string stringValue
            ? stringValue
            : defaultValue;
    }
}
