namespace HeadlessDCGO.Engine.Headless.Diagnostics;

using System.Collections;
using System.Reflection;
using HeadlessDCGO.Engine.Headless.Bootstrap;
using HeadlessDCGO.Engine.Headless.Coroutines;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using UnityEngine;

internal sealed class CapturingContext : SynchronizationContext
{
    public List<Exception> Captured { get; } = new();
    public override void Post(SendOrPostCallback d, object? s) { try { d(s); } catch (Exception e) { Captured.Add(e); } }
    public override void Send(SendOrPostCallback d, object? s) => Post(d, s);
}

public static class DriverSmokeTest
{
    public static int Run()
    {
        CapturingContext ctx = new();
        SynchronizationContext.SetSynchronizationContext(ctx);

        CEntity_Base[] cards = CardEntityLoader.LoadAll("/home/hg/git/headlessDCGO/DCGO/Assets/CardBaseEntity");
        Console.WriteLine($"[1] 카드 정의 {cards.Length}장");

        HeadlessScene scene = new();
        scene.Build();
        scene.SupplyGameData(cards, "ST1");
        Console.WriteLine($"[2] 씬 구성 (일괄 채움 {scene.FilledSlots.Count}) · 덱 ST1");

        scene.RunLifecycle();
        Console.WriteLine("[3] 수명주기");

        // AwakeCoroutine 과 turnStateMachine.Init 은 AS-IS 에서 같은 스케줄러 위에서 동시에 돈다.
        // Init 이 CanSetRandom 을 기다리는데 그걸 세우는 건 아직 돌고 있는 AwakeCoroutine 이다.
        // 별도 드라이버로 나누면 그 상호작용이 끊긴다.
        CoroutineDriver driver = new();
        using IDisposable hook = driver.AttachToStartCoroutine();
        Start(driver, GManager.instance, "AwakeCoroutine");



        // 엔진이 멈추면(= 외부 결정을 기다리면) 가상 플레이어가 답한다.
        AlwaysDeclineVirtualPlayer player = new() { RoutineInFlight = driver.InFlight };
        string last = ""; string lastFrames = ""; int stableFrom = 0; int answered = 0;

        for (int tick = 1; tick <= 300_000; tick++)
        {
            try { driver.Tick(); }
            catch (Exception ex)
            {
                Exception r = ex; while (r is TargetInvocationException && r.InnerException is not null) r = r.InnerException;
                string f = (r.StackTrace ?? "").Split('\n').FirstOrDefault(l => l.Contains("Assets/Scripts")) ?? "";
                Console.WriteLine($"    틱{tick} 예외 {r.GetType().Name}  {f.Trim().Replace("/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/", "")}");
                Console.WriteLine($"       {r.Message.Split('\n')[0]}");
                break;
            }

            // 스케줄러가 멈추길 기다리지 않는다 — 유휴 루프 때문에 결코 멈추지 않는다.
            // 대기 중인 루틴이 있고 패널이 클릭을 기다리면 답한다.
            int before = driver.ActiveRoutines;
            player.Waits = driver.PendingWaits.ToArray();
            if (player.Answer())
            {
                answered++;
            }

            Player? y = GManager.instance?.You; Player? o = GManager.instance?.Opponent;
            if (driver.ActiveRoutines < before && driver.ActiveRoutines <= 3)
            {
                Console.WriteLine($"    틱{tick} 루틴 {before}→{driver.ActiveRoutines} :: {string.Join(" | ", driver.Describe())}");
            }

            GameContext? gc = GManager.instance?.turnStateMachine?.gameContext;
            string now = $"{gc?.TurnPhase} {(gc?.TurnPlayer?.isYou == true ? "You" : "Opp")}  {y?.LibraryCards.Count}/{y?.HandCards.Count}/{y?.SecurityCards.Count}|{o?.LibraryCards.Count}/{o?.HandCards.Count}/{o?.SecurityCards.Count}";
            if (now != last) { Console.WriteLine($"    틱{tick,6} 루틴{driver.ActiveRoutines} {now}"); last = now; stableFrom = tick; }

            if (false)
            {
                string frames = string.Join(" | ", driver.Describe().Select(d => d.Split("  ")[1].Replace("+<", ".").Replace(">d__", "#")));
                if (frames != lastFrames) { Console.WriteLine($"      틱{tick,3} 죽음{driver.Killed} :: {frames}"); lastFrames = frames; }
            }
            if (driver.ActiveRoutines == 0) break;
            if (tick - stableFrom > 2000) { Console.WriteLine($"    틱{tick} 이후 4000틱 상태 불변 — 중단"); break; }
        }

        Console.WriteLine($"    답한 프롬프트 {answered}건");
        if (player.Unhandled.Count > 0) Console.WriteLine($"    미대응 선택기: {string.Join(", ", player.Unhandled)}");
        foreach (var g in player.Answered.GroupBy(a => a.Panel).OrderByDescending(g => g.Count()))
            Console.WriteLine($"       {g.Key,-26} {g.Count()}");
        DriverResult result = new(DriverStopReason.BudgetExhausted, driver.Ticks, driver.ActiveRoutines);

        // 갇힌 루프의 실제 값 확인
        foreach (Player? seat in new[] { GManager.instance?.You, GManager.instance?.Opponent })
        {
            foreach (FieldPermanentCard? c in seat?.FieldPermanentObjects ?? new List<FieldPermanentCard>())
            {
                if (c is not null)
                    Console.WriteLine($"    FieldPermanentCard z={c.transform.localPosition.z}  |z+0.2|={Math.Abs(c.transform.localPosition.z + 0.2f)}");
            }
        }

        Console.WriteLine("    ── 죽은 루틴");
        foreach (string k in driver.KillLog.Where(k => !k.Contains("LoadingObject")).Take(8)) Console.WriteLine($"       {k}");
        Console.WriteLine("    ── 살아있는 루틴");
        foreach (string d in driver.Describe()) Console.WriteLine($"       {d}");


        Console.WriteLine($"[4] 스케줄러  {result.Reason} 틱{result.Ticks} 잔여{result.ActiveRoutines}");
        Console.WriteLine($"[5] turnStateMachine {(GManager.instance?.turnStateMachine != null ? "생성됨" : "null")}");

        Player? you = GManager.instance?.You;
        Player? opp = GManager.instance?.Opponent;
        TurnStateMachine? tsm2 = GManager.instance?.turnStateMachine;
        Console.WriteLine($"[6] 상태  endGame={tsm2?.endGame}  You.IsLose={GManager.instance?.You?.IsLose}  Opp.IsLose={GManager.instance?.Opponent?.IsLose}  죽은루틴={driver.Killed}");
        Console.WriteLine($"    You  Library {N(you?.LibraryCards)} · Digitama {N(you?.DigitamaLibraryCards)} · Hand {N(you?.HandCards)} · Security {N(you?.SecurityCards)}");
        Console.WriteLine($"    Opp  Library {N(opp?.LibraryCards)} · Digitama {N(opp?.DigitamaLibraryCards)} · Hand {N(opp?.HandCards)} · Security {N(opp?.SecurityCards)}");

        List<Exception> all = new(scene.LifecycleErrors);
        all.AddRange(ctx.Captured);
        Console.WriteLine($"[7] 예외 {all.Count}건");
        foreach (Exception ex in all.Take(6))
        {
            Exception root = ex;
            while (root is TargetInvocationException && root.InnerException is not null) root = root.InnerException;
            string f = (root.StackTrace ?? "").Split('\n').FirstOrDefault(l => l.Contains("Assets/Scripts")) ?? "";
            Console.WriteLine($"  ── {root.GetType().Name}  {f.Trim().Replace("/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/", "")}");
        }

        Console.Out.Flush();
        return 0;
    }

    private static string N<T>(List<T>? list) => list?.Count.ToString() ?? "null";

    private static void Start(CoroutineDriver driver, object? target, string method)
    {
        if (target is null) return;
        MethodInfo? m = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (m?.Invoke(target, null) is IEnumerator routine) driver.Start(routine);
    }
}
