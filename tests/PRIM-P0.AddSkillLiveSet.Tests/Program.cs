// PRIM-P0: AddSkillClass ("your Digimon gain <keyword>") maps to the EXISTING player-scope keyword grant
// (ContinuousPlayerScopeKeywordEffect, exposed as e.g. AllianceStaticEffect). The AS-IS AddSkillClass splices
// the skill onto a LIVE-matched set re-evaluated each query; this proves the headless player-scope grant has
// the SAME live-set semantics — a Digimon that ENTERS PLAY AFTER the grant still gains the keyword, and the
// grant is owner-scoped (the opponent's Digimon do not gain it), with the per-card predicate honoured.
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("a Digimon that enters AFTER the grant still gains the keyword (live set)", LateEntrantGainsKeyword),
    ("the grant is owner-scoped: the opponent's Digimon do NOT gain it", OpponentExcluded),
    ("the per-card predicate is honoured (only matching cards gain it)", PredicateHonoured),
    ("all AddSkill keyword grants reach a late entrant (Piercing/Blitz/Retaliation/Scapegoat/Decoy/Barrier)", AllKeywordsLiveSet),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failures.Add(test.Name); Console.Error.WriteLine($"FAIL {test.Name}\n{ex.GetType().Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task LateEntrantGainsKeyword()
{
    EngineContext ctx = Ctx();
    var grantSrc = await Put(ctx, P1, "GRANT", tag: "grant");
    GrantAllianceToMyDigimon(ctx, grantSrc, permanentCondition: null);   // all my Digimon

    // A Digimon enters AFTER the grant is already registered.
    var late = await Put(ctx, P1, "LATE", tag: "late");
    AssertTrue(ContinuousKeywordGate.HasKeyword(ctx, late, ContinuousKeywordGate.Alliance), "the late entrant gained Alliance (live set)");
}

async Task OpponentExcluded()
{
    EngineContext ctx = Ctx();
    var grantSrc = await Put(ctx, P1, "GRANT", tag: "grant");
    GrantAllianceToMyDigimon(ctx, grantSrc, permanentCondition: null);
    var foe = await Put(ctx, P2, "FOE", tag: "foe");
    AssertTrue(!ContinuousKeywordGate.HasKeyword(ctx, foe, ContinuousKeywordGate.Alliance), "the opponent's Digimon did NOT gain Alliance");
}

async Task PredicateHonoured()
{
    EngineContext ctx = Ctx();
    var grantSrc = await Put(ctx, P1, "GRANT", tag: "grant");
    // Only cards whose instance id contains "YES" gain it.
    GrantAllianceToMyDigimon(ctx, grantSrc, permanentCondition: p => p.InstanceId.Value.Contains("YES", StringComparison.Ordinal));
    var yes = await Put(ctx, P1, "YES1", tag: "YES1");
    var no = await Put(ctx, P1, "NO1", tag: "no1");
    AssertTrue(ContinuousKeywordGate.HasKeyword(ctx, yes, ContinuousKeywordGate.Alliance), "the matching card gained Alliance");
    AssertTrue(!ContinuousKeywordGate.HasKeyword(ctx, no, ContinuousKeywordGate.Alliance), "the non-matching card did NOT gain Alliance");
}

async Task AllKeywordsLiveSet()
{
    var kws = new (string Kw, System.Func<CardSource, ICardEffect> Make)[]
    {
        (ContinuousKeywordGate.Piercing, c => CardEffectFactory.PiercingStaticEffect(null, false, c, null)),
        (ContinuousKeywordGate.Blitz, c => CardEffectFactory.BlitzStaticEffect(null, false, c, null)),
        (ContinuousKeywordGate.Retaliation, c => CardEffectFactory.RetaliationStaticEffect(null, false, c, null)),
        (ContinuousKeywordGate.Scapegoat, c => CardEffectFactory.ScapegoatStaticEffect(null, false, c, null)),
        (ContinuousKeywordGate.Decoy, c => CardEffectFactory.DecoyStaticEffect(null, false, c, null)),
        (ContinuousKeywordGate.Barrier, c => CardEffectFactory.BarrierStaticEffect(null, false, c, null)),
    };
    foreach (var (kw, make) in kws)
    {
        EngineContext ctx = Ctx();
        var grantSrc = await Put(ctx, P1, "GRANT", "grant");
        ctx.EffectRegistry.Register(make(new CardSource(ctx, grantSrc, P1, P1)).ToBinding($"{grantSrc.Value}:{kw}:scoped"));
        var late = await Put(ctx, P1, "LATE", "late");
        AssertTrue(ContinuousKeywordGate.HasKeyword(ctx, late, kw), $"late entrant gained {kw} (live set)");
    }
}

// --- Harness -------------------------------------------------------------

void GrantAllianceToMyDigimon(EngineContext ctx, HeadlessEntityId grantSrc, Func<Permanent, bool>? permanentCondition)
{
    var card = new CardSource(ctx, grantSrc, P1, P1);
    ICardEffect grant = CardEffectFactory.AllianceStaticEffect(permanentCondition, isInheritedEffect: false, card, condition: null);
    ctx.EffectRegistry.Register(grant.ToBinding($"{grantSrc.Value}:allianceScoped"));
}

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 5);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    return ctx;
}

async Task<HeadlessEntityId> Put(EngineContext ctx, HeadlessPlayerId owner, string idtag, string tag)
{
    var cards = (CardDatabase)ctx.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId($"DEF:{tag}"), tag, tag, new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{idtag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId($"DEF:{tag}"), owner, Metadata: new Dictionary<string, object?>()));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

static void AssertTrue(bool value, string label) { if (!value) throw new InvalidOperationException($"{label}: expected true."); }
