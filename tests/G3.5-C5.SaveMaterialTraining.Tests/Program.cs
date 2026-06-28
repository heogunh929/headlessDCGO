using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// C-group5 — all three build on AS-IS Permanent.AddDigivolutionCardsBottom (place a card under a
// permanent as a digivolution source):
//   * C-22 Save          — after this card is deleted, place it under another of the owner's permanents.
//                          Post-deletion consumption (DeletionReplacementGate.TrySaveAsync).
//   * C-23 Material Save  — move N of this Digimon's sources onto another permanent's stack.
//   * C-24 Training       — suspend self, place the top library card under self.
// Material Save / Training are ACTIVATED effects (no passive trigger), so the engine exposes the
// primitives in DigivolutionStackHelpers and the activation is authored at porting time.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    // Save (with a target permanent) is now a post-deletion two-step agent CHOICE (F-6.8) — see G3.5-F68.
    ("Save: with no other permanent the card stays in the trash", SaveNoTargetStaysTrashed),
    ("Material Save: sources move to the bottom of another permanent's stack", MaterialSaveMovesSources),
    ("Training: suspends self and places the top library card under it", TrainingAddsLibraryCard),
    ("Training: an already-suspended Digimon cannot train", TrainingSuspendedFails),
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

// --- Save (C-22) ---------------------------------------------------------

async Task SaveNoTargetStaysTrashed()
{
    HeadlessEntityId card = new("P2-Save");
    HeadlessEntityId deleter = new("P1-D");
    EngineContext context = EngineContext.CreateDefault(randomSeed: 12);
    PlaceOnField(context, card, P2, Flag(DeletionReplacementGate.HasSaveKey));
    PlaceInNone(context, deleter, P1);
    MatchStateMutationSink sink = Sink(context);

    sink.Apply(Delete(card, deleter));
    await sink.FlushAsync();

    AssertTrue(InZone(context, P2, ChoiceZone.Trash, card), "no target: save cannot fire, card trashed");
}

// --- Material Save (C-23) ------------------------------------------------

Task MaterialSaveMovesSources()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 12);
    HeadlessEntityId from = new("P2-From");
    HeadlessEntityId to = new("P2-To");
    PlaceInNone(context, from, P2);
    PlaceInNone(context, to, P2);
    SetSources(context, from, "a", "b", "c");

    bool moved = DigivolutionStackHelpers.MoveSourcesBottom(context.CardInstanceRepository, from, to, count: 2);

    AssertTrue(moved, "material save moved sources");
    AssertSequence(SourceIds(context, from), "c");        // first 2 removed, deepest stays
    AssertSequence(SourceIds(context, to), "a", "b");     // moved to the bottom of the target stack
    return Task.CompletedTask;
}

// --- Training (C-24) -----------------------------------------------------

async Task TrainingAddsLibraryCard()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 12);
    HeadlessEntityId digimon = new("P2-Trainer");
    HeadlessEntityId libTop = new("P2-Lib1");
    PlaceOnField(context, digimon, P2, Empty());
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(libTop, new HeadlessEntityId("def"), P2));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, libTop, ChoiceZone.None, ChoiceZone.Library));

    bool trained = await DigivolutionStackHelpers.TrainAsync(context.CardInstanceRepository, context.ZoneMover, digimon);

    AssertTrue(trained, "training applied");
    AssertTrue(ReadFlag(context, digimon, DigivolutionStackHelpers.IsSuspendedKey), "self suspended as the cost");
    AssertSequence(SourceIds(context, digimon), libTop.Value);   // library top placed under self
    AssertFalse(InZone(context, P2, ChoiceZone.Library, libTop), "library top left the deck");
}

async Task TrainingSuspendedFails()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 12);
    HeadlessEntityId digimon = new("P2-Trainer");
    HeadlessEntityId libTop = new("P2-Lib1");
    PlaceOnField(context, digimon, P2, Flag(DigivolutionStackHelpers.IsSuspendedKey));
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(libTop, new HeadlessEntityId("def"), P2));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, libTop, ChoiceZone.None, ChoiceZone.Library));

    bool trained = await DigivolutionStackHelpers.TrainAsync(context.CardInstanceRepository, context.ZoneMover, digimon);

    AssertFalse(trained, "already-suspended Digimon cannot pay the training cost");
    AssertTrue(InZone(context, P2, ChoiceZone.Library, libTop), "library top untouched");
}

// --- Helpers -------------------------------------------------------------

Dictionary<string, object?> Empty() => new(StringComparer.Ordinal);
Dictionary<string, object?> Flag(string key) => new(StringComparer.Ordinal) { [key] = true };

void PlaceOnField(EngineContext context, HeadlessEntityId id, HeadlessPlayerId owner, IReadOnlyDictionary<string, object?> metadata)
{
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId("def"), owner, Metadata: metadata));
    context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
}

void PlaceInNone(EngineContext context, HeadlessEntityId id, HeadlessPlayerId owner) =>
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId("def"), owner));

void SetSources(EngineContext context, HeadlessEntityId cardId, params string[] sources)
{
    CardInstanceRecord record = context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? r) && r is not null
        ? r : throw new InvalidOperationException($"Missing {cardId}.");
    context.CardInstanceRepository.Upsert(record with
    {
        Metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal) { [DigivolutionStackHelpers.SourceIdsKey] = sources }
    });
}

MatchStateMutationSink Sink(EngineContext context) =>
    new(context.CardInstanceRepository, log: null, context.ZoneMover, memory: null, context.EffectRegistry);

EffectMutation Delete(HeadlessEntityId cardId, HeadlessEntityId deleterId) =>
    new(MatchStateMutationSink.DeleteKind, deleterId,
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = cardId.Value });

bool InZone(EngineContext context, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(player, zone).Contains(cardId);

bool ReadFlag(EngineContext context, HeadlessEntityId cardId, string key) =>
    context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? r) && r is not null
        && r.Metadata.TryGetValue(key, out object? raw) && raw is bool b && b;

string[] SourceIds(EngineContext context, HeadlessEntityId cardId) =>
    context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? r) && r is not null
        && r.Metadata.TryGetValue(DigivolutionStackHelpers.SourceIdsKey, out object? raw) && raw is IEnumerable<string> ids
        ? ids.ToArray() : Array.Empty<string>();

// --- Assertions ----------------------------------------------------------

static void AssertTrue(bool value, string label)
{
    if (!value) throw new InvalidOperationException($"{label}: expected true.");
}

static void AssertFalse(bool value, string label)
{
    if (value) throw new InvalidOperationException($"{label}: expected false.");
}

static void AssertSequence(IReadOnlyList<string> actual, params string[] expected)
{
    if (actual.Count != expected.Length)
    {
        throw new InvalidOperationException($"sequence length: expected {expected.Length}, actual {actual.Count} [{string.Join(",", actual)}].");
    }

    for (int i = 0; i < expected.Length; i++)
    {
        if (!string.Equals(actual[i], expected[i], StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"sequence[{i}]: expected '{expected[i]}', actual '{actual[i]}'.");
        }
    }
}
