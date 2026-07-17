// (R4 S3b-2② — docs/audit/r4_tsm_s1_design_2026-07-16.md "S3b-2② 착지")
// ============================================================================================================
// The match-scoped carrier of the AS-IS Permanent "just-after" bookkeeping fields (Permanent.cs:3686-3941):
// PlayingEffect / DigivolvingEffect / LevelJustAfterPlayed / PlayCostJustAfterPlayed / CardNamesJustAfterPlayed
// / CardNamesJustAfterDigivolved / TraitsJustAfterPlayed / IsBurstDigivolved / IsAppFusion — plain mutable
// members on the AS-IS Permanent OBJECT, written by the play executor (PlayPermanentClass :1535-1569) and read
// by card effects ("the level it was PLAYED at", "digivolved into X this turn", IsDigivolvedByTheEffect).
//
// The mirror Permanent is a per-access VIEW keyed by its TOP card's InstanceId, so the AS-IS object lifetime
// maps onto this store as:
//  * CREATE  — a fresh AS-IS `new Permanent(...)` = Reset(topId) as the top card ENTERS a field zone
//              (a re-played card must NOT see the bookkeeping of its previous life);
//  * PERSIST — the AS-IS object survives a top swap (digivolve: the executor's AddCardSource / the digivolve
//              action; de-digivolve/ArmorPurge: the under-card promote) = ReKey(oldTop, newTop) at the ops that
//              own a top swap (DigivolveAction.AttachTargetAsSource callers / DeDigivolveHelpers' promotes);
//  * DIE     — the permanent leaves the field (the AS-IS object is dropped) = Reset(topId) as the top card
//              LEAVES a field zone.
//
// (RD-R3-02) CREATE/DIE are enforced at the single physical zone chokepoint — InMemoryZoneMover.MoveCard
// resets the entry on ANY move that changes the card's field-zone membership (enters or leaves
// BattleArea/BreedingArea), so every path (play action, effect play, sink deletion, GameFlowProcessor's
// deferred/no-DP finalize, bounce, deck return, security return, battle loss, jogress root removal, token
// vanish, ...) shares the same lifetime seat instead of per-call-site Reset wiring. A top SWAP is expressed as
// a leave+enter move PAIR while the AS-IS Permanent object PERSISTS — those moves are stamped with
// PermanentContinuityKey by their owning op (which then calls ReKey), so the chokepoint leaves them alone.
//
// KEYING: by ICardInstanceRepository (1:1 with the match; the two re-key owners hold the repository but not the
// EngineContext). Entries hold live ICardEffect references (PlayingEffect/DigivolvingEffect) — the approved A1
// in-memory continuation substrate (same as SkillWindowContinuation.SkipCondition).
// ============================================================================================================

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Runtime.CompilerServices;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>One permanent's AS-IS bookkeeping fields — names and defaults verbatim from Permanent.cs.</summary>
public sealed class PermanentBookkeepingEntry
{
    /// <summary>AS-IS <c>Permanent.PlayingEffect</c> (:3686).</summary>
    public ICardEffect? PlayingEffect;

    /// <summary>AS-IS <c>Permanent.DigivolvingEffect</c> (:3690).</summary>
    public ICardEffect? DigivolvingEffect;

    /// <summary>AS-IS <c>Permanent.LevelJustAfterPlayed</c> (:3890).</summary>
    public int LevelJustAfterPlayed = -1;

    /// <summary>AS-IS <c>Permanent.PlayCostJustAfterPlayed</c> (:3894).</summary>
    public int PlayCostJustAfterPlayed = -1;

    /// <summary>AS-IS <c>Permanent.CardNamesJustAfterPlayed</c> (:3898).</summary>
    public List<string> CardNamesJustAfterPlayed = new();

    /// <summary>AS-IS <c>Permanent.CardNamesJustAfterDigivolved</c> (:3902).</summary>
    public List<string> CardNamesJustAfterDigivolved = new();

    /// <summary>AS-IS <c>Permanent.TraitsJustAfterPlayed</c> (:3906).</summary>
    public List<string> TraitsJustAfterPlayed = new();

    /// <summary>AS-IS <c>Permanent.IsBurstDigivolved</c> (:3938).</summary>
    public bool IsBurstDigivolved;

    /// <summary>AS-IS <c>Permanent.IsAppFusion</c> (Permanent.cs, sibling of IsBurstDigivolved).</summary>
    public bool IsAppFusion;
}

public static class PermanentBookkeepingStore
{
    /// <summary>(RD-R3-02) ZoneMoveRequest.Metadata marker: this move is one half of a top-SWAP pair (the AS-IS
    /// Permanent object PERSISTS across it — digivolve / de-digivolve / ArmorPurge promote). The zone-mover
    /// lifetime chokepoint skips the CREATE/DIE Reset for a marked move; the owning op calls
    /// <see cref="ReKey"/> instead.</summary>
    public const string PermanentContinuityKey = "permanentContinuity";

    /// <summary>Shared, immutable move metadata carrying only <see cref="PermanentContinuityKey"/> — for the
    /// top-swap moves that need no other metadata.</summary>
    public static readonly IReadOnlyDictionary<string, object?> ContinuityMoveMetadata =
        new Dictionary<string, object?>(StringComparer.Ordinal) { [PermanentContinuityKey] = true };

    private static readonly ConditionalWeakTable<ICardInstanceRepository, Dictionary<HeadlessEntityId, PermanentBookkeepingEntry>> _store = new();

    private static Dictionary<HeadlessEntityId, PermanentBookkeepingEntry> Entries(ICardInstanceRepository repository) =>
        _store.GetValue(repository, static _ => new Dictionary<HeadlessEntityId, PermanentBookkeepingEntry>());

    /// <summary>The entry for the permanent whose CURRENT top is <paramref name="topId"/> (create-on-read —
    /// a never-written permanent reads the AS-IS field defaults).</summary>
    public static PermanentBookkeepingEntry Get(ICardInstanceRepository repository, HeadlessEntityId topId)
    {
        Dictionary<HeadlessEntityId, PermanentBookkeepingEntry> entries = Entries(repository);
        if (!entries.TryGetValue(topId, out PermanentBookkeepingEntry? entry))
        {
            entry = new PermanentBookkeepingEntry();
            entries[topId] = entry;
        }

        return entry;
    }

    /// <summary>Fresh AS-IS object semantics: a NEW permanent (field entry) or a DEAD one (field leave)
    /// discards any previous life's bookkeeping.</summary>
    public static void Reset(ICardInstanceRepository repository, HeadlessEntityId topId)
    {
        Entries(repository).Remove(topId);
    }

    /// <summary>The permanent PERSISTS across a top swap (digivolve / de-digivolve promote): carry its
    /// bookkeeping to the new top key.</summary>
    public static void ReKey(ICardInstanceRepository repository, HeadlessEntityId oldTopId, HeadlessEntityId newTopId)
    {
        if (oldTopId == newTopId)
        {
            return;
        }

        Dictionary<HeadlessEntityId, PermanentBookkeepingEntry> entries = Entries(repository);
        if (entries.TryGetValue(oldTopId, out PermanentBookkeepingEntry? entry))
        {
            entries.Remove(oldTopId);
            entries[newTopId] = entry;
        }
        else
        {
            // (RD-R3-02) stale-block: the OLD top never wrote an entry (e.g. a token / never-stamped
            // permanent), but the NEW top key may still hold an entry from a PREVIOUS life of that card
            // instance — the persisting permanent's bookkeeping is the (absent) old top's, so the new key
            // must read AS-IS field defaults, not the stale entry.
            entries.Remove(newTopId);
        }
    }
}
