using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G11-004: a once-per-turn (maxCountPerTurn=1) trigger whose GATE (CanResolve condition) is NOT met must
// NOT consume its per-turn use — so a later same-turn firing whose gate IS met still resolves. Driven
// through the real GameFlowProcessor trigger loop (self-scoped OnAllyAttack so the event matches the
// listener), which now evaluates the gate BEFORE the OnceFlag cap.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Gate-failing trigger does NOT consume once-per-turn; later gate-passing firing resolves", GateBeforeConsume),
    ("Once-per-turn still caps repeated gate-passing firings in the same turn", OnceStillCaps),
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

async Task GateBeforeConsume()
{
    bool[] gate = { false };
    (EngineContext context, HeadlessEntityId card) = await Setup(gate);

    // Attack #1: gate CLOSED -> condition fails -> must NOT fire and must NOT consume the once-per-turn use.
    await Attack(context, card);
    AssertEqual(5, context.MemoryController.Current.Current, "gate-closed firing did not resolve (memory unchanged)");

    // Attack #2 (same turn): gate OPEN -> fires (proves the once-per-turn was NOT spent by attack #1).
    gate[0] = true;
    await Attack(context, card);
    AssertEqual(4, context.MemoryController.Current.Current, "gate-open firing resolves (-1) since once was not wasted");
}

async Task OnceStillCaps()
{
    bool[] gate = { true };
    (EngineContext context, HeadlessEntityId card) = await Setup(gate);

    await Attack(context, card);
    AssertEqual(4, context.MemoryController.Current.Current, "1st gate-open firing: -1");

    await Attack(context, card);
    AssertEqual(4, context.MemoryController.Current.Current, "2nd same-turn firing blocked by once-per-turn (still 4)");

    // (uniform-사멸 flip) per-turn caps live on the AS-IS CEntity_EffectController store now.
    HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.CEntity_EffectControllerStore.ResetUseCountsForTurn(context);
    await Attack(context, card);
    AssertEqual(3, context.MemoryController.Current.Current, "next turn: once resets, fires again (-1)");
}

// --- Helpers -------------------------------------------------------------

async Task<(EngineContext, HeadlessEntityId)> Setup(bool[] gate)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 1104);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    context.MemoryController.Set(5);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId("DEF"), "DEF", "Greymon", new Dictionary<string, object?>(), CardType: "Digimon"));
    var card = new HeadlessEntityId("p1:battle:C");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(card, new HeadlessEntityId("DEF"), P1));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, card, ChoiceZone.None, ChoiceZone.BattleArea));

    // A MANDATORY once-per-turn [When Attacking] "lose 1 memory" whose gate is the controllable flag.
    // (amount < 0 -> mandatory, so a passing gate auto-resolves instead of surfacing an optional prompt.)
    var source = new CardSource(context, card, P1);
    ICardEffect mem = new LocalMemoryProbe(
        source, EffectTiming.OnAllyAttack, amount: -1, "test once+gate",
        condition: () => gate[0], maxCountPerTurn: 1, hash: "g11_004_once");
    // LocalMemoryProbe (the R3-C2b-2 test-local replacement for the deleted TriggeredMemoryEffect) declares a real
    // ToBinding — bridge via LegacyBindingBridge (see CardEffectCommons/LegacyActivatedBridge.cs).
    if (LegacyBindingBridge.TryToBinding(mem, $"{card.Value}:test:onallyattack", out EffectBinding? memBinding) && memBinding is not null)
    {
        context.EffectRegistry.Register(memBinding);
    }

    // Drain any pending zone-entry events so they don't interfere with the gated firings.
    await new GameFlowProcessor().RunToStableAsync(context);
    return (context, card);
}

// Self-scoped OnAllyAttack (subject = the attacking card = the listener) so the collector matches it.
async Task Attack(EngineContext context, HeadlessEntityId card)
{
    TriggerEventEmitter.Emit(context.GameEventQueue, TriggerTimings.OnAllyAttack, actor: P1, subject: card);
    await new GameFlowProcessor().RunToStableAsync(context);
}

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}

// (R3-C2b-2) The engine's invented old-model TriggeredMemoryEffect + the AddMemoryTriggerEffect factory were
// deleted. This suite exercises the OLD-model registry/scheduler once-per-turn+gate precision (a binding resolved
// via CardEffectSchedulerResolver — NOT the R3 trigger window), so it carries a test-local old-model memory probe:
// a verbatim trim of the deleted TriggeredMemoryEffect (ICardEffect+IHeadlessCardEffect, ToBinding, condition in
// CanResolve, maxCountPerTurn/hash in Definition). Preserves the exact once-gate contract without keeping the
// deleted engine primitive alive.
public sealed class LocalMemoryProbe : ICardEffect, IHeadlessCardEffect
{
    private readonly Func<bool>? _condition;
    private readonly Func<CardEffectResolveContext, bool>? _triggerGate;

    public LocalMemoryProbe(
        CardSource card, EffectTiming timing, int amount, string description,
        Func<bool>? condition = null, Func<CardEffectResolveContext, bool>? triggerGate = null,
        int? maxCountPerTurn = null, string? hash = null, bool? isOptional = null, string? effectIdSuffix = null)
    {
        Card = card;
        Amount = amount;
        _condition = condition;
        _triggerGate = triggerGate;
        string trigger = EffectTimings.ToTriggerName(timing);
        var effectId = new HeadlessEntityId(string.IsNullOrWhiteSpace(effectIdSuffix)
            ? $"{card.InstanceId.Value}:memprobe:{trigger}:{amount}"
            : $"{card.InstanceId.Value}:memprobe:{trigger}:{amount}:{effectIdSuffix}");
        Definition = new CardEffectDefinition(effectId, card.InstanceId, description, trigger,
            isOptional: isOptional ?? (amount > 0), maxCountPerTurn: maxCountPerTurn, hash: hash);
    }

    public CardSource Card { get; }

    public int Amount { get; }

    public CardEffectDefinition Definition { get; }

    public CardEffectCanResolveResult CanResolve(CardEffectResolveContext context)
    {
        if (_condition is not null && !_condition())
        {
            return CardEffectCanResolveResult.Failure("Trigger condition not met.");
        }

        if (_triggerGate is not null && !_triggerGate(context))
        {
            return CardEffectCanResolveResult.Failure("Trigger event condition not met.");
        }

        return CardEffectCanResolveResult.Success();
    }

    public ValueTask<EffectResult> ResolveAsync(CardEffectResolveContext context, IEffectMutationSink mutations, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutations);
        cancellationToken.ThrowIfCancellationRequested();
        CardEffectCanResolveResult check = CanResolve(context);
        if (!check.CanResolve)
        {
            return ValueTask.FromResult(EffectResult.Failure(check.Message ?? "Cannot resolve.", check.Values));
        }

        mutations.Apply(new EffectMutation(
            MatchStateMutationSink.AddMemoryKind, Definition.SourceEntityId,
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.AmountKey] = Amount }));
        return ValueTask.FromResult(EffectResult.Success($"Memory {(Amount >= 0 ? "+" : string.Empty)}{Amount}."));
    }

    public EffectBinding ToBinding(string effectId)
    {
        var ctx = new EffectContext(
            Card.Controller, Card.Owner, Card.InstanceId, triggerEntityId: null, targetEntityIds: Array.Empty<HeadlessEntityId>());
        return new EffectBinding(
            new EffectRequest(Definition.EffectId, Card.Controller, Definition.Timing, ctx),
            keywords: null, EffectQueryRole.None, Array.Empty<string>(), effect: this, duration: null);
    }
}
