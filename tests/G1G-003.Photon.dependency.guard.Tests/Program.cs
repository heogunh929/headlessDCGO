using System.Xml.Linq;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G1G-003 goal row keeps the Photon dependency guard contract", GoalRowKeepsExpectedContract),
    ("Predecessor result document records COMPLETE", PredecessorResultDocumentRecordsComplete),
    ("Headless source has no Photon namespace dependency", HeadlessSourceHasNoPhotonDependency),
    ("Headless project files do not reference Photon packages", HeadlessProjectFilesDoNotReferencePhotonPackages),
    ("Dependency scan returns explicit failure for Photon input", DependencyScanReturnsExplicitFailureForPhotonInput),
    ("Dependency scan is deterministic for identical input", DependencyScanIsDeterministic),
    ("Replacement plan documents local deterministic Photon replacement", ReplacementPlanDocumentsPhotonReplacement),
    ("AS-IS Photon references remain read-only inputs", AsIsPhotonReferencesRemainReadOnlyInputs),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G1G-003")
        ?? throw new InvalidOperationException("G1G-003 row was not found.");

    AssertEqual("Phase 1", Value(row, "phase"), "phase");
    AssertEqual("Session", Value(row, "area"), "area");
    AssertEqual("Photon dependency guard", Value(row, "goal"), "goal");
    AssertTrue(Value(row, "scope").Contains("Photon", StringComparison.Ordinal), "scope");
    AssertEqual("dependency scan test", Value(row, "deliverables"), "deliverables");
    AssertTrue(Value(row, "unit_test_scope").Contains("Photon namespace absence", StringComparison.Ordinal), "unit test scope");
    AssertEqual("docs/test-results/goals/G1G-003_photon_dependency_guard_unit_test_results.md", Value(row, "result_document"), "result document");
    AssertEqual("G1G-002", Value(row, "blocked_until"), "blocked_until");
    AssertTrue(Value(row, "completion_gate").Contains("Photon guard", StringComparison.Ordinal), "completion gate");
    return Task.CompletedTask;
}

Task PredecessorResultDocumentRecordsComplete()
{
    string path = Path.Combine(root, "docs", "test-results", "goals", "G1G-002_action_queue_replay_unit_test_results.md");
    if (!File.Exists(path))
    {
        throw new FileNotFoundException($"Predecessor result document was not found: {path}");
    }

    string text = File.ReadAllText(path);
    AssertTrue(text.Contains("COMPLETE", StringComparison.Ordinal), "G1G-002 COMPLETE");
    return Task.CompletedTask;
}

Task HeadlessSourceHasNoPhotonDependency()
{
    string headlessRoot = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless");
    DependencyScanResult result = DependencyGuard.ScanFiles(
        root,
        EnumerateFiles(headlessRoot, "*.cs"),
        DependencyGuard.PhotonSourceTokens);

    AssertTrue(result.ScannedFileCount > 0, "source files scanned");
    AssertTrue(result.IsClean, FormatViolations(result));
    return Task.CompletedTask;
}

Task HeadlessProjectFilesDoNotReferencePhotonPackages()
{
    string projectRoot = Path.Combine(root, "src", "HeadlessDCGO.Engine");
    DependencyScanResult textResult = DependencyGuard.ScanFiles(
        root,
        EnumerateFiles(projectRoot, "*.csproj"),
        DependencyGuard.PhotonProjectTokens);

    AssertTrue(textResult.ScannedFileCount > 0, "project files scanned");
    AssertTrue(textResult.IsClean, FormatViolations(textResult));

    foreach (string path in EnumerateFiles(projectRoot, "*.csproj"))
    {
        XDocument doc = XDocument.Load(path);
        var packageReferences = doc
            .Descendants()
            .Where(e => e.Name.LocalName == "PackageReference")
            .Select(e => ((string?)e.Attribute("Include") ?? string.Empty).Trim())
            .Where(v => v.Length > 0)
            .ToArray();

        foreach (string package in packageReferences)
        {
            AssertFalse(
                DependencyGuard.PhotonProjectTokens.Any(t => package.Contains(t, StringComparison.OrdinalIgnoreCase)),
                $"{Path.GetFileName(path)} must not reference Photon package {package}");
        }
    }

    return Task.CompletedTask;
}

Task DependencyScanReturnsExplicitFailureForPhotonInput()
{
    var samples = new Dictionary<string, string>
    {
        ["BadPhoton.cs"] = "using Photon.Pun; public sealed class BadPhoton : MonoBehaviourPun { [PunRPC] public void Sync() {} }",
        ["BadPhoton.csproj"] = "<Project><ItemGroup><PackageReference Include=\"Photon.Pun\" Version=\"1.0.0\" /></ItemGroup></Project>",
    };

    DependencyScanResult result = DependencyGuard.ScanText(samples, DependencyGuard.AllPhotonTokens);

    AssertFalse(result.IsClean, "scan should fail when Photon input is present");
    AssertEqual(2, result.Violations.Count, "violation count");
    AssertTrue(result.Violations.Any(v => v.RelativePath == "BadPhoton.cs"), "source violation");
    AssertTrue(result.Violations.Any(v => v.RelativePath == "BadPhoton.csproj" && v.Token == "Photon.Pun"), "project violation");
    return Task.CompletedTask;
}

Task DependencyScanIsDeterministic()
{
    string headlessRoot = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless");
    string[] files = EnumerateFiles(headlessRoot, "*.cs").ToArray();

    DependencyScanResult first = DependencyGuard.ScanFiles(root, files, DependencyGuard.PhotonSourceTokens);
    DependencyScanResult second = DependencyGuard.ScanFiles(root, files, DependencyGuard.PhotonSourceTokens);

    AssertEqual(first.Fingerprint, second.Fingerprint, "scan fingerprint");
    AssertEqual(first.ScannedFileCount, second.ScannedFileCount, "scanned file count");
    AssertEqual(first.Violations.Count, second.Violations.Count, "violation count");
    return Task.CompletedTask;
}

Task ReplacementPlanDocumentsPhotonReplacement()
{
    string path = Path.Combine(root, "docs", "dotnet_non_unity_dependency_replacement_plan.md");
    string text = File.ReadAllText(path);

    AssertTrue(text.Contains("| Photon | Replace |", StringComparison.Ordinal), "Photon replacement decision");
    AssertTrue(text.Contains("No Photon transport, lobby, room, RPC, or network ownership in Headless.", StringComparison.Ordinal), "Photon replacement policy");
    AssertTrue(text.Contains("Network-dependent gameplay flow receives explicit local match/player/action context", StringComparison.Ordinal), "local context replacement");
    return Task.CompletedTask;
}

Task AsIsPhotonReferencesRemainReadOnlyInputs()
{
    var references = new (string Path, string[] Patterns)[]
    {
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "GManager.cs"),
            new[] { "Photon", "Player" }),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "GameContext.cs"),
            new[] { "GameContext", "Photon" }),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "TurnStateMachine.cs"),
            new[] { "TurnStateMachine", "Player" }),
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

static IEnumerable<string> EnumerateFiles(string directory, string searchPattern)
{
    return Directory
        .EnumerateFiles(directory, searchPattern, SearchOption.AllDirectories)
        .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        .OrderBy(path => path, StringComparer.Ordinal);
}

static string FormatViolations(DependencyScanResult result)
{
    if (result.IsClean)
    {
        return "no violations";
    }

    return string.Join("; ", result.Violations.Select(v => $"{v.RelativePath}:{v.Token}"));
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

internal static class DependencyGuard
{
    public static readonly string[] PhotonSourceTokens =
    {
        "using Photon",
        "Photon.Pun",
        "Photon.Realtime",
        "PhotonNetwork",
        "MonoBehaviourPun",
        "PunRPC",
        "IPunObservable",
    };

    public static readonly string[] PhotonProjectTokens =
    {
        "Photon",
        "Photon.Pun",
        "Photon.Realtime",
        "PhotonUnityNetworking",
    };

    public static readonly string[] AllPhotonTokens = PhotonSourceTokens
        .Concat(PhotonProjectTokens)
        .Distinct(StringComparer.Ordinal)
        .ToArray();

    public static DependencyScanResult ScanFiles(
        string repositoryRoot,
        IEnumerable<string> paths,
        IReadOnlyList<string> forbiddenTokens)
    {
        var inputs = paths.ToDictionary(
            path => Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/'),
            File.ReadAllText,
            StringComparer.Ordinal);

        return ScanText(inputs, forbiddenTokens);
    }

    public static DependencyScanResult ScanText(
        IReadOnlyDictionary<string, string> inputs,
        IReadOnlyList<string> forbiddenTokens)
    {
        var violations = new List<DependencyViolation>();
        foreach (KeyValuePair<string, string> input in inputs.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            foreach (string token in forbiddenTokens.OrderByDescending(t => t.Length).ThenBy(t => t, StringComparer.Ordinal))
            {
                if (input.Value.Contains(token, StringComparison.Ordinal))
                {
                    violations.Add(new DependencyViolation(input.Key, token));
                    break;
                }
            }
        }

        string fingerprint = string.Join(
            "\n",
            inputs.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key}:{p.Value.Length}"));

        return new DependencyScanResult(inputs.Count, violations, fingerprint);
    }
}

internal sealed record DependencyScanResult(
    int ScannedFileCount,
    IReadOnlyList<DependencyViolation> Violations,
    string Fingerprint)
{
    public bool IsClean => Violations.Count == 0;
}

internal sealed record DependencyViolation(string RelativePath, string Token);
