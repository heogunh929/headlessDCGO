// N판을 연속 실행해 완주율과 실패 지점을 집계한다. 검증용 임시 진입점.
namespace HeadlessDCGO.Engine.Headless.Diagnostics;

using System.Collections;
using System.Reflection;
using HeadlessDCGO.Engine.Headless.Bootstrap;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Coroutines;
using HeadlessDCGO.Engine.Headless.DataLoading;
using UnityEngine;

internal sealed class SilentContext : SynchronizationContext
{
    public int Count { get; private set; }
    public override void Post(SendOrPostCallback d, object? s) { try { d(s); } catch { Count++; } }
    public override void Send(SendOrPostCallback d, object? s) => Post(d, s);
}

public static class MatchSmoke
{
    public static int Run(int matches)
    {
        SynchronizationContext.SetSynchronizationContext(new SilentContext());
        TextWriter real = Console.Out;
        CEntity_Base[] cards = CardEntityLoader.LoadAll("/home/hg/git/headlessDCGO/DCGO/Assets/CardBaseEntity");

        int completed = 0;
        Dictionary<string, int> stalls = new(StringComparer.Ordinal);
        Dictionary<string, int> errors = new(StringComparer.Ordinal);
        List<int> ticks = new();

        for (int i = 0; i < matches; i++)
        {
            Console.SetOut(TextWriter.Null);          // AS-IS 로그 억제
            (bool ok, string where, int tick) = RunOne(cards, seed: i + 1);
            Console.SetOut(real);

            if (ok) { completed++; ticks.Add(tick); }
            else
            {
                Dictionary<string, int> bucket = where.StartsWith("예외", StringComparison.Ordinal) ? errors : stalls;
                bucket[where] = bucket.GetValueOrDefault(where) + 1;
            }
        }

        Console.WriteLine($"완주 {completed}/{matches}  ({100.0 * completed / matches:F0}%)");
        if (ticks.Count > 0) Console.WriteLine($"  완주 판 틱: 최소 {ticks.Min()} 중앙 {ticks.OrderBy(x => x).ElementAt(ticks.Count / 2)} 최대 {ticks.Max()}");
        Console.WriteLine("  실패 지점:");
        foreach (var kv in stalls.Concat(errors).OrderByDescending(kv => kv.Value))
            Console.WriteLine($"    {kv.Value,3}회  {kv.Key}");

        return completed == matches ? 0 : 1;
    }

    private static (bool, string, int) RunOne(CEntity_Base[] cards, int seed)
    {
        HeadlessScene scene = new();

        try
        {
            return RunToCompletion(scene, cards, seed);
        }
        finally
        {
            scene.Teardown();
        }
    }

    private static (bool, string, int) RunToCompletion(HeadlessScene scene, CEntity_Base[] cards, int seed)
    {
        scene.Build();
        scene.SupplyGameData(cards, "ST1");
        scene.RunLifecycle();

        CoroutineDriver driver = new();
        using IDisposable hook = driver.AttachToStartCoroutine();

        MethodInfo? awake = typeof(GManager).GetMethod("AwakeCoroutine", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (awake?.Invoke(GManager.instance, null) is IEnumerator routine) driver.Start(routine);

        RandomVirtualPlayer player = new(seed);
        string last = ""; int stableFrom = 0;

        for (int tick = 1; tick <= 100_000; tick++)
        {
            try { driver.Tick(); }
            catch (Exception ex)
            {
                Exception r = ex; while (r is TargetInvocationException && r.InnerException is not null) r = r.InnerException;
                string frame = (r.StackTrace ?? "").Split('\n').FirstOrDefault(l => l.Contains("Assets/Scripts")) ?? "";
                return (false, $"예외 {r.GetType().Name} @ {Trim(frame)}", tick);
            }

            player.Waits = driver.PendingWaits.ToArray();
            player.Answer();

            if (GManager.instance?.turnStateMachine?.endGame == true) return (true, "", tick);

            Player? y = GManager.instance?.You; Player? o = GManager.instance?.Opponent;
            string now = $"{y?.LibraryCards.Count}/{y?.HandCards.Count}/{y?.SecurityCards.Count}|{o?.LibraryCards.Count}/{o?.HandCards.Count}/{o?.SecurityCards.Count}";
            if (now != last) { last = now; stableFrom = tick; }

            if (tick - stableFrom > 1500)
            {
                string frames = string.Join(" + ", driver.Describe()
                    .Where(d => !d.Contains("LoadingObject"))
                    .Select(d => d.Split("  ")[1].Replace("+<", ".").Split(">d__")[0]));
                string un = player.Unhandled.Count > 0 ? $" 미대응[{string.Join(",", player.Unhandled)}]" : "";
                if (frames.Length == 0)
                {
                    string trail = string.Join("\n      ", driver.Removals
                        .Where(r => r.Contains("GameStateMachine") || r.Contains("Init") || r.Contains("StartGame")
                                    || r.Contains("MainPhase") || r.Contains("Phase"))
                        .TakeLast(6));

                    string deact = string.Join(", ", UnityEngine.GameObject.Deactivations.Distinct().TakeLast(6));

                    return (false, $"게임루틴 소멸 — 비활성화:[{deact}]", tick);
                }

                return (false, $"정체 {frames}{un}", tick);
            }
        }

        return (false, "틱 예산 소진", 100_000);
    }

    private static string Trim(string frame) =>
        frame.Trim().Replace("/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/", "").Split(" in ").Last();
}
