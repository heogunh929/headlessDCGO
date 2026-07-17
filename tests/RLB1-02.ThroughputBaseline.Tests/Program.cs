using System.Diagnostics;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// RLB1-02 (design doc rl_env_design_2026-07-17.md §7 / D7 B1): throughput BASELINE harness.
// Runs N masked-random pump self-play matches IN-PROCESS (HeadlessRlEnvironment over
// DcgoMatch.CreatePumpDriven, ST1 vs ST2 starter decks, seeds 1000..1000+N-1 for both env and policy)
// and prints per-game agent-steps/wall-ms plus the aggregate steps/sec and the per-game median.
// The numbers are RECORDED in the design doc's §7 (B2 profiling "before" pin), not asserted here —
// wall-clock is machine-dependent, so this harness only fails on a crash / non-terminal game.
//
// Measurement notes:
//   * an agent step = one decision point (mulligan/breeding choice, main-phase action, mid-attack
//     choice) — the pump auto-flows everything between decision points inside env.StepAsync;
//   * in-process = no JSON/stdio transport, isolating the engine cost §7 already attributed the
//     budget to (seat-protocol JSON was measured ms-scale per step);
//   * per-step work includes GetActionMask re-enumeration + observation/factored-mask encoding —
//     the exact surface a trainer consumes (RlStepResult).

// Suite default = 3 games: the suite runner enforces a 180s per-test timeout and games run 3-25s
// each under parallel load. The recorded §7.1 baseline was measured with the full set: `dotnet run 10`.
int games = args.Length > 0 && int.TryParse(args[0], out int parsed) ? parsed : 3;
const int BaseSeed = 1000;
const int StepCap = 2000;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var perGameMs = new List<double>();
var perGameSteps = new List<int>();
int terminals = 0;

// Warmup (JIT + card DB load path) — excluded from the measured window.
await RunGameAsync(BaseSeed - 1);

Stopwatch total = Stopwatch.StartNew();
Console.WriteLine("game  seed   steps  terminal  ms");
for (int i = 0; i < games; i++)
{
    int seed = BaseSeed + i;
    Stopwatch sw = Stopwatch.StartNew();
    (int steps, bool terminal) = await RunGameAsync(seed);
    sw.Stop();

    perGameMs.Add(sw.Elapsed.TotalMilliseconds);
    perGameSteps.Add(steps);
    if (terminal) terminals++;
    Console.WriteLine($"{i + 1,4}  {seed,5} {steps,6}  {(terminal ? "     Y" : "     .")}  {sw.Elapsed.TotalMilliseconds,10:F1}");
}
total.Stop();

double medianMs = Median(perGameMs);
int totalSteps = perGameSteps.Sum();
double stepsPerSec = totalSteps / total.Elapsed.TotalSeconds;

Console.WriteLine();
Console.WriteLine($"games={games} terminal={terminals} totalSteps={totalSteps} wall={total.Elapsed.TotalSeconds:F1}s");
Console.WriteLine($"steps/sec={stepsPerSec:F1}  ms/game median={medianMs:F0} min={perGameMs.Min():F0} max={perGameMs.Max():F0}  steps/game min={perGameSteps.Min()} max={perGameSteps.Max()}");

if (terminals != games)
{
    Console.Error.WriteLine($"\nFAIL RLB1-02: {games - terminals} game(s) did not terminate naturally (cap {StepCap}).");
    Environment.Exit(1);
}

Console.WriteLine("\nPASS RLB1-02: baseline measured (record the numbers in the design doc §7).");

async Task<(int Steps, bool Terminal)> RunGameAsync(int seed)
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

    var rng = new Random(seed);
    var players = new[] { P1, P2 };
    int steps = 0;

    while (steps < StepCap && !match.IsTerminal())
    {
        HeadlessPlayerId mover = default;
        bool found = false;
        foreach (var p in players)
        {
            if (match.GetLegalActions(p).Count > 0) { mover = p; found = true; break; }
        }
        if (!found) break;

        IReadOnlyList<LegalAction> legal = match.GetLegalActions(mover);
        RlStepResult st = await env.StepAsync(legal[rng.Next(legal.Count)]);
        steps++;
        if (st.IsTerminal) break;
    }

    return (steps, match.IsTerminal());
}

static double Median(List<double> values)
{
    var sorted = values.OrderBy(v => v).ToArray();
    int mid = sorted.Length / 2;
    return sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2.0;
}
