using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();
HeadlessPlayerId Player = new(1);
HeadlessPlayerId Opponent = new(2);
HeadlessEntityId AttackerId = new("p1:main:001:P1-M01");
HeadlessEntityId SuspendedAttackerId = new("p1:main:002:P1-M02");
HeadlessEntityId TargetId = new("p2:main:001:P2-M01");
HeadlessEntityId UnsuspendedTargetId = new("p2:main:002:P2-M02");

var tests = new (string Name, Func<Task> Body)[]
{
    ("G2E-004 goal row and predecessor are satisfied", GoalRowAndPredecessorAreSatisfied),
    ("AS-IS AttackPermanentAction references are recorded", AsIsAttackReferencesAreRecorded),
    ("Main phase dispatch exposes direct and suspended-target attack intents", MainPhaseDispatchExposesAttackIntents),
    ("Attack action ids distinguish direct and target intents", AttackActionIdsDistinguishTargets),
    ("Attack processor declares pending attack and suspends attacker", AttackProcessorDeclaresPendingAttackAndSuspendsAttacker),
    ("Attack processor rejects suspended attacker without mutation", AttackProcessorRejectsSuspendedAttackerWithoutMutation),
    ("Attack processor rejects invalid target without mutation", AttackProcessorRejectsInvalidTargetWithoutMutation),
    ("Attack legal query and apply share entered-this-turn condition", LegalQueryAndApplyShareEnteredThisTurnCondition),
    ("Pending attack suppresses further attack legal actions", PendingAttackSuppressesFurtherAttackLegalActions),
    ("G2E-004 source files contain no placeholder markers", AttackActionFilesHaveNoPlaceholderMarkers),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G2E-004")
        ?? throw new InvalidOperationException("G2E-004 row was not found.");

    AssertEqual("Phase 2", Value(row, "phase"), "phase");
    AssertEqual("MainPhaseAction", Value(row, "area"), "area");
    AssertEqual("AttackPermanentAction attack intent", Value(row, "deliverables"), "deliverables");
    AssertContains(Value(row, "scope"), "공격 action", "scope");
    AssertContains(Value(row, "unit_test_scope"), "attack action dispatch", "unit_test_scope");
    AssertEqual("docs/test-results/goals/G2E-004_attack_action_dispatch_unit_test_results.md", Value(row, "result_document"), "result_document");
    AssertEqual("G2A-006", Value(row, "blocked_until"), "blocked_until");

    string predecessor = File.ReadAllText(Path.Combine(root, "docs", "test-results", "goals", "G2A-006_legal_action_dispatch_unit_test_results.md"));
    AssertContains(predecessor, "COMPLETE", "G2A-006 completion marker");
    return Task.CompletedTask;
}

Task AsIsAttackReferencesAreRecorded()
{
    string attackAction = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "MainPhaseAction", "AttackPermanentAction.cs"));
    string turnStateMachine = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "TurnStateMachine.cs"));
    string attackProcess = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "AttackProcess.cs"));
    string permanent = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "Permanent.cs"));

    AssertContains(attackAction, "class AttackPermanentAction : MainPhaseAction", "AS-IS action class");
    AssertContains(attackAction, "PermanentIndex", "AS-IS attacker index payload");
    AssertContains(attackAction, "AttackTargetPermanentIndex", "AS-IS target index payload");
    AssertContains(attackAction, "SetAttackingPermaent", "AS-IS execution target");
    AssertContains(turnStateMachine, "QueueMainPhaseAction(gameContext.TurnPlayer, new AttackPermanentAction", "AS-IS queued attack action");
    AssertContains(turnStateMachine, "GManager.instance.attackProcess.Attack(AttackingPermanent", "AS-IS AttackProcess connection");
    AssertContains(attackProcess, "public IEnumerator Attack(Permanent attackingPermanent", "AS-IS attack process entry");
    AssertContains(attackProcess, "AttackCount++", "AS-IS attack count");
    AssertContains(attackProcess, "new SuspendPermanentsClass", "AS-IS attacker suspend");
    AssertContains(permanent, "public bool CanAttack(ICardEffect", "AS-IS CanAttack");
    AssertContains(permanent, "CanAttackTargetDigimon", "AS-IS target validation");
    return Task.CompletedTask;
}

async Task MainPhaseDispatchExposesAttackIntents()
{
    DcgoMatch match = await CreateConfiguredMatchAsync();
    LegalAction[] attacks = AttackActions(match);

    AssertEqual(2, attacks.Length, "attack action count");
    AssertTrue(attacks.Any(IsDirectAttack), "has direct attack");
    AssertTrue(attacks.Any(action => ReadEntityId(action.Parameters, HeadlessActionParameterKeys.AttackTargetId) == TargetId), "has suspended target attack");
    AssertFalse(attacks.Any(action => ReadEntityId(action.Parameters, HeadlessActionParameterKeys.AttackerId) == SuspendedAttackerId), "suspended attacker excluded");
    AssertFalse(attacks.Any(action => ReadEntityId(action.Parameters, HeadlessActionParameterKeys.AttackTargetId) == UnsuspendedTargetId), "unsuspended target excluded");
}

async Task AttackActionIdsDistinguishTargets()
{
    DcgoMatch match = await CreateConfiguredMatchAsync();
    LegalAction[] attacks = AttackActions(match);

    AssertEqual(attacks.Length, attacks.Select(action => action.Id.Value).Distinct(StringComparer.Ordinal).Count(), "unique attack action ids");
    AssertTrue(attacks.Any(action => action.Id.Value.EndsWith(":player", StringComparison.Ordinal)), "direct attack id suffix");
    AssertTrue(attacks.Any(action => action.Id.Value.EndsWith($":{TargetId.Value}", StringComparison.Ordinal)), "target attack id suffix");
}

async Task AttackProcessorDeclaresPendingAttackAndSuspendsAttacker()
{
    DcgoMatch match = await CreateConfiguredMatchAsync();
    LegalAction direct = AttackActions(match).Single(IsDirectAttack);

    // Process the declaration directly: once the common loop (G3.5-005) is wired, stepping through
    // the game loop would auto-advance the attack pipeline past declaration. This test isolates the
    // AttackPermanentAction declaration + suspend contract.
    ActionProcessResult result = new AttackPermanentAction().Process(direct, match.Context);

    HeadlessAttackState attack = match.Context.AttackController.Current;
    AssertTrue(result.IsSuccess, "declare success");
    AssertTrue(attack.IsPending, "attack pending");
    AssertFalse(attack.IsResolved, "attack resolved");
    AssertEqual(1, attack.AttackCount, "attack count");
    AssertEqual(AttackerId, attack.AttackerId, "attacker id");
    AssertEqual(Opponent, attack.DefendingPlayerId, "defending player");
    AssertEqual(null, attack.TargetId, "direct target");
    AssertTrue(attack.IsDirectAttack, "direct attack");
    AssertEqual(AttackPhase.Declared, attack.Phase, "declared phase");
    AssertTrue(ReadInstanceBool(match, AttackerId, "isSuspended"), "attacker suspended");

    AssertMetadata(result.Metadata, HeadlessActionParameterKeys.ActionType, HeadlessActionTypes.DeclareAttack);
    AssertMetadata(result.Metadata, HeadlessActionParameterKeys.AttackerId, AttackerId.Value);
    AssertMetadata(result.Metadata, HeadlessActionParameterKeys.DefendingPlayerId, Opponent.Value);
    AssertMetadata(result.Metadata, HeadlessActionParameterKeys.IsDirectAttack, true);
    AssertMetadata(result.Metadata, "attackIntent", "AttackPermanentAction");
    AssertMetadata(result.Metadata, "attackerSuspended", true);
}

async Task AttackProcessorRejectsSuspendedAttackerWithoutMutation()
{
    DcgoMatch match = await CreateConfiguredMatchAsync();
    var action = HeadlessActionFactory.DeclareAttack(Player, SuspendedAttackerId, Opponent, targetId: null, isDirectAttack: true);
    string before = SnapshotAttackAndCards(match);

    ActionProcessResult result = new AttackPermanentAction().Process(action, match.Context);

    AssertFalse(result.IsSuccess, "result success");
    AssertTrue(result.IsIllegal, "result illegal");
    AssertContains(result.Message, "suspended", "illegal reason");
    AssertEqual(before, SnapshotAttackAndCards(match), "state unchanged");
}

async Task AttackProcessorRejectsInvalidTargetWithoutMutation()
{
    DcgoMatch match = await CreateConfiguredMatchAsync();
    var action = HeadlessActionFactory.DeclareAttack(Player, AttackerId, Opponent, UnsuspendedTargetId, isDirectAttack: false);
    string before = SnapshotAttackAndCards(match);

    ActionProcessResult result = new AttackPermanentAction().Process(action, match.Context);

    AssertFalse(result.IsSuccess, "result success");
    AssertTrue(result.IsIllegal, "result illegal");
    AssertContains(result.Message, "not suspended", "illegal reason");
    AssertEqual(before, SnapshotAttackAndCards(match), "state unchanged");
}

async Task LegalQueryAndApplyShareEnteredThisTurnCondition()
{
    DcgoMatch match = await CreateConfiguredMatchAsync(attackerMetadata: new Dictionary<string, object?> { ["enteredThisTurn"] = true });
    var action = HeadlessActionFactory.DeclareAttack(Player, AttackerId, Opponent, targetId: null, isDirectAttack: true);
    string before = SnapshotAttackAndCards(match);

    AssertEqual(0, AttackActions(match).Length, "legal attack count");

    ActionProcessResult result = new AttackPermanentAction().Process(action, match.Context);
    AssertFalse(result.IsSuccess, "result success");
    AssertTrue(result.IsIllegal, "result illegal");
    AssertContains(result.Message, "entered this turn", "illegal reason");
    AssertEqual(before, SnapshotAttackAndCards(match), "state unchanged");
}

async Task PendingAttackSuppressesFurtherAttackLegalActions()
{
    DcgoMatch match = await CreateConfiguredMatchAsync();
    LegalAction direct = AttackActions(match).Single(IsDirectAttack);
    await match.ApplyActionAsync(direct);
    await match.StepAsync();

    AssertEqual(0, AttackActions(match).Length, "legal attack count while pending");
}

Task AttackActionFilesHaveNoPlaceholderMarkers()
{
    var scopedFiles = new[]
    {
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "AttackPermanentAction.cs"),
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "HeadlessLegalActionDispatcher.cs"),
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "HeadlessActionFactory.cs"),
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "HeadlessActionPayloads.cs"),
        Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "HeadlessAttackState.cs")
    };

    foreach (string path in scopedFiles)
    {
        string text = File.ReadAllText(path);
        AssertFalse(text.Contains("TODO", StringComparison.OrdinalIgnoreCase), path);
        AssertFalse(text.Contains("NotImplementedException", StringComparison.Ordinal), path);
    }

    return Task.CompletedTask;
}

async Task<DcgoMatch> CreateConfiguredMatchAsync(IReadOnlyDictionary<string, object?>? attackerMetadata = null)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 44);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(new CardRecord(
            new HeadlessEntityId($"P1-M{index:D2}"),
            $"P1-M{index:D2}",
            $"P1 Digimon {index}",
            new Dictionary<string, object?>(),
            CardType: "Digimon"));
        cards.Upsert(new CardRecord(
            new HeadlessEntityId($"P2-M{index:D2}"),
            $"P2-M{index:D2}",
            $"P2 Digimon {index}",
            new Dictionary<string, object?>(),
            CardType: "Digimon"));
    }

    DcgoMatch match = new(context);
    HeadlessPlayerId[] players = { Player, Opponent };
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { BuildDeck(Player, "P1"), BuildDeck(Opponent, "P2") },
        firstPlayerId: Player, shuffleDecks: false, shuffleDigitamaDecks: false);

    await match.InitializeAsync(MatchConfig.Create(players, randomSeed: 44, setup: setup));
    await AdvanceToMainAsync(match, Player);
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Player, AttackerId, ChoiceZone.Hand, ChoiceZone.BattleArea));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Player, SuspendedAttackerId, ChoiceZone.Hand, ChoiceZone.BattleArea));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Opponent, TargetId, ChoiceZone.Hand, ChoiceZone.BattleArea));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Opponent, UnsuspendedTargetId, ChoiceZone.Hand, ChoiceZone.BattleArea));

    SetMetadata(match, AttackerId, attackerMetadata ?? new Dictionary<string, object?> { ["isSuspended"] = false });
    SetMetadata(match, SuspendedAttackerId, new Dictionary<string, object?> { ["isSuspended"] = true });
    SetMetadata(match, TargetId, new Dictionary<string, object?> { ["isSuspended"] = true });
    SetMetadata(match, UnsuspendedTargetId, new Dictionary<string, object?> { ["isSuspended"] = false });
    return match;
}

static PlayerDeckSetup BuildDeck(
    HeadlessPlayerId playerId,
    string prefix,
    int mainCount = 12,
    int digitamaCount = 3)
{
    return new PlayerDeckSetup(
        playerId,
        Enumerable.Range(1, mainCount)
            .Select(index => new HeadlessEntityId($"{prefix}-M{index:D2}"))
            .ToArray(),
        Enumerable.Range(1, digitamaCount)
            .Select(index => new HeadlessEntityId($"{prefix}-D{index:D2}"))
            .ToArray());
}

static async Task AdvanceToMainAsync(DcgoMatch match, HeadlessPlayerId playerId)
{
    for (var attempt = 0; attempt < 8 && match.GetObservation().Turn.Phase != HeadlessPhase.Main; attempt++)
    {
        LegalAction advance = SingleLegalAction(match, playerId, HeadlessActionTypes.AdvancePhase);
        await match.ApplyActionAsync(advance);
        await match.StepAsync();
    }

    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "advance to main");
}

static LegalAction SingleLegalAction(
    DcgoMatch match,
    HeadlessPlayerId playerId,
    string actionType)
{
    LegalAction[] actions = match.GetLegalActions(playerId)
        .Where(action => action.ActionType == actionType)
        .ToArray();
    AssertEqual(1, actions.Length, $"{actionType} count");
    return actions[0];
}

LegalAction[] AttackActions(DcgoMatch match)
{
    return match.GetLegalActions(Player)
        .Where(action => action.ActionType == HeadlessActionTypes.DeclareAttack)
        .OrderBy(action => action.Id.Value, StringComparer.Ordinal)
        .ToArray();
}

static bool IsDirectAttack(LegalAction action)
{
    return ReadBool(action.Parameters, HeadlessActionParameterKeys.IsDirectAttack);
}

static void SetMetadata(DcgoMatch match, HeadlessEntityId cardId, IReadOnlyDictionary<string, object?> values)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
    {
        throw new InvalidOperationException($"Missing card instance '{cardId}'.");
    }

    Dictionary<string, object?> metadata = new(record.Metadata, StringComparer.Ordinal);
    foreach (KeyValuePair<string, object?> pair in values)
    {
        metadata[pair.Key] = pair.Value;
    }

    match.Context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
}

static bool ReadInstanceBool(DcgoMatch match, HeadlessEntityId cardId, string key)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
    {
        throw new InvalidOperationException($"Missing card instance '{cardId}'.");
    }

    return record.Metadata.TryGetValue(key, out object? rawValue) && rawValue switch
    {
        bool value => value,
        string value => bool.TryParse(value, out bool parsed) && parsed,
        _ => false
    };
}

string SnapshotAttackAndCards(DcgoMatch match)
{
    HeadlessAttackState attack = match.Context.AttackController.Current;
    string cards = string.Join(
        "|",
        match.Context.CardInstanceRepository.Snapshot()
            .Where(record => record.InstanceId == AttackerId ||
                record.InstanceId == SuspendedAttackerId ||
                record.InstanceId == TargetId ||
                record.InstanceId == UnsuspendedTargetId)
            .OrderBy(record => record.InstanceId.Value, StringComparer.Ordinal)
            .Select(record => $"{record.InstanceId.Value}:{FlattenMetadata(record.Metadata)}"));
    return $"{attack.AttackCount}:{attack.IsPending}:{attack.AttackerId?.Value}:{attack.TargetId?.Value}:{cards}";
}

static string FlattenMetadata(IReadOnlyDictionary<string, object?> metadata)
{
    return string.Join(
        ",",
        metadata
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}={pair.Value}"));
}

static HeadlessEntityId ReadEntityId(IReadOnlyDictionary<string, object?> parameters, string key)
{
    if (!parameters.TryGetValue(key, out object? raw) || raw is null)
    {
        return default;
    }

    return raw switch
    {
        HeadlessEntityId id => id,
        string value when !string.IsNullOrWhiteSpace(value) => new HeadlessEntityId(value),
        _ => default
    };
}

static bool ReadBool(IReadOnlyDictionary<string, object?> parameters, string key)
{
    if (!parameters.TryGetValue(key, out object? raw) || raw is null)
    {
        return false;
    }

    return raw switch
    {
        bool value => value,
        string value => bool.TryParse(value, out bool parsed) && parsed,
        _ => false
    };
}

static void AssertMetadata(IReadOnlyDictionary<string, object?> metadata, string key, object? expected)
{
    if (!metadata.TryGetValue(key, out object? actual))
    {
        throw new InvalidOperationException($"metadata: missing key '{key}'.");
    }

    AssertEqual(expected, actual, $"metadata {key}");
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
