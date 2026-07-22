using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// R2-1 / N-2 (consumption side): battle deletion now consults continuous deletion-prevention
// REPLACEMENTS (BattleDeletionGate), not just the static preventBattleDeletion flag. A card protected
// by another card's continuous "cannot be deleted" replacement survives a battle it would otherwise
// lose — both a field battle (BattleResolver) and a security-Digimon battle (SecurityResolver).
// This is the engine wiring; a Jamming keyword would register such a replacement in Phase 4.
//
// (4b A-1) RE-TARGETED to the pump (G3.5-005 c5 F68 idiom): the OLD-ctor `new DcgoMatch` + AdvanceToMain
// (AdvancePhase currency) + single-step drive (and the manual `AttackController.DeclareAttack` +
// `AttackPipeline.AdvanceAsync` for the security battle) are replaced by CreatePumpDriven + the pump
// DeclareAttack legal lane (target and direct), driven to auto-resolution. The zone assertions are already
// the persistent-outcome observations and are preserved verbatim. AdvancePhase/EndTurn currency removed.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
HeadlessEntityId AttackerId = new("p1:main:001:P1-M01");
HeadlessEntityId TargetId = new("p2:main:001:P2-M01");
HeadlessEntityId SecurityId = new("p2:main:006:P2-M06");

var tests = new (string Name, Func<Task> Body)[]
{
    ("Field battle: a continuous prevent-deletion replacement saves the loser", FieldBattleReplacementSaves),
    ("Field battle control: without the replacement the loser is deleted", FieldBattleControlDeleted),
    ("Security battle: the replacement saves the attacker from a stronger security Digimon", SecurityBattleReplacementSaves),
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

// --- Tests ---------------------------------------------------------------

async Task FieldBattleReplacementSaves()
{
    // Attacker 5000 beats target 3000 -> target would be deleted; a continuous prevent-deletion
    // replacement on the target saves it.
    DcgoMatch match = await FieldSetup(attackerDp: 5000, targetDp: 3000);
    RegisterPreventDeletion(match.Context, TargetId, owner: P2);
    await DeclareTargetAttack(match);

    AssertInZone(match, P2, ChoiceZone.BattleArea, TargetId, "protected target survived the battle");
}

async Task FieldBattleControlDeleted()
{
    DcgoMatch match = await FieldSetup(attackerDp: 5000, targetDp: 3000);
    await DeclareTargetAttack(match);

    AssertFalse(InZone(match, P2, ChoiceZone.BattleArea, TargetId), "unprotected target left the field");
    AssertInZone(match, P2, ChoiceZone.Trash, TargetId, "unprotected target was deleted");
}

async Task SecurityBattleReplacementSaves()
{
    // Attacker 2000 vs a 7000 security Digimon -> attacker would be deleted; the replacement saves it.
    DcgoMatch match = await SecuritySetup(attackerDp: 2000, topSecurityDp: 7000);
    RegisterPreventDeletion(match.Context, AttackerId, owner: P1);
    await DeclareDirectAttack(match);

    AssertInZone(match, P1, ChoiceZone.BattleArea, AttackerId, "protected attacker survived the security Digimon");
}

// --- Continuous prevent-deletion replacement registration ----------------

// (④ harness rewire) The invented EffectRegistry continuous "preventDeletion" binding is deleted; battle
// deletion now reads the AS-IS-literal BattleDeletionGate → NewModelContinuousScan.HasCanNotBeDestroyed scan
// (ICanNotBeDestroyedEffect over field permanents). Grant the immunity the way a real card does: attach a live
// CanNotBeDestroyedClass kind-class (CardEffectFactory.CanNotBeDestroyedStaticEffect, scoped to this card) to
// the card's live effect list — the same seam the battle resolvers consult.
void RegisterPreventDeletion(EngineContext context, HeadlessEntityId cardId, HeadlessPlayerId owner)
{
    var holder = new CardSource(context, cardId, owner);
    using (AmbientMatchContext.Enter(context))
    {
        holder.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(
            CardEffectFactory.CanNotBeDestroyedStaticEffect(
                permanentCondition: p => p is not null && p.InstanceId == cardId,
                isInheritedEffect: false, card: holder, condition: () => true, effectName: "cannot-be-deleted"));
    }
}

// --- Harness (pump, G3.5-005 F68 idiom) ----------------------------------

async Task<DcgoMatch> FieldSetup(int attackerDp, int targetDp)
{
    DcgoMatch match = await BaseMatch();
    StageCombatant(match, P1, AttackerId, attackerDp, suspended: false);
    StageCombatant(match, P2, TargetId, targetDp, suspended: true);
    return match;
}

async Task DeclareTargetAttack(DcgoMatch match)
{
    await ExpApply(match, TargetAttackLane(match, AttackerId, TargetId));
    await ExpDriveUntil(match, m => m.Context.AttackController.Current.Phase == AttackPhase.None || m.IsTerminal());
}

async Task<DcgoMatch> SecuritySetup(int attackerDp, int topSecurityDp)
{
    DcgoMatch match = await BaseMatch();
    StageCombatant(match, P1, AttackerId, attackerDp, suspended: false, strike: 1);
    StageCombatant(match, P2, TargetId, dp: null, suspended: true);
    StageSecurity(match, P2, SecurityId, topSecurityDp);
    return match;
}

async Task DeclareDirectAttack(DcgoMatch match)
{
    await ExpApply(match, DirectAttackLane(match, AttackerId));
    await ExpDriveUntil(match, m => m.Context.AttackController.Current.Phase == AttackPhase.None || m.IsTerminal());
}

// --- Shared --------------------------------------------------------------

async Task<DcgoMatch> BaseMatch()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 74);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = DcgoMatch.CreatePumpDriven(context, new EngineTrace());
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") }, firstPlayerId: P1,
        initialSecuritySize: 0, shuffleDecks: false, shuffleDigitamaDecks: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 74, setup: setup));
    await ExpStepOnce(match);
    await ExpDriveUntil(match, m => ExpAtMainWait(m, P1));
    return match;
}

void StageCombatant(DcgoMatch match, HeadlessPlayerId owner, HeadlessEntityId id, int? dp, bool suspended, int? strike = null)
{
    EngineContext ctx = match.Context;
    var defId = new HeadlessEntityId($"DEF:{id.Value}");
    var defMeta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["level"] = 5 };
    if (dp.HasValue) defMeta["dp"] = dp.Value;
    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(defId, id.Value, id.Value, defMeta, CardType: "Digimon"));

    var instMeta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["isSuspended"] = suspended };
    if (dp.HasValue) instMeta[BattleResolver.DpKey] = dp.Value;
    if (strike.HasValue) instMeta[SecurityResolver.StrikeKey] = strike.Value;
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner, Metadata: instMeta));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
    CardEffectRegistrar.RegisterCard(ctx, id, owner);
}

void StageSecurity(DcgoMatch match, HeadlessPlayerId owner, HeadlessEntityId id, int dp)
{
    EngineContext ctx = match.Context;
    // The pump StartGame deals its own security stack regardless of the setup config, so clear the dealt
    // stack first — the security check must reveal exactly the staged card.
    var reader = (IZoneStateReader)ctx.ZoneMover;
    foreach (HeadlessEntityId dealt in reader.GetCards(owner, ChoiceZone.Security).ToArray())
    {
        ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, dealt, ChoiceZone.Security, ChoiceZone.Library)).GetAwaiter().GetResult();
    }

    var defId = new HeadlessEntityId($"DEF:{id.Value}");
    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(defId, id.Value, id.Value,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp }, CardType: "Digimon"));
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { [BattleResolver.DpKey] = dp }));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.Security)).GetAwaiter().GetResult();
}

LegalAction TargetAttackLane(DcgoMatch match, HeadlessEntityId attacker, HeadlessEntityId target) =>
    ExpLegal(match, P1)
        .Where(a => a.ActionType == HeadlessActionTypes.DeclareAttack)
        .Where(a => ExpParamId(a, HeadlessActionParameterKeys.AttackerId) == attacker)
        .FirstOrDefault(a => ExpParamId(a, HeadlessActionParameterKeys.AttackTargetId) == target)
        ?? throw new InvalidOperationException("no target-attack lane for " + attacker.Value);

LegalAction DirectAttackLane(DcgoMatch match, HeadlessEntityId attacker) =>
    ExpLegal(match, P1)
        .Where(a => a.ActionType == HeadlessActionTypes.DeclareAttack)
        .Where(a => ExpParamId(a, HeadlessActionParameterKeys.AttackerId) == attacker)
        .FirstOrDefault(a => ExpParamId(a, HeadlessActionParameterKeys.AttackTargetId) is null)
        ?? throw new InvalidOperationException("no direct-attack lane for " + attacker.Value);

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
            $"EXP drive did not reach state — phase:{t.Phase}/{t.StepCursor} attackPhase:{match.Context.AttackController.Current.Phase} " +
            $"pending:{match.HasPendingChoice()} terminal:{match.IsTerminal()}");
    }
}

static CardRecord Digimon(string id) =>
    new(new HeadlessEntityId(id), id, $"{id} Card", new Dictionary<string, object?>(), CardType: "Digimon");

static PlayerDeckSetup Deck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

bool InZone(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)match.Context.ZoneMover).GetCards(player, zone).Contains(cardId);

void AssertInZone(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId, string label)
{
    if (!InZone(match, player, zone, cardId)) throw new InvalidOperationException($"{label}: not in {zone}.");
}

static void AssertFalse(bool value, string label)
{
    if (value) throw new InvalidOperationException($"{label}: expected false.");
}

// (④) attaches a built kind-class to a card's live effect list (the seam a ported card uses); no timing key
// so it surfaces at EffectTiming.None (the continuous-scan read point).
sealed class TestCardEntityEffect : CEntity_Effect
{
    private readonly ICardEffect _effect;
    public TestCardEntityEffect(ICardEffect effect) { _effect = effect; }
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource cardSource) => new() { _effect };
}
