using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
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
    ("Delete: the target SURVIVES via the AS-IS PRE cut-in window -> failure fires (no actual deletion)", DeleteEvadedFailure),
    ("Delete: mixed targets -> success with ONLY the actually-destroyed one (window survivor excluded)", DeleteMixed),
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
    // (C-Del 3c-2b) The target SURVIVES through the AS-IS PRE cut-in window (a window-collectible mandatory
    // replacement), so no permanent actually leaves the field. success requires an ACTUAL deletion
    // (DestroyedPermanents membership), so the delete-process reports FAILURE.
    (DcgoMatch match, HeadlessEntityId src, HeadlessEntityId target, _) = await Board(targetEvades: true);
    bool succeeded = false;
    bool failed = false;

    await CardEffectCommons.DeletePeremanentAndProcessAccordingToResult(
        new[] { Perm(match, target) }, V(match, src),
        successProcess: _ => { succeeded = true; return Task.CompletedTask; },
        failureProcess: () => { failed = true; return Task.CompletedTask; });

    AssertTrue(InZone(match, P2, ChoiceZone.BattleArea, target), "the target survived via the AS-IS PRE cut-in window (willBeRemoveField cancelled)");
    AssertTrue(!InZone(match, P2, ChoiceZone.Trash, target), "the survivor was never trashed");
    AssertTrue(failed && !succeeded, "failure fired — success requires an ACTUAL deletion (AS-IS); the window survivor was never destroyed");
}

async Task DeleteMixed()
{
    // The window survivor is spared; the plain target is actually destroyed. success carries ONLY the target
    // that really left the field.
    (DcgoMatch match, HeadlessEntityId src, HeadlessEntityId survivor, HeadlessEntityId plain) = await Board(targetEvades: true, secondPlainTarget: true);
    IReadOnlyList<Permanent>? destroyed = null;

    await CardEffectCommons.DeletePeremanentAndProcessAccordingToResult(
        new[] { Perm(match, survivor), Perm(match, plain) }, V(match, src),
        successProcess: d => { destroyed = d; return Task.CompletedTask; },
        failureProcess: null);

    AssertTrue(destroyed is not null && destroyed.Count == 1 && destroyed[0].InstanceId == plain,
        "success carries ONLY the actually-destroyed target (the window survivor is excluded)");
    AssertTrue(InZone(match, P2, ChoiceZone.BattleArea, survivor), "the window survivor is still on the field");
    AssertTrue(InZone(match, P2, ChoiceZone.Trash, plain), "the plain target was trashed");
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

    DcgoMatch match = DcgoMatch.CreatePumpDriven(context, new EngineTrace());
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") }, firstPlayerId: P1, shuffleDecks: false, shuffleDigitamaDecks: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 969, setup: setup));
    await AdvanceToMainAsync(match, P1);

    HeadlessEntityId src = HandCard(match, P1, 1);
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, src, ChoiceZone.Hand, ChoiceZone.BattleArea));

    // (C-Del 3c-2b) The invented gate no longer fires the PRE replacement keywords — a synthetic hasEvade marker
    // is inert. A real would-be-deleted SURVIVAL replacement now fires ONLY through the AS-IS PRE cut-in window
    // from a printed WhenPermanentWouldBeDeleted ActivateClass. Use the window-collectible fixture
    // TfxWouldBeDeleted (a MANDATORY survive replacement — the drain resolves inline, no interactive pause), so
    // the delete-process observes an ACTUAL survivor (DestroyedPermanents excludes it) and reports FAILURE. When
    // NOT evading, the target is a plain deck card that is actually destroyed.
    HeadlessEntityId target;
    if (targetEvades)
    {
        target = PlaceWindowSurvivor(context, P2, "1:g9:survivor");
        CardEffectRegistrar.RegisterCard(context, target, P2);
    }
    else
    {
        target = HandCard(match, P2, 1);
        await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, target, ChoiceZone.Hand, ChoiceZone.BattleArea));
    }

    HeadlessEntityId second = default;
    if (secondPlainTarget)
    {
        second = HandCard(match, P2, 1);
        await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, second, ChoiceZone.Hand, ChoiceZone.BattleArea));
    }

    return (match, src, target, second);
}

// Place a registered window-collectible survivor (TfxWouldBeDeleted): a MANDATORY WhenPermanentWouldBeDeleted
// replacement whose printed ActivateClass the AS-IS PRE cut-in window collects and resolves inline (clearing
// willBeRemoveField → the card is spared). No interactive pause, no gate.
HeadlessEntityId PlaceWindowSurvivor(EngineContext context, HeadlessPlayerId owner, string instance)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId("TfxWouldBeDeleted");
    cards.Upsert(new CardRecord(defId, "TfxWouldBeDeleted", "TfxWouldBeDeleted",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId(instance);
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = false, ["level"] = 4 }));
    context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
    return id;
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
    await StepOnceAsync(match);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, player));
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
