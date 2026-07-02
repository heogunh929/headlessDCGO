using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// (W6-G) restriction-grant commons — AS-IS GiveEffectToPermanent family (verbatim verified): target-locked
// duration-tagged restriction with a COUNTERPART predicate (attackerCondition/defenderCondition) the gates
// evaluate per pairing (FR-P3 pattern generalised to Block/BeAttacked/BeBlocked).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("GainCanNotAttack: restricted only against matching defenders (pair evaluation)", CanNotAttackPair),
    ("GainCanNotBlock: restricted only against matching attackers", CanNotBlockPair),
    ("GainCanNotBeAttacked / GainCanNotBeBlocked: counterpart-conditional", BeAttackedBeBlockedPair),
    ("GainCanNotUnsuspend: unsuspend locked, expires at the duration boundary", UnsuspendLock),
    ("GainCanNotSuspend: suspend-cost gate goes false while granted", SuspendLock),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task CanNotAttackPair()
{
    EngineContext ctx = Ctx();
    var src = await Place(ctx, P1, "SRC");
    var attacker = await Place(ctx, P1, "ATK");
    var bigDefender = await Place(ctx, P2, "BIG", level: 6);
    var smallDefender = await Place(ctx, P2, "SMALL", level: 3);

    AssertTrue(CardEffectCommons.GainCanNotAttack(
        Perm(ctx, attacker), p => p.Level >= 6, EffectDuration.UntilOpponentTurnEnd, V(ctx, src)), "granted");

    AssertTrue(ContinuousRestrictionGate.EvaluateAttack(ctx, attacker, bigDefender).IsRestricted,
        "cannot attack a MATCHING (level>=6) defender");
    AssertTrue(!ContinuousRestrictionGate.EvaluateAttack(ctx, attacker, smallDefender).IsRestricted,
        "may still attack a non-matching defender (AS-IS defenderCondition)");
}

async Task CanNotBlockPair()
{
    EngineContext ctx = Ctx();
    var src = await Place(ctx, P1, "SRC");
    var blocker = await Place(ctx, P2, "BLK");
    var bigAttacker = await Place(ctx, P1, "BIGATK", level: 6);
    var smallAttacker = await Place(ctx, P1, "SMALLATK", level: 3);

    CardEffectCommons.GainCanNotBlock(Perm(ctx, blocker), p => p.Level >= 6, EffectDuration.UntilOpponentTurnEnd, V(ctx, src));

    AssertTrue(ContinuousRestrictionGate.EvaluateBlock(ctx, blocker, bigAttacker).IsRestricted,
        "cannot block the MATCHING attacker");
    AssertTrue(!ContinuousRestrictionGate.EvaluateBlock(ctx, blocker, smallAttacker).IsRestricted,
        "may still block a non-matching attacker");
}

async Task BeAttackedBeBlockedPair()
{
    EngineContext ctx = Ctx();
    var src = await Place(ctx, P1, "SRC");
    var protectedDigimon = await Place(ctx, P1, "PROT");
    var bigEnemy = await Place(ctx, P2, "BIGE", level: 6);
    var smallEnemy = await Place(ctx, P2, "SMALLE", level: 3);

    CardEffectCommons.GainCanNotBeAttacked(Perm(ctx, protectedDigimon), p => p.Level <= 4, EffectDuration.UntilOpponentTurnEnd, V(ctx, src));
    AssertTrue(ContinuousRestrictionGate.EvaluateBeAttacked(ctx, protectedDigimon, smallEnemy).IsRestricted,
        "a level<=4 attacker cannot attack it");
    AssertTrue(!ContinuousRestrictionGate.EvaluateBeAttacked(ctx, protectedDigimon, bigEnemy).IsRestricted,
        "a level 6 attacker still can");

    var attacker = await Place(ctx, P1, "UNBLOCKABLE");
    CardEffectCommons.GainCanNotBeBlocked(Perm(ctx, attacker), p => p.Level <= 4, EffectDuration.UntilOpponentTurnEnd, V(ctx, src));
    AssertTrue(ContinuousRestrictionGate.EvaluateBeBlocked(ctx, attacker, smallEnemy).IsRestricted,
        "a level<=4 blocker cannot block it");
    AssertTrue(!ContinuousRestrictionGate.EvaluateBeBlocked(ctx, attacker, bigEnemy).IsRestricted,
        "a level 6 blocker still can");
}

async Task UnsuspendLock()
{
    EngineContext ctx = Ctx();
    var src = await Place(ctx, P1, "SRC");
    var target = await Place(ctx, P2, "TGT");
    SetMeta(ctx, target, "isSuspended", true);

    CardEffectCommons.GainCantUnsuspendUntilOpponentTurnEnd(Perm(ctx, target), V(ctx, src));
    AssertTrue(ContinuousRestrictionGate.EvaluateUnsuspend(ctx, target).IsRestricted, "unsuspend locked");
    AssertTrue(!CardEffectCommons.CanUnsuspend(Perm(ctx, target)), "CanUnsuspend predicate agrees");

    EffectDurationExpiry.ExpireTurnEnd(ctx.EffectRegistry, P2);
    AssertTrue(!ContinuousRestrictionGate.EvaluateUnsuspend(ctx, target).IsRestricted, "expired at the boundary");
}

async Task SuspendLock()
{
    EngineContext ctx = Ctx();
    var src = await Place(ctx, P1, "SRC");
    var target = await Place(ctx, P1, "TGT");
    AssertTrue(CardEffectCommons.CanActivateSuspendCostEffect(V(ctx, target)), "can pay a suspend cost before");
    CardEffectCommons.GainCantSuspendUntilOpponentTurnEnd(Perm(ctx, target), V(ctx, src));
    AssertTrue(!CardEffectCommons.CanActivateSuspendCostEffect(V(ctx, target)), "suspend-locked -> cannot pay");
}

// --- Harness ---

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 972);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    return ctx;
}

async Task<HeadlessEntityId> Place(EngineContext ctx, HeadlessPlayerId owner, string tag, int level = 4)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 5000, ["level"] = level }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 5000 }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

CardSource V(EngineContext ctx, HeadlessEntityId id) => new(ctx, id, OwnerOf(ctx, id), OwnerOf(ctx, id));
Permanent Perm(EngineContext ctx, HeadlessEntityId id) => new(ctx, id, OwnerOf(ctx, id));
HeadlessPlayerId OwnerOf(EngineContext ctx, HeadlessEntityId id) =>
    ctx.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? r) && r is not null ? r.OwnerId : default;

void SetMeta(EngineContext ctx, HeadlessEntityId id, string key, object? value)
{
    ctx.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? r);
    ctx.CardInstanceRepository.Upsert(r! with
    {
        Metadata = new Dictionary<string, object?>(r!.Metadata, StringComparer.Ordinal) { [key] = value }
    });
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
