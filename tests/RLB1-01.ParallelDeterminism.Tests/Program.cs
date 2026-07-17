using System.Security.Cryptography;
using System.Text;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// RLB1-01 (design doc rl_env_design_2026-07-17.md §5 / D5): in-process PARALLEL determinism witness.
// The vectorization premise — AsyncLocal ambient (AmbientMatchContext) + ConditionalWeakTable-per-context
// mirror globals — is structurally sound but was never EXERCISED: the dormant scenario-based verifiers
// (HeadlessDeterminismVerifier / HeadlessBatchParallelDeterminismVerifier) have zero call sites and drive
// the OLD step cadence. This witness is the pump-era replacement: N full masked-random self-play matches
// (real ST1/ST2 starter decks, pump-driven DcgoMatch) run CONCURRENTLY as tasks, and every match's whole
// trajectory digest must be bit-identical to the serial run of the same seed — including when matches
// with DIFFERENT seeds run interleaved (cross-contamination probe: any ambient/static leak between
// concurrently-stepping matches shifts a trajectory and breaks its digest).
//
// Digest material is Guid-free (RD-RLENV-04: token/grant ids are Guid.NewGuid) — action ids on these
// starter decks are seed-deterministic (proven by G13-003 seed-replay), and the appended state material
// is counts/values only (turn, phase, memory, zone counts, winner).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

(int EnvSeed, int PolicySeed) seedA = (101, 11);
(int EnvSeed, int PolicySeed) seedB = (202, 22);

// Serial baselines (also reused by every parallel case as the expected digests).
string serialA = await RolloutDigestAsync(seedA);
string serialB = await RolloutDigestAsync(seedB);

var tests = new (string Name, Func<Task> Body)[]
{
    ("serial replay of the same seed is digest-identical; a different seed diverges (digest is discriminative)", SerialDigestSanity),
    ("N same-seed matches run task-parallel are all digest-identical to the serial run", ParallelSameSeedIdentical),
    ("mixed-seed matches run interleaved keep their own trajectories (no cross-contamination)", ParallelMixedSeedNoCrossContamination),
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

// --- Tests ----------------------------------------------------------------

async Task SerialDigestSanity()
{
    string replayA = await RolloutDigestAsync(seedA);
    AssertEqual(serialA, replayA, "same seed, serial replay: digest identical");
    AssertTrue(serialA != serialB, "different seeds produce different trajectory digests");
}

async Task ParallelSameSeedIdentical()
{
    const int N = 4;
    Task<string>[] runs = Enumerable.Range(0, N)
        .Select(_ => Task.Run(() => RolloutDigestAsync(seedA)))
        .ToArray();
    string[] digests = await Task.WhenAll(runs);

    for (int i = 0; i < N; i++)
    {
        AssertEqual(serialA, digests[i], $"parallel run {i + 1}/{N} matches the serial digest");
    }
}

async Task ParallelMixedSeedNoCrossContamination()
{
    // Interleave A,B,A,B,A,B so differently-seeded matches step concurrently on the thread pool.
    // Any state bleeding between matches (ambient context, mirror-layer static, shared RNG) shifts a
    // trajectory and breaks that run's digest against its own-seed serial baseline.
    (int EnvSeed, int PolicySeed)[] lineup = { seedA, seedB, seedA, seedB, seedA, seedB };
    Task<string>[] runs = lineup
        .Select(seed => Task.Run(() => RolloutDigestAsync(seed)))
        .ToArray();
    string[] digests = await Task.WhenAll(runs);

    for (int i = 0; i < lineup.Length; i++)
    {
        string expected = lineup[i] == seedA ? serialA : serialB;
        AssertEqual(expected, digests[i], $"interleaved run {i + 1} (seed {lineup[i].EnvSeed}) kept its own trajectory");
    }
}

// --- Rollout --------------------------------------------------------------

// One full masked-random pump self-play match (ST1 vs ST2 starter decks), digesting the whole
// trajectory: every applied action id, the post-step turn/phase/memory, and the terminal outcome plus
// final zone counts. Mirrors the G13-003 rollout core on an isolated per-call engine context.
async Task<string> RolloutDigestAsync((int EnvSeed, int PolicySeed) seed)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: seed.EnvSeed);
    var db = (CardDatabase)context.CardRepository;
    CardBaseEntityLoader.LoadInto(db);

    StarterDecks.StarterDeck d1 = StarterDecks.Get("ST1"), d2 = StarterDecks.Get("ST2");
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[]
        {
            new PlayerDeckSetup(P1, d1.MainDefinitions, d1.DigitamaDefinitions),
            new PlayerDeckSetup(P2, d2.MainDefinitions, d2.DigitamaDefinitions),
        }, firstPlayerId: P1);
    MatchConfig config = MatchConfig.Create(new[] { P1, P2 }, randomSeed: seed.EnvSeed, setup: setup);

    DcgoMatch match = DcgoMatch.CreatePumpDriven(context, new EngineTrace());
    var env = new HeadlessRlEnvironment(match);
    await env.InitializeAsync(config);

    var rng = new Random(seed.PolicySeed);
    var players = new[] { P1, P2 };
    var material = new StringBuilder();
    material.Append("seed:").Append(seed.EnvSeed).Append('/').Append(seed.PolicySeed).Append('\n');

    for (int step = 0; step < 800 && !match.IsTerminal(); step++)
    {
        HeadlessPlayerId mover = default;
        bool found = false;
        foreach (var p in players)
        {
            if (match.GetLegalActions(p).Count > 0) { mover = p; found = true; break; }
        }

        if (!found)
        {
            material.Append("deadlock\n");
            break;
        }

        IReadOnlyList<LegalAction> legal = match.GetLegalActions(mover);
        LegalAction action = legal[rng.Next(legal.Count)];
        RlStepResult st = await env.StepAsync(action);

        HeadlessTurnState turn = match.Context.TurnController.Current;
        material.Append(action.Id.Value)
            .Append('|').Append(mover.Value)
            .Append('|').Append(turn.TurnNumber)
            .Append('|').Append(turn.Phase)
            .Append('|').Append(match.Context.MemoryController.Current.Current)
            .Append('|').Append(st.IsTerminal ? 'T' : '.')
            .Append('\n');
    }

    MatchResult result = match.GetResult();
    material.Append("result:")
        .Append(result.WinnerId?.Value.ToString() ?? "-")
        .Append('/').Append(result.IsDraw)
        .Append('/').Append(result.Reason ?? "-")
        .Append('\n');

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
    return Convert.ToHexString(hash);
}

// --- Asserts ---------------------------------------------------------------

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
    }
}

static void AssertTrue(bool value, string label)
{
    if (!value)
    {
        throw new InvalidOperationException($"{label}: expected true.");
    }
}
