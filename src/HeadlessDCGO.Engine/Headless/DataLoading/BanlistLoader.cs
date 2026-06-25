namespace HeadlessDCGO.Engine.Headless.DataLoading;

using HeadlessDCGO.Engine.Headless.Services;

public sealed class BanlistLoader
{
    public Banlist LoadFile(string path, string? name = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            return LoadLines(File.ReadLines(path), name ?? Path.GetFileNameWithoutExtension(path));
        }
        catch (InvalidDataException ex)
        {
            throw InvalidData($"Banlist file is invalid: {path}. {ex.Message}", ex);
        }
    }

    public async Task<Banlist> LoadFileAsync(
        string path,
        string? name = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string[] lines = await File.ReadAllLinesAsync(path, cancellationToken).ConfigureAwait(false);
        try
        {
            return LoadLines(lines, name ?? Path.GetFileNameWithoutExtension(path));
        }
        catch (InvalidDataException ex)
        {
            throw InvalidData($"Banlist file is invalid: {path}. {ex.Message}", ex);
        }
    }

    public Banlist ParseCode(string banlistCode, string name = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(banlistCode);
        return LoadLines(ReadLines(banlistCode), name);
    }

    public Banlist LoadLines(IEnumerable<string> lines, string name = "")
    {
        ArgumentNullException.ThrowIfNull(lines);

        Dictionary<HeadlessEntityId, int> limits = new();

        int lineNumber = 0;
        foreach (string rawLine in lines)
        {
            lineNumber++;
            string line = StripComment(rawLine).Trim();
            if (line.Length == 0 || IsSectionHeader(line))
            {
                continue;
            }

            BanlistEntry entry = ParseEntry(line, lineNumber);
            limits[entry.CardId] = entry.Limit;
        }

        return new Banlist(name, limits);
    }

    private static BanlistEntry ParseEntry(string line, int lineNumber)
    {
        string[] parts = line
            .Replace(',', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0)
        {
            throw InvalidEntry(lineNumber, "Banlist entry is empty.");
        }

        if (parts.Length > 2)
        {
            throw InvalidEntry(lineNumber, $"Banlist entry has too many fields: {line}");
        }

        string cardId;
        int limit;

        if (parts.Length == 1)
        {
            cardId = parts[0];
            limit = 0;
        }
        else if (int.TryParse(parts[0], out int leadingLimit))
        {
            limit = leadingLimit;
            cardId = parts[1];
        }
        else
        {
            cardId = parts[0];
            if (!int.TryParse(parts[1], out int trailingLimit))
            {
                throw InvalidEntry(lineNumber, $"Banlist limit is invalid: {line}");
            }

            limit = trailingLimit;
        }

        if (limit < 0)
        {
            throw InvalidEntry(lineNumber, $"Banlist limit must be non-negative: {line}");
        }

        if (string.IsNullOrWhiteSpace(cardId))
        {
            throw InvalidEntry(lineNumber, "Banlist card id must not be empty.");
        }

        try
        {
            return new BanlistEntry(new HeadlessEntityId(cardId), limit);
        }
        catch (ArgumentException ex)
        {
            throw InvalidEntry(lineNumber, $"Banlist card id is invalid: {cardId}", ex);
        }
    }

    private static bool IsSectionHeader(string line)
    {
        return line.StartsWith("[", StringComparison.Ordinal) &&
            line.EndsWith("]", StringComparison.Ordinal);
    }

    private static string StripComment(string line)
    {
        int hashIndex = line.IndexOf('#');
        int slashIndex = line.IndexOf("//", StringComparison.Ordinal);
        int commentIndex = hashIndex < 0
            ? slashIndex
            : slashIndex < 0
                ? hashIndex
                : Math.Min(hashIndex, slashIndex);

        return commentIndex < 0 ? line : line[..commentIndex];
    }

    private static IEnumerable<string> ReadLines(string text)
    {
        using var reader = new StringReader(text);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            yield return line;
        }
    }

    private static InvalidDataException InvalidEntry(
        int lineNumber,
        string message,
        Exception? innerException = null)
    {
        return InvalidData($"line {lineNumber}: {message}", innerException);
    }

    private static InvalidDataException InvalidData(string message, Exception? innerException = null)
    {
        return new InvalidDataException(message, innerException);
    }
}

public sealed record Banlist(
    string Name,
    IReadOnlyDictionary<HeadlessEntityId, int> Limits)
{
    public int GetLimit(HeadlessEntityId cardId, int defaultLimit = 4)
    {
        return Limits.TryGetValue(cardId, out int limit)
            ? limit
            : defaultLimit;
    }

    public bool IsBanned(HeadlessEntityId cardId)
    {
        return GetLimit(cardId) == 0;
    }
}

public sealed record BanlistEntry(
    HeadlessEntityId CardId,
    int Limit);
