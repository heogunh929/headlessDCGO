namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;
// Aliased (not a namespace import) to avoid pulling the sibling `...Script.CardEffectFactory` namespace
// into scope, which would clash with the CardEffectFactory type below.
using SelectPermanentEffect = HeadlessDCGO.Engine.Assets.Scripts.Script.SelectPermanentEffect;
using PartitionCondition = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory.KeyWordEffects.PartitionCondition;


/// <summary>
/// (G6-001) Maps a card number to its ported effect class. A ported card is a non-abstract
/// <see cref="CEntity_Effect"/> subclass whose type name equals the card number (e.g. class
/// <c>ST1_01</c> -> card "ST1_01"), so the dispatch is discovered by reflection — no manual table, and it
/// auto-grows as cards are ported. Un-ported cards (skeleton files with no class) simply aren't found.
/// </summary>
public static class CardEffectDispatch
{
    private static readonly Lazy<IReadOnlyDictionary<string, Type>> ByCardNumber = new(Build);

    private static IReadOnlyDictionary<string, Type> Build()
    {
        var map = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        foreach (Type type in typeof(CEntity_Effect).Assembly.GetTypes())
        {
            if (type.IsAbstract
                || !type.IsSubclassOf(typeof(CEntity_Effect))
                || type.GetConstructor(Type.EmptyTypes) is null)
            {
                continue;
            }

            map[type.Name] = type;
        }

        return map;
    }

    public static int Count => ByCardNumber.Value.Count;

    public static bool TryCreate(string? cardNumber, out CEntity_Effect? effect)
    {
        effect = null;
        if (string.IsNullOrWhiteSpace(cardNumber) || !ByCardNumber.Value.TryGetValue(cardNumber.Trim(), out Type? type))
        {
            return false;
        }

        effect = (CEntity_Effect)Activator.CreateInstance(type)!;
        return true;
    }

    /// <summary>
    /// Resolves a card's effect class honoring the <c>effectClass</c> alias. cards.json carries an
    /// <c>effectClass</c> per card which is authoritative: for most cards it equals the card number, but
    /// alias cards (e.g. ST2_07 / ST3_07 reuse <c>ST1_06</c>, and every alternate-art reprint <c>*_P2</c>
    /// reuses its base) point at another class. When the metadata carries a non-empty effectClass we resolve
    /// by it exclusively (an un-ported alias is a no-op, like an un-ported card); otherwise we fall back to
    /// the card number — so test-constructed records without effectClass metadata behave exactly as before.
    /// </summary>
    public static bool TryCreateForCard(CardRecord def, out CEntity_Effect? effect)
    {
        effect = null;
        if (def is null)
        {
            return false;
        }

        if (def.Metadata.TryGetValue("effectClass", out object? raw)
            && raw is string alias
            && !string.IsNullOrWhiteSpace(alias))
        {
            return TryCreate(alias, out effect);
        }

        return TryCreate(def.CardNumber, out effect);
    }
}

