// C-12 Iceclad — battle-comparison keyword. AS-IS CardController.CompareStats: when EITHER combatant
// has Iceclad the battle is decided by digivolution-source count (DigivolutionCards.Count) instead of
// DP; otherwise by DP. Engine consumption is in BattleResolver.CompareBattleStats; the grant maps
// GrantIceclad -> hasIceclad (MatchStateMutationSink.KindToFlag). No new subsystem — a comparison branch
// plus a source count. This suite drives a real battle and asserts who is deleted under each branch,
// including the regression that with no Iceclad source counts are ignored (DP still governs).
//
// (4b A-1) RE-TARGETED to the pump (G3.5-005 c5 F68 idiom): the OLD-ctor `new DcgoMatch` + AdvanceToMain
// (AdvancePhase currency) + blocker-parked `new BattleResolver().ResolveAsync` manual seam is replaced by
// CreatePumpDriven + the pump DeclareAttack legal target-attack lane, driven to auto-resolution. The
// deletion result is re-sourced from a RESULT-OBJECT (BattleResolutionResult.AttackerDeleted/DefenderDeleted)
// to the equivalent, stronger ZONE OBSERVATION (loser landed in Trash) — the assertions themselves are
// preserved verbatim. AdvancePhase/EndTurn action currency removed (currency = 0).
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId Player = new(1);
HeadlessPlayerId Opponent = new(2);
HeadlessEntityId AttackerId = new("p1:main:001:P1-M01");
HeadlessEntityId TargetId = new("p2:main:001:P2-M01");

var tests = new (string Name, Func<Task> Body)[]
{
    ("Iceclad attacker with more sources beats a higher-DP defender", IcecladAttackerWinsBySources),
    ("Iceclad defender with more sources beats a higher-DP attacker", IcecladDefenderWinsBySources),
    ("Iceclad with equal source counts deletes both (tie ignores DP)", IcecladEqualSourcesTie),
    ("Iceclad attacker with fewer sources loses despite higher DP", IcecladFewerSourcesLoses),
    ("No Iceclad: source counts are ignored and DP governs", NoIcecladUsesDp),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex)
    {
        failures.Add(test.Name);
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}");
    }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// Attacker: low DP but 3 sources + Iceclad. Defender: high DP, 1 source. By DP the attacker loses;
// Iceclad switches the comparison to sources (3 > 1) so the defender is deleted instead.
async Task IcecladAttackerWinsBySources()
{
    BattleOutcome result = await ResolveBattle(
        attackerDp: 1000, attackerSources: 3, attackerIceclad: true,
        targetDp: 9000, targetSources: 1, targetIceclad: false);

    AssertFalse(result.AttackerDeleted, "Iceclad attacker survives on source count");
    AssertTrue(result.DefenderDeleted, "defender is deleted (fewer sources)");
}

// Either combatant having Iceclad triggers the source comparison: here the defender carries it.
async Task IcecladDefenderWinsBySources()
{
    BattleOutcome result = await ResolveBattle(
        attackerDp: 9000, attackerSources: 1, attackerIceclad: false,
        targetDp: 1000, targetSources: 3, targetIceclad: true);

    AssertTrue(result.AttackerDeleted, "attacker is deleted (fewer sources)");
    AssertFalse(result.DefenderDeleted, "Iceclad defender survives on source count");
}

// Equal sources => clamp(0) tie => both deleted, even though DP differs (DP is not consulted).
async Task IcecladEqualSourcesTie()
{
    BattleOutcome result = await ResolveBattle(
        attackerDp: 9000, attackerSources: 2, attackerIceclad: true,
        targetDp: 1000, targetSources: 2, targetIceclad: false);

    AssertTrue(result.AttackerDeleted, "tie deletes the attacker");
    AssertTrue(result.DefenderDeleted, "tie deletes the defender");
}

// Iceclad does not guarantee a win — fewer sources still loses regardless of DP advantage.
async Task IcecladFewerSourcesLoses()
{
    BattleOutcome result = await ResolveBattle(
        attackerDp: 9000, attackerSources: 0, attackerIceclad: true,
        targetDp: 1000, targetSources: 2, targetIceclad: false);

    AssertTrue(result.AttackerDeleted, "Iceclad attacker with fewer sources is deleted");
    AssertFalse(result.DefenderDeleted, "defender with more sources survives");
}

// Regression: with neither combatant Iceclad, source counts are irrelevant and DP decides.
async Task NoIcecladUsesDp()
{
    BattleOutcome result = await ResolveBattle(
        attackerDp: 1000, attackerSources: 5, attackerIceclad: false,
        targetDp: 9000, targetSources: 0, targetIceclad: false);

    AssertTrue(result.AttackerDeleted, "no Iceclad: lower-DP attacker is deleted (sources ignored)");
    AssertFalse(result.DefenderDeleted, "higher-DP defender survives");
}

// --- Harness (pump, G3.5-005 F68 idiom) ----------------------------------

// The persistent outcome: who ended up in Trash (deleted) after the pump auto-resolved the battle. This is
// the ZONE observation that replaces the OLD manual BattleResolutionResult.AttackerDeleted/DefenderDeleted.
async Task<BattleOutcome> ResolveBattle(
    int attackerDp, int attackerSources, bool attackerIceclad,
    int targetDp, int targetSources, bool targetIceclad)
{
    DcgoMatch match = await CreateMatchAsync();
    StageCombatant(match, Player, AttackerId, attackerDp, attackerSources, attackerIceclad, suspended: false);
    StageCombatant(match, Opponent, TargetId, targetDp, targetSources, targetIceclad, suspended: true);

    await ExpApply(match, TargetAttackLane(match, AttackerId, TargetId));
    await ExpDriveUntil(match, m => m.Context.AttackController.Current.Phase == AttackPhase.None || m.IsTerminal());

    return new BattleOutcome(
        AttackerDeleted: InZone(match, Player, ChoiceZone.Trash, AttackerId),
        DefenderDeleted: InZone(match, Opponent, ChoiceZone.Trash, TargetId));
}

async Task<DcgoMatch> CreateMatchAsync()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 73);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = DcgoMatch.CreatePumpDriven(context, new EngineTrace());
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(Player, "P1"), Deck(Opponent, "P2") },
        firstPlayerId: Player, initialSecuritySize: 0, shuffleDecks: false, shuffleDigitamaDecks: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { Player, Opponent }, randomSeed: 73, setup: setup));
    await ExpStepOnce(match);
    await ExpDriveUntil(match, m => ExpAtMainWait(m, Player));
    return match;
}

// Stage a live battle-area synthetic Digimon (F68 PlaceRealCard idiom): synthetic Digimon def with a def-level
// dp, instance carrying dp + Iceclad source ids/flag, moved None->BattleArea and registered.
void StageCombatant(DcgoMatch match, HeadlessPlayerId owner, HeadlessEntityId id, int dp, int sources, bool iceclad, bool suspended)
{
    EngineContext ctx = match.Context;
    var defId = new HeadlessEntityId($"DEF:{id.Value}");
    var defMeta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["level"] = 5, ["dp"] = dp };
    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(defId, id.Value, id.Value, defMeta, CardType: "Digimon"));

    var instMeta = new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["isSuspended"] = suspended,
        [BattleResolver.DpKey] = dp,
        [BattleResolver.SourceIdsKey] = Enumerable.Range(1, sources).Select(i => $"src{i:D2}").ToArray(),
    };
    if (iceclad) instMeta[BattleResolver.HasIcecladKey] = true;
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

static CardRecord Digimon(string id) =>
    new(new HeadlessEntityId(id), id, $"{id} Card", new Dictionary<string, object?>(), CardType: "Digimon");

static PlayerDeckSetup Deck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

static void AssertTrue(bool value, string label) { if (!value) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertFalse(bool value, string label) { if (value) throw new InvalidOperationException($"{label}: expected false."); }

readonly record struct BattleOutcome(bool AttackerDeleted, bool DefenderDeleted);
