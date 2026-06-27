namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using HeadlessDCGO.Engine.Headless.Effects;

using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

public enum CardRequirementKind
{
    Name = 0,
    Color = 1,
    Trait = 2,
}

public enum CardRequirementQuantifier
{
    Any = 0,
    All = 1,
}

public enum CardRequirementTextMode
{
    Exact = 0,
    Contains = 1,
}

public sealed record CardRequirement
{
    public CardRequirement(
        CardRequirementKind kind,
        IEnumerable<string> requiredValues,
        CardRequirementQuantifier quantifier = CardRequirementQuantifier.Any,
        CardRequirementTextMode textMode = CardRequirementTextMode.Exact)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), "Requirement kind must be known.");
        }

        if (!Enum.IsDefined(quantifier))
        {
            throw new ArgumentOutOfRangeException(nameof(quantifier), "Requirement quantifier must be known.");
        }

        if (!Enum.IsDefined(textMode))
        {
            throw new ArgumentOutOfRangeException(nameof(textMode), "Requirement text mode must be known.");
        }

        Kind = kind;
        RequiredValues = CopyRequiredValues(requiredValues);
        Quantifier = quantifier;
        TextMode = textMode;
    }

    public CardRequirementKind Kind { get; }

    public IReadOnlyList<string> RequiredValues { get; }

    public CardRequirementQuantifier Quantifier { get; }

    public CardRequirementTextMode TextMode { get; }

    public static CardRequirement Name(
        string requiredName,
        CardRequirementTextMode textMode = CardRequirementTextMode.Exact)
    {
        return new CardRequirement(CardRequirementKind.Name, new[] { requiredName }, textMode: textMode);
    }

    public static CardRequirement Color(
        string requiredColor,
        CardRequirementQuantifier quantifier = CardRequirementQuantifier.Any)
    {
        return new CardRequirement(CardRequirementKind.Color, new[] { requiredColor }, quantifier);
    }

    public static CardRequirement Trait(
        string requiredTrait,
        CardRequirementTextMode textMode = CardRequirementTextMode.Exact)
    {
        return new CardRequirement(CardRequirementKind.Trait, new[] { requiredTrait }, textMode: textMode);
    }

    private static IReadOnlyList<string> CopyRequiredValues(IEnumerable<string> requiredValues)
    {
        ArgumentNullException.ThrowIfNull(requiredValues);

        string[] values = requiredValues
            .Select(NormalizeText)
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value, StringComparer.Ordinal)
            .ToArray();

        if (values.Length == 0)
        {
            throw new ArgumentException("At least one non-empty required value is required.", nameof(requiredValues));
        }

        return Array.AsReadOnly(values);
    }

    private static string NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}

public sealed record CardRequirementRequest
{
    public CardRequirementRequest(
        MatchState matchState,
        HeadlessEntityId sourceInstanceId,
        IReadOnlyDictionary<HeadlessEntityId, CardRecord> cardDefinitions,
        IReadOnlyList<CardRequirement> requirements)
    {
        ArgumentNullException.ThrowIfNull(matchState);
        ArgumentNullException.ThrowIfNull(cardDefinitions);
        ArgumentNullException.ThrowIfNull(requirements);

        if (sourceInstanceId.IsEmpty)
        {
            throw new ArgumentException("Source instance id must not be empty.", nameof(sourceInstanceId));
        }

        if (requirements.Count == 0)
        {
            throw new ArgumentException("At least one requirement is required.", nameof(requirements));
        }

        MatchState = matchState;
        SourceInstanceId = sourceInstanceId;
        CardDefinitions = CopyDefinitions(cardDefinitions);
        Requirements = Array.AsReadOnly(requirements.ToArray());
    }

    public MatchState MatchState { get; }

    public HeadlessEntityId SourceInstanceId { get; }

    public IReadOnlyDictionary<HeadlessEntityId, CardRecord> CardDefinitions { get; }

    public IReadOnlyList<CardRequirement> Requirements { get; }

    private static IReadOnlyDictionary<HeadlessEntityId, CardRecord> CopyDefinitions(
        IReadOnlyDictionary<HeadlessEntityId, CardRecord> definitions)
    {
        var copy = new Dictionary<HeadlessEntityId, CardRecord>();
        foreach (KeyValuePair<HeadlessEntityId, CardRecord> pair in definitions)
        {
            if (pair.Key.IsEmpty)
            {
                throw new ArgumentException("Definition id must not be empty.", nameof(definitions));
            }

            ArgumentNullException.ThrowIfNull(pair.Value);
            if (pair.Key != pair.Value.Id)
            {
                throw new ArgumentException("Definition dictionary key must match card record id.", nameof(definitions));
            }

            copy[pair.Key] = pair.Value;
        }

        return new ReadOnlyDictionary<HeadlessEntityId, CardRecord>(copy);
    }
}

public sealed record CardRequirementResult
{
    private CardRequirementResult(
        bool isMatch,
        string reason,
        IReadOnlyDictionary<string, object?> values)
    {
        IsMatch = isMatch;
        Reason = reason;
        Values = CopyValues(values);
    }

    public bool IsMatch { get; }

    public string Reason { get; }

    public IReadOnlyDictionary<string, object?> Values { get; }

    public static CardRequirementResult Match(
        string reason,
        IReadOnlyDictionary<string, object?> values)
    {
        return new CardRequirementResult(true, reason, values);
    }

    public static CardRequirementResult NoMatch(
        string reason,
        IReadOnlyDictionary<string, object?> values)
    {
        return new CardRequirementResult(false, reason, values);
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

public static class CardRequirementHelpers
{
    public const string NameKey = "name";
    public const string CardNameKey = "cardName";
    public const string CardNamesKey = "cardNames";
    public const string ColorKey = "color";
    public const string CardColorKey = "cardColor";
    public const string CardColorsKey = "cardColors";
    public const string TraitKey = "trait";
    public const string TraitsKey = "traits";
    public const string CardTraitsKey = "cardTraits";

    public static CardRequirementResult HasName(
        MatchState matchState,
        HeadlessEntityId sourceInstanceId,
        IReadOnlyDictionary<HeadlessEntityId, CardRecord> cardDefinitions,
        string requiredName,
        CardRequirementTextMode textMode = CardRequirementTextMode.Exact)
    {
        return Evaluate(new CardRequirementRequest(
            matchState,
            sourceInstanceId,
            cardDefinitions,
            new[] { CardRequirement.Name(requiredName, textMode) }));
    }

    public static CardRequirementResult HasColor(
        MatchState matchState,
        HeadlessEntityId sourceInstanceId,
        IReadOnlyDictionary<HeadlessEntityId, CardRecord> cardDefinitions,
        string requiredColor)
    {
        return Evaluate(new CardRequirementRequest(
            matchState,
            sourceInstanceId,
            cardDefinitions,
            new[] { CardRequirement.Color(requiredColor) }));
    }

    public static CardRequirementResult HasAllColors(
        MatchState matchState,
        HeadlessEntityId sourceInstanceId,
        IReadOnlyDictionary<HeadlessEntityId, CardRecord> cardDefinitions,
        IEnumerable<string> requiredColors)
    {
        return Evaluate(new CardRequirementRequest(
            matchState,
            sourceInstanceId,
            cardDefinitions,
            new[] { new CardRequirement(CardRequirementKind.Color, requiredColors, CardRequirementQuantifier.All) }));
    }

    public static CardRequirementResult HasTrait(
        MatchState matchState,
        HeadlessEntityId sourceInstanceId,
        IReadOnlyDictionary<HeadlessEntityId, CardRecord> cardDefinitions,
        string requiredTrait,
        CardRequirementTextMode textMode = CardRequirementTextMode.Exact)
    {
        return Evaluate(new CardRequirementRequest(
            matchState,
            sourceInstanceId,
            cardDefinitions,
            new[] { CardRequirement.Trait(requiredTrait, textMode) }));
    }

    public static CardRequirementResult Evaluate(CardRequirementRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!TryReadCardProfile(request, out CardRequirementProfile? profile, out string reason))
        {
            return CardRequirementResult.NoMatch(reason, BaseValues(request));
        }

        var requirementValues = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (CardRequirement requirement in request.Requirements)
        {
            bool matched = EvaluateRequirement(requirement, profile, out string[] matchedValues, out string[] availableValues);
            requirementValues[$"{requirement.Kind}Required"] = requirement.RequiredValues.ToArray();
            requirementValues[$"{requirement.Kind}Available"] = availableValues;
            requirementValues[$"{requirement.Kind}Matched"] = matchedValues;
            requirementValues[$"{requirement.Kind}Quantifier"] = requirement.Quantifier.ToString();
            requirementValues[$"{requirement.Kind}TextMode"] = requirement.TextMode.ToString();

            if (!matched)
            {
                return CardRequirementResult.NoMatch(
                    $"{requirement.Kind} requirement did not match.",
                    BaseValues(request, profile, requirementValues));
            }
        }

        return CardRequirementResult.Match(
            "All card requirements matched.",
            BaseValues(request, profile, requirementValues));
    }

    public static bool HasGroupedTrait(
        CardRequirementProfile profile,
        string groupName)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(groupName);

        return TraitGroupValues(groupName)
            .Any(trait => MatchesAny(profile.Traits, trait, CardRequirementTextMode.Contains));
    }

    private static bool TryReadCardProfile(
        CardRequirementRequest request,
        [NotNullWhen(true)] out CardRequirementProfile? profile,
        out string reason)
    {
        profile = null;
        if (!request.MatchState.CardInstances.TryGetValue(request.SourceInstanceId, out CardInstanceState? instance))
        {
            reason = $"Source card '{request.SourceInstanceId}' was not found.";
            return false;
        }

        if (!request.CardDefinitions.TryGetValue(instance.DefinitionId, out CardRecord? definition))
        {
            reason = $"Source definition '{instance.DefinitionId}' was not found.";
            return false;
        }

        profile = new CardRequirementProfile(
            request.SourceInstanceId,
            instance.DefinitionId,
            ReadNames(instance, definition),
            ReadCategory(instance, definition, Category.NameColors, ColorKey, CardColorKey, CardColorsKey, "colors", "CardColors"),
            ReadCategory(instance, definition, Category.Traits, TraitKey, TraitsKey, CardTraitsKey, "CardTraits"));
        reason = string.Empty;
        return true;
    }

    private static IReadOnlyList<string> ReadNames(CardInstanceState instance, CardRecord definition)
    {
        string[] modifierNames = ReadStrings(instance.Modifiers, NameKey, CardNameKey, CardNamesKey, "names", "CardNames");
        if (modifierNames.Length > 0)
        {
            return modifierNames;
        }

        string[] metadataNames = ReadStrings(definition.Metadata, NameKey, CardNameKey, CardNamesKey, "names", "CardNames");
        return NormalizeDistinct(new[] { definition.Name }.Concat(metadataNames));
    }

    private static IReadOnlyList<string> ReadCategory(
        CardInstanceState instance,
        CardRecord definition,
        Category category,
        params string[] keys)
    {
        string[] modifierValues = ReadStrings(instance.Modifiers, keys);
        if (modifierValues.Length > 0)
        {
            return modifierValues;
        }

        string[] metadataValues = ReadStrings(definition.Metadata, keys);
        if (category == Category.NameColors)
        {
            return metadataValues;
        }

        return metadataValues;
    }

    private static bool EvaluateRequirement(
        CardRequirement requirement,
        CardRequirementProfile profile,
        out string[] matchedValues,
        out string[] availableValues)
    {
        IReadOnlyList<string> available = requirement.Kind switch
        {
            CardRequirementKind.Name => profile.Names,
            CardRequirementKind.Color => profile.Colors,
            CardRequirementKind.Trait => profile.Traits,
            _ => Array.Empty<string>()
        };
        availableValues = available.ToArray();

        var matched = new List<string>();
        foreach (string required in requirement.RequiredValues)
        {
            if (MatchesAny(available, required, requirement.TextMode))
            {
                matched.Add(required);
            }
        }

        matchedValues = matched.ToArray();
        return requirement.Quantifier == CardRequirementQuantifier.All
            ? matched.Count == requirement.RequiredValues.Count
            : matched.Count > 0;
    }

    private static IReadOnlyDictionary<string, object?> BaseValues(
        CardRequirementRequest request,
        CardRequirementProfile? profile = null,
        IReadOnlyDictionary<string, object?>? extraValues = null)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["sourceInstanceId"] = request.SourceInstanceId.Value,
            ["requirementCount"] = request.Requirements.Count,
        };

        if (profile is not null)
        {
            values["sourceDefinitionId"] = profile.DefinitionId.Value;
            values["names"] = profile.Names.ToArray();
            values["colors"] = profile.Colors.ToArray();
            values["traits"] = profile.Traits.ToArray();
        }

        if (extraValues is not null)
        {
            foreach (KeyValuePair<string, object?> pair in extraValues)
            {
                values[pair.Key] = pair.Value;
            }
        }

        return values;
    }

    private static bool MatchesAny(
        IReadOnlyList<string> available,
        string required,
        CardRequirementTextMode textMode)
    {
        return textMode == CardRequirementTextMode.Exact
            ? ContainsNormalized(available, required)
            : available.Any(value => NormalizeForCompare(value).Contains(NormalizeForCompare(required), StringComparison.Ordinal));
    }

    private static bool ContainsNormalized(IReadOnlyList<string> available, string required)
    {
        string normalizedRequired = NormalizeForCompare(required);
        return available.Any(value => NormalizeForCompare(value) == normalizedRequired);
    }

    private static string[] ReadStrings(
        IReadOnlyDictionary<string, object?> values,
        params string[] keys)
    {
        var collected = new List<string>();
        foreach (string key in keys)
        {
            if (values.TryGetValue(key, out object? raw))
            {
                collected.AddRange(FlattenStrings(raw));
            }
        }

        return NormalizeDistinct(collected).ToArray();
    }

    private static IEnumerable<string> FlattenStrings(object? raw)
    {
        switch (raw)
        {
            case null:
                yield break;
            case string text:
                foreach (string value in SplitText(text))
                {
                    yield return value;
                }

                yield break;
            case IEnumerable<string> strings:
                foreach (string value in strings.SelectMany(SplitText))
                {
                    yield return value;
                }

                yield break;
            case IEnumerable<object?> objects:
                foreach (object? item in objects)
                {
                    foreach (string value in FlattenStrings(item))
                    {
                        yield return value;
                    }
                }

                yield break;
            default:
                yield return raw.ToString() ?? string.Empty;
                yield break;
        }
    }

    private static IEnumerable<string> SplitText(string value)
    {
        return value
            .Split(new[] { ',', ';', '/', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static IReadOnlyList<string> NormalizeDistinct(IEnumerable<string> values)
    {
        return values
            .Select(value => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static string NormalizeForCompare(string value)
    {
        return value
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();
    }

    private static IReadOnlyList<string> TraitGroupValues(string groupName)
    {
        return NormalizeForCompare(groupName) switch
        {
            "bird" => new[] { "Avian", "Bird" },
            "beast" => new[] { "Beast", "Animal", "Sovereign" },
            "plant" => new[] { "Vegetation", "Plant" },
            "fairy" => new[] { "Fairy" },
            "dragon" => new[] { "Dragon", "saur", "Ceratopsian" },
            "aqua" => new[] { "Aqua", "Sea Animal", "SeaAnimal" },
            "angel" => new[] { "Angel", "Cherub", "Throne", "Authority", "Seraph", "Virtue", "Three Great Angels", "Archangel" },
            "royalknight" => new[] { "Royal Knight" },
            "soc" => new[] { "SoC" },
            "digipolice" => new[] { "DigiPolice", "D-Brigade" },
            "xantibody" => new[] { "X Antibody", "X-Antibody" },
            _ => new[] { groupName }
        };
    }

    private enum Category
    {
        NameColors,
        Traits,
    }
}

public sealed record CardRequirementProfile(
    HeadlessEntityId InstanceId,
    HeadlessEntityId DefinitionId,
    IReadOnlyList<string> Names,
    IReadOnlyList<string> Colors,
    IReadOnlyList<string> Traits);
