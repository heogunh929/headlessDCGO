// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/RevealLibrary.cs
// (EFFECT-MODEL REBUILD / bridge W3) AS-IS-signature `Task` overloads for the reveal-library mutation-helper
// family — the HEAVYWEIGHT rows of docs/audit/mutation_helper_bridge_map.md:
//   - SimplifiedRevealDeckTopCardsAndSelect (RevealLibrary.cs:179, 409 card calls — highest in the 91-set)
//   - RevealDeckTopCardsAndSelect            (RevealLibrary.cs:229, 25 card calls)
//   - RevealDeckTopCardsAndProcessForAll     (RevealLibrary.cs:10, 34 card calls — NO-MIRROR row)
//   - ReturnRevealedCardsToLibraryBottom     (RevealLibrary.cs:469, 7 card calls — public AS-IS sibling)
// plus the AS-IS namespace-level types this AS-IS file declares (`RemainingCardsPlace`, the AS-IS-shape
// constructors of `SimplifiedSelectCardConditionClass` / `SelectCardConditionClass`).
//
// DESIGN (per §11.11 rule 4 — NO silent parameter drops): unlike the W1/W2 thin wrappers, the mirror's
// declarative reveal factories (SimplifiedRevealAndSelectEffect / RevealMultiSelectEffect,
// CardEffectCommons/ActivatedEffects.cs:1295/1443) model only a subset of the AS-IS parameters
// (canTargetCondition_ByPreSelecetedList / canEndSelectCondition / canNoSelect / canEndNotMax /
// isSendAllCardsToSamePlace / revealedCardsCoroutine / per-card selectCardCoroutine are unmodeled there), so
// these bridges implement the reveal → per-condition select → route flow IMPERATIVELY over the substrate
// primitives (zone reads, ChoiceProvider requests, sink mutations), mirroring the AS-IS body's statement
// order 1:1. The primitives and payload conventions (ChoiceZone.Library candidates, ReturnToHand/TrashCard/
// ReturnToDeckTop/ReturnToDeckBottom mutation kinds, the RevealTrashFlagKey stamp on a revealed→trash move,
// the DeckTopOrBottom binary pick, the ≥2-card ordering pick with top-reversal) are copied from the VERIFIED
// RevealMultiSelectEffect resolution path so behaviour matches the substrate the engine already exercises.
//
// AS-IS select semantics reproduced (verified against DCGO SelectCardPanel.cs):
//   - selectable(card)   = canTargetCondition(card) && (byPreSelectedList == null ||
//                          byPreSelectedList(selectedSoFar, card))                (SelectCardPanel.cs:451/527)
//   - CanEndSelection    = (canEndSelectCondition == null || canEndSelectCondition(selected))
//                          && (canEndNotMax || selected.Count == maxCount)        (SelectCardPanel.cs:568)
//   - "No Selection"     = separate whole-select opt-out when canNoSelect.
// A path-dependent byPreSelectedList therefore uses an INCREMENTAL one-pick-at-a-time choice loop (the same
// per-pick re-filtering the AS-IS panel performs); the plain case uses one batch ChoiceRequest whose
// SelectionValidator carries canEndSelectCondition (the established SelectPermanentEffect.CanEndSelect route).
//
// UI-only AS-IS statements elided: Effects.ShowCardEffect/ShowCardEffect2 overlays, PlayLog.OnAddLog strings,
// WaitForSeconds delays, and the IsBeingRevealed flag flips (headless carries the reveal marker as the
// RevealTrashFlagKey mutation stamp instead — F1 reveal-remainder convention).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using SelectCardEffect = HeadlessDCGO.Engine.Assets.Scripts.Script.SelectCardEffect;

#region In which area the remaining cards are placed (AS-IS RevealLibrary.cs:723-732, 1:1)

public enum RemainingCardsPlace
{
    DeckBottom,
    DeckTop,
    DeckTopOrBottom,
    Trash,
    AddHand,
    None,
}

#endregion

#region Select conditions class — AS-IS-shape constructor overloads (AS-IS RevealLibrary.cs:655-720)

// The legacy (mirror-invented, HeadlessEntityId-predicate) parts of these classes live in
// CardPortingFramework.cs; these partial parts add the AS-IS constructor shape verbatim cards use
// (`Func<CardSource,bool>` predicate + `SelectCardEffect.Mode` + per-card coroutine, IEnumerator→Task).
// An instance built through an AS-IS constructor must only flow into the AS-IS bridge methods below —
// its legacy `CanTargetCondition` (id-shape) member is set to a LOUD throw so accidental consumption by the
// old declarative factories fails visibly instead of silently misbehaving.
public sealed partial class SimplifiedSelectCardConditionClass
{
    // AS-IS ctor (RevealLibrary.cs:657): (canTargetCondition, message, mode, maxCount, selectCardCoroutine).
    public SimplifiedSelectCardConditionClass(
        Func<CardSource, bool> canTargetCondition,
        string message,
        SelectCardEffect.Mode mode,
        int maxCount,
        Func<CardSource, Task> selectCardCoroutine)
    {
        CanTargetConditionCard = canTargetCondition;
        Message = message ?? string.Empty;
        Mode = mode;
        MaxCount = maxCount;
        SelectCardCoroutine = selectCardCoroutine;
        SelectedTo = ModeToDestination(mode);
        CanTargetCondition = _ => throw new InvalidOperationException(
            "This SimplifiedSelectCardConditionClass was built through the AS-IS constructor (bridge W3) and " +
            "carries a CardSource-shape predicate (CanTargetConditionCard); the legacy id-shape surface is not " +
            "populated. Route it through CardEffectCommons.SimplifiedRevealDeckTopCardsAndSelect / " +
            "RevealDeckTopCardsAndProcessForAll, not the legacy declarative factories.");
    }

    /// <summary>AS-IS <c>CanTargetCondition</c> (CardSource-shape; the legacy same-named id-shape property is a
    /// different member).</summary>
    public Func<CardSource, bool>? CanTargetConditionCard { get; }

    /// <summary>AS-IS <c>Mode</c> (SelectCardEffect.Mode).</summary>
    public SelectCardEffect.Mode Mode { get; }

    /// <summary>AS-IS <c>SelectCardCoroutine</c> (Mode.Custom per-card follow-up), IEnumerator→Task.</summary>
    public Func<CardSource, Task>? SelectCardCoroutine { get; }

    internal static RevealDestination ModeToDestination(SelectCardEffect.Mode mode) => mode switch
    {
        SelectCardEffect.Mode.AddHand => RevealDestination.Hand,
        SelectCardEffect.Mode.Discard => RevealDestination.Trash,
        _ => RevealDestination.Custom,
    };
}

public sealed partial class SelectCardConditionClass
{
    // AS-IS ctor (RevealLibrary.cs:683): all nine parameters, AS-IS order (delegates IEnumerator→Task).
    public SelectCardConditionClass(
        Func<CardSource, bool> canTargetCondition,
        Func<List<CardSource>, CardSource, bool> canTargetCondition_ByPreSelecetedList,
        Func<List<CardSource>, bool> canEndSelectCondition,
        bool canNoSelect,
        Func<CardSource, Task> selectCardCoroutine,
        string message,
        int maxCount,
        bool canEndNotMax,
        SelectCardEffect.Mode mode)
    {
        CanTargetConditionCard = canTargetCondition;
        CanTargetCondition_ByPreSelecetedList = canTargetCondition_ByPreSelecetedList;
        CanEndSelectConditionCard = canEndSelectCondition;
        CanNoSelect = canNoSelect;
        SelectCardCoroutine = selectCardCoroutine;
        Message = message ?? string.Empty;
        MaxCount = maxCount;
        CanEndNotMax = canEndNotMax;
        Mode = mode;
        SelectedTo = SimplifiedSelectCardConditionClass.ModeToDestination(mode);
        CanTargetCondition = _ => throw new InvalidOperationException(
            "This SelectCardConditionClass was built through the AS-IS constructor (bridge W3); use " +
            "CanTargetConditionCard and route it through CardEffectCommons.RevealDeckTopCardsAndSelect.");
    }

    /// <summary>AS-IS <c>CanTargetCondition</c> (CardSource-shape).</summary>
    public Func<CardSource, bool>? CanTargetConditionCard { get; }

    /// <summary>AS-IS <c>CanTargetCondition_ByPreSelecetedList</c> (original spelling kept — the legacy part's
    /// corrected-spelling id-shape property is a different member).</summary>
    public Func<List<CardSource>, CardSource, bool>? CanTargetCondition_ByPreSelecetedList { get; }

    /// <summary>AS-IS <c>CanEndSelectCondition</c> (CardSource-shape).</summary>
    public Func<List<CardSource>, bool>? CanEndSelectConditionCard { get; }

    /// <summary>AS-IS <c>SelectCardCoroutine</c>, IEnumerator→Task.</summary>
    public Func<CardSource, Task>? SelectCardCoroutine { get; }

    /// <summary>AS-IS <c>Mode</c>.</summary>
    public SelectCardEffect.Mode Mode { get; }
}

#endregion

public static partial class CardEffectCommons
{
    #region Reveal cards from top of deck and process for all of them (AS-IS RevealLibrary.cs:10-176)

    /// <summary>(BRIDGE W3) AS-IS <c>RevealDeckTopCardsAndProcessForAll</c> — reveal N, partition ALL revealed
    /// by the single condition (no player selection), route matched per the condition's Mode, run the optional
    /// whole-reveal follow-up, route unmatched per <paramref name="remainingCardsPlace"/>, and report the
    /// matched cards through the AS-IS <paramref name="refSelectedCards"/> out-list. AS-IS statement order kept:
    /// mode routing → <paramref name="revealedCardsCoroutine"/> → remaining routing → refSelectedCards.</summary>
    public static async Task RevealDeckTopCardsAndProcessForAll(
        int revealCount,
        SimplifiedSelectCardConditionClass simplifiedSelectCardCondition,
        RemainingCardsPlace remainingCardsPlace,
        ICardEffect activateClass,
        Func<List<CardSource>, Task>? revealedCardsCoroutine = null,
        List<CardSource>? refSelectedCards = null,
        bool isOpponentDeck = false)
    {
        // AS-IS guards (RevealLibrary.cs:19-29).
        if (revealCount <= 0 || activateClass == null)
        {
            return;
        }

        CardSource effectSourceCard = activateClass.EffectSourceCard;
        if (effectSourceCard == null)
        {
            return;
        }

        EngineContext context = effectSourceCard.Context;
        var zones = (IZoneStateReader)context.ZoneMover;
        HeadlessPlayerId selectPlayer = effectSourceCard.Owner;
        HeadlessPlayerId revealPlayer = isOpponentDeck ? OpponentOf(effectSourceCard) : selectPlayer;
        if (revealPlayer.IsEmpty || zones.GetCards(revealPlayer, ChoiceZone.Library).Count == 0 ||
            simplifiedSelectCardCondition == null)
        {
            return;
        }

        Func<CardSource, bool> canTarget = simplifiedSelectCardCondition.CanTargetConditionCard
            ?? throw new InvalidOperationException(
                "RevealDeckTopCardsAndProcessForAll (bridge W3) requires a condition built through the AS-IS " +
                "constructor (CardSource-shape CanTargetCondition).");

        // AS-IS RevealLibraryClass.RevealLibrary(): the top revealCount library cards (fewer when short).
        List<CardSource> allRevealed = zones.GetCards(revealPlayer, ChoiceZone.Library)
            .Take(revealCount)
            .Select(id => new CardSource(context, id, revealPlayer, revealPlayer))
            .ToList();

        List<CardSource> selectedCards = allRevealed.Where(canTarget).ToList();
        List<CardSource> remainingCards = allRevealed.Where(cs => !selectedCards.Contains(cs)).ToList();

        // AS-IS mode switch (RevealLibrary.cs:39-96): only AddHand / Discard / Custom are handled — other
        // modes fall through with no built-in routing, exactly as the AS-IS switch does.
        switch (simplifiedSelectCardCondition.Mode)
        {
            case SelectCardEffect.Mode.AddHand:
                await StageRevealMovesAsync(context, activateClass, selectedCards, MatchStateMutationSink.ReturnToHandKind).ConfigureAwait(false);
                break;
            case SelectCardEffect.Mode.Discard:
                await StageRevealMovesAsync(context, activateClass, selectedCards, MatchStateMutationSink.TrashCardKind).ConfigureAwait(false);
                break;
            case SelectCardEffect.Mode.Custom:
                if (simplifiedSelectCardCondition.SelectCardCoroutine != null)
                {
                    foreach (CardSource selectedCard in selectedCards)
                    {
                        await simplifiedSelectCardCondition.SelectCardCoroutine(selectedCard).ConfigureAwait(false);
                    }
                }

                break;
        }

        // AS-IS order: the whole-reveal follow-up runs BEFORE the remaining-cards routing here (:98-101) —
        // note this is the OPPOSITE order of RevealDeckTopCardsAndSelect (:460-463).
        if (revealedCardsCoroutine != null)
        {
            await revealedCardsCoroutine(new List<CardSource>(allRevealed)).ConfigureAwait(false);
        }

        await RouteRemainingRevealedCardsAsync(context, activateClass, selectPlayer, remainingCardsPlace, remainingCards).ConfigureAwait(false);

        if (refSelectedCards != null)
        {
            foreach (CardSource selectedCard in selectedCards)
            {
                refSelectedCards.Add(selectedCard);
            }
        }
    }

    #endregion

    #region Reveal cards from top of deck and select them (AS-IS RevealLibrary.cs:179-465)

    /// <summary>(BRIDGE W3) AS-IS <c>SimplifiedRevealDeckTopCardsAndSelect</c> (RevealLibrary.cs:179; the
    /// highest-call-count helper of the whole 91-row bridge set) — 1:1 with the AS-IS body: expand every
    /// simplified condition into a full <see cref="SelectCardConditionClass"/> carrying the SHARED advanced
    /// parameters, then run the full reveal-select (with <c>canNoAction</c> left at its default, as AS-IS
    /// does). AS-IS quirk kept: the early empty-library guard here checks the effect OWNER's library even when
    /// <paramref name="isOpponentDeck"/> is set (the full method then re-checks the actual reveal player).</summary>
    public static async Task SimplifiedRevealDeckTopCardsAndSelect(
        int revealCount,
        SimplifiedSelectCardConditionClass[] simplifiedSelectCardConditions,
        RemainingCardsPlace remainingCardsPlace,
        ICardEffect activateClass,
        Func<List<CardSource>, CardSource, bool>? canTargetCondition_ByPreSelecetedList = null,
        Func<List<CardSource>, bool>? canEndSelectCondition = null,
        bool canNoSelect = false,
        bool canEndNotMax = false,
        bool isSendAllCardsToSamePlace = false,
        bool isOpponentDeck = false,
        Func<List<CardSource>, Task>? revealedCardsCoroutine = null,
        bool mutualConditions = false)
    {
        // AS-IS guards (RevealLibrary.cs:193-202).
        if (revealCount <= 0 || activateClass == null)
        {
            return;
        }

        CardSource effectSourceCard = activateClass.EffectSourceCard;
        if (effectSourceCard == null)
        {
            return;
        }

        var zones = (IZoneStateReader)effectSourceCard.Context.ZoneMover;
        if (zones.GetCards(effectSourceCard.Owner, ChoiceZone.Library).Count == 0 ||
            simplifiedSelectCardConditions == null || simplifiedSelectCardConditions.Length == 0)
        {
            return;
        }

        // AS-IS mapping (RevealLibrary.cs:204-216): per-condition core + the SHARED advanced params.
        SelectCardConditionClass[] selectCardConditions = simplifiedSelectCardConditions
            .Select(selectCardConditionTuple => new SelectCardConditionClass(
                canTargetCondition: selectCardConditionTuple.CanTargetConditionCard
                    ?? throw new InvalidOperationException(
                        "SimplifiedRevealDeckTopCardsAndSelect (bridge W3) requires conditions built through " +
                        "the AS-IS constructor (CardSource-shape CanTargetCondition)."),
                canTargetCondition_ByPreSelecetedList: canTargetCondition_ByPreSelecetedList!,
                canEndSelectCondition: canEndSelectCondition!,
                canNoSelect: canNoSelect,
                selectCardCoroutine: selectCardConditionTuple.SelectCardCoroutine!,
                message: selectCardConditionTuple.Message,
                maxCount: selectCardConditionTuple.MaxCount,
                canEndNotMax: canEndNotMax,
                mode: selectCardConditionTuple.Mode))
            .ToArray();

        await RevealDeckTopCardsAndSelect(
            revealCount: revealCount,
            selectCardConditions: selectCardConditions,
            remainingCardsPlace: remainingCardsPlace,
            activateClass: activateClass,
            isSendAllCardsToSamePlace: isSendAllCardsToSamePlace,
            isOpponentDeck: isOpponentDeck,
            revealedCardsCoroutine: revealedCardsCoroutine,
            mutualConditions: mutualConditions).ConfigureAwait(false);
    }

    /// <summary>(BRIDGE W3) AS-IS <c>RevealDeckTopCardsAndSelect</c> (RevealLibrary.cs:229) — reveal N, then
    /// run every condition pass SEQUENTIALLY over the shared revealed pool (per-pass maxCount = Min(pass max,
    /// matching in the CURRENT pool); a pass with no match is skipped; chosen cards leave the pool), honouring
    /// the whole-effect <paramref name="canNoAction"/> opt-out (≥2 passes), the exact AS-IS
    /// <paramref name="mutualConditions"/> relaxation (:302-308), per-pass Mode routing + per-card
    /// <c>SelectCardCoroutine</c>, <paramref name="isSendAllCardsToSamePlace"/> (remaining := ALL revealed),
    /// the remaining-cards routing, and the trailing <paramref name="revealedCardsCoroutine"/> (AS-IS runs it
    /// AFTER the remaining routing in this method).</summary>
    public static async Task RevealDeckTopCardsAndSelect(
        int revealCount,
        SelectCardConditionClass[] selectCardConditions,
        RemainingCardsPlace remainingCardsPlace,
        ICardEffect activateClass,
        bool canNoAction = false,
        bool isSendAllCardsToSamePlace = false,
        bool isOpponentDeck = false,
        Func<List<CardSource>, Task>? revealedCardsCoroutine = null,
        bool mutualConditions = false)
    {
        // AS-IS guards (RevealLibrary.cs:240-249).
        if (revealCount <= 0 || activateClass == null)
        {
            return;
        }

        CardSource effectSourceCard = activateClass.EffectSourceCard;
        if (effectSourceCard == null)
        {
            return;
        }

        EngineContext context = effectSourceCard.Context;
        var zones = (IZoneStateReader)context.ZoneMover;
        HeadlessPlayerId selectPlayer = effectSourceCard.Owner;
        HeadlessPlayerId revealPlayer = isOpponentDeck ? OpponentOf(effectSourceCard) : selectPlayer;
        if (revealPlayer.IsEmpty || zones.GetCards(revealPlayer, ChoiceZone.Library).Count == 0 ||
            selectCardConditions == null || selectCardConditions.Length == 0)
        {
            return;
        }

        List<CardSource> allRevealed = zones.GetCards(revealPlayer, ChoiceZone.Library)
            .Take(revealCount)
            .Select(id => new CardSource(context, id, revealPlayer, revealPlayer))
            .ToList();
        List<CardSource> revealedCards = new(allRevealed);   // the working pool the passes consume.
        List<CardSource> chosenCards = new();

        // AS-IS whole-effect opt-out (BT10-096 comment, RevealLibrary.cs:263-283): with >=2 conditions and
        // canNoAction the select player first decides whether to select at all.
        bool doAction = true;
        if (selectCardConditions.Length >= 2 && canNoAction)
        {
            var optOut = new ChoiceRequest(
                ChoiceType.Card, selectPlayer, "Will you select cards?",
                minCount: 0, maxCount: 1, canSkip: true, ChoiceZone.Library,
                new[]
                {
                    new ChoiceCandidate(
                        new HeadlessEntityId($"reveal-optout:{effectSourceCard.InstanceId.Value}"),
                        "Select cards", ChoiceZone.Library, IsSelectable: true, ownerId: selectPlayer),
                });
            ChoiceResult optResult = await context.ChoiceProvider.ChooseAsync(optOut).ConfigureAwait(false);
            doAction = !optResult.IsSkipped && optResult.SelectedIds.Count > 0;
        }

        if (doAction)
        {
            bool firstSelection = true;
            foreach (SelectCardConditionClass selectCondition in selectCardConditions)
            {
                Func<CardSource, bool> canTarget = selectCondition.CanTargetConditionCard
                    ?? throw new InvalidOperationException(
                        "RevealDeckTopCardsAndSelect (bridge W3) requires conditions built through the AS-IS " +
                        "constructor (CardSource-shape CanTargetCondition).");
                int maxCount = Math.Min(selectCondition.MaxCount, revealedCards.Count(canTarget));

                if (maxCount >= 1)
                {
                    bool canNoSelect = selectCondition.CanNoSelect;
                    if (firstSelection)
                    {
                        firstSelection = false;
                    }
                    else if (mutualConditions                       // AS-IS :302-308 verbatim rule:
                          && chosenCards.Count == 1                 // the passes so far chose exactly 1,
                          && canTarget(chosenCards[0])              // that one also fulfils THIS condition,
                          && !revealedCards.Any(cs =>               // and no remaining card can fulfil the
                                 selectCardConditions[0].CanTargetConditionCard!(cs)))   // FIRST condition.
                    {
                        canNoSelect = true;
                    }

                    List<CardSource> selected = await SelectCardsFromRevealPoolAsync(
                        context, selectPlayer, revealedCards, canTarget,
                        selectCondition.CanTargetCondition_ByPreSelecetedList,
                        selectCondition.CanEndSelectConditionCard,
                        maxCount, canNoSelect, selectCondition.CanEndNotMax,
                        string.IsNullOrEmpty(selectCondition.Message) ? "Select card(s)." : selectCondition.Message).ConfigureAwait(false);

                    // AS-IS per-pick selectCardCoroutine runs during the pick (before the mode move).
                    if (selectCondition.SelectCardCoroutine != null)
                    {
                        foreach (CardSource selectedCard in selected)
                        {
                            await selectCondition.SelectCardCoroutine(selectedCard).ConfigureAwait(false);
                        }
                    }

                    // AS-IS SelectCardEffect mode routing for the pass's selection.
                    switch (selectCondition.Mode)
                    {
                        case SelectCardEffect.Mode.AddHand:
                            await StageRevealMovesAsync(context, activateClass, selected, MatchStateMutationSink.ReturnToHandKind).ConfigureAwait(false);
                            break;
                        case SelectCardEffect.Mode.Discard:
                            await StageRevealMovesAsync(context, activateClass, selected, MatchStateMutationSink.TrashCardKind).ConfigureAwait(false);
                            break;
                        case SelectCardEffect.Mode.PlayForFree:
                        case SelectCardEffect.Mode.PlayForCost:
                            // AS-IS SelectCardEffect Mode.PlayForFree/PlayForCost plays the selection via
                            // PlayCardClass — routed through this batch's own AS-IS-signature bridge so the
                            // cardEffect-gated play filtering applies uniformly.
                            await PlayPermanentCards(
                                selected, activateClass,
                                payCost: selectCondition.Mode == SelectCardEffect.Mode.PlayForCost,
                                isTapped: false, root: SelectCardEffect.Root.Library, activateETB: true).ConfigureAwait(false);
                            break;
                        case SelectCardEffect.Mode.Custom:
                            break;   // the per-card SelectCardCoroutine (above) is the whole follow-up.
                    }

                    // AS-IS AfterSelectCardCoroutine (:333-342): chosen cards leave the shared pool.
                    foreach (CardSource cardSource in selected)
                    {
                        chosenCards.Add(cardSource);
                        revealedCards.Remove(cardSource);
                    }
                }
            }
        }

        // AS-IS :425-428: isSendAllCardsToSamePlace routes ALL revealed cards (chosen included) to the
        // remaining place (callers use it with non-moving Custom passes).
        List<CardSource> remainingCards = isSendAllCardsToSamePlace
            ? new List<CardSource>(allRevealed)
            : new List<CardSource>(revealedCards);

        await RouteRemainingRevealedCardsAsync(context, activateClass, selectPlayer, remainingCardsPlace, remainingCards).ConfigureAwait(false);

        // AS-IS :460-463: the whole-reveal follow-up runs AFTER the remaining routing in THIS method.
        if (revealedCardsCoroutine != null)
        {
            await revealedCardsCoroutine(new List<CardSource>(allRevealed)).ConfigureAwait(false);
        }
    }

    #endregion

    #region Return revealed cards to the bottom of deck by any order (AS-IS RevealLibrary.cs:469-533)

    /// <summary>(BRIDGE W3) AS-IS-signature overload of <c>ReturnRevealedCardsToLibraryBottom</c> — AS-IS
    /// guards, then delegates to the verified substrate implementation (Script/CardEffectCommons.cs: 1 card →
    /// straight to bottom; 2+ → the AS-IS ordering pick, pick order = placement).</summary>
    public static async Task ReturnRevealedCardsToLibraryBottom(List<CardSource> remainingCards, ICardEffect activateClass)
    {
        // AS-IS guards (:471-476).
        if (remainingCards == null || remainingCards.Count == 0 || activateClass?.EffectSourceCard == null)
        {
            return;
        }

        await ReturnRevealedCardsToLibraryBottom(remainingCards, activateClass.EffectSourceCard).ConfigureAwait(false);
    }

    #endregion

    #region Shared reveal-select plumbing (private)

    /// <summary>One condition pass's selection over the revealed pool — AS-IS SelectCardPanel semantics (see
    /// file header). Batch ChoiceRequest when no path-dependent per-pick filter; incremental one-pick loop when
    /// <paramref name="byPreSelectedList"/> is present (the AS-IS panel re-filters clickable cards per pick).</summary>
    private static async Task<List<CardSource>> SelectCardsFromRevealPoolAsync(
        EngineContext context,
        HeadlessPlayerId selectPlayer,
        List<CardSource> pool,
        Func<CardSource, bool> canTarget,
        Func<List<CardSource>, CardSource, bool>? byPreSelectedList,
        Func<List<CardSource>, bool>? canEndSelectCondition,
        int maxCount,
        bool canNoSelect,
        bool canEndNotMax,
        string message,
        ChoiceZone zone = ChoiceZone.Library)
    {
        var selected = new List<CardSource>();
        if (maxCount < 1)
        {
            return selected;
        }

        if (byPreSelectedList == null)
        {
            ChoiceCandidate[] candidates = pool
                .Select(cs => new ChoiceCandidate(cs.InstanceId, cs.InstanceId.Value, zone,
                    IsSelectable: canTarget(cs), ownerId: cs.Owner))
                .ToArray();
            int minCount = canNoSelect ? 0 : (canEndNotMax ? Math.Min(1, maxCount) : maxCount);
            var request = new ChoiceRequest(
                ChoiceType.Card, selectPlayer, message, minCount, maxCount, canSkip: canNoSelect,
                zone, candidates);
            if (canEndSelectCondition != null)
            {
                // AS-IS CanEndSelection's combination gate — the established SelectionValidator route
                // (SelectPermanentEffect.CanEndSelect; try-reject-retry contract).
                request = request with
                {
                    SelectionValidator = ids => canEndSelectCondition(
                        ids.Select(id => pool.First(cs => cs.InstanceId == id)).ToList()),
                };
            }

            ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request).ConfigureAwait(false);
            if (!result.IsSkipped)
            {
                foreach (HeadlessEntityId id in result.SelectedIds)
                {
                    CardSource? pick = pool.FirstOrDefault(cs => cs.InstanceId == id);
                    if (pick is not null && !selected.Contains(pick))
                    {
                        selected.Add(pick);
                    }
                }
            }

            return selected;
        }

        // Incremental path: per pick, clickable = canTarget && byPreSelectedList(selectedSoFar, candidate)
        // (SelectCardPanel.cs:451/527); ending allowed per CanEndSelection (:568). One corner the one-pick loop
        // cannot reproduce — UN-picking an already-picked card to satisfy canEndSelectCondition at maxCount —
        // is unreachable for the AS-IS caller shapes (prefix-monotone conditions, e.g. "all chosen have
        // different names"); design item RD-W3-1 records it.
        while (selected.Count < maxCount)
        {
            List<CardSource> legal = pool
                .Where(cs => !selected.Contains(cs) && canTarget(cs) && byPreSelectedList(selected, cs))
                .ToList();
            if (legal.Count == 0)
            {
                break;
            }

            bool canEndNow = (canNoSelect && selected.Count == 0)
                || (canEndNotMax && (canEndSelectCondition == null || canEndSelectCondition(selected)));
            var request = new ChoiceRequest(
                ChoiceType.Card, selectPlayer, message,
                minCount: canEndNow ? 0 : 1, maxCount: 1, canSkip: canEndNow, zone,
                legal.Select(cs => new ChoiceCandidate(cs.InstanceId, cs.InstanceId.Value, zone,
                    IsSelectable: true, ownerId: cs.Owner)).ToArray());
            ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request).ConfigureAwait(false);
            if (result.IsSkipped || result.SelectedIds.Count == 0)
            {
                break;
            }

            CardSource? picked = legal.FirstOrDefault(cs => cs.InstanceId == result.SelectedIds[0]);
            if (picked is null)
            {
                break;
            }

            selected.Add(picked);
        }

        return selected;
    }

    /// <summary>Stage one reveal-flow move per card and flush. A revealed card sent to the TRASH carries the
    /// <see cref="MatchStateMutationSink.RevealTrashFlagKey"/> stamp (AS-IS resets IsBeingRevealed only after
    /// the trash move, so its OnDiscardLibrary broadcast is suppressed — F1 reveal-remainder convention,
    /// copied from the verified RevealMultiSelectEffect.StageMove).</summary>
    private static async Task StageRevealMovesAsync(
        EngineContext context, ICardEffect activateClass, IReadOnlyList<CardSource> cards, string mutationKind)
    {
        if (cards.Count == 0)
        {
            return;
        }

        HeadlessEntityId sourceId = activateClass.EffectSourceCard.InstanceId;
        var sink = NewSink(context);
        foreach (CardSource cs in cards)
        {
            var values = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [MatchStateMutationSink.TargetEntityIdKey] = cs.InstanceId.Value,
            };
            if (mutationKind == MatchStateMutationSink.TrashCardKind)
            {
                values[MatchStateMutationSink.RevealTrashFlagKey] = true;
            }

            sink.Apply(new EffectMutation(mutationKind, sourceId, values));
        }

        await sink.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>The AS-IS remaining-cards routing switch, shared by all three reveal bridges (AS-IS
    /// RevealLibrary.cs:103-166/:430-458): DeckBottom / DeckTop (≥2 → ordering pick; top = first pick topmost),
    /// Trash (reveal-stamped), AddHand, DeckTopOrBottom (top-vs-bottom binary pick first), None.</summary>
    private static async Task RouteRemainingRevealedCardsAsync(
        EngineContext context,
        ICardEffect activateClass,
        HeadlessPlayerId selectPlayer,
        RemainingCardsPlace remainingCardsPlace,
        List<CardSource> remainingCards)
    {
        if (remainingCards.Count == 0 || remainingCardsPlace == RemainingCardsPlace.None)
        {
            return;
        }

        switch (remainingCardsPlace)
        {
            case RemainingCardsPlace.DeckBottom:
                await ReturnRevealedCardsToLibraryBottom(remainingCards, activateClass.EffectSourceCard).ConfigureAwait(false);
                break;

            case RemainingCardsPlace.DeckTop:
                await ReturnRevealedCardsToLibraryTopAsync(context, activateClass, selectPlayer, remainingCards).ConfigureAwait(false);
                break;

            case RemainingCardsPlace.Trash:
                await StageRevealMovesAsync(context, activateClass, remainingCards, MatchStateMutationSink.TrashCardKind).ConfigureAwait(false);
                break;

            case RemainingCardsPlace.AddHand:
                await StageRevealMovesAsync(context, activateClass, remainingCards, MatchStateMutationSink.ReturnToHandKind).ConfigureAwait(false);
                break;

            case RemainingCardsPlace.DeckTopOrBottom:
            {
                // AS-IS ReturnRevealedCardsToLibraryTopOrBottom (:619-651): the select player picks Top or
                // Bottom first (verified RevealMultiSelectEffect.HandleRemainingAsync pattern).
                var placeRequest = new ChoiceRequest(
                    ChoiceType.Card, selectPlayer, "To which area do you place cards?",
                    minCount: 1, maxCount: 1, canSkip: false, ChoiceZone.Library,
                    new[]
                    {
                        new ChoiceCandidate(
                            new HeadlessEntityId($"reveal-place-top:{activateClass.EffectSourceCard.InstanceId.Value}"),
                            "Deck Top", ChoiceZone.Library, IsSelectable: true, ownerId: selectPlayer),
                        new ChoiceCandidate(
                            new HeadlessEntityId($"reveal-place-bottom:{activateClass.EffectSourceCard.InstanceId.Value}"),
                            "Deck Bottom", ChoiceZone.Library, IsSelectable: true, ownerId: selectPlayer),
                    });
                ChoiceResult placeResult = await context.ChoiceProvider.ChooseAsync(placeRequest).ConfigureAwait(false);
                bool toTop = placeResult.SelectedIds.Count > 0 &&
                    placeResult.SelectedIds[0].Value.StartsWith("reveal-place-top", StringComparison.Ordinal);
                if (toTop)
                {
                    await ReturnRevealedCardsToLibraryTopAsync(context, activateClass, selectPlayer, remainingCards).ConfigureAwait(false);
                }
                else
                {
                    await ReturnRevealedCardsToLibraryBottom(remainingCards, activateClass.EffectSourceCard).ConfigureAwait(false);
                }

                break;
            }
        }
    }

    /// <summary>AS-IS <c>ReturnRevealedCardsToLibraryTop</c> (RevealLibrary.cs:537-583, non-public sibling):
    /// one card → straight to top; two-plus → the AS-IS ordering pick, where the FIRST pick ends up TOPMOST
    /// (AS-IS reverses the picked list before AddLibraryTopCards) — staged in reverse pick order because each
    /// ReturnToDeckTop insert stacks (verified RevealMultiSelectEffect convention).</summary>
    private static async Task ReturnRevealedCardsToLibraryTopAsync(
        EngineContext context, ICardEffect activateClass, HeadlessPlayerId selectPlayer, List<CardSource> remainingCards)
    {
        if (remainingCards.Count == 0 || activateClass?.EffectSourceCard == null)
        {
            return;
        }

        IReadOnlyList<CardSource> ordered = remainingCards;
        if (remainingCards.Count >= 2)
        {
            var orderRequest = new ChoiceRequest(
                ChoiceType.Card, selectPlayer,
                "Specify the order to place the card at the top of the deck\n(cards will be placed back to the top of the deck so that cards with lower numbers are on top).",
                minCount: remainingCards.Count, maxCount: remainingCards.Count, canSkip: false, ChoiceZone.Library,
                remainingCards.Select(cs => new ChoiceCandidate(cs.InstanceId, cs.InstanceId.Value, ChoiceZone.Library,
                    IsSelectable: true, ownerId: cs.Owner)).ToArray());
            ChoiceResult orderResult = await context.ChoiceProvider.ChooseAsync(orderRequest).ConfigureAwait(false);
            if (orderResult.SelectedIds.Count == remainingCards.Count)
            {
                ordered = orderResult.SelectedIds
                    .Select(id => remainingCards.First(cs => cs.InstanceId == id))
                    .ToArray();
            }
        }

        var sink = NewSink(context);
        foreach (CardSource cs in ordered.Reverse())   // first pick staged LAST -> ends up topmost.
        {
            sink.Apply(new EffectMutation(
                MatchStateMutationSink.ReturnToDeckTopKind, activateClass.EffectSourceCard.InstanceId,
                new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = cs.InstanceId.Value }));
        }

        await sink.FlushAsync().ConfigureAwait(false);
    }

    #endregion
}
