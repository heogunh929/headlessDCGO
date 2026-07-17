using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using Cec = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

// (R4 batch S3b — docs/audit/r4_tsm_s1_design_2026-07-16.md "S3 실행 설계")
// Main-phase DISPATCH witnesses: the agent's PlayCard / DeclareAttack / Pass actions flow through
// TurnFlowDriver -> mirror MainPhaseAction packets -> the TurnStateMachine selection wait (:971-1170) ->
// the AS-IS dispatch arms (:1176-1252) — PlayCardClass (cost pipeline + [On Play] windows), the RD-9 attack
// declaration + the full attack pipeline ON the pump stack, and PassTurn -> EndTurnProcess.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("PlayCard dispatch reaches the AS-IS executor hand-off and STOPs honestly at RD-P6C1-4 (PlayPermanentClass/UseOptionClass unported — the S3b-2 batch)", PlayCardStopBoundary),
    ("(S3b-2 flip) DeclareAttack dispatch: a staged attacker's security attack resolves the full pipeline on the pump stack and consumes a security card", AttackDispatch),
    ("the pass/breeding surface stays fully live alongside the registered STOPs (S3a suite parity on this build)", PassSurfaceStillLive),
    ("(S3b-2①) CreateNewPermanent: zone entry + suspended/entered metadata + effect registration + view identity", CreateNewPermanentOp),
    ("(S3b-2①) AddCardSource: the new card becomes the zone-resident top, the old top threads into sourceIds, sickness inherits", AddCardSourceOp),
    ("(S3b-2②) just-after bookkeeping: view-stable writes, PERSISTS across the top swap (re-key), DIES on field leave (reset)", BookkeepingLifetime),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex)
    {
        failures.Add(test.Name);
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
    }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

async Task PlayCardStopBoundary()
{
    DcgoMatch match = await NewPumpMatchAsync(seed: 31);
    await ReachMainWaitAsync(match);
    HeadlessPlayerId turnPlayer = match.Context.TurnController.Current.TurnPlayerId!.Value;

    (HeadlessEntityId cardId, _) = FirstPlayableDigimon(match, turnPlayer);
    // The dispatch arm runs PlayCardClass.PlayCard 1:1 — the pay half executes, then the AS-IS
    // PlayPermanentClass/UseOptionClass hand-off is the registered STOP (RD-P6C1-4). The pump surfaces it as a
    // deterministic engine fault (EngineTaskRunner rethrows a Faulted task) — pinned here so the S3b-2 port
    // flips THIS witness to the real play assertions.
    string? fault = null;
    try
    {
        await SendAsync(match, turnPlayer, HeadlessActionTypes.PlayCard,
            new Dictionary<string, object?> { [HeadlessActionParameterKeys.CardId] = cardId.Value });
    }
    catch (NotSupportedException ex)
    {
        fault = ex.Message;
    }

    AssertTrue(fault is not null, "the pump surfaced the executor STOP as a fault");
    AssertTrue(fault!.Contains("RD-P6C1-4", StringComparison.Ordinal), $"the fault names the design item (got: {fault})");
}

async Task AttackDispatch()
{
    // (S3b-2 flip) The CanEvolve engine landed (RD-P6C1-2 read side), so a staged battle digimon no longer
    // gates CanSelect — the attack ARM is now exercisable end-to-end on the pump stack. STAGED board (fixture,
    // R4P2a Place pattern): the play EXECUTOR is still the RD-P6C1-4 STOP, so the attacker is placed directly.
    DcgoMatch match = await NewPumpMatchAsync(seed: 31);
    HeadlessEntityId attackerId = StageBattleDigimon(match, P1, dp: 3000);

    await ReachMainWaitAsync(match);
    HeadlessPlayerId attackerOwner = match.Context.TurnController.Current.TurnPlayerId!.Value;
    HeadlessPlayerId defenderOwner = attackerOwner == P1 ? P2 : P1;
    AssertEqual(P1, attackerOwner, "P1 is the staged attacker's owner and the first turn player");

    // Turn 1: pass (summoning-sickness parity; the staged permanent attacks on P1's next turn).
    await SendAsync(match, attackerOwner, HeadlessActionTypes.Pass, new Dictionary<string, object?>());
    await DriveUntilAsync(match, m => m.Context.TurnController.Current.TurnNumber == 2 && AtMainWait(m));
    await SendAsync(match, defenderOwner, HeadlessActionTypes.Pass, new Dictionary<string, object?>());
    await DriveUntilAsync(match, m => m.Context.TurnController.Current.TurnNumber == 3 && AtMainWait(m));

    int securityBefore = Count(match, defenderOwner, ChoiceZone.Security);
    await SendAsync(match, attackerOwner, HeadlessActionTypes.DeclareAttack,
        new Dictionary<string, object?> { [HeadlessActionParameterKeys.AttackerId] = attackerId.Value });
    await DriveUntilAsync(match, m =>
        Count(m, defenderOwner, ChoiceZone.Security) < securityBefore || m.IsTerminal());

    AssertEqual(securityBefore - 1, Count(match, defenderOwner, ChoiceZone.Security),
        "the security attack consumed one security card");
    AssertTrue(!match.IsTerminal(), "the match continues after the first security check");
}

async Task PassSurfaceStillLive()
{
    // Alongside the two registered STOP gates, the whole pass/breeding surface must stay live on THIS build —
    // a compact re-run of the S3a full-game drive (pass-only agent to the deck-out terminal).
    DcgoMatch match = await NewPumpMatchAsync(seed: 47);
    await StepAsync(match);
    for (int i = 0; i < 2; i++)
    {
        await ResolvePendingAsync(match, skip: true);
    }

    for (int i = 0; i < 800 && !match.IsTerminal(); i++)
    {
        if (match.HasPendingChoice())
        {
            await ResolvePendingAsync(match, skip: true);   // decline breeding: board stays digimon-free
        }
        else if (AtMainWait(match))
        {
            await SendAsync(match, match.Context.TurnController.Current.TurnPlayerId!.Value,
                HeadlessActionTypes.Pass, new Dictionary<string, object?>());
        }
        else
        {
            await StepAsync(match);
        }
    }

    AssertTrue(match.IsTerminal(), "the pass-only game reached the deck-out terminal");
    AssertTrue(match.GetResult().WinnerId is not null, "a winner was marked");
}

async Task CreateNewPermanentOp()
{
    DcgoMatch match = await NewPumpMatchAsync(seed: 53);
    await StepAsync(match);   // StartGame: hands drawn (mulligan pending is fine — the op is zone-level)
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);

    var player = new Cec.Player(match.Context, P1);
    Cec.CardSource card = player.HandCards.First(c => c.IsDigimon);

    Cec.Permanent permanent = await HeadlessDCGO.Engine.Assets.Scripts.Script.CardObjectController
        .CreateNewPermanent(card, isSuspended: true);

    AssertEqual(1, Count(match, P1, ChoiceZone.BattleArea), "the card entered the battle area");
    AssertEqual(card.InstanceId, permanent.TopCard.InstanceId, "the view keys on the entered card");
    AssertTrue(permanent.IsSuspended, "the AS-IS initializer state (IsSuspended) landed in metadata");
    AssertEqual(match.Context.TurnController.Current.TurnNumber, permanent.EnterFieldTurnCount,
        "entered-this-turn stamped (AS-IS EnterFieldTurnCount == TurnCount)");

    // AS-IS jogress writes EnterFieldTurnCount = -1 — the setter maps it onto the sickness carrier.
    permanent.EnterFieldTurnCount = -1;
    AssertEqual(-1, permanent.EnterFieldTurnCount, "the jogress -1 write clears the sickness flag");
}

async Task AddCardSourceOp()
{
    DcgoMatch match = await NewPumpMatchAsync(seed: 53);
    await StepAsync(match);
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);

    var player = new Cec.Player(match.Context, P1);
    List<Cec.CardSource> digimons = player.HandCards.Where(c => c.IsDigimon).Take(2).ToList();
    AssertTrue(digimons.Count == 2, "two hand digimons available");

    Cec.Permanent permanent = await HeadlessDCGO.Engine.Assets.Scripts.Script.CardObjectController
        .CreateNewPermanent(digimons[0], isSuspended: false);
    HeadlessEntityId oldTopId = permanent.TopCard.InstanceId;

    await HeadlessDCGO.Engine.Assets.Scripts.Script.CardObjectController.RemoveFromAllArea(digimons[1]);
    permanent = await permanent.AddCardSource(digimons[1]);

    AssertEqual(digimons[1].InstanceId, permanent.TopCard.InstanceId, "the new card is the top");
    AssertEqual(1, Count(match, P1, ChoiceZone.BattleArea), "still ONE battle-area permanent (top swapped)");
    AssertTrue(permanent.DigivolutionCards.Any(c => c.InstanceId == oldTopId),
        "the old top threaded into the digivolution sources");
    AssertEqual(match.Context.TurnController.Current.TurnNumber, permanent.EnterFieldTurnCount,
        "sickness INHERITED from the under-card (N-1: both entered this turn)");
}

async Task BookkeepingLifetime()
{
    DcgoMatch match = await NewPumpMatchAsync(seed: 59);
    await StepAsync(match);
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);

    var player = new Cec.Player(match.Context, P1);
    List<Cec.CardSource> digimons = player.HandCards.Where(c => c.IsDigimon).Take(2).ToList();

    // CREATE: the executor-shaped writes land on the store and read back through a FRESH view.
    Cec.Permanent permanent = await HeadlessDCGO.Engine.Assets.Scripts.Script.CardObjectController
        .CreateNewPermanent(digimons[0], isSuspended: false);
    permanent.LevelJustAfterPlayed = 3;
    permanent.PlayCostJustAfterPlayed = 4;
    permanent.TraitsJustAfterPlayed = new List<string> { "D-Brigade" };
    Cec.Permanent freshView = new(match.Context, permanent.TopCard.InstanceId, P1);
    AssertEqual(3, freshView.LevelJustAfterPlayed, "a fresh view reads the same entry (match-scoped store)");
    AssertEqual(4, freshView.PlayCostJustAfterPlayed, "play cost survives the view boundary");

    // PERSIST: the top swap (AddCardSource → AttachTargetAsSource) re-keys the entry to the new top.
    HeadlessEntityId oldTop = permanent.TopCard.InstanceId;
    await HeadlessDCGO.Engine.Assets.Scripts.Script.CardObjectController.RemoveFromAllArea(digimons[1]);
    permanent = await permanent.AddCardSource(digimons[1]);
    AssertEqual(3, permanent.LevelJustAfterPlayed, "the AS-IS object persists: played-level survives digivolving");
    AssertTrue(permanent.TraitsJustAfterPlayed.Contains("D-Brigade"), "played-traits survive digivolving");

    // DIE: leaving the field drops the entry — a fresh life reads AS-IS defaults.
    await HeadlessDCGO.Engine.Assets.Scripts.Script.CardObjectController.RemoveField(permanent, ignoreOverflow: true);
    Cec.Permanent rePlayed = await HeadlessDCGO.Engine.Assets.Scripts.Script.CardObjectController
        .CreateNewPermanent(permanent.TopCard, isSuspended: false);
    AssertEqual(-1, rePlayed.LevelJustAfterPlayed, "a re-played card starts a FRESH bookkeeping life (default -1)");
    AssertEqual(0, rePlayed.TraitsJustAfterPlayed.Count, "fresh life: empty played-traits");
}

// --- Drivers -------------------------------------------------------------

async Task<DcgoMatch> NewPumpMatchAsync(int seed)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: seed);
    var db = (CardDatabase)context.CardRepository;
    CardBaseEntityLoader.LoadInto(db);
    StarterDecks.StarterDeck d1 = StarterDecks.Get("ST1"), d2 = StarterDecks.Get("ST2");
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[]
        {
            new PlayerDeckSetup(P1, d1.MainDefinitions, d1.DigitamaDefinitions),
            new PlayerDeckSetup(P2, d2.MainDefinitions, d2.DigitamaDefinitions),
        },
        firstPlayerId: P1,
        initialHandSize: 0,
        initialSecuritySize: 0,
        enableMulligan: false);
    MatchConfig config = MatchConfig.Create(new[] { P1, P2 }, randomSeed: seed, setup: setup);

    DcgoMatch match = new(context, new EngineTrace(), actionProcessor: new TurnFlowDriver());
    await match.InitializeAsync(config);
    TurnFlowPumpHost.Install(context);
    return match;
}

// Start the pump, keep both mulligans, decline the breeding decision, land at the main selection wait.
async Task ReachMainWaitAsync(DcgoMatch match)
{
    await StepAsync(match);
    for (int i = 0; i < 2; i++)
    {
        AssertEqual(ChoiceType.Mulligan, match.Context.ChoiceController.PendingRequest!.Type, "mulligan pending");
        await ResolvePendingAsync(match, skip: true);
    }

    await DriveUntilAsync(match, AtMainWait);
}

static bool AtMainWait(DcgoMatch match) =>
    match.Context.TurnController.Current.Phase == HeadlessPhase.Main
    && !match.HasPendingChoice()
    && !match.IsTerminal();

// Generic driver: resolve breeding decisions with DECLINE (keep the board minimal), resolve any other
// pending choice with ACT, otherwise step — until the condition holds. Bounded.
async Task DriveUntilAsync(DcgoMatch match, Func<DcgoMatch, bool> condition)
{
    for (int i = 0; i < 64 && !condition(match); i++)
    {
        if (match.HasPendingChoice())
        {
            bool decline = match.Context.ChoiceController.PendingRequest!.Type == ChoiceType.BreedingDecision;
            await ResolvePendingAsync(match, skip: decline);
        }
        else
        {
            await StepAsync(match);
        }
    }

    if (!condition(match))
    {
        HeadlessTurnState t = match.Context.TurnController.Current;
        throw new InvalidOperationException(
            $"drive did not reach the expected state — phase:{t.Phase}/{t.StepCursor} turn:{t.TurnNumber} " +
            $"choice:{match.Context.ChoiceController.PendingRequest?.Type.ToString() ?? "<none>"} terminal:{match.IsTerminal()} " +
            $"memory:{match.Context.MemoryController.Current.Current}");
    }
}

async Task SendAsync(DcgoMatch match, HeadlessPlayerId player, string actionType, Dictionary<string, object?> parameters)
{
    var action = new LegalAction(
        new HeadlessEntityId($"witness:{actionType}:{Guid.NewGuid():N}"),
        player,
        actionType,
        parameters);
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.ApplyActionAsync(action);
    await match.StepAsync();
    await match.StepAsync();
}

async Task ResolvePendingAsync(DcgoMatch match, bool skip)
{
    HeadlessPlayerId chooser = match.Context.ChoiceController.PendingRequest!.PlayerId;
    LegalAction action;
    using (AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context))
    {
        action = match.GetLegalActions(chooser)
            .First(a => a.ActionType == HeadlessActionTypes.ResolveChoice
                && a.Id.Value.EndsWith(":skip", StringComparison.Ordinal) == skip);
    }

    using AmbientMatchContext.Scope __ = AmbientMatchContext.Enter(match.Context);
    await match.ApplyActionAsync(action);
    await match.StepAsync();
    await match.StepAsync();
}

static async Task StepAsync(DcgoMatch match)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.StepAsync();
}

// Stage a synthetic battle-area digimon (R4P2a Place fixture pattern) — the RD-P6C1-4 STOP blocks the real
// play path, so the attack witness constructs its board directly.
static HeadlessEntityId StageBattleDigimon(DcgoMatch match, HeadlessPlayerId owner, int dp)
{
    EngineContext context = match.Context;
    var defId = new HeadlessEntityId($"DEF:staged:{owner.Value}");
    ((CardDatabase)context.CardRepository).Upsert(new CardRecord(defId, "Staged", "Staged",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = 3 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"card:staged:{owner.Value}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner));
    context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
    return id;
}

// The cheapest playable digimon in hand by the MIRROR predicate (the same term CanSelect() reads).
static (HeadlessEntityId Id, int Cost) FirstPlayableDigimon(DcgoMatch match, HeadlessPlayerId owner)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    var player = new Cec.Player(match.Context, owner);
    Cec.CardSource card = player.HandCards
        .Where(c => c.IsDigimon && c.CanPlayFromHandDuringMainPhase)
        .OrderBy(c => c.PayingCost(HeadlessDCGO.Engine.Assets.Scripts.Script.SelectCardEffect.Root.Hand, null, checkAvailability: true))
        .First();
    int cost = card.PayingCost(HeadlessDCGO.Engine.Assets.Scripts.Script.SelectCardEffect.Root.Hand, null, checkAvailability: true);
    return (card.InstanceId, cost);
}


static int Count(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone)
{
    return match.Context.ZoneMover is IZoneStateReader zones ? zones.GetCards(player, zone).Count : -1;
}


// --- Asserts -------------------------------------------------------------

static void AssertTrue(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException($"expected TRUE: {message}");
}

static void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
}
