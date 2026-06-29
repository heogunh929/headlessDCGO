using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

// (G6-003) Offline converter: DCGO/Assets/CardBaseEntity/**/*.asset (Unity YAML, local-only / git-ignored)
//   -> src/HeadlessDCGO.Engine/Assets/CardBaseEntity/cards.json (committed card-stat data).
// Run from the repo root: dotnet run --project tools/CardDataConverter
//
// Parses the flat MonoBehaviour scalar fields needed to instantiate cards with real stats. Color/Set come
// from the directory layout (CardBaseEntity/<Set>/<Color>/<Form>/<id>.asset). cardKind byte0 maps to the
// card type. The first EvoCosts.MemoryCost is taken as the evolution cost.

string repo = Directory.GetCurrentDirectory();
string root = Path.Combine(repo, "DCGO", "Assets", "CardBaseEntity");
string outPath = Path.Combine(repo, "src", "HeadlessDCGO.Engine", "Assets", "CardBaseEntity", "cards.json");

if (!Directory.Exists(root))
{
    Console.Error.WriteLine($"CardBaseEntity not found at {root} (DCGO/ is local-only).");
    return 1;
}

string[] cardTypes = { "Digimon", "Tamer", "Option", "DigiEgg" };
var cards = new List<CardJson>();

foreach (string file in Directory.EnumerateFiles(root, "*.asset", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
{
    string rel = Path.GetRelativePath(root, file).Replace('\\', '/');
    string[] segs = rel.Split('/');
    string set = segs.Length > 0 ? segs[0] : string.Empty;
    string color = segs.Length > 1 ? segs[1] : string.Empty;

    string[] lines = File.ReadAllLines(file);

    string cardNumber = Scalar(lines, "m_Name") ?? Path.GetFileNameWithoutExtension(file);
    int kind = HexByte0(Scalar(lines, "cardKind"));
    int playCost = Int(lines, "PlayCost") ?? -1;

    cards.Add(new CardJson
    {
        cardNumber = cardNumber,
        name = Scalar(lines, "CardName_ENG") ?? cardNumber,
        cardType = kind >= 0 && kind < cardTypes.Length ? cardTypes[kind] : "Unknown",
        set = set,
        color = color,
        colors = Colors(lines),
        level = Int(lines, "Level") ?? 0,
        playCost = playCost < 0 ? null : playCost,
        evolutionCost = FirstEvoCost(lines),
        evolutionConditions = EvoCostList(lines),
        dp = Int(lines, "DP") ?? 0,
        types = Sequence(lines, "Type_ENG"),
        attributes = Sequence(lines, "Attribute_ENG"),
        forms = Sequence(lines, "Form_ENG"),
        effect = Multiline(lines, "EffectDiscription_ENG"),
        inheritedEffect = Multiline(lines, "InheritedEffectDiscription_ENG"),
        securityEffect = Multiline(lines, "SecurityEffectDiscription_ENG"),
        effectClass = Scalar(lines, "CardEffectClassName"),
    });
}

Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
string json = JsonSerializer.Serialize(cards, new JsonSerializerOptions { WriteIndented = false });
File.WriteAllText(outPath, json, new UTF8Encoding(false));
Console.WriteLine($"Wrote {cards.Count} cards -> {outPath} ({new FileInfo(outPath).Length / 1024} KB)");
return 0;

// --- parsing helpers ------------------------------------------------------

static int FindKey(string[] lines, string key)
{
    string needle = "  " + key + ":";
    for (int i = 0; i < lines.Length; i++)
    {
        if (lines[i].StartsWith(needle, StringComparison.Ordinal))
        {
            return i;
        }
    }

    return -1;
}

static string? Scalar(string[] lines, string key)
{
    int i = FindKey(lines, key);
    if (i < 0)
    {
        return null;
    }

    string raw = lines[i][(lines[i].IndexOf(':') + 1)..].Trim();
    return raw.Length == 0 ? null : Clean(raw);
}

static int? Int(string[] lines, string key)
{
    string? s = Scalar(lines, key);
    return s is not null && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : null;
}

// cardKind / cardColors are serialised as little-endian hex bytes ("03000000" -> 3).
static int HexByte0(string? hex)
{
    if (string.IsNullOrWhiteSpace(hex) || hex.Length < 2)
    {
        return -1;
    }

    return int.TryParse(hex.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int b) ? b : -1;
}

// Color enum order (matches the folder layout / standard Digimon TCG ordering).
static string ColorName(int index)
{
    string[] names = { "Red", "Blue", "Yellow", "Green", "Black", "Purple", "White" };
    return index >= 0 && index < names.Length ? names[index] : $"Color{index}";
}

// cardColors is one or more concatenated 4-byte little-endian ints; each chunk's first byte is a color
// index. Multi-color cards have multiple chunks (e.g. "0000000002000000" -> Red + Yellow).
static string[] Colors(string[] lines)
{
    string? hex = Scalar(lines, "cardColors");
    if (string.IsNullOrWhiteSpace(hex))
    {
        return Array.Empty<string>();
    }

    var colors = new List<string>();
    for (int i = 0; i + 8 <= hex.Length; i += 8)
    {
        if (int.TryParse(hex.AsSpan(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int idx))
        {
            string name = ColorName(idx);
            if (!colors.Contains(name))
            {
                colors.Add(name);
            }
        }
    }

    return colors.ToArray();
}

// Read a YAML block sequence ("  Key:" then "  - item" lines) into a string array; "[]" -> empty.
static string[] Sequence(string[] lines, string key)
{
    int i = FindKey(lines, key);
    if (i < 0)
    {
        return Array.Empty<string>();
    }

    var items = new List<string>();
    for (int j = i + 1; j < lines.Length; j++)
    {
        if (Regex.IsMatch(lines[j], @"^  [^ -]"))
        {
            break; // next top-level field
        }

        string t = lines[j].TrimStart();
        if (t.StartsWith("- ", StringComparison.Ordinal))
        {
            string v = Clean(t[2..]);
            if (v.Length > 0)
            {
                items.Add(v);
            }
        }
    }

    return items.ToArray();
}

// Full EvoCosts list: each entry is { CardColor, Level, MemoryCost }.
static List<EvoCostJson> EvoCostList(string[] lines)
{
    int i = FindKey(lines, "EvoCosts");
    var result = new List<EvoCostJson>();
    if (i < 0)
    {
        return result;
    }

    EvoCostJson? current = null;
    for (int j = i + 1; j < lines.Length; j++)
    {
        if (Regex.IsMatch(lines[j], @"^  [^ -]"))
        {
            break; // next top-level field (e.g. the card's own Level)
        }

        string t = lines[j].TrimStart();
        if (t.StartsWith("- CardColor:", StringComparison.Ordinal))
        {
            if (current is not null) result.Add(current);
            current = new EvoCostJson { color = ColorName(ParseInt(t["- CardColor:".Length..])) };
        }
        else if (current is not null && t.StartsWith("Level:", StringComparison.Ordinal))
        {
            current.level = ParseInt(t["Level:".Length..]);
        }
        else if (current is not null && t.StartsWith("MemoryCost:", StringComparison.Ordinal))
        {
            current.cost = ParseInt(t["MemoryCost:".Length..]);
        }
    }

    if (current is not null) result.Add(current);
    return result;
}

static int ParseInt(string s) => int.TryParse(s.Trim(), out int v) ? v : 0;

static int? FirstEvoCost(string[] lines)
{
    int i = FindKey(lines, "EvoCosts");
    if (i < 0)
    {
        return null;
    }

    for (int j = i + 1; j < lines.Length; j++)
    {
        string t = lines[j].TrimStart();
        if (lines[j].StartsWith("  ", StringComparison.Ordinal) && !lines[j].StartsWith("   ", StringComparison.Ordinal) && !t.StartsWith("-", StringComparison.Ordinal))
        {
            break; // next top-level field
        }

        if (t.StartsWith("MemoryCost:", StringComparison.Ordinal)
            && int.TryParse(t["MemoryCost:".Length..].Trim(), out int cost))
        {
            return cost;
        }
    }

    return null;
}

static string? Multiline(string[] lines, string key)
{
    int i = FindKey(lines, key);
    if (i < 0)
    {
        return null;
    }

    var sb = new StringBuilder(lines[i][(lines[i].IndexOf(':') + 1)..].Trim());
    for (int j = i + 1; j < lines.Length; j++)
    {
        // Continuation lines of a wrapped scalar are indented deeper than the 2-space key; a new
        // top-level field ("  Key:") or list item ("  -") ends it.
        if (Regex.IsMatch(lines[j], @"^  \S"))
        {
            break;
        }

        sb.Append(' ').Append(lines[j].Trim());
    }

    string text = Clean(sb.ToString());
    return string.IsNullOrWhiteSpace(text) ? null : text;
}

// Strip YAML quoting, unescape \uXXXX, collapse whitespace.
static string Clean(string raw)
{
    string s = raw.Trim();
    if (s.Length >= 2 && ((s[0] == '\'' && s[^1] == '\'') || (s[0] == '"' && s[^1] == '"')))
    {
        char q = s[0];
        s = s[1..^1];
        s = q == '\'' ? s.Replace("''", "'") : Unescape(s);
    }

    return Regex.Replace(s, @"\s+", " ").Trim();
}

static string Unescape(string s) =>
    Regex.Replace(s, @"\\u([0-9A-Fa-f]{4})", m => ((char)int.Parse(m.Groups[1].Value, NumberStyles.HexNumber)).ToString())
        .Replace("\\\"", "\"").Replace("\\\\", "\\");

sealed class CardJson
{
    public string cardNumber { get; set; } = string.Empty;
    public string name { get; set; } = string.Empty;
    public string cardType { get; set; } = string.Empty;
    public string set { get; set; } = string.Empty;
    public string color { get; set; } = string.Empty;
    public string[] colors { get; set; } = Array.Empty<string>();
    public int level { get; set; }
    public int? playCost { get; set; }
    public int? evolutionCost { get; set; }
    public List<EvoCostJson> evolutionConditions { get; set; } = new();
    public int dp { get; set; }
    public string[] types { get; set; } = Array.Empty<string>();
    public string[] attributes { get; set; } = Array.Empty<string>();
    public string[] forms { get; set; } = Array.Empty<string>();
    public string? effect { get; set; }
    public string? inheritedEffect { get; set; }
    public string? securityEffect { get; set; }
    public string? effectClass { get; set; }
}

sealed class EvoCostJson
{
    public string color { get; set; } = string.Empty;
    public int level { get; set; }
    public int cost { get; set; }
}
