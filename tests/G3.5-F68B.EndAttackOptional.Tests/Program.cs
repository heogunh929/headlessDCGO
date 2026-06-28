// (#6) Verifies the F-6.8 fidelity fix for end-attack triggers: a bound effect whose
// Definition.IsOptional is true ("you may") must be RE-CLASSIFIED to Optional and held for an
// agent decision (DeferredOptionalTriggers), even when the end-attack event carries a Mandatory
// kind. Without this, optional end-attack effects were auto-resolved -> a rules change, which a
// port must not do. Mandatory (IsOptional=false) bodies must still enqueue. Bodyless bindings keep
// the collected kind (no registry -> no behaviour change), guarding the existing G2G-005 contract.
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Rules;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId Player = new(1);
HeadlessPlayerId Opponent = new(2);
HeadlessEntityId AttackerId = new("attacker-001");
HeadlessEntityId TargetId = new("target-001");
HeadlessEntityId BlockerId = new("blocker-001");

var tests = new (string Name, Func<Task> Body)[]
{
    ("Optional-bodied end-attack trigger is reclassified and deferred", OptionalBodyIsDeferred),
    ("Mandatory-bodied end-attack trigger still enqueues", MandatoryBodyStillEnqueues),
    ("Reclassification overrides a Mandatory event kind for optional bodies", OptionalOverridesEventKind),
    ("No-registry hook keeps collected kind (G2G-005 parity)", NoRegistryKeepsCollectedKind),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try
    {
        await test.Body();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        failures.Add($"{test.Name}: {ex.GetType().Name}: {ex.Message}");
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}");
    }
}

if (failures.Count > 0)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine($"{failures.Count} test(s) failed.");
    Environment.Exit(1);
}

Console.WriteLine();
Console.WriteLine($"{tests.Length} test(s) passed.");

// A turn-player optional body, fired by a Mandatory end-attack event, must be deferred not enqueued.
Task OptionalBodyIsDeferred()
{
    var registry = new InMemoryEffectRegistry();
    registry.Register(CreateBinding("effect-optional", Player, "turn-card", isOptional: true));
    var scheduler = new EffectScheduler();
    var hook = new EndAttackTriggerHook(new AutoProcessingTriggerCollector(registry), null, registry);

    EndAttackTriggerHookResult result = hook.Process(
        ResolvedAttack(),
        sequence: 30,
        scheduler,
        turnPlayerId: Player,
        nonTurnPlayerId: Opponent,
        kind: TimingWindowTriggerKind.Mandatory);

    AssertTrue(result.IsSuccess, "hook success");
    AssertEqual(1, result.CollectedCount, "collected count");
    AssertEqual(0, result.EnqueuedMandatoryCount, "enqueued mandatory count");
    AssertEqual(1, result.DeferredOptionalCount, "deferred optional count");
    AssertEqual(0, scheduler.PendingCount, "scheduler must not auto-resolve the optional effect");
    return Task.CompletedTask;
}

// A mandatory body is unaffected by reclassification: it still enqueues for auto-resolution.
Task MandatoryBodyStillEnqueues()
{
    var registry = new InMemoryEffectRegistry();
    registry.Register(CreateBinding("effect-mandatory", Player, "turn-card", isOptional: false));
    var scheduler = new EffectScheduler();
    var hook = new EndAttackTriggerHook(new AutoProcessingTriggerCollector(registry), null, registry);

    EndAttackTriggerHookResult result = hook.Process(
        ResolvedAttack(),
        sequence: 31,
        scheduler,
        turnPlayerId: Player,
        nonTurnPlayerId: Opponent,
        kind: TimingWindowTriggerKind.Mandatory);

    AssertTrue(result.IsSuccess, "hook success");
    AssertEqual(1, result.CollectedCount, "collected count");
    AssertEqual(1, result.EnqueuedMandatoryCount, "enqueued mandatory count");
    AssertEqual(0, result.DeferredOptionalCount, "deferred optional count");
    AssertEqual(1, scheduler.PendingCount, "scheduler pending");
    return Task.CompletedTask;
}

// Mixed bodies under one Mandatory event: optional defers, mandatory enqueues -> proves the per-effect
// IsOptional decision overrides the event kind rather than the event kind dictating all triggers.
Task OptionalOverridesEventKind()
{
    var registry = new InMemoryEffectRegistry();
    registry.Register(CreateBinding("effect-opt", Player, "turn-card", isOptional: true));
    registry.Register(CreateBinding("effect-mand", Opponent, "opponent-card", isOptional: false));
    var scheduler = new EffectScheduler();
    var hook = new EndAttackTriggerHook(new AutoProcessingTriggerCollector(registry), null, registry);

    EndAttackTriggerHookResult result = hook.Process(
        ResolvedAttack(),
        sequence: 32,
        scheduler,
        turnPlayerId: Player,
        nonTurnPlayerId: Opponent,
        kind: TimingWindowTriggerKind.Mandatory);

    AssertTrue(result.IsSuccess, "hook success");
    AssertEqual(2, result.CollectedCount, "collected count");
    AssertEqual(1, result.EnqueuedMandatoryCount, "enqueued mandatory count");
    AssertEqual(1, result.DeferredOptionalCount, "deferred optional count");
    AssertEqual(1, scheduler.PendingCount, "only the mandatory effect auto-resolves");
    return Task.CompletedTask;
}

// Without a registry the hook cannot read bodies, so it keeps the collected kind. This is the
// G2G-005 path (bodyless InMemoryEffectQueryService) and must stay regression-free.
Task NoRegistryKeepsCollectedKind()
{
    var query = new InMemoryEffectQueryService();
    query.Register(new EffectRequest(
        new HeadlessEntityId("effect-bodyless"),
        Player,
        EndAttackTriggerHook.OnEndAttackTiming,
        new EffectContext(
            Player,
            Player,
            new HeadlessEntityId("turn-card"),
            triggerEntityId: new HeadlessEntityId("turn-card"),
            targetEntityIds: Array.Empty<HeadlessEntityId>())));
    var scheduler = new EffectScheduler();
    var hook = new EndAttackTriggerHook(new AutoProcessingTriggerCollector(query));

    EndAttackTriggerHookResult result = hook.Process(
        ResolvedAttack(),
        sequence: 33,
        scheduler,
        turnPlayerId: Player,
        nonTurnPlayerId: Opponent,
        kind: TimingWindowTriggerKind.Mandatory);

    AssertTrue(result.IsSuccess, "hook success");
    AssertEqual(1, result.EnqueuedMandatoryCount, "bodyless stays mandatory");
    AssertEqual(0, result.DeferredOptionalCount, "bodyless not deferred");
    return Task.CompletedTask;
}

EffectBinding CreateBinding(string effectId, HeadlessPlayerId controller, string source, bool isOptional)
{
    var sourceId = new HeadlessEntityId(source);
    var eid = new HeadlessEntityId(effectId);
    var request = new EffectRequest(
        eid,
        controller,
        EndAttackTriggerHook.OnEndAttackTiming,
        new EffectContext(
            controller,
            controller,
            sourceId,
            triggerEntityId: sourceId,
            targetEntityIds: Array.Empty<HeadlessEntityId>()));
    var definition = new CardEffectDefinition(
        eid,
        sourceId,
        isOptional ? "optional-end-attack" : "mandatory-end-attack",
        EndAttackTriggerHook.OnEndAttackTiming,
        isOptional: isOptional);
    return new EffectBinding(request, effect: new StubEndAttackEffect(definition));
}

HeadlessAttackState ResolvedAttack()
{
    var controller = new InMemoryHeadlessAttackController();
    controller.DeclareAttack(Player, AttackerId, Opponent, null, isDirectAttack: true);
    return controller.ResolveAttack("Attack flow completed.");
}

static void AssertTrue(bool condition, string label)
{
    if (!condition)
    {
        throw new InvalidOperationException($"Expected '{label}' to be true.");
    }
}

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected '{label}' to be '{expected}' but was '{actual}'.");
    }
}

// Minimal optional/mandatory end-attack effect body whose only meaningful property is Definition.
sealed class StubEndAttackEffect : IHeadlessCardEffect
{
    public StubEndAttackEffect(CardEffectDefinition definition) => Definition = definition;

    public CardEffectDefinition Definition { get; }

    public CardEffectCanResolveResult CanResolve(CardEffectResolveContext context)
        => CardEffectCanResolveResult.Success();

    public ValueTask<EffectResult> ResolveAsync(
        CardEffectResolveContext context,
        IEffectMutationSink mutations,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(EffectResult.Success());
}
