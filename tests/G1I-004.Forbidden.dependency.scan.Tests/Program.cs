using System.Security.Cryptography;
using System.Text;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("G1I-004 goal row keeps the forbidden dependency scan contract", GoalRowKeepsExpectedContract),
    ("Predecessor result documents record COMPLETE", PredecessorResultDocumentsRecordComplete),
    ("Forbidden dependency scan covers Headless source and engine project files", ScanCoversHeadlessSourceAndProjectFiles),
    ("Headless source has no Unity Photon TMPro DOTween or UI namespace dependency", HeadlessSourceHasNoForbiddenDependency),
    ("Headless project files do not reference forbidden dependency packages", HeadlessProjectFilesHaveNoForbiddenPackages),
    ("Dependency scan reports Unity Photon TMPro DOTween and UI violations", DependencyScanReportsForbiddenSamples),
    ("Dependency scan treats AS-IS Assets source as out of scope", AsIsAssetsSourceIsOutOfScope),
    ("Dependency scan result is deterministic for repeated input", DependencyScanIsDeterministic),
    ("Source dependency policy documents predecessor guard results", PredecessorGuardResultsAreLinked),
    ("AS-IS forbidden dependency references remain read-only inputs", AsIsForbiddenReferencesRemainReadOnlyInputs),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G1I-004")
        ?? throw new InvalidOperationException("G1I-004 row was not found.");

    AssertEqual("Phase 1", Value(row, "phase"), "phase");
    AssertEqual("Diagnostics", Value(row, "area"), "area");
    AssertEqual("Forbidden dependency scan", Value(row, "goal"), "goal");
    AssertTrue(Value(row, "scope").Contains("의존성", StringComparison.Ordinal), "scope");
    AssertEqual("dependency scan test", Value(row, "deliverables"), "deliverables");
    AssertTrue(Value(row, "unit_test_scope").Contains("Unity Photon TMPro DOTween UI namespace absence", StringComparison.Ordinal), "unit test scope");
    AssertEqual("docs/test-results/goals/G1I-004_forbidden_dependency_scan_unit_test_results.md", Value(row, "result_document"), "result document");
    AssertEqual("G1G-003; G1H-005", Value(row, "blocked_until"), "blocked_until");
    AssertTrue(Value(row, "completion_gate").Contains("forbidden dependency", StringComparison.Ordinal), "completion gate");
    return Task.CompletedTask;
}

Task PredecessorResultDocumentsRecordComplete()
{
    string[] paths =
    {
        Path.Combine(root, "docs", "test-results", "goals", "G1G-003_photon_dependency_guard_unit_test_results.md"),
        Path.Combine(root, "docs", "test-results", "goals", "G1H-005_unity_asset_exclusion_guard_unit_test_results.md"),
    };

    foreach (string path in paths)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Predecessor result document was not found: {path}");
        }

        string text = File.ReadAllText(path);
        AssertTrue(text.Contains("COMPLETE", StringComparison.Ordinal), $"{Path.GetFileName(path)} COMPLETE");
    }

    return Task.CompletedTask;
}

Task ScanCoversHeadlessSourceAndProjectFiles()
{
    string[] sourceFiles = HeadlessSourceFiles().ToArray();
    string[] projectFiles = EngineProjectFiles().ToArray();

    AssertTrue(sourceFiles.Length > 50, "Headless source file count");
    AssertTrue(projectFiles.Length >= 1, "engine project file count");
    AssertTrue(sourceFiles.Any(path => path.EndsWith("Diagnostics/EngineTrace.cs", StringComparison.Ordinal)), "diagnostics source covered");
    AssertTrue(sourceFiles.Any(path => path.EndsWith("Services/IRandomSource.cs", StringComparison.Ordinal)), "random source covered");
    AssertTrue(sourceFiles.Any(path => path.EndsWith("Services/ILogSink.cs", StringComparison.Ordinal)), "log sink covered");
    AssertTrue(sourceFiles.Any(path => path.EndsWith("Bridge/ContinuousContext.cs", StringComparison.Ordinal)), "continuous context covered");
    AssertFalse(sourceFiles.Any(path => path.Contains("/Assets/", StringComparison.Ordinal)), "Assets directory excluded from Headless scan");
    return Task.CompletedTask;
}

Task HeadlessSourceHasNoForbiddenDependency()
{
    DependencyScanResult result = ForbiddenDependencyGuard.ScanFiles(
        root,
        HeadlessSourceFiles(),
        ForbiddenDependencyGuard.ForbiddenSourceTokens);

    AssertTrue(result.ScannedFileCount > 50, "source files scanned");
    AssertTrue(result.IsClean, FormatViolations(result));
    AssertTrue(result.Fingerprint.Length > 0, "scan fingerprint");
    return Task.CompletedTask;
}

Task HeadlessProjectFilesHaveNoForbiddenPackages()
{
    DependencyScanResult result = ForbiddenDependencyGuard.ScanFiles(
        root,
        EngineProjectFiles(),
        ForbiddenDependencyGuard.ForbiddenProjectTokens);

    AssertTrue(result.ScannedFileCount >= 1, "project files scanned");
    AssertTrue(result.IsClean, FormatViolations(result));
    return Task.CompletedTask;
}

Task DependencyScanReportsForbiddenSamples()
{
    var samples = new Dictionary<string, string>
    {
        ["UnitySample.cs"] = "using UnityEngine; public sealed class Bad : MonoBehaviour { public GameObject Target = null!; }",
        ["PhotonSample.cs"] = "using Photon.Pun; public sealed class BadPun : MonoBehaviourPun { [PunRPC] public void Sync() {} }",
        ["TmproSample.cs"] = "using TMPro; public sealed class BadText { public TMP_Text Text = null!; }",
        ["DotweenSample.cs"] = "using DG.Tweening; public sealed class BadTween { public void Run() => DOTween.KillAll(); }",
        ["UiSample.cs"] = "using UnityEngine.UI; using UnityEngine.UIElements; public sealed class BadUi { public Button Button = null!; }",
        ["Packages.csproj"] = "<Project><ItemGroup><PackageReference Include=\"Photon.Pun\" /><PackageReference Include=\"DOTween\" /></ItemGroup></Project>",
    };

    DependencyScanResult result = ForbiddenDependencyGuard.ScanText(
        samples,
        ForbiddenDependencyGuard.AllForbiddenTokens);

    AssertFalse(result.IsClean, "forbidden sample scan should fail");
    AssertTrue(result.Violations.Any(v => v.Token == "using UnityEngine"), "Unity violation");
    AssertTrue(result.Violations.Any(v => v.Token == "Photon.Pun"), "Photon violation");
    AssertTrue(result.Violations.Any(v => v.Token == "using TMPro"), "TMPro violation");
    AssertTrue(result.Violations.Any(v => v.Token == "DG.Tweening"), "DOTween violation");
    AssertTrue(result.Violations.Any(v => v.Token == "UnityEngine.UI"), "UI violation");
    return Task.CompletedTask;
}

Task AsIsAssetsSourceIsOutOfScope()
{
    string assetsSourceRoot = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Assets");
    AssertTrue(Directory.Exists(assetsSourceRoot), "AS-IS Assets source exists under engine project");

    string[] sourceFiles = HeadlessSourceFiles().ToArray();
    AssertFalse(sourceFiles.Any(path => path.Contains("/Assets/", StringComparison.Ordinal)), "Assets source excluded");

    string[] assetsFiles = EnumerateFiles(assetsSourceRoot, "*.cs").ToArray();
    AssertTrue(assetsFiles.Length > 0, "AS-IS Assets source file count");
    return Task.CompletedTask;
}

Task DependencyScanIsDeterministic()
{
    string[] sourceFiles = HeadlessSourceFiles().ToArray();

    DependencyScanResult first = ForbiddenDependencyGuard.ScanFiles(
        root,
        sourceFiles,
        ForbiddenDependencyGuard.ForbiddenSourceTokens);
    DependencyScanResult second = ForbiddenDependencyGuard.ScanFiles(
        root,
        sourceFiles.Reverse(),
        ForbiddenDependencyGuard.ForbiddenSourceTokens);

    AssertEqual(first.Fingerprint, second.Fingerprint, "scan fingerprint");
    AssertEqual(first.ScannedFileCount, second.ScannedFileCount, "scanned file count");
    AssertSequence(first.Violations.Select(FormatViolation).ToArray(), second.Violations.Select(FormatViolation).ToArray(), "violations");
    return Task.CompletedTask;
}

Task PredecessorGuardResultsAreLinked()
{
    string photon = File.ReadAllText(Path.Combine(root, "docs", "test-results", "goals", "G1G-003_photon_dependency_guard_unit_test_results.md"));
    string asset = File.ReadAllText(Path.Combine(root, "docs", "test-results", "goals", "G1H-005_unity_asset_exclusion_guard_unit_test_results.md"));

    AssertTrue(photon.Contains("Photon namespace absence", StringComparison.Ordinal), "Photon guard result");
    AssertTrue(asset.Contains("asset exclusion scan", StringComparison.Ordinal), "asset exclusion result");
    AssertTrue(asset.Contains("Unity asset guard", StringComparison.Ordinal), "Unity asset guard result");
    return Task.CompletedTask;
}

Task AsIsForbiddenReferencesRemainReadOnlyInputs()
{
    var references = new (string Path, string[] Patterns)[]
    {
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "TurnStateMachine.cs"),
            new[] { "Photon", "UnityEngine", "UnityEngine.UI" }),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "PlayLog.cs"),
            new[] { "UnityEngine", "TMPro", "UnityEngine.UI" }),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "GameRandom.cs"),
            new[] { "GameRandom", "Seed", "Range" }),
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

IEnumerable<string> HeadlessSourceFiles()
{
    return EnumerateFiles(Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless"), "*.cs")
        .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'));
}

IEnumerable<string> EngineProjectFiles()
{
    return EnumerateFiles(Path.Combine(root, "src", "HeadlessDCGO.Engine"), "*.csproj")
        .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'));
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
    return result.IsClean
        ? "no violations"
        : string.Join("; ", result.Violations.Select(FormatViolation));
}

static string FormatViolation(DependencyViolation violation)
{
    return $"{violation.RelativePath}:{violation.Line}:{violation.Token}";
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

internal static class ForbiddenDependencyGuard
{
    public static readonly string[] ForbiddenSourceTokens =
    {
        "using UnityEngine",
        "UnityEngine.",
        "UnityEngine.UI",
        "UnityEngine.UIElements",
        "using UnityEditor",
        "UnityEditor.",
        "MonoBehaviour",
        "ScriptableObject",
        "Resources.",
        "AssetDatabase",
        "using Photon",
        "Photon.",
        "PhotonNetwork",
        "MonoBehaviourPun",
        "PunRPC",
        "IPunObservable",
        "using TMPro",
        "TMPro.",
        "TMP_Text",
        "using DG.Tweening",
        "DG.Tweening",
        "DOTween",
    };

    public static readonly string[] ForbiddenProjectTokens =
    {
        "UnityEngine",
        "UnityEditor",
        "Photon",
        "Photon.Pun",
        "PhotonUnityNetworking",
        "TMPro",
        "TextMeshPro",
        "DOTween",
        "DG.Tweening",
    };

    public static readonly string[] AllForbiddenTokens = ForbiddenSourceTokens
        .Concat(ForbiddenProjectTokens)
        .Distinct(StringComparer.Ordinal)
        .ToArray();

    public static DependencyScanResult ScanFiles(
        string repositoryRoot,
        IEnumerable<string> relativePaths,
        IReadOnlyList<string> forbiddenTokens)
    {
        var inputs = relativePaths
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToDictionary(
                path => path,
                path => File.ReadAllText(Path.Combine(repositoryRoot, path)),
                StringComparer.Ordinal);

        return ScanText(inputs, forbiddenTokens);
    }

    public static DependencyScanResult ScanText(
        IReadOnlyDictionary<string, string> inputs,
        IReadOnlyList<string> forbiddenTokens)
    {
        var violations = new List<DependencyViolation>();
        var fingerprintBuilder = new StringBuilder();

        foreach ((string path, string text) in inputs.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            fingerprintBuilder.Append(path).Append('\n').Append(text).Append('\n');

            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex];
                foreach (string token in forbiddenTokens.OrderBy(token => token, StringComparer.Ordinal))
                {
                    if (line.Contains(token, StringComparison.Ordinal))
                    {
                        violations.Add(new DependencyViolation(path, lineIndex + 1, token));
                    }
                }
            }
        }

        string fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintBuilder.ToString())));
        return new DependencyScanResult(
            inputs.Count,
            violations
                .OrderBy(v => v.RelativePath, StringComparer.Ordinal)
                .ThenBy(v => v.Line)
                .ThenBy(v => v.Token, StringComparer.Ordinal)
                .ToArray(),
            fingerprint);
    }
}

internal sealed record DependencyScanResult(
    int ScannedFileCount,
    IReadOnlyList<DependencyViolation> Violations,
    string Fingerprint)
{
    public bool IsClean => Violations.Count == 0;
}

internal sealed record DependencyViolation(
    string RelativePath,
    int Line,
    string Token);
