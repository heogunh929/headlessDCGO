using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// (AD1-S) CanNotSwitchAttackTarget — AS-IS Permanent.CanSwitchAttackTarget (Permanent.cs:3745) gates
// exactly two actions on the ATTACKING permanent: block eligibility (:2156) and SwitchDefender
// (AttackProcess.cs:519, shared by blocker-redirect and retarget effects). Grant shape (AD1_011:110-113):
// UntilEachTurnEndEffects.Add(PermanentEffectFactory.CanNotSwitchAttackTargetEffect(...)) — expires at turn
// end; CanUse includes IsOwnerTurn (the controller's own turn only).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("a locked attacker offers NO blocker candidates (even vs Collision)", LockedAttackerNoBlock),
    ("a locked Raid attacker gets no switch offer (SwitchDefender gate mirror)", LockedRaidNoSwitch),
    ("the lock expires at turn end (UntilEachTurnEnd bucket mirror)", LockExpiresAtTurnEnd),
    ("IsOwnerTurn CanUse gate: the lock is inert on the opponent's turn", OwnerTurnGate),
    ("predicate form: only matching attackers are locked (direct-construction card shape)", PredicateForm),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task LockedAttackerNoBlock()
{
    EngineContext ctx = Ctx(turnPlayer: 1);
    var attacker = await Place(ctx, P1, "ATK");
    var blocker = await Place(ctx, P2, "BLK", extra: new() { ["hasBlocker"] = true, [BlockTiming.HasCollisionKey] = true });

    GrantLock(ctx, attacker);
    ctx.AttackController.DeclareAttack(P1, attacker, P2, targetId: null, isDirectAttack: true);

    AssertTrue(new BlockTiming().GetBlockerCandidates(ctx).Count == 0,
        "no blocker candidates for a locked attacker (AS-IS Permanent.cs:2156; outranks Collision)");
}

async Task LockedRaidNoSwitch()
{
    EngineContext ctx = Ctx(turnPlayer: 1);
    var attacker = await Place(ctx, P1, "ATK", extra: new() { [RaidAttackSwitch.HasRaidKey] = true });
    await Place(ctx, P2, "BIG", dp: 9000);

    ctx.AttackController.DeclareAttack(P1, attacker, P2, targetId: null, isDirectAttack: true);
    AssertTrue(RaidAttackSwitch.GetSwitchCandidates(ctx).Count > 0, "sanity: a switch candidate exists");

    GrantLock(ctx, attacker);
    AssertTrue(!RaidAttackSwitch.RequestChoice(ctx), "the Raid retarget offer never opens for a locked attacker");
}

async Task LockExpiresAtTurnEnd()
{
    EngineContext ctx = Ctx(turnPlayer: 1);
    var attacker = await Place(ctx, P1, "ATK");
    GrantLock(ctx, attacker);
    AssertTrue(AttackTargetSwitchGate.IsLocked(ctx, attacker), "locked while granted");

    EffectDurationExpiry.ExpireTurnEnd(ctx.EffectRegistry, P1);
    AssertTrue(!AttackTargetSwitchGate.IsLocked(ctx, attacker), "the UntilEachTurnEnd grant expired at turn end");
}

async Task OwnerTurnGate()
{
    EngineContext ctx = Ctx(turnPlayer: 2);   // NOT the granting owner's turn
    var attacker = await Place(ctx, P1, "ATK");
    GrantLock(ctx, attacker);
    AssertTrue(!AttackTargetSwitchGate.IsLocked(ctx, attacker),
        "AS-IS CanUse includes IsOwnerTurn — the lock is inert on the opponent's turn");
}

async Task PredicateForm()
{
    EngineContext ctx = Ctx(turnPlayer: 1);
    var named = await Place(ctx, P1, "OMEGA");
    var other = await Place(ctx, P1, "PLAIN");

    // Direct-construction shape (EX8_025/BT20_026 …): the card's own PermanentCondition, here name-based.
    var effect = new CanNotSwitchAttackTargetClass();
    effect.SetUpICardEffect("named lock", null, new CardSource(ctx, named, P1));
    effect.SetUpCanNotSwitchAttackTargetClass(p => p.TopCard.EqualsCardName("OMEGA"));
    ctx.EffectRegistry.Register(effect.ToBinding($"lock:{named.Value}"));

    AssertTrue(AttackTargetSwitchGate.IsLocked(ctx, named), "matching attacker is locked");
    AssertTrue(!AttackTargetSwitchGate.IsLocked(ctx, other), "non-matching attacker is not (predicate evaluated 1:1)");
}

// --- Helpers ---

void GrantLock(EngineContext ctx, HeadlessEntityId attackerId)
{
    // AS-IS AD1_011 shape: UntilEachTurnEndEffects.Add(PermanentEffectFactory.CanNotSwitchAttackTargetEffect(...)).
    CanNotSwitchAttackTargetClass effect = PermanentEffectFactory.CanNotSwitchAttackTargetEffect(
        new Permanent(ctx, attackerId, OwnerOf(ctx, attackerId)));
    ctx.EffectRegistry.Register(effect.ToBinding($"switchlock:{attackerId.Value}", EffectDuration.UntilEachTurnEnd));
}

HeadlessPlayerId OwnerOf(EngineContext ctx, HeadlessEntityId id) =>
    ctx.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? r) && r is not null ? r.OwnerId : default;

EngineContext Ctx(int turnPlayer)
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 964);
    ctx.TurnController.Initialize(new[] { P1, P2 }, turnPlayer == 1 ? P1 : P2);
    return ctx;
}

async Task<HeadlessEntityId> Place(EngineContext ctx, HeadlessPlayerId owner, string tag, int dp = 5000, Dictionary<string, object?>? extra = null)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = 5 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["isSuspended"] = false };
    if (extra is not null)
    {
        foreach (var kv in extra) { meta[kv.Key] = kv.Value; }
    }

    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner, Metadata: meta));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
