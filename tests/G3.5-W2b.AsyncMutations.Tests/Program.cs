using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G3.5-W2-follow: async / controller-backed effect mutations — zone moves (trash/bounce/deck),
// draw, and memory. Zone moves are recorded synchronously and applied on FlushAsync (which the
// resolver calls after the effect body); memory is applied immediately via the controller.

HeadlessPlayerId P1 = new(1);
HeadlessEntityId Card = new("p1:main:001:X");

var tests = new (string Name, Func<Task> Body)[]
{
    ("TrashCard moves the target to the trash on flush", TrashCardOnFlush),
    ("ReturnToHand bounces the target to hand on flush", ReturnToHandOnFlush),
    ("Zone moves are deferred until FlushAsync", DeferredUntilFlush),
    ("DrawCards draws for the player on flush", DrawOnFlush),
    ("AddMemory / SetMemory apply immediately via the controller", MemoryApplies),
    ("Without a zone mover, a zone mutation is unsupported", NoZoneMoverUnsupported),
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

async Task TrashCardOnFlush()
{
    var (zones, _, sink) = await SetupWithCardInBattleAsync();
    sink.Apply(new EffectMutation(MatchStateMutationSink.TrashCardKind, Card));
    await sink.FlushAsync();

    AssertFalse(InZone(zones, ChoiceZone.BattleArea), "card left battle area");
    AssertTrue(InZone(zones, ChoiceZone.Trash), "card moved to trash");
}

async Task ReturnToHandOnFlush()
{
    var (zones, _, sink) = await SetupWithCardInBattleAsync();
    sink.Apply(new EffectMutation(MatchStateMutationSink.ReturnToHandKind, Card));
    await sink.FlushAsync();

    AssertTrue(InZone(zones, ChoiceZone.Hand), "card returned to hand");
}

async Task DeferredUntilFlush()
{
    var (zones, _, sink) = await SetupWithCardInBattleAsync();
    sink.Apply(new EffectMutation(MatchStateMutationSink.TrashCardKind, Card));

    AssertTrue(InZone(zones, ChoiceZone.BattleArea), "move not applied before flush");
    AssertEqual(1, sink.AppliedCount, "recorded as applied (pending)");

    await sink.FlushAsync();
    AssertTrue(InZone(zones, ChoiceZone.Trash), "move applied after flush");
}

async Task DrawOnFlush()
{
    var rng = new GameRandomSource(7);
    var zoneMover = new InMemoryZoneMover(rng);
    await zoneMover.MoveAsync(new ZoneMoveRequest(P1, new HeadlessEntityId("lib1"), ChoiceZone.None, ChoiceZone.Library));
    await zoneMover.MoveAsync(new ZoneMoveRequest(P1, new HeadlessEntityId("lib2"), ChoiceZone.None, ChoiceZone.Library));

    var repo = new InMemoryCardInstanceRepository();
    var sink = new MatchStateMutationSink(repo, null, zoneMover);

    sink.Apply(new EffectMutation(MatchStateMutationSink.DrawCardsKind, Card, new Dictionary<string, object?>
    {
        [MatchStateMutationSink.PlayerIdKey] = P1,
        [MatchStateMutationSink.CountKey] = 1,
    }));

    var zones = (IZoneStateReader)zoneMover;
    AssertEqual(2, zones.GetCards(P1, ChoiceZone.Library).Count, "no draw before flush");

    await sink.FlushAsync();
    AssertEqual(1, zones.GetCards(P1, ChoiceZone.Library).Count, "one card drawn");
    AssertEqual(1, zones.GetCards(P1, ChoiceZone.Hand).Count, "card is in hand");
}

Task MemoryApplies()
{
    var memory = new InMemoryHeadlessMemoryController();
    memory.Initialize(0);
    var repo = new InMemoryCardInstanceRepository();
    var sink = new MatchStateMutationSink(repo, null, null, memory);

    sink.Apply(new EffectMutation(MatchStateMutationSink.AddMemoryKind, Card, new Dictionary<string, object?> { [MatchStateMutationSink.AmountKey] = 3 }));
    AssertEqual(3, memory.Current.Current, "AddMemory applied immediately");

    sink.Apply(new EffectMutation(MatchStateMutationSink.SetMemoryKind, Card, new Dictionary<string, object?> { [MatchStateMutationSink.AmountKey] = -2 }));
    AssertEqual(-2, memory.Current.Current, "SetMemory applied immediately");
    return Task.CompletedTask;
}

Task NoZoneMoverUnsupported()
{
    var repo = new InMemoryCardInstanceRepository();
    repo.Upsert(new CardInstanceRecord(Card, new HeadlessEntityId("def"), P1));
    var sink = new MatchStateMutationSink(repo); // no zone mover

    sink.Apply(new EffectMutation(MatchStateMutationSink.TrashCardKind, Card));
    AssertEqual(1, sink.UnsupportedCount, "zone mutation unsupported without a zone mover");
    return Task.CompletedTask;
}

// --- Helpers -------------------------------------------------------------

async Task<(IZoneStateReader Zones, InMemoryCardInstanceRepository Repo, MatchStateMutationSink Sink)> SetupWithCardInBattleAsync()
{
    var rng = new GameRandomSource(7);
    var zoneMover = new InMemoryZoneMover(rng);
    var memory = new InMemoryHeadlessMemoryController();
    memory.Initialize(0);
    var repo = new InMemoryCardInstanceRepository();
    repo.Upsert(new CardInstanceRecord(Card, new HeadlessEntityId("def"), P1));

    await zoneMover.MoveAsync(new ZoneMoveRequest(P1, Card, ChoiceZone.None, ChoiceZone.BattleArea));

    var sink = new MatchStateMutationSink(repo, null, zoneMover, memory);
    return ((IZoneStateReader)zoneMover, repo, sink);
}

bool InZone(IZoneStateReader zones, ChoiceZone zone) => zones.GetCards(P1, zone).Contains(Card);

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
