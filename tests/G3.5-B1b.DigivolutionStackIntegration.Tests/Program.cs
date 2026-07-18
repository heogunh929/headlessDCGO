using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

// G3.5-B1b: the typed DigivolutionStack is now built from the live `sourceIds` metadata storage via
// DigivolutionStackReader, and DigivolveAction uses it (stamps typed stack depth / base DP). This
// proves the B1 type is integrated into the real digivolution path rather than dead.

HeadlessPlayerId Player = new(1);
HeadlessEntityId EvolveCardId = new("p1:main:001:P1-M01");
HeadlessEntityId TargetCardId = new("p1:main:002:P1-M02");

var tests = new (string Name, Func<Task> Body)[]
{
    ("Reader projects sourceIds into an ordered DigiEgg..Top stack", () => Pure(ReaderBuildsOrderedStack)),
    ("Reader base DP comes from the top card, depth counts the whole stack", () => Pure(ReaderTopCardSuppliesBaseDp)),
    ("A card with no sources reads as a single Top card", () => Pure(ReaderNoSourcesSingleTop)),
    ("An unknown / empty top card reads as the empty stack", () => Pure(ReaderUnknownTopIsEmpty)),
    ("Digivolve action builds the typed stack and stamps its depth", DigivolveStampsTypedStack),
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

static Task Pure(Action body) { body(); return Task.CompletedTask; }

// --- Reader unit tests ---------------------------------------------------

void ReaderBuildsOrderedStack()
{
    var (instances, cards) = NewRepos();
    Define(cards, "EGG", dp: 0, level: 2);
    Define(cards, "RK", dp: 2000, level: 3);
    Define(cards, "CH", dp: 5000, level: 4);

    Instance(instances, "i-egg", "EGG");
    Instance(instances, "i-rk", "RK");
    // sourceIds is stored newest-under-card first: the rookie (just digivolved from), then the egg.
    Instance(instances, "i-ch", "CH", sourceIds: new[] { "i-rk", "i-egg" });

    DigivolutionStack stack = DigivolutionStackReader.Read(instances, cards, new HeadlessEntityId("i-ch"));

    AssertEqual(3, stack.Depth, "stack depth");
    AssertEqual(2, stack.UnderCards.Count, "under-card count");
    AssertEqual(StackRole.DigiEgg, stack.Cards[0].Role, "bottom is the DigiEgg");
    AssertEqual("EGG", stack.Cards[0].CardNumber, "bottom card number");
    AssertEqual(StackRole.Digivolution, stack.Cards[1].Role, "middle is a digivolution");
    AssertEqual("RK", stack.Cards[1].CardNumber, "middle card number");
    AssertEqual(StackRole.Top, stack.TopCard!.Role, "topmost is Top");
    AssertEqual("CH", stack.TopCard!.CardNumber, "top card number");
}

void ReaderTopCardSuppliesBaseDp()
{
    var (instances, cards) = NewRepos();
    Define(cards, "EGG", dp: 0, level: 2);
    Define(cards, "CH", dp: 5000, level: 4);
    Instance(instances, "i-egg", "EGG");
    Instance(instances, "i-ch", "CH", sourceIds: new[] { "i-egg" });

    DigivolutionStack stack = DigivolutionStackReader.Read(instances, cards, new HeadlessEntityId("i-ch"));

    AssertEqual(5000, stack.BaseDp, "base DP is the top card's DP");
    AssertEqual(4, stack.TopCard!.Level, "top card level");
}

void ReaderNoSourcesSingleTop()
{
    var (instances, cards) = NewRepos();
    Define(cards, "CH", dp: 5000, level: 4);
    Instance(instances, "i-solo", "CH");

    DigivolutionStack stack = DigivolutionStackReader.Read(instances, cards, new HeadlessEntityId("i-solo"));

    AssertEqual(1, stack.Depth, "single card depth");
    AssertEqual(0, stack.UnderCards.Count, "no under cards");
    AssertEqual(StackRole.Top, stack.TopCard!.Role, "lone card is the Top");
}

void ReaderUnknownTopIsEmpty()
{
    var (instances, cards) = NewRepos();
    DigivolutionStack stack = DigivolutionStackReader.Read(instances, cards, new HeadlessEntityId("missing"));
    AssertTrue(stack.IsEmpty, "unknown top yields an empty stack");
}

// --- End-to-end through DigivolveAction ----------------------------------

async Task DigivolveStampsTypedStack()
{
    // The match is returned already at P1's main wait with the target staged onto the battle area
    // (pump-owned hand deal; the evolve card P1-M01 stays in hand — 4b B5-c2 / G2E-002 staging precedent).
    DcgoMatch match = await CreateConfiguredMatchAsync();

    var action = HeadlessActionFactory.Digivolve(Player, EvolveCardId, TargetCardId, memoryCost: 2);
    ActionProcessResult result = await new DigivolveAction().ProcessAsync(action, match.Context);

    AssertTrue(result.IsSuccess, "digivolve succeeded");
    AssertEqual(2, ReadInt(result.Metadata, "stackDepth"), "result stamps typed stack depth");

    // The reader, run over the post-digivolve state, sees the target as the lone under-card.
    DigivolutionStack stack = DigivolutionStackReader.Read(
        match.Context.CardInstanceRepository, match.Context.CardRepository, EvolveCardId);

    AssertEqual(2, stack.Depth, "live stack depth after digivolve");
    AssertEqual(1, stack.UnderCards.Count, "one under-card (the card digivolved from)");
    AssertEqual(TargetCardId, stack.UnderCards[0].InstanceId, "under-card is the target");
    AssertEqual(StackRole.DigiEgg, stack.UnderCards[0].Role, "lone under-card is the DigiEgg");
    AssertEqual(EvolveCardId, stack.TopCard!.InstanceId, "top card is the evolved card");
}

// --- Repository builders -------------------------------------------------

static (InMemoryCardInstanceRepository Instances, CardDatabase Cards) NewRepos() =>
    (new InMemoryCardInstanceRepository(), new CardDatabase());

static void Define(CardDatabase cards, string number, int dp, int level) =>
    cards.Upsert(new CardRecord(
        new HeadlessEntityId(number),
        number,
        $"{number} Card",
        new Dictionary<string, object?> { ["dp"] = dp, ["level"] = level },
        CardType: "Digimon"));

void Instance(InMemoryCardInstanceRepository instances, string id, string definition, string[]? sourceIds = null)
{
    var metadata = new Dictionary<string, object?>(StringComparer.Ordinal);
    if (sourceIds is not null)
    {
        metadata["sourceIds"] = sourceIds;
    }

    instances.Upsert(new CardInstanceRecord(
        new HeadlessEntityId(id), new HeadlessEntityId(definition), Player, Metadata: metadata));
}

// --- Match harness (trimmed from G2E-002) --------------------------------

async Task<DcgoMatch> CreateConfiguredMatchAsync()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 41);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    cards.Upsert(new CardRecord(
        new HeadlessEntityId("P1-M01"), "P1-M01", "Evolving Digimon", new Dictionary<string, object?>(),
        CardType: "Digimon", EvolutionCost: 2, EvolutionCondition: "definition:P1-M02"));
    cards.Upsert(new CardRecord(
        new HeadlessEntityId("P1-M02"), "P1-M02", "Base Digimon", new Dictionary<string, object?>(),
        CardType: "Digimon", PlayCost: 3));
    for (int index = 3; index <= 12; index++)
    {
        cards.Upsert(new CardRecord(new HeadlessEntityId($"P1-M{index:D2}"), $"P1-M{index:D2}", $"P1 filler {index}",
            new Dictionary<string, object?>(), CardType: "Digimon"));
    }
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(new CardRecord(new HeadlessEntityId($"P2-M{index:D2}"), $"P2-M{index:D2}", $"P2 filler {index}",
            new Dictionary<string, object?>(), CardType: "Digimon"));
    }

    DcgoMatch match = DcgoMatch.CreatePumpDriven(context, new EngineTrace());
    HeadlessPlayerId[] players = { new(1), new(2) };
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { BuildDeck(new HeadlessPlayerId(1), "P1"), BuildDeck(new HeadlessPlayerId(2), "P2") },
        firstPlayerId: new HeadlessPlayerId(1), shuffleDecks: false, shuffleDigitamaDecks: false);

    await match.InitializeAsync(MatchConfig.Create(
        players, randomSeed: 41, initialMemory: 0, minimumMemory: -5, maximumMemory: 10, setup: setup));

    // Reach P1's main wait BEFORE staging: the pump deals the opening hand during its auto-flow, so the
    // target must be moved to the battle area AFTER the deal (staging before it would be lost to the pump
    // auto-draw — RD-R3-02 silent skip). The evolve card P1-M01 stays in hand for the digivolve.
    await AdvanceToMainAsync(match, new HeadlessPlayerId(1));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(new HeadlessPlayerId(1), TargetCardId, ChoiceZone.Hand, ChoiceZone.BattleArea));
    return match;
}

static PlayerDeckSetup BuildDeck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

// Drive the pump's natural Active->Draw->Breeding->Main auto-flow to the player's main wait; the OLD
// AdvancePhase step currency is retired (G2E-002/F62 precedent). Breeding/Mulligan decisions are declined.
static async Task AdvanceToMainAsync(DcgoMatch match, HeadlessPlayerId playerId)
{
    await StepOnceDriveAsync(match);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, playerId));

    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "advance to main");
}

static bool AtMainWaitOf(DcgoMatch match, HeadlessPlayerId player) =>
    match.Context.TurnController.Current.Phase == HeadlessPhase.Main
    && match.Context.TurnController.Current.TurnPlayerId == player
    && !match.HasPendingChoice() && !match.IsTerminal();

static async Task DriveUntilAsync(DcgoMatch match, Func<DcgoMatch, bool> condition)
{
    for (int i = 0; i < 96 && !condition(match); i++)
    {
        if (match.HasPendingChoice())
        {
            bool decline = match.Context.ChoiceController.PendingRequest!.Type is ChoiceType.BreedingDecision or ChoiceType.Mulligan;
            await ResolvePendingDriveAsync(match, skip: decline);
        }
        else await StepOnceDriveAsync(match);
    }
    if (!condition(match))
    {
        HeadlessTurnState t = match.Context.TurnController.Current;
        throw new InvalidOperationException(
            $"pump drive did not reach the expected state - phase:{t.Phase} turn:{t.TurnNumber} player:{t.TurnPlayerId} " +
            $"choice:{match.Context.ChoiceController.PendingRequest?.Type.ToString() ?? "<none>"} pending:{match.HasPendingChoice()} terminal:{match.IsTerminal()}");
    }
}

static async Task ResolvePendingDriveAsync(DcgoMatch match, bool skip)
{
    HeadlessPlayerId chooser = match.Context.ChoiceController.PendingRequest!.PlayerId;
    LegalAction? action;
    using (AmbientMatchContext.Enter(match.Context))
    {
        action = match.GetLegalActions(chooser).FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice
                && a.Id.Value.EndsWith(":skip", StringComparison.Ordinal) == skip)
            ?? match.GetLegalActions(chooser).FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice);
    }
    if (action is null) throw new InvalidOperationException("no ResolveChoice lane for the pending request");
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.ApplyActionAsync(action);
    await match.StepAsync();
    await match.StepAsync();
}

static async Task StepOnceDriveAsync(DcgoMatch match)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.StepAsync();
}

static int ReadInt(IReadOnlyDictionary<string, object?> metadata, string key)
{
    if (!metadata.TryGetValue(key, out object? raw) || raw is null)
    {
        throw new InvalidOperationException($"Missing int metadata '{key}'.");
    }

    return raw switch
    {
        int value => value,
        long value => (int)value,
        string value when int.TryParse(value, out int parsed) => parsed,
        _ => throw new InvalidOperationException($"Invalid int metadata '{key}'.")
    };
}

// --- Assertions ----------------------------------------------------------

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
    }
}

static void AssertTrue(bool value, string label)
{
    if (!value) throw new InvalidOperationException($"{label}: expected true.");
}
