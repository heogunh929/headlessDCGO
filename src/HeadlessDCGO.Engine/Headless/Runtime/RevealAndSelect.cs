namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>Where a revealed card is sent after the reveal-and-select decision.</summary>
public enum RevealDestination
{
    Hand = 0,
    DeckTop = 1,
    DeckBottom = 2,
    Trash = 3,

    /// <summary>(B4) AS-IS <c>RemainingCardsPlace.DeckTopOrBottom</c> — the controller first picks Top or
    /// Bottom for the remaining cards, then specifies their order.</summary>
    DeckTopOrBottom = 4,

    /// <summary>(P4) AS-IS <c>SelectCardEffect.Mode.Custom</c> — no built-in move: the selected cards are
    /// recorded on the <see cref="RevealFlowState"/> for the card script's follow-up (e.g. "play it for
    /// free"). They are excluded from the remaining-cards handling.</summary>
    Custom = 5,
}

/// <summary>(P4) One selection pass of a multi-condition reveal (AS-IS <c>SelectCardConditionClass</c>):
/// only condition-matching revealed cards are selectable, up to MaxCount, sent to Destination.</summary>
public sealed record RevealSelectPass(
    Func<HeadlessEntityId, bool> Condition,
    int MaxCount,
    RevealDestination Destination,
    string Message,
    bool CanNoSelect = false,
    bool CanEndNotMax = false);

/// <summary>(P4) The in-flight multi-condition reveal flow — a context service (conditions are Funcs, so
/// nothing is serialised into request ids). One flow at a time (choices serialise).</summary>
public sealed class RevealFlowState
{
    public IReadOnlyList<RevealSelectPass> Passes { get; internal set; } = Array.Empty<RevealSelectPass>();
    public int PassIndex { get; internal set; }
    public List<HeadlessEntityId> Remaining { get; } = new();
    public List<HeadlessEntityId> Chosen { get; } = new();
    internal List<HeadlessEntityId> CustomSelections { get; } = new();
    public RevealDestination RemainingTo { get; internal set; }
    public HeadlessPlayerId Chooser { get; internal set; }
    public HeadlessPlayerId Owner { get; internal set; }
    public bool MutualConditions { get; internal set; }
    public bool IsActive { get; internal set; }

    /// <summary>The cards picked by <see cref="RevealDestination.Custom"/> passes — the card script's
    /// follow-up consumes (and clears) them after the flow finishes.</summary>
    public IReadOnlyList<HeadlessEntityId> TakeCustomSelections()
    {
        var taken = CustomSelections.ToArray();
        CustomSelections.Clear();
        return taken;
    }

    internal void Reset()
    {
        Passes = Array.Empty<RevealSelectPass>();
        PassIndex = 0;
        Remaining.Clear();
        Chosen.Clear();
        RemainingTo = RevealDestination.DeckBottom;
        MutualConditions = false;
        IsActive = false;
    }
}

/// <summary>
/// (B-7) Reveal the top N library cards and let the controller SELECT some of them (AS-IS
/// <c>RevealLibrary.RevealDeckTopCardsAndSelect</c>): the selected cards go to one destination (e.g. the
/// hand), the remaining revealed cards to another (e.g. the deck bottom). The reveal only peeks the library
/// top; the cards move when the choice resolves. The selection is an agent choice (rules-faithful).
///
/// (B4) AS-IS parity restored:
/// <list type="bullet">
/// <item><b>selectCondition</b> — only condition-matching revealed cards are selectable (AS-IS
/// <c>SetUp(canTargetCondition: selectCondition.CanTargetCondition)</c>); the rest are shown unselectable.</item>
/// <item><b>Remaining-card ORDER</b> — with ≥2 remaining cards bound for the deck, the controller specifies
/// the order (AS-IS <c>ReturnRevealedCardsToLibraryBottom/Top</c>: a full sequential pick; pick order =
/// placement order, "lower numbers on top"; the TOP variant reverses before inserting).</item>
/// <item><b>DeckTopOrBottom</b> — the controller picks Top vs Bottom first, then the order (AS-IS
/// <c>ReturnRevealedCardsToLibraryTopOrBottom</c>).</item>
/// <item><b>isOpponentDeck</b> — reveals the OPPONENT's library (AS-IS <c>revealPlayer = selectPlayer.Enemy</c>).</item>
/// <item><b>ProcessForAll</b> — <see cref="RevealAndProcessAllAsync"/>: NO selection; every matching card is
/// processed mandatorily (AS-IS <c>RevealDeckTopCardsAndProcessForAll</c> has no player choice).</item>
/// </list>
/// (P4) Multi-condition sequential passes (AS-IS <c>SelectCardConditionClass[]</c>, BT10-096/BT10-097/
/// ST17-11 shape): <see cref="RequestMultiChoice"/> runs each pass over the SHARED revealed pool (chosen
/// cards removed between passes, per-pass maxCount recomputed, empty passes skipped, per-pass Mode =
/// destination — <see cref="RevealDestination.Custom"/> records for the card script). The AS-IS
/// <c>mutualConditions</c> rule (relax canNoSelect on a later pass when the single already-chosen card also
/// satisfied it and pass[0] has no candidates left) is mirrored; no current caller sets it.
/// </summary>
public static class RevealAndSelect
{
    public const string RequestIdPrefix = "reveal-select";
    public const string OrderRequestIdPrefix = "reveal-order";
    public const string PlaceRequestIdPrefix = "reveal-place";
    public const string MultiRequestIdPrefix = "reveal-multi";
    public const string PlaceTopCandidate = "reveal-place-top";
    public const string PlaceBottomCandidate = "reveal-place-bottom";
    private const char Delimiter = ':';
    private const char IdListDelimiter = '|';

    /// <summary>(P4) Reveals the top <paramref name="revealCount"/> cards and runs the AS-IS
    /// multi-condition selection: one choice per pass, sequentially. Returns true when a choice opened;
    /// false when nothing was revealed (or every pass was empty and the remaining cards resolved without a
    /// choice).</summary>
    public static async Task<bool> RequestMultiChoice(
        EngineContext context,
        HeadlessPlayerId player,
        int revealCount,
        IReadOnlyList<RevealSelectPass> passes,
        RevealDestination remainingTo,
        bool isOpponentDeck = false,
        bool mutualConditions = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(passes);
        if (context.ChoiceController.Current.IsPending ||
            context.ZoneMover is not IZoneStateReader zones ||
            player.IsEmpty || passes.Count == 0)
        {
            return false;
        }

        HeadlessPlayerId revealPlayer = isOpponentDeck ? Opponent(context, player) : player;
        if (revealPlayer.IsEmpty)
        {
            return false;
        }

        HeadlessEntityId[] revealed = zones.GetCards(revealPlayer, ChoiceZone.Library).Take(Math.Max(0, revealCount)).ToArray();
        if (revealed.Length == 0)
        {
            return false;
        }

        RevealFlowState state = GetFlowState(context);
        state.Reset();
        state.IsActive = true;
        state.Passes = passes;
        state.Remaining.AddRange(revealed);
        state.RemainingTo = remainingTo;
        state.Chooser = player;
        state.Owner = revealPlayer;
        state.MutualConditions = mutualConditions;
        return await OpenNextPassAsync(context, state).ConfigureAwait(false);
    }

    // (P4) AS-IS per-condition loop (RevealLibrary.cs:291-341): maxCount = Min(cond.MaxCount, matching in
    // the CURRENT pool); a pass with no matching card is skipped (the loop continues); chosen cards leave
    // the pool between passes.
    private static async Task<bool> OpenNextPassAsync(EngineContext context, RevealFlowState state)
    {
        while (state.PassIndex < state.Passes.Count)
        {
            RevealSelectPass pass = state.Passes[state.PassIndex];
            int matching = state.Remaining.Count(pass.Condition);
            if (matching == 0)
            {
                state.PassIndex++;
                continue;
            }

            bool canNoSelect = pass.CanNoSelect;
            // AS-IS mutualConditions (RevealLibrary.cs:302-308): a later pass becomes optional when exactly
            // one card was chosen so far, it also satisfies THIS pass, and pass[0] has no candidates left.
            if (!canNoSelect && state.MutualConditions && state.PassIndex > 0 &&
                state.Chosen.Count == 1 && pass.Condition(state.Chosen[0]) &&
                !state.Remaining.Any(state.Passes[0].Condition))
            {
                canNoSelect = true;
            }

            int maxCount = Math.Min(pass.MaxCount, matching);
            int minCount = canNoSelect ? 0 : (pass.CanEndNotMax ? Math.Min(1, maxCount) : maxCount);
            ChoiceCandidate[] candidates = state.Remaining
                .Select(id => new ChoiceCandidate(
                    id, $"Reveal {id.Value}", ChoiceZone.Library,
                    IsSelectable: pass.Condition(id), ownerId: state.Owner))
                .ToArray();
            var request = new ChoiceRequest(
                ChoiceType.RevealSelect, state.Chooser, pass.Message,
                minCount, maxCount, canSkip: canNoSelect, ChoiceZone.Library, candidates);
            context.ChoiceController.RequestChoice(request, new HeadlessEntityId($"{MultiRequestIdPrefix}{Delimiter}{state.PassIndex}"));
            return true;
        }

        // All passes done — the untouched revealed cards follow the remaining-cards flow.
        var remaining = state.Remaining.ToArray();
        RevealDestination remainingTo = state.RemainingTo;
        HeadlessPlayerId chooser = state.Chooser;
        HeadlessPlayerId owner = state.Owner;
        state.Reset();
        return await HandleRemainingAsync(context, chooser, owner, remaining, remainingTo).ConfigureAwait(false);
    }

    private static async Task<bool> ResolveMultiChoice(EngineContext context, ChoiceRequest request, ChoiceResult result)
    {
        RevealFlowState state = GetFlowState(context);
        if (!state.IsActive || state.PassIndex >= state.Passes.Count)
        {
            return false;
        }

        RevealSelectPass pass = state.Passes[state.PassIndex];

        try
        {
            context.ChoiceController.ResolveChoice(result);
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        var selected = result.IsSkipped ? new List<HeadlessEntityId>() : result.SelectedIds.ToList();
        foreach (HeadlessEntityId cardId in selected)
        {
            state.Remaining.Remove(cardId);
            state.Chosen.Add(cardId);
            if (pass.Destination == RevealDestination.Custom)
            {
                state.CustomSelections.Add(cardId);   // no move — the card script's follow-up handles it.
            }
            else
            {
                await MoveAsync(context, state.Owner, cardId, pass.Destination).ConfigureAwait(false);
            }
        }

        state.PassIndex++;
        await OpenNextPassAsync(context, state).ConfigureAwait(false);
        return true;
    }

    private static RevealFlowState GetFlowState(EngineContext context)
    {
        if (context.TryGetService(out RevealFlowState? state) && state is not null)
        {
            return state;
        }

        var created = new RevealFlowState();
        context.RegisterService(created);
        return created;
    }

    /// <summary>Reveals the top <paramref name="revealCount"/> library cards and opens the selection choice
    /// (up to <paramref name="maxSelect"/> of the condition-matching cards). Returns true when a choice
    /// opened (false if the library is empty).</summary>
    public static bool RequestChoice(
        EngineContext context,
        HeadlessPlayerId player,
        int revealCount,
        int maxSelect,
        RevealDestination selectedTo,
        RevealDestination remainingTo,
        Func<HeadlessEntityId, bool>? selectCondition = null,
        bool isOpponentDeck = false)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.ChoiceController.Current.IsPending ||
            context.ZoneMover is not IZoneStateReader zones ||
            player.IsEmpty)
        {
            return false;
        }

        HeadlessPlayerId revealPlayer = isOpponentDeck ? Opponent(context, player) : player;
        if (revealPlayer.IsEmpty)
        {
            return false;
        }

        HeadlessEntityId[] revealed = zones.GetCards(revealPlayer, ChoiceZone.Library).Take(Math.Max(0, revealCount)).ToArray();
        if (revealed.Length == 0)
        {
            return false;
        }

        // (B4/F11) every revealed card is SHOWN (the reveal is public), but only condition-matching cards
        // are selectable; maxCount clamps to the matching pool (AS-IS Min(MaxCount, revealed.Count(cond))).
        ChoiceCandidate[] candidates = revealed
            .Select(id => new ChoiceCandidate(
                id, $"Reveal {id.Value}", ChoiceZone.Library,
                IsSelectable: selectCondition is null || selectCondition(id), ownerId: revealPlayer))
            .ToArray();

        int matching = candidates.Count(candidate => candidate.IsSelectable);
        int max = Math.Clamp(maxSelect, 0, matching);
        var request = new ChoiceRequest(
            ChoiceType.RevealSelect,
            player,
            $"Reveal {revealed.Length}: select up to {max} card(s).",
            minCount: 0,
            maxCount: Math.Max(1, max),
            canSkip: true,
            ChoiceZone.Library,
            candidates);
        context.ChoiceController.RequestChoice(
            request,
            new HeadlessEntityId($"{RequestIdPrefix}{Delimiter}{(int)selectedTo}{Delimiter}{(int)remainingTo}{Delimiter}{revealPlayer.Value}"));
        return true;
    }

    /// <summary>(B4/F16) AS-IS <c>RevealDeckTopCardsAndProcessForAll</c> — NO selection: EVERY revealed card
    /// matching <paramref name="condition"/> goes to <paramref name="matchedTo"/> (mandatory), the rest to
    /// <paramref name="remainingTo"/> (which may open the ordering/top-or-bottom choices). Returns true when
    /// a follow-up choice opened, false when everything resolved immediately.</summary>
    public static async Task<bool> RevealAndProcessAllAsync(
        EngineContext context,
        HeadlessPlayerId player,
        int revealCount,
        Func<HeadlessEntityId, bool> condition,
        RevealDestination matchedTo,
        RevealDestination remainingTo,
        bool isOpponentDeck = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(condition);
        if (context.ChoiceController.Current.IsPending ||
            context.ZoneMover is not IZoneStateReader zones ||
            player.IsEmpty)
        {
            return false;
        }

        HeadlessPlayerId revealPlayer = isOpponentDeck ? Opponent(context, player) : player;
        if (revealPlayer.IsEmpty)
        {
            return false;
        }

        HeadlessEntityId[] revealed = zones.GetCards(revealPlayer, ChoiceZone.Library).Take(Math.Max(0, revealCount)).ToArray();
        var remaining = new List<HeadlessEntityId>();
        foreach (HeadlessEntityId cardId in revealed)
        {
            if (condition(cardId))
            {
                await MoveAsync(context, revealPlayer, cardId, matchedTo).ConfigureAwait(false);
            }
            else
            {
                remaining.Add(cardId);
            }
        }

        return await HandleRemainingAsync(context, player, revealPlayer, remaining, remainingTo).ConfigureAwait(false);
    }

    /// <summary>Resolves any reveal-flow choice (primary select / top-or-bottom place / ordering).</summary>
    public static async Task<bool> ResolveChoice(EngineContext context, ChoiceResult result)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(result);
        if (!context.ChoiceController.Current.IsPending ||
            context.ChoiceController.PendingRequest is not ChoiceRequest request ||
            request.Type != ChoiceType.RevealSelect)
        {
            return false;
        }

        string requestId = (context.ChoiceController.Current.RequestId ?? default).Value;
        if (requestId.StartsWith($"{OrderRequestIdPrefix}{Delimiter}", StringComparison.Ordinal))
        {
            return await ResolveOrderChoice(context, request, result, requestId).ConfigureAwait(false);
        }

        if (requestId.StartsWith($"{PlaceRequestIdPrefix}{Delimiter}", StringComparison.Ordinal))
        {
            return await ResolvePlaceChoice(context, request, result, requestId).ConfigureAwait(false);
        }

        if (requestId.StartsWith($"{MultiRequestIdPrefix}{Delimiter}", StringComparison.Ordinal))
        {
            return await ResolveMultiChoice(context, request, result).ConfigureAwait(false);
        }

        return await ResolvePrimaryChoice(context, request, result, requestId).ConfigureAwait(false);
    }

    private static async Task<bool> ResolvePrimaryChoice(EngineContext context, ChoiceRequest request, ChoiceResult result, string requestId)
    {
        HeadlessPlayerId player = request.PlayerId;
        var revealed = request.Candidates.Select(candidate => candidate.Id).ToList();
        (RevealDestination selectedTo, RevealDestination remainingTo, HeadlessPlayerId revealPlayer) =
            ParsePrimaryRequest(requestId, player);

        try
        {
            context.ChoiceController.ResolveChoice(result);
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        var selected = result.IsSkipped ? new List<HeadlessEntityId>() : result.SelectedIds.ToList();
        var remaining = new List<HeadlessEntityId>();
        foreach (HeadlessEntityId cardId in revealed)
        {
            if (selected.Contains(cardId))
            {
                await MoveAsync(context, revealPlayer, cardId, selectedTo).ConfigureAwait(false);
            }
            else
            {
                remaining.Add(cardId);
            }
        }

        await HandleRemainingAsync(context, player, revealPlayer, remaining, remainingTo).ConfigureAwait(false);
        return true;
    }

    // (B4/F12+F13) route the non-selected cards: DeckTopOrBottom asks Top vs Bottom first (AS-IS
    // ReturnRevealedCardsToLibraryTopOrBottom); a deck destination with >= 2 cards asks the ORDER (AS-IS
    // full sequential pick, no prompt for a single card); anything else moves in reveal order.
    private static async Task<bool> HandleRemainingAsync(
        EngineContext context,
        HeadlessPlayerId chooser,
        HeadlessPlayerId owner,
        IReadOnlyList<HeadlessEntityId> remaining,
        RevealDestination destination)
    {
        if (remaining.Count == 0)
        {
            return false;
        }

        if (destination == RevealDestination.DeckTopOrBottom)
        {
            if (remaining.Count == 0)
            {
                return false;
            }

            var placeCandidates = new[]
            {
                new ChoiceCandidate(new HeadlessEntityId(PlaceTopCandidate), "Top of the deck", ChoiceZone.Library, IsSelectable: true, ownerId: chooser),
                new ChoiceCandidate(new HeadlessEntityId(PlaceBottomCandidate), "Bottom of the deck", ChoiceZone.Library, IsSelectable: true, ownerId: chooser),
            };
            var placeRequest = new ChoiceRequest(
                ChoiceType.RevealSelect, chooser,
                "Place the remaining revealed cards on the top or the bottom of the deck.",
                minCount: 1, maxCount: 1, canSkip: false, ChoiceZone.Library, placeCandidates);
            context.ChoiceController.RequestChoice(placeRequest, new HeadlessEntityId(
                $"{PlaceRequestIdPrefix}{Delimiter}{owner.Value}{Delimiter}{string.Join(IdListDelimiter, remaining.Select(id => id.Value))}"));
            return true;
        }

        bool deckDestination = destination is RevealDestination.DeckTop or RevealDestination.DeckBottom;
        if (deckDestination && remaining.Count >= 2)
        {
            ChoiceCandidate[] orderCandidates = remaining
                .Select(id => new ChoiceCandidate(id, $"Order {id.Value}", ChoiceZone.Library, IsSelectable: true, ownerId: owner))
                .ToArray();
            var orderRequest = new ChoiceRequest(
                ChoiceType.RevealSelect, chooser,
                destination == RevealDestination.DeckBottom
                    ? "Specify the order to place the cards at the bottom of the deck (earlier picks end up higher)."
                    : "Specify the order to place the cards on the top of the deck (earlier picks end up higher).",
                minCount: remaining.Count, maxCount: remaining.Count, canSkip: false, ChoiceZone.Library, orderCandidates);
            context.ChoiceController.RequestChoice(orderRequest, new HeadlessEntityId(
                $"{OrderRequestIdPrefix}{Delimiter}{(int)destination}{Delimiter}{owner.Value}"));
            return true;
        }

        foreach (HeadlessEntityId cardId in remaining)
        {
            await MoveAsync(context, owner, cardId, destination).ConfigureAwait(false);
        }

        return false;
    }

    private static async Task<bool> ResolvePlaceChoice(EngineContext context, ChoiceRequest request, ChoiceResult result, string requestId)
    {
        // {place}:{owner}:{id|id|...}
        string[] parts = requestId.Split(Delimiter, 3);
        if (parts.Length != 3)
        {
            return false;
        }

        var owner = new HeadlessPlayerId(int.TryParse(parts[1], out int ownerId) ? ownerId : 0);
        List<HeadlessEntityId> remaining = parts[2]
            .Split(IdListDelimiter, StringSplitOptions.RemoveEmptyEntries)
            .Select(value => new HeadlessEntityId(value))
            .ToList();

        try
        {
            context.ChoiceController.ResolveChoice(result);
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        RevealDestination destination = result.SelectedIds.Count > 0 && result.SelectedIds[0].Value == PlaceTopCandidate
            ? RevealDestination.DeckTop
            : RevealDestination.DeckBottom;
        await HandleRemainingAsync(context, request.PlayerId, owner, remaining, destination).ConfigureAwait(false);
        return true;
    }

    private static async Task<bool> ResolveOrderChoice(EngineContext context, ChoiceRequest request, ChoiceResult result, string requestId)
    {
        // {order}:{destination}:{owner}
        string[] parts = requestId.Split(Delimiter);
        if (parts.Length != 3 || !int.TryParse(parts[1], out int destinationRaw) || !int.TryParse(parts[2], out int ownerRaw))
        {
            return false;
        }

        var destination = (RevealDestination)destinationRaw;
        var owner = new HeadlessPlayerId(ownerRaw);
        var revealedOrder = request.Candidates.Select(candidate => candidate.Id).ToList();

        try
        {
            context.ChoiceController.ResolveChoice(result);
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        // The pick ORDER is the placement order; unpicked stragglers (defensive) keep reveal order at the end.
        List<HeadlessEntityId> ordered = result.SelectedIds.ToList();
        ordered.AddRange(revealedOrder.Where(id => !ordered.Contains(id)));

        if (destination == RevealDestination.DeckTop)
        {
            // AS-IS ReturnRevealedCardsToLibraryTop: topCards.Reverse() before inserting so the FIRST pick
            // ends topmost — sequential insert-at-top must run in reverse pick order.
            for (int index = ordered.Count - 1; index >= 0; index--)
            {
                await context.ZoneMover.MoveToDeckTopAsync(owner, ordered[index]).ConfigureAwait(false);
            }
        }
        else
        {
            // AS-IS ReturnRevealedCardsToLibraryBottom: bottom in pick order ("lower numbers are on top").
            foreach (HeadlessEntityId cardId in ordered)
            {
                await context.ZoneMover.MoveToDeckBottomAsync(owner, cardId).ConfigureAwait(false);
            }
        }

        return true;
    }

    private static Task MoveAsync(EngineContext context, HeadlessPlayerId player, HeadlessEntityId cardId, RevealDestination destination) =>
        destination switch
        {
            RevealDestination.Hand => context.ZoneMover.AddToHandAsync(player, cardId),
            RevealDestination.DeckTop => context.ZoneMover.MoveToDeckTopAsync(player, cardId),
            RevealDestination.DeckBottom => context.ZoneMover.MoveToDeckBottomAsync(player, cardId),
            RevealDestination.Trash => context.ZoneMover.AddToTrashAsync(player, cardId),
            _ => context.ZoneMover.MoveToDeckBottomAsync(player, cardId),
        };

    private static (RevealDestination Selected, RevealDestination Remaining, HeadlessPlayerId RevealPlayer) ParsePrimaryRequest(
        string requestId, HeadlessPlayerId fallbackPlayer)
    {
        string[] parts = requestId.Split(Delimiter);
        // {prefix}:{selected}:{remaining}[:{revealPlayer}]
        if (parts.Length >= 3 &&
            int.TryParse(parts[1], out int selected) &&
            int.TryParse(parts[2], out int remaining))
        {
            HeadlessPlayerId revealPlayer = parts.Length >= 4 && int.TryParse(parts[3], out int playerRaw)
                ? new HeadlessPlayerId(playerRaw)
                : fallbackPlayer;
            return ((RevealDestination)selected, (RevealDestination)remaining, revealPlayer);
        }

        return (RevealDestination.Hand, RevealDestination.DeckBottom, fallbackPlayer);
    }

    private static HeadlessPlayerId Opponent(EngineContext context, HeadlessPlayerId player)
    {
        foreach (HeadlessPlayerId candidate in context.TurnController.Current.PlayerOrder)
        {
            if (!candidate.IsEmpty && candidate != player)
            {
                return candidate;
            }
        }

        return default;
    }
}
