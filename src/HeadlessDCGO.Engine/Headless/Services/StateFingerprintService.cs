namespace HeadlessDCGO.Engine.Headless.Services;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.State;

public sealed class StateFingerprintService : IStateFingerprintService
{
    public static StateFingerprintService Default { get; } = new();

    public string BuildCanonicalSnapshot(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return BuildCanonicalSnapshot(state.Snapshot());
    }

    public string BuildCanonicalSnapshot(MatchStateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        StringBuilder builder = new();
        builder.Append("version=").Append(snapshot.Version).Append('\n');
        builder.Append("terminal=").Append(snapshot.IsTerminal).Append('\n');

        foreach (PlayerState player in snapshot.Players.OrderBy(player => player.PlayerId.Value))
        {
            AppendPlayer(builder, player);
        }

        foreach (CardInstanceState instance in snapshot.CardInstances
                     .OrderBy(instance => instance.InstanceId.Value, StringComparer.Ordinal))
        {
            builder.Append("card|")
                .Append(instance.FingerprintSegment())
                .Append('\n');
        }

        foreach (GameEvent gameEvent in snapshot.Events
                     .OrderBy(gameEvent => gameEvent.Sequence)
                     .ThenBy(gameEvent => gameEvent.Type.ToString(), StringComparer.Ordinal)
                     .ThenBy(gameEvent => gameEvent.Message, StringComparer.Ordinal))
        {
            AppendEvent(builder, gameEvent);
        }

        return builder.ToString();
    }

    public string ComputeFingerprint(MatchState state)
    {
        return ComputeHash(BuildCanonicalSnapshot(state));
    }

    public string ComputeFingerprint(MatchStateSnapshot snapshot)
    {
        return ComputeHash(BuildCanonicalSnapshot(snapshot));
    }

    private static void AppendPlayer(StringBuilder builder, PlayerState player)
    {
        builder.Append("player|id=").Append(player.PlayerId.Value)
            .Append("|memory=").Append(player.Memory)
            .Append('\n');

        foreach (KeyValuePair<string, bool> flag in player.Flags
                     .OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            builder.Append("playerFlag|player=").Append(player.PlayerId.Value)
                .Append("|key=").Append(flag.Key)
                .Append("|value=").Append(flag.Value)
                .Append('\n');
        }

        foreach (KeyValuePair<ChoiceZone, IReadOnlyList<HeadlessEntityId>> zone in player.Zones
                     .OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal))
        {
            builder.Append("zone|player=").Append(player.PlayerId.Value)
                .Append("|id=").Append(zone.Key)
                .Append("|cards=");

            foreach (HeadlessEntityId cardId in zone.Value)
            {
                builder.Append(cardId.Value).Append(',');
            }

            builder.Append('\n');
        }
    }

    private static void AppendEvent(StringBuilder builder, GameEvent gameEvent)
    {
        builder.Append("event|sequence=").Append(gameEvent.Sequence)
            .Append("|type=").Append(gameEvent.Type)
            .Append("|message=").Append(gameEvent.Message)
            .Append('|');

        foreach (KeyValuePair<string, object?> pair in gameEvent.Metadata
                     .OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            builder.Append(pair.Key)
                .Append('=')
                .Append(FormatMetadataValue(pair.Value))
                .Append(';');
        }

        builder.Append('\n');
    }

    private static string FormatMetadataValue(object? value)
    {
        return value switch
        {
            null => "<null>",
            bool boolean => boolean.ToString(CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string ComputeHash(string canonicalSnapshot)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalSnapshot));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
