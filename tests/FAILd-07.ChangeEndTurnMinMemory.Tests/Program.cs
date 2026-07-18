using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

// FAIL-d: ChangeEndTurnMinMemory (AS-IS BT14_081/BT17_069 "set the turn-end min memory to 3") was MISSING. The
// turn auto-ends when the opponent reaches the min-memory threshold (default 1); the effect raises it to 3, so at
// memory -1 the turn no longer passes.
//
// RE-TARGETED (4b B4, P-D EndTurn seam): the OLD driver called the invented `new HeadlessMainPhaseFlow()
// .EvaluateAfterMemoryMutation` directly (a page-flow-direct currency). It is RETIRED onto
// DcgoMatch.CreatePumpDriven, where the AS-IS-canonical scan `AutoProcessing.TurnEndMinMemory` (AutoProcessing.cs
// :1448, folded across every usable IChangeEndTurnMinMemoryEffect on the players' + permanents' effect lists) is
// the LIVE threshold resolver consumed by the pump's real EndTurnCheck / EndTurnProcess. The test now drives a
// costed play across the memory threshold and observes whether the pump AUTO-ends the turn — exercising the
// canonical scan end-to-end. See suite_retarget_4b_design §3.1e (FAILd-07 item-5 dependency): the flow copy
// HeadlessMainPhaseFlow.ResolveTurnEndMinMemory is byte-identical to the AS-IS owner and survives ONLY for the
// OLD scaffold (its GManager-independent EvaluateMemoryPass path); it is deleted wholesale with HeadlessMainPhaseFlow
// at B6 (item-2/item-5 coupled). This retarget removes FAILd-07's dependency on that copy without a premature
// source re-point (which §3.1e note-3 flags as B6-order-dependent).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("default threshold 1: a cost-1 play (memory -> -1) auto-ends the turn", () => AssertPass(memory: -1, changeMinMemory: false, expectTurnEnds: true)),
    ("ChangeEndTurnMinMemory(3): a cost-1 play (memory -> -1) does NOT auto-end the turn", () => AssertPass(memory: -1, changeMinMemory: true, expectTurnEnds: false)),
    ("ChangeEndTurnMinMemory(3): a cost-3 play (memory -> -3) DOES auto-end the turn", () => AssertPass(memory: -3, changeMinMemory: true, expectTurnEnds: true)),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// Drive a pump-real costed play to <memory> and assert whether the AS-IS threshold (AutoProcessing
// .TurnEndMinMemory, raised to 3 iff <changeMinMemory>) auto-ends the turn. <memory> is the negative amount the
// costed play reaches from 0; the play cost is |memory|.
async Task AssertPass(int memory, bool changeMinMemory, bool expectTurnEnds)
{
    int cost = -memory;
    (DcgoMatch match, EngineContext context) = await NewPumpMatchAsync(seed: 927);

    // The turn-end-min-memory effect on P1's board (the AS-IS-literal live scan AutoProcessing.TurnEndMinMemory
    // walks each field permanent's EffectList(None) for IChangeEndTurnMinMemoryEffect). Reflection dispatch keys
    // the CEntity_Effect off the def's CardNumber (TfxChangeEndTurnMinMemory -> minMemory 3). dp/level keep it a
    // valid field permanent that the pump's no-DP sweep does not trash.
    if (changeMinMemory)
    {
        StageFieldPermanent(match, P1, "TfxChangeEndTurnMinMemory", "p1:battle:MinMem", register: true);
    }

    // A vanilla Digimon that costs |memory|, staged into P1's hand.
    var cards = (CardDatabase)context.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId("VAN"), "VAN", "Vanilla",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 3 }, CardType: "Digimon", PlayCost: cost));
    var hand = new HeadlessEntityId("p1:hand:VAN");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(hand, new HeadlessEntityId("VAN"), P1));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, hand, ChoiceZone.None, ChoiceZone.Hand));

    context.MemoryController.Set(0);
    AssertTrue(AtMainWaitOf(match, P1), "starts at P1's main-phase wait");
    int p1BattleBefore = ZoneCards(match, P1, ChoiceZone.BattleArea).Count;

    LegalAction play = Legal(match, P1)
        .Single(x => x.ActionType == HeadlessActionTypes.PlayCard && x.Id.Value.Contains(hand.Value, StringComparison.Ordinal));
    await ApplyAsync(match, play);
    // Let any auto turn-end drive settle (to P2's main if it ends, or back to P1's main if it does not).
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P2) || AtMainWaitOf(m, P1) || m.IsTerminal());

    // Proof the play actually resolved (not an Illegal silent skip, RD-R3-02 guard).
    AssertTrue(!ZoneCards(match, P1, ChoiceZone.Hand).Contains(hand), "the played card left P1's hand (it resolved)");
    AssertEqual(p1BattleBefore + 1, ZoneCards(match, P1, ChoiceZone.BattleArea).Count, "the costed play entered P1's battle area");

    HeadlessPlayerId? turnPlayer = context.TurnController.Current.TurnPlayerId;
    if (expectTurnEnds)
    {
        // The threshold was met: the pump's EndTurnCheck/EndTurnProcess auto-ended the turn -> flipped to P2.
        AssertEqual(P2.Value, turnPlayer?.Value ?? 0, $"memory {memory} reaches the turn-end threshold -> the pump auto-ended P1's turn");
        AssertEqual(cost, context.MemoryController.Current.Current, "opponent starts with the mirrored +|m| memory");
    }
    else
    {
        // The ChangeEndTurnMinMemory(3) effect raised the threshold above +|m|: the turn did NOT auto-end; P1 keeps
        // the turn and can still play (this is the AUTO-threshold path, not an explicit Pass — we never passed).
        AssertEqual(P1.Value, turnPlayer?.Value ?? 0, $"memory {memory} is below the raised threshold 3 -> P1's turn did NOT auto-end");
        AssertEqual(memory, context.MemoryController.Current.Current, "P1 still holds the negative memory (no flip, no mirror)");
        AssertTrue(Legal(match, P1).Any(a => a.ActionType == HeadlessActionTypes.Pass),
            "P1 is still the acting turn player (a Pass lane remains) — the turn continues");
    }
}

// --- Harness (pump P-D retarget scaffold; F62/EXEMPLAR-T1 precedent) ------

async Task<(DcgoMatch Match, EngineContext Context)> NewPumpMatchAsync(int seed)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: seed);
    var cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = DcgoMatch.CreatePumpDriven(context, new EngineTrace());
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") }, firstPlayerId: P1,
        initialHandSize: 0, initialSecuritySize: 0, enableMulligan: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: seed, setup: setup));
    await StepOnceAsync(match);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P1));
    return (match, context);
}

void StageFieldPermanent(DcgoMatch match, HeadlessPlayerId owner, string cardNumber, string instanceId, bool register)
{
    EngineContext ctx = match.Context;
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId(cardNumber);
    cards.Upsert(new CardRecord(defId, cardNumber, cardNumber,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 3 }, CardType: "Digimon"));
    var id = new HeadlessEntityId(instanceId);
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["isSuspended"] = false }));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
    if (register)
    {
        using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(ctx);
        CardEffectRegistrar.RegisterCard(ctx, id, owner);
    }
}

async Task ApplyAsync(DcgoMatch match, LegalAction action)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.ApplyActionAsync(action);
    await match.StepAsync();
    await match.StepAsync();
}

async Task DriveUntilAsync(DcgoMatch match, Func<DcgoMatch, bool> condition)
{
    for (int i = 0; i < 96 && !condition(match); i++)
    {
        if (match.HasPendingChoice())
        {
            bool decline = match.Context.ChoiceController.PendingRequest!.Type is ChoiceType.BreedingDecision or ChoiceType.Mulligan;
            await ResolvePendingAsync(match, skip: decline);
        }
        else await StepOnceAsync(match);
    }
    if (!condition(match))
    {
        HeadlessTurnState t = match.Context.TurnController.Current;
        throw new InvalidOperationException(
            $"pump drive did not reach the expected state — phase:{t.Phase}/{t.StepCursor} turn:{t.TurnNumber} player:{t.TurnPlayerId} " +
            $"choice:{match.Context.ChoiceController.PendingRequest?.Type.ToString() ?? "<none>"} pending:{match.HasPendingChoice()} " +
            $"terminal:{match.IsTerminal()} memory:{match.Context.MemoryController.Current.Current}");
    }
}

async Task ResolvePendingAsync(DcgoMatch match, bool skip)
{
    HeadlessPlayerId chooser = match.Context.ChoiceController.PendingRequest!.PlayerId;
    LegalAction? action;
    using (AmbientMatchContext.Enter(match.Context))
    {
        action = match.GetLegalActions(chooser).FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice
                && a.Id.Value.EndsWith(":skip", StringComparison.Ordinal) == skip)
            ?? match.GetLegalActions(chooser).FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice);
    }
    if (action is null) throw new InvalidOperationException("no ResolveChoice lane for the pending request");
    await ApplyAsync(match, action);
}

async Task StepOnceAsync(DcgoMatch match)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.StepAsync();
}

IReadOnlyList<LegalAction> Legal(DcgoMatch match, HeadlessPlayerId player)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    return match.GetLegalActions(player);
}

IReadOnlyList<HeadlessEntityId> ZoneCards(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone) =>
    ((IZoneStateReader)match.Context.ZoneMover).GetCards(player, zone).ToArray();

bool AtMainWaitOf(DcgoMatch match, HeadlessPlayerId player) =>
    match.Context.TurnController.Current.Phase == HeadlessPhase.Main
    && match.Context.TurnController.Current.TurnPlayerId == player
    && !match.HasPendingChoice() && !match.IsTerminal();

// --- Helpers -------------------------------------------------------------

static CardRecord Digimon(string id) =>
    new(new HeadlessEntityId(id), id, $"{id} Card", new Dictionary<string, object?>(), CardType: "Digimon");

static PlayerDeckSetup Deck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
