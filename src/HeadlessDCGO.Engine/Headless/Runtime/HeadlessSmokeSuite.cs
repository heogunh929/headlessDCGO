namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Services;

// TODO: Move this suite into executable tests once a .NET SDK/test project is available.
public sealed class HeadlessSmokeSuite(
    IReadOnlyList<HeadlessSmokeSuiteCase>? cases = null,
    HeadlessScenarioVerifier? verifier = null)
{
    private readonly IReadOnlyList<HeadlessSmokeSuiteCase> _cases = cases ?? DefaultCases();
    private readonly HeadlessScenarioVerifier _verifier = verifier ?? new HeadlessScenarioVerifier();

    public static IReadOnlyList<HeadlessSmokeSuiteCase> DefaultCases()
    {
        HeadlessPlayerId playerOne = new(1);
        HeadlessRlEnvironmentOptions playerOnePerspective =
            HeadlessSmokeScenarios.EnvironmentOptionsForPerspective(playerOne);
        HeadlessRlEnvironmentOptions rejectIllegalActionOptions = playerOnePerspective with
        {
            RejectActionsOutsideMask = true,
            InvalidActionPenalty = -1d
        };
        HeadlessScenarioRunnerOptions actionIdSelection = new()
        {
            ActionSelectionMode = HeadlessScenarioActionSelectionMode.ActionId
        };
        HeadlessScenarioRunnerOptions encodedKeySelection = new()
        {
            ActionSelectionMode = HeadlessScenarioActionSelectionMode.EncodedKey
        };
        HeadlessScenarioRunnerOptions actionIndexSelection = new()
        {
            ActionSelectionMode = HeadlessScenarioActionSelectionMode.ActionIndex
        };
        HeadlessPolicyEpisodeRunnerOptions singlePolicyStep = new()
        {
            MaxSteps = 1
        };

        return new[]
        {
            new HeadlessSmokeSuiteCase(
                Name: "empty-two-player",
                Scenario: HeadlessSmokeScenarios.EmptyTwoPlayer(),
                Expectation: HeadlessSmokeScenarios.EmptyTwoPlayerExpectation(),
                EnvironmentOptions: playerOnePerspective),
            new HeadlessSmokeSuiteCase(
                Name: "random-seed-is-observed",
                Scenario: HeadlessSmokeScenarios.RandomSeedIsObserved(),
                Expectation: HeadlessSmokeScenarios.RandomSeedIsObservedExpectation(),
                EnvironmentOptions: playerOnePerspective),
            new HeadlessSmokeSuiteCase(
                Name: "seed-deck-from-deck-list",
                Scenario: HeadlessSmokeScenarios.SeedDeckFromDeckList(),
                Expectation: HeadlessSmokeScenarios.SeedDeckFromDeckListExpectation(),
                EnvironmentOptions: playerOnePerspective),
            new HeadlessSmokeSuiteCase(
                Name: "consume-seeded-legal-action",
                Scenario: HeadlessSmokeScenarios.ConsumeSeededLegalAction(),
                Expectation: HeadlessSmokeScenarios.ConsumeSeededLegalActionExpectation(),
                EnvironmentOptions: playerOnePerspective),
            new HeadlessSmokeSuiteCase(
                Name: "consume-seeded-legal-action-by-id",
                Scenario: HeadlessSmokeScenarios.ConsumeSeededLegalAction("consume-seeded-legal-action-by-id"),
                Expectation: HeadlessSmokeScenarios.ConsumeSeededLegalActionExpectation(),
                EnvironmentOptions: playerOnePerspective,
                RunnerOptions: actionIdSelection),
            new HeadlessSmokeSuiteCase(
                Name: "consume-seeded-legal-action-by-encoded-key",
                Scenario: HeadlessSmokeScenarios.ConsumeSeededLegalAction("consume-seeded-legal-action-by-encoded-key"),
                Expectation: HeadlessSmokeScenarios.ConsumeSeededLegalActionExpectation(),
                EnvironmentOptions: playerOnePerspective,
                RunnerOptions: encodedKeySelection),
            new HeadlessSmokeSuiteCase(
                Name: "consume-seeded-legal-action-by-index",
                Scenario: HeadlessSmokeScenarios.ConsumeSeededLegalAction("consume-seeded-legal-action-by-index"),
                Expectation: HeadlessSmokeScenarios.ConsumeSeededLegalActionExpectation(),
                EnvironmentOptions: playerOnePerspective,
                RunnerOptions: actionIndexSelection),
            new HeadlessSmokeSuiteCase(
                Name: "policy-consume-seeded-legal-action",
                Scenario: HeadlessSmokeScenarios.ConsumeSeededLegalAction("policy-consume-seeded-legal-action"),
                Expectation: HeadlessSmokeScenarios.ConsumeSeededLegalActionExpectation(),
                EnvironmentOptions: playerOnePerspective,
                ActionPolicy: new FirstLegalActionPolicy(),
                PolicyRunnerOptions: singlePolicyStep),
            new HeadlessSmokeSuiteCase(
                Name: "reject-action-outside-mask",
                Scenario: HeadlessSmokeScenarios.RejectActionOutsideMask(),
                Expectation: HeadlessSmokeScenarios.RejectActionOutsideMaskExpectation(),
                EnvironmentOptions: rejectIllegalActionOptions),
            new HeadlessSmokeSuiteCase(
                Name: "terminal-win",
                Scenario: HeadlessSmokeScenarios.TerminalWin(),
                Expectation: HeadlessSmokeScenarios.TerminalWinExpectation(),
                EnvironmentOptions: playerOnePerspective),
            new HeadlessSmokeSuiteCase(
                Name: "terminal-draw",
                Scenario: HeadlessSmokeScenarios.TerminalDraw(),
                Expectation: HeadlessSmokeScenarios.TerminalDrawExpectation(),
                EnvironmentOptions: playerOnePerspective),
            new HeadlessSmokeSuiteCase(
                Name: "move-seeded-card-to-hand",
                Scenario: HeadlessSmokeScenarios.MoveSeededCardToHand(),
                Expectation: HeadlessSmokeScenarios.MoveSeededCardToHandExpectation(),
                EnvironmentOptions: playerOnePerspective),
            new HeadlessSmokeSuiteCase(
                Name: "draw-seeded-library-card",
                Scenario: HeadlessSmokeScenarios.DrawSeededLibraryCard(),
                Expectation: HeadlessSmokeScenarios.DrawSeededLibraryCardExpectation(),
                EnvironmentOptions: playerOnePerspective),
            new HeadlessSmokeSuiteCase(
                Name: "add-security-from-seeded-library",
                Scenario: HeadlessSmokeScenarios.AddSecurityFromSeededLibrary(),
                Expectation: HeadlessSmokeScenarios.AddSecurityFromSeededLibraryExpectation(),
                EnvironmentOptions: playerOnePerspective),
            new HeadlessSmokeSuiteCase(
                Name: "trash-seeded-security",
                Scenario: HeadlessSmokeScenarios.TrashSeededSecurity(),
                Expectation: HeadlessSmokeScenarios.TrashSeededSecurityExpectation(),
                EnvironmentOptions: playerOnePerspective),
            new HeadlessSmokeSuiteCase(
                Name: "move-seeded-cards-to-battle-and-breeding",
                Scenario: HeadlessSmokeScenarios.MoveSeededCardsToBattleAndBreeding(),
                Expectation: HeadlessSmokeScenarios.MoveSeededCardsToBattleAndBreedingExpectation(),
                EnvironmentOptions: playerOnePerspective),
            new HeadlessSmokeSuiteCase(
                Name: "hatch-seeded-digitama",
                Scenario: HeadlessSmokeScenarios.HatchSeededDigitama(),
                Expectation: HeadlessSmokeScenarios.HatchSeededDigitamaExpectation(),
                EnvironmentOptions: playerOnePerspective),
            new HeadlessSmokeSuiteCase(
                Name: "move-seeded-breeding-to-battle",
                Scenario: HeadlessSmokeScenarios.MoveSeededBreedingToBattle(),
                Expectation: HeadlessSmokeScenarios.MoveSeededBreedingToBattleExpectation(),
                EnvironmentOptions: playerOnePerspective),
            new HeadlessSmokeSuiteCase(
                Name: "declare-seeded-attack",
                Scenario: HeadlessSmokeScenarios.DeclareSeededAttack(),
                Expectation: HeadlessSmokeScenarios.DeclareSeededAttackExpectation(),
                EnvironmentOptions: playerOnePerspective),
            new HeadlessSmokeSuiteCase(
                Name: "resolve-seeded-attack",
                Scenario: HeadlessSmokeScenarios.ResolveSeededAttack(),
                Expectation: HeadlessSmokeScenarios.ResolveSeededAttackExpectation(),
                EnvironmentOptions: playerOnePerspective),
            new HeadlessSmokeSuiteCase(
                Name: "clear-seeded-attack",
                Scenario: HeadlessSmokeScenarios.ClearSeededAttack(),
                Expectation: HeadlessSmokeScenarios.ClearSeededAttackExpectation(),
                EnvironmentOptions: playerOnePerspective),
            new HeadlessSmokeSuiteCase(
                Name: "request-seeded-card-choice",
                Scenario: HeadlessSmokeScenarios.RequestSeededCardChoice(),
                Expectation: HeadlessSmokeScenarios.RequestSeededCardChoiceExpectation(),
                EnvironmentOptions: playerOnePerspective),
            new HeadlessSmokeSuiteCase(
                Name: "resolve-seeded-card-choice",
                Scenario: HeadlessSmokeScenarios.ResolveSeededCardChoice(),
                Expectation: HeadlessSmokeScenarios.ResolveSeededCardChoiceExpectation(),
                EnvironmentOptions: playerOnePerspective),
            new HeadlessSmokeSuiteCase(
                Name: "enqueue-and-resolve-effect",
                Scenario: HeadlessSmokeScenarios.EnqueueAndResolveEffect(),
                Expectation: HeadlessSmokeScenarios.EnqueueAndResolveEffectExpectation(),
                EnvironmentOptions: playerOnePerspective),
            new HeadlessSmokeSuiteCase(
                Name: "advance-phase-to-draw",
                Scenario: HeadlessSmokeScenarios.AdvancePhaseToDraw(),
                Expectation: HeadlessSmokeScenarios.AdvancePhaseToDrawExpectation(),
                EnvironmentOptions: playerOnePerspective),
            new HeadlessSmokeSuiteCase(
                Name: "memory-set-add-pay",
                Scenario: HeadlessSmokeScenarios.MemorySetAddPay(),
                Expectation: HeadlessSmokeScenarios.MemorySetAddPayExpectation(),
                EnvironmentOptions: playerOnePerspective)
        };
    }

    public async Task<HeadlessSmokeSuiteResult> RunAsync(CancellationToken cancellationToken = default)
    {
        List<HeadlessSmokeSuiteCaseResult> results = new();

        foreach (HeadlessSmokeSuiteCase smokeCase in _cases)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Func<HeadlessRlEnvironment> environmentFactory =
                () => new HeadlessRlEnvironment(options: smokeCase.EnvironmentOptions);
            HeadlessEpisodeResult episode = smokeCase.ActionPolicy is null
                ? await new HeadlessScenarioRunner(
                        environmentFactory: environmentFactory,
                        options: smokeCase.RunnerOptions)
                    .RunAsync(smokeCase.Scenario, cancellationToken)
                    .ConfigureAwait(false)
                : await new HeadlessPolicyEpisodeRunner(
                        environmentFactory: environmentFactory,
                        options: smokeCase.PolicyRunnerOptions)
                    .RunAsync(smokeCase.Scenario, smokeCase.ActionPolicy, cancellationToken)
                    .ConfigureAwait(false);
            HeadlessScenarioVerificationResult verification = _verifier.Verify(
                episode,
                smokeCase.Expectation);

            results.Add(new HeadlessSmokeSuiteCaseResult(smokeCase, episode, verification));
        }

        return new HeadlessSmokeSuiteResult(results.ToArray());
    }
}

public sealed record HeadlessSmokeSuiteCase(
    string Name,
    HeadlessScenario Scenario,
    HeadlessScenarioExpectation Expectation,
    HeadlessRlEnvironmentOptions EnvironmentOptions,
    HeadlessScenarioRunnerOptions? RunnerOptions = null,
    IHeadlessActionPolicy? ActionPolicy = null,
    HeadlessPolicyEpisodeRunnerOptions? PolicyRunnerOptions = null);

public sealed record HeadlessSmokeSuiteCaseResult(
    HeadlessSmokeSuiteCase Case,
    HeadlessEpisodeResult Episode,
    HeadlessScenarioVerificationResult Verification)
{
    public bool Passed => Verification.Passed;
}

public sealed record HeadlessSmokeSuiteResult(IReadOnlyList<HeadlessSmokeSuiteCaseResult> Cases)
{
    public bool Passed => Cases.All(result => result.Passed);

    public int CaseCount => Cases.Count;

    public int FailedCaseCount => Cases.Count(result => !result.Passed);

    public IReadOnlyList<HeadlessSmokeSuiteCaseResult> FailedCases()
    {
        return Cases
            .Where(result => !result.Passed)
            .ToArray();
    }

    public HeadlessSmokeSuiteReport ToReport()
    {
        return new HeadlessSmokeSuiteReporter().Create(this);
    }
}
