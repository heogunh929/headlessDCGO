using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// Rule-correctness AUDIT (not a gate test). Runs random-legal self-play over real ST1/ST2/ST3 cards and
// checks DCGO rule invariants by inspecting LIVE STATE each step (no action-id parsing). It flags where the
// engine ALLOWS something the rules forbid. Diagnostic only — lives in tools/, never runs in run-tests.sh.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

// Real ST1/ST2/ST3 starter decks (50 main + 4 digitama). 20 games over every cross-set matchup, varied seeds.
var matchups = new[] { ("ST1", "ST2"), ("ST2", "ST3"), ("ST3", "ST1"), ("ST1", "ST3"), ("ST2", "ST1"), ("ST3", "ST2") };
var games = Enumerable.Range(0, 20)
    .Select(i => { var m = matchups[i % matchups.Length]; return (A: m.Item1, B: m.Item2, EnvSeed: 100 + i * 37, PolicySeed: 11 + i * 13, Cap: 800); })
    .ToArray();

var violations = new List<string>();
int totalSteps = 0;
int directAttacks = 0, securityDrops = 0; // security bookkeeping (informational)
int secNoDropZeroBefore = 0, secNoDropTerminal = 0, secNoDropUnexplained = 0;
var unexplained = new List<string>();
int attacksDeclared = 0, blockOffered = 0, blockTaken = 0; // blocker exercise tracking
int atkDefHadBlockerCard = 0, atkDefHadBlockerFlag = 0, atkDefHadBlockerReady = 0;
var StarterBlockers = new HashSet<string>(StringComparer.Ordinal) { "ST1_06", "ST2_07", "ST3_07" };
int terminalGames = 0; var winners = new Dictionary<string, int>();

foreach (var g in games)
    totalSteps += await AuditGameAsync(g.A, g.B, g.EnvSeed, g.PolicySeed, g.Cap);

// --- Report --------------------------------------------------------------
Console.WriteLine($"\n===== RULE AUDIT: {games.Length} games, {totalSteps} total steps =====");
Console.WriteLine($"outcomes: {terminalGames}/{games.Length} reached a terminal; winners {string.Join(" ", winners.OrderBy(w => w.Key).Select(w => $"P{w.Key}={w.Value}"))}");
Console.WriteLine($"blocker: {blockOffered} block windows offered ({blockTaken} taken / {blockOffered - blockTaken} skipped) across {attacksDeclared} attacks declared");
Console.WriteLine($"  blocker diag (at attack time, defender side): had-blocker-card={atkDefHadBlockerCard}, of which hasBlocker-flag-set={atkDefHadBlockerFlag}, unsuspended+flagged={atkDefHadBlockerReady}");
Console.WriteLine($"security check: {securityDrops}/{directAttacks} direct player-attacks consumed a security card");
Console.WriteLine($"  non-consuming breakdown: zero-security(direct loss)={secNoDropZeroBefore}, ended-game={secNoDropTerminal}, UNEXPLAINED={secNoDropUnexplained}");
foreach (var u in unexplained.Take(8)) Console.WriteLine($"    unexplained: {u}");
Console.WriteLine();
var byType = violations.GroupBy(v => v.Split('|')[0]).OrderByDescending(grp => grp.Count());
if (violations.Count == 0)
{
    Console.WriteLine("No rule-invariant violations detected.");
}
else
{
    Console.WriteLine($"{violations.Count} violation(s) across {byType.Count()} rule(s):\n");
    foreach (var grp in byType)
    {
        Console.WriteLine($"[{grp.Count(),4}x] {grp.Key}");
        foreach (var ex in grp.Take(3)) Console.WriteLine($"          e.g. {ex.Split('|', 2)[1]}");
    }
}

async Task<int> AuditGameAsync(string aSet, string bSet, int envSeed, int policySeed, int cap)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: envSeed);
    var db = (CardDatabase)context.CardRepository;
    CardBaseEntityLoader.LoadInto(db);

    StarterDecks.StarterDeck d1 = StarterDecks.Get(aSet), d2 = StarterDecks.Get(bSet);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[]
        {
            new PlayerDeckSetup(P1, d1.MainDefinitions, d1.DigitamaDefinitions),
            new PlayerDeckSetup(P2, d2.MainDefinitions, d2.DigitamaDefinitions),
        }, firstPlayerId: P1);
    MatchConfig config = MatchConfig.Create(new[] { P1, P2 }, randomSeed: envSeed, setup: setup);

    var match = new DcgoMatch(context, new EngineTrace(), actionLegality: new LegalActionSetValidator());
    var env = new HeadlessRlEnvironment(match);
    await env.InitializeAsync(config);

    string tag = $"{aSet}v{bSet}#{envSeed}";
    var rng = new Random(policySeed);
    var players = new[] { P1, P2 };
    var zones = (IZoneStateReader)context.ZoneMover;

    int negTurn = -1; HeadlessPlayerId negPlayer = default; // turn in which memory went <= -1, and for whom
    int steps = 0, prevTurn = 0;

    void Flag(string type, int turn, string detail) => violations.Add($"{type}|{tag} T{turn}: {detail}");

    while (steps < cap && !match.IsTerminal())
    {
        HeadlessPlayerId mover = default; bool found = false;
        foreach (var p in players) { if (match.GetLegalActions(p).Count > 0) { mover = p; found = true; break; } }
        if (!found) break;

        IReadOnlyList<LegalAction> legal = match.GetLegalActions(mover);
        LegalAction action = legal[rng.Next(legal.Count)];
        HeadlessTurnState turnBefore = match.GetObservation().Turn;
        int turn = turnBefore.TurnNumber;

        // blocker exercise: a pending Blocker choice means an attack actually opened a block window with >=1
        // unsuspended blocker candidate on the defender. Count windows offered, and how many the policy took.
        if (action.ActionType == HeadlessActionTypes.DeclareAttack)
        {
            attacksDeclared++;
            // Diagnostic: at attack time, did the defender have a starter-deck blocker card on the field, and
            // was its <Blocker> flag actually applied + unsuspended? Distinguishes "blockers never reach the
            // field" (rarity) from "blocker present but hasBlocker not live-applied" (a static-effect bug).
            var defender2 = mover.Value == P1.Value ? P2 : P1;
            bool hadCard = false, hadFlag = false, hadReady = false;
            foreach (var id in zones.GetCards(defender2, ChoiceZone.BattleArea))
                if (context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? bi) && bi is not null
                    && StarterBlockers.Contains(bi.DefinitionId.Value))
                {
                    hadCard = true;
                    bool flag = ReadFlag(bi.Metadata, "hasBlocker");
                    if (flag) hadFlag = true;
                    if (flag && !ReadFlag(bi.Metadata, "isSuspended")) hadReady = true;
                }
            if (hadCard) atkDefHadBlockerCard++;
            if (hadFlag) atkDefHadBlockerFlag++;
            if (hadReady) atkDefHadBlockerReady++;
        }
        if (context.ChoiceController.Current.IsPending
            && context.ChoiceController.PendingRequest?.Type == ChoiceType.Blocker
            && action.ActionType == HeadlessActionTypes.ResolveChoice)
        {
            blockOffered++;
            if (!action.Id.Value.Contains("skip", StringComparison.OrdinalIgnoreCase)) blockTaken++;
        }

        // (#9) at the start of a player's own turn, their memory must not be negative (handover sign).
        if (turn != prevTurn)
        {
            prevTurn = turn;
            int memStart = context.MemoryController.Current.Current;
            if (turnBefore.TurnPlayerId is { } stp && stp.Value == mover.Value && memStart <= -1)
                Flag("NEG_MEMORY_AT_TURN_START", turn, $"P{mover.Value} starts its turn with memory {memStart}");
        }

        // (#8) only the turn player may take a costed play / attack (choices are exempt).
        if ((IsCostedPlay(action.ActionType) || action.ActionType == HeadlessActionTypes.DeclareAttack)
            && turnBefore.TurnPlayerId is { } tpId && tpId.Value != mover.Value)
            Flag("NON_TURN_PLAYER_ACTION", turn, $"P{mover.Value} did {action.ActionType} but it is P{tpId.Value}'s turn");

        // security bookkeeping: count direct player attacks and the defender's security before the step.
        bool directAttack = action.ActionType == HeadlessActionTypes.DeclareAttack
            && (!action.Parameters.TryGetValue(HeadlessActionParameterKeys.AttackTargetId, out object? tgt)
                || tgt is null || tgt is string ts && (ts == "player" || ts.StartsWith("player", StringComparison.OrdinalIgnoreCase)));
        HeadlessPlayerId defender = mover.Value == P1.Value ? P2 : P1;
        int defSecBefore = zones.GetCards(defender, ChoiceZone.Security).Count;

        // (#1) memory turn-end: a play action AFTER memory already went <= -1 this turn for this player.
        if (IsCostedPlay(action.ActionType) && negTurn == turn && negPlayer.Value == mover.Value)
            Flag("MEM_TURN_NOT_ENDED", turn, $"P{mover.Value} {action.ActionType} while memory already <= -1 (turn should have ended)");

        // (#2) breeding move target must be a Digimon (AS-IS Permanent.CanMove). Capture the to-be-moved
        // top card BEFORE the move: only a DigiEgg-typed / DP<=0-egg breeding card is illegal to move.
        // NOTE: there is intentionally NO "hatch+move in the same turn" check — the original permits
        // moving an existing Digimon out and then hatching a fresh egg (state-gated, not turn-gated).
        if (action.ActionType == HeadlessActionTypes.MoveBreedingToBattle)
        {
            var top = zones.GetCards(mover, ChoiceZone.BreedingArea);
            if (top.Count > 0 && string.Equals(DefType(db, context, top[0]), "DigiEgg", StringComparison.Ordinal))
                Flag("BREED_MOVE_NOT_DIGIMON", turn, $"P{mover.Value} moves DigiEgg '{ShortDef(context, top[0])}' from breeding to battle");
        }

        // (#5/#6) attack legality: inspect the attacker's live state BEFORE the attack resolves.
        if (action.ActionType == HeadlessActionTypes.DeclareAttack
            && action.Parameters.TryGetValue(HeadlessActionParameterKeys.AttackerId, out object? atkObj)
            && atkObj is string atkId
            && context.CardInstanceRepository.TryGetInstance(new HeadlessEntityId(atkId), out CardInstanceRecord? atk) && atk is not null)
        {
            bool entered = ReadFlag(atk.Metadata, "enteredThisTurn");
            bool rush = ReadFlag(atk.Metadata, "hasRush")
                || (db.TryGetCard(atk.DefinitionId, out CardRecord? ac) && ac is not null && ReadFlag(ac.Metadata, "hasRush"));
            bool suspended = ReadFlag(atk.Metadata, "isSuspended");
            if (entered && !rush) Flag("ATTACK_SUMMONING_SICK", turn, $"P{mover.Value} attacks with '{ShortDef(context, new HeadlessEntityId(atkId))}' that entered this turn (no Rush)");
            if (suspended) Flag("ATTACK_WHILE_SUSPENDED", turn, $"P{mover.Value} attacks with a suspended '{ShortDef(context, new HeadlessEntityId(atkId))}'");
        }

        RlStepResult st = await env.StepAsync(action);
        steps++;

        // post-apply state checks.
        int mem = context.MemoryController.Current.Current;
        HeadlessPlayerId? tp = match.GetObservation().Turn.TurnPlayerId;
        if (mem is > 10 or < -10) Flag("MEM_OUT_OF_RANGE", turn, $"memory={mem}");
        if (mem <= -1 && tp is { } cur2) { negTurn = match.GetObservation().Turn.TurnNumber; negPlayer = cur2; }
        else if (mem >= 0) { negTurn = -1; }

        // (#7) an Option card must not remain on the battle area once it has resolved.
        if (action.ActionType == HeadlessActionTypes.ActivateOption && !st.HasPendingChoice
            && action.Parameters.TryGetValue(HeadlessActionParameterKeys.CardId, out object? optObj) && optObj is string optId
            && InZone(zones, mover, ChoiceZone.BattleArea, new HeadlessEntityId(optId))
            && string.Equals(DefType(db, context, new HeadlessEntityId(optId)), "Option", StringComparison.Ordinal))
            Flag("OPTION_STAYS_ON_BOARD", turn, $"P{mover.Value} Option '{ShortDef(context, new HeadlessEntityId(optId))}' is still in the battle area after resolving");

        // (#6b) the attacker must be suspended after declaring an attack (unless still in a pending sub-step).
        if (action.ActionType == HeadlessActionTypes.DeclareAttack && !st.HasPendingChoice
            && action.Parameters.TryGetValue(HeadlessActionParameterKeys.AttackerId, out object? aObj) && aObj is string aId
            && InZone(zones, mover, ChoiceZone.BattleArea, new HeadlessEntityId(aId))
            && context.CardInstanceRepository.TryGetInstance(new HeadlessEntityId(aId), out CardInstanceRecord? aInst) && aInst is not null
            && !ReadFlag(aInst.Metadata, "isSuspended"))
            Flag("ATTACKER_NOT_SUSPENDED", turn, $"P{mover.Value} attacker '{ShortDef(context, new HeadlessEntityId(aId))}' is not suspended after attacking");

        // (#10) Digi-Eggs must never leak into the hand or security stack (they live in the digitama deck).
        foreach (var pl in players)
            foreach (var z in new[] { ChoiceZone.Hand, ChoiceZone.Security })
                foreach (var id in zones.GetCards(pl, z))
                    if (string.Equals(DefType(db, context, id), "DigiEgg", StringComparison.Ordinal))
                    { Flag("EGG_IN_HAND_OR_SECURITY", turn, $"P{pl.Value} {z} holds DigiEgg '{ShortDef(context, id)}'"); goto secInfo; }
        secInfo: ;

        // security bookkeeping: a direct player-attack should consume a security card UNLESS the defender
        // had no security (the attack instead ends the game) or the attack was intercepted. Categorize the
        // non-consuming cases to confirm none are unexplained (a silent security-skip would be a real bug).
        if (directAttack)
        {
            directAttacks++;
            int defSecAfter = zones.GetCards(defender, ChoiceZone.Security).Count;
            if (defSecAfter < defSecBefore) securityDrops++;
            else if (defSecBefore == 0) secNoDropZeroBefore++;      // no security to lose -> direct loss
            else if (st.IsTerminal) secNoDropTerminal++;            // game ended on this attack
            else
            {
                secNoDropUnexplained++;                             // <-- investigate each
                int defenderDigimon = zones.GetCards(defender, ChoiceZone.BattleArea)
                    .Count(id => string.Equals(DefType(db, context, id), "Digimon", StringComparison.Ordinal));
                string evs = string.Join(",", st.Events.Select(e => e.Type).Distinct());
                unexplained.Add($"{tag} T{turn}: defSec {defSecBefore}->{defSecAfter}, defenderDigimon={defenderDigimon}, events=[{evs}]");
            }
        }

        // (#4) a DigiEgg must never sit in the battle area (Tamers/Options legitimately stay there with
        // level 0, so the test is the card TYPE, not its level).
        foreach (var pl in players)
            foreach (var id in zones.GetCards(pl, ChoiceZone.BattleArea))
                if (string.Equals(DefType(db, context, id), "DigiEgg", StringComparison.Ordinal))
                { Flag("EGG_IN_BATTLE", turn, $"P{pl.Value} battle area holds DigiEgg '{ShortDef(context, id)}'"); goto nextStep; }
        nextStep: ;
    }

    if (match.IsTerminal())
    {
        terminalGames++;
        MatchResult r = match.GetResult();
        string w = r.IsDraw ? "draw" : (r.WinnerId?.Value.ToString() ?? "?");
        winners[w] = winners.GetValueOrDefault(w) + 1;
    }
    return steps;
}

// --- helpers -------------------------------------------------------------
static bool IsCostedPlay(string t) =>
    t is HeadlessActionTypes.PlayCard or HeadlessActionTypes.Digivolve
        or HeadlessActionTypes.SpecialPlay or HeadlessActionTypes.ActivateOption;

static bool ReadFlag(IReadOnlyDictionary<string, object?> m, string k) =>
    m.TryGetValue(k, out object? v) && v is bool b && b;

static bool InZone(IZoneStateReader zones, HeadlessPlayerId p, ChoiceZone z, HeadlessEntityId id) =>
    zones.GetCards(p, z).Contains(id);

static int DefLevel(CardDatabase db, EngineContext ctx, HeadlessEntityId instanceId)
{
    if (ctx.CardInstanceRepository.TryGetInstance(instanceId, out CardInstanceRecord? inst) && inst is not null
        && db.TryGetCard(inst.DefinitionId, out CardRecord? c) && c is not null
        && c.Metadata.TryGetValue("level", out object? lv) && lv is int i)
        return i;
    return -1;
}

static string DefType(CardDatabase db, EngineContext ctx, HeadlessEntityId instanceId)
{
    if (ctx.CardInstanceRepository.TryGetInstance(instanceId, out CardInstanceRecord? inst) && inst is not null
        && db.TryGetCard(inst.DefinitionId, out CardRecord? c) && c is not null)
        return c.CardType ?? "?";
    return "?";
}

static string ShortDef(EngineContext ctx, HeadlessEntityId instanceId) =>
    ctx.CardInstanceRepository.TryGetInstance(instanceId, out CardInstanceRecord? inst) && inst is not null
        ? inst.DefinitionId.Value : instanceId.Value;

