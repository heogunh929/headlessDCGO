using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G3.5-D6: the breeding step is a player DECISION, not auto-resolved. The turn player may hatch a
// digitama, move a breeding Digimon, or decline (AdvancePhase). The decision is surfaced as agent
// legal actions, accepted by the A1 legality boundary, and reachable through the factored action space.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Breeding offers hatch and decline (no auto-hatch)", BreedingOffersHatchAndDecline),
    ("Declining breeding leaves the digitama unhatched", DeclineLeavesDigitamaUnhatched),
    ("The hatch decision is reachable through the factored action space", HatchReachableViaFactored),
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

async Task BreedingOffersHatchAndDecline()
{
    DcgoMatch match = await BreedingPhaseAsync();

    string[] types = match.GetLegalActions(P1).Select(a => a.ActionType).Distinct().ToArray();
    AssertTrue(types.Contains(HeadlessActionTypes.HatchDigitama), "hatch offered");
    AssertTrue(types.Contains(HeadlessActionTypes.AdvancePhase), "decline (AdvancePhase) offered");
    AssertFalse(types.Contains(HeadlessActionTypes.MoveBreedingToBattle), "no move offered (breeding area empty)");
    // Breeding did NOT auto-hatch on entry.
    AssertEqual(0, Count(match, ChoiceZone.BreedingArea), "breeding area still empty (no auto-hatch)");
}

async Task DeclineLeavesDigitamaUnhatched()
{
    DcgoMatch match = await BreedingPhaseAsync();
    int digitamaBefore = Count(match, ChoiceZone.DigitamaLibrary);

    StepResult declined = await Apply(match, HeadlessActionFactory.AdvancePhase(P1));

    AssertEqual(HeadlessPhase.Main, declined.Observation.Turn.Phase, "declining advances to Main");
    AssertEqual(digitamaBefore, Count(match, ChoiceZone.DigitamaLibrary), "digitama unchanged when declined");
    AssertEqual(0, Count(match, ChoiceZone.BreedingArea), "breeding area empty when declined");
}

async Task HatchReachableViaFactored()
{
    DcgoMatch match = await BreedingPhaseAsync();

    FactoredActionMask mask = match.EncodeFactoredActionMask();
    FactoredAction? hatch = mask.Actions.FirstOrDefault(a => a.Action.ActionType == HeadlessActionTypes.HatchDigitama);
    AssertTrue(hatch is not null, "hatch is a placed factored action (RL-reachable)");
    AssertTrue(mask.ToMaskVector()[hatch!.Index] == 1d, "hatch's factored index is legal in the mask");

    await Apply(match, hatch.Action);
    AssertEqual(1, Count(match, ChoiceZone.BreedingArea), "hatch via factored action produced a breeding Digimon");
}

// --- Harness -------------------------------------------------------------

int Count(DcgoMatch match, ChoiceZone zone) =>
    ((IZoneStateReader)match.Context.ZoneMover).GetCards(P1, zone).Count;

static async Task<StepResult> Apply(DcgoMatch match, LegalAction action)
{
    await match.ApplyActionAsync(action);
    return await match.StepAsync();
}

async Task<DcgoMatch> BreedingPhaseAsync()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 74);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = DcgoMatch.CreateValidated(context, new EngineTrace());
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") }, firstPlayerId: P1);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 74, setup: setup));

    for (var attempt = 0; attempt < 8 && match.GetObservation().Turn.Phase != HeadlessPhase.Breeding; attempt++)
    {
        await Apply(match, HeadlessActionFactory.AdvancePhase(P1));
    }

    if (match.GetObservation().Turn.Phase != HeadlessPhase.Breeding)
    {
        throw new InvalidOperationException("Failed to reach the breeding phase.");
    }

    return match;
}

static CardRecord Digimon(string id) =>
    new(new HeadlessEntityId(id), id, $"{id} Card", new Dictionary<string, object?>(), CardType: "Digimon");

static PlayerDeckSetup Deck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

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

static void AssertFalse(bool value, string label)
{
    if (value) throw new InvalidOperationException($"{label}: expected false.");
}
