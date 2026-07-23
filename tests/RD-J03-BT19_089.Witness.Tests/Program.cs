// RD-P6B-13 / RD-J-03 witness — BT19_089 (Red), the FIRST live caller of the permanent-target
// GainImmuneFromDPMinus primitive, and the card whose [Main] SkillCondition is the confirmed dual-flag
// predicate (`EffectSourceCard.Owner == Enemy && (!IsDigimonEffect || !IsTamerEffect)`).
//
// The [Main] effect grants the selected owner Digimon (a) immune-from-DP-minus gated by SkillCondition and
// (b) immune-from-opponent-Option (CanNotAffectedClass on UntilOpponentTurnEndEffects). This suite drives the
// [Main] ActivateClass and reads the grants back through the LIVE read path Permanent.ImmuneFromDPMinus(cause) —
// which passes the REAL causing effect to the stored predicate, so the dual-flag refinement is exercised
// verbatim: an opponent effect flagged BOTH digimon- AND tamer-effect is NOT immune (refinement honoured), a
// plain opponent effect IS immune, and an own effect is not. It also witnesses [Security] (add to hand).
//
// The RD-J-03 residual is orthogonal to this read path: it lives in the DP-APPLICATION path
// (ContinuousDpGate -> NewModelContinuousScan.BuildCausingEffectStandIn), which reconstructs the cause from a
// bare source id, so the two per-instance flags default false there and the both-flags refinement is lost
// (narrow over-approximation). That fix needs shared continuous-scan/DP-gate infra threading, coordinated with
// the parallel immunity-guard work — see the card header. This suite proves the predicate itself is faithfully
// threaded (not simplified) and correct given a real cause.

using System.Collections;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;
using Cec = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using Cfx = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using Script = HeadlessDCGO.Engine.Assets.Scripts.Script;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("MAIN GRANT: [Main] grants the selected owner Digimon immune-from-DP-minus (plain opponent cause blocked) + immune-from-opponent-Option", MainGrantsImmunities),
    ("DUAL-FLAG refinement (live read): an opponent cause flagged BOTH digimon- AND tamer-effect is NOT immune; own cause not immune", DualFlagRefinementHonoured),
    ("APPLICATION PATH (RD-J-03): a cause rebuilt from a bare source id (BuildCausingEffectStandIn) reproduces the real IsDigimonEffect/IsTamerEffect — a both-flagged enemy cause is NOT immune (DP-minus applies); plain-enemy blocked + own not-immune controls unchanged", ApplicationPathReconstructsFlags),
    ("SECURITY: [Security] adds this card from security to the hand", SecurityAddsToHand),
};

int failed = 0;
foreach ((string name, Func<Task> body) in tests)
{
    try { await body(); Console.WriteLine($"PASS {name}"); }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"FAIL {name}");
        Console.WriteLine($"     {ex.GetType().Name}: {ex.Message}");
        if (ex.StackTrace is string st) Console.WriteLine(string.Join('\n', st.Split('\n').Take(12)));
    }
}
Console.WriteLine($"SUMMARY: PASS={tests.Length - failed} FAIL={failed} TOTAL={tests.Length}");
if (failed > 0) Environment.Exit(1);

// ───────────────────────────── tests ─────────────────────────────

async Task<(EngineContext Ctx, HeadlessEntityId Bt, HeadlessEntityId Target)> DriveMain(int seed)
{
    (EngineContext ctx, PolicyChoiceProvider policy) = NewCtx(seed, P1);
    var bt = await StageReal(ctx, P1, "BT19_089", "1:battle:bt19089");
    var target = StageSyn(ctx, P1, "OWN-DIGI", "1:battle:target");
    policy.On(req => req.Type == ChoiceType.Permanent && req.Candidates.Any(c => c.Id == target),
        req => ChoiceResult.Select(target), oneShot: false);
    policy.On(req => true, req => req.CanSkip ? ChoiceResult.Skip() : ChoiceResult.Select(req.Candidates.First(c => c.IsSelectable).Id), oneShot: false);

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(ctx);
    var card = new Cec.CardSource(ctx, bt, P1);
    var effect = new HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT19.Red.BT19_089();
    var main = (Cfx.ActivateClass)effect.CardEffects(Cec.EffectTiming.OptionSkill, card).First(e => e.EffectName.StartsWith("This Digimon becomes immune"));
    await main.Activate(new Hashtable());
    return (ctx, bt, target);
}

async Task MainGrantsImmunities()
{
    var (ctx, _, target) = await DriveMain(9001);
    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(ctx);
    var targetPerm = new Cec.Permanent(ctx, target, P1);

    var plainOpponentCause = MakeCause(ctx, P2, "opp-opt", digimon: false, tamer: false);
    AssertTrue(targetPerm.ImmuneFromDPMinus(plainOpponentCause), "a plain opponent effect's DP-minus is blocked (immune-from-DP-minus granted)");

    bool hasCanNotAffected = targetPerm.UntilOpponentTurnEndEffects.Any(g => g(Cec.EffectTiming.None) is Cfx.CanNotAffectedClass);
    AssertTrue(hasCanNotAffected, "the selected Digimon gained the immune-from-opponent-Option (CanNotAffectedClass) grant on UntilOpponentTurnEndEffects");
}

async Task DualFlagRefinementHonoured()
{
    var (ctx, _, target) = await DriveMain(9002);
    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(ctx);
    var targetPerm = new Cec.Permanent(ctx, target, P1);

    var bothFlagsOpponent = MakeCause(ctx, P2, "opp-both", digimon: true, tamer: true);
    AssertFalse(targetPerm.ImmuneFromDPMinus(bothFlagsOpponent),
        "an opponent effect flagged BOTH digimon- and tamer-effect is NOT immune — the dual-flag refinement is honoured on the live read path");

    var ownCause = MakeCause(ctx, P1, "own-opt", digimon: false, tamer: false);
    AssertFalse(targetPerm.ImmuneFromDPMinus(ownCause), "an OWN effect's DP-minus is not immune (Owner != Enemy)");
}

async Task ApplicationPathReconstructsFlags()
{
    // RD-J-03 / RD-P6B-13 DP-application half. Where the read path (above) passes the LIVE causing effect, the
    // APPLICATION path rebuilds the cause from a bare source id via NewModelContinuousScan.BuildCausingEffectStandIn
    // (the shared stand-in the restriction/mutation cause-scans and the retired ContinuousDpGate DP half consume).
    // The fix threads the source card's IsDigimonEffect/IsTamerEffect onto that stand-in, so BT19_089's dual-flag
    // SkillCondition (`Owner==Enemy && (!IsDigimonEffect || !IsTamerEffect)`) now evaluates IDENTICALLY to the read
    // path. Before the fix the stand-in defaulted both flags false, so a both-flagged enemy cause wrongly satisfied
    // the refinement (immune / DP-minus blocked — a narrow over-approximation).
    var (ctx, _, target) = await DriveMain(9004);
    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(ctx);
    var targetPerm = new Cec.Permanent(ctx, target, P1);

    // A BOTH-flagged enemy cause — a dual Digimon-and-Tamer source card, so the reconstructed stand-in reports
    // IsDigimonEffect && IsTamerEffect: the refinement excludes it → NOT immune → the DP-minus APPLIES.
    var bothFlaggedId = StageCauseCard(ctx, P2, "cause-both", cardType: "Digimon", extraTypes: "Tamer");
    var bothStandIn = Cec.NewModelContinuousScan.BuildCausingEffectStandIn(ctx, bothFlaggedId);
    AssertFalse(targetPerm.ImmuneFromDPMinus(bothStandIn),
        "application path: a stand-in rebuilt from a BOTH-flagged (Digimon+Tamer) enemy cause is NOT immune — DP-minus applies (dual-flag refinement now honoured on the reconstruction path; previously wrongly blocked)");

    // Control: a plain enemy cause (Option — neither flag) still satisfies the refinement → immune → BLOCKED.
    var plainId = StageCauseCard(ctx, P2, "cause-plain", cardType: "Option", extraTypes: null);
    var plainStandIn = Cec.NewModelContinuousScan.BuildCausingEffectStandIn(ctx, plainId);
    AssertTrue(targetPerm.ImmuneFromDPMinus(plainStandIn),
        "application path (control): a stand-in rebuilt from a PLAIN enemy cause is immune — DP-minus blocked (unchanged)");

    // Control: an OWN cause (Owner != Enemy) is never immune.
    var ownId = StageCauseCard(ctx, P1, "cause-own", cardType: "Option", extraTypes: null);
    var ownStandIn = Cec.NewModelContinuousScan.BuildCausingEffectStandIn(ctx, ownId);
    AssertFalse(targetPerm.ImmuneFromDPMinus(ownStandIn),
        "application path (control): a stand-in rebuilt from an OWN cause is not immune (unchanged)");
}

async Task SecurityAddsToHand()
{
    (EngineContext ctx, PolicyChoiceProvider policy) = NewCtx(9003, P1);
    policy.On(req => true, req => req.CanSkip ? ChoiceResult.Skip() : ChoiceResult.Select(req.Candidates.First(c => c.IsSelectable).Id), oneShot: false);
    var bt = await StageReal(ctx, P1, "BT19_089", "1:sec:bt19089", zone: ChoiceZone.Security);

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(ctx);
    var card = new Cec.CardSource(ctx, bt, P1);
    var effect = new HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT19.Red.BT19_089();
    var sec = (Cfx.ActivateClass)effect.CardEffects(Cec.EffectTiming.SecuritySkill, card).First(e => e.EffectName == "Add this card to hand");
    await sec.Activate(new Hashtable());

    bool inHand = ((IZoneStateReader)ctx.ZoneMover).GetCards(P1, ChoiceZone.Hand).Contains(bt);
    AssertTrue(inHand, "[Security] moved BT19_089 from the security stack into the hand");
}

// ───────────────────────────── harness ─────────────────────────────

Cec.ICardEffect MakeCause(EngineContext ctx, HeadlessPlayerId owner, string instance, bool digimon, bool tamer)
{
    var defId = new HeadlessEntityId($"DEF:CAUSE:{instance}");
    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(defId, instance, instance,
        new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Option"));
    var id = new HeadlessEntityId($"{owner.Value}:cause:{instance}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)));
    var src = new Cec.CardSource(ctx, id, owner);
    var eff = new Cfx.ActivateClass();
    eff.SetUpICardEffect(instance, _ => true, src);
    eff.SetIsDigimonEffect(digimon);
    eff.SetIsTamerEffect(tamer);
    return eff;
}

// Stage a bare CAUSE card (definition + owned instance) whose type drives the reconstructed stand-in's
// IsDigimonEffect/IsTamerEffect (source.IsDigimon/IsTamer). `extraTypes` rides the AdditionalCardTypesKey so a
// single card can report BOTH Digimon and Tamer (a dual card), the only shape that exercises the refinement.
HeadlessEntityId StageCauseCard(EngineContext ctx, HeadlessPlayerId owner, string instance, string cardType, string? extraTypes)
{
    var defId = new HeadlessEntityId($"DEF:CAUSE:{instance}");
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal);
    if (extraTypes is not null) meta[CardRecord.AdditionalCardTypesKey] = extraTypes;
    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(defId, instance, instance, meta, CardType: cardType));
    var id = new HeadlessEntityId($"{owner.Value}:cause:{instance}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)));
    return id;
}

(EngineContext, PolicyChoiceProvider) NewCtx(int seed, HeadlessPlayerId turnPlayer)
{
    var policy = new PolicyChoiceProvider();
    EngineContext ctx = ContextFactory.CreateWithProvider(policy, seed);
    CardBaseEntityLoader.LoadInto((CardDatabase)ctx.CardRepository);
    ctx.TurnController.Initialize(new[] { P1, P2 }, turnPlayer);
    ctx.TurnController.SetPhase(HeadlessPhase.Main);
    return (ctx, policy);
}

async Task<HeadlessEntityId> StageReal(EngineContext ctx, HeadlessPlayerId owner, string number, string instanceId, ChoiceZone zone = ChoiceZone.BattleArea)
{
    var defId = new HeadlessEntityId(number);
    if (!ctx.CardRepository.TryGetCard(defId, out CardRecord? def) || def is null)
        throw new InvalidOperationException($"definition {number} not loaded");
    var id = new HeadlessEntityId(instanceId);
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["isSuspended"] = false }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone));
    Cec.CardEffectRegistrar.RegisterCard(ctx, id, owner);
    return id;
}

HeadlessEntityId StageSyn(EngineContext ctx, HeadlessPlayerId owner, string number, string instanceId)
{
    var defId = new HeadlessEntityId($"DEF:{number}:{owner.Value}");
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 5000, ["level"] = 5 };
    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(defId, number, number, meta, CardType: "Digimon"));
    var id = new HeadlessEntityId(instanceId);
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 5000, ["level"] = 5, ["isSuspended"] = false }));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
    Cec.CardEffectRegistrar.RegisterCard(ctx, id, owner);
    return id;
}

static void AssertTrue(bool v, string m) { if (!v) throw new InvalidOperationException($"Assertion failed: {m}"); }
static void AssertFalse(bool v, string m) { if (v) throw new InvalidOperationException($"Assertion failed: {m}"); }

sealed class PolicyChoiceProvider : IChoiceProvider
{
    private readonly List<(Func<ChoiceRequest, bool>, Func<ChoiceRequest, ChoiceResult>, bool)> _h = new();
    private readonly ScriptedChoiceProvider _fallback = new();
    public List<string> Seen { get; } = new();
    public void On(Func<ChoiceRequest, bool> a, Func<ChoiceRequest, ChoiceResult> b, bool oneShot = true) => _h.Add((a, b, oneShot));
    public Task<ChoiceResult> ChooseAsync(ChoiceRequest request, CancellationToken ct = default)
    {
        Seen.Add($"{request.Type}:'{request.Message}'x{request.Candidates.Count}");
        for (int i = 0; i < _h.Count; i++)
        {
            var (a, b, one) = _h[i];
            if (a(request)) { var r = b(request); r.ThrowIfInvalid(request); if (one) _h.RemoveAt(i); return Task.FromResult(r); }
        }
        return _fallback.ChooseAsync(request, ct);
    }
}

static class ContextFactory
{
    public static EngineContext CreateWithProvider(IChoiceProvider provider, int randomSeed)
    {
        var rs = new GameRandomSource(randomSeed);
        var cir = new InMemoryCardInstanceRepository();
        var log = new NullLogSink();
        var zm = new InMemoryZoneMover(rs);
        var mem = new InMemoryHeadlessMemoryController();
        var geq = new GameEventQueue();
        EngineContext? self = null;
        var es = new EffectScheduler(new EffectResolutionQueue(),
            CardEffectSchedulerResolver.Create(
                sinkFactory: _ => new MatchStateMutationSink(cir, log, zm, mem, geq,
                    currentTurnPlayer: () => self?.TurnController.Current.TurnPlayerId, context: self),
                strictUnbound: false));
        var cc = new InMemoryHeadlessChoiceController();
        var ctx = new EngineContext(provider, rs, new CardDatabase(), cir, zm, new InMemoryRuleQueryService(),
            new InMemoryHeadlessTurnController(), cc, new InMemoryHeadlessAttackController(), mem, log,
            new HeadlessDCGO.Engine.Headless.Coroutines.EngineTaskRunner(), es, gameEventQueue: geq);
        self = ctx;
        return ctx;
    }
}
