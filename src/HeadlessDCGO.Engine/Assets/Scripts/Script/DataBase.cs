// Source: DCGO/Assets/Scripts/Script/DataBase.cs
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

/// <summary>(EFFECT-MODEL REBUILD) Minimal mirror of AS-IS <c>DataBase</c> (DCGO Script/DataBase.cs) — the
/// large static card-data / text service. Only the members ported effect code needs so far are mirrored here,
/// grown member-by-member as ports require (not ported wholesale). Current members: <see cref="ReplaceToASCII"/>.
/// (File lives at the AS-IS path <c>Script/DataBase.cs</c>; namespace kept <c>...CardEffectCommons</c> so
/// existing references are unaffected — a later namespace-normalisation pass, not a path concern.)</summary>
public static class DataBase
{
    /// <summary>1:1 mirror of AS-IS <c>DataBase.ReplaceToASCII</c> (DataBase.cs:567): normalises full-width /
    /// Japanese punctuation in effect descriptions to ASCII so the <c>[On Play]</c>-style prefix checks in
    /// <see cref="ICardEffect.IsOnPlay"/> etc. match. Verbatim replacement set.</summary>
    public static string ReplaceToASCII(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "";
        }

        return text
            .Replace("＜", "<")
            .Replace("＞", ">")
            .Replace("、", ",")
            .Replace("，", ",")
            .Replace("“", "\"")
            .Replace("・", "")
            .Replace("　", "")
            .Replace("！", "!");
    }

    /// <summary>(bridge W5) 1:1 mirror of AS-IS <c>DataBase.IsXAntibodyString</c> (DataBase.cs:440): the
    /// space/hyphen-insensitive "X Antibody" trait/name normaliser <c>CardSource.HasXAntibodyTraits</c> reads.
    /// Verbatim (pure string helper).</summary>
    public static bool IsXAntibodyString(string text) =>
    !string.IsNullOrEmpty(text) && text.Replace(" ", "").Replace("-", "").ToLower() == "xantibody";
}
