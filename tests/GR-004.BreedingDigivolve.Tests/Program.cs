using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// GR-004: in-breeding digivolution (the digi-egg ramp). A Lv.2 Digi-Egg in the breeding area can be
// digivolved into a Lv.3 Digimon IN PLACE (the result stays in breeding, the egg becomes a digivolution
// source), after which it is a movable Digimon (GR-002 gate). Mirrors AS-IS: the breeding digi-egg frame
// is a valid digivolution target. Uses real cards so the printed evolution condition (Red@2) matches.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Hatch -> digivolve egg into a Lv.3 in breeding -> move the Lv.3 to battle (full ramp)", BreedingRamp),
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

// --- Tests ---------------------------------------------------------------

async Task BreedingRamp()
{
    // (4b B5-c2) The RL wrapper is retired for the pump match: HeadlessRlEnvironment.InitializeAsync is
    // just match.InitializeAsync + a pump drive to the first decision, which AdvanceToMainAsync already
    // performs. Drive to P1's main wait BEFORE staging so the pump's opening hand deal does not clobber
    // the synthetic hand card (RD-R3-02 / G2E-002 staging precedent).
    var match = DcgoMatch.CreatePumpDriven(EngineContext.CreateDefault(), new EngineTrace());
    await match.InitializeAsync(BuildMatchConfig());
    EngineContext context = match.Context;
    var db = (CardDatabase)context.CardRepository;
    CardBaseEntityLoader.LoadInto(db);
    var zones = (IZoneStateReader)context.ZoneMover;

    await AdvanceToMainAsync(match, P1);

    // A real Red Lv.2 Digi-Egg (Koromon) hatched into the breeding area, and a real Red Lv.3 (Biyomon,
    // evolution condition Red@2, cost 0) in hand.
    var egg = new HeadlessEntityId("p1:breed:ST1_01");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(egg, new HeadlessEntityId("ST1_01"), P1));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, egg, ChoiceZone.None, ChoiceZone.BreedingArea));
    var rookie = new HeadlessEntityId("p1:hand:ST1_02");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(rookie, new HeadlessEntityId("ST1_02"), P1));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, rookie, ChoiceZone.None, ChoiceZone.Hand));

    // 1) In the MAIN phase, digivolving the breeding egg into the Lv.3 is offered.
    context.MemoryController.Set(0); // evolution cost is 0
    LegalAction digi = match.GetLegalActions(P1).Single(a =>
        a.ActionType == HeadlessActionTypes.Digivolve && a.Id.Value.Contains("ST1_02", StringComparison.Ordinal)
        && a.Id.Value.Contains(egg.Value, StringComparison.Ordinal));

    using (AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(context))
    {
        await match.ApplyActionAsync(digi);
        for (int i = 0; i < 12 && !zones.GetCards(P1, ChoiceZone.BreedingArea).Contains(rookie); i++)
        {
            await match.StepAsync();
        }
    }

    // The Lv.3 now occupies the breeding area (the egg is a digivolution source, not a standalone card).
    AssertTrue(zones.GetCards(P1, ChoiceZone.BreedingArea).Contains(rookie), "the Lv.3 is the breeding permanent's top card");
    AssertTrue(!zones.GetCards(P1, ChoiceZone.BreedingArea).Contains(egg), "the egg is no longer a standalone breeding card (it is a source)");
    AssertEqual("Digimon", DefType(context, zones.GetCards(P1, ChoiceZone.BreedingArea)[0]), "breeding top is a Digimon now");
    AssertEqual(0, zones.GetCards(P1, ChoiceZone.BattleArea).Count, "nothing entered the battle area from the in-breeding digivolution");

    // 2) Next breeding step: the Lv.3 Digimon is now movable to the battle area (GR-002 gate). Under the
    // pump the OLD discrete MoveBreedingToBattle action + manual SetPhase do not exist — the pump owns the
    // phase and surfaces the breeding step as a BreedingDecision CHOICE whose ":breeding:act" candidate
    // performs the move (offered because the breeding top is a movable Digimon, the GR-002 gate). Drive the
    // natural pump cycle to P1's next breeding decision and resolve it with the ACT (move) candidate.
    int digivolveTurn = context.TurnController.Current.TurnNumber;
    await DriveToP1BreedingDecisionAsync(match, digivolveTurn);
    LegalAction act;
    using (AmbientMatchContext.Enter(context))
    {
        act = match.GetLegalActions(P1)
            .Single(a => a.ActionType == HeadlessActionTypes.ResolveChoice && a.Id.Value.EndsWith(":breeding:act", StringComparison.Ordinal));
    }
    using (AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(context))
    {
        await match.ApplyActionAsync(act);
        for (int i = 0; i < 12 && !zones.GetCards(P1, ChoiceZone.BattleArea).Contains(rookie); i++)
        {
            await match.StepAsync();
        }
    }

    AssertEqual(0, zones.GetCards(P1, ChoiceZone.BreedingArea).Count, "breeding area emptied after the move");
    AssertTrue(zones.GetCards(P1, ChoiceZone.BattleArea).Contains(rookie), "the Lv.3 reached the battle area via the breeding ramp");
}

// --- Helpers -------------------------------------------------------------

static string DefType(EngineContext ctx, HeadlessEntityId id) =>
    ctx.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? inst) && inst is not null
        && ctx.CardRepository.TryGetCard(inst.DefinitionId, out CardRecord? def) && def is not null ? def.CardType ?? "?" : "?";

static MatchConfig BuildMatchConfig()
{
    HeadlessPlayerId[] players = { new(1), new(2) };
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { BuildDeck(new HeadlessPlayerId(1), "P1"), BuildDeck(new HeadlessPlayerId(2), "P2") },
        firstPlayerId: new HeadlessPlayerId(1));
    return MatchConfig.Create(players, randomSeed: 17, setup: setup);
}

static PlayerDeckSetup BuildDeck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

// Drive the natural pump cycle (passing each turn player, declining every mulligan/breeding choice) until
// P1 is parked at a BreedingDecision on a turn AFTER the in-breeding digivolution — the seam where the
// pump offers the breeding-to-battle move as a choice candidate.
async Task DriveToP1BreedingDecisionAsync(DcgoMatch match, int afterTurn)
{
    for (int i = 0; i < 200; i++)
    {
        HeadlessTurnState ts = match.Context.TurnController.Current;
        if (ts.TurnPlayerId == P1 && ts.Phase == HeadlessPhase.Breeding && ts.TurnNumber > afterTurn
            && match.HasPendingChoice()
            && match.Context.ChoiceController.PendingRequest!.Type == ChoiceType.BreedingDecision)
        {
            return;
        }
        using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
        if (match.HasPendingChoice())
        {
            HeadlessPlayerId chooser = match.Context.ChoiceController.PendingRequest!.PlayerId;
            LegalAction rc = match.GetLegalActions(chooser).FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice && a.Id.Value.EndsWith(":skip", StringComparison.Ordinal))
                ?? match.GetLegalActions(chooser).First(a => a.ActionType == HeadlessActionTypes.ResolveChoice);
            await match.ApplyActionAsync(rc);
            await match.StepAsync();
            await match.StepAsync();
        }
        else
        {
            HeadlessPlayerId tp = ts.TurnPlayerId ?? P1;
            LegalAction? pass = match.GetLegalActions(tp).FirstOrDefault(a => a.ActionType == HeadlessActionTypes.Pass);
            if (pass is not null)
            {
                await match.ApplyActionAsync(pass);
                await match.StepAsync();
                await match.StepAsync();
            }
            else await match.StepAsync();
        }
    }
    HeadlessTurnState t = match.Context.TurnController.Current;
    throw new InvalidOperationException(
        $"did not reach P1's next breeding decision - phase:{t.Phase}/{t.StepCursor} turn:{t.TurnNumber} player:{t.TurnPlayerId} " +
        $"choice:{match.Context.ChoiceController.PendingRequest?.Type.ToString() ?? "<none>"}");
}

// Drive the pump's natural Active->Draw->Breeding->Main auto-flow to the player's main wait; the OLD
// AdvancePhase step currency is retired (G11-002/F62 precedent). Breeding/Mulligan decisions are declined.
static async Task AdvanceToMainAsync(DcgoMatch match, HeadlessPlayerId playerId)
{
    await StepOnceDriveAsync(match);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, playerId));
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

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
