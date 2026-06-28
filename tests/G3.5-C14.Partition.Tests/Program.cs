// C-14 Partition (S4): when this Digimon leaves the field by an effect (not battle, >= 2 sources), its
// controller plays TWO of its digivolution sources as new permanents for free (AS-IS PartitionProcess: one
// source per colour group). Ported onto the F-6.8 POST window as a repeated single-select (2 picks) reusing
// the Decode play-for-free primitive. Engine: DeletionReplacementTiming PartitionOption +
// DeletionReplacementGate.TryPartitionPlaySourceAsync; grant GrantPartition -> hasPartition.
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
HeadlessEntityId DigimonDef = new("def:partition-digimon");

var tests = new (string Name, Func<Task> Body)[]
{
    ("Partition opens a post-removal choice with >= 2 sources", PartitionOpensChoice),
    ("Partition is not offered with a single source", PartitionNeedsTwoSources),
    ("Partition plays two chosen sources to the battle area for free", PartitionPlaysTwoSources),
    ("Battle removal does not trigger Partition", PartitionNotOnBattleRemoval),
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

async Task PartitionOpensChoice()
{
    (DcgoMatch match, HeadlessEntityId holder, _) = await EffectDeletePartitioner(sourceCount: 3);

    AssertTrue(InZone(match, P1, ChoiceZone.Trash, holder), "the holder is in the trash");
    AssertTrue(match.Context.ChoiceController.Current.IsPending, "a post-removal Partition choice is open");
    AssertEqual(ChoiceType.DeletionReplacement, match.Context.ChoiceController.PendingRequest!.Type, "choice type");
    AssertTrue(ResolveActions(match, P1).Any(a => a.Id.Value.Contains("#partition", StringComparison.Ordinal)), "partition option offered");
}

async Task PartitionNeedsTwoSources()
{
    (DcgoMatch match, HeadlessEntityId holder, _) = await EffectDeletePartitioner(sourceCount: 1);

    AssertTrue(InZone(match, P1, ChoiceZone.Trash, holder), "holder swept to trash");
    AssertFalse(ResolveActions(match, P1).Any(a => a.Id.Value.Contains("#partition", StringComparison.Ordinal)),
        "no partition option with a single source");
}

async Task PartitionPlaysTwoSources()
{
    (DcgoMatch match, HeadlessEntityId holder, HeadlessEntityId[] sources) = await EffectDeletePartitioner(sourceCount: 3);

    // Step 1: activate partition (candidate "{holder}#partition", no source segment).
    LegalAction activate = ResolveActions(match, P1).Single(a =>
        a.Id.Value.Contains("#partition", StringComparison.Ordinal) && sources.All(src => !a.Id.Value.Contains(src.Value, StringComparison.Ordinal)));
    await match.ApplyActionAsync(activate);
    await match.StepAsync();   // step-2 (first source) opens

    LegalAction pick1 = ResolveActions(match, P1).First(a => sources.Any(src => a.Id.Value.Contains(src.Value, StringComparison.Ordinal)));
    HeadlessEntityId first = sources.Single(src => pick1.Id.Value.Contains(src.Value, StringComparison.Ordinal));
    await match.ApplyActionAsync(pick1);
    await match.StepAsync();   // repeated: step-2 (second source) opens

    AssertTrue(match.Context.ChoiceController.Current.IsPending, "the second partition pick is open");
    LegalAction pick2 = ResolveActions(match, P1).First(a =>
        sources.Any(src => src != first && a.Id.Value.Contains(src.Value, StringComparison.Ordinal)));
    HeadlessEntityId second = sources.Single(src => src != first && pick2.Id.Value.Contains(src.Value, StringComparison.Ordinal));
    await match.ApplyActionAsync(pick2);
    await match.StepAsync();

    AssertTrue(InZone(match, P1, ChoiceZone.BattleArea, first), "first chosen source played to the battle area");
    AssertTrue(InZone(match, P1, ChoiceZone.BattleArea, second), "second chosen source played to the battle area");
    AssertTrue(ReadFlag(match, holder, DeletionReplacementGate.PartitionedKey), "partitioned marker stamped");
    string[] remaining = SourceIds(match, holder);
    AssertFalse(remaining.Contains(first.Value), "first source detached from the dead card");
    AssertFalse(remaining.Contains(second.Value), "second source detached from the dead card");
}

async Task PartitionNotOnBattleRemoval()
{
    EngineContext context = await NewMatchContext();
    DcgoMatch match = await StartedMatch(context);

    HeadlessEntityId holder = HandCard(match, P1, 1);
    HeadlessEntityId[] sources = MakeSources(context, 2);
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, holder, ChoiceZone.Hand, ChoiceZone.BattleArea));
    SetMetadata(match, holder, new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [DeletionReplacementGate.HasPartitionKey] = true,
        [DeletionReplacementGate.DeletedByBattleKey] = true,
        [DeletionReplacementGate.SourceIdsKey] = sources.Select(s => s.Value).ToArray(),
    });
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, holder, ChoiceZone.BattleArea, ChoiceZone.Trash, FaceUp: true));
    await match.StepAsync();

    AssertFalse(ResolveActions(match, P1).Any(a => a.Id.Value.Contains("#partition", StringComparison.Ordinal)),
        "battle removal offers no Partition option");
}

// --- Shared setup --------------------------------------------------------

async Task<(DcgoMatch, HeadlessEntityId, HeadlessEntityId[])> EffectDeletePartitioner(int sourceCount)
{
    EngineContext context = await NewMatchContext();
    DcgoMatch match = await StartedMatch(context);

    HeadlessEntityId holder = HandCard(match, P1, 1);
    HeadlessEntityId[] sources = MakeSources(context, sourceCount);
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, holder, ChoiceZone.Hand, ChoiceZone.BattleArea));
    SetMetadata(match, holder, new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [DeletionReplacementGate.HasPartitionKey] = true,
        [DeletionReplacementGate.SourceIdsKey] = sources.Select(s => s.Value).ToArray(),
    });

    var sink = new MatchStateMutationSink(context.CardInstanceRepository, log: null, context.ZoneMover, memory: null, context.EffectRegistry);
    sink.Apply(new EffectMutation(MatchStateMutationSink.DeleteKind, new HeadlessEntityId("deleter"),
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = holder.Value }));
    await sink.FlushAsync();
    await match.StepAsync();
    return (match, holder, sources);
}

HeadlessEntityId[] MakeSources(EngineContext context, int count)
{
    var ids = new HeadlessEntityId[count];
    for (int i = 0; i < count; i++)
    {
        ids[i] = new HeadlessEntityId($"P1-PartSrc{i + 1:D2}");
        context.CardInstanceRepository.Upsert(new CardInstanceRecord(ids[i], DigimonDef, P1));
    }

    return ids;
}

async Task<EngineContext> NewMatchContext()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 73);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    cards.Upsert(new CardRecord(DigimonDef, "PART-DIGI", "Partition source", new Dictionary<string, object?>(), CardType: "Digimon"));
    return context;
}

async Task<DcgoMatch> StartedMatch(EngineContext context)
{
    DcgoMatch match = new(context);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") }, firstPlayerId: P1, shuffleDecks: false, shuffleDigitamaDecks: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 73, setup: setup));
    await AdvanceToMainAsync(match, P1);
    return match;
}

IEnumerable<LegalAction> ResolveActions(DcgoMatch match, HeadlessPlayerId player) =>
    match.GetLegalActions(player).Where(a => a.ActionType == HeadlessActionTypes.ResolveChoice);

HeadlessEntityId HandCard(DcgoMatch match, HeadlessPlayerId player, int index)
{
    HeadlessEntityId[] hand = ((IZoneStateReader)match.Context.ZoneMover)
        .GetCards(player, ChoiceZone.Hand).OrderBy(id => id.Value, StringComparer.Ordinal).ToArray();
    if (hand.Length < index) throw new InvalidOperationException($"hand short: {hand.Length} < {index}");
    return hand[index - 1];
}

async Task AdvanceToMainAsync(DcgoMatch match, HeadlessPlayerId player)
{
    for (var attempt = 0; attempt < 10 && match.GetObservation().Turn.Phase != HeadlessPhase.Main; attempt++)
    {
        LegalAction advance = match.GetLegalActions(player).Single(a => a.ActionType == HeadlessActionTypes.AdvancePhase);
        await match.ApplyActionAsync(advance);
        await match.StepAsync();
    }

    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "advance to main");
}

void SetMetadata(DcgoMatch match, HeadlessEntityId cardId, IReadOnlyDictionary<string, object?> values)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
        throw new InvalidOperationException($"Missing {cardId}.");
    Dictionary<string, object?> metadata = new(record.Metadata, StringComparer.Ordinal);
    foreach (KeyValuePair<string, object?> pair in values) metadata[pair.Key] = pair.Value;
    match.Context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
}

bool InZone(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)match.Context.ZoneMover).GetCards(player, zone).Contains(cardId);

bool ReadFlag(DcgoMatch match, HeadlessEntityId cardId, string key) =>
    match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? r) && r is not null
        && r.Metadata.TryGetValue(key, out object? raw) && raw is bool b && b;

string[] SourceIds(DcgoMatch match, HeadlessEntityId cardId) =>
    match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? r) && r is not null
        && r.Metadata.TryGetValue("sourceIds", out object? raw) && raw is IEnumerable<string> ids
        ? ids.ToArray() : Array.Empty<string>();

static CardRecord Digimon(string id) =>
    new(new HeadlessEntityId(id), id, $"{id} Card", new Dictionary<string, object?>(), CardType: "Digimon");

static PlayerDeckSetup Deck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

static void AssertTrue(bool value, string label) { if (!value) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertFalse(bool value, string label) { if (value) throw new InvalidOperationException($"{label}: expected false."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"{label}: expected '{expected}', actual '{actual}'.");
}
