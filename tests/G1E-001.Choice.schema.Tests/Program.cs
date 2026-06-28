using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G1E-001 goal row keeps the Choice schema contract", GoalRowKeepsExpectedContract),
    ("ChoiceCandidate preserves candidate id owner zone label and selectable flag", ChoiceCandidatePreservesSchemaFields),
    ("ChoiceCandidate rejects invalid candidate schema values", ChoiceCandidateRejectsInvalidSchemaValues),
    ("ChoiceRequest preserves immutable request and candidate snapshots", ChoiceRequestPreservesImmutableSnapshots),
    ("ChoiceRequest rejects invalid request schema values", ChoiceRequestRejectsInvalidSchemaValues),
    ("ChoiceType and ChoiceZone cover AS-IS selection categories", ChoiceEnumsCoverAsIsSelectionCategories),
    ("AS-IS selection references remain read-only inputs", AsIsSelectionReferencesRemainReadOnlyInputs),
    ("Choice schema source files no longer contain placeholder TODO contracts", ChoiceSchemaFilesHaveNoTodoContracts),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G1E-001")
        ?? throw new InvalidOperationException("G1E-001 row was not found.");

    AssertEqual("Phase 1", Value(row, "phase"), "phase");
    AssertEqual("Choices", Value(row, "area"), "area");
    AssertEqual("Choice schema", Value(row, "goal"), "goal");
    AssertEqual("choice request candidate zone type schema 확정", Value(row, "scope"), "scope");
    AssertEqual("ChoiceRequest ChoiceCandidate ChoiceType ChoiceZone", Value(row, "deliverables"), "deliverables");
    AssertEqual("schema validation 테스트", Value(row, "unit_test_scope"), "unit test scope");
    AssertEqual("docs/test-results/goals/G1E-001_choice_schema_unit_test_results.md", Value(row, "result_document"), "result document");
    AssertEqual("G1A-003", Value(row, "blocked_until"), "blocked_until");
    AssertEqual("Choice schema 테스트 통과", Value(row, "completion_gate"), "completion gate");
    return Task.CompletedTask;
}

Task ChoiceCandidatePreservesSchemaFields()
{
    var id = new HeadlessEntityId(" card-001 ");
    var owner = new HeadlessPlayerId(2);
    var candidate = new ChoiceCandidate(id, "  BT1-001 Agumon  ", ChoiceZone.Hand, IsSelectable: true, owner);

    AssertEqual(new HeadlessEntityId("card-001"), candidate.Id, "id");
    AssertEqual(owner, candidate.OwnerId, "owner");
    AssertEqual("BT1-001 Agumon", candidate.Label, "label");
    AssertEqual(ChoiceZone.Hand, candidate.Zone, "zone");
    AssertTrue(candidate.IsSelectable, "selectable");
    return Task.CompletedTask;
}

Task ChoiceCandidateRejectsInvalidSchemaValues()
{
    var id = new HeadlessEntityId("card-001");

    ExpectThrows<ArgumentException>(() => new ChoiceCandidate(default, "card", ChoiceZone.Hand, true));
    ExpectThrows<ArgumentNullException>(() => new ChoiceCandidate(id, null!, ChoiceZone.Hand, true));
    ExpectThrows<ArgumentOutOfRangeException>(() => new ChoiceCandidate(id, "card", ChoiceZone.None, true));
    ExpectThrows<ArgumentOutOfRangeException>(() => new ChoiceCandidate(id, "card", (ChoiceZone)999, true));
    return Task.CompletedTask;
}

Task ChoiceRequestPreservesImmutableSnapshots()
{
    var player = new HeadlessPlayerId(1);
    var candidates = new List<ChoiceCandidate>
    {
        new(new HeadlessEntityId("card-a"), "Card A", ChoiceZone.Library, IsSelectable: true, player),
        new(new HeadlessEntityId("card-b"), "Card B", ChoiceZone.Library, IsSelectable: false, player),
    };

    var request = new ChoiceRequest(
        ChoiceType.Card,
        player,
        "  Select one card.  ",
        minCount: 1,
        maxCount: 2,
        canSkip: false,
        sourceZone: ChoiceZone.Library,
        candidates);

    candidates.Add(new ChoiceCandidate(new HeadlessEntityId("card-c"), "Card C", ChoiceZone.Trash, IsSelectable: true, player));

    AssertEqual(ChoiceType.Card, request.Type, "type");
    AssertEqual(player, request.PlayerId, "player");
    AssertEqual("Select one card.", request.Message, "message");
    AssertEqual(1, request.MinCount, "min");
    AssertEqual(2, request.MaxCount, "max");
    AssertFalse(request.CanSkip, "can skip");
    AssertEqual(ChoiceZone.Library, request.SourceZone, "source zone");
    AssertEqual(2, request.Candidates.Count, "snapshot count");
    AssertEqual(1, request.SelectableCandidates.Count, "selectable count");
    AssertEqual(new HeadlessEntityId("card-a"), request.SelectableCandidates[0].Id, "selectable id");
    AssertTrue(request.Candidates is System.Collections.ObjectModel.ReadOnlyCollection<ChoiceCandidate>, "read-only candidates");
    return Task.CompletedTask;
}

Task ChoiceRequestRejectsInvalidSchemaValues()
{
    var player = new HeadlessPlayerId(1);
    var candidate = new ChoiceCandidate(new HeadlessEntityId("card-a"), "Card A", ChoiceZone.Hand, IsSelectable: true, player);
    IReadOnlyList<ChoiceCandidate> candidates = new[] { candidate };

    ExpectThrows<ArgumentOutOfRangeException>(() => new ChoiceRequest(ChoiceType.Unknown, player, "message", 0, 1, false, ChoiceZone.Hand, candidates));
    ExpectThrows<ArgumentOutOfRangeException>(() => new ChoiceRequest((ChoiceType)999, player, "message", 0, 1, false, ChoiceZone.Hand, candidates));
    ExpectThrows<ArgumentException>(() => new ChoiceRequest(ChoiceType.Card, default, "message", 0, 1, false, ChoiceZone.Hand, candidates));
    ExpectThrows<ArgumentNullException>(() => new ChoiceRequest(ChoiceType.Card, player, null!, 0, 1, false, ChoiceZone.Hand, candidates));
    ExpectThrows<ArgumentOutOfRangeException>(() => new ChoiceRequest(ChoiceType.Card, player, "message", -1, 1, false, ChoiceZone.Hand, candidates));
    ExpectThrows<ArgumentOutOfRangeException>(() => new ChoiceRequest(ChoiceType.Card, player, "message", 2, 1, false, ChoiceZone.Hand, candidates));
    ExpectThrows<ArgumentOutOfRangeException>(() => new ChoiceRequest(ChoiceType.Card, player, "message", 0, 1, false, (ChoiceZone)999, candidates));
    ExpectThrows<ArgumentNullException>(() => new ChoiceRequest(ChoiceType.Card, player, "message", 0, 1, false, ChoiceZone.Hand, null!));
    ExpectThrows<ArgumentException>(() => new ChoiceRequest(ChoiceType.Card, player, "message", 0, 1, false, ChoiceZone.Hand, new ChoiceCandidate?[] { null! }!));
    return Task.CompletedTask;
}

Task ChoiceEnumsCoverAsIsSelectionCategories()
{
    AssertSequence(
        new[]
        {
            "Unknown",
            "Card",
            "HandCard",
            "Permanent",
            "Count",
            "AttackTarget",
            "MainPhaseAction",
            // Added after this schema test was first written; both are live selection categories:
            "OptionalEffect", // OptionalPromptQueue — optional ("you may") effect prompts
            "Blocker",        // BlockTiming — the defender's blocker selection
            "Mulligan",       // N-5: MulliganCoordinator — opening-hand keep/redraw decision
            "DeletionReplacement", // F-6.8: DeletionReplacementTiming — would-be-deleted optional keyword decision
            "AllianceTarget", // C-18 Alliance: AllianceAttackBoost — optional suspend-an-ally attack boost
            "EffectAttack", // S1 (C-20 Vortex / C-16 Overclock): EffectDrivenAttack — optional effect-driven attack target
            "OverclockTarget", // C-16 Overclock: OverclockEffect — optional delete-a-trait-ally choice
            "RevealSelect", // B-7: RevealAndSelect — select from revealed deck-top cards
        },
        Enum.GetNames<ChoiceType>(),
        "choice type names");

    AssertContains(Enum.GetNames<ChoiceZone>(), "Library", "zone Library");
    AssertContains(Enum.GetNames<ChoiceZone>(), "Trash", "zone Trash");
    AssertContains(Enum.GetNames<ChoiceZone>(), "Security", "zone Security");
    AssertContains(Enum.GetNames<ChoiceZone>(), "Hand", "zone Hand");
    AssertContains(Enum.GetNames<ChoiceZone>(), "Recollection", "zone Recollection");
    AssertContains(Enum.GetNames<ChoiceZone>(), "Execution", "zone Execution");
    AssertContains(Enum.GetNames<ChoiceZone>(), "DigivolutionCards", "zone DigivolutionCards");
    AssertContains(Enum.GetNames<ChoiceZone>(), "LinkedCards", "zone LinkedCards");
    AssertContains(Enum.GetNames<ChoiceZone>(), "BattleArea", "zone BattleArea");
    AssertContains(Enum.GetNames<ChoiceZone>(), "BreedingArea", "zone BreedingArea");
    AssertContains(Enum.GetNames<ChoiceZone>(), "DigitamaLibrary", "zone DigitamaLibrary");
    AssertContains(Enum.GetNames<ChoiceZone>(), "None", "zone None");
    return Task.CompletedTask;
}

Task AsIsSelectionReferencesRemainReadOnlyInputs()
{
    var references = new (string Path, string[] Patterns)[]
    {
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "SelectCardEffect.cs"),
            new[] { "RootCardSources", "_MaxCount", "_CanNoSelect", "CardSelection" }),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "SelectPermanentEffect.cs"),
            new[] { "CanSelectedPermanets", "_maxCount", "_canNoSelect", "PermanentSelection" }),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "SelectCountEffect.cs"),
            new[] { "MaxCount", "CanNoSelect", "ValueSelection" }),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "SelectHandEffect.cs"),
            new[] { "SelectHandEffect", "SetUp", "Select" }),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "SelectAttackEffect.cs"),
            new[] { "SelectAttackEffect", "SetUp", "Attack" }),
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

Task ChoiceSchemaFilesHaveNoTodoContracts()
{
    string[] paths =
    {
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Choices", "ChoiceCandidate.cs"),
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Choices", "ChoiceRequest.cs"),
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Choices", "ChoiceType.cs"),
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Choices", "ChoiceZone.cs"),
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

static void AssertContains<T>(IEnumerable<T> values, T expected, string label)
{
    if (!values.Contains(expected))
    {
        throw new InvalidOperationException($"{label}: expected collection to contain '{expected}'.");
    }
}

static void AssertSequence<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual, string label)
{
    AssertEqual(expected.Count, actual.Count, $"{label} count");
    for (int i = 0; i < expected.Count; i++)
    {
        AssertEqual(expected[i], actual[i], $"{label}[{i}]");
    }
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
