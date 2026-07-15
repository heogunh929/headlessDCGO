// PRIM-P0 (last STOP): triggered set-splice — AS-IS AddSkillClass whose getEffects grants a TRIGGERED activated
// effect to a LIVE-matched set (e.g. BT8_031 "your Digimon gain '[When Attacking] trash the bottom source'").
// One player-scope trigger-grant binding fires for ANY event whose actor is the scoped player; the collector
// injects the triggering card as the subject (TriggerEntityId) so the nested effect resolves against THAT card
// and applies its per-card predicate. This covers a live set (no per-card pre-registration).
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
HeadlessEntityId GrantSrc = new("1:battle:GRANT");
HeadlessEntityId A1 = new("1:battle:A1");     // a matching attacker
HeadlessEntityId Other = new("1:battle:NOPE"); // a non-matching attacker

var tests = new (string Name, Func<Task> Body)[]
{
    ("fires when a matching Digimon (subject) of the scoped player triggers the timing (+2)", MatchingAttackerFires),
    ("does NOT fire when the actor is the OPPONENT (player-scoped)", OpponentActorExcluded),
    ("does NOT fire when the triggering card fails the per-card predicate", PredicateExcludes),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failures.Add(test.Name); Console.Error.WriteLine($"FAIL {test.Name}\n{ex.GetType().Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task MatchingAttackerFires()
{
    EngineContext ctx = Ctx();
    GrantWhenAttacking(ctx);
    ctx.MemoryController.Set(0);
    await EmitAttack(ctx, actor: P1, attacker: A1);
    AssertEqual(2, ctx.MemoryController.Current.Current, "the matching attacker fired the granted trigger");
}

async Task OpponentActorExcluded()
{
    EngineContext ctx = Ctx();
    GrantWhenAttacking(ctx);
    ctx.MemoryController.Set(0);
    await EmitAttack(ctx, actor: P2, attacker: A1);
    AssertEqual(0, ctx.MemoryController.Current.Current, "an opponent's attack did NOT fire a P1-scoped grant");
}

async Task PredicateExcludes()
{
    EngineContext ctx = Ctx();
    GrantWhenAttacking(ctx);
    ctx.MemoryController.Set(0);
    await EmitAttack(ctx, actor: P1, attacker: Other);   // subject fails the predicate (id != A1)
    AssertEqual(0, ctx.MemoryController.Current.Current, "a non-matching attacker did NOT fire the grant");
}

// --- Harness -------------------------------------------------------------

// The nested triggered effect: "[When Attacking] gain 2" gated on the triggering card (TriggerEntityId, injected
// by the collector) matching the per-card predicate (here: the attacker is A1).
void GrantWhenAttacking(EngineContext ctx)
{
    var grantCard = new CardSource(ctx, GrantSrc, P1, P1);
    ICardEffect nested = new LocalMemoryProbe(
        grantCard, EffectTiming.OnAllyAttack, amount: 2, "[When Attacking] Gain 2 memory.",
        triggerGate: rc => rc.EffectContext.TriggerEntityId is HeadlessEntityId subj && subj == A1,
        isOptional: false);
    // GrantTriggeredEffectToScopedSet's declared return type is the ICardEffect interface (ToBinding is not
    // part of it); the concrete instance it always constructs is PlayerScopeTriggerGrantEffect, which does
    // carry ToBinding — cast to it (value/behavior unchanged).
    var grant = (PlayerScopeTriggerGrantEffect)CardEffectFactory.GrantTriggeredEffectToScopedSet(grantCard, P1, nested);
    ctx.EffectRegistry.Register(grant.ToBinding($"{GrantSrc.Value}:whenAttackGrant"));
}

async Task EmitAttack(EngineContext ctx, HeadlessPlayerId actor, HeadlessEntityId attacker)
{
    TriggerEventEmitter.Emit(ctx.GameEventQueue, TriggerTimings.OnAllyAttack, actor: actor, subject: attacker);
    await new GameFlowProcessor().RunToStableAsync(ctx);
}

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 5);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(GrantSrc, new HeadlessEntityId("DEF:G"), P1, Metadata: new Dictionary<string, object?>()));
    return ctx;
}

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"{label}: expected '{expected}', actual '{actual}'.");
}

// (R3-C2b-2) The engine's invented old-model TriggeredMemoryEffect + the AddMemoryTriggerEffect factory were
// deleted. This suite exercises the OLD-model registry grant-to-scoped-set subsystem (GrantTriggeredEffectToScopedSet
// lowers an ICardEffect with ToBinding to a registry binding — NOT the R3 trigger window), so it carries a
// test-local old-model memory probe: a verbatim trim of the deleted TriggeredMemoryEffect (ICardEffect+
// IHeadlessCardEffect, ToBinding, condition + triggerGate in CanResolve). Preserves the exact grant contract
// without keeping the deleted engine primitive alive.
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
