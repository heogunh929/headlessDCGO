// C-14 Partition (S4): when this Digimon leaves the field by an effect (not battle, >= 2 sources), its
// controller plays TWO of its digivolution sources as new permanents for free (AS-IS PartitionProcess: one
// source per colour group). Ported onto the F-6.8 POST window as a repeated single-select (2 picks) reusing
// the Decode play-for-free primitive. Engine: DeletionReplacementTiming PartitionOption +
// DeletionReplacementGate.TryPartitionPlaySourceAsync; grant GrantPartition -> hasPartition.
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory.KeyWordEffects;
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
    ("(A4) colour groups: pick #1 = group[0] only, pick #2 = group[1] only (BT16_012 shape)", PartitionColourGroups),
    ("(A4) an empty colour group means Partition is not offered (AS-IS CanActivateCondition)", PartitionGroupEmptyNotOffered),
    ("(A4) mutual exclusion: group 2's sole candidate is reserved from pick #1 (AS-IS pre-adjust)", PartitionMutualExclusion),
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

// (A4) AS-IS Partition.cs: the two PartitionConditions define colour group 1 ([0]) and group 2 ([1]);
// activation needs BOTH groups non-empty and one source is played from EACH group. The grant is the
// KEYWORD binding (PartitionSelfEffect + conditions) — no metadata flag — so this also exercises the
// sink's deletion-time keyword/conditions snapshot.

async Task PartitionColourGroups()
{
    var red1 = ("RED1", new[] { "Red" });
    var red2 = ("RED2", new[] { "Red" });
    var yellow = ("YEL1", new[] { "Yellow" });
    var blue = ("BLU1", new[] { "Blue" });
    (DcgoMatch match, HeadlessEntityId holder, Dictionary<string, HeadlessEntityId> src) =
        await EffectDeleteConditionedPartitioner(new[] { red1, red2, yellow, blue },
            new PartitionCondition(4, "Red"), new PartitionCondition(4, "Yellow"));

    LegalAction activate = ResolveActions(match, P1).Single(a =>
        a.Id.Value.Contains("#partition", StringComparison.Ordinal) && src.Values.All(s => !a.Id.Value.Contains(s.Value, StringComparison.Ordinal)));
    await match.ApplyActionAsync(activate);
    await match.StepAsync();

    var pick1 = ResolveActions(match, P1).Where(a => src.Values.Any(s => a.Id.Value.Contains(s.Value, StringComparison.Ordinal))).ToArray();
    AssertTrue(pick1.Any(a => a.Id.Value.Contains(src["RED1"].Value, StringComparison.Ordinal)), "pick #1 offers Red Lv4");
    AssertFalse(pick1.Any(a => a.Id.Value.Contains(src["YEL1"].Value, StringComparison.Ordinal)), "pick #1 does NOT offer Yellow (group[0] only)");
    AssertFalse(pick1.Any(a => a.Id.Value.Contains(src["BLU1"].Value, StringComparison.Ordinal)), "pick #1 does NOT offer Blue (no group)");

    await match.ApplyActionAsync(pick1.First(a => a.Id.Value.Contains(src["RED1"].Value, StringComparison.Ordinal)));
    await match.StepAsync();

    var pick2 = ResolveActions(match, P1).Where(a => src.Values.Any(s => a.Id.Value.Contains(s.Value, StringComparison.Ordinal))).ToArray();
    AssertTrue(pick2.Any(a => a.Id.Value.Contains(src["YEL1"].Value, StringComparison.Ordinal)), "pick #2 offers Yellow (group[1])");
    AssertFalse(pick2.Any(a => a.Id.Value.Contains(src["RED2"].Value, StringComparison.Ordinal)), "pick #2 does NOT offer the other Red");

    await match.ApplyActionAsync(pick2.First(a => a.Id.Value.Contains(src["YEL1"].Value, StringComparison.Ordinal)));
    await match.StepAsync();

    AssertTrue(InZone(match, P1, ChoiceZone.BattleArea, src["RED1"]), "the Red pick was played");
    AssertTrue(InZone(match, P1, ChoiceZone.BattleArea, src["YEL1"]), "the Yellow pick was played");
}

async Task PartitionGroupEmptyNotOffered()
{
    (DcgoMatch match, HeadlessEntityId holder, _) =
        await EffectDeleteConditionedPartitioner(new[] { ("RED1", new[] { "Red" }), ("RED2", new[] { "Red" }) },
            new PartitionCondition(4, "Red"), new PartitionCondition(4, "Yellow"));

    AssertTrue(InZone(match, P1, ChoiceZone.Trash, holder), "holder in trash");
    AssertFalse(ResolveActions(match, P1).Any(a => a.Id.Value.Contains("#partition", StringComparison.Ordinal)),
        "no Partition option when a colour group is empty");
}

async Task PartitionMutualExclusion()
{
    // A = Red only; B = Red AND Yellow (dual). Group Red = {A, B}, group Yellow = {B} -> B is reserved
    // for group 2, so pick #1 offers only A (AS-IS Except pre-adjust).
    (DcgoMatch match, HeadlessEntityId holder, Dictionary<string, HeadlessEntityId> src) =
        await EffectDeleteConditionedPartitioner(new[] { ("A", new[] { "Red" }), ("B", new[] { "Red", "Yellow" }) },
            new PartitionCondition(4, "Red"), new PartitionCondition(4, "Yellow"));

    LegalAction activate = ResolveActions(match, P1).Single(a =>
        a.Id.Value.Contains("#partition", StringComparison.Ordinal) && src.Values.All(s => !a.Id.Value.Contains(s.Value, StringComparison.Ordinal)));
    await match.ApplyActionAsync(activate);
    await match.StepAsync();

    var pick1 = ResolveActions(match, P1).Where(a => src.Values.Any(s => a.Id.Value.Contains(s.Value, StringComparison.Ordinal))).ToArray();
    AssertTrue(pick1.Any(a => a.Id.Value.Contains(src["A"].Value, StringComparison.Ordinal)), "pick #1 offers A");
    AssertFalse(pick1.Any(a => a.Id.Value.Contains(src["B"].Value, StringComparison.Ordinal)),
        "pick #1 reserves B for group 2 (mutual exclusion)");
}

async Task<(DcgoMatch, HeadlessEntityId, Dictionary<string, HeadlessEntityId>)> EffectDeleteConditionedPartitioner(
    (string Tag, string[] Colors)[] sourceSpecs, PartitionCondition group0, PartitionCondition group1)
{
    EngineContext context = await NewMatchContext();
    DcgoMatch match = await StartedMatch(context);

    HeadlessEntityId holder = HandCard(match, P1, 1);
    var cards = (CardDatabase)context.CardRepository;
    var src = new Dictionary<string, HeadlessEntityId>(StringComparer.Ordinal);
    foreach ((string tag, string[] colors) in sourceSpecs)
    {
        var defId = new HeadlessEntityId($"def:part-{tag}");
        cards.Upsert(new CardRecord(defId, tag, tag,
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["colors"] = colors, ["level"] = 4, ["dp"] = 3000 }, CardType: "Digimon"));
        var id = new HeadlessEntityId($"P1-Part-{tag}");
        context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, P1));
        src[tag] = id;
    }

    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, holder, ChoiceZone.Hand, ChoiceZone.BattleArea));
    SetMetadata(match, holder, new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [DeletionReplacementGate.SourceIdsKey] = src.Values.Select(s => s.Value).ToArray(),
    });
    // KEYWORD grant with the AS-IS condition pair — no HasPartitionKey metadata (snapshot path).
    context.EffectRegistry.Register(CardEffectFactory.PartitionSelfEffect(
        false, new CardSource(context, holder, P1), null,
        new[] { group0, group1 }).ToBinding($"part:{holder.Value}"));

    var sink = new MatchStateMutationSink(context.CardInstanceRepository, log: null, context.ZoneMover, memory: null, context.EffectRegistry);
    sink.Apply(new EffectMutation(MatchStateMutationSink.DeleteKind, new HeadlessEntityId("deleter"),
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = holder.Value }));
    await sink.FlushAsync();
    await match.StepAsync();
    return (match, holder, src);
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
