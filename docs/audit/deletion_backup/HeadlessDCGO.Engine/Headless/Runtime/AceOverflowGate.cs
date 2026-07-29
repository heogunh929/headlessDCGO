// ============================================================================
// ⛔ DELETION-TARGET · DO-NOT-REFERENCE
// 원장(docs/audit/filelist/merged_files_no_cards.csv): 삭제대상여부=Y · 결함여부=Y
// 분류: substrate 오배치(other)
// 미러 원가(재이관 대상): Script/CardController.cs::AceOverflowClass.Overflow()@5827
// 이 파일은 AS-IS 원본에 동일-경로 대응이 없는 오배치/발명 코드다.
// 규칙 로직은 위 미러 원가로 재이관 후 이 파일은 삭제 예정.
// 서브에이전트/포팅 작업 시: 이 파일의 심볼을 참조·모방·확장하지 말 것.
// ============================================================================
namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Choices;
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

    /// <summary>(C-2 adversarial review P1) Apply the TOP card's own overflow when a permanent leaves the field
    /// by DELETION on a path that does NOT route through <c>MatchStateMutationSink.ApplyDelete</c> — the battle /
    /// security / deferred-finalize deletion finishers move the top with a raw <c>MoveAsync</c>. AS-IS
    /// <c>DestroyPermanentsClass</c> always charges the top's overflow via <c>RemoveField</c>
    /// (CardController.cs:4885/5062), AFTER <c>DiscardEvoRoots</c> charges the sources — so this must be called
    /// AFTER <see cref="DeletionSourceTrash.TrashEvoSourcesAsync"/> and BEFORE the top's trash move (the card is
    /// still on the battle area for the on-field guard). The sink path keeps its own <c>ApplyAceOverflowOnLeave</c>
    /// (it also honours a per-move IgnoreOverflow flag); this covers the three raw-move finishers so a
    /// battle-deleted un-flipped ACE loses its OWN overflow too, not just its sources'.</summary>
    public static void ApplyTopOverflowOnDelete(
        Bridge.EngineContext context, HeadlessEntityId cardId)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (cardId.IsEmpty
            || !context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record)
            || record is null)
        {
            return;
        }

        int overflow = OverflowFor(record);
        if (overflow <= 0 || context.ZoneMover is not IZoneStateReader reader)
        {
            return;
        }

        bool onField = reader.GetCards(record.OwnerId, ChoiceZone.BattleArea).Contains(cardId)
            || reader.GetCards(record.OwnerId, ChoiceZone.BreedingArea).Contains(cardId);
        if (!onField)
        {
            return;
        }

        context.MemoryController.Add(
            MemoryDelta(overflow, record.OwnerId, context.TurnController.Current.TurnPlayerId));
    }

    private static bool ReadBool(IReadOnlyDictionary<string, object?> metadata, string key) =>
        metadata.TryGetValue(key, out object? raw) && raw is bool value && value;

    private static int ReadInt(IReadOnlyDictionary<string, object?> metadata, string key) =>
        metadata.TryGetValue(key, out object? raw) && raw is int value ? value : 0;
}
