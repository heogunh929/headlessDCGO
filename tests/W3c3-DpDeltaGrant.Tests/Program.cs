// R3-W3c-3 witness — DP/SAttack-delta grant flip (dead registry → AS-IS live bucket).
//
// Before this batch, CardEffectCommons.ChangeDigimonDP / ChangeDigimonSAttack / ChangeDigimonDPPlayerEffect /
// ChangeDigimonSAttackPlayerEffect / ChangeBaseDigimonDP registered a ContinuousModifierGate binding into the
// EffectRegistry. That registry consumer (ContinuousEffectEvaluator.ResolveDp / ResolveSAttack) has ZERO live
// callers — the delta NEVER fired ("무발화"): Permanent.DP / Permanent.Strike / Permanent.BaseDP compute LIVE by
// scanning permanent.EffectList(None) for IChangeDPEffect / IChangeSAttackEffect / IChangeBaseDPEffect (R1
// getters). This batch restores the AS-IS shape verbatim (GiveEffectToPermanent/ChangeDP.cs:34-41): build the
// factory kind-class (ChangeDPClass / ChangeSAttackClass / ChangeBaseDPClass) and store it in the target's
// (or player's) EffectTiming.None duration bucket via AddEffectToPermanent / AddEffectToPlayer, which those
// getters then read. Duration expiry moves from the registry sweep to the AS-IS bucket-reset sites
// (HeadlessEndTurnCleanupFlow for turn-end durations; AttackProcess.Cleanup for UntilEndAttack).
//
// The two CanUse preconditions that make the flip observable are the SAME ones every real resolution path
// establishes: an AmbientMatchContext scope (GManager.instance non-null) and a live phase (DoneStartGame).

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("self-buff DP: ChangeDigimonDP(+2000) folds LIVE via Permanent.DP, reverts at the turn-end bucket reset", SelfBuffDp),
    ("opponent-debuff DP: ChangeDigimonDP(-2000) on an opponent Digimon (owner-resolution 1:1)", OpponentDebuffDp),
    ("SAttack: ChangeDigimonSAttack(+1) folds LIVE via Permanent.Strike, reverts", SecurityAttack),
    ("player-scope DP: ChangeDigimonDPPlayerEffect(predicate) buffs matching, leaves non-matching untouched", PlayerScopeDp),
    ("base-DP SET: ChangeBaseDigimonDP OVERWRITES base DP (not a delta); stacks with a delta grant", BaseDpSet),
    ("UntilEndAttack: the grant lands in the UntilEndAttackEffects bucket (AttackProcess.Cleanup reset site)", UntilEndAttackBucket),
    ("control: no grant → DP/Strike unchanged (the fold is grant-gated, not ambient)", NoGrantControl),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task SelfBuffDp()
{
    var ctx = Ctx();
    using var scope = AmbientMatchContext.Enter(ctx);
    var src = await Put(ctx, P1, "SRC", ChoiceZone.BattleArea);
    var t = await Put(ctx, P1, "TGT", ChoiceZone.BattleArea, dp: 5000);

    AssertEqual(5000, Dp(ctx, t, P1), "base DP before grant");
    AssertTrue(CardEffectCommons.ChangeDigimonDP(Perm(ctx, t, P1), 2000, EffectDuration.UntilEachTurnEnd, V(ctx, src, P1)), "grant returned true");
    AssertEqual(7000, Dp(ctx, t, P1), "+2000 folded LIVE (would be 5000 under the dead registry path)");

    Expire(ctx);
    AssertEqual(5000, Dp(ctx, t, P1), "reverted at the turn-end bucket reset");
}

async Task OpponentDebuffDp()
{
    var ctx = Ctx();
    using var scope = AmbientMatchContext.Enter(ctx);
    var src = await Put(ctx, P1, "SRC", ChoiceZone.BattleArea);
    var foe = await Put(ctx, P2, "FOE", ChoiceZone.BattleArea, dp: 5000);

    // The source is P1's; the target is P2's — the modifier must register against the opponent Digimon
    // (the A-군 owner-resolution class of bug: a -DP on an opponent target must NOT be dropped).
    AssertTrue(CardEffectCommons.ChangeDigimonDP(new Permanent(ctx, foe, P2), -2000, EffectDuration.UntilEachTurnEnd, V(ctx, src, P1)), "grant");
    AssertEqual(3000, Dp(ctx, foe, P2), "-2000 folded on the OPPONENT Digimon (5000 -> 3000)");

    Expire(ctx);
    AssertEqual(5000, Dp(ctx, foe, P2), "reverted");
}

async Task SecurityAttack()
{
    var ctx = Ctx();
    using var scope = AmbientMatchContext.Enter(ctx);
    var src = await Put(ctx, P1, "SRC", ChoiceZone.BattleArea);
    var t = await Put(ctx, P1, "TGT", ChoiceZone.BattleArea, dp: 5000);

    AssertEqual(1, Strike(ctx, t, P1), "base security attack = 1");
    AssertTrue(CardEffectCommons.ChangeDigimonSAttack(Perm(ctx, t, P1), 1, EffectDuration.UntilEachTurnEnd, V(ctx, src, P1)), "grant");
    AssertEqual(2, Strike(ctx, t, P1), "+1 SA folded LIVE");

    Expire(ctx);
    AssertEqual(1, Strike(ctx, t, P1), "reverted");
}

async Task PlayerScopeDp()
{
    var ctx = Ctx();
    using var scope = AmbientMatchContext.Enter(ctx);
    var src = await Put(ctx, P1, "SRC", ChoiceZone.BattleArea);
    var big = await Put(ctx, P1, "BIG", ChoiceZone.BattleArea, dp: 5000, level: 6);
    var small = await Put(ctx, P1, "SMALL", ChoiceZone.BattleArea, dp: 4000, level: 3);

    AssertTrue(CardEffectCommons.ChangeDigimonDPPlayerEffect(p => p.Level >= 6, 3000, EffectDuration.UntilEachTurnEnd, V(ctx, src, P1)), "grant");
    AssertEqual(8000, Dp(ctx, big, P1), "matching (level>=6) buffed +3000");
    AssertEqual(4000, Dp(ctx, small, P1), "non-matching (level 3) untouched — the predicate is evaluated 1:1");

    Expire(ctx);
    AssertEqual(5000, Dp(ctx, big, P1), "reverted");
}

async Task BaseDpSet()
{
    var ctx = Ctx();
    using var scope = AmbientMatchContext.Enter(ctx);
    var src = await Put(ctx, P1, "SRC", ChoiceZone.BattleArea);
    var t = await Put(ctx, P1, "TGT", ChoiceZone.BattleArea, dp: 5000);

    // AS-IS ChangeOriginDP OVERWRITES base DP (GetDP returns _changeValue()), it is NOT a delta. So a "set to
    // 9000" grant yields base 9000 regardless of the prior base — the old mirror approximated it with a delta.
    AssertTrue(CardEffectCommons.ChangeBaseDigimonDP(Perm(ctx, t, P1), 9000, EffectDuration.UntilEachTurnEnd, V(ctx, src, P1)), "base-set grant");
    AssertEqual(9000, BaseDp(ctx, t, P1), "base DP OVERWRITTEN to 9000");
    // a +2000 delta on top folds after the base override: 9000 + 2000.
    CardEffectCommons.ChangeDigimonDP(Perm(ctx, t, P1), 2000, EffectDuration.UntilEachTurnEnd, V(ctx, src, P1));
    AssertEqual(11000, Dp(ctx, t, P1), "delta folds on top of the overwritten base (9000 + 2000)");

    Expire(ctx);
    AssertEqual(5000, BaseDp(ctx, t, P1), "base reverted");
    AssertEqual(5000, Dp(ctx, t, P1), "DP reverted");
}

async Task UntilEndAttackBucket()
{
    var ctx = Ctx();
    using var scope = AmbientMatchContext.Enter(ctx);
    var src = await Put(ctx, P1, "SRC", ChoiceZone.BattleArea);
    var t = await Put(ctx, P1, "TGT", ChoiceZone.BattleArea, dp: 5000);

    CardEffectCommons.ChangeDigimonDP(Perm(ctx, t, P1), 2000, EffectDuration.UntilEndAttack, V(ctx, src, P1));
    AssertEqual(7000, Dp(ctx, t, P1), "UntilEndAttack grant folds while the attack is live");
    // The grant lives in the UntilEndAttackEffects bucket — the exact list AttackProcess.Cleanup() resets at
    // attack end (AttackProcess.cs:734), NOT a turn-end bucket.
    AssertEqual(1, new Permanent(ctx, t, P1).UntilEndAttackEffects.Count, "stored in the UntilEndAttack bucket");
    AssertEqual(0, new Permanent(ctx, t, P1).UntilEachTurnEndEffects.Count, "NOT in the turn-end bucket");
    // Simulate the attack-end reset (AttackProcess.Cleanup:734).
    new Permanent(ctx, t, P1).UntilEndAttackEffects = new();
    AssertEqual(5000, Dp(ctx, t, P1), "reverted at the attack-end bucket reset");
}

async Task NoGrantControl()
{
    var ctx = Ctx();
    using var scope = AmbientMatchContext.Enter(ctx);
    var t = await Put(ctx, P1, "TGT", ChoiceZone.BattleArea, dp: 5000);
    AssertEqual(5000, Dp(ctx, t, P1), "no grant → DP is the printed base");
    AssertEqual(1, Strike(ctx, t, P1), "no grant → SA is the base 1");
}

// --- Harness ---

EngineContext Ctx()
{
    var ctx = EngineContext.CreateDefault(randomSeed: 733);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.TurnController.SetPhase(HeadlessPhase.Main); // DoneStartGame gate for CanTrigger
    return ctx;
}

void Expire(EngineContext ctx) => new HeadlessEndTurnCleanupFlow().Cleanup(ctx, ctx.TurnController.Current);

async Task<HeadlessEntityId> Put(EngineContext ctx, HeadlessPlayerId owner, string tag, ChoiceZone zone, int dp = 5000, int level = 4)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = level }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone));
    return id;
}

CardSource V(EngineContext ctx, HeadlessEntityId id, HeadlessPlayerId o) => new(ctx, id, o, o);
Permanent Perm(EngineContext ctx, HeadlessEntityId id, HeadlessPlayerId o) => new(ctx, id, o);
int Dp(EngineContext ctx, HeadlessEntityId id, HeadlessPlayerId o) => new Permanent(ctx, id, o).DP;
int Strike(EngineContext ctx, HeadlessEntityId id, HeadlessPlayerId o) => new Permanent(ctx, id, o).Strike;
int BaseDp(EngineContext ctx, HeadlessEntityId id, HeadlessPlayerId o) => new Permanent(ctx, id, o).BaseDP;

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected {expected}, got {actual}.");
    }
}
