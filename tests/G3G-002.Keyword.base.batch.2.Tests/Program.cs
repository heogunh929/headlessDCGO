using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;
using FactoryArmorPurge = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory.KeyWordEffects.ArmorPurge;
using FactoryBlitz = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory.KeyWordEffects.Blitz;
using FactoryRetaliation = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory.KeyWordEffects.Retaliation;
using FactoryRush = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory.KeyWordEffects.Rush;

var root = FindRepositoryRoot();
HeadlessPlayerId PlayerOne = new(1);
HeadlessPlayerId PlayerTwo = new(2);
HeadlessEntityId KeywordSource = new("p1-keyword-source");
HeadlessEntityId RetaliationSource = new("p1-retaliation-source");
HeadlessEntityId OpponentBattleCard = new("p2-battle-card");
HeadlessEntityId HandCard = new("p1-hand-card");
HeadlessEntityId SourceBottom = new("p1-source-bottom");
HeadlessEntityId SourceTop = new("p1-source-top");

var tests = new (string Name, Func<Task> Body)[]
{
    ("G3G-002 goal row and predecessor are satisfied", GoalRowAndPredecessorAreSatisfied),
    ("AS-IS keyword batch 2 references are recorded", AsIsKeywordBatch2ReferencesAreRecorded),
    ("Factory creates rush blitz retaliation armor purge effects", FactoryCreatesFourKeywordEffects),
    ("Keyword batch 2 effects register deterministic keyword bindings", KeywordEffectsRegisterDeterministicBindings),
    ("Rush resolves by granting immediate attack mutation", RushResolvesByGrantingMutation),
    ("Blitz resolves only when trigger and attack conditions match", BlitzResolvesOnlyWhenConditionsMatch),
    ("Retaliation resolves from battle-deleted keyword card in trash", RetaliationResolvesFromDeletedKeywordCard),
    ("Armor Purge resolves only with digivolution source", ArmorPurgeResolvesWithSource),
    ("Invalid keyword target fails without mutation", InvalidKeywordTargetFailsWithoutMutation),
    ("G3G-002 source files stay inside keyword base batch 2 scope", SourceFilesStayInsideGoalScope),
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
    List<Dictionary<string, string>> rows = ReadCsv(Path.Combine(root, "docs", "headless_complete_goal_breakdown.csv"));
    Dictionary<string, string> row = rows.SingleOrDefault(row => Value(row, "goal_id") == "G3G-002")
        ?? throw new InvalidOperationException("G3G-002 row was not found.");

    AssertEqual("Phase 3", Value(row, "phase"), "phase");
    AssertEqual("Keywords", Value(row, "area"), "area");
    AssertContains(Value(row, "scope"), "rush blitz retaliation armor purge base", "scope");
    AssertEqual("keyword base batch2", Value(row, "deliverables"), "deliverables");
    AssertContains(Value(row, "unit_test_scope"), "keyword base batch2", "unit_test_scope");
    AssertEqual("docs/test-results/goals/G3G-002_keyword_base_batch2_unit_test_results.md", Value(row, "result_document"), "result_document");
    AssertEqual("G3G-001", Value(row, "blocked_until"), "prerequisite");
    AssertContains(Value(row, "completion_gate"), "keyword base2", "completion gate");
    AssertComplete("G3G-001_keyword_base_batch1_unit_test_results.md");
    return Task.CompletedTask;
}

Task AsIsKeywordBatch2ReferencesAreRecorded()
{
    string factoryRush = ReadAsIsFactory("Rush.cs");
    string factoryBlitz = ReadAsIsFactory("Blitz.cs");
    string factoryRetaliation = ReadAsIsFactory("Retaliation.cs");
    string factoryArmor = ReadAsIsFactory("ArmorPurge.cs");
    string commonsBlitz = ReadAsIsCommons("Blitz.cs");
    string commonsRetaliation = ReadAsIsCommons("Retaliation.cs");
    string commonsArmor = ReadAsIsCommons("ArmorPurge.cs");

    AssertContains(factoryRush, "RushStaticEffect", "AS-IS rush factory");
    AssertContains(factoryRush, "SetUpRushClass", "AS-IS rush class setup");
    AssertContains(factoryBlitz, "BlitzEffect", "AS-IS blitz factory");
    AssertContains(commonsBlitz, "CanActivateBlitz", "AS-IS blitz activation");
    AssertContains(commonsBlitz, "MemoryForPlayer >= 1", "AS-IS blitz memory condition");
    AssertContains(factoryRetaliation, "RetaliationEffect", "AS-IS retaliation factory");
    AssertContains(commonsRetaliation, "CanActivateRetaliation", "AS-IS retaliation activation");
    AssertContains(commonsRetaliation, "IsExistOnTrash", "AS-IS retaliation deleted card zone");
    AssertContains(factoryArmor, "ArmorPurgeEffect", "AS-IS armor purge factory");
    AssertContains(commonsArmor, "CanActivateArmorPurge", "AS-IS armor purge activation");
    AssertContains(commonsArmor, "DigivolutionCards.Count >= 1", "AS-IS armor purge source requirement");
    return Task.CompletedTask;
}

Task FactoryCreatesFourKeywordEffects()
{
    KeywordBaseBatch2Effect rush = FactoryRush.Create(KeywordSource);
    KeywordBaseBatch2Effect blitz = FactoryBlitz.Create(KeywordSource, triggerReason: "WhenDigivolving");
    KeywordBaseBatch2Effect retaliation = FactoryRetaliation.Create(KeywordSource, isInherited: true, isLinked: true);
    KeywordBaseBatch2Effect armor = FactoryArmorPurge.Create(KeywordSource);

    AssertEqual(KeywordBaseBatch2Kind.Rush, rush.Kind, "rush kind");
    AssertEqual("Rush", rush.Definition.Name, "rush name");
    AssertEqual(KeywordBaseBatch2Timings.ImmediateAttackPermission, rush.Definition.Timing, "rush timing");
    AssertEqual(KeywordBaseBatch2Kind.Blitz, blitz.Kind, "blitz kind");
    AssertEqual("WhenDigivolving", blitz.TriggerReason, "blitz trigger reason");
    AssertTrue(blitz.Definition.IsOptional, "blitz optional");
    AssertEqual(KeywordBaseBatch2Kind.Retaliation, retaliation.Kind, "retaliation kind");
    AssertTrue(retaliation.IsInherited, "retaliation inherited");
    AssertTrue(retaliation.IsLinked, "retaliation linked");
    AssertEqual(KeywordBaseBatch2Kind.ArmorPurge, armor.Kind, "armor kind");
    AssertEqual("Armor Purge", armor.Keyword, "armor keyword");
    AssertTrue(armor.Definition.IsOptional, "armor optional");
    return Task.CompletedTask;
}

Task KeywordEffectsRegisterDeterministicBindings()
{
    InMemoryEffectRegistry registry = new();
    EffectContext context = CreateContext(KeywordSource, new Dictionary<string, object?> { [KeywordBaseBatch2ContextKeys.MatchState] = CreateState() });

    IReadOnlyList<EffectBinding> bindings = KeywordBaseBatch2Factory.RegisterBaseBatch2(
        registry,
        KeywordSource,
        PlayerOne,
        context);

    AssertEqual(4, bindings.Count, "binding count");
    AssertEqual("Rush,Blitz,Retaliation,Armor Purge", string.Join(",", bindings.Select(binding => binding.Keywords[0])), "registered order");
    AssertEqual(1, registry.GetKeywordEffects("Rush").Count, "rush registry");
    AssertEqual(1, registry.GetKeywordEffects("Blitz").Count, "blitz registry");
    AssertEqual(1, registry.GetKeywordEffects("Retaliation").Count, "retaliation registry");
    AssertEqual(1, registry.GetKeywordEffects("Armor Purge").Count, "armor registry");
    AssertEqual(1, registry.GetKeywordEffects("ArmorPurge").Count, "AS-IS armor alias");
    AssertEqual(1, registry.GetContinuousEffects(new EffectQueryContext(KeywordBaseBatch2Scopes.BlitzAttack, sourceEntityId: KeywordSource)).Count, "blitz continuous query");
    AssertEqual(1, registry.GetReplacementEffects(new EffectQueryContext(KeywordBaseBatch2Scopes.ArmorPurgeReplacement, sourceEntityId: KeywordSource)).Count, "armor replacement query");
    return Task.CompletedTask;
}

async Task RushResolvesByGrantingMutation()
{
    KeywordBaseBatch2Effect effect = KeywordBaseBatch2Factory.Create(KeywordBaseBatch2Kind.Rush, KeywordSource);
    EffectRequest request = CreateRequest(effect, CreateContext(KeywordSource, new Dictionary<string, object?> { [KeywordBaseBatch2ContextKeys.MatchState] = CreateState() }));
    RecordingEffectMutationSink sink = new();

    EffectResult result = await effect.ResolveAsync(new CardEffectResolveContext(request), sink);

    AssertTrue(result.Resolved, "rush resolved");
    AssertEqual(1, sink.Count, "mutation count");
    EffectMutation mutation = sink.Snapshot().Single();
    AssertEqual("GrantRush", mutation.Kind, "mutation kind");
    AssertEqual("Rush", mutation.Values["keyword"], "keyword value");
    AssertEqual(KeywordSource.Value, mutation.Values["targetEntityId"], "target id");
}

async Task BlitzResolvesOnlyWhenConditionsMatch()
{
    KeywordBaseBatch2Effect effect = KeywordBaseBatch2Factory.Create(KeywordBaseBatch2Kind.Blitz, KeywordSource, triggerReason: "OnPlay");
    EffectContext validContext = CreateContext(KeywordSource, new Dictionary<string, object?>
    {
        [KeywordBaseBatch2ContextKeys.MatchState] = CreateState(),
        [KeywordBaseBatch2ContextKeys.TriggerReason] = "OnPlay",
        [KeywordBaseBatch2ContextKeys.CanAttack] = true,
        [KeywordBaseBatch2ContextKeys.OpponentMemory] = 1,
        [KeywordBaseBatch2ContextKeys.IsAttacking] = false,
    });
    EffectContext invalidContext = CreateContext(KeywordSource, new Dictionary<string, object?>
    {
        [KeywordBaseBatch2ContextKeys.MatchState] = CreateState(),
        [KeywordBaseBatch2ContextKeys.TriggerReason] = "OnPlay",
        [KeywordBaseBatch2ContextKeys.CanAttack] = true,
        [KeywordBaseBatch2ContextKeys.OpponentMemory] = 0,
        [KeywordBaseBatch2ContextKeys.IsAttacking] = false,
    });

    RecordingEffectMutationSink firstSink = new();
    RecordingEffectMutationSink secondSink = new();
    RecordingEffectMutationSink invalidSink = new();
    EffectResult firstValid = await effect.ResolveAsync(new CardEffectResolveContext(CreateRequest(effect, validContext)), firstSink);
    EffectResult secondValid = await effect.ResolveAsync(new CardEffectResolveContext(CreateRequest(effect, validContext)), secondSink);
    EffectResult invalid = await effect.ResolveAsync(new CardEffectResolveContext(CreateRequest(effect, invalidContext)), invalidSink);

    AssertTrue(firstValid.Resolved, "valid blitz resolved");
    AssertTrue(secondValid.Resolved, "second blitz resolved");
    AssertEqual("RequestBlitzAttack", firstValid.Values["mutationKind"], "valid mutation kind");
    AssertEqual(Signature(firstValid, firstSink), Signature(secondValid, secondSink), "deterministic blitz");
    AssertFalse(invalid.Resolved, "invalid blitz failed");
    AssertEqual(0, invalidSink.Count, "invalid no mutation");
    AssertEqual("opponentMemory", invalid.Values["field"], "invalid field");
}

async Task RetaliationResolvesFromDeletedKeywordCard()
{
    KeywordBaseBatch2Effect effect = KeywordBaseBatch2Factory.Create(KeywordBaseBatch2Kind.Retaliation, RetaliationSource);
    EffectContext validContext = CreateContext(RetaliationSource, new Dictionary<string, object?>
    {
        [KeywordBaseBatch2ContextKeys.MatchState] = CreateState(),
        [KeywordBaseBatch2ContextKeys.DeletedByBattle] = true,
        [KeywordBaseBatch2ContextKeys.DeletedCardId] = RetaliationSource,
        [KeywordBaseBatch2ContextKeys.OpponentBattleCardId] = OpponentBattleCard,
    });
    EffectContext invalidContext = CreateContext(RetaliationSource, new Dictionary<string, object?>
    {
        [KeywordBaseBatch2ContextKeys.MatchState] = CreateState(),
        [KeywordBaseBatch2ContextKeys.DeletedByBattle] = false,
        [KeywordBaseBatch2ContextKeys.DeletedCardId] = RetaliationSource,
        [KeywordBaseBatch2ContextKeys.OpponentBattleCardId] = OpponentBattleCard,
    });

    RecordingEffectMutationSink sink = new();
    EffectResult valid = await effect.ResolveAsync(new CardEffectResolveContext(CreateRequest(effect, validContext)), sink);
    sink.Clear();
    EffectResult invalid = await effect.ResolveAsync(new CardEffectResolveContext(CreateRequest(effect, invalidContext)), sink);

    AssertTrue(valid.Resolved, "valid retaliation resolved");
    AssertEqual("DeleteRetaliationTarget", valid.Values["mutationKind"], "valid mutation kind");
    AssertFalse(invalid.Resolved, "invalid retaliation failed");
    AssertEqual(0, sink.Count, "invalid no mutation");
    AssertEqual("deletedByBattle", invalid.Values["field"], "invalid field");
}

async Task ArmorPurgeResolvesWithSource()
{
    KeywordBaseBatch2Effect effect = KeywordBaseBatch2Factory.Create(KeywordBaseBatch2Kind.ArmorPurge, KeywordSource);
    EffectContext validContext = CreateContext(KeywordSource, new Dictionary<string, object?>
    {
        [KeywordBaseBatch2ContextKeys.MatchState] = CreateState(),
        [KeywordBaseBatch2ContextKeys.RemovedFromField] = true,
        [KeywordBaseBatch2ContextKeys.RemovedCardId] = KeywordSource,
    });
    MatchState noSourceState = CreateState(withArmorSources: false);
    EffectContext invalidContext = CreateContext(KeywordSource, new Dictionary<string, object?>
    {
        [KeywordBaseBatch2ContextKeys.MatchState] = noSourceState,
        [KeywordBaseBatch2ContextKeys.RemovedFromField] = true,
        [KeywordBaseBatch2ContextKeys.RemovedCardId] = KeywordSource,
    });

    RecordingEffectMutationSink sink = new();
    EffectResult valid = await effect.ResolveAsync(new CardEffectResolveContext(CreateRequest(effect, validContext)), sink);
    sink.Clear();
    EffectResult invalid = await effect.ResolveAsync(new CardEffectResolveContext(CreateRequest(effect, invalidContext)), sink);

    AssertTrue(valid.Resolved, "valid armor purge resolved");
    AssertEqual("ApplyArmorPurge", valid.Values["mutationKind"], "valid mutation kind");
    AssertEqual(SourceTop.Value, valid.Values["purgedSourceId"], "top source purged");
    AssertFalse(invalid.Resolved, "invalid armor purge failed");
    AssertEqual(0, sink.Count, "invalid no mutation");
    AssertEqual("sourceIds", invalid.Values["field"], "invalid field");
}

async Task InvalidKeywordTargetFailsWithoutMutation()
{
    KeywordBaseBatch2Effect rush = KeywordBaseBatch2Factory.Create(KeywordBaseBatch2Kind.Rush, KeywordSource, HandCard);
    EffectRequest request = CreateRequest(rush, CreateContext(KeywordSource, new Dictionary<string, object?> { [KeywordBaseBatch2ContextKeys.MatchState] = CreateState() }));
    RecordingEffectMutationSink sink = new();

    EffectResult result = await rush.ResolveAsync(new CardEffectResolveContext(request), sink);

    AssertFalse(result.Resolved, "hand target fails");
    AssertEqual(0, sink.Count, "no mutation");
    AssertEqual("targetEntityId", result.Values["field"], "failure field");
}

Task SourceFilesStayInsideGoalScope()
{
    // Relocated to the AS-IS mirror structure (CardEffectCommons/KeyWordEffects) for 1:1 parity with the original.
    string helper = File.ReadAllText(Path.Combine(root, "src", "HeadlessDCGO.Engine", "Assets", "Scripts", "Script", "CardEffectCommons", "KeyWordEffects", "KeywordBaseBatch2.cs"));
    string rush = File.ReadAllText(Path.Combine(root, "src", "HeadlessDCGO.Engine", "Assets", "Scripts", "Script", "CardEffectFactory", "KeyWordEffects", "Rush.cs"));
    string blitz = File.ReadAllText(Path.Combine(root, "src", "HeadlessDCGO.Engine", "Assets", "Scripts", "Script", "CardEffectFactory", "KeyWordEffects", "Blitz.cs"));
    string retaliation = File.ReadAllText(Path.Combine(root, "src", "HeadlessDCGO.Engine", "Assets", "Scripts", "Script", "CardEffectFactory", "KeyWordEffects", "Retaliation.cs"));
    string armor = File.ReadAllText(Path.Combine(root, "src", "HeadlessDCGO.Engine", "Assets", "Scripts", "Script", "CardEffectFactory", "KeyWordEffects", "ArmorPurge.cs"));

    AssertContains(helper, "KeywordBaseBatch2Kind", "batch kind model");
    AssertContains(helper, "KeywordBaseBatch2Factory", "batch factory");
    AssertContains(helper, "GrantRush", "rush mutation");
    AssertContains(helper, "RequestBlitzAttack", "blitz mutation");
    AssertContains(helper, "DeleteRetaliationTarget", "retaliation mutation");
    AssertContains(helper, "ApplyArmorPurge", "armor mutation");

    foreach (string text in new[] { helper, rush, blitz, retaliation, armor })
    {
        AssertDoesNotContain(text, "TODO", "no placeholder");
        AssertDoesNotContain(text, "UnityEngine", "no Unity dependency");
        AssertDoesNotContain(text, "MonoBehaviour", "no MonoBehaviour dependency");
        AssertDoesNotContain(text, "Photon", "no Photon dependency");
    }

    return Task.CompletedTask;
}

string ReadAsIsFactory(string fileName)
{
    return File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardEffectFactory", "KeyWordEffects", fileName));
}

string ReadAsIsCommons(string fileName)
{
    return File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardEffectCommons", "KeyWordEffects", fileName));
}

MatchState CreateState(bool withArmorSources = true)
{
    PlayerState playerOne = new PlayerState(PlayerOne)
        .WithZone(ChoiceZone.BattleArea, new[] { KeywordSource })
        .WithZone(ChoiceZone.Trash, new[] { RetaliationSource })
        .WithZone(ChoiceZone.Hand, new[] { HandCard });
    PlayerState playerTwo = new PlayerState(PlayerTwo)
        .WithZone(ChoiceZone.BattleArea, new[] { OpponentBattleCard });

    var cards = new Dictionary<HeadlessEntityId, CardInstanceState>
    {
        [KeywordSource] = new(
            KeywordSource,
            new HeadlessEntityId("DEF-SOURCE"),
            PlayerOne,
            IsFaceUp: true,
            SourceIds: withArmorSources ? new[] { SourceBottom, SourceTop } : Array.Empty<HeadlessEntityId>()),
        [RetaliationSource] = new(RetaliationSource, new HeadlessEntityId("DEF-RETALIATION"), PlayerOne, IsFaceUp: true),
        [OpponentBattleCard] = new(OpponentBattleCard, new HeadlessEntityId("DEF-OPPONENT"), PlayerTwo, IsFaceUp: true),
        [HandCard] = new(HandCard, new HeadlessEntityId("DEF-HAND"), PlayerOne),
        [SourceBottom] = new(SourceBottom, new HeadlessEntityId("DEF-SOURCE-BOTTOM"), PlayerOne),
        [SourceTop] = new(SourceTop, new HeadlessEntityId("DEF-SOURCE-TOP"), PlayerOne),
    };

    return new MatchState(new[] { playerOne, playerTwo }, cards);
}

EffectContext CreateContext(HeadlessEntityId sourceId, IReadOnlyDictionary<string, object?> values)
{
    return new EffectContext(
        PlayerOne,
        PlayerOne,
        sourceId,
        triggerEntityId: null,
        targetEntityIds: Array.Empty<HeadlessEntityId>(),
        values);
}

EffectRequest CreateRequest(KeywordBaseBatch2Effect effect, EffectContext context)
{
    return new EffectRequest(effect.Definition.EffectId, PlayerOne, effect.Definition.Timing, context);
}

string Signature(EffectResult result, RecordingEffectMutationSink sink)
{
    string values = string.Join(
        ",",
        result.Values.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={pair.Value}"));
    string mutations = string.Join(
        ";",
        sink.Snapshot().Select(mutation => $"{mutation.Kind}:{mutation.SourceEntityId.Value}:{string.Join(",", mutation.Values.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={pair.Value}"))}"));
    return $"{result.Resolved}|{result.Message}|{values}|{mutations}";
}

static List<Dictionary<string, string>> ReadCsv(string path)
{
    string[] lines = File.ReadAllLines(path);
    string[] headers = SplitCsvLine(lines[0]);
    var rows = new List<Dictionary<string, string>>();
    foreach (string line in lines.Skip(1))
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            continue;
        }

        string[] cells = SplitCsvLine(line);
        var row = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 0; index < headers.Length && index < cells.Length; index++)
        {
            row[headers[index]] = cells[index];
        }

        rows.Add(row);
    }

    return rows;
}

static string[] SplitCsvLine(string line)
{
    var cells = new List<string>();
    var current = new System.Text.StringBuilder();
    bool quoted = false;
    for (int index = 0; index < line.Length; index++)
    {
        char ch = line[index];
        if (ch == '"')
        {
            if (quoted && index + 1 < line.Length && line[index + 1] == '"')
            {
                current.Append('"');
                index++;
            }
            else
            {
                quoted = !quoted;
            }
        }
        else if (ch == ',' && !quoted)
        {
            cells.Add(current.ToString());
            current.Clear();
        }
        else
        {
            current.Append(ch);
        }
    }

    cells.Add(current.ToString());
    return cells.ToArray();
}

static string Value(Dictionary<string, string> row, string key)
{
    return row.TryGetValue(key, out string? value) ? value : string.Empty;
}

void AssertComplete(string fileName)
{
    string path = Path.Combine(root, "docs", "test-results", "goals", fileName);
    if (!File.Exists(path))
    {
        throw new InvalidOperationException($"Required predecessor result document is missing: {fileName}");
    }

    string text = File.ReadAllText(path);
    AssertContains(text, "COMPLETE", $"predecessor complete: {fileName}");
}

static void AssertTrue(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException($"Expected true: {message}");
    }
}

static void AssertFalse(bool condition, string message)
{
    if (condition)
    {
        throw new InvalidOperationException($"Expected false: {message}");
    }
}

static void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected '{expected}' but got '{actual}' for {message}.");
    }
}

static void AssertContains(string text, string expected, string message)
{
    if (!text.Contains(expected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Expected text to contain '{expected}' for {message}.");
    }
}

static void AssertDoesNotContain(string text, string forbidden, string message)
{
    if (text.Contains(forbidden, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Expected text not to contain '{forbidden}' for {message}.");
    }
}

static string FindRepositoryRoot()
{
    DirectoryInfo? directory = new(AppContext.BaseDirectory);
    while (directory is not null)
    {
        string marker = Path.Combine(directory.FullName, "docs", "headless_complete_goal_breakdown.csv");
        if (File.Exists(marker))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    throw new InvalidOperationException("Could not locate repository root.");
}
