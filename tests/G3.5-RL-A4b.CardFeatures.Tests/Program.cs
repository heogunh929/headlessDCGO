using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

// G3.5-RL-A4b: per-card features in the observation (fixes the "too little" half of P0-4).
// Visible cards expose typed stats (DP computed via B1 DpCalculator, type, level, cost, suspend,
// stack depth); hidden zones expose nothing; the encoder emits fixed per-card feature slots.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
HeadlessEntityId FieldCard = new("p1:main:001:P1-M01");

var tests = new (string Name, Func<Task> Body)[]
{
    ("Visible field card exposes typed features with B1-computed DP", VisibleCardExposesComputedDp),
    ("Suspend and stack depth are reflected on the card observation", SuspendAndStackReflected),
    ("Opponent hidden-zone cards expose no per-card features", HiddenZoneHasNoCardFeatures),
    ("Encoder emits fixed per-card feature slots", EncoderEmitsCardFeatureSlots),
    ("Card features can be disabled via options", CardFeaturesCanBeDisabled),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try
    {
        await test.Body();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        failures.Add($"{test.Name}: {ex.GetType().Name}: {ex.Message}");
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}");
    }
}

if (failures.Count > 0)
{
    Console.Error.WriteLine($"\n{failures.Count} test(s) failed.");
    Environment.Exit(1);
}

Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

async Task VisibleCardExposesComputedDp()
{
    DcgoMatch match = await CreateFieldMatchAsync();
    SetMetadata(match, FieldCard, new Dictionary<string, object?>
    {
        ["dp"] = 3000,
        ["dpModifiers"] = new[] { DpModifier.Relative(2000, source: "test") }
    });

    CardObservation card = FieldCardObservation(match.GetObservation(P1));
    AssertEqual(5000, card.Dp, "DP = base 3000 + modifier 2000 (DpCalculator)");
    AssertEqual("Digimon", card.CardType, "card type");
    AssertEqual("P1-M01", card.CardNumber, "card number");
    AssertEqual(3, card.Level, "level from definition metadata");
}

async Task SuspendAndStackReflected()
{
    DcgoMatch match = await CreateFieldMatchAsync();
    SetMetadata(match, FieldCard, new Dictionary<string, object?>
    {
        ["dp"] = 4000,
        ["isSuspended"] = true,
        ["sourceIds"] = new[] { "s1", "s2" }
    });

    CardObservation card = FieldCardObservation(match.GetObservation(P1));
    AssertTrue(card.IsSuspended, "suspended reflected");
    AssertEqual(2, card.StackDepth, "stack depth from sourceIds");
    AssertEqual(4000, card.Dp, "dp with no modifiers equals base");
}

async Task HiddenZoneHasNoCardFeatures()
{
    DcgoMatch match = await CreateFieldMatchAsync();

    ObservationSnapshot filtered = match.GetObservation(P1);
    ZoneObservation opponentHand = Zone(filtered, P2, ChoiceZone.Hand);

    AssertTrue(opponentHand.Count > 0, "opponent hand has cards");
    AssertEqual(0, opponentHand.Cards.Count, "opponent hidden-zone exposes no card features");
}

async Task EncoderEmitsCardFeatureSlots()
{
    DcgoMatch match = await CreateFieldMatchAsync();
    SetMetadata(match, FieldCard, new Dictionary<string, object?> { ["dp"] = 7000 });

    var options = ObservationEncodingOptions.Default with { MaxCardsPerZone = 4 };
    EncodedObservation encoded = new ObservationEncoder(options).Encode(match.GetObservation(P1));

    bool hasDpSlot = encoded.Features.Any(f => f.Name.EndsWith(".card.0.dp", StringComparison.Ordinal));
    AssertTrue(hasDpSlot, "encoder emits a card.0.dp feature");

    // 10 features per card slot * 4 slots * 1 zone * 2 players = 80 card features.
    int cardFeatureCount = encoded.Features.Count(f => f.Name.Contains(".card.", StringComparison.Ordinal));
    AssertEqual(10 * 4 * 2, cardFeatureCount, "fixed per-card feature count");

    ObservationFeature dpFeature = encoded.Features.First(f => f.Name == "player.1.zone.BattleArea.card.0.dp");
    AssertEqual(7000d, dpFeature.Value, "encoded dp value");
}

async Task CardFeaturesCanBeDisabled()
{
    DcgoMatch match = await CreateFieldMatchAsync();
    var options = ObservationEncodingOptions.Default with { IncludeCardFeatures = false };
    EncodedObservation encoded = new ObservationEncoder(options).Encode(match.GetObservation(P1));

    AssertFalse(
        encoded.Features.Any(f => f.Name.Contains(".card.", StringComparison.Ordinal)),
        "no card features when disabled");
}

// --- Harness -------------------------------------------------------------

async Task<DcgoMatch> CreateFieldMatchAsync()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 73);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(CreateDigimon($"P1-M{index:D2}"));
        cards.Upsert(CreateDigimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = new(context);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { BuildDeck(P1, "P1"), BuildDeck(P2, "P2") },
        firstPlayerId: P1);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 73, setup: setup));
    await AdvanceToMainAsync(match);
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, FieldCard, ChoiceZone.Hand, ChoiceZone.BattleArea));
    return match;
}

static CardRecord CreateDigimon(string id)
{
    return new CardRecord(
        new HeadlessEntityId(id),
        id,
        $"{id} Card",
        new Dictionary<string, object?> { ["level"] = 3 },
        CardType: "Digimon",
        PlayCost: 5,
        EvolutionCost: 2);
}

static PlayerDeckSetup BuildDeck(HeadlessPlayerId playerId, string prefix)
{
    return new PlayerDeckSetup(
        playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());
}

async Task AdvanceToMainAsync(DcgoMatch match)
{
    for (var attempt = 0; attempt < 8 && match.GetObservation().Turn.Phase != HeadlessPhase.Main; attempt++)
    {
        LegalAction advance = match.GetLegalActions(P1).Single(a => a.ActionType == HeadlessActionTypes.AdvancePhase);
        await match.ApplyActionAsync(advance);
        await match.StepAsync();
    }

    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "advance to main");
}

void SetMetadata(DcgoMatch match, HeadlessEntityId cardId, IReadOnlyDictionary<string, object?> values)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
    {
        throw new InvalidOperationException($"Missing card instance '{cardId}'.");
    }

    Dictionary<string, object?> metadata = new(record.Metadata, StringComparer.Ordinal);
    foreach (KeyValuePair<string, object?> pair in values)
    {
        metadata[pair.Key] = pair.Value;
    }

    match.Context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
}

CardObservation FieldCardObservation(ObservationSnapshot snapshot)
{
    ZoneObservation field = Zone(snapshot, P1, ChoiceZone.BattleArea);
    CardObservation? card = field.Cards.FirstOrDefault(c => c.InstanceId == FieldCard);
    return card ?? throw new InvalidOperationException("field card not in observation");
}

static ZoneObservation Zone(ObservationSnapshot snapshot, HeadlessPlayerId player, ChoiceZone zone)
{
    PlayerObservation p = snapshot.Players.FirstOrDefault(x => x.PlayerId == player)
        ?? throw new InvalidOperationException($"player {player.Value} not in observation");
    return p.FindZone(zone) ?? throw new InvalidOperationException($"zone {zone} missing");
}

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
    }
}

static void AssertTrue(bool value, string label)
{
    if (!value)
    {
        throw new InvalidOperationException($"{label}: expected true.");
    }
}

static void AssertFalse(bool value, string label)
{
    if (value)
    {
        throw new InvalidOperationException($"{label}: expected false.");
    }
}
