using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G1E-003 goal row keeps the ScriptedChoiceProvider contract", GoalRowKeepsExpectedContract),
    ("ScriptedChoiceProvider returns queued results in deterministic FIFO order", ScriptedChoiceProviderReturnsQueuedResultsInOrder),
    ("ScriptedChoiceProvider constructor and enqueue reject null choices", ScriptedChoiceProviderRejectsNullChoices),
    ("ScriptedChoiceProvider does not dequeue invalid scripted result", ScriptedChoiceProviderDoesNotDequeueInvalidResult),
    ("ScriptedChoiceProvider fallback skips when request allows skip", ScriptedChoiceProviderFallbackSkipsWhenAllowed),
    ("ScriptedChoiceProvider fallback selects request minimum selectable ids", ScriptedChoiceProviderFallbackSelectsMinimumSelectableIds),
    ("ScriptedChoiceProvider fallback selects minimum count for count request", ScriptedChoiceProviderFallbackSelectsMinimumCount),
    ("ScriptedChoiceProvider honors cancellation before consuming queue", ScriptedChoiceProviderHonorsCancellationBeforeConsumingQueue),
    ("AS-IS scripted choice references remain read-only inputs", AsIsScriptedChoiceReferencesRemainReadOnlyInputs),
    ("ScriptedChoiceProvider source file has no placeholder or Unity dependency", ScriptedChoiceProviderSourceHasNoPlaceholderOrUnityDependency),
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

Task GoalRowKeepsExpectedContract()
{
    var rows = ReadCsv(Path.Combine(root, "docs", "headless_complete_goal_breakdown.csv"));
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G1E-003")
        ?? throw new InvalidOperationException("G1E-003 row was not found.");

    AssertEqual("Phase 1", Value(row, "phase"), "phase");
    AssertEqual("Choices", Value(row, "area"), "area");
    AssertEqual("ScriptedChoiceProvider", Value(row, "goal"), "goal");
    AssertEqual("scripted deterministic choice 확정", Value(row, "scope"), "scope");
    AssertEqual("ScriptedChoiceProvider", Value(row, "deliverables"), "deliverables");
    AssertEqual("queued result deterministic 테스트", Value(row, "unit_test_scope"), "unit test scope");
    AssertEqual("docs/test-results/goals/G1E-003_scripted_choice_provider_unit_test_results.md", Value(row, "result_document"), "result document");
    AssertEqual("G1E-002", Value(row, "blocked_until"), "blocked_until");
    AssertEqual("Scripted provider 테스트 통과", Value(row, "completion_gate"), "completion gate");
    return Task.CompletedTask;
}

async Task ScriptedChoiceProviderReturnsQueuedResultsInOrder()
{
    ChoiceRequest request = CardRequest(min: 1, max: 2, canSkip: false);
    var first = ChoiceResult.Select(new HeadlessEntityId("card-a"));
    var second = ChoiceResult.Select(new HeadlessEntityId("card-b"));
    var provider = new ScriptedChoiceProvider(new[] { first, second });

    ChoiceResult actualFirst = await provider.ChooseAsync(request);
    ChoiceResult actualSecond = await provider.ChooseAsync(request);

    AssertSameResult(first, actualFirst, "first result");
    AssertSameResult(second, actualSecond, "second result");
    AssertEqual(0, provider.Count, "queue count");
}

Task ScriptedChoiceProviderRejectsNullChoices()
{
    ExpectThrows<ArgumentNullException>(() => new ScriptedChoiceProvider(null!));
    ExpectThrows<ArgumentNullException>(() => new ScriptedChoiceProvider().Enqueue(null!));

    IEnumerable<ChoiceResult> choices = new ChoiceResult[] { null! };
    ExpectThrows<ArgumentNullException>(() => new ScriptedChoiceProvider(choices));
    return Task.CompletedTask;
}

async Task ScriptedChoiceProviderDoesNotDequeueInvalidResult()
{
    var provider = new ScriptedChoiceProvider(new[] { ChoiceResult.Skip() });
    ChoiceRequest noSkip = CardRequest(min: 1, max: 1, canSkip: false);

    InvalidOperationException ex = ExpectThrows<InvalidOperationException>(
        () => provider.ChooseAsync(noSkip).GetAwaiter().GetResult());

    AssertTrue(ex.Message.Contains("does not allow skipping", StringComparison.Ordinal), "invalid message");
    AssertEqual(1, provider.Count, "invalid result retained");

    ChoiceResult retry = await provider.ChooseAsync(CardRequest(min: 0, max: 1, canSkip: true));
    AssertTrue(retry.IsSkipped, "retry returns original skip");
    AssertEqual(0, provider.Count, "queue consumed after valid retry");
}

async Task ScriptedChoiceProviderFallbackSkipsWhenAllowed()
{
    var provider = new ScriptedChoiceProvider();
    ChoiceResult result = await provider.ChooseAsync(CardRequest(min: 0, max: 2, canSkip: true));

    AssertTrue(result.IsSkipped, "fallback skip");
    AssertEqual(0, result.SelectedIds.Count, "fallback skip ids");
    AssertEqual(null, result.SelectedCount, "fallback skip count");
    AssertEqual(0, provider.Count, "queue count");
}

async Task ScriptedChoiceProviderFallbackSelectsMinimumSelectableIds()
{
    var provider = new ScriptedChoiceProvider();
    ChoiceResult result = await provider.ChooseAsync(CardRequest(min: 2, max: 2, canSkip: false));

    AssertFalse(result.IsSkipped, "fallback selected");
    AssertEqual(2, result.SelectedIds.Count, "selected id count");
    AssertEqual(new HeadlessEntityId("card-a"), result.SelectedIds[0], "first selected id");
    AssertEqual(new HeadlessEntityId("card-b"), result.SelectedIds[1], "second selected id");
    AssertEqual(null, result.SelectedCount, "selected count");
}

async Task ScriptedChoiceProviderFallbackSelectsMinimumCount()
{
    var provider = new ScriptedChoiceProvider();
    ChoiceResult result = await provider.ChooseAsync(CountRequest(min: 2, max: 5));

    AssertFalse(result.IsSkipped, "fallback count skipped");
    AssertEqual(0, result.SelectedIds.Count, "count result ids");
    AssertEqual(2, result.SelectedCount, "count result");
}

Task ScriptedChoiceProviderHonorsCancellationBeforeConsumingQueue()
{
    var provider = new ScriptedChoiceProvider(new[] { ChoiceResult.Select(new HeadlessEntityId("card-a")) });
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();

    ExpectThrows<OperationCanceledException>(
        () => provider.ChooseAsync(CardRequest(min: 1, max: 1, canSkip: false), cancellation.Token).GetAwaiter().GetResult());

    AssertEqual(1, provider.Count, "canceled queue count");
    return Task.CompletedTask;
}

Task AsIsScriptedChoiceReferencesRemainReadOnlyInputs()
{
    var references = new (string Path, string[] Patterns)[]
    {
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "SelectCardEffect.cs"),
            new[] { "SelectCardEffect", "SelectedList", "_canEndSelectCondition", "_canNoSelect" }),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "SelectPermanentEffect.cs"),
            new[] { "SelectPermanentEffect", "PermanentSelection", "_canNoSelect", "_maxCount" }),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "SelectCountEffect.cs"),
            new[] { "SelectCountEffect", "ValueSelection", "_maxCount" }),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "SelectHandEffect.cs"),
            new[] { "SelectHandEffect", "CardSelection" }),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "SelectAttackEffect.cs"),
            new[] { "SelectAttackEffect" }),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "PlayerSelection", "CardSelection.cs"),
            new[] { "CardSelection", "CardIDList" }),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "PlayerSelection", "PermanentSelection.cs"),
            new[] { "PermanentSelection", "PermanentIDList" }),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "PlayerSelection", "ValueSelection.cs"),
            new[] { "ValueSelection", "ValueAsInt" }),
    };

    foreach ((string path, string[] patterns) in references)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"AS-IS reference file was not found: {path}");
        }

        string text = File.ReadAllText(path);
        foreach (string pattern in patterns)
        {
            AssertTrue(text.Contains(pattern, StringComparison.Ordinal), $"{Path.GetFileName(path)} contains {pattern}");
        }
    }

    return Task.CompletedTask;
}

Task ScriptedChoiceProviderSourceHasNoPlaceholderOrUnityDependency()
{
    string path = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Choices", "ScriptedChoiceProvider.cs");
    string text = File.ReadAllText(path);

    if (text.Contains("TODO", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("ScriptedChoiceProvider still contains a TODO placeholder.");
    }

    AssertFalse(text.Contains("UnityEngine", StringComparison.Ordinal), "ScriptedChoiceProvider must not reference UnityEngine");
    AssertFalse(text.Contains("MonoBehaviour", StringComparison.Ordinal), "ScriptedChoiceProvider must not reference MonoBehaviour");
    AssertTrue(text.Contains("ThrowIfInvalid", StringComparison.Ordinal), "ScriptedChoiceProvider validates scripted choices");
    AssertTrue(text.Contains("_choices.Peek()", StringComparison.Ordinal), "ScriptedChoiceProvider validates before dequeue");
    return Task.CompletedTask;
}

static ChoiceRequest CardRequest(int min, int max, bool canSkip)
{
    var player = new HeadlessPlayerId(1);
    return new ChoiceRequest(
        ChoiceType.Card,
        player,
        "Select cards",
        min,
        max,
        canSkip,
        ChoiceZone.Hand,
        new[]
        {
            new ChoiceCandidate(new HeadlessEntityId("card-a"), "Card A", ChoiceZone.Hand, IsSelectable: true, player),
            new ChoiceCandidate(new HeadlessEntityId("card-b"), "Card B", ChoiceZone.Hand, IsSelectable: true, player),
            new ChoiceCandidate(new HeadlessEntityId("card-c"), "Card C", ChoiceZone.Hand, IsSelectable: false, player),
        });
}

static ChoiceRequest CountRequest(int min, int max)
{
    return new ChoiceRequest(
        ChoiceType.Count,
        new HeadlessPlayerId(1),
        "Choose count",
        min,
        max,
        canSkip: false,
        ChoiceZone.None,
        Array.Empty<ChoiceCandidate>());
}

static void AssertSameResult(ChoiceResult expected, ChoiceResult actual, string label)
{
    AssertEqual(expected.IsSkipped, actual.IsSkipped, $"{label} skipped");
    AssertEqual(expected.SelectedCount, actual.SelectedCount, $"{label} selected count");
    AssertEqual(expected.SelectedIds.Count, actual.SelectedIds.Count, $"{label} selected id count");

    for (var index = 0; index < expected.SelectedIds.Count; index++)
    {
        AssertEqual(expected.SelectedIds[index], actual.SelectedIds[index], $"{label} selected id {index}");
    }
}

static List<Dictionary<string, string>> ReadCsv(string path)
{
    if (!File.Exists(path))
    {
        throw new FileNotFoundException($"CSV file was not found: {path}");
    }

    var records = ParseCsv(File.ReadAllText(path));
    if (records.Count == 0)
    {
        throw new InvalidOperationException($"CSV file has no header row: {path}");
    }

    var headers = records[0];
    var rows = new List<Dictionary<string, string>>();
    foreach (var record in records.Skip(1))
    {
        if (record.Count != headers.Count)
        {
            throw new InvalidOperationException($"{path} has a row with {record.Count} fields; expected {headers.Count}.");
        }

        var row = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < headers.Count; i++)
        {
            row[headers[i]] = record[i];
        }

        rows.Add(row);
    }

    return rows;
}

static List<List<string>> ParseCsv(string text)
{
    var records = new List<List<string>>();
    var record = new List<string>();
    var field = new System.Text.StringBuilder();
    var inQuotes = false;

    for (var i = 0; i < text.Length; i++)
    {
        var ch = text[i];
        if (inQuotes)
        {
            if (ch == '"')
            {
                if (i + 1 < text.Length && text[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = false;
                }
            }
            else
            {
                field.Append(ch);
            }

            continue;
        }

        switch (ch)
        {
            case '"':
                inQuotes = true;
                break;
            case ',':
                record.Add(field.ToString());
                field.Clear();
                break;
            case '\r':
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                AddRecord();
                break;
            case '\n':
                AddRecord();
                break;
            default:
                field.Append(ch);
                break;
        }
    }

    if (inQuotes)
    {
        throw new InvalidOperationException("CSV has an unterminated quoted field.");
    }

    if (field.Length > 0 || record.Count > 0)
    {
        AddRecord();
    }

    return records;

    void AddRecord()
    {
        record.Add(field.ToString());
        field.Clear();

        if (record.Count > 1 || record[0].Length > 0)
        {
            records.Add(record);
        }

        record = new List<string>();
    }
}

static string FindRepositoryRoot()
{
    var current = new DirectoryInfo(AppContext.BaseDirectory);
    while (current is not null)
    {
        var docsPath = Path.Combine(current.FullName, "docs", "headless_complete_goal_breakdown.csv");
        if (File.Exists(docsPath))
        {
            return current.FullName;
        }

        current = current.Parent;
    }

    throw new DirectoryNotFoundException("Could not find docs/headless_complete_goal_breakdown.csv from the test binary path.");
}

static string Value(IReadOnlyDictionary<string, string> row, string key)
{
    return row.TryGetValue(key, out var value)
        ? value
        : throw new InvalidOperationException($"Missing key '{key}'.");
}

static TException ExpectThrows<TException>(Action action)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException ex)
    {
        return ex;
    }

    throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
}

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', actual '{actual}'.");
    }
}

static void AssertTrue(bool value, string label)
{
    if (!value)
    {
        throw new InvalidOperationException($"{label}: expected true.");
    }
}

static void AssertFalse(bool value, string label)
{
    if (value)
    {
        throw new InvalidOperationException($"{label}: expected false.");
    }
}
