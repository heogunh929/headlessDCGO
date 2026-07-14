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
    // (P7 RD-P6B-12-family resolved SEAM) CannotReturnToHandClass is a new-model kind-class with no
    // ToBinding/EffectRegistry bridge — the AS-IS-faithful path is the LIVE cEntity_EffectController scan
    // NewModelContinuousScan.HasCannotReturnToHand/MatchStateMutationSink.IsRestrictedFromCause now performs
    // (evaluated against the REAL causing source's stand-in, RD-P6B-13). Attach the built effect via the same
    // seam every ported card definition class uses. (cardEffectCondition takes the CAUSING ICardEffect — AS-IS
    // CardEffectCondition — so the owner check reads its EffectSourceCard, not a nonexistent Owner member on
    // ICardEffect itself.)
    var protectedCard0 = new CardSource(ctx, protectedCard, P1);
    ICardEffect built = CardEffectFactory.CannotReturnToHandStaticEffect(
        permanentCondition: null, cardEffectCondition: src => src.EffectSourceCard?.Owner != P1, isInheritedEffect: false,
        card: protectedCard0, condition: null, effectName: $"crh:{protectedCard.Value}");
    protectedCard0.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(built);

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
    // (P7 SEAM) see the SEAM note in Return() above for the full gate explanation.
    var protectedCard0 = new CardSource(ctx, protectedCard, P1);
    ICardEffect built = CardEffectFactory.CannotReturnToHandStaticEffect(
        permanentCondition: null, cardEffectCondition: null, isInheritedEffect: false,
        card: protectedCard0, condition: null, effectName: $"crh:{protectedCard.Value}");
    protectedCard0.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(built);

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
    // (P7 test-fix) ICardEffect.CanTrigger gates on TurnStateMachine.DoneStartGame (phase past None/Setup) —
    // without this every candidate effect's CanUse(null) trivially returns false and the new-model scan never
    // fires (same fix already documented in rebuild_p6_stageB_notes.md §5).
    ctx.TurnController.SetPhase(HeadlessPhase.Main);
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

sealed class TestCardEntityEffect : CEntity_Effect
{
    private readonly ICardEffect _effect;
    public TestCardEntityEffect(ICardEffect effect) { _effect = effect; }
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource cardSource) => new() { _effect };
}
