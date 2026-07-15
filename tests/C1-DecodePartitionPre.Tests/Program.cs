// C-1 (design item TODO-96) — Decode/Partition PRE (would-be-deleted) window, end-to-end through the REAL ported
// cards.
//
// AS-IS: Decode/Partition register at EffectTiming.WhenRemoveField (CanUse = IsExistOnBattleAreaDigimon &&
// CanTriggerWhenRemoveField && !IsByBattle). They fire in the would-be-deleted cut-in WHILE the card is still on the
// field with its stack attached, play source(s) for free (PlayPermanentCards payCost:false), and then DiscardEvoRoots
// trashes the REMAINING sources. Crucially they do NOT set willBeRemoveField=false — the deletion still proceeds
// (they are NOT survival replacements like Evade/Barrier).
//
// (C-Del 3c-2b) The invented DeletionReplacementGate firing-half is retired: the keywords fire ONLY through the AS-IS
// PRE cut-in window (opened by the universal effect-delete sink; step 1 = OptionalEffect "Will you use …?"). This
// suite drives BT19_024 <Decode> ([Blue Lv.4]) and BT16_025 <Partition> ([Blue Lv.4]/[Green Lv.4]):
// (a) an effect deletion plays a matching source, trashes the remainder, and the card still leaves; (b) a battle
// deletion offers neither keyword; (c) declining plays nothing and trashes every source; (d) Partition plays one per
// colour group and still deletes; (e) a stack with no source-condition match offers nothing.
using HeadlessDCGO.Engine.Assets.Scripts.Script;
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
    ("(a) effect deletion: PRE plays the matching source, trashes the remainder, and the card still leaves", EffectDeletionPrePlaysAndProceeds),
    ("(b) battle deletion offers neither Decode nor Partition (AS-IS !IsByBattle)", BattleDeletionOffersNeither),
    ("(c) declining Decode plays nothing and every source is trashed", DecliningTrashesAllSources),
    ("(d) Partition plays one source per colour group and the card still leaves", PartitionColourGroupThenDeletes),
    ("(e) a stack with no source-condition match offers no Decode", SourceConditionMismatchNotOffered),
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

// (a) Effect-delete a real BT19_024 <Decode> with [Blue Lv.4, Red Lv.3] sources. The PRE window opens with the holder
// STILL on the field; activating Decode + picking the Blue source plays it, the deletion proceeds, and the remaining
// (Red) source is trashed with the card.
async Task EffectDeletionPrePlaysAndProceeds()
{
    (DcgoMatch match, EngineContext ctx) = await StartedMatch();
    HeadlessEntityId holder = HandCard(match, P2, 1);
    RetypeHolder(ctx, holder, "BT19_024");
    HeadlessEntityId blue = Source(ctx, P2, "c1a-blue", "Blue", 4);
    HeadlessEntityId red = Source(ctx, P2, "c1a-red", "Red", 3);

    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, holder, ChoiceZone.Hand, ChoiceZone.BattleArea));
    SetMetadata(match, holder, new() { [DeletionReplacementGate.SourceIdsKey] = new[] { blue.Value, red.Value } });
    CardEffectRegistrar.RegisterCard(ctx, holder, P2);

    await DeleteByEffect(match, ctx, holder);

    AssertTrue(InZone(match, P2, ChoiceZone.BattleArea, holder), "PRE window: holder still on the field");
    LegalAction activate = ResolveActions(match, P2).Single(a => !a.Id.Value.EndsWith(":skip", StringComparison.Ordinal));
    await match.ApplyActionAsync(activate);
    await match.StepAsync();

    LegalAction pick = ResolveActions(match, P2).Single(a => a.Id.Value.Contains(blue.Value, StringComparison.Ordinal));
    await match.ApplyActionAsync(pick);
    await match.StepAsync();

    AssertTrue(InZone(match, P2, ChoiceZone.BattleArea, blue), "the matching source was played for free");
    AssertTrue(ReadFlag(match, blue, "enteredThisTurn"), "the played source is summoning-sick");
    AssertTrue(InZone(match, P2, ChoiceZone.Trash, holder), "the deletion proceeded — the card left play");
    AssertTrue(InZone(match, P2, ChoiceZone.Trash, red), "the remaining (unplayed) source was trashed with the stack");
    AssertFalse(InZone(match, P2, ChoiceZone.Trash, blue), "the played source was NOT re-trashed");
}

// (b) A battle deletion routes through BattleResolver, which threads the byBattle cause. Decode/Partition are
// effect-deletion only (AS-IS !IsByBattle), so the by-battle would-be-delete window collects NEITHER — while the same
// cards under an effect would-be-delete (byBattle:false) DO collect. Asserted on the window's GetSkillInfos.
async Task BattleDeletionOffersNeither()
{
    (DcgoMatch match, EngineContext ctx) = await StartedMatch();
    using var scope = AmbientMatchContext.Enter(ctx);

    HeadlessEntityId decoder = HandCard(match, P2, 1);
    RetypeHolder(ctx, decoder, "BT19_024");
    HeadlessEntityId dBlue = Source(ctx, P2, "c1b-dblue", "Blue", 4);
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, decoder, ChoiceZone.Hand, ChoiceZone.BattleArea));
    SetMetadata(match, decoder, new() { [DeletionReplacementGate.SourceIdsKey] = new[] { dBlue.Value } });
    CardEffectRegistrar.RegisterCard(ctx, decoder, P2);

    HeadlessEntityId partitioner = HandCard(match, P1, 1);
    RetypeHolder(ctx, partitioner, "BT16_025");
    HeadlessEntityId pBlue = Source(ctx, P1, "c1b-pblue", "Blue", 4);
    HeadlessEntityId pGreen = Source(ctx, P1, "c1b-pgreen", "Green", 4);
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, partitioner, ChoiceZone.Hand, ChoiceZone.BattleArea));
    SetMetadata(match, partitioner, new() { [DeletionReplacementGate.SourceIdsKey] = new[] { pBlue.Value, pGreen.Value } });
    CardEffectRegistrar.RegisterCard(ctx, partitioner, P1);

    AssertTrue(WindowCollects(ctx, decoder, P2, byBattle: false) >= 1, "an EFFECT deletion collects Decode");
    AssertEqual(0, WindowCollects(ctx, decoder, P2, byBattle: true), "a BATTLE deletion offers no Decode");
    AssertTrue(WindowCollects(ctx, partitioner, P1, byBattle: false) >= 1, "an EFFECT deletion collects Partition");
    AssertEqual(0, WindowCollects(ctx, partitioner, P1, byBattle: true), "a BATTLE deletion offers no Partition");
}

// (c) Declining the PRE Decode plays nothing; the deletion proceeds and EVERY source is trashed (AS-IS
// willBeRemoveField unchanged — Decode never cancels the deletion).
async Task DecliningTrashesAllSources()
{
    (DcgoMatch match, EngineContext ctx) = await StartedMatch();
    HeadlessEntityId holder = HandCard(match, P2, 1);
    RetypeHolder(ctx, holder, "BT19_024");
    HeadlessEntityId s0 = Source(ctx, P2, "c1c-0", "Blue", 4);
    HeadlessEntityId s1 = Source(ctx, P2, "c1c-1", "Blue", 4);

    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, holder, ChoiceZone.Hand, ChoiceZone.BattleArea));
    SetMetadata(match, holder, new() { [DeletionReplacementGate.SourceIdsKey] = new[] { s0.Value, s1.Value } });
    CardEffectRegistrar.RegisterCard(ctx, holder, P2);

    await DeleteByEffect(match, ctx, holder);
    LegalAction skip = ResolveActions(match, P2).Single(a => a.Id.Value.EndsWith(":skip", StringComparison.Ordinal));
    await match.ApplyActionAsync(skip);
    await match.StepAsync();

    AssertTrue(InZone(match, P2, ChoiceZone.Trash, holder), "the deletion proceeded — the card left play");
    AssertTrue(InZone(match, P2, ChoiceZone.Trash, s0) && InZone(match, P2, ChoiceZone.Trash, s1), "both sources trashed (nothing played)");
    AssertFalse(InZone(match, P2, ChoiceZone.BattleArea, s0) || InZone(match, P2, ChoiceZone.BattleArea, s1), "no source entered the battle area");
}

// (d) A real BT16_025 <Partition> plays one source per colour group (Blue + Green); both are played and the card
// still leaves (a 3rd, un-grouped Red source is trashed).
async Task PartitionColourGroupThenDeletes()
{
    (DcgoMatch match, EngineContext ctx) = await StartedMatch();
    HeadlessEntityId holder = HandCard(match, P1, 1);
    RetypeHolder(ctx, holder, "BT16_025");
    HeadlessEntityId blue = Source(ctx, P1, "c1d-blue", "Blue", 4);
    HeadlessEntityId green = Source(ctx, P1, "c1d-green", "Green", 4);
    HeadlessEntityId red = Source(ctx, P1, "c1d-red", "Red", 3);   // matches no group -> trashed with the stack

    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, holder, ChoiceZone.Hand, ChoiceZone.BattleArea));
    SetMetadata(match, holder, new() { [DeletionReplacementGate.SourceIdsKey] = new[] { blue.Value, green.Value, red.Value } });
    CardEffectRegistrar.RegisterCard(ctx, holder, P1);

    await DeleteByEffect(match, ctx, holder);
    AssertTrue(InZone(match, P1, ChoiceZone.BattleArea, holder), "PRE window: holder still on the field");

    LegalAction activate = ResolveActions(match, P1).Single(a => !a.Id.Value.EndsWith(":skip", StringComparison.Ordinal));
    await match.ApplyActionAsync(activate);
    await match.StepAsync();

    AssertTrue(InZone(match, P1, ChoiceZone.BattleArea, blue), "the Blue pick (group[0]) was played");
    AssertTrue(InZone(match, P1, ChoiceZone.BattleArea, green), "the Green pick (group[1]) was played");
    AssertTrue(InZone(match, P1, ChoiceZone.Trash, holder), "the deletion proceeded — the card left play");
    AssertTrue(InZone(match, P1, ChoiceZone.Trash, red), "the un-grouped source was trashed with the stack");
}

// (e) A BT19_024 <Decode> whose only source fails the Blue-Lv.4 condition offers no Decode; the card is deleted and
// the source is trashed.
async Task SourceConditionMismatchNotOffered()
{
    (DcgoMatch match, EngineContext ctx) = await StartedMatch();
    HeadlessEntityId holder = HandCard(match, P2, 1);
    RetypeHolder(ctx, holder, "BT19_024");
    HeadlessEntityId redSrc = Source(ctx, P2, "c1e-red", "Red", 3);   // not Blue Lv.4

    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, holder, ChoiceZone.Hand, ChoiceZone.BattleArea));
    SetMetadata(match, holder, new() { [DeletionReplacementGate.SourceIdsKey] = new[] { redSrc.Value } });
    CardEffectRegistrar.RegisterCard(ctx, holder, P2);

    await DeleteByEffect(match, ctx, holder);

    AssertFalse(match.Context.ChoiceController.Current.IsPending, "no Decode option when no source matches the condition");
    AssertTrue(InZone(match, P2, ChoiceZone.Trash, holder), "the card was deleted");
    AssertTrue(InZone(match, P2, ChoiceZone.Trash, redSrc), "its non-matching source was trashed");
}

// --- Shared setup --------------------------------------------------------

// The AS-IS would-be-delete window's collection over the card for a given cause (byBattle threads !IsByBattle).
int WindowCollects(EngineContext ctx, HeadlessEntityId holder, HeadlessPlayerId owner, bool byBattle)
{
    var perm = new Permanent(ctx, holder, owner) { willBeRemoveField = true };
    var ht = byBattle
        ? CardEffectCommons.WhenPermanentWouldRemoveFieldCheckHashtable(new List<Permanent> { perm }, byBattleCause: true)
        : CardEffectCommons.WhenPermanentWouldRemoveFieldCheckHashtable(new List<Permanent> { perm }, cardEffect: null, battle: null);
    int collected = AutoProcessing.GetSkillInfos(ht, EffectTiming.WhenRemoveField).Count;
    perm.willBeRemoveField = false;
    return collected;
}

async Task DeleteByEffect(DcgoMatch match, EngineContext ctx, HeadlessEntityId cardId)
{
    var sink = new MatchStateMutationSink(ctx.CardInstanceRepository, log: null, ctx.ZoneMover, memory: null, ctx.EffectRegistry, context: ctx);
    sink.Apply(new EffectMutation(MatchStateMutationSink.DeleteKind, new HeadlessEntityId("deleter"),
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = cardId.Value }));
    await sink.FlushAsync();
    await match.StepAsync();
}

async Task<(DcgoMatch, EngineContext)> StartedMatch()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 73, deferredChoice: true);
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
    (ctx.ChoiceProvider as DeferredChoiceProvider)?.CompleteResolution();
    return (match, ctx);
}

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

bool ReadFlag(DcgoMatch match, HeadlessEntityId cardId, string key) =>
    match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? r) && r is not null
        && r.Metadata.TryGetValue(key, out object? raw) && raw is bool b && b;

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
