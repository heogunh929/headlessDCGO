using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// GPT-#1: LegalActionSetValidator.ValueEquals must compare collection-valued parameters element-wise.
// Convert.ToString(array) returns the TYPE NAME, so before the fix any two same-typed arrays compared
// equal — letting a ResolveChoice that selected an ILLEGAL candidate pass the A1 legality boundary
// (its ChoiceSelectedIds is the only distinguishing parameter). This test drives the real validator.

HeadlessPlayerId P1 = new(1);

var tests = new (string Name, Func<Task> Body)[]
{
    ("A ResolveChoice selecting a legal candidate is accepted", LegalCandidateAccepted),
    ("A ResolveChoice selecting a NON-candidate is rejected (array compared by content)", NonCandidateRejected),
    ("Each distinct legal candidate selection is accepted", EachCandidateAccepted),
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

async Task LegalCandidateAccepted()
{
    DcgoMatch match = await ValidatedMatchWithChoiceAsync("c1", "c2");
    var validator = new LegalActionSetValidator();

    LegalityVerdict verdict = validator.Validate(
        HeadlessActionFactory.ResolveChoice(P1, ChoiceResult.Select(new HeadlessEntityId("c1"))),
        match.Context);

    AssertTrue(verdict.IsLegal, "selecting candidate c1 is legal");
}

async Task NonCandidateRejected()
{
    DcgoMatch match = await ValidatedMatchWithChoiceAsync("c1", "c2");
    var validator = new LegalActionSetValidator();

    // "c3" is NOT one of the pending choice's candidates. It differs from every legal ResolveChoice
    // ONLY in the ChoiceSelectedIds array. Before the deep-equality fix this passed (array == array by
    // type name); now it must be rejected.
    LegalityVerdict verdict = validator.Validate(
        HeadlessActionFactory.ResolveChoice(P1, ChoiceResult.Select(new HeadlessEntityId("c3"))),
        match.Context);

    AssertTrue(!verdict.IsLegal, "selecting a non-candidate must be rejected by the boundary");
}

async Task EachCandidateAccepted()
{
    DcgoMatch match = await ValidatedMatchWithChoiceAsync("c1", "c2");
    var validator = new LegalActionSetValidator();

    foreach (string candidate in new[] { "c1", "c2" })
    {
        LegalityVerdict verdict = validator.Validate(
            HeadlessActionFactory.ResolveChoice(P1, ChoiceResult.Select(new HeadlessEntityId(candidate))),
            match.Context);
        AssertTrue(verdict.IsLegal, $"candidate {candidate} accepted (no over-rejection)");
    }
}

// --- Harness -------------------------------------------------------------

async Task<DcgoMatch> ValidatedMatchWithChoiceAsync(params string[] candidateIds)
{
    DcgoMatch match = new(
        EngineContext.CreateDefault(),
        new EngineTrace(),
        actionProcessor: null,
        actionLegality: new LegalActionSetValidator());

    HeadlessPlayerId[] players = { new(1), new(2) };
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { BuildDeck(new HeadlessPlayerId(1), "P1"), BuildDeck(new HeadlessPlayerId(2), "P2") },
        firstPlayerId: new HeadlessPlayerId(1));
    await match.InitializeAsync(MatchConfig.Create(players, randomSeed: 17, setup: setup));

    LegalAction request = HeadlessActionFactory.RequestChoice(
        P1, ChoiceType.Card, message: "pick", minCount: 1, maxCount: 1, canSkip: false,
        sourceZone: ChoiceZone.Hand, candidateIds: candidateIds.Select(id => new HeadlessEntityId(id)).ToArray());
    await match.ApplyActionAsync(request);
    await match.StepAsync();

    if (!match.Context.ChoiceController.Current.IsPending)
    {
        throw new InvalidOperationException("Choice was not pending after RequestChoice.");
    }

    return match;
}

static PlayerDeckSetup BuildDeck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

static void AssertTrue(bool value, string label)
{
    if (!value) throw new InvalidOperationException($"{label}: expected true.");
}
