// ============================================================================================================
// THE HEADLESS REPLACEMENT FOR UNITY'S SCRIPTABLEOBJECT DESERIALISER.
//
// WHY THIS FILE EXISTS. Card definitions are Unity assets: `CEntity_Base : ScriptableObject`, one `.asset` per
// card (8,187 of them under DCGO/Assets/CardBaseEntity/). Unity's serialiser turns those YAML files into
// objects; there is no such serialiser here, and no AS-IS *file* to mirror — reading the game's own data
// format is substrate work, like the scene and the coroutine driver.
//
// WHY IT IS LOAD-BEARING. `ContinuousController.getCardEntityByCardID` (ContinuousController.cs:1084) is
//     SortedCardList.First(entity => entity.CardIndex == cardIndex)
// — `First`, not `FirstOrDefault`. A deck naming a card that is not in the list throws rather than skipping,
// so a PARTIAL load is not a degraded mode, it is a crash. Everything downstream depends on this:
//     DeckData.DeckCards() -> getCardEntityByCardID -> CardObjectController.CreateCardSource -> LibraryCards
//
// THE FORMAT. Unity YAML, one MonoBehaviour document per file. Scalars and string lists map to the AS-IS
// field names directly (`CardIndex`, `PlayCost`, `Level`, `CardName_ENG`, …), so the mapping is by NAME and
// needs no table to drift out of date. Two encodings need decoding:
//
//   enum lists   `cardColors: 00000000` — Unity packs a List<enum> as a hex byte string, 4 little-endian
//                bytes per element. `00000000` is one element, value 0 (CardColor.Red). An empty list is an
//                empty scalar.
//   sequences    `Form_ENG:` followed by `- Rookie` lines, and `EvoCosts:` followed by `- CardColor: 0` /
//                `Level: 2` / `MemoryCost: 0` blocks.
//
// Strings arrive as plain scalars, single-quoted (with '' escapes), or double-quoted with \uXXXX escapes for
// the Japanese text. All three are handled; the Japanese fields are not read by any rule, but mangling them
// would corrupt data we were asked to carry verbatim.
//
// WHAT IS DELIBERATELY NOT DONE
//   * No sprite/image loading. `CardSprite` stays null; nothing in the rules reads it (measured: every
//     GetSprite caller is presentation), and there is no screen.
//   * No caching to a converted file. The `.asset` tree is the game's own data and reading it directly means
//     there is no second copy to drift. If load time becomes a problem, cache then — with a staleness check.
// ============================================================================================================

namespace HeadlessDCGO.Engine.Headless.DataLoading;

using System.Globalization;
using System.Reflection;
using System.Text;

/// <summary>Reads Unity `.asset` card definitions into the AS-IS <see cref="CEntity_Base"/> type.</summary>
public static class CardEntityLoader
{
    private const BindingFlags AnyInstance =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    /// <summary>Loads every card definition under <paramref name="cardBaseEntityRoot"/>, ordered by
    /// <c>CardIndex</c> as <c>ContinuousController.SortedCardList</c> expects.</summary>
    public static CEntity_Base[] LoadAll(string cardBaseEntityRoot)
    {
        if (!Directory.Exists(cardBaseEntityRoot))
        {
            throw new DirectoryNotFoundException(
                $"Card definitions not found at '{cardBaseEntityRoot}'. They live in the local DCGO checkout, "
                + "which is not committed — see docs/roadmap.md.");
        }

        List<CEntity_Base> cards = new();

        foreach (string path in Directory.EnumerateFiles(cardBaseEntityRoot, "*.asset", SearchOption.AllDirectories))
        {
            if (Load(path) is { } card && card.CardIndex > 0)
            {
                cards.Add(card);
            }
        }

        return cards.OrderBy(card => card.CardIndex).ToArray();
    }

    /// <summary>Reads one `.asset`. Returns null when the file is not a card definition (the tree also holds
    /// loader//index assets that carry no CardIndex).</summary>
    public static CEntity_Base? Load(string path)
    {
        string[] lines = File.ReadAllLines(path, Encoding.UTF8);
        CEntity_Base card = new();
        bool sawField = false;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];

            // Only top-level `  Key: value` entries; deeper indentation belongs to a sequence handled below.
            if (!line.StartsWith("  ", StringComparison.Ordinal) || line.StartsWith("   ", StringComparison.Ordinal))
            {
                continue;
            }

            int colon = line.IndexOf(':', StringComparison.Ordinal);

            if (colon < 0)
            {
                continue;
            }

            string key = line[2..colon].Trim();
            string value = line[(colon + 1)..].Trim();

            if (key.StartsWith("m_", StringComparison.Ordinal))
            {
                continue;
            }

            FieldInfo? field = typeof(CEntity_Base).GetField(key, AnyInstance);

            if (field is null)
            {
                continue;
            }

            sawField = true;

            if (value.Length == 0 && IsSequenceAt(lines, i + 1))
            {
                ReadSequence(card, field, lines, ref i);

                continue;
            }

            AssignScalar(card, field, value, lines, ref i);
        }

        return sawField ? card : null;
    }

    private static bool IsSequenceAt(string[] lines, int index) =>
        index < lines.Length && lines[index].TrimStart().StartsWith("- ", StringComparison.Ordinal);

    /// <summary>Reads a `- item` sequence into a List field. Element blocks (EvoCosts) carry their own
    /// `Key: value` lines at deeper indentation.</summary>
    private static void ReadSequence(CEntity_Base card, FieldInfo field, string[] lines, ref int i)
    {
        Type element = field.FieldType.IsGenericType
            ? field.FieldType.GetGenericArguments()[0]
            : typeof(string);

        if (Activator.CreateInstance(field.FieldType) is not System.Collections.IList list)
        {
            return;
        }

        object? current = null;

        while (i + 1 < lines.Length)
        {
            string next = lines[i + 1];
            string trimmed = next.TrimStart();

            bool isItem = trimmed.StartsWith("- ", StringComparison.Ordinal);
            bool isContinuation = current is not null
                && next.StartsWith("    ", StringComparison.Ordinal)
                && trimmed.Contains(':', StringComparison.Ordinal);

            if (!isItem && !isContinuation)
            {
                break;
            }

            i++;

            string body = isItem ? trimmed[2..].Trim() : trimmed;

            if (element == typeof(string))
            {
                list.Add(DecodeString(body));
                current = null;

                continue;
            }

            if (isItem)
            {
                current = Activator.CreateInstance(element);
                list.Add(current);
            }

            if (current is null)
            {
                continue;
            }

            int colon = body.IndexOf(':', StringComparison.Ordinal);

            if (colon < 0)
            {
                continue;
            }

            FieldInfo? member = element.GetField(body[..colon].Trim(), AnyInstance);
            member?.SetValue(current, ConvertScalar(member.FieldType, body[(colon + 1)..].Trim()));
        }

        field.SetValue(card, list);
    }

    private static void AssignScalar(CEntity_Base card, FieldInfo field, string value, string[] lines, ref int i)
    {
        if (field.FieldType.IsGenericType)
        {
            // Unity packs a List<enum> as a hex byte string: 4 little-endian bytes per element.
            // Any other list reaching here is EMPTY in the file (`EvoCosts:` with nothing under it) — the
            // sequence reader only runs when a `- item` follows, so this is where an empty one lands.
            field.SetValue(
                card,
                field.FieldType.GetGenericArguments()[0].IsEnum
                    ? DecodeEnumList(field.FieldType, value)
                    : Activator.CreateInstance(field.FieldType));

            return;
        }

        if (field.FieldType == typeof(string))
        {
            field.SetValue(card, DecodeString(JoinFolded(value, lines, ref i)));

            return;
        }

        field.SetValue(card, ConvertScalar(field.FieldType, value));
    }

    /// <summary>YAML folds long scalars onto following, more-indented lines. Rejoins them with a space, as the
    /// folded style means.</summary>
    private static string JoinFolded(string value, string[] lines, ref int i)
    {
        if (value.Length == 0)
        {
            return value;
        }

        bool open = value[0] == '\'' && !EndsQuoted(value, '\'');
        open |= value[0] == '"' && !EndsQuoted(value, '"');

        StringBuilder builder = new(value);

        while (i + 1 < lines.Length && lines[i + 1].StartsWith("    ", StringComparison.Ordinal))
        {
            string continuation = lines[i + 1].Trim();

            if (!open && continuation.Contains(':', StringComparison.Ordinal))
            {
                break;
            }

            builder.Append(' ').Append(continuation);
            i++;

            if (open && EndsQuoted(continuation, value[0]))
            {
                break;
            }
        }

        return builder.ToString();
    }

    private static bool EndsQuoted(string text, char quote) => text.Length > 1 && text[^1] == quote;

    private static object ConvertScalar(Type type, string value)
    {
        if (type.IsEnum)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int enumValue)
                ? Enum.ToObject(type, enumValue)
                : Enum.ToObject(type, 0);
        }

        if (type == typeof(int))
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) ? number : 0;
        }

        if (type == typeof(bool))
        {
            return value is "1" or "true" or "True";
        }

        if (type == typeof(float))
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : 0f;
        }

        return DecodeString(value);
    }

    /// <summary>`00000000` -> one element, value 0. Four little-endian bytes per enum element.</summary>
    private static object DecodeEnumList(Type listType, string hex)
    {
        object list = Activator.CreateInstance(listType)!;

        if (list is not System.Collections.IList target || hex.Length < 8)
        {
            return list;
        }

        Type element = listType.GetGenericArguments()[0];

        for (int offset = 0; offset + 8 <= hex.Length; offset += 8)
        {
            int value = 0;

            for (int b = 0; b < 4; b++)
            {
                value |= Convert.ToInt32(hex.Substring(offset + (b * 2), 2), 16) << (8 * b);
            }

            target.Add(Enum.ToObject(element, value));
        }

        return list;
    }

    /// <summary>Unwraps the three scalar forms Unity emits: plain, single-quoted (with '' escapes), and
    /// double-quoted with \uXXXX escapes for the Japanese text.</summary>
    private static string DecodeString(string value)
    {
        if (value.Length >= 2 && value[0] == '\'' && value[^1] == '\'')
        {
            return value[1..^1].Replace("''", "'", StringComparison.Ordinal);
        }

        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            return Unescape(value[1..^1]);
        }

        return value;
    }

    private static string Unescape(string value)
    {
        StringBuilder builder = new(value.Length);

        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] != '\\' || i + 1 >= value.Length)
            {
                builder.Append(value[i]);

                continue;
            }

            char escape = value[++i];

            switch (escape)
            {
                case 'u' when i + 4 < value.Length:
                    builder.Append((char)Convert.ToInt32(value.Substring(i + 1, 4), 16));
                    i += 4;

                    break;

                case 'n': builder.Append('\n'); break;
                case 't': builder.Append('\t'); break;
                case 'r': builder.Append('\r'); break;
                default: builder.Append(escape); break;
            }
        }

        return builder.ToString();
    }
}
