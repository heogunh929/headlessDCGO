// J-2 witness: the CanNotUnsuspend grant wrappers, flipped off the invented (reader-INERT) EffectRegistry
// CannotUnsuspendKey arm onto the AS-IS bucket storage (AddEffectToPermanent / AddEffectToPlayer, timing None), read
// LIVE by the AS-IS-literal interface scan Permanent.CanUnsuspend (Permanent.cs:3022 field arm / :3050 player arm) and
// consumed by the unsuspend step at TurnStateMachine.ActivePhaseAsync:222. RD-RC-02 resolution: BT7_055 / ST17_08 /
// BT1_113's "can't unsuspend" text WORKS for the first time.
//
// Each subtest drives the REAL turn-flow unsuspend step (TurnStateMachine.For(ctx).ActivePhaseAsync — which contains
// TSM:222 `permanent.IsSuspended && permanent.CanUnsuspend` and the per-duration bucket resets at TSM:256/259), then
// reads the card's suspend state. The assertion goes through Permanent.CanUnsuspend + TSM:222, never a direct getter
// poke alone. A CONTROL card (ungranted / non-matching) unsuspends in the same drive so a "unsuspend never ran"
// false-green is impossible.
//
//   (a) BT7_055 idiom — permanent-scope GainCanNotUnsuspend(UntilNextUntap): the target permanent stays suspended
//       while the grant lives (unsuspend step SKIPS it), the grant expires at the AS-IS reset (TSM:259 per-permanent
//       UntilNextUntap bucket), and the NEXT unsuspend works.
//   (b) BT1_113 idiom — player-scope GainCanNotUnsuspendPlayerEffect(UntilOwnerActivePhase, isOnlyActivePhase:true):
//       a matching permanent is blocked during its owner's active phase, a non-matching one unsuspends (predicate
//       narrowing), and the grant expires at the AS-IS reset (TSM:256 turn-player UntilOwnerActivePhase bucket, which
//       AddEffectToPlayer routes into the card owner's Enemy).
//   (c) RD-J-01: a grant onto a live-immune target LANDS (AS-IS grants unconditionally — no grant-time refusal) but
//       is INERT while immunity holds (CanUnsuspend stays TRUE), and the SAME grant becomes ACTIVE once immunity
//       lifts (CanUnsuspend flips FALSE) — the re-application semantics the invented guard broke.

using HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;   // TfxCanNotBeAffectedToggle
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;   // CardEffectCommons / Permanent / CardSource / TurnStateMachine
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("(a) BT7_055 UntilNextUntap: granted permanent stays suspended (TSM:222 skip), control unsuspends", A_PermanentGrantSkipsUnsuspend),
    ("(a) BT7_055 UntilNextUntap: grant expires at TSM:259 reset, next unsuspend works", A_PermanentGrantExpiresNextUntap),
    ("(b) BT1_113 player-scope UntilOwnerActivePhase: matching blocked, non-matching unsuspends", B_PlayerScopeGrantNarrows),
    ("(b) BT1_113 player-scope: grant expires at TSM:256 reset, next unsuspend works", B_PlayerScopeGrantExpires),
    ("(c) RD-J-01: grant LANDS on a live-immune target — inert while immune, ACTIVE once immunity lifts", C_ImmuneGrantLandsInertUntilImmunityLifts),
    ("(d) ST17_08 UntilOpponentTurnEnd: blocked through opponent turn, cleared by REAL HeadlessEndTurnCleanupFlow, unsuspends next untap", D_UntilOpponentTurnEndClearedByEndTurnCleanup),
};

int failed = 0;
foreach ((string name, Func<Task> body) in tests)
{
    try { await body(); Console.WriteLine($"PASS {name}"); }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"FAIL {name}");
        Console.WriteLine($"     {ex.GetType().Name}: {ex.Message}");
        if (ex.StackTrace is string st) { Console.WriteLine(string.Join('\n', st.Split('\n').Take(6))); }
    }
}

Console.WriteLine($"SUMMARY: PASS={tests.Length - failed} FAIL={failed} TOTAL={tests.Length}");
if (failed > 0) { Environment.Exit(1); }

// ===== (a) permanent-scope grant (BT7_055 idiom) =====================================================

async Task A_PermanentGrantSkipsUnsuspend()
{
    EngineContext ctx = Ctx(turnPlayer: P2);   // P2 = turn player; its own suspended digimons unsuspend at TSM:222.
    using AmbientMatchContext.Scope _scope = AmbientMatchContext.Enter(ctx);
    HeadlessEntityId source = Place(ctx, P1, "A-SRC", "VANILLA", suspended: false);   // BT7_055 owner
    HeadlessEntityId target = Place(ctx, P2, "A-TGT", "VANILLA", suspended: true);
    HeadlessEntityId control = Place(ctx, P2, "A-CTL", "VANILLA", suspended: true);

    bool granted = CardEffectCommons.GainCanNotUnsuspend(
        new Permanent(ctx, target, P2), EffectDuration.UntilNextUntap, new CardSource(ctx, source, P1),
        condition: () => true, effectName: "This Digimon can't unsuspend.");
    Assert(granted, "grant accepted on a non-immune target");
    Assert(!new Permanent(ctx, target, P2).CanUnsuspend, "reader: target CanUnsuspend is FALSE while the grant lives");

    await DriveActivePhase(ctx);

    Assert(new Permanent(ctx, target, P2).IsSuspended, "granted target STAYS suspended (TSM:222 honoured CanUnsuspend)");
    Assert(!new Permanent(ctx, control, P2).IsSuspended, "ungranted control DID unsuspend (the step actually ran)");
}

async Task A_PermanentGrantExpiresNextUntap()
{
    EngineContext ctx = Ctx(turnPlayer: P2);
    using AmbientMatchContext.Scope _scope = AmbientMatchContext.Enter(ctx);
    HeadlessEntityId source = Place(ctx, P1, "A2-SRC", "VANILLA", suspended: false);
    HeadlessEntityId target = Place(ctx, P2, "A2-TGT", "VANILLA", suspended: true);

    CardEffectCommons.GainCanNotUnsuspend(
        new Permanent(ctx, target, P2), EffectDuration.UntilNextUntap, new CardSource(ctx, source, P1),
        condition: () => true, effectName: "This Digimon can't unsuspend.");

    await DriveActivePhase(ctx);   // #1: stays suspended, then TSM:259 clears the UntilNextUntap bucket.
    Assert(new Permanent(ctx, target, P2).IsSuspended, "still suspended after the blocked unsuspend");
    Assert(new Permanent(ctx, target, P2).CanUnsuspend, "grant expired at the TSM:259 reset (CanUnsuspend flipped TRUE)");

    await DriveActivePhase(ctx);   // #2: the next unsuspend now works.
    Assert(!new Permanent(ctx, target, P2).IsSuspended, "the NEXT unsuspend works (target unsuspended)");
}

// ===== (b) player-scope grant (BT1_113 idiom) =======================================================

async Task B_PlayerScopeGrantNarrows()
{
    EngineContext ctx = Ctx(turnPlayer: P2);   // block fires during the card-owner's ENEMY (P2)'s active phase.
    using AmbientMatchContext.Scope _scope = AmbientMatchContext.Enter(ctx);
    HeadlessEntityId source = Place(ctx, P1, "B-SRC", "VANILLA", suspended: false);   // BT1_113 owner
    HeadlessEntityId target = Place(ctx, P2, "B-TGT", "VANILLA", suspended: true);
    HeadlessEntityId other = Place(ctx, P2, "B-OTH", "VANILLA", suspended: true);

    bool granted = CardEffectCommons.GainCanNotUnsuspendPlayerEffect(
        permanentCondition: p => p.InstanceId == target, EffectDuration.UntilOwnerActivePhase,
        new CardSource(ctx, source, P1), isOnlyActivePhase: true, effectName: "Your Digimon can't unsuspend");
    Assert(granted, "player-scope grant accepted");

    // isOnlyActivePhase narrowing: the grant's CanUseCondition gates on the Active phase, so at the CURRENT Main
    // phase nothing is blocked yet (a positive demonstration of the phase narrowing).
    Assert(new Permanent(ctx, target, P2).CanUnsuspend, "isOnlyActivePhase: matching target NOT blocked outside the Active phase (Main)");

    await DriveActivePhase(ctx);   // ActivePhaseAsync enters the Active phase, so the block applies at TSM:222.

    Assert(new Permanent(ctx, target, P2).IsSuspended, "matching target STAYS suspended during the Active phase (player-scope block via TSM:222)");
    Assert(!new Permanent(ctx, other, P2).IsSuspended, "non-matching permanent unsuspended (predicate narrowing)");
}

async Task B_PlayerScopeGrantExpires()
{
    EngineContext ctx = Ctx(turnPlayer: P2);
    using AmbientMatchContext.Scope _scope = AmbientMatchContext.Enter(ctx);
    HeadlessEntityId source = Place(ctx, P1, "B2-SRC", "VANILLA", suspended: false);
    HeadlessEntityId target = Place(ctx, P2, "B2-TGT", "VANILLA", suspended: true);

    CardEffectCommons.GainCanNotUnsuspendPlayerEffect(
        permanentCondition: p => p.InstanceId == target, EffectDuration.UntilOwnerActivePhase,
        new CardSource(ctx, source, P1), isOnlyActivePhase: true, effectName: "Your Digimon can't unsuspend");

    await DriveActivePhase(ctx);   // #1: blocked, then TSM:256 clears the turn-player UntilOwnerActivePhase bucket.
    Assert(new Permanent(ctx, target, P2).IsSuspended, "still suspended after the blocked unsuspend");
    Assert(new Permanent(ctx, target, P2).CanUnsuspend, "grant expired at the TSM:256 reset (CanUnsuspend flipped TRUE)");

    await DriveActivePhase(ctx);   // #2: the next unsuspend works.
    Assert(!new Permanent(ctx, target, P2).IsSuspended, "the NEXT unsuspend works (target unsuspended)");
}

// ===== (c) immunity guard ===========================================================================

async Task C_ImmuneGrantLandsInertUntilImmunityLifts()
{
    TfxCanNotBeAffectedToggle.ImmunityActive = true;
    try
    {
        EngineContext ctx = Ctx(turnPlayer: P1);   // P1 owns the (immune) targets; P2 is the opponent effect source.
        using AmbientMatchContext.Scope _scope = AmbientMatchContext.Enter(ctx);
        HeadlessEntityId source = Place(ctx, P2, "C-SRC", "VANILLA", suspended: false);   // opponent of the targets
        HeadlessEntityId immune = Place(ctx, P1, "C-IMM", "TfxCanNotBeAffectedToggle", suspended: true);
        HeadlessEntityId plain = Place(ctx, P1, "C-PLN", "VANILLA", suspended: true);

        // (RD-J-01) AS-IS grants unconditionally: the grant LANDS even on a live-immune target (no grant-time refusal).
        bool grantedToImmune = CardEffectCommons.GainCanNotUnsuspend(
            new Permanent(ctx, immune, P1), EffectDuration.UntilNextUntap, new CardSource(ctx, source, P2),
            condition: () => true, effectName: "This Digimon can't unsuspend.");
        Assert(grantedToImmune, "RD-J-01: GainCanNotUnsuspend LANDS on a live-immune target (AS-IS unconditional grant)");

        bool grantedToPlain = CardEffectCommons.GainCanNotUnsuspend(
            new Permanent(ctx, plain, P1), EffectDuration.UntilNextUntap, new CardSource(ctx, source, P2),
            condition: () => true, effectName: "This Digimon can't unsuspend.");
        Assert(grantedToPlain, "control: the grant lands on a non-immune target");

        // While immune: the granted block is INERT via the live CanUnsuspend read (immunity gates its CanUse). The
        // non-immune control is blocked.
        Assert(new Permanent(ctx, immune, P1).CanUnsuspend, "inert while immune: the granted can't-unsuspend block does not bite (CanUnsuspend TRUE)");
        Assert(!new Permanent(ctx, plain, P1).CanUnsuspend, "control: the block bites a non-immune target (CanUnsuspend FALSE)");

        // Immunity expires -> the SAME granted block becomes ACTIVE (CanUnsuspend FALSE). The invented grant-time
        // refusal would have stored nothing, so this re-application could never happen.
        TfxCanNotBeAffectedToggle.ImmunityActive = false;
        Assert(!new Permanent(ctx, immune, P1).CanUnsuspend, "ACTIVE after immunity lifts: the same granted block now bites (CanUnsuspend FALSE — AS-IS re-application)");
    }
    finally
    {
        TfxCanNotBeAffectedToggle.ImmunityActive = true;
    }
}

// ===== (d) UntilOpponentTurnEnd cleared by the REAL end-turn cleanup (ST17_08 idiom) ================

async Task D_UntilOpponentTurnEndClearedByEndTurnCleanup()
{
    // (RD-J-04) ST17_08 makes its OWN Digimon "can't unsuspend until the end of your opponent's turn" — an
    // UntilOpponentTurnEnd grant from a P1 source onto a P1 permanent. AddEffectToPermanent's owner-relative swap
    // (IsOwnerPermanent true -> UntilOpponentTurnEndEffects) stores it so the block holds through the OPPONENT
    // (P2)'s turn and clears when P2's turn ENDS via the REAL HeadlessEndTurnCleanupFlow: the ending turn is P2's,
    // so nonTurnPlayer = P1 and every P1 permanent drops UntilOpponentTurnEndEffects (HeadlessEndTurnCleanupFlow
    // :139-142, the AS-IS TurnStateMachine EndPhase reset). The target then unsuspends on its next untap (P1's
    // active phase). Drives the real cleanup flow — not a direct bucket poke.
    EngineContext ctx = Ctx(turnPlayer: P2);   // it is the opponent (P2)'s turn while the block holds
    using AmbientMatchContext.Scope _scope = AmbientMatchContext.Enter(ctx);
    HeadlessEntityId source = Place(ctx, P1, "D-SRC", "VANILLA", suspended: false);   // ST17_08 owner (P1's own card)
    HeadlessEntityId target = Place(ctx, P1, "D-TGT", "VANILLA", suspended: true);
    HeadlessEntityId control = Place(ctx, P1, "D-CTL", "VANILLA", suspended: true);

    bool granted = CardEffectCommons.GainCantUnsuspendUntilOpponentTurnEnd(
        new Permanent(ctx, target, P1), new CardSource(ctx, source, P1));
    Assert(granted, "grant accepted on a non-immune target");
    Assert(!new Permanent(ctx, target, P1).CanUnsuspend, "reader: target blocked while the grant lives (opponent P2's turn)");

    // End the opponent (P2)'s turn through the REAL cleanup flow — clears P1's UntilOpponentTurnEnd bucket.
    new HeadlessEndTurnCleanupFlow().Cleanup(ctx, ctx.TurnController.Current);
    Assert(new Permanent(ctx, target, P1).CanUnsuspend, "grant cleared by HeadlessEndTurnCleanupFlow (CanUnsuspend flipped TRUE)");

    // The target's next untap is P1's active phase (the turn after P2's ended); drive the real unsuspend step.
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.TurnController.SetPhase(HeadlessPhase.Main);
    await DriveActivePhase(ctx);
    Assert(!new Permanent(ctx, target, P1).IsSuspended, "target unsuspends on its next untap (P1's active phase)");
    Assert(!new Permanent(ctx, control, P1).IsSuspended, "control also unsuspended (the step actually ran)");
}

// ===== harness ======================================================================================

EngineContext Ctx(HeadlessPlayerId turnPlayer)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 722);
    context.TurnController.Initialize(new[] { P1, P2 }, turnPlayer);
    context.TurnController.SetPhase(HeadlessPhase.Main);   // past None so DoneStartGame / CanUse gates are open
    return context;
}

HeadlessEntityId Place(EngineContext ctx, HeadlessPlayerId owner, string tag, string cardNumber, bool suspended)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}:{cardNumber}");
    cards.Upsert(new CardRecord(defId, cardNumber, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = suspended }));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
    return id;
}

async Task DriveActivePhase(EngineContext ctx)
{
    // Callers already hold an AmbientMatchContext scope (effect evaluation reads GManager.instance via it).
    await TurnStateMachine.For(ctx).ActivePhaseAsync();
}

static void Assert(bool value, string label) { if (!value) throw new InvalidOperationException($"{label}: expected true."); }
