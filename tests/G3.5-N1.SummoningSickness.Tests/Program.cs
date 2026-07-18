using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

// N-1 (summoning sickness): a Digimon that entered the field this turn cannot attack until its
// controller's next turn unless it has Rush. The engine SETS this on play (PlayCardAction),
// INHERITS it on digivolve (DigivolveAction keeps the existing permanent's status), and CLEARS it at
// the controller's next-turn boundary. Previously the consumption check existed (AttackPermanentAction)
// but nothing set the flag, so freshly played Digimon could attack instantly.
//
// RE-TARGETED (4b B4, P-D EndTurn seam): the OLD driver's `HeadlessActionFactory.EndTurn` step-cadence
// turn cycle and `AdvancePhase` step-to-main are RETIRED onto DcgoMatch.CreatePumpDriven. Turn cycling is
// now the pump's real turn-end (explicit Pass -> EndPhaseAsync -> flip; §2.1 P-D) and phase progression is
// the pump auto-flow (DriveUntil(AtMainWaitOf), EXEMPLAR-T1/F62 precedent). The AS-IS next-turn clear is now
// the pump's TurnFlowPump.ExpireEnteredThisTurnFlags at the turn boundary (the substrate carrier of the AS-IS
// TurnCount++ expiry). All summoning-sickness assertions (flag set/inherit/clear, attack-declaration gating)
// are preserved unchanged.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Playing a Digimon marks it summoning-sick and blocks its attack", PlayMarksSickAndBlocksAttack),
    ("A played Digimon with Rush can attack the same turn", RushBypassesSickness),
    ("The summoning-sickness flag is cleared at the controller's next turn", NextTurnClearsSickness),
    ("Digivolving onto an established Digimon inherits not-sick and can attack", DigivolveInheritsNotSick),
    ("Digivolving onto a freshly played Digimon stays sick and cannot attack", DigivolveInheritsSick),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex)
    {
        failures.Add(test.Name);
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}");
    }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

async Task PlayMarksSickAndBlocksAttack()
{
    (DcgoMatch match, EngineContext ctx) = await BaseMatch();
    HeadlessEntityId cardId = StageHand(match, P1, "P1-M01", "p1:hand:M01");

    await PlayAsync(match, P1, cardId);

    AssertTrue(ReadFlag(match, cardId, "enteredThisTurn"), "played card is marked entered-this-turn");
    AssertFalse(HasDeclaration(match, P1, cardId), "summoning-sick Digimon produces no attack declaration");
    _ = ctx;
}

async Task RushBypassesSickness()
{
    (DcgoMatch match, _) = await BaseMatch(rushDefinitions: true);
    HeadlessEntityId cardId = StageHand(match, P1, "P1-M01", "p1:hand:M01");

    await PlayAsync(match, P1, cardId);

    AssertTrue(ReadFlag(match, cardId, "enteredThisTurn"), "rush card still entered this turn");
    AssertTrue(HasDeclaration(match, P1, cardId), "rush Digimon can attack the same turn");
}

async Task NextTurnClearsSickness()
{
    (DcgoMatch match, _) = await BaseMatch();
    HeadlessEntityId cardId = StageHand(match, P1, "P1-M01", "p1:hand:M01");
    await PlayAsync(match, P1, cardId);
    AssertFalse(HasDeclaration(match, P1, cardId), "sick on the turn it was played");

    // End P1's turn, play out P2's turn, and return to P1 — the next-turn boundary clears the flag.
    await PassToAsync(match, P1, P2);
    await PassToAsync(match, P2, P1);

    AssertFalse(ReadFlag(match, cardId, "enteredThisTurn"), "flag cleared at the controller's next turn");
    AssertTrue(HasDeclaration(match, P1, cardId), "no longer summoning-sick next turn");
}

async Task DigivolveInheritsNotSick()
{
    (DcgoMatch match, EngineContext ctx) = await BaseMatch();
    // Under-card has been on the field since a prior turn (no entered-this-turn flag).
    HeadlessEntityId underCard = StageField(match, P1, "P1-M01", "p1:battle:M01");

    HeadlessEntityId evolving = StageHand(match, P1, "P1-M02", "p1:hand:M02");
    await DigivolveAsync(match, P1, evolving, underCard);

    AssertFalse(ReadFlag(match, evolving, "enteredThisTurn"), "evolved Digimon inherits not-sick");
    AssertTrue(HasDeclaration(match, P1, evolving), "evolved established Digimon can attack");
    _ = ctx;
}

async Task DigivolveInheritsSick()
{
    (DcgoMatch match, _) = await BaseMatch();
    // Under-card was played THIS turn, so it is summoning-sick; digivolving inherits that.
    HeadlessEntityId underCard = StageHand(match, P1, "P1-M01", "p1:hand:M01");
    await PlayAsync(match, P1, underCard);
    AssertTrue(ReadFlag(match, underCard, "enteredThisTurn"), "under-card sick after play");

    HeadlessEntityId evolving = StageHand(match, P1, "P1-M02", "p1:hand:M02");
    await DigivolveAsync(match, P1, evolving, underCard);

    AssertTrue(ReadFlag(match, evolving, "enteredThisTurn"), "evolved Digimon inherits sick");
    AssertFalse(HasDeclaration(match, P1, evolving), "evolved freshly played Digimon cannot attack");
}

// --- Action drivers (pump; EXEMPLAR-T1 precedent) ------------------------

async Task PlayAsync(DcgoMatch match, HeadlessPlayerId player, HeadlessEntityId cardId)
{
    LegalAction play = Legal(match, player)
        .Single(a => a.ActionType == HeadlessActionTypes.PlayCard &&
            ReadId(a.Parameters, HeadlessActionParameterKeys.CardId) == cardId.Value);
    await ApplyAsync(match, play);
}

async Task DigivolveAsync(DcgoMatch match, HeadlessPlayerId player, HeadlessEntityId cardId, HeadlessEntityId targetCardId)
{
    LegalAction digivolve = Legal(match, player)
        .Single(a => a.ActionType == HeadlessActionTypes.Digivolve &&
            ReadId(a.Parameters, HeadlessActionParameterKeys.CardId) == cardId.Value &&
            ReadId(a.Parameters, HeadlessActionParameterKeys.TargetCardId) == targetCardId.Value);
    await ApplyAsync(match, digivolve);
}

// End <from>'s turn with an explicit Pass and drive the pump auto-flow to <to>'s main wait (the P-D mirror
// of the OLD explicit EndTurn + AdvanceToMain step cadence).
async Task PassToAsync(DcgoMatch match, HeadlessPlayerId from, HeadlessPlayerId to)
{
    LegalAction pass = Legal(match, from).First(a => a.ActionType == HeadlessActionTypes.Pass);
    await ApplyAsync(match, pass);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, to) || m.IsTerminal());
    AssertTrue(AtMainWaitOf(match, to), $"reached {to}'s main wait after {from} passed");
}

// --- Queries -------------------------------------------------------------

bool HasDeclaration(DcgoMatch match, HeadlessPlayerId player, HeadlessEntityId attackerId)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    return new AttackPermanentAction()
        .GetAttackDeclarations(match.Context, player)
        .Any(declaration => declaration.AttackerId == attackerId);
}

bool ReadFlag(DcgoMatch match, HeadlessEntityId cardId, string key)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
    {
        throw new InvalidOperationException($"Missing card instance '{cardId}'.");
    }

    return record.Metadata.TryGetValue(key, out object? raw) && raw is bool value && value;
}

// --- Setup ---------------------------------------------------------------

async Task<(DcgoMatch Match, EngineContext Context)> BaseMatch(bool rushDefinitions = false)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 91);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}", rushDefinitions));
        cards.Upsert(Digimon($"P2-M{index:D2}", rush: false));
    }

    DcgoMatch match = DcgoMatch.CreatePumpDriven(context, new EngineTrace());
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") }, firstPlayerId: P1,
        initialHandSize: 0, initialSecuritySize: 0, enableMulligan: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 91, setup: setup));
    await StepOnceAsync(match);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P1));
    return (match, context);
}

// Stage a card instance (def id = card number, already upserted) into a zone at the pump-staged board.
HeadlessEntityId Stage(DcgoMatch match, HeadlessPlayerId owner, string cardNumber, string instanceId, ChoiceZone zone)
{
    EngineContext ctx = match.Context;
    var id = new HeadlessEntityId(instanceId);
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId(cardNumber), owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["isSuspended"] = false }));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone)).GetAwaiter().GetResult();
    return id;
}

HeadlessEntityId StageHand(DcgoMatch match, HeadlessPlayerId owner, string cardNumber, string instanceId) =>
    Stage(match, owner, cardNumber, instanceId, ChoiceZone.Hand);

HeadlessEntityId StageField(DcgoMatch match, HeadlessPlayerId owner, string cardNumber, string instanceId) =>
    Stage(match, owner, cardNumber, instanceId, ChoiceZone.BattleArea);

static CardRecord Digimon(string id, bool rush)
{
    Dictionary<string, object?> metadata = new(StringComparer.Ordinal)
    {
        ["fixedDigivolutionCost"] = 0,
        ["dp"] = 3000,
        ["level"] = 3,
    };
    if (rush)
    {
        metadata["hasRush"] = true;
    }

    // PlayCost 0 keeps every card playable from an empty memory pool; EvolutionCondition null matches
    // any digivolution target so the digivolve drivers do not depend on printed evolution lines.
    return new CardRecord(
        new HeadlessEntityId(id),
        id,
        $"{id} Card",
        metadata,
        CardType: "Digimon",
        PlayCost: 0);
}

static PlayerDeckSetup Deck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

static string? ReadId(IReadOnlyDictionary<string, object?> p, string key)
{
    if (!p.TryGetValue(key, out object? raw) || raw is null) return null;
    return raw is HeadlessEntityId id ? id.Value : raw.ToString();
}

// --- Pump harness (EXEMPLAR-T1 precedent) --------------------------------

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

bool AtMainWaitOf(DcgoMatch match, HeadlessPlayerId player) =>
    match.Context.TurnController.Current.Phase == HeadlessPhase.Main
    && match.Context.TurnController.Current.TurnPlayerId == player
    && !match.HasPendingChoice() && !match.IsTerminal();

// --- Assertions ----------------------------------------------------------

static void AssertTrue(bool value, string label)
{
    if (!value) throw new InvalidOperationException($"{label}: expected true.");
}

static void AssertFalse(bool value, string label)
{
    if (value) throw new InvalidOperationException($"{label}: expected false.");
}
