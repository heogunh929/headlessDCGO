using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// #2 (optional triggers like the original): a MANDATORY ("강제발동") trigger resolves immediately, while
// an OPTIONAL ("선택발동", CardEffectDefinition.IsOptional) trigger is NOT auto-fired — it is surfaced as
// an agent ResolveChoice decision (activate-which / skip) via the OptionalPromptQueue + A2. Mandatory vs
// optional is distinguished per-effect from the bound effect's Definition.IsOptional.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
const string Timing = "OnTestTiming";

var tests = new (string Name, Func<Task> Body)[]
{
    ("Mandatory fires immediately; optional pauses for an agent choice", MandatoryAutoOptionalPauses),
    ("Agent activating the optional resolves it", ActivateOptional),
    ("Agent skipping the optional leaves it unresolved", SkipOptional),
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

async Task MandatoryAutoOptionalPauses()
{
    var h = await TriggeredMatchAsync();

    AssertEqual(1, h.Mandatory.ResolveCalls, "mandatory trigger resolved immediately");
    AssertEqual(0, h.Optional.ResolveCalls, "optional trigger NOT auto-resolved");
    AssertTrue(h.Match.Context.ChoiceController.Current.IsPending, "an optional prompt is pending");
    AssertEqual(ChoiceType.OptionalEffect, h.Match.Context.ChoiceController.Current.Type, "pending choice is an optional-effect prompt");
    AssertTrue(
        h.Match.GetLegalActions(P1).Any(a => a.ActionType == HeadlessActionTypes.ResolveChoice),
        "agent is offered ResolveChoice actions for the optional prompt");
}

async Task ActivateOptional()
{
    var h = await TriggeredMatchAsync();

    // Activate the optional effect (its EffectId is the choice candidate).
    await Apply(h.Match, HeadlessActionFactory.ResolveChoice(P1, ChoiceResult.Select(new HeadlessEntityId("opt-fx"))));

    AssertEqual(1, h.Optional.ResolveCalls, "activated optional trigger resolved");
    AssertFalse(h.Match.Context.ChoiceController.Current.IsPending, "no choice remains pending");
}

async Task SkipOptional()
{
    var h = await TriggeredMatchAsync();

    await Apply(h.Match, HeadlessActionFactory.ResolveChoice(P1, ChoiceResult.Skip()));

    AssertEqual(0, h.Optional.ResolveCalls, "skipped optional trigger does NOT resolve");
    AssertFalse(h.Match.Context.ChoiceController.Current.IsPending, "choice resolved (skipped)");
}

// --- Harness -------------------------------------------------------------

static async Task<StepResult> Apply(DcgoMatch match, LegalAction action)
{
    await match.ApplyActionAsync(action);
    return await match.StepAsync();
}

async Task<Harness> TriggeredMatchAsync()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 74);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = new(context);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") }, firstPlayerId: P1);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 74, setup: setup));

    // Advance to Main so a turn player is established (P1).
    for (var attempt = 0; attempt < 8 && match.GetObservation().Turn.Phase != HeadlessPhase.Main; attempt++)
    {
        await Apply(match, HeadlessActionFactory.AdvancePhase(P1));
    }

    var mandatory = new RecordingEffect("mand-fx", isOptional: false);
    var optional = new RecordingEffect("opt-fx", isOptional: true);
    context.EffectRegistry.Register(new EffectBinding(Request("mand-fx", P1), effect: mandatory));
    context.EffectRegistry.Register(new EffectBinding(Request("opt-fx", P1), effect: optional));

    // Fire the timing window (global; both effects bound to it collect). Then run the loop.
    context.GameEventQueue.Publish(new GameEvent(0, GameEventType.StateChanged, "trigger",
        new Dictionary<string, object?>(StringComparer.Ordinal) { [AutoProcessingTriggerCollector.TriggerTimingKey] = Timing }));
    await match.StepAsync();

    return new Harness(match, mandatory, optional);
}

static EffectRequest Request(string effectId, HeadlessPlayerId controller) =>
    new(new HeadlessEntityId(effectId), controller, Timing,
        new EffectContext(controller, controller, new HeadlessEntityId($"src-{effectId}"), triggerEntityId: null, targetEntityIds: Array.Empty<HeadlessEntityId>()));

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

static void AssertFalse(bool value, string label)
{
    if (value) throw new InvalidOperationException($"{label}: expected false.");
}

internal sealed record Harness(DcgoMatch Match, RecordingEffect Mandatory, RecordingEffect Optional);

internal sealed class RecordingEffect : IHeadlessCardEffect
{
    public RecordingEffect(string effectId, bool isOptional)
    {
        Definition = new CardEffectDefinition(
            new HeadlessEntityId(effectId), new HeadlessEntityId($"src-{effectId}"), name: effectId, timing: "OnTestTiming", isOptional: isOptional);
    }

    public CardEffectDefinition Definition { get; }

    public int ResolveCalls { get; private set; }

    public CardEffectCanResolveResult CanResolve(CardEffectResolveContext context) => CardEffectCanResolveResult.Success();

    public ValueTask<EffectResult> ResolveAsync(CardEffectResolveContext context, IEffectMutationSink mutations, CancellationToken cancellationToken = default)
    {
        ResolveCalls++;
        return ValueTask.FromResult(EffectResult.Success());
    }
}
