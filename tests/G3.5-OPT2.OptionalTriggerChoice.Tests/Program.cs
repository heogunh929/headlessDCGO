using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// #2 (optional triggers, Stage 5 window model): a MANDATORY ("강제발동") and an OPTIONAL ("선택발동",
// CardEffectDefinition.IsOptional) trigger fire SIMULTANEOUSLY, so the controlling player ORDERS them (RD-14/15)
// via the window's order choice — nothing auto-resolves first. When the optional is picked it is confirmed
// yes/no (RD-13, AS-IS Activate_Optional): accepting resolves it, declining leaves it unresolved. Mandatory vs
// optional is still distinguished per-effect from the bound effect's Definition.IsOptional.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
const string Timing = "OnTestTiming";

var tests = new (string Name, Func<Task> Body)[]
{
    ("Simultaneous mandatory+optional open a player ORDER choice (nothing auto-fires)", MandatoryAutoOptionalPauses),
    ("Ordering the mandatory first then ACCEPTING the optional resolves both", ActivateOptional),
    ("Declining the optional at its confirm leaves it unresolved (mandatory still fires)", SkipOptional),
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

    // Nothing auto-resolves: the two simultaneous triggers open the window's ORDER choice for the player.
    AssertEqual(0, h.Mandatory.ResolveCalls, "mandatory trigger did NOT auto-resolve before the player orders");
    AssertEqual(0, h.Optional.ResolveCalls, "optional trigger did NOT auto-resolve");
    AssertTrue(h.Match.Context.ChoiceController.Current.IsPending, "a window choice is pending");
    AssertEqual(ChoiceType.WindowChoice, h.Match.Context.ChoiceController.Current.Type, "pending choice is the window order prompt");
    AssertTrue(
        h.Match.GetLegalActions(P1).Any(a => a.ActionType == HeadlessActionTypes.ResolveChoice),
        "agent is offered ResolveChoice actions for the order prompt");
}

async Task ActivateOptional()
{
    var h = await TriggeredMatchAsync();

    // Pick the mandatory first (order choice), which resolves it and opens the optional's yes/no confirm; then
    // accept the optional. Both fire.
    await Apply(h.Match, HeadlessActionFactory.ResolveChoice(P1, ChoiceResult.Select(new HeadlessEntityId("mand-fx"))));
    await Apply(h.Match, HeadlessActionFactory.ResolveChoice(P1, ChoiceResult.Select(new HeadlessEntityId("opt-fx"))));

    AssertEqual(1, h.Mandatory.ResolveCalls, "the ordered mandatory trigger resolved");
    AssertEqual(1, h.Optional.ResolveCalls, "the accepted optional trigger resolved");
    AssertFalse(h.Match.Context.ChoiceController.Current.IsPending, "no choice remains pending");
}

async Task SkipOptional()
{
    var h = await TriggeredMatchAsync();

    // Pick the mandatory first (resolves it, opens the optional confirm), then DECLINE the optional.
    await Apply(h.Match, HeadlessActionFactory.ResolveChoice(P1, ChoiceResult.Select(new HeadlessEntityId("mand-fx"))));
    await Apply(h.Match, HeadlessActionFactory.ResolveChoice(P1, ChoiceResult.Skip()));

    AssertEqual(1, h.Mandatory.ResolveCalls, "the ordered mandatory trigger still resolved");
    AssertEqual(0, h.Optional.ResolveCalls, "the declined optional trigger does NOT resolve");
    AssertFalse(h.Match.Context.ChoiceController.Current.IsPending, "choice resolved (declined)");
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

    // Establish P1's Main directly on the substrate turn controller (W1b/G7-005 idiom) — the OLD
    // AdvancePhase step currency is retired (4b B6); this synth registry fixture only needs a turn
    // player and a Main phase, not the OLD step driver.
    context.TurnController.SetPhase(HeadlessPhase.Main);

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
