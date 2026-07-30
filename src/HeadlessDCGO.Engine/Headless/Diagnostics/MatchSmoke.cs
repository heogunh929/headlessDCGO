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
    public static int Run(int matches, string deckCode = "ST1", bool printDigest = false)
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
            (bool ok, string where, int tick, ulong digest) = RunOne(cards, seed: i + 1, deckCode);
            Console.SetOut(real);

            if (printDigest)
            {
                real.WriteLine($"판 {i + 1,3}  시드 {i + 1,3}  다이제스트 {digest:x16}  {(ok ? "완주" : "실패")} 틱 {tick}");
            }

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

    private static (bool, string, int, ulong) RunOne(CEntity_Base[] cards, int seed, string deckCode)
    {
        HeadlessScene scene = new();

        try
        {
            return RunToCompletion(scene, cards, seed, deckCode);
        }
        finally
        {
            scene.Teardown();
        }
    }

    private static (bool, string, int, ulong) RunToCompletion(HeadlessScene scene, CEntity_Base[] cards, int seed, string deckCode)
    {
        scene.Build();

        // "ST1" = 양석 AS-IS 기본 경로(상대석은 무작위 샘플덱 폴백), "ST1:ST2" = You석 ST1, 상대석 ST2.
        string[] pair = deckCode.Split(':', 2);
        scene.SupplyGameData(cards, pair[0], pair.Length > 1 ? pair[1] : null);
        scene.RunLifecycle();

        CoroutineDriver driver = new();
        using IDisposable hook = driver.AttachToStartCoroutine();

        MethodInfo? awake = typeof(GManager).GetMethod("AwakeCoroutine", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (awake?.Invoke(GManager.instance, null) is IEnumerator routine) driver.Start(routine);

        // 좌석별 정책 인스턴스 — 양석 isYou=true 자기대전 배선(HeadlessScene 참조). 시드 분리로 두 좌석의
        // 무작위 스트림이 서로 독립.
        RandomVirtualPlayer you = new(seed * 2) { Seat = GManager.instance?.You, RoutineInFlight = driver.InFlight };
        RandomVirtualPlayer opponent = new(seed * 2 + 1) { Seat = GManager.instance?.Opponent, RoutineInFlight = driver.InFlight };
        RandomVirtualPlayer[] seats = { you, opponent };
        string last = ""; int stableFrom = 0;
        bool pinned = false;
        ulong digest = 14695981039346656037UL;   // FNV-1a 64 오프셋

        // 상태 서명이 바뀐 틱마다 (틱, 상세 서명)을 러닝 해시에 접는다. CardIndex는 생성 서수라
        // 룰이 결정하는 값(프로세스-로컬 아님) — GetInstanceID·DateTime류는 설계상 금지.
        void Fold(string s)
        {
            foreach (char c in s) { digest ^= c; digest *= 1099511628211UL; }
        }

        static string Sig(Player? p) => p is null ? "-" :
            $"{string.Join(",", p.HandCards.Select(c => c.CardIndex))}" +
            $"/{p.LibraryCards.Count}/{string.Join(",", p.SecurityCards.Select(c => c.CardIndex))}" +
            $"/{p.TrashCards.Count}" +
            $"/{string.Join(",", p.GetFieldPermanents().Select(x => $"{x.TopCard?.CardIndex}~{(x.IsSuspended ? 1 : 0)}~{x.DigivolutionCards.Count}"))}";

        for (int tick = 1; tick <= 100_000; tick++)
        {
            try { driver.Tick(); }
            catch (Exception ex)
            {
                Exception r = ex; while (r is TargetInvocationException && r.InnerException is not null) r = r.InnerException;
                string frame = (r.StackTrace ?? "").Split('\n').FirstOrDefault(l => l.Contains("Assets/Scripts")) ?? "";
                return (false, $"예외 {r.GetType().Name} @ {Trim(frame)}", tick, digest);
            }

            // AS-IS의 공유-시드 악수(SetRandom)가 끝난 첫 틱에 매치 시드로 재고정 — 이 뒤가 첫 셔플이다.
            if (!pinned)
            {
                pinned = Determinism.MatchSeed.TryPin(seed);
            }

            foreach (RandomVirtualPlayer seat in seats)
            {
                seat.Waits = driver.PendingWaits.ToArray();
                seat.Answer();
            }

            if (GManager.instance?.turnStateMachine?.endGame == true)
            {
                Fold($"끝:{tick}:{last}");

                return (true, "", tick, digest);
            }

            Player? y = GManager.instance?.You; Player? o = GManager.instance?.Opponent;
            string now = $"{Sig(y)}|{Sig(o)}|{GManager.instance?.turnStateMachine?.gameContext?.Memory}" +
                $"|{GManager.instance?.turnStateMachine?.gameContext?.TurnPhase}";
            if (now != last) { last = now; stableFrom = tick; Fold($"{tick}:{now}"); }

            if (tick - stableFrom > 1500)
            {
                string frames = string.Join(" + ", driver.Describe()
                    .Where(d => !d.Contains("LoadingObject"))
                    .Select(d => d.Split("  ")[1].Replace("+<", ".").Split(">d__")[0]));
                string[] unhandled = seats.SelectMany(s => s.Unhandled).Distinct().ToArray();
                string un = unhandled.Length > 0 ? $" 미대응[{string.Join(",", unhandled)}]" : "";
                if (frames.Length == 0)
                {
                    string trail = string.Join("\n      ", driver.Removals
                        .Where(r => r.Contains("GameStateMachine") || r.Contains("Init") || r.Contains("StartGame")
                                    || r.Contains("MainPhase") || r.Contains("Phase"))
                        .TakeLast(6));

                    string deact = string.Join(", ", UnityEngine.GameObject.Deactivations.Distinct().TakeLast(6));

                    return (false, $"게임루틴 소멸 — 비활성화:[{deact}]", tick, digest);
                }

                return (false, $"정체 {frames}{un}", tick, digest);
            }
        }

        return (false, "틱 예산 소진", 100_000, digest);
    }

    private static string Trim(string frame) =>
        frame.Trim().Replace("/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/", "").Split(" in ").Last();
}
