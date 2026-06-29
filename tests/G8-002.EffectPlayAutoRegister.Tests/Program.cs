using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G8-002: a card played onto the field by an EFFECT (MatchStateMutationSink PlayCardKind) auto-registers
// its ported effects, exactly like a hand play — verified through the real scheduler/sink path that
// EngineContext.CreateDefault wires (onCardEnteredPlay -> CardEffectRegistrar.RegisterCard).

HeadlessPlayerId P1 = new(1);
HeadlessEntityId Played = new("p1:trash:ST7_10");

var tests = new (string Name, Func<Task> Body)[]
{
    ("Effect-playing ST7_10 from trash auto-registers its effects", EffectPlayRegisters),
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

async Task EffectPlayRegisters()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 802);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId("ST7_10"), "ST7_10", "MetalGreymon", new Dictionary<string, object?>(), CardType: "Digimon"));
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(Played, new HeadlessEntityId("ST7_10"), P1));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, Played, ChoiceZone.None, ChoiceZone.Trash));

    // An effect that plays ST7_10 onto the field. Resolved through context.EffectScheduler, whose sink
    // carries the G8-002 onCardEnteredPlay hook.
    var source = new HeadlessEntityId("p1:battle:SRC");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(source, new HeadlessEntityId("ST7_10"), P1));
    var effect = new PlayCardEmitEffect(source, Played);
    var request = new EffectRequest(effect.Definition.EffectId, P1, effect.Definition.Timing,
        new EffectContext(P1, source, new Dictionary<string, object?>(StringComparer.Ordinal)));
    context.EffectRegistry.Register(new EffectBinding(request, keywords: null, EffectQueryRole.None, Array.Empty<string>(), effect, duration: null));

    context.EffectScheduler.Enqueue(request);
    await context.EffectScheduler.ResolveAllAsync();

    AssertTrue(((IZoneStateReader)context.ZoneMover).GetCards(P1, ChoiceZone.BattleArea).Contains(Played), "ST7_10 played onto the field");
    AssertEqual(2, ContinuousModifierGate.ResolveSecurityAttack(context, Played, baseSecurityAttack: 1), "SA +1 auto-active after the effect-play");
    AssertTrue(context.EffectRegistry.GetKeywordEffects("Piercing").Count >= 1, "Piercing auto-registered after the effect-play");
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}

// --- Minimal effect that emits a PlayCard mutation -----------------------

sealed class PlayCardEmitEffect : IHeadlessCardEffect
{
    private readonly HeadlessEntityId _playedCardId;

    public PlayCardEmitEffect(HeadlessEntityId source, HeadlessEntityId playedCardId)
    {
        _playedCardId = playedCardId;
        Definition = new CardEffectDefinition(new HeadlessEntityId($"{source.Value}:playcard"), source, "PlayCard", "OnPlay");
    }

    public CardEffectDefinition Definition { get; }

    public CardEffectCanResolveResult CanResolve(CardEffectResolveContext context) => CardEffectCanResolveResult.Success();

    public ValueTask<EffectResult> ResolveAsync(CardEffectResolveContext context, IEffectMutationSink mutations, CancellationToken cancellationToken = default)
    {
        mutations.Apply(new EffectMutation(
            MatchStateMutationSink.PlayCardKind,
            Definition.SourceEntityId,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [MatchStateMutationSink.TargetEntityIdKey] = _playedCardId.Value,
                [MatchStateMutationSink.FromZoneKey] = ChoiceZone.Trash.ToString(),
            }));
        return ValueTask.FromResult(EffectResult.Success("played"));
    }
}
