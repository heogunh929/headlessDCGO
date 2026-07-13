// PRIM-P0: AddSkillClass ("your Digimon gain <keyword>") maps to the EXISTING player-scope keyword grant
// (ContinuousPlayerScopeKeywordEffect, exposed as e.g. AllianceStaticEffect). The AS-IS AddSkillClass splices
// the skill onto a LIVE-matched set re-evaluated each query; this proves the headless player-scope grant has
// the SAME live-set semantics — a Digimon that ENTERS PLAY AFTER the grant still gains the keyword, and the
// grant is owner-scoped (the opponent's Digimon do not gain it), with the per-card predicate honoured.
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
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
    // Piercing/Blitz/Retaliation/Decoy/Barrier's *StaticEffect factories still construct the OLD-model
    // ContinuousPlayerScopeKeywordEffect (ICardEffect + ToBinding) — cast to it to reach ToBinding, which is
    // not part of the ICardEffect interface the factories are declared to return. Scapegoat's factory
    // (KeyWordEffects/Scapegoat.cs) was rebuilt to the new-model ScapegoatClass kind-class, which has no
    // ToBinding/EffectRegistry bridge (see MIGRATION-NOTE below) — its Make returns null (nothing to
    // register) and the assertion is left as-is, expected to fail until stage B lands.
    var kws = new (string Kw, Func<CardSource, string, EffectBinding?> MakeBinding)[]
    {
        (ContinuousKeywordGate.Piercing, (c, id) => ((ContinuousPlayerScopeKeywordEffect)CardEffectFactory.PiercingStaticEffect(null, false, c, null)).ToBinding(id)),
        (ContinuousKeywordGate.Blitz, (c, id) => ((ContinuousPlayerScopeKeywordEffect)CardEffectFactory.BlitzStaticEffect(null, false, c, null)).ToBinding(id)),
        (ContinuousKeywordGate.Retaliation, (c, id) => ((ContinuousPlayerScopeKeywordEffect)CardEffectFactory.RetaliationStaticEffect(null, false, c, null)).ToBinding(id)),
        (ContinuousKeywordGate.Scapegoat, (c, id) =>
        {
            // MIGRATION-NOTE (P7 test-fix): ScapegoatClass (Assets/Scripts/Script/CardEffects/ScapegoatClass.cs)
            // is a new-model kind-class with no ToBinding/EffectRegistry bridge (stage-B RED, docs/audit/
            // rebuild_p6_stageA_notes.md). The gate this test checks (ContinuousKeywordGate.HasKeyword(...,
            // ContinuousKeywordGate.Scapegoat)) reads only the substrate EffectRegistry; there is no live
            // interface-scan bridge from that gate to a new-model keyword kind-class (the engine's stage-B live
            // is-scan serves real ported cards, not a synthetic fixture card), so there is no buildable way to make this grant observable yet. The assertion
            // below is UNCHANGED and EXPECTED TO FAIL until stage B lands — tracked, not silently weakened.
            CardEffectFactory.ScapegoatStaticEffect(null, false, c, null);
            return null;
        }),
        (ContinuousKeywordGate.Decoy, (c, id) => ((ContinuousPlayerScopeKeywordEffect)CardEffectFactory.DecoyStaticEffect(null, false, c, null)).ToBinding(id)),
        (ContinuousKeywordGate.Barrier, (c, id) => ((ContinuousPlayerScopeKeywordEffect)CardEffectFactory.BarrierStaticEffect(null, false, c, null)).ToBinding(id)),
    };
    foreach (var (kw, makeBinding) in kws)
    {
        EngineContext ctx = Ctx();
        var grantSrc = await Put(ctx, P1, "GRANT", "grant");
        EffectBinding? binding = makeBinding(new CardSource(ctx, grantSrc, P1, P1), $"{grantSrc.Value}:{kw}:scoped");
        if (binding is not null)
        {
            ctx.EffectRegistry.Register(binding);
        }

        var late = await Put(ctx, P1, "LATE", "late");
        AssertTrue(ContinuousKeywordGate.HasKeyword(ctx, late, kw), $"late entrant gained {kw} (live set)");
    }
}

// --- Harness -------------------------------------------------------------

// CardEffectFactory.AllianceStaticEffect was rebuilt AS-IS-1:1 (KeyWordEffects/Alliance.cs) to the true
// [Alliance] TRIGGERED-on-attack ActivateClass — it is no longer the player-scope "your Digimon gain
// Alliance" continuous grant this fixture needs (that convenience wrapper was removed per that file's
// header: "Replaces ... the monolith's invented AllianceSelfEffect/AllianceStaticEffect"). The AS-IS
// player-scope keyword-grant primitive for Alliance is CardEffectCommons.GainAlliancePlayerEffect
// (AS-IS GainAlliancePlayerEffect, KeyWordEffects/Alliance.cs:180), which registers the binding itself.
// EffectDuration.UntilOpponentTurnEnd is the same "won't expire mid-test" placeholder duration used by
// other fixtures exercising this player-scope-grant family (no turn boundary is crossed in this test).
void GrantAllianceToMyDigimon(EngineContext ctx, HeadlessEntityId grantSrc, Func<Permanent, bool>? permanentCondition)
{
    var card = new CardSource(ctx, grantSrc, P1, P1);
    CardEffectCommons.GainAlliancePlayerEffect(permanentCondition, EffectDuration.UntilOpponentTurnEnd, card);
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
