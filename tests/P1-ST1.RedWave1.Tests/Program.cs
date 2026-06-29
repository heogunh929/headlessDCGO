using HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST1.Red;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// Phase 1 — ST1 Red wave 1: inherited continuous self-modifiers with condition gating.
//   ST1_07: inherited <Security Attack +1>            (unconditional)
//   ST1_03: inherited [Your Turn] DP +1000           (owner-turn condition)
//   ST1_01: inherited [Your Turn] DP +1000 if >= 4 digivolution sources
// Each is registered against a buried digivolution SOURCE; the gate folds it into the TOP card only while
// the source is buried, the permanent is the owner's, and the card's condition holds.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
HeadlessEntityId Top = new("p1:battle:TOP");

var tests = new (string Name, Func<Task> Body)[]
{
    ("ST1_07: inherited Security Attack +1 reaches the top card", ST1_07_SecurityAttack),
    ("ST1_03: inherited DP +1000 only on the owner's turn", ST1_03_OwnerTurnDp),
    ("ST1_01: inherited DP +1000 only with >= 4 sources", ST1_01_SourceCountDp),
    ("ST1_11: dynamic Security Attack +(sources / 2) on the owner's turn", ST1_11_DynamicSecurityAttack),
    ("ST1_12: player-scope +1000 DP to owner's Digimon on the owner's turn", ST1_12_PlayerScopeDp),
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

async Task ST1_07_SecurityAttack()
{
    (EngineContext context, HeadlessEntityId source) = await StackOf(1);   // 1 source = ST1_07
    Register(context, new ST1_07(), "ST1_07", source);
    AssertEqual(2, ContinuousModifierGate.ResolveSecurityAttack(context, Top, baseSecurityAttack: 1), "inherited SA +1 on top");
}

async Task ST1_03_OwnerTurnDp()
{
    (EngineContext context, HeadlessEntityId source) = await StackOf(1);
    Register(context, new ST1_03(), "ST1_03", source);

    context.TurnController.Initialize(new[] { P1, P2 }, P1);   // owner's turn
    AssertEqual(3000, ContinuousDpGate.ResolveDp(context, Top, baseDp: 2000), "owner turn: +1000");

    context.TurnController.EndTurn();                          // opponent's turn
    AssertEqual(2000, ContinuousDpGate.ResolveDp(context, Top, baseDp: 2000), "opponent turn: no buff");
}

async Task ST1_01_SourceCountDp()
{
    (EngineContext four, HeadlessEntityId source4) = await StackOf(4);
    Register(four, new ST1_01(), "ST1_01", source4);
    four.TurnController.Initialize(new[] { P1, P2 }, P1);
    AssertEqual(3000, ContinuousDpGate.ResolveDp(four, Top, baseDp: 2000), "4 sources, owner turn: +1000");

    (EngineContext two, HeadlessEntityId source2) = await StackOf(2);
    Register(two, new ST1_01(), "ST1_01", source2);
    two.TurnController.Initialize(new[] { P1, P2 }, P1);
    AssertEqual(2000, ContinuousDpGate.ResolveDp(two, Top, baseDp: 2000), "2 sources: no buff");
}

// ST1_11 is a MAIN (non-inherited) effect on the top card itself: SA + (own source count / 2).
async Task ST1_11_DynamicSecurityAttack()
{
    (EngineContext four, _) = await StackOf(4);
    Register(four, new ST1_11(), "ST1_11", Top);                 // registered on the top card
    four.TurnController.Initialize(new[] { P1, P2 }, P1);
    AssertEqual(3, ContinuousModifierGate.ResolveSecurityAttack(four, Top, baseSecurityAttack: 1), "4 sources -> +2 SA");

    (EngineContext one, _) = await StackOf(1);
    Register(one, new ST1_11(), "ST1_11", Top);
    one.TurnController.Initialize(new[] { P1, P2 }, P1);
    AssertEqual(1, ContinuousModifierGate.ResolveSecurityAttack(one, Top, baseSecurityAttack: 1), "1 source -> count 0 -> base");
}

// ST1_12 is a Tamer: a player-scope continuous "[Your Turn] your Digimon get +1000 DP".
async Task ST1_12_PlayerScopeDp()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 12);
    CardDatabase cards = (CardDatabase)ctx.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId("TAMER"), "TAMER", "Tai", new Dictionary<string, object?>(), CardType: "Tamer"));
    cards.Upsert(new CardRecord(new HeadlessEntityId("MYDIGI"), "MYDIGI", "Greymon", new Dictionary<string, object?>(), CardType: "Digimon"));
    cards.Upsert(new CardRecord(new HeadlessEntityId("OPPDIGI"), "OPPDIGI", "Greymon", new Dictionary<string, object?>(), CardType: "Digimon"));

    var tamer = new HeadlessEntityId("p1:battle:T");
    var mine = new HeadlessEntityId("p1:battle:D");
    var opp = new HeadlessEntityId("p2:battle:D");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(tamer, new HeadlessEntityId("TAMER"), P1));
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(mine, new HeadlessEntityId("MYDIGI"), P1));
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(opp, new HeadlessEntityId("OPPDIGI"), P2));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, tamer, ChoiceZone.None, ChoiceZone.BattleArea));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, mine, ChoiceZone.None, ChoiceZone.BattleArea));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, opp, ChoiceZone.None, ChoiceZone.BattleArea));

    CardEffectRegistrar.RegisterOnEnterPlay(ctx, new ST1_12(), "ST1_12", new CardSource(ctx, tamer, P1));
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);

    AssertEqual(3000, ContinuousDpGate.ResolveDp(ctx, mine, baseDp: 2000), "owner's Digimon +1000 on owner turn");
    AssertEqual(2000, ContinuousDpGate.ResolveDp(ctx, opp, baseDp: 2000), "opponent's Digimon unaffected");

    ctx.TurnController.EndTurn();
    AssertEqual(2000, ContinuousDpGate.ResolveDp(ctx, mine, baseDp: 2000), "no buff on opponent turn");
}

// --- Helpers -------------------------------------------------------------

// Builds a battle-area permanent owned by P1 with `sourceCount` buried sources; returns the context and
// the id of the deepest source (the one we register the ported effect onto).
async Task<(EngineContext, HeadlessEntityId)> StackOf(int sourceCount)
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 11);
    CardDatabase cards = (CardDatabase)ctx.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId("TOPDEF"), "TOPDEF", "Greymon", new Dictionary<string, object?>(), CardType: "Digimon"));
    cards.Upsert(new CardRecord(new HeadlessEntityId("SRCDEF"), "SRCDEF", "Agumon", new Dictionary<string, object?>(), CardType: "Digimon"));

    var sourceIds = new List<string>();
    for (int i = 0; i < sourceCount; i++)
    {
        var sid = new HeadlessEntityId($"p1:src:S{i}");
        ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(sid, new HeadlessEntityId("SRCDEF"), P1));
        sourceIds.Add(sid.Value);
    }

    // sourceIds are stored newest-under-card first; the deepest (DigiEgg) is last.
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["sourceIds"] = sourceIds };
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(Top, new HeadlessEntityId("TOPDEF"), P1, Metadata: meta));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, Top, ChoiceZone.None, ChoiceZone.BattleArea));

    HeadlessEntityId deepest = new($"p1:src:S{sourceCount - 1}");
    return (ctx, deepest);
}

void Register(EngineContext ctx, CEntity_Effect effect, string cardNumber, HeadlessEntityId source) =>
    CardEffectRegistrar.RegisterOnEnterPlay(ctx, effect, cardNumber, new CardSource(ctx, source, P1));

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
