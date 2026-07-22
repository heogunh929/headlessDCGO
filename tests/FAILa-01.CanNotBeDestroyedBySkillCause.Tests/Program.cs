using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// FAIL-a #1 (mapping remediation): CanNotBeDestroyedBySkillStaticEffect must honour its CAUSING-effect predicate
// (AS-IS CanNotBeDestroyedBySkill(permanent, cardEffect) — cardEffectCondition). "Cannot be deleted by your
// OPPONENT's effects" must BLOCK an opponent-caused effect-delete but ALLOW a self-caused one. The port
// previously ignored cardEffectCondition and returned immune for ANY effect delete (incl. the card's own
// controller) — over-immunity. A no-predicate form stays immune to either.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Opponent-caused effect-delete is BLOCKED (cardEffectCondition matches)", () => Delete(byOwner: P2, expectBlocked: true)),
    ("Self-caused effect-delete is ALLOWED (cardEffectCondition does not match)", () => Delete(byOwner: P1, expectBlocked: false)),
    ("Unconditional immunity (no cardEffectCondition) blocks either", UnconditionalBlocks),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task Delete(HeadlessPlayerId byOwner, bool expectBlocked)
{
    EngineContext ctx = Ctx();
    var protectedCard = await Place(ctx, P1, "PROT", ChoiceZone.BattleArea);
    var causingSource = await Place(ctx, byOwner, "CAUSE", ChoiceZone.BattleArea);
    // "This cannot be deleted by the OPPONENT's effects" — cardEffectCondition = deleting source is P1's enemy.
    // (P7 RD-P6B-12 resolved SEAM) CanNotBeDestroyedBySkillClass (Assets/Scripts/Script/CardEffects/
    // CanNotBeDestroyedBySkillClass.cs) is a new-model kind-class with no ToBinding/EffectRegistry bridge — the
    // AS-IS-faithful path is the LIVE cEntity_EffectController scan
    // NewModelContinuousScan.HasCanNotBeDestroyedBySkill/MatchStateMutationSink.IsRestrictedFromCause now
    // performs (evaluated against the REAL causing source's stand-in, RD-P6B-13). Attach the built effect via
    // the same seam every ported card definition class uses.
    var protectedCard0 = new CardSource(ctx, protectedCard, P1);
    ICardEffect built = CardEffectFactory.CanNotBeDestroyedBySkillStaticEffect(
        permanentCondition: null, cardEffectCondition: src => src.EffectSourceCard.Owner != P1, isInheritedEffect: false,
        card: protectedCard0, condition: null, effectName: "CanNotBeDestroyedBySkill");
    protectedCard0.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(built);

    await ApplyDelete(ctx, protectedCard, causingSource);
    bool onField = ((IZoneStateReader)ctx.ZoneMover).GetCards(P1, ChoiceZone.BattleArea).Contains(protectedCard);
    bool blocked = onField;
    AssertTrue(blocked == expectBlocked, $"blocked == {expectBlocked} (caused by {(byOwner == P1 ? "self" : "opponent")})");
}

async Task UnconditionalBlocks()
{
    EngineContext ctx = Ctx();
    var protectedCard = await Place(ctx, P1, "PROT", ChoiceZone.BattleArea);
    var causingSource = await Place(ctx, P1, "CAUSE", ChoiceZone.BattleArea);
    // (P7 RD-P6B-12 resolved SEAM) see the SEAM note in Delete() above for the full gate explanation.
    var protectedCard0 = new CardSource(ctx, protectedCard, P1);
    ICardEffect built = CardEffectFactory.CanNotBeDestroyedBySkillStaticEffect(
        permanentCondition: null, cardEffectCondition: null, isInheritedEffect: false,
        card: protectedCard0, condition: null, effectName: "CanNotBeDestroyedBySkill");
    protectedCard0.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(built);

    await ApplyDelete(ctx, protectedCard, causingSource);
    bool onField = ((IZoneStateReader)ctx.ZoneMover).GetCards(P1, ChoiceZone.BattleArea).Contains(protectedCard);
    AssertTrue(onField, "unconditional immunity blocks even a self-caused effect-delete");
}

async Task ApplyDelete(EngineContext ctx, HeadlessEntityId target, HeadlessEntityId causingSource)
{
    var sink = new MatchStateMutationSink(ctx.CardInstanceRepository, ctx.LogSink, ctx.ZoneMover, ctx.MemoryController, ctx.GameEventQueue, context: ctx);
    sink.Apply(new EffectMutation(MatchStateMutationSink.DeleteKind, causingSource,
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = target.Value }));
    await sink.FlushAsync();
}

// --- Helpers ---

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 951);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    // (P7 test-fix) ICardEffect.CanTrigger gates on TurnStateMachine.DoneStartGame (phase past None/Setup) — a
    // real board is in Main; without this every candidate effect's CanUse(null) trivially returns false and the
    // new-model scan never fires (same fix already documented in rebuild_p6_stageB_notes.md §5).
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
