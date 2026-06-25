using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();
var fixtureRoot = Path.Combine(root, ".tmp", "g1h-005-unity-asset-exclusion-guard-tests", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(fixtureRoot);

var tests = new (string Name, Func<Task> Body)[]
{
    ("G1H-005 goal row keeps the Unity asset exclusion contract", GoalRowKeepsExpectedContract),
    ("G1H-005 predecessor result documents record COMPLETE", PredecessorResultDocumentsRecordComplete),
    ("Asset exclusion scan covers DataLoading and repository contract files", AssetExclusionScanCoversExpectedFiles),
    ("Headless data loading sources have no Unity visual asset dependencies", DataLoadingSourcesHaveNoUnityVisualAssetDependencies),
    ("CardRecord public gameplay fields exclude image prefab audio animation fields", CardRecordPublicGameplayFieldsExcludeVisualAssets),
    ("ICardRepository public contract returns only headless card records", CardRepositoryContractReturnsHeadlessRecords),
    ("CardAssetJsonLoader keeps visual paths as metadata instead of gameplay fields", CardLoaderKeepsVisualPathsAsMetadata),
    ("Asset exclusion scan reports forbidden Unity visual asset usage", AssetExclusionScanReportsForbiddenUsage),
    ("Asset exclusion scan result is deterministic for repeated input", AssetExclusionScanIsDeterministic),
    ("AS-IS Unity asset references remain read-only inputs", AsIsReferencesRemainReadOnlyInputs),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G1H-005")
        ?? throw new InvalidOperationException("G1H-005 row was not found.");

    AssertEqual("Phase 1", Value(row, "phase"), "phase");
    AssertEqual("DataLoading", Value(row, "area"), "area");
    AssertEqual("Unity asset exclusion guard", Value(row, "goal"), "goal");
    AssertTrue(Value(row, "scope").Contains("visual asset", StringComparison.Ordinal), "scope");
    AssertEqual("asset exclusion scan", Value(row, "deliverables"), "deliverables");
    AssertTrue(Value(row, "unit_test_scope").Contains("image prefab audio animation exclusion", StringComparison.Ordinal), "unit test scope");
    AssertEqual("docs/test-results/goals/G1H-005_unity_asset_exclusion_guard_unit_test_results.md", Value(row, "result_document"), "result document");
    AssertEqual("G1H-002; G1H-003", Value(row, "blocked_until"), "blocked_until");
    AssertTrue(Value(row, "completion_gate").Contains("Unity asset guard", StringComparison.Ordinal), "completion gate");
    return Task.CompletedTask;
}

Task PredecessorResultDocumentsRecordComplete()
{
    string[] paths =
    {
        Path.Combine(root, "docs", "test-results", "goals", "G1H-002_card_json_loader_unit_test_results.md"),
        Path.Combine(root, "docs", "test-results", "goals", "G1H-003_deck_list_loader_unit_test_results.md"),
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

Task AssetExclusionScanCoversExpectedFiles()
{
    string[] relativeFiles = ProductionScanFiles().ToArray();

    AssertContains(relativeFiles, Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "DataLoading", "BanlistLoader.cs"), "banlist loader coverage");
    AssertContains(relativeFiles, Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "DataLoading", "CardAssetJsonLoader.cs"), "card loader coverage");
    AssertContains(relativeFiles, Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "DataLoading", "DeckListLoader.cs"), "deck loader coverage");
    AssertContains(relativeFiles, Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "Services", "ICardRepository.cs"), "repository coverage");
    AssertContains(relativeFiles, Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "Services", "CardRecord.cs"), "card record coverage");

    foreach (string relativeFile in relativeFiles)
    {
        AssertTrue(File.Exists(Path.Combine(root, relativeFile)), $"scan target exists: {relativeFile}");
    }

    return Task.CompletedTask;
}

Task DataLoadingSourcesHaveNoUnityVisualAssetDependencies()
{
    AssetScanResult result = AssetExclusionGuard.ScanFiles(root, ProductionScanFiles());
    AssertEqual(0, result.Violations.Count, "asset exclusion violation count");
    AssertTrue(result.Fingerprint.Length > 0, "fingerprint");
    return Task.CompletedTask;
}

Task CardRecordPublicGameplayFieldsExcludeVisualAssets()
{
    string[] forbiddenNameParts =
    {
        "Image",
        "Sprite",
        "Prefab",
        "Audio",
        "Animation",
        "Texture",
        "Material",
        "AssetPath",
        "ResourcePath",
    };

    string[] propertyNames = typeof(CardRecord)
        .GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .Select(property => property.Name)
        .OrderBy(name => name, StringComparer.Ordinal)
        .ToArray();

    AssertContains(propertyNames, nameof(CardRecord.Id), "card id property");
    AssertContains(propertyNames, nameof(CardRecord.CardNumber), "card number property");
    AssertContains(propertyNames, nameof(CardRecord.Name), "name property");
    AssertContains(propertyNames, nameof(CardRecord.PlayCost), "play cost property");
    AssertContains(propertyNames, nameof(CardRecord.EvolutionCost), "evolution cost property");
    AssertContains(propertyNames, nameof(CardRecord.EffectBindingKey), "effect binding property");

    foreach (string propertyName in propertyNames)
    {
        foreach (string forbiddenNamePart in forbiddenNameParts)
        {
            AssertFalse(propertyName.Contains(forbiddenNamePart, StringComparison.OrdinalIgnoreCase), $"{propertyName} excludes {forbiddenNamePart}");
        }
    }

    return Task.CompletedTask;
}

Task CardRepositoryContractReturnsHeadlessRecords()
{
    MethodInfo[] methods = typeof(ICardRepository)
        .GetMethods(BindingFlags.Instance | BindingFlags.Public)
        .OrderBy(method => method.Name, StringComparer.Ordinal)
        .ThenBy(method => method.ToString(), StringComparer.Ordinal)
        .ToArray();

    AssertSequence(new[] { "GetCard", "Query", "Snapshot", "TryGetCard" }, methods.Select(method => method.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray(), "repository method names");

    string apiText = string.Join(
        "\n",
        methods.Select(method => string.Join(
            " ",
            method.Name,
            method.ReturnType.FullName,
            string.Join(" ", method.GetParameters().Select(parameter => $"{parameter.ParameterType.FullName} {parameter.Name}")))));

    AssetScanResult scan = AssetExclusionGuard.ScanText(new Dictionary<string, string>
    {
        ["ICardRepository public contract"] = apiText,
    });

    AssertEqual(0, scan.Violations.Count, "repository contract violation count");
    AssertTrue(apiText.Contains(typeof(CardRecord).FullName!, StringComparison.Ordinal), "repository exposes CardRecord");
    return Task.CompletedTask;
}

Task CardLoaderKeepsVisualPathsAsMetadata()
{
    string cardPath = WriteFixture(
        "visual-metadata-card.json",
        """
        {
          "id": "visual-fixture",
          "cardNumber": "BTX-001",
          "name": "Visual Metadata Fixture",
          "cardType": "Digimon",
          "playCost": 3,
          "evolutionCost": 1,
          "effectBindingKey": "btx-001-main",
          "imagePath": "Cards/BTX-001.png",
          "prefabPath": "Prefabs/Card.prefab",
          "audioPath": "Audio/card.wav",
          "animationPath": "Animations/card.anim"
        }
        """);

    CardRecord card = new CardAssetJsonLoader().LoadFile(cardPath);

    AssertEqual(new HeadlessEntityId("visual-fixture"), card.Id, "id");
    AssertEqual("BTX-001", card.CardNumber, "card number");
    AssertEqual("Visual Metadata Fixture", card.Name, "name");
    AssertEqual("Digimon", card.CardType, "card type");
    AssertEqual(3, card.PlayCost, "play cost");
    AssertEqual(1, card.EvolutionCost, "evolution cost");
    AssertEqual("btx-001-main", card.EffectBindingKey, "effect binding key");
    AssertEqual("Cards/BTX-001.png", card.Metadata["imagePath"], "image metadata");
    AssertEqual("Prefabs/Card.prefab", card.Metadata["prefabPath"], "prefab metadata");
    AssertEqual("Audio/card.wav", card.Metadata["audioPath"], "audio metadata");
    AssertEqual("Animations/card.anim", card.Metadata["animationPath"], "animation metadata");

    string[] gameplayValues =
    {
        card.Id.Value,
        card.CardNumber,
        card.Name,
        card.CardType,
        card.EvolutionCondition ?? string.Empty,
        card.EffectBindingKey ?? string.Empty,
    };

    AssertFalse(gameplayValues.Any(value => value.EndsWith(".png", StringComparison.OrdinalIgnoreCase)), "image path not in gameplay values");
    AssertFalse(gameplayValues.Any(value => value.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)), "prefab path not in gameplay values");
    AssertFalse(gameplayValues.Any(value => value.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)), "audio path not in gameplay values");
    AssertFalse(gameplayValues.Any(value => value.EndsWith(".anim", StringComparison.OrdinalIgnoreCase)), "animation path not in gameplay values");
    return Task.CompletedTask;
}

Task AssetExclusionScanReportsForbiddenUsage()
{
    AssetScanResult result = AssetExclusionGuard.ScanText(new Dictionary<string, string>
    {
        ["bad-image.cs"] = "using UnityEngine; public sealed class Bad { public Sprite Image { get; init; } = null!; }",
        ["bad-prefab.cs"] = "public sealed class BadPrefab { public GameObject Prefab { get; init; } = null!; }",
        ["bad-audio.cs"] = "public sealed class BadAudio { public AudioClip Audio { get; init; } = null!; }",
        ["bad-animation.cs"] = "public sealed class BadAnimation { public AnimationClip Clip { get; init; } = null!; }",
    });

    AssertTrue(result.Violations.Count >= 4, "forbidden asset usage should be reported");
    AssertTrue(result.Violations.Any(violation => violation.Token == "using UnityEngine"), "UnityEngine violation");
    AssertTrue(result.Violations.Any(violation => violation.Token == "Sprite"), "image Sprite violation");
    AssertTrue(result.Violations.Any(violation => violation.Token == "GameObject"), "prefab GameObject violation");
    AssertTrue(result.Violations.Any(violation => violation.Token == "AudioClip"), "audio AudioClip violation");
    AssertTrue(result.Violations.Any(violation => violation.Token == "AnimationClip"), "animation AnimationClip violation");
    return Task.CompletedTask;
}

Task AssetExclusionScanIsDeterministic()
{
    var inputs = new Dictionary<string, string>
    {
        ["b.cs"] = "public sealed class B { }",
        ["a.cs"] = "public sealed class A { }",
    };

    AssetScanResult first = AssetExclusionGuard.ScanText(inputs);
    AssetScanResult second = AssetExclusionGuard.ScanText(inputs.Reverse().ToDictionary(pair => pair.Key, pair => pair.Value));

    AssertEqual(first.Fingerprint, second.Fingerprint, "repeat fingerprint");
    AssertSequence(first.Violations.Select(FormatViolation).ToArray(), second.Violations.Select(FormatViolation).ToArray(), "repeat violations");
    return Task.CompletedTask;
}

Task AsIsReferencesRemainReadOnlyInputs()
{
    var references = new (string Path, string[] Patterns)[]
    {
        (
            Path.Combine(root, "DCGO", "Assets", "CardBaseEntity"),
            Array.Empty<string>()),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "DeckData.cs"),
            new[] { "DeckData", "IsValidDeckCode", "DeckCardIDs", "DigitamaDeckCardIDs" }),
        (
            Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "DeckCodeUtility.cs"),
            new[] { "DeckCodeUtility", "GetDeckBuilderDeckCode", "CardID" }),
    };

    foreach ((string path, string[] patterns) in references)
    {
        if (Directory.Exists(path))
        {
            AssertTrue(Directory.EnumerateFileSystemEntries(path).Any(), $"AS-IS directory has entries: {path}");
            continue;
        }

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

IEnumerable<string> ProductionScanFiles()
{
    string dataLoadingRoot = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "DataLoading");

    foreach (string file in Directory.EnumerateFiles(dataLoadingRoot, "*.cs", SearchOption.TopDirectoryOnly)
        .Select(path => Path.GetRelativePath(root, path))
        .OrderBy(path => path, StringComparer.Ordinal))
    {
        yield return file;
    }

    yield return Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "Services", "ICardRepository.cs");
    yield return Path.Combine("src", "HeadlessDCGO.Engine", "Headless", "Services", "CardRecord.cs");
}

string WriteFixture(string relativePath, string content)
{
    string path = Path.Combine(fixtureRoot, relativePath);
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, content);
    return path;
}

static string FormatViolation(AssetScanViolation violation)
{
    return $"{violation.Path}:{violation.Line}:{violation.Token}";
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

static void AssertContains(IReadOnlyCollection<string> values, string expected, string message)
{
    if (!values.Contains(expected))
    {
        throw new InvalidOperationException($"{message}: expected collection to contain {expected}.");
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

internal static class AssetExclusionGuard
{
    public static readonly string[] ForbiddenTokens =
    {
        "using UnityEngine",
        "UnityEngine.",
        "Resources.",
        "ScriptableObject",
        "GameObject",
        "MonoBehaviour",
        "SerializeField",
        "Sprite",
        "Texture2D",
        "AudioClip",
        "AnimationClip",
        "Animator",
        "Addressables",
        "AssetDatabase",
    };

    public static AssetScanResult ScanFiles(string repositoryRoot, IEnumerable<string> relativePaths)
    {
        var inputs = relativePaths
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToDictionary(
                path => path,
                path => File.ReadAllText(Path.Combine(repositoryRoot, path)),
                StringComparer.Ordinal);

        return ScanText(inputs);
    }

    public static AssetScanResult ScanText(IReadOnlyDictionary<string, string> inputs)
    {
        var violations = new List<AssetScanViolation>();
        var fingerprintBuilder = new StringBuilder();

        foreach ((string path, string text) in inputs.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            fingerprintBuilder.Append(path).Append('\n');
            fingerprintBuilder.Append(text).Append('\n');

            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex];
                foreach (string token in ForbiddenTokens.OrderBy(token => token, StringComparer.Ordinal))
                {
                    if (line.Contains(token, StringComparison.Ordinal))
                    {
                        violations.Add(new AssetScanViolation(path, lineIndex + 1, token));
                    }
                }
            }
        }

        string fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintBuilder.ToString())));
        return new AssetScanResult(
            fingerprint,
            violations
                .OrderBy(violation => violation.Path, StringComparer.Ordinal)
                .ThenBy(violation => violation.Line)
                .ThenBy(violation => violation.Token, StringComparer.Ordinal)
                .ToArray());
    }
}

internal sealed record AssetScanResult(
    string Fingerprint,
    IReadOnlyList<AssetScanViolation> Violations);

internal sealed record AssetScanViolation(
    string Path,
    int Line,
    string Token);
