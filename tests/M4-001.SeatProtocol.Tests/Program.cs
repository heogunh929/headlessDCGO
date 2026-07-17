using System.Text.Json.Nodes;
using HeadlessDCGO.Tools.RlBridgeHost;

// M4: seat 매치 프로토콜 v1 계약 스모크 (docs/audit/rl_seat_protocol_v1.md, 로드맵 M4 게이트).
//  - 합법 0-위반: 마스크의 1인 인덱스만으로 매치가 완주한다(모든 action 수락).
//  - 보상 귀속(🔴1): 승자 +1 / 패자 −1, 스텝캡 0/0.
//  - 결정론: 같은 seed + 같은 행동열 = 같은 관측열·같은 결과.
//  - 차원 일치: welcome의 obsSize/actionSize와 turn의 벡터 길이 일치.
//  - 합법성 경계: 불법 인덱스 → 상태 무변경 error + 같은 turn 재발행.
//  - 프로토콜 위반·미지원 카드 레시피 → 명시 실패.

var tests = new (string Name, Func<Task> Body)[]
{
    ("Handshake advertises stable schema/vocab", HandshakeAdvertisesSchema),
    ("Random-legal policy completes a match with zero rejections", LegalPolicyCompletesMatch),
    ("Rewards attribute winner +1 / loser -1", RewardsAttribution),
    ("Step cap yields zero rewards for both seats", StepCapZeroRewards),
    ("Same seed + same action sequence is deterministic", Determinism),
    ("Illegal action is rejected without state change and turn is re-issued", IllegalActionRejected),
    ("Action for the wrong seat is a protocol violation", WrongSeatRejected),
    ("Recipe decks with unsupported cards fail explicitly", UnsupportedRecipeFails),
    ("Recipe decks play a full match", RecipeDeckPlays),
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
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}");
    }
}

if (failures.Count > 0)
{
    Console.Error.WriteLine($"\n{failures.Count} test(s) failed.");
    Environment.Exit(1);
}

Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests -------------------------------------------------------------------

async Task HandshakeAdvertisesSchema()
{
    var host = new SeatMatchHost();
    JsonObject welcome = await SendAsync(host, Hello());
    AssertEqual("welcome", TypeOf(welcome), "welcome reply");
    AssertTrue(welcome["obsSize"]!.GetValue<int>() > 0, "obs size positive");
    // (B5-3) factored-v2: 599 + the appended multi-select Confirm slot (설계 §B5.7 — prior offsets stable).
    AssertEqual(600, welcome["actionSize"]!.GetValue<int>(), "default factored size (v2)");
    AssertEqual("factored-v2", welcome["actionSchemaVersion"]!.GetValue<string>(), "schema version handshake");
    AssertTrue(welcome["vocabSize"]!.GetValue<int>() > 4000, "vocab covers the card pool");
    AssertEqual(64, welcome["vocabHash"]!.GetValue<string>().Length, "sha256 vocab hash");
    AssertEqual(64, welcome["obsSchemaHash"]!.GetValue<string>().Length, "sha256 obs schema hash");

    // 같은 옵션의 두 호스트는 같은 해시를 낸다(계약의 재현성).
    var second = new SeatMatchHost();
    JsonObject welcome2 = await SendAsync(second, Hello());
    AssertEqual(
        welcome["obsSchemaHash"]!.GetValue<string>(),
        welcome2["obsSchemaHash"]!.GetValue<string>(),
        "obs schema hash stable");
    AssertEqual(
        welcome["vocabHash"]!.GetValue<string>(),
        welcome2["vocabHash"]!.GetValue<string>(),
        "vocab hash stable");
}

async Task LegalPolicyCompletesMatch()
{
    (SeatMatchHost host, JsonObject welcome) = await SessionAsync();
    (JsonObject result, int steps, int rejections) =
        await PlayMatchAsync(host, welcome, seed: 41, policySeed: 5, maxSteps: 3000);

    AssertEqual("result", TypeOf(result), "match reaches result");
    AssertEqual(0, rejections, "zero illegal-action rejections with mask-legal policy");
    AssertTrue(steps > 10, $"match plays through ({steps} steps)");
}

async Task RewardsAttribution()
{
    (SeatMatchHost host, JsonObject welcome) = await SessionAsync();
    JsonObject result = (await PlayMatchAsync(host, welcome, seed: 41, policySeed: 5, maxSteps: 3000)).Result;

    int? winner = result["winnerSeat"]?.GetValue<int>();
    AssertTrue(winner is 1 or 2, "random starter match produces a winner");
    double winnerReward = result["rewards"]![winner!.Value.ToString()]!.GetValue<double>();
    double loserReward = result["rewards"]![(winner.Value == 1 ? "2" : "1")]!.GetValue<double>();
    AssertEqual(1d, winnerReward, "winner seat +1");
    AssertEqual(-1d, loserReward, "loser seat -1");
}

async Task StepCapZeroRewards()
{
    (SeatMatchHost host, JsonObject welcome) = await SessionAsync();
    JsonObject result = (await PlayMatchAsync(host, welcome, seed: 41, policySeed: 5, maxSteps: 5)).Result;

    AssertEqual("step_cap", result["reason"]!.GetValue<string>(), "cap reason");
    AssertEqual(0d, result["rewards"]!["1"]!.GetValue<double>(), "seat 1 zero");
    AssertEqual(0d, result["rewards"]!["2"]!.GetValue<double>(), "seat 2 zero");
}

async Task Determinism()
{
    // 첫 판: 행동열 + 관측 지문 기록.
    (SeatMatchHost host1, JsonObject welcome1) = await SessionAsync();
    (JsonObject result1, List<int> actions1, List<double> obsFingerprint1) =
        await PlayMatchRecordingAsync(host1, welcome1, seed: 77, policySeed: 9);

    // 둘째 판: 기록된 행동열 그대로 재생.
    (SeatMatchHost host2, JsonObject welcome2) = await SessionAsync();
    (JsonObject result2, List<double> obsFingerprint2) =
        await ReplayMatchAsync(host2, welcome2, seed: 77, actions1);

    AssertEqual(result1["winnerSeat"]?.GetValue<int>(), result2["winnerSeat"]?.GetValue<int>(), "same winner");
    AssertEqual(result1["steps"]!.GetValue<int>(), result2["steps"]!.GetValue<int>(), "same step count");
    AssertEqual(obsFingerprint1.Count, obsFingerprint2.Count, "same observation count");
    for (int i = 0; i < obsFingerprint1.Count; i++)
    {
        if (Math.Abs(obsFingerprint1[i] - obsFingerprint2[i]) > 1e-12)
        {
            throw new InvalidOperationException($"observation fingerprint diverged at turn {i}");
        }
    }
}

async Task IllegalActionRejected()
{
    (SeatMatchHost host, _) = await SessionAsync();
    JsonObject turn = await ResetAsync(host, seed: 41);
    int seat = turn["seat"]!.GetValue<int>();
    int firstLegal = FirstLegalIndex(turn);

    // 마스크가 0인 인덱스를 하나 고른다.
    var mask = turn["actionMask"]!.AsArray();
    int illegal = Enumerable.Range(0, mask.Count).First(i => mask[i]!.GetValue<double>() == 0d);

    IReadOnlyList<string> responses = await host.HandleLineAsync(
        new JsonObject { ["type"] = "action", ["seat"] = seat, ["index"] = illegal }.ToJsonString());
    AssertEqual(2, responses.Count, "error + re-issued turn");
    JsonObject error = Parse(responses[0]);
    AssertEqual("error", TypeOf(error), "error reply");
    AssertEqual("illegal_action", error["code"]!.GetValue<string>(), "error code");

    JsonObject reissued = Parse(responses[1]);
    AssertEqual("turn", TypeOf(reissued), "turn re-issued");
    AssertEqual(turn["stepIndex"]!.GetValue<long>(), reissued["stepIndex"]!.GetValue<long>(), "state unchanged");
    AssertEqual(firstLegal, FirstLegalIndex(reissued), "same mask re-issued");
}

async Task WrongSeatRejected()
{
    (SeatMatchHost host, _) = await SessionAsync();
    JsonObject turn = await ResetAsync(host, seed: 41);
    int wrongSeat = turn["seat"]!.GetValue<int>() == 1 ? 2 : 1;

    IReadOnlyList<string> responses = await host.HandleLineAsync(
        new JsonObject { ["type"] = "action", ["seat"] = wrongSeat, ["index"] = FirstLegalIndex(turn) }.ToJsonString());
    AssertEqual(1, responses.Count, "single error");
    JsonObject error = Parse(responses[0]);
    AssertEqual("protocol_violation", error["code"]!.GetValue<string>(), "wrong-seat action rejected");
}

async Task UnsupportedRecipeFails()
{
    (SeatMatchHost host, _) = await SessionAsync();
    var recipe = new JsonObject
    {
        ["name"] = "bogus",
        ["main"] = new JsonArray(
            new JsonObject { ["card"] = "ZZ9_999", ["count"] = 4 },
            new JsonObject { ["card"] = "ST1_02", ["count"] = 4 }),
        ["digitama"] = new JsonArray(new JsonObject { ["card"] = "ST1_01", ["count"] = 4 })
    };

    IReadOnlyList<string> responses = await host.HandleLineAsync(new JsonObject
    {
        ["type"] = "reset",
        ["seed"] = 1,
        ["decks"] = new JsonObject { ["1"] = recipe, ["2"] = "starter:ST2" }
    }.ToJsonString());

    JsonObject error = Parse(responses[0]);
    AssertEqual("bad_deck", error["code"]!.GetValue<string>(), "unsupported cards rejected");
    AssertTrue(error["message"]!.GetValue<string>().Contains("ZZ9_999"), "missing card named explicitly");
}

async Task RecipeDeckPlays()
{
    (SeatMatchHost host, JsonObject welcome) = await SessionAsync();

    // ST1 스타터를 내부 표준 레시피 형태로 표현(공급 통로 동등성 — FR-3.4).
    var recipe = new JsonObject
    {
        ["name"] = "st1_as_recipe",
        ["main"] = new JsonArray(
            new[] { ("ST1_02", 4), ("ST1_03", 4), ("ST1_04", 4), ("ST1_05", 4), ("ST1_06", 4), ("ST1_07", 2),
                    ("ST1_08", 4), ("ST1_09", 4), ("ST1_10", 2), ("ST1_11", 2), ("ST1_12", 4), ("ST1_13", 4),
                    ("ST1_14", 4), ("ST1_15", 2), ("ST1_16", 2) }
                .Select(e => (JsonNode)new JsonObject { ["card"] = e.Item1, ["count"] = e.Item2 })
                .ToArray()),
        ["digitama"] = new JsonArray(new JsonObject { ["card"] = "ST1_01", ["count"] = 4 })
    };

    JsonObject turn = await ResetAsync(host, seed: 55, decksOverride: new JsonObject
    {
        ["1"] = recipe,
        ["2"] = "starter:ST2"
    });
    AssertEqual("turn", TypeOf(turn), "recipe deck match starts");

    (JsonObject result, _, int rejections) = await ContinueMatchAsync(host, welcome, turn, policySeed: 3, guard: 4000);
    AssertEqual("result", TypeOf(result), "recipe deck match completes");
    AssertEqual(0, rejections, "no rejections");
}

// --- Protocol driver helpers ---------------------------------------------------

static string Hello() => new JsonObject { ["type"] = "hello", ["protocol"] = 1 }.ToJsonString();

static async Task<JsonObject> SendAsync(SeatMatchHost host, string line)
{
    IReadOnlyList<string> responses = await host.HandleLineAsync(line);
    AssertEqual(1, responses.Count, "single reply expected");
    return Parse(responses[0]);
}

static async Task<(SeatMatchHost Host, JsonObject Welcome)> SessionAsync()
{
    var host = new SeatMatchHost();
    JsonObject welcome = await SendAsync(host, Hello());
    JsonObject claimed = await SendAsync(host, new JsonObject
    {
        ["type"] = "claim",
        ["seats"] = new JsonArray(1, 2)
    }.ToJsonString());
    AssertEqual("claimed", TypeOf(claimed), "claim ok");
    return (host, welcome);
}

static async Task<JsonObject> ResetAsync(SeatMatchHost host, int seed, int maxSteps = 3000, JsonObject? decksOverride = null)
{
    JsonObject reset = new()
    {
        ["type"] = "reset",
        ["seed"] = seed,
        ["maxSteps"] = maxSteps,
        ["decks"] = decksOverride ?? new JsonObject { ["1"] = "starter:ST1", ["2"] = "starter:ST2" }
    };
    return await SendAsync(host, reset.ToJsonString());
}

static async Task<(JsonObject Result, int Steps, int Rejections)> PlayMatchAsync(
    SeatMatchHost host, JsonObject welcome, int seed, int policySeed, int maxSteps)
{
    JsonObject first = await ResetAsync(host, seed, maxSteps);
    return await ContinueMatchAsync(host, welcome, first, policySeed, guard: maxSteps + 100);
}

static async Task<(JsonObject Result, int Steps, int Rejections)> ContinueMatchAsync(
    SeatMatchHost host, JsonObject welcome, JsonObject current, int policySeed, int guard)
{
    var rng = new Random(policySeed);
    int steps = 0, rejections = 0;
    int obsSize = welcome["obsSize"]!.GetValue<int>();
    int actionSize = welcome["actionSize"]!.GetValue<int>();

    while (TypeOf(current) == "turn" && steps < guard)
    {
        AssertEqual(obsSize, current["observation"]!.AsArray().Count, "obs dimension matches welcome");
        AssertEqual(actionSize, current["actionMask"]!.AsArray().Count, "mask dimension matches welcome");

        int index = RandomLegalIndex(current, rng);
        IReadOnlyList<string> responses = await host.HandleLineAsync(new JsonObject
        {
            ["type"] = "action",
            ["seat"] = current["seat"]!.GetValue<int>(),
            ["index"] = index
        }.ToJsonString());

        JsonObject reply = Parse(responses[0]);
        if (TypeOf(reply) == "error")
        {
            rejections++;
            current = Parse(responses[1]);
            continue;
        }

        steps++;
        current = reply;
    }

    return (current, steps, rejections);
}

static async Task<(JsonObject Result, List<int> Actions, List<double> ObsFingerprint)> PlayMatchRecordingAsync(
    SeatMatchHost host, JsonObject welcome, int seed, int policySeed)
{
    var rng = new Random(policySeed);
    var actions = new List<int>();
    var fingerprint = new List<double>();
    JsonObject current = await ResetAsync(host, seed);

    while (TypeOf(current) == "turn")
    {
        fingerprint.Add(current["observation"]!.AsArray().Sum(n => n!.GetValue<double>()));
        int index = RandomLegalIndex(current, rng);
        actions.Add(index);
        current = await SendAsync(host, new JsonObject
        {
            ["type"] = "action",
            ["seat"] = current["seat"]!.GetValue<int>(),
            ["index"] = index
        }.ToJsonString());
    }

    return (current, actions, fingerprint);
}

static async Task<(JsonObject Result, List<double> ObsFingerprint)> ReplayMatchAsync(
    SeatMatchHost host, JsonObject welcome, int seed, List<int> actions)
{
    var fingerprint = new List<double>();
    JsonObject current = await ResetAsync(host, seed);

    foreach (int index in actions)
    {
        AssertEqual("turn", TypeOf(current), "replay expects a turn");
        fingerprint.Add(current["observation"]!.AsArray().Sum(n => n!.GetValue<double>()));
        current = await SendAsync(host, new JsonObject
        {
            ["type"] = "action",
            ["seat"] = current["seat"]!.GetValue<int>(),
            ["index"] = index
        }.ToJsonString());
    }

    AssertEqual("result", TypeOf(current), "replay ends at result");
    return (current, fingerprint);
}

static int RandomLegalIndex(JsonObject turn, Random rng)
{
    var mask = turn["actionMask"]!.AsArray();
    int[] legal = Enumerable.Range(0, mask.Count).Where(i => mask[i]!.GetValue<double>() == 1d).ToArray();
    AssertTrue(legal.Length > 0, "turn has at least one legal index");
    return legal[rng.Next(legal.Length)];
}

static int FirstLegalIndex(JsonObject turn)
{
    var mask = turn["actionMask"]!.AsArray();
    return Enumerable.Range(0, mask.Count).First(i => mask[i]!.GetValue<double>() == 1d);
}

static JsonObject Parse(string line) => JsonNode.Parse(line)!.AsObject();

static string TypeOf(JsonObject obj) => obj["type"]?.GetValue<string>() ?? string.Empty;

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
