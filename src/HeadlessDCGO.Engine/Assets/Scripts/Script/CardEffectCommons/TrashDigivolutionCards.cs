// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/TrashDigivolutionCards.cs
// (EFFECT-MODEL REBUILD / bridge W2, Group B) AS-IS-signature `Task` overload for `SelectTrashDigivolutionCards`
// (TrashDigivolutionCards.cs:11); delegates to the verified substrate implementation (CardEffectCommons.cs:2126).
//
// The two rows W2 deferred (`TrashDigivolutionCardsAndProcessAccordingToResult`, monolith CardEffectCommons.cs:541,
// and `TrashDigivolutionCardsFromTopOrBottom`, monolith CardEffectCommons.cs:675 — their AS-IS home is the monolith,
// not this AS-IS file) are now filled by bridge W3 in DigivolveAndTrashBridge.cs.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public static partial class CardEffectCommons
{
    /// <summary>(BRIDGE) AS-IS <c>CardEffectCommons.SelectTrashDigivolutionCards(...)</c>
    /// (TrashDigivolutionCards.cs:11) — AS-IS-signature overload; delegates to the verified substrate
    /// implementation. Clean 1:1 payload (the substrate's <c>afterSelectionCoroutine</c> already hands back the
    /// exact selected/trashed <see cref="CardSource"/> list) — only <c>List</c>&lt;-&gt;<c>IReadOnlyList</c>
    /// needs bridging.</summary>
    public static async Task SelectTrashDigivolutionCards(
        Func<Permanent, bool> permanentCondition,
        Func<CardSource, bool> cardCondition,
        int maxCount,
        bool canNoTrash,
        bool isFromOnly1Permanent,
        ICardEffect activateClass,
        string selectString = "Digimon",
        Func<Permanent, List<CardSource>, Task> afterSelectionCoroutine = null,
        bool canEndNotMax = false)
    {
        Func<Permanent, IReadOnlyList<CardSource>, Task>? adaptedAfterSelection = afterSelectionCoroutine is null
            ? null
            : ((permanent, cards) => afterSelectionCoroutine(permanent, cards.ToList()));

        await SelectTrashDigivolutionCards(
            permanentCondition, cardCondition, maxCount, canNoTrash, isFromOnly1Permanent,
            activateClass?.EffectSourceCard, selectString, adaptedAfterSelection, canEndNotMax).ConfigureAwait(false);
    }
}
