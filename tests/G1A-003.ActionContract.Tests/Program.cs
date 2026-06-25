using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G1A-003 goal row keeps the action contract", GoalRowKeepsExpectedContract),
    ("HeadlessAction and LegalAction preserve immutable payload snapshots", HeadlessAndLegalActionPreservePayloadSnapshots),
    ("Action models reject missing required action fields", ActionModelsRejectMissingRequiredFields),
    ("ActionProcessResult preserves success failure and illegal result contracts", ActionProcessResultPreservesResultContracts),
    ("MetadataActionProcessor distinguishes legal and illegal action results", MetadataActionProcessorDistinguishesLegalAndIllegalResults),
    ("Action contract source files no longer contain placeholder TODO contracts", ActionContractFilesHaveNoTodoContracts),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G1A-003")
        ?? throw new InvalidOperationException("G1A-003 row was not found.");

    AssertEqual("Phase 1", Value(row, "phase"), "phase");
    AssertEqual("Runtime", Value(row, "area"), "area");
    AssertEqual("action payload와 처리 결과 계약 고정", Value(row, "scope"), "scope");
    AssertEqual("HeadlessAction ActionProcessResult IllegalAction model", Value(row, "deliverables"), "deliverables");
    AssertEqual("legal illegal action 결과 테스트", Value(row, "unit_test_scope"), "unit_test_scope");
    AssertEqual("docs/test-results/goals/G1A-003_action_contract_unit_test_results.md", Value(row, "result_document"), "result_document");
    AssertEqual("G1A-001", Value(row, "blocked_until"), "blocked_until");
    AssertEqual("action contract 테스트 통과", Value(row, "completion_gate"), "completion_gate");
    return Task.CompletedTask;
}

Task HeadlessAndLegalActionPreservePayloadSnapshots()
{
    var parameters = new Dictionary<string, object?>
    {
        ["cardId"] = "BT1-001"
    };
    var headlessAction = new HeadlessAction(
        new HeadlessEntityId("action-1"),
        new HeadlessPlayerId(1),
        "  PlayCard  ",
        parameters);

    parameters["cardId"] = "mutated";
    parameters["extra"] = true;

    AssertEqual("PlayCard", headlessAction.ActionType, "headless action type trim");
    AssertEqual("BT1-001", headlessAction.Parameters["cardId"], "headless parameter snapshot");
    AssertFalse(headlessAction.Parameters.ContainsKey("extra"), "headless parameter extra key");

    LegalAction legalAction = headlessAction.ToLegalAction();
    AssertEqual(headlessAction.Id, legalAction.Id, "legal action id");
    AssertEqual(headlessAction.PlayerId, legalAction.PlayerId, "legal player id");
    AssertEqual(headlessAction.ActionType, legalAction.ActionType, "legal action type");
    AssertEqual("BT1-001", legalAction.Parameters["cardId"], "legal parameter snapshot");

    HeadlessAction roundTrip = legalAction.ToHeadlessAction();
    AssertEqual("BT1-001", roundTrip.Parameters["cardId"], "roundtrip parameter snapshot");
    return Task.CompletedTask;
}

Task ActionModelsRejectMissingRequiredFields()
{
    ExpectThrows<ArgumentException>(() => new HeadlessAction(
        new HeadlessEntityId("action-1"),
        new HeadlessPlayerId(1),
        " ",
        new Dictionary<string, object?>()));

    ExpectThrows<ArgumentNullException>(() => new HeadlessAction(
        new HeadlessEntityId("action-1"),
        new HeadlessPlayerId(1),
        HeadlessActionTypes.NoOp,
        null!));

    ExpectThrows<ArgumentException>(() => new LegalAction(
        new HeadlessEntityId("action-1"),
        new HeadlessPlayerId(1),
        "",
        new Dictionary<string, object?>()));

    ExpectThrows<ArgumentException>(() => new IllegalAction(
        null,
        null,
        "Unknown",
        "",
        new Dictionary<string, object?>()));

    return Task.CompletedTask;
}

Task ActionProcessResultPreservesResultContracts()
{
    var metadata = new Dictionary<string, object?>
    {
        ["state"] = "before"
    };

    ActionProcessResult success = ActionProcessResult.Success(" ok ", metadata);
    metadata["state"] = "after";
    AssertTrue(success.IsSuccess, "success flag");
    AssertFalse(success.IsIllegal, "success illegal flag");
    AssertEqual("ok", success.Message, "success message trim");
    AssertEqual("before", success.Metadata["state"], "success metadata snapshot");

    ActionProcessResult failure = ActionProcessResult.Failure(" bad ", metadata);
    AssertFalse(failure.IsSuccess, "failure flag");
    AssertFalse(failure.IsIllegal, "failure illegal flag");
    AssertEqual("bad", failure.Message, "failure message trim");

    var legalAction = HeadlessActionFactory.NoOp(new HeadlessPlayerId(2), "noop-1");
    ActionProcessResult illegal = ActionProcessResult.Illegal(legalAction, "unsupported");
    AssertFalse(illegal.IsSuccess, "illegal success flag");
    AssertTrue(illegal.IsIllegal, "illegal flag");
    AssertEqual("unsupported", illegal.Message, "illegal message");
    AssertEqual("noop-1", illegal.Metadata["actionId"], "illegal action id metadata");
    AssertEqual(2, illegal.Metadata["playerId"], "illegal player id metadata");
    AssertEqual(HeadlessActionTypes.NoOp, illegal.IllegalAction?.ActionType, "illegal action type");

    ExpectThrows<ArgumentException>(() => new ActionProcessResult(
        IsSuccess: true,
        Message: "bad",
        Metadata: new Dictionary<string, object?>(),
        IllegalAction: illegal.IllegalAction));

    return Task.CompletedTask;
}

async Task MetadataActionProcessorDistinguishesLegalAndIllegalResults()
{
    var processor = new MetadataActionProcessor();
    var context = EngineContext.CreateDefault();
    var player = new HeadlessPlayerId(1);

    ActionProcessResult legal = await processor.ProcessAsync(
        HeadlessActionFactory.NoOp(player, "legal-noop"),
        context);
    AssertTrue(legal.IsSuccess, "legal success flag");
    AssertFalse(legal.IsIllegal, "legal illegal flag");
    AssertEqual("legal-noop", legal.Metadata["actionId"], "legal action id metadata");

    var unsupported = HeadlessActionFactory.Create("UnsupportedAction", player, "bad-action");
    ActionProcessResult illegal = await processor.ProcessAsync(unsupported, context);
    AssertFalse(illegal.IsSuccess, "unsupported success flag");
    AssertTrue(illegal.IsIllegal, "unsupported illegal flag");
    AssertEqual("bad-action", illegal.Metadata["actionId"], "unsupported action id metadata");
    AssertEqual("UnsupportedAction", illegal.IllegalAction?.ActionType, "unsupported illegal action type");
    AssertTrue(illegal.Message.Contains("Unsupported headless action type", StringComparison.Ordinal), "unsupported reason");
}

Task ActionContractFilesHaveNoTodoContracts()
{
    var relativeFiles = new[]
    {
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "Runtime", "HeadlessAction.cs"),
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "Runtime", "ActionProcessResult.cs"),
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "Services", "LegalAction.cs"),
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "Services", "IllegalAction.cs"),
    };

    foreach (var relativeFile in relativeFiles)
    {
        var path = Path.Combine(root, relativeFile);
        var text = File.ReadAllText(path);
        if (text.Contains("TODO", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{relativeFile} still contains a TODO placeholder.");
        }
    }

    return Task.CompletedTask;
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

static void ExpectThrows<TException>(Action action)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
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
