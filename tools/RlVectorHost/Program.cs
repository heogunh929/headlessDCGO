using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
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

string mode = "procs";
int workers = Environment.ProcessorCount;
int gamesPerWorker = 6;
int baseSeed = 1000;
int maxSteps = 2000;
string? outPath = null;
string hostDll = Path.Combine(AppContext.BaseDirectory, "RlBridgeHost.dll");

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

if (mode == "procs" && !File.Exists(hostDll))
{
    Console.Error.WriteLine($"RlBridgeHost.dll not found: {hostDll} (pass --host)");
    return 2;
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

// Single-pass Utf8JsonReader scan of a host message: extracts type/seat and the legal factored
// indices (actionMask entries != 0) WITHOUT building a DOM. A turn message carries the full
// 3,088-feature observation — DOM-parsing it per step made the parent driver a measurable
// contender for the workers' cores at high fan-out.
static TurnScan ScanMessage(string json, List<int> legalOut)
{
    legalOut.Clear();
    var reader = new Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(json));
    MessageType type = MessageType.Other;
    int seat = 0;

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
        else
        {
            reader.Read();
            if (reader.TokenType is JsonTokenType.StartArray or JsonTokenType.StartObject)
            {
                reader.Skip();
            }
        }
    }

    return new TurnScan(type, seat);
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

readonly record struct TurnScan(MessageType Type, int Seat);

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

    public static Task<ProcTransport> LaunchAsync(string hostDll)
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
