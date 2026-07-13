// (EFFECT-MODEL REBUILD / bridge W2, Group B) AS-IS-signature `Task` overloads for the
// "...AndProcessAccordingToResult" mutation-helper family whose AS-IS home is NOT a per-method subfolder file
// but the single monolith `DCGO/Assets/Scripts/Script/CardEffectCommons.cs` (lines 437-644 per
// docs/audit/mutation_helper_bridge_map.md) — the same file the mirror's own substrate translations of these
// methods already live in (`Script/CardEffectCommons.cs`, W6-S region, lines ~104-485). Per this batch's rules
// ("no edits to substrate/cards/engine"), that file cannot be touched, and AS-IS itself has no separate
// per-method subfolder file for these 8 methods to fill as a stub — so, following the SAME AS-IS convention the
// mirror's own `CardEffectCommons.cs` documents at its top ("partial so the AS-IS partial class CardEffectCommons
// file split … mirrors 1:1 as sibling partial files in this directory, exactly as AS-IS organises them" — AS-IS
// itself already splits pieces of this monolith `partial class` into sibling files under this very
// `Script/CardEffectCommons/` folder, e.g. TrashDigivolutionCards.cs, TrashLinkedCards.cs, RevealLibrary.cs),
// this file adds ANOTHER sibling partial-class file for the bridge overloads of this specific mutation-helper
// family. Every AS-IS line-cite below is to the monolith `CardEffectCommons.cs`; every substrate line-cite is to
// the mirror's `Script/CardEffectCommons.cs`.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public static partial class CardEffectCommons
{
    /// <summary>(BRIDGE) AS-IS <c>CardEffectCommons.BouncePeremanentAndProcessAccordingToResult</c>
    /// (CardEffectCommons.cs:489) — AS-IS-signature overload; delegates to the verified substrate
    /// implementation (CardEffectCommons.cs:214). AS-IS's <c>successProcess</c>/<c>failureProcess</c> are bare
    /// <c>IEnumerator</c> instances (a not-yet-driven coroutine reference — calling the local iterator method
    /// that produced them does not itself run any body code; only an explicit drive does). The direct C#-native
    /// translation of "an inert, undriven coroutine reference" is a deferred <c>Func&lt;Task&gt;</c> factory —
    /// which is exactly what the substrate itself already expects, so this is a straight pass-through, not a
    /// re-typed adapter (see docs/audit/rebuild_bridge_w2_notes.md).</summary>
    public static async Task BouncePeremanentAndProcessAccordingToResult(List<Permanent> targetPermanents, ICardEffect activateClass, Func<Task> successProcess, Func<Task> failureProcess)
    {
        await BouncePeremanentAndProcessAccordingToResult(targetPermanents, activateClass?.EffectSourceCard, successProcess, failureProcess).ConfigureAwait(false);
    }

    /// <summary>(BRIDGE) AS-IS <c>CardEffectCommons.DeckBouncePeremanentAndProcessAccordingToResult</c>
    /// (CardEffectCommons.cs:515) — AS-IS-signature overload; delegates to the verified substrate
    /// implementation (CardEffectCommons.cs:254). Same bare-<c>IEnumerator</c>→<c>Func&lt;Task&gt;</c>
    /// translation as <see cref="BouncePeremanentAndProcessAccordingToResult"/> above.</summary>
    public static async Task DeckBouncePeremanentAndProcessAccordingToResult(List<Permanent> targetPermanents, ICardEffect activateClass, Func<Task> successProcess, Func<Task> failureProcess)
    {
        await DeckBouncePeremanentAndProcessAccordingToResult(targetPermanents, activateClass?.EffectSourceCard, successProcess, failureProcess).ConfigureAwait(false);
    }

    /// <summary>(BRIDGE) AS-IS <c>CardEffectCommons.DeletePeremanentAndProcessAccordingToResult</c>
    /// (CardEffectCommons.cs:463) — AS-IS-signature overload; delegates to the verified substrate
    /// implementation (CardEffectCommons.cs:115). Clean 1:1 payload (the substrate already hands back the
    /// actual destroyed <see cref="Permanent"/> views, not a mere count) — only <c>List</c>&lt;-&gt;
    /// <c>IReadOnlyList</c> needs bridging.</summary>
    public static async Task DeletePeremanentAndProcessAccordingToResult(List<Permanent> targetPermanents, ICardEffect activateClass, Func<List<Permanent>, Task> successProcess, Func<Task> failureProcess)
    {
        Func<IReadOnlyList<Permanent>, Task>? adaptedSuccess = successProcess is null
            ? null
            : (destroyed => successProcess(destroyed.ToList()));
        await DeletePeremanentAndProcessAccordingToResult(targetPermanents, activateClass?.EffectSourceCard, adaptedSuccess, failureProcess).ConfigureAwait(false);
    }

    /// <summary>(BRIDGE) AS-IS <c>CardEffectCommons.SuspendPeremanentAndProcessAccordingToResult</c>
    /// (CardEffectCommons.cs:437) — AS-IS-signature overload; delegates to the verified substrate
    /// implementation (CardEffectCommons.cs:174). Same clean payload shape as
    /// <see cref="DeletePeremanentAndProcessAccordingToResult"/> above (the substrate hands back the actual
    /// suspended <see cref="Permanent"/> views).</summary>
    public static async Task SuspendPeremanentAndProcessAccordingToResult(List<Permanent> targetPermanents, ICardEffect activateClass, Func<List<Permanent>, Task> successProcess, Func<Task> failureProcess)
    {
        Func<IReadOnlyList<Permanent>, Task>? adaptedSuccess = successProcess is null
            ? null
            : (suspended => successProcess(suspended.ToList()));
        await SuspendPeremanentAndProcessAccordingToResult(targetPermanents, activateClass?.EffectSourceCard, adaptedSuccess, failureProcess).ConfigureAwait(false);
    }

    /// <summary>(BRIDGE) AS-IS <c>CardEffectCommons.PlacePermanentInSecurityAndProcessAccordingToResult</c>
    /// (CardEffectCommons.cs:644) — AS-IS-signature overload (AS-IS param order kept: <c>activateClass</c>
    /// precedes <c>toTop</c>, matching the original call sites); delegates to the verified substrate
    /// implementation (CardEffectCommons.cs:440, which takes <c>sourceCard</c> in a different position — a pure
    /// reorder, no payload adaptation needed since the substrate's <c>successProcess</c> is already
    /// <c>Func&lt;CardSource,Task&gt;</c>, an exact match for AS-IS's placed-card payload).</summary>
    public static async Task PlacePermanentInSecurityAndProcessAccordingToResult(Permanent targetPermanent, ICardEffect activateClass, bool toTop, Func<CardSource, Task> successProcess, Func<Task> failureProcess = null, bool isFaceUp = false)
    {
        await PlacePermanentInSecurityAndProcessAccordingToResult(targetPermanent, toTop, activateClass?.EffectSourceCard, successProcess, failureProcess, isFaceUp).ConfigureAwait(false);
    }

    /// <summary>(BRIDGE) AS-IS <c>CardEffectCommons.TrashHandAndProcessAccordingToResult</c>
    /// (CardEffectCommons.cs:619) — AS-IS-signature overload; delegates to the verified substrate
    /// implementation (CardEffectCommons.cs:412). AS-IS's <c>Player player</c>/<c>Hashtable hashtable</c> params
    /// are dead in the AS-IS body itself (never read) — kept here (not dropped) purely so any AS-IS-verbatim
    /// card that still passes them positionally keeps compiling; the substrate call simply does not use them.
    /// AS-IS types <c>activateClass</c> as the concrete <c>ActivateClass</c> (not the usual <c>ICardEffect</c>)
    /// — widened to <c>ICardEffect</c> here for this batch's uniform convention (every real AS-IS caller
    /// constructs a genuine <c>ActivateClass</c>, which IS-A <c>ICardEffect</c>, so this accepts every real
    /// argument unchanged; see docs/audit/mutation_helper_bridge_map.md's own "minor outlier, no behavioral
    /// implication found" note). The substrate's <c>successProcess</c> takes no payload; AS-IS's takes the
    /// trashed <c>CardSource</c> — but <c>TrashHandAndProcessAccordingToResult</c> only ever attempts to trash
    /// the SAME <paramref name="cardToTrash"/> the caller supplied, so on success that IS the trashed card:
    /// re-supplying <paramref name="cardToTrash"/> itself is exact, not a guess.</summary>
    public static async Task TrashHandAndProcessAccordingToResult(Player player, Hashtable hashtable, CardSource cardToTrash, ICardEffect activateClass, Func<CardSource, Task> successProcess, Func<Task> failureProcess)
    {
        _ = player;
        _ = hashtable;
        Func<Task>? adaptedSuccess = successProcess is null ? null : (() => successProcess(cardToTrash));
        await TrashHandAndProcessAccordingToResult(cardToTrash, activateClass?.EffectSourceCard, adaptedSuccess, failureProcess).ConfigureAwait(false);
    }

    /// <summary>(BRIDGE) AS-IS <c>CardEffectCommons.TrashLinkCardsAndProcessAccordingToResult</c>
    /// (CardEffectCommons.cs:567) — AS-IS-signature overload; delegates to the verified substrate
    /// implementation (CardEffectCommons.cs:342).
    ///
    /// Design item RD-W2-2 (docs/audit/rebuild_bridge_w2_notes.md): the substrate's success payload is an
    /// <c>int</c> count (its per-card <c>RemoveLinkCardAsync</c> loop can partially succeed), not AS-IS's
    /// <c>List&lt;CardSource&gt;</c> of the cards that actually ended up trashed. Rather than assume
    /// "all requested == all trashed" (wrong whenever a subset is protected), this reconstructs the EXACT
    /// sublist by checking, after the substrate call, which of the caller-supplied <paramref name="targetLinkCards"/>
    /// now sit in their owner's Trash zone — the same "did it actually land" check the substrate itself uses
    /// internally to compute its own count, just re-applied per-candidate instead of via a running tally. This is
    /// a faithful reconstruction (real zone-membership evidence), not a heuristic guess.</summary>
    public static async Task TrashLinkCardsAndProcessAccordingToResult(Permanent targetPermanent, List<CardSource> targetLinkCards, ICardEffect activateClass, Func<List<CardSource>, Task> successProcess, Func<Task> failureProcess)
    {
        CardSource sourceCard = activateClass?.EffectSourceCard;
        List<CardSource> candidates = targetLinkCards ?? new List<CardSource>();
        IReadOnlyList<HeadlessEntityId> linkCardIds = candidates.Select(c => c.InstanceId).ToList();

        Func<int, Task>? adaptedSuccess = successProcess is null
            ? null
            : async _ =>
            {
                var trashedCards = new List<CardSource>();
                if (sourceCard is not null)
                {
                    var zones = (IZoneStateReader)sourceCard.Context.ZoneMover;
                    foreach (CardSource candidate in candidates)
                    {
                        if (candidate is not null && zones.GetCards(candidate.Owner, ChoiceZone.Trash).Contains(candidate.InstanceId))
                        {
                            trashedCards.Add(candidate);
                        }
                    }
                }

                await successProcess(trashedCards).ConfigureAwait(false);
            };

        await TrashLinkCardsAndProcessAccordingToResult(targetPermanent, linkCardIds, sourceCard, adaptedSuccess, failureProcess).ConfigureAwait(false);
    }

    /// <summary>(BRIDGE) AS-IS <c>CardEffectCommons.TrashSecurityAndProcessAccordingToResult</c>
    /// (CardEffectCommons.cs:593) — AS-IS-signature overload; delegates to the verified substrate
    /// implementation (CardEffectCommons.cs:378).
    ///
    /// Design item RD-W2-2 (docs/audit/rebuild_bridge_w2_notes.md, same shape as
    /// <see cref="TrashLinkCardsAndProcessAccordingToResult"/> above): the substrate's success payload is an
    /// <c>int</c> trashed count (computed via a before/after Security-zone-count diff), not AS-IS's
    /// <c>List&lt;CardSource&gt;</c> of the destroyed security cards. This snapshots the player's Security zone
    /// BEFORE calling the substrate and, on success, diffs against the AFTER snapshot to recover the exact set
    /// of ids that left — <c>TrashSecurityAndProcessAccordingToResult</c> always removes from the specified
    /// end (<paramref name="fromTop"/>) deterministically, so "ids present before but absent after" is the
    /// exact destroyed set, not an approximation.</summary>
    public static async Task TrashSecurityAndProcessAccordingToResult(Player player, int trashAmount, ICardEffect activateClass, bool fromTop, Func<List<CardSource>, Task> successProcess, Func<Task> failureProcess)
    {
        CardSource sourceCard = activateClass?.EffectSourceCard;
        if (player is null || sourceCard is null)
        {
            if (failureProcess is not null)
            {
                await failureProcess().ConfigureAwait(false);
            }

            return;
        }

        var zones = (IZoneStateReader)sourceCard.Context.ZoneMover;
        List<HeadlessEntityId> before = zones.GetCards(player.PlayerId, ChoiceZone.Security).ToList();

        Func<int, Task>? adaptedSuccess = successProcess is null
            ? null
            : async _ =>
            {
                IReadOnlyList<HeadlessEntityId> after = zones.GetCards(player.PlayerId, ChoiceZone.Security);
                List<CardSource> destroyed = before
                    .Where(id => !after.Contains(id))
                    .Select(id => new CardSource(sourceCard.Context, id, player.PlayerId, player.PlayerId))
                    .ToList();
                await successProcess(destroyed).ConfigureAwait(false);
            };

        await TrashSecurityAndProcessAccordingToResult(player.PlayerId, trashAmount, fromTop, sourceCard, adaptedSuccess, failureProcess).ConfigureAwait(false);
    }
}
