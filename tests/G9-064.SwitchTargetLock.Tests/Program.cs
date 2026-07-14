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
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task LockedAttackerNoBlock()
{
    EngineContext ctx = Ctx(turnPlayer: 1);
    // (P7 test-fix pattern) CheckEffectDisabledClass.PotentiallyDisablingEffects reads GManager.instance (the
    // mirror proxy: AmbientMatchContext) — scope the match so CanUse/CanActivate/IsDisabled don't NRE.
    using var _ambientScope = AmbientMatchContext.Enter(ctx);
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
    using var _ambientScope = AmbientMatchContext.Enter(ctx);
    var attacker = await Place(ctx, P1, "ATK", extra: new() { [RaidAttackSwitch.HasRaidKey] = true });
    await Place(ctx, P2, "BIG", dp: 9000);

    ctx.AttackController.DeclareAttack(P1, attacker, P2, targetId: null, isDirectAttack: true);
    AssertTrue(RaidAttackSwitch.GetSwitchCandidates(ctx).Count > 0, "sanity: a switch candidate exists");

    GrantLock(ctx, attacker);
    AssertTrue(!RaidAttackSwitch.RequestChoice(ctx), "the Raid retarget offer never opens for a locked attacker");
}

async Task LockExpiresAtTurnEnd()
{
    // STOP (per task brief — heavy-substrate, outside NewModelContinuousScan.cs/*Gate.cs/
    // MatchStateMutationSink.cs touch scope): AS-IS AD1_011 registers the grant into a DURATION bucket
    // (UntilEachTurnEndEffects), so EffectDurationExpiry.ExpireTurnEnd can drop it at turn end. The seam this
    // pass uses (GrantLock attaches the built effect to the attacker's own `cEntity_Effect`, the card's own
    // PRINTED effect surface) has no duration/expiry concept at all — it is permanent for the process's
    // lifetime, same as a real card's own keyword. AS-IS's actual "grant a TEMPORARY effect onto a Permanent
    // with a duration bucket" store does not exist yet in the mirror (Permanent.EffectList_Added is a stub
    // that always returns empty — design item P6A-PERMANENT-EFFECTLIST-ADDED, referenced in
    // NewModelContinuousScan.cs's own EffectList_Added doc-comment). Fixing this needs that permanent-grant-
    // with-duration store, not a Gate/scan change. First assertion (grant observed while active) still PASSES
    // via the seam and proves the lock mechanism itself; the second (turn-end expiry) is left failing/STOP.
    EngineContext ctx = Ctx(turnPlayer: 1);
    using var _ambientScope = AmbientMatchContext.Enter(ctx);
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
    using var _ambientScope = AmbientMatchContext.Enter(ctx);
    var named = await Place(ctx, P1, "OMEGA");
    var other = await Place(ctx, P1, "PLAIN");

    // Direct-construction shape (EX8_025/BT20_026 …): the card's own PermanentCondition, here name-based.
    // SEAM (post-stage-B): CanNotSwitchAttackTargetClass is a new-model kind-class observed via
    // AttackTargetSwitchGate.IsLocked's live Permanent.EffectList(None) scan — attach it to the granting
    // card's controller (the same settable `cEntity_Effect` seam every ported card definition class uses).
    // TEST-BUG FIX (same recurring class as other null-condition factories — precedent
    // docs/audit/rebuild_p6_stageB_notes.md §7): ICardEffect.CanTrigger is `CanUseCondition == null ||
    // !CanUseCondition(hashtable) => false` — a NULL CanUseCondition makes CanTrigger/CanUse ALWAYS false, not
    // "always available". Pass an explicit accept-all `_ => true`.
    var namedSource = new CardSource(ctx, named, P1);
    var effect = new CanNotSwitchAttackTargetClass();
    effect.SetUpICardEffect("named lock", _ => true, namedSource);
    effect.SetUpCanNotSwitchAttackTargetClass(p => p.TopCard.EqualsCardName("OMEGA"));
    namedSource.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(effect);

    AssertTrue(AttackTargetSwitchGate.IsLocked(ctx, named), "matching attacker is locked");
    AssertTrue(!AttackTargetSwitchGate.IsLocked(ctx, other), "non-matching attacker is not (predicate evaluated 1:1)");
}

// --- Helpers ---

// SEAM (post-stage-B): CanNotSwitchAttackTargetClass is a new-model kind-class observed via
// AttackTargetSwitchGate.IsLocked's OWN live Permanent.EffectList(None) scan (already 1:1, no legacy binding
// fold — this gate has always been the new-model scan) — attach the built effect to the attacker's own
// controller the same way every other seam test does; the factory call previously discarded its return value.
void GrantLock(EngineContext ctx, HeadlessEntityId attackerId)
{
    // AS-IS AD1_011 shape: UntilEachTurnEndEffects.Add(PermanentEffectFactory.CanNotSwitchAttackTargetEffect(...)).
    var attackerSource = new CardSource(ctx, attackerId, OwnerOf(ctx, attackerId));
    ICardEffect effect = PermanentEffectFactory.CanNotSwitchAttackTargetEffect(
        new Permanent(ctx, attackerId, OwnerOf(ctx, attackerId)));
    attackerSource.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(effect);
}

HeadlessPlayerId OwnerOf(EngineContext ctx, HeadlessEntityId id) =>
    ctx.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? r) && r is not null ? r.OwnerId : default;

EngineContext Ctx(int turnPlayer)
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 964);
    ctx.TurnController.Initialize(new[] { P1, P2 }, turnPlayer == 1 ? P1 : P2);
    // (P7 test-fix pattern) ICardEffect.CanTrigger gates on TurnStateMachine.DoneStartGame (phase past
    // None/Setup) UNCONDITIONALLY before any specific CanUseCondition runs — without this every candidate
    // effect's CanUse/CanTrigger trivially returns false regardless of the grant, silently masquerading as
    // "not locked" (protection never firing looks identical to "correctly not blocked").
    ctx.TurnController.SetPhase(HeadlessPhase.Main);
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

// Minimal AS-IS-shaped CEntity_Effect: the same seam every ported card definition class (e.g. `class BT1_001 :
// CEntity_Effect`) uses to surface its printed effect list to CardSource.EffectList/EffectList_ExceptAddedEffects.
sealed class TestCardEntityEffect : CEntity_Effect
{
    private readonly ICardEffect _effect;

    public TestCardEntityEffect(ICardEffect effect)
    {
        _effect = effect;
    }

    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource cardSource) => new() { _effect };
}
