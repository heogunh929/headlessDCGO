using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// D-1 Link: link-card attach/detach on a Digimon (AS-IS Permanent.AddLinkCard / RemoveLinkedCard).
// Linked cards move off-field and are tracked on the host metadata; link DP accumulates; the host's
// max (default 1) force-trashes excess; WhenLinked / OnLinkCardDiscarded windows open (F-6.9).

HeadlessPlayerId P1 = new(1);
HeadlessEntityId Host = new("p1:main:HOST");
HeadlessEntityId Link1 = new("p1:hand:L1");
HeadlessEntityId Link2 = new("p1:hand:L2");

var tests = new (string Name, Func<Task> Body)[]
{
    ("AddLinkCard attaches off-field, tracks the card + link DP, opens WhenLinked", AttachLink),
    ("RemoveLinkCard detaches, trashes, decrements DP, opens OnLinkCardDiscarded", DetachLink),
    ("Linked max (default 1) force-trashes the oldest excess link card", MaxEnforced),
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

async Task AttachLink()
{
    EngineContext context = await Board();
    bool ok = await LinkHelpers.AddLinkCardAsync(context.CardInstanceRepository, context.ZoneMover, Host, Link1, ChoiceZone.Hand, context.GameEventQueue);
    AssertTrue(ok, "attach succeeded");

    CardInstanceRecord host = Instance(context, Host);
    AssertTrue(LinkHelpers.ReadLinkedCardIds(host.Metadata).Contains(Link1), "Link1 tracked on host");
    AssertEqual(2000, LinkHelpers.ReadLinkedDp(host.Metadata), "link DP accumulated");
    AssertFalse(InZone(context, ChoiceZone.Hand, Link1), "Link1 left the hand");
    AssertFalse(InZone(context, ChoiceZone.BattleArea, Link1), "Link1 is off-field, not on the battle area");
    AssertTrue(QueueOpens(context, TriggerTimings.WhenLinked), "WhenLinked opened");
}

async Task DetachLink()
{
    EngineContext context = await Board();
    await LinkHelpers.AddLinkCardAsync(context.CardInstanceRepository, context.ZoneMover, Host, Link1, ChoiceZone.Hand, context.GameEventQueue);
    context.GameEventQueue.DrainPending();

    bool ok = await LinkHelpers.RemoveLinkCardAsync(context.CardInstanceRepository, context.ZoneMover, Host, Link1, trash: true, context.GameEventQueue);
    AssertTrue(ok, "detach succeeded");

    CardInstanceRecord host = Instance(context, Host);
    AssertFalse(LinkHelpers.ReadLinkedCardIds(host.Metadata).Contains(Link1), "Link1 no longer linked");
    AssertEqual(0, LinkHelpers.ReadLinkedDp(host.Metadata), "link DP back to 0");
    AssertTrue(InZone(context, ChoiceZone.Trash, Link1), "Link1 trashed");
    AssertTrue(QueueOpens(context, TriggerTimings.OnLinkCardDiscarded), "OnLinkCardDiscarded opened");
}

async Task MaxEnforced()
{
    EngineContext context = await Board();
    await LinkHelpers.AddLinkCardAsync(context.CardInstanceRepository, context.ZoneMover, Host, Link1, ChoiceZone.Hand, context.GameEventQueue);
    await LinkHelpers.AddLinkCardAsync(context.CardInstanceRepository, context.ZoneMover, Host, Link2, ChoiceZone.Hand, context.GameEventQueue);

    CardInstanceRecord host = Instance(context, Host);
    IReadOnlyList<HeadlessEntityId> linked = LinkHelpers.ReadLinkedCardIds(host.Metadata);
    AssertEqual(1, linked.Count, "max 1 enforced");
    AssertTrue(linked.Contains(Link2), "newest link card kept");
    AssertFalse(linked.Contains(Link1), "oldest excess removed");
    AssertTrue(InZone(context, ChoiceZone.Trash, Link1), "excess Link1 trashed");
    AssertEqual(2000, LinkHelpers.ReadLinkedDp(host.Metadata), "link DP reflects only the kept card");
}

// --- Helpers -------------------------------------------------------------

async Task<EngineContext> Board()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 11);
    await Place(context, Host, ChoiceZone.BattleArea, linkDp: null);
    await Place(context, Link1, ChoiceZone.Hand, linkDp: 2000);
    await Place(context, Link2, ChoiceZone.Hand, linkDp: 2000);
    context.GameEventQueue.DrainPending();
    return context;
}

async Task Place(EngineContext context, HeadlessEntityId id, ChoiceZone zone, int? linkDp)
{
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal);
    if (linkDp.HasValue)
    {
        meta[LinkHelpers.LinkDpKey] = linkDp.Value;
    }

    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId(id.Value), P1, Metadata: meta));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, zone));
}

CardInstanceRecord Instance(EngineContext context, HeadlessEntityId id) =>
    context.CardInstanceRepository.TryGetInstance(id, out var r) && r is not null ? r : throw new InvalidOperationException($"missing {id}");

bool InZone(EngineContext context, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(P1, zone).Contains(cardId);

bool QueueOpens(EngineContext context, string timing) =>
    context.GameEventQueue.DrainPending().Any(e => TriggerTimingMap.Derive(e).Contains(timing));

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertFalse(bool v, string label) { if (v) throw new InvalidOperationException($"{label}: expected false."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
