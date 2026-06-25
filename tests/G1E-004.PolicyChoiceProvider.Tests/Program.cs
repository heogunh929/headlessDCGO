using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G1E-004 goal row keeps the PolicyChoiceProvider contract", GoalRowKeepsExpectedContract),
    ("PolicyChoiceProvider passes request and cancellation token to delegate", PolicyChoiceProviderPassesRequestAndTokenToDelegate),
    ("PolicyChoiceProvider validates delegate result before returning", PolicyChoiceProviderValidatesDelegateResultBeforeReturning),
    ("PolicyChoiceProvider rejects null task and null result from delegate", PolicyChoiceProviderRejectsNullDelegateOutputs),
    ("PolicyChoiceProvider propagates delegate exceptions", PolicyChoiceProviderPropagatesDelegateExceptions),
    ("PolicyChoiceProvider honors pre-canceled token before delegate call", PolicyChoiceProviderHonorsPreCanceledTokenBeforeDelegateCall),
    ("PolicyChoiceProvider propagates delegate cancellation", PolicyChoiceProviderPropagatesDelegateCancellation),
    ("PolicyChoiceProvider default policy skips when request allows skip", PolicyChoiceProviderDefaultPolicySkipsWhenAllowed),
    ("PolicyChoiceProvider default policy chooses deterministic minimum legal result", PolicyChoiceProviderDefaultPolicyChoosesDeterministicMinimumLegalResult),
    ("AS-IS policy choice references remain read-only inputs", AsIsPolicyChoiceReferencesRemainReadOnlyInputs),
    ("PolicyChoiceProvider source file has no placeholder or Unity dependency", PolicyChoiceProviderSourceHasNoPlaceholderOrUnityDependency),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G1E-004")
        ?? throw new InvalidOperationException("G1E-004 row was not found.");

    AssertEqual("Phase 1", Value(row, "phase"), "phase");
    AssertEqual("Choices", Value(row, "area"), "area");
    AssertEqual("PolicyChoiceProvider", Value(row, "goal"), "goal");
    AssertEqual("policy delegate choice 확정", Value(row, "scope"), "scope");
    AssertEqual("PolicyChoiceProvider", Value(row, "deliverables"), "deliverables");
    AssertEqual("delegate cancellation error 테스트", Value(row, "unit_test_scope"), "unit test scope");
    AssertEqual("docs/test-results/goals/G1E-004_policy_choice_provider_unit_test_results.md", Value(row, "result_document"), "result document");
    AssertEqual("G1E-002", Value(row, "blocked_until"), "blocked_until");
    AssertEqual("Policy provider 테스트 통과", Value(row, "completion_gate"), "completion gate");
    return Task.CompletedTask;
}

async Task PolicyChoiceProviderPassesRequestAndTokenToDelegate()
{
    ChoiceRequest request = CardRequest(min: 1, max: 1, canSkip: false);
    using var cancellation = new CancellationTokenSource();
    ChoiceRequest? observedRequest = null;
    CancellationToken observedToken = default;

    var provider = new PolicyChoiceProvider((policyRequest, token) =>
    {
        observedRequest = policyRequest;
        observedToken = token;
        return Task.FromResult(ChoiceResult.Select(new HeadlessEntityId("card-a")));
    });

    ChoiceResult result = await provider.ChooseAsync(request, cancellation.Token);

    if (observedRequest is null)
    {
        throw new InvalidOperationException("request: delegate did not observe a request.");
    }

    AssertSame(request, observedRequest, "request");
    AssertEqual(cancellation.Token, observedToken, "token");
    AssertEqual(new HeadlessEntityId("card-a"), result.SelectedIds[0], "selected id");
}

async Task PolicyChoiceProviderValidatesDelegateResultBeforeReturning()
{
    var provider = new PolicyChoiceProvider((_, _) => Task.FromResult(ChoiceResult.Skip()));

    InvalidOperationException ex = await ExpectThrowsAsync<InvalidOperationException>(
        () => provider.ChooseAsync(CardRequest(min: 1, max: 1, canSkip: false)));

    AssertTrue(ex.Message.Contains("does not allow skipping", StringComparison.Ordinal), "validation message");
}

async Task PolicyChoiceProviderRejectsNullDelegateOutputs()
{
    var nullTaskProvider = new PolicyChoiceProvider((_, _) => null!);
    InvalidOperationException nullTask = await ExpectThrowsAsync<InvalidOperationException>(
        () => nullTaskProvider.ChooseAsync(CardRequest(min: 1, max: 1, canSkip: false)));

    AssertTrue(nullTask.Message.Contains("null task", StringComparison.Ordinal), "null task message");

    var nullResultProvider = new PolicyChoiceProvider((_, _) => Task.FromResult<ChoiceResult>(null!));
    InvalidOperationException nullResult = await ExpectThrowsAsync<InvalidOperationException>(
        () => nullResultProvider.ChooseAsync(CardRequest(min: 1, max: 1, canSkip: false)));

    AssertTrue(nullResult.Message.Contains("null result", StringComparison.Ordinal), "null result message");
}

async Task PolicyChoiceProviderPropagatesDelegateExceptions()
{
    var provider = new PolicyChoiceProvider((_, _) => throw new InvalidOperationException("policy failed"));

    InvalidOperationException ex = await ExpectThrowsAsync<InvalidOperationException>(
        () => provider.ChooseAsync(CardRequest(min: 1, max: 1, canSkip: false)));

    AssertEqual("policy failed", ex.Message, "exception message");
}

async Task PolicyChoiceProviderHonorsPreCanceledTokenBeforeDelegateCall()
{
    var delegateCalled = false;
    var provider = new PolicyChoiceProvider((_, _) =>
    {
        delegateCalled = true;
        return Task.FromResult(ChoiceResult.Select(new HeadlessEntityId("card-a")));
    });

    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();

    await ExpectThrowsAsync<OperationCanceledException>(
        () => provider.ChooseAsync(CardRequest(min: 1, max: 1, canSkip: false), cancellation.Token));

    AssertFalse(delegateCalled, "delegate called");
}

async Task PolicyChoiceProviderPropagatesDelegateCancellation()
{
    using var cancellation = new CancellationTokenSource();
    var provider = new PolicyChoiceProvider((_, token) =>
    {
        cancellation.Cancel();
        token.ThrowIfCancellationRequested();
        return Task.FromResult(ChoiceResult.Select(new HeadlessEntityId("card-a")));
    });

    await ExpectThrowsAsync<OperationCanceledException>(
        () => provider.ChooseAsync(CardRequest(min: 1, max: 1, canSkip: false), cancellation.Token));
}

async Task PolicyChoiceProviderDefaultPolicySkipsWhenAllowed()
{
    var provider = new PolicyChoiceProvider();
    ChoiceResult result = await provider.ChooseAsync(CardRequest(min: 0, max: 2, canSkip: true));

    AssertTrue(result.IsSkipped, "skip");
    AssertEqual(0, result.SelectedIds.Count, "selected ids");
    AssertEqual(null, result.SelectedCount, "selected count");
}

async Task PolicyChoiceProviderDefaultPolicyChoosesDeterministicMinimumLegalResult()
{
    var provider = new PolicyChoiceProvider();

    ChoiceResult selected = await provider.ChooseAsync(CardRequest(min: 2, max: 2, canSkip: false));
    AssertFalse(selected.IsSkipped, "card result skipped");
    AssertEqual(2, selected.SelectedIds.Count, "selected id count");
    AssertEqual(new HeadlessEntityId("card-a"), selected.SelectedIds[0], "first selected id");
    AssertEqual(new HeadlessEntityId("card-b"), selected.SelectedIds[1], "second selected id");

    ChoiceResult count = await provider.ChooseAsync(CountRequest(min: 3, max: 5));
    AssertFalse(count.IsSkipped, "count skipped");
    AssertEqual(3, count.SelectedCount, "selected count");
    AssertEqual(0, count.SelectedIds.Count, "count selected ids");
}

Task AsIsPolicyChoiceReferencesRemainReadOnlyInputs()
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

Task PolicyChoiceProviderSourceHasNoPlaceholderOrUnityDependency()
{
    string[] paths =
    {
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Choices", "PolicyChoiceProvider.cs"),
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Choices", "IChoiceProvider.cs"),
    };

    foreach (string path in paths)
    {
        string text = File.ReadAllText(path);
        if (text.Contains("TODO", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{Path.GetFileName(path)} still contains a TODO placeholder.");
        }

        AssertFalse(text.Contains("UnityEngine", StringComparison.Ordinal), $"{Path.GetFileName(path)} must not reference UnityEngine");
        AssertFalse(text.Contains("MonoBehaviour", StringComparison.Ordinal), $"{Path.GetFileName(path)} must not reference MonoBehaviour");
    }

    string providerText = File.ReadAllText(paths[0]);
    AssertTrue(providerText.Contains("ThrowIfInvalid", StringComparison.Ordinal), "PolicyChoiceProvider validates delegate choices");
    AssertTrue(providerText.Contains("ConfigureAwait(false)", StringComparison.Ordinal), "PolicyChoiceProvider awaits delegate tasks");
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

static async Task<TException> ExpectThrowsAsync<TException>(Func<Task> action)
    where TException : Exception
{
    try
    {
        await action();
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

static void AssertSame<T>(T expected, T actual, string label)
    where T : class
{
    if (!ReferenceEquals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected same reference.");
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
