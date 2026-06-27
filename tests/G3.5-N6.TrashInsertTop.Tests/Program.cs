using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

// N-6: the original always inserts into the trash at index 0 (most recent on top, TrashCards.Insert(0)),
// regardless of the move path. The port now centralises this in AddToZone, so every trash insertion
// (AddToTrash / generic MoveAsync / security trash) puts the newest card on top (index 0).

HeadlessPlayerId P = new(1);

var tests = new (string Name, Func<Task> Body)[]
{
    ("AddToTrash puts the most recent card on top", AddToTrashTop),
    ("Generic MoveAsync into trash also inserts on top", MoveIntoTrashTop),
    ("Trashing security stacks the newest on top", SecurityTrashTop),
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

async Task AddToTrashTop()
{
    var mover = new InMemoryZoneMover(new GameRandomSource(seed: 3));
    await mover.AddToTrashAsync(P, new HeadlessEntityId("T1"));
    await mover.AddToTrashAsync(P, new HeadlessEntityId("T2"));
    await mover.AddToTrashAsync(P, new HeadlessEntityId("T3"));

    AssertSequence(new[] { "T3", "T2", "T1" }, Trash(mover), "most recent on top");
}

async Task MoveIntoTrashTop()
{
    var mover = new InMemoryZoneMover(new GameRandomSource(seed: 3));
    await mover.MoveAsync(new ZoneMoveRequest(P, new HeadlessEntityId("A"), ChoiceZone.None, ChoiceZone.BattleArea));
    await mover.MoveAsync(new ZoneMoveRequest(P, new HeadlessEntityId("B"), ChoiceZone.None, ChoiceZone.BattleArea));
    // Move both to trash via generic MoveAsync; the second moved lands on top.
    await mover.MoveAsync(new ZoneMoveRequest(P, new HeadlessEntityId("A"), ChoiceZone.BattleArea, ChoiceZone.Trash));
    await mover.MoveAsync(new ZoneMoveRequest(P, new HeadlessEntityId("B"), ChoiceZone.BattleArea, ChoiceZone.Trash));

    AssertSequence(new[] { "B", "A" }, Trash(mover), "generic move newest on top");
}

async Task SecurityTrashTop()
{
    var mover = new InMemoryZoneMover(new GameRandomSource(seed: 3));
    foreach (string id in new[] { "S1", "S2", "S3" })
    {
        await mover.MoveAsync(new ZoneMoveRequest(P, new HeadlessEntityId(id), ChoiceZone.None, ChoiceZone.Security));
    }

    // Trash two from the security top (S1 then S2 after N-3 ordering: security = [S1,S2,S3], top=index0).
    await mover.TrashSecurityAsync(P, count: 2, fromTop: true);

    // Newest trashed (second) is on top of the trash.
    AssertEqual(2, Trash(mover).Count, "two cards trashed");
    AssertEqual("S2", Trash(mover)[0], "second trashed on top");
    AssertEqual("S1", Trash(mover)[1], "first trashed below");
}

// --- Helpers -------------------------------------------------------------

IReadOnlyList<string> Trash(InMemoryZoneMover mover) =>
    mover.GetCards(P, ChoiceZone.Trash).Select(id => id.Value).ToArray();

static void AssertSequence(IEnumerable<string> expected, IEnumerable<string> actual, string label)
{
    string[] e = expected.ToArray();
    string[] a = actual.ToArray();
    if (e.Length != a.Length || !e.SequenceEqual(a))
        throw new InvalidOperationException($"{label}: expected [{string.Join(",", e)}], got [{string.Join(",", a)}].");
}

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
