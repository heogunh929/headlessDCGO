using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// S2 (G9-057): CanNotAffectedStaticEffect un-sealed + honours SkillCondition (AS-IS
// CanNotAffect = CardCondition && SkillCondition). skillCondition "opponent's DIGIMON effects only" must block
// an opponent Digimon effect, but NOT an opponent TAMER effect nor the card's OWN effect (no flattening to
// "all opponent effects").

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
    // AS-IS SkillCondition: immune to the OPPONENT's DIGIMON effects only.
    ctx.EffectRegistry.Register(CardEffectFactory.CanNotAffectedStaticEffect(
        permanentCondition: null,
        skillCondition: src => src.Owner != P1 && src.IsDigimon,
        isInheritedEffect: false, card: new CardSource(ctx, target, P1), condition: null).ToBinding($"cna:{target.Value}"));

    bool blocked = ContinuousImmunityGate.BlocksOpponentEffect(ctx.EffectRegistry, ctx.CardInstanceRepository, target, source, ctx);
    AssertTrue(blocked == expectBlocked, $"blocked == {expectBlocked} (source={sourceTag})");
}

// (C2) AS-IS CanNotAffect = CardCondition(target) && SkillCondition(cause) — the target-side predicate
// (permanentCondition, previously dropped) narrows WHICH permanents the immunity protects.
async Task PermanentConditionScopes()
{
    EngineContext ctx = Ctx();
    var granter = await Place(ctx, P1, "GRANTER", "Tamer");
    var protectedAlly = await Place(ctx, P1, "MATCH", "Digimon");
    var otherAlly = await Place(ctx, P1, "OTHER", "Digimon");
    var source = await Place(ctx, P2, "P2-DIGI", "Digimon");

    // "Your [MATCH] Digimon cannot be affected by your opponent's Digimon effects."
    ctx.EffectRegistry.Register(CardEffectFactory.CanNotAffectedStaticEffect(
        permanentCondition: p => p.TopCard.EqualsCardName("MATCH"),
        skillCondition: src => src.Owner != P1 && src.IsDigimon,
        isInheritedEffect: false, card: new CardSource(ctx, granter, P1), condition: null).ToBinding($"cna2:{granter.Value}"));

    AssertTrue(ContinuousImmunityGate.BlocksOpponentEffect(ctx.EffectRegistry, ctx.CardInstanceRepository, protectedAlly, source, ctx),
        "the predicate-matching ally is protected");
    AssertTrue(!ContinuousImmunityGate.BlocksOpponentEffect(ctx.EffectRegistry, ctx.CardInstanceRepository, otherAlly, source, ctx),
        "a non-matching ally is NOT protected (predicate honoured, not flattened)");
    AssertTrue(!ContinuousImmunityGate.BlocksOpponentEffect(ctx.EffectRegistry, ctx.CardInstanceRepository, granter, source, ctx),
        "the granter itself is not implicitly protected (AS-IS CardCondition decides)");
}

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 957);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
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
