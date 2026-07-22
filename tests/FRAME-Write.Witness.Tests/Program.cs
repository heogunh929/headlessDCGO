using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;
using System.Collections;
using Cec = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using Scr = HeadlessDCGO.Engine.Assets.Scripts.Script;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// FRAME-Write — behavior witnesses for the frame/SLOT WRITE side (RD-P6C1-1/-2/-6/-8): SetBurst/BurstTamer
// frame resolution, jogress evo-root target resolution, the jogress collapse WRITE, the burst play-flow
// (BounceTamer + !TamerBounced retry) + turn-end trash loop, and the 1:1-ported capacity checks
// CanJogressFromTargetPermanents / CanBurstDigivolutionFromTargetPermanent.
// Harness = W3-SpecialWitness 정본 (DcgoMatch.CreatePumpDriven + PolicyChoiceProvider). Cards: BT16_025 (real
// jogress condition = Blue Lv4 + Green Lv4) and BT25_104 (real burst condition = Marcus Damon tamer +
// ShineGreymon digimon).
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("CanJogressFromTargetPermanents: BT16_025 + [Blue Lv4, Green Lv4] field pair → true; wrong count / wrong-colour pair → false", JogressCapacityCheck),
    ("CanBurstDigivolutionFromTargetPermanent: BT25_104 + ShineGreymon(target) + Marcus Damon(tamer) → true; no-tamer / wrong-digimon → false", BurstCapacityCheck),
    ("Jogress WRITE (PlayCardClass end-to-end): BT16_025 SetJogress([blue,green]) collapses 2 roots → 1 permanent, roots stacked underneath, roots gone from field", JogressCollapseWrite),
    ("Burst WRITE (PlayCardClass end-to-end): BT25_104 SetBurst(marcusFrame) → BounceTamer(Marcus) → burst-digivolve over ShineGreymon → IsBurstDigivolved + turn-end trash effect registered", BurstWriteEndToEnd),
    ("Burst turn-end trash loop: AddTrashTopCardAtTurnEnd registers an OnEndTurn ace-overflow+trash effect on the burst permanent", BurstTurnEndTrashLoop),
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

// ═══════════════════════════════════ Test 1: jogress capacity ═══════════════════════════════════

async Task JogressCapacityCheck()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewMatchAsync(seed: 7101);
    await ReachMainWaitAsync(match);

    HeadlessEntityId bt16 = Stage(match, P1, "BT16_025", ChoiceZone.BattleArea, "1:hand:bt16", zone: ChoiceZone.Hand, register: true);
    HeadlessEntityId blue = StageSynthetic(match, P1, "BLUE4", dp: 4000, level: 4, "1:battle:blue", name: "BlueMon", colors: new[] { "Blue" });
    HeadlessEntityId green = StageSynthetic(match, P1, "GREEN4", dp: 4000, level: 4, "1:battle:green", name: "GreenMon", colors: new[] { "Green" });
    HeadlessEntityId blue2 = StageSynthetic(match, P1, "BLUE4B", dp: 4000, level: 4, "1:battle:blue2", name: "BlueMon2", colors: new[] { "Blue" });

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var card = new Cec.CardSource(match.Context, bt16, P1);
    var pBlue = new Cec.Permanent(match.Context, blue, P1);
    var pGreen = new Cec.Permanent(match.Context, green, P1);
    var pBlue2 = new Cec.Permanent(match.Context, blue2, P1);

    AssertTrue(card.CanJogressFromTargetPermanents(new List<Cec.Permanent> { pBlue, pGreen }, false),
        "the Blue-Lv4 + Green-Lv4 field pair satisfies BT16_025's jogress condition (CanPlayJogress + ordered EvoRootCondition + !CanNotEvolve)");
    AssertTrue(!card.CanJogressFromTargetPermanents(new List<Cec.Permanent> { pBlue }, false),
        "negative: a single-target list fails the Count==2 gate");
    AssertTrue(!card.CanJogressFromTargetPermanents(new List<Cec.Permanent> { pBlue, pBlue2 }, false),
        "negative: a Blue+Blue pair fails the per-slot element check (neither ordering matches Blue-then-Green)");
}

// ═══════════════════════════════════ Test 2: burst capacity ═══════════════════════════════════

async Task BurstCapacityCheck()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewMatchAsync(seed: 7201);
    await ReachMainWaitAsync(match);

    HeadlessEntityId bt25 = Stage(match, P1, "BT25_104", ChoiceZone.BattleArea, "1:hand:bt25", zone: ChoiceZone.Hand, register: true);
    HeadlessEntityId shine = StageSynthetic(match, P1, "SHINE", dp: 12000, level: 6, "1:battle:shine", name: "ShineGreymon");
    HeadlessEntityId marcus = StageSynthetic(match, P1, "MARCUS", dp: 3000, level: 3, "1:battle:marcus", name: "Marcus Damon", cardType: "Tamer");
    HeadlessEntityId agumon = StageSynthetic(match, P1, "AGU", dp: 3000, level: 3, "1:battle:agu", name: "Agumon");

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var card = new Cec.CardSource(match.Context, bt25, P1);
    var pShine = new Cec.Permanent(match.Context, shine, P1);
    var pAgu = new Cec.Permanent(match.Context, agumon, P1);

    AssertTrue(card.CanBurstDigivolutionFromTargetPermanent(pShine, false),
        "ShineGreymon (target) with a Marcus Damon tamer on the battle area satisfies BT25_104's burst condition");
    AssertTrue(!card.CanBurstDigivolutionFromTargetPermanent(pAgu, false),
        "negative: Agumon is not [ShineGreymon] — digimonCondition fails");

    // no-tamer negative: a fresh match with ShineGreymon but no Marcus.
    (DcgoMatch match2, PolicyChoiceProvider _2) = await NewMatchAsync(seed: 7202);
    await ReachMainWaitAsync(match2);
    HeadlessEntityId bt25b = Stage(match2, P1, "BT25_104", ChoiceZone.BattleArea, "1:hand:bt25", zone: ChoiceZone.Hand, register: true);
    HeadlessEntityId shineB = StageSynthetic(match2, P1, "SHINE", dp: 12000, level: 6, "1:battle:shine", name: "ShineGreymon");
    using AmbientMatchContext.Scope _s2 = AmbientMatchContext.Enter(match2.Context);
    var cardB = new Cec.CardSource(match2.Context, bt25b, P1);
    var pShineB = new Cec.Permanent(match2.Context, shineB, P1);
    AssertTrue(!cardB.CanBurstDigivolutionFromTargetPermanent(pShineB, false),
        "negative: no Marcus Damon tamer on the battle area — CanPlayBurst false (no distinct qualifying tamer)");
}

// ═══════════════════════════════════ Test 3: jogress collapse WRITE ═══════════════════════════════════

async Task JogressCollapseWrite()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewMatchAsync(seed: 7301);
    await ReachMainWaitAsync(match);

    HeadlessEntityId bt16 = Stage(match, P1, "BT16_025", ChoiceZone.BattleArea, "1:hand:bt16", zone: ChoiceZone.Hand, register: true);
    HeadlessEntityId blue = StageSynthetic(match, P1, "BLUE4", dp: 4000, level: 4, "1:battle:blue", name: "BlueMon", colors: new[] { "Blue" });
    HeadlessEntityId green = StageSynthetic(match, P1, "GREEN4", dp: 4000, level: 4, "1:battle:green", name: "GreenMon", colors: new[] { "Green" });

    int blueIdx, greenIdx;
    using (AmbientMatchContext.Scope _p = AmbientMatchContext.Enter(match.Context))
    {
        blueIdx = new Cec.Permanent(match.Context, blue, P1).PermanentFrame!.FrameID;
        greenIdx = new Cec.Permanent(match.Context, green, P1).PermanentFrame!.FrameID;
    }

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);

    var card = new Cec.CardSource(match.Context, bt16, P1);
    var play = new Cec.PlayCardClass(
        cardSources: new List<Cec.CardSource> { card },
        hashtable: new Hashtable(),
        payCost: false,
        targetPermanent: null,
        isTapped: false,
        root: Scr.SelectCardEffect.Root.Hand,
        activateETB: true);
    play.SetJogress(new[] { blueIdx, greenIdx });

    // The frame-WRITE jogress path drives: jogress target resolution (PlayCardClass:3093 — resolves the two roots
    // from the compacted field list at [blueIdx, greenIdx]), the CanJogressFromTargetPermanents gate, and the
    // hand-off into PlayPermanentClass's jogress arm, which calls DiscardEvoRoots(putToTrash:false) on the TWO
    // RESOLVED ROOTS (bare-detach to None) then re-parents them under the new BT16_025 permanent.
    // (MIG4-DISCARDEVOROOTS-PUTTOTRASH LANDED) that bare-detach leaf — the former STOP — is now a live 1:1 port,
    // so the collapse runs END-TO-END. This assert was extended from "reaches the MIG4 STOP" to the full collapse.
    await play.PlayCard();

    List<Cec.Permanent> field = new Cec.Player(match.Context, P1).GetFieldPermanents();
    AssertTrue(field.All(p => p.InstanceId != blue && p.InstanceId != green),
        "both jogress roots (Blue Lv4, Green Lv4) are gone from the field as standalone permanents (bare-detached)");
    Cec.Permanent? result = field.FirstOrDefault(p => p.InstanceId == bt16);
    AssertTrue(result is not null, "BT16_025 is the surviving jogress permanent (2 roots collapsed → 1 permanent)");
    IReadOnlyList<Cec.CardSource> sources = result!.DigivolutionCards;
    AssertTrue(sources.Any(s => s.InstanceId == blue) && sources.Any(s => s.InstanceId == green),
        "both roots are stacked underneath BT16_025 as its digivolution sources (full collapse WRITE)");
}

// ═══════════════════════════════════ Test 4: burst WRITE end-to-end ═══════════════════════════════════

async Task BurstWriteEndToEnd()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewMatchAsync(seed: 7401);
    await ReachMainWaitAsync(match);

    HeadlessEntityId bt25 = Stage(match, P1, "BT25_104", ChoiceZone.BattleArea, "1:hand:bt25", zone: ChoiceZone.Hand, register: true);
    // ShineGreymon carries DATA SQUAD + level 6 so BT25_104's AddSelfDigivolutionRequirementStaticEffect (DATA
    // SQUAD, level 6) admits it — after the tamer bounce empties the burst frame, the endPlayCard evolution gate is
    // the normal-evolution CanEvolve arm (AS-IS: IsBurst goes false once the bounced tamer's frame is vacated).
    HeadlessEntityId shine = StageSynthetic(match, P1, "SHINE", dp: 12000, level: 6, "1:battle:shine", name: "ShineGreymon", traits: new[] { "DATA SQUAD" });
    HeadlessEntityId marcus = StageSynthetic(match, P1, "MARCUS", dp: 3000, level: 3, "1:battle:marcus", name: "Marcus Damon", cardType: "Tamer");

    int marcusIdx;
    using (AmbientMatchContext.Scope _p = AmbientMatchContext.Enter(match.Context))
    {
        marcusIdx = new Cec.Permanent(match.Context, marcus, P1).PermanentFrame!.FrameID;
    }

    // The burst "Normal/Burst" method panel + tamer bounce raise ModeChoice/Permanent prompts — accept burst.
    policy.On(req => true, req => req.CanSkip ? ChoiceResult.Skip() : (req.Candidates.Count > 0 ? ChoiceResult.Select(req.Candidates[0].Id) : ChoiceResult.Skip()), oneShot: false);

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);

    var card = new Cec.CardSource(match.Context, bt25, P1);
    var pShine = new Cec.Permanent(match.Context, shine, P1);
    var play = new Cec.PlayCardClass(
        cardSources: new List<Cec.CardSource> { card },
        hashtable: new Hashtable(),
        payCost: false,
        targetPermanent: pShine,
        isTapped: false,
        root: Scr.SelectCardEffect.Root.Hand,
        activateETB: true);
    play.SetBurst(marcusIdx, card);

    await play.PlayCard();

    // Marcus was bounced back to hand (burst tamer bounce), ShineGreymon collapsed into BT25_104 (Burst Mode).
    List<Cec.Permanent> field = new Cec.Player(match.Context, P1).GetFieldPermanents();
    AssertTrue(field.All(p => p.InstanceId != marcus),
        "the Marcus Damon tamer was bounced off the battle area (BounceTamer → TamerBounced)");
    Cec.Permanent? result = field.FirstOrDefault(p => p.InstanceId == bt25);
    AssertTrue(result is not null, "BT25_104 (ShineGreymon: Burst Mode) is the surviving burst permanent over ShineGreymon");
    AssertTrue(result!.IsBurstDigivolved, "the burst permanent is flagged IsBurstDigivolved");
    Cec.ICardEffect? trash = result.EffectList(Cec.EffectTiming.OnEndTurn)
        .FirstOrDefault(e => (e.EffectName ?? string.Empty).Contains("Trash", StringComparison.Ordinal) || (e.EffectDiscription ?? string.Empty).Contains("burst digivolution turn", StringComparison.OrdinalIgnoreCase));
    AssertTrue(trash is not null, "the turn-end trash effect (AddTrashTopCardAtTurnEnd) is registered on the burst permanent's OnEndTurn window");
}

// ═══════════════════════════════════ Test 5: turn-end trash loop (isolated) ═══════════════════════════════════

async Task BurstTurnEndTrashLoop()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewMatchAsync(seed: 7501);
    await ReachMainWaitAsync(match);

    // A permanent with a digivolution stack so the OnEndTurn effect's CanActivate (DigivolutionCards.Count>=1) holds.
    HeadlessEntityId top = StageSynthetic(match, P1, "TOPD", dp: 9000, level: 5, "1:battle:top", name: "TopMon");
    HeadlessEntityId under = StageSynthetic(match, P1, "UNDERD", dp: 3000, level: 3, "1:battle:under", name: "UnderMon", zone: ChoiceZone.Hand);

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var permanent = new Cec.Permanent(match.Context, top, P1);
    await permanent.AddDigivolutionCardsTop(new List<Cec.CardSource> { new Cec.CardSource(match.Context, under, P1) }, null);

    Scr.SelectBurstDigivolutionEffect burst = GManagerBurst(match);
    burst.AddTrashTopCardAtTurnEnd(permanent);

    Cec.ICardEffect? onEnd = permanent.EffectList(Cec.EffectTiming.OnEndTurn)
        .FirstOrDefault(e => (e.EffectDiscription ?? string.Empty).Contains("burst digivolution turn", StringComparison.OrdinalIgnoreCase));
    AssertTrue(onEnd is not null, "AddTrashTopCardAtTurnEnd registered an OnEndTurn ActivateClass on the permanent's UntilEachTurnEndEffects");
    AssertTrue(onEnd!.CanActivate(new Hashtable()),
        "CanActivate ON: the permanent is on the field with >=1 digivolution card (the burst top-card trash target)");
}

static Scr.SelectBurstDigivolutionEffect GManagerBurst(DcgoMatch match)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    return Cec.GManager.instance!.GetComponent<Scr.SelectBurstDigivolutionEffect>();
}

// ═══════════════════════════════ assertions ═══════════════════════════════

static void AssertTrue(bool condition, string message)
{
    if (!condition) { throw new InvalidOperationException($"Assertion failed: {message}"); }
}

static void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Assertion failed: {message} (expected {expected}, got {actual})");
    }
}

// ═══════════════════════════════════ harness (W3-SpecialWitness 정본) ═══════════════════════════════════

PlayerDeckSetup[] MonoDecks() => new[]
{
    new PlayerDeckSetup(P1, Enumerable.Repeat(new HeadlessEntityId("BT1_028"), 50).ToArray()),
    new PlayerDeckSetup(P2, Enumerable.Repeat(new HeadlessEntityId("BT1_028"), 50).ToArray()),
};

async Task<(DcgoMatch Match, PolicyChoiceProvider Policy)> NewMatchAsync(int seed)
{
    var policy = new PolicyChoiceProvider();
    EngineContext context = ContextFactory.CreateWithProvider(policy, seed);
    CardBaseEntityLoader.LoadInto((CardDatabase)context.CardRepository);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        MonoDecks(), firstPlayerId: P1, initialHandSize: 0, initialSecuritySize: 0, enableMulligan: false);
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
        throw new InvalidOperationException($"drive did not reach main-wait — phase:{t.Phase}/{t.StepCursor} player:{t.TurnPlayerId}");
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

HeadlessEntityId Stage(DcgoMatch match, HeadlessPlayerId owner, string cardNumber, ChoiceZone _unused, string instanceId, ChoiceZone zone = ChoiceZone.BattleArea, bool register = false)
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

HeadlessEntityId StageSynthetic(DcgoMatch match, HeadlessPlayerId owner, string number, int dp, int level, string instanceId,
    string? name = null, string cardType = "Digimon", ChoiceZone zone = ChoiceZone.BattleArea,
    string[]? traits = null, string[]? colors = null, Dictionary<string, object?>? extraDefMeta = null, int? playCost = null)
{
    EngineContext ctx = match.Context;
    var defId = new HeadlessEntityId($"DEF:{number}:{owner.Value}:{instanceId}");
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = level };
    if (traits is { Length: > 0 })
    {
        meta["traits"] = traits;
    }

    if (colors is { Length: > 0 })
    {
        meta["colors"] = colors;
    }

    if (extraDefMeta is not null)
    {
        foreach ((string k, object? v) in extraDefMeta)
        {
            meta[k] = v;
        }
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

// ═══════════════════════════════ providers/context (W3-SpecialWitness 정본) ═══════════════════════════════

sealed class PolicyChoiceProvider : IChoiceProvider
{
    private readonly List<(Func<ChoiceRequest, bool> Applies, Func<ChoiceRequest, ChoiceResult> Answer, bool OneShot)> _handlers = new();
    private readonly ScriptedChoiceProvider _fallback = new();

    public void On(Func<ChoiceRequest, bool> applies, Func<ChoiceRequest, ChoiceResult> answer, bool oneShot = true)
        => _handlers.Add((applies, answer, oneShot));

    public Task<ChoiceResult> ChooseAsync(ChoiceRequest request, CancellationToken cancellationToken = default)
    {
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
