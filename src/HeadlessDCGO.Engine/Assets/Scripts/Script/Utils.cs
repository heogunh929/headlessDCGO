// Source: DCGO/Assets/Scripts/Script/Utils.cs
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

/// <summary>1:1 mirror of AS-IS <c>Utils</c> (DCGO Script/Utils.cs) — a tiny pure text-formatting helper.
/// (File lives at the AS-IS path <c>Script/Utils.cs</c>; namespace kept <c>...CardEffectCommons</c> so
/// existing bare-name references from that namespace resolve unqualified, same convention as
/// <see cref="DataBase"/>/<see cref="GManager"/>.)</summary>
public static class Utils
{
    /// <summary>1:1 mirror of AS-IS <c>Utils.PluralFormSuffix</c> (Utils.cs:7).</summary>
    public static string PluralFormSuffix(int count) => count >= 2 ? "s" : "";
}
