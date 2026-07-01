using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// M-2 (G9-053): CannotReturnToHandStaticEffect honours cardEffectCondition (AS-IS IsOpponentEffect) against the
// CAUSING effect's source — "cannot be returned to hand by the OPPONENT's effects" blocks an opponent-caused
// return but allows a self-caused one. Previously the port ignored cardEffectCondition = over-protection.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Opponent-caused return is BLOCKED (cardEffectCondition matches)", () => Return(byOwner: P2, expectBlocked: true)),
    ("Self-caused return is ALLOWED (cardEffectCondition does not match)", () => Return(byOwner: P1, expectBlocked: false)),
    ("Unconditional restriction (no cardEffectCondition) blocks either", UnconditionalBlocks),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task Return(HeadlessPlayerId byOwner, bool expectBlocked)
{
    EngineContext ctx = Ctx();
    var protectedCard = await Place(ctx, P1, "PROT", ChoiceZone.BattleArea);
    var causingSource = await Place(ctx, byOwner, "CAUSE", ChoiceZone.BattleArea);
    // "This cannot be returned to hand by the OPPONENT's effects" — cardEffectCondition = source is P1's enemy.
    ctx.EffectRegistry.Register(CardEffectFactory.CannotReturnToHandStaticEffect(
        permanentCondition: null, cardEffectCondition: src => src.Owner != P1, isInheritedEffect: false,
        card: new CardSource(ctx, protectedCard, P1), condition: null).ToBinding($"crh:{protectedCard.Value}"));

    await ApplyReturn(ctx, protectedCard, causingSource);
    bool inHand = ((IZoneStateReader)ctx.ZoneMover).GetCards(P1, ChoiceZone.Hand).Contains(protectedCard);
    bool blocked = !inHand;
    AssertTrue(blocked == expectBlocked, $"blocked == {expectBlocked} (caused by {(byOwner == P1 ? "self" : "opponent")})");
}

async Task UnconditionalBlocks()
{
    EngineContext ctx = Ctx();
    var protectedCard = await Place(ctx, P1, "PROT", ChoiceZone.BattleArea);
    var causingSource = await Place(ctx, P1, "CAUSE", ChoiceZone.BattleArea);
    ctx.EffectRegistry.Register(CardEffectFactory.CannotReturnToHandStaticEffect(
        permanentCondition: null, cardEffectCondition: null, isInheritedEffect: false,
        card: new CardSource(ctx, protectedCard, P1), condition: null).ToBinding($"crh:{protectedCard.Value}"));

    await ApplyReturn(ctx, protectedCard, causingSource);
    bool inHand = ((IZoneStateReader)ctx.ZoneMover).GetCards(P1, ChoiceZone.Hand).Contains(protectedCard);
    AssertTrue(!inHand, "unconditional restriction blocks even a self-caused return");
}

async Task ApplyReturn(EngineContext ctx, HeadlessEntityId target, HeadlessEntityId causingSource)
{
    var sink = new MatchStateMutationSink(ctx.CardInstanceRepository, ctx.LogSink, ctx.ZoneMover, ctx.MemoryController, ctx.EffectRegistry, ctx.GameEventQueue, context: ctx);
    sink.Apply(new EffectMutation(MatchStateMutationSink.ReturnToHandKind, causingSource,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["targetEntityId"] = target.Value }));
    await sink.FlushAsync();
}

// --- Helpers ---

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 953);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    return ctx;
}

async Task<HeadlessEntityId> Place(EngineContext ctx, HeadlessPlayerId owner, string tag, ChoiceZone zone)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:{zone}:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = false }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone));
    return id;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
