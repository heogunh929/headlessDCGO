// C-1 witness — the Decode/Partition PRE (would-be-deleted) window driven by the REAL ported cards through the
// enter-play registrar path (CardEffectRegistrar.RegisterCard), NOT synthetic metadata flags. Proves the golf's
// load-bearing claim: the C-1 engine actually EVALUATES the per-card source predicate carried on the keyword
// grant when choosing which digivolution sources may be played for free.
//
//   * BT19_024 <Decode> ([Blue Lv.4]) — DecodeSelfEffect(sourceCondition = Blue && HasLevel && IsLevel4). The
//     PRE candidate filter must offer ONLY Blue-Lv.4 sources; a non-matching source (Red Lv.3) is excluded, and a
//     stack whose only source fails the condition offers no Decode at all. Battle deletions offer nothing (!IsByBattle).
//   * BT16_025 <Partition> ([Blue Lv.4]/[Green Lv.4]) — PartitionSelfEffect with two colour groups; pick #1 draws
//     group[0] (Blue), pick #2 group[1] (Green); an un-grouped source (Red) is trashed with the stack.
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("BT19_024 (a) effect deletion: Decode offers ONLY the Blue Lv.4 source (sourceCondition filters), plays it, card still leaves", Bt19DecodeFiltersAndPlays),
    ("BT19_024 (b) a stack whose only source fails the sourceCondition (Red Lv.3) offers no Decode", Bt19DecodeNoMatchNotOffered),
    ("BT19_024 (c) a battle deletion offers no Decode (AS-IS !IsByBattle)", Bt19DecodeBattleOffersNothing),
    ("BT16_025 (d) Partition plays one source per colour group (Blue then Green); the un-grouped Red is trashed", Bt16PartitionPerColourGroup),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex)
    {
        failures.Add(test.Name);
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
    }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// (a) A real BT19_024 on the field with [Blue Lv.4 Digimon, Red Lv.3 Digimon] sources. Registering it places the
// live Decode grant (with the Blue-Lv.4 sourceCondition). An effect deletion opens the PRE window; only the Blue
// Lv.4 source is a legal Decode pick, it is played for free, and the deletion still proceeds.
async Task Bt19DecodeFiltersAndPlays()
{
    (DcgoMatch match, EngineContext ctx) = await StartedMatch();
    HeadlessEntityId holder = HandCard(match, P2, 1);
    RetypeHolder(ctx, holder, "BT19_024");
    HeadlessEntityId blue = Source(ctx, P2, "b19a-blue", "Blue", 4);
    HeadlessEntityId red = Source(ctx, P2, "b19a-red", "Red", 3);

    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, holder, ChoiceZone.Hand, ChoiceZone.BattleArea));
    SetMetadata(match, holder, new() { [DeletionReplacementGate.SourceIdsKey] = new[] { blue.Value, red.Value } });
    AssertTrue(CardEffectRegistrar.RegisterCard(ctx, holder, P2), "BT19_024 registers via the dispatch (real card path)");

    await DeleteByEffect(match, ctx, holder);
    AssertTrue(InZone(match, P2, ChoiceZone.BattleArea, holder), "PRE window: holder still on the field");

    LegalAction activate = ResolveActions(match, P2).Single(a =>
        a.Id.Value.Contains("#decode", StringComparison.Ordinal) &&
        !a.Id.Value.Contains(blue.Value, StringComparison.Ordinal) &&
        !a.Id.Value.Contains(red.Value, StringComparison.Ordinal));
    await match.ApplyActionAsync(activate);
    await match.StepAsync();

    // The load-bearing assertion: the sourceCondition (Blue Lv.4) filters the pick list — Blue is offered, Red is not.
    LegalAction[] picks = ResolveActions(match, P2).Where(a =>
        a.Id.Value.Contains(blue.Value, StringComparison.Ordinal) || a.Id.Value.Contains(red.Value, StringComparison.Ordinal)).ToArray();
    AssertTrue(picks.Any(a => a.Id.Value.Contains(blue.Value, StringComparison.Ordinal)), "Decode offers the Blue Lv.4 source");
    AssertFalse(picks.Any(a => a.Id.Value.Contains(red.Value, StringComparison.Ordinal)), "Decode does NOT offer the Red Lv.3 source (sourceCondition filters)");

    await match.ApplyActionAsync(picks.Single(a => a.Id.Value.Contains(blue.Value, StringComparison.Ordinal)));
    await match.StepAsync();

    AssertTrue(InZone(match, P2, ChoiceZone.BattleArea, blue), "the Blue Lv.4 source was played for free");
    AssertTrue(InZone(match, P2, ChoiceZone.Trash, holder), "the deletion proceeded — the card left play");
    AssertTrue(InZone(match, P2, ChoiceZone.Trash, red), "the un-played Red source was trashed with the stack");
    AssertFalse(InZone(match, P2, ChoiceZone.Trash, blue), "the played source was NOT trashed");
}

// (b) The only source is a Red Lv.3 Digimon (fails the Blue-Lv.4 sourceCondition). The PRE window offers no Decode
// even though the keyword is live — the card is deleted and its source trashed.
async Task Bt19DecodeNoMatchNotOffered()
{
    (DcgoMatch match, EngineContext ctx) = await StartedMatch();
    HeadlessEntityId holder = HandCard(match, P2, 1);
    RetypeHolder(ctx, holder, "BT19_024");
    HeadlessEntityId red = Source(ctx, P2, "b19b-red", "Red", 3);

    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, holder, ChoiceZone.Hand, ChoiceZone.BattleArea));
    SetMetadata(match, holder, new() { [DeletionReplacementGate.SourceIdsKey] = new[] { red.Value } });
    CardEffectRegistrar.RegisterCard(ctx, holder, P2);

    await DeleteByEffect(match, ctx, holder);

    AssertFalse(ResolveActions(match, P2).Any(a => a.Id.Value.Contains("#decode", StringComparison.Ordinal)),
        "no Decode option when no source passes the Blue-Lv.4 sourceCondition");
    AssertTrue(InZone(match, P2, ChoiceZone.Trash, holder), "the card was deleted");
    AssertTrue(InZone(match, P2, ChoiceZone.Trash, red), "its non-matching source was trashed");
}

// (c) A battle deletion is effect-deletion-only for Decode (AS-IS !IsByBattle). Asserted on the shared gate both
// deletion paths consult: effect (byBattle:false) offers Decode, battle (byBattle:true) does not.
async Task Bt19DecodeBattleOffersNothing()
{
    (DcgoMatch match, EngineContext ctx) = await StartedMatch();
    HeadlessEntityId holder = HandCard(match, P2, 1);
    RetypeHolder(ctx, holder, "BT19_024");
    HeadlessEntityId blue = Source(ctx, P2, "b19c-blue", "Blue", 4);

    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, holder, ChoiceZone.Hand, ChoiceZone.BattleArea));
    SetMetadata(match, holder, new() { [DeletionReplacementGate.SourceIdsKey] = new[] { blue.Value } });
    CardEffectRegistrar.RegisterCard(ctx, holder, P2);

    ctx.CardInstanceRepository.TryGetInstance(holder, out CardInstanceRecord? record);
    var zones = (IZoneStateReader)ctx.ZoneMover;
    AssertTrue(DeletionReplacementTiming.PreOptions(ctx.CardInstanceRepository, zones, record!, byBattle: false, ctx.EffectRegistry).Contains("decode"),
        "an EFFECT deletion offers Decode");
    AssertFalse(DeletionReplacementTiming.PreOptions(ctx.CardInstanceRepository, zones, record!, byBattle: true, ctx.EffectRegistry).Contains("decode"),
        "a BATTLE deletion offers no Decode");
}

// (d) A real BT16_025 with [Blue Lv.4, Green Lv.4, Red Lv.3] sources. Partition plays one per colour group — pick
// #1 offers only the Blue (group[0]), pick #2 only the Green (group[1]); the un-grouped Red is trashed.
async Task Bt16PartitionPerColourGroup()
{
    (DcgoMatch match, EngineContext ctx) = await StartedMatch();
    HeadlessEntityId holder = HandCard(match, P1, 1);
    RetypeHolder(ctx, holder, "BT16_025");
    HeadlessEntityId blue = Source(ctx, P1, "b16d-blue", "Blue", 4);
    HeadlessEntityId green = Source(ctx, P1, "b16d-green", "Green", 4);
    HeadlessEntityId red = Source(ctx, P1, "b16d-red", "Red", 3);

    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, holder, ChoiceZone.Hand, ChoiceZone.BattleArea));
    SetMetadata(match, holder, new() { [DeletionReplacementGate.SourceIdsKey] = new[] { blue.Value, green.Value, red.Value } });
    AssertTrue(CardEffectRegistrar.RegisterCard(ctx, holder, P1), "BT16_025 registers via the dispatch (real card path)");

    await DeleteByEffect(match, ctx, holder);
    AssertTrue(InZone(match, P1, ChoiceZone.BattleArea, holder), "PRE window: holder still on the field");

    LegalAction activate = ResolveActions(match, P1).Single(a =>
        a.Id.Value.Contains("#partition", StringComparison.Ordinal) &&
        !a.Id.Value.Contains(blue.Value, StringComparison.Ordinal) &&
        !a.Id.Value.Contains(green.Value, StringComparison.Ordinal) &&
        !a.Id.Value.Contains(red.Value, StringComparison.Ordinal));
    await match.ApplyActionAsync(activate);
    await match.StepAsync();

    // pick #1 = group[0] (Blue) only.
    LegalAction[] pick1 = ResolveActions(match, P1).Where(a =>
        a.Id.Value.Contains(blue.Value, StringComparison.Ordinal) || a.Id.Value.Contains(green.Value, StringComparison.Ordinal) ||
        a.Id.Value.Contains(red.Value, StringComparison.Ordinal)).ToArray();
    AssertTrue(pick1.Any(a => a.Id.Value.Contains(blue.Value, StringComparison.Ordinal)), "pick #1 offers the Blue Lv.4 (group[0])");
    AssertFalse(pick1.Any(a => a.Id.Value.Contains(green.Value, StringComparison.Ordinal)), "pick #1 does NOT offer group[1] (Green)");
    AssertFalse(pick1.Any(a => a.Id.Value.Contains(red.Value, StringComparison.Ordinal)), "pick #1 does NOT offer the un-grouped Red");
    await match.ApplyActionAsync(pick1.Single(a => a.Id.Value.Contains(blue.Value, StringComparison.Ordinal)));
    await match.StepAsync();

    // pick #2 = group[1] (Green) only.
    LegalAction pick2 = ResolveActions(match, P1).Single(a => a.Id.Value.Contains(green.Value, StringComparison.Ordinal));
    await match.ApplyActionAsync(pick2);
    await match.StepAsync();

    AssertTrue(InZone(match, P1, ChoiceZone.BattleArea, blue), "the Blue pick was played");
    AssertTrue(InZone(match, P1, ChoiceZone.BattleArea, green), "the Green pick was played");
    AssertTrue(InZone(match, P1, ChoiceZone.Trash, holder), "the deletion proceeded — the card left play");
    AssertTrue(InZone(match, P1, ChoiceZone.Trash, red), "the un-grouped Red source was trashed with the stack");
}

// --- Shared setup --------------------------------------------------------

async Task DeleteByEffect(DcgoMatch match, EngineContext ctx, HeadlessEntityId cardId)
{
    var sink = new MatchStateMutationSink(ctx.CardInstanceRepository, log: null, ctx.ZoneMover, memory: null, ctx.EffectRegistry);
    sink.Apply(new EffectMutation(MatchStateMutationSink.DeleteKind, new HeadlessEntityId("deleter"),
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = cardId.Value }));
    await sink.FlushAsync();
    await match.StepAsync();
}

// Re-point a match-dealt hand card at the ported card's number so RegisterCard dispatches its real effect class.
void RetypeHolder(EngineContext ctx, HeadlessEntityId holder, string cardNumber)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"def:{cardNumber}");
    cards.Upsert(new CardRecord(defId, cardNumber, cardNumber,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["colors"] = new[] { "Blue" }, ["level"] = 6, ["dp"] = 5000 }, CardType: "Digimon"));
    if (!ctx.CardInstanceRepository.TryGetInstance(holder, out CardInstanceRecord? record) || record is null)
        throw new InvalidOperationException($"missing {holder}");
    ctx.CardInstanceRepository.Upsert(record with { DefinitionId = defId });
}

HeadlessEntityId Source(EngineContext ctx, HeadlessPlayerId owner, string tag, string colour, int level)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"def:{tag}");
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["colors"] = new[] { colour }, ["level"] = level, ["dp"] = 3000 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}-{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner));
    return id;
}

async Task<(DcgoMatch, EngineContext)> StartedMatch()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 73);
    var cards = (CardDatabase)ctx.CardRepository;
    for (int i = 1; i <= 12; i++)
    {
        cards.Upsert(Digimon($"P1-M{i:D2}"));
        cards.Upsert(Digimon($"P2-M{i:D2}"));
    }

    DcgoMatch match = new(ctx);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") }, firstPlayerId: P1, shuffleDecks: false, shuffleDigitamaDecks: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 73, setup: setup));
    await AdvanceToMainAsync(match, P1);
    return (match, ctx);
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

void SetMetadata(DcgoMatch match, HeadlessEntityId cardId, Dictionary<string, object?> values)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
        throw new InvalidOperationException($"Missing {cardId}.");
    Dictionary<string, object?> metadata = new(record.Metadata, StringComparer.Ordinal);
    foreach (KeyValuePair<string, object?> pair in values) metadata[pair.Key] = pair.Value;
    match.Context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
}

bool InZone(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)match.Context.ZoneMover).GetCards(player, zone).Contains(cardId);

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
