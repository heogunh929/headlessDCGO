namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (PRIM-W4 AceOverflowClass) The ACE Overflow rule. AS-IS <c>AceOverflowClass.Overflow()</c> fires from the
/// central card-movement controller (CardObjectController.RemoveField / AddHandCards / AddLibraryTop/Bottom)
/// whenever an un-flipped ACE Digimon LEAVES the field (battle / breeding area): its owner loses memory equal
/// to the card's printed <c>OverflowMemory</c>.
///
/// In the headless model the central movement layer is <c>MatchStateMutationSink</c>, so the field-leave
/// mutations (Delete, ReturnToHand, ReturnToDeckTop/Bottom) consult this gate. The ACE flag and overflow value
/// live on the instance metadata (stamped from the card definition, like dp/level); the memory value is
/// single-signed and turn-player-relative (positive = turn player), so an off-turn owner losing memory moves
/// the value toward the turn player.
/// </summary>
public static class AceOverflowGate
{
    /// <summary>Instance metadata: this card is an ACE.</summary>
    public const string IsAceKey = "isAce";

    /// <summary>Instance metadata: the card's printed Overflow value.</summary>
    public const string OverflowMemoryKey = "overflowMemory";

    /// <summary>Instance metadata: the ACE is flipped (its overflow already accounted) — no penalty.</summary>
    public const string IsFlippedKey = "isFlipped";

    /// <summary>Mutation value flag: skip the overflow penalty for this specific move (AS-IS ignoreOverflow).</summary>
    public const string IgnoreOverflowKey = "ignoreOverflow";

    /// <summary>The overflow penalty of <paramref name="record"/> (its printed value) when it is an
    /// un-flipped ACE, else 0.</summary>
    public static int OverflowFor(CardInstanceRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (!ReadBool(record.Metadata, IsAceKey) || ReadBool(record.Metadata, IsFlippedKey))
        {
            return 0;
        }

        return Math.Max(0, ReadInt(record.Metadata, OverflowMemoryKey));
    }

    /// <summary>The turn-player-relative memory delta for <paramref name="owner"/> losing
    /// <paramref name="overflow"/> memory. When the owner is the turn player the value drops (-overflow); when
    /// the owner is the off-turn player the (turn-relative) value rises (+overflow). If the turn player is
    /// unknown, assume the owner is active (the common case: an ACE leaves on its owner's turn).</summary>
    public static int MemoryDelta(int overflow, HeadlessPlayerId owner, HeadlessPlayerId? turnPlayer)
    {
        if (overflow <= 0)
        {
            return 0;
        }

        return turnPlayer is { } tp && tp != owner ? overflow : -overflow;
    }

    private static bool ReadBool(IReadOnlyDictionary<string, object?> metadata, string key) =>
        metadata.TryGetValue(key, out object? raw) && raw is bool value && value;

    private static int ReadInt(IReadOnlyDictionary<string, object?> metadata, string key) =>
        metadata.TryGetValue(key, out object? raw) && raw is int value ? value : 0;
}
