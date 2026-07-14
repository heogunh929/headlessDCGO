using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// M-4 (G9-056): link-subsystem un-seal. ChangeSelfLinkMax (linkedMaxDelta) and GrantedReduceLinkCost
// (linkCostDelta) registered continuous modifiers that were emitted as NO modifier and read by NOTHING.
// Now LinkHelpers.ResolveLinkedMax / ResolveLinkCost fold them.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("ChangeSelfLinkMax(+2) raises the effective link max (1 -> 3)", LinkMax),
    ("No effect -> base link max (control)", LinkMaxControl),
    ("GrantedReduceLinkCost(2) lowers the effective link cost (3 -> 1)", LinkCost),
    ("Link cost reduction clamps at 0", LinkCostClamp),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task LinkMax()
{
    EngineContext ctx = Ctx();
    var host = await Place(ctx, "HOST");
    // SEAM (RD-P6B-16): ChangeLinkMaxClass is a new-model kind-class (IChangeLinkMaxEffect.GetLinkMax, no
    // ToBinding). LinkHelpers.ResolveLinkedMax now UNIONs the AS-IS Permanent.LinkedMax interface scan
    // (NewModelContinuousScan.FoldLinkedMax) onto its legacy modifier fold. Attach the built effect to the
    // host's controller — a self-scope grant folded over the owner's field permanents (which include the host).
    var hostCard = new CardSource(ctx, host, P1);
    ICardEffect eff = CardEffectFactory.ChangeSelfLinkMaxStaticEffect(2, false, hostCard, null);
    hostCard.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(eff);
    AssertTrue(LinkHelpers.ResolveLinkedMax(ctx, host) == LinkHelpers.DefaultLinkedMax + 2, $"effective link max == {LinkHelpers.DefaultLinkedMax + 2}");
}

async Task LinkMaxControl()
{
    EngineContext ctx = Ctx();
    var host = await Place(ctx, "HOST");
    AssertTrue(LinkHelpers.ResolveLinkedMax(ctx, host) == LinkHelpers.DefaultLinkedMax, "base link max with no effect");
}

async Task LinkCost()
{
    EngineContext ctx = Ctx();
    var card = await Place(ctx, "CARD");
    // SEAM (RD-P6B-16): ChangeLinkCostClass (IChangeLinkCostEffect.GetCost, no ToBinding). LinkHelpers.
    // ResolveLinkCost now UNIONs the AS-IS CardSource.GetChangedLinkCost interface scan
    // (NewModelContinuousScan.FoldLinkCost). Attach the built effect to the card's controller. NOTE: the
    // conditions were `null, null, null` — AS-IS ChangeLinkCostClass.CardCondition/PermanentCondition return
    // FALSE for a null predicate (never "any"), so a null grant never applies (AS-IS-false setup); a real card
    // passes `_ => true` (as G9-037 does). Corrected to the AS-IS "applies to any" predicates.
    var cardSource = new CardSource(ctx, card, P1);
    ICardEffect eff = CardEffectFactory.GrantedReduceLinkCostClass(cardSource, 2, _ => true, _ => true, _ => true);
    cardSource.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(eff);
    AssertTrue(LinkHelpers.ResolveLinkCost(ctx, card, 3) == 1, "effective link cost 3 - 2 == 1");
}

async Task LinkCostClamp()
{
    EngineContext ctx = Ctx();
    var card = await Place(ctx, "CARD");
    // SEAM (RD-P6B-16): same as LinkCost() — attach + AS-IS "any" predicates; FoldLinkCost clamps >= 0.
    var cardSource = new CardSource(ctx, card, P1);
    ICardEffect eff = CardEffectFactory.GrantedReduceLinkCostClass(cardSource, 5, _ => true, _ => true, _ => true);
    cardSource.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(eff);
    AssertTrue(LinkHelpers.ResolveLinkCost(ctx, card, 3) == 0, "3 - 5 clamps to 0");
}

// --- Helpers ---

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 956);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    // (RD-P6B-16 seam) live phase so ICardEffect.CanUse's DoneStartGame gate passes for the new-model scan.
    ctx.TurnController.SetPhase(HeadlessPhase.Main);
    return ctx;
}

async Task<HeadlessEntityId> Place(EngineContext ctx, string tag)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"1:battle:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = false }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }

// Minimal AS-IS-shaped CEntity_Effect: the seam every ported card definition class uses to surface its printed
// effect list to CardSource.EffectList → CEntity_Effect.CardEffects.
sealed class TestCardEntityEffect : CEntity_Effect
{
    private readonly ICardEffect _effect;
    public TestCardEntityEffect(ICardEffect effect) { _effect = effect; }
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource cardSource) => new() { _effect };
}
