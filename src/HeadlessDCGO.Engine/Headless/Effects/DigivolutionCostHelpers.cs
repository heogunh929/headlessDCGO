namespace HeadlessDCGO.Engine.Headless.Effects;

using System.Collections.ObjectModel;
using System.Globalization;
using HeadlessDCGO.Engine.Headless.Services;

public sealed record DigivolutionCostRequirement
{
    public DigivolutionCostRequirement(
        string id,
        int memoryCost,
        int? targetLevel = null,
        string? targetColor = null,
        string? targetCardType = null,
        string? targetDefinitionId = null,
        string? targetIdentity = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (memoryCost < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(memoryCost), "Digivolution memory cost must not be negative.");
        }

        if (targetLevel < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetLevel), "Target level must not be negative.");
        }

        Id = id.Trim();
        MemoryCost = memoryCost;
        TargetLevel = targetLevel;
        TargetColor = NormalizeOptional(targetColor);
        TargetCardType = NormalizeOptional(targetCardType);
        TargetDefinitionId = NormalizeOptional(targetDefinitionId);
        TargetIdentity = NormalizeOptional(targetIdentity);
    }

    public string Id { get; }

    public int MemoryCost { get; }

    public int? TargetLevel { get; }

    public string? TargetColor { get; }

    public string? TargetCardType { get; }

    public string? TargetDefinitionId { get; }

    /// <summary>(RD-R3-01) A non-Color@Level EvolutionCondition identity token (definition / card-number /
    /// card-type condition): the target's TOP card must match this token on its definition id (Ordinal) OR
    /// card number (OrdinalIgnoreCase) OR card type (OrdinalIgnoreCase) — the OR-of-three gate of the mirror
    /// <c>CardSource.PrintedEvoCosts</c> TokenMatch and <c>DigivolveAction.MatchesEvolutionCondition</c>'s
    /// legacy-token arm. Unlike <see cref="TargetDefinitionId"/>/<see cref="TargetCardType"/> (AND-composed
    /// metadata conditions), this is a single disjunctive gate.</summary>
    public string? TargetIdentity { get; }

    public static DigivolutionCostRequirement Any(string id, int memoryCost)
    {
        return new DigivolutionCostRequirement(id, memoryCost);
    }

    public static DigivolutionCostRequirement ForColorLevel(
        string id,
        int memoryCost,
        string targetColor,
        int targetLevel)
    {
        return new DigivolutionCostRequirement(id, memoryCost, targetLevel, targetColor);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

public sealed record DigivolutionCostRequest
{
    public DigivolutionCostRequest(
        CardRecord card,
        CardRecord targetCard,
        CardInstanceRecord? instance = null,
        CardInstanceRecord? targetInstance = null,
        IReadOnlyList<DigivolutionCostRequirement>? requirements = null,
        IReadOnlyList<PlayCostModifier>? modifiers = null,
        int? fixedCost = null,
        bool ignoreLevel = false,
        bool checkAvailability = false,
        bool canReduceCost = true,
        int? availableMemory = null,
        bool ignoreColor = false)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(targetCard);
        if (fixedCost < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fixedCost), "Fixed digivolution cost must not be negative.");
        }

        if (availableMemory < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(availableMemory), "Available memory must not be negative.");
        }

        Card = card;
        TargetCard = targetCard;
        Instance = instance;
        TargetInstance = targetInstance;
        Requirements = Array.AsReadOnly((requirements ?? Array.Empty<DigivolutionCostRequirement>()).ToArray());
        Modifiers = Array.AsReadOnly((modifiers ?? Array.Empty<PlayCostModifier>()).ToArray());
        FixedCost = fixedCost;
        IgnoreLevel = ignoreLevel;
        IgnoreColor = ignoreColor;
        CheckAvailability = checkAvailability;
        CanReduceCost = canReduceCost;
        AvailableMemory = availableMemory;
    }

    public CardRecord Card { get; }

    public CardRecord TargetCard { get; }

    public CardInstanceRecord? Instance { get; }

    public CardInstanceRecord? TargetInstance { get; }

    public IReadOnlyList<DigivolutionCostRequirement> Requirements { get; }

    public IReadOnlyList<PlayCostModifier> Modifiers { get; }

    public int? FixedCost { get; }

    public bool IgnoreLevel { get; }

    /// <summary>(IgnoreRequirement.Color / .All) waive the printed COLOR requirement — the digivolving card may
    /// come from any color as long as the level still matches (or level too, under IgnoreLevel).</summary>
    public bool IgnoreColor { get; }

    public bool CheckAvailability { get; }

    public bool CanReduceCost { get; }

    public int? AvailableMemory { get; }
}

public sealed record DigivolutionCostResult
{
    private DigivolutionCostResult(
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

    public static DigivolutionCostResult Success(
        int cost,
        bool canPay,
        string reason,
        IReadOnlyDictionary<string, object?> values)
    {
        return new DigivolutionCostResult(true, cost, canPay, reason, values);
    }

    public static DigivolutionCostResult Failure(
        string reason,
        IReadOnlyDictionary<string, object?> values)
    {
        return new DigivolutionCostResult(false, 0, false, reason, values);
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

public static class DigivolutionCostHelpers
{
    public const string DigivolutionCostsKey = "digivolutionCosts";
    public const string EvolutionCostsKey = "evolutionCosts";
    public const string EvoCostsKey = "evoCosts";

    /// <summary>(RD-R3-01) The card-data loader's structured evolution-condition metadata key
    /// (CardBaseEntityLoader: <c>metadata["evolutionConditions"]</c> = the "Color@Level:Cost" token strings,
    /// the SAME encoding joined into <c>CardRecord.EvolutionCondition</c>).</summary>
    public const string EvolutionConditionsKey = "evolutionConditions";
    public const string DigivolutionCostDeltaKey = "digivolutionCostDelta";
    public const string DigivolutionPayingCostDeltaKey = "digivolutionPayingCostDelta";
    public const string DigivolutionCostModifiersKey = "digivolutionCostModifiers";
    public const string FixedDigivolutionCostKey = "fixedDigivolutionCost";
    public const string LevelKey = "level";
    public const string CardColorKey = "cardColor";
    public const string CardColorsKey = "cardColors";

    public static DigivolutionCostResult Evaluate(DigivolutionCostRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        DigivolutionCostRequirement[] requirements = ResolveRequirements(request).ToArray();
        DigivolutionCostRequirement[] matchedRequirements = requirements
            .Where(requirement => Matches(requirement, request.TargetCard, request.IgnoreLevel, request.IgnoreColor))
            .OrderBy(requirement => requirement.MemoryCost)
            .ThenBy(requirement => requirement.Id, StringComparer.Ordinal)
            .ToArray();
        var values = BaseValues(request, requirements, matchedRequirements);

        int baseCost;
        if (request.FixedCost.HasValue)
        {
            baseCost = request.FixedCost.Value;
            values["selectedRequirementId"] = "fixed";
        }
        else if (matchedRequirements.Length > 0)
        {
            DigivolutionCostRequirement selected = matchedRequirements[0];
            baseCost = selected.MemoryCost;
            values["selectedRequirementId"] = selected.Id;
        }
        else if (requirements.Length == 0)
        {
            return DigivolutionCostResult.Failure(
                $"Card definition '{request.Card.Id}' has no digivolution cost.",
                values);
        }
        else
        {
            return DigivolutionCostResult.Failure(
                $"No digivolution cost requirement matched target definition '{request.TargetCard.Id}'.",
                values);
        }

        PlayCostResult costResult = PlayCostHelpers.Evaluate(new PlayCostRequest(
            request.Card,
            request.Instance,
            request.Modifiers,
            fixedCost: baseCost,
            root: PlayCostRoot.Hand,
            checkAvailability: request.CheckAvailability,
            canReduceCost: request.CanReduceCost,
            availableMemory: request.AvailableMemory));

        foreach (KeyValuePair<string, object?> pair in costResult.Values)
        {
            values[$"costPipeline.{pair.Key}"] = pair.Value;
        }

        values["baseDigivolutionCost"] = baseCost;
        values["finalDigivolutionCost"] = costResult.Cost;
        values["canPay"] = costResult.CanPay;

        return costResult.IsSuccess
            ? DigivolutionCostResult.Success(
                costResult.Cost,
                costResult.CanPay,
                costResult.CanPay ? "Digivolution cost resolved." : "Resolved digivolution cost exceeds available memory.",
                values)
            : DigivolutionCostResult.Failure(costResult.Reason, values);
    }

    public static bool TryResolveCost(
        CardRecord card,
        CardInstanceRecord? instance,
        CardRecord targetCard,
        CardInstanceRecord? targetInstance,
        out int digivolutionCost,
        out string? error,
        bool ignoreLevel = false,
        bool ignoreColor = false)
    {
        DigivolutionCostResult result = Evaluate(new DigivolutionCostRequest(
            card,
            targetCard,
            instance,
            targetInstance,
            ReadRequirements(card),
            ReadModifiers(card, instance),
            ReadFixedCost(card, instance),
            ignoreLevel: ignoreLevel,
            ignoreColor: ignoreColor));
        digivolutionCost = result.Cost;
        error = result.IsSuccess ? null : result.Reason;
        return result.IsSuccess;
    }

    public static IReadOnlyList<DigivolutionCostRequirement> ReadRequirements(CardRecord card)
    {
        ArgumentNullException.ThrowIfNull(card);

        var requirements = new List<DigivolutionCostRequirement>();
        // (RD-R3-01) the printed EvolutionCondition tokens are the CANONICAL digivolution requirements — the
        // same "Color@Level(:Cost)" encoding the mirror CardSource.PrintedEvoCosts parses (the AS-IS
        // BaseEvoCostsFromEntity {CardColor, Level, MemoryCost} carrier). Before this gap-fill the loader's
        // conditions were invisible here (only the {digivolutionCosts, evolutionCosts, evoCosts} metadata keys
        // were scanned) and every condition card degraded to the Any(EvolutionCost) fallback, so multi-path
        // cards (e.g. BT10_026 Blue@4:4 / Blue@5:2) resolved the wrong cost in the legal-action table.
        requirements.AddRange(ParseEvolutionCondition(card.EvolutionCondition, card.EvolutionCost));
        if (requirements.Count == 0)
        {
            requirements.AddRange(ReadRequirementsFromMetadata(card.Metadata, card.EvolutionCost));
        }

        if (requirements.Count == 0 && card.EvolutionCost.HasValue)
        {
            requirements.Add(DigivolutionCostRequirement.Any("cardRecordEvolutionCost", card.EvolutionCost.Value));
        }

        return requirements
            .OrderBy(requirement => requirement.MemoryCost)
            .ThenBy(requirement => requirement.Id, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>(RD-R3-01) The single canonical EvolutionCondition token parser, shared by BOTH digivolution
    /// cost seats (this helper's requirement table AND the mirror <c>CardSource.PrintedEvoCosts</c>). Grammar
    /// 1:1 with the mirror/AS-IS carrier: tokens split on <c>, ; |</c>; an optional <c>definition:</c> /
    /// <c>from:</c> prefix is stripped; <c>Color@Level(:Cost)</c> yields a colour+level requirement whose cost
    /// falls back to the printed <paramref name="printedEvolutionCost"/> (?? 0) when the <c>:Cost</c> suffix is
    /// absent; any other token is an identity condition (<see cref="DigivolutionCostRequirement.TargetIdentity"/>)
    /// at the printed cost. Token order is preserved (AS-IS BaseEvoCostsFromEntity data order). A token whose
    /// resolved cost (or level) is negative is dropped — observably identical to AS-IS, where a negative-cost
    /// path exists but is filtered by <c>CostList</c>'s <c>&gt;= 0</c> gate and so is never payable (and no
    /// negative level/cost exists in the shipped card corpus).</summary>
    public static IReadOnlyList<DigivolutionCostRequirement> ParseEvolutionCondition(
        string? evolutionCondition,
        int? printedEvolutionCost)
    {
        var requirements = new List<DigivolutionCostRequirement>();
        if (string.IsNullOrWhiteSpace(evolutionCondition))
        {
            return requirements;
        }

        string[] tokens = evolutionCondition
            .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (int index = 0; index < tokens.Length; index++)
        {
            string token = tokens[index];
            if (token.StartsWith("definition:", StringComparison.OrdinalIgnoreCase))
            {
                token = token["definition:".Length..];
            }
            else if (token.StartsWith("from:", StringComparison.OrdinalIgnoreCase))
            {
                token = token["from:".Length..];
            }

            string id = $"evolutionCondition[{index.ToString(CultureInfo.InvariantCulture)}]";
            int at = token.IndexOf('@');
            if (at > 0 && at < token.Length - 1)
            {
                string color = token[..at].Trim();
                string rest = token[(at + 1)..];
                int colon = rest.IndexOf(':');
                string levelText = (colon >= 0 ? rest[..colon] : rest).Trim();
                int cost = colon >= 0 && int.TryParse(rest[(colon + 1)..].Trim(), out int tokenCost)
                    ? tokenCost
                    : printedEvolutionCost ?? 0;
                if (int.TryParse(levelText, out int level) && color.Length > 0)
                {
                    if (cost >= 0 && level >= 0)
                    {
                        requirements.Add(new DigivolutionCostRequirement(id, cost, level, color));
                    }

                    continue;
                }
            }

            // definition / card-number / card-type condition token — identity gate on the target's top card.
            if (printedEvolutionCost is null or >= 0)
            {
                requirements.Add(new DigivolutionCostRequirement(id, printedEvolutionCost ?? 0, targetIdentity: token));
            }
        }

        return requirements;
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

        if (instance is not null && TryReadInt(instance.Metadata, FixedDigivolutionCostKey, out int instanceFixed))
        {
            return instanceFixed;
        }

        return TryReadInt(card.Metadata, FixedDigivolutionCostKey, out int cardFixed)
            ? cardFixed
            : null;
    }

    private static IEnumerable<DigivolutionCostRequirement> ResolveRequirements(DigivolutionCostRequest request)
    {
        return request.Requirements.Count > 0
            ? request.Requirements
            : ReadRequirements(request.Card);
    }

    private static bool Matches(
        DigivolutionCostRequirement requirement,
        CardRecord targetCard,
        bool ignoreLevel,
        bool ignoreColor = false)
    {
        if (requirement.TargetLevel.HasValue &&
            !ignoreLevel &&
            (!TryReadLevel(targetCard.Metadata, out int targetLevel) || targetLevel != requirement.TargetLevel.Value))
        {
            return false;
        }

        if (requirement.TargetColor is not null && !ignoreColor && !ReadColors(targetCard.Metadata).Contains(requirement.TargetColor, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        if (requirement.TargetCardType is not null &&
            !string.Equals(requirement.TargetCardType, targetCard.CardType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // (RD-R3-01) identity-token condition (mirror CardSource.PrintedEvoCosts TokenMatch): OR over the
        // target's definition id / card number / card type. There are no colour/level halves to waive
        // separately, so only the FULL ignore (both flags — the AS-IS IgnoreRequirement.All arm, which returns
        // the cost before the TokenMatch gate) bypasses it; ignoreColor/ignoreLevel alone do not.
        if (requirement.TargetIdentity is not null &&
            !(ignoreLevel && ignoreColor) &&
            !(string.Equals(requirement.TargetIdentity, targetCard.Id.Value, StringComparison.Ordinal) ||
                string.Equals(requirement.TargetIdentity, targetCard.CardNumber, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(requirement.TargetIdentity, targetCard.CardType, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return requirement.TargetDefinitionId is null ||
            string.Equals(requirement.TargetDefinitionId, targetCard.Id.Value, StringComparison.Ordinal) ||
            string.Equals(requirement.TargetDefinitionId, targetCard.CardNumber, StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<string, object?> BaseValues(
        DigivolutionCostRequest request,
        IReadOnlyList<DigivolutionCostRequirement> requirements,
        IReadOnlyList<DigivolutionCostRequirement> matchedRequirements)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["cardDefinitionId"] = request.Card.Id.Value,
            ["targetDefinitionId"] = request.TargetCard.Id.Value,
            ["targetCardNumber"] = request.TargetCard.CardNumber,
            ["targetCardType"] = request.TargetCard.CardType,
            ["ignoreLevel"] = request.IgnoreLevel,
            ["checkAvailability"] = request.CheckAvailability,
            ["canReduceCost"] = request.CanReduceCost,
            ["requirementCount"] = requirements.Count,
            ["matchedRequirementIds"] = matchedRequirements.Select(requirement => requirement.Id).ToArray(),
            ["modifierCount"] = request.Modifiers.Count,
        };

        if (TryReadLevel(request.TargetCard.Metadata, out int targetLevel))
        {
            values["targetLevel"] = targetLevel;
        }

        string[] targetColors = ReadColors(request.TargetCard.Metadata);
        if (targetColors.Length > 0)
        {
            values["targetColors"] = targetColors;
        }

        if (request.Instance is not null)
        {
            values["cardInstanceId"] = request.Instance.InstanceId.Value;
            values["ownerId"] = request.Instance.OwnerId.Value;
        }

        if (request.TargetInstance is not null)
        {
            values["targetInstanceId"] = request.TargetInstance.InstanceId.Value;
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

    private static IEnumerable<DigivolutionCostRequirement> ReadRequirementsFromMetadata(
        IReadOnlyDictionary<string, object?> metadata,
        int? printedEvolutionCost)
    {
        // (RD-R3-01) the loader's structured `evolutionConditions` key is scanned too — for CardRecords built
        // without the joined EvolutionCondition string. Each flattened STRING item under THAT key is a token
        // run through the canonical parser; the legacy keys keep their exact prior (dictionary-only) read.
        foreach (string key in new[] { DigivolutionCostsKey, EvolutionCostsKey, EvoCostsKey, EvolutionConditionsKey })
        {
            if (!metadata.TryGetValue(key, out object? rawCosts) || rawCosts is null)
            {
                continue;
            }

            bool parseStringTokens = string.Equals(key, EvolutionConditionsKey, StringComparison.Ordinal);
            foreach (object? rawRequirement in FlattenObjects(rawCosts))
            {
                if (rawRequirement is string tokenText)
                {
                    if (parseStringTokens)
                    {
                        foreach (DigivolutionCostRequirement parsed in ParseEvolutionCondition(tokenText, printedEvolutionCost))
                        {
                            yield return parsed;
                        }
                    }

                    continue;
                }

                if (TryReadRequirement(rawRequirement, out DigivolutionCostRequirement? requirement))
                {
                    yield return requirement!;
                }
            }
        }
    }

    private static bool TryReadRequirement(
        object? rawRequirement,
        out DigivolutionCostRequirement? requirement)
    {
        requirement = null;
        if (rawRequirement is DigivolutionCostRequirement typed)
        {
            requirement = typed;
            return true;
        }

        if (rawRequirement is not IReadOnlyDictionary<string, object?> dictionary)
        {
            return false;
        }

        if (!TryReadInt(dictionary, "memoryCost", out int cost) &&
            !TryReadInt(dictionary, "cost", out cost) &&
            !TryReadInt(dictionary, "evolutionCost", out cost) &&
            !TryReadInt(dictionary, "digivolutionCost", out cost))
        {
            return false;
        }

        string id = $"digivolutionCost-{cost.ToString(CultureInfo.InvariantCulture)}";
        if (TryReadString(dictionary, "id", out string? parsedId))
        {
            id = parsedId!;
        }

        int? level = TryReadInt(dictionary, LevelKey, out int parsedLevel) || TryReadInt(dictionary, "targetLevel", out parsedLevel)
            ? parsedLevel
            : null;
        string? color = null;
        if (TryReadString(dictionary, "targetColor", out string? parsedColor) ||
            TryReadString(dictionary, CardColorKey, out parsedColor) ||
            TryReadString(dictionary, "color", out parsedColor))
        {
            color = parsedColor;
        }

        string? cardType = null;
        if (TryReadString(dictionary, "targetCardType", out string? parsedCardType) ||
            TryReadString(dictionary, "cardType", out parsedCardType))
        {
            cardType = parsedCardType;
        }

        string? definitionId = null;
        if (TryReadString(dictionary, "targetDefinitionId", out string? parsedDefinitionId) ||
            TryReadString(dictionary, "definitionId", out parsedDefinitionId))
        {
            definitionId = parsedDefinitionId;
        }

        requirement = new DigivolutionCostRequirement(id, cost, level, color, cardType, definitionId);
        return true;
    }

    private static IEnumerable<PlayCostModifier> ReadModifiersFromMetadata(
        IReadOnlyDictionary<string, object?> metadata)
    {
        if (TryReadInt(metadata, DigivolutionCostDeltaKey, out int costDelta) && costDelta != 0)
        {
            yield return PlayCostModifier.AddToCost(DigivolutionCostDeltaKey, costDelta);
        }

        if (TryReadInt(metadata, DigivolutionPayingCostDeltaKey, out int payingCostDelta) && payingCostDelta != 0)
        {
            yield return PlayCostModifier.AddToPayingCost(DigivolutionPayingCostDeltaKey, payingCostDelta);
        }

        if (!metadata.TryGetValue(DigivolutionCostModifiersKey, out object? rawModifiers) || rawModifiers is null)
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

        if (rawModifier is not IReadOnlyDictionary<string, object?> dictionary || !TryReadInt(dictionary, "value", out int value))
        {
            return false;
        }

        string id = $"metadata-{DigivolutionCostModifiersKey}";
        if (TryReadString(dictionary, "id", out string? parsedId))
        {
            id = parsedId!;
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

    private static bool TryReadLevel(IReadOnlyDictionary<string, object?> metadata, out int level)
    {
        return TryReadInt(metadata, LevelKey, out level) ||
            TryReadInt(metadata, "Level", out level) ||
            TryReadInt(metadata, "cardLevel", out level);
    }

    private static string[] ReadColors(IReadOnlyDictionary<string, object?> metadata)
    {
        return ReadStrings(metadata, CardColorsKey, CardColorKey, "colors", "color", "CardColors")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string[] ReadStrings(IReadOnlyDictionary<string, object?> metadata, params string[] keys)
    {
        var values = new List<string>();
        foreach (string key in keys)
        {
            if (!metadata.TryGetValue(key, out object? raw) || raw is null)
            {
                continue;
            }

            if (raw is string stringValue)
            {
                values.AddRange(stringValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                continue;
            }

            if (raw is IEnumerable<string> stringValues)
            {
                values.AddRange(stringValues);
            }
        }

        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToArray();
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
            int intValue => SetInt(intValue, out value),
            long longValue when longValue >= int.MinValue && longValue <= int.MaxValue => SetInt((int)longValue, out value),
            string stringValue when int.TryParse(stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) => SetInt(parsed, out value),
            _ => false,
        };
    }

    private static bool TryReadString(
        IReadOnlyDictionary<string, object?> values,
        string key,
        out string? value)
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
            bool boolValue => SetBool(boolValue, out value),
            string stringValue when bool.TryParse(stringValue, out bool parsed) => SetBool(parsed, out value),
            _ => false,
        };
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

        if (raw is string stringValue && Enum.TryParse(stringValue, ignoreCase: true, out TEnum parsed))
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
