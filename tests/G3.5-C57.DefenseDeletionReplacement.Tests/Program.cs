using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// C-5 Barrier / C-7 Evade: defense-keyword deletion REPLACEMENTS. When a Digimon would be deleted it may
// pay a cost to survive instead, mirroring AS-IS WhenPermanentWouldBeDeleted effects:
//   * Evade  (EvadeProcess)  — suspend self (cost), then willBeRemoveField = false. Battle + effect.
//   * Barrier (BarrierProcess) — trash the top security card (cost), then willBeRemoveField = false. Battle.
// Consumption lives in DeletionReplacementGate, consulted by BattleResolver (battle path) and
// MatchStateMutationSink's Delete kind (effect path).

HeadlessPlayerId P1 = new(1);   // attacker side
HeadlessPlayerId P2 = new(2);   // defender side
HeadlessEntityId Defender = new("P2-M01");   // effect-delete path: self-created instance id

var tests = new (string Name, Func<Task> Body)[]
{
    // Barrier/Evade survival in battle are now agent CHOICES (F-6.8) — covered in G3.5-F68.
    ("Barrier: with no security the Digimon is deleted normally", BarrierWithoutSecurityIsDeleted),
    ("Evade: an already-suspended Digimon cannot pay the cost and is deleted", EvadeSuspendedIsDeleted),
    // Evade (unsuspended) via effect deletion is now an agent CHOICE (F-6.8) — covered in G3.5-F68.
    ("Evade: an already-suspended Digimon is trashed by an effect deletion", EvadeSuspendedEffectDelete),
    ("Control: a battle-losing Digimon without defense keywords is deleted", NoKeywordIsDeleted),
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

// --- Battle-path tests ---------------------------------------------------

async Task BarrierWithoutSecurityIsDeleted()
{
    (DcgoMatch match, HeadlessEntityId att, HeadlessEntityId def) = await BattleMatch(
        defenderSuspended: true, defenderFlags: (DeletionReplacementGate.HasBarrierKey, true));
    ClearSecurity(match, P2);
    DeclareBattle(match, att, def);

    BattleResolutionResult result = await new BattleResolver().ResolveAsync(match.Context);

    AssertTrue(result.DefenderDeleted, "no security: barrier cannot pay, defender deleted");
    AssertTrue(InZone(match, P2, ChoiceZone.Trash, def), "defender trashed");
}

async Task EvadeSuspendedIsDeleted()
{
    (DcgoMatch match, HeadlessEntityId att, HeadlessEntityId def) = await BattleMatch(
        defenderSuspended: true, defenderFlags: (DeletionReplacementGate.HasEvadeKey, true));
    DeclareBattle(match, att, def);

    BattleResolutionResult result = await new BattleResolver().ResolveAsync(match.Context);

    AssertTrue(result.DefenderDeleted, "already-suspended evade cannot pay the cost, defender deleted");
    AssertTrue(InZone(match, P2, ChoiceZone.Trash, def), "defender trashed");
}

// --- Effect-deletion-path tests ------------------------------------------

async Task EvadeSuspendedEffectDelete()
{
    EngineContext context = await EffectDeleteSetup(suspended: true, hasEvade: true);
    MatchStateMutationSink sink = Sink(context);

    sink.Apply(Delete(Defender));
    await sink.FlushAsync();

    AssertTrue(InZoneCtx(context, P2, ChoiceZone.Trash, Defender), "already-suspended evade target is trashed");
    AssertFalse(InZoneCtx(context, P2, ChoiceZone.BattleArea, Defender), "target left the field");
}

async Task NoKeywordIsDeleted()
{
    (DcgoMatch match, HeadlessEntityId att, HeadlessEntityId def) = await BattleMatch(
        defenderSuspended: true, defenderFlags: null);
    DeclareBattle(match, att, def);

    BattleResolutionResult result = await new BattleResolver().ResolveAsync(match.Context);

    AssertTrue(result.DefenderDeleted, "plain defender deleted");
    AssertTrue(InZone(match, P2, ChoiceZone.Trash, def), "defender trashed");
}

// --- Battle harness ------------------------------------------------------

async Task<(DcgoMatch Match, HeadlessEntityId Attacker, HeadlessEntityId Defender)> BattleMatch(
    bool defenderSuspended, (string Key, bool Value)? defenderFlags)
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

    // Use the real (init-generated) hand instance ids, not definition ids.
    HeadlessEntityId attacker = HandCard(match, P1, 1);
    HeadlessEntityId defender = HandCard(match, P2, 1);
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, attacker, ChoiceZone.Hand, ChoiceZone.BattleArea));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, defender, ChoiceZone.Hand, ChoiceZone.BattleArea));

    // Attacker 9000 DP beats the defender's 7000 DP, so the defender would be deleted in battle.
    var attackerMeta = new Dictionary<string, object?>(StringComparer.Ordinal) { [BattleResolver.DpKey] = 9000 };
    var defenderMeta = new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [BattleResolver.DpKey] = 7000,
        [DeletionReplacementGate.IsSuspendedKey] = defenderSuspended
    };
    if (defenderFlags is { } flags)
    {
        defenderMeta[flags.Key] = flags.Value;
    }

    SetMetadata(match, attacker, attackerMeta);
    SetMetadata(match, defender, defenderMeta);
    return (match, attacker, defender);
}

void DeclareBattle(DcgoMatch match, HeadlessEntityId attacker, HeadlessEntityId defender) =>
    match.Context.AttackController.DeclareAttack(P1, attacker, P2, defender, isDirectAttack: false);

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

// --- Effect-delete harness -----------------------------------------------

async Task<EngineContext> EffectDeleteSetup(bool suspended, bool hasEvade)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 12);
    var metadata = new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [DeletionReplacementGate.IsSuspendedKey] = suspended
    };
    if (hasEvade)
    {
        metadata[DeletionReplacementGate.HasEvadeKey] = true;
    }

    context.CardInstanceRepository.Upsert(new CardInstanceRecord(Defender, new HeadlessEntityId("P2-M01"), P2, Metadata: metadata));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, Defender, ChoiceZone.None, ChoiceZone.BattleArea));
    return context;
}

MatchStateMutationSink Sink(EngineContext context) =>
    new(context.CardInstanceRepository, log: null, context.ZoneMover, memory: null, context.EffectRegistry);

EffectMutation Delete(HeadlessEntityId cardId) =>
    new(MatchStateMutationSink.DeleteKind, new HeadlessEntityId("deleter"),
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = cardId.Value });

// --- Shared helpers ------------------------------------------------------

int SecurityCount(DcgoMatch match, HeadlessPlayerId player) =>
    ((IZoneStateReader)match.Context.ZoneMover).GetCards(player, ChoiceZone.Security).Count;

void ClearSecurity(DcgoMatch match, HeadlessPlayerId player)
{
    foreach (HeadlessEntityId card in ((IZoneStateReader)match.Context.ZoneMover)
        .GetCards(player, ChoiceZone.Security).ToArray())
    {
        match.Context.ZoneMover.MoveAsync(
            new ZoneMoveRequest(player, card, ChoiceZone.Security, ChoiceZone.Trash)).GetAwaiter().GetResult();
    }
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
    CardInstanceRecord record = Instance(match.Context, cardId);
    Dictionary<string, object?> metadata = new(record.Metadata, StringComparer.Ordinal);
    foreach (KeyValuePair<string, object?> pair in values)
    {
        metadata[pair.Key] = pair.Value;
    }

    match.Context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
}

CardInstanceRecord Instance(EngineContext context, HeadlessEntityId cardId)
{
    if (!context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
    {
        throw new InvalidOperationException($"Missing card instance '{cardId}'.");
    }

    return record;
}

bool InZone(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)match.Context.ZoneMover).GetCards(player, zone).Contains(cardId);

bool InZoneCtx(EngineContext context, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(player, zone).Contains(cardId);

bool ReadFlag(DcgoMatch match, HeadlessEntityId cardId, string key) => ReadFlagCtx(match.Context, cardId, key);

bool ReadFlagCtx(EngineContext context, HeadlessEntityId cardId, string key) =>
    context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? r) && r is not null
        && r.Metadata.TryGetValue(key, out object? raw) && raw is bool b && b;

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
