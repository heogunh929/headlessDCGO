namespace ST1RedTests;

using HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST1.Red;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// Inherited / conditional / dynamic / player-scope continuous self-modifiers: ST1_07/03/01/11/12.
internal static class Wave1Tests
{
    private static readonly HeadlessPlayerId P1 = new(1);
    private static readonly HeadlessPlayerId P2 = new(2);
    private static readonly HeadlessEntityId Top = new("p1:battle:TOP");

    public static (string Name, Func<Task> Body)[] Cases => new (string, Func<Task>)[]
    {
        ("ST1_07: inherited Security Attack +1 reaches the top card", ST1_07_SecurityAttack),
        ("ST1_03: inherited DP +1000 only on the owner's turn", ST1_03_OwnerTurnDp),
        ("ST1_01: inherited DP +1000 only with >= 4 sources", ST1_01_SourceCountDp),
        ("ST1_11: dynamic Security Attack +(sources / 2) on the owner's turn", ST1_11_DynamicSecurityAttack),
        ("ST1_12: player-scope +1000 DP to owner's Digimon on the owner's turn", ST1_12_PlayerScopeDp),
    };

    private static async Task ST1_07_SecurityAttack()
    {
        (EngineContext context, HeadlessEntityId source) = await StackOf(1);
        Register(context, new ST1_07(), "ST1_07", source);
        AssertEqual(2, ContinuousModifierGate.ResolveSecurityAttack(context, Top, baseSecurityAttack: 1), "inherited SA +1 on top");
    }

    private static async Task ST1_03_OwnerTurnDp()
    {
        (EngineContext context, HeadlessEntityId source) = await StackOf(1);
        Register(context, new ST1_03(), "ST1_03", source);

        context.TurnController.Initialize(new[] { P1, P2 }, P1);
        AssertEqual(3000, ContinuousDpGate.ResolveDp(context, Top, baseDp: 2000), "owner turn: +1000");

        context.TurnController.EndTurn();
        AssertEqual(2000, ContinuousDpGate.ResolveDp(context, Top, baseDp: 2000), "opponent turn: no buff");
    }

    private static async Task ST1_01_SourceCountDp()
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

    private static async Task ST1_11_DynamicSecurityAttack()
    {
        (EngineContext four, _) = await StackOf(4);
        Register(four, new ST1_11(), "ST1_11", Top);
        four.TurnController.Initialize(new[] { P1, P2 }, P1);
        AssertEqual(3, ContinuousModifierGate.ResolveSecurityAttack(four, Top, baseSecurityAttack: 1), "4 sources -> +2 SA");

        (EngineContext one, _) = await StackOf(1);
        Register(one, new ST1_11(), "ST1_11", Top);
        one.TurnController.Initialize(new[] { P1, P2 }, P1);
        AssertEqual(1, ContinuousModifierGate.ResolveSecurityAttack(one, Top, baseSecurityAttack: 1), "1 source -> count 0 -> base");
    }

    private static async Task ST1_12_PlayerScopeDp()
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

    private static async Task<(EngineContext, HeadlessEntityId)> StackOf(int sourceCount)
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

        var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["sourceIds"] = sourceIds };
        ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(Top, new HeadlessEntityId("TOPDEF"), P1, Metadata: meta));
        await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, Top, ChoiceZone.None, ChoiceZone.BattleArea));

        HeadlessEntityId deepest = new($"p1:src:S{sourceCount - 1}");
        return (ctx, deepest);
    }

    private static void Register(EngineContext ctx, CEntity_Effect effect, string cardNumber, HeadlessEntityId source) =>
        CardEffectRegistrar.RegisterOnEnterPlay(ctx, effect, cardNumber, new CardSource(ctx, source, P1));

    private static void AssertEqual<T>(T expected, T actual, string label)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
    }
}
