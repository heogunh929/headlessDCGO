namespace HeadlessDCGO.Engine.Headless.Runtime;

using System.Globalization;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

/// <summary>
/// Typed per-card view exposed in the observation (G3.5-RL-A4b / B1b-lite). Consolidates the
/// card stats a policy needs — identity, computed DP, level, costs, suspend/face-up, stack depth —
/// into one typed record instead of scattered untyped metadata reads.
/// </summary>
public sealed record CardObservation(
    HeadlessEntityId InstanceId,
    string CardNumber,
    string CardType,
    int Dp,
    int Level,
    int PlayCost,
    int EvolutionCost,
    bool IsSuspended,
    bool IsFaceUp,
    int StackDepth)
{
    // (M2, 설계 §5-3) Digivolution-stack composition wired from DigivolutionStackReader: the cards
    // stacked under this one, bottom (DigiEgg) first. Empty when the card has no sources or the
    // observation was built without repository access (legacy Build overload).
    public IReadOnlyList<StackedCard> UnderCards { get; init; } = Array.Empty<StackedCard>();
}

/// <summary>Builds a <see cref="CardObservation"/> from a card instance and its definition.
/// (DP re-migration) DP used to be folded by the substrate <c>DpCalculator.ComputeDp(baseDp, dpModifiers)</c>,
/// whose <c>dpModifiers</c> instance-metadata channel has ZERO writers on the whole tree — so the fold was the
/// identity <c>max(0, baseDp)</c> and the observation reported PRINTED DP, never the effective one. The AS-IS DP
/// source is <c>Permanent.BaseDP</c>/<c>GetDP</c> (DCGO Permanent.cs:193/327), mirrored 1:1 by the live
/// <c>Permanent.DP</c> getter (continuous IChangeDPEffect fold + LinkedDP + Boosts). The
/// <see cref="BuildFieldPermanent"/> overload uses it, so a battle/breeding-area card's observed DP matches
/// battle resolution; off-field zones keep the printed value (AS-IS has no Permanent for a hand/deck/trash card,
/// and the fold's field scans would be meaningless there).</summary>
public static class CardObservationView
{
    public const string DpKey = "dp";
    public const string DpModifiersKey = "dpModifiers";
    public const string LevelKey = "level";
    public const string SuspendedKey = "isSuspended";
    public const string FaceUpKey = "isFaceUp";
    public const string SourceIdsKey = "sourceIds";

    // (M2) Repository-aware overload: additionally resolves the digivolution-stack composition
    // (under-card identities) through DigivolutionStackReader. Only cards that actually carry
    // sourceIds pay the reader cost.
    public static CardObservation Build(
        CardInstanceRecord instance,
        CardRecord? definition,
        ICardInstanceRepository instances,
        ICardRepository cards)
    {
        ArgumentNullException.ThrowIfNull(instances);
        ArgumentNullException.ThrowIfNull(cards);

        CardObservation observation = Build(instance, definition);
        if (observation.StackDepth <= 0)
        {
            return observation;
        }

        DigivolutionStack stack = DigivolutionStackReader.Read(instances, cards, instance.InstanceId);
        return stack.IsEmpty ? observation : observation with { UnderCards = stack.UnderCards };
    }

    public static CardObservation Build(CardInstanceRecord instance, CardRecord? definition)
    {
        ArgumentNullException.ThrowIfNull(instance);

        IReadOnlyDictionary<string, object?> meta = instance.Metadata;
        IReadOnlyDictionary<string, object?>? defMeta = definition?.Metadata;

        int baseDp = ReadInt(meta, DpKey) ?? ReadInt(defMeta, DpKey) ?? 0;
        int dp = baseDp < 0 ? 0 : baseDp;

        int level = ReadInt(meta, LevelKey) ?? ReadInt(defMeta, LevelKey) ?? ReadInt(defMeta, "Level") ?? 0;
        int playCost = definition?.PlayCost ?? ReadInt(defMeta, "playCost") ?? 0;
        int evolutionCost = definition?.EvolutionCost ?? ReadInt(defMeta, "evolutionCost") ?? 0;

        return new CardObservation(
            instance.InstanceId,
            definition?.CardNumber ?? string.Empty,
            definition?.CardType ?? "Unknown",
            dp,
            level,
            playCost,
            evolutionCost,
            ReadBool(meta, SuspendedKey),
            ReadBool(meta, FaceUpKey),
            ReadStackDepth(meta));
    }

    /// <summary>(DP re-migration) A battle/breeding-area card's observation, with DP taken from the AS-IS
    /// source — the mirror <c>Permanent.DP</c> getter (AS-IS <c>Permanent.BaseDP</c>/<c>GetDP</c>), i.e. the
    /// EFFECTIVE value the battle pipeline compares. Off-field callers stay on <see cref="Build(CardInstanceRecord,
    /// CardRecord?, ICardInstanceRepository, ICardRepository)"/>, which reports the printed DP.</summary>
    public static CardObservation BuildFieldPermanent(
        Bridge.EngineContext context,
        CardInstanceRecord instance,
        CardRecord? definition,
        ICardInstanceRepository instances,
        ICardRepository cards)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(instance);

        CardObservation observation = Build(instance, definition, instances, cards);
        int dp = new Assets.Scripts.Script.CardEffectCommons.Permanent(context, instance.InstanceId, instance.OwnerId).DP;
        return observation with { Dp = dp < 0 ? 0 : dp };
    }

    private static int ReadStackDepth(IReadOnlyDictionary<string, object?> metadata)
    {
        if (!metadata.TryGetValue(SourceIdsKey, out object? raw) || raw is null)
        {
            return 0;
        }

        return raw switch
        {
            System.Collections.ICollection collection => collection.Count,
            System.Collections.IEnumerable enumerable => enumerable.Cast<object?>().Count(),
            _ => 0
        };
    }

    private static bool ReadBool(IReadOnlyDictionary<string, object?> metadata, string key)
    {
        return metadata.TryGetValue(key, out object? raw) && raw is bool value && value;
    }

    private static int? ReadInt(IReadOnlyDictionary<string, object?>? metadata, string key)
    {
        if (metadata is null || !metadata.TryGetValue(key, out object? raw) || raw is null)
        {
            return null;
        }

        return raw switch
        {
            int intValue => intValue,
            long longValue when longValue >= int.MinValue && longValue <= int.MaxValue => (int)longValue,
            double doubleValue when doubleValue % 1 == 0 && doubleValue is >= int.MinValue and <= int.MaxValue => (int)doubleValue,
            string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) => parsed,
            _ => null
        };
    }
}
