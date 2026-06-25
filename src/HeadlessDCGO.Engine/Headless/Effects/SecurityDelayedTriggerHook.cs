namespace HeadlessDCGO.Engine.Headless.Effects;

using HeadlessDCGO.Engine.Headless.Rules;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class SecurityDelayedTriggerHook
{
    public const string SecurityCheckTiming = "OnSecurityCheck";
    public const string SecuritySkillTiming = "SecuritySkill";
    public const string DelayedTriggerTiming = "DelayedTrigger";
    public const string HookKindKey = "securityDelayedHook";
    public const string SecurityCardIdKey = "securityCardId";
    public const string DelayedSourceEntityIdKey = "delayedSourceEntityId";
    public const string OptionalPromptMessage = "Choose a security or delayed optional effect to activate.";

    private readonly AutoProcessingTriggerCollector _collector;
    private readonly MandatoryEffectOrdering _mandatoryOrdering;

    public SecurityDelayedTriggerHook(
        AutoProcessingTriggerCollector collector,
        MandatoryEffectOrdering? mandatoryOrdering = null)
    {
        _collector = collector ?? throw new ArgumentNullException(nameof(collector));
        _mandatoryOrdering = mandatoryOrdering ?? new MandatoryEffectOrdering();
    }

    public static GameEvent CreateSecurityCheckEvent(
        long sequence,
        HeadlessPlayerId checkedPlayerId,
        HeadlessEntityId securityCardId,
        HeadlessEntityId? attackerEntityId = null,
        TimingWindowTriggerKind kind = TimingWindowTriggerKind.Mandatory,
        EffectResolutionMode mode = EffectResolutionMode.MainStack,
        int priority = 0)
    {
        if (checkedPlayerId.IsEmpty)
        {
            throw new ArgumentException("Checked player id must not be empty.", nameof(checkedPlayerId));
        }

        if (securityCardId.IsEmpty)
        {
            throw new ArgumentException("Security card id must not be empty.", nameof(securityCardId));
        }

        var metadata = BaseMetadata(SecurityCheckTiming, checkedPlayerId, kind, mode, priority);
        metadata[AutoProcessingTriggerCollector.CardIdKey] = securityCardId;
        metadata[SecurityCardIdKey] = securityCardId;

        if (attackerEntityId is HeadlessEntityId attacker)
        {
            if (attacker.IsEmpty)
            {
                throw new ArgumentException("Attacker entity id must not be empty.", nameof(attackerEntityId));
            }

            metadata[AutoProcessingTriggerCollector.TargetEntityIdKey] = attacker;
        }

        return new GameEvent(
            sequence,
            GameEventType.SecurityCheck,
            "Security check trigger window opened.",
            metadata);
    }

    public static GameEvent CreateSecuritySkillEvent(
        long sequence,
        HeadlessPlayerId checkedPlayerId,
        HeadlessEntityId securityCardId,
        TimingWindowTriggerKind kind = TimingWindowTriggerKind.Mandatory,
        EffectResolutionMode mode = EffectResolutionMode.MainStack,
        int priority = 0)
    {
        if (checkedPlayerId.IsEmpty)
        {
            throw new ArgumentException("Checked player id must not be empty.", nameof(checkedPlayerId));
        }

        if (securityCardId.IsEmpty)
        {
            throw new ArgumentException("Security card id must not be empty.", nameof(securityCardId));
        }

        var metadata = BaseMetadata(SecuritySkillTiming, checkedPlayerId, kind, mode, priority);
        metadata[AutoProcessingTriggerCollector.CardIdKey] = securityCardId;
        metadata[SecurityCardIdKey] = securityCardId;

        return new GameEvent(
            sequence,
            GameEventType.SecuritySkill,
            "Security skill trigger window opened.",
            metadata);
    }

    public static GameEvent CreateDelayedTriggerEvent(
        long sequence,
        HeadlessPlayerId controllerId,
        HeadlessEntityId delayedSourceEntityId,
        TimingWindowTriggerKind kind = TimingWindowTriggerKind.Mandatory,
        EffectResolutionMode mode = EffectResolutionMode.MainStack,
        int priority = 0)
    {
        if (controllerId.IsEmpty)
        {
            throw new ArgumentException("Controller id must not be empty.", nameof(controllerId));
        }

        if (delayedSourceEntityId.IsEmpty)
        {
            throw new ArgumentException("Delayed source entity id must not be empty.", nameof(delayedSourceEntityId));
        }

        var metadata = BaseMetadata(DelayedTriggerTiming, controllerId, kind, mode, priority);
        metadata[AutoProcessingTriggerCollector.SourceEntityIdKey] = delayedSourceEntityId;
        metadata[DelayedSourceEntityIdKey] = delayedSourceEntityId;

        return new GameEvent(
            sequence,
            GameEventType.DelayedTrigger,
            "Delayed trigger window opened.",
            metadata);
    }

    public SecurityDelayedTriggerHookResult Process(
        GameEvent gameEvent,
        EffectScheduler scheduler,
        HeadlessPlayerId turnPlayerId,
        HeadlessPlayerId? nonTurnPlayerId = null,
        OptionalPromptQueue? optionalPromptQueue = null,
        HeadlessPlayerId? optionalPromptPlayerId = null)
    {
        ArgumentNullException.ThrowIfNull(gameEvent);
        ArgumentNullException.ThrowIfNull(scheduler);

        if (!IsSupportedEvent(gameEvent))
        {
            return SecurityDelayedTriggerHookResult.Failure(
                gameEvent,
                $"Game event '{gameEvent.Type}' is not a security or delayed trigger event.");
        }

        TriggerCollectionResult collection = _collector.Collect(gameEvent);
        if (!collection.IsSuccess)
        {
            return SecurityDelayedTriggerHookResult.Failure(gameEvent, collection.FailureReason, collection);
        }

        MandatoryEffectOrderResult order = _mandatoryOrdering.OrderAndEnqueue(
            collection.Triggers,
            scheduler,
            turnPlayerId,
            nonTurnPlayerId);

        if (!order.IsSuccess)
        {
            return SecurityDelayedTriggerHookResult.Failure(gameEvent, order.FailureReason, collection, order);
        }

        OptionalPromptQueueResult? optionalPrompt = null;
        if (order.DeferredOptionalTriggers.Count > 0)
        {
            if (optionalPromptQueue is null)
            {
                return SecurityDelayedTriggerHookResult.Failure(
                    gameEvent,
                    "Optional security or delayed triggers require an optional prompt queue.",
                    collection,
                    order);
            }

            HeadlessPlayerId promptPlayer = optionalPromptPlayerId ?? ResolveOptionalPromptPlayer(order.DeferredOptionalTriggers);
            if (promptPlayer.IsEmpty)
            {
                return SecurityDelayedTriggerHookResult.Failure(
                    gameEvent,
                    "Optional prompt player id must not be empty.",
                    collection,
                    order);
            }

            optionalPrompt = optionalPromptQueue.EnqueuePrompt(
                order.DeferredOptionalTriggers,
                promptPlayer,
                OptionalPromptMessage);

            if (!optionalPrompt.IsSuccess)
            {
                return SecurityDelayedTriggerHookResult.Failure(
                    gameEvent,
                    optionalPrompt.FailureReason,
                    collection,
                    order,
                    optionalPrompt);
            }
        }

        return SecurityDelayedTriggerHookResult.Success(gameEvent, collection, order, optionalPrompt);
    }

    private static Dictionary<string, object?> BaseMetadata(
        string timing,
        HeadlessPlayerId playerId,
        TimingWindowTriggerKind kind,
        EffectResolutionMode mode,
        int priority)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [HookKindKey] = true,
            [AutoProcessingTriggerCollector.TriggerTimingKey] = timing,
            [AutoProcessingTriggerCollector.PlayerIdKey] = playerId,
            [AutoProcessingTriggerCollector.TriggerKindKey] = kind.ToString(),
            [AutoProcessingTriggerCollector.ResolutionModeKey] = mode.ToString(),
            [AutoProcessingTriggerCollector.PriorityKey] = priority,
        };
    }

    private static bool IsSupportedEvent(GameEvent gameEvent)
    {
        return gameEvent.Type is GameEventType.SecurityCheck
            or GameEventType.SecuritySkill
            or GameEventType.DelayedTrigger;
    }

    private static HeadlessPlayerId ResolveOptionalPromptPlayer(
        IReadOnlyList<TimingWindowTrigger> optionalTriggers)
    {
        return optionalTriggers.Count == 0
            ? default
            : optionalTriggers[0].Request.ControllerId;
    }
}

public sealed record SecurityDelayedTriggerHookResult(
    bool IsSuccess,
    long EventSequence,
    GameEventType EventType,
    string Timing,
    int CollectedCount,
    int EnqueuedMandatoryCount,
    int QueuedOptionalCount,
    int UnknownPlayerCount,
    TriggerCollectionResult? Collection,
    MandatoryEffectOrderResult? MandatoryOrder,
    OptionalPromptQueueResult? OptionalPrompt,
    string FailureReason)
{
    public static SecurityDelayedTriggerHookResult Success(
        GameEvent gameEvent,
        TriggerCollectionResult collection,
        MandatoryEffectOrderResult mandatoryOrder,
        OptionalPromptQueueResult? optionalPrompt)
    {
        ArgumentNullException.ThrowIfNull(gameEvent);
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(mandatoryOrder);

        return new SecurityDelayedTriggerHookResult(
            true,
            gameEvent.Sequence,
            gameEvent.Type,
            collection.Timing,
            collection.Triggers.Count,
            mandatoryOrder.EnqueuedCount,
            mandatoryOrder.DeferredOptionalTriggers.Count,
            mandatoryOrder.UnknownPlayerTriggers.Count,
            collection,
            mandatoryOrder,
            optionalPrompt,
            string.Empty);
    }

    public static SecurityDelayedTriggerHookResult Failure(
        GameEvent gameEvent,
        string failureReason,
        TriggerCollectionResult? collection = null,
        MandatoryEffectOrderResult? mandatoryOrder = null,
        OptionalPromptQueueResult? optionalPrompt = null)
    {
        ArgumentNullException.ThrowIfNull(gameEvent);

        return new SecurityDelayedTriggerHookResult(
            false,
            gameEvent.Sequence,
            gameEvent.Type,
            collection?.Timing ?? string.Empty,
            collection?.Triggers.Count ?? 0,
            mandatoryOrder?.EnqueuedCount ?? 0,
            mandatoryOrder?.DeferredOptionalTriggers.Count ?? 0,
            mandatoryOrder?.UnknownPlayerTriggers.Count ?? 0,
            collection,
            mandatoryOrder,
            optionalPrompt,
            failureReason ?? string.Empty);
    }
}
