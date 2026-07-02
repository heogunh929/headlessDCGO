using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

// (W6-S) "...AndProcessAccordingToResult" commons — AS-IS CardEffectCommons.cs:437-644: run the action,
// then branch on whether it ACTUALLY happened. The Delete form runs the FULL deletion pipeline (would-be-
// deleted replacements may respond across a game-loop pause -> DeletionOutcomeWatcher parks the
// continuation); success = at least one target really left the field (DestroyedPermanents membership).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Delete: a target with no replacement dies -> success fires immediately with the destroyed list", DeleteImmediateSuccess),
    ("Delete: the target EVADES across the window pause -> failure fires after settle", DeleteEvadedFailure),
    ("Delete: mixed targets -> success with ONLY the actually-destroyed one", DeleteMixed),
    ("Suspend sibling: success on actual suspension, failure on already-suspended-only set", SuspendSibling),
    ("Bounce sibling: success when the permanent actually left the field", BounceSibling),
    ("TrashSecurity sibling: counts the actually-trashed security", TrashSecuritySibling),
    ("TrashDigivolutionCards sibling + plain FromTopOrBottom commons", TrashSourcesSibling),
    ("(W6-D) PlaceDelayOptionCards: cost-free battle-area placement + IsPlayedOptionPermanent tag", DelayPlacement),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task DeleteImmediateSuccess()
{
    (DcgoMatch match, HeadlessEntityId src, HeadlessEntityId target, _) = await Board(targetEvades: false);
    int destroyedCount = -1;
    bool failed = false;

    await CardEffectCommons.DeletePeremanentAndProcessAccordingToResult(
        new[] { Perm(match, target) }, V(match, src),
        successProcess: destroyed => { destroyedCount = destroyed.Count; return Task.CompletedTask; },
        failureProcess: () => { failed = true; return Task.CompletedTask; });

    AssertTrue(InZone(match, P2, ChoiceZone.Trash, target), "the target died");
    AssertTrue(destroyedCount == 1 && !failed, "success fired immediately with 1 destroyed");
}

async Task DeleteEvadedFailure()
{
    (DcgoMatch match, HeadlessEntityId src, HeadlessEntityId target, _) = await Board(targetEvades: true);
    bool succeeded = false;
    bool failed = false;

    await CardEffectCommons.DeletePeremanentAndProcessAccordingToResult(
        new[] { Perm(match, target) }, V(match, src),
        successProcess: _ => { succeeded = true; return Task.CompletedTask; },
        failureProcess: () => { failed = true; return Task.CompletedTask; });

    AssertTrue(!succeeded && !failed, "the continuation is PARKED while the Evade window is open");
    await match.StepAsync();   // the would-be-deleted window opens
    LegalAction evade = ResolveActions(match, P2).Single(a => a.Id.Value.Contains("#evade", StringComparison.Ordinal));
    await match.ApplyActionAsync(evade);
    await match.StepAsync();   // Evade resolves -> the watcher settles

    AssertTrue(InZone(match, P2, ChoiceZone.BattleArea, target), "the target evaded (survived, suspended)");
    AssertTrue(failed && !succeeded, "failure fired after settle — success requires an ACTUAL deletion (AS-IS)");
}

async Task DeleteMixed()
{
    (DcgoMatch match, HeadlessEntityId src, HeadlessEntityId evader, HeadlessEntityId plain) = await Board(targetEvades: true, secondPlainTarget: true);
    IReadOnlyList<Permanent>? destroyed = null;

    await CardEffectCommons.DeletePeremanentAndProcessAccordingToResult(
        new[] { Perm(match, evader), Perm(match, plain) }, V(match, src),
        successProcess: d => { destroyed = d; return Task.CompletedTask; },
        failureProcess: null);

    await match.StepAsync();
    LegalAction evade = ResolveActions(match, P2).Single(a => a.Id.Value.Contains("#evade", StringComparison.Ordinal));
    await match.ApplyActionAsync(evade);
    await match.StepAsync();

    AssertTrue(destroyed is not null && destroyed.Count == 1 && destroyed[0].InstanceId == plain,
        "success carries ONLY the actually-destroyed target (the evader is excluded)");
}

async Task SuspendSibling()
{
    (DcgoMatch match, HeadlessEntityId src, HeadlessEntityId target, _) = await Board(targetEvades: false);
    int suspended = -1;
    await CardEffectCommons.SuspendPeremanentAndProcessAccordingToResult(
        new[] { Perm(match, target) }, V(match, src),
        successProcess: s => { suspended = s.Count; return Task.CompletedTask; }, failureProcess: null);
    AssertTrue(suspended == 1, "actual suspension -> success");

    bool failedOnNoop = false;
    await CardEffectCommons.SuspendPeremanentAndProcessAccordingToResult(
        Array.Empty<Permanent>(), V(match, src),
        successProcess: null, failureProcess: () => { failedOnNoop = true; return Task.CompletedTask; });
    AssertTrue(failedOnNoop, "nothing suspended -> failure");
}

async Task BounceSibling()
{
    (DcgoMatch match, HeadlessEntityId src, HeadlessEntityId target, _) = await Board(targetEvades: false);
    bool succeeded = false;
    await CardEffectCommons.BouncePeremanentAndProcessAccordingToResult(
        new[] { Perm(match, target) }, V(match, src),
        successProcess: () => { succeeded = true; return Task.CompletedTask; }, failureProcess: null);
    AssertTrue(succeeded, "the permanent actually left the field -> success");
    AssertTrue(!InZone(match, P2, ChoiceZone.BattleArea, target), "target left the battle area");
}

async Task TrashSecuritySibling()
{
    (DcgoMatch match, HeadlessEntityId src, _, _) = await Board(targetEvades: false);
    int before = ((IZoneStateReader)match.Context.ZoneMover).GetCards(P2, ChoiceZone.Security).Count;
    AssertTrue(before >= 2, "sanity: security stack present");

    int trashed = -1;
    await CardEffectCommons.TrashSecurityAndProcessAccordingToResult(
        P2, trashAmount: 2, fromTop: true, V(match, src),
        successProcess: n => { trashed = n; return Task.CompletedTask; }, failureProcess: null);
    AssertTrue(trashed == 2, "2 security actually trashed -> success(2)");

    bool failed = false;
    await CardEffectCommons.TrashSecurityAndProcessAccordingToResult(
        P2, trashAmount: 0, fromTop: true, V(match, src),
        successProcess: null, failureProcess: () => { failed = true; return Task.CompletedTask; });
    AssertTrue(failed, "nothing trashed -> failure");
}

async Task TrashSourcesSibling()
{
    (DcgoMatch match, HeadlessEntityId src, HeadlessEntityId host, _) = await Board(targetEvades: false);
    // give the host 2 under-cards
    HeadlessEntityId u1 = HandCard(match, P2, 1);
    HeadlessEntityId u2 = HandCard(match, P2, 2);
    await match.Context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, u1, ChoiceZone.Hand, ChoiceZone.Trash));
    await match.Context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, u2, ChoiceZone.Hand, ChoiceZone.Trash));
    await DigivolutionStackHelpers.AddSourcesBottomAsync(
        match.Context.CardInstanceRepository, match.Context.ZoneMover, host, new[] { u1, u2 }, ChoiceZone.Trash);

    int trashed = -1;
    await CardEffectCommons.TrashDigivolutionCardsAndProcessAccordingToResult(
        Perm(match, host), trashCount: 1, isFromTop: true, V(match, src),
        successProcess: n => { trashed = n; return Task.CompletedTask; }, failureProcess: null);
    AssertTrue(trashed == 1, "1 source actually trashed -> success(1)");

    int more = await CardEffectCommons.TrashDigivolutionCardsFromTopOrBottom(Perm(match, host), 1, isFromTop: false, V(match, src));
    AssertTrue(more == 1, "the plain FromTopOrBottom commons trashes too");
}

async Task DelayPlacement()
{
    (DcgoMatch match, _, _, _) = await Board(targetEvades: false);
    // an Option card staged on the execution area (AS-IS: the resolving option places itself)
    var cards = (CardDatabase)match.Context.CardRepository;
    var defId = new HeadlessEntityId("DEF:DELAYOPT");
    cards.Upsert(new CardRecord(defId, "DELAYOPT", "Delay Option",
        new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Option"));
    var opt = new HeadlessEntityId("p2:exec:DELAYOPT");
    match.Context.CardInstanceRepository.Upsert(new CardInstanceRecord(opt, defId, P2));
    await match.Context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, opt, ChoiceZone.None, ChoiceZone.Execution));

    AssertTrue(await CardEffectCommons.PlaceDelayOptionCards(V(match, opt)), "the delay option placed");
    AssertTrue(InZone(match, P2, ChoiceZone.BattleArea, opt), "it is a REAL battle-area permanent");
    match.Context.CardInstanceRepository.TryGetInstance(opt, out CardInstanceRecord? rec);
    AssertTrue(rec!.Metadata.TryGetValue(GameFlowProcessor.IsPlayedOptionPermanentKey, out object? tag) && tag is true,
        "IsPlayedOptionPermanent tagged (P7 no-DP-trash exemption)");
    AssertTrue(rec.Metadata.TryGetValue("enteredThisTurn", out object? sick) && sick is true,
        "summoning-sickness marked -> CanDeclareOptionDelayEffect is false this turn (AS-IS gate)");
    AssertTrue(!CardEffectCommons.CanDeclareOptionDelayEffect(V(match, opt)), "cannot declare the turn it entered");
}

// --- Harness (F68 pattern) ---

async Task<(DcgoMatch Match, HeadlessEntityId Src, HeadlessEntityId Target, HeadlessEntityId Second)> Board(
    bool targetEvades, bool secondPlainTarget = false)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 969);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = new(context);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") }, firstPlayerId: P1, shuffleDecks: false, shuffleDigitamaDecks: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 969, setup: setup));
    await AdvanceToMainAsync(match, P1);

    HeadlessEntityId src = HandCard(match, P1, 1);
    HeadlessEntityId target = HandCard(match, P2, 1);
    HeadlessEntityId second = default;
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, src, ChoiceZone.Hand, ChoiceZone.BattleArea));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, target, ChoiceZone.Hand, ChoiceZone.BattleArea));
    if (targetEvades)
    {
        SetMetadata(match, target, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [DeletionReplacementGate.HasEvadeKey] = true,
            [DeletionReplacementGate.IsSuspendedKey] = false,
        });
    }

    if (secondPlainTarget)
    {
        second = HandCard(match, P2, 1);
        await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, second, ChoiceZone.Hand, ChoiceZone.BattleArea));
    }

    return (match, src, target, second);
}

Permanent Perm(DcgoMatch match, HeadlessEntityId id) =>
    new(match.Context, id, OwnerOf(match, id));

CardSource V(DcgoMatch match, HeadlessEntityId id) =>
    new(match.Context, id, OwnerOf(match, id), OwnerOf(match, id));

HeadlessPlayerId OwnerOf(DcgoMatch match, HeadlessEntityId id) =>
    match.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? r) && r is not null ? r.OwnerId : default;

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
}

void SetMetadata(DcgoMatch match, HeadlessEntityId cardId, IReadOnlyDictionary<string, object?> values)
{
    match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record);
    Dictionary<string, object?> metadata = new(record!.Metadata, StringComparer.Ordinal);
    foreach (KeyValuePair<string, object?> pair in values) metadata[pair.Key] = pair.Value;
    match.Context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
}

bool InZone(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)match.Context.ZoneMover).GetCards(player, zone).Contains(cardId);

static CardRecord Digimon(string id) =>
    new(new HeadlessEntityId(id), id, $"{id} Card", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon");

static PlayerDeckSetup Deck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
