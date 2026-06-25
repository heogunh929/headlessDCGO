using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();
HeadlessPlayerId Player = new(1);
HeadlessPlayerId Opponent = new(2);
HeadlessEntityId AttackerId = new("p1:main:001:P1-M01");
HeadlessEntityId SecondAttackerId = new("p1:main:002:P1-M02");
HeadlessEntityId TargetId = new("p2:main:001:P2-M01");
HeadlessEntityId UnsuspendedTargetId = new("p2:main:002:P2-M02");

var tests = new (string Name, Func<Task> Body)[]
{
    ("G2G-001 goal row and predecessor are satisfied", GoalRowAndPredecessorAreSatisfied),
    ("AS-IS attack declaration target references are recorded", AsIsAttackDeclarationTargetReferencesAreRecorded),
    ("Attack declarations expose direct and suspended Digimon target candidates", DeclarationsExposeDirectAndSuspendedDigimonTargets),
    ("Attack target candidates convert to DeclareAttack legal actions", CandidatesConvertToLegalActions),
    ("Suspended attackers do not produce attack declarations", SuspendedAttackersProduceNoDeclarations),
    ("Entered-this-turn attacker without rush has no target candidates", EnteredThisTurnAttackerHasNoCandidates),
    ("Cannot-attack-player still allows legal Digimon target candidates", CannotAttackPlayerStillAllowsDigimonTargets),
    ("Unsuspended targets require explicit unsuspended target permission", UnsuspendedTargetsRequirePermission),
    ("Attack declaration candidates are deterministic", AttackDeclarationCandidatesAreDeterministic),
    ("G2G-001 source files contain no placeholder or Unity dependency", AttackDeclarationSourceHasNoPlaceholderOrUnityDependency),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G2G-001")
        ?? throw new InvalidOperationException("G2G-001 row was not found.");

    AssertEqual("Phase 2", Value(row, "phase"), "phase");
    AssertEqual("AttackProcess", Value(row, "area"), "area");
    AssertContains(Value(row, "goal"), "Attack declaration target", "goal");
    AssertContains(Value(row, "scope"), "target", "scope");
    AssertEqual("attack declaration", Value(row, "deliverables"), "deliverables");
    AssertContains(Value(row, "unit_test_scope"), "target candidate", "unit_test_scope");
    AssertEqual("docs/test-results/goals/G2G-001_attack_declaration_targets_unit_test_results.md", Value(row, "result_document"), "result_document");
    AssertEqual("G2E-004", Value(row, "blocked_until"), "blocked_until");

    AssertComplete("G2E-004_attack_action_dispatch_unit_test_results.md");
    return Task.CompletedTask;
}

Task AsIsAttackDeclarationTargetReferencesAreRecorded()
{
    string selectAttack = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "SelectAttackEffect.cs"));
    string attackProcess = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "AttackProcess.cs"));
    string permanent = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "Permanent.cs"));

    AssertContains(selectAttack, "const int SecurityIndex = -1", "AS-IS direct attack sentinel");
    AssertContains(selectAttack, "CanTarget(Permanent permanent)", "AS-IS target predicate");
    AssertContains(selectAttack, "CanAttackDigimon()", "AS-IS Digimon target query");
    AssertContains(selectAttack, "CanAttackPlayer()", "AS-IS player target query");
    AssertContains(selectAttack, "SetAttackTarget", "AS-IS selected target payload");
    AssertContains(attackProcess, "public IEnumerator Attack(Permanent attackingPermanent", "AS-IS attack declaration entry");
    AssertContains(permanent, "CanAttackTargetDigimon", "AS-IS target legality");
    return Task.CompletedTask;
}

async Task DeclarationsExposeDirectAndSuspendedDigimonTargets()
{
    DcgoMatch match = await CreateConfiguredMatchAsync();
    AttackDeclaration declaration = SingleDeclaration(match);

    AssertEqual(Player, declaration.PlayerId, "declaration player");
    AssertEqual(AttackerId, declaration.AttackerId, "attacker");
    AssertEqual(new HeadlessEntityId("P1-M01"), declaration.AttackerDefinitionId, "attacker definition");
    AssertEqual(2, declaration.TargetCandidates.Count, "candidate count");

    AttackTargetCandidate direct = declaration.TargetCandidates.Single(candidate => candidate.Kind == AttackTargetKind.Player);
    AssertTrue(direct.IsDirectAttack, "direct flag");
    AssertEqual(null, direct.TargetId, "direct target");
    AssertEqual(Opponent, direct.DefendingPlayerId, "direct defending player");

    AttackTargetCandidate digimon = declaration.TargetCandidates.Single(candidate => candidate.Kind == AttackTargetKind.Digimon);
    AssertFalse(digimon.IsDirectAttack, "digimon direct flag");
    AssertEqual(TargetId, digimon.TargetId, "digimon target");
    AssertEqual(new HeadlessEntityId("P2-M01"), digimon.TargetDefinitionId, "digimon target definition");
    AssertFalse(declaration.TargetCandidates.Any(candidate => candidate.TargetId == UnsuspendedTargetId), "unsuspended target excluded");
}

async Task CandidatesConvertToLegalActions()
{
    DcgoMatch match = await CreateConfiguredMatchAsync();
    AttackDeclaration declaration = SingleDeclaration(match);
    LegalAction[] candidateActions = declaration.TargetCandidates
        .Select(candidate => candidate.ToLegalAction())
        .OrderBy(action => action.Id.Value, StringComparer.Ordinal)
        .ToArray();
    LegalAction[] legalActions = match.GetLegalActions(Player)
        .Where(action => action.ActionType == HeadlessActionTypes.DeclareAttack)
        .OrderBy(action => action.Id.Value, StringComparer.Ordinal)
        .ToArray();

    AssertEqual(
        string.Join("|", legalActions.Select(DescribeAction)),
        string.Join("|", candidateActions.Select(DescribeAction)),
        "candidate action payloads");
}

async Task SuspendedAttackersProduceNoDeclarations()
{
    DcgoMatch match = await CreateConfiguredMatchAsync(
        attackerMetadata: new Dictionary<string, object?> { ["isSuspended"] = true },
        secondAttackerMetadata: new Dictionary<string, object?> { ["isSuspended"] = true });

    AttackDeclaration[] declarations = new AttackPermanentAction()
        .GetAttackDeclarations(match.Context, Player)
        .ToArray();

    AssertEqual(0, declarations.Length, "declaration count");
}

async Task EnteredThisTurnAttackerHasNoCandidates()
{
    DcgoMatch match = await CreateConfiguredMatchAsync(
        attackerMetadata: new Dictionary<string, object?> { ["enteredThisTurn"] = true });

    AttackDeclaration[] declarations = new AttackPermanentAction()
        .GetAttackDeclarations(match.Context, Player)
        .Where(declaration => declaration.AttackerId == AttackerId)
        .ToArray();

    AssertEqual(0, declarations.Length, "entered-this-turn declaration count");
}

async Task CannotAttackPlayerStillAllowsDigimonTargets()
{
    DcgoMatch match = await CreateConfiguredMatchAsync(
        attackerMetadata: new Dictionary<string, object?>
        {
            ["cannotAttackPlayer"] = true
        });

    AttackDeclaration declaration = SingleDeclaration(match);

    AssertEqual(1, declaration.TargetCandidates.Count, "candidate count");
    AssertEqual(AttackTargetKind.Digimon, declaration.TargetCandidates[0].Kind, "candidate kind");
    AssertEqual(TargetId, declaration.TargetCandidates[0].TargetId, "target id");
}

async Task UnsuspendedTargetsRequirePermission()
{
    DcgoMatch withoutPermission = await CreateConfiguredMatchAsync();
    AttackDeclaration normalDeclaration = SingleDeclaration(withoutPermission);
    AssertFalse(normalDeclaration.TargetCandidates.Any(candidate => candidate.TargetId == UnsuspendedTargetId), "normal excludes unsuspended target");

    DcgoMatch withPermission = await CreateConfiguredMatchAsync(
        attackerMetadata: new Dictionary<string, object?>
        {
            ["canAttackUnsuspendedDigimon"] = true
        });
    AttackDeclaration permissionDeclaration = SingleDeclaration(withPermission);
    AssertTrue(permissionDeclaration.TargetCandidates.Any(candidate => candidate.TargetId == UnsuspendedTargetId), "permission includes unsuspended target");
}

async Task AttackDeclarationCandidatesAreDeterministic()
{
    DcgoMatch first = await CreateConfiguredMatchAsync();
    DcgoMatch second = await CreateConfiguredMatchAsync();

    string firstSnapshot = SnapshotDeclarations(first);
    string secondSnapshot = SnapshotDeclarations(second);

    AssertEqual(firstSnapshot, secondSnapshot, "repeated declaration snapshot");
    AssertContains(firstSnapshot, "Player:<direct>", "direct candidate");
    AssertContains(firstSnapshot, $"Digimon:{TargetId.Value}", "digimon candidate");
}

Task AttackDeclarationSourceHasNoPlaceholderOrUnityDependency()
{
    string path = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "AttackPermanentAction.cs");
    string text = File.ReadAllText(path);

    AssertFalse(text.Contains("TODO", StringComparison.OrdinalIgnoreCase), "source must not contain TODO");
    AssertFalse(text.Contains("NotImplementedException", StringComparison.Ordinal), "source must not contain NotImplementedException");
    AssertFalse(text.Contains("UnityEngine", StringComparison.Ordinal), "source must not reference UnityEngine");
    AssertFalse(text.Contains("MonoBehaviour", StringComparison.Ordinal), "source must not reference MonoBehaviour");
    AssertContains(text, "GetAttackDeclarations", "declaration API");
    AssertContains(text, "AttackTargetCandidate", "candidate model");
    AssertContains(text, "AttackTargetKind", "candidate kind");
    return Task.CompletedTask;
}

AttackDeclaration SingleDeclaration(DcgoMatch match)
{
    AttackDeclaration[] declarations = new AttackPermanentAction()
        .GetAttackDeclarations(match.Context, Player)
        .Where(declaration => declaration.AttackerId == AttackerId)
        .ToArray();
    AssertEqual(1, declarations.Length, "declaration count");
    return declarations[0];
}

async Task<DcgoMatch> CreateConfiguredMatchAsync(
    IReadOnlyDictionary<string, object?>? attackerMetadata = null,
    IReadOnlyDictionary<string, object?>? secondAttackerMetadata = null)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 67);
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
        firstPlayerId: Player);

    await match.InitializeAsync(MatchConfig.Create(players, randomSeed: 67, setup: setup));
    await AdvanceToMainAsync(match, Player);
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Player, AttackerId, ChoiceZone.Hand, ChoiceZone.BattleArea));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Player, SecondAttackerId, ChoiceZone.Hand, ChoiceZone.BattleArea));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Opponent, TargetId, ChoiceZone.Hand, ChoiceZone.BattleArea));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Opponent, UnsuspendedTargetId, ChoiceZone.Hand, ChoiceZone.BattleArea));

    SetMetadata(match, AttackerId, attackerMetadata ?? new Dictionary<string, object?> { ["isSuspended"] = false });
    SetMetadata(match, SecondAttackerId, secondAttackerMetadata ?? new Dictionary<string, object?> { ["isSuspended"] = true });
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

string SnapshotDeclarations(DcgoMatch match)
{
    return string.Join(
        "|",
        new AttackPermanentAction()
            .GetAttackDeclarations(match.Context, Player)
            .OrderBy(declaration => declaration.AttackerId.Value, StringComparer.Ordinal)
            .Select(declaration => $"{declaration.AttackerId.Value}={string.Join(",", declaration.TargetCandidates.Select(DescribeCandidate))}"));
}

string DescribeCandidate(AttackTargetCandidate candidate)
{
    return $"{candidate.Kind}:{candidate.TargetId?.Value ?? "<direct>"}";
}

string DescribeAction(LegalAction action)
{
    HeadlessEntityId attackerId = ReadEntityId(action.Parameters, HeadlessActionParameterKeys.AttackerId);
    HeadlessEntityId targetId = ReadEntityId(action.Parameters, HeadlessActionParameterKeys.AttackTargetId);
    bool isDirect = ReadBool(action.Parameters, HeadlessActionParameterKeys.IsDirectAttack);
    return $"{action.Id.Value}:{attackerId.Value}:{targetId.Value}:{isDirect}";
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
