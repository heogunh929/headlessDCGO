using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using CardSource = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.CardSource;
using Permanent = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.Permanent;

// (MIG4 goal-4 slice 1) The AS-IS Permanent instance-method surface added to the mirror Permanent class,
// each delegating to a verified headless helper: DiscardEvoRoots, AddDigivolutionCardsTop/Bottom, AddLinkCard,
// RemoveCardSource. Smoke coverage for the common (single-zone, hand-origin) paths — the surface is otherwise
// unwired (no current caller), so these tests are its only coverage until card ports call it.

HeadlessPlayerId P1 = new(1);

var tests = new (string Name, Func<Task> Body)[]
{
    ("AddDigivolutionCardsTop moves a hand card under the top (now a digivolution source)", AddTop),
    ("AddDigivolutionCardsBottom appends a hand card to the stack bottom", AddBottom),
    ("AddLinkCard attaches a hand card as a link card", AddLink),
    ("RemoveCardSource detaches a source without trashing it (bare removal)", RemoveSource),
    ("DiscardEvoRoots trashes the permanent's digivolution sources", DiscardRoots),
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

async Task AddTop()
{
    EngineContext context = Board();
    HeadlessEntityId host = PlaceDigimon(context, "HOST", dp: 4000);
    HeadlessEntityId src = PlaceHand(context, "SRC");

    var permanent = new Permanent(context, host, P1);
    await permanent.AddDigivolutionCardsTop(new List<CardSource> { new(context, src, P1, P1) }, causeEffectSourceId: null);

    AssertTrue(SourceIds(context, host).Contains(src), "SRC is now a digivolution source of HOST");
    AssertFalse(InZone(context, P1, ChoiceZone.Hand, src), "SRC left the hand");
    AssertTrue(new Permanent(context, host, P1).DigivolutionCards.Any(c => c.InstanceId == src), "DigivolutionCards view includes SRC");
}

async Task AddBottom()
{
    EngineContext context = Board();
    HeadlessEntityId host = PlaceDigimon(context, "HOST", dp: 4000);
    HeadlessEntityId src = PlaceHand(context, "SRC");

    var permanent = new Permanent(context, host, P1);
    await permanent.AddDigivolutionCardsBottom(new List<CardSource> { new(context, src, P1, P1) }, causeEffectSourceId: null);

    AssertTrue(SourceIds(context, host).Contains(src), "SRC is now a digivolution source of HOST");
    AssertFalse(InZone(context, P1, ChoiceZone.Hand, src), "SRC left the hand");
}

async Task AddLink()
{
    EngineContext context = Board();
    HeadlessEntityId host = PlaceDigimon(context, "HOST", dp: 4000);
    HeadlessEntityId link = PlaceHand(context, "LINK");

    var permanent = new Permanent(context, host, P1);
    await permanent.AddLinkCard(new CardSource(context, link, P1, P1), causeEffectSourceId: null);

    var linked = LinkHelpers.ReadLinkedCardIds(Instance(context, host).Metadata);
    AssertTrue(linked.Contains(link), "LINK is now a link card of HOST");
    AssertFalse(InZone(context, P1, ChoiceZone.Hand, link), "LINK left the hand");
}

async Task RemoveSource()
{
    EngineContext context = Board();
    HeadlessEntityId host = PlaceDigimon(context, "HOST", dp: 4000);
    HeadlessEntityId src = PlaceHand(context, "SRC");
    var permanent = new Permanent(context, host, P1);
    await permanent.AddDigivolutionCardsTop(new List<CardSource> { new(context, src, P1, P1) }, causeEffectSourceId: null);
    AssertTrue(SourceIds(context, host).Contains(src), "precondition: SRC is a source");

    await permanent.RemoveCardSource(new CardSource(context, src, P1, P1));

    AssertFalse(SourceIds(context, host).Contains(src), "SRC detached from the stack");
    AssertFalse(InZone(context, P1, ChoiceZone.Trash, src), "bare removal did NOT trash SRC");
}

async Task DiscardRoots()
{
    EngineContext context = Board();
    HeadlessEntityId host = PlaceDigimon(context, "HOST", dp: 4000);
    HeadlessEntityId src = PlaceHand(context, "SRC");
    var permanent = new Permanent(context, host, P1);
    await permanent.AddDigivolutionCardsTop(new List<CardSource> { new(context, src, P1, P1) }, causeEffectSourceId: null);
    AssertTrue(SourceIds(context, host).Contains(src), "precondition: SRC is a source");

    await permanent.DiscardEvoRoots();

    AssertFalse(SourceIds(context, host).Contains(src), "source removed from the stack");
    AssertTrue(InZone(context, P1, ChoiceZone.Trash, src), "source trashed");
}

// --- Helpers -------------------------------------------------------------

EngineContext Board()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 41);
    context.TurnController.Initialize(new[] { P1, new HeadlessPlayerId(2) }, P1);
    return context;
}

HeadlessEntityId PlaceDigimon(EngineContext context, string tag, int dp)
{
    ((CardDatabase)context.CardRepository).Upsert(new CardRecord(new HeadlessEntityId($"DEF:{tag}"), tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"card:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId($"DEF:{tag}"), P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { [BattleResolver.DpKey] = dp }));
    context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
    return id;
}

HeadlessEntityId PlaceHand(EngineContext context, string tag)
{
    ((CardDatabase)context.CardRepository).Upsert(new CardRecord(new HeadlessEntityId($"DEF:{tag}"), tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 1000 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"card:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId($"DEF:{tag}"), P1));
    context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.Hand)).GetAwaiter().GetResult();
    return id;
}

// Read the sources via the public mirror Permanent.DigivolutionCards view (the same accessor card ports use).
IReadOnlyList<HeadlessEntityId> SourceIds(EngineContext context, HeadlessEntityId host) =>
    new Permanent(context, host, P1).DigivolutionCards.Select(c => c.InstanceId).ToList();

CardInstanceRecord Instance(EngineContext context, HeadlessEntityId id) =>
    context.CardInstanceRepository.TryGetInstance(id, out var r) && r is not null ? r : throw new InvalidOperationException($"missing {id}");

bool InZone(EngineContext context, HeadlessPlayerId owner, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(owner, zone).Contains(cardId);

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertFalse(bool v, string label) { if (v) throw new InvalidOperationException($"{label}: expected false."); }
