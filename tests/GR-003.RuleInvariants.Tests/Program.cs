using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// GR-003: a permanent RULE-INVARIANT gate. Drives random-legal self-play over real ST1/ST2/ST3 cards and
// FAILS if the engine ever ALLOWS a state the DCGO rules forbid. This guards against the class of bug the
// stability smoke (G13-003) cannot catch — "the loop runs fine but a rule is wrong". The broader diagnostic
// lives in tools/RuleAudit; this is the fast, deterministic regression guard (bounded games/cap).
//
// Invariants asserted (all must hold every step):
//   memory: turn ends when it crosses < 0 (no costed play after); never starts a turn negative; range [-10,10]
//   breeding: a Digi-Egg never moves to / sits in the battle area; eggs never leak to hand/security
//   attack: no summoning-sick attack (entered this turn, no Rush); no suspended attacker; attacker suspends after
//   turn: only the turn player takes a costed play / attack
//   option: an Option never stays on the battle area after resolving

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

// Real ST1/ST2/ST3 starter decks (50 main + 4 digitama), cross-set matchups.
var games = new (string A, string B, int Seed, int Cap)[]
{
    ("ST1", "ST2", 101, 400),
    ("ST2", "ST3", 202, 400),
    ("ST3", "ST1", 303, 400),
};

var violations = new List<string>();
int steps = 0;
foreach (var g in games) steps += await RunAsync(g.A, g.B, g.Seed, g.Cap);

Console.WriteLine($"GR-003 rule-invariant gate: {games.Length} self-play games, {steps} steps.");
if (violations.Count > 0)
{
    Console.Error.WriteLine($"FAIL: {violations.Count} rule violation(s):");
    foreach (var v in violations.Take(15)) Console.Error.WriteLine($"  - {v}");
    Environment.Exit(1);
}
Console.WriteLine("PASS: no rule-invariant violation across the audited dimensions.");

async Task<int> RunAsync(string aSet, string bSet, int seed, int cap)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: seed);
    var db = (CardDatabase)context.CardRepository;
    CardBaseEntityLoader.LoadInto(db);
    StarterDecks.StarterDeck d1 = StarterDecks.Get(aSet), d2 = StarterDecks.Get(bSet);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[]
        {
            new PlayerDeckSetup(P1, d1.MainDefinitions, d1.DigitamaDefinitions),
            new PlayerDeckSetup(P2, d2.MainDefinitions, d2.DigitamaDefinitions),
        }, firstPlayerId: P1);
    var match = new DcgoMatch(context, new EngineTrace(), actionLegality: new LegalActionSetValidator());
    var env = new HeadlessRlEnvironment(match);
    await env.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: seed, setup: setup));

    var rng = new Random(seed * 7 + 1);
    var players = new[] { P1, P2 };
    var zones = (IZoneStateReader)context.ZoneMover;
    string tag = $"{aSet}v{bSet}#{seed}";
    int negTurn = -1; HeadlessPlayerId negPlayer = default; int prevTurn = 0; int n = 0;
    void V(string t, int turn, string d) => violations.Add($"{t}|{tag} T{turn}: {d}");

    while (n < cap && !match.IsTerminal())
    {
        HeadlessPlayerId mover = default; bool found = false;
        foreach (var p in players) { if (match.GetLegalActions(p).Count > 0) { mover = p; found = true; break; } }
        if (!found) break;

        IReadOnlyList<LegalAction> legal = match.GetLegalActions(mover);
        LegalAction action = legal[rng.Next(legal.Count)];
        HeadlessTurnState turnBefore = match.GetObservation().Turn;
        int turn = turnBefore.TurnNumber;

        if (turn != prevTurn)
        {
            prevTurn = turn;
            int memStart = context.MemoryController.Current.Current;
            if (turnBefore.TurnPlayerId is { } stp && stp.Value == mover.Value && memStart <= -1)
                V("NEG_MEMORY_AT_TURN_START", turn, $"P{mover.Value} starts with memory {memStart}");
        }

        if ((IsCostedPlay(action.ActionType) || action.ActionType == HeadlessActionTypes.DeclareAttack)
            && turnBefore.TurnPlayerId is { } tp && tp.Value != mover.Value)
            V("NON_TURN_PLAYER_ACTION", turn, $"P{mover.Value} {action.ActionType} on P{tp.Value}'s turn");

        if (IsCostedPlay(action.ActionType) && negTurn == turn && negPlayer.Value == mover.Value)
            V("MEM_TURN_NOT_ENDED", turn, $"P{mover.Value} {action.ActionType} while memory already <= -1");

        if (action.ActionType == HeadlessActionTypes.MoveBreedingToBattle)
        {
            var top = zones.GetCards(mover, ChoiceZone.BreedingArea);
            if (top.Count > 0 && DefType(db, context, top[0]) == "DigiEgg")
                V("BREED_MOVE_DIGIEGG", turn, $"P{mover.Value} moves a DigiEgg out of breeding");
        }

        if (action.ActionType == HeadlessActionTypes.DeclareAttack
            && action.Parameters.TryGetValue(HeadlessActionParameterKeys.AttackerId, out object? aObj) && aObj is string aId
            && context.CardInstanceRepository.TryGetInstance(new HeadlessEntityId(aId), out CardInstanceRecord? atk) && atk is not null)
        {
            bool rush = ReadFlag(atk.Metadata, "hasRush")
                || (db.TryGetCard(atk.DefinitionId, out CardRecord? ac) && ac is not null && ReadFlag(ac.Metadata, "hasRush"));
            if (ReadFlag(atk.Metadata, "enteredThisTurn") && !rush) V("ATTACK_SUMMONING_SICK", turn, $"P{mover.Value} attacks with a card that entered this turn");
            if (ReadFlag(atk.Metadata, "isSuspended")) V("ATTACK_WHILE_SUSPENDED", turn, $"P{mover.Value} attacks while suspended");
        }

        RlStepResult st = await env.StepAsync(action);
        n++;

        int mem = context.MemoryController.Current.Current;
        HeadlessPlayerId? cur = match.GetObservation().Turn.TurnPlayerId;
        if (mem is > 10 or < -10) V("MEM_OUT_OF_RANGE", turn, $"memory={mem}");
        if (mem <= -1 && cur is { } c) { negTurn = match.GetObservation().Turn.TurnNumber; negPlayer = c; }
        else if (mem >= 0) negTurn = -1;

        if (action.ActionType == HeadlessActionTypes.ActivateOption && !st.HasPendingChoice
            && action.Parameters.TryGetValue(HeadlessActionParameterKeys.CardId, out object? oObj) && oObj is string oId
            && zones.GetCards(mover, ChoiceZone.BattleArea).Contains(new HeadlessEntityId(oId))
            && DefType(db, context, new HeadlessEntityId(oId)) == "Option")
            V("OPTION_STAYS_ON_BOARD", turn, $"P{mover.Value} Option stays on board after resolving");

        if (action.ActionType == HeadlessActionTypes.DeclareAttack && !st.HasPendingChoice
            && action.Parameters.TryGetValue(HeadlessActionParameterKeys.AttackerId, out object? a2) && a2 is string a2Id
            && zones.GetCards(mover, ChoiceZone.BattleArea).Contains(new HeadlessEntityId(a2Id))
            && context.CardInstanceRepository.TryGetInstance(new HeadlessEntityId(a2Id), out CardInstanceRecord? a2Inst) && a2Inst is not null
            && !ReadFlag(a2Inst.Metadata, "isSuspended"))
            V("ATTACKER_NOT_SUSPENDED", turn, $"P{mover.Value} attacker not suspended after attacking");

        foreach (var pl in players)
        {
            foreach (var id in zones.GetCards(pl, ChoiceZone.BattleArea))
                if (DefType(db, context, id) == "DigiEgg") { V("EGG_IN_BATTLE", turn, $"P{pl.Value} battle area holds a DigiEgg"); break; }
            foreach (var z in new[] { ChoiceZone.Hand, ChoiceZone.Security })
                foreach (var id in zones.GetCards(pl, z))
                    if (DefType(db, context, id) == "DigiEgg") { V("EGG_IN_HAND_OR_SECURITY", turn, $"P{pl.Value} {z} holds a DigiEgg"); goto doneScan; }
        }
        doneScan: ;
    }
    return n;
}

// --- helpers -------------------------------------------------------------
static bool IsCostedPlay(string t) =>
    t is HeadlessActionTypes.PlayCard or HeadlessActionTypes.Digivolve
        or HeadlessActionTypes.SpecialPlay or HeadlessActionTypes.ActivateOption;

static bool ReadFlag(IReadOnlyDictionary<string, object?> m, string k) => m.TryGetValue(k, out object? v) && v is bool b && b;

static string DefType(CardDatabase db, EngineContext ctx, HeadlessEntityId id) =>
    ctx.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? inst) && inst is not null
        && db.TryGetCard(inst.DefinitionId, out CardRecord? c) && c is not null ? c.CardType ?? "?" : "?";
