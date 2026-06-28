using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// C-group4 deletion-family keywords (the subset that fits DeletionReplacementGate):
//   * C-11 Fragment  — when this would be deleted, trash fragmentCost digivolution sources to survive.
//   * C-17 Ascension — after this is deleted, place the card into the security stack.
//   * C-19 Scapegoat — when this would be deleted, sacrifice another of the owner's Digimon instead.
// (The other C-group4 keywords — Iceclad/Decode/Partition/Progress/Overclock/Alliance/Vortex — need
// subsystems that don't exist yet and are deferred per discipline 2-5.)

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    // Fragment/Scapegoat (with sources/ally) — incl. in battle — are now agent CHOICES (F-6.8): G3.5-F68.
    // Ascension is now an OPTIONAL post-deletion agent choice (F-6.8) — covered in G3.5-F68.
    ("Fragment: with no source the Digimon is deleted", FragmentNoSourceDeleted),
    ("Scapegoat: with no ally the holder is deleted", ScapegoatNoAllyDeleted),
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

// --- Fragment ------------------------------------------------------------

async Task FragmentNoSourceDeleted()
{
    HeadlessEntityId top = new("P2-Top");
    HeadlessEntityId deleter = new("P1-D");
    EngineContext context = EngineContext.CreateDefault(randomSeed: 12);
    PlaceOnField(context, top, P2, Meta((DeletionReplacementGate.HasFragmentKey, true)));
    PlaceInNone(context, deleter, P1);
    MatchStateMutationSink sink = Sink(context);

    sink.Apply(Delete(top, deleter));
    await sink.FlushAsync();

    AssertTrue(InZoneCtx(context, P2, ChoiceZone.Trash, top), "no source: fragment cannot pay, top trashed");
}

// --- Scapegoat -----------------------------------------------------------

async Task ScapegoatNoAllyDeleted()
{
    HeadlessEntityId holder = new("P2-Holder");
    HeadlessEntityId deleter = new("P1-D");
    EngineContext context = EngineContext.CreateDefault(randomSeed: 12);
    PlaceOnField(context, holder, P2, Meta((DeletionReplacementGate.HasScapegoatKey, true)));
    PlaceInNone(context, deleter, P1);
    MatchStateMutationSink sink = Sink(context);

    sink.Apply(Delete(holder, deleter));
    await sink.FlushAsync();

    AssertTrue(InZoneCtx(context, P2, ChoiceZone.Trash, holder), "no ally: scapegoat cannot fire, holder deleted");
}

// --- Battle harness ------------------------------------------------------

async Task<(DcgoMatch Match, HeadlessEntityId Attacker, HeadlessEntityId Defender)> BattleMatch(
    int attackerDp, int defenderDp, (string Key, object? Value)[] defenderFlags)
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

    SetMetadata(match, attacker, new Dictionary<string, object?>(StringComparer.Ordinal) { [BattleResolver.DpKey] = attackerDp });
    var defenderMeta = new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [BattleResolver.DpKey] = defenderDp,
        [DeletionReplacementGate.IsSuspendedKey] = true
    };
    foreach ((string key, object? value) in defenderFlags)
    {
        defenderMeta[key] = value;
    }

    SetMetadata(match, defender, defenderMeta);
    return (match, attacker, defender);
}

// --- Effect-path helpers -------------------------------------------------

Dictionary<string, object?> Meta(params (string Key, object? Value)[] entries)
{
    var metadata = new Dictionary<string, object?>(StringComparer.Ordinal);
    foreach ((string key, object? value) in entries)
    {
        metadata[key] = value;
    }

    return metadata;
}

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
