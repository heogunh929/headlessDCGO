namespace HeadlessDCGO.Engine.Headless.DataLoading;

using HeadlessDCGO.Engine.Headless.Services;

public sealed class DeckListLoader
{
    public DeckList LoadFile(string path, string? name = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            return LoadLines(File.ReadLines(path), name ?? Path.GetFileNameWithoutExtension(path));
        }
        catch (InvalidDataException ex)
        {
            throw InvalidData($"Deck list file is invalid: {path}. {ex.Message}", ex);
        }
    }

    public async Task<DeckList> LoadFileAsync(
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
            throw InvalidData($"Deck list file is invalid: {path}. {ex.Message}", ex);
        }
    }

    public DeckList ParseCode(string deckCode, string name = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deckCode);
        return LoadLines(ReadLines(deckCode), name);
    }

    public DeckList LoadLines(IEnumerable<string> lines, string name = "")
    {
        ArgumentNullException.ThrowIfNull(lines);

        List<DeckListEntry> mainDeck = new();
        List<DeckListEntry> digitamaDeck = new();
        DeckSection section = DeckSection.Main;

        int lineNumber = 0;
        foreach (string rawLine in lines)
        {
            lineNumber++;
            string line = StripComment(rawLine).Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (TryReadSection(line, out DeckSection parsedSection))
            {
                section = parsedSection;
                continue;
            }

            DeckListEntry entry = ParseEntry(line, lineNumber);
            if (section == DeckSection.Digitama)
            {
                digitamaDeck.Add(entry);
            }
            else
            {
                mainDeck.Add(entry);
            }
        }

        return new DeckList(name, mainDeck.ToArray(), digitamaDeck.ToArray());
    }

    private static DeckListEntry ParseEntry(string line, int lineNumber)
    {
        string[] parts = line
            .Replace(',', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0)
        {
            throw InvalidEntry(lineNumber, "Deck list entry is empty.");
        }

        if (parts.Length > 2)
        {
            throw InvalidEntry(lineNumber, $"Deck list entry has too many fields: {line}");
        }

        string cardId;
        int count;

        if (parts.Length == 1)
        {
            cardId = parts[0];
            count = 1;
        }
        else if (TryParseCount(parts[0], out int leadingCount))
        {
            count = leadingCount;
            cardId = parts[1];
        }
        else
        {
            cardId = parts[0];
            if (!TryParseCount(parts[1], out int trailingCount))
            {
                throw InvalidEntry(lineNumber, $"Deck list count is invalid: {line}");
            }

            count = trailingCount;
        }

        if (count <= 0)
        {
            throw InvalidEntry(lineNumber, $"Deck list count must be positive: {line}");
        }

        if (string.IsNullOrWhiteSpace(cardId))
        {
            throw InvalidEntry(lineNumber, "Deck list card id must not be empty.");
        }

        try
        {
            return new DeckListEntry(new HeadlessEntityId(cardId), count);
        }
        catch (ArgumentException ex)
        {
            throw InvalidEntry(lineNumber, $"Deck list card id is invalid: {cardId}", ex);
        }
    }

    private static bool TryReadSection(string line, out DeckSection section)
    {
        string normalized = line.Trim('[', ']', ' ', '\t').ToLowerInvariant();

        if (normalized is "digitama" or "egg" or "eggs" or "digi-egg" or "digi-eggs")
        {
            section = DeckSection.Digitama;
            return true;
        }

        if (normalized is "main" or "deck" or "main deck" or "main-deck")
        {
            section = DeckSection.Main;
            return true;
        }

        section = DeckSection.Main;
        return false;
    }

    private static bool TryParseCount(string value, out int count)
    {
        string normalized = value.Trim().TrimStart('x', 'X').TrimEnd('x', 'X');
        return int.TryParse(normalized, out count);
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

    private static string StripComment(string line)
    {
        int hashIndex = line.IndexOf('#', StringComparison.Ordinal);
        int slashIndex = line.IndexOf("//", StringComparison.Ordinal);
        int commentIndex = hashIndex < 0
            ? slashIndex
            : slashIndex < 0
                ? hashIndex
                : Math.Min(hashIndex, slashIndex);

        return commentIndex < 0 ? line : line[..commentIndex];
    }

    private enum DeckSection
    {
        Main,
        Digitama
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

public sealed record DeckList(
    string Name,
    IReadOnlyList<DeckListEntry> MainDeck,
    IReadOnlyList<DeckListEntry> DigitamaDeck)
{
    public int MainDeckCount => MainDeck.Sum(entry => entry.Count);

    public int DigitamaDeckCount => DigitamaDeck.Sum(entry => entry.Count);
}

public sealed record DeckListEntry(
    HeadlessEntityId CardId,
    int Count);
