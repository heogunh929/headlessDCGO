namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Services;

// TODO: Replace terminal-only reward calculation once win/loss/scoring logic is ported.
public sealed class HeadlessRlEnvironment
{
    private readonly DcgoMatch _match;
    private readonly HeadlessRlEnvironmentOptions _options;
    private readonly ObservationEncoder _observationEncoder;
    private readonly ActionEncoder _actionEncoder;
    private readonly IRlRewardCalculator _rewardCalculator;

    public HeadlessRlEnvironment(
        DcgoMatch? match = null,
        HeadlessRlEnvironmentOptions? options = null)
    {
        _match = match ?? new DcgoMatch();
        _options = options ?? HeadlessRlEnvironmentOptions.Default;
        _observationEncoder = new ObservationEncoder(_options.ObservationEncoding);
        _actionEncoder = new ActionEncoder(_options.ActionEncoding);
        _rewardCalculator = _options.RewardCalculator;
    }

    public DcgoMatch Match => _match;

    public async Task<RlStepResult> InitializeAsync(
        MatchConfig config,
        CancellationToken cancellationToken = default)
    {
        await _match.InitializeAsync(config, cancellationToken).ConfigureAwait(false);
        return Observe();
    }

    public async Task<RlStepResult> ResetAsync(CancellationToken cancellationToken = default)
    {
        await _match.ResetAsync(cancellationToken).ConfigureAwait(false);
        return Observe();
    }

    public RlStepResult Observe()
    {
        return Encode(new StepResult(
            IsTerminal: _match.IsTerminal(),
            HasPendingChoice: _match.HasPendingChoice(),
            Events: Array.Empty<GameEvent>(),
            Observation: _match.GetObservation(),
            ActionMask: _match.GetActionMask()));
    }

    public async Task<RlStepResult> StepAsync(
        EncodedAction action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        return await StepAsync(action.LegalAction, cancellationToken).ConfigureAwait(false);
    }

    public async Task<RlStepResult> StepByActionIdAsync(
        HeadlessEntityId actionId,
        CancellationToken cancellationToken = default)
    {
        EncodedActionMask currentMask = _actionEncoder.Encode(_match.GetActionMask());
        EncodedAction? action = currentMask.FindByActionId(actionId);
        if (action is null)
        {
            return RejectMissingAction(
                $"Rejected unknown action id: {actionId.Value}",
                new Dictionary<string, object?>
                {
                    ["actionId"] = actionId.Value
                });
        }

        return await StepAsync(action, cancellationToken).ConfigureAwait(false);
    }

    public async Task<RlStepResult> StepByEncodedKeyAsync(
        string encodedKey,
        CancellationToken cancellationToken = default)
    {
        EncodedActionMask currentMask = _actionEncoder.Encode(_match.GetActionMask());
        EncodedAction? action = currentMask.FindByEncodedKey(encodedKey);
        if (action is null)
        {
            return RejectMissingAction(
                $"Rejected unknown encoded action key: {encodedKey}",
                new Dictionary<string, object?>
                {
                    ["encodedKey"] = encodedKey
                });
        }

        return await StepAsync(action, cancellationToken).ConfigureAwait(false);
    }

    public async Task<RlStepResult> StepByActionIndexAsync(
        int actionIndex,
        CancellationToken cancellationToken = default)
    {
        EncodedActionMask currentMask = _actionEncoder.Encode(_match.GetActionMask());
        EncodedAction? action = currentMask.FirstByActionIndex(actionIndex);
        if (action is null)
        {
            return RejectMissingAction(
                $"Rejected unknown action index: {actionIndex}",
                new Dictionary<string, object?>
                {
                    ["actionIndex"] = actionIndex
                });
        }

        return await StepAsync(action, cancellationToken).ConfigureAwait(false);
    }

    public async Task<RlStepResult> StepAsync(
        LegalAction action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (_options.RejectActionsOutsideMask)
        {
            ActionMask currentMask = _match.GetActionMask();
            if (!currentMask.ContainsAction(action))
            {
                RlStepResult rejectedResult = Encode(new StepResult(
                    IsTerminal: _match.IsTerminal(),
                    HasPendingChoice: _match.HasPendingChoice(),
                    Events: new[]
                    {
                        CreateInvalidActionEvent(action)
                    },
                    Observation: _match.GetObservation(),
                    ActionMask: currentMask));

                return rejectedResult with
                {
                    Reward = rejectedResult.Reward + _options.InvalidActionPenalty
                };
            }
        }

        StepResult applyResult = await _match.ApplyActionAsync(action, cancellationToken).ConfigureAwait(false);
        StepResult stepResult = await _match.StepAsync(cancellationToken).ConfigureAwait(false);

        return Encode(stepResult with
        {
            Events = applyResult.Events.Concat(stepResult.Events).ToArray()
        });
    }

    public RlStepResult Encode(StepResult stepResult)
    {
        ArgumentNullException.ThrowIfNull(stepResult);

        MatchResult? result = stepResult.IsTerminal ? _match.GetResult() : null;
        RlReward reward = _rewardCalculator.Evaluate(
            stepResult.IsTerminal,
            result,
            _options.PerspectivePlayerId);

        return new RlStepResult(
            stepResult.IsTerminal,
            stepResult.HasPendingChoice,
            _observationEncoder.Encode(stepResult.Observation),
            _actionEncoder.Encode(stepResult.ActionMask),
            stepResult.Events,
            result,
            reward.Reward,
            reward.Discount);
    }

    private RlStepResult RejectMissingAction(
        string message,
        IReadOnlyDictionary<string, object?> metadata)
    {
        RlStepResult rejectedResult = Encode(new StepResult(
            IsTerminal: _match.IsTerminal(),
            HasPendingChoice: _match.HasPendingChoice(),
            Events: new[]
            {
                CreateInvalidActionEvent(message, metadata)
            },
            Observation: _match.GetObservation(),
            ActionMask: _match.GetActionMask()));

        return rejectedResult with
        {
            Reward = rejectedResult.Reward + _options.InvalidActionPenalty
        };
    }

    private static GameEvent CreateInvalidActionEvent(LegalAction action)
    {
        return CreateInvalidActionEvent(
            $"Rejected action outside current action mask: {action.ActionType}",
            new Dictionary<string, object?>
            {
                ["actionId"] = action.Id.Value,
                ["playerId"] = action.PlayerId.Value,
                ["actionType"] = action.ActionType
            });
    }

    private static GameEvent CreateInvalidActionEvent(
        string message,
        IReadOnlyDictionary<string, object?> metadata)
    {
        return new GameEvent(
            Sequence: 0,
            GameEventType.InvalidAction,
            message,
            metadata);
    }
}

public sealed record HeadlessRlEnvironmentOptions
{
    public static HeadlessRlEnvironmentOptions Default { get; } = new();

    public ObservationEncodingOptions ObservationEncoding { get; init; } = ObservationEncodingOptions.Default;

    public ActionEncodingOptions ActionEncoding { get; init; } = ActionEncodingOptions.Default;

    public HeadlessPlayerId? PerspectivePlayerId { get; init; }

    public IRlRewardCalculator RewardCalculator { get; init; } = TerminalRlRewardCalculator.Default;

    public bool RejectActionsOutsideMask { get; init; }

    public double InvalidActionPenalty { get; init; }
}
