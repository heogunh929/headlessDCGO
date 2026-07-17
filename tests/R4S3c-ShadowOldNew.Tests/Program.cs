using System.Text;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using Cec = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

// (R4 batch S3c-a — docs/audit/r4_tsm_s1_design_2026-07-16.md 결정 3: shadow 게이트 경계 재정의)
// ============================================================================================================
// OLD-vs-NEW shadow: the two drivers speak DIFFERENT action currencies (OLD = the invented AdvancePhase /
// EndTurn step cadence; NEW = the AS-IS pump with its interactive stops), so the P4 lockstep protocol
// (same action, per-step digest) is undefined across them. The redefined protocol (decision 3):
//   * ONE deterministic seeded POLICY makes GAME-level decisions (keep mulligan / hatch / attack X / play Y /
//     pass) from the observed state, and each side TRANSLATES the decision into its own currency;
//   * state digests are compared at every INTERACTIVE-STOP boundary both models share — the MAIN-WAIT of each
//     turn (OLD: (Main, PhaseStart) with no pending choice; NEW: the pump's main selection park) — plus the
//     terminal verdict/digest;
//   * RNG parity precondition (asserted per game): deck shuffles happen in the SHARED setup path, draws pop
//     deck tops without consuming RNG, so both sides deal identical opening hands.
// A divergence is REPORTED with the boundary, the digest diff, and the decision trail — the S3c-c analysis
// input, not auto-judged here.
// ============================================================================================================

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
int[] seeds = { 101, 202, 303, 404, 505 };
const int TurnCap = 40;

var reports = new List<GameReport>();
foreach (int seed in seeds)
{
    reports.Add(await RunShadowGameAsync(seed));
}

int identical = reports.Count(r => r.Identical);
Console.WriteLine($"\n=== S3c-a shadow OLD-vs-NEW: {identical}/{reports.Count} games boundary-identical ===");
foreach (GameReport r in reports)
{
    Console.WriteLine($"seed {r.Seed}: {(r.Identical ? "IDENTICAL" : "DIVERGED")} " +
        $"turns:{r.TurnsCompared} terminalOld:{r.OldTerminal} terminalNew:{r.NewTerminal}");
    if (!r.Identical)
    {
        Console.WriteLine($"  first divergence at {r.DivergenceBoundary}:");
        Console.WriteLine(r.DivergenceDiff);
    }
}

// The harness itself must complete every game (a wedge/exception is a harness failure); divergences are
// FINDINGS for the S3c-c cutover analysis, reported above and summarized in the exit line.
if (reports.Any(r => !r.Completed))
{
    Console.Error.WriteLine("FAIL: a shadow game did not complete (wedge/exception).");
    foreach (GameReport r in reports.Where(r => !r.Completed))
    {
        Console.Error.WriteLine($"  seed {r.Seed}: {r.HarnessError}");
    }
    Environment.Exit(1);
}

Console.WriteLine($"\n{reports.Count} shadow game(s) completed; {identical} identical, {reports.Count - identical} diverged (findings above).");

// --- Shadow game ----------------------------------------------------------

async Task<GameReport> RunShadowGameAsync(int seed)
{
    var report = new GameReport { Seed = seed };
    try
    {
        DcgoMatch oldMatch = await NewOldMatchAsync(seed);
        DcgoMatch newMatch = await NewPumpMatchAsync(seed);

        // NEW: start the pump (deals hands, opens mulligan); OLD: setup already dealt + opened mulligan.
        await StepAsync(newMatch);

        // RNG-parity precondition: identical opening hands.
        string oldHands = HandsDigest(oldMatch);
        string newHands = HandsDigest(newMatch);
        if (oldHands != newHands)
        {
            report.Identical = false;
            report.DivergenceBoundary = "opening hands (RNG parity)";
            report.DivergenceDiff = DiffLines(oldHands, newHands);
            report.Completed = true;
            return report;
        }

        // Mulligan: keep on both (OLD: skip-actions from the legal table; NEW: same).
        for (int i = 0; i < 2; i++)
        {
            await ResolvePendingAsync(oldMatch, skip: true);
            await ResolvePendingAsync(newMatch, skip: true);
        }

        // Decision loop: drive both sides to their CURRENT main-wait (attacks and cheap plays return to the
        // same turn's main), compare the boundary digest whenever a NEW turn's main is first reached, and take
        // ONE policy decision per iteration — until a terminal or the cap.
        int lastComparedTurn = 0;
        for (int step = 0; step < 600; step++)
        {
            bool oldAtMain = await DriveOldToMainWaitAsync(oldMatch);
            bool newAtMain = await DriveNewToMainWaitAsync(newMatch);

            if (oldMatch.IsTerminal() || newMatch.IsTerminal())
            {
                if (!oldMatch.IsTerminal()) { await DriveOldToMainWaitAsync(oldMatch); }
                if (!newMatch.IsTerminal()) { await DriveNewToMainWaitAsync(newMatch); }

                report.OldTerminal = oldMatch.IsTerminal();
                report.NewTerminal = newMatch.IsTerminal();
                string oldFinal = Digest(oldMatch);
                string newFinal = Digest(newMatch);
                if (oldFinal != newFinal)
                {
                    report.Identical = false;
                    report.DivergenceBoundary = $"terminal (around turn {lastComparedTurn})";
                    report.DivergenceDiff = DiffLines(oldFinal, newFinal);
                }

                report.Completed = true;
                return report;
            }

            if (!oldAtMain || !newAtMain)
            {
                report.Identical = false;
                report.DivergenceBoundary = $"main-wait reachability after turn {lastComparedTurn} (old:{oldAtMain} new:{newAtMain})";
                report.DivergenceDiff = DiffLines(Digest(oldMatch), Digest(newMatch));
                report.Completed = true;
                return report;
            }

            int oldTurn = oldMatch.Context.TurnController.Current.TurnNumber;
            int newTurn = newMatch.Context.TurnController.Current.TurnNumber;
            if (oldTurn != newTurn)
            {
                report.Identical = false;
                report.DivergenceBoundary = $"turn desync (old:{oldTurn} new:{newTurn})";
                report.DivergenceDiff = DiffLines(Digest(oldMatch), Digest(newMatch));
                report.Completed = true;
                return report;
            }

            if (oldTurn > lastComparedTurn)
            {
                string oldDigest = Digest(oldMatch);
                string newDigest = Digest(newMatch);
                if (oldDigest != newDigest)
                {
                    report.Identical = false;
                    report.DivergenceBoundary = $"turn {oldTurn} main-wait";
                    report.DivergenceDiff = DiffLines(oldDigest, newDigest);
                    report.Completed = true;
                    return report;
                }

                lastComparedTurn = oldTurn;
                report.TurnsCompared = oldTurn;
                if (oldTurn > TurnCap)
                {
                    break;   // long enough — the boundary trail up to the cap is the finding.
                }
            }

            // ONE policy decision, executed on both sides in their own currency.
            PolicyDecision decision = DecideMain(newMatch);
            await ApplyMainOnOldAsync(oldMatch, decision);
            await ApplyMainOnNewAsync(newMatch, decision);
        }

        report.Completed = true;
        return report;
    }
    catch (Exception ex)
    {
        report.Completed = false;
        report.HarnessError = $"{ex.GetType().Name}: {ex.Message}";
        return report;
    }
}

// --- Policy ---------------------------------------------------------------

PolicyDecision DecideMain(DcgoMatch match)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    HeadlessPlayerId turnPlayer = match.Context.TurnController.Current.TurnPlayerId!.Value;
    var player = new Cec.Player(match.Context, turnPlayer);

    Cec.Permanent? attacker = player.GetFieldPermanents().FirstOrDefault(p => p.CanAttack(null));
    if (attacker is not null)
    {
        return new PolicyDecision(PolicyKind.Attack, attacker.InstanceId);
    }

    if (player.GetFieldPermanents().Count < 4)
    {
        Cec.CardSource? playable = player.HandCards
            .Where(c => c.IsDigimon && c.CanPlayFromHandDuringMainPhase
                && player.MaxMemoryCost >= c.PayingCost(HeadlessDCGO.Engine.Assets.Scripts.Script.SelectCardEffect.Root.Hand, null, checkAvailability: true))
            .OrderBy(c => c.PayingCost(HeadlessDCGO.Engine.Assets.Scripts.Script.SelectCardEffect.Root.Hand, null, checkAvailability: true))
            .ThenBy(c => c.InstanceId.Value, StringComparer.Ordinal)
            .FirstOrDefault();
        if (playable is not null)
        {
            return new PolicyDecision(PolicyKind.Play, playable.InstanceId);
        }
    }

    return new PolicyDecision(PolicyKind.Pass, default);
}

async Task ApplyMainOnNewAsync(DcgoMatch match, PolicyDecision decision)
{
    HeadlessPlayerId turnPlayer = match.Context.TurnController.Current.TurnPlayerId!.Value;
    switch (decision.Kind)
    {
        case PolicyKind.Attack:
            await SendAsync(match, turnPlayer, HeadlessActionTypes.DeclareAttack,
                new Dictionary<string, object?> { [HeadlessActionParameterKeys.AttackerId] = decision.Target.Value });
            break;
        case PolicyKind.Play:
            await SendAsync(match, turnPlayer, HeadlessActionTypes.PlayCard,
                new Dictionary<string, object?> { [HeadlessActionParameterKeys.CardId] = decision.Target.Value });
            break;
        default:
            await SendAsync(match, turnPlayer, HeadlessActionTypes.Pass, new Dictionary<string, object?>());
            break;
    }
}

async Task ApplyMainOnOldAsync(DcgoMatch match, PolicyDecision decision)
{
    HeadlessPlayerId turnPlayer = match.Context.TurnController.Current.TurnPlayerId!.Value;
    switch (decision.Kind)
    {
        case PolicyKind.Attack:
        {
            LegalAction? attack = Legal(match, turnPlayer).FirstOrDefault(a =>
                HeadlessActionTypes.Normalize(a.ActionType) == HeadlessActionTypes.NormalizedDeclareAttack
                && ReadId(a, HeadlessActionParameterKeys.AttackerId) == decision.Target.Value
                && ReadId(a, HeadlessActionParameterKeys.AttackTargetId) is null);   // security attack
            if (attack is null) throw new InvalidOperationException($"OLD legal table offers no security attack for {decision.Target.Value}");
            await ApplyAsync(match, attack);
            break;
        }
        case PolicyKind.Play:
        {
            LegalAction? play = Legal(match, turnPlayer).FirstOrDefault(a =>
                HeadlessActionTypes.Normalize(a.ActionType) == HeadlessActionTypes.NormalizedPlayCard
                && ReadId(a, HeadlessActionParameterKeys.CardId) == decision.Target.Value);
            if (play is null) throw new InvalidOperationException($"OLD legal table offers no play for {decision.Target.Value}");
            await ApplyAsync(match, play);
            break;
        }
        default:
        {
            LegalAction? pass = Legal(match, turnPlayer).FirstOrDefault(a =>
                HeadlessActionTypes.Normalize(a.ActionType) == HeadlessActionTypes.NormalizedPass);
            if (pass is null) throw new InvalidOperationException("OLD legal table offers no Pass");
            await ApplyAsync(match, pass);
            break;
        }
    }
}

// --- Per-side turn drivers --------------------------------------------------

// Drive OLD to the main-wait of the given turn: resolve pending choices (breeding-free model — OLD's breeding
// is action-based), send AdvancePhase through the early phases, complete a memory-pass with EndTurn, hatch by
// policy when offered.
async Task<bool> DriveOldToMainWaitAsync(DcgoMatch match)
{
    for (int i = 0; i < 200; i++)
    {
        if (match.IsTerminal()) return false;
        HeadlessTurnState t = match.Context.TurnController.Current;

        if (match.HasPendingChoice())
        {
            await ResolvePendingAsync(match, skip: false);
            continue;
        }

        if (t.Phase == HeadlessPhase.Main && t.StepCursor == TurnStepCursor.AwaitingMemoryPassEnd)
        {
            await SendAsync(match, t.TurnPlayerId!.Value, HeadlessActionTypes.EndTurn, new Dictionary<string, object?>());
            continue;
        }

        if (t.Phase == HeadlessPhase.Main && t.StepCursor == TurnStepCursor.PhaseStart)
        {
            return true;
        }

        HeadlessPlayerId turnPlayer = t.TurnPlayerId!.Value;
        // Breeding: hatch by policy when the action is offered (digitama available + breeding empty).
        if (t.Phase == HeadlessPhase.Breeding)
        {
            LegalAction? hatch = Legal(match, turnPlayer).FirstOrDefault(a =>
                HeadlessActionTypes.Normalize(a.ActionType) == HeadlessActionTypes.NormalizedHatchDigitama);
            if (hatch is not null)
            {
                await ApplyAsync(match, hatch);
                continue;
            }
        }

        LegalAction? advance = Legal(match, turnPlayer).FirstOrDefault(a =>
            HeadlessActionTypes.Normalize(a.ActionType) == HeadlessActionTypes.NormalizedAdvancePhase);
        if (advance is not null)
        {
            await ApplyAsync(match, advance);
            continue;
        }

        await StepAsync(match);
    }

    return false;
}

// Drive NEW to the main-wait of the given turn: step the pump, resolve choices (breeding = act, matching the
// OLD hatch policy).
async Task<bool> DriveNewToMainWaitAsync(DcgoMatch match)
{
    for (int i = 0; i < 200; i++)
    {
        if (match.IsTerminal()) return false;
        HeadlessTurnState t = match.Context.TurnController.Current;

        if (match.HasPendingChoice())
        {
            bool act = true;   // breeding: hatch by policy (mirrors the OLD hatch); other choices: act (first candidate)
            await ResolvePendingAsync(match, skip: !act);
            continue;
        }

        if (t.Phase == HeadlessPhase.Main && !match.HasPendingChoice()
            && t.StepCursor == TurnStepCursor.PhaseStart)
        {
            return true;
        }

        await StepAsync(match);
    }

    return false;
}

// --- Match builders ---------------------------------------------------------

async Task<DcgoMatch> NewOldMatchAsync(int seed)
{
    EngineContext context = NewContext(seed);
    StarterDecks.StarterDeck d1 = StarterDecks.Get("ST1"), d2 = StarterDecks.Get("ST2");
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[]
        {
            new PlayerDeckSetup(P1, d1.MainDefinitions, d1.DigitamaDefinitions),
            new PlayerDeckSetup(P2, d2.MainDefinitions, d2.DigitamaDefinitions),
        },
        firstPlayerId: P1,
        enableMulligan: true);
    MatchConfig config = MatchConfig.Create(new[] { P1, P2 }, randomSeed: seed, setup: setup);

    DcgoMatch match = new(context, new EngineTrace());
    await match.InitializeAsync(config);
    return match;
}

async Task<DcgoMatch> NewPumpMatchAsync(int seed)
{
    EngineContext context = NewContext(seed);
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

static EngineContext NewContext(int seed)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: seed);
    var db = (CardDatabase)context.CardRepository;
    CardBaseEntityLoader.LoadInto(db);
    return context;
}

// --- Plumbing ---------------------------------------------------------------

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
        throw new InvalidOperationException($"no ResolveChoice action for pending {match.Context.ChoiceController.PendingRequest!.Type}");
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

async Task SendAsync(DcgoMatch match, HeadlessPlayerId player, string actionType, Dictionary<string, object?> parameters)
{
    var action = new LegalAction(
        new HeadlessEntityId($"shadow:{actionType}:{parameters.GetValueOrDefault(HeadlessActionParameterKeys.CardId) ?? parameters.GetValueOrDefault(HeadlessActionParameterKeys.AttackerId) ?? "x"}:{player.Value}"),
        player,
        actionType,
        parameters);
    await ApplyAsync(match, action);
}

static async Task StepAsync(DcgoMatch match)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.StepAsync();
}

static IReadOnlyList<LegalAction> Legal(DcgoMatch match, HeadlessPlayerId player)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    return match.GetLegalActions(player);
}

static string? ReadId(LegalAction action, string key)
{
    if (action.Parameters.TryGetValue(key, out object? raw) && raw is not null)
    {
        return raw switch
        {
            HeadlessEntityId id => id.Value,
            string s when !string.IsNullOrEmpty(s) => s,
            _ => null,
        };
    }

    return null;
}

static string HandsDigest(DcgoMatch match)
{
    var sb = new StringBuilder();
    foreach (HeadlessPlayerId p in new[] { new HeadlessPlayerId(1), new HeadlessPlayerId(2) })
    {
        if (match.Context.ZoneMover is IZoneStateReader zones)
        {
            sb.AppendLine($"p{p.Value}:hand:{string.Join(",", zones.GetCards(p, ChoiceZone.Hand).Select(c => c.Value).OrderBy(v => v, StringComparer.Ordinal))}");
        }
    }

    return sb.ToString();
}

static string Digest(DcgoMatch match)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    HeadlessTurnState t = match.Context.TurnController.Current;
    var sb = new StringBuilder();
    sb.AppendLine($"turn:{t.TurnNumber} player:{t.TurnPlayerId?.Value} phase:{t.Phase}");
    sb.AppendLine($"memory:{match.Context.MemoryController.Current.Current}");
    sb.AppendLine($"terminal:{match.IsTerminal()} winner:{match.GetResult().WinnerId?.Value}");
    foreach (HeadlessPlayerId p in new[] { new HeadlessPlayerId(1), new HeadlessPlayerId(2) })
    {
        foreach (ChoiceZone z in new[] { ChoiceZone.Hand, ChoiceZone.Library, ChoiceZone.Security, ChoiceZone.BreedingArea, ChoiceZone.BattleArea, ChoiceZone.Trash })
        {
            if (match.Context.ZoneMover is IZoneStateReader zones)
            {
                sb.AppendLine($"p{p.Value}:{z}:{string.Join(",", zones.GetCards(p, z).Select(c => c.Value))}");
            }
        }
    }

    return sb.ToString();
}

static string DiffLines(string a, string b)
{
    string[] la = a.Split('\n');
    string[] lb = b.Split('\n');
    var sb = new StringBuilder();
    int n = Math.Max(la.Length, lb.Length);
    for (int i = 0; i < n; i++)
    {
        string ra = i < la.Length ? la[i] : "<none>";
        string rb = i < lb.Length ? lb[i] : "<none>";
        if (ra != rb)
        {
            sb.AppendLine($"  OLD| {ra}");
            sb.AppendLine($"  NEW| {rb}");
        }
    }

    return sb.ToString();
}

enum PolicyKind { Attack, Play, Pass }

readonly record struct PolicyDecision(PolicyKind Kind, HeadlessEntityId Target);

class GameReport
{
    public int Seed;
    public bool Identical = true;
    public bool Completed;
    public int TurnsCompared;
    public bool OldTerminal;
    public bool NewTerminal;
    public string DivergenceBoundary = string.Empty;
    public string DivergenceDiff = string.Empty;
    public string HarnessError = string.Empty;
}
