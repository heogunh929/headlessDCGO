using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G2G-003: battle DP deletion. The higher-DP combatant survives, the loser lands in Trash; equal DP deletes
// both. Mirrors AS-IS CardController.IBattle (AttackingPermanent.DP - DefendingPermanent.DP) -> LoserPermanents.
//
// (4b A'-2) PUMP-NATIVE REWRITE. The OLD-ctor `new DcgoMatch` + AdvanceToMain (AdvancePhase/EndTurn currency)
// + manual `new BattleResolver().ResolveAsync` result-object seam is replaced for the battle-OUTCOME tests by
// CreatePumpDriven + the pump DeclareAttack legal lane driven to AUTO-RESOLUTION, with the deletion re-sourced
// from the RESULT-OBJECT diagnostics (BattleResolutionResult.AttackerDeleted/DefenderDeleted/DeletedCardIds) to
// the equivalent, stronger ZONE OBSERVATION (loser landed in Trash) + the persistent DeletedByBattleKey /
// DpBeforeBattleKey metadata (A-1 C12/W5 re-source precedent). AdvancePhase/EndTurn action currency removed
// (currency = 0), OLD default-ctor removed. The BattleResolver DEFENSIVE-GUARD + determinism subtests keep a
// direct `new BattleResolver().ResolveAsync` call — BattleResolver is RETAINED substrate (the pump itself calls
// it), so directly unit-testing its rejection contract (which has no pump auto-resolution equivalent — the pump
// routes a direct attack to security and never presents a DP-less / non-Digimon battle to the resolver) is the
// D1/D2 retained-substrate disposition; those matches are built pump-driven (no AdvancePhase currency) and the
// attack is declared straight on the retained AttackController. All assertion intent is preserved.
var root = FindRepositoryRoot();
HeadlessPlayerId Player = new(1);
HeadlessPlayerId Opponent = new(2);
HeadlessEntityId AttackerId = new("p1:main:001:P1-M01");
HeadlessEntityId TargetId = new("p2:main:001:P2-M01");
HeadlessEntityId BlockerId = new("p2:main:002:P2-M02");

var tests = new (string Name, Func<Task> Body)[]
{
    ("G2G-003 goal row and predecessor are satisfied", GoalRowAndPredecessorAreSatisfied),
    ("AS-IS battle DP deletion references are recorded", AsIsBattleDeletionReferencesAreRecorded),
    ("Higher attacker DP deletes defender and resolves attack", HigherAttackerDpDeletesDefender),
    ("Higher defender DP deletes attacker and resolves attack", HigherDefenderDpDeletesAttacker),
    ("Equal DP deletes both battle participants", EqualDpDeletesBoth),
    ("Blocked attack resolves battle against selected blocker", BlockedAttackUsesSelectedBlocker),
    ("Direct attack is rejected without zone mutation", DirectAttackIsRejectedWithoutMutation),
    ("Missing DP is rejected without zone mutation", MissingDpIsRejectedWithoutMutation),
    ("Non-Digimon participant is rejected without zone mutation", NonDigimonParticipantIsRejectedWithoutMutation),
    ("Battle resolver is deterministic and source scoped", BattleResolverIsDeterministicAndSourceScoped),
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
    var row = rows.SingleOrDefault(r => Value(r, "goal_id") == "G2G-003")
        ?? throw new InvalidOperationException("G2G-003 row was not found.");

    AssertEqual("Phase 2", Value(row, "phase"), "phase");
    AssertEqual("AttackProcess", Value(row, "area"), "area");
    AssertContains(Value(row, "goal"), "Battle DP deletion", "goal");
    AssertContains(Value(row, "scope"), "DP", "scope DP");
    AssertContains(Value(row, "scope"), "battle deletion", "scope deletion");
    AssertEqual("battle resolver", Value(row, "deliverables"), "deliverables");
    AssertContains(Value(row, "unit_test_scope"), "battle DP deletion", "unit_test_scope");
    AssertEqual("docs/test-results/goals/G2G-003_battle_dp_deletion_unit_test_results.md", Value(row, "result_document"), "result_document");
    AssertEqual("G2G-002", Value(row, "blocked_until"), "blocked_until");

    AssertComplete("G2G-002_block_timing_unit_test_results.md");
    return Task.CompletedTask;
}

Task AsIsBattleDeletionReferencesAreRecorded()
{
    string attackProcess = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "AttackProcess.cs"));
    string cardController = File.ReadAllText(Path.Combine(root, "DCGO", "Assets", "Scripts", "Script", "CardController.cs"));

    AssertContains(attackProcess, "DetermineAttackOutcome", "AS-IS attack outcome");
    AssertContains(attackProcess, "new IBattle(AttackingPermanent: AttackingPermanent, DefendingPermanent: DefendingPermanent, null)", "AS-IS battle call");
    AssertContains(cardController, "public class IBattle", "AS-IS battle class");
    AssertContains(cardController, "AttackingPermanent.DP - DefendingPermanent.DP", "AS-IS DP comparison");
    AssertContains(cardController, "LoserPermanents.Add(DefendingPermanent)", "AS-IS defender deletion");
    AssertContains(cardController, "LoserPermanents.Add(AttackingPermanent)", "AS-IS attacker deletion");
    AssertContains(cardController, "DestroyPermanentsClass destoryBattlePermanents", "AS-IS battle destroy handoff");
    AssertContains(cardController, "CardObjectController.AddTrashCard(topCard)", "AS-IS trash movement");
    return Task.CompletedTask;
}

// (re-source) result.IsSuccess + AttackerDp/DefenderDp reads + AttackerDeleted=false/DefenderDeleted=true +
// DeletedCardIds=[Target] + attack IsResolved -> the pump auto-resolved the target attack: the DEFENDER landed
// in Trash (deleted), the higher-DP attacker stayed in the battle area, the loser carries DeletedByBattleKey +
// its pre-battle DP, and the attack cleared (AttackPhase.None => resolved / not pending).
async Task HigherAttackerDpDeletesDefender()
{
    DcgoMatch match = await CreateMatchAsync(attackerDp: 9000, targetDp: 7000);
    await DriveTargetAttackAsync(match);

    AssertEqual(AttackPhase.None, match.Context.AttackController.Current.Phase, "attack resolved and cleared");
    AssertFalse(match.HasPendingChoice(), "no pending choice for unblockable target attack");
    AssertZoneContains(match, Player, ChoiceZone.BattleArea, AttackerId, "attacker remains in battle");
    AssertZoneContains(match, Opponent, ChoiceZone.Trash, TargetId, "defender moved to trash");
    AssertFalse(InZone(match, Player, ChoiceZone.Trash, AttackerId), "attacker was NOT deleted");
    AssertMetadata(match, TargetId, BattleResolver.DeletedByBattleKey, true);
    AssertMetadata(match, TargetId, BattleResolver.DpBeforeBattleKey, 7000);
}

// (re-source) the mirror direction (result.AttackerDeleted=true/DefenderDeleted=false): a weaker attacker is
// deleted and the stronger target survives — this pair is mutually non-vacuous (proves the pump reads DP, no
// separate control needed).
async Task HigherDefenderDpDeletesAttacker()
{
    DcgoMatch match = await CreateMatchAsync(attackerDp: 5000, targetDp: 8000);
    await DriveTargetAttackAsync(match);

    AssertEqual(AttackPhase.None, match.Context.AttackController.Current.Phase, "attack resolved and cleared");
    AssertZoneContains(match, Player, ChoiceZone.Trash, AttackerId, "attacker moved to trash");
    AssertZoneContains(match, Opponent, ChoiceZone.BattleArea, TargetId, "defender remains in battle");
    AssertFalse(InZone(match, Opponent, ChoiceZone.Trash, TargetId), "defender was NOT deleted");
    AssertMetadata(match, AttackerId, BattleResolver.DeletedByBattleKey, true);
    AssertMetadata(match, AttackerId, BattleResolver.DpBeforeBattleKey, 5000);
}

// (re-source) result.AttackerDeleted && DefenderDeleted + MovementResults.Count == 2 -> both combatants land in
// Trash on an equal-DP clash.
async Task EqualDpDeletesBoth()
{
    DcgoMatch match = await CreateMatchAsync(attackerDp: 6000, targetDp: 6000);
    await DriveTargetAttackAsync(match);

    AssertEqual(AttackPhase.None, match.Context.AttackController.Current.Phase, "attack resolved and cleared");
    AssertZoneContains(match, Player, ChoiceZone.Trash, AttackerId, "attacker moved to trash");
    AssertZoneContains(match, Opponent, ChoiceZone.Trash, TargetId, "defender moved to trash");
}

// (re-source) result.AttackerDeleted (deleted by the blocker) + DefenderDeleted=false + original target
// unaffected -> the pump pauses for the block choice, selecting the stronger blocker redirects the battle to it,
// the attacker is deleted, the blocker survives, and the original target is untouched.
async Task BlockedAttackUsesSelectedBlocker()
{
    DcgoMatch match = await CreateMatchAsync(attackerDp: 9000, targetDp: 3000, withBlocker: true, blockerDp: 12000);
    await ExpApply(match, TargetAttackLane(match, AttackerId, TargetId));
    await ExpStepUntilPending(match);

    AssertTrue(match.HasPendingChoice(), "attack pauses for the block choice");
    AssertEqual(AttackPhase.Blocking, match.Context.AttackController.Current.Phase, "attack parked in blocking phase");

    await ExpResolveSelecting(match, BlockerId);
    await ExpDriveUntil(match, m => m.Context.AttackController.Current.Phase == AttackPhase.None || m.IsTerminal());

    AssertEqual(AttackPhase.None, match.Context.AttackController.Current.Phase, "attack cleared after blocked battle");
    AssertZoneContains(match, Player, ChoiceZone.Trash, AttackerId, "attacker moved to trash");
    AssertZoneContains(match, Opponent, ChoiceZone.BattleArea, BlockerId, "blocker remains in battle");
    AssertZoneContains(match, Opponent, ChoiceZone.BattleArea, TargetId, "original target unaffected");
}

// GUARD (retained substrate): BattleResolver refuses to resolve a DIRECT attack as a battle. Under the pump a
// direct attack routes to security (never to BattleResolver), so this rejection contract has no pump
// auto-resolution equivalent — it is a defensive BattleResolver API unit, exercised directly. The match is
// pump-built and the attack declared straight on the retained AttackController (no AdvancePhase currency).
async Task DirectAttackIsRejectedWithoutMutation()
{
    DcgoMatch match = await CreateResolverMatchAsync(attackerDp: 9000, targetDp: 7000);
    match.Context.AttackController.DeclareAttack(Player, AttackerId, Opponent, TargetId, isDirectAttack: true);
    string before = SnapshotZones(match);

    BattleResolutionResult result = await new BattleResolver().ResolveAsync(match.Context);

    AssertFalse(result.IsSuccess, "resolve failure");
    AssertContains(result.FailureReason, "non-direct", "failure reason");
    AssertEqual(before, SnapshotZones(match), "zones unchanged");
    AssertTrue(match.Context.AttackController.Current.IsPending, "attack remains pending");
}

// GUARD (retained substrate): BattleResolver refuses to resolve a battle whose attacker has no battle DP.
async Task MissingDpIsRejectedWithoutMutation()
{
    DcgoMatch match = await CreateResolverMatchAsync(attackerDp: null, targetDp: 7000);
    match.Context.AttackController.DeclareAttack(Player, AttackerId, Opponent, TargetId, isDirectAttack: false);
    string before = SnapshotZones(match);

    BattleResolutionResult result = await new BattleResolver().ResolveAsync(match.Context);

    AssertFalse(result.IsSuccess, "resolve failure");
    AssertContains(result.FailureReason, "has no battle DP", "failure reason");
    AssertEqual(before, SnapshotZones(match), "zones unchanged");
    AssertTrue(match.Context.AttackController.Current.IsPending, "attack remains pending");
}

// GUARD (retained substrate): BattleResolver refuses to battle a non-Digimon participant.
async Task NonDigimonParticipantIsRejectedWithoutMutation()
{
    DcgoMatch match = await CreateResolverMatchAsync(attackerDp: 9000, targetDp: 7000, targetCardType: "Option");
    HeadlessAttackState beforeAttack = match.Context.AttackController.DeclareAttack(
        Player,
        AttackerId,
        Opponent,
        TargetId,
        isDirectAttack: false);
    string before = SnapshotZones(match);

    BattleResolutionResult result = await new BattleResolver().ResolveAsync(match.Context);

    AssertFalse(result.IsSuccess, "resolve failure");
    AssertContains(result.FailureReason, "not a Digimon", "failure reason");
    AssertEqual(before, SnapshotZones(match), "zones unchanged");
    AssertEqual(beforeAttack, match.Context.AttackController.Current, "attack unchanged");
}

// GUARD (retained substrate): the resolver is deterministic (identical staged battles -> identical outcome) and
// its source is substrate-clean.
async Task BattleResolverIsDeterministicAndSourceScoped()
{
    DcgoMatch first = await CreateResolverMatchAsync(attackerDp: 6000, targetDp: 6000);
    DcgoMatch second = await CreateResolverMatchAsync(attackerDp: 6000, targetDp: 6000);
    first.Context.AttackController.DeclareAttack(Player, AttackerId, Opponent, TargetId, isDirectAttack: false);
    second.Context.AttackController.DeclareAttack(Player, AttackerId, Opponent, TargetId, isDirectAttack: false);

    BattleResolutionResult firstResult = await new BattleResolver().ResolveAsync(first.Context);
    BattleResolutionResult secondResult = await new BattleResolver().ResolveAsync(second.Context);

    AssertEqual(SnapshotResult(firstResult), SnapshotResult(secondResult), "result snapshot");

    string resolverPath = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "BattleResolver.cs");
    string resolverText = File.ReadAllText(resolverPath);
    AssertFalse(resolverText.Contains("TODO", StringComparison.OrdinalIgnoreCase), "BattleResolver must not contain TODO");
    AssertFalse(resolverText.Contains("UnityEngine", StringComparison.Ordinal), "BattleResolver must not reference UnityEngine");
    AssertFalse(resolverText.Contains("MonoBehaviour", StringComparison.Ordinal), "BattleResolver must not reference MonoBehaviour");
    AssertContains(resolverText, "ResolveAsync", "resolver API");
    AssertContains(resolverText, "ChoiceZone.Trash", "battle deletion moves to trash");
    AssertContains(resolverText, "ResolveAttack", "attack resolution");
}

// === Harness (pump, G3.5-005 F68 idiom) =================================================================

async Task DriveTargetAttackAsync(DcgoMatch match)
{
    await ExpApply(match, TargetAttackLane(match, AttackerId, TargetId));
    await ExpDriveUntil(match, m => m.Context.AttackController.Current.Phase == AttackPhase.None || m.IsTerminal());
}

// A pump-driven match at P1's main wait with the combatants staged as live battle-area synthetic Digimon.
async Task<DcgoMatch> CreateMatchAsync(int? attackerDp, int? targetDp, bool withBlocker = false, int? blockerDp = 5000)
{
    DcgoMatch match = await CreatePumpMatchAsync();
    StagePermanent(match, Player, AttackerId, attackerDp, suspended: false, isBlocker: false, cardType: "Digimon");
    StagePermanent(match, Opponent, TargetId, targetDp, suspended: true, isBlocker: false, cardType: "Digimon");
    if (withBlocker)
    {
        StagePermanent(match, Opponent, BlockerId, blockerDp, suspended: false, isBlocker: true, cardType: "Digimon");
    }

    return match;
}

// A pump-driven match with the combatants staged, for a DIRECT retained-substrate BattleResolver unit call
// (the attack is declared straight on the AttackController by the caller).
async Task<DcgoMatch> CreateResolverMatchAsync(int? attackerDp, int? targetDp, string targetCardType = "Digimon")
{
    DcgoMatch match = await CreatePumpMatchAsync();
    StagePermanent(match, Player, AttackerId, attackerDp, suspended: false, isBlocker: false, cardType: "Digimon");
    StagePermanent(match, Opponent, TargetId, targetDp, suspended: true, isBlocker: false, cardType: targetCardType);
    StagePermanent(match, Opponent, BlockerId, 5000, suspended: false, isBlocker: true, cardType: "Digimon");
    return match;
}

async Task<DcgoMatch> CreatePumpMatchAsync()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 73);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(CreateDefinition($"P1-M{index:D2}", "Digimon"));
        cards.Upsert(CreateDefinition($"P2-M{index:D2}", "Digimon"));
    }

    DcgoMatch match = DcgoMatch.CreatePumpDriven(context, new EngineTrace());
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { BuildDeck(Player, "P1"), BuildDeck(Opponent, "P2") },
        firstPlayerId: Player, initialSecuritySize: 0, shuffleDecks: false, shuffleDigitamaDecks: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { Player, Opponent }, randomSeed: 73, setup: setup));
    await ExpStepOnce(match);
    await ExpDriveUntil(match, m => ExpAtMainWait(m, Player));
    return match;
}

// Stage a live battle-area synthetic combatant (F68 PlaceRealCard idiom): synthetic def with a def-level dp,
// instance carrying dp + isSuspended (+ HasBlockerKey for a blocker), moved None->BattleArea and registered.
void StagePermanent(DcgoMatch match, HeadlessPlayerId owner, HeadlessEntityId id, int? dp, bool suspended, bool isBlocker, string cardType)
{
    EngineContext ctx = match.Context;
    var defId = new HeadlessEntityId($"DEF:{id.Value}");
    var defMeta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["level"] = 5 };
    if (dp.HasValue) defMeta["dp"] = dp.Value;
    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(defId, id.Value, id.Value, defMeta, CardType: cardType));

    var instMeta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["isSuspended"] = suspended };
    if (dp.HasValue) instMeta[BattleResolver.DpKey] = dp.Value;
    if (isBlocker) instMeta[BlockTiming.HasBlockerKey] = true;
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner, Metadata: instMeta));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
    CardEffectRegistrar.RegisterCard(ctx, id, owner);
}

LegalAction TargetAttackLane(DcgoMatch match, HeadlessEntityId attacker, HeadlessEntityId target) =>
    ExpLegal(match, Player)
        .Where(a => a.ActionType == HeadlessActionTypes.DeclareAttack)
        .Where(a => ExpParamId(a, HeadlessActionParameterKeys.AttackerId) == attacker)
        .FirstOrDefault(a => ExpParamId(a, HeadlessActionParameterKeys.AttackTargetId) == target)
        ?? throw new InvalidOperationException("no target-attack lane for " + attacker.Value + " -> " + target.Value);

bool InZone(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)match.Context.ZoneMover).GetCards(player, zone).Contains(cardId);

async Task ExpStepUntilPending(DcgoMatch match)
{
    for (int i = 0; i < 32 && !match.HasPendingChoice() && !match.IsTerminal(); i++)
    {
        await ExpStepOnce(match);
    }
}

async Task ExpResolveSelecting(DcgoMatch match, HeadlessEntityId targetId)
{
    HeadlessPlayerId chooser = match.Context.ChoiceController.PendingRequest!.PlayerId;
    LegalAction action;
    using (AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context))
    {
        action = match.GetLegalActions(chooser)
            .First(a => a.ActionType == HeadlessActionTypes.ResolveChoice && ExpSelectedIds(a).Contains(targetId));
    }
    await ExpApply(match, action);
}

static IReadOnlyList<HeadlessEntityId> ExpSelectedIds(LegalAction action) =>
    action.Parameters.TryGetValue(HeadlessActionParameterKeys.ChoiceSelectedIds, out object? raw) && raw is IEnumerable<HeadlessEntityId> ids
        ? ids.ToArray()
        : Array.Empty<HeadlessEntityId>();

static IReadOnlyList<LegalAction> ExpLegal(DcgoMatch match, HeadlessPlayerId player)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    return match.GetLegalActions(player);
}

static HeadlessEntityId? ExpParamId(LegalAction action, string key) =>
    action.Parameters.TryGetValue(key, out object? raw) && raw is HeadlessEntityId id ? id : null;

static bool ExpAtMainWait(DcgoMatch match, HeadlessPlayerId player) =>
    match.Context.TurnController.Current.Phase == HeadlessPhase.Main
    && match.Context.TurnController.Current.TurnPlayerId == player
    && !match.HasPendingChoice()
    && !match.IsTerminal();

static async Task ExpStepOnce(DcgoMatch match)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.StepAsync();
}

async Task ExpApply(DcgoMatch match, LegalAction action)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.ApplyActionAsync(action);
    await match.StepAsync();
    await match.StepAsync();
}

async Task ExpDriveUntil(DcgoMatch match, Func<DcgoMatch, bool> condition)
{
    for (int i = 0; i < 96 && !condition(match); i++)
    {
        if (match.HasPendingChoice())
        {
            HeadlessPlayerId chooser = match.Context.ChoiceController.PendingRequest!.PlayerId;
            LegalAction? resolve;
            using (AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context))
            {
                resolve = match.GetLegalActions(chooser)
                    .FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice
                        && a.Id.Value.EndsWith(":skip", StringComparison.Ordinal))
                    ?? match.GetLegalActions(chooser).FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice);
            }
            if (resolve is null) { await ExpStepOnce(match); }
            else { await ExpApply(match, resolve); }
        }
        else
        {
            await ExpStepOnce(match);
        }
    }

    if (!condition(match))
    {
        HeadlessTurnState t = match.Context.TurnController.Current;
        throw new InvalidOperationException(
            $"EXP drive did not reach state — phase:{t.Phase}/{t.StepCursor} turn:{t.TurnNumber} player:{t.TurnPlayerId} " +
            $"attackPhase:{match.Context.AttackController.Current.Phase} pending:{match.HasPendingChoice()} terminal:{match.IsTerminal()}");
    }
}

static CardRecord CreateDefinition(string id, string cardType)
{
    return new CardRecord(
        new HeadlessEntityId(id),
        id,
        $"{id} Card",
        new Dictionary<string, object?>(),
        CardType: cardType);
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

void AssertMetadata(DcgoMatch match, HeadlessEntityId cardId, string key, object expected)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
    {
        throw new InvalidOperationException($"Missing card instance '{cardId}'.");
    }

    if (!record.Metadata.TryGetValue(key, out object? actual))
    {
        throw new InvalidOperationException($"Metadata '{key}' was not found on '{cardId}'.");
    }

    AssertEqual(expected, actual, $"metadata {key}");
}

void AssertZoneContains(DcgoMatch match, HeadlessPlayerId playerId, ChoiceZone zone, HeadlessEntityId cardId, string message)
{
    if (match.Context.ZoneMover is not IZoneStateReader zoneReader)
    {
        throw new InvalidOperationException("Zone reader missing.");
    }

    AssertTrue(zoneReader.GetCards(playerId, zone).Contains(cardId), message);
}

string SnapshotZones(DcgoMatch match)
{
    if (match.Context.ZoneMover is not IZoneStateReader zoneReader)
    {
        throw new InvalidOperationException("Zone reader missing.");
    }

    return string.Join(
        "|",
        new[] { Player, Opponent }.SelectMany(player =>
            new[] { ChoiceZone.BattleArea, ChoiceZone.Trash }.Select(zone =>
                $"{player.Value}:{zone}:{string.Join(",", zoneReader.GetCards(player, zone).Select(card => card.Value))}")));
}

static string SnapshotResult(BattleResolutionResult result)
{
    return string.Join(
        ":",
        result.IsSuccess,
        result.AttackerDp,
        result.DefenderDp,
        result.AttackerDeleted,
        result.DefenderDeleted,
        string.Join(",", result.DeletedCardIds.Select(card => card.Value)));
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

static IReadOnlyList<Dictionary<string, string>> ReadCsv(string path)
{
    string[] lines = File.ReadAllLines(path);
    string[] headers = ParseCsvLine(lines[0]).ToArray();
    return lines.Skip(1)
        .Where(line => !string.IsNullOrWhiteSpace(line))
        .Select(line =>
        {
            string[] values = ParseCsvLine(line).ToArray();
            var row = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int index = 0; index < headers.Length; index++)
            {
                row[headers[index]] = index < values.Length ? values[index] : string.Empty;
            }

            return row;
        })
        .ToArray();
}

static IEnumerable<string> ParseCsvLine(string line)
{
    var values = new List<string>();
    var current = new System.Text.StringBuilder();
    var inQuotes = false;

    for (var index = 0; index < line.Length; index++)
    {
        char ch = line[index];
        if (ch == '"')
        {
            if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
            {
                current.Append('"');
                index++;
            }
            else
            {
                inQuotes = !inQuotes;
            }
        }
        else if (ch == ',' && !inQuotes)
        {
            values.Add(current.ToString());
            current.Clear();
        }
        else
        {
            current.Append(ch);
        }
    }

    values.Add(current.ToString());
    return values;
}

static string Value(Dictionary<string, string> row, string key)
{
    return row.TryGetValue(key, out string? value) ? value : string.Empty;
}

static string FindRepositoryRoot()
{
    string directory = Directory.GetCurrentDirectory();
    while (!File.Exists(Path.Combine(directory, "docs", "headless_complete_goal_breakdown.csv")))
    {
        directory = Directory.GetParent(directory)?.FullName
            ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }

    return directory;
}

static void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{message}: expected '{expected}', actual '{actual}'.");
    }
}

static void AssertContains(string text, string expected, string message)
{
    if (!text.Contains(expected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"{message}: expected to contain '{expected}'.");
    }
}

static void AssertTrue(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertFalse(bool condition, string message)
{
    if (condition)
    {
        throw new InvalidOperationException(message);
    }
}
