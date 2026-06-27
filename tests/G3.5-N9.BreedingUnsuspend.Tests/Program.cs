using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// N-9: the original breeding-area unsuspend loop unsuspends UNCONDITIONALLY — it does not consult the
// CanUnsuspend gate (which governs field permanents). The port's turn-start Unsuspend now bypasses the
// gate for the breeding area while STILL honouring it on the battle area.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Breeding card unsuspends at turn start even with canUnsuspend=false", BreedingIgnoresGate),
    ("Battle-area card with canUnsuspend=false stays suspended (gate still applies)", FieldHonoursGate),
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

async Task BreedingIgnoresGate()
{
    DcgoMatch match = await SetupAtSetupPhase();
    HeadlessEntityId card = FirstHand(match, P1);
    await match.Context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, card, ChoiceZone.Hand, ChoiceZone.BreedingArea));
    SetMeta(match, card, isSuspended: true, canUnsuspend: false);

    await RunUnsuspendPhase(match);

    AssertFalse(ReadBool(match, card, "isSuspended"), "breeding card unsuspended despite canUnsuspend=false");
}

async Task FieldHonoursGate()
{
    DcgoMatch match = await SetupAtSetupPhase();
    HeadlessEntityId card = FirstHand(match, P1);
    await match.Context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, card, ChoiceZone.Hand, ChoiceZone.BattleArea));
    SetMeta(match, card, isSuspended: true, canUnsuspend: false);

    await RunUnsuspendPhase(match);

    AssertTrue(ReadBool(match, card, "isSuspended"), "battle-area card stays suspended (gate applies)");
}

// --- Drivers -------------------------------------------------------------

async Task RunUnsuspendPhase(DcgoMatch match)
{
    // Advance from Setup until the turn reaches the Unsuspend phase; that AdvancePhase step runs
    // UnsuspendForTurnPlayer (which is what N-9 exercises).
    for (var attempt = 0; attempt < 6 && match.GetObservation().Turn.Phase != HeadlessPhase.Unsuspend; attempt++)
    {
        LegalAction advance = match.GetLegalActions(P1).Single(a => a.ActionType == HeadlessActionTypes.AdvancePhase);
        await match.ApplyActionAsync(advance);
        await match.StepAsync();
    }

    AssertEqual(HeadlessPhase.Unsuspend, match.GetObservation().Turn.Phase, "reached Unsuspend phase");
}

async Task<DcgoMatch> SetupAtSetupPhase()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 9);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = new(context);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") }, firstPlayerId: P1,
        shuffleDecks: false, shuffleDigitamaDecks: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 9, setup: setup));
    AssertEqual(HeadlessPhase.Setup, match.GetObservation().Turn.Phase, "starts at Setup");
    return match;
}

HeadlessEntityId FirstHand(DcgoMatch match, HeadlessPlayerId player) =>
    ((IZoneStateReader)match.Context.ZoneMover).GetCards(player, ChoiceZone.Hand)
        .OrderBy(id => id.Value, StringComparer.Ordinal).First();

void SetMeta(DcgoMatch match, HeadlessEntityId cardId, bool isSuspended, bool canUnsuspend)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
        throw new InvalidOperationException($"Missing instance '{cardId}'.");
    Dictionary<string, object?> meta = new(record.Metadata, StringComparer.Ordinal)
    {
        ["isSuspended"] = isSuspended,
        ["canUnsuspend"] = canUnsuspend
    };
    match.Context.CardInstanceRepository.Upsert(record with { Metadata = meta });
}

bool ReadBool(DcgoMatch match, HeadlessEntityId cardId, string key)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
        throw new InvalidOperationException($"Missing instance '{cardId}'.");
    return record.Metadata.TryGetValue(key, out object? raw) && raw is bool b && b;
}

static CardRecord Digimon(string id) =>
    new(new HeadlessEntityId(id), id, $"{id} Card", new Dictionary<string, object?>(), CardType: "Digimon");

static PlayerDeckSetup Deck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertFalse(bool v, string label) { if (v) throw new InvalidOperationException($"{label}: expected false."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
