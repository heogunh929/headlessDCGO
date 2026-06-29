namespace HeadlessDCGO.Engine.Headless.Runtime;

using System.Globalization;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (F-8.5 producer) DP-zero deletion rule: a battle-area Digimon whose RESOLVED DP (printed + continuous
/// modifiers, via <see cref="ContinuousDpGate"/>) is 0 or less is deleted. Each such card is stamped
/// <c>DPZero</c> on its metadata so an <c>IsDpZeroDelete</c> condition can distinguish it from a
/// battle/effect deletion. Mirrors the AS-IS continuous "DP &lt;= 0 → delete" check.
/// </summary>
public static class DpZeroDeletionHelpers
{
    public const string DpKey = "dp";
    public const string DpZeroKey = "DPZero";
    public const string DeletedByEffectKey = "deletedByEffect";

    /// <summary>Delete every battle-area Digimon of <paramref name="players"/> whose resolved DP is &lt;= 0.
    /// Returns the deleted card ids.</summary>
    public static async Task<IReadOnlyList<HeadlessEntityId>> SweepAsync(
        EngineContext context,
        IEnumerable<HeadlessPlayerId> players,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(players);
        if (context.ZoneMover is not IZoneStateReader zones)
        {
            return Array.Empty<HeadlessEntityId>();
        }

        var deleted = new List<HeadlessEntityId>();
        foreach (HeadlessPlayerId player in players)
        {
            // Snapshot — the battle-area list mutates as we delete.
            foreach (HeadlessEntityId cardId in zones.GetCards(player, ChoiceZone.BattleArea).ToArray())
            {
                if (!context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? instance) || instance is null)
                {
                    continue;
                }

                // Only Digimon with a printed DP participate in the DP-zero rule.
                int? baseDp = ReadInt(instance.Metadata, DpKey);
                if (baseDp is not int dp)
                {
                    continue;
                }

                if (ContinuousDpGate.ResolveDp(context, cardId, dp) > 0)
                {
                    continue;
                }

                var metadata = new Dictionary<string, object?>(instance.Metadata, StringComparer.Ordinal)
                {
                    [DpZeroKey] = true,
                    [DeletedByEffectKey] = true,
                };
                context.CardInstanceRepository.Upsert(instance with { Metadata = metadata });
                await context.ZoneMover.MoveAsync(
                    new ZoneMoveRequest(instance.OwnerId, cardId, ChoiceZone.BattleArea, ChoiceZone.Trash),
                    cancellationToken).ConfigureAwait(false);
                deleted.Add(cardId);
            }
        }

        return deleted;
    }

    private static int? ReadInt(IReadOnlyDictionary<string, object?> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out object? raw) || raw is null)
        {
            return null;
        }

        return raw switch
        {
            int i => i,
            long l when l >= int.MinValue && l <= int.MaxValue => (int)l,
            string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) => parsed,
            _ => null,
        };
    }
}
