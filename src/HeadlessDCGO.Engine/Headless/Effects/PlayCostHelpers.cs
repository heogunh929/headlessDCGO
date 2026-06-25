namespace HeadlessDCGO.Engine.Headless.Effects;

using System.Collections.ObjectModel;
using System.Globalization;
using HeadlessDCGO.Engine.Headless.Services;

public enum PlayCostModifierStage
{
    CostItself = 0,
    PayingCost = 1,
}

public enum PlayCostModifierMode
{
    Add = 0,
    Set = 1,
}

public enum PlayCostRoot
{
    None = 0,
    Hand = 1,
    Trash = 2,
    Library = 3,
    Security = 4,
    Source = 5,
    Link = 6,
}

public sealed record PlayCostModifier
{
    public PlayCostModifier(
        string id,
        int value,
        PlayCostModifierStage stage,
        PlayCostModifierMode mode = PlayCostModifierMode.Add,
        bool isUpDown = true,
        bool requiresAvailabilityCheck = false,
        IReadOnlyList<PlayCostRoot>? allowedRoots = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!Enum.IsDefined(stage))
        {
            throw new ArgumentOutOfRangeException(nameof(stage), "Play cost modifier stage must be known.");
        }

        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), "Play cost modifier mode must be known.");
        }

        Id = id.Trim();
        Value = value;
        Stage = stage;
        Mode = mode;
        IsUpDown = isUpDown;
        RequiresAvailabilityCheck = requiresAvailabilityCheck;
        AllowedRoots = CopyRoots(allowedRoots);
    }

    public string Id { get; }

    public int Value { get; }

    public PlayCostModifierStage Stage { get; }

    public PlayCostModifierMode Mode { get; }

    public bool IsUpDown { get; }

    public bool RequiresAvailabilityCheck { get; }

    public IReadOnlyList<PlayCostRoot> AllowedRoots { get; }

    public static PlayCostModifier AddToCost(
        string id,
        int value,
        bool requiresAvailabilityCheck = false)
    {
        return new PlayCostModifier(id, value, PlayCostModifierStage.CostItself, requiresAvailabilityCheck: requiresAvailabilityCheck);
    }

    public static PlayCostModifier SetCost(
        string id,
        int value,
        bool requiresAvailabilityCheck = false)
    {
        return new PlayCostModifier(
            id,
            value,
            PlayCostModifierStage.CostItself,
            PlayCostModifierMode.Set,
            isUpDown: false,
            requiresAvailabilityCheck);
    }

    public static PlayCostModifier AddToPayingCost(
        string id,
        int value,
        bool requiresAvailabilityCheck = false)
    {
        return new PlayCostModifier(id, value, PlayCostModifierStage.PayingCost, requiresAvailabilityCheck: requiresAvailabilityCheck);
    }

    private static IReadOnlyList<PlayCostRoot> CopyRoots(IReadOnlyList<PlayCostRoot>? roots)
    {
        if (roots is null || roots.Count == 0)
        {
            return Array.AsReadOnly(Array.Empty<PlayCostRoot>());
        }

        PlayCostRoot[] copy = roots.ToArray();
        if (copy.Any(root => !Enum.IsDefined(root)))
        {
            throw new ArgumentOutOfRangeException(nameof(roots), "Allowed play cost roots must be known.");
        }

        return Array.AsReadOnly(copy.Distinct().OrderBy(root => root).ToArray());
    }
}

public sealed record PlayCostRequest
{
    public PlayCostRequest(
        CardRecord card,
        CardInstanceRecord? instance = null,
        IReadOnlyList<PlayCostModifier>? modifiers = null,
        int? fixedCost = null,
        PlayCostRoot root = PlayCostRoot.None,
        bool checkAvailability = false,
        bool canReduceCost = true,
        int? availableMemory = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (fixedCost < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fixedCost), "Fixed play cost must not be negative.");
        }

        if (!Enum.IsDefined(root))
        {
            throw new ArgumentOutOfRangeException(nameof(root), "Play cost root must be known.");
        }

        if (availableMemory < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(availableMemory), "Available memory must not be negative.");
        }

        Card = card;
        Instance = instance;
        Modifiers = CopyModifiers(modifiers);
        FixedCost = fixedCost;
        Root = root;
        CheckAvailability = checkAvailability;
        CanReduceCost = canReduceCost;
        AvailableMemory = availableMemory;
    }

    public CardRecord Card { get; }

    public CardInstanceRecord? Instance { get; }

    public IReadOnlyList<PlayCostModifier> Modifiers { get; }

    public int? FixedCost { get; }

    public PlayCostRoot Root { get; }

    public bool CheckAvailability { get; }

    public bool CanReduceCost { get; }

    public int? AvailableMemory { get; }

    private static IReadOnlyList<PlayCostModifier> CopyModifiers(IReadOnlyList<PlayCostModifier>? modifiers)
    {
        return Array.AsReadOnly((modifiers ?? Array.Empty<PlayCostModifier>()).ToArray());
    }
}

public sealed record PlayCostResult
{
    private PlayCostResult(
        bool isSuccess,
        int cost,
        bool canPay,
        string reason,
        IReadOnlyDictionary<string, object?> values)
    {
        IsSuccess = isSuccess;
        Cost = cost;
        CanPay = canPay;
        Reason = reason;
        Values = CopyValues(values);
    }

    public bool IsSuccess { get; }

    public int Cost { get; }

    public bool CanPay { get; }

    public string Reason { get; }

    public IReadOnlyDictionary<string, object?> Values { get; }

    public static PlayCostResult Success(
        int cost,
        bool canPay,
        string reason,
        IReadOnlyDictionary<string, object?> values)
    {
        return new PlayCostResult(true, cost, canPay, reason, values);
    }

    public static PlayCostResult Failure(
        string reason,
        IReadOnlyDictionary<string, object?> values)
    {
        return new PlayCostResult(false, 0, false, reason, values);
    }

    private static IReadOnlyDictionary<string, object?> CopyValues(IReadOnlyDictionary<string, object?> values)
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

public static class PlayCostHelpers
{
    public const string FixedPlayCostKey = "fixedPlayCost";
    public const string PlayCostDeltaKey = "playCostDelta";
    public const string PayingCostDeltaKey = "payingCostDelta";
    public const string PlayCostModifiersKey = "playCostModifiers";

    public static PlayCostResult Evaluate(PlayCostRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.Card.PlayCost.HasValue && !request.FixedCost.HasValue)
        {
            return PlayCostResult.Failure(
                $"Card definition '{request.Card.Id}' has no play cost.",
                BaseValues(request));
        }

        int baseCost = request.FixedCost ?? request.Card.PlayCost.GetValueOrDefault();
        int costItself = ApplyModifiers(
            baseCost,
            request,
            PlayCostModifierStage.CostItself,
            out string[] costModifierIds,
            out string[] skippedCostModifierIds);
        int payingCost = ApplyModifiers(
            costItself,
            request,
            PlayCostModifierStage.PayingCost,
            out string[] payingModifierIds,
            out string[] skippedPayingModifierIds);
        int finalCost = Math.Max(0, payingCost);
        bool canPay = !request.AvailableMemory.HasValue || finalCost <= request.AvailableMemory.Value;

        var values = BaseValues(request);
        values["baseCost"] = baseCost;
        values["costItself"] = costItself;
        values["payingCost"] = finalCost;
        values["canPay"] = canPay;
        values["appliedCostModifierIds"] = costModifierIds;
        values["appliedPayingCostModifierIds"] = payingModifierIds;
        values["skippedCostModifierIds"] = skippedCostModifierIds;
        values["skippedPayingCostModifierIds"] = skippedPayingModifierIds;

        return PlayCostResult.Success(
            finalCost,
            canPay,
            canPay ? "Play cost resolved." : "Resolved play cost exceeds available memory.",
            values);
    }

    public static bool TryResolveCost(
        CardRecord card,
        CardInstanceRecord? instance,
        out int playCost,
        out string? error)
    {
        PlayCostResult result = Evaluate(new PlayCostRequest(
            card,
            instance,
            ReadModifiers(card, instance),
            fixedCost: ReadFixedCost(card, instance)));
        playCost = result.Cost;
        error = result.IsSuccess ? null : result.Reason;
        return result.IsSuccess;
    }

    public static IReadOnlyList<PlayCostModifier> ReadModifiers(
        CardRecord card,
        CardInstanceRecord? instance = null)
    {
        ArgumentNullException.ThrowIfNull(card);

        var modifiers = new List<PlayCostModifier>();
        modifiers.AddRange(ReadModifiersFromMetadata(card.Metadata));
        if (instance is not null)
        {
            modifiers.AddRange(ReadModifiersFromMetadata(instance.Metadata));
        }

        return modifiers
            .OrderBy(modifier => modifier.Stage)
            .ThenBy(modifier => modifier.IsUpDown)
            .ThenBy(modifier => modifier.Id, StringComparer.Ordinal)
            .ToArray();
    }

    public static int? ReadFixedCost(
        CardRecord card,
        CardInstanceRecord? instance = null)
    {
        ArgumentNullException.ThrowIfNull(card);

        if (instance is not null && TryReadInt(instance.Metadata, FixedPlayCostKey, out int instanceFixed))
        {
            return instanceFixed;
        }

        return TryReadInt(card.Metadata, FixedPlayCostKey, out int cardFixed)
            ? cardFixed
            : null;
    }

    private static int ApplyModifiers(
        int startingCost,
        PlayCostRequest request,
        PlayCostModifierStage stage,
        out string[] appliedIds,
        out string[] skippedIds)
    {
        int cost = startingCost;
        var applied = new List<string>();
        var skipped = new List<string>();
        PlayCostModifier[] stageModifiers = request.Modifiers
            .Where(modifier => modifier.Stage == stage)
            .OrderBy(modifier => modifier.IsUpDown)
            .ThenBy(modifier => modifier.Id, StringComparer.Ordinal)
            .ToArray();

        foreach (PlayCostModifier modifier in stageModifiers)
        {
            if (!CanApply(modifier, request))
            {
                skipped.Add(modifier.Id);
                continue;
            }

            int nextCost = modifier.Mode == PlayCostModifierMode.Set
                ? modifier.Value
                : cost + modifier.Value;
            if (modifier.IsUpDown && nextCost < cost && !request.CanReduceCost)
            {
                skipped.Add(modifier.Id);
                continue;
            }

            cost = Math.Max(0, nextCost);
            applied.Add(modifier.Id);
        }

        appliedIds = applied.ToArray();
        skippedIds = skipped.ToArray();
        return Math.Max(0, cost);
    }

    private static bool CanApply(
        PlayCostModifier modifier,
        PlayCostRequest request)
    {
        if (modifier.RequiresAvailabilityCheck && !request.CheckAvailability)
        {
            return false;
        }

        return modifier.AllowedRoots.Count == 0 || modifier.AllowedRoots.Contains(request.Root);
    }

    private static Dictionary<string, object?> BaseValues(PlayCostRequest request)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["cardDefinitionId"] = request.Card.Id.Value,
            ["root"] = request.Root.ToString(),
            ["checkAvailability"] = request.CheckAvailability,
            ["canReduceCost"] = request.CanReduceCost,
            ["modifierCount"] = request.Modifiers.Count,
        };

        if (request.Instance is not null)
        {
            values["cardInstanceId"] = request.Instance.InstanceId.Value;
            values["ownerId"] = request.Instance.OwnerId.Value;
        }

        if (request.FixedCost.HasValue)
        {
            values["fixedCost"] = request.FixedCost.Value;
        }

        if (request.AvailableMemory.HasValue)
        {
            values["availableMemory"] = request.AvailableMemory.Value;
        }

        return values;
    }

    private static IEnumerable<PlayCostModifier> ReadModifiersFromMetadata(
        IReadOnlyDictionary<string, object?> metadata)
    {
        if (TryReadInt(metadata, PlayCostDeltaKey, out int costDelta) && costDelta != 0)
        {
            yield return PlayCostModifier.AddToCost(PlayCostDeltaKey, costDelta);
        }

        if (TryReadInt(metadata, PayingCostDeltaKey, out int payingCostDelta) && payingCostDelta != 0)
        {
            yield return PlayCostModifier.AddToPayingCost(PayingCostDeltaKey, payingCostDelta);
        }

        if (!metadata.TryGetValue(PlayCostModifiersKey, out object? rawModifiers) || rawModifiers is null)
        {
            yield break;
        }

        foreach (object? rawModifier in FlattenObjects(rawModifiers))
        {
            if (TryReadModifier(rawModifier, out PlayCostModifier? modifier))
            {
                yield return modifier!;
            }
        }
    }

    private static bool TryReadModifier(
        object? rawModifier,
        out PlayCostModifier? modifier)
    {
        modifier = null;
        if (rawModifier is PlayCostModifier typed)
        {
            modifier = typed;
            return true;
        }

        if (rawModifier is not IReadOnlyDictionary<string, object?> dictionary)
        {
            return false;
        }

        string id = TryReadString(dictionary, "id", out string? parsedId)
            ? parsedId
            : $"metadata-{PlayCostModifiersKey}";
        if (!TryReadInt(dictionary, "value", out int value))
        {
            return false;
        }

        PlayCostModifierStage stage = TryReadEnum(dictionary, "stage", PlayCostModifierStage.CostItself, out PlayCostModifierStage parsedStage)
            ? parsedStage
            : PlayCostModifierStage.CostItself;
        PlayCostModifierMode mode = TryReadEnum(dictionary, "mode", PlayCostModifierMode.Add, out PlayCostModifierMode parsedMode)
            ? parsedMode
            : PlayCostModifierMode.Add;
        bool isUpDown = !TryReadBool(dictionary, "isUpDown", out bool parsedUpDown) || parsedUpDown;
        bool requiresAvailability = TryReadBool(dictionary, "requiresAvailabilityCheck", out bool parsedAvailability) && parsedAvailability;

        modifier = new PlayCostModifier(id, value, stage, mode, isUpDown, requiresAvailability);
        return true;
    }

    private static IEnumerable<object?> FlattenObjects(object raw)
    {
        if (raw is IEnumerable<PlayCostModifier> typedModifiers)
        {
            foreach (PlayCostModifier modifier in typedModifiers)
            {
                yield return modifier;
            }

            yield break;
        }

        if (raw is IEnumerable<IReadOnlyDictionary<string, object?>> dictionaries)
        {
            foreach (IReadOnlyDictionary<string, object?> dictionary in dictionaries)
            {
                yield return dictionary;
            }

            yield break;
        }

        yield return raw;
    }

    private static bool TryReadInt(
        IReadOnlyDictionary<string, object?> values,
        string key,
        out int value)
    {
        value = 0;
        if (!values.TryGetValue(key, out object? raw) || raw is null)
        {
            return false;
        }

        return raw switch
        {
            int intValue when intValue >= 0 || key != FixedPlayCostKey => Assign(intValue, out value),
            long longValue when longValue >= int.MinValue && longValue <= int.MaxValue && (longValue >= 0 || key != FixedPlayCostKey) => Assign((int)longValue, out value),
            double doubleValue when doubleValue >= int.MinValue && doubleValue <= int.MaxValue && doubleValue % 1 == 0 && (doubleValue >= 0 || key != FixedPlayCostKey) => Assign((int)doubleValue, out value),
            string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) && (parsed >= 0 || key != FixedPlayCostKey) => Assign(parsed, out value),
            _ => false
        };
    }

    private static bool TryReadString(
        IReadOnlyDictionary<string, object?> values,
        string key,
        out string value)
    {
        value = string.Empty;
        if (!values.TryGetValue(key, out object? raw) || raw is null || string.IsNullOrWhiteSpace(raw.ToString()))
        {
            return false;
        }

        value = raw.ToString()!.Trim();
        return true;
    }

    private static bool TryReadBool(
        IReadOnlyDictionary<string, object?> values,
        string key,
        out bool value)
    {
        value = false;
        if (!values.TryGetValue(key, out object? raw) || raw is null)
        {
            return false;
        }

        return raw switch
        {
            bool boolValue => Assign(boolValue, out value),
            string text when bool.TryParse(text, out bool parsed) => Assign(parsed, out value),
            _ => false
        };
    }

    private static bool TryReadEnum<T>(
        IReadOnlyDictionary<string, object?> values,
        string key,
        T defaultValue,
        out T value)
        where T : struct, Enum
    {
        value = defaultValue;
        if (!values.TryGetValue(key, out object? raw) || raw is null)
        {
            return false;
        }

        if (raw is T typed)
        {
            value = typed;
            return true;
        }

        return Enum.TryParse(raw.ToString(), ignoreCase: true, out value);
    }

    private static bool Assign<T>(T input, out T output)
    {
        output = input;
        return true;
    }
}
