using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("GrantBlocker sets hasBlocker on the target instance", GrantBlockerSetsHasBlocker),
    ("GrantRush sets hasRush on the target instance", GrantRushSetsHasRush),
    ("Mutation without target falls back to the source instance", FallsBackToSourceInstance),
    ("Unknown mutation kind is recorded as unsupported", UnknownKindIsUnsupported),
    ("Mutation for a missing instance is recorded as skipped", MissingInstanceIsSkipped),
    // (campaign ④) "Default engine resolves Blocker keyword into real hasBlocker state" RETIRED — it drove the
    // sink through the deleted EffectRegistry + KeywordBaseBatch1Effect.ToBinding + scheduler bound-body resolve
    // path (all removed). The real sink-mutation behavior it shared is covered by the direct sink.Apply subtests
    // above (GrantBlocker/GrantRush/fallback/unsupported/skipped).
    ("Sink source has no placeholder or Unity dependency", SinkSourceHasNoPlaceholderOrUnityDependency),
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

Task GrantBlockerSetsHasBlocker()
{
    var repository = new InMemoryCardInstanceRepository();
    repository.Upsert(new CardInstanceRecord(new HeadlessEntityId("c1"), new HeadlessEntityId("def-1"), new HeadlessPlayerId(1)));
    var sink = new MatchStateMutationSink(repository);

    sink.Apply(new EffectMutation(
        "GrantBlocker",
        new HeadlessEntityId("src-1"),
        new Dictionary<string, object?> { ["targetEntityId"] = "c1" }));

    AssertEqual(1, sink.AppliedCount, "applied count");
    AssertEqual(0, sink.UnsupportedCount, "unsupported count");
    AssertTrue(ReadFlag(repository, "c1", "hasBlocker"), "hasBlocker flag");
    return Task.CompletedTask;
}

Task GrantRushSetsHasRush()
{
    var repository = new InMemoryCardInstanceRepository();
    repository.Upsert(new CardInstanceRecord(new HeadlessEntityId("c2"), new HeadlessEntityId("def-2"), new HeadlessPlayerId(1)));
    var sink = new MatchStateMutationSink(repository);

    sink.Apply(new EffectMutation(
        "GrantRush",
        new HeadlessEntityId("src-2"),
        new Dictionary<string, object?> { ["targetEntityId"] = "c2" }));

    AssertEqual(1, sink.AppliedCount, "applied count");
    AssertTrue(ReadFlag(repository, "c2", "hasRush"), "hasRush flag");
    return Task.CompletedTask;
}

Task FallsBackToSourceInstance()
{
    var repository = new InMemoryCardInstanceRepository();
    repository.Upsert(new CardInstanceRecord(new HeadlessEntityId("src-3"), new HeadlessEntityId("def-3"), new HeadlessPlayerId(1)));
    var sink = new MatchStateMutationSink(repository);

    sink.Apply(new EffectMutation("GrantBlocker", new HeadlessEntityId("src-3")));

    AssertEqual(1, sink.AppliedCount, "applied count");
    AssertTrue(ReadFlag(repository, "src-3", "hasBlocker"), "hasBlocker on source");
    AssertEqual("src-3", sink.Applied[0].TargetId.Value, "target id");
    return Task.CompletedTask;
}

Task UnknownKindIsUnsupported()
{
    var repository = new InMemoryCardInstanceRepository();
    repository.Upsert(new CardInstanceRecord(new HeadlessEntityId("c4"), new HeadlessEntityId("def-4"), new HeadlessPlayerId(1)));
    var sink = new MatchStateMutationSink(repository);

    sink.Apply(new EffectMutation(
        "Frobnicate",
        new HeadlessEntityId("src-4"),
        new Dictionary<string, object?> { ["targetEntityId"] = "c4" }));

    AssertEqual(0, sink.AppliedCount, "applied count");
    AssertEqual(1, sink.UnsupportedCount, "unsupported count");
    return Task.CompletedTask;
}

Task MissingInstanceIsSkipped()
{
    var repository = new InMemoryCardInstanceRepository();
    var sink = new MatchStateMutationSink(repository);

    sink.Apply(new EffectMutation(
        "GrantBlocker",
        new HeadlessEntityId("src-5"),
        new Dictionary<string, object?> { ["targetEntityId"] = "ghost" }));

    AssertEqual(0, sink.AppliedCount, "applied count");
    AssertEqual(1, sink.SkippedCount, "skipped count");
    return Task.CompletedTask;
}

Task SinkSourceHasNoPlaceholderOrUnityDependency()
{
    string path = Path.Combine(
        root,
        "src",
        "HeadlessDCGO.Engine",
        "Headless",
        "Effects",
        "MatchStateMutationSink.cs");

    if (!File.Exists(path))
    {
        throw new FileNotFoundException($"Sink source was not found: {path}");
    }

    string text = File.ReadAllText(path);
    AssertTrue(!text.Contains("TODO", StringComparison.Ordinal), "no TODO marker");
    AssertTrue(!text.Contains("NotImplementedException", StringComparison.Ordinal), "no NotImplementedException");
    AssertTrue(!text.Contains("UnityEngine", StringComparison.Ordinal), "no Unity dependency");
    return Task.CompletedTask;
}

static bool ReadFlag(ICardInstanceRepository repository, string instanceId, string key)
{
    if (!repository.TryGetInstance(new HeadlessEntityId(instanceId), out CardInstanceRecord? record) || record is null)
    {
        throw new InvalidOperationException($"Instance '{instanceId}' was not found.");
    }

    return record.Metadata.TryGetValue(key, out object? value) && value is true;
}

static void AssertTrue(bool condition, string label)
{
    if (!condition)
    {
        throw new InvalidOperationException($"Assertion failed: {label}");
    }
}

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Assertion failed: {label}. Expected '{expected}', got '{actual}'.");
    }
}

static string FindRepositoryRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null)
    {
        if (Directory.Exists(Path.Combine(directory.FullName, "src"))
            && Directory.Exists(Path.Combine(directory.FullName, "docs")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    throw new InvalidOperationException("Repository root with 'src' and 'docs' was not found.");
}
