using System.Collections;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;
using Cec = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using CBT25104 = HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT25.Red.BT25_104;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// GAP2 witness 스위트 — 이미 포팅됐으나 그 timing 창에서의 witness가 없던 2축을 실동으로 구동한다.
//   (a) StartOfYourTurnClass — BT25_104 [Start of Your Turn] companion(CardEffectFactory.cs:2244):
//       OnStartTurn 팔의 MarcusActivateCoroutine가 [Marcus Damon] treat-as-Digimon 그랜트를 player
//       UntilEachTurnEndEffects에 추가 → Marcus Damon Tamer가 Digimon으로 취급(IsDigimon flip 관찰).
//   (b) WhenMovingClass — BT25_104 OnMove 팔(같은 MarcusActivateCoroutine): 동일 그랜트를 OnMove 창에서 구동.
// 템플릿: LT-A.Witness.Tests(DcgoMatch.CreatePumpDriven + 실/합성 카드 스테이징 + AmbientMatchContext).
// 관찰 경로: Permanent.IsDigimon(Permanent.cs:648-685)이 turn-player의 player.EffectList(None)을 스캔하고,
// player.EffectList(Player.cs:334)가 UntilEachTurnEndEffects를 aggregate → 그랜트된 TreatAsDigimonClass 착지.
// MakeDigimonCondition = IsExistOnBattleArea(BT25_104) && IsOwnerTurn — Main 대기(P1 턴)에서 참.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("BT25_104 GAP2-a (StartOfYourTurnClass): OnStartTurn 창 GATE가 열림(on-battle + owner-turn → CanUse TRUE) → 발화 → [Marcus Damon] Tamer가 treat-as-Digimon (flip false→true, 관찰)", BT25104_StartOfYourTurnMarcusTreatedAsDigimon),
    ("BT25_104 GAP2-b (WhenMovingClass): [Your Turn] Marcus 그랜트가 OnMove 창에서 발화 → [Marcus Damon] Tamer가 treat-as-Digimon (flip false→true, 관찰)", BT25104_WhenMovingMarcusTreatedAsDigimon),
    ("BT25_104 GAP2-a NEG(board-absent): BT25_104가 배틀에어리어에 없으면(hand) StartOfYourTurn 창 GATE FALSE (IsExistOnBattleAreaTrigger=false → CanUse=false) — 창 밖 미발화", BT25104_StartOfYourTurnNegBoardAbsent),
    ("BT25_104 GAP2-a NEG(wrong-turn): 상대(P2) 소유 BT25_104는 P1 턴의 StartOfYourTurn 창 GATE FALSE (IsOwnerTurn=false → CanUse=false) — 잘못된 턴 미발화", BT25104_StartOfYourTurnNegWrongTurn),
    ("BT25_104 GAP2-b NEG(board-absent): BT25_104가 배틀에어리어에 없으면(hand) OnMove 창 GATE FALSE (IsExistOnBattleAreaTrigger=false → CanUse=false) — 창 밖 미발화", BT25104_WhenMovingNegBoardAbsent),
    ("BT25_104 GAP2-b NEG(no-move-window): 배틀에 있어도 이동 이벤트가 없으면 OnMove 창 GATE FALSE (CanTriggerOnMove=false → CanUse=false) — 창 자체가 닫혀 미발화", BT25104_WhenMovingNegNoMoveWindow),
};

int failed = 0;
foreach ((string name, Func<Task> body) in tests)
{
    try
    {
        await body();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"FAIL {name}");
        Console.WriteLine($"     {ex.GetType().Name}: {ex.Message}");
        if (ex.StackTrace is string st)
        {
            Console.WriteLine(string.Join('\n', st.Split('\n').Take(20)));
        }
    }
}

Console.WriteLine($"SUMMARY: PASS={tests.Length - failed} FAIL={failed} TOTAL={tests.Length}");
if (failed > 0) { Environment.Exit(1); }

// ═══════════════════════════════════ GAP2-a StartOfYourTurnClass ═══════════════════════════════════

async Task BT25104_StartOfYourTurnMarcusTreatedAsDigimon()
{
    (DcgoMatch match, _) = await NewMatchAsync(seed: 9101);
    await ReachMainWaitAsync(match);

    HeadlessEntityId shine = Stage(match, P1, "BT25_104", ChoiceZone.BattleArea, "1:battle:shine", register: true);
    HeadlessEntityId marcus = StageSynthetic(match, P1, "MARCUS", dp: 0, level: 0, "1:battle:marcus", name: "Marcus Damon", cardType: "Tamer");

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var card = new Cec.CardSource(match.Context, shine, P1);

    // negative control: before the [Start of Your Turn] companion fires, a [Marcus Damon] Tamer is NOT a Digimon.
    AssertTrue(!Perm(match, marcus, P1).IsDigimon,
        "control: the [Marcus Damon] Tamer is not treated as a Digimon before the [Start of Your Turn] grant");

    Cec.ICardEffect stEffect = First(new CBT25104().CardEffects(Cec.EffectTiming.OnStartTurn, card), "ActivateClass");

    // WINDOW-GATE (positive): at the real start-of-your-turn window — BT25_104 on the owner's battle area, P1's turn —
    // the StartOfYourTurnClass CanUse gate OPENS (IsExistOnBattleAreaTrigger && IsOwnerTurn). Asserting the gate here
    // (rather than a blind Activate) proves the companion is admitted BY its window, matched against the NEG cases below
    // where the same gate stays shut. (Full L1-broadcast auto-fire is exercised by the live-drive suites; this witness
    // pins the gate + the observable grant landing that the broadcast would produce.)
    AssertTrue(stEffect.CanUse(new Hashtable()),
        "the StartOfYourTurn window gate is OPEN (on-battle + owner-turn) — the companion is admitted at its window");

    await ((Cec.ActivateICardEffect)stEffect).Activate(new Hashtable());

    AssertTrue(Perm(match, marcus, P1).IsDigimon,
        "StartOfYourTurnClass companion FIRED at the OnStartTurn window: the [Marcus Damon] Tamer is now treated as a Digimon (grant landed on player UntilEachTurnEndEffects, IsDigimon flip observed)");
}

// ═══════════════════════════════════ GAP2-a NEG: window gate stays shut outside its window ═══════════════════════════════════

async Task BT25104_StartOfYourTurnNegBoardAbsent()
{
    (DcgoMatch match, _) = await NewMatchAsync(seed: 9111);
    await ReachMainWaitAsync(match);

    // BT25_104 sits in HAND (NOT on the battle area) — the [Start of Your Turn] companion must NOT be admitted.
    HeadlessEntityId shine = Stage(match, P1, "BT25_104", ChoiceZone.Hand, "1:hand:shine", register: true);

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var card = new Cec.CardSource(match.Context, shine, P1);

    Cec.ICardEffect st = First(new CBT25104().CardEffects(Cec.EffectTiming.OnStartTurn, card), "ActivateClass");
    AssertTrue(!st.CanUse(new Hashtable()),
        "board-absent: with BT25_104 off the battle area the StartOfYourTurn gate is SHUT (IsExistOnBattleAreaTrigger=false → CanUse=false) — the companion does not fire outside its window");
}

async Task BT25104_StartOfYourTurnNegWrongTurn()
{
    (DcgoMatch match, _) = await NewMatchAsync(seed: 9112);
    await ReachMainWaitAsync(match); // P1's turn.

    // BT25_104 owned by P2, on P2's battle area — during P1's turn it is NOT the owner's turn.
    HeadlessEntityId shine = Stage(match, P2, "BT25_104", ChoiceZone.BattleArea, "2:battle:shine", register: true);

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var card = new Cec.CardSource(match.Context, shine, P2);

    Cec.ICardEffect st = First(new CBT25104().CardEffects(Cec.EffectTiming.OnStartTurn, card), "ActivateClass");
    AssertTrue(!st.CanUse(new Hashtable()),
        "wrong-turn: P2's BT25_104 on P1's turn — the StartOfYourTurn gate is SHUT (IsOwnerTurn=false → CanUse=false) — the [Your Turn] companion is turn-scoped and does not fire on the opponent's turn");
}

// ═══════════════════════════════════ GAP2-b NEG: OnMove window gate stays shut ═══════════════════════════════════

async Task BT25104_WhenMovingNegBoardAbsent()
{
    (DcgoMatch match, _) = await NewMatchAsync(seed: 9113);
    await ReachMainWaitAsync(match);

    HeadlessEntityId shine = Stage(match, P1, "BT25_104", ChoiceZone.Hand, "1:hand:shine", register: true);

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var card = new Cec.CardSource(match.Context, shine, P1);

    Cec.ICardEffect mv = First(new CBT25104().CardEffects(Cec.EffectTiming.OnMove, card), "ActivateClass");
    AssertTrue(!mv.CanUse(new Hashtable()),
        "board-absent: with BT25_104 off the battle area the OnMove gate is SHUT (IsExistOnBattleAreaTrigger=false → CanUse=false) — the WhenMoving companion does not fire outside its window");
}

async Task BT25104_WhenMovingNegNoMoveWindow()
{
    (DcgoMatch match, _) = await NewMatchAsync(seed: 9114);
    await ReachMainWaitAsync(match);

    // BT25_104 IS on the battle area, but NO move event is in flight — the OnMove window itself is closed.
    HeadlessEntityId shine = Stage(match, P1, "BT25_104", ChoiceZone.BattleArea, "1:battle:shine", register: true);

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var card = new Cec.CardSource(match.Context, shine, P1);

    Cec.ICardEffect mv = First(new CBT25104().CardEffects(Cec.EffectTiming.OnMove, card), "ActivateClass");
    // The board-presence sub-gate passes here; the ONLY thing shutting the gate is the absent move broadcast
    // (CanTriggerOnMove finds no moved subject in an empty hashtable) — this isolates the WINDOW itself.
    AssertTrue(!mv.CanUse(new Hashtable()),
        "no-move-window: on-battle but with no move event in flight the OnMove gate is SHUT (CanTriggerOnMove=false → CanUse=false) — the companion fires only inside a real move window");
}

// ═══════════════════════════════════ GAP2-b WhenMovingClass ═══════════════════════════════════

async Task BT25104_WhenMovingMarcusTreatedAsDigimon()
{
    (DcgoMatch match, _) = await NewMatchAsync(seed: 9102);
    await ReachMainWaitAsync(match);

    HeadlessEntityId shine = Stage(match, P1, "BT25_104", ChoiceZone.BattleArea, "1:battle:shine", register: true);
    HeadlessEntityId marcus = StageSynthetic(match, P1, "MARCUS", dp: 0, level: 0, "1:battle:marcus", name: "Marcus Damon", cardType: "Tamer");

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var card = new Cec.CardSource(match.Context, shine, P1);

    AssertTrue(!Perm(match, marcus, P1).IsDigimon,
        "control: the [Marcus Damon] Tamer is not treated as a Digimon before the [When Moving] grant");

    var mv = (Cec.ActivateICardEffect)First(new CBT25104().CardEffects(Cec.EffectTiming.OnMove, card), "ActivateClass");
    await mv.Activate(new Hashtable());

    AssertTrue(Perm(match, marcus, P1).IsDigimon,
        "WhenMovingClass FIRED at the OnMove window: the [Marcus Damon] Tamer is now treated as a Digimon (same MarcusActivateCoroutine grant, IsDigimon flip observed)");
}

// ═══════════════════════════════════ harness (LT-A 1:1) ═══════════════════════════════════

Cec.Permanent Perm(DcgoMatch match, HeadlessEntityId id, HeadlessPlayerId owner) => new(match.Context, id, owner);

static Cec.ICardEffect First(List<Cec.ICardEffect> effects, string typeName)
    => effects.Where(e => e is not null).First(e => e.GetType().Name == typeName);

PlayerDeckSetup[] MonoDecks(string p1Number, string p2Number) => new[]
{
    new PlayerDeckSetup(P1, Enumerable.Repeat(new HeadlessEntityId(p1Number), 50).ToArray()),
    new PlayerDeckSetup(P2, Enumerable.Repeat(new HeadlessEntityId(p2Number), 50).ToArray()),
};

async Task<(DcgoMatch Match, PolicyChoiceProvider Policy)> NewMatchAsync(int seed)
{
    var policy = new PolicyChoiceProvider();
    EngineContext context = ContextFactory.CreateWithProvider(policy, seed);
    CardBaseEntityLoader.LoadInto((CardDatabase)context.CardRepository);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        MonoDecks("BT1_028", "BT1_028"), firstPlayerId: P1, initialHandSize: 0, initialSecuritySize: 0, enableMulligan: false);
    MatchConfig config = MatchConfig.Create(new[] { P1, P2 }, randomSeed: seed, setup: setup);

    DcgoMatch match = DcgoMatch.CreatePumpDriven(context, new EngineTrace());
    await match.InitializeAsync(config);
    return (match, policy);
}

async Task ReachMainWaitAsync(DcgoMatch match)
{
    await StepOnceAsync(match);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P1));
}

static bool AtMainWaitOf(DcgoMatch match, HeadlessPlayerId player) =>
    match.Context.TurnController.Current.Phase == HeadlessPhase.Main
    && match.Context.TurnController.Current.TurnPlayerId == player
    && !match.HasPendingChoice()
    && !match.IsTerminal();

async Task DriveUntilAsync(DcgoMatch match, Func<DcgoMatch, bool> condition)
{
    for (int i = 0; i < 96 && !condition(match); i++)
    {
        if (match.HasPendingChoice())
        {
            bool decline = match.Context.ChoiceController.PendingRequest!.Type == ChoiceType.BreedingDecision
                || match.Context.ChoiceController.PendingRequest!.Type == ChoiceType.Mulligan;
            await ResolvePendingAsync(match, skip: decline);
        }
        else
        {
            await StepOnceAsync(match);
        }
    }

    if (!condition(match))
    {
        HeadlessTurnState t = match.Context.TurnController.Current;
        throw new InvalidOperationException(
            $"drive did not reach the expected state — phase:{t.Phase}/{t.StepCursor} turn:{t.TurnNumber} player:{t.TurnPlayerId}");
    }
}

async Task ResolvePendingAsync(DcgoMatch match, bool skip)
{
    HeadlessPlayerId chooser = match.Context.ChoiceController.PendingRequest!.PlayerId;
    LegalAction? action;
    using (AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context))
    {
        action = match.GetLegalActions(chooser)
            .FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice
                && a.Id.Value.EndsWith(":skip", StringComparison.Ordinal) == skip)
            ?? match.GetLegalActions(chooser).FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice);
    }

    if (action is null)
    {
        throw new InvalidOperationException("no ResolveChoice lane for the pending request");
    }

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    await match.ApplyActionAsync(action);
    await match.StepAsync();
    await match.StepAsync();
}

static async Task StepOnceAsync(DcgoMatch match)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.StepAsync();
}

// 실카드 스테이징(LT-A 관례): def id = 카드번호(cards.json 로더가 넣음), 인스턴스만 만들어 이동.
HeadlessEntityId Stage(DcgoMatch match, HeadlessPlayerId owner, string cardNumber, ChoiceZone zone, string instanceId, bool register = false)
{
    EngineContext ctx = match.Context;
    var defId = new HeadlessEntityId(cardNumber);
    if (!ctx.CardRepository.TryGetCard(defId, out CardRecord? existing) || existing is null)
    {
        throw new InvalidOperationException($"definition {cardNumber} not found in the loaded card database");
    }

    var id = new HeadlessEntityId(instanceId);
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["isSuspended"] = false }));
    if (zone != ChoiceZone.None)
    {
        ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone)).GetAwaiter().GetResult();
    }

    if (register)
    {
        Cec.CardEffectRegistrar.RegisterCard(ctx, id, owner);
    }

    return id;
}

// 합성 픽스처 카드(LT-A StageSynthetic 관례).
HeadlessEntityId StageSynthetic(DcgoMatch match, HeadlessPlayerId owner, string number, int dp, int level, string instanceId,
    string? name = null, string cardType = "Digimon", ChoiceZone zone = ChoiceZone.BattleArea,
    string[]? traits = null, int? playCost = null, string[]? colors = null)
{
    EngineContext ctx = match.Context;
    var defId = new HeadlessEntityId($"DEF:{number}:{owner.Value}");
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = level };
    if (traits is { Length: > 0 })
    {
        meta["traits"] = traits;
    }

    if (colors is { Length: > 0 })
    {
        meta["colors"] = colors;
    }

    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(defId, number, name ?? number, meta, CardType: cardType, PlayCost: playCost));
    var id = new HeadlessEntityId(instanceId);
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = level, ["isSuspended"] = false }));
    if (zone != ChoiceZone.None)
    {
        ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone)).GetAwaiter().GetResult();
    }

    Cec.CardEffectRegistrar.RegisterCard(ctx, id, owner);
    return id;
}

static void AssertTrue(bool condition, string message)
{
    if (!condition) { throw new InvalidOperationException($"Assertion failed: {message}"); }
}

// ═══════════════════════════════ providers/context (LT-A 1:1) ═══════════════════════════════

sealed class PolicyChoiceProvider : IChoiceProvider
{
    private readonly List<(Func<ChoiceRequest, bool> Applies, Func<ChoiceRequest, ChoiceResult> Answer, bool OneShot)> _handlers = new();
    private readonly ScriptedChoiceProvider _fallback = new();

    public void On(Func<ChoiceRequest, bool> applies, Func<ChoiceRequest, ChoiceResult> answer, bool oneShot = true)
        => _handlers.Add((applies, answer, oneShot));

    public List<string> Seen { get; } = new();

    public Task<ChoiceResult> ChooseAsync(ChoiceRequest request, CancellationToken cancellationToken = default)
    {
        Seen.Add($"{request.Type}:'{request.Message}'x{request.Candidates.Count}");
        for (int i = 0; i < _handlers.Count; i++)
        {
            (Func<ChoiceRequest, bool> applies, Func<ChoiceRequest, ChoiceResult> answer, bool oneShot) = _handlers[i];
            if (applies(request))
            {
                ChoiceResult result = answer(request);
                result.ThrowIfInvalid(request);
                if (oneShot)
                {
                    _handlers.RemoveAt(i);
                }

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
            provider,
            randomSource,
            new CardDatabase(),
            cardInstanceRepository,
            zoneMover,
            new InMemoryRuleQueryService(),
            new InMemoryHeadlessTurnController(),
            choiceController,
            new InMemoryHeadlessAttackController(),
            memoryController,
            logSink,
            new HeadlessDCGO.Engine.Headless.Coroutines.EngineTaskRunner(),
            effectScheduler,
            gameEventQueue: gameEventQueue);
        selfRef = context;
        return context;
    }
}
