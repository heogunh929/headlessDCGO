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
//  * CREATE  — a fresh AS-IS `new Permanent(...)` = Reset(topId) at CardObjectController.CreateNewPermanent
//              (a re-played card must NOT see the bookkeeping of its previous life);
//  * PERSIST — the AS-IS object survives a top swap (digivolve: the executor's AddCardSource / the digivolve
//              action; de-digivolve: the under-card promote) = ReKey(oldTop, newTop) at the two ops that own
//              a top swap (DigivolveAction.AttachTargetAsSource / DeDigivolveHelpers' promote);
//  * DIE     — the permanent leaves the field (the AS-IS object is dropped) = Reset(topId) at
//              CardObjectController.RemoveField.
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
    }
}
