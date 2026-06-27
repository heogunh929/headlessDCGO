using System.Collections;
using System.Text;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();
HeadlessPlayerId PlayerOne = new(1);
HeadlessEntityId EffectId = new("effect-once");
HeadlessEntityId SourceId = new("source-card");

var tests = new (string Name, Func<Task> Body)[]
{
    ("G3L-001 goal row and predecessor are satisfied", GoalRowAndPredecessorAreSatisfied),
    ("AS-IS once flag references are recorded", AsIsOnceFlagReferencesAreRecorded),
    ("First once per turn use is allowed and registered", FirstUseIsAllowedAndRegistered),
    ("Second once per turn use is blocked without mutating state", SecondUseIsBlockedWithoutMutation),
    ("Max count per turn allows limited repeated uses", MaxCountAllowsLimitedUses),
    ("Timing scoped flags separate same effect by timing", TimingScopedFlagsSeparateTiming),
    ("Turn reset clears registered flags deterministically", TurnResetClearsFlags),
    ("Remove use decrements and removes empty flag entries", RemoveUseDecrements),
    ("Use count can be written into effect context for CanUse helpers", UseCountWritesToEffectContext),
    ("Assets facade delegates and source files stay inside G3L scope", AssetsFacadeAndSourceScope),
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
        failures.Add($"{test.Name}: {ex.GetType().Name}: {ex.Message}");
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}");
    }
}

if (failures.Count > 0)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine($"{failures.Count} test(s) failed.");
    Environment.Exit(1);
}

Console.WriteLine();
Console.WriteLine($"{tests.Length} test(s) passed.");

Task GoalRowAndPredecessorAreSatisfied()
{
    List<Dictionary<string, string>> rows = ReadCsv(Path.Combine(root, "docs", "headless_complete_goal_breakdown.csv"));
    Dictionary<string, string> row = rows.SingleOrDefault(row => Value(row, "goal_id") == "G3L-001")
        ?? throw new InvalidOperationException("G3L-001 row was not found.");

    AssertEqual("Phase 3", Value(row, "phase"), "phase");
    AssertEqual("Flags", Value(row, "area"), "area");
    AssertContains(Value(row, "scope"), "turn-scoped flag", "scope");
    AssertEqual("once flag helpers", Value(row, "deliverables"), "deliverables");
    AssertEqual("once per turn 테스트", Value(row, "unit_test_scope"), "unit test scope");
    AssertEqual("docs/test-results/goals/G3L-001_once_per_turn_flags_unit_test_results.md", Value(row, "result_document"), "result document");
    AssertEqual("G3K-002", Value(row, "blocked_until"), "predecessor");
    AssertContains(Value(row, "completion_gate"), "once flag", "completion gate");
    AssertComplete("G3K-002_timing_priority_helpers_unit_test_results.md");
    return Task.CompletedTask;
}

Task AsIsOnceFlagReferencesAreRecorded()
{
    string controller = ReadAsIs("CEntity_EffectController.cs");
    string turnState = ReadAsIs("TurnStateMachine.cs");
    string effect = ReadAsIs("ICardEffect.cs");
    string autoProcessing = ReadAsIs("AutoProcessing.cs");

    AssertContains(controller, "UseEffectsThisTurn", "AS-IS use list");
    AssertContains(controller, "InitUseCountThisTurn", "AS-IS reset method");
    AssertContains(controller, "RegisterUseEffectThisTurn", "AS-IS register method");
    AssertContains(controller, "RemoveUseEffectThisTurn", "AS-IS remove method");
    AssertContains(turnState, "Reset the number of times the effect is used", "AS-IS turn reset section");
    AssertContains(turnState, "InitUseCountThisTurn", "AS-IS turn reset calls");
    AssertContains(effect, "MaxCountPerTurn", "AS-IS max count");
    AssertContains(autoProcessing, "RegisterUseEffectThisTurn", "AS-IS activation records use");
    return Task.CompletedTask;
}

Task FirstUseIsAllowedAndRegistered()
{
    OnceFlagState state = new(turnSequence: 12, turnPlayerId: PlayerOne);
    OnceFlagKey key = Key();

    OnceFlagResult before = OnceFlagHelpers.CanUse(state, key);
    OnceFlagResult after = OnceFlagHelpers.RegisterUse(state, key);

    AssertTrue(before.IsSuccess, "before success");
    AssertTrue(before.CanUse, "first can use");
    AssertEqual(0, before.UseCount, "before count");
    AssertTrue(after.IsSuccess, "after success");
    AssertFalse(after.CanUse, "after max reached");
    AssertEqual(1, after.UseCount, "after count");
    AssertEqual(1, after.State.GetUseCount(key), "state count");
    AssertSequence(new[] { key.Value }, Strings(after.Values[OnceFlagHelpers.ActiveFlagKeysKey]), "active keys");
    return Task.CompletedTask;
}

Task SecondUseIsBlockedWithoutMutation()
{
    OnceFlagKey key = Key();
    OnceFlagResult first = OnceFlagHelpers.RegisterUse(OnceFlagState.Empty, key);

    OnceFlagResult second = OnceFlagHelpers.RegisterUse(first.State, key);

    AssertFalse(second.IsSuccess, "second success");
    AssertFalse(second.CanUse, "second can use");
    AssertContains(second.FailureReason, "max count", "failure reason");
    AssertEqual(1, second.State.GetUseCount(key), "state not mutated");
    AssertEqual(first.State, second.State, "same state value");
    return Task.CompletedTask;
}

Task MaxCountAllowsLimitedUses()
{
    OnceFlagKey key = Key();
    OnceFlagResult first = OnceFlagHelpers.RegisterUse(OnceFlagState.Empty, key, maxCount: 2);
    OnceFlagResult second = OnceFlagHelpers.RegisterUse(first.State, key, maxCount: 2);
    OnceFlagResult third = OnceFlagHelpers.RegisterUse(second.State, key, maxCount: 2);

    AssertTrue(first.IsSuccess, "first success");
    AssertTrue(second.IsSuccess, "second success");
    AssertEqual(2, second.UseCount, "second count");
    AssertFalse(second.CanUse, "second reaches max");
    AssertFalse(third.IsSuccess, "third blocked");
    AssertEqual(2, third.State.GetUseCount(key), "third not mutated");
    return Task.CompletedTask;
}

Task TimingScopedFlagsSeparateTiming()
{
    EffectRequest request = Request("effect-same", "OnPlay");
    OnceFlagKey onPlay = OnceFlagHelpers.ForRequest(request, OnceFlagScope.Timing);
    OnceFlagKey onDelete = OnceFlagHelpers.ForRequest(request, OnceFlagScope.Timing, "OnDeletion");

    OnceFlagResult usedOnPlay = OnceFlagHelpers.RegisterUse(OnceFlagState.Empty, onPlay);
    OnceFlagResult canUseOnDelete = OnceFlagHelpers.CanUse(usedOnPlay.State, onDelete);

    AssertEqual("OnPlay", onPlay.Timing, "on play timing");
    AssertEqual("OnDeletion", onDelete.Timing, "on delete timing");
    AssertTrue(canUseOnDelete.CanUse, "different timing can use");
    AssertEqual(0, canUseOnDelete.UseCount, "different timing count");
    AssertNotEqual(onPlay.Value, onDelete.Value, "timing key value");
    return Task.CompletedTask;
}

Task TurnResetClearsFlags()
{
    OnceFlagKey key = Key();
    OnceFlagResult used = OnceFlagHelpers.RegisterUse(new OnceFlagState(3, PlayerOne), key);

    OnceFlagResult reset = OnceFlagHelpers.ResetTurn(used.State, nextTurnSequence: 4, nextTurnPlayerId: new HeadlessPlayerId(2));

    AssertTrue(reset.IsSuccess, "reset success");
    AssertEqual(4L, reset.State.TurnSequence, "turn sequence");
    AssertEqual(new HeadlessPlayerId(2), reset.State.TurnPlayerId, "turn player");
    AssertEqual(0, reset.State.UseCounts.Count, "reset counts");
    AssertTrue(OnceFlagHelpers.CanUse(reset.State, key).CanUse, "can use after reset");
    AssertEqual(Signature(reset), Signature(OnceFlagHelpers.ResetTurn(used.State, 4, new HeadlessPlayerId(2))), "deterministic reset");
    return Task.CompletedTask;
}

Task RemoveUseDecrements()
{
    OnceFlagKey key = Key();
    OnceFlagResult first = OnceFlagHelpers.RegisterUse(OnceFlagState.Empty, key, maxCount: 2);
    OnceFlagResult second = OnceFlagHelpers.RegisterUse(first.State, key, maxCount: 2);
    OnceFlagResult removedOne = OnceFlagHelpers.RemoveUse(second.State, key);
    OnceFlagResult removedTwo = OnceFlagHelpers.RemoveUse(removedOne.State, key);
    OnceFlagResult removedMissing = OnceFlagHelpers.RemoveUse(removedTwo.State, key);

    AssertTrue(removedOne.IsSuccess, "remove one");
    AssertEqual(1, removedOne.State.GetUseCount(key), "count after one remove");
    AssertTrue(removedTwo.IsSuccess, "remove two");
    AssertEqual(0, removedTwo.State.GetUseCount(key), "count after two removes");
    AssertEqual(0, removedTwo.State.UseCounts.Count, "empty key removed");
    AssertFalse(removedMissing.IsSuccess, "missing remove failure");
    return Task.CompletedTask;
}

Task UseCountWritesToEffectContext()
{
    EffectRequest request = Request();
    OnceFlagKey key = OnceFlagHelpers.ForRequest(request);
    OnceFlagResult registered = OnceFlagHelpers.RegisterUse(OnceFlagState.Empty, key);

    EffectContext context = OnceFlagHelpers.WithUseCount(request.Context, registered.State, key);

    AssertEqual(1, context.GetRequiredValue<int>(CanUseEffectHelpers.UseCountThisTurnKey), "CanUse use count key");
    AssertEqual(key.Value, context.GetRequiredValue<string>(OnceFlagHelpers.FlagKeyValueKey), "flag key");
    AssertEqual("kept", context.GetRequiredValue<string>("fixture"), "existing value retained");
    return Task.CompletedTask;
}

Task AssetsFacadeAndSourceScope()
{
    EffectRequest request = Request("facade-effect", "Main");
    OnceFlagKey key = OnceFlagHelperFactory.ForRequest(request);
    OnceFlagResult result = OnceFlagHelperFactory.RegisterUse(OnceFlagState.Empty, key);
    EffectContext context = OnceFlagHelperFactory.WithUseCount(request.Context, result.State, key);

    AssertTrue(result.IsSuccess, "facade register");
    AssertEqual(1, context.GetRequiredValue<int>(CanUseEffectHelpers.UseCountThisTurnKey), "facade context count");

    string headlessPath = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Assets", "Scripts", "Script", "CardEffectCommons", "OnceFlagHelpers.cs");
    string facadePath = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Assets", "Scripts", "Script", "CardEffectCommons", "OnceFlagHelpers.cs");
    string testPath = Path.Combine(root, "tests", "G3L-001.Once.per.turn.flag.helper.Tests", "Program.cs");

    AssertTrue(File.Exists(headlessPath), "headless helper exists");
    AssertTrue(File.Exists(facadePath), "facade helper exists");
    AssertTrue(File.Exists(testPath), "test file exists");
    AssertDoesNotContain(File.ReadAllText(headlessPath), "UnityEngine", "headless Unity dependency");
    AssertDoesNotContain(File.ReadAllText(facadePath), "UnityEngine", "facade Unity dependency");
    AssertDoesNotContain(File.ReadAllText(headlessPath), "TODO", "headless TODO");
    AssertDoesNotContain(File.ReadAllText(facadePath), "TODO", "facade TODO");
    return Task.CompletedTask;
}

OnceFlagKey Key()
{
    return new OnceFlagKey(EffectId, SourceId, PlayerOne, OnceFlagScope.Turn);
}

EffectRequest Request(string effectId = "effect-once", string timing = "Main")
{
    return new EffectRequest(
        new HeadlessEntityId(effectId),
        PlayerOne,
        timing,
        new EffectContext(
            PlayerOne,
            PlayerOne,
            SourceId,
            triggerEntityId: null,
            targetEntityIds: Array.Empty<HeadlessEntityId>(),
            values: new Dictionary<string, object?> { ["fixture"] = "kept" }));
}

string Signature(OnceFlagResult result)
{
    return string.Join(
        "|",
        $"success={result.IsSuccess}",
        $"canUse={result.CanUse}",
        $"count={result.UseCount}",
        $"turn={result.State.TurnSequence}",
        $"keys={string.Join(",", result.State.UseCounts.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}:{pair.Value}"))}",
        $"values={string.Join(";", result.Values.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={ValueToString(pair.Value)}"))}");
}

string[] Strings(object? value)
{
    if (value is null)
    {
        return Array.Empty<string>();
    }

    if (value is string text)
    {
        return new[] { text };
    }

    if (value is IEnumerable enumerable)
    {
        return enumerable.Cast<object?>().Select(item => item?.ToString() ?? string.Empty).ToArray();
    }

    return new[] { value.ToString() ?? string.Empty };
}

string ValueToString(object? value)
{
    return string.Join(",", Strings(value));
}

string ReadAsIs(string relativePath)
{
    return File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", relativePath));
}

void AssertComplete(string fileName)
{
    string path = Path.Combine(root, "docs", "test-results", "goals", fileName);
    AssertTrue(File.Exists(path), $"predecessor result document {fileName}");
    AssertContains(File.ReadAllText(path), "COMPLETE", $"predecessor completion {fileName}");
}

List<Dictionary<string, string>> ReadCsv(string path)
{
    string[] lines = File.ReadAllLines(path);
    string[] headers = SplitCsvLine(lines[0]).ToArray();
    var rows = new List<Dictionary<string, string>>();

    foreach (string line in lines.Skip(1))
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            continue;
        }

        string[] values = SplitCsvLine(line).ToArray();
        var row = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < headers.Length; i++)
        {
            row[headers[i]] = i < values.Length ? values[i] : string.Empty;
        }

        rows.Add(row);
    }

    return rows;
}

IEnumerable<string> SplitCsvLine(string line)
{
    var value = new StringBuilder();
    bool inQuotes = false;

    for (int i = 0; i < line.Length; i++)
    {
        char ch = line[i];
        if (ch == '"')
        {
            if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
            {
                value.Append('"');
                i++;
            }
            else
            {
                inQuotes = !inQuotes;
            }

            continue;
        }

        if (ch == ',' && !inQuotes)
        {
            yield return value.ToString();
            value.Clear();
            continue;
        }

        value.Append(ch);
    }

    yield return value.ToString();
}

string Value(Dictionary<string, string> row, string key)
{
    return row.TryGetValue(key, out string? value) ? value : string.Empty;
}

string FindRepositoryRoot()
{
    DirectoryInfo? directory = new(Environment.CurrentDirectory);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "docs", "headless_complete_goal_breakdown.csv")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    throw new InvalidOperationException("Repository root was not found.");
}

void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', actual '{actual}'.");
    }
}

void AssertNotEqual<T>(T expectedDifferent, T actual, string label)
{
    if (EqualityComparer<T>.Default.Equals(expectedDifferent, actual))
    {
        throw new InvalidOperationException($"{label}: expected values to differ.");
    }
}

void AssertTrue(bool condition, string label)
{
    if (!condition)
    {
        throw new InvalidOperationException($"{label}: expected true.");
    }
}

void AssertFalse(bool condition, string label)
{
    if (condition)
    {
        throw new InvalidOperationException($"{label}: expected false.");
    }
}

void AssertContains(string text, string expected, string label)
{
    if (!text.Contains(expected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"{label}: expected text to contain '{expected}'.");
    }
}

void AssertDoesNotContain(string text, string unexpected, string label)
{
    if (text.Contains(unexpected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"{label}: text must not contain '{unexpected}'.");
    }
}

void AssertSequence<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual, string label)
{
    if (!expected.SequenceEqual(actual))
    {
        throw new InvalidOperationException(
            $"{label}: expected [{string.Join(", ", expected)}], actual [{string.Join(", ", actual)}].");
    }
}
