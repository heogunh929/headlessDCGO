using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// (G-clean-2 RETARGET, 2026-07-16) Formerly W6-G "all 16 keyword wrappers register their keyword" — that test
// asserted the INVENTED GainKeywordToPermanent funnel (a ContinuousKeywordGate.<Keyword> registry marker
// registered by the bool Gain*(CardSource) wrappers). Those wrappers + the funnel are DELETED in G-clean-2;
// every keyword now grants AS-IS 1:1 through the Task Gain*(Permanent, EffectDuration, ICardEffect) overloads
// that store the keyword's Static/Activate effect in the target permanent's duration bucket (AddEffectToPermanent).
// This file is RETARGETED to witness that AS-IS bucket path for the CONTINUOUS presence keywords (the 충실-7:
// Blocker/Rush/Jamming/Reboot/Iceclad/Collision) + TreatAsDigimon — grant -> Permanent.Has<Keyword> presence
// (via ContinuousKeywordGate.HasKeyword's NewModelContinuousScan union of the EffectList(None)/EffectList(
// OnCounterTiming) interface scan), the live CanUse gate, and immunity refusal. The FIRING keywords
// (Pierce/Retaliation/Vortex/Overclock/Execute/Raid/Alliance/Blitz/Evade/Barrier/Fortitude) fire through their
// timing windows and are validated by their cluster witnesses (P1w-GrantedPierce, G3.5-C821, C-EoT2, A4-Execute,
// C-Atk-Raid/Alliance/Blitz, C-Del-*), not by a presence scan.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Blocker grant lands in the target's None bucket -> HasKeyword true on TARGET only", BlockerGrantPresence),
    ("Rush grant is live-gated: leaving play turns the granted keyword off (AS-IS CanUseCondition)", RushLiveGate),
    ("Jamming grant lands in the None bucket (a named CanNotBeDestroyedByBattle effect) -> HasKeyword true", JammingGrant),
    ("Collision grant lands in the OnCounterTiming bucket -> HasKeyword(Collision) true", CollisionOnCounterBucket),
    ("Reboot + Iceclad continuous grants are present", RebootIcecladGrants),
    ("BecomeDigimonThatCantDigivolve grants TreatAsDigimon -> the Tamer IS a Digimon", TreatAsDigimonGrant),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task BlockerGrantPresence()
{
    EngineContext ctx = Ctx();
    var src = await Place(ctx, P1, "SRC");
    var target = await Place(ctx, P1, "TGT");
    var bystander = await Place(ctx, P1, "OTHER");

    await CardEffectCommons.GainBlocker(Perm(ctx, target), EffectDuration.UntilOpponentTurnEnd, Grant(ctx, src));

    AssertTrue(ContinuousKeywordGate.HasKeyword(ctx, target, ContinuousKeywordGate.Blocker), "TARGET gained Blocker (None bucket)");
    AssertTrue(!ContinuousKeywordGate.HasKeyword(ctx, bystander, ContinuousKeywordGate.Blocker), "bystander did not");
}

async Task RushLiveGate()
{
    EngineContext ctx = Ctx();
    var src = await Place(ctx, P1, "SRC");
    var target = await Place(ctx, P1, "TGT");

    await CardEffectCommons.GainRush(Perm(ctx, target), EffectDuration.UntilEachTurnEnd, Grant(ctx, src));
    AssertTrue(ContinuousKeywordGate.HasKeyword(ctx, target, ContinuousKeywordGate.Rush), "granted while in play");

    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, target, ChoiceZone.BattleArea, ChoiceZone.Trash));
    AssertTrue(!ContinuousKeywordGate.HasKeyword(ctx, target, ContinuousKeywordGate.Rush), "leaving play turns the grant off (live CanUseCondition mirror)");
}

async Task JammingGrant()
{
    EngineContext ctx = Ctx();
    var src = await Place(ctx, P1, "SRC");
    var target = await Place(ctx, P1, "TGT");
    var bystander = await Place(ctx, P1, "OTHER");

    await CardEffectCommons.GainJamming(Perm(ctx, target), EffectDuration.UntilOpponentTurnEnd, Grant(ctx, src));

    AssertTrue(ContinuousKeywordGate.HasKeyword(ctx, target, ContinuousKeywordGate.Jamming), "TARGET gained Jamming (None bucket)");
    AssertTrue(!ContinuousKeywordGate.HasKeyword(ctx, bystander, ContinuousKeywordGate.Jamming), "bystander did not");
}

async Task CollisionOnCounterBucket()
{
    EngineContext ctx = Ctx();
    var src = await Place(ctx, P1, "SRC");
    var target = await Place(ctx, P1, "TGT");

    await CardEffectCommons.GainCollision(Perm(ctx, target), EffectDuration.UntilEachTurnEnd, Grant(ctx, src));
    AssertTrue(ContinuousKeywordGate.HasKeyword(ctx, target, ContinuousKeywordGate.Collision), "Collision present (OnCounterTiming bucket)");
}

async Task RebootIcecladGrants()
{
    EngineContext ctx = Ctx();
    var src = await Place(ctx, P1, "SRC");
    var reb = await Place(ctx, P1, "REB");
    var ice = await Place(ctx, P1, "ICE");

    await CardEffectCommons.GainReboot(Perm(ctx, reb), EffectDuration.UntilEachTurnEnd, Grant(ctx, src));
    await CardEffectCommons.GainIceclad(Perm(ctx, ice), EffectDuration.UntilEachTurnEnd, Grant(ctx, src));

    AssertTrue(ContinuousKeywordGate.HasKeyword(ctx, reb, ContinuousKeywordGate.Reboot), "Reboot present");
    AssertTrue(ContinuousKeywordGate.HasKeyword(ctx, ice, ContinuousKeywordGate.Iceclad), "Iceclad present");
}

async Task TreatAsDigimonGrant()
{
    EngineContext ctx = Ctx();
    var src = await Place(ctx, P1, "SRC");
    var tamer = await PlaceTamer(ctx, P1, "TAMER");
    var controlTamer = await PlaceTamer(ctx, P1, "TAMER2");

    AssertTrue(!ContinuousKeywordGate.IsDigimon(ctx, tamer), "a printed Tamer is not a Digimon");

    await CardEffectCommons.BecomeDigimonThatCantDigivolve(Perm(ctx, tamer), 3000, EffectDuration.UntilOpponentTurnEnd, Grant(ctx, src));

    AssertTrue(ContinuousKeywordGate.IsDigimon(ctx, tamer), "the granted Tamer is now treated as a Digimon");
    AssertTrue(!ContinuousKeywordGate.IsDigimon(ctx, controlTamer), "an un-granted Tamer stays a non-Digimon");
}

// --- Harness ---

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 967);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.TurnController.SetPhase(HeadlessPhase.Main);   // past Setup -> DoneStartGame true (the granted effects' CanTrigger gate)
    return ctx;
}

async Task<HeadlessEntityId> Place(EngineContext ctx, HeadlessPlayerId owner, string tag) => await PlaceOfType(ctx, owner, tag, "Digimon");

async Task<HeadlessEntityId> PlaceTamer(EngineContext ctx, HeadlessPlayerId owner, string tag) => await PlaceOfType(ctx, owner, tag, "Tamer");

async Task<HeadlessEntityId> PlaceOfType(EngineContext ctx, HeadlessPlayerId owner, string tag, string cardType)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 5000, ["level"] = 4 }, CardType: cardType));
    var id = new HeadlessEntityId($"{owner.Value}:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 5000 }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

// A grant-source ICardEffect whose EffectSourceCard is the granting card (AS-IS: card = activateClass.EffectSourceCard).
ICardEffect Grant(EngineContext ctx, HeadlessEntityId srcId)
{
    var host = new CardSource(ctx, srcId, OwnerOf(ctx, srcId), OwnerOf(ctx, srcId));
    var ac = new ActivateClass();
    ac.SetUpICardEffect("G-clean-2 grant", _ => true, host);
    return ac;
}

Permanent Perm(EngineContext ctx, HeadlessEntityId id) => new(ctx, id, OwnerOf(ctx, id));
HeadlessPlayerId OwnerOf(EngineContext ctx, HeadlessEntityId id) =>
    ctx.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? r) && r is not null ? r.OwnerId : default;

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
