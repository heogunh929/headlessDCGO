using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// F-6.2: phase-boundary timing windows. The engine now opens OnStartMainPhase when an advance enters
// the main phase (the original's verified emit point) and OnEndMainPhase / OnEndAttackPhase on the
// Main→(next) transition. Verified end-to-end: an effect bound to each timing fires through the common
// loop at the right phase transition and stays dormant otherwise.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Phase timing constants are defined", () => Pure(ConstantsDefined)),
    ("OnStartMainPhase effect fires when the main phase is entered", StartMainPhaseFires),
    ("OnEndMainPhase effect fires on leaving the main phase, not on entry", EndMainPhaseFires),
    ("OnEndAttackPhase effect fires on leaving the main phase", EndAttackPhaseFires),
    ("An OnEndMainPhase effect does NOT fire merely on entering main", EndMainPhaseDormantOnEntry),
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

static Task Pure(Action body) { body(); return Task.CompletedTask; }

// --- Tests ---------------------------------------------------------------

void ConstantsDefined()
{
    foreach (string t in new[] { TriggerTimings.OnStartMainPhase, TriggerTimings.OnEndMainPhase, TriggerTimings.OnEndAttackPhase })
    {
        AssertTrue(!string.IsNullOrWhiteSpace(t), "timing constant non-empty");
    }
}

async Task StartMainPhaseFires()
{
    (DcgoMatch match, RecordingFakeEffect effect) = await CreateMatchAsync(TriggerTimings.OnStartMainPhase);
    await AdvanceToMainAsync(match, P1);
    AssertEqual(1, effect.ResolveCalls, "OnStartMainPhase effect fired once on main-phase entry");
}

async Task EndMainPhaseFires()
{
    (DcgoMatch match, RecordingFakeEffect effect) = await CreateMatchAsync(TriggerTimings.OnEndMainPhase);
    await AdvanceToMainAsync(match, P1);
    AssertEqual(0, effect.ResolveCalls, "OnEndMainPhase effect does not fire on entry");
    await LeaveMainAsync(match, P1);
    AssertEqual(1, effect.ResolveCalls, "OnEndMainPhase effect fired on leaving the main phase");
}

async Task EndAttackPhaseFires()
{
    (DcgoMatch match, RecordingFakeEffect effect) = await CreateMatchAsync(TriggerTimings.OnEndAttackPhase);
    await AdvanceToMainAsync(match, P1);
    await LeaveMainAsync(match, P1);
    AssertEqual(1, effect.ResolveCalls, "OnEndAttackPhase effect fired on leaving the main phase");
}

async Task EndMainPhaseDormantOnEntry()
{
    (DcgoMatch match, RecordingFakeEffect effect) = await CreateMatchAsync(TriggerTimings.OnEndMainPhase);
    await AdvanceToMainAsync(match, P1);
    AssertEqual(0, effect.ResolveCalls, "OnEndMainPhase stays dormant when only entering main");
}

// --- Phase driving -------------------------------------------------------

async Task AdvanceToMainAsync(DcgoMatch match, HeadlessPlayerId player)
{
    for (int attempt = 0; attempt < 10 && match.GetObservation().Turn.Phase != HeadlessPhase.Main; attempt++)
    {
        LegalAction advance = SingleLegalAction(match, player, HeadlessActionTypes.AdvancePhase);
        await match.ApplyActionAsync(advance);
        await match.StepAsync();
    }

    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "advanced to main");
}

async Task LeaveMainAsync(DcgoMatch match, HeadlessPlayerId player)
{
    // From the main phase the phase-advancing legal action is Pass (Main→End). Apply it so the
    // Main→(next) transition runs through AdvancePhaseAsync and opens the end-of-main windows.
    LegalAction leave = match.GetLegalActions(player)
        .First(a => a.ActionType is HeadlessActionTypes.Pass or HeadlessActionTypes.AdvancePhase);
    await match.ApplyActionAsync(leave);
    await match.StepAsync();
    AssertTrue(match.GetObservation().Turn.Phase != HeadlessPhase.Main, "left the main phase");
}

LegalAction SingleLegalAction(DcgoMatch match, HeadlessPlayerId player, string actionType)
{
    LegalAction[] actions = match.GetLegalActions(player).Where(a => a.ActionType == actionType).ToArray();
    AssertTrue(actions.Length >= 1, $"{actionType} available");
    return actions[0];
}

// --- Harness (mirrors G3.5-W1b) ------------------------------------------

async Task<(DcgoMatch Match, RecordingFakeEffect Effect)> CreateMatchAsync(string timing)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 73);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = new(context);
    MatchSetupConfig setup = MatchSetupConfig.Create(new[] { Deck(P1, "P1"), Deck(P2, "P2") }, firstPlayerId: P1);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 73, setup: setup));

    var effect = new RecordingFakeEffect("fx", "src", timing);
    context.EffectRegistry.Register(new EffectBinding(CreateRequest("fx", "src", timing), effect: effect));
    return (match, effect);
}

static EffectRequest CreateRequest(string effectId, string sourceId, string timing)
{
    var player = new HeadlessPlayerId(1);
    return new EffectRequest(
        new HeadlessEntityId(effectId), player, timing,
        new EffectContext(player, player, new HeadlessEntityId(sourceId), triggerEntityId: null, targetEntityIds: Array.Empty<HeadlessEntityId>()));
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
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}

static void AssertTrue(bool value, string label)
{
    if (!value) throw new InvalidOperationException($"{label}: expected true.");
}

internal sealed class RecordingFakeEffect : IHeadlessCardEffect
{
    public RecordingFakeEffect(string effectId, string sourceId, string timing)
    {
        Definition = new CardEffectDefinition(
            new HeadlessEntityId(effectId), new HeadlessEntityId(sourceId), name: effectId, timing: timing);
    }

    public CardEffectDefinition Definition { get; }

    public int ResolveCalls { get; private set; }

    public CardEffectCanResolveResult CanResolve(CardEffectResolveContext context) => CardEffectCanResolveResult.Success();

    public ValueTask<EffectResult> ResolveAsync(
        CardEffectResolveContext context,
        IEffectMutationSink mutations,
        CancellationToken cancellationToken = default)
    {
        ResolveCalls++;
        return ValueTask.FromResult(EffectResult.Success("fake resolved"));
    }
}
