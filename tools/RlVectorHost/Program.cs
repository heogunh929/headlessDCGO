using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using HeadlessDCGO.Tools.RlBridgeHost;

// (RL B2 / D5) RlVectorHost — N-way vectorized masked-random self-play over seat protocol v1.
//
// One driver, two transports:
//   --mode procs  : each worker SPAWNS an RlBridgeHost child process and speaks protocol v1 over
//                   stdio — the D5 first-choice vectorization shape (SubprocVecEnv-equivalent:
//                   crash isolation per match, no GIL/thread coupling, parent distributes seeds
//                   and collects result JSONL).
//   --mode tasks  : each worker drives an IN-PROCESS SeatMatchHost (same protocol strings, no
//                   transport). Safety of in-process parallel matches is the RLB1-01 witness
//                   (AsyncLocal ambient + per-context mirror state, digest-identical under
//                   interleaving).
//
// Usage: RlVectorHost [--mode procs|tasks] [--workers N] [--games G(per worker)]
//                     [--base-seed 1000] [--max-steps 2000] [--out results.jsonl]
//                     [--host <RlBridgeHost.dll path>]
// Gate (design doc §7 / B2): aggregate steps/sec at N workers >= 5x the same runner at 1 worker.
//
// (RL B4 / D6) Campaign mode — fuzzing harvest layer over mass random self-play:
//   RlVectorHost --campaign <decks dir> [--total-games 10000] [--recheck 100]
//                [--workers N] [--base-seed 1000] [--max-steps 1000] [--strict]
//                [--out results.jsonl] [--summary summary.json] [--progress-every 250]
//                [--hang-timeout-sec 300] [--host <RlBridgeHost.dll>]
// Every ordered deck pair from <decks dir> (n^2 incl. mirrors) is scheduled round-robin; each game's
// defects are HARVESTED, never fatal: (1) uncaught engine throw (forensics from the aborted result:
// type/stack/step + seed + recent action tail), (2) STOP / NotSupportedException with design-item tag,
// (3) deadlock (stalled / no_mappable_action / step_cap reasons + wall-clock hang watchdog: child kill
// + respawn), (4) determinism drift (post-campaign sampled seed-replays compared by trajectory digest —
// per-step legal sets + full observation bytes + chosen action + terminal outcome; matchId excluded).
// Children run the --strict profile by default (unbound effect = hard fail => harvested via (1)).

string mode = "procs";
int workers = Environment.ProcessorCount;
int gamesPerWorker = 6;
int baseSeed = 1000;
int maxSteps = 2000;
string? outPath = null;
string hostDll = Path.Combine(AppContext.BaseDirectory, "RlBridgeHost.dll");
string? campaignDecksDir = null;
int campaignTotalGames = 10_000;
int recheckSamples = 100;
int progressEvery = 250;
int hangTimeoutSec = 300;
bool strict = false;
string? summaryPath = null;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--mode" when i + 1 < args.Length: mode = args[++i]; break;
        case "--workers" when i + 1 < args.Length: workers = int.Parse(args[++i]); break;
        case "--games" when i + 1 < args.Length: gamesPerWorker = int.Parse(args[++i]); break;
        case "--base-seed" when i + 1 < args.Length: baseSeed = int.Parse(args[++i]); break;
        case "--max-steps" when i + 1 < args.Length: maxSteps = int.Parse(args[++i]); break;
        case "--out" when i + 1 < args.Length: outPath = args[++i]; break;
        case "--host" when i + 1 < args.Length: hostDll = args[++i]; break;
        case "--campaign" when i + 1 < args.Length: campaignDecksDir = args[++i]; break;
        case "--total-games" when i + 1 < args.Length: campaignTotalGames = int.Parse(args[++i]); break;
        case "--recheck" when i + 1 < args.Length: recheckSamples = int.Parse(args[++i]); break;
        case "--progress-every" when i + 1 < args.Length: progressEvery = int.Parse(args[++i]); break;
        case "--hang-timeout-sec" when i + 1 < args.Length: hangTimeoutSec = int.Parse(args[++i]); break;
        case "--strict": strict = true; break;
        case "--summary" when i + 1 < args.Length: summaryPath = args[++i]; break;
        default:
            Console.Error.WriteLine($"unknown arg: {args[i]}");
            return 2;
    }
}

if (mode is not ("procs" or "tasks"))
{
    Console.Error.WriteLine("--mode must be procs or tasks");
    return 2;
}

if ((mode == "procs" || campaignDecksDir is not null) && !File.Exists(hostDll))
{
    Console.Error.WriteLine($"RlBridgeHost.dll not found: {hostDll} (pass --host)");
    return 2;
}

if (campaignDecksDir is not null)
{
    return await RunCampaignAsync();
}

var results = new ConcurrentQueue<string>();
long totalSteps = 0;
int totalGames = 0;
int failures = 0;

// Parent-owned seed distribution (design doc D5): a shared queue instead of a static per-worker
// partition, so a worker that lands short games keeps pulling work and no straggler tail idles the
// pool. Total game count is workers * gamesPerWorker either way.
var seedQueue = new ConcurrentQueue<int>(Enumerable.Range(0, workers * gamesPerWorker).Select(i => baseSeed + i));

Console.WriteLine($"RlVectorHost mode={mode} workers={workers} games/worker={gamesPerWorker} baseSeed={baseSeed}");
Stopwatch wall = Stopwatch.StartNew();

Task[] workerTasks = Enumerable.Range(0, workers)
    .Select(w => Task.Run(() => RunWorkerAsync(w)))
    .ToArray();
await Task.WhenAll(workerTasks);
wall.Stop();

if (outPath is not null)
{
    File.WriteAllLines(outPath, results);
    Console.WriteLine($"results: {results.Count} lines -> {outPath}");
}

double stepsPerSec = totalSteps / wall.Elapsed.TotalSeconds;
Console.WriteLine();
Console.WriteLine($"aggregate: games={totalGames} steps={totalSteps} wall={wall.Elapsed.TotalSeconds:F1}s steps/sec={stepsPerSec:F1} failures={failures}");
Console.WriteLine($"SUMMARY mode={mode} workers={workers} steps_per_sec={stepsPerSec:F2}");
return failures == 0 ? 0 : 1;

async Task RunWorkerAsync(int workerIndex)
{
    try
    {
        ISeatTransport transport = mode == "procs"
            ? await ProcTransport.LaunchAsync(hostDll)
            : new InProcTransport();

        try
        {
            await DriveHandshakeAsync(transport);
            while (seedQueue.TryDequeue(out int seed))
            {
                (int steps, string resultJson, double ms) = await DriveGameAsync(transport, seed);
                Interlocked.Add(ref totalSteps, steps);
                Interlocked.Increment(ref totalGames);
                results.Enqueue(JsonSerializer.Serialize(new
                {
                    worker = workerIndex,
                    mode,
                    seed,
                    steps,
                    elapsedMs = Math.Round(ms, 1),
                    result = JsonSerializer.Deserialize<JsonElement>(resultJson)
                }));
            }
        }
        finally
        {
            transport.Dispose();
        }
    }
    catch (Exception ex)
    {
        Interlocked.Increment(ref failures);
        Console.Error.WriteLine($"worker {workerIndex} FAILED: {ex.GetType().Name}: {ex.Message}");
    }
}

async Task DriveHandshakeAsync(ISeatTransport transport)
{
    Expect(await transport.RequestAsync("""{"type":"hello","protocol":1}"""), "welcome");
    Expect(await transport.RequestAsync("""{"type":"claim","seats":[1,2]}"""), "claimed");
}

async Task<(int Steps, string ResultJson, double Ms)> DriveGameAsync(ISeatTransport transport, int seed)
{
    var sw = Stopwatch.StartNew();
    string reset = $"{{\"type\":\"reset\",\"seed\":{seed},\"maxSteps\":{maxSteps},\"decks\":{{\"1\":\"starter:ST1\",\"2\":\"starter:ST2\"}}}}";
    string msg = await transport.RequestAsync(reset);
    var rng = new Random(seed);
    var legal = new List<int>(64);
    int steps = 0;

    while (true)
    {
        TurnScan scan = ScanMessage(msg, legal);
        if (scan.Type == MessageType.Result)
        {
            sw.Stop();
            return (steps, msg, sw.Elapsed.TotalMilliseconds);
        }

        if (scan.Type == MessageType.Error)
        {
            // Illegal action / protocol error: the host re-issues the turn as the next message.
            msg = await transport.ReadAsync();
            continue;
        }

        if (scan.Type != MessageType.Turn)
        {
            throw new InvalidOperationException($"unexpected message (seed {seed}): {Truncate(msg)}");
        }

        if (legal.Count == 0)
        {
            throw new InvalidOperationException($"empty action mask on a turn (seed {seed}, step {steps})");
        }

        int action = legal[rng.Next(legal.Count)];
        msg = await transport.RequestAsync($"{{\"type\":\"action\",\"seat\":{scan.Seat},\"index\":{action}}}");
        steps++;
    }
}

// --- campaign mode (B4/D6): fuzzing harvest layer -------------------------

async Task<int> RunCampaignAsync()
{
    // D6 fixed profile for fuzz runs: strict (unbound effect = hard fail, harvested via the throw
    // channel) + validated (the driver only ever plays mask-member actions; apply-side legality is
    // the engine's own A1 boundary).
    strict = true;

    string[] deckFiles = Directory.GetFiles(campaignDecksDir!, "*.json")
        .OrderBy(Path.GetFileName, StringComparer.Ordinal)
        .ToArray();
    if (deckFiles.Length == 0)
    {
        Console.Error.WriteLine($"no deck recipes (*.json) in {campaignDecksDir}");
        return 2;
    }

    DeckRecipe[] recipes = deckFiles
        .Select(path =>
        {
            JsonNode node = JsonNode.Parse(File.ReadAllText(path))!;
            string name = node["name"]?.GetValue<string>() ?? Path.GetFileNameWithoutExtension(path);
            return new DeckRecipe(name, node.ToJsonString());
        })
        .ToArray();

    // Ordered pairs INCLUDING mirrors: seat 1 always moves first, so (A,B) and (B,A) are distinct
    // exposures. Games are scheduled round-robin over pairs, seeds are unique per game.
    var pairs = new List<DeckPair>(recipes.Length * recipes.Length);
    foreach (DeckRecipe a in recipes)
    {
        foreach (DeckRecipe b in recipes)
        {
            pairs.Add(new DeckPair(a, b));
        }
    }

    var state = new CampaignState(pairs, outPath);
    var jobs = new ConcurrentQueue<CampaignJob>(
        Enumerable.Range(0, campaignTotalGames).Select(g => new CampaignJob(g, baseSeed + g, g % pairs.Count)));

    Console.WriteLine(
        $"campaign: decks={recipes.Length} orderedPairs={pairs.Count} games={campaignTotalGames} workers={workers} " +
        $"baseSeed={baseSeed} maxSteps={maxSteps} strict={strict} hangTimeoutSec={hangTimeoutSec} recheck={recheckSamples}");

    var campaignWall = Stopwatch.StartNew();
    Task[] campaignWorkers = Enumerable.Range(0, workers)
        .Select(w => Task.Run(() => CampaignWorkerAsync(jobs, state, campaignWall, replay: false)))
        .ToArray();
    await Task.WhenAll(campaignWorkers);
    campaignWall.Stop();

    double campaignStepsPerSec = state.Steps / campaignWall.Elapsed.TotalSeconds;
    Console.WriteLine();
    Console.WriteLine(
        $"campaign done: games={state.GamesDone} steps={state.Steps} wall={campaignWall.Elapsed.TotalSeconds:F0}s " +
        $"steps/sec={campaignStepsPerSec:F1}");

    // --- determinism recheck (harvest signal 4): sampled seed-replays vs campaign digests --------
    CampaignGameRecord[] replayable = state.Games.Values
        .Where(g => g.Digest is not null)
        .OrderBy(g => g.GameIndex)
        .ToArray();
    int sampleCount = Math.Min(recheckSamples, replayable.Length);
    if (sampleCount > 0)
    {
        var sampleRng = new Random(baseSeed);
        CampaignGameRecord[] sampled = replayable.OrderBy(_ => sampleRng.Next()).Take(sampleCount).ToArray();
        var replayJobs = new ConcurrentQueue<CampaignJob>(
            sampled.Select(g => new CampaignJob(g.GameIndex, g.Seed, g.PairIndex)));

        Console.WriteLine($"\nrecheck: replaying {sampleCount} sampled games (same seed, fresh match) for digest drift...");
        var recheckWall = Stopwatch.StartNew();
        Task[] recheckWorkers = Enumerable.Range(0, workers)
            .Select(w => Task.Run(() => CampaignWorkerAsync(replayJobs, state, recheckWall, replay: true)))
            .ToArray();
        await Task.WhenAll(recheckWorkers);
        recheckWall.Stop();
        Console.WriteLine(
            $"recheck done in {recheckWall.Elapsed.TotalSeconds:F0}s: match={state.RecheckMatches} " +
            $"MISMATCH={state.RecheckMismatches} faults={state.RecheckFaults}");
    }

    // --- coverage report -------------------------------------------------------------------------
    JsonObject summary = state.BuildSummary(recipes, campaignTotalGames, campaignWall.Elapsed.TotalSeconds, sampleCount);
    if (summaryPath is not null)
    {
        File.WriteAllText(
            summaryPath,
            summary.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"summary -> {summaryPath}");
    }

    Console.WriteLine("\nreason distribution:");
    foreach ((string reason, int count) in state.ReasonCounts.OrderByDescending(kv => kv.Value))
    {
        Console.WriteLine($"  {reason,-24} {count}");
    }

    Console.WriteLine("\nharvest tallies:");
    foreach ((string kind, int count) in state.DefectCounts.OrderByDescending(kv => kv.Value))
    {
        Console.WriteLine($"  {kind,-24} {count}");
    }

    if (state.DefectCounts.Count == 0)
    {
        Console.WriteLine("  (none)");
    }

    state.Dispose();
    Console.WriteLine(
        $"\nSUMMARY campaign games={state.GamesDone} steps={state.Steps} steps_per_sec={campaignStepsPerSec:F2} " +
        $"defects={state.DefectCounts.Values.Sum()} recheck_mismatch={state.RecheckMismatches} infra_failures={state.InfraFailures}");
    return state.InfraFailures == 0 ? 0 : 1;
}

// Campaign worker: one child RlBridgeHost process (strict profile, crash-isolated), pulled jobs from
// the shared queue. A defective game NEVER stops the pipeline — driver-level faults (hang watchdog,
// transport loss, protocol surprises) are harvested and the child is recycled.
async Task CampaignWorkerAsync(ConcurrentQueue<CampaignJob> queue, CampaignState state, Stopwatch wallClock, bool replay)
{
    ISeatTransport? transport = null;
    try
    {
        while (queue.TryDequeue(out CampaignJob job))
        {
            DeckPair pair = state.Pairs[job.PairIndex];
            try
            {
                transport ??= await LaunchCampaignTransportAsync();
                CampaignGameRecord record = await DriveCampaignGameAsync(transport, job, pair);
                if (replay)
                {
                    state.RecordReplay(job, record, ReproOf(job, pair));
                }
                else
                {
                    state.RecordGame(record, ReproOf(job, pair));
                }
            }
            catch (Exception ex)
            {
                state.RecordDriverFault(job, pair, ex, replay, ReproOf(job, pair));
                transport?.Dispose();
                transport = null;
            }

            if (!replay)
            {
                int done = Interlocked.Increment(ref state.GamesDone);
                if (done % progressEvery == 0 || done == campaignTotalGames)
                {
                    double elapsed = wallClock.Elapsed.TotalSeconds;
                    double gamesPerSec = done / Math.Max(elapsed, 1e-9);
                    double etaSec = (campaignTotalGames - done) / Math.Max(gamesPerSec, 1e-9);
                    Console.WriteLine(
                        $"[{wallClock.Elapsed:hh\\:mm\\:ss}] {done}/{campaignTotalGames} games steps={Interlocked.Read(ref state.Steps)} " +
                        $"steps/s={Interlocked.Read(ref state.Steps) / Math.Max(elapsed, 1e-9):F1} " +
                        $"harvest[throw={state.Count("throw")} stop={state.Count("stop")} deadlock={state.Count("deadlock")} " +
                        $"cap={state.Count("step_cap")} hang={state.Count("hang")} driver={state.Count("driver")}] " +
                        $"eta={TimeSpan.FromSeconds(Math.Min(etaSec, TimeSpan.MaxValue.TotalSeconds - 1)):hh\\:mm\\:ss}");
                }
            }
        }
    }
    catch (Exception ex)
    {
        Interlocked.Increment(ref state.InfraFailures);
        Console.Error.WriteLine($"campaign worker FAILED (pipeline-level): {ex.GetType().Name}: {ex.Message}");
    }
    finally
    {
        transport?.Dispose();
    }
}

string ReproOf(CampaignJob job, DeckPair pair)
{
    return $"seat-protocol reset seed={job.Seed} maxSteps={maxSteps} strict decks=1:{pair.A.Name},2:{pair.B.Name}; " +
           $"policy=masked-random(Random({job.Seed})); pairIndex={job.PairIndex} gameIndex={job.GameIndex}";
}

async Task<ISeatTransport> LaunchCampaignTransportAsync()
{
    ISeatTransport transport = await ProcTransport.LaunchAsync(hostDll, strict ? new[] { "--strict" } : Array.Empty<string>());
    await DriveHandshakeAsync(transport);
    return transport;
}

// One full masked-random campaign game over the seat protocol, folding the whole trajectory into a
// SHA256 digest: per turn message the raw observation number literals (during the scan), then
// stepIndex|seat|legal-index-set|chosen-action; on the result its reason/winner/steps/turns.
// matchId is deliberately excluded (host-session counter, differs on replay).
async Task<CampaignGameRecord> DriveCampaignGameAsync(ISeatTransport transport, CampaignJob job, DeckPair pair)
{
    const int RecentActionWindow = 24;
    var sw = Stopwatch.StartNew();
    string reset =
        $"{{\"type\":\"reset\",\"seed\":{job.Seed},\"maxSteps\":{maxSteps},\"decks\":{{\"1\":{pair.A.Json},\"2\":{pair.B.Json}}}}}";
    string msg = await RequestWithWatchdogAsync(transport, reset, job);
    var rng = new Random(job.Seed);
    var legal = new List<int>(64);
    using IncrementalHash digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    var recent = new Queue<string>(RecentActionWindow);
    int steps = 0;

    while (true)
    {
        TurnScan scan = ScanMessage(msg, legal, digest);
        if (scan.Type == MessageType.Result)
        {
            var result = JsonNode.Parse(msg)!.AsObject();
            digest.AppendData(Encoding.UTF8.GetBytes(
                $"r|{result["reason"]}|{result["winnerSeat"]?.ToJsonString() ?? "-"}|{result["isDraw"]}|{result["steps"]}|{result["turns"]}\n"));
            sw.Stop();
            return new CampaignGameRecord(
                job.GameIndex, job.Seed, job.PairIndex, pair.A.Name, pair.B.Name,
                steps, sw.Elapsed.TotalMilliseconds, Convert.ToHexString(digest.GetHashAndReset()), result, recent.ToArray());
        }

        if (scan.Type == MessageType.Error)
        {
            // Host follows an error with either the aborted result (internal error — the harvest case)
            // or the re-issued turn (illegal action — cannot happen for mask-member play).
            msg = await ReadWithWatchdogAsync(transport, job);
            continue;
        }

        if (scan.Type != MessageType.Turn)
        {
            throw new InvalidOperationException($"unexpected message (seed {job.Seed}): {Truncate(msg)}");
        }

        if (legal.Count == 0)
        {
            throw new InvalidOperationException($"empty action mask on a turn (seed {job.Seed}, step {steps})");
        }

        int action = legal[rng.Next(legal.Count)];
        digest.AppendData(Encoding.UTF8.GetBytes($"t{scan.StepIndex}|s{scan.Seat}|l{string.Join(",", legal)}|a{action}\n"));
        if (recent.Count == RecentActionWindow)
        {
            recent.Dequeue();
        }

        recent.Enqueue($"step={scan.StepIndex} seat={scan.Seat} index={action} legal={legal.Count}");
        msg = await RequestWithWatchdogAsync(transport, $"{{\"type\":\"action\",\"seat\":{scan.Seat},\"index\":{action}}}", job);
        steps++;
    }
}

// Hang watchdog (harvest signal 3): a step that produces no host response within the timeout is a
// wall-clock deadlock candidate — thrown as TimeoutException, harvested as kind=hang, child recycled.
async Task<string> RequestWithWatchdogAsync(ISeatTransport transport, string line, CampaignJob job)
{
    Task<string> request = transport.RequestAsync(line);
    Task winner = await Task.WhenAny(request, Task.Delay(TimeSpan.FromSeconds(hangTimeoutSec)));
    if (winner != request)
    {
        throw new TimeoutException($"hang: no host response within {hangTimeoutSec}s (gameIndex {job.GameIndex}, seed {job.Seed})");
    }

    return await request;
}

async Task<string> ReadWithWatchdogAsync(ISeatTransport transport, CampaignJob job)
{
    Task<string> read = transport.ReadAsync();
    Task winner = await Task.WhenAny(read, Task.Delay(TimeSpan.FromSeconds(hangTimeoutSec)));
    if (winner != read)
    {
        throw new TimeoutException($"hang: no host response within {hangTimeoutSec}s (gameIndex {job.GameIndex}, seed {job.Seed})");
    }

    return await read;
}

// Single-pass Utf8JsonReader scan of a host message: extracts type/seat/stepIndex and the legal
// factored indices (actionMask entries != 0) WITHOUT building a DOM. A turn message carries the full
// 3,088-feature observation — DOM-parsing it per step made the parent driver a measurable
// contender for the workers' cores at high fan-out.
// When a digest is supplied (campaign mode), the raw observation number literals are folded into it
// as they stream by — full-observation determinism material at near-zero extra cost.
static TurnScan ScanMessage(string json, List<int> legalOut, IncrementalHash? digest = null)
{
    legalOut.Clear();
    var reader = new Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(json));
    MessageType type = MessageType.Other;
    int seat = 0;
    int stepIndex = -1;

    while (reader.Read())
    {
        if (reader.TokenType != JsonTokenType.PropertyName || reader.CurrentDepth != 1)
        {
            continue;
        }

        if (reader.ValueTextEquals("type"))
        {
            reader.Read();
            type = reader.ValueTextEquals("turn") ? MessageType.Turn
                : reader.ValueTextEquals("result") ? MessageType.Result
                : reader.ValueTextEquals("error") ? MessageType.Error
                : reader.ValueTextEquals("welcome") ? MessageType.Welcome
                : reader.ValueTextEquals("claimed") ? MessageType.Claimed
                : MessageType.Other;
        }
        else if (reader.ValueTextEquals("seat"))
        {
            reader.Read();
            seat = reader.GetInt32();
        }
        else if (reader.ValueTextEquals("stepIndex"))
        {
            reader.Read();
            stepIndex = reader.GetInt32();
        }
        else if (reader.ValueTextEquals("actionMask"))
        {
            reader.Read(); // StartArray
            int index = 0;
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (reader.GetDouble() != 0)
                {
                    legalOut.Add(index);
                }

                index++;
            }
        }
        else if (reader.ValueTextEquals("observation"))
        {
            reader.Read(); // StartArray
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (digest is not null && !reader.HasValueSequence)
                {
                    digest.AppendData(reader.ValueSpan);
                    digest.AppendData(","u8);
                }
            }
        }
        else
        {
            reader.Read();
            if (reader.TokenType is JsonTokenType.StartArray or JsonTokenType.StartObject)
            {
                reader.Skip();
            }
        }
    }

    return new TurnScan(type, seat, stepIndex);
}

static string Truncate(string value) => value.Length <= 200 ? value : value[..200] + "...";

static void Expect(string msg, string type)
{
    if (!msg.Contains($"\"type\":\"{type}\"", StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"expected '{type}', got: {Truncate(msg)}");
    }
}

enum MessageType
{
    Turn,
    Result,
    Error,
    Welcome,
    Claimed,
    Other,
}

readonly record struct TurnScan(MessageType Type, int Seat, int StepIndex);

// --- campaign bookkeeping (B4/D6) -----------------------------------------

readonly record struct CampaignJob(int GameIndex, int Seed, int PairIndex);

sealed record DeckRecipe(string Name, string Json);

sealed record DeckPair(DeckRecipe A, DeckRecipe B);

sealed record CampaignGameRecord(
    int GameIndex,
    int Seed,
    int PairIndex,
    string Deck1,
    string Deck2,
    int Steps,
    double ElapsedMs,
    string? Digest,
    JsonObject? Result,
    string[] RecentActions);

/// <summary>
/// Thread-safe campaign ledger: per-game records, per-pair/reason tallies, harvested defect records,
/// and the result JSONL writer. Defects are DATA, never control flow (D6: a defective game is
/// discarded and harvested; the pipeline keeps running).
/// </summary>
sealed class CampaignState : IDisposable
{
    public readonly IReadOnlyList<DeckPair> Pairs;
    public readonly ConcurrentDictionary<int, CampaignGameRecord> Games = new();
    public readonly ConcurrentDictionary<string, int> ReasonCounts = new();
    public readonly ConcurrentDictionary<string, int> DefectCounts = new();
    public readonly ConcurrentQueue<JsonObject> Defects = new();
    private readonly ConcurrentDictionary<(int Pair, string Reason), int> _pairReasons = new();
    private readonly int[] _pairGames;
    private readonly StreamWriter? _writer;
    private readonly object _writerLock = new();

    public int GamesDone;
    public long Steps;
    public int RecheckMatches;
    public int RecheckMismatches;
    public int RecheckFaults;
    public int InfraFailures;

    public CampaignState(IReadOnlyList<DeckPair> pairs, string? outPath)
    {
        Pairs = pairs;
        _pairGames = new int[pairs.Count];
        _writer = outPath is null ? null : new StreamWriter(outPath, append: false);
    }

    public int Count(string kind) => DefectCounts.TryGetValue(kind, out int value) ? value : 0;

    public void RecordGame(CampaignGameRecord record, string repro)
    {
        Games[record.GameIndex] = record;
        Interlocked.Add(ref Steps, record.Steps);
        string reason = record.Result?["reason"]?.GetValue<string>() ?? "unknown";
        Interlocked.Increment(ref _pairGames[record.PairIndex]);
        ReasonCounts.AddOrUpdate(reason, 1, (_, v) => v + 1);
        _pairReasons.AddOrUpdate((record.PairIndex, reason), 1, (_, v) => v + 1);

        var exception = record.Result?["exception"] as JsonObject;
        (string kind, string? designItem) = ClassifyOutcome(reason, exception);

        var line = new JsonObject
        {
            ["game"] = record.GameIndex,
            ["seed"] = record.Seed,
            ["deck1"] = record.Deck1,
            ["deck2"] = record.Deck2,
            ["reason"] = reason,
            ["winnerSeat"] = record.Result?["winnerSeat"]?.DeepClone(),
            ["isDraw"] = record.Result?["isDraw"]?.DeepClone(),
            ["steps"] = record.Steps,
            ["turns"] = record.Result?["turns"]?.DeepClone(),
            ["elapsedMs"] = Math.Round(record.ElapsedMs, 1),
            ["digest"] = record.Digest,
            ["kind"] = kind
        };

        if (kind != "natural")
        {
            DefectCounts.AddOrUpdate(kind, 1, (_, v) => v + 1);
            var defect = new JsonObject
            {
                ["kind"] = kind,
                ["designItem"] = designItem,
                ["game"] = record.GameIndex,
                ["seed"] = record.Seed,
                ["deck1"] = record.Deck1,
                ["deck2"] = record.Deck2,
                ["reason"] = reason,
                ["steps"] = record.Steps,
                ["exception"] = exception?.DeepClone(),
                ["recentActions"] = new JsonArray(record.RecentActions.Select(a => (JsonNode)JsonValue.Create(a)!).ToArray()),
                ["repro"] = repro
            };
            Defects.Enqueue(defect);
            line["designItem"] = designItem;
        }

        WriteLine(line);
    }

    public void RecordReplay(CampaignJob job, CampaignGameRecord replayRecord, string repro)
    {
        CampaignGameRecord original = Games[job.GameIndex];
        bool identical = string.Equals(original.Digest, replayRecord.Digest, StringComparison.Ordinal);
        if (identical)
        {
            Interlocked.Increment(ref RecheckMatches);
        }
        else
        {
            Interlocked.Increment(ref RecheckMismatches);
            DefectCounts.AddOrUpdate("drift", 1, (_, v) => v + 1);
            Defects.Enqueue(new JsonObject
            {
                ["kind"] = "drift",
                ["game"] = job.GameIndex,
                ["seed"] = job.Seed,
                ["deck1"] = original.Deck1,
                ["deck2"] = original.Deck2,
                ["campaignDigest"] = original.Digest,
                ["replayDigest"] = replayRecord.Digest,
                ["repro"] = repro
            });
        }

        WriteLine(new JsonObject
        {
            ["recheckGame"] = job.GameIndex,
            ["seed"] = job.Seed,
            ["identical"] = identical,
            ["campaignDigest"] = original.Digest,
            ["replayDigest"] = replayRecord.Digest
        });
    }

    public void RecordDriverFault(CampaignJob job, DeckPair pair, Exception ex, bool duringRecheck, string repro)
    {
        string kind = ex is TimeoutException ? "hang" : "driver";
        if (duringRecheck)
        {
            Interlocked.Increment(ref RecheckFaults);
        }
        else
        {
            Interlocked.Increment(ref _pairGames[job.PairIndex]);
            ReasonCounts.AddOrUpdate(kind, 1, (_, v) => v + 1);
            _pairReasons.AddOrUpdate((job.PairIndex, kind), 1, (_, v) => v + 1);
        }

        DefectCounts.AddOrUpdate(kind, 1, (_, v) => v + 1);
        var defect = new JsonObject
        {
            ["kind"] = kind,
            ["game"] = job.GameIndex,
            ["seed"] = job.Seed,
            ["deck1"] = pair.A.Name,
            ["deck2"] = pair.B.Name,
            ["duringRecheck"] = duringRecheck,
            ["error"] = $"{ex.GetType().Name}: {ex.Message}",
            ["repro"] = repro
        };
        Defects.Enqueue(defect);
        WriteLine((JsonObject)defect.DeepClone());
    }

    public JsonObject BuildSummary(DeckRecipe[] recipes, int scheduledGames, double wallSeconds, int recheckSampleCount)
    {
        var pairArray = new JsonArray();
        for (int i = 0; i < Pairs.Count; i++)
        {
            var reasons = new JsonObject();
            foreach (var kv in _pairReasons.Where(kv => kv.Key.Pair == i).OrderBy(kv => kv.Key.Reason, StringComparer.Ordinal))
            {
                reasons[kv.Key.Reason] = kv.Value;
            }

            pairArray.Add(new JsonObject
            {
                ["deck1"] = Pairs[i].A.Name,
                ["deck2"] = Pairs[i].B.Name,
                ["games"] = _pairGames[i],
                ["reasons"] = reasons
            });
        }

        return new JsonObject
        {
            ["games"] = GamesDone,
            ["scheduled"] = scheduledGames,
            ["steps"] = Steps,
            ["wallSeconds"] = Math.Round(wallSeconds, 1),
            ["stepsPerSec"] = Math.Round(Steps / Math.Max(wallSeconds, 1e-9), 2),
            ["decks"] = new JsonArray(recipes.Select(r => (JsonNode)JsonValue.Create(r.Name)!).ToArray()),
            ["reasonDistribution"] = ToJsonObject(ReasonCounts),
            ["harvest"] = ToJsonObject(DefectCounts),
            ["recheck"] = new JsonObject
            {
                ["samples"] = recheckSampleCount,
                ["identical"] = RecheckMatches,
                ["mismatches"] = RecheckMismatches,
                ["faults"] = RecheckFaults
            },
            ["pairs"] = pairArray,
            ["defects"] = new JsonArray(Defects.Select(d => d.DeepClone()).ToArray())
        };
    }

    private static JsonObject ToJsonObject(ConcurrentDictionary<string, int> counts)
    {
        var obj = new JsonObject();
        foreach (var kv in counts.OrderByDescending(kv => kv.Value))
        {
            obj[kv.Key] = kv.Value;
        }

        return obj;
    }

    // Harvest taxonomy (D6): stop = NotSupportedException (ported STOP / infra STOP throw, design-item
    // tag extracted when present), throw = any other engine exception, deadlock = the host's
    // stalled / no_mappable_action reasons, step_cap = non-terminating-by-cap, natural = game-rule end.
    private static (string Kind, string? DesignItem) ClassifyOutcome(string reason, JsonObject? exception)
    {
        if (reason is "stalled" or "no_mappable_action")
        {
            return ("deadlock", null);
        }

        if (reason == "step_cap")
        {
            return ("step_cap", null);
        }

        if (reason == "aborted")
        {
            string rootType = exception?["rootType"]?.GetValue<string>()
                ?? exception?["type"]?.GetValue<string>()
                ?? string.Empty;
            string message = exception?["rootMessage"]?.GetValue<string>()
                ?? exception?["message"]?.GetValue<string>()
                ?? string.Empty;
            Match tag = Regex.Match(message, @"design item ([A-Za-z0-9_\-]+)");
            string? designItem = tag.Success ? tag.Groups[1].Value : null;
            return rootType.Contains("NotSupportedException", StringComparison.Ordinal)
                ? ("stop", designItem)
                : ("throw", designItem);
        }

        return ("natural", null);
    }

    private void WriteLine(JsonObject line)
    {
        if (_writer is null)
        {
            return;
        }

        lock (_writerLock)
        {
            _writer.WriteLine(line.ToJsonString());
            _writer.Flush();
        }
    }

    public void Dispose()
    {
        lock (_writerLock)
        {
            _writer?.Dispose();
        }
    }
}

// --- transports -----------------------------------------------------------

interface ISeatTransport : IDisposable
{
    /// <summary>Send one protocol line, return the first response line.</summary>
    Task<string> RequestAsync(string line);

    /// <summary>Read the next pending response line (e.g. the re-issued turn after an error).</summary>
    Task<string> ReadAsync();
}

/// <summary>In-process transport: the same protocol strings against a private SeatMatchHost.</summary>
sealed class InProcTransport : ISeatTransport
{
    private readonly SeatMatchHost _host = new();
    private readonly Queue<string> _pending = new();

    public async Task<string> RequestAsync(string line)
    {
        foreach (string response in await _host.HandleLineAsync(line))
        {
            _pending.Enqueue(response);
        }

        return await ReadAsync();
    }

    public Task<string> ReadAsync()
    {
        if (_pending.Count == 0)
        {
            throw new InvalidOperationException("no pending in-process response");
        }

        return Task.FromResult(_pending.Dequeue());
    }

    public void Dispose()
    {
    }
}

/// <summary>Child-process transport: RlBridgeHost over stdio (protocol v1's v1 transport).</summary>
sealed class ProcTransport : ISeatTransport
{
    private readonly Process _process;

    private ProcTransport(Process process)
    {
        _process = process;
    }

    public static Task<ProcTransport> LaunchAsync(string hostDll, IReadOnlyList<string>? extraArgs = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add(hostDll);
        foreach (string extraArg in extraArgs ?? Array.Empty<string>())
        {
            psi.ArgumentList.Add(extraArg);
        }

        Process process = Process.Start(psi) ?? throw new InvalidOperationException("failed to start RlBridgeHost");
        // Drain stderr in the background so the child never blocks on a full pipe.
        _ = Task.Run(async () =>
        {
            while (await process.StandardError.ReadLineAsync() is not null)
            {
            }
        });

        return Task.FromResult(new ProcTransport(process));
    }

    public async Task<string> RequestAsync(string line)
    {
        await _process.StandardInput.WriteLineAsync(line);
        await _process.StandardInput.FlushAsync();
        return await ReadAsync();
    }

    public async Task<string> ReadAsync()
    {
        return await _process.StandardOutput.ReadLineAsync()
            ?? throw new InvalidOperationException("RlBridgeHost closed stdout");
    }

    public void Dispose()
    {
        try
        {
            _process.StandardInput.Close();
            if (!_process.WaitForExit(3000))
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // best-effort teardown
        }

        _process.Dispose();
    }
}
