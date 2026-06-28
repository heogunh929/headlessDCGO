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
    AssertTrue(InZone(context, P1, ChoiceZone.Library, top[1]), "unselected revealed card stays in the library");
    AssertTrue(InZone(context, P1, ChoiceZone.Library, top[2]), "unselected revealed card stays in the library");
    AssertFalse(InZone(context, P1, ChoiceZone.Hand, top[1]), "unselected card is not in hand");
    // The remaining revealed cards were sent to the bottom: they are no longer the library top.
    HeadlessEntityId[] newTop = Top(context, P1, 2);
    AssertFalse(newTop.Contains(top[1]) && newTop.Contains(top[2]), "remaining revealed cards moved off the top (to the bottom)");
}

async Task SkipSendsAllToRemaining()
{
    EngineContext context = await NewMatch();
    HeadlessEntityId[] top = Top(context, P1, 3);
    int handBefore = Count(context, P1, ChoiceZone.Hand);

    RevealAndSelect.RequestChoice(context, P1, revealCount: 3, maxSelect: 1, RevealDestination.Hand, RevealDestination.DeckBottom);
    await RevealAndSelect.ResolveChoice(context, ChoiceResult.Skip());

    AssertEqual(handBefore, Count(context, P1, ChoiceZone.Hand), "skipping adds nothing to hand");
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
