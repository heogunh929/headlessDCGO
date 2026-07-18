using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// P1-3 (C2r / RD-C2-SECCHECK-INTERACTIVE) witness. An INTERACTIVE OnSecurityCheck window reactor (TfxOnSecurityCheckSelect,
// a select-and-destroy body) suspends the security-check window mid-loop. Before the fix, SecurityResolver's window
// drive (ResolveSecurityCheckWindowAsync -> AutoProcessCheck) had NO catch, so the pending-choice suspend unwound the
// security loop as an UNCAUGHT throw — aborting the attack. The fix catches the suspend and PARKS the remaining check
// via the deferred-security-check machinery, so: (1) no exception escapes, (2) the game pauses on the pending choice,
// (3) resolving the choice resumes the suspended window and completes the reactor (the target is deleted).

HeadlessPlayerId P1 = new(1);   // attacker side
HeadlessPlayerId P2 = new(2);   // defender / the reactor owner

var tests = new (string Name, Func<Task> Body)[]
{
    ("interactive OnSecurityCheck reactor: suspend does NOT throw out of the security loop; game pauses; resume deletes the target", InteractiveSecCheckParksAndResumes),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex)
    {
        failures.Add(test.Name);
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine($"{ex}");
    }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task InteractiveSecCheckParksAndResumes()
{
    var match = DcgoMatch.CreatePumpDriven(EngineContext.CreateDefault(deferredChoice: true), new EngineTrace());
    var env = new HeadlessRlEnvironment(match);
    await env.InitializeAsync(BuildMatchConfig());
    await AdvanceToMainAsync(match, P1);

    EngineContext context = match.Context;
    CardDatabase cards = (CardDatabase)context.CardRepository;

    // Clear the dealt security so exactly ONE plain security card is checked (the reactor's window opens on it).
    foreach (var dealt in ((IZoneStateReader)context.ZoneMover).GetCards(P2, ChoiceZone.Security).ToArray())
        await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, dealt, ChoiceZone.Security, ChoiceZone.None));
    cards.Upsert(new CardRecord(new HeadlessEntityId("PLAINSEC"), "PLAINSEC", "PlainSec",
        new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Option"));
    var plainSec = new HeadlessEntityId("p2:security:PLAIN");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(plainSec, new HeadlessEntityId("PLAINSEC"), P2));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, plainSec, ChoiceZone.None, ChoiceZone.Security));

    // P2 field Digimon owns the interactive [On Security Check] reactor (dispatched by class-name).
    var reactor = await PlaceDigimon(context, P2, "TfxOnSecurityCheckSelect", "p2:battle:REACT");
    // P1 attacker + a P1 target the reactor can destroy (opponent-of-P2 = P1).
    var attacker = await PlaceDigimon(context, P1, "ATK", "p1:battle:ATK");
    var target = await PlaceDigimon(context, P1, "TGT", "p1:battle:TGT");

    // 1) Run the security check. The revealed plain card opens the OnSecurityCheck window; the reactor's select body
    //    suspends it. The fix must catch the suspend and PARK (no throw escapes).
    SecurityCheckLoopResult loop;
    try
    {
        loop = await new SecurityResolver().RunSecurityCheckLoopAsync(
            context, (IZoneStateReader)context.ZoneMover, P1, attacker, P2, strike: 1);
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException($"the interactive OnSecurityCheck suspend escaped the security loop UNCAUGHT: {ex.GetType().Name}", ex);
    }

    AssertTrue(loop.DeferredForDeletionReplacement, "the security loop PARKED (deferred) instead of aborting the attack");
    AssertTrue(context.ChoiceController.Current.IsPending, "the game paused: a choice is pending after the window suspended");
    AssertTrue(InZone(context, P1, ChoiceZone.BattleArea, target), "nothing applied yet: the target is still on the battle area");
    AssertTrue(SecurityResolver.HasDeferredSecurityCheck(context, attacker), "the remaining security check was parked on the attacker (resumable via FinalizeDeferredSecurityCheckAsync)");

    // 2) The reactor owner (P2) resolves the choice -> the suspended window resumes (ResumeSuspendedWindowsAsync) and
    //    the reactor completes, deleting the chosen target. Proves the suspend is resumable, not a dead abort.
    LegalAction resolve = match.GetLegalActions(P2)
        .Single(x => x.ActionType == HeadlessActionTypes.ResolveChoice && x.Id.Value.Contains(target.Value, StringComparison.Ordinal));
    await match.ApplyActionAsync(resolve);
    await match.StepAsync();

    AssertTrue(!context.ChoiceController.Current.IsPending, "no pending choice after the suspended window resumed");
    AssertTrue(!InZone(context, P1, ChoiceZone.BattleArea, target) && InZone(context, P1, ChoiceZone.Trash, target),
        "the resumed reactor completed: the chosen target was deleted");
}

// --- Helpers -------------------------------------------------------------

async Task<HeadlessEntityId> PlaceDigimon(EngineContext context, HeadlessPlayerId owner, string cardNumber, string instanceId)
{
    CardDatabase cards = (CardDatabase)context.CardRepository;
    var def = new HeadlessEntityId($"DEF:{instanceId}");
    cards.Upsert(new CardRecord(def, cardNumber, cardNumber,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000 }, CardType: "Digimon"));
    var id = new HeadlessEntityId(instanceId);
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["isSuspended"] = false }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

static MatchConfig BuildMatchConfig()
{
    HeadlessPlayerId[] players = { new(1), new(2) };
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { BuildDeck(new HeadlessPlayerId(1), "P1"), BuildDeck(new HeadlessPlayerId(2), "P2") },
        firstPlayerId: new HeadlessPlayerId(1));
    return MatchConfig.Create(players, randomSeed: 17, setup: setup);
}

static PlayerDeckSetup BuildDeck(HeadlessPlayerId playerId, string prefix, int mainCount = 12, int digitamaCount = 3) =>
    new(playerId,
        Enumerable.Range(1, mainCount).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, digitamaCount).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

static async Task AdvanceToMainAsync(DcgoMatch match, HeadlessPlayerId playerId)
{
    await StepOnceDriveAsync(match);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, playerId));
}

static bool InZone(EngineContext context, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId id) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(player, zone).Contains(id);

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }


// --- Phase driving (pump auto-flow, F62/c2/EXEMPLAR-T1 precedent, 4b B2-c3) ---
// Drive the pump's natural Active->Draw->Breeding->Main auto-flow to the player's main wait; the OLD
// AdvancePhase step currency is retired. Breeding/Mulligan decisions are declined; assertion strength unchanged.
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
            await ResolvePendingDriveAsync(match, skip: decline);
        }
        else await StepOnceDriveAsync(match);
    }
    if (!condition(match))
    {
        HeadlessTurnState t = match.Context.TurnController.Current;
        throw new InvalidOperationException(
            $"pump drive did not reach the expected state - phase:{t.Phase} turn:{t.TurnNumber} player:{t.TurnPlayerId} " +
            $"choice:{match.Context.ChoiceController.PendingRequest?.Type.ToString() ?? "<none>"} pending:{match.HasPendingChoice()} terminal:{match.IsTerminal()}");
    }
}

static async Task ResolvePendingDriveAsync(DcgoMatch match, bool skip)
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

static async Task StepOnceDriveAsync(DcgoMatch match)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.StepAsync();
}

