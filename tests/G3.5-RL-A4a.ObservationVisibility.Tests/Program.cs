using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G3.5-RL-A4a: Perspective-filtered observation (fixes the "too much" half of P0-4).
// Hidden zones (Library/Hand/Security/DigitamaLibrary) of OTHER players are exposed as count-only
// to a viewer; the viewer's own cards and all public zones remain fully visible. A null perspective
// preserves the full debug view for back-compat.

var hiddenZones = new[] { ChoiceZone.Library, ChoiceZone.Hand, ChoiceZone.Security, ChoiceZone.DigitamaLibrary };

var tests = new (string Name, Func<Task> Body)[]
{
    ("Opponent hidden zones are count-only to a viewer", OpponentHiddenZonesAreCountOnly),
    ("Viewer's own hidden zones stay fully visible", OwnHiddenZonesStayVisible),
    ("Public zones stay visible for all players", PublicZonesStayVisible),
    ("Counts are preserved exactly under filtering", CountsPreservedUnderFiltering),
    ("Full (null-perspective) view exposes opponent card ids", FullViewExposesEverything),
    ("RL environment observation pipeline filters by perspective", RlEnvironmentFiltersByPerspective),
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
    Console.Error.WriteLine();
    Console.Error.WriteLine($"{failures.Count} test(s) failed.");
    Environment.Exit(1);
}

Console.WriteLine();
Console.WriteLine($"{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

async Task OpponentHiddenZonesAreCountOnly()
{
    DcgoMatch match = await CreateMatchAsync();
    HeadlessPlayerId viewer = new(1);
    HeadlessPlayerId opponent = new(2);

    ObservationSnapshot filtered = match.GetObservation(viewer);

    // At least one hidden zone must actually hold cards, or the test proves nothing.
    int totalOpponentHidden = hiddenZones.Sum(z => ZoneCount(filtered, opponent, z));
    AssertTrue(totalOpponentHidden > 0, "opponent has cards in hidden zones after setup");

    foreach (ChoiceZone zone in hiddenZones)
    {
        ZoneObservation z = Zone(filtered, opponent, zone);
        AssertEqual(0, z.CardIds.Count, $"opponent {zone} card ids withheld");
    }
}

async Task OwnHiddenZonesStayVisible()
{
    DcgoMatch match = await CreateMatchAsync();
    HeadlessPlayerId viewer = new(1);

    ObservationSnapshot filtered = match.GetObservation(viewer);

    foreach (ChoiceZone zone in hiddenZones)
    {
        ZoneObservation z = Zone(filtered, viewer, zone);
        AssertEqual(z.Count, z.CardIds.Count, $"viewer's own {zone} ids fully visible");
    }
}

async Task PublicZonesStayVisible()
{
    DcgoMatch match = await CreateMatchAsync();
    HeadlessPlayerId viewer = new(1);
    HeadlessPlayerId opponent = new(2);

    ObservationSnapshot filtered = match.GetObservation(viewer);

    foreach (PlayerObservation player in filtered.Players)
    {
        foreach (ZoneObservation z in player.Zones)
        {
            bool isHidden = hiddenZones.Contains(z.Zone);
            if (isHidden && player.PlayerId == opponent)
            {
                continue; // covered by the hidden-zone test
            }

            AssertEqual(z.Count, z.CardIds.Count, $"visible zone {player.PlayerId.Value}.{z.Zone} exposes ids");
        }
    }
}

async Task CountsPreservedUnderFiltering()
{
    DcgoMatch match = await CreateMatchAsync();
    HeadlessPlayerId viewer = new(1);

    ObservationSnapshot full = match.GetObservation();
    ObservationSnapshot filtered = match.GetObservation(viewer);

    foreach (PlayerObservation player in full.Players)
    {
        foreach (ZoneObservation fullZone in player.Zones)
        {
            int filteredCount = ZoneCount(filtered, player.PlayerId, fullZone.Zone);
            AssertEqual(fullZone.Count, filteredCount, $"count preserved {player.PlayerId.Value}.{fullZone.Zone}");
        }
    }
}

async Task FullViewExposesEverything()
{
    DcgoMatch match = await CreateMatchAsync();
    HeadlessPlayerId opponent = new(2);

    ObservationSnapshot full = match.GetObservation(); // no perspective -> debug/god's-eye

    int exposed = hiddenZones.Sum(z => Zone(full, opponent, z).CardIds.Count);
    AssertTrue(exposed > 0, "full view exposes opponent hidden-zone card ids (back-compat)");
}

async Task RlEnvironmentFiltersByPerspective()
{
    // Fixed perspective = player 1; player 2's hidden cards must be withheld in the env pipeline.
    var env = new HeadlessRlEnvironment(
        options: HeadlessRlEnvironmentOptions.Default with { PerspectivePlayerId = new HeadlessPlayerId(1) });
    await env.InitializeAsync(BuildMatchConfig());

    ObservationSnapshot filtered = env.Match.GetObservation(new HeadlessPlayerId(1));
    HeadlessPlayerId opponent = new(2);

    foreach (ChoiceZone zone in hiddenZones)
    {
        AssertEqual(0, Zone(filtered, opponent, zone).CardIds.Count, $"env hides opponent {zone}");
    }

    // The encoded step result still encodes (counts), proving the pipeline runs end-to-end.
    RlStepResult observed = env.Observe();
    AssertTrue(observed.Observation.Features.Count > 0, "env produces an encoded observation");
}

// --- Helpers -------------------------------------------------------------

static ZoneObservation Zone(ObservationSnapshot snapshot, HeadlessPlayerId player, ChoiceZone zone)
{
    PlayerObservation p = snapshot.Players.FirstOrDefault(x => x.PlayerId == player)
        ?? throw new InvalidOperationException($"player {player.Value} not in observation");
    return p.FindZone(zone) ?? throw new InvalidOperationException($"zone {zone} not in observation for {player.Value}");
}

static int ZoneCount(ObservationSnapshot snapshot, HeadlessPlayerId player, ChoiceZone zone)
{
    return Zone(snapshot, player, zone).Count;
}

static async Task<DcgoMatch> CreateMatchAsync()
{
    DcgoMatch match = new();
    await match.InitializeAsync(BuildMatchConfig());
    return match;
}

static MatchConfig BuildMatchConfig()
{
    HeadlessPlayerId[] players = { new(1), new(2) };
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { BuildDeck(new HeadlessPlayerId(1), "P1"), BuildDeck(new HeadlessPlayerId(2), "P2") },
        firstPlayerId: new HeadlessPlayerId(1));
    return MatchConfig.Create(players, randomSeed: 17, setup: setup);
}

static PlayerDeckSetup BuildDeck(HeadlessPlayerId playerId, string prefix, int mainCount = 12, int digitamaCount = 3)
{
    return new PlayerDeckSetup(
        playerId,
        Enumerable.Range(1, mainCount).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, digitamaCount).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());
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
