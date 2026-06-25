using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G1I-001 goal row keeps the seeded random contract", GoalRowKeepsExpectedContract),
    ("Predecessor result document records COMPLETE", PredecessorResultDocumentRecordsComplete),
    ("IRandomSource exposes next double and shuffle contract", RandomSourceInterfaceKeepsExpectedContract),
    ("GameRandomSource produces the same NextInt sequence for the same seed", SameSeedProducesSameNextIntSequence),
    ("GameRandomSource produces the same NextDouble sequence for the same seed", SameSeedProducesSameNextDoubleSequence),
    ("Different seeds produce different observable random sequences", DifferentSeedsProduceDifferentSequences),
    ("ResetSeed replays the original sequence and updates state reader", ResetSeedReplaysSequence),
    ("Shuffle is deterministic for equal seeds and changes order", ShuffleIsDeterministicForEqualSeeds),
    ("GameRandomSource handles degenerate range and null shuffle inputs explicitly", RangeAndNullInputsAreExplicit),
    ("Seeded random source files have no placeholder or Unity dependency", SourceHasNoPlaceholderOrUnityDependency),
    ("AS-IS GameRandom reference remains read-only input", AsIsGameRandomReferenceRemainsReadOnlyInput),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G1I-001")
        ?? throw new InvalidOperationException("G1I-001 row was not found.");

    AssertEqual("Phase 1", Value(row, "phase"), "phase");
    AssertEqual("Diagnostics", Value(row, "area"), "area");
    AssertEqual("Seeded random", Value(row, "goal"), "goal");
    AssertTrue(Value(row, "scope").Contains("deterministic random", StringComparison.Ordinal), "scope");
    AssertTrue(Value(row, "deliverables").Contains("IRandomSource", StringComparison.Ordinal), "IRandomSource deliverable");
    AssertTrue(Value(row, "deliverables").Contains("GameRandomSource", StringComparison.Ordinal), "GameRandomSource deliverable");
    AssertTrue(Value(row, "unit_test_scope").Contains("next shuffle same seed", StringComparison.Ordinal), "unit test scope");
    AssertEqual("docs/test-results/goals/G1I-001_seeded_random_unit_test_results.md", Value(row, "result_document"), "result document");
    AssertEqual("G1A-001", Value(row, "blocked_until"), "blocked_until");
    AssertTrue(Value(row, "completion_gate").Contains("Seeded random", StringComparison.Ordinal), "completion gate");
    return Task.CompletedTask;
}

Task PredecessorResultDocumentRecordsComplete()
{
    string path = Path.Combine(root, "docs", "test-results", "goals", "G1A-001_runtime_models_unit_test_results.md");
    if (!File.Exists(path))
    {
        throw new FileNotFoundException($"Predecessor result document was not found: {path}");
    }

    string text = File.ReadAllText(path);
    AssertTrue(text.Contains("COMPLETE", StringComparison.Ordinal), "G1A-001 COMPLETE");
    return Task.CompletedTask;
}

Task RandomSourceInterfaceKeepsExpectedContract()
{
    string[] methodNames = typeof(IRandomSource)
        .GetMethods()
        .Select(method => method.Name)
        .OrderBy(name => name, StringComparer.Ordinal)
        .ToArray();

    AssertSequence(new[] { "NextDouble", "NextInt", "Shuffle" }, methodNames, "IRandomSource methods");
    AssertTrue(typeof(IRandomSeedController).IsAssignableFrom(typeof(GameRandomSource)), "seed controller");
    AssertTrue(typeof(IRandomStateReader).IsAssignableFrom(typeof(GameRandomSource)), "state reader");
    return Task.CompletedTask;
}

Task SameSeedProducesSameNextIntSequence()
{
    int[] first = DrawInts(new GameRandomSource(2026));
    int[] second = DrawInts(new GameRandomSource(2026));

    AssertSequence(first, second, "same seed int sequence");
    AssertTrue(first.All(value => value is >= -7 and < 11), "int range");
    return Task.CompletedTask;
}

Task SameSeedProducesSameNextDoubleSequence()
{
    double[] first = DrawDoubles(new GameRandomSource(-77));
    double[] second = DrawDoubles(new GameRandomSource(-77));

    AssertSequence(first, second, "same seed double sequence");
    AssertTrue(first.All(value => value >= 0.0 && value < 1.0), "double range");
    return Task.CompletedTask;
}

Task DifferentSeedsProduceDifferentSequences()
{
    int[] first = DrawInts(new GameRandomSource(12));
    int[] second = DrawInts(new GameRandomSource(13));

    AssertFalse(first.SequenceEqual(second), "different seed int sequence differs");
    return Task.CompletedTask;
}

Task ResetSeedReplaysSequence()
{
    var random = new GameRandomSource(42);
    int[] first = DrawInts(random);
    AssertEqual(42, random.CurrentSeed, "initial seed");

    random.ResetSeed(99);
    int[] changed = DrawInts(random);
    AssertEqual(99, random.CurrentSeed, "changed seed");
    AssertFalse(first.SequenceEqual(changed), "changed seed differs");

    random.ResetSeed(42);
    int[] replay = DrawInts(random);
    AssertEqual(42, random.CurrentSeed, "reset seed");
    AssertSequence(first, replay, "reset replay");
    return Task.CompletedTask;
}

Task ShuffleIsDeterministicForEqualSeeds()
{
    string[] original = Enumerable.Range(1, 20).Select(value => $"card-{value:00}").ToArray();
    List<string> first = original.ToList();
    List<string> second = original.ToList();
    List<string> third = original.ToList();

    new GameRandomSource(31415).Shuffle(first);
    new GameRandomSource(31415).Shuffle(second);
    new GameRandomSource(31416).Shuffle(third);

    AssertSequence(first, second, "same seed shuffle");
    AssertFalse(first.SequenceEqual(original), "shuffle changes order");
    AssertFalse(first.SequenceEqual(third), "different seed shuffle differs");
    AssertSequence(original.OrderBy(value => value, StringComparer.Ordinal).ToArray(), first.OrderBy(value => value, StringComparer.Ordinal).ToArray(), "shuffle preserves members");
    return Task.CompletedTask;
}

Task RangeAndNullInputsAreExplicit()
{
    var random = new GameRandomSource(5);

    AssertEqual(7, random.NextInt(7, 7), "equal range returns min");
    AssertEqual(8, random.NextInt(8, 3), "inverted range returns min");
    ExpectThrows<ArgumentNullException>(() => random.Shuffle<string>(null!));
    return Task.CompletedTask;
}

Task SourceHasNoPlaceholderOrUnityDependency()
{
    string[] relativeFiles =
    {
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "Services", "IRandomSource.cs"),
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "Services", "IRandomSeedController.cs"),
        Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "Services", "GameRandomSource.cs"),
    };

    foreach (string relativeFile in relativeFiles)
    {
        string text = File.ReadAllText(Path.Combine(root, relativeFile));
        AssertFalse(text.Contains("TODO", StringComparison.OrdinalIgnoreCase), $"{relativeFile} TODO");
        AssertFalse(text.Contains("UnityEngine", StringComparison.Ordinal), $"{relativeFile} UnityEngine");
        AssertFalse(text.Contains("Resources.", StringComparison.Ordinal), $"{relativeFile} Resources");
    }

    string randomSource = File.ReadAllText(Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Services", "GameRandomSource.cs"));
    AssertTrue(randomSource.Contains("SplitMix64", StringComparison.Ordinal), "SplitMix64 seeding");
    AssertTrue(randomSource.Contains("RotateLeft", StringComparison.Ordinal), "xoshiro rotate");
    AssertFalse(randomSource.Contains("new Random", StringComparison.Ordinal), "no System.Random runtime source");
    return Task.CompletedTask;
}

Task AsIsGameRandomReferenceRemainsReadOnlyInput()
{
    string path = Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "GameRandom.cs");
    if (!File.Exists(path))
    {
        throw new FileNotFoundException($"AS-IS GameRandom reference file was not found: {path}");
    }

    string text = File.ReadAllText(path);
    AssertTrue(text.Contains("Xoshiro256", StringComparison.OrdinalIgnoreCase), "AS-IS xoshiro reference");
    AssertTrue(text.Contains("SplitMix64", StringComparison.Ordinal), "AS-IS splitmix reference");
    AssertTrue(text.Contains("Range", StringComparison.Ordinal), "AS-IS range reference");
    return Task.CompletedTask;
}

static int[] DrawInts(IRandomSource random)
{
    return Enumerable.Range(0, 16)
        .Select(_ => random.NextInt(-7, 11))
        .ToArray();
}

static double[] DrawDoubles(IRandomSource random)
{
    return Enumerable.Range(0, 16)
        .Select(_ => random.NextDouble())
        .ToArray();
}

static IReadOnlyList<Dictionary<string, string>> ReadCsv(string path)
{
    string[] lines = File.ReadAllLines(path);
    if (lines.Length == 0)
    {
        return Array.Empty<Dictionary<string, string>>();
    }

    string[] headers = SplitCsvLine(lines[0]).ToArray();
    var rows = new List<Dictionary<string, string>>();

    foreach (string line in lines.Skip(1))
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            continue;
        }

        string[] cells = SplitCsvLine(line).ToArray();
        var row = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < headers.Length; i++)
        {
            row[headers[i]] = i < cells.Length ? cells[i] : string.Empty;
        }

        rows.Add(row);
    }

    return rows;
}

static IEnumerable<string> SplitCsvLine(string line)
{
    var current = new List<char>();
    bool inQuotes = false;

    for (int i = 0; i < line.Length; i++)
    {
        char c = line[i];
        if (c == '"')
        {
            if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
            {
                current.Add('"');
                i++;
            }
            else
            {
                inQuotes = !inQuotes;
            }
        }
        else if (c == ',' && !inQuotes)
        {
            yield return new string(current.ToArray());
            current.Clear();
        }
        else
        {
            current.Add(c);
        }
    }

    yield return new string(current.ToArray());
}

static string Value(Dictionary<string, string> row, string key)
{
    return row.TryGetValue(key, out string? value) ? value : string.Empty;
}

static string FindRepositoryRoot()
{
    var current = new DirectoryInfo(AppContext.BaseDirectory);
    while (current is not null)
    {
        var docsPath = Path.Combine(current.FullName, "docs", "headless_complete_goal_breakdown.csv");
        var srcPath = Path.Combine(current.FullName, "src", "HeadlessDCGO.Engine", "HeadlessDCGO.Engine.csproj");
        if (File.Exists(docsPath) && File.Exists(srcPath))
        {
            return current.FullName;
        }

        current = current.Parent;
    }

    throw new DirectoryNotFoundException("Could not find repository root from the test binary path.");
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

static void AssertTrue(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException($"Expected true: {message}.");
    }
}

static void AssertFalse(bool condition, string message)
{
    if (condition)
    {
        throw new InvalidOperationException($"Expected false: {message}.");
    }
}

static void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{message}: expected {expected}, actual {actual}.");
    }
}

static void AssertSequence<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual, string message)
{
    if (expected.Count != actual.Count)
    {
        throw new InvalidOperationException($"{message}: expected count {expected.Count}, actual {actual.Count}.");
    }

    for (int i = 0; i < expected.Count; i++)
    {
        if (!EqualityComparer<T>.Default.Equals(expected[i], actual[i]))
        {
            throw new InvalidOperationException($"{message}: index {i} expected {expected[i]}, actual {actual[i]}.");
        }
    }
}
