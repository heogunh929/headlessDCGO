namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Diagnostics;
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
        _options = options ?? HeadlessRlEnvironmentOptions.Default;
        _match = match ?? CreateDefaultMatch(_options);
        _observationEncoder = new ObservationEncoder(_options.ObservationEncoding);
        _actionEncoder = new ActionEncoder(_options.ActionEncoding);
        _rewardCalculator = _options.RewardCalculator;
    }

    public DcgoMatch Match => _match;

    private static DcgoMatch CreateDefaultMatch(HeadlessRlEnvironmentOptions options)
    {
        IActionLegality? legality = options.EnforceAgentActionLegality
            ? new LegalActionSetValidator()
            : null;

        return new DcgoMatch(
            EngineContext.CreateDefault(),
            new EngineTrace(),
            actionProcessor: null,
            actionLegality: legality);
    }

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

    // G3.5-RL-A3: act via the fixed factored action space. The index is the position in the
    // factored mask; out-of-mask indices are rejected (no state mutation) like other step paths.
    public async Task<RlStepResult> StepByFactoredIndexAsync(
        int factoredIndex,
        FactoredActionSchema? schema = null,
        CancellationToken cancellationToken = default)
    {
        FactoredActionMask mask = _match.EncodeFactoredActionMask(schema);
        if (!mask.TryGetAction(factoredIndex, out LegalAction action))
        {
            return RejectMissingAction(
                $"Rejected unknown factored action index: {factoredIndex}",
                new Dictionary<string, object?>
                {
                    ["factoredIndex"] = factoredIndex
                });
        }

        return await StepAsync(action, cancellationToken).ConfigureAwait(false);
    }

    public FactoredActionMask EncodeFactoredActionMask(FactoredActionSchema? schema = null)
    {
        return _match.EncodeFactoredActionMask(schema);
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

        // Authoritative boundary rejected the action at apply: no state mutation, no loop step.
        if (WasRejectedAtApply(applyResult))
        {
            RlStepResult rejected = Encode(applyResult);
            return rejected with
            {
                Reward = rejected.Reward + _options.InvalidActionPenalty
            };
        }

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

        // G3.5-RL-A4: encode the perspective-filtered observation so a self-play agent never
        // receives an opponent's hidden card identities. Single source of truth for the RL layer.
        ObservationSnapshot observation = _match.GetObservation(ResolvePerspective());

        return new RlStepResult(
            stepResult.IsTerminal,
            stepResult.HasPendingChoice,
            _observationEncoder.Encode(observation),
            _actionEncoder.Encode(stepResult.ActionMask),
            stepResult.Events,
            result,
            reward.Reward,
            reward.Discount);
    }

    // G3.5-RL-A4: the viewer whose private information is preserved. A fixed PerspectivePlayerId
    // (single-agent training) takes precedence; otherwise the current turn player (self-play, where
    // each decision is made from the acting player's view). Null only when no turn is established.
    private HeadlessPlayerId? ResolvePerspective()
    {
        if (_options.PerspectivePlayerId is { } configured)
        {
            return configured;
        }

        HeadlessPlayerId? turnPlayer = _match.Context.TurnController.Current.TurnPlayerId;
        return turnPlayer is { IsEmpty: false } ? turnPlayer : null;
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

    private static bool WasRejectedAtApply(StepResult applyResult)
    {
        return applyResult.Events.Any(gameEvent => gameEvent.Type == GameEventType.InvalidAction);
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

    /// <summary>
    /// When true (default), a default-constructed match enforces the single authoritative
    /// agent-action legality boundary (G3.5-RL-A1): actions outside the current legal set are
    /// rejected at apply time without mutating state. Ignored when an external match is supplied.
    /// </summary>
    public bool EnforceAgentActionLegality { get; init; } = true;
}
