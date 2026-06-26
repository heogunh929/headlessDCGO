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
    int StackDepth);

/// <summary>Builds a <see cref="CardObservation"/> from a card instance and its definition,
/// computing DP through <see cref="DpCalculator"/> so the observed DP matches battle resolution.</summary>
public static class CardObservationView
{
    public const string DpKey = "dp";
    public const string DpModifiersKey = "dpModifiers";
    public const string LevelKey = "level";
    public const string SuspendedKey = "isSuspended";
    public const string FaceUpKey = "isFaceUp";
    public const string SourceIdsKey = "sourceIds";

    public static CardObservation Build(CardInstanceRecord instance, CardRecord? definition)
    {
        ArgumentNullException.ThrowIfNull(instance);

        IReadOnlyDictionary<string, object?> meta = instance.Metadata;
        IReadOnlyDictionary<string, object?>? defMeta = definition?.Metadata;

        int baseDp = ReadInt(meta, DpKey) ?? ReadInt(defMeta, DpKey) ?? 0;
        int dp = DpCalculator.ComputeDp(baseDp, ReadModifiers(meta));

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

    private static IReadOnlyList<DpModifier> ReadModifiers(IReadOnlyDictionary<string, object?> metadata)
    {
        return metadata.TryGetValue(DpModifiersKey, out object? raw) && raw is IEnumerable<DpModifier> modifiers
            ? modifiers.ToArray()
            : Array.Empty<DpModifier>();
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
