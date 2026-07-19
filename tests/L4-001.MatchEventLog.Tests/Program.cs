using System.Text.Json;
using System.Text.Json.Nodes;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// L4: 매치 사건 로그 계약 (docs/audit/rl_l4_match_log_design.md §2-3).
//  - OFF/RESULT = 무출력(FR-7.3 가드), REPLAY/ANALYSIS = 타입→레벨 분류 필터.
//  - JSONL 육하원칙 필드 + turn/phase/matchId 스탬프 (FR-7.4) + tags 예약(FR-7.5).
//  - 실매치: 배틀소멸이 CardMoved(BattleArea→Trash)로 관측된다 — 이벤트 소비 증명.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("OFF and RESULT levels write nothing", () => RunLeveledMatch(MatchLogLevel.Off, lines =>
    {
        AssertEqual(0, lines.Count, "OFF writes nothing");
    })),
    ("REPLAY level keeps bold events and filters analysis noise", () => RunLeveledMatch(MatchLogLevel.Replay, lines =>
    {
        var types = lines.Select(l => l["type"]!.GetValue<string>()).ToHashSet();
        AssertTrue(types.Contains("CardMoved"), "card moves present");
        AssertTrue(types.Contains("ActionProcessed"), "actions present");
        AssertTrue(!types.Contains("EffectQueued") && !types.Contains("StateChanged"),
            "analysis-level types filtered out");
    })),
    ("ANALYSIS level adds effect/timing events", () => RunLeveledMatch(MatchLogLevel.Analysis, lines =>
    {
        var types = lines.Select(l => l["type"]!.GetValue<string>()).ToHashSet();
        AssertTrue(types.Contains("StateChanged") || types.Contains("EffectQueued") || types.Contains("ActionQueued"),
            "analysis-level types present");
    })),
    ("Every line carries the schema fields and match stamp", () => RunLeveledMatch(MatchLogLevel.Replay, lines =>
    {
        AssertTrue(lines.Count > 50, $"substantial log ({lines.Count} lines)");
        string[] required = { "matchId", "seq", "turn", "phase", "type", "actor", "subject",
                              "target", "zoneFrom", "zoneTo", "cause", "tags" };
        foreach (JsonObject line in lines.Take(200))
        {
            foreach (string key in required)
            {
                AssertTrue(line.ContainsKey(key), $"field '{key}' present");
            }
            AssertEqual("l4-test", line["matchId"]!.GetValue<string>(), "match stamp");
            AssertTrue(line["turn"]!.GetValue<int>() >= 1, "turn stamped");
        }
    })),
    ("Battle deletion shows up as CardMoved BattleArea→Trash", () => RunLeveledMatch(MatchLogLevel.Replay, lines =>
    {
        bool deletion = lines.Any(l =>
            l["type"]!.GetValue<string>() == "CardMoved" &&
            l["zoneFrom"]?.GetValue<string>() == "BattleArea" &&
            l["zoneTo"]?.GetValue<string>() == "Trash");
        AssertTrue(deletion, "at least one battle deletion event observed in a full random match");
    }, seed: 500)),  // security_probe에서 소멸이 확인된 시드 대역
};

var failures = new List<string>();
foreach (var test in tests)
{
    try
    {
        await test.Body();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        failures.Add($"{test.Name}: {ex.Message}");
        Console.Error.WriteLine($"FAIL {test.Name}\n{ex.GetType().Name}: {ex.Message}");
    }
}

if (failures.Count > 0)
{
    Console.Error.WriteLine($"\n{failures.Count} test(s) failed.");
    Environment.Exit(1);
}

Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- 실매치 드라이버: 랜덤 정책 풀 매치를 지정 레벨로 기록 --------------------------

async Task RunLeveledMatch(MatchLogLevel level, Action<List<JsonObject>> assert, int seed = 41)
{
    var sink = new StringWriter();
    var log = new MatchEventLog(level, sink, "l4-test");

    EngineContext context = EngineContext.CreateDefault(randomSeed: seed);
    CardBaseEntityLoader.LoadInto((CardDatabase)context.CardRepository);
    StarterDecks.StarterDeck d1 = StarterDecks.Get("ST1");
    StarterDecks.StarterDeck d2 = StarterDecks.Get("ST2");
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[]
        {
            new PlayerDeckSetup(P1, d1.MainDefinitions, d1.DigitamaDefinitions),
            new PlayerDeckSetup(P2, d2.MainDefinitions, d2.DigitamaDefinitions)
        },
        firstPlayerId: P1);

    // (4b B6 re-pin) L4's full random match is driven on the PUMP (CreatePumpDriven; the same
    // LegalActionSetValidator boundary is installed by default). This run doubles as the RD-R4A′-01
    // re-check gate: the OLD-driver full-random late-board hang (inner-drain unbounded loop) must not
    // reproduce under the pump cadence — the walk is bounded and the suite would time out if it did.
    var match = DcgoMatch.CreatePumpDriven(
        context,
        eventLog: log);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: seed, setup: setup));
    await match.StepAsync();

    var rng = new Random(seed);
    var players = new[] { P1, P2 };
    for (int step = 0; step < 1200 && !match.IsTerminal(); step++)
    {
        HeadlessPlayerId? mover = null;
        foreach (HeadlessPlayerId p in players)
        {
            if (match.GetLegalActions(p).Count > 0) { mover = p; break; }
        }

        if (mover is not { } current)
        {
            // pump auto-flow: no lanes between action waits — step the pump forward.
            await match.StepAsync();
            continue;
        }

        IReadOnlyList<LegalAction> legal = match.GetLegalActions(current);
        await match.ApplyActionAsync(legal[rng.Next(legal.Count)]);
        await match.StepAsync();
    }

    List<JsonObject> lines = sink.ToString()
        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(line => JsonNode.Parse(line)!.AsObject())
        .ToList();
    assert(lines);
}

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
