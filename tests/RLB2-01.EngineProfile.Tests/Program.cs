using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// RLB2-01 (design doc rl_env_design_2026-07-17.md §7 / RD-RLENV-03, B2): ONE-OFF ENGINE PROFILE PROBE.
// This is deliberately a tests/-side probe: NO instrumentation is added to engine sources. Two jobs:
//
//   1. `digest` mode — SEMANTICS-INVARIANCE WITNESS for substrate-only performance work. For seeds
//      1000..1009 it prints one SHA256 per game over the FULL agent-visible trajectory: every chosen
//      action id, the complete sorted legal table of BOTH players at every decision point, the whole
//      3,088-feature encoded observation, the factored mask bits, per-step turn/phase/memory, and the
//      terminal result + final zone counts. Any semantic drift the RL surface could ever observe
//      (legality, observation, cadence, outcome) changes the digest. Run before and after an
//      optimization; the 10 lines must be bit-identical.
//
//   2. `time` mode — public-surface stopwatch decomposition per step TYPE (choice-resolution step vs
//      main-phase action step) plus the read-surface costs (GetLegalActions x2, Observe/Encode),
//      complementing the dotnet-trace sampling profile with agent-step-level attribution.
//
// Usage: dotnet run [digest|time|both] [games]   (default: both 10, seeds 1000..1009 as pinned in §7.1)

string mode = args.Length > 0 ? args[0] : "both";
int games = args.Length > 1 && int.TryParse(args[1], out int parsed) ? parsed : 10;
const int BaseSeed = 1000;
const int StepCap = 2000;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

if (mode is "digest" or "both")
{
    Console.WriteLine("== trajectory digests (semantics-invariance witness) ==");
    for (int i = 0; i < games; i++)
    {
        int seed = BaseSeed + i;
        (string digest, int steps) = await RunDigestGameAsync(seed);
        Console.WriteLine($"seed={seed} steps={steps} digest={digest}");
    }
}

if (mode is "time" or "both")
{
    Console.WriteLine("\n== stopwatch decomposition (per agent-step type) ==");
    var agg = new TimingAgg();
    // Warmup excluded.
    await RunTimedGameAsync(BaseSeed - 1, new TimingAgg());
    for (int i = 0; i < games; i++)
    {
        await RunTimedGameAsync(BaseSeed + i, agg);
    }

    agg.Print();
}

Console.WriteLine("\nPASS RLB2-01 probe complete.");

async Task<(string Digest, int Steps)> RunDigestGameAsync(int seed)
{
    (DcgoMatch match, HeadlessRlEnvironment env) = await CreateMatchAsync(seed);
    var rng = new Random(seed);
    var players = new[] { P1, P2 };
    var material = new StringBuilder();
    material.Append("seed:").Append(seed).Append('\n');
    int steps = 0;

    RlStepResult st = env.Observe();
    while (steps < StepCap && !match.IsTerminal())
    {
        // Full both-player legal table, sorted: witnesses the legality surface bit-for-bit.
        List<string> table = new();
        foreach (var p in players)
        {
            foreach (LegalAction a in match.GetLegalActions(p))
            {
                table.Add($"{p.Value}:{a.Id.Value}:{a.ActionType}");
            }
        }

        table.Sort(StringComparer.Ordinal);
        material.Append("mask:").Append(string.Join(",", table)).Append('\n');

        HeadlessPlayerId mover = default;
        bool found = false;
        foreach (var p in players)
        {
            if (match.GetLegalActions(p).Count > 0) { mover = p; found = true; break; }
        }

        if (!found) { material.Append("deadlock\n"); break; }

        IReadOnlyList<LegalAction> legal = match.GetLegalActions(mover);
        LegalAction action = legal[rng.Next(legal.Count)];
        st = await env.StepAsync(action);
        steps++;

        HeadlessTurnState turn = match.Context.TurnController.Current;
        material.Append("act:").Append(action.Id.Value)
            .Append('|').Append(mover.Value)
            .Append('|').Append(turn.TurnNumber)
            .Append('|').Append(turn.Phase)
            .Append('|').Append(match.Context.MemoryController.Current.Current)
            .Append('|').Append(st.IsTerminal ? 'T' : '.')
            .Append('\n');

        // Whole encoded observation + factored mask: witnesses the observation surface bit-for-bit.
        double[] obs = st.ObservationVector;
        material.Append("obs:");
        foreach (double v in obs)
        {
            material.Append(v.ToString("R")).Append(';');
        }

        material.Append('\n');
        double[] fmask = st.FactoredActionMaskVector;
        material.Append("fmask:");
        for (int b = 0; b < fmask.Length; b++)
        {
            if (fmask[b] != 0) { material.Append(b).Append(';'); }
        }

        material.Append('\n');
        if (st.IsTerminal) { break; }
    }

    MatchResult result = match.GetResult();
    material.Append("result:").Append(result.WinnerId?.Value.ToString() ?? "-")
        .Append('/').Append(result.IsDraw).Append('/').Append(result.Reason ?? "-").Append('\n');
    if (match.Context.ZoneMover is IZoneStateReader zones)
    {
        foreach (HeadlessPlayerId p in players)
        {
            foreach (ChoiceZone zone in new[]
            {
                ChoiceZone.Hand, ChoiceZone.Library, ChoiceZone.Security,
                ChoiceZone.BreedingArea, ChoiceZone.BattleArea, ChoiceZone.Trash,
            })
            {
                material.Append('z').Append(p.Value).Append(':').Append(zone)
                    .Append('=').Append(zones.GetCards(p, zone).Count).Append('\n');
            }
        }
    }

    byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(material.ToString()));
    return (Convert.ToHexString(hash), steps);
}

async Task RunTimedGameAsync(int seed, TimingAgg agg)
{
    var sw = Stopwatch.StartNew();
    (DcgoMatch match, HeadlessRlEnvironment env) = await CreateMatchAsync(seed);
    agg.InitMs += sw.Elapsed.TotalMilliseconds;

    var rng = new Random(seed);
    var players = new[] { P1, P2 };
    int steps = 0;

    while (steps < StepCap && !match.IsTerminal())
    {
        bool isChoiceStep = match.HasPendingChoice();

        sw.Restart();
        HeadlessPlayerId mover = default;
        bool found = false;
        foreach (var p in players)
        {
            if (match.GetLegalActions(p).Count > 0) { mover = p; found = true; break; }
        }

        if (!found) { break; }
        IReadOnlyList<LegalAction> legal = match.GetLegalActions(mover);
        agg.MaskMs += sw.Elapsed.TotalMilliseconds;
        agg.MaskCalls++;

        LegalAction action = legal[rng.Next(legal.Count)];
        sw.Restart();
        RlStepResult st = await env.StepAsync(action);
        double ms = sw.Elapsed.TotalMilliseconds;
        steps++;

        if (isChoiceStep) { agg.ChoiceStepMs += ms; agg.ChoiceSteps++; }
        else { agg.ActionStepMs += ms; agg.ActionSteps++; agg.RecordActionType(action.ActionType, ms); }

        if (st.IsTerminal) { break; }
    }

    // Read-surface unit costs at the final decision-ish state (representative sampling).
    sw.Restart();
    _ = env.Observe();
    agg.ObserveMs += sw.Elapsed.TotalMilliseconds;
    agg.ObserveCalls++;
}

async Task<(DcgoMatch, HeadlessRlEnvironment)> CreateMatchAsync(int seed)
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
        }, firstPlayerId: P1);
    MatchConfig config = MatchConfig.Create(new[] { P1, P2 }, randomSeed: seed, setup: setup);

    DcgoMatch match = DcgoMatch.CreatePumpDriven(context, new EngineTrace());
    var env = new HeadlessRlEnvironment(match);
    await env.InitializeAsync(config);
    return (match, env);
}

sealed class TimingAgg
{
    public double InitMs;
    public double MaskMs;
    public long MaskCalls;
    public double ChoiceStepMs;
    public long ChoiceSteps;
    public double ActionStepMs;
    public long ActionSteps;
    public double ObserveMs;
    public long ObserveCalls;
    private readonly Dictionary<string, (double Ms, long N)> _byActionType = new(StringComparer.Ordinal);

    public void RecordActionType(string actionType, double ms)
    {
        _byActionType.TryGetValue(actionType, out (double Ms, long N) cur);
        _byActionType[actionType] = (cur.Ms + ms, cur.N + 1);
    }

    public void Print()
    {
        long steps = ChoiceSteps + ActionSteps;
        double stepMs = ChoiceStepMs + ActionStepMs;
        Console.WriteLine($"agent steps        : {steps} (choice {ChoiceSteps}, action {ActionSteps})");
        Console.WriteLine($"env.StepAsync total: {stepMs:F0} ms  (avg {stepMs / Math.Max(1, steps):F1} ms/step)");
        Console.WriteLine($"  choice-resolution steps: {ChoiceStepMs:F0} ms (avg {ChoiceStepMs / Math.Max(1, ChoiceSteps):F1} ms/step)");
        Console.WriteLine($"  main/action steps      : {ActionStepMs:F0} ms (avg {ActionStepMs / Math.Max(1, ActionSteps):F1} ms/step)");
        Console.WriteLine($"caller GetLegalActions (2 players/step): {MaskMs:F0} ms over {MaskCalls} decision points (avg {MaskMs / Math.Max(1, MaskCalls):F1} ms)");
        Console.WriteLine($"env.Observe (obs+mask+encode) unit     : avg {ObserveMs / Math.Max(1, ObserveCalls):F1} ms over {ObserveCalls} calls");
        Console.WriteLine($"match init+deck load                   : avg {InitMs / Math.Max(1, ObserveCalls):F1} ms");
        Console.WriteLine("per action type (avg ms, n):");
        foreach (var kv in _byActionType.OrderByDescending(kv => kv.Value.Ms))
        {
            Console.WriteLine($"  {kv.Key,-22} {kv.Value.Ms / kv.Value.N,8:F1} ms  n={kv.Value.N}");
        }
    }
}
