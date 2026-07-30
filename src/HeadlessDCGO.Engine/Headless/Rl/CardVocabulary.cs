// ============================================================================================================
// THE CARD VOCABULARY — the one mapping cardNumber → embedding id both sides of the bridge must agree on.
//
// THE CONTRACT IS OWNED BY THE PYTHON SIDE (rl/dcgo_rl/cards.py): canonicalisation = trim, upper-case,
// '-'→'_', collapse illustration-variant suffix `_P\d+$`; vocabulary = canonical numbers sorted ordinal,
// ids 1..N (0 = PAD); hash = sha256 of ",".join("number:id") ordered by id. The trainer REFUSES the
// handshake when the hashes differ (protocol §2), so this file replicates that rule EXACTLY — and both
// sides read the same data file, `src/HeadlessDCGO.Engine/Assets/CardBaseEntity/cards.json`
// (이중 구현 금지). The json is exported from the DCGO card assets by `RlBridgeHost --export-cards-json`.
// ============================================================================================================

namespace HeadlessDCGO.Engine.Headless.Rl;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

public sealed class CardVocabulary
{
    public const int PadId = 0;

    private readonly Dictionary<string, int> _ids;

    private CardVocabulary(Dictionary<string, int> ids, string hash)
    {
        _ids = ids;
        Hash = hash;
    }

    public int Count => _ids.Count;

    public string Hash { get; }

    public string Version => "v1";

    private static readonly Regex VariantSuffix = new(@"_P\d+$", RegexOptions.Compiled);

    /// <summary>rl/dcgo_rl/cards.py `canonical_card_number`와 문자 단위로 동일해야 한다.</summary>
    public static string Canonical(string raw) =>
        VariantSuffix.Replace(raw.Trim().ToUpperInvariant().Replace('-', '_'), "");

    public int IdOf(string cardNumber) =>
        _ids.TryGetValue(Canonical(cardNumber), out int id)
            ? id
            : throw new KeyNotFoundException($"vocab에 없는 카드번호: {cardNumber} (조용한 fallback 금지)");

    private Dictionary<int, string>? _numbers;

    /// <summary>id → 정규 카드번호 역인덱스 — 아레나 서술 페이로드용. PadId·미지 id는 null.</summary>
    public string? NumberOf(int id)
    {
        _numbers ??= _ids.ToDictionary(pair => pair.Value, pair => pair.Key);

        return _numbers.TryGetValue(id, out string? number) ? number : null;
    }

    public static CardVocabulary FromCardsJson(string path)
    {
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));

        HashSet<string> canonicals = new(StringComparer.Ordinal);

        foreach (JsonElement record in doc.RootElement.EnumerateArray())
        {
            canonicals.Add(Canonical(record.GetProperty("cardNumber").GetString()!));
        }

        List<string> ordered = canonicals.ToList();
        ordered.Sort(StringComparer.Ordinal);

        Dictionary<string, int> ids = new(StringComparer.Ordinal);

        for (int i = 0; i < ordered.Count; i++)
        {
            ids[ordered[i]] = i + 1;
        }

        string joined = string.Join(",", ordered.Select(number => $"{number}:{ids[number]}"));
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(joined))).ToLowerInvariant();

        return new CardVocabulary(ids, hash);
    }

    /// <summary>DCGO 카드 자산 → cards.json. 파이썬 오프라인 경로(CardIndex.load)와 이 vocab이 같은
    /// 파일을 읽게 하는 내보내기. cardType은 파이썬이 유일하게 판정에 쓰는 "DigiEgg"만 정확하면 된다.</summary>
    public static void ExportCardsJson(IEnumerable<CEntity_Base> cards, string path)
    {
        var records = cards
            .Select(card => new
            {
                cardNumber = card.CardID,
                name = string.IsNullOrEmpty(card.CardName_ENG) ? card.CardName_JPN : card.CardName_ENG,
                cardType = card.cardKind.Contains(CardKind.DigiEgg)
                    ? "DigiEgg"
                    : card.cardKind.Count > 0 ? card.cardKind[0].ToString() : "Unknown",
            })
            .ToArray();

        File.WriteAllText(path, JsonSerializer.Serialize(records,
            new JsonSerializerOptions { WriteIndented = false }));
    }
}
