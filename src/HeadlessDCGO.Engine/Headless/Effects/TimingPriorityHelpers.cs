namespace HeadlessDCGO.Engine.Headless.Effects;

using System.Collections.ObjectModel;
using HeadlessDCGO.Engine.Headless.Rules;
using HeadlessDCGO.Engine.Headless.Services;

public sealed record TimingPriorityOrderResult
{
    private TimingPriorityOrderResult(
        bool isSuccess,
        IReadOnlyList<TimingWindowTrigger> orderedTriggers,
        IReadOnlyList<TimingWindowTrigger> mandatoryTriggers,
        IReadOnlyList<TimingWindowTrigger> optionalTriggers,
        IReadOnlyList<TimingWindowTrigger> turnPlayerTriggers,
        IReadOnlyList<TimingWindowTrigger> nonTurnPlayerTriggers,
        IReadOnlyList<TimingWindowTrigger> unknownPlayerTriggers,
        int enqueuedMandatoryCount,
        string failureReason,
        IReadOnlyDictionary<string, object?> values)
    {
        IsSuccess = isSuccess;
        OrderedTriggers = CopyTriggers(orderedTriggers);
        MandatoryTriggers = CopyTriggers(mandatoryTriggers);
        OptionalTriggers = CopyTriggers(optionalTriggers);
        TurnPlayerTriggers = CopyTriggers(turnPlayerTriggers);
        NonTurnPlayerTriggers = CopyTriggers(nonTurnPlayerTriggers);
        UnknownPlayerTriggers = CopyTriggers(unknownPlayerTriggers);
        EnqueuedMandatoryCount = enqueuedMandatoryCount;
        FailureReason = failureReason ?? string.Empty;
        Values = CopyValues(values);
    }

    public bool IsSuccess { get; }

    public IReadOnlyList<TimingWindowTrigger> OrderedTriggers { get; }

    public IReadOnlyList<TimingWindowTrigger> MandatoryTriggers { get; }

    public IReadOnlyList<TimingWindowTrigger> OptionalTriggers { get; }

    public IReadOnlyList<TimingWindowTrigger> TurnPlayerTriggers { get; }

    public IReadOnlyList<TimingWindowTrigger> NonTurnPlayerTriggers { get; }

    public IReadOnlyList<TimingWindowTrigger> UnknownPlayerTriggers { get; }

    public int EnqueuedMandatoryCount { get; init; }

    public string FailureReason { get; }

    public IReadOnlyDictionary<string, object?> Values { get; }

    public static TimingPriorityOrderResult Success(
        IReadOnlyList<TimingWindowTrigger> orderedTriggers,
        IReadOnlyList<TimingWindowTrigger> mandatoryTriggers,
        IReadOnlyList<TimingWindowTrigger> optionalTriggers,
        IReadOnlyList<TimingWindowTrigger> turnPlayerTriggers,
        IReadOnlyList<TimingWindowTrigger> nonTurnPlayerTriggers,
        IReadOnlyList<TimingWindowTrigger> unknownPlayerTriggers,
        IReadOnlyDictionary<string, object?> values)
    {
        return new TimingPriorityOrderResult(
            true,
            orderedTriggers,
            mandatoryTriggers,
            optionalTriggers,
            turnPlayerTriggers,
            nonTurnPlayerTriggers,
            unknownPlayerTriggers,
            enqueuedMandatoryCount: 0,
            failureReason: string.Empty,
            values);
    }

    public static TimingPriorityOrderResult Failure(
        string failureReason,
        IReadOnlyDictionary<string, object?>? values = null)
    {
        return new TimingPriorityOrderResult(
            false,
            Array.Empty<TimingWindowTrigger>(),
            Array.Empty<TimingWindowTrigger>(),
            Array.Empty<TimingWindowTrigger>(),
            Array.Empty<TimingWindowTrigger>(),
            Array.Empty<TimingWindowTrigger>(),
            Array.Empty<TimingWindowTrigger>(),
            enqueuedMandatoryCount: 0,
            failureReason,
            values ?? new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>(StringComparer.Ordinal)));
    }

    private static IReadOnlyList<TimingWindowTrigger> CopyTriggers(
        IReadOnlyList<TimingWindowTrigger> triggers)
    {
        ArgumentNullException.ThrowIfNull(triggers);
        return Array.AsReadOnly(triggers.ToArray());
    }

    private static IReadOnlyDictionary<string, object?> CopyValues(
        IReadOnlyDictionary<string, object?> values)
    {
        var copy = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, object?> pair in values.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pair.Key);
            copy[pair.Key.Trim()] = pair.Value;
        }

        return new ReadOnlyDictionary<string, object?>(copy);
    }
}

public static class TimingPriorityHelpers
{
    public const string OrderedEffectIdsKey = "orderedEffectIds";
    public const string MandatoryEffectIdsKey = "mandatoryEffectIds";
    public const string OptionalEffectIdsKey = "optionalEffectIds";
    public const string TurnPlayerEffectIdsKey = "turnPlayerEffectIds";
    public const string NonTurnPlayerEffectIdsKey = "nonTurnPlayerEffectIds";
    public const string UnknownPlayerEffectIdsKey = "unknownPlayerEffectIds";
    public const string TurnPlayerIdKey = "turnPlayerId";
    public const string NonTurnPlayerIdKey = "nonTurnPlayerId";

    public static TimingPriorityOrderResult Order(
        IEnumerable<TimingWindowTrigger> triggers,
        HeadlessPlayerId turnPlayerId,
        HeadlessPlayerId? nonTurnPlayerId = null)
    {
        TimingPriorityValidationResult validation = ValidateInputs(triggers, turnPlayerId, nonTurnPlayerId);
        if (!validation.IsSuccess)
        {
            return TimingPriorityOrderResult.Failure(validation.FailureReason);
        }

        var indexed = new List<IndexedTrigger>();
        var unknown = new List<TimingWindowTrigger>();
        int inputIndex = 0;
        foreach (TimingWindowTrigger trigger in validation.Triggers)
        {
            int playerOrder = PlayerOrder(trigger.Request.ControllerId, turnPlayerId, nonTurnPlayerId);
            if (playerOrder == UnknownPlayerOrder)
            {
                unknown.Add(trigger);
                inputIndex++;
                continue;
            }

            indexed.Add(new IndexedTrigger(
                trigger,
                KindOrder(trigger.Kind),
                playerOrder,
                inputIndex));
            inputIndex++;
        }

        TimingWindowTrigger[] ordered = indexed
            .OrderBy(item => item.KindOrder)
            .ThenBy(item => item.PlayerOrder)
            .ThenBy(item => item.Trigger.Priority)
            .ThenBy(item => item.Trigger.Sequence)
            .ThenBy(item => item.InputIndex)
            .Select(item => item.Trigger)
            .ToArray();

        TimingWindowTrigger[] mandatory = ordered
            .Where(trigger => trigger.Kind == TimingWindowTriggerKind.Mandatory)
            .ToArray();
        TimingWindowTrigger[] optional = ordered
            .Where(trigger => trigger.Kind == TimingWindowTriggerKind.Optional)
            .ToArray();
        TimingWindowTrigger[] turnPlayer = ordered
            .Where(trigger => trigger.Request.ControllerId == turnPlayerId)
            .ToArray();
        TimingWindowTrigger[] nonTurnPlayer = ordered
            .Where(trigger => IsNonTurnPlayer(trigger.Request.ControllerId, turnPlayerId, nonTurnPlayerId))
            .ToArray();

        return TimingPriorityOrderResult.Success(
            ordered,
            mandatory,
            optional,
            turnPlayer,
            nonTurnPlayer,
            unknown,
            Values(turnPlayerId, nonTurnPlayerId, ordered, mandatory, optional, turnPlayer, nonTurnPlayer, unknown));
    }

    public static TimingPriorityOrderResult OrderAndEnqueueMandatory(
        IEnumerable<TimingWindowTrigger> triggers,
        EffectScheduler scheduler,
        HeadlessPlayerId turnPlayerId,
        HeadlessPlayerId? nonTurnPlayerId = null)
    {
        ArgumentNullException.ThrowIfNull(scheduler);

        TimingPriorityOrderResult result = Order(triggers, turnPlayerId, nonTurnPlayerId);
        if (!result.IsSuccess)
        {
            return result;
        }

        foreach (TimingWindowTrigger trigger in result.MandatoryTriggers)
        {
            scheduler.Enqueue(trigger.Request, trigger.Mode);
        }

        return result with { EnqueuedMandatoryCount = result.MandatoryTriggers.Count };
    }

    public static IReadOnlyDictionary<string, object?> Values(
        HeadlessPlayerId turnPlayerId,
        HeadlessPlayerId? nonTurnPlayerId,
        IReadOnlyList<TimingWindowTrigger> ordered,
        IReadOnlyList<TimingWindowTrigger> mandatory,
        IReadOnlyList<TimingWindowTrigger> optional,
        IReadOnlyList<TimingWindowTrigger> turnPlayer,
        IReadOnlyList<TimingWindowTrigger> nonTurnPlayer,
        IReadOnlyList<TimingWindowTrigger> unknown)
    {
        return new ReadOnlyDictionary<string, object?>(
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [TurnPlayerIdKey] = turnPlayerId.Value,
                [NonTurnPlayerIdKey] = nonTurnPlayerId?.Value,
                [OrderedEffectIdsKey] = EffectIds(ordered),
                [MandatoryEffectIdsKey] = EffectIds(mandatory),
                [OptionalEffectIdsKey] = EffectIds(optional),
                [TurnPlayerEffectIdsKey] = EffectIds(turnPlayer),
                [NonTurnPlayerEffectIdsKey] = EffectIds(nonTurnPlayer),
                [UnknownPlayerEffectIdsKey] = EffectIds(unknown),
            });
    }

    private const int MandatoryKindOrder = 0;
    private const int OptionalKindOrder = 1;
    private const int TurnPlayerOrder = 0;
    private const int NonTurnPlayerOrder = 1;
    private const int UnknownPlayerOrder = 2;

    private static TimingPriorityValidationResult ValidateInputs(
        IEnumerable<TimingWindowTrigger> triggers,
        HeadlessPlayerId turnPlayerId,
        HeadlessPlayerId? nonTurnPlayerId)
    {
        if (turnPlayerId.IsEmpty)
        {
            return TimingPriorityValidationResult.Failure("Turn player id must not be empty.");
        }

        if (nonTurnPlayerId is { IsEmpty: true })
        {
            return TimingPriorityValidationResult.Failure("Non-turn player id must not be empty.");
        }

        if (nonTurnPlayerId is HeadlessPlayerId nonTurn && nonTurn == turnPlayerId)
        {
            return TimingPriorityValidationResult.Failure("Turn player and non-turn player ids must be different.");
        }

        if (triggers is null)
        {
            return TimingPriorityValidationResult.Failure("Timing priority trigger list must not be null.");
        }

        var snapshot = new List<TimingWindowTrigger>();
        foreach (TimingWindowTrigger? trigger in triggers)
        {
            if (trigger is null)
            {
                return TimingPriorityValidationResult.Failure("Timing priority trigger list must not contain null values.");
            }

            snapshot.Add(trigger);
        }

        return TimingPriorityValidationResult.Success(snapshot);
    }

    private static int KindOrder(TimingWindowTriggerKind kind)
    {
        return kind == TimingWindowTriggerKind.Mandatory
            ? MandatoryKindOrder
            : OptionalKindOrder;
    }

    private static int PlayerOrder(
        HeadlessPlayerId controllerId,
        HeadlessPlayerId turnPlayerId,
        HeadlessPlayerId? nonTurnPlayerId)
    {
        if (controllerId == turnPlayerId)
        {
            return TurnPlayerOrder;
        }

        if (nonTurnPlayerId is HeadlessPlayerId nonTurn)
        {
            return controllerId == nonTurn ? NonTurnPlayerOrder : UnknownPlayerOrder;
        }

        return NonTurnPlayerOrder;
    }

    private static bool IsNonTurnPlayer(
        HeadlessPlayerId controllerId,
        HeadlessPlayerId turnPlayerId,
        HeadlessPlayerId? nonTurnPlayerId)
    {
        if (controllerId == turnPlayerId)
        {
            return false;
        }

        return nonTurnPlayerId is null || controllerId == nonTurnPlayerId.Value;
    }

    private static string[] EffectIds(IEnumerable<TimingWindowTrigger> triggers)
    {
        return triggers.Select(trigger => trigger.Request.EffectId.Value).ToArray();
    }

    private sealed record TimingPriorityValidationResult(
        bool IsSuccess,
        IReadOnlyList<TimingWindowTrigger> Triggers,
        string FailureReason)
    {
        public static TimingPriorityValidationResult Success(
            IReadOnlyList<TimingWindowTrigger> triggers)
        {
            return new TimingPriorityValidationResult(true, triggers.ToArray(), string.Empty);
        }

        public static TimingPriorityValidationResult Failure(string failureReason)
        {
            return new TimingPriorityValidationResult(false, Array.Empty<TimingWindowTrigger>(), failureReason);
        }
    }

    private readonly record struct IndexedTrigger(
        TimingWindowTrigger Trigger,
        int KindOrder,
        int PlayerOrder,
        int InputIndex);
}
