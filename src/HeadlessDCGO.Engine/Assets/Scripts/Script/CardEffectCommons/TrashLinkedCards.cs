// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/TrashLinkedCards.cs
// (SKEL-Exhaust) AS-IS SelectTrashLinkedCards. The AS-IS-signature guards are ported 1:1; the interactive
// selection loop is STOP-guarded (design item RD-SKEL-01) — see the throw site for the evidence. Latent
// (0 callers). Substrate translation: coroutine IEnumerator -> Task; ICardEffect activateClass kept (AS-IS
// signature) since it feeds both CanNotBeAffected(activateClass) and ITrashLinkCards(cardEffect).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public static partial class CardEffectCommons
{
    /// <summary>AS-IS <c>SelectTrashLinkedCards</c> (TrashLinkedCards.cs:11): repeatedly select a battle-area
    /// Digimon and trash some of its LINKED cards, up to <paramref name="maxCount"/> total. The guards are
    /// ported 1:1; the selection loop is STOP-guarded (RD-SKEL-01) — see the throw site.</summary>
    public static Task SelectTrashLinkedCards(
        Func<Permanent, bool>? permanentCondition,
        Func<CardSource, bool>? cardCondition,
        int maxCount,
        bool canNoTrash,
        bool isFromOnly1Permanent,
        ICardEffect activateClass,
        string selectString = "Digimon",
        Func<Permanent, IReadOnlyList<CardSource>, Task>? afterSelectionCoroutine = null,
        CancellationToken cancellationToken = default)
    {
        // AS-IS 1:1 guards (TrashLinkedCards.cs:13-16).
        if (maxCount <= 0)
        {
            return Task.CompletedTask;
        }

        if (activateClass is null || activateClass.EffectSourceCard is null)
        {
            return Task.CompletedTask;
        }

        _ = (permanentCondition, cardCondition, canNoTrash, isFromOnly1Permanent, selectString,
            afterSelectionCoroutine, cancellationToken);

        // design item RD-SKEL-01 — STOP-guard for the interactive selection loop.
        // Substrate present: ITrashLinkCards (CardController.cs:1304), Permanent.LinkedCards (Permanent.cs:2758),
        // the ChoiceProvider host/source selection idiom (cf. the ported SelectTrashDigivolutionCards substrate,
        // CardEffectCommons.cs). So the pieces to translate exist. The BLOCK is a genuine AS-IS internal
        // asymmetry that a byte-faithful ChoiceProvider translation cannot reproduce without either replicating a
        // non-terminating case or inventing a used-host guard:
        //   AS-IS TrashLinkedCards.cs:53  computes the availability sum from `permanent.LinkedCards.Count(...)`,
        //   AS-IS TrashLinkedCards.cs:111/115 gates AND budgets each per-permanent trash on
        //     `selectedPermanent.DigivolutionCards.Count(...)` (NOT LinkedCards),
        //   AS-IS TrashLinkedCards.cs:134 draws the actual pool from `selectedPermanent.LinkedCards`.
        // ITrashLinkCards trashes LINKED cards only, so DigivolutionCards.Count never decreases; combined with the
        // AS-IS loop having no used-permanent tracking, an auto-resolving ChoiceProvider re-selects the same
        // permanent when DigivolutionCards>0 but the qualifying LinkedCards pool is empty, trashing 0 each pass —
        // a non-terminating loop. AS-IS's Unity SelectPermanentEffect masks this via human re-offer/end-selection;
        // the headless substrate has no equivalent masking. Porting the guards 1:1 and STOP-guarding the loop
        // avoids both "fixing" the AS-IS asymmetry (a simplification) and inventing a used-host guard.
        throw new NotSupportedException(
            "SelectTrashLinkedCards selection loop is STOP-guarded (design item RD-SKEL-01): the AS-IS body budgets " +
            "per-permanent trashing on DigivolutionCards.Count while drawing the pool from LinkedCards and tracks no " +
            "used permanent, which a faithful headless ChoiceProvider translation cannot reproduce without a " +
            "non-terminating loop or an invented used-host guard. See the file comment for the AS-IS line evidence.");
    }
}
