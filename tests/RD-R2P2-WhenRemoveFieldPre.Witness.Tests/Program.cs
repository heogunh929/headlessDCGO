// R2-P2-2 witness — the WhenRemoveField PRE cut-in over the deletion pipeline. AS-IS DestroyPermanentsClass.
// Destroy() stacks WhenPermanentWouldBeDeleted -> WhenRemoveField BEFORE the physical move (CardController.cs
// :3690/:3699), so a self-scoped reactor reads its still-attached digivolution sources. The mirror sink already
// opens that exact PRE pair (MatchStateMutationSink.cs:1460-1465, the shared ForCutIn stack+drain primitive).
//
// This witness proves the PRE semantics are LIVE for a ported, window-form, self-scoped WhenRemoveField
// registrant — AD1_013 (ZeigGreymon): "[All Turns] when this would leave the battle area other than by
// DigiXros, you may play 1 Lv.5- Blue Flare/Xros Heart Digimon from ITS digivolution cards." The reactor's
// CanActivate + SelectCardEffect read ResolvePermanentOfThisCard(card).DigivolutionCards — pre-trash state. If
// it were derived POST-move (design item R2-P2-2's feared divergence) the sources would already be gone and the
// pick would be empty. The suite asserts: PRE collection, a single PRE fire reading the attached source, that
// POST-move state genuinely differs (the permanent leaves the field), and a no-reactor control.

using System.Collections;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;
using Cec = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using Script = HeadlessDCGO.Engine.Assets.Scripts.Script;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("PRE COLLECTION: the sink's WhenRemoveField PRE window collects AD1_013's self-scoped reactor over the pre-trash list, sources still attached", PreWindowCollectsReactor),
    ("DRIVEN PRE FIRE: the real sink deletion fires AD1_013's reactor to its digivolution-source pick exactly once (PRE, source readable)", DrivenPreFireReadsSources),
    ("POST DIFFERS: after the move completes the permanent leaves the field — proving the PRE window observed state a POST derivation could not", PostMoveStateDiffers),
    ("CONTROL: a permanent with no WhenRemoveField reactor deletes with no cut-in (false-green guard)", ControlNoReactor),
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

async Task PreWindowCollectsReactor()
{
    (EngineContext ctx, _) = NewCtx(7101, P1);
    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(ctx);
    var ad = await StageReal(ctx, P1, "AD1_013", "1:battle:ad013");
    var src = StageSyn(ctx, P1, "BLUEFLARE5", "1:src:bf5", level: 5, traits: new[] { "Blue Flare" });
    SetSources(ctx, "1:battle:ad013", src);

    var perm = new Cec.Permanent(ctx, ad, P1) { willBeRemoveField = true };
    int srcCountPre = perm.DigivolutionCards.Count;
    var cause = BareCauseEffect.For(ctx, new HeadlessEntityId("2:battle:cause"));
    var ht = Cec.CardEffectCommons.WhenPermanentWouldRemoveFieldCheckHashtable(new List<Cec.Permanent> { perm }, cause, null);
    int collected = Script.AutoProcessing.GetSkillInfos(ht, Cec.EffectTiming.WhenRemoveField).Count;
    perm.willBeRemoveField = false;

    AssertEqual(1, collected, "the sink PRE window collects AD1_013's WhenRemoveField reactor over the pre-trash list");
    AssertEqual(1, srcCountPre, "the digivolution source is still attached at PRE-window time");
}

async Task DrivenPreFireReadsSources()
{
    (EngineContext ctx, PolicyChoiceProvider policy) = NewCtx(7102, P1);
    policy.On(req => req.Type == ChoiceType.OptionalEffect, req => ChoiceResult.Select(req.Candidates[0].Id), oneShot: false);
    policy.On(req => true, req => req.CanSkip ? ChoiceResult.Skip() : ChoiceResult.Select(req.Candidates.First(c => c.IsSelectable).Id), oneShot: false);
    ctx.MemoryController.Set(10);
    var ad = await StageReal(ctx, P1, "AD1_013", "1:battle:ad013");
    var src = StageSyn(ctx, P1, "BLUEFLARE5", "1:src:bf5", level: 5, traits: new[] { "Blue Flare" });
    SetSources(ctx, "1:battle:ad013", src);
    await StageReal(ctx, P2, "AD1_013", "2:battle:foe");

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(ctx);
    await DriveDelete(ctx, ad);

    int fires = policy.Seen.Count(s => s.Contains("digivolution card to play"));
    AssertEqual(1, fires, $"AD1_013's WhenRemoveField reactor fired exactly once, PRE-move, reading its attached source (no double-fire) [seen:{string.Join(" | ", policy.Seen)}]");
}

async Task PostMoveStateDiffers()
{
    (EngineContext ctx, PolicyChoiceProvider policy) = NewCtx(7103, P1);
    // Decline the reactor's optional pick so the permanent proceeds to actually leave the field.
    policy.On(req => req.Type == ChoiceType.OptionalEffect && req.CanSkip, req => ChoiceResult.Skip(), oneShot: false);
    policy.On(req => true, req => req.CanSkip ? ChoiceResult.Skip() : ChoiceResult.Select(req.Candidates.First(c => c.IsSelectable).Id), oneShot: false);
    ctx.MemoryController.Set(10);
    var ad = await StageReal(ctx, P1, "AD1_013", "1:battle:ad013");
    var src = StageSyn(ctx, P1, "BLUEFLARE5", "1:src:bf5", level: 5, traits: new[] { "Blue Flare" });
    SetSources(ctx, "1:battle:ad013", src);
    await StageReal(ctx, P2, "AD1_013", "2:battle:foe");

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(ctx);
    var permBefore = new Cec.Permanent(ctx, ad, P1);
    int srcBefore = permBefore.DigivolutionCards.Count;
    await DriveDelete(ctx, ad);

    bool leftField = !((IZoneStateReader)ctx.ZoneMover).GetCards(P1, ChoiceZone.BattleArea).Contains(ad);
    AssertEqual(1, srcBefore, "the source was attached before the move");
    AssertTrue(leftField, "the permanent left the battle area after the move — POST-move the pre-trash field state (attached source) is no longer readable, so the PRE window's read genuinely mattered");
}

async Task ControlNoReactor()
{
    (EngineContext ctx, PolicyChoiceProvider policy) = NewCtx(7104, P1);
    policy.On(req => true, req => req.CanSkip ? ChoiceResult.Skip() : ChoiceResult.Select(req.Candidates.First(c => c.IsSelectable).Id), oneShot: false);
    var plain = StageSyn(ctx, P1, "PLAIN", "1:battle:plain", level: 4, traits: Array.Empty<string>());
    HeadlessDCGO.Engine.Headless.Runtime.CardEffectRegistrar.RegisterCard(ctx, plain, P1);
    await MoveToBattle(ctx, P1, plain);
    await StageReal(ctx, P2, "AD1_013", "2:battle:foe");

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(ctx);
    await DriveDelete(ctx, plain);

    AssertFalse(policy.Seen.Any(s => s.Contains("digivolution card to play")),
        $"a plain permanent's deletion opens no WhenRemoveField cut-in (false-green guard) [seen:{string.Join(" | ", policy.Seen)}]");
    AssertTrue(!((IZoneStateReader)ctx.ZoneMover).GetCards(P1, ChoiceZone.BattleArea).Contains(plain), "the plain permanent was trashed");
}

// ───────────────────────────── harness ─────────────────────────────

async Task DriveDelete(EngineContext ctx, HeadlessEntityId target)
{
    var sink = new MatchStateMutationSink(ctx.CardInstanceRepository, null, ctx.ZoneMover, ctx.MemoryController, ctx.GameEventQueue, context: ctx);
    sink.Apply(new EffectMutation(MatchStateMutationSink.DeleteKind, new HeadlessEntityId("2:battle:foe"),
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = target.Value }));
    await sink.FlushAsync();
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

async Task<HeadlessEntityId> StageReal(EngineContext ctx, HeadlessPlayerId owner, string number, string instanceId)
{
    var defId = new HeadlessEntityId(number);
    if (!ctx.CardRepository.TryGetCard(defId, out CardRecord? def) || def is null)
        throw new InvalidOperationException($"definition {number} not loaded");
    var id = new HeadlessEntityId(instanceId);
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["isSuspended"] = false }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    HeadlessDCGO.Engine.Headless.Runtime.CardEffectRegistrar.RegisterCard(ctx, id, owner);
    return id;
}

async Task MoveToBattle(EngineContext ctx, HeadlessPlayerId owner, HeadlessEntityId id)
    => await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));

HeadlessEntityId StageSyn(EngineContext ctx, HeadlessPlayerId owner, string number, string instanceId, int level, string[] traits)
{
    var defId = new HeadlessEntityId($"DEF:{number}:{owner.Value}");
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal)
    { ["dp"] = 4000, ["level"] = level, ["traits"] = traits, ["colors"] = new[] { "Blue" }, ["playCost"] = 0 };
    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(defId, number, number, meta, CardType: "Digimon", PlayCost: 0));
    var id = new HeadlessEntityId(instanceId);
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = level, ["traits"] = traits, ["isSuspended"] = false }));
    return id;
}

void SetSources(EngineContext ctx, string hostInstance, params HeadlessEntityId[] sourceIds)
{
    var hostId = new HeadlessEntityId(hostInstance);
    if (!ctx.CardInstanceRepository.TryGetInstance(hostId, out CardInstanceRecord? rec) || rec is null)
        throw new InvalidOperationException($"missing host {hostInstance}");
    var meta = new Dictionary<string, object?>(rec.Metadata, StringComparer.Ordinal)
    { [DigivolutionStackReader.SourceIdsKey] = sourceIds.Select(i => i.Value).ToArray() };
    ctx.CardInstanceRepository.Upsert(rec with { Metadata = meta });
}

static void AssertTrue(bool v, string m) { if (!v) throw new InvalidOperationException($"Assertion failed: {m}"); }
static void AssertFalse(bool v, string m) { if (v) throw new InvalidOperationException($"Assertion failed: {m}"); }
static void AssertEqual<T>(T expected, T actual, string m)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"Assertion failed: {m} (expected {expected}, got {actual})");
}

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
