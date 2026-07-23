using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using Cec = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

var root = FindRepositoryRoot();
HeadlessPlayerId Player = new(1);
HeadlessPlayerId Opponent = new(2);
HeadlessEntityId AttackerId = new("p1:main:001:P1-M01");
HeadlessEntityId TargetId = new("p2:main:001:P2-M01");
HeadlessEntityId BlockerId = new("p2:main:002:P2-M02");
HeadlessEntityId SecurityId = new("p2:main:006:P2-M06");

var tests = new (string Name, Func<Task> Body)[]
{
    ("Predecessor goal G3.5-004 is complete", PredecessorGoalsComplete),
    ("Target attack auto-resolves battle and clears the attack", TargetAttackAutoResolvesBattleAndClears),
    ("Target attack control: a weaker attacker dies, the stronger target survives", TargetAttackControlWeakerAttackerDies),
    ("Direct attack auto-resolves security and clears the attack", DirectAttackAutoResolvesSecurityAndClears),
    ("Blocked attack pauses for choice then resolves against blocker", BlockedAttackPausesThenResolvesAgainstBlocker),
    ("Skipped block resolves battle against the original target", SkippedBlockResolvesAgainstOriginalTarget),
    ("AttackPipeline advances one phase per step to completion", AttackPipelineAdvancesPhaseByPhase),
    ("AttackPipeline source is clean and wired into the flow", AttackPipelineSourceIsClean),
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

// === (4b B5-c5) pump combat harness — the F68 PlaceRealCard idiom generalized ===
// A staged permanent = synthetic Digimon def (def-level dp, CardType Digimon) + instance (dp + isSuspended) moved
// None->BattleArea and registered through CardEffectRegistrar. A blocker also carries the metadata HasBlockerKey
// (BlockTiming.cs:271 reads it). This is exactly the F68/EXEMPLAR StageSynthetic recipe.
HeadlessEntityId StagePermanent(DcgoMatch match, HeadlessPlayerId owner, HeadlessEntityId id, int? dp, bool suspended, bool isBlocker)
{
    EngineContext ctx = match.Context;
    var defId = new HeadlessEntityId($"DEF:{id.Value}");
    var defMeta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["level"] = 5 };
    if (dp.HasValue) defMeta["dp"] = dp.Value;
    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(defId, id.Value, id.Value, defMeta, CardType: "Digimon"));

    var instMeta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["isSuspended"] = suspended };
    if (dp.HasValue) instMeta[BattleResolver.DpKey] = dp.Value;
    if (isBlocker) instMeta[BlockTiming.HasBlockerKey] = true;
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner, Metadata: instMeta));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
    HeadlessDCGO.Engine.Headless.Runtime.CardEffectRegistrar.RegisterCard(ctx, id, owner);
    return id;
}

void StageSecurity(DcgoMatch match, HeadlessPlayerId owner, HeadlessEntityId id)
{
    EngineContext ctx = match.Context;
    // The pump StartGame deals its own security stack (AS-IS ~5 cards) regardless of the setup config, so clear
    // the dealt stack first — the security check must reveal exactly the staged card.
    var reader = (IZoneStateReader)ctx.ZoneMover;
    foreach (HeadlessEntityId dealt in reader.GetCards(owner, ChoiceZone.Security).ToArray())
    {
        ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, dealt, ChoiceZone.Security, ChoiceZone.Library)).GetAwaiter().GetResult();
    }

    var defId = new HeadlessEntityId($"DEF:{id.Value}");
    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(defId, id.Value, id.Value,
        new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.Security)).GetAwaiter().GetResult();
}

// Pump DeclareAttack seam: the legal target-attack lane (attacker -> target), not a direct attack.
LegalAction TargetAttackLane(DcgoMatch match, HeadlessEntityId attacker, HeadlessEntityId target) =>
    ExpLegal(match, Player)
        .Where(a => a.ActionType == HeadlessActionTypes.DeclareAttack)
        .Where(a => ExpParamId(a, HeadlessActionParameterKeys.AttackerId) == attacker)
        .FirstOrDefault(a => ExpParamId(a, HeadlessActionParameterKeys.AttackTargetId) == target)
        ?? throw new InvalidOperationException(
            "no target-attack lane: " + string.Join(", ", ExpLegal(match, Player)
                .Where(a => a.ActionType == HeadlessActionTypes.DeclareAttack)
                .Select(a => $"atk={ExpParamId(a, HeadlessActionParameterKeys.AttackerId)?.Value}/tgt={ExpParamId(a, HeadlessActionParameterKeys.AttackTargetId)?.Value}")));

// Pump DeclareAttack seam: the direct (targetless) attack lane.
LegalAction DirectAttackLane(DcgoMatch match, HeadlessEntityId attacker) =>
    ExpLegal(match, Player)
        .Where(a => a.ActionType == HeadlessActionTypes.DeclareAttack)
        .Where(a => ExpParamId(a, HeadlessActionParameterKeys.AttackerId) == attacker)
        .FirstOrDefault(a => ExpParamId(a, HeadlessActionParameterKeys.AttackTargetId) is null)
        ?? throw new InvalidOperationException("no direct-attack lane for " + attacker.Value);

bool InZoneById(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)match.Context.ZoneMover).GetCards(player, zone).Contains(cardId);

// Step (without auto-resolving) until a choice surfaces — used to REACH a block window (which the auto-resolving
// ExpDriveUntil would otherwise skip).
async Task ExpStepUntilPending(DcgoMatch match)
{
    for (int i = 0; i < 32 && !match.HasPendingChoice() && !match.IsTerminal(); i++)
    {
        await ExpStepOnce(match);
    }
}

// Resolve the pending choice by SELECTING a specific candidate (e.g. choosing the blocker).
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

// Resolve the pending choice by SKIPPING (declining the optional block).
async Task ExpResolveSkip(DcgoMatch match)
{
    HeadlessPlayerId chooser = match.Context.ChoiceController.PendingRequest!.PlayerId;
    LegalAction action;
    using (AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context))
    {
        action = match.GetLegalActions(chooser)
            .First(a => a.ActionType == HeadlessActionTypes.ResolveChoice && a.Id.Value.EndsWith(":skip", StringComparison.Ordinal));
    }
    await ExpApply(match, action);
}

static IReadOnlyList<HeadlessEntityId> ExpSelectedIds(LegalAction action) =>
    action.Parameters.TryGetValue(HeadlessActionParameterKeys.ChoiceSelectedIds, out object? raw) && raw is IEnumerable<HeadlessEntityId> ids
        ? ids.ToArray()
        : Array.Empty<HeadlessEntityId>();

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

static IReadOnlyList<LegalAction> ExpLegal(DcgoMatch match, HeadlessPlayerId player)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    return match.GetLegalActions(player);
}

static HeadlessEntityId? ExpParamId(LegalAction action, string key) =>
    action.Parameters.TryGetValue(key, out object? raw) && raw is HeadlessEntityId id ? id : null;

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
            $"attackPhase:{match.Context.AttackController.Current.Phase} pending:{match.HasPendingChoice()} terminal:{match.IsTerminal()} " +
            $"lanes:[{string.Join(", ", ExpLegal(match, t.TurnPlayerId ?? default).Select(a => a.ActionType))}]");
    }
}

Task PredecessorGoalsComplete()
{
    AssertComplete("G3.5-004_game_flow_processor_unit_test_results.md");
    return Task.CompletedTask;
}

// (4b B5-c5) RE-TARGETED to the pump: the OLD-ctor `new DcgoMatch` + direct AttackController.DeclareAttack seam
// is replaced by CreatePumpDriven + the pump DeclareAttack legal lane (F68 PlaceRealCard idiom: synthetic Digimon
// def with a def-level dp, instance dp, zone-register + CardEffectRegistrar). §3.4b P0 confirmed combat outcomes
// reproduce under the pump — the c4 "R1/R6-gated" verdict was a driver-mechanical misdiagnosis. Assertions are
// preserved; the AdvancePhase/EndTurn action currency is removed (currency = 0).
async Task TargetAttackAutoResolvesBattleAndClears()
{
    DcgoMatch match = await CreateMatchAsync(attackerDp: 9000, targetDp: 7000, withBlocker: false);
    await ExpApply(match, TargetAttackLane(match, AttackerId, TargetId));
    await ExpDriveUntil(match, m => m.Context.AttackController.Current.Phase == AttackPhase.None || m.IsTerminal());

    AssertFalse(match.HasPendingChoice(), "no pending choice for unblockable target attack");
    AssertEqual(AttackPhase.None, match.Context.AttackController.Current.Phase, "attack cleared after resolution");
    AssertZoneContains(match, Opponent, ChoiceZone.Trash, TargetId, "defender moved to trash");
    AssertZoneContains(match, Player, ChoiceZone.BattleArea, AttackerId, "attacker remains in battle");
    AssertMetadata(match, TargetId, BattleResolver.DeletedByBattleKey, true);
}

// (4b B5-c5 false-green guard) A delete-capable control: with a weaker attacker (5000) the STRONGER suspended
// target (9000) survives and is NOT trashed — proving the trash-landing assertion above is non-vacuous and the
// pump combat genuinely reads DP.
async Task TargetAttackControlWeakerAttackerDies()
{
    DcgoMatch match = await CreateMatchAsync(attackerDp: 5000, targetDp: 9000, withBlocker: false);
    await ExpApply(match, TargetAttackLane(match, AttackerId, TargetId));
    await ExpDriveUntil(match, m => m.Context.AttackController.Current.Phase == AttackPhase.None || m.IsTerminal());

    AssertZoneContains(match, Player, ChoiceZone.Trash, AttackerId, "the weaker attacker was deleted");
    AssertZoneContains(match, Opponent, ChoiceZone.BattleArea, TargetId, "the stronger target survived");
    AssertFalse(InZoneById(match, Opponent, ChoiceZone.Trash, TargetId), "the target was NOT trashed");
}

async Task DirectAttackAutoResolvesSecurityAndClears()
{
    DcgoMatch match = await CreateMatchAsync(attackerDp: 9000, targetDp: null, withBlocker: false, securityCount: 1);
    await ExpApply(match, DirectAttackLane(match, AttackerId));
    await ExpDriveUntil(match, m => m.Context.AttackController.Current.Phase == AttackPhase.None || m.IsTerminal());

    AssertFalse(match.HasPendingChoice(), "no pending choice for direct attack without blockers");
    AssertEqual(AttackPhase.None, match.Context.AttackController.Current.Phase, "attack cleared after security check");
    AssertZoneContains(match, Opponent, ChoiceZone.Trash, SecurityId, "checked security card moved to trash");
    AssertMetadata(match, SecurityId, SecurityResolver.CheckedBySecurityCheckKey, true);
}

async Task BlockedAttackPausesThenResolvesAgainstBlocker()
{
    DcgoMatch match = await CreateMatchAsync(attackerDp: 9000, targetDp: 3000, withBlocker: true, blockerDp: 12000);
    await ExpApply(match, TargetAttackLane(match, AttackerId, TargetId));
    await ExpStepUntilPending(match);

    AssertTrue(match.HasPendingChoice(), "attack pauses for the block choice");
    AssertEqual(AttackPhase.Blocking, match.Context.AttackController.Current.Phase, "attack parked in blocking phase");

    await ExpResolveSelecting(match, BlockerId);
    await ExpDriveUntil(match, m => m.Context.AttackController.Current.Phase == AttackPhase.None || m.IsTerminal());

    AssertFalse(match.HasPendingChoice(), "block choice resolved");
    AssertEqual(AttackPhase.None, match.Context.AttackController.Current.Phase, "attack cleared after blocked battle");
    AssertZoneContains(match, Player, ChoiceZone.Trash, AttackerId, "attacker deleted by stronger blocker");
    AssertZoneContains(match, Opponent, ChoiceZone.BattleArea, BlockerId, "blocker survives");
    AssertZoneContains(match, Opponent, ChoiceZone.BattleArea, TargetId, "original target untouched");
}

async Task SkippedBlockResolvesAgainstOriginalTarget()
{
    DcgoMatch match = await CreateMatchAsync(attackerDp: 9000, targetDp: 3000, withBlocker: true, blockerDp: 12000);
    await ExpApply(match, TargetAttackLane(match, AttackerId, TargetId));
    await ExpStepUntilPending(match);
    AssertTrue(match.HasPendingChoice(), "attack pauses for the block choice");

    await ExpResolveSkip(match);
    await ExpDriveUntil(match, m => m.Context.AttackController.Current.Phase == AttackPhase.None || m.IsTerminal());

    AssertFalse(match.HasPendingChoice(), "block choice skipped");
    AssertEqual(AttackPhase.None, match.Context.AttackController.Current.Phase, "attack cleared after unblocked battle");
    AssertZoneContains(match, Opponent, ChoiceZone.Trash, TargetId, "original target deleted by attacker");
    AssertZoneContains(match, Player, ChoiceZone.BattleArea, AttackerId, "attacker survives weaker target");
    AssertZoneContains(match, Opponent, ChoiceZone.BattleArea, BlockerId, "unused blocker remains");
}

async Task AttackPipelineAdvancesPhaseByPhase()
{
    // (MIG1) AS-IS-faithful setup: the attacker must be a LIVE battle-area Digimon — the AS-IS post-counter
    // boundary (AttackProcess.cs:301 `TopCard == null || !IsDigimon`) force-ends an attack whose attacker is
    // not a live Digimon, so the old bare-context (no card records) setup asserted an impossible state. With a
    // live attacker, the counter stage runs its AS-IS TWO passes (each parks one iteration for the cut-in drain).
    EngineContext context = EngineContext.CreateDefault(randomSeed: 11);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId("DEF:ATK"), "ATK", "Attacker",
        new Dictionary<string, object?> { ["dp"] = 5000 }, CardType: "Digimon"));
    cards.Upsert(new CardRecord(new HeadlessEntityId("DEF:TGT"), "TGT", "Target",
        new Dictionary<string, object?> { ["dp"] = 3000 }, CardType: "Digimon"));
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(AttackerId, new HeadlessEntityId("DEF:ATK"), Player,
        Metadata: new Dictionary<string, object?> { [BattleResolver.DpKey] = 5000 }));
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(TargetId, new HeadlessEntityId("DEF:TGT"), Opponent,
        Metadata: new Dictionary<string, object?> { [BattleResolver.DpKey] = 3000 }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Player, AttackerId, ChoiceZone.None, ChoiceZone.BattleArea));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Opponent, TargetId, ChoiceZone.None, ChoiceZone.BattleArea));

    context.AttackController.DeclareAttack(Player, AttackerId, Opponent, TargetId, isDirectAttack: false);
    AssertEqual(AttackPhase.Declared, context.AttackController.Current.Phase, "declared phase");

    var pipeline = new AttackPipeline();

    // AS-IS CounterTiming two-pass (AttackProcess.cs:266-296): each pass emits and parks one iteration.
    AttackAdvanceResult pass1 = await pipeline.AdvanceAsync(context);
    AssertTrue(pass1.Progressed, "counter pass 1 progressed");
    AssertEqual(AttackPhase.Declared, context.AttackController.Current.Phase, "counter pass 1 parks at Declared");

    AttackAdvanceResult pass2 = await pipeline.AdvanceAsync(context);
    AssertTrue(pass2.Progressed, "counter pass 2 progressed");
    AssertEqual(AttackPhase.Declared, context.AttackController.Current.Phase, "counter pass 2 parks at Declared");

    AttackAdvanceResult step1 = await pipeline.AdvanceAsync(context);
    AssertTrue(step1.Progressed, "step 1 progressed");
    AssertEqual(AttackPhase.Combat, context.AttackController.Current.Phase, "no blockers skips straight to combat");

    AttackAdvanceResult step2 = await pipeline.AdvanceAsync(context);
    AssertTrue(step2.Progressed, "step 2 progressed");
    AssertEqual(AttackPhase.Resolved, context.AttackController.Current.Phase, "combat resolves the attack");

    AttackAdvanceResult step3 = await pipeline.AdvanceAsync(context);
    AssertTrue(step3.Progressed, "step 3 progressed");
    AssertEqual(AttackPhase.Completed, context.AttackController.Current.Phase, "end attack triggers collected");

    AttackAdvanceResult step4 = await pipeline.AdvanceAsync(context);
    AssertTrue(step4.Progressed, "step 4 progressed");
    AssertEqual(AttackPhase.None, context.AttackController.Current.Phase, "cleanup clears the attack");

    AttackAdvanceResult idle = await pipeline.AdvanceAsync(context);
    AssertFalse(idle.Progressed, "no work remains once cleared");
}

Task AttackPipelineSourceIsClean()
{
    string pipelinePath = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "AttackPipeline.cs");
    if (!File.Exists(pipelinePath))
    {
        throw new FileNotFoundException($"Attack pipeline source was not found: {pipelinePath}");
    }

    string pipeline = File.ReadAllText(pipelinePath);
    AssertFalse(pipeline.Contains("TODO", StringComparison.OrdinalIgnoreCase), "AttackPipeline must not contain TODO");
    AssertFalse(pipeline.Contains("NotImplementedException", StringComparison.Ordinal), "AttackPipeline must not throw NotImplementedException");
    AssertFalse(pipeline.Contains("UnityEngine", StringComparison.Ordinal), "AttackPipeline must not reference UnityEngine");

    string flowPath = Path.Combine(root, "src", "HeadlessDCGO.Engine", "Headless", "Runtime", "GameFlowProcessor.cs");
    string flow = File.ReadAllText(flowPath);
    AssertContains(flow, "AttackPipeline", "flow processor wires the attack pipeline");
    AssertFalse(flow.Contains("Placeholder: attack pipeline", StringComparison.Ordinal), "attack placeholder must be removed");
    return Task.CompletedTask;
}

// (4b B5-c5) Pump-driven setup: CreatePumpDriven + reach P1's main wait, then stage the combatants as live
// battle-area synthetic Digimon (the F68 idiom). The pump owns the hand/security deal (NormalizeForPump), so the
// combatants are staged directly None->BattleArea rather than from a dealt hand. No AdvancePhase action currency.
async Task<DcgoMatch> CreateMatchAsync(
    int? attackerDp,
    int? targetDp,
    bool withBlocker,
    int? blockerDp = 5000,
    int securityCount = 0)
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
        firstPlayerId: Player,
        initialSecuritySize: 0, shuffleDecks: false, shuffleDigitamaDecks: false);

    await match.InitializeAsync(MatchConfig.Create(new[] { Player, Opponent }, randomSeed: 73, setup: setup));
    await ExpStepOnce(match);
    await ExpDriveUntil(match, m => ExpAtMainWait(m, Player));

    StagePermanent(match, Player, AttackerId, attackerDp, suspended: false, isBlocker: false);
    if (targetDp.HasValue)
    {
        StagePermanent(match, Opponent, TargetId, targetDp, suspended: true, isBlocker: false);
    }

    if (withBlocker)
    {
        StagePermanent(match, Opponent, BlockerId, blockerDp, suspended: false, isBlocker: true);
    }

    if (securityCount > 0)
    {
        StageSecurity(match, Opponent, SecurityId);
    }

    return match;
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

void AssertComplete(string fileName)
{
    string path = Path.Combine(root, "docs", "test-results", "goals", fileName);
    if (!File.Exists(path))
    {
        throw new FileNotFoundException($"Predecessor result document was not found: {path}");
    }

    AssertContains(File.ReadAllText(path), "COMPLETE", fileName);
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
