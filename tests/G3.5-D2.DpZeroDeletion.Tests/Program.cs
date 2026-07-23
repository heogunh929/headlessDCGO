using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;
using Cec = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

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
        dpDelta: -3000); // effective 0
    await RuleProcessAsync(match);
    AssertInTrash(match, "DP 0 Digimon destroyed");
}

async Task DpNegativeDeleted()
{
    DcgoMatch match = await FieldDigimonAsync(cardType: "Digimon", dp: 5000,
        dpDelta: -6000); // effective -1000
    await RuleProcessAsync(match);
    AssertInTrash(match, "negative-DP Digimon destroyed");
}

async Task DpPositiveSurvives()
{
    DcgoMatch match = await FieldDigimonAsync(cardType: "Digimon", dp: 3000, dpDelta: null);
    await RuleProcessAsync(match);
    AssertOnField(match, "positive-DP Digimon survives");
}

async Task NoDpSurvives()
{
    DcgoMatch match = await FieldDigimonAsync(cardType: "Digimon", dp: null, dpDelta: null);
    await RuleProcessAsync(match);
    AssertOnField(match, "DP-less Digimon is not swept (guard)");
}

async Task NonDigimonSurvives()
{
    DcgoMatch match = await FieldDigimonAsync(cardType: "Tamer", dp: 0, dpDelta: null);
    await RuleProcessAsync(match);
    AssertOnField(match, "non-Digimon at DP 0 is not deleted by the Digimon rule");
}

// (B3) AS-IS DigimonLackDPProcess routes DP<=0 through DestroyPermanentsClass — the SAME deletion path as
// effects (would-be-deleted windows, OnDeletion, Fortitude, the DPZero flag). Previously a raw zone move.

async Task DpZeroFortitudeReplays()
{
    DcgoMatch match = await FieldDigimonAsync(cardType: "Digimon", dp: 3000,
        dpDelta: -3000);
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
        dpDelta: -3000);
    // (B6-Db item 1 re-pin — 수리-2 C5 판례) The retired HasEvadeKey metadata gate-key is replaced by the
    // current-model canon: a card-registered OPTIONAL [WhenPermanentWouldBeDeleted] survival replacement
    // (TfxWouldBeDeletedInteractive). The DP<=0 sweep now opens its "will you use it?" PRE cut-in.
    GiveWouldBeDeleted(match.Context, Card, P1, "TfxWouldBeDeletedInteractive");

    await RuleProcessAsync(match);
    AssertOnField(match, "the card is not swept while the Evade window is open");
    AssertTrue(match.Context.ChoiceController.Current.IsPending, "a would-be-deleted (PRE) choice is open");
    AssertEqual(ChoiceType.OptionalEffect, match.Context.ChoiceController.PendingRequest!.Type, "choice type");
}

void GiveWouldBeDeleted(EngineContext context, HeadlessEntityId card, HeadlessPlayerId owner, string tfxNumber)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId("def:" + tfxNumber);
    cards.Upsert(new CardRecord(defId, tfxNumber, tfxNumber,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["level"] = 4 }, CardType: "Digimon"));
    if (context.CardInstanceRepository.TryGetInstance(card, out CardInstanceRecord? record) && record is not null)
    {
        context.CardInstanceRepository.Upsert(record with { DefinitionId = defId });
    }

    HeadlessDCGO.Engine.Headless.Runtime.CardEffectRegistrar.RegisterCard(context, card, owner);
}

async Task DpZeroFlagStamped()
{
    DcgoMatch match = await FieldDigimonAsync(cardType: "Digimon", dp: 3000,
        dpDelta: -3000);
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
    DcgoMatch match = await FieldDigimonAsync(cardType: "DigiEgg", dp: null, dpDelta: null);
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
    DcgoMatch trashed = await FieldDigimonAsync(cardType: "Option", dp: null, dpDelta: null);
    await RuleProcessAsync(trashed);
    AssertInTrash(trashed, "an un-played no-DP Option is trashed");

    DcgoMatch kept = await FieldDigimonAsync(cardType: "Option", dp: null, dpDelta: null);
    SetMetadata(kept, Card, new Dictionary<string, object?> { [GameFlowProcessor.IsPlayedOptionPermanentKey] = true });
    await RuleProcessAsync(kept);
    AssertOnField(kept, "a played-option permanent is exempt (AS-IS IsPlayedOptionPermanent)");
}

// --- Harness -------------------------------------------------------------

// The rule sweep reads Permanent.DP (GameFlowProcessor.cs:671), which folds live continuous ChangeDigimonDP
// effects — that fold requires the ambient match scope.
static async Task RuleProcessAsync(DcgoMatch match)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await new GameFlowProcessor().RunToStableAsync(match.Context);
}

async Task<DcgoMatch> FieldDigimonAsync(string cardType, int? dp, int? dpDelta)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 74, deferredChoice: true);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId("P1-M01"), "P1-M01", "Subject", new Dictionary<string, object?>(), CardType: cardType));
    for (int index = 2; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }
    cards.Upsert(Digimon("P2-M01"));

    DcgoMatch match = DcgoMatch.CreatePumpDriven(context);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") }, firstPlayerId: P1, initialSecuritySize: 0, shuffleDecks: false, shuffleDigitamaDecks: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 74, setup: setup));
    await AdvanceToMainAsync(match);

    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, Card, ChoiceZone.Hand, ChoiceZone.BattleArea));

    var meta = new Dictionary<string, object?> { ["isSuspended"] = false };
    if (dp.HasValue) meta["dp"] = dp.Value;
    SetMetadata(match, Card, meta);

    // (B6-Db item 1 re-target — 수리-2 N2 판례) The dead `dpModifiers` metadata array was only read by
    // CardObservation, never folded by Permanent.DP (the seat the DP<=0 rule sweep reads) — so it never
    // materialised. Reconstructed with the LIVE canon: a ChangeDigimonDP continuous ±DP effect (ChangeDPClass /
    // IChangeDPEffect) which Permanent.DP folds live, the same mechanism N2's field battle reads.
    if (dpDelta is int delta)
    {
        using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(context);
        var srcDef = new HeadlessEntityId("SRC-DPMOD");
        cards.Upsert(new CardRecord(srcDef, "SRC-DPMOD", "dp source", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
        var srcId = new HeadlessEntityId($"src:{Card.Value}:{delta}");
        context.CardInstanceRepository.Upsert(new CardInstanceRecord(srcId, srcDef, P1));
        var source = new Cec.CardSource(context, srcId, P1);
        var target = new Cec.Permanent(context, Card, P1);
        Cec.CardEffectCommons.ChangeDigimonDP(target, delta, EffectDuration.UntilEachTurnEnd, source);
    }

    return match;
}

static CardRecord Digimon(string id) =>
    new(new HeadlessEntityId(id), id, $"{id} Card", new Dictionary<string, object?>(), CardType: "Digimon");

static PlayerDeckSetup Deck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

// (B6-Dc currency-drain) Reach Main via the pump's natural Active->Draw->Breeding->Main auto-flow; the OLD
// AdvancePhase step currency is retired. The DP<=0 deletion pipeline / would-be-deleted window still require
// the match past the early phases at Main (dropping reach-Main regresses the sweep, D2/B6-Db proof) — observed
// Main arrival is asserted (assertion strength unchanged). Breeding/Mulligan decisions are declined.
async Task AdvanceToMainAsync(DcgoMatch match)
{
    await StepOnceAsync(match);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P1));

    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "advance to main");
}

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
