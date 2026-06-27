using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

// N-3: security insertion now matches the original AddSecurityCard(toTop: true) default. Index 0 is the
// security TOP (the next card checked / trashed-from-top), so:
//   * AddToSecurityAsync defaults to a TOP insert (a recovered/returned card is checked first), and
//     toTop:false appends to the bottom.
//   * AddSecurityFromLibraryAsync deals each library-top card to the top (original Insert(0) stacking),
//     so the LAST dealt card ends up on top.
// Previously both paths bottom-appended, reversing the stack vs the original.

HeadlessPlayerId P = new(1);

var tests = new (string Name, Func<Task> Body)[]
{
    ("AddToSecurity defaults to the top (returned card is checked first)", AddToSecurityDefaultsTop),
    ("AddToSecurity toTop:false appends to the bottom", AddToSecurityBottomOverride),
    ("AddSecurityFromLibrary stacks each library-top card on top (last dealt = top)", LibraryDealStacksOnTop),
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

async Task AddToSecurityDefaultsTop()
{
    var mover = new InMemoryZoneMover(new GameRandomSource(seed: 5));
    await SeedSecurity(mover, "S1", "S2", "S3"); // bottom-appended baseline: [S1, S2, S3]

    await mover.AddToSecurityAsync(P, new HeadlessEntityId("R"), faceUp: false); // default toTop:true

    AssertSequence(new[] { "R", "S1", "S2", "S3" }, Security(mover), "returned card on top (index 0)");

    // Consumption confirms it: trash-from-top takes the just-returned card first.
    IReadOnlyList<HeadlessEntityId> trashed = await mover.TrashSecurityAsync(P, count: 1, fromTop: true);
    AssertSequence(new[] { "R" }, trashed.Select(id => id.Value), "top card is consumed first");
}

async Task AddToSecurityBottomOverride()
{
    var mover = new InMemoryZoneMover(new GameRandomSource(seed: 5));
    await SeedSecurity(mover, "S1", "S2", "S3");

    await mover.AddToSecurityAsync(P, new HeadlessEntityId("R"), faceUp: false, toTop: false);

    AssertSequence(new[] { "S1", "S2", "S3", "R" }, Security(mover), "toTop:false appends to the bottom");
}

async Task LibraryDealStacksOnTop()
{
    var mover = new InMemoryZoneMover(new GameRandomSource(seed: 5));
    // Library top order A, B, C (index 0 = top, the order DrawAsync/deal consumes).
    await Seed(mover, ChoiceZone.Library, "A", "B", "C");

    IReadOnlyList<HeadlessEntityId> dealt = await mover.AddSecurityFromLibraryAsync(P, count: 3);

    AssertSequence(new[] { "A", "B", "C" }, dealt.Select(id => id.Value), "dealt in library-top order");
    // Each is Insert(0)'d, so the last dealt (C) ends up on top — matches the original AddSecurity stack.
    AssertSequence(new[] { "C", "B", "A" }, Security(mover), "last dealt card is on top");
}

// --- Helpers -------------------------------------------------------------

async Task SeedSecurity(InMemoryZoneMover mover, params string[] ids) =>
    await Seed(mover, ChoiceZone.Security, ids);

async Task Seed(InMemoryZoneMover mover, ChoiceZone zone, params string[] ids)
{
    foreach (string id in ids)
    {
        await mover.MoveAsync(new ZoneMoveRequest(P, new HeadlessEntityId(id), ChoiceZone.None, zone));
    }
}

IReadOnlyList<string> Security(InMemoryZoneMover mover) =>
    mover.GetCards(P, ChoiceZone.Security).Select(id => id.Value).ToArray();

static void AssertSequence(IEnumerable<string> expected, IEnumerable<string> actual, string label)
{
    string[] e = expected.ToArray();
    string[] a = actual.ToArray();
    if (e.Length != a.Length || !e.SequenceEqual(a))
    {
        throw new InvalidOperationException($"{label}: expected [{string.Join(",", e)}], got [{string.Join(",", a)}].");
    }
}
