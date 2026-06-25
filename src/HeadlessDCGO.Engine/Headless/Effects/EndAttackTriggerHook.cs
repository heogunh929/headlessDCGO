namespace HeadlessDCGO.Engine.Headless.Effects;

using HeadlessDCGO.Engine.Headless.Rules;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class EndAttackTriggerHook
{
    public const string OnEndAttackTiming = "OnEndAttack";
    public const string HookKindKey = "endAttackTriggerHook";
    public const string AttackingPlayerIdKey = "attackingPlayerId";
    public const string DefendingPlayerIdKey = "defendingPlayerId";
    public const string AttackerIdKey = "attackerId";
    public const string AttackTargetIdKey = "attackTargetId";
    public const string BlockerIdKey = "blockerId";
    public const string AttackBlockedKey = "attackBlocked";
    public const string DirectAttackKey = "directAttack";

    private readonly AutoProcessingTriggerCollector _collector;
    private readonly MandatoryEffectOrdering _mandatoryOrdering;

    public EndAttackTriggerHook(
        AutoProcessingTriggerCollector collector,
        MandatoryEffectOrdering? mandatoryOrdering = null)
    {
        _collector = collector ?? throw new ArgumentNullException(nameof(collector));
        _mandatoryOrdering = mandatoryOrdering ?? new MandatoryEffectOrdering();
    }

    public static GameEvent CreateEndAttackEvent(
        long sequence,
        HeadlessAttackState attack,
        TimingWindowTriggerKind kind = TimingWindowTriggerKind.Mandatory,
        EffectResolutionMode mode = EffectResolutionMode.MainStack,
        int priority = 0)
    {
        ArgumentNullException.ThrowIfNull(attack);

        if (!attack.AttackingPlayerId.HasValue)
        {
            throw new ArgumentException("Attack state must include an attacking player.", nameof(attack));
        }

        if (!attack.AttackerId.HasValue)
        {
            throw new ArgumentException("Attack state must include an attacker.", nameof(attack));
        }

        var metadata = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [HookKindKey] = true,
            [AutoProcessingTriggerCollector.TriggerTimingKey] = OnEndAttackTiming,
            [AutoProcessingTriggerCollector.TriggerKindKey] = kind.ToString(),
            [AutoProcessingTriggerCollector.ResolutionModeKey] = mode.ToString(),
            [AutoProcessingTriggerCollector.PriorityKey] = priority,
            [AttackingPlayerIdKey] = attack.AttackingPlayerId.Value,
            [AttackerIdKey] = attack.AttackerId.Value,
            [AttackBlockedKey] = attack.IsBlocked,
            [DirectAttackKey] = attack.IsDirectAttack,
        };

        if (attack.DefendingPlayerId.HasValue)
        {
            metadata[DefendingPlayerIdKey] = attack.DefendingPlayerId.Value;
        }

        if (attack.TargetId.HasValue)
        {
            metadata[AttackTargetIdKey] = attack.TargetId.Value;
        }

        if (attack.BlockerId.HasValue)
        {
            metadata[BlockerIdKey] = attack.BlockerId.Value;
        }

        return new GameEvent(
            sequence,
            GameEventType.AttackResolved,
            "End attack trigger window opened.",
            metadata);
    }

    public EndAttackTriggerHookResult Process(
        HeadlessAttackState attack,
        long sequence,
        EffectScheduler scheduler,
        HeadlessPlayerId turnPlayerId,
        HeadlessPlayerId? nonTurnPlayerId = null,
        TimingWindowTriggerKind kind = TimingWindowTriggerKind.Mandatory,
        EffectResolutionMode mode = EffectResolutionMode.MainStack,
        int priority = 0)
    {
        ArgumentNullException.ThrowIfNull(attack);
        ArgumentNullException.ThrowIfNull(scheduler);

        if (!attack.IsResolved || attack.IsPending)
        {
            return EndAttackTriggerHookResult.Failure(
                $"End attack triggers require a resolved non-pending attack. pending={attack.IsPending}, resolved={attack.IsResolved}");
        }

        GameEvent gameEvent = CreateEndAttackEvent(sequence, attack, kind, mode, priority);
        return Process(gameEvent, scheduler, turnPlayerId, nonTurnPlayerId);
    }

    public EndAttackTriggerHookResult Process(
        GameEvent gameEvent,
        EffectScheduler scheduler,
        HeadlessPlayerId turnPlayerId,
        HeadlessPlayerId? nonTurnPlayerId = null)
    {
        ArgumentNullException.ThrowIfNull(gameEvent);
        ArgumentNullException.ThrowIfNull(scheduler);

        if (!IsSupportedEvent(gameEvent))
        {
            return EndAttackTriggerHookResult.Failure(
                gameEvent,
                $"Game event '{gameEvent.Type}' is not an end attack trigger event.");
        }

        TriggerCollectionResult collection = _collector.Collect(gameEvent);
        if (!collection.IsSuccess)
        {
            return EndAttackTriggerHookResult.Failure(gameEvent, collection.FailureReason, collection);
        }

        MandatoryEffectOrderResult order = _mandatoryOrdering.OrderAndEnqueue(
            collection.Triggers,
            scheduler,
            turnPlayerId,
            nonTurnPlayerId);

        if (!order.IsSuccess)
        {
            return EndAttackTriggerHookResult.Failure(gameEvent, order.FailureReason, collection, order);
        }

        return EndAttackTriggerHookResult.Success(gameEvent, collection, order);
    }

    private static bool IsSupportedEvent(GameEvent gameEvent)
    {
        return gameEvent.Type == GameEventType.AttackResolved
            && gameEvent.Metadata.TryGetValue(HookKindKey, out object? marker)
            && marker is bool value
            && value
            && gameEvent.Metadata.TryGetValue(AutoProcessingTriggerCollector.TriggerTimingKey, out object? timing)
            && string.Equals(timing?.ToString(), OnEndAttackTiming, StringComparison.Ordinal);
    }
}

public sealed record EndAttackTriggerHookResult(
    bool IsSuccess,
    long EventSequence,
    GameEventType EventType,
    string Timing,
    int CollectedCount,
    int EnqueuedMandatoryCount,
    int DeferredOptionalCount,
    int UnknownPlayerCount,
    TriggerCollectionResult? Collection,
    MandatoryEffectOrderResult? MandatoryOrder,
    string FailureReason)
{
    public static EndAttackTriggerHookResult Success(
        GameEvent gameEvent,
        TriggerCollectionResult collection,
        MandatoryEffectOrderResult mandatoryOrder)
    {
        ArgumentNullException.ThrowIfNull(gameEvent);
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(mandatoryOrder);

        return new EndAttackTriggerHookResult(
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
            string.Empty);
    }

    public static EndAttackTriggerHookResult Failure(string failureReason)
    {
        return new EndAttackTriggerHookResult(
            false,
            -1,
            GameEventType.Unknown,
            string.Empty,
            CollectedCount: 0,
            EnqueuedMandatoryCount: 0,
            DeferredOptionalCount: 0,
            UnknownPlayerCount: 0,
            Collection: null,
            MandatoryOrder: null,
            FailureReason: failureReason ?? string.Empty);
    }

    public static EndAttackTriggerHookResult Failure(
        GameEvent gameEvent,
        string failureReason,
        TriggerCollectionResult? collection = null,
        MandatoryEffectOrderResult? mandatoryOrder = null)
    {
        ArgumentNullException.ThrowIfNull(gameEvent);

        return new EndAttackTriggerHookResult(
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
            failureReason ?? string.Empty);
    }
}
