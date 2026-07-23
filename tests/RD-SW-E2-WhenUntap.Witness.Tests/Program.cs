// RD-SW-E-02 witness — the WhenUntapAnyone PRE cut-in window (design item MIG3-CUTIN-WHENUNTAP, now wired
// into IUnsuspendPermanents.Unsuspend, CardController.cs :1985 region). The window is the MANUAL-push variant:
// AutoProcessing.GetSkillInfos({CardEffect, Permanents}, WhenUntapAnyone) -> PutStackedSkill each onto the
// ForCutIn stack -> TriggeredSkillProcess(false, HasExecutedSameEffect), opened over the pre-fix
// untappedPermanents list BEFORE the untap applies (SetIsSuspended).
//
// Witness = the ported reactor BT7_055 [None] AddSkillClass: it grants a WhenUntapAnyone reactor to the
// opponent's Digimon reading "you must trash 1 card in your hand to unsuspend this Digimon"; with no hand card
// to trash, the reactor calls GainCanNotUnsuspend, so the pre-untap re-filter (untappedPermanentsFixed) drops
// the target and it STAYS suspended. This is the AS-IS PRE semantics — a reactor cancelling an unsuspend before
// it lands — that the POST-only OnUnTappedAnyone window cannot express.
//
// Collection proofs (the wired GetSkillInfos collects the granted reactor; false-green control collects nothing)
// + a DRIVEN-FIRE proof (the real IUnsuspendPermanents.Unsuspend routes through the wired window, fires the
// granted reactor, and the target stays suspended; the ungranted control unsuspends).

using System.Collections;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;
using Cec = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using Script = HeadlessDCGO.Engine.Assets.Scripts.Script;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("WINDOW COLLECTS: the wired GetSkillInfos({CardEffect,Permanents}, WhenUntapAnyone) collects BT7_055's granted unsuspend reactor", WindowCollectsReactor),
    ("CONTROL (no grantor): with BT7_055 absent, the window collects nothing (false-green guard)", ControlNoGrantorCollectsZero),
    ("DRIVEN FIRE: IUnsuspendPermanents.Unsuspend routes through the wired PRE window, fires BT7_055's reactor (no hand -> GainCanNotUnsuspend), target STAYS suspended", DrivenFireBlocksUnsuspend),
    ("DRIVEN CONTROL (no grantor): the same unsuspend with BT7_055 absent unsuspends the target normally", DrivenControlUnsuspends),
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

async Task WindowCollectsReactor()
{
    (EngineContext ctx, _) = NewCtx(seed: 6101, turnPlayer: P2);
    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(ctx);

    await StageReal(ctx, P1, "BT7_055", "1:battle:bt7055", suspended: false);
    HeadlessEntityId target = StageSyn(ctx, P2, "OPP-DIGI", "2:battle:target", suspended: true);

    Hashtable ht = new() { { "CardEffect", (Cec.ICardEffect?)null }, { "Permanents", new List<Cec.Permanent> { new(ctx, target, P2) } } };
    int collected = Script.AutoProcessing.GetSkillInfos(ht, Cec.EffectTiming.WhenUntapAnyone).Count;
    AssertEqual(1, collected, "the window collects BT7_055's granted WhenUntapAnyone reactor on the opponent's Digimon");
}

async Task ControlNoGrantorCollectsZero()
{
    (EngineContext ctx, _) = NewCtx(seed: 6102, turnPlayer: P2);
    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(ctx);

    HeadlessEntityId target = StageSyn(ctx, P2, "OPP-DIGI", "2:battle:target", suspended: true);

    Hashtable ht = new() { { "CardEffect", (Cec.ICardEffect?)null }, { "Permanents", new List<Cec.Permanent> { new(ctx, target, P2) } } };
    int collected = Script.AutoProcessing.GetSkillInfos(ht, Cec.EffectTiming.WhenUntapAnyone).Count;
    AssertEqual(0, collected, "no grantor on field -> the window is a no-op (false-green guard)");
    await Task.CompletedTask;
}

async Task DrivenFireBlocksUnsuspend()
{
    (EngineContext ctx, PolicyChoiceProvider policy) = NewCtx(seed: 6103, turnPlayer: P2);
    policy.On(req => true, req => req.CanSkip ? ChoiceResult.Skip() : ChoiceResult.Select(req.Candidates.First(c => c.IsSelectable).Id), oneShot: false);

    await StageReal(ctx, P1, "BT7_055", "1:battle:bt7055", suspended: false);
    HeadlessEntityId target = StageSyn(ctx, P2, "OPP-DIGI", "2:battle:target", suspended: true);
    // P2 has NO hand cards -> BT7_055's reactor mandatorily GainCanNotUnsuspend's the target.

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(ctx);
    var targetPerm = new Cec.Permanent(ctx, target, P2);
    await new Script.IUnsuspendPermanents(new List<Cec.Permanent> { targetPerm }, cardEffect: null).Unsuspend();

    AssertTrue(IsSuspended(ctx, target),
        "the wired PRE window fired BT7_055's reactor which blocked the unsuspend (target STAYS suspended)");
}

async Task DrivenControlUnsuspends()
{
    (EngineContext ctx, PolicyChoiceProvider policy) = NewCtx(seed: 6104, turnPlayer: P2);
    policy.On(req => true, req => req.CanSkip ? ChoiceResult.Skip() : ChoiceResult.Select(req.Candidates.First(c => c.IsSelectable).Id), oneShot: false);

    HeadlessEntityId target = StageSyn(ctx, P2, "OPP-DIGI", "2:battle:target", suspended: true);

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(ctx);
    var targetPerm = new Cec.Permanent(ctx, target, P2);
    await new Script.IUnsuspendPermanents(new List<Cec.Permanent> { targetPerm }, cardEffect: null).Unsuspend();

    AssertFalse(IsSuspended(ctx, target),
        "with no grantor the PRE window is a no-op and the target unsuspends normally");
}

// ───────────────────────────── harness ─────────────────────────────

(EngineContext, PolicyChoiceProvider) NewCtx(int seed, HeadlessPlayerId turnPlayer)
{
    var policy = new PolicyChoiceProvider();
    EngineContext ctx = ContextFactory.CreateWithProvider(policy, seed);
    CardBaseEntityLoader.LoadInto((CardDatabase)ctx.CardRepository);
    ctx.TurnController.Initialize(new[] { P1, P2 }, turnPlayer);
    ctx.TurnController.SetPhase(HeadlessPhase.Main);
    return (ctx, policy);
}

async Task<HeadlessEntityId> StageReal(EngineContext ctx, HeadlessPlayerId owner, string number, string instanceId, bool suspended)
{
    var defId = new HeadlessEntityId(number);
    if (!ctx.CardRepository.TryGetCard(defId, out CardRecord? def) || def is null)
        throw new InvalidOperationException($"definition {number} not loaded");
    var id = new HeadlessEntityId(instanceId);
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["isSuspended"] = suspended }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    HeadlessDCGO.Engine.Headless.Runtime.CardEffectRegistrar.RegisterCard(ctx, id, owner);
    return id;
}

HeadlessEntityId StageSyn(EngineContext ctx, HeadlessPlayerId owner, string number, string instanceId, bool suspended)
{
    var defId = new HeadlessEntityId($"DEF:{number}:{owner.Value}");
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 };
    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(defId, number, number, meta, CardType: "Digimon"));
    var id = new HeadlessEntityId(instanceId);
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4, ["isSuspended"] = suspended }));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
    HeadlessDCGO.Engine.Headless.Runtime.CardEffectRegistrar.RegisterCard(ctx, id, owner);
    return id;
}

bool IsSuspended(EngineContext ctx, HeadlessEntityId id) =>
    ctx.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? r) && r is not null
    && r.Metadata.TryGetValue("isSuspended", out object? v) && v is true;

static void AssertTrue(bool v, string m) { if (!v) throw new InvalidOperationException($"Assertion failed: {m}"); }
static void AssertFalse(bool v, string m) { if (v) throw new InvalidOperationException($"Assertion failed: {m}"); }
static void AssertEqual<T>(T expected, T actual, string m)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"Assertion failed: {m} (expected {expected}, got {actual})");
}

sealed class PolicyChoiceProvider : IChoiceProvider
{
    private readonly List<(Func<ChoiceRequest, bool> Applies, Func<ChoiceRequest, ChoiceResult> Answer, bool OneShot)> _handlers = new();
    private readonly ScriptedChoiceProvider _fallback = new();
    public List<string> Seen { get; } = new();
    public void On(Func<ChoiceRequest, bool> applies, Func<ChoiceRequest, ChoiceResult> answer, bool oneShot = true)
        => _handlers.Add((applies, answer, oneShot));
    public Task<ChoiceResult> ChooseAsync(ChoiceRequest request, CancellationToken cancellationToken = default)
    {
        Seen.Add($"{request.Type}:'{request.Message}'x{request.Candidates.Count}");
        for (int i = 0; i < _handlers.Count; i++)
        {
            var (applies, answer, oneShot) = _handlers[i];
            if (applies(request))
            {
                ChoiceResult result = answer(request);
                result.ThrowIfInvalid(request);
                if (oneShot) _handlers.RemoveAt(i);
                return Task.FromResult(result);
            }
        }
        return _fallback.ChooseAsync(request, cancellationToken);
    }
}

static class ContextFactory
{
    public static EngineContext CreateWithProvider(IChoiceProvider provider, int randomSeed)
    {
        var randomSource = new GameRandomSource(randomSeed);
        var cardInstanceRepository = new InMemoryCardInstanceRepository();
        var logSink = new NullLogSink();
        var zoneMover = new InMemoryZoneMover(randomSource);
        var memoryController = new InMemoryHeadlessMemoryController();
        var gameEventQueue = new GameEventQueue();
        EngineContext? selfRef = null;
        var effectScheduler = new EffectScheduler(
            new EffectResolutionQueue(),
            CardEffectSchedulerResolver.Create(
                sinkFactory: _ => new MatchStateMutationSink(
                    cardInstanceRepository, logSink, zoneMover, memoryController, gameEventQueue,
                    currentTurnPlayer: () => selfRef?.TurnController.Current.TurnPlayerId,
                    context: selfRef),
                strictUnbound: false));
        var choiceController = new InMemoryHeadlessChoiceController();
        var context = new EngineContext(
            provider, randomSource, new CardDatabase(), cardInstanceRepository, zoneMover,
            new InMemoryRuleQueryService(), new InMemoryHeadlessTurnController(), choiceController,
            new InMemoryHeadlessAttackController(), memoryController, logSink,
            new HeadlessDCGO.Engine.Headless.Coroutines.EngineTaskRunner(),
            effectScheduler, gameEventQueue: gameEventQueue);
        selfRef = context;
        return context;
    }
}
