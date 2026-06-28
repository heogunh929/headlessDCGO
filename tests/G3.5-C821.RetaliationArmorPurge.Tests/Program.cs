using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// C-8 Retaliation / C-21 Armor Purge: finishing the consumption for two keywords that already SET a flag
// (KeywordBaseBatch2) but had no consumer.
//   * Retaliation — when this Digimon is deleted in battle, delete the Digimon it battled (AS-IS
//                   RetaliationProcess: destroy the battle's winner/opponent). BattleResolver.
//   * Armor Purge — when this Digimon (>= 1 source) would be deleted, shed the top card to the trash and
//                   promote the under-source as the new top, surviving in a lower form (AS-IS
//                   ArmorPurgeClass). Effect + battle deletion, via DeletionReplacementGate.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Retaliation: a battle-deleted Digimon also deletes the winner it battled", RetaliationDeletesWinner),
    ("Retaliation: without the keyword only the loser is deleted", NoRetaliationKeepsWinner),
    ("Retaliation: a tie deletes both regardless of the keyword", RetaliationTieDeletesBoth),
    // Armor Purge promotion is now a POST-deletion agent CHOICE (F-6.8) — covered in G3.5-F68.
    ("Armor Purge: without a source the Digimon is deleted normally", ArmorPurgeNoSourceIsDeleted),
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

// --- Retaliation (battle path) -------------------------------------------

async Task RetaliationDeletesWinner()
{
    (DcgoMatch match, HeadlessEntityId attacker, HeadlessEntityId defender) = await BattleMatch(
        attackerDp: 5000, defenderDp: 8000,
        attackerFlags: (BattleResolver.HasRetaliationKey, true), defenderFlags: null);
    match.Context.AttackController.DeclareAttack(P1, attacker, P2, defender, isDirectAttack: false);

    BattleResolutionResult result = await new BattleResolver().ResolveAsync(match.Context);

    AssertTrue(result.AttackerDeleted, "retaliation holder lost the battle");
    AssertTrue(result.DefenderDeleted, "retaliation also deletes the winner");
    AssertTrue(InZone(match, P1, ChoiceZone.Trash, attacker), "attacker trashed");
    AssertTrue(InZone(match, P2, ChoiceZone.Trash, defender), "winner trashed by retaliation");
}

async Task NoRetaliationKeepsWinner()
{
    (DcgoMatch match, HeadlessEntityId attacker, HeadlessEntityId defender) = await BattleMatch(
        attackerDp: 5000, defenderDp: 8000, attackerFlags: null, defenderFlags: null);
    match.Context.AttackController.DeclareAttack(P1, attacker, P2, defender, isDirectAttack: false);

    BattleResolutionResult result = await new BattleResolver().ResolveAsync(match.Context);

    AssertTrue(result.AttackerDeleted, "loser deleted");
    AssertFalse(result.DefenderDeleted, "winner survives without retaliation");
    AssertTrue(InZone(match, P2, ChoiceZone.BattleArea, defender), "winner stays on the field");
}

async Task RetaliationTieDeletesBoth()
{
    (DcgoMatch match, HeadlessEntityId attacker, HeadlessEntityId defender) = await BattleMatch(
        attackerDp: 6000, defenderDp: 6000,
        attackerFlags: (BattleResolver.HasRetaliationKey, true), defenderFlags: null);
    match.Context.AttackController.DeclareAttack(P1, attacker, P2, defender, isDirectAttack: false);

    BattleResolutionResult result = await new BattleResolver().ResolveAsync(match.Context);

    AssertTrue(result.AttackerDeleted, "attacker deleted in tie");
    AssertTrue(result.DefenderDeleted, "defender deleted in tie");
}

// --- Armor Purge ---------------------------------------------------------

async Task ArmorPurgeNoSourceIsDeleted()
{
    HeadlessEntityId top = new("P2-Top");
    HeadlessEntityId deleter = new("P1-Deleter");
    EngineContext context = EngineContext.CreateDefault(randomSeed: 12);
    PlaceOnField(context, top, P2, new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [DeletionReplacementGate.HasArmorPurgeKey] = true
    });
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(deleter, new HeadlessEntityId("def"), P1));
    MatchStateMutationSink sink = Sink(context);

    sink.Apply(Delete(top, deleter));
    await sink.FlushAsync();

    AssertTrue(InZoneCtx(context, P2, ChoiceZone.Trash, top), "no source: armor purge cannot fire, top trashed");
    AssertFalse(InZoneCtx(context, P2, ChoiceZone.BattleArea, top), "top left the battle area");
}

// --- Battle harness ------------------------------------------------------

async Task<(DcgoMatch Match, HeadlessEntityId Attacker, HeadlessEntityId Defender)> BattleMatch(
    int attackerDp, int defenderDp, (string Key, bool Value)? attackerFlags, (string Key, bool Value)? defenderFlags)
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

    SetMetadata(match, attacker, Meta(BattleResolver.DpKey, attackerDp, attackerFlags));
    SetMetadata(match, defender, Meta(BattleResolver.DpKey, defenderDp, defenderFlags));
    return (match, attacker, defender);
}

Dictionary<string, object?> Meta(string dpKey, int dp, (string Key, bool Value)? flag)
{
    var metadata = new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [dpKey] = dp,
        [DeletionReplacementGate.IsSuspendedKey] = true
    };
    if (flag is { } f)
    {
        metadata[f.Key] = f.Value;
    }

    return metadata;
}

// --- Effect-path helpers -------------------------------------------------

void PlaceOnField(EngineContext context, HeadlessEntityId id, HeadlessPlayerId owner, IReadOnlyDictionary<string, object?> metadata)
{
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId("def"), owner, Metadata: metadata));
    context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
}

void PlaceInNone(EngineContext context, HeadlessEntityId id, HeadlessPlayerId owner) =>
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId("def"), owner));

MatchStateMutationSink Sink(EngineContext context) =>
    new(context.CardInstanceRepository, log: null, context.ZoneMover, memory: null, context.EffectRegistry);

EffectMutation Delete(HeadlessEntityId cardId, HeadlessEntityId deleterId) =>
    new(MatchStateMutationSink.DeleteKind, deleterId,
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = cardId.Value });

// --- Shared helpers ------------------------------------------------------

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
    if (!match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
    {
        throw new InvalidOperationException($"Missing card instance '{cardId}'.");
    }

    Dictionary<string, object?> metadata = new(record.Metadata, StringComparer.Ordinal);
    foreach (KeyValuePair<string, object?> pair in values)
    {
        metadata[pair.Key] = pair.Value;
    }

    match.Context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
}

bool InZone(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId) =>
    InZoneCtx(match.Context, player, zone, cardId);

bool InZoneCtx(EngineContext context, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(player, zone).Contains(cardId);

bool ReadFlagM(DcgoMatch match, HeadlessEntityId cardId, string key) => ReadFlagCtx(match.Context, cardId, key);

bool ReadFlagCtx(EngineContext context, HeadlessEntityId cardId, string key) =>
    context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? r) && r is not null
        && r.Metadata.TryGetValue(key, out object? raw) && raw is bool b && b;

string[] SourceIds(EngineContext context, HeadlessEntityId cardId) =>
    context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? r) && r is not null
        && r.Metadata.TryGetValue(DeletionReplacementGate.SourceIdsKey, out object? raw) && raw is IEnumerable<string> ids
        ? ids.ToArray() : Array.Empty<string>();

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

static void AssertSequence(IReadOnlyList<string> actual, params string[] expected)
{
    if (actual.Count != expected.Length)
    {
        throw new InvalidOperationException($"sequence length: expected {expected.Length}, actual {actual.Count}.");
    }

    for (int i = 0; i < expected.Length; i++)
    {
        if (!string.Equals(actual[i], expected[i], StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"sequence[{i}]: expected '{expected[i]}', actual '{actual[i]}'.");
        }
    }
}
