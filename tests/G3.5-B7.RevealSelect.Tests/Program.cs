// B-7 reveal & select: reveal the top N library cards and let the controller select some (AS-IS
// RevealLibrary.RevealDeckTopCardsAndSelect). Selected cards go to one destination (here: hand), the rest
// to another (deck bottom). Engine: RevealAndSelect (RequestChoice/ResolveChoice via ChoiceType.RevealSelect)
// + MetadataActionProcessor routing. The selection is an agent choice.
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Reveal opens a select choice over the top N cards", RevealOpensChoice),
    ("Selected card goes to hand; the rest go to the deck bottom", SelectedToHandRestToBottom),
    ("Skipping sends all revealed cards to the remaining destination", SkipSendsAllToRemaining),
    ("An empty library opens no reveal choice", EmptyLibraryNoChoice),
    ("(B4) selectCondition: non-matching revealed cards are shown but NOT selectable; max clamps to the pool", ConditionFiltersSelectables),
    ("(B4) ProcessForAll: no selection — every matching card is processed mandatorily", ProcessForAllMandatory),
    ("(B4) DeckTop ordering: the FIRST pick ends topmost (AS-IS Reverse)", TopOrderReversed),
    ("(B4) DeckTopOrBottom: the controller picks top/bottom, then the order", TopOrBottomTwoStep),
    ("(B4) isOpponentDeck reveals the OPPONENT's library", OpponentDeckReveal),
    ("(P4) multi-condition passes share the pool; per-pass destination incl. Custom (BT10-096 shape)", MultiConditionPasses),
    ("(P4) a pass with no matching revealed card is skipped (loop continues)", EmptyPassSkipped),
    ("(P4) mutualConditions relaxes a later pass when the single chosen card consumed its only candidate", MutualConditionsRelaxes),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex)
    {
        failures.Add(test.Name);
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}");
    }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

async Task RevealOpensChoice()
{
    EngineContext context = await NewMatch();
    AssertTrue(RevealAndSelect.RequestChoice(context, P1, revealCount: 3, maxSelect: 1, RevealDestination.Hand, RevealDestination.DeckBottom),
        "reveal choice opened");
    AssertEqual(ChoiceType.RevealSelect, context.ChoiceController.PendingRequest!.Type, "choice type");
    AssertEqual(3, context.ChoiceController.PendingRequest!.Candidates.Count, "3 cards revealed");
}

async Task SelectedToHandRestToBottom()
{
    EngineContext context = await NewMatch();
    HeadlessEntityId[] top = Top(context, P1, 3);
    int handBefore = Count(context, P1, ChoiceZone.Hand);

    RevealAndSelect.RequestChoice(context, P1, revealCount: 3, maxSelect: 1, RevealDestination.Hand, RevealDestination.DeckBottom);
    await RevealAndSelect.ResolveChoice(context, ChoiceResult.Select(top[0]));

    AssertTrue(InZone(context, P1, ChoiceZone.Hand, top[0]), "the selected card is in hand");
    AssertEqual(handBefore + 1, Count(context, P1, ChoiceZone.Hand), "hand grew by exactly 1");
    AssertFalse(InZone(context, P1, ChoiceZone.Hand, top[1]), "unselected card is not in hand");

    // (B4) >= 2 remaining cards bound for the deck: the controller specifies the ORDER (AS-IS
    // ReturnRevealedCardsToLibraryBottom) — pick top[2] first so it ends up higher at the bottom.
    AssertTrue(context.ChoiceController.Current.IsPending, "an ordering choice opened for the remaining cards");
    await RevealAndSelect.ResolveChoice(context, ChoiceResult.Select(top[2], top[1]));

    HeadlessEntityId[] library = ((IZoneStateReader)context.ZoneMover).GetCards(P1, ChoiceZone.Library).ToArray();
    AssertEqual(top[2].Value, library[^2].Value, "first pick sits HIGHER at the bottom (AS-IS pick order)");
    AssertEqual(top[1].Value, library[^1].Value, "second pick is the very bottom");
}

async Task SkipSendsAllToRemaining()
{
    EngineContext context = await NewMatch();
    HeadlessEntityId[] top = Top(context, P1, 3);
    int handBefore = Count(context, P1, ChoiceZone.Hand);

    RevealAndSelect.RequestChoice(context, P1, revealCount: 3, maxSelect: 1, RevealDestination.Hand, RevealDestination.DeckBottom);
    await RevealAndSelect.ResolveChoice(context, ChoiceResult.Skip());

    AssertEqual(handBefore, Count(context, P1, ChoiceZone.Hand), "skipping adds nothing to hand");
    // (B4) all three remaining -> ordering choice; resolve in reveal order.
    AssertTrue(context.ChoiceController.Current.IsPending, "ordering choice opened");
    await RevealAndSelect.ResolveChoice(context, ChoiceResult.Select(top));
    foreach (HeadlessEntityId id in top)
    {
        AssertTrue(InZone(context, P1, ChoiceZone.Library, id), "all revealed cards remain in the library (sent to bottom)");
    }
}

async Task EmptyLibraryNoChoice()
{
    EngineContext context = await NewMatch();
    // Drain the library into the hand.
    await context.ZoneMover.DrawAsync(P1, Count(context, P1, ChoiceZone.Library));
    AssertEqual(0, Count(context, P1, ChoiceZone.Library), "library emptied");

    AssertFalse(RevealAndSelect.RequestChoice(context, P1, revealCount: 3, maxSelect: 1, RevealDestination.Hand, RevealDestination.DeckBottom),
        "no reveal choice with an empty library");
}

// --- (B4) AS-IS parity ----------------------------------------------------

async Task ConditionFiltersSelectables()
{
    EngineContext context = await NewMatch();
    HeadlessEntityId[] top = Top(context, P1, 3);

    // Only the SECOND revealed card matches.
    RevealAndSelect.RequestChoice(context, P1, revealCount: 3, maxSelect: 2,
        RevealDestination.Hand, RevealDestination.DeckBottom, selectCondition: id => id == top[1]);

    ChoiceRequest request = context.ChoiceController.PendingRequest!;
    AssertEqual(3, request.Candidates.Count, "all revealed cards are SHOWN (public reveal)");
    AssertFalse(request.Candidates.First(c => c.Id == top[0]).IsSelectable, "non-matching card is NOT selectable");
    AssertTrue(request.Candidates.First(c => c.Id == top[1]).IsSelectable, "matching card is selectable");
    AssertEqual(1, request.MaxCount, "maxCount clamps to the matching pool (AS-IS Min(MaxCount, matching))");
}

async Task ProcessForAllMandatory()
{
    EngineContext context = await NewMatch();
    HeadlessEntityId[] top = Top(context, P1, 3);
    int handBefore = Count(context, P1, ChoiceZone.Hand);

    // Matches = top[0] and top[2] — both MUST go to hand, no player choice (AS-IS ProcessForAll).
    bool choiceOpened = await RevealAndSelect.RevealAndProcessAllAsync(
        context, P1, revealCount: 3, condition: id => id == top[0] || id == top[2],
        matchedTo: RevealDestination.Hand, remainingTo: RevealDestination.DeckBottom);

    AssertFalse(choiceOpened, "one remaining card -> no ordering prompt (AS-IS single-card no-prompt)");
    AssertEqual(handBefore + 2, Count(context, P1, ChoiceZone.Hand), "BOTH matching cards were processed (mandatory)");
    AssertTrue(InZone(context, P1, ChoiceZone.Hand, top[0]) && InZone(context, P1, ChoiceZone.Hand, top[2]), "the matches are in hand");
    HeadlessEntityId[] library = ((IZoneStateReader)context.ZoneMover).GetCards(P1, ChoiceZone.Library).ToArray();
    AssertEqual(top[1].Value, library[^1].Value, "the non-matching card went to the deck bottom");
}

async Task TopOrderReversed()
{
    EngineContext context = await NewMatch();
    HeadlessEntityId[] top = Top(context, P1, 3);

    RevealAndSelect.RequestChoice(context, P1, revealCount: 3, maxSelect: 1, RevealDestination.Hand, RevealDestination.DeckTop);
    await RevealAndSelect.ResolveChoice(context, ChoiceResult.Select(top[0]));
    AssertTrue(context.ChoiceController.Current.IsPending, "ordering choice opened (DeckTop)");
    await RevealAndSelect.ResolveChoice(context, ChoiceResult.Select(top[2], top[1]));

    HeadlessEntityId[] newTop = Top(context, P1, 2);
    AssertEqual(top[2].Value, newTop[0].Value, "the FIRST pick is topmost (AS-IS topCards.Reverse before insert)");
    AssertEqual(top[1].Value, newTop[1].Value, "the second pick sits beneath it");
}

async Task TopOrBottomTwoStep()
{
    EngineContext context = await NewMatch();
    HeadlessEntityId[] top = Top(context, P1, 3);

    RevealAndSelect.RequestChoice(context, P1, revealCount: 3, maxSelect: 1, RevealDestination.Hand, RevealDestination.DeckTopOrBottom);
    await RevealAndSelect.ResolveChoice(context, ChoiceResult.Select(top[0]));

    // Step 2: Top vs Bottom (AS-IS ReturnRevealedCardsToLibraryTopOrBottom).
    ChoiceRequest place = context.ChoiceController.PendingRequest!;
    AssertEqual(2, place.Candidates.Count, "top/bottom binary choice");
    await RevealAndSelect.ResolveChoice(context, ChoiceResult.Select(new HeadlessEntityId(RevealAndSelect.PlaceBottomCandidate)));

    // Step 3: ordering.
    AssertTrue(context.ChoiceController.Current.IsPending, "ordering choice opened after the placement pick");
    await RevealAndSelect.ResolveChoice(context, ChoiceResult.Select(top[1], top[2]));

    HeadlessEntityId[] library = ((IZoneStateReader)context.ZoneMover).GetCards(P1, ChoiceZone.Library).ToArray();
    AssertEqual(top[1].Value, library[^2].Value, "bottom placement in pick order");
    AssertEqual(top[2].Value, library[^1].Value, "bottom placement in pick order (2)");
}

async Task OpponentDeckReveal()
{
    EngineContext context = await NewMatch();
    HeadlessEntityId[] opponentTop = Top(context, P2, 2);
    int opponentHandBefore = Count(context, P2, ChoiceZone.Hand);
    int myHandBefore = Count(context, P1, ChoiceZone.Hand);

    // P1 reveals the top of P2's deck and trashes the selected card (AS-IS isOpponentDeck).
    RevealAndSelect.RequestChoice(context, P1, revealCount: 2, maxSelect: 1,
        RevealDestination.Trash, RevealDestination.DeckBottom, isOpponentDeck: true);
    ChoiceRequest request = context.ChoiceController.PendingRequest!;
    AssertTrue(request.Candidates.All(c => opponentTop.Contains(c.Id)), "the OPPONENT's top cards were revealed");
    AssertEqual(P1.Value, request.PlayerId.Value, "P1 (the effect controller) selects");

    await RevealAndSelect.ResolveChoice(context, ChoiceResult.Select(opponentTop[0]));
    AssertTrue(InZone(context, P2, ChoiceZone.Trash, opponentTop[0]), "the selected opponent card was trashed (owner's trash)");
    AssertEqual(myHandBefore, Count(context, P1, ChoiceZone.Hand), "nothing entered P1's zones");
    AssertEqual(opponentHandBefore, Count(context, P2, ChoiceZone.Hand), "nothing entered P2's hand");
}

// (P4) AS-IS RevealDeckTopCardsAndSelect with SelectCardConditionClass[] (RevealLibrary.cs:291-341):
// sequential passes over the SHARED revealed pool, chosen cards removed between passes, per-pass Mode.

async Task MultiConditionPasses()
{
    EngineContext context = await NewMatch();
    HeadlessEntityId[] top = Top(context, P1, 3);

    // Pass 0 (BT10-096 [0]): "the first revealed card", mandatory, to hand.
    // Pass 1 (BT10-096 [1]): "the second revealed card", optional, Custom (card script plays it).
    var passes = new[]
    {
        new RevealSelectPass(id => id == top[0], MaxCount: 1, RevealDestination.Hand, "Select the Xros Heart Digimon."),
        new RevealSelectPass(id => id == top[1], MaxCount: 1, RevealDestination.Custom, "Select 1 Taiki Kudo.", CanNoSelect: true),
    };
    AssertTrue(await RevealAndSelect.RequestMultiChoice(context, P1, revealCount: 3, passes, RevealDestination.DeckBottom),
        "pass 0 choice opened");
    AssertFalse(context.ChoiceController.PendingRequest!.CanSkip, "pass 0 is mandatory (canNoSelect:false)");
    await RevealAndSelect.ResolveChoice(context, ChoiceResult.Select(top[0]));

    // Pass 1: the pool no longer offers top[0].
    ChoiceRequest pass1 = context.ChoiceController.PendingRequest!;
    AssertTrue(pass1.CanSkip, "pass 1 is optional (canNoSelect:true)");
    AssertFalse(pass1.Candidates.Any(c => c.Id == top[0]), "the pass-0 pick left the shared pool");
    await RevealAndSelect.ResolveChoice(context, ChoiceResult.Select(top[1]));

    AssertTrue(InZone(context, P1, ChoiceZone.Hand, top[0]), "pass 0 pick went to hand");
    var state = GetFlow(context);
    var custom = state.TakeCustomSelections();
    AssertEqual(1, custom.Count, "the Custom pick is recorded for the card script");
    AssertEqual(top[1].Value, custom[0].Value, "the recorded pick is the pass-1 selection");
    AssertTrue(InZone(context, P1, ChoiceZone.Library, top[1]), "a Custom pick is NOT moved by the flow");

    // Remaining (top[2]) — single card, straight to the bottom without an ordering prompt.
    HeadlessEntityId[] library = ((IZoneStateReader)context.ZoneMover).GetCards(P1, ChoiceZone.Library).ToArray();
    AssertEqual(top[2].Value, library[^1].Value, "the untouched card went to the deck bottom");
}

async Task EmptyPassSkipped()
{
    EngineContext context = await NewMatch();
    HeadlessEntityId[] top = Top(context, P1, 2);

    var passes = new[]
    {
        new RevealSelectPass(_ => false, MaxCount: 1, RevealDestination.Hand, "never matches"),
        new RevealSelectPass(id => id == top[0], MaxCount: 1, RevealDestination.Hand, "matches the top"),
    };
    AssertTrue(await RevealAndSelect.RequestMultiChoice(context, P1, revealCount: 2, passes, RevealDestination.DeckBottom),
        "a choice opened (pass 0 skipped, pass 1 offered — AS-IS the loop continues)");
    AssertTrue(context.ChoiceController.PendingRequest!.Candidates.First(c => c.Id == top[0]).IsSelectable,
        "the offered pass is the matching one");
    await RevealAndSelect.ResolveChoice(context, ChoiceResult.Select(top[0]));
    AssertTrue(InZone(context, P1, ChoiceZone.Hand, top[0]), "pass 1 pick landed");
}

async Task MutualConditionsRelaxes()
{
    EngineContext context = await NewMatch();
    HeadlessEntityId[] top = Top(context, P1, 2);

    // Both passes match ONLY top[0]; pass 1 is mandatory by itself — the mutual rule relaxes it once
    // pass 0 consumed the only candidate (AS-IS RevealLibrary.cs:302-308).
    var passes = new[]
    {
        new RevealSelectPass(id => id == top[0], MaxCount: 1, RevealDestination.Hand, "first"),
        new RevealSelectPass(id => id == top[0] || id == top[1], MaxCount: 1, RevealDestination.Hand, "second", CanNoSelect: false),
    };
    await RevealAndSelect.RequestMultiChoice(context, P1, revealCount: 2, passes, RevealDestination.DeckBottom, mutualConditions: true);
    await RevealAndSelect.ResolveChoice(context, ChoiceResult.Select(top[0]));

    ChoiceRequest pass1 = context.ChoiceController.PendingRequest!;
    AssertTrue(pass1.CanSkip, "the mutual rule made the later pass optional (the chosen card also satisfied it and pass[0] is dry)");
}

RevealFlowState GetFlow(EngineContext context) =>
    context.TryGetService(out RevealFlowState? state) && state is not null ? state : throw new InvalidOperationException("no flow state");

// --- Harness -------------------------------------------------------------

async Task<EngineContext> NewMatch()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 73);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 20; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = new(context);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") },
        firstPlayerId: P1, shuffleDecks: false, shuffleDigitamaDecks: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 73, setup: setup));
    return context;
}

HeadlessEntityId[] Top(EngineContext context, HeadlessPlayerId player, int n) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(player, ChoiceZone.Library).Take(n).ToArray();

int Count(EngineContext context, HeadlessPlayerId player, ChoiceZone zone) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(player, zone).Count;

bool InZone(EngineContext context, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId card) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(player, zone).Contains(card);

static CardRecord Digimon(string id) =>
    new(new HeadlessEntityId(id), id, $"{id} Card", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon");

static PlayerDeckSetup Deck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 20).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

static void AssertTrue(bool value, string label) { if (!value) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertFalse(bool value, string label) { if (value) throw new InvalidOperationException($"{label}: expected false."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"{label}: expected '{expected}', actual '{actual}'.");
}
