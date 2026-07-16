using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// PRIM-W5-e (G9-047): tail wrappers. ChangeCardNames is behavior-live (folds into CardSource.CardNames so
// EqualsCardName sees the added name). CanNotAffected registers the EffectMutation/Immune replacement.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("ChangeCardNames -> added name visible via EqualsCardName", ChangeCardNames),
    ("CanNotAffected -> ImmuneFromEffects replacement registered", CanNotAffected),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failures.Add(test.Name); Console.Error.WriteLine($"FAIL {test.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

async Task ChangeCardNames()
{
    EngineContext ctx = Ctx();
    var id = await Place(ctx, P1, "GREY", "Greymon");
    var cs = new CardSource(ctx, id, P1);
    AssertTrue(cs.EqualsCardName("Greymon") && !cs.EqualsCardName("Agumon"), "printed name only before");
    var changeNamesEffect = CardEffectFactory.ChangeCardNamesStaticEffect("Agumon", false, new CardSource(ctx, id, P1), null);
    if (!LegacyBindingBridge.TryToBinding(changeNamesEffect, $"ccn:{id.Value}", out var changeNamesBinding) || changeNamesBinding is null)
        throw new InvalidOperationException($"{changeNamesEffect.GetType().Name} has no ToBinding bridge.");
    ctx.EffectRegistry.Register(changeNamesBinding);
    AssertTrue(cs.EqualsCardName("Agumon") && cs.EqualsCardName("Greymon"), "added name folded into CardNames");
}

async Task CanNotAffected()
{
    // (R3-W3c-1) the flipped factory returns a new-model CanNotAffectedClass consumed by the LIVE
    // CardSource.CanNotBeAffected scan (no registry). With null skillCondition the fallback is opponent-only —
    // an opponent-sourced effect is blocked, an own effect is not.
    EngineContext ctx = Ctx();
    var id = await Place(ctx, P1, "SELF", "Digimon");
    var opp = await Place(ctx, P2, "OPP", "Digimon");
    var own = await Place(ctx, P1, "OWN", "Digimon");
    var selfCard = new CardSource(ctx, id, P1);
    var canNotAffectedEffect = CardEffectFactory.CanNotAffectedStaticEffect(null, null, false, selfCard, null);
    selfCard.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(canNotAffectedEffect);

    var oppCause = new ActivateClass();
    oppCause.SetUpICardEffect("cause", _ => true, new CardSource(ctx, opp, P2));
    var ownCause = new ActivateClass();
    ownCause.SetUpICardEffect("cause", _ => true, new CardSource(ctx, own, P1));

    using var _ = AmbientMatchContext.Enter(ctx);
    AssertTrue(selfCard.CanNotBeAffected(oppCause), "opponent effect blocked (immunity live)");
    AssertTrue(!selfCard.CanNotBeAffected(ownCause), "own effect not blocked");
}

// --- Helpers -------------------------------------------------------------

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 947);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    // (R3-W3c-1) DoneStartGame gate for the live CanNotBeAffected scan's CanUse(null) — see CanNotAffected test.
    ctx.TurnController.SetPhase(HeadlessPhase.Main);
    return ctx;
}

async Task<HeadlessEntityId> Place(EngineContext ctx, HeadlessPlayerId owner, string tag, string name)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, defId.Value, name,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = false }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }

sealed class TestCardEntityEffect : CEntity_Effect
{
    private readonly ICardEffect _effect;
    public TestCardEntityEffect(ICardEffect effect) { _effect = effect; }
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource cardSource) => new() { _effect };
}
