namespace HeadlessDCGO.Engine.Headless.Runtime;

using System.Text.RegularExpressions;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (M2, 설계 §4.1 / dev design §1.3) Card-number → integer id vocabulary for card-identity
/// embedding. Single source of truth is the engine card repository; the canonicalization rule
/// (trim / upper / hyphen→underscore / illustration-variant collapse) MUST stay identical to the
/// Python trainer's <c>dcgo_rl.cards.canonical_card_number</c> — the bridge handshake compares
/// vocabulary versions, and a silent rule drift would desync embeddings from recipes.
/// Id 0 is reserved for PAD (empty slot); real cards start at 1. Extension is append-only
/// (FR-5.3: new cards get new tail slots, existing ids never move).
/// </summary>
public sealed partial class CardVocabulary
{
    public const int PadId = 0;

    private readonly Dictionary<string, int> _ids;

    private CardVocabulary(string version, Dictionary<string, int> ids)
    {
        Version = version;
        _ids = ids;
    }

    public string Version { get; }

    public int Count => _ids.Count;

    /// <summary>id 오름차순 (번호, id) 열거 — 핸드셰이크 vocab 해시(프로토콜 v1 §2)의 입력.</summary>
    public IReadOnlyList<KeyValuePair<string, int>> Entries =>
        _ids.OrderBy(pair => pair.Value).ToArray();

    [GeneratedRegex(@"_P\d+$")]
    private static partial Regex VariantSuffix();

    /// <summary>BT1-020 / bt1-020_P1 → BT1_020. Promo numbers (P_016) are not suffixes and stay.</summary>
    public static string CanonicalCardNumber(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        string normalized = raw.Trim().ToUpperInvariant().Replace('-', '_');
        return VariantSuffix().Replace(normalized, string.Empty);
    }

    public static CardVocabulary Build(IEnumerable<string> cardNumbers, string version = "v1")
    {
        ArgumentNullException.ThrowIfNull(cardNumbers);

        string[] ordered = cardNumbers
            .Select(CanonicalCardNumber)
            .Where(number => number.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Dictionary<string, int> ids = new(ordered.Length, StringComparer.Ordinal);
        for (int index = 0; index < ordered.Length; index++)
        {
            ids[ordered[index]] = index + 1; // 0 = PAD
        }

        return new CardVocabulary(version, ids);
    }

    public static CardVocabulary FromRepository(ICardRepository cards, string version = "v1")
    {
        ArgumentNullException.ThrowIfNull(cards);
        return Build(cards.Snapshot().Select(card => card.CardNumber), version);
    }

    public bool TryGetId(string cardNumber, out int id)
    {
        return _ids.TryGetValue(CanonicalCardNumber(cardNumber), out id);
    }

    /// <summary>Unknown card = explicit failure (조용한 0 매핑 금지 — 임베딩/레시피 desync 조기 검출).</summary>
    public int GetId(string cardNumber)
    {
        if (!TryGetId(cardNumber, out int id))
        {
            throw new KeyNotFoundException($"Card number '{cardNumber}' is not in vocabulary {Version}.");
        }

        return id;
    }

    /// <summary>Append-only extension: existing ids are preserved, new canonical numbers are added
    /// after the current max id in sorted order.</summary>
    public CardVocabulary Extend(IEnumerable<string> newCardNumbers, string version)
    {
        ArgumentNullException.ThrowIfNull(newCardNumbers);

        string[] fresh = newCardNumbers
            .Select(CanonicalCardNumber)
            .Where(number => number.Length > 0 && !_ids.ContainsKey(number))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Dictionary<string, int> merged = new(_ids, StringComparer.Ordinal);
        int nextId = _ids.Count == 0 ? 1 : _ids.Values.Max() + 1;
        foreach (string number in fresh)
        {
            merged[number] = nextId++;
        }

        return new CardVocabulary(version, merged);
    }
}
