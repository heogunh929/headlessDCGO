using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G3.5-D6: the breeding step is a player DECISION, not auto-resolved. The turn player may hatch a
// digitama, move a breeding Digimon, or decline. The decision is surfaced as agent legal actions and is
// reachable through the factored action space.
//
// (4b A-1) RE-POINTED to the pump-dispatch seat (§3.1f/§3.4c c4 judgment). Under the cutover pump the
// breeding step is the AS-IS ChoiceType.BreedingDecision choice-pause (TurnStateMachine.BreedingPhaseAsync
// :333) — the invented AdvancePhase step-cadence surface is retired. The OLD "decline == AdvancePhase"
// assertion is TRANSLATED, intent preserved: decline is now the choice's SKIP lane (canSkip: true), hatch
// is the "breeding:act" ResolveChoice candidate. No AdvancePhase/EndTurn action currency.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
HeadlessEntityId BreedingAct = new("breeding:act");

var tests = new (string Name, Func<Task> Body)[]
{
    ("Breeding offers hatch and decline (no auto-hatch)", BreedingOffersHatchAndDecline),
    ("Declining breeding leaves the digitama unhatched", DeclineLeavesDigitamaUnhatched),
    ("The hatch decision is reachable through the factored action space", HatchReachableViaFactored),
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

async Task BreedingOffersHatchAndDecline()
{
    DcgoMatch match = await BreedingPhaseAsync();

    // The breeding decision is a pending choice owned by the turn player (NOT auto-resolved).
    HeadlessChoiceState choice = match.Context.ChoiceController.Current;
    AssertTrue(choice.IsPending, "breeding decision is pending (a player choice, not auto-resolved)");
    AssertEqual(ChoiceType.BreedingDecision, choice.Type, "breeding decision type");
    AssertEqual(P1, choice.PlayerId!.Value, "turn player owns the breeding decision");

    LegalAction[] legal = Legal(match, P1);
    // Hatch offered: the "breeding:act" ResolveChoice candidate (label "hatch" while the breeding area is empty).
    AssertTrue(legal.Any(a => a.ActionType == HeadlessActionTypes.ResolveChoice && Selects(a, BreedingAct)),
        "hatch offered (breeding:act ResolveChoice candidate)");
    // Decline offered: TRANSLATION of the OLD "AdvancePhase decline" — the choice's SKIP lane (canSkip: true).
    AssertTrue(choice.CanSkip, "decline offered (the breeding choice can be skipped)");
    AssertTrue(legal.Any(a => a.ActionType == HeadlessActionTypes.ResolveChoice && IsSkip(a)),
        "decline is the ResolveChoice skip lane");
    // No move offered: with an empty breeding area the sole candidate is a HATCH (there is nothing to move).
    AssertEqual("hatch", match.Context.ChoiceController.PendingRequest!.Candidates.Single().Label, "sole candidate is hatch, not move");
    // Breeding did NOT auto-hatch on entry.
    AssertEqual(0, Count(match, ChoiceZone.BreedingArea), "breeding area still empty (no auto-hatch)");
}

async Task DeclineLeavesDigitamaUnhatched()
{
    DcgoMatch match = await BreedingPhaseAsync();
    int digitamaBefore = Count(match, ChoiceZone.DigitamaLibrary);

    // Decline = skip the breeding decision (was: AdvancePhase). The pump auto-flows on to the main phase.
    await ExpApply(match, SkipAction(match, P1));
    await ExpDriveUntil(match, m => ExpAtMainWait(m, P1));

    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "declining advances to Main");
    AssertEqual(digitamaBefore, Count(match, ChoiceZone.DigitamaLibrary), "digitama unchanged when declined");
    AssertEqual(0, Count(match, ChoiceZone.BreedingArea), "breeding area empty when declined");
}

async Task HatchReachableViaFactored()
{
    DcgoMatch match = await BreedingPhaseAsync();

    FactoredActionMask mask = match.EncodeFactoredActionMask();
    FactoredAction? hatch = mask.Actions.FirstOrDefault(a =>
        a.Action.ActionType == HeadlessActionTypes.ResolveChoice && Selects(a.Action, BreedingAct));
    AssertTrue(hatch is not null, "hatch is a placed factored action (RL-reachable)");
    AssertTrue(mask.ToMaskVector()[hatch!.Index] == 1d, "hatch's factored index is legal in the mask");

    await ExpApply(match, hatch.Action);
    AssertEqual(1, Count(match, ChoiceZone.BreedingArea), "hatch via factored action produced a breeding Digimon");
}

// --- Harness (pump) ------------------------------------------------------

int Count(DcgoMatch match, ChoiceZone zone) =>
    ((IZoneStateReader)match.Context.ZoneMover).GetCards(P1, zone).Count;

// Drive the pump from StartGame until it parks at the breeding decision choice (turn 1, first player).
async Task<DcgoMatch> BreedingPhaseAsync()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 74);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = DcgoMatch.CreatePumpDriven(context, new EngineTrace());
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") }, firstPlayerId: P1,
        initialSecuritySize: 0, shuffleDecks: false, shuffleDigitamaDecks: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 74, setup: setup));
    await ExpStepOnce(match);

    // Drive the pump to the breeding decision, auto-skipping any prior pump choice (e.g. the StartGame Mulligan).
    for (int i = 0; i < 64 && !(match.HasPendingChoice()
        && match.Context.ChoiceController.Current.Type == ChoiceType.BreedingDecision); i++)
    {
        if (match.HasPendingChoice())
        {
            await ExpApply(match, SkipAction(match, match.Context.ChoiceController.PendingRequest!.PlayerId));
        }
        else
        {
            await ExpStepOnce(match);
        }
    }

    if (!(match.HasPendingChoice() && match.Context.ChoiceController.Current.Type == ChoiceType.BreedingDecision))
    {
        HeadlessTurnState t = match.Context.TurnController.Current;
        throw new InvalidOperationException(
            $"Failed to reach the breeding decision — phase:{t.Phase}/{t.StepCursor} pending:{match.HasPendingChoice()} " +
            $"type:{match.Context.ChoiceController.Current.Type}");
    }

    return match;
}

LegalAction[] Legal(DcgoMatch match, HeadlessPlayerId player)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    return match.GetLegalActions(player).ToArray();
}

LegalAction SkipAction(DcgoMatch match, HeadlessPlayerId player) =>
    Legal(match, player).First(a => a.ActionType == HeadlessActionTypes.ResolveChoice && IsSkip(a));

static bool Selects(LegalAction action, HeadlessEntityId id) =>
    action.Parameters.TryGetValue(HeadlessActionParameterKeys.ChoiceSelectedIds, out object? raw)
    && raw is IEnumerable<HeadlessEntityId> ids && ids.Contains(id);

static bool IsSkip(LegalAction action) =>
    action.Id.Value.EndsWith(":skip", StringComparison.Ordinal);

static bool ExpAtMainWait(DcgoMatch match, HeadlessPlayerId player) =>
    match.Context.TurnController.Current.Phase == HeadlessPhase.Main
    && match.Context.TurnController.Current.TurnPlayerId == player
    && !match.HasPendingChoice()
    && !match.IsTerminal();

static async Task ExpStepOnce(DcgoMatch match)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.StepAsync();
}

async Task ExpApply(DcgoMatch match, LegalAction action)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.ApplyActionAsync(action);
    await match.StepAsync();
    await match.StepAsync();
}

async Task ExpDriveUntil(DcgoMatch match, Func<DcgoMatch, bool> condition)
{
    for (int i = 0; i < 96 && !condition(match); i++)
    {
        await ExpStepOnce(match);
    }

    if (!condition(match))
    {
        throw new InvalidOperationException("EXP drive did not reach the main wait.");
    }
}

static CardRecord Digimon(string id) =>
    new(new HeadlessEntityId(id), id, $"{id} Card", new Dictionary<string, object?>(), CardType: "Digimon");

static PlayerDeckSetup Deck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

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
