namespace HeadlessDCGO.Engine.Headless.Effects;

using HeadlessDCGO.Engine.Headless.Rules;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class MandatoryEffectOrdering
{
    public MandatoryEffectOrderResult Order(
        IEnumerable<TimingWindowTrigger> triggers,
        HeadlessPlayerId turnPlayerId,
        HeadlessPlayerId? nonTurnPlayerId = null)
    {
        if (turnPlayerId.IsEmpty)
        {
            return MandatoryEffectOrderResult.Failure("Turn player id must not be empty.");
        }

        if (nonTurnPlayerId is { IsEmpty: true })
        {
            return MandatoryEffectOrderResult.Failure("Non-turn player id must not be empty.");
        }

        if (triggers is null)
        {
            return MandatoryEffectOrderResult.Failure("Trigger list must not be null.");
        }

        var indexed = new List<IndexedTrigger>();
        var deferred = new List<TimingWindowTrigger>();
        var unknownPlayers = new List<TimingWindowTrigger>();
        var index = 0;

        foreach (TimingWindowTrigger? trigger in triggers)
        {
            if (trigger is null)
            {
                return MandatoryEffectOrderResult.Failure("Trigger list must not contain null values.");
            }

            if (trigger.Kind != TimingWindowTriggerKind.Mandatory)
            {
                deferred.Add(trigger);
                index++;
                continue;
            }

            int playerOrder = PlayerOrder(trigger.Request.ControllerId, turnPlayerId, nonTurnPlayerId);
            if (playerOrder == UnknownPlayerOrder)
            {
                unknownPlayers.Add(trigger);
                index++;
                continue;
            }

            indexed.Add(new IndexedTrigger(trigger, playerOrder, index));
            index++;
        }

        TimingWindowTrigger[] ordered = indexed
            .OrderBy(item => item.PlayerOrder)
            .ThenBy(item => item.Trigger.Priority)
            .ThenBy(item => item.Trigger.Sequence)
            .ThenBy(item => item.InputIndex)
            .Select(item => item.Trigger)
            .ToArray();

        return MandatoryEffectOrderResult.Success(
            ordered,
            deferred,
            unknownPlayers);
    }

    public MandatoryEffectOrderResult OrderAndEnqueue(
        IEnumerable<TimingWindowTrigger> triggers,
        EffectScheduler scheduler,
        HeadlessPlayerId turnPlayerId,
        HeadlessPlayerId? nonTurnPlayerId = null)
    {
        ArgumentNullException.ThrowIfNull(scheduler);

        MandatoryEffectOrderResult result = Order(triggers, turnPlayerId, nonTurnPlayerId);
        if (!result.IsSuccess)
        {
            return result;
        }

        foreach (TimingWindowTrigger trigger in result.OrderedMandatoryTriggers)
        {
            scheduler.Enqueue(trigger.Request, trigger.Mode);
        }

        return result with { EnqueuedCount = result.OrderedMandatoryTriggers.Count };
    }

    private const int TurnPlayerOrder = 0;
    private const int NonTurnPlayerOrder = 1;
    private const int UnknownPlayerOrder = 2;

    private static int PlayerOrder(
        HeadlessPlayerId controllerId,
        HeadlessPlayerId turnPlayerId,
        HeadlessPlayerId? nonTurnPlayerId)
    {
        if (controllerId == turnPlayerId)
        {
            return TurnPlayerOrder;
        }

        if (nonTurnPlayerId is HeadlessPlayerId nonTurn && controllerId == nonTurn)
        {
            return NonTurnPlayerOrder;
        }

        return nonTurnPlayerId is null ? NonTurnPlayerOrder : UnknownPlayerOrder;
    }

    private readonly record struct IndexedTrigger(
        TimingWindowTrigger Trigger,
        int PlayerOrder,
        int InputIndex);
}

public sealed record MandatoryEffectOrderResult(
    bool IsSuccess,
    IReadOnlyList<TimingWindowTrigger> OrderedMandatoryTriggers,
    IReadOnlyList<TimingWindowTrigger> DeferredOptionalTriggers,
    IReadOnlyList<TimingWindowTrigger> UnknownPlayerTriggers,
    int EnqueuedCount,
    string FailureReason)
{
    public static MandatoryEffectOrderResult Success(
        IReadOnlyList<TimingWindowTrigger> orderedMandatoryTriggers,
        IReadOnlyList<TimingWindowTrigger> deferredOptionalTriggers,
        IReadOnlyList<TimingWindowTrigger> unknownPlayerTriggers)
    {
        ArgumentNullException.ThrowIfNull(orderedMandatoryTriggers);
        ArgumentNullException.ThrowIfNull(deferredOptionalTriggers);
        ArgumentNullException.ThrowIfNull(unknownPlayerTriggers);

        return new MandatoryEffectOrderResult(
            true,
            orderedMandatoryTriggers.ToArray(),
            deferredOptionalTriggers.ToArray(),
            unknownPlayerTriggers.ToArray(),
            EnqueuedCount: 0,
            FailureReason: string.Empty);
    }

    public static MandatoryEffectOrderResult Failure(string failureReason)
    {
        return new MandatoryEffectOrderResult(
            false,
            Array.Empty<TimingWindowTrigger>(),
            Array.Empty<TimingWindowTrigger>(),
            Array.Empty<TimingWindowTrigger>(),
            EnqueuedCount: 0,
            FailureReason: failureReason ?? string.Empty);
    }
}
