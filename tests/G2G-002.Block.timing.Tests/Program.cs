using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();
HeadlessPlayerId Player = new(1);
HeadlessPlayerId Opponent = new(2);
HeadlessEntityId AttackerId = new("p1:main:001:P1-M01");
HeadlessEntityId InitialTargetId = new("p2:main:001:P2-M01");
HeadlessEntityId BlockerId = new("p2:main:002:P2-M02");
HeadlessEntityId SuspendedBlockerId = new("p2:main:003:P2-M03");
HeadlessEntityId NonBlockerId = new("p2:main:004:P2-M04");

var tests = new (string Name, Func<Task> Body)[]
{
    ("G2G-002 goal row and predecessors are satisfied", GoalRowAndPredecessorsAreSatisfied),
    ("AS-IS block timing references are recorded", AsIsBlockTimingReferencesAreRecorded),
    ("Block timing exposes legal blocker candidates only", BlockTimingExposesLegalBlockersOnly),
    ("Block timing opens skippable blocker choice for defender", BlockTimingOpensSkippableChoice),
    ("Collision attack requires block selection and grants blocker candidates", CollisionAttackRequiresBlockSelection),
    ("Resolving block choice selects blocker and rewrites attack target", ResolvingBlockChoiceSelectsBlocker),
    ("Skipping block choice leaves attack target unchanged", SkippingBlockChoiceLeavesAttackTargetUnchanged),
    ("Block timing rejects missing pending attack", BlockTimingRejectsMissingPendingAttack),
    ("Invalid blocker selection fails without mutating attack state", InvalidBlockerSelectionFailsWithoutMutation),
    ("Block timing candidates are deterministic and source is scoped", BlockTimingCandidatesAreDeterministicAndSourceScoped),
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

Task GoalRowAndPredecessorsAreSatisfied()
{
    var rows = ReadCsv(Path.Combine(root, "docs", "headless_complete_goal_breakdown.csv"));
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G2G-002")
        ?? throw new InvalidOperationException("G2G-002 row was not found.");

    AssertEqual("Phase 2", Value(row, "phase"), "phase");
    AssertEqual("AttackProcess", Value(row, "area"), "area");
    AssertContains(Value(row, "goal"), "Block timing", "goal");
    AssertContains(Value(row, "scope"), "block timing", "scope");
    AssertContains(Value(row, "scope"), "blocker", "scope blocker");
    AssertEqual("block timing", Value(row, "deliverables"), "deliverables");
    AssertContains(Value(row, "unit_test_scope"), "block choice", "unit_test_scope");
    AssertEqual("docs/test-results/goals/G2G-002_block_timing_unit_test_results.md", Value(row, "result_document"), "result_document");
    AssertEqual("G2G-001; G1E-005", Value(row, "blocked_until"), "blocked_until");

    AssertComplete("G2G-001_attack_declaration_targets_unit_test_results.md");
    AssertComplete("G1E-005_choice_pause_resume_unit_test_results.md");
    return Task.CompletedTask;
}

Task AsIsBlockTimingReferencesAreRecorded()
{
    string attackProcess = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "AttackProcess.cs"));
    string permanent = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "Permanent.cs"));
    string selectPermanent = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "SelectPermanentEffect.cs"));

    AssertContains(attackProcess, "IEnumerator BlockTiming()", "AS-IS BlockTiming entry");
    AssertContains(attackProcess, "CanSelectBlockerCondition", "AS-IS blocker predicate");
    AssertContains(attackProcess, "permanent.HasBlocker", "AS-IS HasBlocker");
    AssertContains(attackProcess, "permanent.CanBlock", "AS-IS CanBlock");
    AssertContains(attackProcess, "canNoSelect: !AttackingPermanent.HasCollision", "AS-IS collision no-skip rule");
    AssertContains(attackProcess, "SwitchDefender(null, true, selectedPermanent)", "AS-IS switch defender on block");
    AssertContains(permanent, "public bool CanBlock(Permanent AttackingPermanent)", "AS-IS CanBlock method");
    AssertContains(selectPermanent, "canNoSelect", "AS-IS choice skip support");
    return Task.CompletedTask;
}

async Task BlockTimingExposesLegalBlockersOnly()
{
    DcgoMatch match = await CreateConfiguredMatchAsync();
    await DeclareDirectAttackAsync(match);

    BlockerCandidate[] candidates = new BlockTiming().GetBlockerCandidates(match.Context).ToArray();

    AssertEqual(1, candidates.Length, "candidate count");
    AssertEqual(BlockerId, candidates[0].BlockerId, "blocker id");
    AssertEqual(new HeadlessEntityId("P2-M02"), candidates[0].BlockerDefinitionId, "blocker definition");
    AssertEqual(Opponent, candidates[0].PlayerId, "blocker owner");
}

async Task BlockTimingOpensSkippableChoice()
{
    DcgoMatch match = await CreateConfiguredMatchAsync();
    await DeclareDirectAttackAsync(match);

    BlockTimingResult result = new BlockTiming().RequestBlockChoice(match.Context);

    AssertTrue(result.IsSuccess, "request success");
    AssertTrue(result.ChoiceRequested, "choice requested");
    AssertEqual(1, result.Candidates.Count, "candidate count");
    AssertTrue(match.Context.ChoiceController.Current.IsPending, "choice pending");
    AssertEqual(ChoiceType.Blocker, match.Context.ChoiceController.Current.Type, "choice type");
    AssertEqual(Opponent, match.Context.ChoiceController.Current.PlayerId!.Value, "choice player");
    AssertEqual(0, match.Context.ChoiceController.Current.MinCount, "min count");
    AssertEqual(1, match.Context.ChoiceController.Current.MaxCount, "max count");
    AssertTrue(match.Context.ChoiceController.Current.CanSkip, "can skip");
    AssertEqual(BlockerId, match.Context.ChoiceController.PendingRequest!.Candidates[0].Id, "choice candidate");
}

async Task CollisionAttackRequiresBlockSelection()
{
    DcgoMatch match = await CreateConfiguredMatchAsync(
        attackerMetadata: new Dictionary<string, object?> { [BlockTiming.HasCollisionKey] = true },
        blockerMetadata: new Dictionary<string, object?>());
    await DeclareDirectAttackAsync(match);

    BlockTimingResult result = new BlockTiming().RequestBlockChoice(match.Context);

    AssertTrue(result.IsSuccess, "request success");
    AssertTrue(result.Candidates.Count >= 1, "collision-granted candidate count");
    AssertTrue(result.Candidates.Any(candidate => candidate.BlockerId == BlockerId), "collision blocker id");
    AssertFalse(match.Context.ChoiceController.Current.CanSkip, "collision cannot skip");
    AssertEqual(1, match.Context.ChoiceController.Current.MinCount, "collision min count");
}

async Task ResolvingBlockChoiceSelectsBlocker()
{
    DcgoMatch match = await CreateConfiguredMatchAsync();
    await DeclareDirectAttackAsync(match);
    var timing = new BlockTiming();
    timing.RequestBlockChoice(match.Context);

    BlockTimingResult result = timing.ResolveBlockChoice(match.Context, ChoiceResult.Select(BlockerId));
    HeadlessAttackState attack = match.Context.AttackController.Current;

    AssertTrue(result.IsSuccess, "resolve success");
    AssertTrue(result.ChoiceResolved, "choice resolved");
    AssertFalse(result.IsSkipped, "not skipped");
    AssertEqual(BlockerId, result.BlockerId, "result blocker id");
    AssertEqual(BlockerId, attack.BlockerId, "state blocker id");
    AssertEqual(BlockerId, attack.TargetId, "state target switched to blocker");
    AssertTrue(attack.IsBlocked, "state is blocked");
    AssertFalse(attack.IsDirectAttack, "blocked attack is not direct");
    AssertTrue(attack.IsPending, "attack remains pending");
    AssertTrue(match.Context.ChoiceController.Current.IsResolved, "choice state resolved");
}

async Task SkippingBlockChoiceLeavesAttackTargetUnchanged()
{
    DcgoMatch match = await CreateConfiguredMatchAsync();
    await DeclareDirectAttackAsync(match);
    var timing = new BlockTiming();
    timing.RequestBlockChoice(match.Context);

    BlockTimingResult result = timing.ResolveBlockChoice(match.Context, ChoiceResult.Skip());
    HeadlessAttackState attack = match.Context.AttackController.Current;

    AssertTrue(result.IsSuccess, "resolve success");
    AssertTrue(result.IsSkipped, "skipped");
    AssertEqual(null, result.BlockerId, "result blocker id");
    AssertEqual(null, attack.BlockerId, "state blocker id");
    AssertEqual(null, attack.TargetId, "target remains direct");
    AssertFalse(attack.IsBlocked, "not blocked");
    AssertTrue(attack.IsDirectAttack, "still direct");
}

async Task BlockTimingRejectsMissingPendingAttack()
{
    DcgoMatch match = await CreateConfiguredMatchAsync();

    BlockTimingResult result = new BlockTiming().RequestBlockChoice(match.Context);

    AssertFalse(result.IsSuccess, "request failure");
    AssertContains(result.FailureReason, "pending attack", "failure reason");
    AssertFalse(match.Context.ChoiceController.Current.IsPending, "choice not pending");
}

async Task InvalidBlockerSelectionFailsWithoutMutation()
{
    DcgoMatch match = await CreateConfiguredMatchAsync();
    await DeclareDirectAttackAsync(match);
    var timing = new BlockTiming();
    timing.RequestBlockChoice(match.Context);
    string before = SnapshotAttack(match);

    BlockTimingResult result = timing.ResolveBlockChoice(match.Context, ChoiceResult.Select(NonBlockerId));

    AssertFalse(result.IsSuccess, "resolve failure");
    AssertContains(result.FailureReason, "not a selectable candidate", "failure reason");
    AssertEqual(before, SnapshotAttack(match), "attack unchanged");
    AssertTrue(match.Context.ChoiceController.Current.IsPending, "choice remains pending");
}

async Task BlockTimingCandidatesAreDeterministicAndSourceScoped()
{
    DcgoMatch first = await CreateConfiguredMatchAsync();
    DcgoMatch second = await CreateConfiguredMatchAsync();
    await DeclareDirectAttackAsync(first);
    await DeclareDirectAttackAsync(second);
    string firstSnapshot = SnapshotCandidates(first);
    string secondSnapshot = SnapshotCandidates(second);

    AssertEqual(firstSnapshot, secondSnapshot, "candidate snapshot");
    AssertEqual(BlockerId.Value, firstSnapshot, "candidate id");

    string blockTimingPath = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "BlockTiming.cs");
    string attackStatePath = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "HeadlessAttackState.cs");
    string blockText = File.ReadAllText(blockTimingPath);
    string attackText = File.ReadAllText(attackStatePath);

    AssertFalse(blockText.Contains("TODO", StringComparison.OrdinalIgnoreCase), "BlockTiming must not contain TODO");
    AssertFalse(blockText.Contains("UnityEngine", StringComparison.Ordinal), "BlockTiming must not reference UnityEngine");
    AssertFalse(blockText.Contains("MonoBehaviour", StringComparison.Ordinal), "BlockTiming must not reference MonoBehaviour");
    AssertContains(blockText, "RequestBlockChoice", "block choice API");
    AssertContains(blockText, "ResolveBlockChoice", "block resolve API");
    AssertContains(attackText, "BlockerId", "attack state blocker");
}

Task DeclareDirectAttackAsync(DcgoMatch match)
{
    // Declare directly on the controller so the common loop (G3.5-005) does not auto-advance the
    // attack; this keeps BlockTiming under isolated test exactly as in Phase 2.
    match.Context.AttackController.DeclareAttack(Player, AttackerId, Opponent, targetId: null, isDirectAttack: true);
    return Task.CompletedTask;
}

async Task<DcgoMatch> CreateConfiguredMatchAsync(
    IReadOnlyDictionary<string, object?>? attackerMetadata = null,
    IReadOnlyDictionary<string, object?>? blockerMetadata = null)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 71);
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
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { BuildDeck(Player, "P1"), BuildDeck(Opponent, "P2") },
        firstPlayerId: Player);

    await match.InitializeAsync(MatchConfig.Create(new[] { Player, Opponent }, randomSeed: 71, setup: setup));
    await AdvanceToMainAsync(match, Player);
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Player, AttackerId, ChoiceZone.Hand, ChoiceZone.BattleArea));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Opponent, InitialTargetId, ChoiceZone.Hand, ChoiceZone.BattleArea));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Opponent, BlockerId, ChoiceZone.Hand, ChoiceZone.BattleArea));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Opponent, SuspendedBlockerId, ChoiceZone.Hand, ChoiceZone.BattleArea));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Opponent, NonBlockerId, ChoiceZone.Hand, ChoiceZone.BattleArea));

    SetMetadata(match, AttackerId, attackerMetadata ?? new Dictionary<string, object?> { ["isSuspended"] = false });
    SetMetadata(match, InitialTargetId, new Dictionary<string, object?> { ["isSuspended"] = true, [BlockTiming.HasBlockerKey] = true });
    SetMetadata(match, BlockerId, blockerMetadata ?? new Dictionary<string, object?> { ["isSuspended"] = false, [BlockTiming.HasBlockerKey] = true });
    SetMetadata(match, SuspendedBlockerId, new Dictionary<string, object?> { ["isSuspended"] = true, [BlockTiming.HasBlockerKey] = true });
    SetMetadata(match, NonBlockerId, new Dictionary<string, object?> { ["isSuspended"] = false });
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

static LegalAction SingleLegalAction(DcgoMatch match, HeadlessPlayerId playerId, string actionType)
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

string SnapshotCandidates(DcgoMatch match)
{
    return string.Join(
        ",",
        new BlockTiming()
            .GetBlockerCandidates(match.Context)
            .Select(candidate => candidate.BlockerId.Value));
}

string SnapshotAttack(DcgoMatch match)
{
    HeadlessAttackState attack = match.Context.AttackController.Current;
    return $"{attack.AttackCount}:{attack.IsPending}:{attack.AttackerId?.Value}:{attack.TargetId?.Value}:{attack.BlockerId?.Value}:{attack.IsBlocked}:{attack.IsDirectAttack}";
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
