// C-1 (design item TODO-96) — Decode/Partition PRE (would-be-deleted) window.
//
// AS-IS (re-derived): Decode/Partition register at EffectTiming.WhenRemoveField (Decode.cs:54 CanUseCondition
// = IsExistOnBattleAreaDigimon && CanTriggerWhenRemoveField && !IsByBattle; Partition CanTriggerPartition =
// WhenPermanentRemoveField && !IsByBattle && !IsOwnerEffect). They fire in the WhenRemoveField cut-in
// (CardController.cs:3690-3705) WHILE the card is still on the field with its stack attached, play source(s)
// for free (PlayPermanentCards payCost:false, activateETB:true), and then DiscardEvoRoots (Permanent.cs:106)
// trashes the REMAINING sources. Crucially Decode/Partition do NOT set willBeRemoveField=false — the deletion
// still proceeds (they are NOT survival replacements like Evade/Barrier).
//
// This suite asserts the PRE behaviour end-to-end: (a) an effect deletion plays a matching source, trashes the
// remainder, and the card itself still leaves; (b) a battle deletion offers neither keyword; (c) declining
// plays nothing and trashes every source; (d) Partition repeats per colour group and still deletes; (e) a
// stack with no candidate matching the source condition offers nothing.
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
HeadlessEntityId DigimonDef = new("def:c1-digimon");
HeadlessEntityId TamerDef = new("def:c1-tamer");

var tests = new (string Name, Func<Task> Body)[]
{
    ("(a) effect deletion: PRE plays the matching source, trashes the remainder, and the card still leaves", EffectDeletionPrePlaysAndProceeds),
    ("(b) battle deletion offers neither Decode nor Partition (AS-IS !IsByBattle)", BattleDeletionOffersNeither),
    ("(c) declining Decode plays nothing and every source is trashed", DecliningTrashesAllSources),
    ("(d) Partition plays one source per colour group and the card still leaves", PartitionColourGroupRepeatThenDeletes),
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

// (a) Effect-delete a hasDecode holder with [Digimon source, non-Digimon source]. The PRE window opens with the
// holder STILL on the field; activating Decode + picking the Digimon source plays it, the deletion proceeds,
// and the remaining (non-Digimon) source is trashed with the card.
async Task EffectDeletionPrePlaysAndProceeds()
{
    (DcgoMatch match, EngineContext ctx) = await StartedMatch();
    HeadlessEntityId holder = HandCard(match, P2, 1);
    HeadlessEntityId digimonSrc = new("P2-c1a-digi");
    HeadlessEntityId otherSrc = new("P2-c1a-tamer");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(digimonSrc, DigimonDef, P2));
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(otherSrc, TamerDef, P2));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, holder, ChoiceZone.Hand, ChoiceZone.BattleArea));
    SetMetadata(match, holder, new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [DeletionReplacementGate.HasDecodeKey] = true,
        [DeletionReplacementGate.SourceIdsKey] = new[] { digimonSrc.Value, otherSrc.Value },
    });

    await DeleteByEffect(match, ctx, holder);

    // PRE: the holder is still on the field while the deletion is deferred for the decision.
    AssertTrue(InZone(match, P2, ChoiceZone.BattleArea, holder), "PRE window: holder still on the field");
    LegalAction activate = ResolveActions(match, P2).Single(a =>
        a.Id.Value.Contains("#decode", StringComparison.Ordinal) &&
        !a.Id.Value.Contains(digimonSrc.Value, StringComparison.Ordinal) &&
        !a.Id.Value.Contains(otherSrc.Value, StringComparison.Ordinal));
    await match.ApplyActionAsync(activate);
    await match.StepAsync();

    LegalAction pick = ResolveActions(match, P2).Single(a => a.Id.Value.Contains(digimonSrc.Value, StringComparison.Ordinal));
    await match.ApplyActionAsync(pick);
    await match.StepAsync();

    AssertTrue(InZone(match, P2, ChoiceZone.BattleArea, digimonSrc), "the matching source was played for free");
    AssertTrue(ReadFlag(match, digimonSrc, "enteredThisTurn"), "the played source is summoning-sick");
    AssertTrue(InZone(match, P2, ChoiceZone.Trash, holder), "the deletion proceeded — the card left play");
    AssertTrue(InZone(match, P2, ChoiceZone.Trash, otherSrc), "the remaining (unplayed) source was trashed with the stack");
    AssertFalse(InZone(match, P2, ChoiceZone.Trash, digimonSrc), "the played source was NOT trashed");
    AssertTrue(ReadFlag(match, holder, DeletionReplacementGate.DecodedKey), "decoded marker stamped (single use)");
}

// (b) A battle deletion routes through BattleResolver, which asks HasPreOption(byBattle:true). Decode/Partition
// are effect-deletion only (AS-IS !IsByBattle), so the by-battle window offers neither — while the same record
// under an effect deletion (byBattle:false) does. Asserted directly on the shared gate both paths use.
async Task BattleDeletionOffersNeither()
{
    (DcgoMatch match, EngineContext ctx) = await StartedMatch();
    HeadlessEntityId holder = HandCard(match, P2, 1);
    HeadlessEntityId s0 = new("P2-c1b-0");
    HeadlessEntityId s1 = new("P2-c1b-1");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(s0, DigimonDef, P2));
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(s1, DigimonDef, P2));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, holder, ChoiceZone.Hand, ChoiceZone.BattleArea));
    SetMetadata(match, holder, new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [DeletionReplacementGate.HasDecodeKey] = true,
        [DeletionReplacementGate.HasPartitionKey] = true,
        [DeletionReplacementGate.SourceIdsKey] = new[] { s0.Value, s1.Value },
    });

    ctx.CardInstanceRepository.TryGetInstance(holder, out CardInstanceRecord? record);
    var zones = (IZoneStateReader)ctx.ZoneMover;

    var byEffect = DeletionReplacementTiming.PreOptions(ctx.CardInstanceRepository, zones, record!, byBattle: false, ctx.EffectRegistry);
    AssertTrue(byEffect.Contains("decode") && byEffect.Contains("partition"), "an EFFECT deletion (byBattle:false) offers both");

    var byBattle = DeletionReplacementTiming.PreOptions(ctx.CardInstanceRepository, zones, record!, byBattle: true, ctx.EffectRegistry);
    AssertFalse(byBattle.Contains("decode"), "a BATTLE deletion offers no Decode");
    AssertFalse(byBattle.Contains("partition"), "a BATTLE deletion offers no Partition");
}

// (c) Declining the PRE Decode plays nothing; the deletion proceeds and EVERY source is trashed (AS-IS
// willBeRemoveField unchanged — Decode never cancels the deletion).
async Task DecliningTrashesAllSources()
{
    (DcgoMatch match, EngineContext ctx) = await StartedMatch();
    HeadlessEntityId holder = HandCard(match, P2, 1);
    HeadlessEntityId s0 = new("P2-c1c-0");
    HeadlessEntityId s1 = new("P2-c1c-1");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(s0, DigimonDef, P2));
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(s1, DigimonDef, P2));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, holder, ChoiceZone.Hand, ChoiceZone.BattleArea));
    SetMetadata(match, holder, new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [DeletionReplacementGate.HasDecodeKey] = true,
        [DeletionReplacementGate.SourceIdsKey] = new[] { s0.Value, s1.Value },
    });

    await DeleteByEffect(match, ctx, holder);
    LegalAction skip = ResolveActions(match, P2).Single(a => a.Id.Value.Contains(":skip", StringComparison.Ordinal));
    await match.ApplyActionAsync(skip);
    await match.StepAsync();

    AssertTrue(InZone(match, P2, ChoiceZone.Trash, holder), "the deletion proceeded — the card left play");
    AssertTrue(InZone(match, P2, ChoiceZone.Trash, s0) && InZone(match, P2, ChoiceZone.Trash, s1), "both sources trashed (nothing played)");
    AssertFalse(ReadFlag(match, holder, DeletionReplacementGate.DecodedKey), "no decoded marker when declined");
}

// (d) A Partition keyword grant with two colour groups: pick #1 draws group[0], pick #2 group[1]; both are
// played and the card still leaves (a 3rd, non-grouped source is trashed).
async Task PartitionColourGroupRepeatThenDeletes()
{
    (DcgoMatch match, EngineContext ctx) = await StartedMatch();
    HeadlessEntityId holder = HandCard(match, P1, 1);
    var cards = (CardDatabase)ctx.CardRepository;

    HeadlessEntityId Colored(string tag, string colour)
    {
        var defId = new HeadlessEntityId($"def:c1d-{tag}");
        cards.Upsert(new CardRecord(defId, tag, tag,
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["colors"] = new[] { colour }, ["level"] = 4, ["dp"] = 3000 }, CardType: "Digimon"));
        var id = new HeadlessEntityId($"P1-c1d-{tag}");
        ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, P1));
        return id;
    }

    HeadlessEntityId red = Colored("RED", "Red");
    HeadlessEntityId yellow = Colored("YEL", "Yellow");
    HeadlessEntityId blue = Colored("BLU", "Blue");   // matches no group -> trashed with the stack

    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, holder, ChoiceZone.Hand, ChoiceZone.BattleArea));
    // MIGRATION-NOTE (P7 test-fix): PartitionSelfEffect (CardEffectFactory/KeyWordEffects/Partition.cs) now
    // returns the new-model ActivateClass, which has no ToBinding — EffectRegistry.Register can no longer take
    // its result (CS1061 if attempted). DeletionReplacementTiming.PartitionConditionsOf reads the holder's
    // colour-group pair either from the live EffectRegistry (keyword-tagged binding — unavailable here) OR
    // directly from the card's metadata under PartitionCondition.PartitionConditionsKey, so the metadata path
    // is used instead to supply the exact same two colour-group conditions the factory call constructed.
    SetMetadata(match, holder, new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [DeletionReplacementGate.SourceIdsKey] = new[] { red.Value, yellow.Value, blue.Value },
        [DeletionReplacementGate.HasPartitionKey] = true,
        [PartitionCondition.PartitionConditionsKey] = (IReadOnlyList<PartitionCondition>)new List<PartitionCondition>
        {
            new PartitionCondition(4, "Red"), new PartitionCondition(4, "Yellow"),
        },
    });

    await DeleteByEffect(match, ctx, holder);
    AssertTrue(InZone(match, P1, ChoiceZone.BattleArea, holder), "PRE window: holder still on the field");

    LegalAction activate = ResolveActions(match, P1).Single(a =>
        a.Id.Value.Contains("#partition", StringComparison.Ordinal) &&
        !a.Id.Value.Contains(red.Value, StringComparison.Ordinal) &&
        !a.Id.Value.Contains(yellow.Value, StringComparison.Ordinal) &&
        !a.Id.Value.Contains(blue.Value, StringComparison.Ordinal));
    await match.ApplyActionAsync(activate);
    await match.StepAsync();

    // pick #1 = group[0] (Red) only.
    var pick1 = ResolveActions(match, P1).Where(a => a.Id.Value.Contains(red.Value, StringComparison.Ordinal) ||
        a.Id.Value.Contains(yellow.Value, StringComparison.Ordinal) || a.Id.Value.Contains(blue.Value, StringComparison.Ordinal)).ToArray();
    AssertTrue(pick1.Any(a => a.Id.Value.Contains(red.Value, StringComparison.Ordinal)), "pick #1 offers the Red source");
    AssertFalse(pick1.Any(a => a.Id.Value.Contains(yellow.Value, StringComparison.Ordinal)), "pick #1 does NOT offer group[1] (Yellow)");
    AssertFalse(pick1.Any(a => a.Id.Value.Contains(blue.Value, StringComparison.Ordinal)), "pick #1 does NOT offer the un-grouped Blue");
    await match.ApplyActionAsync(pick1.Single(a => a.Id.Value.Contains(red.Value, StringComparison.Ordinal)));
    await match.StepAsync();

    // pick #2 = group[1] (Yellow) only.
    LegalAction pick2 = ResolveActions(match, P1).Single(a => a.Id.Value.Contains(yellow.Value, StringComparison.Ordinal));
    await match.ApplyActionAsync(pick2);
    await match.StepAsync();

    AssertTrue(InZone(match, P1, ChoiceZone.BattleArea, red), "the Red pick was played");
    AssertTrue(InZone(match, P1, ChoiceZone.BattleArea, yellow), "the Yellow pick was played");
    AssertTrue(ReadFlag(match, holder, DeletionReplacementGate.PartitionedKey), "partitioned marker stamped");
    AssertTrue(InZone(match, P1, ChoiceZone.Trash, holder), "the deletion proceeded — the card left play");
    AssertTrue(InZone(match, P1, ChoiceZone.Trash, blue), "the un-grouped source was trashed with the stack");
}

// (e) A hasDecode holder whose only source fails the source condition (here: a non-Digimon source — AS-IS
// CanSelectSourceCardCondition requires source.IsDigimon) offers no Decode; the card is deleted and the
// source is trashed.
async Task SourceConditionMismatchNotOffered()
{
    (DcgoMatch match, EngineContext ctx) = await StartedMatch();
    HeadlessEntityId holder = HandCard(match, P2, 1);
    HeadlessEntityId tamerSrc = new("P2-c1e-tamer");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(tamerSrc, TamerDef, P2));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, holder, ChoiceZone.Hand, ChoiceZone.BattleArea));
    SetMetadata(match, holder, new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [DeletionReplacementGate.HasDecodeKey] = true,
        [DeletionReplacementGate.SourceIdsKey] = new[] { tamerSrc.Value },
    });

    await DeleteByEffect(match, ctx, holder);

    AssertFalse(ResolveActions(match, P2).Any(a => a.Id.Value.Contains("#decode", StringComparison.Ordinal)),
        "no Decode option when no source matches the condition");
    AssertTrue(InZone(match, P2, ChoiceZone.Trash, holder), "the card was deleted");
    AssertTrue(InZone(match, P2, ChoiceZone.Trash, tamerSrc), "its non-matching source was trashed");
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

async Task<(DcgoMatch, EngineContext)> StartedMatch()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 73);
    var cards = (CardDatabase)ctx.CardRepository;
    for (int i = 1; i <= 12; i++)
    {
        cards.Upsert(Digimon($"P1-M{i:D2}"));
        cards.Upsert(Digimon($"P2-M{i:D2}"));
    }

    cards.Upsert(new CardRecord(DigimonDef, "C1-DIGI", "C1 Digimon source", new Dictionary<string, object?>(), CardType: "Digimon"));
    cards.Upsert(new CardRecord(TamerDef, "C1-TAMER", "C1 Tamer source", new Dictionary<string, object?>(), CardType: "Tamer"));

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
