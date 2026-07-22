namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections.ObjectModel;
using System.Globalization;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;
using HeadlessDCGO.Engine.Headless.Effects;

public enum NumericModifierMetric
{
    Dp = 0,
    BaseDp = 1,
    PlayCost = 2,
    DigivolutionCost = 3,
    SecurityAttack = 4,
    LinkedMax = 5,
    LinkCost = 6,
}

public enum NumericModifierMode
{
    Add = 0,
    Set = 1,
    InvertDelta = 2,
}

/// <summary>
/// Mirror of AS-IS <c>CalculateOrder</c> (ICardEffect.cs). AS-IS orders the "Change Security Attack" and
/// "Change Link Max" effects into three tiers applied strictly in this sequence — UpToConstant, then
/// UpDownValue, then DownToConstant (Permanent.cs:1872-1930 for SAttack, 975-1000 for LinkMax) — while the DP
/// path uses a separate boolean isUpDown 2-group split (not this enum). Only the three tiers above are bucketed
/// by the AS-IS switch; <see cref="UpValue"/>/<see cref="DownValue"/> have no switch case and are therefore
/// collected-but-never-applied (dropped). Every current SAttack/LinkMax producer emits <see cref="UpDownValue"/>
/// (both factories hardcode it), so this tiering is behaviourally inert for additive deltas today; it exists so
/// a future non-additive (cap-style) port can set its tier and fold in the correct order.
/// </summary>
public enum CalculateOrder
{
    UpValue = 0,
    DownValue = 1,
    UpToConstant = 2,
    UpDownValue = 3,
    DownToConstant = 4,
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
        bool requiresAvailabilityCheck = false,
        HeadlessEntityId? sourceEntityId = null,
        CalculateOrder calcOrder = CalculateOrder.UpDownValue,
        long activationOrder = 0)
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

        if (!Enum.IsDefined(calcOrder))
        {
            throw new ArgumentOutOfRangeException(nameof(calcOrder), "Modifier calc order must be known.");
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
        SourceEntityId = sourceEntityId;
        CalcOrder = calcOrder;
        ActivationOrder = activationOrder;
    }

    public string Id { get; }

    public NumericModifierMetric Metric { get; }

    public int Value { get; }

    public NumericModifierMode Mode { get; }

    public bool IsUpDown { get; }

    public HeadlessEntityId? TargetEntityId { get; }

    public bool RequiresAvailabilityCheck { get; }

    /// <summary>The instance whose continuous effect emitted this modifier (the "causing effect" source). Used
    /// by DP-reduction immunity to honour a per-causing-effect predicate (AS-IS ImmuneFromDPMinus(permanent,
    /// cardEffect)). Null for modifiers read off instance/card metadata (no distinct source effect).</summary>
    public HeadlessEntityId? SourceEntityId { get; init; }

    /// <summary>AS-IS <c>isUpDown()</c>-&gt;<see cref="CalculateOrder"/> tier. Governs the fold order for the
    /// SecurityAttack and LinkedMax metrics ONLY (their AS-IS 3-tier switch); ignored for DP/Cost metrics, which
    /// order by the boolean <see cref="IsUpDown"/> / <see cref="Mode"/> instead. Defaults to the universal
    /// producer value <see cref="CalculateOrder.UpDownValue"/>.</summary>
    public CalculateOrder CalcOrder { get; init; }

    /// <summary>AS-IS <c>ICardEffect.ActivatedTime</c> (default <c>DateTime.MinValue</c>) mapped to a deterministic
    /// monotonic order. AS-IS applies the DP NotIsUpDown/"set" group in <c>OrderBy(ActivatedTime)</c> — the LATEST
    /// activated "DP becomes X" wins (Permanent.cs:301/472). Consulted ONLY for the DP/BaseDp NotIsUpDown group
    /// (the sole AS-IS ActivatedTime consumer); everywhere else the value is inert. Defaults to 0 (the MinValue
    /// analog); an explicitly later-activated set-DP supplies a higher value to win. Same convention as
    /// <see cref="HeadlessDCGO.Engine.Headless.State.DpModifier.ActivatedOrder"/> on the static-DP path.</summary>
    public long ActivationOrder { get; init; }

    public static NumericModifier Add(
        string id,
        NumericModifierMetric metric,
        int value,
        HeadlessEntityId? targetEntityId = null,
        bool requiresAvailabilityCheck = false,
        HeadlessEntityId? sourceEntityId = null,
        CalculateOrder calcOrder = CalculateOrder.UpDownValue,
        long activationOrder = 0)
    {
        return new NumericModifier(id, metric, value, targetEntityId: targetEntityId, requiresAvailabilityCheck: requiresAvailabilityCheck, sourceEntityId: sourceEntityId, calcOrder: calcOrder, activationOrder: activationOrder);
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
    // (SAttack 3-tier) optional per-modifier CalculateOrder tier for SecurityAttack/LinkedMax structured
    // modifiers. Absent → UpDownValue (the universal AS-IS producer value; both AS-IS factories hardcode it).
    public const string ModifierCalcOrderKey = "calcOrder";
    // (ActivatedTime) optional per-modifier AS-IS ActivatedTime analog (monotonic long). Consulted only for the
    // DP NotIsUpDown/set group — the latest-activated "DP becomes X" wins. Absent → 0 (MinValue analog). Same
    // key name as the static-DP path's DpModifier activatedOrder (MatchStateMutationSink.DpActivatedOrderKey).
    public const string ModifierActivationOrderKey = "activatedOrder";
    public const string DpDeltaKey = "dpDelta";
    public const string BaseDpDeltaKey = "baseDpDelta";
    public const string PlayCostDeltaKey = PlayCostHelpers.PlayCostDeltaKey;
    public const string DigivolutionCostDeltaKey = DigivolutionCostHelpers.DigivolutionCostDeltaKey;
    public const string SecurityAttackDeltaKey = "securityAttackDelta";
    public const string SAttackDeltaKey = "sAttackDelta";
    // (PRIM-W3) continuous link modifiers. Registered as continuous-role deltas queryable via
    // ContinuousModifierGate; the link subsystem consumers (LinkHelpers.EnforceLinkedMaxAsync /
    // LinkSelfEffect cost) migrate to consult these separately — grant is live, behavior-consumer latent.
    public const string LinkedMaxDeltaKey = "linkedMaxDelta";
    public const string LinkCostDeltaKey = "linkCostDelta";
    // (P1-DP-5) synthetic modifier id for the host's accumulated LinkedDP (AS-IS Permanent.LinkedDP), injected by
    // ContinuousDpGate so it folds between the isUpDown and NotIsUpDown DP groups (see ModifierOrder).
    public const string LinkedDpModifierId = "__linkedDp";
    public const string FixedDpKey = "fixedDp";
    public const string FixedBaseDpKey = "fixedBaseDp";
    public const string FixedSecurityAttackKey = "fixedSecurityAttack";
    public const string InvertSecurityAttackDeltaKey = "invertSecurityAttackDelta";
    // (d-remediation) AS-IS IChangeEndTurnMinMemoryEffect / AutoProcessing.TurnEndMinMemory — the memory the
    // opponent must reach for the turn to auto-end (default 1). BT14_081/BT17_069 SET it to 3. Not a numeric
    // metric (no derived NumericModifier); read as a raw value by the turn-pass gate.
    public const string EndTurnMinMemoryKey = "endTurnMinMemory";

    public static NumericModifierResult Evaluate(NumericModifierRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        int current = Math.Max(request.MinimumValue, request.BaseValue);
        var appliedIds = new List<string>();
        var skippedIds = new List<string>();
        NumericModifier[] modifiers = request.Modifiers
            .Where(modifier => modifier.Metric == request.Metric)
            .OrderBy(modifier => ModifierOrder(modifier))
            .ThenBy(modifier => ActivationTieBreak(modifier))
            .ThenBy(modifier => modifier.Id, StringComparer.Ordinal)
            .ToArray();

        // (b-remediation) AS-IS Permanent.InvertSecutiryValue: sum every applicable invert modifier and CLAMP to
        // [-1, 1] — this global inversion FLIPS the direction of each security-attack change (a decrease becomes an
        // equal increase and vice versa; AS-IS ChangeSAttackClass.GetSAttack). Previously the invert deltas were
        // accumulated but never applied (dead). Non-SecurityAttack metrics have no invert modifiers, so this is 0.
        int invertDelta = modifiers
            .Where(modifier => modifier.Mode == NumericModifierMode.InvertDelta && CanApply(modifier, request))
            .Sum(modifier => modifier.Value);
        int invertValue = Math.Clamp(invertDelta, -1, 1);

        foreach (NumericModifier modifier in modifiers)
        {
            if (!CanApply(modifier, request))
            {
                skippedIds.Add(modifier.Id);
                continue;
            }

            // (SAttack 3-tier) AS-IS's CalculateOrder switch has no UpValue/DownValue case for SAttack/LinkedMax,
            // so such an effect is collected but never folded — mirror the drop.
            if (IsDroppedByCalcOrder(modifier))
            {
                skippedIds.Add(modifier.Id);
                continue;
            }

            if (modifier.Mode == NumericModifierMode.InvertDelta)
            {
                appliedIds.Add(modifier.Id);   // already folded into invertValue above
                continue;
            }

            int nextValue = modifier.Mode == NumericModifierMode.Set
                ? modifier.Value
                : current + modifier.Value;

            // (b-remediation) apply the global inversion to this change (AS-IS GetSAttack invertValue switch):
            //   -1 → a decrease (nextValue < current) is flipped to the equal increase,
            //   +1 → an increase (nextValue > current) is flipped to the equal decrease.
            if (invertValue == -1 && nextValue < current)
            {
                nextValue = current + Math.Abs(nextValue - current);
            }
            else if (invertValue == 1 && nextValue > current)
            {
                nextValue = current - (nextValue - current);
            }

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
                // Tag each modifier with the emitting effect's source instance so DP-reduction immunity can
                // evaluate its causing-effect predicate per modifier (AS-IS ImmuneFromDPMinus(permanent, cardEffect)).
                HeadlessEntityId? src = request.Context.SourceEntityId is { IsEmpty: false } s ? s : null;
                foreach (NumericModifier modifier in ReadModifiersFromValues(request.Context.Values, request.EffectId))
                {
                    modifiers.Add(src is null ? modifier : modifier with { SourceEntityId = src });
                }
            }
        }

        return modifiers
            .OrderBy(ModifierOrder)
            .ThenBy(ActivationTieBreak)
            .ThenBy(modifier => modifier.Id, StringComparer.Ordinal)
            .ToArray();
    }

    // (RD-A6-02) ResolveDp/ResolvePlayCost/ResolveDigivolutionCost/ResolveSecurityAttack convenience wrappers
    // DELETED — zero src consumers (only ever called through ContinuousEvaluationResult's identically-named
    // instance methods, themselves zero-consumer and deleted alongside). Callers needing this fold now call
    // Evaluate(NumericModifierRequest) directly, the live surface (LinkHelpers.ResolveLinkedMax/ResolveLinkCost).

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

        // (M-4) link-subsystem deltas — ChangeLinkMax (linkedMax) / GrantedReduceLinkCost (linkCost). Previously
        // registered but emitted as no modifier and read by nothing; now folded by LinkHelpers.
        if (TryReadInt(values, LinkedMaxDeltaKey, out int linkedMaxDelta) && linkedMaxDelta != 0)
        {
            yield return NumericModifier.Add(IdFor(effectId, LinkedMaxDeltaKey), NumericModifierMetric.LinkedMax, linkedMaxDelta);
        }

        if (TryReadInt(values, LinkCostDeltaKey, out int linkCostDelta) && linkCostDelta != 0)
        {
            yield return NumericModifier.Add(IdFor(effectId, LinkCostDeltaKey), NumericModifierMetric.LinkCost, linkCostDelta);
        }

        if (TryReadInt(values, SecurityAttackDeltaKey, out int securityAttackDelta) && securityAttackDelta != 0)
        {
            yield return NumericModifier.Add(IdFor(effectId, SecurityAttackDeltaKey), NumericModifierMetric.SecurityAttack, securityAttackDelta);
        }
        else if (TryReadInt(values, SAttackDeltaKey, out int sAttackDelta) && sAttackDelta != 0)
        {
            yield return NumericModifier.Add(IdFor(effectId, SAttackDeltaKey), NumericModifierMetric.SecurityAttack, sAttackDelta);
        }

        // (ActivatedTime) a "DP becomes X" set can carry an activation order so the latest-activated set wins
        // among several (AS-IS Permanent.cs:301/472 OrderBy(ActivatedTime)); absent → 0.
        long setDpActivationOrder = TryReadLong(values, ModifierActivationOrderKey, out long parsedSetDpOrder) ? parsedSetDpOrder : 0;

        if (TryReadInt(values, FixedDpKey, out int fixedDp))
        {
            yield return NumericModifier.Set(IdFor(effectId, FixedDpKey), NumericModifierMetric.Dp, fixedDp) with { ActivationOrder = setDpActivationOrder };
        }

        if (TryReadInt(values, FixedBaseDpKey, out int fixedBaseDp))
        {
            yield return NumericModifier.Set(IdFor(effectId, FixedBaseDpKey), NumericModifierMetric.BaseDp, fixedBaseDp) with { ActivationOrder = setDpActivationOrder };
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
        CalculateOrder calcOrder = TryReadEnum(values, ModifierCalcOrderKey, CalculateOrder.UpDownValue, out CalculateOrder parsedCalcOrder)
            ? parsedCalcOrder
            : CalculateOrder.UpDownValue;
        long activationOrder = TryReadLong(values, ModifierActivationOrderKey, out long parsedActivation) ? parsedActivation : 0;
        string id = TryReadString(values, "id", out string? parsedId)
            ? parsedId!
            : IdFor(effectId, $"{metric}-{mode}-{value.ToString(CultureInfo.InvariantCulture)}");

        modifier = new NumericModifier(id, metric, value, mode, isUpDown, targetEntityId, requiresAvailability, calcOrder: calcOrder, activationOrder: activationOrder);
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
        // AS-IS orders each metric's "Change X" modifiers DIFFERENTLY (a single shared order cannot mirror all):
        //   - DP  (Permanent.DP / GetDP): isUpDown group FIRST, then NotIsUpDown/Set  (Permanent.cs:290,301).
        //   - Cost (CardSource cost):     NotIsUpDown/Set FIRST, then isUpDown         (CardSource.cs:848-852).
        //   - SecurityAttack / LinkedMax: UpToConstant → UpDownValue → DownToConstant  (Permanent.cs:1872-1930 /
        //                                 975-1000) — the 3-tier CalculateOrder switch, NOT the boolean isUpDown.
        // (P0-DP-2) Only DP was reversed; scope the isUpDown-first ordering to the DP metrics and keep the
        // Set-first ordering (which matches Cost) for play/digivolution cost.
        if (modifier.Metric is NumericModifierMetric.Dp or NumericModifierMetric.BaseDp)
        {
            // (P1-DP-5) AS-IS injects `DP += LinkedDP` BETWEEN the isUpDown group and the NotIsUpDown/Set group
            // (Permanent.cs:639). A later "DP becomes X" set therefore overwrites the linked DP, so LinkedDP must
            // sort strictly after isUpDown (0) and strictly before Set/notUpDown (2).
            if (modifier.Id == LinkedDpModifierId)
            {
                return 1;
            }

            return modifier.Mode == NumericModifierMode.InvertDelta ? 4 : (modifier.IsUpDown ? 0 : 2);
        }

        // (SAttack 3-tier) SecurityAttack and LinkedMax use the AS-IS CalculateOrder 3-tier switch. Invert
        // modifiers are consumed globally (pre-summed invertValue) not applied positionally, so they sort last.
        if (modifier.Metric is NumericModifierMetric.SecurityAttack or NumericModifierMetric.LinkedMax)
        {
            if (modifier.Mode == NumericModifierMode.InvertDelta)
            {
                return 9;
            }

            return modifier.CalcOrder switch
            {
                CalculateOrder.UpToConstant => 0,
                CalculateOrder.UpDownValue => 1,
                CalculateOrder.DownToConstant => 2,
                // UpValue/DownValue have no AS-IS switch case (dropped in Evaluate); rank them last for a stable sort.
                _ => 8,
            };
        }

        return modifier.Mode switch
        {
            NumericModifierMode.Set => 0,
            NumericModifierMode.Add => modifier.IsUpDown ? 2 : 1,
            NumericModifierMode.InvertDelta => 3,
            _ => 9,
        };
    }

    /// <summary>(ActivatedTime) AS-IS orders ONLY the DP NotIsUpDown/"set" group by <c>ActivatedTime</c>
    /// (Permanent.cs:301/472) — the isUpDown group, SAttack, LinkedMax and cost groups are NOT time-ordered. Return
    /// the modifier's <see cref="NumericModifier.ActivationOrder"/> for exactly that group (DP/BaseDp at fold tier 2,
    /// i.e. the NotIsUpDown/Set group), so the latest-activated "DP becomes X" is applied last (wins); 0 everywhere
    /// else keeps the value inert. The final <c>ThenBy(Id)</c> is a deterministic tie-break for equal orders,
    /// mirroring the AS-IS stable sort over equal (MinValue) timestamps.</summary>
    private static long ActivationTieBreak(NumericModifier modifier)
    {
        return modifier.Metric is NumericModifierMetric.Dp or NumericModifierMetric.BaseDp && ModifierOrder(modifier) == 2
            ? modifier.ActivationOrder
            : 0;
    }

    /// <summary>(SAttack 3-tier) AS-IS buckets SecurityAttack/LinkedMax effects by the <see cref="CalculateOrder"/>
    /// switch, which has cases ONLY for UpToConstant/UpDownValue/DownToConstant — an effect reporting UpValue or
    /// DownValue is added to no tier list and thus collected-but-never-applied. Mirror that drop.</summary>
    private static bool IsDroppedByCalcOrder(NumericModifier modifier)
    {
        return modifier.Metric is NumericModifierMetric.SecurityAttack or NumericModifierMetric.LinkedMax
            && modifier.Mode != NumericModifierMode.InvertDelta
            && modifier.CalcOrder is CalculateOrder.UpValue or CalculateOrder.DownValue;
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

    private static bool TryReadLong(IReadOnlyDictionary<string, object?> values, string key, out long value)
    {
        value = 0;
        if (!values.TryGetValue(key, out object? raw) || raw is null)
        {
            return false;
        }

        switch (raw)
        {
            case long longValue:
                value = longValue;
                return true;
            case int intValue:
                value = intValue;
                return true;
            case double doubleValue when doubleValue % 1 == 0:
                value = (long)doubleValue;
                return true;
            case string text when long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed):
                value = parsed;
                return true;
            default:
                return false;
        }
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

// (RD-A6-02) ModifierHelperFactory DELETED — zero src consumers (test-only NumericModifier-builder sugar over
// NumericModifier.Add/Set/InvertSecurityAttack, which are themselves called live from ReadSimpleModifiers
// above). Callers now build a NumericModifier directly via those record factories.
