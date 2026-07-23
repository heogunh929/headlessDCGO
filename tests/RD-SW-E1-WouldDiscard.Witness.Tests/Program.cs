// RD-SW-E-01 witness — the WhenWouldDigivolutionCardDiscarded PRE cut-in window
// (design item MIG3-CUTIN-WOULDDISCARD, now wired into ITrashDigivolutionCards.TrashDigivolutionCards,
// CardController.cs :1176 region). The window rides the shared ForCutIn synchronous stack+drain primitive
// (StackSkillInfos(WhenDigivolutionCardWouldDiscardedCheckHashtable(...), WhenWouldDigivolutionCardDiscarded)
// -> HasAwaitingActivateEffects gate -> TriggeredSkillProcess(false, HasExecutedSameEffect)), opened BEFORE
// the digivolution-source trash is fixed.
//
// Witness = the ported reactor BT10_084 [Opponent's Turn]: "When an effect would trash one of your OTHER
// Digimon's digivolution cards, you may trash THIS Digimon's digivolution cards instead." Its ActivateCoroutine
// reads the pre-trash DiscardedCards list, clears willBeRemoveSources on them (sparing them), and opens a
// SelectCardEffect over its OWN sources to trash instead.
//
// Following the C-Del-PRE discipline: collection proofs (the window's GetSkillInfos over the real pre-trash
// hashtable collects the ported reactor; a false-green control collects nothing; the self-source exclusion is
// honoured) + a DRIVEN-FIRE proof (running the real TrashDigivolutionCards pipeline routes through the wired
// window and drives BT10_084's reactor to its interactive SelectCardEffect pick — observed via the choice
// provider's Seen log, independent of any downstream sink-trash mechanics).

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
    ("WINDOW COLLECTS: the wired GetSkillInfos over the pre-trash hashtable collects BT10_084's WhenWouldDigivolutionCardDiscarded reactor", WindowCollectsReactor),
    ("CONTROL (no reactor): with BT10_084 absent, the window collects nothing (false-green guard)", ControlNoReactorCollectsZero),
    ("SELF-SOURCE EXCLUDED: when the trashed permanent IS BT10_084 itself, PermanentCondition excludes it -> 0 collected", SelfSourceExcluded),
    ("DRIVEN FIRE: running TrashDigivolutionCards over another Digimon's source routes through the wired PRE window and drives BT10_084's reactor to its SelectCardEffect pick", DrivenFireReachesReactor),
    ("DRIVEN CONTROL (no reactor): the same trash with BT10_084 absent opens NO cut-in choice", DrivenControlNoReactor),
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
    (EngineContext ctx, _) = NewCtx(seed: 5101, turnPlayer: P2);
    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(ctx);

    await StageReal(ctx, P1, "BT10_084", "1:battle:bt084");
    HeadlessEntityId bt084src = StageSyn(ctx, P1, "SRC-A", "1:src:bt084src", cardType: "Option", zone: ChoiceZone.None);
    SetSources(ctx, "1:battle:bt084", bt084src);

    HeadlessEntityId other = StageSyn(ctx, P1, "OTHER-DIGI", "1:battle:other");
    HeadlessEntityId othersrc = StageSyn(ctx, P1, "SRC-B", "1:src:othersrc", cardType: "Option", zone: ChoiceZone.None);
    SetSources(ctx, "1:battle:other", othersrc);

    Hashtable ht = BuildHashtable(ctx, other, P1, othersrc, P2);
    int collected = Script.AutoProcessing.GetSkillInfos(ht, Cec.EffectTiming.WhenWouldDigivolutionCardDiscarded).Count;
    AssertEqual(1, collected, "the window collects BT10_084's would-discard reactor over the real pre-trash hashtable");
}

async Task ControlNoReactorCollectsZero()
{
    (EngineContext ctx, _) = NewCtx(seed: 5102, turnPlayer: P2);
    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(ctx);

    HeadlessEntityId other = StageSyn(ctx, P1, "OTHER-DIGI", "1:battle:other");
    HeadlessEntityId othersrc = StageSyn(ctx, P1, "SRC-B", "1:src:othersrc", cardType: "Option", zone: ChoiceZone.None);
    SetSources(ctx, "1:battle:other", othersrc);

    Hashtable ht = BuildHashtable(ctx, other, P1, othersrc, P2);
    int collected = Script.AutoProcessing.GetSkillInfos(ht, Cec.EffectTiming.WhenWouldDigivolutionCardDiscarded).Count;
    AssertEqual(0, collected, "no reactor on field -> the window is a no-op (false-green guard)");
    await Task.CompletedTask;
}

async Task SelfSourceExcluded()
{
    (EngineContext ctx, _) = NewCtx(seed: 5103, turnPlayer: P2);
    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(ctx);

    await StageReal(ctx, P1, "BT10_084", "1:battle:bt084");
    HeadlessEntityId bt084src = StageSyn(ctx, P1, "SRC-A", "1:src:bt084src", cardType: "Option", zone: ChoiceZone.None);
    SetSources(ctx, "1:battle:bt084", bt084src);

    // The permanent being source-trashed IS BT10_084 itself -> PermanentCondition (permanent != self) is false.
    HeadlessEntityId self = new("1:battle:bt084");
    Hashtable ht = BuildHashtable(ctx, self, P1, bt084src, P2);
    int collected = Script.AutoProcessing.GetSkillInfos(ht, Cec.EffectTiming.WhenWouldDigivolutionCardDiscarded).Count;
    AssertEqual(0, collected, "BT10_084 does not react to a trash of its OWN sources (PermanentCondition self-exclusion)");
}

async Task DrivenFireReachesReactor()
{
    (EngineContext ctx, PolicyChoiceProvider policy) = NewCtx(seed: 5104, turnPlayer: P2);

    await StageReal(ctx, P1, "BT10_084", "1:battle:bt084");
    HeadlessEntityId bt084src = StageSyn(ctx, P1, "SRC-A", "1:src:bt084src", cardType: "Option", zone: ChoiceZone.None);
    SetSources(ctx, "1:battle:bt084", bt084src);

    HeadlessEntityId other = StageSyn(ctx, P1, "OTHER-DIGI", "1:battle:other");
    HeadlessEntityId othersrc = StageSyn(ctx, P1, "SRC-B", "1:src:othersrc", cardType: "Option", zone: ChoiceZone.None);
    SetSources(ctx, "1:battle:other", othersrc);

    // Accept BT10_084's optional "you may" prompt, then resolve its SelectCardEffect (Mode.Discard over its
    // own sources) by picking its own source.
    policy.On(req => req.Type == ChoiceType.OptionalEffect, req => ChoiceResult.Select(req.Candidates[0].Id), oneShot: false);
    policy.On(req => req.Candidates.Any(c => c.Id == bt084src), req => ChoiceResult.Select(bt084src), oneShot: false);
    policy.On(req => true, req => req.CanSkip ? ChoiceResult.Skip() : ChoiceResult.Select(req.Candidates.First(c => c.IsSelectable).Id), oneShot: false);

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(ctx);
    var otherPerm = new Cec.Permanent(ctx, other, P1);
    var srcView = new Cec.CardSource(ctx, othersrc, P1);
    var cause = BareCauseEffect.For(ctx, new HeadlessEntityId("2:battle:cause"));

    await new Script.ITrashDigivolutionCards(
        otherPerm, new List<Cec.CardSource> { srcView },
        causeEffectSourceId: new HeadlessEntityId("2:battle:cause"), cardEffect: cause)
        .TrashDigivolutionCards();

    AssertTrue(policy.Seen.Any(s => s.Contains("Select card(s) to trash.")),
        $"the wired PRE window drove BT10_084's reactor to its interactive SelectCardEffect pick [seen:{string.Join(" | ", policy.Seen)}]");
}

async Task DrivenControlNoReactor()
{
    (EngineContext ctx, PolicyChoiceProvider policy) = NewCtx(seed: 5105, turnPlayer: P2);

    HeadlessEntityId other = StageSyn(ctx, P1, "OTHER-DIGI", "1:battle:other");
    HeadlessEntityId othersrc = StageSyn(ctx, P1, "SRC-B", "1:src:othersrc", cardType: "Option", zone: ChoiceZone.None);
    SetSources(ctx, "1:battle:other", othersrc);
    policy.On(req => true, req => req.CanSkip ? ChoiceResult.Skip() : ChoiceResult.Select(req.Candidates.First(c => c.IsSelectable).Id), oneShot: false);

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(ctx);
    var otherPerm = new Cec.Permanent(ctx, other, P1);
    var srcView = new Cec.CardSource(ctx, othersrc, P1);
    var cause = BareCauseEffect.For(ctx, new HeadlessEntityId("2:battle:cause"));

    await new Script.ITrashDigivolutionCards(
        otherPerm, new List<Cec.CardSource> { srcView },
        causeEffectSourceId: new HeadlessEntityId("2:battle:cause"), cardEffect: cause)
        .TrashDigivolutionCards();

    AssertFalse(policy.Seen.Any(s => s.Contains("Select card(s) to trash.")),
        $"with no reactor on field the PRE window opens NO cut-in choice [seen:{string.Join(" | ", policy.Seen)}]");
}

// ───────────────────────────── harness ─────────────────────────────

Hashtable BuildHashtable(EngineContext ctx, HeadlessEntityId permId, HeadlessPlayerId owner, HeadlessEntityId srcId, HeadlessPlayerId causeOwner)
{
    var perm = new Cec.Permanent(ctx, permId, owner);
    var src = new Cec.CardSource(ctx, srcId, owner);
    var cause = BareCauseEffect.For(ctx, new HeadlessEntityId($"{causeOwner.Value}:battle:cause"));
    return Cec.CardEffectCommons.WhenDigivolutionCardWouldDiscardedCheckHashtable(
        perm, new List<Cec.CardSource> { src }, cause);
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

HeadlessEntityId StageSyn(EngineContext ctx, HeadlessPlayerId owner, string number, string instanceId,
    string cardType = "Digimon", ChoiceZone zone = ChoiceZone.BattleArea)
{
    var defId = new HeadlessEntityId($"DEF:{number}:{owner.Value}");
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 };
    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(defId, number, number, meta, CardType: cardType));
    var id = new HeadlessEntityId(instanceId);
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4, ["isSuspended"] = false }));
    if (zone != ChoiceZone.None)
        ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone)).GetAwaiter().GetResult();
    return id;
}

void SetSources(EngineContext ctx, string hostInstance, params HeadlessEntityId[] sourceIds)
{
    var hostId = new HeadlessEntityId(hostInstance);
    if (!ctx.CardInstanceRepository.TryGetInstance(hostId, out CardInstanceRecord? rec) || rec is null)
        throw new InvalidOperationException($"missing host {hostInstance}");
    var meta = new Dictionary<string, object?>(rec.Metadata, StringComparer.Ordinal)
    {
        [DigivolutionStackReader.SourceIdsKey] = sourceIds.Select(i => i.Value).ToArray(),
    };
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
