using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();
var playerOne = new HeadlessPlayerId(1);
var sourceEntity = new HeadlessEntityId("effect-source-card");
var targetEntity = new HeadlessEntityId("target-card");

var tests = new (string Name, Func<Task> Body)[]
{
    ("G3A-001 goal row and predecessor are satisfied", GoalRowAndPredecessorAreSatisfied),
    ("AS-IS ICardEffect and SkillInfo references are recorded", AsIsReferencesAreRecorded),
    ("CardEffectDefinition validates and normalizes metadata", CardEffectDefinitionValidatesAndNormalizesMetadata),
    ("EffectContext exposes typed value lookup contract", EffectContextExposesTypedValueLookup),
    ("CanResolve failure returns explicit failure and no mutation", CanResolveFailureReturnsFailureAndNoMutation),
    ("Resolve success mutates only through mutation sink", ResolveSuccessMutatesThroughSink),
    ("Missing required target prevents resolution", MissingRequiredTargetPreventsResolution),
    ("Malformed typed context returns validation failure", MalformedTypedContextReturnsValidationFailure),
    ("Same effect and context produce deterministic result and mutation", SameContextProducesDeterministicResult),
    ("G3A-001 source files contain no placeholder or Unity dependency", SourceFilesContainNoPlaceholderOrUnityDependency),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G3A-001")
        ?? throw new InvalidOperationException("G3A-001 row was not found.");

    AssertEqual("Phase 3", Value(row, "phase"), "phase");
    AssertEqual("EffectContract", Value(row, "area"), "area");
    AssertEqual("ICardEffect contract 포팅", Value(row, "goal"), "goal");
    AssertContains(Value(row, "scope"), "typed card effect", "scope");
    AssertEqual("ICardEffect equivalent", Value(row, "deliverables"), "deliverables");
    AssertContains(Value(row, "unit_test_scope"), "resolve contract", "unit test scope");
    AssertEqual(
        "docs/test-results/goals/G3A-001_icard_effect_contract_unit_test_results.md",
        Value(row, "result_document"),
        "result document");
    AssertEqual("G2Z-001", Value(row, "blocked_until"), "blocked_until");
    AssertContains(Value(row, "completion_gate"), "effect contract", "completion gate");

    AssertComplete("G2Z-001_phase2_aggregate_unit_test_results.md");
    return Task.CompletedTask;
}

Task AsIsReferencesAreRecorded()
{
    string iCardEffect = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "ICardEffect.cs"));
    string skillInfo = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "SkillInfo.cs"));
    string autoProcessing = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "AutoProcessing.cs"));

    AssertContains(iCardEffect, "CanTrigger", "AS-IS CanTrigger");
    AssertContains(iCardEffect, "CanActivate", "AS-IS CanActivate");
    AssertContains(iCardEffect, "ActivateICardEffect", "AS-IS activate interface");
    AssertContains(iCardEffect, "EffectTiming", "AS-IS timing");
    AssertContains(skillInfo, "CardEffect", "AS-IS SkillInfo effect");
    AssertContains(skillInfo, "Hashtable", "AS-IS SkillInfo hashtable");
    AssertContains(autoProcessing, "ActivateEffectProcess", "AS-IS activation process");
    AssertContains(autoProcessing, "StackSkillInfos", "AS-IS stack process");
    return Task.CompletedTask;
}

Task CardEffectDefinitionValidatesAndNormalizesMetadata()
{
    var definition = new CardEffectDefinition(
        new HeadlessEntityId(" effect-1 "),
        new HeadlessEntityId(" source-a "),
        " Add Memory ",
        " OnPlay ",
        isOptional: true,
        isBackgroundProcess: false,
        maxCountPerTurn: 1,
        hash: " hash-a ");

    AssertEqual("effect-1", definition.EffectId.Value, "effect id");
    AssertEqual("source-a", definition.SourceEntityId.Value, "source entity id");
    AssertEqual("Add Memory", definition.Name, "name");
    AssertEqual("OnPlay", definition.Timing, "timing");
    AssertTrue(definition.IsOptional, "optional");
    AssertEqual(1, definition.MaxCountPerTurn, "max count");
    AssertEqual("hash-a", definition.Hash, "hash");

    ExpectThrows<ArgumentException>(() => new CardEffectDefinition(default, sourceEntity, "Name", "OnPlay"));
    ExpectThrows<ArgumentException>(() => new CardEffectDefinition(new HeadlessEntityId("effect"), default, "Name", "OnPlay"));
    ExpectThrows<ArgumentException>(() => new CardEffectDefinition(new HeadlessEntityId("effect"), sourceEntity, " ", "OnPlay"));
    ExpectThrows<ArgumentException>(() => new CardEffectDefinition(new HeadlessEntityId("effect"), sourceEntity, "Name", " "));
    ExpectThrows<ArgumentOutOfRangeException>(() => new CardEffectDefinition(new HeadlessEntityId("effect"), sourceEntity, "Name", "OnPlay", maxCountPerTurn: 0));
    return Task.CompletedTask;
}

Task EffectContextExposesTypedValueLookup()
{
    var context = new EffectContext(
        playerOne,
        playerOne,
        sourceEntity,
        triggerEntityId: null,
        targetEntityIds: new[] { targetEntity },
        new Dictionary<string, object?>
        {
            [" amount "] = 3,
            ["label"] = "draw",
        });

    AssertTrue(context.HasValue("amount"), "has amount");
    AssertTrue(context.TryGetValue<int>(" amount ", out int amount), "try int amount");
    AssertEqual(3, amount, "amount");
    AssertFalse(context.TryGetValue<string>("amount", out _), "wrong type rejected");
    AssertEqual("draw", context.GetRequiredValue<string>("label"), "label");
    ExpectThrows<KeyNotFoundException>(() => context.GetRequiredValue<int>("missing"));
    ExpectThrows<InvalidOperationException>(() => context.GetRequiredValue<long>("amount"));
    return Task.CompletedTask;
}

async Task CanResolveFailureReturnsFailureAndNoMutation()
{
    var effect = new AmountEffect(requiredTarget: null);
    var resolver = new HeadlessCardEffectResolver();
    var sink = new RecordingEffectMutationSink();
    EffectRequest request = CreateRequest(new Dictionary<string, object?> { ["amount"] = 0 });

    EffectResult result = await resolver.ResolveAsync(effect, request, sink);

    AssertFalse(result.Resolved, "result failed");
    AssertContains(result.Message ?? string.Empty, "amount", "failure message");
    AssertEqual(0, sink.Count, "no mutation");
    AssertEqual("amount", result.Values["field"], "failure field");
}

async Task ResolveSuccessMutatesThroughSink()
{
    var effect = new AmountEffect(requiredTarget: null);
    var resolver = new HeadlessCardEffectResolver();
    var sink = new RecordingEffectMutationSink();
    EffectRequest request = CreateRequest(new Dictionary<string, object?> { ["amount"] = 3 });

    EffectResult result = await resolver.ResolveAsync(effect, request, sink);
    IReadOnlyList<EffectMutation> mutations = sink.Snapshot();

    AssertTrue(result.Resolved, "result resolved");
    AssertEqual(1, mutations.Count, "mutation count");
    AssertEqual("AddMemory", mutations[0].Kind, "mutation kind");
    AssertEqual(sourceEntity, mutations[0].SourceEntityId, "mutation source");
    AssertEqual(3, mutations[0].Values["amount"], "mutation amount");
    AssertEqual(playerOne.Value, mutations[0].Values["playerId"], "mutation player");
    AssertEqual(3, result.Values["amount"], "result amount");
}

async Task MissingRequiredTargetPreventsResolution()
{
    var effect = new AmountEffect(targetEntity);
    var resolver = new HeadlessCardEffectResolver();
    var sink = new RecordingEffectMutationSink();
    EffectRequest request = CreateRequest(
        new Dictionary<string, object?> { ["amount"] = 2 },
        Array.Empty<HeadlessEntityId>());

    EffectResult result = await resolver.ResolveAsync(effect, request, sink);

    AssertFalse(result.Resolved, "result failed");
    AssertContains(result.Message ?? string.Empty, "target", "target failure");
    AssertEqual(0, sink.Count, "no mutation");
}

async Task MalformedTypedContextReturnsValidationFailure()
{
    var effect = new AmountEffect(requiredTarget: null);
    var resolver = new HeadlessCardEffectResolver();
    var sink = new RecordingEffectMutationSink();
    EffectRequest request = CreateRequest(new Dictionary<string, object?> { ["amount"] = "three" });

    EffectResult result = await resolver.ResolveAsync(effect, request, sink);

    AssertFalse(result.Resolved, "result failed");
    AssertContains(result.Message ?? string.Empty, "amount", "amount failure");
    AssertEqual("amount", result.Values["field"], "failure field");
    AssertEqual(0, sink.Count, "no mutation");
}

async Task SameContextProducesDeterministicResult()
{
    var effect = new AmountEffect(requiredTarget: null);
    var resolver = new HeadlessCardEffectResolver();
    EffectRequest request = CreateRequest(new Dictionary<string, object?> { ["amount"] = 5 });
    var firstSink = new RecordingEffectMutationSink();
    var secondSink = new RecordingEffectMutationSink();

    EffectResult firstResult = await resolver.ResolveAsync(effect, request, firstSink);
    EffectResult secondResult = await resolver.ResolveAsync(effect, request, secondSink);

    AssertEqual(Signature(firstResult, firstSink), Signature(secondResult, secondSink), "deterministic signature");
}

Task SourceFilesContainNoPlaceholderOrUnityDependency()
{
    string contractPath = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Effects", "HeadlessCardEffectContract.cs");
    string contextPath = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Effects", "EffectContext.cs");
    string contract = File.ReadAllText(contractPath);
    string context = File.ReadAllText(contextPath);

    AssertFalse(contract.Contains("TODO", StringComparison.OrdinalIgnoreCase), "contract must not contain TODO");
    AssertFalse(contract.Contains("UnityEngine", StringComparison.Ordinal), "contract must not reference UnityEngine");
    AssertFalse(contract.Contains("MonoBehaviour", StringComparison.Ordinal), "contract must not reference MonoBehaviour");
    AssertContains(contract, "IHeadlessCardEffect", "effect interface");
    AssertContains(contract, "CanResolve", "can resolve contract");
    AssertContains(contract, "ResolveAsync", "resolve contract");
    AssertContains(contract, "IEffectMutationSink", "mutation boundary");
    AssertContains(context, "GetRequiredValue", "typed context required value");
    return Task.CompletedTask;
}

EffectRequest CreateRequest(
    IReadOnlyDictionary<string, object?> values,
    IReadOnlyList<HeadlessEntityId>? targets = null)
{
    return new EffectRequest(
        new HeadlessEntityId("effect-1"),
        playerOne,
        "OnPlay",
        new EffectContext(
            playerOne,
            playerOne,
            sourceEntity,
            triggerEntityId: null,
            targetEntityIds: targets ?? new[] { targetEntity },
            values));
}

string Signature(EffectResult result, RecordingEffectMutationSink sink)
{
    string resultValues = string.Join(
        ",",
        result.Values.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}={pair.Value}"));
    string mutations = string.Join(
        ";",
        sink.Snapshot().Select(mutation =>
            $"{mutation.Kind}:{mutation.SourceEntityId.Value}:"
            + string.Join(
                ",",
                mutation.Values.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => $"{pair.Key}={pair.Value}"))));

    return $"{result.Resolved}|{result.Message}|{resultValues}|{mutations}";
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

sealed class AmountEffect : IHeadlessCardEffect
{
    private readonly HeadlessEntityId? _requiredTarget;

    public AmountEffect(HeadlessEntityId? requiredTarget)
    {
        _requiredTarget = requiredTarget;
        Definition = new CardEffectDefinition(
            new HeadlessEntityId("effect-1"),
            new HeadlessEntityId("effect-source-card"),
            "Add Memory",
            "OnPlay",
            isOptional: false,
            isBackgroundProcess: false,
            maxCountPerTurn: 1,
            hash: "add-memory");
    }

    public CardEffectDefinition Definition { get; }

    public CardEffectCanResolveResult CanResolve(CardEffectResolveContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (_requiredTarget is HeadlessEntityId requiredTarget
            && !context.EffectContext.TargetEntityIds.Contains(requiredTarget))
        {
            return CardEffectCanResolveResult.Failure(
                "Required target is missing.",
                new Dictionary<string, object?>
                {
                    ["field"] = "target",
                    ["targetEntityId"] = requiredTarget.Value,
                });
        }

        if (!context.EffectContext.TryGetValue<int>("amount", out int amount) || amount <= 0)
        {
            return CardEffectCanResolveResult.Failure(
                "Effect amount must be a positive integer.",
                new Dictionary<string, object?>
                {
                    ["field"] = "amount",
                });
        }

        return CardEffectCanResolveResult.Success();
    }

    public ValueTask<EffectResult> ResolveAsync(
        CardEffectResolveContext context,
        IEffectMutationSink mutations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(mutations);
        cancellationToken.ThrowIfCancellationRequested();

        int amount = context.EffectContext.GetRequiredValue<int>("amount");
        mutations.Apply(new EffectMutation(
            "AddMemory",
            Definition.SourceEntityId,
            new Dictionary<string, object?>
            {
                ["amount"] = amount,
                ["playerId"] = context.Request.ControllerId.Value,
                ["targetCount"] = context.EffectContext.TargetEntityIds.Count,
            }));

        return ValueTask.FromResult(EffectResult.Success(
            "Resolved add memory effect.",
            new Dictionary<string, object?>
            {
                ["amount"] = amount,
                ["effectId"] = Definition.EffectId.Value,
                ["sourceEntityId"] = Definition.SourceEntityId.Value,
            }));
    }
}
