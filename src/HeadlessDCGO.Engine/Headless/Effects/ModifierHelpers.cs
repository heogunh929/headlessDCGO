namespace HeadlessDCGO.Engine.Headless.Effects;

using System.Collections.ObjectModel;
using System.Globalization;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

public enum NumericModifierMetric
{
    Dp = 0,
    BaseDp = 1,
    PlayCost = 2,
    DigivolutionCost = 3,
    SecurityAttack = 4,
}

public enum NumericModifierMode
{
    Add = 0,
    Set = 1,
    InvertDelta = 2,
}

public sealed record NumericModifier
{
    public NumericModifier(
        string id,
        NumericModifierMetric metric,
        int value,
        NumericModifierMode mode = NumericModifierMode.Add,
        bool isUpDown = true,
        HeadlessEntityId? targetEntityId = null,
        bool requiresAvailabilityCheck = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!Enum.IsDefined(metric))
        {
            throw new ArgumentOutOfRangeException(nameof(metric), "Modifier metric must be known.");
        }

        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), "Modifier mode must be known.");
        }

        if (targetEntityId is { IsEmpty: true })
        {
            throw new ArgumentException("Modifier target id must not be empty.", nameof(targetEntityId));
        }

        Id = id.Trim();
        Metric = metric;
        Value = value;
        Mode = mode;
        IsUpDown = isUpDown;
        TargetEntityId = targetEntityId;
        RequiresAvailabilityCheck = requiresAvailabilityCheck;
    }

    public string Id { get; }

    public NumericModifierMetric Metric { get; }

    public int Value { get; }

    public NumericModifierMode Mode { get; }

    public bool IsUpDown { get; }

    public HeadlessEntityId? TargetEntityId { get; }

    public bool RequiresAvailabilityCheck { get; }

    public static NumericModifier Add(
        string id,
        NumericModifierMetric metric,
        int value,
        HeadlessEntityId? targetEntityId = null,
        bool requiresAvailabilityCheck = false)
    {
        return new NumericModifier(id, metric, value, targetEntityId: targetEntityId, requiresAvailabilityCheck: requiresAvailabilityCheck);
    }

    public static NumericModifier Set(
        string id,
        NumericModifierMetric metric,
        int value,
        HeadlessEntityId? targetEntityId = null,
        bool requiresAvailabilityCheck = false)
    {
        return new NumericModifier(id, metric, value, NumericModifierMode.Set, isUpDown: false, targetEntityId, requiresAvailabilityCheck);
    }

    public static NumericModifier InvertSecurityAttack(
        string id,
        int value,
        HeadlessEntityId? targetEntityId = null,
        bool requiresAvailabilityCheck = false)
    {
        return new NumericModifier(
            id,
            NumericModifierMetric.SecurityAttack,
            value,
            NumericModifierMode.InvertDelta,
            isUpDown: false,
            targetEntityId,
            requiresAvailabilityCheck);
    }
}

public sealed record NumericModifierRequest
{
    public NumericModifierRequest(
        NumericModifierMetric metric,
        int baseValue,
        IReadOnlyList<NumericModifier>? modifiers = null,
        HeadlessEntityId? targetEntityId = null,
        bool checkAvailability = false,
        bool canReduceValue = true,
        int minimumValue = int.MinValue)
    {
        if (!Enum.IsDefined(metric))
        {
            throw new ArgumentOutOfRangeException(nameof(metric), "Modifier metric must be known.");
        }

        if (targetEntityId is { IsEmpty: true })
        {
            throw new ArgumentException("Modifier request target id must not be empty.", nameof(targetEntityId));
        }

        Metric = metric;
        BaseValue = baseValue;
        Modifiers = Array.AsReadOnly((modifiers ?? Array.Empty<NumericModifier>()).ToArray());
        TargetEntityId = targetEntityId;
        CheckAvailability = checkAvailability;
        CanReduceValue = canReduceValue;
        MinimumValue = minimumValue;
    }

    public NumericModifierMetric Metric { get; }

    public int BaseValue { get; }

    public IReadOnlyList<NumericModifier> Modifiers { get; }

    public HeadlessEntityId? TargetEntityId { get; }

    public bool CheckAvailability { get; }

    public bool CanReduceValue { get; }

    public int MinimumValue { get; }
}

public sealed record NumericModifierResult
{
    private NumericModifierResult(
        int baseValue,
        int finalValue,
        int invertDelta,
        IReadOnlyList<string> appliedModifierIds,
        IReadOnlyList<string> skippedModifierIds,
        IReadOnlyDictionary<string, object?> values)
    {
        BaseValue = baseValue;
        FinalValue = finalValue;
        InvertDelta = invertDelta;
        AppliedModifierIds = Array.AsReadOnly(appliedModifierIds.ToArray());
        SkippedModifierIds = Array.AsReadOnly(skippedModifierIds.ToArray());
        Values = CopyValues(values);
    }

    public int BaseValue { get; }

    public int FinalValue { get; }

    public int InvertDelta { get; }

    public IReadOnlyList<string> AppliedModifierIds { get; }

    public IReadOnlyList<string> SkippedModifierIds { get; }

    public IReadOnlyDictionary<string, object?> Values { get; }

    public static NumericModifierResult Success(
        int baseValue,
        int finalValue,
        int invertDelta,
        IReadOnlyList<string> appliedModifierIds,
        IReadOnlyList<string> skippedModifierIds,
        IReadOnlyDictionary<string, object?> values)
    {
        return new NumericModifierResult(baseValue, finalValue, invertDelta, appliedModifierIds, skippedModifierIds, values);
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

public static class ModifierHelpers
{
    public const string NumericModifiersKey = "numericModifiers";
    public const string ModifierMetricKey = "metric";
    public const string ModifierValueKey = "value";
    public const string ModifierModeKey = "mode";
    public const string ModifierTargetEntityIdKey = "targetEntityId";
    public const string DpDeltaKey = "dpDelta";
    public const string BaseDpDeltaKey = "baseDpDelta";
    public const string PlayCostDeltaKey = PlayCostHelpers.PlayCostDeltaKey;
    public const string DigivolutionCostDeltaKey = DigivolutionCostHelpers.DigivolutionCostDeltaKey;
    public const string SecurityAttackDeltaKey = "securityAttackDelta";
    public const string SAttackDeltaKey = "sAttackDelta";
    public const string FixedDpKey = "fixedDp";
    public const string FixedBaseDpKey = "fixedBaseDp";
    public const string FixedSecurityAttackKey = "fixedSecurityAttack";
    public const string InvertSecurityAttackDeltaKey = "invertSecurityAttackDelta";

    public static NumericModifierResult Evaluate(NumericModifierRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        int current = Math.Max(request.MinimumValue, request.BaseValue);
        int invertDelta = 0;
        var appliedIds = new List<string>();
        var skippedIds = new List<string>();
        NumericModifier[] modifiers = request.Modifiers
            .Where(modifier => modifier.Metric == request.Metric)
            .OrderBy(modifier => ModifierOrder(modifier))
            .ThenBy(modifier => modifier.Id, StringComparer.Ordinal)
            .ToArray();

        foreach (NumericModifier modifier in modifiers)
        {
            if (!CanApply(modifier, request))
            {
                skippedIds.Add(modifier.Id);
                continue;
            }

            if (modifier.Mode == NumericModifierMode.InvertDelta)
            {
                invertDelta += modifier.Value;
                appliedIds.Add(modifier.Id);
                continue;
            }

            int nextValue = modifier.Mode == NumericModifierMode.Set
                ? modifier.Value
                : current + modifier.Value;
            if (modifier.IsUpDown && nextValue < current && !request.CanReduceValue)
            {
                skippedIds.Add(modifier.Id);
                continue;
            }

            current = Math.Max(request.MinimumValue, nextValue);
            appliedIds.Add(modifier.Id);
        }

        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["metric"] = request.Metric.ToString(),
            ["baseValue"] = request.BaseValue,
            ["finalValue"] = current,
            ["minimumValue"] = request.MinimumValue,
            ["invertDelta"] = invertDelta,
            ["checkAvailability"] = request.CheckAvailability,
            ["canReduceValue"] = request.CanReduceValue,
            ["modifierCount"] = request.Modifiers.Count,
            ["appliedModifierIds"] = appliedIds.ToArray(),
            ["skippedModifierIds"] = skippedIds.ToArray(),
        };

        if (request.TargetEntityId is HeadlessEntityId targetEntityId)
        {
            values["targetEntityId"] = targetEntityId.Value;
        }

        return NumericModifierResult.Success(
            request.BaseValue,
            current,
            invertDelta,
            appliedIds,
            skippedIds,
            values);
    }

    public static IReadOnlyList<NumericModifier> ReadModifiers(
        CardRecord? card = null,
        CardInstanceRecord? instance = null,
        CardInstanceState? state = null,
        IEnumerable<EffectRequest>? effectRequests = null)
    {
        var modifiers = new List<NumericModifier>();
        if (card is not null)
        {
            modifiers.AddRange(ReadModifiersFromValues(card.Metadata));
        }

        if (instance is not null)
        {
            modifiers.AddRange(ReadModifiersFromValues(instance.Metadata));
        }

        if (state is not null)
        {
            modifiers.AddRange(ReadModifiersFromValues(state.Modifiers));
        }

        if (effectRequests is not null)
        {
            foreach (EffectRequest request in effectRequests)
            {
                modifiers.AddRange(ReadModifiersFromValues(request.Context.Values, request.EffectId));
            }
        }

        return modifiers
            .OrderBy(ModifierOrder)
            .ThenBy(modifier => modifier.Id, StringComparer.Ordinal)
            .ToArray();
    }

    public static NumericModifierResult ResolveDp(int baseDp, IReadOnlyList<NumericModifier> modifiers, HeadlessEntityId? targetEntityId = null)
    {
        return Evaluate(new NumericModifierRequest(NumericModifierMetric.Dp, baseDp, modifiers, targetEntityId));
    }

    public static NumericModifierResult ResolvePlayCost(
        int baseCost,
        IReadOnlyList<NumericModifier> modifiers,
        bool checkAvailability = false,
        bool canReduceCost = true)
    {
        return Evaluate(new NumericModifierRequest(
            NumericModifierMetric.PlayCost,
            baseCost,
            modifiers,
            checkAvailability: checkAvailability,
            canReduceValue: canReduceCost,
            minimumValue: 0));
    }

    public static NumericModifierResult ResolveDigivolutionCost(
        int baseCost,
        IReadOnlyList<NumericModifier> modifiers,
        bool checkAvailability = false,
        bool canReduceCost = true)
    {
        return Evaluate(new NumericModifierRequest(
            NumericModifierMetric.DigivolutionCost,
            baseCost,
            modifiers,
            checkAvailability: checkAvailability,
            canReduceValue: canReduceCost,
            minimumValue: 0));
    }

    public static NumericModifierResult ResolveSecurityAttack(
        int baseSecurityAttack,
        IReadOnlyList<NumericModifier> modifiers,
        HeadlessEntityId? targetEntityId = null)
    {
        return Evaluate(new NumericModifierRequest(
            NumericModifierMetric.SecurityAttack,
            baseSecurityAttack,
            modifiers,
            targetEntityId,
            minimumValue: 0));
    }

    private static IEnumerable<NumericModifier> ReadModifiersFromValues(
        IReadOnlyDictionary<string, object?> values,
        HeadlessEntityId? effectId = null)
    {
        foreach (NumericModifier modifier in ReadSimpleModifiers(values, effectId))
        {
            yield return modifier;
        }

        if (!values.TryGetValue(NumericModifiersKey, out object? rawModifiers) || rawModifiers is null)
        {
            yield break;
        }

        foreach (object? rawModifier in FlattenObjects(rawModifiers))
        {
            if (TryReadModifier(rawModifier, effectId, out NumericModifier? modifier))
            {
                yield return modifier!;
            }
        }
    }

    private static IEnumerable<NumericModifier> ReadSimpleModifiers(
        IReadOnlyDictionary<string, object?> values,
        HeadlessEntityId? effectId)
    {
        if (TryReadInt(values, DpDeltaKey, out int dpDelta) && dpDelta != 0)
        {
            yield return NumericModifier.Add(IdFor(effectId, DpDeltaKey), NumericModifierMetric.Dp, dpDelta);
        }

        if (TryReadInt(values, BaseDpDeltaKey, out int baseDpDelta) && baseDpDelta != 0)
        {
            yield return NumericModifier.Add(IdFor(effectId, BaseDpDeltaKey), NumericModifierMetric.BaseDp, baseDpDelta);
        }

        if (TryReadInt(values, PlayCostDeltaKey, out int playCostDelta) && playCostDelta != 0)
        {
            yield return NumericModifier.Add(IdFor(effectId, PlayCostDeltaKey), NumericModifierMetric.PlayCost, playCostDelta);
        }

        if (TryReadInt(values, DigivolutionCostDeltaKey, out int digivolutionCostDelta) && digivolutionCostDelta != 0)
        {
            yield return NumericModifier.Add(IdFor(effectId, DigivolutionCostDeltaKey), NumericModifierMetric.DigivolutionCost, digivolutionCostDelta);
        }

        if (TryReadInt(values, SecurityAttackDeltaKey, out int securityAttackDelta) && securityAttackDelta != 0)
        {
            yield return NumericModifier.Add(IdFor(effectId, SecurityAttackDeltaKey), NumericModifierMetric.SecurityAttack, securityAttackDelta);
        }
        else if (TryReadInt(values, SAttackDeltaKey, out int sAttackDelta) && sAttackDelta != 0)
        {
            yield return NumericModifier.Add(IdFor(effectId, SAttackDeltaKey), NumericModifierMetric.SecurityAttack, sAttackDelta);
        }

        if (TryReadInt(values, FixedDpKey, out int fixedDp))
        {
            yield return NumericModifier.Set(IdFor(effectId, FixedDpKey), NumericModifierMetric.Dp, fixedDp);
        }

        if (TryReadInt(values, FixedBaseDpKey, out int fixedBaseDp))
        {
            yield return NumericModifier.Set(IdFor(effectId, FixedBaseDpKey), NumericModifierMetric.BaseDp, fixedBaseDp);
        }

        if (TryReadInt(values, FixedSecurityAttackKey, out int fixedSecurityAttack))
        {
            yield return NumericModifier.Set(IdFor(effectId, FixedSecurityAttackKey), NumericModifierMetric.SecurityAttack, fixedSecurityAttack);
        }

        if (TryReadInt(values, InvertSecurityAttackDeltaKey, out int invertDelta) && invertDelta != 0)
        {
            yield return NumericModifier.InvertSecurityAttack(IdFor(effectId, InvertSecurityAttackDeltaKey), invertDelta);
        }
    }

    private static bool TryReadModifier(
        object? rawModifier,
        HeadlessEntityId? effectId,
        out NumericModifier? modifier)
    {
        modifier = null;
        if (rawModifier is NumericModifier typed)
        {
            modifier = typed;
            return true;
        }

        if (rawModifier is not IReadOnlyDictionary<string, object?> values ||
            !TryReadInt(values, ModifierValueKey, out int value))
        {
            return false;
        }

        if (!TryReadEnum(values, ModifierMetricKey, NumericModifierMetric.Dp, out NumericModifierMetric metric))
        {
            return false;
        }

        NumericModifierMode mode = TryReadEnum(values, ModifierModeKey, NumericModifierMode.Add, out NumericModifierMode parsedMode)
            ? parsedMode
            : NumericModifierMode.Add;
        bool isUpDown = !TryReadBool(values, "isUpDown", out bool parsedUpDown) || parsedUpDown;
        bool requiresAvailability = TryReadBool(values, "requiresAvailabilityCheck", out bool parsedAvailability) && parsedAvailability;
        HeadlessEntityId? targetEntityId = TryReadEntityId(values, ModifierTargetEntityIdKey, out HeadlessEntityId parsedTarget)
            ? parsedTarget
            : null;
        string id = TryReadString(values, "id", out string? parsedId)
            ? parsedId!
            : IdFor(effectId, $"{metric}-{mode}-{value.ToString(CultureInfo.InvariantCulture)}");

        modifier = new NumericModifier(id, metric, value, mode, isUpDown, targetEntityId, requiresAvailability);
        return true;
    }

    private static bool CanApply(NumericModifier modifier, NumericModifierRequest request)
    {
        if (modifier.RequiresAvailabilityCheck && !request.CheckAvailability)
        {
            return false;
        }

        return modifier.TargetEntityId is null ||
            request.TargetEntityId is HeadlessEntityId targetEntityId &&
            modifier.TargetEntityId.GetValueOrDefault() == targetEntityId;
    }

    private static int ModifierOrder(NumericModifier modifier)
    {
        return modifier.Mode switch
        {
            NumericModifierMode.Set => 0,
            NumericModifierMode.Add => modifier.IsUpDown ? 2 : 1,
            NumericModifierMode.InvertDelta => 3,
            _ => 9,
        };
    }

    private static IEnumerable<object?> FlattenObjects(object raw)
    {
        if (raw is string)
        {
            yield return raw;
            yield break;
        }

        if (raw is System.Collections.IEnumerable values)
        {
            foreach (object? value in values)
            {
                yield return value;
            }

            yield break;
        }

        yield return raw;
    }

    private static string IdFor(HeadlessEntityId? effectId, string fallback)
    {
        return effectId is HeadlessEntityId id ? $"{id.Value}:{fallback}" : fallback;
    }

    private static bool TryReadInt(IReadOnlyDictionary<string, object?> values, string key, out int value)
    {
        value = 0;
        if (!values.TryGetValue(key, out object? raw) || raw is null)
        {
            return false;
        }

        return raw switch
        {
            int intValue => SetInt(intValue, out value),
            long longValue when longValue >= int.MinValue && longValue <= int.MaxValue => SetInt((int)longValue, out value),
            double doubleValue when doubleValue >= int.MinValue && doubleValue <= int.MaxValue && doubleValue % 1 == 0 => SetInt((int)doubleValue, out value),
            string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) => SetInt(parsed, out value),
            _ => false,
        };
    }

    private static bool TryReadString(IReadOnlyDictionary<string, object?> values, string key, out string? value)
    {
        value = null;
        if (!values.TryGetValue(key, out object? raw) || raw is null)
        {
            return false;
        }

        string? parsed = raw switch
        {
            string stringValue => stringValue,
            HeadlessEntityId entityId => entityId.Value,
            _ => raw.ToString(),
        };
        if (string.IsNullOrWhiteSpace(parsed))
        {
            return false;
        }

        value = parsed.Trim();
        return true;
    }

    private static bool TryReadBool(IReadOnlyDictionary<string, object?> values, string key, out bool value)
    {
        value = false;
        if (!values.TryGetValue(key, out object? raw) || raw is null)
        {
            return false;
        }

        return raw switch
        {
            bool boolValue => SetBool(boolValue, out value),
            string text when bool.TryParse(text, out bool parsed) => SetBool(parsed, out value),
            _ => false,
        };
    }

    private static bool TryReadEntityId(IReadOnlyDictionary<string, object?> values, string key, out HeadlessEntityId value)
    {
        value = default;
        if (!TryReadString(values, key, out string? text))
        {
            return false;
        }

        value = new HeadlessEntityId(text!);
        return !value.IsEmpty;
    }

    private static bool TryReadEnum<TEnum>(
        IReadOnlyDictionary<string, object?> values,
        string key,
        TEnum fallback,
        out TEnum value)
        where TEnum : struct, Enum
    {
        value = fallback;
        if (!values.TryGetValue(key, out object? raw) || raw is null)
        {
            return false;
        }

        if (raw is TEnum enumValue)
        {
            value = enumValue;
            return true;
        }

        if (raw is string text && Enum.TryParse(text, ignoreCase: true, out TEnum parsed))
        {
            value = parsed;
            return true;
        }

        return false;
    }

    private static bool SetInt(int input, out int output)
    {
        output = input;
        return true;
    }

    private static bool SetBool(bool input, out bool output)
    {
        output = input;
        return true;
    }
}
