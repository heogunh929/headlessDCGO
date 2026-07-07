namespace ST1RedTests;

using HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST1.Red;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

// Triggered memory effects: ST1_06 (<Blocker> + [When Attacking] lose 2) and ST1_09 ([Your Turn] +3 on block).
internal static class TriggerTests
{
    private static readonly HeadlessPlayerId P1 = new(1);
    private static readonly HeadlessPlayerId P2 = new(2);

    public static (string Name, Func<Task> Body)[] Cases => new (string, Func<Task>)[]
    {
        ("ST1_06: <Blocker> is registered", () => Pure(ST1_06_Blocker)),
        ("ST1_06: [When Attacking] resolves to -2 memory", ST1_06_LoseMemory),
        ("ST1_06: another ally attacking does NOT trigger it (self-scope)", ST1_06_OtherAllyNoFire),
        ("ST1_09: gains 3 memory on the owner's turn", () => ST1_09_Memory(ownerTurn: true, expected: 3)),
        ("ST1_09: no memory gain on the opponent's turn", () => ST1_09_Memory(ownerTurn: false, expected: 0)),
        ("ST1_09: another ally being blocked does NOT trigger it (self-scope)", ST1_09_OtherAllyNoFire),
    };

    private static Task Pure(Action body) { body(); return Task.CompletedTask; }

    private static void ST1_06_Blocker()
    {
        (EngineContext context, _) = Card("ST1_06");
        CardEffectRegistrar.RegisterOnEnterPlay(context, new ST1_06(), "ST1_06", new CardSource(context, new HeadlessEntityId("p1:battle:ST1_06"), P1));
        AssertTrue(context.EffectRegistry.GetKeywordEffects("Blocker").Count >= 1, "Blocker registered");
    }

    private static async Task ST1_06_LoseMemory()
    {
        (EngineContext context, HeadlessEntityId card) = Card("ST1_06");
        IReadOnlyList<EffectBinding> bindings =
            CardEffectRegistrar.RegisterOnEnterPlay(context, new ST1_06(), "ST1_06", new CardSource(context, card, P1));
        context.MemoryController.Set(3);
        await ResolveTrigger(context, bindings, "OnAllyAttack", subject: card);
        AssertEqual(1, context.MemoryController.Current.Current, "3 - 2 = 1 memory when THIS card attacks");
    }

    // Self-scope: when ANOTHER ally attacks (subject != this card), the trigger must NOT fire.
    private static async Task ST1_06_OtherAllyNoFire()
    {
        (EngineContext context, HeadlessEntityId card) = Card("ST1_06");
        IReadOnlyList<EffectBinding> bindings =
            CardEffectRegistrar.RegisterOnEnterPlay(context, new ST1_06(), "ST1_06", new CardSource(context, card, P1));
        context.MemoryController.Set(3);
        await ResolveTrigger(context, bindings, "OnAllyAttack", subject: new HeadlessEntityId("p1:battle:OTHER"));
        AssertEqual(3, context.MemoryController.Current.Current, "another ally attacking does NOT trigger the -2");
    }

    private static async Task ST1_09_Memory(bool ownerTurn, int expected)
    {
        (EngineContext context, HeadlessEntityId card) = Card("ST1_09");
        IReadOnlyList<EffectBinding> bindings =
            CardEffectRegistrar.RegisterOnEnterPlay(context, new ST1_09(), "ST1_09", new CardSource(context, card, P1));
        context.TurnController.Initialize(new[] { P1, P2 }, ownerTurn ? P1 : P2);
        context.MemoryController.Set(0);
        await ResolveTrigger(context, bindings, "OnBlockAnyone", subject: card);
        AssertEqual(expected, context.MemoryController.Current.Current, ownerTurn ? "+3 on owner turn" : "no change on opponent turn");
    }

    // Self-scope: when ANOTHER ally is blocked (subject != this card), the +3 must NOT fire.
    private static async Task ST1_09_OtherAllyNoFire()
    {
        (EngineContext context, HeadlessEntityId card) = Card("ST1_09");
        IReadOnlyList<EffectBinding> bindings =
            CardEffectRegistrar.RegisterOnEnterPlay(context, new ST1_09(), "ST1_09", new CardSource(context, card, P1));
        context.TurnController.Initialize(new[] { P1, P2 }, P1);
        context.MemoryController.Set(0);
        await ResolveTrigger(context, bindings, "OnBlockAnyone", subject: new HeadlessEntityId("p1:battle:OTHER"));
        AssertEqual(0, context.MemoryController.Current.Current, "another ally being blocked does NOT trigger the +3");
    }

    private static (EngineContext, HeadlessEntityId) Card(string number)
    {
        EngineContext context = EngineContext.CreateDefault(randomSeed: 9);
        CardDatabase cards = (CardDatabase)context.CardRepository;
        cards.Upsert(new CardRecord(new HeadlessEntityId(number), number, number, new Dictionary<string, object?>(), CardType: "Digimon"));
        var id = new HeadlessEntityId($"p1:battle:{number}");
        context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId(number), P1));
        return (context, id);
    }

    // Resolve a trigger as the live scheduler would: enrich the request with the event SUBJECT
    // (EnrichWithEventSubject) so a self-scope triggerGate (CanTriggerOnAttack) sees who actually attacked.
    private static async Task ResolveTrigger(EngineContext context, IReadOnlyList<EffectBinding> bindings, string timing, HeadlessEntityId subject)
    {
        EffectBinding binding = bindings.Single(b => string.Equals(b.Request.Timing, timing, StringComparison.Ordinal));
        AssertTrue(binding.Effect is not null, $"trigger binding for {timing} carries an effect body");

        EffectContext ec = binding.Request.Context;
        var enriched = new EffectContext(ec.SourcePlayerId, ec.OwnerPlayerId, ec.SourceEntityId, triggerEntityId: subject, targetEntityIds: ec.TargetEntityIds);
        var request = new EffectRequest(binding.Request.EffectId, binding.Request.ControllerId, binding.Request.Timing, enriched);

        var sink = new MatchStateMutationSink(context.CardInstanceRepository, memory: context.MemoryController);
        var resolveContext = new CardEffectResolveContext(request);
        await binding.Effect!.ResolveAsync(resolveContext, sink);
        await sink.FlushAsync();
    }

    private static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
    private static void AssertEqual<T>(T expected, T actual, string label)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
    }
}
