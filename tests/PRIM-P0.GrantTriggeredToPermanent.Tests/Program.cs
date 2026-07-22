// PRIM-P0 B.O.5-tail: grant a TRIGGERED nested effect to a permanent with a duration (AS-IS temp
// AddEffectToPermanent, e.g. EX8_059 "1 Digimon gains '[On Deletion] ...' until end of turn").
//   - tractable subset (target survives): AddEffectToPermanent registers the nested trigger; it fires at its
//     timing and expires at the duration boundary.
//   - self-[On Deletion] (fires on the target's OWN removal): AddSelfRemovalEffectToPermanent marks the binding
//     SurviveOwnLeave so leave-play cleanup does not drop it before OnDeletion resolves; it fires on the real
//     deletion then self-removes (DelayedOneShot). Target-scoped via the nested effect's self-gate.
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("tractable: a triggered grant fires at its timing while the target is alive ([End of Turn] +2)", FiresAtTiming),
    ("tractable: the grant expires at its duration boundary and no longer fires", ExpiresAtBoundary),
    // (③-A) The three self-[On Deletion] subtests below pinned the RETIRED registry surface
    // (ctx.EffectRegistry.GetEffectsForTiming(OnDeletion) after AddSelfRemovalEffectToPermanent lowered an
    // old-model ICardEffect into a SurviveOwnLeave/DelayedOneShot registry trigger binding). That EffectRegistry
    // producer seat is gone; the substrate STOPs (RD-3A-02). Retargeted to a single retirement witness.
    ("self-[On Deletion]: the invented registry substrate is retired — AddSelfRemovalEffectToPermanent STOPs (RD-3A-02)", SelfDeletionRetired),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failures.Add(test.Name); Console.Error.WriteLine($"FAIL {test.Name}\n{ex.GetType().Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task FiresAtTiming()
{
    EngineContext ctx = Ctx();
    var src = await Put(ctx, "SRC", 4000);
    var target = await Put(ctx, "TGT", 4000);
    GrantEndTurn(ctx, src, target);
    ctx.MemoryController.Set(0);
    await EmitEndTurn(ctx);
    AssertEqual(2, ctx.MemoryController.Current.Current, "the granted [End of Turn] trigger fired");
}

async Task ExpiresAtBoundary()
{
    EngineContext ctx = Ctx();
    var src = await Put(ctx, "SRC", 4000);
    var target = await Put(ctx, "TGT", 4000);
    GrantEndTurn(ctx, src, target);
    ctx.MemoryController.Set(0);
    EffectDurationExpiry.ExpireTurnEnd(ctx.EffectRegistry, P2);
    await EmitEndTurn(ctx);
    AssertEqual(0, ctx.MemoryController.Current.Current, "after expiry the grant no longer fires");
}

async Task SelfDeletionRetired()
{
    EngineContext ctx = Ctx();
    var src = await Put(ctx, "SRC", 4000);
    var target = await Put(ctx, "TGT", 0);
    // (③-A) The invented AddSelfRemovalEffectToPermanent lowered an old-model ICardEffect into a
    // SurviveOwnLeave/DelayedOneShot EffectRegistry trigger binding — the last EffectRegistry.Register producer
    // seat in CardEffectCommons. It has NO AS-IS original and NO live caller (EX8_059 is an unported skeleton), so
    // the seat is retired and the substrate STOPs (RD-3A-02) until a live self-removal firing window + caller
    // exists. Witness the retirement instead of the old registry surface.
    ICardEffect nested = new LocalMemoryProbe(
        V(ctx, target), EffectTiming.OnDestroyedAnyone, amount: 2, "[On Deletion] Gain 2 memory.",
        triggerGate: rc => rc.EffectContext.TriggerEntityId is HeadlessEntityId subj && subj == target,
        isOptional: false);
    bool threw = false;
    try
    {
        CardEffectCommons.AddSelfRemovalEffectToPermanent(Perm(ctx, target), EffectDuration.UntilOpponentTurnEnd, V(ctx, src), nested, EffectTiming.OnDestroyedAnyone);
    }
    catch (NotSupportedException ex) when (ex.Message.Contains("RD-3A-02"))
    {
        threw = true;
    }

    AssertEqual(true, threw, "AddSelfRemovalEffectToPermanent STOPs (RD-3A-02) — invented registry substrate retired");
}

// --- Harness -------------------------------------------------------------

void GrantEndTurn(EngineContext ctx, HeadlessEntityId src, HeadlessEntityId target)
{
    ICardEffect nested = new LocalMemoryProbe(V(ctx, target), EffectTiming.OnEndTurn, amount: 2, "[End of Your Turn] Gain 2 memory.");
    CardEffectCommons.AddEffectToPermanent(Perm(ctx, target), EffectDuration.UntilOpponentTurnEnd, V(ctx, src), nested, EffectTiming.OnEndTurn);
}

async Task EmitEndTurn(EngineContext ctx)
{
    TriggerEventEmitter.Emit(ctx.GameEventQueue, TriggerTimings.OnEndTurn, actor: P1, subject: default);
    await new GameFlowProcessor().RunToStableAsync(ctx);
}

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 5);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);   // P1's turn (target owner) for the gain-memory gate
    return ctx;
}

async Task<HeadlessEntityId> Put(EngineContext ctx, string tag, int dp)
{
    var cards = (CardDatabase)ctx.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId($"DEF:{tag}"), tag, tag, new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"1:battle:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId($"DEF:{tag}"), P1, Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

CardSource V(EngineContext ctx, HeadlessEntityId id) => new(ctx, id, P1, P1);
Permanent Perm(EngineContext ctx, HeadlessEntityId id) => new(ctx, id, P1);

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"{label}: expected '{expected}', actual '{actual}'.");
}

// (R3-C2b-2) The engine's invented old-model TriggeredMemoryEffect/TriggeredGainMemoryEffect + the
// AddMemoryTriggerEffect factory were deleted. This suite exercises the OLD-model registry grant-to-permanent
// subsystem (AddEffectToPermanent / AddSelfRemovalEffectToPermanent lower an ICardEffect with ToBinding to a
// registry binding — NOT the R3 trigger window), so it carries a test-local old-model memory probe: a verbatim
// trim of the deleted TriggeredMemoryEffect (ICardEffect+IHeadlessCardEffect, ToBinding, condition + triggerGate
// in CanResolve). Preserves the exact grant contract without keeping the deleted engine primitive alive.
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
