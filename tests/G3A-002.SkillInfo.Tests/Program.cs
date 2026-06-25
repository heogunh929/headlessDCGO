using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();
var playerOne = new HeadlessPlayerId(1);
var sourceEntity = new HeadlessEntityId("skill-source-card");
var targetEntity = new HeadlessEntityId("skill-target-card");

var tests = new (string Name, Func<Task> Body)[]
{
    ("G3A-002 goal row and predecessor are satisfied", GoalRowAndPredecessorAreSatisfied),
    ("AS-IS SkillInfo metadata references are recorded", AsIsSkillInfoReferencesAreRecorded),
    ("SkillInfo stores typed effect request metadata", SkillInfoStoresTypedMetadata),
    ("SkillInfo creates request from IHeadlessCardEffect definition", SkillInfoCreatesRequestFromEffect),
    ("Background effect defaults to background resolution mode", BackgroundEffectDefaultsToBackgroundMode),
    ("SkillInfo converts to pending effect and registry binding", SkillInfoConvertsToPendingEffectAndBinding),
    ("SkillInfo metadata snapshot is immutable and normalized", SkillInfoMetadataSnapshotIsImmutable),
    ("SkillInfo rejects mismatched definition request pairs", SkillInfoRejectsMismatchedPairs),
    ("SkillInfo rejects invalid metadata mode and sequence", SkillInfoRejectsInvalidMetadata),
    ("SkillInfo signatures are deterministic for identical input", SkillInfoSignaturesAreDeterministic),
    ("G3A-002 source files contain no placeholder or Unity dependency", SourceFilesContainNoPlaceholderOrUnityDependency),
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

Task GoalRowAndPredecessorAreSatisfied()
{
    var rows = ReadCsv(Path.Combine(root, "docs", "headless_complete_goal_breakdown.csv"));
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G3A-002")
        ?? throw new InvalidOperationException("G3A-002 row was not found.");

    AssertEqual("Phase 3", Value(row, "phase"), "phase");
    AssertEqual("EffectContract", Value(row, "area"), "area");
    AssertEqual("SkillInfo 포팅", Value(row, "goal"), "goal");
    AssertContains(Value(row, "scope"), "SkillInfo", "scope");
    AssertContains(Value(row, "scope"), "metadata", "scope");
    AssertEqual("SkillInfo model", Value(row, "deliverables"), "deliverables");
    AssertContains(Value(row, "unit_test_scope"), "skill metadata", "unit test scope");
    AssertEqual(
        "docs/test-results/goals/G3A-002_skill_info_unit_test_results.md",
        Value(row, "result_document"),
        "result document");
    AssertEqual("G3A-001", Value(row, "blocked_until"), "blocked_until");
    AssertContains(Value(row, "completion_gate"), "SkillInfo", "completion gate");

    AssertComplete("G3A-001_icard_effect_contract_unit_test_results.md");
    return Task.CompletedTask;
}

Task AsIsSkillInfoReferencesAreRecorded()
{
    string skillInfo = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "SkillInfo.cs"));
    string autoProcessing = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "AutoProcessing.cs"));
    string iCardEffect = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "ICardEffect.cs"));

    AssertContains(skillInfo, "ICardEffect", "AS-IS effect reference");
    AssertContains(skillInfo, "Hashtable", "AS-IS context payload");
    AssertContains(skillInfo, "EffectTiming", "AS-IS timing");
    AssertContains(autoProcessing, "new SkillInfo", "AS-IS SkillInfo creation");
    AssertContains(autoProcessing, "StackedSkillInfos", "AS-IS stack metadata");
    AssertContains(iCardEffect, "EffectName", "AS-IS effect name metadata");
    AssertContains(iCardEffect, "HashString", "AS-IS hash metadata");
    AssertContains(iCardEffect, "MaxCountPerTurn", "AS-IS max count metadata");
    return Task.CompletedTask;
}

Task SkillInfoStoresTypedMetadata()
{
    CardEffectDefinition definition = CreateDefinition("effect-1", "OnPlay", isOptional: true);
    EffectRequest request = CreateRequest(definition);
    var skill = new SkillInfo(
        definition,
        request,
        EffectResolutionMode.CutIn,
        priority: 7,
        sequence: 3,
        new Dictionary<string, object?>
        {
            [" origin "] = "trigger",
            ["mandatory"] = true,
        });

    AssertEqual(definition, skill.Definition, "definition");
    AssertEqual(request, skill.Request, "request");
    AssertEqual(new HeadlessEntityId("effect-1"), skill.EffectId, "effect id");
    AssertEqual(sourceEntity, skill.SourceEntityId, "source");
    AssertEqual(playerOne, skill.ControllerId, "controller");
    AssertEqual("OnPlay", skill.Timing, "timing");
    AssertTrue(skill.IsOptional, "optional");
    AssertFalse(skill.IsBackgroundProcess, "background");
    AssertEqual(1, skill.MaxCountPerTurn, "max count");
    AssertEqual("hash-effect-1", skill.Hash, "hash");
    AssertEqual(EffectResolutionMode.CutIn, skill.Mode, "mode");
    AssertEqual(7, skill.Priority, "priority");
    AssertEqual(3L, skill.Sequence, "sequence");
    AssertEqual("trigger", skill.Metadata["origin"], "trimmed metadata");
    AssertEqual(true, skill.Metadata["mandatory"], "metadata bool");
    AssertEqual(targetEntity, skill.Context.TargetEntityIds[0], "context");
    return Task.CompletedTask;
}

Task SkillInfoCreatesRequestFromEffect()
{
    var effect = new TestCardEffect(CreateDefinition("effect-from-card", "OnAttack"));
    EffectContext context = CreateContext(effect.Definition.SourceEntityId);

    SkillInfo skill = SkillInfo.FromEffect(
        effect,
        playerOne,
        context,
        priority: 2,
        sequence: 9,
        metadata: new Dictionary<string, object?> { ["window"] = "attack" });

    AssertEqual(effect.Definition, skill.Definition, "definition");
    AssertEqual(effect.Definition.EffectId, skill.Request.EffectId, "request effect");
    AssertEqual(effect.Definition.Timing, skill.Request.Timing, "request timing");
    AssertEqual(playerOne, skill.Request.ControllerId, "controller");
    AssertEqual(EffectResolutionMode.MainStack, skill.Mode, "default mode");
    AssertEqual(2, skill.Priority, "priority");
    AssertEqual(9L, skill.Sequence, "sequence");
    AssertEqual("attack", skill.Metadata["window"], "metadata");
    return Task.CompletedTask;
}

Task BackgroundEffectDefaultsToBackgroundMode()
{
    var effect = new TestCardEffect(CreateDefinition("background-effect", "RulesTiming", isBackground: true));
    SkillInfo skill = SkillInfo.FromEffect(effect, playerOne, CreateContext(effect.Definition.SourceEntityId));

    AssertTrue(skill.IsBackgroundProcess, "background flag");
    AssertEqual(EffectResolutionMode.Background, skill.Mode, "background mode");
    return Task.CompletedTask;
}

Task SkillInfoConvertsToPendingEffectAndBinding()
{
    CardEffectDefinition definition = CreateDefinition("effect-bind", "OnPlay");
    SkillInfo skill = new(
        definition,
        CreateRequest(definition),
        EffectResolutionMode.RuleProcess);

    PendingEffect pending = skill.ToPendingEffect();
    EffectBinding binding = skill.ToBinding(
        new[] { " OnPlay ", "Draw" },
        EffectQueryRole.Continuous,
        new[] { "field" });

    AssertEqual(skill.Request, pending.Request, "pending request");
    AssertEqual(EffectResolutionMode.RuleProcess, pending.Mode, "pending mode");
    AssertEqual(skill.Request, binding.Request, "binding request");
    AssertEqual(2, binding.Keywords.Count, "keywords");
    AssertEqual("OnPlay", binding.Keywords[0], "trimmed keyword");
    AssertTrue(binding.HasRole(EffectQueryRole.Continuous), "query role");
    AssertEqual("field", binding.QueryScopes[0], "query scope");
    return Task.CompletedTask;
}

Task SkillInfoMetadataSnapshotIsImmutable()
{
    CardEffectDefinition definition = CreateDefinition("effect-immutable", "OnPlay");
    var metadata = new Dictionary<string, object?>
    {
        [" value "] = 1,
    };
    var skill = new SkillInfo(definition, CreateRequest(definition), metadata: metadata);
    metadata["value"] = 99;
    metadata["extra"] = true;
    SkillInfo changed = skill.WithMetadata(new Dictionary<string, object?> { ["next"] = 2 });

    AssertEqual(1, skill.Metadata.Count, "original metadata count");
    AssertEqual(1, skill.Metadata["value"], "original value");
    AssertFalse(skill.Metadata.ContainsKey("extra"), "snapshot excludes mutation");
    AssertEqual(1, changed.Metadata.Count, "changed metadata count");
    AssertEqual(2, changed.Metadata["next"], "changed metadata");
    AssertEqual(skill.Request, changed.Request, "same request");
    return Task.CompletedTask;
}

Task SkillInfoRejectsMismatchedPairs()
{
    CardEffectDefinition definition = CreateDefinition("effect-a", "OnPlay");
    CardEffectDefinition otherEffect = CreateDefinition("effect-b", "OnPlay");
    CardEffectDefinition otherTiming = CreateDefinition("effect-a", "OnAttack");
    CardEffectDefinition otherSource = new(
        definition.EffectId,
        new HeadlessEntityId("other-source"),
        definition.Name,
        definition.Timing);

    ExpectThrows<ArgumentException>(() => new SkillInfo(definition, CreateRequest(otherEffect)));
    ExpectThrows<ArgumentException>(() => new SkillInfo(definition, CreateRequest(otherTiming)));
    ExpectThrows<ArgumentException>(() => new SkillInfo(definition, CreateRequest(otherSource)));
    return Task.CompletedTask;
}

Task SkillInfoRejectsInvalidMetadata()
{
    CardEffectDefinition definition = CreateDefinition("effect-invalid", "OnPlay");
    EffectRequest request = CreateRequest(definition);

    ExpectThrows<ArgumentNullException>(() => new SkillInfo(null!, request));
    ExpectThrows<ArgumentNullException>(() => new SkillInfo(definition, null!));
    ExpectThrows<ArgumentOutOfRangeException>(() => new SkillInfo(definition, request, (EffectResolutionMode)999));
    ExpectThrows<ArgumentOutOfRangeException>(() => new SkillInfo(definition, request, sequence: -1));
    ExpectThrows<ArgumentException>(() => new SkillInfo(definition, request, metadata: new Dictionary<string, object?> { [" "] = 1 }));
    ExpectThrows<ArgumentNullException>(() => SkillInfo.FromEffect(null!, playerOne, CreateContext(definition.SourceEntityId)));
    ExpectThrows<ArgumentNullException>(() => SkillInfo.FromEffect(new TestCardEffect(definition), playerOne, null!));
    return Task.CompletedTask;
}

Task SkillInfoSignaturesAreDeterministic()
{
    CardEffectDefinition definition = CreateDefinition("effect-deterministic", "OnPlay");
    var metadata = new Dictionary<string, object?>
    {
        ["b"] = 2,
        ["a"] = 1,
    };

    SkillInfo first = new(definition, CreateRequest(definition), priority: 5, sequence: 11, metadata: metadata);
    SkillInfo second = new(definition, CreateRequest(definition), priority: 5, sequence: 11, metadata: metadata);

    AssertEqual(Signature(first), Signature(second), "signature");
    return Task.CompletedTask;
}

Task SourceFilesContainNoPlaceholderOrUnityDependency()
{
    string skillInfoPath = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Effects", "SkillInfo.cs");
    string text = File.ReadAllText(skillInfoPath);

    AssertFalse(text.Contains("TODO", StringComparison.OrdinalIgnoreCase), "SkillInfo must not contain TODO");
    AssertFalse(text.Contains("UnityEngine", StringComparison.Ordinal), "SkillInfo must not reference UnityEngine");
    AssertFalse(text.Contains("MonoBehaviour", StringComparison.Ordinal), "SkillInfo must not reference MonoBehaviour");
    AssertFalse(text.Contains("Hashtable", StringComparison.Ordinal), "SkillInfo must not reference Hashtable");
    AssertContains(text, "CardEffectDefinition", "definition contract");
    AssertContains(text, "EffectRequest", "request contract");
    AssertContains(text, "EffectResolutionMode", "mode contract");
    AssertContains(text, "ToPendingEffect", "queue bridge");
    return Task.CompletedTask;
}

CardEffectDefinition CreateDefinition(
    string effectId,
    string timing,
    bool isOptional = false,
    bool isBackground = false)
{
    return new CardEffectDefinition(
        new HeadlessEntityId(effectId),
        sourceEntity,
        $"Effect {effectId}",
        timing,
        isOptional,
        isBackground,
        maxCountPerTurn: 1,
        hash: $"hash-{effectId}");
}

EffectRequest CreateRequest(CardEffectDefinition definition)
{
    return new EffectRequest(
        definition.EffectId,
        playerOne,
        definition.Timing,
        CreateContext(definition.SourceEntityId));
}

EffectContext CreateContext(HeadlessEntityId source)
{
    return new EffectContext(
        playerOne,
        playerOne,
        source,
        triggerEntityId: null,
        targetEntityIds: new[] { targetEntity },
        new Dictionary<string, object?>
        {
            ["amount"] = 1,
        });
}

string Signature(SkillInfo skill)
{
    string metadata = string.Join(
        ",",
        skill.Metadata.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}={pair.Value}"));

    return string.Join(
        "|",
        skill.EffectId.Value,
        skill.SourceEntityId.Value,
        skill.ControllerId.Value,
        skill.Timing,
        skill.Mode,
        skill.Priority,
        skill.Sequence,
        skill.IsOptional,
        skill.IsBackgroundProcess,
        skill.MaxCountPerTurn,
        skill.Hash,
        metadata);
}

void AssertComplete(string fileName)
{
    string path = Path.Combine(root, "docs", "test-results", "goals", fileName);
    if (!File.Exists(path))
    {
        throw new FileNotFoundException($"Predecessor result document was not found: {path}");
    }

    AssertContains(File.ReadAllText(path), "COMPLETE", fileName);
}

static List<Dictionary<string, string>> ReadCsv(string path)
{
    if (!File.Exists(path))
    {
        throw new FileNotFoundException($"CSV file was not found: {path}");
    }

    List<List<string>> records = ParseCsv(File.ReadAllText(path));
    if (records.Count == 0)
    {
        throw new InvalidOperationException("CSV file was empty.");
    }

    string[] headers = records[0].ToArray();
    var rows = new List<Dictionary<string, string>>();
    foreach (List<string> record in records.Skip(1))
    {
        var row = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 0; index < headers.Length; index++)
        {
            row[headers[index]] = index < record.Count ? record[index] : string.Empty;
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
    bool inQuotes = false;

    for (int index = 0; index < text.Length; index++)
    {
        char current = text[index];
        if (inQuotes)
        {
            if (current == '"')
            {
                if (index + 1 < text.Length && text[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = false;
                }
            }
            else
            {
                field.Append(current);
            }

            continue;
        }

        if (current == '"')
        {
            inQuotes = true;
        }
        else if (current == ',')
        {
            record.Add(field.ToString());
            field.Clear();
        }
        else if (current == '\r')
        {
            if (index + 1 < text.Length && text[index + 1] == '\n')
            {
                index++;
            }

            AddRecord();
        }
        else if (current == '\n')
        {
            AddRecord();
        }
        else
        {
            field.Append(current);
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
        if (record.Any(value => value.Length > 0))
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
        if (File.Exists(Path.Combine(current.FullName, "docs", "headless_complete_goal_breakdown.csv")))
        {
            return current.FullName;
        }

        current = current.Parent;
    }

    throw new InvalidOperationException("Repository root could not be found.");
}

static string Value(IReadOnlyDictionary<string, string> row, string key)
{
    return row.TryGetValue(key, out string? value)
        ? value
        : throw new KeyNotFoundException($"CSV column was not found: {key}");
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

static void AssertContains(string text, string expected, string label)
{
    if (!text.Contains(expected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"{label}: expected to contain '{expected}'.");
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

sealed class TestCardEffect : IHeadlessCardEffect
{
    public TestCardEffect(CardEffectDefinition definition)
    {
        Definition = definition;
    }

    public CardEffectDefinition Definition { get; }

    public CardEffectCanResolveResult CanResolve(CardEffectResolveContext context)
    {
        return CardEffectCanResolveResult.Success();
    }

    public ValueTask<EffectResult> ResolveAsync(
        CardEffectResolveContext context,
        IEffectMutationSink mutations,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(EffectResult.Success());
    }
}
