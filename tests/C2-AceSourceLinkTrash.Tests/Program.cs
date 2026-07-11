using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// C-2 (TODO-98): AS-IS Permanent.DiscardEvoRoots (Permanent.cs:106-142) — when a permanent is deleted, its
// digivolution SOURCES and LINK cards leave the field: an un-flipped ACE among them costs its owner memory
// (AceOverflowClass.Overflow, CardController.cs:5836), evo roots then link roots, BEFORE anything is trashed;
// then the evo sources are trashed and the link cards detached/trashed. Verified end-to-end through the
// mutation sink's Delete path (the host is still on the field when TrashEvoSources runs, mirroring AS-IS
// DiscardEvoRoots being called before RemoveField).

HeadlessPlayerId P1 = new(1); // turn player
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("(a) un-flip ACE evo source -> owner -3 (5->2) + source trashed", CaseAceSourceOverflow),
    ("(b) flipped ACE evo source -> no overflow (5) + source still trashed", CaseFlippedSourceNoOverflow),
    ("(c) ACE link card -> owner -3 (5->2) + link trashed", CaseAceLinkOverflow),
    ("(d) turn-player-first ordering across two ACE sources (clamp reveals order)", CaseTurnPlayerPriority),
    ("(e) evo-overflow -> link-overflow -> trash order (clamp reveals evo-before-link)", CaseEvoBeforeLinkThenTrash),
    ("(f) [P1 review] battle/security/finalize path charges the leaving ACE's OWN top overflow", CaseTopOverflowOnRawMovePath),
    ("(g) [P1 review] a flipped ACE top on the raw-move path pays no overflow", CaseFlippedTopNoOverflow),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failures.Add(test.Name); Console.Error.WriteLine($"FAIL {test.Name}: {ex.Message}"); }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

// (C-2 adversarial review P1) The battle / security / deferred-finalize deletion finishers move the top card
// with a raw MoveAsync that skips the sink's ApplyAceOverflowOnLeave, so a battle-deleted un-flipped ACE
// previously paid ONLY its sources' overflow, not its own top. AceOverflowGate.ApplyTopOverflowOnDelete is the
// shared helper those three paths now call before the top move; verify it directly (the battle path itself is
// covered by the battle-resolution regression suites).
async Task CaseTopOverflowOnRawMovePath()
{
    EngineContext context = Context();
    context.MemoryController.Set(5);
    HeadlessEntityId ace = PlaceAceHostOnField(context, P1, flipped: false);

    AceOverflowGate.ApplyTopOverflowOnDelete(context, ace);
    AssertTrue(context.MemoryController.Current.Current == 2, $"un-flip ACE top on field -> owner -3 (5->2); got {context.MemoryController.Current.Current}");
}

async Task CaseFlippedTopNoOverflow()
{
    EngineContext context = Context();
    context.MemoryController.Set(5);
    HeadlessEntityId ace = PlaceAceHostOnField(context, P1, flipped: true);

    AceOverflowGate.ApplyTopOverflowOnDelete(context, ace);
    AssertTrue(context.MemoryController.Current.Current == 5, $"flipped ACE top pays no overflow (5); got {context.MemoryController.Current.Current}");
}

async Task CaseAceSourceOverflow()
{
    EngineContext context = Context();
    context.MemoryController.Set(5);
    HeadlessEntityId host = PlaceHost(context, P1);
    HeadlessEntityId src = PlaceLoose(context, P1, "SRC", ace: true, flipped: false);
    SetSources(context, host, src);

    await DeleteHost(context, host);

    AssertEqual(2, context.MemoryController.Current.Current, "memory after ACE source overflow");
    AssertTrue(InZone(context, P1, ChoiceZone.Trash, src), "ACE source trashed");
}

async Task CaseFlippedSourceNoOverflow()
{
    EngineContext context = Context();
    context.MemoryController.Set(5);
    HeadlessEntityId host = PlaceHost(context, P1);
    HeadlessEntityId src = PlaceLoose(context, P1, "SRC", ace: true, flipped: true);
    SetSources(context, host, src);

    await DeleteHost(context, host);

    AssertEqual(5, context.MemoryController.Current.Current, "no overflow for flipped ACE source");
    AssertTrue(InZone(context, P1, ChoiceZone.Trash, src), "flipped ACE source still trashed");
}

async Task CaseAceLinkOverflow()
{
    EngineContext context = Context();
    context.MemoryController.Set(5);
    HeadlessEntityId host = PlaceHost(context, P1);
    HeadlessEntityId link = PlaceLoose(context, P1, "LINK", ace: true, flipped: false);
    SetLinks(context, host, link);

    await DeleteHost(context, host);

    AssertEqual(2, context.MemoryController.Current.Current, "memory after ACE link overflow");
    AssertTrue(InZone(context, P1, ChoiceZone.Trash, link), "ACE link card trashed");
    CardInstanceRecord h = Instance(context, host);
    AssertEqual(0, LinkHelpers.ReadLinkedCardIds(h.Metadata).Count, "host link list cleared");
}

// Two ACE evo sources, one owned by the turn player (P1) and one off-turn (P2). Their penalties net to 0
// (P1: -3, P2: +3), so ONLY the turn-player-first ordering plus the +10 memory cap distinguishes the order:
// correct (P1 first) 9 -3 -> 6, +3 -> 9; wrong (P2 first) 9 +3 -> 10 (capped), -3 -> 7.
async Task CaseTurnPlayerPriority()
{
    EngineContext context = Context();
    context.MemoryController.Set(9);
    HeadlessEntityId host = PlaceHost(context, P1);
    HeadlessEntityId offTurn = PlaceLoose(context, P2, "OFF", ace: true, flipped: false); // top of stack
    HeadlessEntityId onTurn = PlaceLoose(context, P1, "ON", ace: true, flipped: false);   // bottom of stack
    SetSources(context, host, offTurn, onTurn); // stack order puts the off-turn source first

    await DeleteHost(context, host);

    AssertEqual(9, context.MemoryController.Current.Current, "turn-player ACE source penalized first");
}

// Evo source owned by the turn player (P1), link card owned off-turn (P2). Overflow order evo-then-link with
// the +10 cap reveals evo-before-link: correct 9 -3 -> 6, +3 -> 9; wrong (link first) 9 +3 -> 10, -3 -> 7.
// Both cards must also end up in the trash (overflow precedes trash).
async Task CaseEvoBeforeLinkThenTrash()
{
    EngineContext context = Context();
    context.MemoryController.Set(9);
    HeadlessEntityId host = PlaceHost(context, P1);
    HeadlessEntityId src = PlaceLoose(context, P1, "SRC", ace: true, flipped: false);
    HeadlessEntityId link = PlaceLoose(context, P2, "LINK", ace: true, flipped: false);
    SetSources(context, host, src);
    SetLinks(context, host, link);

    await DeleteHost(context, host);

    AssertEqual(9, context.MemoryController.Current.Current, "evo overflow applied before link overflow");
    AssertTrue(InZone(context, P1, ChoiceZone.Trash, src), "evo source trashed");
    AssertTrue(InZone(context, P2, ChoiceZone.Trash, link), "link card trashed");
}

// --- Harness -------------------------------------------------------------

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 98);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    return context;
}

// A non-ACE Digimon host on the battle area (non-ACE so its OWN top-card overflow does not confound the
// source/link penalty under test).
HeadlessEntityId PlaceHost(EngineContext context, HeadlessPlayerId owner)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:HOST");
    cards.Upsert(new CardRecord(defId, defId.Value, "HOST",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 }, CardType: "Digimon"));
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["dp"] = 4000,
        [AceOverflowGate.IsAceKey] = false,
    };
    var id = new HeadlessEntityId($"{owner.Value}:HOST");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner, Metadata: meta));
    context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
    return id;
}

// A loose card (source or link) sitting off-field (ChoiceZone.None) with ACE metadata (overflow 3).
HeadlessEntityId PlaceLoose(EngineContext context, HeadlessPlayerId owner, string tag, bool ace, bool flipped)
{
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [AceOverflowGate.IsAceKey] = ace,
        [AceOverflowGate.OverflowMemoryKey] = 3,
        [AceOverflowGate.IsFlippedKey] = flipped,
    };
    var id = new HeadlessEntityId($"{owner.Value}:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner, Metadata: meta));
    return id;
}

// An un-flipped/flipped ACE Digimon (overflow 3) on the battle area — the leaving-permanent (top card) under
// the P1-review top-overflow path.
HeadlessEntityId PlaceAceHostOnField(EngineContext context, HeadlessPlayerId owner, bool flipped)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:ACEHOST");
    cards.Upsert(new CardRecord(defId, defId.Value, "ACEHOST",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 }, CardType: "Digimon"));
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["dp"] = 4000,
        [AceOverflowGate.IsAceKey] = true,
        [AceOverflowGate.OverflowMemoryKey] = 3,
        [AceOverflowGate.IsFlippedKey] = flipped,
    };
    var id = new HeadlessEntityId($"{owner.Value}:ACEHOST");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner, Metadata: meta));
    context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
    return id;
}

void SetSources(EngineContext context, HeadlessEntityId host, params HeadlessEntityId[] sources)
{
    CardInstanceRecord h = Instance(context, host);
    var meta = new Dictionary<string, object?>(h.Metadata, StringComparer.Ordinal)
    {
        ["sourceIds"] = sources.Select(s => s.Value).ToArray(),
    };
    context.CardInstanceRepository.Upsert(h with { Metadata = meta });
}

void SetLinks(EngineContext context, HeadlessEntityId host, params HeadlessEntityId[] links)
{
    CardInstanceRecord h = Instance(context, host);
    var meta = new Dictionary<string, object?>(h.Metadata, StringComparer.Ordinal)
    {
        [LinkHelpers.LinkedCardIdsKey] = links.Select(l => l.Value).ToArray(),
    };
    context.CardInstanceRepository.Upsert(h with { Metadata = meta });
}

async Task DeleteHost(EngineContext context, HeadlessEntityId host)
{
    var sink = new MatchStateMutationSink(
        context.CardInstanceRepository, context.LogSink, context.ZoneMover, context.MemoryController,
        context.EffectRegistry, context.GameEventQueue, currentTurnPlayer: () => P1);

    var values = new Dictionary<string, object?>(StringComparer.Ordinal) { ["targetEntityId"] = host.Value };
    sink.Apply(new EffectMutation(MatchStateMutationSink.DeleteKind, new HeadlessEntityId("src"), values));
    await sink.FlushAsync();
}

CardInstanceRecord Instance(EngineContext context, HeadlessEntityId id) =>
    context.CardInstanceRepository.TryGetInstance(id, out var r) && r is not null ? r : throw new InvalidOperationException($"missing {id}");

bool InZone(EngineContext context, HeadlessPlayerId owner, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(owner, zone).Contains(cardId);

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T e, T a, string label) { if (!EqualityComparer<T>.Default.Equals(e, a)) throw new InvalidOperationException($"{label}: expected '{e}', got '{a}'."); }
