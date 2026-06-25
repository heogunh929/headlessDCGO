using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();

var tests = new (string Name, Action Body)[]
{
    ("G2A-001 goal row and predecessor are satisfied", GoalRowAndPredecessorAreSatisfied),
    ("Korean mapping definition documents every AS-IS phase flow", MappingDefinitionDocumentsPhaseFlows),
    ("AS-IS phase aliases map to stable HeadlessPhase values", AsIsPhaseAliasesMapToHeadlessPhases),
    ("Headless phases roundtrip to canonical AS-IS names", HeadlessPhasesRoundTripToCanonicalAsIsNames),
    ("AS-IS turn flow sequence includes setup unsuspend and memory pass", AsIsTurnFlowSequenceIsFixed),
    ("Turn controller initializes and advances through mapped flow", TurnControllerAdvancesThroughMappedFlow),
    ("EndTurn starts next player at AS-IS Active phase", EndTurnStartsNextPlayerAtActivePhase),
    ("Observation encoder exposes every mapped phase flag", ObservationEncoderExposesMappedPhaseFlags),
    ("Scoped phase mapping files contain no placeholder TODOs", ScopedPhaseFilesHaveNoPlaceholderTodos),
};

var failures = new List<string>();

foreach (var test in tests)
{
    try
    {
        test.Body();
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

void GoalRowAndPredecessorAreSatisfied()
{
    var rows = ReadCsv(Path.Combine(root, "docs", "headless_complete_goal_breakdown.csv"));
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G2A-001")
        ?? throw new InvalidOperationException("G2A-001 row was not found.");

    AssertEqual("Phase 2", Value(row, "phase"), "phase");
    AssertEqual("TurnStateMachine", Value(row, "area"), "area");
    AssertEqual("HeadlessPhase mapping", Value(row, "deliverables"), "deliverables");
    AssertEqual("docs/test-results/goals/G2A-001_phase_mapping_unit_test_results.md", Value(row, "result_document"), "result_document");
    AssertEqual("G1I-005", Value(row, "blocked_until"), "blocked_until");

    string predecessor = File.ReadAllText(Path.Combine(root, "docs", "test-results", "goals", "G1I-005_phase1_aggregate_unit_test_results.md"));
    AssertContains(predecessor, "COMPLETE", "G1I-005 completion marker");
}

void MappingDefinitionDocumentsPhaseFlows()
{
    string path = Path.Combine(root, "docs", "headless_phase_mapping_definition_ko.csv");
    var rows = ReadCsv(path);
    string document = File.ReadAllText(path);

    AssertTrue(rows.Count >= 11, "mapping row count");
    AssertContains(document, "GameContext.phase.Active", "active mapping");
    AssertContains(document, "GameContext.phase.Draw", "draw mapping");
    AssertContains(document, "GameContext.phase.Breeding", "breeding mapping");
    AssertContains(document, "GameContext.phase.Main", "main mapping");
    AssertContains(document, "GameContext.phase.End", "end mapping");
    AssertContains(document, "ActivePhase unsuspend block", "unsuspend mapping");
    AssertContains(document, "PassTurn/EndTurnProcess", "memory pass mapping");
    AssertContains(document, "HeadlessPhaseMapping.AsIsTurnSequence", "sequence mapping");
}

void AsIsPhaseAliasesMapToHeadlessPhases()
{
    AssertEqual(HeadlessPhase.Setup, HeadlessPhaseMapping.FromAsIsName("GameStateMachine setup"), "setup alias");
    AssertEqual(HeadlessPhase.Active, HeadlessPhaseMapping.FromAsIsName("GameContext.phase.Active"), "active alias");
    AssertEqual(HeadlessPhase.Unsuspend, HeadlessPhaseMapping.FromAsIsName("IUnsuspendPermanents"), "unsuspend alias");
    AssertEqual(HeadlessPhase.Draw, HeadlessPhaseMapping.FromAsIsName("TurnStateMachine.DrawPhase"), "draw alias");
    AssertEqual(HeadlessPhase.Breeding, HeadlessPhaseMapping.FromAsIsName("raising"), "breeding alias");
    AssertEqual(HeadlessPhase.Main, HeadlessPhaseMapping.FromAsIsName("main phase"), "main alias");
    AssertEqual(HeadlessPhase.MemoryPass, HeadlessPhaseMapping.FromAsIsName("PassTurn"), "memory pass alias");
    AssertEqual(HeadlessPhase.End, HeadlessPhaseMapping.FromAsIsName("GameContext.phase.End"), "end alias");

    AssertFalse(HeadlessPhaseMapping.TryFromAsIsName("unknown phase", out _), "unknown alias");
    ExpectThrows<ArgumentException>(() => HeadlessPhaseMapping.FromAsIsName("unknown phase"));
}

void HeadlessPhasesRoundTripToCanonicalAsIsNames()
{
    foreach (HeadlessPhase phase in HeadlessPhaseMapping.ObservationPhaseOrder)
    {
        string asIsName = HeadlessPhaseMapping.ToAsIsName(phase);
        AssertEqual(phase, HeadlessPhaseMapping.FromAsIsName(asIsName), $"{phase} roundtrip");
    }

    ExpectThrows<ArgumentOutOfRangeException>(() => HeadlessPhaseMapping.ToAsIsName((HeadlessPhase)999));
}

void AsIsTurnFlowSequenceIsFixed()
{
    var expected = new[]
    {
        HeadlessPhase.Setup,
        HeadlessPhase.Active,
        HeadlessPhase.Unsuspend,
        HeadlessPhase.Draw,
        HeadlessPhase.Breeding,
        HeadlessPhase.Main,
        HeadlessPhase.MemoryPass,
        HeadlessPhase.End
    };

    AssertSequence(expected, HeadlessPhaseMapping.AsIsTurnSequence, "AS-IS turn flow sequence");
    AssertEqual(HeadlessPhase.Setup, HeadlessPhaseMapping.Next(HeadlessPhase.None), "none next");
    AssertEqual(HeadlessPhase.MemoryPass, HeadlessPhaseMapping.Next(HeadlessPhase.Main), "main next");
    AssertEqual(HeadlessPhase.End, HeadlessPhaseMapping.Next(HeadlessPhase.MemoryPass), "memory pass next");
    AssertEqual(HeadlessPhase.End, HeadlessPhaseMapping.Next(HeadlessPhase.End), "end next");
    AssertFalse(HeadlessPhaseMapping.CanAdvance(HeadlessPhase.End), "end can advance");
}

void TurnControllerAdvancesThroughMappedFlow()
{
    var controller = new InMemoryHeadlessTurnController();
    controller.Initialize(new[] { new HeadlessPlayerId(1), new HeadlessPlayerId(2) });

    AssertEqual(1, controller.Current.TurnNumber, "initial turn");
    AssertEqual(new HeadlessPlayerId(1), controller.Current.TurnPlayerId, "initial turn player");
    AssertEqual(HeadlessPhase.Setup, controller.Current.Phase, "initial phase");
    AssertTrue(controller.Current.IsSetupPhase, "setup helper");

    var expected = new[]
    {
        HeadlessPhase.Active,
        HeadlessPhase.Unsuspend,
        HeadlessPhase.Draw,
        HeadlessPhase.Breeding,
        HeadlessPhase.Main,
        HeadlessPhase.MemoryPass,
        HeadlessPhase.End,
        HeadlessPhase.End
    };

    foreach (HeadlessPhase phase in expected)
    {
        HeadlessTurnState state = controller.AdvancePhase();
        AssertEqual(phase, state.Phase, $"advance to {phase}");
    }
}

void EndTurnStartsNextPlayerAtActivePhase()
{
    var controller = new InMemoryHeadlessTurnController();
    controller.Initialize(new[] { new HeadlessPlayerId(1), new HeadlessPlayerId(2) });
    controller.SetPhase(HeadlessPhase.End);

    HeadlessTurnState nextTurn = controller.EndTurn();
    AssertEqual(2, nextTurn.TurnNumber, "next turn number");
    AssertEqual(new HeadlessPlayerId(2), nextTurn.TurnPlayerId, "next turn player");
    AssertEqual(new HeadlessPlayerId(1), nextTurn.NonTurnPlayerId, "next non-turn player");
    AssertEqual(HeadlessPhase.Active, nextTurn.Phase, "next turn phase");
    AssertFalse(nextTurn.IsFirstTurn, "next turn first flag");

    ExpectThrows<ArgumentOutOfRangeException>(() => controller.SetPhase((HeadlessPhase)999));
}

void ObservationEncoderExposesMappedPhaseFlags()
{
    var turn = new HeadlessTurnState(
        TurnNumber: 1,
        TurnPlayerId: new HeadlessPlayerId(1),
        NonTurnPlayerId: new HeadlessPlayerId(2),
        Phase: HeadlessPhase.MemoryPass,
        IsFirstTurn: true,
        PlayerOrder: new[] { new HeadlessPlayerId(1), new HeadlessPlayerId(2) });

    var snapshot = ObservationSnapshot.Empty with { Turn = turn };
    var encoded = new ObservationEncoder().Encode(snapshot);
    var features = encoded.Features.ToDictionary(feature => feature.Name, feature => feature.Value, StringComparer.Ordinal);

    foreach (HeadlessPhase phase in HeadlessPhaseMapping.ObservationPhaseOrder)
    {
        AssertTrue(features.ContainsKey($"turn.phase.{phase}"), $"observation flag for {phase}");
    }

    AssertEqual((double)(int)HeadlessPhase.MemoryPass, features["turn.phaseIndex"], "memory pass phase index");
    AssertEqual(1d, features["turn.phase.MemoryPass"], "memory pass flag");
    AssertEqual(0d, features["turn.phase.Main"], "main flag");
}

void ScopedPhaseFilesHaveNoPlaceholderTodos()
{
    var scopedFiles = new[]
    {
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "HeadlessPhase.cs"),
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "HeadlessPhaseMapping.cs"),
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "HeadlessTurnState.cs"),
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "IHeadlessTurnController.cs"),
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "InMemoryHeadlessTurnController.cs")
    };

    foreach (string path in scopedFiles)
    {
        AssertFalse(File.ReadAllText(path).Contains("TODO", StringComparison.OrdinalIgnoreCase), path);
    }

    string asIs = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "TurnStateMachine.cs"));
    AssertContains(asIs, "ActivePhase", "AS-IS ActivePhase");
    AssertContains(asIs, "DrawPhase", "AS-IS DrawPhase");
    AssertContains(asIs, "BreedingPhase", "AS-IS BreedingPhase");
    AssertContains(asIs, "MainPhase", "AS-IS MainPhase");
    AssertContains(asIs, "EndPhase", "AS-IS EndPhase");
    AssertContains(asIs, "PassTurn", "AS-IS PassTurn");
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

static void AssertSequence<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual, string label)
{
    AssertEqual(expected.Count, actual.Count, $"{label} count");
    for (int i = 0; i < expected.Count; i++)
    {
        AssertEqual(expected[i], actual[i], $"{label}[{i}]");
    }
}

static void AssertContains(string text, string expected, string label)
{
    if (!text.Contains(expected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"{label}: expected text to contain '{expected}'.");
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
