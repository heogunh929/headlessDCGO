using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// D-7 effect invalidation: a continuous "disable effects" effect turns OFF a card's own effects (AS-IS
// ICardEffect.IsDisabled / IDisableCardEffect). The trigger loop skips effects whose source card is
// invalidated; without the disable they fire normally.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
const string Timing = "OnTestTiming";

var tests = new (string Name, Func<Task> Body)[]
{
    ("IsEffectsDisabled is false with no disable effect", () => Pure(NotDisabledByDefault)),
    ("IsEffectsDisabled is true when a continuous disable targets the card", () => Pure(DisabledByContinuous)),
    ("Player-scope disable invalidates the owner's card", () => Pure(DisabledByPlayerScope)),
    ("A disabled card's triggered effect does NOT fire", DisabledEffectDoesNotFire),
    ("Without the disable, the effect fires", EnabledEffectFires),
    ("A disabled card's CONTINUOUS effect is inert in the gates", () => Pure(DisabledContinuousIsInert)),
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

// --- Unit -----------------------------------------------------------------

void NotDisabledByDefault()
{
    EngineContext context = Board();
    AssertFalse(EffectInvalidation.IsEffectsDisabled(context, new HeadlessEntityId("p1:main:X")), "no disable → enabled");
}

void DisabledByContinuous()
{
    EngineContext context = Board();
    HeadlessEntityId card = new("p1:main:X");
    RegisterDisable(context, targets: new[] { card }, scopePlayer: null);
    AssertTrue(EffectInvalidation.IsEffectsDisabled(context, card), "card-targeted disable → disabled");
}

void DisabledByPlayerScope()
{
    EngineContext context = Board();
    RegisterDisable(context, targets: Array.Empty<HeadlessEntityId>(), scopePlayer: P1);
    AssertTrue(EffectInvalidation.IsEffectsDisabled(context, new HeadlessEntityId("p1:main:X")), "P1's card disabled by player-scope");
    AssertFalse(EffectInvalidation.IsEffectsDisabled(context, new HeadlessEntityId("p2:main:Y")), "P2's card unaffected");
}

void DisabledContinuousIsInert()
{
    EngineContext context = Board();
    HeadlessEntityId buffSource = new("p1:main:X");   // the card whose continuous effect buffs Z
    HeadlessEntityId boosted = new("p2:main:Y");

    // X grants +2000 DP to Y via a continuous modifier (source = X, target = Y).
    var values = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dpDelta"] = 2000 };
    var effectContext = new EffectContext(P1, P1, buffSource, triggerEntityId: null, targetEntityIds: new[] { boosted }, values: values);
    context.EffectRegistry.Register(new EffectBinding(
        new EffectRequest(new HeadlessEntityId("buff"), P1, "Continuous", effectContext),
        keywords: null, EffectQueryRole.Continuous, new[] { EffectInvalidation.Scope }));

    AssertEqual(5000, ContinuousDpGate.ResolveDp(context, boosted, baseDp: 3000), "buff applies while X is enabled");

    // Disable X's effects → its continuous buff becomes inert.
    RegisterDisable(context, targets: new[] { buffSource }, scopePlayer: null);
    AssertEqual(3000, ContinuousDpGate.ResolveDp(context, boosted, baseDp: 3000), "buff inert once X is disabled");
}

// --- E2E (loop) -----------------------------------------------------------

async Task DisabledEffectDoesNotFire()
{
    (DcgoMatch match, RecordingFakeEffect effect) = await CreateMatchAsync();
    RegisterDisable(match.Context, targets: new[] { new HeadlessEntityId("src") }, scopePlayer: null);

    TriggerEventEmitter.Emit(match.Context.GameEventQueue, Timing);
    await match.StepAsync();

    AssertEqual(0, effect.ResolveCalls, "disabled effect did not fire");
}

async Task EnabledEffectFires()
{
    (DcgoMatch match, RecordingFakeEffect effect) = await CreateMatchAsync();

    TriggerEventEmitter.Emit(match.Context.GameEventQueue, Timing);
    await match.StepAsync();

    AssertEqual(1, effect.ResolveCalls, "enabled effect fired");
}

// --- Helpers --------------------------------------------------------------

EngineContext Board()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 16);
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(new("p1:main:X"), new HeadlessEntityId("X"), P1));
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(new("p2:main:Y"), new HeadlessEntityId("Y"), P2));
    return context;
}

void RegisterDisable(EngineContext context, HeadlessEntityId[] targets, HeadlessPlayerId? scopePlayer)
{
    var values = new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [EffectInvalidation.DisableEffectsKey] = true,
    };
    if (scopePlayer is HeadlessPlayerId sp)
    {
        values[PlayerScopeContinuousHelpers.PlayerScopeKey] = true;
        values[PlayerScopeContinuousHelpers.ScopePlayerIdKey] = sp.Value;
    }

    var owner = scopePlayer ?? P1;
    var effectContext = new EffectContext(owner, owner, new HeadlessEntityId("disabler"),
        triggerEntityId: null, targetEntityIds: targets, values: values);
    context.EffectRegistry.Register(new EffectBinding(
        new EffectRequest(new HeadlessEntityId($"disable:{scopePlayer?.Value.ToString() ?? targets.FirstOrDefault().Value}"), owner, "Continuous", effectContext),
        keywords: null, EffectQueryRole.Continuous, new[] { EffectInvalidation.Scope }));
}

async Task<(DcgoMatch, RecordingFakeEffect)> CreateMatchAsync()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 73);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int i = 1; i <= 12; i++)
    {
        cards.Upsert(Digimon($"P1-M{i:D2}"));
        cards.Upsert(Digimon($"P2-M{i:D2}"));
    }

    DcgoMatch match = new(context);
    MatchSetupConfig setup = MatchSetupConfig.Create(new[] { Deck(P1, "P1"), Deck(P2, "P2") }, firstPlayerId: P1);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 73, setup: setup));

    var effect = new RecordingFakeEffect("fx", "src", Timing);
    context.EffectRegistry.Register(new EffectBinding(
        new EffectRequest(new HeadlessEntityId("fx"), P1, Timing,
            new EffectContext(P1, P1, new HeadlessEntityId("src"), triggerEntityId: null, targetEntityIds: Array.Empty<HeadlessEntityId>())),
        effect: effect));
    return (match, effect);
}

static CardRecord Digimon(string id) => new(new HeadlessEntityId(id), id, $"{id} Card", new Dictionary<string, object?>(), CardType: "Digimon");
static PlayerDeckSetup Deck(HeadlessPlayerId p, string prefix) =>
    new(p, Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertFalse(bool v, string label) { if (v) throw new InvalidOperationException($"{label}: expected false."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}

internal sealed class RecordingFakeEffect : IHeadlessCardEffect
{
    public RecordingFakeEffect(string effectId, string sourceId, string timing)
    {
        Definition = new CardEffectDefinition(new HeadlessEntityId(effectId), new HeadlessEntityId(sourceId), name: effectId, timing: timing);
    }

    public CardEffectDefinition Definition { get; }
    public int ResolveCalls { get; private set; }
    public CardEffectCanResolveResult CanResolve(CardEffectResolveContext context) => CardEffectCanResolveResult.Success();
    public ValueTask<EffectResult> ResolveAsync(CardEffectResolveContext context, IEffectMutationSink mutations, CancellationToken cancellationToken = default)
    {
        ResolveCalls++;
        return ValueTask.FromResult(EffectResult.Success("fake"));
    }
}
