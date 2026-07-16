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
    ("Former 9-value phase states map to unique (phase, cursor) pairs", NineValueStatesMapToUniquePhaseCursorPairs),
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
    AssertContains(document, "HeadlessPhaseMapping.TurnStepSequence", "sequence mapping");
}

void AsIsPhaseAliasesMapToHeadlessPhases()
{
    // (R4 S2) Aliases now map to the AS-IS 6-value phase model. The former Setup/Unsuspend/MemoryPass step names
    // fold to their AS-IS phase (setup→None, unsuspend→Active, PassTurn→Main); the step position within a phase is
    // the substrate TurnStepCursor, not a phase alias.
    AssertEqual(HeadlessPhase.None, HeadlessPhaseMapping.FromAsIsName("GameStateMachine setup"), "setup alias");
    AssertEqual(HeadlessPhase.Active, HeadlessPhaseMapping.FromAsIsName("GameContext.phase.Active"), "active alias");
    AssertEqual(HeadlessPhase.Active, HeadlessPhaseMapping.FromAsIsName("IUnsuspendPermanents"), "unsuspend alias");
    AssertEqual(HeadlessPhase.Draw, HeadlessPhaseMapping.FromAsIsName("TurnStateMachine.DrawPhase"), "draw alias");
    AssertEqual(HeadlessPhase.Breeding, HeadlessPhaseMapping.FromAsIsName("raising"), "breeding alias");
    AssertEqual(HeadlessPhase.Main, HeadlessPhaseMapping.FromAsIsName("main phase"), "main alias");
    AssertEqual(HeadlessPhase.Main, HeadlessPhaseMapping.FromAsIsName("PassTurn"), "memory pass alias");
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
    // (R4 S2) The 8-step turn walk, now keyed by (phase, cursor). Each former 9-value phase state is one step,
    // preserving the same eight distinct turn positions: setup / active / unsuspend / draw / breeding / main /
    // memory-pass / end.
    var expected = new (HeadlessPhase Phase, TurnStepCursor Cursor)[]
    {
        (HeadlessPhase.None, TurnStepCursor.Starting),               // former Setup
        (HeadlessPhase.Active, TurnStepCursor.PhaseStart),           // former Active
        (HeadlessPhase.Active, TurnStepCursor.Unsuspending),         // former Unsuspend
        (HeadlessPhase.Draw, TurnStepCursor.PhaseStart),             // former Draw
        (HeadlessPhase.Breeding, TurnStepCursor.PhaseStart),         // former Breeding
        (HeadlessPhase.Main, TurnStepCursor.PhaseStart),             // former Main
        (HeadlessPhase.Main, TurnStepCursor.AwaitingMemoryPassEnd),  // former MemoryPass
        (HeadlessPhase.End, TurnStepCursor.PhaseStart)               // former End
    };

    AssertSequence(expected, HeadlessPhaseMapping.TurnStepSequence, "AS-IS turn flow step sequence");
    AssertEqual((HeadlessPhase.None, TurnStepCursor.Starting), HeadlessPhaseMapping.NextStep(HeadlessPhase.None, TurnStepCursor.PhaseStart), "none next");
    AssertEqual((HeadlessPhase.Main, TurnStepCursor.AwaitingMemoryPassEnd), HeadlessPhaseMapping.NextStep(HeadlessPhase.Main, TurnStepCursor.PhaseStart), "main next");
    AssertEqual((HeadlessPhase.End, TurnStepCursor.PhaseStart), HeadlessPhaseMapping.NextStep(HeadlessPhase.Main, TurnStepCursor.AwaitingMemoryPassEnd), "memory pass next");
    AssertEqual((HeadlessPhase.End, TurnStepCursor.PhaseStart), HeadlessPhaseMapping.NextStep(HeadlessPhase.End, TurnStepCursor.PhaseStart), "end next");
    AssertFalse(HeadlessPhaseMapping.CanAdvance(HeadlessPhase.End), "end can advance");
}

void TurnControllerAdvancesThroughMappedFlow()
{
    var controller = new InMemoryHeadlessTurnController();
    controller.Initialize(new[] { new HeadlessPlayerId(1), new HeadlessPlayerId(2) });

    AssertEqual(1, controller.Current.TurnNumber, "initial turn");
    AssertEqual(new HeadlessPlayerId(1), controller.Current.TurnPlayerId, "initial turn player");
    // (R4 S2) initial state is the setup step = (None, Starting).
    AssertEqual(HeadlessPhase.None, controller.Current.Phase, "initial phase");
    AssertEqual(TurnStepCursor.Starting, controller.Current.StepCursor, "initial cursor");
    AssertTrue(controller.Current.IsSetupPhase, "setup helper");

    // (R4 S2) AdvancePhase walks the (phase, cursor) step sequence — same distinct positions as the former
    // 9-value flow.
    var expected = new (HeadlessPhase Phase, TurnStepCursor Cursor)[]
    {
        (HeadlessPhase.Active, TurnStepCursor.PhaseStart),
        (HeadlessPhase.Active, TurnStepCursor.Unsuspending),
        (HeadlessPhase.Draw, TurnStepCursor.PhaseStart),
        (HeadlessPhase.Breeding, TurnStepCursor.PhaseStart),
        (HeadlessPhase.Main, TurnStepCursor.PhaseStart),
        (HeadlessPhase.Main, TurnStepCursor.AwaitingMemoryPassEnd),
        (HeadlessPhase.End, TurnStepCursor.PhaseStart),
        (HeadlessPhase.End, TurnStepCursor.PhaseStart)
    };

    foreach ((HeadlessPhase Phase, TurnStepCursor Cursor) step in expected)
    {
        HeadlessTurnState state = controller.AdvancePhase();
        AssertEqual(step, (state.Phase, state.StepCursor), $"advance to {step}");
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
    // (R4 S2) The former MemoryPass state is now (Main, AwaitingMemoryPassEnd). The observation exposes the
    // 6-value phase one-hot AND the step-cursor one-hot, so the state is fully reconstructible.
    var turn = new HeadlessTurnState(
        TurnNumber: 1,
        TurnPlayerId: new HeadlessPlayerId(1),
        NonTurnPlayerId: new HeadlessPlayerId(2),
        Phase: HeadlessPhase.Main,
        StepCursor: TurnStepCursor.AwaitingMemoryPassEnd,
        IsFirstTurn: true,
        PlayerOrder: new[] { new HeadlessPlayerId(1), new HeadlessPlayerId(2) });

    var snapshot = ObservationSnapshot.Empty with { Turn = turn };
    var encoded = new ObservationEncoder().Encode(snapshot);
    var features = encoded.Features.ToDictionary(feature => feature.Name, feature => feature.Value, StringComparer.Ordinal);

    foreach (HeadlessPhase phase in HeadlessPhaseMapping.ObservationPhaseOrder)
    {
        AssertTrue(features.ContainsKey($"turn.phase.{phase}"), $"observation flag for {phase}");
    }

    foreach (TurnStepCursor cursor in HeadlessPhaseMapping.StepCursorOrder)
    {
        AssertTrue(features.ContainsKey($"turn.stepCursor.{cursor}"), $"observation flag for cursor {cursor}");
    }

    AssertEqual((double)(int)HeadlessPhase.Main, features["turn.phaseIndex"], "memory pass folds to main phase index");
    AssertEqual((double)(int)TurnStepCursor.AwaitingMemoryPassEnd, features["turn.stepCursorIndex"], "memory pass step-cursor index");
    AssertEqual(1d, features["turn.phase.Main"], "main phase flag");
    AssertEqual(1d, features["turn.stepCursor.AwaitingMemoryPassEnd"], "memory pass cursor flag");
    AssertEqual(0d, features["turn.stepCursor.PhaseStart"], "not main-play cursor flag");
}

// (R4 S2) Info-preservation gate: every former 9-value HeadlessPhase state maps to a UNIQUE (phase, cursor)
// pair, so the 6-phase + step-cursor representation reconstructs the old model with no information loss. This
// is the RL-observation soundness the encoder relies on — turn.phase.* one-hot × turn.stepCursor.* one-hot
// uniquely identifies each former state.
void NineValueStatesMapToUniquePhaseCursorPairs()
{
    var oldStatesToPairs = new (string OldName, HeadlessPhase Phase, TurnStepCursor Cursor)[]
    {
        ("None",       HeadlessPhase.None,     TurnStepCursor.PhaseStart),
        ("Setup",      HeadlessPhase.None,     TurnStepCursor.Starting),
        ("Active",     HeadlessPhase.Active,   TurnStepCursor.PhaseStart),
        ("Unsuspend",  HeadlessPhase.Active,   TurnStepCursor.Unsuspending),
        ("Draw",       HeadlessPhase.Draw,     TurnStepCursor.PhaseStart),
        ("Breeding",   HeadlessPhase.Breeding, TurnStepCursor.PhaseStart),
        ("Main",       HeadlessPhase.Main,     TurnStepCursor.PhaseStart),
        ("MemoryPass", HeadlessPhase.Main,     TurnStepCursor.AwaitingMemoryPassEnd),
        ("End",        HeadlessPhase.End,      TurnStepCursor.PhaseStart)
    };

    // Uniqueness: all nine (phase, cursor) pairs are distinct.
    var pairs = oldStatesToPairs.Select(s => (s.Phase, s.Cursor)).ToArray();
    AssertEqual(9, pairs.Distinct().Count(), "nine old states -> nine distinct (phase, cursor) pairs");

    // Reconstruction: from a state's observation one-hots we can name the exact old state.
    foreach ((string oldName, HeadlessPhase phase, TurnStepCursor cursor) in oldStatesToPairs)
    {
        var turn = new HeadlessTurnState(
            TurnNumber: 1,
            TurnPlayerId: new HeadlessPlayerId(1),
            NonTurnPlayerId: new HeadlessPlayerId(2),
            Phase: phase,
            StepCursor: cursor,
            IsFirstTurn: false,
            PlayerOrder: new[] { new HeadlessPlayerId(1), new HeadlessPlayerId(2) });

        var features = new ObservationEncoder()
            .Encode(ObservationSnapshot.Empty with { Turn = turn })
            .Features.ToDictionary(f => f.Name, f => f.Value, StringComparer.Ordinal);

        HeadlessPhase decodedPhase = HeadlessPhaseMapping.ObservationPhaseOrder
            .Single(p => features[$"turn.phase.{p}"] == 1d);
        TurnStepCursor decodedCursor = HeadlessPhaseMapping.StepCursorOrder
            .Single(c => features[$"turn.stepCursor.{c}"] == 1d);
        AssertEqual(phase, decodedPhase, $"{oldName} phase reconstructed");
        AssertEqual(cursor, decodedCursor, $"{oldName} cursor reconstructed");
    }
}

void ScopedPhaseFilesHaveNoPlaceholderTodos()
{
    var scopedFiles = new[]
    {
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "HeadlessPhase.cs"),
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "TurnStepCursor.cs"),
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
