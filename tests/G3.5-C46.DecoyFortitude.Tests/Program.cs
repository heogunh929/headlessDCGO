using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// C-4 Decoy / C-6 Fortitude: the remaining C-group2 defense keywords, both deletion-related but with
// hooks distinct from Evade/Barrier (which are would-be-deleted self-cost preventions):
//   * Decoy     — when an ENEMY effect would delete one of your battle-area Digimon, sacrifice a different
//                 Decoy Digimon of yours instead (redirect). Effect-deletion path only (AS-IS IsByEffect).
//   * Fortitude — AFTER a Digimon with >= 1 digivolution source is deleted, replay it from the trash for
//                 free as a new permanent (OnDestroyed). Both deletion paths.
// Consumption: DeletionReplacementGate.FindDecoyRedirect / TryFortitudeReplayAsync.

HeadlessPlayerId P1 = new(1);   // enemy / attacker side
HeadlessPlayerId P2 = new(2);   // owner / defender side

var tests = new (string Name, Func<Task> Body)[]
{
    // Decoy redirect (enemy effect + Decoy ally) is now a two-step agent CHOICE (F-6.8) — see G3.5-F68.
    ("Decoy: a same-owner effect deletion is not redirected", DecoyIgnoresOwnDeletion),
    ("Decoy: without a Decoy ally the target is deleted normally", DecoyWithoutAllyDeletesTarget),
    ("Fortitude: a deleted Digimon with sources is replayed from the trash (effect)", FortitudeReplaysAfterEffectDelete),
    ("Fortitude: a deleted Digimon without sources stays in the trash", FortitudeNoSourceStaysTrashed),
    ("Fortitude: a battle-deleted Digimon with sources is replayed (battle)", FortitudeReplaysAfterBattle),
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

// --- Decoy (effect-deletion path) ----------------------------------------

async Task DecoyIgnoresOwnDeletion()
{
    HeadlessEntityId target = new("P2-Target");
    HeadlessEntityId decoy = new("P2-Decoy");
    HeadlessEntityId deleter = new("P2-Deleter");   // same owner — not an enemy effect
    EngineContext context = await FieldSetup(
        (target, P2, null),
        (decoy, P2, (DeletionReplacementGate.HasDecoyKey, true)),
        (deleter, P2, null));
    MatchStateMutationSink sink = Sink(context);

    sink.Apply(Delete(target, deleter));
    await sink.FlushAsync();

    AssertTrue(InZone(context, P2, ChoiceZone.Trash, target), "own-effect deletion is not redirected");
    AssertTrue(InZone(context, P2, ChoiceZone.BattleArea, decoy), "the Decoy is not sacrificed");
}

async Task DecoyWithoutAllyDeletesTarget()
{
    HeadlessEntityId target = new("P2-Target");
    HeadlessEntityId deleter = new("P1-Deleter");
    EngineContext context = await FieldSetup(
        (target, P2, null),
        (deleter, P1, null));
    MatchStateMutationSink sink = Sink(context);

    sink.Apply(Delete(target, deleter));
    await sink.FlushAsync();

    AssertTrue(InZone(context, P2, ChoiceZone.Trash, target), "no Decoy ally: target is deleted");
}

// --- Fortitude -----------------------------------------------------------

async Task FortitudeReplaysAfterEffectDelete()
{
    HeadlessEntityId card = new("P2-Fort");
    HeadlessEntityId deleter = new("P1-Deleter");
    EngineContext context = await FieldSetup(
        (card, P2, (DeletionReplacementGate.HasFortitudeKey, true)),
        (deleter, P1, null));
    SetSources(context, card, "P2-Src01");
    MatchStateMutationSink sink = Sink(context);

    sink.Apply(Delete(card, deleter));
    await sink.FlushAsync();

    AssertTrue(InZone(context, P2, ChoiceZone.BattleArea, card), "fortitude card replayed onto the battle area");
    AssertFalse(InZone(context, P2, ChoiceZone.Trash, card), "fortitude card not left in the trash");
    AssertTrue(ReadFlag(context, card, DeletionReplacementGate.FortitudeReplayedKey), "fortitudeReplayed marker stamped");
    AssertTrue(ReadFlag(context, card, DeletionReplacementGate.EnteredThisTurnKey), "replayed permanent is summoning sick");
    AssertEqual(0, SourceCount(context, card), "digivolution sources cleared on replay");
}

async Task FortitudeNoSourceStaysTrashed()
{
    HeadlessEntityId card = new("P2-Fort");
    HeadlessEntityId deleter = new("P1-Deleter");
    EngineContext context = await FieldSetup(
        (card, P2, (DeletionReplacementGate.HasFortitudeKey, true)),
        (deleter, P1, null));
    MatchStateMutationSink sink = Sink(context);

    sink.Apply(Delete(card, deleter));
    await sink.FlushAsync();

    AssertTrue(InZone(context, P2, ChoiceZone.Trash, card), "no sources: fortitude cannot replay, card stays trashed");
    AssertFalse(InZone(context, P2, ChoiceZone.BattleArea, card), "card left the battle area");
}

async Task FortitudeReplaysAfterBattle()
{
    (DcgoMatch match, HeadlessEntityId attacker, HeadlessEntityId defender) = await BattleMatch();
    SetMetadata(match, defender, new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [BattleResolver.DpKey] = 7000,
        [DeletionReplacementGate.IsSuspendedKey] = true,
        [DeletionReplacementGate.HasFortitudeKey] = true,
        [DeletionReplacementGate.SourceIdsKey] = new[] { "P2-Src01" }
    });
    match.Context.AttackController.DeclareAttack(P1, attacker, P2, defender, isDirectAttack: false);

    BattleResolutionResult result = await new BattleResolver().ResolveAsync(match.Context);

    AssertTrue(result.DefenderDeleted, "defender was deleted in battle (OnDestroyed)");
    AssertTrue(InZoneM(match, P2, ChoiceZone.BattleArea, defender), "fortitude defender replayed onto the battle area");
    AssertTrue(ReadFlagM(match, defender, DeletionReplacementGate.FortitudeReplayedKey), "fortitudeReplayed marker stamped");
    AssertEqual(0, SourceCountM(match, defender), "sources cleared on replay");
}

// --- Effect-path setup (direct instances) --------------------------------

async Task<EngineContext> FieldSetup(params (HeadlessEntityId Id, HeadlessPlayerId Owner, (string Key, bool Value)? Flag)[] cards)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 12);
    foreach ((HeadlessEntityId id, HeadlessPlayerId owner, (string Key, bool Value)? flag) in cards)
    {
        var metadata = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (flag is { } f)
        {
            metadata[f.Key] = f.Value;
        }

        context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId("def"), owner, Metadata: metadata));
        await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    }

    return context;
}

void SetSources(EngineContext context, HeadlessEntityId cardId, params string[] sourceIds)
{
    CardInstanceRecord record = InstanceCtx(context, cardId);
    var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal)
    {
        [DeletionReplacementGate.SourceIdsKey] = sourceIds
    };
    context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
}

MatchStateMutationSink Sink(EngineContext context) =>
    new(context.CardInstanceRepository, log: null, context.ZoneMover, memory: null, context.EffectRegistry);

EffectMutation Delete(HeadlessEntityId cardId, HeadlessEntityId deleterId) =>
    new(MatchStateMutationSink.DeleteKind, deleterId,
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = cardId.Value });

// --- Battle-path setup ---------------------------------------------------

async Task<(DcgoMatch Match, HeadlessEntityId Attacker, HeadlessEntityId Defender)> BattleMatch()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 73);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = new(context);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") },
        firstPlayerId: P1, shuffleDecks: false, shuffleDigitamaDecks: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 73, setup: setup));
    await AdvanceToMainAsync(match, P1);

    HeadlessEntityId attacker = HandCard(match, P1, 1);
    HeadlessEntityId defender = HandCard(match, P2, 1);
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, attacker, ChoiceZone.Hand, ChoiceZone.BattleArea));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, defender, ChoiceZone.Hand, ChoiceZone.BattleArea));
    SetMetadata(match, attacker, new Dictionary<string, object?>(StringComparer.Ordinal) { [BattleResolver.DpKey] = 9000 });
    return (match, attacker, defender);
}

HeadlessEntityId HandCard(DcgoMatch match, HeadlessPlayerId player, int index)
{
    HeadlessEntityId[] hand = ((IZoneStateReader)match.Context.ZoneMover)
        .GetCards(player, ChoiceZone.Hand)
        .OrderBy(id => id.Value, StringComparer.Ordinal)
        .ToArray();
    if (hand.Length < index)
    {
        throw new InvalidOperationException($"Player '{player}' hand has {hand.Length} cards; needed index {index}.");
    }

    return hand[index - 1];
}

async Task AdvanceToMainAsync(DcgoMatch match, HeadlessPlayerId player)
{
    for (var attempt = 0; attempt < 10 && match.GetObservation().Turn.Phase != HeadlessPhase.Main; attempt++)
    {
        LegalAction advance = match.GetLegalActions(player)
            .Single(a => a.ActionType == HeadlessActionTypes.AdvancePhase);
        await match.ApplyActionAsync(advance);
        await match.StepAsync();
    }

    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "advance to main");
}

void SetMetadata(DcgoMatch match, HeadlessEntityId cardId, IReadOnlyDictionary<string, object?> values)
{
    CardInstanceRecord record = InstanceCtx(match.Context, cardId);
    Dictionary<string, object?> metadata = new(record.Metadata, StringComparer.Ordinal);
    foreach (KeyValuePair<string, object?> pair in values)
    {
        metadata[pair.Key] = pair.Value;
    }

    match.Context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
}

// --- Shared helpers ------------------------------------------------------

CardInstanceRecord InstanceCtx(EngineContext context, HeadlessEntityId cardId)
{
    if (!context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
    {
        throw new InvalidOperationException($"Missing card instance '{cardId}'.");
    }

    return record;
}

bool InZone(EngineContext context, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(player, zone).Contains(cardId);

bool InZoneM(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId) =>
    InZone(match.Context, player, zone, cardId);

bool ReadFlag(EngineContext context, HeadlessEntityId cardId, string key) =>
    InstanceCtx(context, cardId).Metadata.TryGetValue(key, out object? raw) && raw is bool b && b;

bool ReadFlagM(DcgoMatch match, HeadlessEntityId cardId, string key) => ReadFlag(match.Context, cardId, key);

int SourceCount(EngineContext context, HeadlessEntityId cardId) =>
    InstanceCtx(context, cardId).Metadata.TryGetValue(DeletionReplacementGate.SourceIdsKey, out object? raw)
        && raw is IEnumerable<string> ids ? ids.Count() : 0;

int SourceCountM(DcgoMatch match, HeadlessEntityId cardId) => SourceCount(match.Context, cardId);

static CardRecord Digimon(string id) =>
    new(new HeadlessEntityId(id), id, $"{id} Card",
        new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon");

static PlayerDeckSetup Deck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

// --- Assertions ----------------------------------------------------------

static void AssertTrue(bool value, string label)
{
    if (!value) throw new InvalidOperationException($"{label}: expected true.");
}

static void AssertFalse(bool value, string label)
{
    if (value) throw new InvalidOperationException($"{label}: expected false.");
}

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', actual '{actual}'.");
    }
}
