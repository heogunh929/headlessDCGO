using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G3.5-D3: the common loop orders simultaneously-collected triggers before resolving them — turn-player
// triggers first, then non-turn (AS-IS MultipleSkills). (Stage 5) among ONE player's simultaneous triggers the
// controlling player CHOOSES the order (RD-14/15) rather than a fixed mandatory-before-optional drain, and an
// optional is confirmed yes/no when it is picked (RD-13); both still fire when accepted.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
List<string> resolveOrder = new();

var tests = new (string Name, Func<Task> Body)[]
{
    ("Turn-player triggers resolve before non-turn-player triggers", TurnPlayerPriority),
    ("Mandatory triggers resolve before optional, and optionals still fire", MandatoryBeforeOptional),
};

var failures = new List<string>();
foreach (var test in tests)
{
    resolveOrder.Clear();
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

async Task TurnPlayerPriority()
{
    DcgoMatch match = await MainPhaseMatchAsync();
    EngineContext context = match.Context;

    // Register the NON-turn player's effect FIRST so collection order is [P2, P1]; ordering must
    // still resolve the turn player's (P1) trigger first.
    Register(context, "fx-P2", P2, "T");
    Register(context, "fx-P1", P1, "T");
    Emit(context, timing: "T", optional: false);

    await new GameFlowProcessor().RunToStableAsync(context);

    AssertOrder("fx-P1", "fx-P2");
}

async Task MandatoryBeforeOptional()
{
    DcgoMatch match = await MainPhaseMatchAsync();
    EngineContext context = match.Context;
    var processor = new MetadataActionProcessor();

    Register(context, "fx-opt", P1, "O");
    Register(context, "fx-mand", P1, "M");
    Emit(context, timing: "M", optional: false);
    Emit(context, timing: "O", optional: true);

    // (Stage 5) the two simultaneous P1 triggers are the player's to ORDER — the window opens a choice rather
    // than draining them in a fixed order. Drive the agent picking the mandatory first, then accepting the
    // optional at its yes/no confirm; both fire, in the chosen order.
    await new GameFlowProcessor().RunToStableAsync(context);
    AssertTrue(context.ChoiceController.Current.IsPending, "the window opened an order choice for the two simultaneous triggers");

    await processor.ProcessAsync(HeadlessActionFactory.ResolveChoice(P1, ChoiceResult.Select(new HeadlessEntityId("fx-mand"))), context);
    await processor.ProcessAsync(HeadlessActionFactory.ResolveChoice(P1, ChoiceResult.Select(new HeadlessEntityId("fx-opt"))), context);

    AssertOrder("fx-mand", "fx-opt");
}

// --- Helpers -------------------------------------------------------------

void AssertOrder(params string[] expected)
{
    if (resolveOrder.Count != expected.Length || !resolveOrder.SequenceEqual(expected))
    {
        throw new InvalidOperationException(
            $"resolution order: expected [{string.Join(", ", expected)}], got [{string.Join(", ", resolveOrder)}].");
    }
}

static void AssertTrue(bool value, string label)
{
    if (!value) throw new InvalidOperationException($"{label}: expected true.");
}

void Register(EngineContext context, string effectId, HeadlessPlayerId controller, string timing)
{
    var effect = new OrderRecordingEffect(effectId, resolveOrder);
    context.EffectRegistry.Register(new EffectBinding(
        new EffectRequest(new HeadlessEntityId(effectId), controller, timing,
            new EffectContext(controller, controller, new HeadlessEntityId($"src-{effectId}"),
                triggerEntityId: null, targetEntityIds: Array.Empty<HeadlessEntityId>())),
        effect: effect));
}

static void Emit(EngineContext context, string timing, bool optional)
{
    var metadata = new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [AutoProcessingTriggerCollector.TriggerTimingKey] = timing,
    };
    if (optional)
    {
        metadata[AutoProcessingTriggerCollector.TriggerKindKey] = "Optional";
    }

    context.GameEventQueue.Publish(new GameEvent(0, GameEventType.StateChanged, $"evt:{timing}", metadata));
}

async Task<DcgoMatch> MainPhaseMatchAsync()
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

    for (var attempt = 0; attempt < 8 && match.GetObservation().Turn.Phase != HeadlessPhase.Main; attempt++)
    {
        LegalAction advance = match.GetLegalActions(P1).Single(a => a.ActionType == HeadlessActionTypes.AdvancePhase);
        await match.ApplyActionAsync(advance);
        await match.StepAsync();
    }

    if (match.GetObservation().Turn.Phase != HeadlessPhase.Main)
    {
        throw new InvalidOperationException("Failed to reach Main phase.");
    }

    return match;
}

static CardRecord Digimon(string id) =>
    new(new HeadlessEntityId(id), id, $"{id} Card", new Dictionary<string, object?>(), CardType: "Digimon");

static PlayerDeckSetup Deck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

internal sealed class OrderRecordingEffect : IHeadlessCardEffect
{
    private readonly List<string> _order;

    public OrderRecordingEffect(string effectId, List<string> order)
    {
        Definition = new CardEffectDefinition(new HeadlessEntityId(effectId), new HeadlessEntityId($"src-{effectId}"), name: effectId, timing: "any");
        _order = order;
    }

    public CardEffectDefinition Definition { get; }

    public CardEffectCanResolveResult CanResolve(CardEffectResolveContext context) => CardEffectCanResolveResult.Success();

    public ValueTask<EffectResult> ResolveAsync(CardEffectResolveContext context, IEffectMutationSink mutations, CancellationToken cancellationToken = default)
    {
        _order.Add(Definition.EffectId.Value);
        return ValueTask.FromResult(EffectResult.Success());
    }
}
