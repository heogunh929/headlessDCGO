using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;
using Cec = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using Cfx = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// W1-MinMaxCostDp witness — behavioural coverage for the four cards re-ported 1:1 against the freshly-relocated
// MinMax_DP_Cost_Level predicates (Cost/IsMaxCost, Cost/IsMinCost, DP/IsMaxDP, DP/IsMinDP). Each subtest drives
// the card's effect END-TO-END at the exact point the MinMax predicate gates, and pins that a control candidate
// on the OTHER extreme is excluded:
//   BT6_106 [Main]  IsMaxCost — DestroyPermanentsClass deletes ONLY the opponent's highest-cost Digimon.
//   BT6_067 [WhenD] IsMinCost — DestroyPermanentsClass deletes ONLY the opponent's lowest-cost Digimon.
//   BT6_095 [Main]  IsMinDP   — DestroyPermanentsClass deletes ONLY the opponent's lowest-DP Digimon.
//   BT2_112 [WhenA] IsMaxDP   — the gate distinguishes attacking the highest-DP defender (true) from a lower-DP
//                               one (false, control excluded); on the max case the effect unsuspends this Digimon.
// 표준 템플릿 = PILOT-S6.Witness.Tests: 효과 인스턴스를 직접 만들어 ActivateClass를 꺼내 Activate 구동
// (트리거 게이트 CanTrigger*는 실 디스패치 밖에서 구성 불가 → 보드-상태 게이트/predicate만 직접 실증).
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("BT6_106 W1: [Main] IsMaxCost — 상대 최고코스트 디지몬만 삭제, 저코스트 control 생존", BT6106_MaxCostDeletesOnlyHighest),
    ("BT6_067 W1: [When Digivolving] IsMinCost — 상대 최저코스트 디지몬만 삭제, 고코스트 control 생존", BT6067_MinCostDeletesOnlyLowest),
    ("BT6_095 W1: [Main] IsMinDP — 상대 최저DP 디지몬만 삭제, 고DP control 생존", BT6095_MinDpDeletesOnlyLowest),
    ("BT2_112 W1: [When Attacking] IsMaxDP — 최고DP 방어자 공격 시 게이트 true(+언서스펜드), 비-최고 시 false(control 제외)", BT2112_MaxDpUnsuspendsOnAttackingHighest),
    ("AD1_013 W1: [On Play] IsMinDigivolutionCards — predicate gates on the opponent's FEWEST-source Digimon (min true / higher-stack false control); SelectPermanent(Destroy) deletes ONLY the min, the deeper-stack control survives", AD1013_MinDigivolutionCardsGatesDelete),
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
            Console.WriteLine(string.Join('\n', st.Split('\n').Take(12)));
        }
    }
}

Console.WriteLine($"SUMMARY: PASS={tests.Length - failed} FAIL={failed} TOTAL={tests.Length}");
if (failed > 0) { Environment.Exit(1); }

// ═══════════════════════════════════ BT6_106 (IsMaxCost) ═══════════════════════════════════

async Task BT6106_MaxCostDeletesOnlyHighest()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewMatchAsync(seed: 71061);
    await ReachMainWaitAsync(match);

    HeadlessEntityId host = StageSynthetic(match, P1, "W1-106-HOST", dp: 4000, level: 5, "1:battle:106host", playCost: 5);
    HeadlessEntityId highCost = StageSynthetic(match, P2, "W1-106-HIGH", dp: 3000, level: 6, "2:battle:106hi", playCost: 6);
    HeadlessEntityId lowCost = StageSynthetic(match, P2, "W1-106-LOW", dp: 3000, level: 3, "2:battle:106lo", playCost: 2);

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var card = new Cec.CardSource(match.Context, host, P1);
    var effect = new HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT6.Black.BT6_106();
    List<Cec.ICardEffect> opEffects = effect.CardEffects(Cec.EffectTiming.OptionSkill, card);
    var main = (Cfx.ActivateClass)opEffects.First();

    await main.Activate(new System.Collections.Hashtable());

    AssertTrue(!ZoneCards(match, P2, ChoiceZone.BattleArea).Contains(highCost),
        "[Main] IsMaxCost: the opponent's HIGHEST-cost (6) Digimon was deleted");
    AssertTrue(ZoneCards(match, P2, ChoiceZone.BattleArea).Contains(lowCost),
        "the opponent's lower-cost (2) Digimon was NOT deleted (IsMaxCost scoping verified)");
}

// ═══════════════════════════════════ BT6_067 (IsMinCost) ═══════════════════════════════════

async Task BT6067_MinCostDeletesOnlyLowest()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewMatchAsync(seed: 70671);
    await ReachMainWaitAsync(match);

    HeadlessEntityId host = StageSynthetic(match, P1, "W1-067-HOST", dp: 4000, level: 5, "1:battle:067host", playCost: 5);
    HeadlessEntityId lowCost = StageSynthetic(match, P2, "W1-067-LOW", dp: 3000, level: 3, "2:battle:067lo", playCost: 2);
    HeadlessEntityId highCost = StageSynthetic(match, P2, "W1-067-HIGH", dp: 3000, level: 6, "2:battle:067hi", playCost: 6);

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var card = new Cec.CardSource(match.Context, host, P1);
    var effect = new HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT6.Black.BT6_067();
    List<Cec.ICardEffect> wdEffects = effect.CardEffects(Cec.EffectTiming.OnEnterFieldAnyone, card);
    var wd = (Cfx.ActivateClass)wdEffects.First();

    AssertTrue(wd.CanActivate(new System.Collections.Hashtable()),
        "CanActivate is true — this Digimon is on the battle area and a lowest-cost opponent Digimon matches");

    await wd.Activate(new System.Collections.Hashtable());

    AssertTrue(!ZoneCards(match, P2, ChoiceZone.BattleArea).Contains(lowCost),
        "[When Digivolving] IsMinCost: the opponent's LOWEST-cost (2) Digimon was deleted");
    AssertTrue(ZoneCards(match, P2, ChoiceZone.BattleArea).Contains(highCost),
        "the opponent's higher-cost (6) Digimon was NOT deleted (IsMinCost scoping verified)");
}

// ═══════════════════════════════════ BT6_095 (IsMinDP) ═══════════════════════════════════

async Task BT6095_MinDpDeletesOnlyLowest()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewMatchAsync(seed: 70951);
    await ReachMainWaitAsync(match);

    HeadlessEntityId host = StageSynthetic(match, P1, "W1-095-HOST", dp: 4000, level: 5, "1:battle:095host");
    HeadlessEntityId lowDp = StageSynthetic(match, P2, "W1-095-LOW", dp: 2000, level: 3, "2:battle:095lo");
    HeadlessEntityId highDp = StageSynthetic(match, P2, "W1-095-HIGH", dp: 9000, level: 6, "2:battle:095hi");

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var card = new Cec.CardSource(match.Context, host, P1);
    var effect = new HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT6.Red.BT6_095();
    List<Cec.ICardEffect> opEffects = effect.CardEffects(Cec.EffectTiming.OptionSkill, card);
    var main = (Cfx.ActivateClass)opEffects.First();

    await main.Activate(new System.Collections.Hashtable());

    AssertTrue(!ZoneCards(match, P2, ChoiceZone.BattleArea).Contains(lowDp),
        "[Main] IsMinDP: the opponent's LOWEST-DP (2000) Digimon was deleted");
    AssertTrue(ZoneCards(match, P2, ChoiceZone.BattleArea).Contains(highDp),
        "the opponent's higher-DP (9000) Digimon was NOT deleted (IsMinDP scoping verified)");
}

// ═══════════════════════════════════ BT2_112 (IsMaxDP) ═══════════════════════════════════

async Task BT2112_MaxDpUnsuspendsOnAttackingHighest()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewMatchAsync(seed: 21121);
    await ReachMainWaitAsync(match);

    HeadlessEntityId host = StageSynthetic(match, P1, "W1-112-HOST", dp: 6000, level: 5, "1:battle:112host");
    HeadlessEntityId highDp = StageSynthetic(match, P2, "W1-112-HIGH", dp: 10000, level: 6, "2:battle:112hi");
    HeadlessEntityId lowDp = StageSynthetic(match, P2, "W1-112-LOW", dp: 3000, level: 3, "2:battle:112lo");
    SetSuspended(match, host, true); // this Digimon is suspended (it is the attacker); the effect will unsuspend it.

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var card = new Cec.CardSource(match.Context, host, P1);
    var effect = new HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Black.BT2_112();
    List<Cec.ICardEffect> attackEffects = effect.CardEffects(Cec.EffectTiming.OnAllyAttack, card);
    var ess = (Cfx.ActivateClass)attackEffects.First();

    // Attack the opponent's HIGHEST-DP Digimon → the exact IsMaxDP call the card's CanUse gate makes is true.
    match.Context.AttackController.DeclareAttack(P1, host, P2, targetId: highDp);
    AssertTrue(
        Cec.CardEffectCommons.IsMaxDP(Cec.GManager.instance.attackProcess.DefendingPermanent, Cec.CardEffectCommons.OpponentOf(card), null),
        "[When Attacking] IsMaxDP gate: attacking the opponent's highest-DP (10000) defender → predicate TRUE");

    // Control: redirect to a lower-DP defender → the same gate is FALSE (the effect would not trigger).
    match.Context.AttackController.RetargetDefender(lowDp, "witness control");
    AssertTrue(
        !Cec.CardEffectCommons.IsMaxDP(Cec.GManager.instance.attackProcess.DefendingPermanent, Cec.CardEffectCommons.OpponentOf(card), null),
        "IsMaxDP gate: attacking the lower-DP (3000) defender → predicate FALSE (control excluded)");

    // On the max case, drive the effect body and confirm this Digimon becomes unsuspended.
    match.Context.AttackController.RetargetDefender(highDp, "witness restore");
    AssertTrue(IsSuspended(match, host), "this Digimon starts suspended (it is the attacker)");
    AssertTrue(ess.CanActivate(new System.Collections.Hashtable()),
        "CanActivate is true — this Digimon is on the battle area and can unsuspend");

    await ess.Activate(new System.Collections.Hashtable());

    AssertTrue(!IsSuspended(match, host),
        "[When Attacking] IsMaxDP: after attacking the highest-DP defender, this Digimon was unsuspended");
}

// ═══════════════════════════════════ AD1_013 (IsMinDigivolutionCards) ═══════════════════════════════════

async Task AD1013_MinDigivolutionCardsGatesDelete()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewMatchAsync(seed: 10131);
    await ReachMainWaitAsync(match);

    HeadlessEntityId host = StageSynthetic(match, P1, "AD1-013-HOST", dp: 6000, level: 6, "1:battle:013host");
    // Opponent Digimon: minPerm has ZERO digivolution cards, deepPerm has ONE (a deeper stack) → minPerm is the
    // UNIQUE fewest-source opponent Digimon (the metric IsMinDigivolutionCards ranks).
    HeadlessEntityId minPerm = StageSynthetic(match, P2, "AD1-013-MIN", dp: 3000, level: 4, "2:battle:013min");
    HeadlessEntityId deepPerm = StageSynthetic(match, P2, "AD1-013-DEEP", dp: 3000, level: 4, "2:battle:013deep");
    HeadlessEntityId under = StageSynthetic(match, P2, "AD1-013-UNDER", dp: 1000, level: 2, "2:under:013u", zone: ChoiceZone.None);
    AttachSource(match, deepPerm, under);

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);

    // (a) predicate subtest (+control) — the card-facing IsMinDigivolutionCards the card's canTargetCondition gates on.
    HeadlessPlayerId enemy = Cec.CardEffectCommons.OpponentOf(new Cec.CardSource(match.Context, host, P1));
    var minView = new Cec.Permanent(match.Context, minPerm, P2);
    var deepView = new Cec.Permanent(match.Context, deepPerm, P2);
    AssertTrue(Cec.CardEffectCommons.IsMinDigivolutionCards(minView, enemy),
        "IsMinDigivolutionCards: the opponent's ZERO-source Digimon is the fewest → TRUE");
    AssertTrue(!Cec.CardEffectCommons.IsMinDigivolutionCards(deepView, enemy),
        "control: the opponent's deeper (1-source) Digimon is NOT the fewest → FALSE");

    // (b) behaviour — drive AD1_013 [On Play] end-to-end; the SelectPermanent(Destroy) gate admits ONLY the min.
    var card = new Cec.CardSource(match.Context, host, P1);
    var effect = new HeadlessDCGO.Engine.Assets.Scripts.CardEffect.AD1.Blue.AD1_013();
    List<Cec.ICardEffect> onPlayEffects = effect.CardEffects(Cec.EffectTiming.OnEnterFieldAnyone, card);
    var onPlay = (Cfx.ActivateClass)onPlayEffects.First(); // [On Play] (the first OnEnterFieldAnyone arm; [When Digivolving] shares the body)

    policy.On(_ => true, req =>
    {
        ChoiceCandidate? sel = req.Candidates.FirstOrDefault(c => c.IsSelectable);
        return sel is not null
            ? ChoiceResult.Select(sel.Id)
            : (req.CanSkip ? ChoiceResult.Skip() : ChoiceResult.Select(req.Candidates[0].Id));
    }, oneShot: false);

    await onPlay.Activate(new System.Collections.Hashtable());

    AssertTrue(!ZoneCards(match, P2, ChoiceZone.BattleArea).Contains(minPerm),
        "[On Play] IsMinDigivolutionCards: the opponent's fewest-source (0) Digimon was deleted");
    AssertTrue(ZoneCards(match, P2, ChoiceZone.BattleArea).Contains(deepPerm),
        "the opponent's deeper-stack (1-source) Digimon was NOT deleted (IsMinDigivolutionCards scoping verified)");
}

// ═══════════════════════════════════ harness (PILOT-S6 template) ═══════════════════════════════════

async Task<(DcgoMatch Match, PolicyChoiceProvider Policy)> NewMatchAsync(int seed)
{
    var policy = new PolicyChoiceProvider();
    EngineContext context = ContextFactory.CreateWithProvider(policy, seed);
    CardBaseEntityLoader.LoadInto((CardDatabase)context.CardRepository);
    PlayerDeckSetup[] decks =
    {
        new PlayerDeckSetup(P1, Enumerable.Repeat(new HeadlessEntityId("BT1_028"), 50).ToArray()),
        new PlayerDeckSetup(P2, Enumerable.Repeat(new HeadlessEntityId("BT1_028"), 50).ToArray()),
    };
    MatchSetupConfig setup = MatchSetupConfig.Create(
        decks, firstPlayerId: P1, initialHandSize: 0, initialSecuritySize: 0, enableMulligan: false);
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
            $"drive did not reach the expected state — phase:{t.Phase}/{t.StepCursor} turn:{t.TurnNumber} " +
            $"player:{t.TurnPlayerId} choice:{match.Context.ChoiceController.PendingRequest?.Type.ToString() ?? "<none>"}");
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

    await ApplyAsync(match, action);
}

async Task ApplyAsync(DcgoMatch match, LegalAction action)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.ApplyActionAsync(action);
    await match.StepAsync();
    await match.StepAsync();
}

static async Task StepOnceAsync(DcgoMatch match)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.StepAsync();
}

// 합성 픽스처 카드(PILOT-S6 StageSynthetic 관례): def 업서트 + 인스턴스 + 존 이동. 합성 번호는 실 효과에
// 매핑되지 않으므로 RegisterCard는 no-op(false) — 호스트/상대 모두 보드 픽스처로만 사용, 효과는 직접 구동.
HeadlessEntityId StageSynthetic(DcgoMatch match, HeadlessPlayerId owner, string number, int dp, int level, string instanceId,
    string cardType = "Digimon", ChoiceZone zone = ChoiceZone.BattleArea, int? playCost = null)
{
    EngineContext ctx = match.Context;
    var defId = new HeadlessEntityId($"DEF:{number}:{owner.Value}");
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = level };
    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(defId, number, number, meta, CardType: cardType, PlayCost: playCost));
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

// Attach `under` as a digivolution card beneath the permanent topped by `top` (the canonical `sourceIds`
// metadata shape DigivolutionStackReader reads — EXEMPLAR-T3B idiom). `under` must not itself occupy a field zone.
void AttachSource(DcgoMatch match, HeadlessEntityId top, HeadlessEntityId under)
{
    EngineContext ctx = match.Context;
    if (!ctx.CardInstanceRepository.TryGetInstance(top, out CardInstanceRecord? rec) || rec is null)
    {
        throw new InvalidOperationException($"missing top instance {top.Value}");
    }

    List<string> ids = rec.Metadata.TryGetValue(DigivolutionStackReader.SourceIdsKey, out object? raw) && raw is string[] arr
        ? arr.ToList()
        : new List<string>();
    ids.Add(under.Value);
    var meta = new Dictionary<string, object?>(rec.Metadata, StringComparer.Ordinal) { [DigivolutionStackReader.SourceIdsKey] = ids.ToArray() };
    ctx.CardInstanceRepository.Upsert(rec with { Metadata = meta });
}

static void SetSuspended(DcgoMatch match, HeadlessEntityId id, bool suspended)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? record) || record is null)
    {
        throw new InvalidOperationException($"missing instance {id.Value}");
    }

    var meta = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal) { ["isSuspended"] = suspended };
    match.Context.CardInstanceRepository.Upsert(record with { Metadata = meta });
}

static bool IsSuspended(DcgoMatch match, HeadlessEntityId id)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? record) || record is null)
    {
        throw new InvalidOperationException($"missing instance {id.Value}");
    }

    return record.Metadata.TryGetValue("isSuspended", out object? v) && v is bool b && b;
}

static IReadOnlyList<HeadlessEntityId> ZoneCards(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone)
{
    return match.Context.ZoneMover is IZoneStateReader zones
        ? zones.GetCards(player, zone)
        : Array.Empty<HeadlessEntityId>();
}

static void AssertTrue(bool condition, string message)
{
    if (!condition) { throw new InvalidOperationException($"Assertion failed: {message}"); }
}

// ═══════════════════════════════ providers/context (PILOT-S6 template) ═══════════════════════════════

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
