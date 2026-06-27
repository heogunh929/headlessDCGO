using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// N-5: the opening-hand mulligan is an interactive per-player decision (first player first), made BEFORE
// security is dealt — mirroring the original. It is surfaced as a ChoiceType.Mulligan choice (select the
// redraw candidate = mulligan, skip = keep). When EnableMulligan is off (default), setup advances
// straight through with no pending decision (existing behaviour preserved).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Default setup has no mulligan decision and deals security immediately", DefaultNoMulligan),
    ("Enabled: each player decides in order before security is dealt", EnabledSequenceKeepKeep),
    ("Enabled: the decider has exactly keep and redraw legal actions; others have none", LegalActionsDuringMulligan),
    ("Enabled: a redraw changes the decider's opening hand", RedrawChangesHand),
    ("Enabled: keeping leaves the opening hand unchanged", KeepLeavesHandUnchanged),
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

async Task DefaultNoMulligan()
{
    DcgoMatch match = await NewMatch(enableMulligan: false, shuffle: false);

    AssertFalse(match.HasPendingChoice(), "no pending choice when mulligan disabled");
    AssertEqual(5, Count(match, P1, ChoiceZone.Security), "P1 security dealt at setup");
    AssertEqual(5, Count(match, P2, ChoiceZone.Security), "P2 security dealt at setup");
}

async Task EnabledSequenceKeepKeep()
{
    DcgoMatch match = await NewMatch(enableMulligan: true, shuffle: false);

    AssertTrue(match.HasPendingChoice(), "a mulligan decision is pending after setup");
    AssertEqual(ChoiceType.Mulligan, match.Context.ChoiceController.PendingRequest!.Type, "pending choice is a mulligan");
    HeadlessPlayerId first = match.Context.ChoiceController.PendingRequest!.PlayerId;
    HeadlessPlayerId second = first == P1 ? P2 : P1;

    // Security is deferred until both players decide.
    AssertEqual(0, Count(match, P1, ChoiceZone.Security), "security deferred before mulligan");
    AssertEqual(0, Count(match, P2, ChoiceZone.Security), "security deferred before mulligan");

    await Decide(match, first, redraw: false);
    AssertTrue(match.HasPendingChoice(), "second player's decision is pending");
    AssertEqual(second, match.Context.ChoiceController.PendingRequest!.PlayerId, "second decider in order");

    await Decide(match, second, redraw: false);
    AssertFalse(match.HasPendingChoice(), "no pending choice after both decide");

    AssertEqual(5, Count(match, P1, ChoiceZone.Security), "P1 security dealt after mulligan");
    AssertEqual(5, Count(match, P2, ChoiceZone.Security), "P2 security dealt after mulligan");
    AssertEqual(5, Count(match, first, ChoiceZone.Hand), "first hand size preserved");
    AssertEqual(5, Count(match, second, ChoiceZone.Hand), "second hand size preserved");
}

async Task LegalActionsDuringMulligan()
{
    DcgoMatch match = await NewMatch(enableMulligan: true, shuffle: false);
    HeadlessPlayerId first = match.Context.ChoiceController.PendingRequest!.PlayerId;
    HeadlessPlayerId second = first == P1 ? P2 : P1;

    LegalAction[] firstActions = match.GetLegalActions(first).ToArray();
    AssertEqual(2, firstActions.Length, "decider has keep + redraw");
    AssertTrue(firstActions.All(a => a.ActionType == HeadlessActionTypes.ResolveChoice), "both are ResolveChoice");
    AssertTrue(firstActions.Any(IsSkip), "a keep (skip) action is offered");
    AssertTrue(firstActions.Any(a => !IsSkip(a)), "a redraw (select) action is offered");

    AssertEqual(0, match.GetLegalActions(second).Count, "the non-deciding player has no legal action");
}

async Task RedrawChangesHand()
{
    DcgoMatch match = await NewMatch(enableMulligan: true, shuffle: false);
    HeadlessPlayerId first = match.Context.ChoiceController.PendingRequest!.PlayerId;

    string before = HandSignature(match, first);
    await Decide(match, first, redraw: true);
    string after = HandSignature(match, first);

    AssertNotEqual(before, after, "redraw produces a different opening hand");
    AssertEqual(5, Count(match, first, ChoiceZone.Hand), "redrawn hand still has 5 cards");
}

async Task KeepLeavesHandUnchanged()
{
    DcgoMatch match = await NewMatch(enableMulligan: true, shuffle: false);
    HeadlessPlayerId first = match.Context.ChoiceController.PendingRequest!.PlayerId;

    string before = HandSignature(match, first);
    await Decide(match, first, redraw: false);
    string after = HandSignature(match, first);

    AssertEqual(before, after, "keeping leaves the opening hand unchanged");
}

// --- Drivers -------------------------------------------------------------

async Task Decide(DcgoMatch match, HeadlessPlayerId player, bool redraw)
{
    LegalAction action = match.GetLegalActions(player)
        .Single(a => a.ActionType == HeadlessActionTypes.ResolveChoice && IsSkip(a) == !redraw);
    await match.ApplyActionAsync(action);
    await match.StepAsync();
}

static bool IsSkip(LegalAction action) => action.Id.Value.EndsWith(":skip", StringComparison.Ordinal);

// --- Setup ---------------------------------------------------------------

async Task<DcgoMatch> NewMatch(bool enableMulligan, bool shuffle)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 88);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 30; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = new(context);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") },
        firstPlayerId: P1,
        shuffleDecks: shuffle,
        shuffleDigitamaDecks: shuffle,
        enableMulligan: enableMulligan);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 88, setup: setup));
    return match;
}

int Count(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone) =>
    ((IZoneStateReader)match.Context.ZoneMover).GetCards(player, zone).Count;

string HandSignature(DcgoMatch match, HeadlessPlayerId player) =>
    string.Join(",", ((IZoneStateReader)match.Context.ZoneMover).GetCards(player, ChoiceZone.Hand).Select(id => id.Value));

static CardRecord Digimon(string id) =>
    new(new HeadlessEntityId(id), id, $"{id} Card", new Dictionary<string, object?>(), CardType: "Digimon");

static PlayerDeckSetup Deck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 30).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 5).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

// --- Assertions ----------------------------------------------------------

static void AssertTrue(bool value, string label)
{
    if (!value) throw new InvalidOperationException($"{label}: expected true.");
}

static void AssertFalse(bool value, string label)
{
    if (value) throw new InvalidOperationException($"{label}: expected false.");
}

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', actual '{actual}'.");
    }
}

static void AssertNotEqual<T>(T notExpected, T actual, string label)
{
    if (EqualityComparer<T>.Default.Equals(notExpected, actual))
    {
        throw new InvalidOperationException($"{label}: expected a value different from '{actual}'.");
    }
}
