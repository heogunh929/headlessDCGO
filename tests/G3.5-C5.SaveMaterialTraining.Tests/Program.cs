using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// C-group5 — all three build on AS-IS Permanent.AddDigivolutionCardsBottom (place a card under a
// permanent as a digivolution source):
//   * C-22 Save          — after this card is deleted, place it under another of the owner's permanents.
//                          Post-deletion consumption (DeletionReplacementGate.TrySaveAsync).
//   * C-23 Material Save  — move N of this Digimon's sources onto another permanent's stack.
//   * C-24 Training       — suspend self, place the deck's top card FACE-DOWN under self.
// Material Save is an ACTIVATED effect (no passive trigger), so the engine exposes its primitive in
// DigivolutionStackHelpers and the activation is authored at porting time.
//
// (C-Act re-home) <Training> is now driven through the LIVE AS-IS window/activated path — a player-declared
// [Main] OnDeclaration skill: MainSkillActivateAction -> ActivatedEffectResolver -> CardEffectFactory.
// TrainingEffect (an ActivateClass: SuspendPermanentsClass.Tap cost + Permanent.AddDigivolutionCardsBottom
// (isFacedown: true)). The invented firing-half (TrainingActivatedEffect + the Train mutation +
// DigivolutionStackHelpers.TrainAsync) is RETIRED — these witnesses drive the window path only (window XOR
// gate). The fixture TfxMainTraining returns TrainingEffect at OnDeclaration exactly as AS-IS EX9_026.cs:31-33.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    // Save (with a target permanent) is now a post-deletion two-step agent CHOICE (F-6.8) — see G3.5-F68.
    ("Save: with no other permanent the card stays in the trash", SaveNoTargetStaysTrashed),
    ("Material Save: sources move to the bottom of another permanent's stack", MaterialSaveMovesSources),
    ("Training: the [Main] skill is OFFERED via the activated/window path and resolves (suspend + face-down deck-top under self)", TrainingResolvesThroughActivatedPath),
    ("Training: an already-suspended Digimon is NOT offered the [Main] <Training> move (CanActivateSuspendCostEffect gate)", TrainingSuspendedNotOffered),
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

// --- Training (C-24, live window/activated path) -------------------------

async Task TrainingResolvesThroughActivatedPath()
{
    // LIVE PATH witness: declare the fixture's [Main] OnDeclaration <Training> skill through
    // MainSkillActivateAction (-> ActivatedEffectResolver -> CardEffectFactory.TrainingEffect ActivateClass),
    // NOT the retired invented TrainAsync mutation.
    (EngineContext context, HeadlessEntityId digimon, HeadlessEntityId libTop) = SetupTrainer(suspended: false);

    var action = new MainSkillActivateAction();
    LegalAction? offered = FindActivateMain(action, context, P2, digimon);
    AssertTrue(offered is not null, "the [Main] <Training> skill is offered as an ActivateMain legal move (window/activated path)");
    AssertTrue(offered!.ActionType == HeadlessActionTypes.ActivateMain, "the offered move is ActivateMain (player-declared activated path)");

    ActionProcessResult result = await action.ProcessAsync(offered!, context);
    AssertTrue(result.IsSuccess, "declaring <Training> resolves through the ActivateClass");

    // AS-IS TrainingEffect body (Training.cs): suspend self (SuspendPermanentsClass.Tap cost), then place the
    // deck's top card FACE-DOWN as this Digimon's bottom digivolution source (AddDigivolutionCardsBottom isFacedown: true).
    AssertTrue(ReadFlag(context, digimon, DigivolutionStackHelpers.IsSuspendedKey), "self suspended as the training cost");
    AssertSequence(SourceIds(context, digimon), libTop.Value);                     // deck top placed under self
    AssertFalse(InZone(context, P2, ChoiceZone.Library, libTop), "deck top left the library into the stack");
    AssertTrue(ReadFlag(context, libTop, "isFlipped"), "the placed source is FACE-DOWN (AS-IS isFacedown: true)");
}

async Task TrainingSuspendedNotOffered()
{
    // The AS-IS CanActivateSuspendCostEffect gate (Training's CanActivateCondition) fails for an already-suspended
    // Digimon, so CanUse(null) is false and CanDeclareAt does NOT surface the [Main] <Training> move — the window
    // path correctly withholds the illegal declaration (and the retired gate half no longer fires it either).
    (EngineContext context, HeadlessEntityId digimon, HeadlessEntityId libTop) = SetupTrainer(suspended: true);

    var action = new MainSkillActivateAction();
    LegalAction? offered = FindActivateMain(action, context, P2, digimon);
    AssertTrue(offered is null, "an already-suspended Digimon's <Training> is NOT offered (suspend-cost gate)");
    AssertTrue(InZone(context, P2, ChoiceZone.Library, libTop), "deck top untouched");
}

// Places a battle-area trainer (fixture TfxMainTraining) with a single deck-top card. `suspended` seeds the
// permanent's suspend state (the training cost gate).
(EngineContext, HeadlessEntityId, HeadlessEntityId) SetupTrainer(bool suspended)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 12);
    context.TurnController.Initialize(new[] { P1, P2 }, P2);
    // (harness) satisfy AS-IS ICardEffect.CanTrigger's DoneStartGame gate (phase not None/Setup) so the
    // declared skill is not silently withheld.
    context.TurnController.SetPhase(HeadlessPhase.Main);
    var cards = (CardDatabase)context.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId("TfxMainTraining"), "TfxMainTraining", "TR",
        new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));

    HeadlessEntityId digimon = new("2:battle:TR");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(digimon, new HeadlessEntityId("TfxMainTraining"), P2,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["isSuspended"] = suspended, ["canSuspend"] = true }));
    context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, digimon, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();

    cards.Upsert(new CardRecord(new HeadlessEntityId("DEF:TR-L"), "TR-L", "TR-L", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    HeadlessEntityId libTop = new("2:lib:top");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(libTop, new HeadlessEntityId("DEF:TR-L"), P2, Metadata: new Dictionary<string, object?>()));
    context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, libTop, ChoiceZone.None, ChoiceZone.Library)).GetAwaiter().GetResult();

    return (context, digimon, libTop);
}

LegalAction? FindActivateMain(MainSkillActivateAction action, EngineContext context, HeadlessPlayerId playerId, HeadlessEntityId permanent) =>
    action.GetLegalActions(context, playerId)
        .FirstOrDefault(a => a.Parameters.TryGetValue(HeadlessActionParameterKeys.CardId, out object? v) && Equals(v, permanent));

// --- Helpers -------------------------------------------------------------

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
