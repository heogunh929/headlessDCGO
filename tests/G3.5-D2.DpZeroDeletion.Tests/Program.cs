using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

// G3.5-D2: a field Digimon whose effective DP drops to 0 or below is destroyed as a state-based action
// (AS-IS DigimonLackDPProcess / TrashNoDPPermanentProcess / CutInProcess: DP<=0 && IsDigimon). Only
// applies when DP is actually defined — a DP-less card is left alone (guard for abstract fixtures).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
HeadlessEntityId Card = new("p1:main:001:P1-M01");

var tests = new (string Name, Func<Task> Body)[]
{
    ("DP reduced to 0 by a modifier is deleted", DpZeroDeleted),
    ("DP reduced below 0 by a modifier is deleted", DpNegativeDeleted),
    ("DP above 0 survives the rule sweep", DpPositiveSurvives),
    ("A Digimon with no defined DP is left alone", NoDpSurvives),
    ("A non-Digimon at DP 0 is not deleted by the Digimon rule", NonDigimonSurvives),
    ("(B3) a DP-zero death runs the deletion pipeline: Fortitude replays the Digimon", DpZeroFortitudeReplays),
    ("(B3) a DP-zero death opens the would-be-deleted (Evade) window", DpZeroOpensPreWindow),
    ("(B3) the AS-IS DPZero flag is stamped on the deleted card", DpZeroFlagStamped),
    ("(P7) a no-DP Digi-Egg on the BATTLE area is trashed directly (no deletion triggers)", NoDpEggTrashed),
    ("(P7) an un-played Option lingers -> trashed; the played-option flag exempts it", NoDpOptionRules),
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

// --- Tests ---------------------------------------------------------------

async Task DpZeroDeleted()
{
    DcgoMatch match = await FieldDigimonAsync(cardType: "Digimon", dp: 3000,
        modifiers: new[] { DpModifier.Relative(-3000) }); // effective 0
    await RuleProcessAsync(match);
    AssertInTrash(match, "DP 0 Digimon destroyed");
}

async Task DpNegativeDeleted()
{
    DcgoMatch match = await FieldDigimonAsync(cardType: "Digimon", dp: 5000,
        modifiers: new[] { DpModifier.Relative(-6000) }); // effective -1000
    await RuleProcessAsync(match);
    AssertInTrash(match, "negative-DP Digimon destroyed");
}

async Task DpPositiveSurvives()
{
    DcgoMatch match = await FieldDigimonAsync(cardType: "Digimon", dp: 3000, modifiers: null);
    await RuleProcessAsync(match);
    AssertOnField(match, "positive-DP Digimon survives");
}

async Task NoDpSurvives()
{
    DcgoMatch match = await FieldDigimonAsync(cardType: "Digimon", dp: null, modifiers: null);
    await RuleProcessAsync(match);
    AssertOnField(match, "DP-less Digimon is not swept (guard)");
}

async Task NonDigimonSurvives()
{
    DcgoMatch match = await FieldDigimonAsync(cardType: "Tamer", dp: 0, modifiers: null);
    await RuleProcessAsync(match);
    AssertOnField(match, "non-Digimon at DP 0 is not deleted by the Digimon rule");
}

// (B3) AS-IS DigimonLackDPProcess routes DP<=0 through DestroyPermanentsClass — the SAME deletion path as
// effects (would-be-deleted windows, OnDeletion, Fortitude, the DPZero flag). Previously a raw zone move.

async Task DpZeroFortitudeReplays()
{
    DcgoMatch match = await FieldDigimonAsync(cardType: "Digimon", dp: 3000,
        modifiers: new[] { DpModifier.Relative(-3000) });
    var source = new HeadlessEntityId("P1-FortSrc");
    match.Context.CardInstanceRepository.Upsert(new CardInstanceRecord(source, new HeadlessEntityId("P1-M02"), P1));
    SetMetadata(match, Card, new Dictionary<string, object?>
    {
        [DeletionReplacementGate.HasFortitudeKey] = true,
        [DeletionReplacementGate.SourceIdsKey] = new[] { source.Value },
    });

    await RuleProcessAsync(match);
    // The persistent -DP modifier kills the replayed Digimon again (AS-IS: the rule process re-runs), so
    // the end state is the trash — but the FIRST death ran Fortitude: the digivolution source was consumed
    // (a raw sweep leaves sourceIds untouched on the trashed record).
    AssertInTrash(match, "the re-replayed Digimon died again to the persistent -DP");
    AssertTrue(!HasSourceIds(match, Card), "Fortitude consumed the source on the first death (pipeline ran)");
}

bool HasSourceIds(DcgoMatch match, HeadlessEntityId cardId) =>
    match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? r) && r is not null
        && r.Metadata.TryGetValue(DeletionReplacementGate.SourceIdsKey, out object? raw)
        && raw is IEnumerable<string> ids && ids.Any();

async Task DpZeroOpensPreWindow()
{
    DcgoMatch match = await FieldDigimonAsync(cardType: "Digimon", dp: 3000,
        modifiers: new[] { DpModifier.Relative(-3000) });
    SetMetadata(match, Card, new Dictionary<string, object?> { [DeletionReplacementGate.HasEvadeKey] = true });

    await RuleProcessAsync(match);
    AssertOnField(match, "the card is not swept while the Evade window is open");
    AssertTrue(match.Context.ChoiceController.Current.IsPending, "a would-be-deleted (PRE) choice is open");
    AssertEqual(ChoiceType.DeletionReplacement, match.Context.ChoiceController.PendingRequest!.Type, "choice type");
}

async Task DpZeroFlagStamped()
{
    DcgoMatch match = await FieldDigimonAsync(cardType: "Digimon", dp: 3000,
        modifiers: new[] { DpModifier.Relative(-3000) });
    await RuleProcessAsync(match);
    AssertInTrash(match, "DP-zero Digimon deleted");
    AssertTrue(ReadFlag(match, Card, HeadlessDCGO.Engine.Headless.Effects.MatchStateMutationSink.IsDpZeroKey),
        "the AS-IS DPZero flag travels with the deletion");
}

bool ReadFlag(DcgoMatch match, HeadlessEntityId cardId, string key) =>
    match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? r) && r is not null
        && r.Metadata.TryGetValue(key, out object? raw) && raw is bool b && b;

// (P7) AS-IS TrashNoDPPermanentProcess: a no-DP Digi-Egg / un-played Option on the battle area is trashed
// DIRECTLY (DiscardEvoRoots + RemoveField + AddTrash — not a destroy).
async Task NoDpEggTrashed()
{
    DcgoMatch match = await FieldDigimonAsync(cardType: "DigiEgg", dp: null, modifiers: null);
    // Give it a source and a POST keyword flag — a direct trash must fire NO deletion windows.
    var source = new HeadlessEntityId("P1-EggSrc");
    match.Context.CardInstanceRepository.Upsert(new CardInstanceRecord(source, new HeadlessEntityId("P1-M02"), P1));
    SetMetadata(match, Card, new Dictionary<string, object?>
    {
        [DeletionReplacementGate.SourceIdsKey] = new[] { source.Value },
        [DeletionReplacementGate.HasAscensionKey] = true,
    });

    await RuleProcessAsync(match);

    AssertInTrash(match, "the no-DP Digi-Egg was trashed");
    var zones = (IZoneStateReader)match.Context.ZoneMover;
    AssertTrue(zones.GetCards(P1, ChoiceZone.Trash).Contains(source), "its digivolution source was discarded too (DiscardEvoRoots)");
    AssertTrue(!match.Context.ChoiceController.Current.IsPending, "NO deletion-replacement window (direct trash, not a destroy)");
}

async Task NoDpOptionRules()
{
    DcgoMatch trashed = await FieldDigimonAsync(cardType: "Option", dp: null, modifiers: null);
    await RuleProcessAsync(trashed);
    AssertInTrash(trashed, "an un-played no-DP Option is trashed");

    DcgoMatch kept = await FieldDigimonAsync(cardType: "Option", dp: null, modifiers: null);
    SetMetadata(kept, Card, new Dictionary<string, object?> { [GameFlowProcessor.IsPlayedOptionPermanentKey] = true });
    await RuleProcessAsync(kept);
    AssertOnField(kept, "a played-option permanent is exempt (AS-IS IsPlayedOptionPermanent)");
}

// --- Harness -------------------------------------------------------------

static async Task RuleProcessAsync(DcgoMatch match) =>
    await new GameFlowProcessor().RunToStableAsync(match.Context);

async Task<DcgoMatch> FieldDigimonAsync(string cardType, int? dp, DpModifier[]? modifiers)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 74);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId("P1-M01"), "P1-M01", "Subject", new Dictionary<string, object?>(), CardType: cardType));
    for (int index = 2; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }
    cards.Upsert(Digimon("P2-M01"));

    DcgoMatch match = new(context);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") }, firstPlayerId: P1, initialSecuritySize: 0, shuffleDecks: false, shuffleDigitamaDecks: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 74, setup: setup));
    await AdvanceToMainAsync(match);

    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, Card, ChoiceZone.Hand, ChoiceZone.BattleArea));

    var meta = new Dictionary<string, object?> { ["isSuspended"] = false };
    if (dp.HasValue) meta["dp"] = dp.Value;
    if (modifiers is not null) meta["dpModifiers"] = modifiers;
    SetMetadata(match, Card, meta);
    return match;
}

static CardRecord Digimon(string id) =>
    new(new HeadlessEntityId(id), id, $"{id} Card", new Dictionary<string, object?>(), CardType: "Digimon");

static PlayerDeckSetup Deck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

async Task AdvanceToMainAsync(DcgoMatch match)
{
    for (var attempt = 0; attempt < 8 && match.GetObservation().Turn.Phase != HeadlessPhase.Main; attempt++)
    {
        LegalAction advance = match.GetLegalActions(P1).Single(a => a.ActionType == HeadlessActionTypes.AdvancePhase);
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
    foreach (KeyValuePair<string, object?> pair in values) metadata[pair.Key] = pair.Value;
    match.Context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
}

void AssertInTrash(DcgoMatch match, string label)
{
    var zones = (IZoneStateReader)match.Context.ZoneMover;
    AssertTrue(zones.GetCards(P1, ChoiceZone.Trash).Contains(Card), $"{label}: in trash");
    AssertFalse(zones.GetCards(P1, ChoiceZone.BattleArea).Contains(Card), $"{label}: left battle area");
}

void AssertOnField(DcgoMatch match, string label)
{
    var zones = (IZoneStateReader)match.Context.ZoneMover;
    AssertTrue(zones.GetCards(P1, ChoiceZone.BattleArea).Contains(Card), $"{label}: still on field");
}

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
    }
}

static void AssertTrue(bool value, string label)
{
    if (!value) throw new InvalidOperationException($"{label}: expected true.");
}

static void AssertFalse(bool value, string label)
{
    if (value) throw new InvalidOperationException($"{label}: expected false.");
}
