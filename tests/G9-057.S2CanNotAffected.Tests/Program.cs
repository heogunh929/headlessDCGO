using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// S2 (G9-057): CanNotAffectedStaticEffect honours SkillCondition (AS-IS CanNotAffect = CardCondition &&
// SkillCondition). skillCondition "opponent's DIGIMON effects only" must block an opponent Digimon effect, but
// NOT an opponent TAMER effect nor the card's OWN effect (no flattening to "all opponent effects").
// (R3-W3c-1) The factory now returns the new-model CanNotAffectedClass (kind-class, ICanNotAffectedEffect)
// consumed by the LIVE CardSource.CanNotBeAffected scan — no registry binding. Re-aimed from the old
// ContinuousImmunityGate.BlocksOpponentEffect registry probe to that live scan (assertions unchanged, AS-IS shape).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Opponent Digimon effect is blocked (immunity live + skillCondition matches)", () => Check("P2-DIGI", CardType: "Digimon", owner: 2, expectBlocked: true)),
    ("Opponent TAMER effect is NOT blocked (skillCondition narrows to Digimon effects)", () => Check("P2-TAMER", CardType: "Tamer", owner: 2, expectBlocked: false)),
    ("OWN Digimon effect is NOT blocked (skillCondition owner check)", () => Check("P1-DIGI", CardType: "Digimon", owner: 1, expectBlocked: false)),
    ("(C2) permanentCondition scopes the protection to matching permanents only", PermanentConditionScopes),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task Check(string sourceTag, string CardType, int owner, bool expectBlocked)
{
    EngineContext ctx = Ctx();
    var target = await Place(ctx, P1, "TARGET", "Digimon");
    var source = await Place(ctx, new HeadlessPlayerId(owner), sourceTag, CardType);
    var targetCard = new CardSource(ctx, target, P1);
    // AS-IS SkillCondition: immune to the OPPONENT's DIGIMON effects only — Func<ICardEffect,bool> over the cause.
    ICardEffect cnaEffect1 = CardEffectFactory.CanNotAffectedStaticEffect(
        permanentCondition: null,
        skillCondition: e => e.EffectSourceCard is not null && e.EffectSourceCard.Owner != P1 && e.EffectSourceCard.IsDigimon,
        isInheritedEffect: false, card: targetCard, condition: null);
    // (R3-W3c-1) attach the CanNotAffectedClass to the target's LIVE effect list (the seam every ported card uses);
    // the flipped factory has no registry binding.
    targetCard.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(cnaEffect1);

    // AS-IS causing effect: an ActivateClass whose EffectSourceCard is the source card.
    var cause = new ActivateClass();
    cause.SetUpICardEffect("cause", _ => true, new CardSource(ctx, source, new HeadlessPlayerId(owner)));
    // (R3-W3c-1) CanNotBeAffected -> CanUse -> IsDisabled reads GManager.instance, which resolves from the ambient
    // match scope (in production the game loop provides it; direct-drive tests enter it explicitly).
    using var _ = AmbientMatchContext.Enter(ctx);
    bool blocked = targetCard.CanNotBeAffected(cause);
    AssertTrue(blocked == expectBlocked, $"blocked == {expectBlocked} (source={sourceTag})");
}

// (C2) AS-IS CanNotAffect = CardCondition(target) && SkillCondition(cause) — the target-side predicate
// (permanentCondition) narrows WHICH permanents the immunity protects.
async Task PermanentConditionScopes()
{
    EngineContext ctx = Ctx();
    var granter = await Place(ctx, P1, "GRANTER", "Tamer");
    var protectedAlly = await Place(ctx, P1, "MATCH", "Digimon");
    var otherAlly = await Place(ctx, P1, "OTHER", "Digimon");
    var source = await Place(ctx, P2, "P2-DIGI", "Digimon");

    // "Your [MATCH] Digimon cannot be affected by your opponent's Digimon effects." (field-wide grant on the granter.)
    var granterCard = new CardSource(ctx, granter, P1);
    ICardEffect cnaEffect2 = CardEffectFactory.CanNotAffectedStaticEffect(
        permanentCondition: p => p.TopCard.EqualsCardName("MATCH"),
        skillCondition: e => e.EffectSourceCard is not null && e.EffectSourceCard.Owner != P1 && e.EffectSourceCard.IsDigimon,
        isInheritedEffect: false, card: granterCard, condition: null);
    granterCard.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(cnaEffect2);

    var cause = new ActivateClass();
    cause.SetUpICardEffect("cause", _ => true, new CardSource(ctx, source, P2));

    using var _ = AmbientMatchContext.Enter(ctx);
    AssertTrue(new CardSource(ctx, protectedAlly, P1).CanNotBeAffected(cause),
        "the predicate-matching ally is protected");
    AssertTrue(!new CardSource(ctx, otherAlly, P1).CanNotBeAffected(cause),
        "a non-matching ally is NOT protected (predicate honoured, not flattened)");
    AssertTrue(!new CardSource(ctx, granter, P1).CanNotBeAffected(cause),
        "the granter itself is not implicitly protected (AS-IS CardCondition decides)");
}

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 957);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    // (R3-W3c-1) the live CanNotBeAffected scan calls each immunity's CanUse(null), which gates on
    // TurnStateMachine.DoneStartGame (phase past None/Setup) — set Main so the new-model scan fires.
    ctx.TurnController.SetPhase(HeadlessPhase.Main);
    return ctx;
}

async Task<HeadlessEntityId> Place(EngineContext ctx, HeadlessPlayerId owner, string tag, string cardType)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 }, CardType: cardType));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000 }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }

// Returns the built CanNotAffectedClass from the card's live effect list (the seam a ported card definition uses).
sealed class TestCardEntityEffect : CEntity_Effect
{
    private readonly ICardEffect _effect;
    public TestCardEntityEffect(ICardEffect effect) { _effect = effect; }
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource cardSource) => new() { _effect };
}
