using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// GR-002: the breeding area may move to the battle area ONLY once its top card is a Digimon — a freshly
// hatched Digi-Egg (level 2, DP 0) cannot walk into the battle area. Mirrors AS-IS Permanent.CanMove
// (`if (!IsDigimon) return false;` + `if (TopCard.IsDigiEgg && DP <= 0) return false;`).
//
// (4b A'-2) PUMP-NATIVE REWRITE. The OLD `new DcgoMatch` + `TurnController.SetPhase(Breeding)` FORCING + the
// discrete `MoveBreedingToBattle` action no longer exist under the cutover driver: the pump OWNS the phase and
// surfaces the breeding step as a BreedingDecision CHOICE whose `:breeding:act` candidate performs the move —
// offered ONLY when the breeding top is a movable Digimon (the GR-002 gate). So breeding is REACHED by the
// natural pump cycle (Active->Draw->Breeding auto-flow, GR-004 c2 breeding:act precedent) rather than forced,
// and the move's legal-registration = the presence of the `:breeding:act` candidate; the negative case (a
// non-movable Digi-Egg) = the pump opens NO breeding move (it auto-passes breeding to main, the egg stays put).
// AdvancePhase/EndTurn / SetPhase / MoveBreedingToBattle currency removed. Assertion intent preserved.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("A freshly-hatched Digi-Egg in breeding CANNOT move to battle", DigiEggCannotMove),
    ("A Digimon in breeding CAN move to battle (and lands there)", DigimonCanMove),
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

async Task DigiEggCannotMove()
{
    var match = await BreedingMatchAsync();
    int stageTurn = match.Context.TurnController.Current.TurnNumber;
    // A Digi-Egg (the only thing a hatch produces) sitting in the breeding area.
    await PlaceInBreedingAsync(match, P1, "EGG", CardType: "DigiEgg", level: 2, dp: 0);

    // (re-source) `MoveBreedingToBattle` NOT a legal action + hatch not offered while occupied -> drive the
    // NATURAL pump cycle to P1's next breeding seam: the pump opens NO breeding move for a non-movable egg (it
    // auto-passes breeding to main). moveOffered == false is the pump equivalent of the missing discrete action.
    bool moveOffered = await DriveToP1NextBreedingSeamAsync(match, stageTurn);
    AssertTrue(!moveOffered, "a level-2 Digi-Egg is NOT offered the breeding-to-battle move");

    // The gate held: the egg never left breeding and no Digi-Egg ever entered the battle area.
    var zones = (IZoneStateReader)match.Context.ZoneMover;
    AssertTrue(zones.GetCards(P1, ChoiceZone.BreedingArea).Contains(new HeadlessEntityId("EGG")),
        "the Digi-Egg stays in the breeding area");
    AssertEqual(0, zones.GetCards(P1, ChoiceZone.BattleArea).Count, "nothing entered the battle area");
}

async Task DigimonCanMove()
{
    var match = await BreedingMatchAsync();
    int stageTurn = match.Context.TurnController.Current.TurnNumber;
    await PlaceInBreedingAsync(match, P1, "ROOKIE", CardType: "Digimon", level: 3, dp: 3000);

    // (re-source) `MoveBreedingToBattle` IS a legal action -> the pump opens a BreedingDecision whose
    // `:breeding:act` (move) candidate is offered because the breeding top is a movable Digimon (the GR-002 gate).
    bool moveOffered = await DriveToP1NextBreedingSeamAsync(match, stageTurn);
    AssertTrue(moveOffered, "a level-3 Digimon IS offered the breeding-to-battle move");

    // Resolve the pump's move candidate and let the move settle.
    var zones = (IZoneStateReader)match.Context.ZoneMover;
    var rookie = new HeadlessEntityId("ROOKIE");
    LegalAction act;
    using (AmbientMatchContext.Enter(match.Context))
    {
        act = match.GetLegalActions(P1)
            .Single(a => a.ActionType == HeadlessActionTypes.ResolveChoice && a.Id.Value.EndsWith(":breeding:act", StringComparison.Ordinal));
    }
    using (AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context))
    {
        await match.ApplyActionAsync(act);
        for (int i = 0; i < 12 && !zones.GetCards(P1, ChoiceZone.BattleArea).Contains(rookie); i++)
        {
            await match.StepAsync();
        }
    }

    AssertEqual(0, zones.GetCards(P1, ChoiceZone.BreedingArea).Count, "breeding area is now empty");
    AssertEqual(1, zones.GetCards(P1, ChoiceZone.BattleArea).Count, "the Digimon moved to the battle area");
    // And the battle area holds a Digimon, never a Digi-Egg.
    AssertTrue(zones.GetCards(P1, ChoiceZone.BattleArea).All(id => DefType(match, id) == "Digimon"),
        "no Digi-Egg ever lands in the battle area");
}

// --- Helpers -------------------------------------------------------------

async Task<DcgoMatch> BreedingMatchAsync()
{
    var match = DcgoMatch.CreatePumpDriven(EngineContext.CreateDefault(), new EngineTrace());
    await match.InitializeAsync(BuildMatchConfig());
    // Drive the pump's natural cadence to P1's first main wait (breeding is auto-flowed, decisions declined).
    await StepOnceDriveAsync(match);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P1));
    return match;
}

// Drive the natural pump cycle (passing each turn player, declining every mulligan/breeding SKIP) until P1's
// NEXT-turn breeding seam. Returns true if a BreedingDecision opened for P1 with a `:breeding:act` (move)
// candidate (parked at the decision for the caller to resolve); returns false if the pump auto-passed breeding
// and P1 reached its next main wait without any breeding move (the non-movable case).
async Task<bool> DriveToP1NextBreedingSeamAsync(DcgoMatch match, int afterTurn)
{
    for (int i = 0; i < 300; i++)
    {
        HeadlessTurnState ts = match.Context.TurnController.Current;
        bool p1NextTurn = ts.TurnPlayerId == P1 && ts.TurnNumber > afterTurn;

        // Parked at P1's next-turn breeding decision -> the move is offered iff a :breeding:act candidate exists.
        if (p1NextTurn && ts.Phase == HeadlessPhase.Breeding && match.HasPendingChoice()
            && match.Context.ChoiceController.PendingRequest!.Type == ChoiceType.BreedingDecision)
        {
            using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
            return match.GetLegalActions(P1).Any(a => a.ActionType == HeadlessActionTypes.ResolveChoice
                && a.Id.Value.EndsWith(":breeding:act", StringComparison.Ordinal));
        }

        // Reached P1's next main wait without a breeding decision -> no move was offered (pump passed breeding).
        if (p1NextTurn && ts.Phase == HeadlessPhase.Main && !match.HasPendingChoice() && !match.IsTerminal())
        {
            return false;
        }

        using (AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context))
        {
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
    }

    HeadlessTurnState t = match.Context.TurnController.Current;
    throw new InvalidOperationException(
        $"did not reach P1's next breeding seam - phase:{t.Phase}/{t.StepCursor} turn:{t.TurnNumber} player:{t.TurnPlayerId} " +
        $"choice:{match.Context.ChoiceController.PendingRequest?.Type.ToString() ?? "<none>"}");
}

static async Task PlaceInBreedingAsync(DcgoMatch match, HeadlessPlayerId owner, string tag, string CardType, int level, int dp)
{
    var cards = (CardDatabase)match.Context.CardRepository;
    var def = new HeadlessEntityId($"{tag}-def");
    cards.Upsert(new CardRecord(def, def.Value, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = level }, CardType: CardType));
    var inst = new HeadlessEntityId(tag);
    match.Context.CardInstanceRepository.Upsert(new CardInstanceRecord(inst, def, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp }));
    await match.Context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, inst, ChoiceZone.None, ChoiceZone.BreedingArea));
}

static string DefType(DcgoMatch match, HeadlessEntityId instanceId) =>
    match.Context.CardInstanceRepository.TryGetInstance(instanceId, out CardInstanceRecord? inst) && inst is not null
        && match.Context.CardRepository.TryGetCard(inst.DefinitionId, out CardRecord? def) && def is not null
        ? def.CardType ?? "?" : "?";

// --- Pump drive helpers (GR-004 idiom) -----------------------------------

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

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
