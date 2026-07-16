// B군 P0-1 witness: the general effect-immunity (AS-IS CardSource.CanNotBeAffected) and the memory-gain gate,
// rehomed off the dead ContinuousImmunityGate.BlocksOpponentEffect registry scan onto the AS-IS-literal live
// getters. These drive the SINK / Commons paths end-to-end (not a direct-read assertion) so a false-green
// (immunity granted but never consulted) is impossible.
//
// P0-1 (general immunity, live CardSource.CanNotBeAffected):
//   - a sink-direct Delete (SelectPermanentEffect Destroy mode) on a live-immune card is SKIPPED; a non-immune
//     control is deleted (MatchStateMutationSink.cs:527).
//   - a sink Bounce (ReturnToHand) and a sink PutSecurity (AddToSecurity) on a live-immune card are SKIPPED.
//   - an OWN-sourced delete on the SAME immune card still applies (source-relativity — opponent-only skillCondition).
//   - CardEffectCommons.GainCanNotAttack (GainRestrictionToPermanent grant-time guard, :2984) REFUSES to grant a
//     restriction to a live-immune target; a non-immune target accepts it.
// P1-1 (memory-gain gate keys on the GAINING player, not the causing effect's owner):
//   - a POSITIVE gauge delta from a NON-turn-player source is gated on the TURN player (the gainer), not the source
//     owner; a grant on the source owner does NOT block it.
//   - a NEGATIVE gauge delta (the opponent gains) is gated on the opponent; ungranted it applies.

using HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);   // turn player / protected side
HeadlessPlayerId P2 = new(2);   // opponent / effect source side

var tests = new (string Name, Func<Task> Body)[]
{
    ("sink Delete on a live-immune card is skipped; non-immune control is deleted", SinkDelete),
    ("sink Bounce (ReturnToHand) on a live-immune card is skipped", SinkBounce),
    ("sink PutSecurity (AddToSecurity) on a live-immune card is skipped", SinkPutSecurity),
    ("an OWN-sourced delete on the immune card still applies (source-relativity)", OwnSourceDeleteApplies),
    ("Commons GainCanNotAttack refuses a grant to a live-immune target; accepts a non-immune one", CommonsGrantRefused),
    ("memory: a +delta from a non-turn source gates the TURN player (gainer), not the source owner", MemPositiveGatesGainer),
    ("memory: a -delta (opponent gains) is gated on the opponent; ungranted it applies", MemNegativeGatesOpponent),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failures.Add(test.Name); Console.Error.WriteLine($"FAIL {test.Name}\n{ex.GetType().Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// ===== P0-1: general immunity via the live CardSource.CanNotBeAffected scan ==========================

async Task SinkDelete()
{
    EngineContext ctx = Ctx();
    using var _ = AmbientMatchContext.Enter(ctx);
    var immune = await Place(ctx, P1, "IMMUNE", "TfxCanNotBeAffected");
    var plain = await Place(ctx, P1, "PLAIN", "VANILLA");
    var enemy = await Place(ctx, P2, "ENEMY", "VANILLA");

    await Drive(ctx, new EffectMutation(MatchStateMutationSink.DeleteKind, enemy, Target(immune)));
    Assert(InZone(ctx, P1, ChoiceZone.BattleArea, immune), "immune card survives the opponent delete");
    Assert(!InZone(ctx, P1, ChoiceZone.Trash, immune), "immune card is not in trash");

    await Drive(ctx, new EffectMutation(MatchStateMutationSink.DeleteKind, enemy, Target(plain)));
    Assert(InZone(ctx, P1, ChoiceZone.Trash, plain), "non-immune control IS deleted");
}

async Task SinkBounce()
{
    EngineContext ctx = Ctx();
    using var _ = AmbientMatchContext.Enter(ctx);
    var immune = await Place(ctx, P1, "IMMUNE", "TfxCanNotBeAffected");
    var enemy = await Place(ctx, P2, "ENEMY", "VANILLA");

    await Drive(ctx, new EffectMutation(MatchStateMutationSink.ReturnToHandKind, enemy, Target(immune)));
    Assert(InZone(ctx, P1, ChoiceZone.BattleArea, immune), "immune card stays on the field (bounce skipped)");
    Assert(!InZone(ctx, P1, ChoiceZone.Hand, immune), "immune card was not returned to hand");
}

async Task SinkPutSecurity()
{
    EngineContext ctx = Ctx();
    using var _ = AmbientMatchContext.Enter(ctx);
    var immune = await Place(ctx, P1, "IMMUNE", "TfxCanNotBeAffected");
    var enemy = await Place(ctx, P2, "ENEMY", "VANILLA");

    await Drive(ctx, new EffectMutation(MatchStateMutationSink.AddToSecurityKind, enemy, Target(immune)));
    Assert(InZone(ctx, P1, ChoiceZone.BattleArea, immune), "immune card stays on the field (put-security skipped)");
    Assert(!InZone(ctx, P1, ChoiceZone.Security, immune), "immune card was not put into security");
}

async Task OwnSourceDeleteApplies()
{
    EngineContext ctx = Ctx();
    using var _ = AmbientMatchContext.Enter(ctx);
    var immune = await Place(ctx, P1, "IMMUNE", "TfxCanNotBeAffected");
    var ally = await Place(ctx, P1, "ALLY", "VANILLA");   // an OWN (same-controller) source

    await Drive(ctx, new EffectMutation(MatchStateMutationSink.DeleteKind, ally, Target(immune)));
    Assert(InZone(ctx, P1, ChoiceZone.Trash, immune), "an OWN delete applies (opponent-only immunity does not shield it)");
}

async Task CommonsGrantRefused()
{
    EngineContext ctx = Ctx();
    using var _ = AmbientMatchContext.Enter(ctx);
    var immune = await Place(ctx, P1, "IMMUNE", "TfxCanNotBeAffected");
    var plain = await Place(ctx, P1, "PLAIN", "VANILLA");
    var enemy = await Place(ctx, P2, "ENEMY", "VANILLA");
    var enemyCard = new CardSource(ctx, enemy, P2);

    bool grantedToImmune = CardEffectCommons.GainCanNotAttack(
        new Permanent(ctx, immune, P1), defenderCondition: null, EffectDuration.UntilOpponentTurnEnd, enemyCard);
    Assert(!grantedToImmune, "GainRestrictionToPermanent refuses a live-immune target (grant-time CanNotBeAffected guard)");

    bool grantedToPlain = CardEffectCommons.GainCanNotAttack(
        new Permanent(ctx, plain, P1), defenderCondition: null, EffectDuration.UntilOpponentTurnEnd, enemyCard);
    Assert(grantedToPlain, "GainRestrictionToPermanent accepts a non-immune target (control)");
}

// ===== P1-1: memory-gain gate keys on the GAINING player =============================================

async Task MemPositiveGatesGainer()
{
    // Turn player = P1. A NON-turn (P2) card drives a +delta -> the gauge rises -> the TURN player (P1) gains.
    // The gate must key on P1 (the gainer), NOT on the source's owner (P2).
    EngineContext ctx = Ctx();
    using var _ = AmbientMatchContext.Enter(ctx);
    var nonTurnSource = await Place(ctx, P2, "SRC", "VANILLA");

    // grant on the GAINER (turn player P1) -> blocked.
    await GrantCannotAddMemory(ctx, P1);
    ctx.MemoryController.Set(0);
    await Drive(ctx, new EffectMutation(MatchStateMutationSink.AddMemoryKind, nonTurnSource, Amount(3)));
    Assert(ctx.MemoryController.Current.Current == 0, "a +gain by the turn player is blocked by the turn player's restriction");

    // control: grant on the SOURCE OWNER (P2, not the gainer) -> NOT blocked.
    EngineContext ctx2 = Ctx();
    using var _2 = AmbientMatchContext.Enter(ctx2);
    var nonTurnSource2 = await Place(ctx2, P2, "SRC", "VANILLA");
    await GrantCannotAddMemory(ctx2, P2);
    ctx2.MemoryController.Set(0);
    await Drive(ctx2, new EffectMutation(MatchStateMutationSink.AddMemoryKind, nonTurnSource2, Amount(3)));
    Assert(ctx2.MemoryController.Current.Current == 3, "a restriction on the SOURCE owner (not the gainer) does not block the gain");
}

async Task MemNegativeGatesOpponent()
{
    // Turn player = P1. A P1 card drives a -delta -> the gauge falls -> the OPPONENT (P2) gains. The gate must key
    // on P2 (the gainer). The earlier port never gated a negative delta at all.
    EngineContext ctx = Ctx();
    using var _ = AmbientMatchContext.Enter(ctx);
    var turnSource = await Place(ctx, P1, "SRC", "VANILLA");

    await GrantCannotAddMemory(ctx, P2);
    ctx.MemoryController.Set(0);
    await Drive(ctx, new EffectMutation(MatchStateMutationSink.AddMemoryKind, turnSource, Amount(-2)));
    Assert(ctx.MemoryController.Current.Current == 0, "an opponent-gain (negative delta) is blocked by the opponent's restriction");

    // control: no grant -> the negative delta applies.
    EngineContext ctx2 = Ctx();
    using var _2 = AmbientMatchContext.Enter(ctx2);
    var turnSource2 = await Place(ctx2, P1, "SRC", "VANILLA");
    ResetMemoryFixture();
    ctx2.MemoryController.Set(0);
    await Drive(ctx2, new EffectMutation(MatchStateMutationSink.AddMemoryKind, turnSource2, Amount(-2)));
    Assert(ctx2.MemoryController.Current.Current == -2, "ungranted, the opponent gain applies");
}

// ===== harness ======================================================================================

EngineContext Ctx()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 611);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);   // P1 is the turn player
    context.TurnController.SetPhase(HeadlessPhase.Main);        // CanUse -> DoneStartGame gate past Setup
    return context;
}

async Task<HeadlessEntityId> Place(EngineContext ctx, HeadlessPlayerId owner, string tag, string cardNumber)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}:{cardNumber}");
    cards.Upsert(new CardRecord(defId, cardNumber, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000 }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

async Task GrantCannotAddMemory(EngineContext ctx, HeadlessPlayerId restricted)
{
    TfxCannotAddMemory.ScopePlayer = restricted;
    TfxCannotAddMemory.CausingPredicate = null;
    await Place(ctx, restricted, "MEMLOCK", "TfxCannotAddMemory");
}

void ResetMemoryFixture()
{
    TfxCannotAddMemory.ScopePlayer = default;
    TfxCannotAddMemory.CausingPredicate = null;
}

async Task Drive(EngineContext ctx, EffectMutation mutation)
{
    var sink = new MatchStateMutationSink(
        ctx.CardInstanceRepository, log: null, ctx.ZoneMover, ctx.MemoryController,
        ctx.EffectRegistry, ctx.GameEventQueue, context: ctx);
    sink.Apply(mutation);
    await sink.FlushAsync();
}

Dictionary<string, object?> Target(HeadlessEntityId id) =>
    new(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = id.Value };

Dictionary<string, object?> Amount(int amount) =>
    new(StringComparer.Ordinal) { [MatchStateMutationSink.AmountKey] = amount };

bool InZone(EngineContext ctx, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId id) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(player, zone).Contains(id);

static void Assert(bool value, string label) { if (!value) throw new InvalidOperationException($"{label}: expected true."); }
