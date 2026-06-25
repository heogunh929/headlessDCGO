namespace HeadlessDCGO.Engine.Headless.DataLoading;

using System.Text.Json;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class CardAssetJsonLoader
{
    private static readonly string[] IdKeys =
    {
        "id",
        "cardId",
        "cardID",
        "sourcePath"
    };

    private static readonly string[] CardNumberKeys =
    {
        "cardNumber",
        "cardNo",
        "number",
        "id"
    };

    private static readonly string[] NameKeys =
    {
        "name",
        "cardName",
        "displayName",
        "title"
    };

    private static readonly string[] CardTypeKeys =
    {
        "cardType",
        "type"
    };

    private static readonly string[] PlayCostKeys =
    {
        "playCost",
        "cost"
    };

    private static readonly string[] EvolutionCostKeys =
    {
        "evolutionCost",
        "evoCost",
        "digivolutionCost"
    };

    private static readonly string[] EvolutionConditionKeys =
    {
        "evolutionCondition",
        "evoCondition",
        "digivolutionCondition"
    };

    private static readonly string[] EffectBindingKeyKeys =
    {
        "effectBindingKey",
        "effectKey",
        "effectId"
    };

    public CardRecord LoadFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            return ParseCard(document.RootElement, path);
        }
        catch (JsonException ex)
        {
            throw InvalidData($"Card JSON is not valid JSON: {path}", ex);
        }
        catch (ArgumentException ex)
        {
            throw InvalidData($"Card JSON schema is invalid: {path}. {ex.Message}", ex);
        }
    }

    public async Task<CardRecord> LoadFileAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            string json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            using JsonDocument document = JsonDocument.Parse(json);
            return ParseCard(document.RootElement, path);
        }
        catch (JsonException ex)
        {
            throw InvalidData($"Card JSON is not valid JSON: {path}", ex);
        }
        catch (ArgumentException ex)
        {
            throw InvalidData($"Card JSON schema is invalid: {path}. {ex.Message}", ex);
        }
    }

    public IReadOnlyList<CardRecord> LoadDirectory(
        string rootPath,
        string searchPattern = "*.json",
        SearchOption searchOption = SearchOption.AllDirectories)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(searchPattern);

        if (!Directory.Exists(rootPath))
        {
            return Array.Empty<CardRecord>();
        }

        return Directory
            .EnumerateFiles(rootPath, searchPattern, searchOption)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ThenBy(path => path, StringComparer.Ordinal)
            .Select(LoadFile)
            .ToArray();
    }

    public CardDatabase LoadDirectoryInto(
        CardDatabase database,
        string rootPath,
        string searchPattern = "*.json",
        SearchOption searchOption = SearchOption.AllDirectories)
    {
        ArgumentNullException.ThrowIfNull(database);

        database.UpsertRange(LoadDirectory(rootPath, searchPattern, searchOption));
        return database;
    }

    private static CardRecord ParseCard(JsonElement root, string sourcePath)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw InvalidData($"Card JSON root must be an object: {sourcePath}");
        }

        Dictionary<string, object?> metadata = ConvertObject(root);
        metadata["sourceFile"] = sourcePath;

        string id = ReadRequiredString(root, IdKeys, "id", sourcePath);
        string cardNumber = ReadRequiredString(root, CardNumberKeys, "cardNumber", sourcePath);
        string name = ReadRequiredString(root, NameKeys, "name", sourcePath);

        return new CardRecord(
            new HeadlessEntityId(id),
            cardNumber,
            name,
            metadata,
            ReadFirstString(root, CardTypeKeys),
            ReadFirstInt(root, PlayCostKeys, sourcePath),
            ReadFirstInt(root, EvolutionCostKeys, sourcePath),
            ReadFirstString(root, EvolutionConditionKeys),
            ReadFirstString(root, EffectBindingKeyKeys));
    }

    private static string? ReadFirstString(JsonElement root, IEnumerable<string> keys)
    {
        foreach (string key in keys)
        {
            if (TryGetPropertyIgnoreCase(root, key, out JsonElement value) &&
                value.ValueKind != JsonValueKind.Null &&
                value.ValueKind != JsonValueKind.Undefined)
            {
                string? result = value.ValueKind == JsonValueKind.String
                    ? value.GetString()
                    : value.ToString();

                if (!string.IsNullOrWhiteSpace(result))
                {
                    return result;
                }
            }
        }

        return null;
    }

    private static string ReadRequiredString(
        JsonElement root,
        IEnumerable<string> keys,
        string logicalName,
        string sourcePath)
    {
        string? value = ReadFirstString(root, keys);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw InvalidData($"Card JSON is missing required property '{logicalName}': {sourcePath}");
        }

        return value;
    }

    private static int? ReadFirstInt(JsonElement root, IEnumerable<string> keys, string sourcePath)
    {
        foreach (string key in keys)
        {
            if (TryGetPropertyIgnoreCase(root, key, out JsonElement value) &&
                value.ValueKind != JsonValueKind.Null &&
                value.ValueKind != JsonValueKind.Undefined)
            {
                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int intValue))
                {
                    if (intValue < 0)
                    {
                        throw InvalidData($"Card JSON property '{key}' must be non-negative: {sourcePath}");
                    }

                    return intValue;
                }

                if (value.ValueKind == JsonValueKind.String &&
                    int.TryParse(value.GetString(), out int stringValue))
                {
                    if (stringValue < 0)
                    {
                        throw InvalidData($"Card JSON property '{key}' must be non-negative: {sourcePath}");
                    }

                    return stringValue;
                }

                throw InvalidData($"Card JSON property '{key}' must be an integer: {sourcePath}");
            }
        }

        return null;
    }

    private static bool TryGetPropertyIgnoreCase(
        JsonElement root,
        string name,
        out JsonElement value)
    {
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static Dictionary<string, object?> ConvertObject(JsonElement element)
    {
        Dictionary<string, object?> values = new(StringComparer.OrdinalIgnoreCase);

        foreach (JsonProperty property in element.EnumerateObject())
        {
            values[property.Name] = ConvertValue(property.Value);
        }

        return values;
    }

    private static object? ConvertValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => ConvertObject(element),
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertValue).ToArray(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => ConvertNumber(element),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Undefined => null,
            _ => element.ToString()
        };
    }

    private static object ConvertNumber(JsonElement element)
    {
        if (element.TryGetInt64(out long longValue))
        {
            return longValue;
        }

        return element.GetDouble();
    }

    private static InvalidDataException InvalidData(string message, Exception? innerException = null)
    {
        return new InvalidDataException(message, innerException);
    }
}
