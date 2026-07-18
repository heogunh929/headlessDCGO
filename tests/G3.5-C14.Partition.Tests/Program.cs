// C-14 Partition (S4): when this Digimon WOULD leave the field by an effect (not battle), its controller plays
// one digivolution source per colour group as new permanents for free (AS-IS PartitionProcess). The card still
// leaves; DiscardEvoRoots trashes any remaining sources. (C-Del 3c-2b) The keyword fires ONLY through the AS-IS PRE
// cut-in window now (the invented DeletionReplacementGate firing-half is retired), so this suite drives the REAL
// ported <Partition> card BT16_025 ([Blue Lv.4]/[Green Lv.4]) through the universal effect-delete sink's
// would-be-deleted window. Step 1 is the OptionalEffect ("Will you use Partition?"); the AS-IS PartitionProcess then
// plays one source per colour group. Battle deletions offer nothing (AS-IS !IsByBattle).
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Partition opens a would-be-deleted choice with both colour groups present", PartitionOpensChoice),
    ("Partition plays one source per colour group (Blue + Green); the un-grouped Red is trashed", PartitionColourGroups),
    ("Declining Partition plays nothing and trashes every source", PartitionDeclineTrashesAll),
    ("Battle removal does not trigger Partition (AS-IS !IsByBattle)", PartitionNotOnBattleRemoval),
    ("An empty colour group means Partition is not offered (AS-IS CanActivateCondition)", PartitionGroupEmptyNotOffered),
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
    (DcgoMatch match, HeadlessEntityId holder, _, _, _) = await EffectDeletePartitioner();

    AssertTrue(InZone(match, P1, ChoiceZone.BattleArea, holder), "the holder is still on the field (PRE window, deletion deferred)");
    AssertTrue(match.Context.ChoiceController.Current.IsPending, "a would-be-deleted Partition choice is open");
    AssertTrue(ResolveActions(match, P1).Any(a => !a.Id.Value.EndsWith(":skip", StringComparison.Ordinal)), "partition option offered");
    AssertTrue(ResolveActions(match, P1).Any(a => a.Id.Value.EndsWith(":skip", StringComparison.Ordinal)), "the optional decline is offered (you MAY)");
}

// Activate Partition: the AS-IS process plays one source per colour group — Blue (group[0]) AND Green (group[1]) —
// for free; the un-grouped Red is trashed with the stack; the holder still leaves.
async Task PartitionColourGroups()
{
    (DcgoMatch match, HeadlessEntityId holder, HeadlessEntityId blue, HeadlessEntityId green, HeadlessEntityId red) = await EffectDeletePartitioner();

    LegalAction activate = ResolveActions(match, P1).Single(a => !a.Id.Value.EndsWith(":skip", StringComparison.Ordinal));
    await match.ApplyActionAsync(activate);
    await match.StepAsync();

    AssertTrue(InZone(match, P1, ChoiceZone.BattleArea, blue), "the Blue Lv.4 (group[0]) was played for free");
    AssertTrue(InZone(match, P1, ChoiceZone.BattleArea, green), "the Green Lv.4 (group[1]) was played for free");
    AssertFalse(InZone(match, P1, ChoiceZone.Trash, blue), "the played Blue source was NOT re-trashed");
    AssertFalse(InZone(match, P1, ChoiceZone.Trash, green), "the played Green source was NOT re-trashed");
    AssertFalse(SourceIds(match, holder).Contains(blue.Value), "the played Blue source was detached");
    AssertFalse(SourceIds(match, holder).Contains(green.Value), "the played Green source was detached");
    AssertTrue(InZone(match, P1, ChoiceZone.Trash, holder), "the deletion proceeded — the card left play");
    AssertTrue(InZone(match, P1, ChoiceZone.Trash, red), "the un-grouped Red Lv.3 source was trashed with the stack");
}

async Task PartitionDeclineTrashesAll()
{
    (DcgoMatch match, HeadlessEntityId holder, HeadlessEntityId blue, HeadlessEntityId green, HeadlessEntityId red) = await EffectDeletePartitioner();

    LegalAction skip = ResolveActions(match, P1).Single(a => a.Id.Value.EndsWith(":skip", StringComparison.Ordinal));
    await match.ApplyActionAsync(skip);
    await match.StepAsync();

    AssertFalse(match.Context.ChoiceController.Current.IsPending, "no choice remains after declining");
    AssertTrue(InZone(match, P1, ChoiceZone.Trash, holder), "the deletion proceeded — the card left play");
    AssertTrue(InZone(match, P1, ChoiceZone.Trash, blue) && InZone(match, P1, ChoiceZone.Trash, green) && InZone(match, P1, ChoiceZone.Trash, red),
        "every source was trashed (nothing played)");
    AssertFalse(InZone(match, P1, ChoiceZone.BattleArea, blue), "no source entered the battle area");
}

// A battle-deleted holder must NOT offer Partition (AS-IS !IsByBattle). Asserted on the window's collection.
async Task PartitionNotOnBattleRemoval()
{
    (DcgoMatch match, EngineContext ctx) = await StartedMatch();
    using var scope = AmbientMatchContext.Enter(ctx);
    HeadlessEntityId holder = HandCard(match, P1, 1);
    RetypeHolder(ctx, holder, "BT16_025");
    HeadlessEntityId blue = Source(ctx, P1, "c14bat-blue", "Blue", 4);
    HeadlessEntityId green = Source(ctx, P1, "c14bat-green", "Green", 4);

    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, holder, ChoiceZone.Hand, ChoiceZone.BattleArea));
    SetMetadata(match, holder, new Dictionary<string, object?>(StringComparer.Ordinal) { [DeletionReplacementGate.SourceIdsKey] = new[] { blue.Value, green.Value } });
    CardEffectRegistrar.RegisterCard(ctx, holder, P1);

    var effectPerm = new Permanent(ctx, holder, P1) { willBeRemoveField = true };
    var effectHt = CardEffectCommons.WhenPermanentWouldRemoveFieldCheckHashtable(new List<Permanent> { effectPerm }, cardEffect: null, battle: null);
    int effectCollected = AutoProcessing.GetSkillInfos(effectHt, EffectTiming.WhenRemoveField).Count;
    effectPerm.willBeRemoveField = false;
    AssertTrue(effectCollected >= 1, "an EFFECT would-be-delete collects Partition");

    var battlePerm = new Permanent(ctx, holder, P1) { willBeRemoveField = true };
    var battleHt = CardEffectCommons.WhenPermanentWouldRemoveFieldCheckHashtable(new List<Permanent> { battlePerm }, byBattleCause: true);
    int battleCollected = AutoProcessing.GetSkillInfos(battleHt, EffectTiming.WhenRemoveField).Count;
    battlePerm.willBeRemoveField = false;
    AssertEqual(0, battleCollected, "battle removal offers no Partition (AS-IS !IsByBattle)");
}

// One colour group has no candidate source (only Blue present, no Green) -> AS-IS CanActivatePartition fails ->
// no window opens.
async Task PartitionGroupEmptyNotOffered()
{
    (DcgoMatch match, EngineContext ctx) = await StartedMatch();
    HeadlessEntityId holder = HandCard(match, P1, 1);
    RetypeHolder(ctx, holder, "BT16_025");
    HeadlessEntityId blue = Source(ctx, P1, "c14e-blue", "Blue", 4);   // group[0] only; group[1] (Green) empty

    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, holder, ChoiceZone.Hand, ChoiceZone.BattleArea));
    SetMetadata(match, holder, new Dictionary<string, object?>(StringComparer.Ordinal) { [DeletionReplacementGate.SourceIdsKey] = new[] { blue.Value } });
    CardEffectRegistrar.RegisterCard(ctx, holder, P1);

    await DeleteByEffect(match, ctx, holder);

    AssertTrue(InZone(match, P1, ChoiceZone.Trash, holder), "holder swept to trash");
    AssertFalse(match.Context.ChoiceController.Current.IsPending, "no Partition choice when a colour group is empty");
    AssertTrue(InZone(match, P1, ChoiceZone.Trash, blue), "the lone source was trashed with the stack");
}

// --- Shared setup --------------------------------------------------------

// A real BT16_025 with [Blue Lv.4, Green Lv.4, Red Lv.3] sources, deleted by an effect through the sink; the PRE
// window opens (both colour groups have a candidate).
async Task<(DcgoMatch, HeadlessEntityId, HeadlessEntityId, HeadlessEntityId, HeadlessEntityId)> EffectDeletePartitioner()
{
    (DcgoMatch match, EngineContext ctx) = await StartedMatch();
    HeadlessEntityId holder = HandCard(match, P1, 1);
    RetypeHolder(ctx, holder, "BT16_025");
    HeadlessEntityId blue = Source(ctx, P1, "c14-blue", "Blue", 4);
    HeadlessEntityId green = Source(ctx, P1, "c14-green", "Green", 4);
    HeadlessEntityId red = Source(ctx, P1, "c14-red", "Red", 3);

    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, holder, ChoiceZone.Hand, ChoiceZone.BattleArea));
    SetMetadata(match, holder, new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [DeletionReplacementGate.SourceIdsKey] = new[] { blue.Value, green.Value, red.Value },
    });
    CardEffectRegistrar.RegisterCard(ctx, holder, P1);

    await DeleteByEffect(match, ctx, holder);
    return (match, holder, blue, green, red);
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
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = DcgoMatch.CreatePumpDriven(ctx, new EngineTrace());
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
    await StepOnceAsync(match);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, player));

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


// --- Phase driving (pump auto-flow, F62/alpha/EXEMPLAR-T1 precedent) ------
// Drive the pump's natural Active->Draw->Breeding->Main auto-flow to the player's main wait; the OLD
// AdvancePhase step currency is retired. Breeding/Mulligan decisions are declined; observed Main arrival
// is asserted (assertion strength unchanged).
static bool AtMainWaitOf(DcgoMatch match, HeadlessPlayerId player) =>
    match.Context.TurnController.Current.Phase == HeadlessPhase.Main
    && match.Context.TurnController.Current.TurnPlayerId == player
    && !match.HasPendingChoice() && !match.IsTerminal();

static async Task DriveUntilAsync(DcgoMatch match, Func<DcgoMatch, bool> condition)
{
    for (int i = 0; i < 96 && !condition(match); i++)
    {
        if (match.HasPendingChoice())
        {
            bool decline = match.Context.ChoiceController.PendingRequest!.Type is ChoiceType.BreedingDecision or ChoiceType.Mulligan;
            await ResolvePendingAsync(match, skip: decline);
        }
        else await StepOnceAsync(match);
    }
    if (!condition(match))
    {
        HeadlessTurnState t = match.Context.TurnController.Current;
        throw new InvalidOperationException(
            $"pump drive did not reach the expected state - phase:{t.Phase} turn:{t.TurnNumber} player:{t.TurnPlayerId} " +
            $"choice:{match.Context.ChoiceController.PendingRequest?.Type.ToString() ?? "<none>"} pending:{match.HasPendingChoice()} terminal:{match.IsTerminal()}");
    }
}

static async Task ResolvePendingAsync(DcgoMatch match, bool skip)
{
    HeadlessPlayerId chooser = match.Context.ChoiceController.PendingRequest!.PlayerId;
    LegalAction? action;
    using (AmbientMatchContext.Enter(match.Context))
    {
        action = match.GetLegalActions(chooser).FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice
                && a.Id.Value.EndsWith(":skip", StringComparison.Ordinal) == skip)
            ?? match.GetLegalActions(chooser).FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice);
    }
    if (action is null) throw new InvalidOperationException("no ResolveChoice lane for the pending request");
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.ApplyActionAsync(action);
    await match.StepAsync();
    await match.StepAsync();
}

static async Task StepOnceAsync(DcgoMatch match)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.StepAsync();
}
