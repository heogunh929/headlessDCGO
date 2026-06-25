using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;
using FactoryBlocker = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory.KeyWordEffects.Blocker;
using FactoryJamming = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory.KeyWordEffects.Jamming;
using FactoryPierce = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory.KeyWordEffects.Pierce;
using FactoryReboot = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory.KeyWordEffects.Reboot;

var root = FindRepositoryRoot();
HeadlessPlayerId PlayerOne = new(1);
HeadlessPlayerId PlayerTwo = new(2);
HeadlessEntityId KeywordSource = new("p1-keyword-source");
HeadlessEntityId OpponentBattleCard = new("p2-battle-loser");
HeadlessEntityId HandCard = new("p1-hand-card");

var tests = new (string Name, Func<Task> Body)[]
{
    ("G3G-001 goal row and predecessor are satisfied", GoalRowAndPredecessorAreSatisfied),
    ("AS-IS keyword batch 1 references are recorded", AsIsKeywordBatch1ReferencesAreRecorded),
    ("Factory creates blocker jamming reboot piercing effects", FactoryCreatesFourKeywordEffects),
    ("Keyword effects register deterministic keyword bindings", KeywordEffectsRegisterDeterministicBindings),
    ("Blocker resolves by granting blocker mutation to battle target", BlockerResolvesByGrantingMutation),
    ("Reboot resolves by scheduling opponent unsuspend mutation", RebootResolvesBySchedulingMutation),
    ("Jamming prevents battle deletion only against security battle", JammingPreventsBattleDeletionOnlyAgainstSecurity),
    ("Piercing enables security check only after deleting opponent by battle", PiercingEnablesSecurityCheckOnlyAfterOpponentBattleDeletion),
    ("Invalid keyword target fails without mutation", InvalidKeywordTargetFailsWithoutMutation),
    ("G3G-001 source files stay inside keyword base batch 1 scope", SourceFilesStayInsideGoalScope),
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
    Dictionary<string, string> row = rows.SingleOrDefault(row => Value(row, "goal_id") == "G3G-001")
        ?? throw new InvalidOperationException("G3G-001 row was not found.");

    AssertEqual("Phase 3", Value(row, "phase"), "phase");
    AssertEqual("Keywords", Value(row, "area"), "area");
    AssertContains(Value(row, "scope"), "blocker jamming reboot piercing base", "scope");
    AssertEqual("keyword base batch1", Value(row, "deliverables"), "deliverables");
    AssertContains(Value(row, "unit_test_scope"), "keyword base batch1", "unit_test_scope");
    AssertEqual("docs/test-results/goals/G3G-001_keyword_base_batch1_unit_test_results.md", Value(row, "result_document"), "result_document");
    AssertEqual("G3F-002", Value(row, "blocked_until"), "prerequisite");
    AssertContains(Value(row, "completion_gate"), "keyword base1", "completion gate");
    AssertComplete("G3F-002_zone_query_helpers_unit_test_results.md");
    return Task.CompletedTask;
}

Task AsIsKeywordBatch1ReferencesAreRecorded()
{
    string factoryBlocker = ReadAsIsFactory("Blocker.cs");
    string factoryJamming = ReadAsIsFactory("Jamming.cs");
    string factoryReboot = ReadAsIsFactory("Reboot.cs");
    string factoryPierce = ReadAsIsFactory("Pierce.cs");
    string commonsPierce = ReadAsIsCommons("Pierce.cs");

    AssertContains(factoryBlocker, "BlockerStaticEffect", "AS-IS blocker factory");
    AssertContains(factoryBlocker, "SetUpBlockerClass", "AS-IS blocker class setup");
    AssertContains(factoryJamming, "JammingStaticEffect", "AS-IS jamming factory");
    AssertContains(factoryJamming, "CanNotBeDestroyedByBattleCondition", "AS-IS jamming battle condition");
    AssertContains(factoryReboot, "RebootStaticEffect", "AS-IS reboot factory");
    AssertContains(factoryReboot, "SetUpRebootClass", "AS-IS reboot class setup");
    AssertContains(factoryPierce, "PierceEffect", "AS-IS pierce factory");
    AssertContains(commonsPierce, "CanActivatePierce", "AS-IS pierce activation");
    AssertContains(commonsPierce, "DoSecurityCheck = true", "AS-IS security check mutation");
    return Task.CompletedTask;
}

Task FactoryCreatesFourKeywordEffects()
{
    KeywordBaseBatch1Effect blocker = FactoryBlocker.Create(KeywordSource);
    KeywordBaseBatch1Effect jamming = FactoryJamming.Create(KeywordSource);
    KeywordBaseBatch1Effect reboot = FactoryReboot.Create(KeywordSource);
    KeywordBaseBatch1Effect piercing = FactoryPierce.Create(KeywordSource, isInherited: true, isLinked: true);

    AssertEqual(KeywordBaseBatch1Kind.Blocker, blocker.Kind, "blocker kind");
    AssertEqual("Blocker", blocker.Definition.Name, "blocker name");
    AssertEqual(KeywordBaseBatch1Timings.BlockTiming, blocker.Definition.Timing, "blocker timing");
    AssertEqual(KeywordBaseBatch1Kind.Jamming, jamming.Kind, "jamming kind");
    AssertEqual(KeywordBaseBatch1Timings.BattleDeletionCheck, jamming.Definition.Timing, "jamming timing");
    AssertEqual(KeywordBaseBatch1Kind.Reboot, reboot.Kind, "reboot kind");
    AssertEqual(KeywordBaseBatch1Timings.OpponentUnsuspend, reboot.Definition.Timing, "reboot timing");
    AssertEqual(KeywordBaseBatch1Kind.Piercing, piercing.Kind, "piercing kind");
    AssertEqual("Piercing", piercing.Keyword, "piercing keyword");
    AssertTrue(piercing.Definition.IsOptional, "piercing optional");
    AssertTrue(piercing.IsInherited, "piercing inherited");
    AssertTrue(piercing.IsLinked, "piercing linked");
    return Task.CompletedTask;
}

Task KeywordEffectsRegisterDeterministicBindings()
{
    InMemoryEffectRegistry registry = new();
    EffectContext context = CreateContext(new Dictionary<string, object?> { [KeywordBaseBatch1ContextKeys.MatchState] = CreateState() });

    IReadOnlyList<EffectBinding> bindings = KeywordBaseBatch1Factory.RegisterBaseBatch1(
        registry,
        KeywordSource,
        PlayerOne,
        context);

    AssertEqual(4, bindings.Count, "binding count");
    AssertEqual("Blocker,Jamming,Reboot,Piercing", string.Join(",", bindings.Select(binding => binding.Request.Context.SourceEntityId == KeywordSource ? binding.Keywords[0] : "bad")), "registered order");
    AssertEqual(1, registry.GetKeywordEffects("Blocker").Count, "blocker registry");
    AssertEqual(1, registry.GetKeywordEffects("Jamming").Count, "jamming registry");
    AssertEqual(1, registry.GetKeywordEffects("Reboot").Count, "reboot registry");
    AssertEqual(1, registry.GetKeywordEffects("Piercing").Count, "piercing registry");
    AssertEqual(1, registry.GetKeywordEffects("Pierce").Count, "AS-IS pierce alias");
    AssertEqual(1, registry.GetReplacementEffects(new EffectQueryContext(KeywordBaseBatch1Scopes.SecurityBattleDeletion, sourceEntityId: KeywordSource)).Count, "jamming replacement query");
    AssertEqual(1, registry.GetModifierEffects(new EffectQueryContext(KeywordBaseBatch1Scopes.RebootUnsuspend, sourceEntityId: KeywordSource)).Count, "reboot modifier query");
    return Task.CompletedTask;
}

async Task BlockerResolvesByGrantingMutation()
{
    KeywordBaseBatch1Effect effect = KeywordBaseBatch1Factory.Create(KeywordBaseBatch1Kind.Blocker, KeywordSource);
    EffectRequest request = CreateRequest(effect, CreateContext(new Dictionary<string, object?> { [KeywordBaseBatch1ContextKeys.MatchState] = CreateState() }));
    RecordingEffectMutationSink sink = new();

    EffectResult result = await effect.ResolveAsync(new CardEffectResolveContext(request), sink);

    AssertTrue(result.Resolved, "blocker resolved");
    AssertEqual(1, sink.Count, "mutation count");
    EffectMutation mutation = sink.Snapshot().Single();
    AssertEqual("GrantBlocker", mutation.Kind, "mutation kind");
    AssertEqual("Blocker", mutation.Values["keyword"], "keyword value");
    AssertEqual(KeywordSource.Value, mutation.Values["targetEntityId"], "target id");
}

async Task RebootResolvesBySchedulingMutation()
{
    KeywordBaseBatch1Effect effect = KeywordBaseBatch1Factory.Create(KeywordBaseBatch1Kind.Reboot, KeywordSource);
    EffectRequest request = CreateRequest(effect, CreateContext(new Dictionary<string, object?> { [KeywordBaseBatch1ContextKeys.MatchState] = CreateState() }));
    RecordingEffectMutationSink sink = new();

    EffectResult first = await effect.ResolveAsync(new CardEffectResolveContext(request), sink);
    string firstSignature = Signature(first, sink);
    sink.Clear();
    EffectResult second = await effect.ResolveAsync(new CardEffectResolveContext(request), sink);

    AssertTrue(first.Resolved, "first reboot resolved");
    AssertTrue(second.Resolved, "second reboot resolved");
    AssertEqual("ScheduleRebootUnsuspend", sink.Snapshot().Single().Kind, "mutation kind");
    AssertEqual(firstSignature, Signature(second, sink), "deterministic reboot");
}

async Task JammingPreventsBattleDeletionOnlyAgainstSecurity()
{
    KeywordBaseBatch1Effect effect = KeywordBaseBatch1Factory.Create(KeywordBaseBatch1Kind.Jamming, KeywordSource);
    EffectContext validContext = CreateContext(new Dictionary<string, object?>
    {
        [KeywordBaseBatch1ContextKeys.MatchState] = CreateState(),
        [KeywordBaseBatch1ContextKeys.AttackingCardId] = KeywordSource,
        [KeywordBaseBatch1ContextKeys.DefendingCardIsSecurity] = true,
    });
    EffectContext invalidContext = CreateContext(new Dictionary<string, object?>
    {
        [KeywordBaseBatch1ContextKeys.MatchState] = CreateState(),
        [KeywordBaseBatch1ContextKeys.AttackingCardId] = KeywordSource,
        [KeywordBaseBatch1ContextKeys.DefendingCardIsSecurity] = false,
    });

    RecordingEffectMutationSink sink = new();
    EffectResult valid = await effect.ResolveAsync(new CardEffectResolveContext(CreateRequest(effect, validContext)), sink);
    sink.Clear();
    EffectResult invalid = await effect.ResolveAsync(new CardEffectResolveContext(CreateRequest(effect, invalidContext)), sink);

    AssertTrue(valid.Resolved, "valid jamming resolved");
    AssertEqual("PreventBattleDeletion", valid.Values["mutationKind"], "valid mutation kind");
    AssertFalse(invalid.Resolved, "invalid jamming failed");
    AssertEqual(0, sink.Count, "invalid no mutation");
    AssertEqual("defendingCardIsSecurity", invalid.Values["field"], "invalid field");
}

async Task PiercingEnablesSecurityCheckOnlyAfterOpponentBattleDeletion()
{
    KeywordBaseBatch1Effect effect = KeywordBaseBatch1Factory.Create(KeywordBaseBatch1Kind.Piercing, KeywordSource);
    EffectContext validContext = CreateContext(new Dictionary<string, object?>
    {
        [KeywordBaseBatch1ContextKeys.MatchState] = CreateState(),
        [KeywordBaseBatch1ContextKeys.BattleDeletedByBattle] = true,
        [KeywordBaseBatch1ContextKeys.BattleWinnerCardId] = KeywordSource,
        [KeywordBaseBatch1ContextKeys.BattleLoserCardId] = OpponentBattleCard,
        [KeywordBaseBatch1ContextKeys.OpponentSecurityCount] = 1,
        [KeywordBaseBatch1ContextKeys.DoSecurityCheck] = false,
    });
    EffectContext alreadyCheckedContext = CreateContext(new Dictionary<string, object?>
    {
        [KeywordBaseBatch1ContextKeys.MatchState] = CreateState(),
        [KeywordBaseBatch1ContextKeys.BattleDeletedByBattle] = true,
        [KeywordBaseBatch1ContextKeys.BattleWinnerCardId] = KeywordSource,
        [KeywordBaseBatch1ContextKeys.BattleLoserCardId] = OpponentBattleCard,
        [KeywordBaseBatch1ContextKeys.OpponentSecurityCount] = 1,
        [KeywordBaseBatch1ContextKeys.DoSecurityCheck] = true,
    });

    RecordingEffectMutationSink sink = new();
    EffectResult valid = await effect.ResolveAsync(new CardEffectResolveContext(CreateRequest(effect, validContext)), sink);
    sink.Clear();
    EffectResult invalid = await effect.ResolveAsync(new CardEffectResolveContext(CreateRequest(effect, alreadyCheckedContext)), sink);

    AssertTrue(valid.Resolved, "valid piercing resolved");
    AssertEqual("SetSecurityCheck", valid.Values["mutationKind"], "valid mutation kind");
    AssertEqual(true, valid.Values["doSecurityCheck"], "security check enabled");
    AssertFalse(invalid.Resolved, "already checked failed");
    AssertEqual(0, sink.Count, "invalid no mutation");
    AssertEqual("doSecurityCheck", invalid.Values["field"], "invalid field");
}

async Task InvalidKeywordTargetFailsWithoutMutation()
{
    KeywordBaseBatch1Effect blocker = KeywordBaseBatch1Factory.Create(KeywordBaseBatch1Kind.Blocker, KeywordSource, HandCard);
    EffectRequest request = CreateRequest(blocker, CreateContext(new Dictionary<string, object?> { [KeywordBaseBatch1ContextKeys.MatchState] = CreateState() }));
    RecordingEffectMutationSink sink = new();

    EffectResult result = await blocker.ResolveAsync(new CardEffectResolveContext(request), sink);

    AssertFalse(result.Resolved, "hand target fails");
    AssertEqual(0, sink.Count, "no mutation");
    AssertEqual("targetEntityId", result.Values["field"], "failure field");
}

Task SourceFilesStayInsideGoalScope()
{
    string helper = File.ReadAllText(Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Effects", "KeywordBaseBatch1.cs"));
    string blocker = File.ReadAllText(Path.Combine(root, "src", "HeadlessDCGO.Engine", "Assets", "Scripts", "Script", "CardEffectFactory", "KeyWordEffects", "Blocker.cs"));
    string jamming = File.ReadAllText(Path.Combine(root, "src", "HeadlessDCGO.Engine", "Assets", "Scripts", "Script", "CardEffectFactory", "KeyWordEffects", "Jamming.cs"));
    string reboot = File.ReadAllText(Path.Combine(root, "src", "HeadlessDCGO.Engine", "Assets", "Scripts", "Script", "CardEffectFactory", "KeyWordEffects", "Reboot.cs"));
    string pierce = File.ReadAllText(Path.Combine(root, "src", "HeadlessDCGO.Engine", "Assets", "Scripts", "Script", "CardEffectFactory", "KeyWordEffects", "Pierce.cs"));

    AssertContains(helper, "KeywordBaseBatch1Kind", "batch kind model");
    AssertContains(helper, "KeywordBaseBatch1Factory", "batch factory");
    AssertContains(helper, "GrantBlocker", "blocker mutation");
    AssertContains(helper, "PreventBattleDeletion", "jamming mutation");
    AssertContains(helper, "ScheduleRebootUnsuspend", "reboot mutation");
    AssertContains(helper, "SetSecurityCheck", "piercing mutation");

    foreach (string text in new[] { helper, blocker, jamming, reboot, pierce })
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

MatchState CreateState()
{
    PlayerState playerOne = new PlayerState(PlayerOne)
        .WithZone(ChoiceZone.BattleArea, new[] { KeywordSource })
        .WithZone(ChoiceZone.Hand, new[] { HandCard });
    PlayerState playerTwo = new PlayerState(PlayerTwo)
        .WithZone(ChoiceZone.BattleArea, new[] { OpponentBattleCard })
        .WithZone(ChoiceZone.Security, new[] { new HeadlessEntityId("p2-security") });

    var cards = new Dictionary<HeadlessEntityId, CardInstanceState>
    {
        [KeywordSource] = new(KeywordSource, new HeadlessEntityId("DEF-SOURCE"), PlayerOne, IsFaceUp: true),
        [OpponentBattleCard] = new(OpponentBattleCard, new HeadlessEntityId("DEF-OPPONENT"), PlayerTwo, IsFaceUp: true),
        [HandCard] = new(HandCard, new HeadlessEntityId("DEF-HAND"), PlayerOne),
        [new HeadlessEntityId("p2-security")] = new(new HeadlessEntityId("p2-security"), new HeadlessEntityId("DEF-SECURITY"), PlayerTwo),
    };

    return new MatchState(new[] { playerOne, playerTwo }, cards);
}

EffectContext CreateContext(IReadOnlyDictionary<string, object?> values)
{
    return new EffectContext(
        PlayerOne,
        PlayerOne,
        KeywordSource,
        triggerEntityId: null,
        targetEntityIds: Array.Empty<HeadlessEntityId>(),
        values);
}

EffectRequest CreateRequest(KeywordBaseBatch1Effect effect, EffectContext context)
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
