namespace ST1RedTests;

using HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST1.Red;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// Inherited / conditional / dynamic / player-scope continuous self-modifiers: ST1_07/03/01/11/12.
//
// (deferred-queue re-aim, ST2.Blue toolbox) These were OLD-model white-box reads: after RegisterOnEnterPlay
// they read Permanent.DP/Strike with the turn phase left at HeadlessPhase.None. The uniform-사멸 flip made the
// continuous DP/SA modifiers new-model kind-classes served by the LIVE EffectList scan (NewModelContinuousScan),
// whose CanUse -> ICardEffect.CanTrigger gates on TurnStateMachine.DoneStartGame (phase != None). With phase
// None the scan is inert, so every DP/SA read returned the bare base (0 with no printed dp). Re-aimed onto the
// live surface exactly as ST2_01/08 did: SetPhase(HeadlessPhase.Main) opens the DoneStartGame gate, the top
// carries a base dp, and Strike (which — unlike DP — does not self-scope AmbientMatchContext) is read inside the
// match scope. RegisterOnEnterPlay is retained: for a new-model kind-class it registers no binding but ATTACHES
// the effect to the instance's cEntity_EffectController, which is what the live EffectList scan reads.
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
        // Permanent.Strike (unlike Permanent.DP) does not self-scope AmbientMatchContext — read inside the match
        // scope, the same convention ST2_08 established for the security-attack fold.
        using (AmbientMatchContext.Enter(context))
        {
            AssertEqual(2, new HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.Permanent(context, Top).Strike, "inherited SA +1 on top");
        }
    }

    private static async Task ST1_03_OwnerTurnDp()
    {
        (EngineContext context, HeadlessEntityId source) = await StackOf(1);
        Register(context, new ST1_03(), "ST1_03", source);

        // Owner's turn (StackOf initialised P1 as the turn player): the inherited [Your Turn] +1000 applies.
        AssertEqual(3000, new HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.Permanent(context, Top).DP, "owner turn: +1000");

        // EndTurn advances to the opponent's turn (phase Active — DoneStartGame stays open); the IsOwnerTurn
        // condition now reads false, so the buff is off (a true condition read, not a closed gate).
        context.TurnController.EndTurn();
        AssertEqual(2000, new HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.Permanent(context, Top).DP, "opponent turn: no buff");
    }

    private static async Task ST1_01_SourceCountDp()
    {
        (EngineContext four, HeadlessEntityId source4) = await StackOf(4);
        Register(four, new ST1_01(), "ST1_01", source4);
        AssertEqual(3000, new HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.Permanent(four, Top).DP, "4 sources, owner turn: +1000");

        (EngineContext two, HeadlessEntityId source2) = await StackOf(2);
        Register(two, new ST1_01(), "ST1_01", source2);
        AssertEqual(2000, new HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.Permanent(two, Top).DP, "2 sources: no buff");
    }

    private static async Task ST1_11_DynamicSecurityAttack()
    {
        (EngineContext four, _) = await StackOf(4);
        Register(four, new ST1_11(), "ST1_11", Top);
        using (AmbientMatchContext.Enter(four))
        {
            AssertEqual(3, new HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.Permanent(four, Top).Strike, "4 sources -> +2 SA");
        }

        (EngineContext one, _) = await StackOf(1);
        Register(one, new ST1_11(), "ST1_11", Top);
        using (AmbientMatchContext.Enter(one))
        {
            AssertEqual(1, new HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.Permanent(one, Top).Strike, "1 source -> count 0 -> base");
        }
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
        var baseDp = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 2000 };
        ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(tamer, new HeadlessEntityId("TAMER"), P1));
        ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(mine, new HeadlessEntityId("MYDIGI"), P1, Metadata: new Dictionary<string, object?>(baseDp, StringComparer.Ordinal)));
        ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(opp, new HeadlessEntityId("OPPDIGI"), P2, Metadata: new Dictionary<string, object?>(baseDp, StringComparer.Ordinal)));
        await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, tamer, ChoiceZone.None, ChoiceZone.BattleArea));
        await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, mine, ChoiceZone.None, ChoiceZone.BattleArea));
        await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, opp, ChoiceZone.None, ChoiceZone.BattleArea));

        CardEffectRegistrar.RegisterOnEnterPlay(ctx, new ST1_12(), "ST1_12", new CardSource(ctx, tamer, P1));
        ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
        ctx.TurnController.SetPhase(HeadlessPhase.Main);

        AssertEqual(3000, new HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.Permanent(ctx, mine).DP, "owner's Digimon +1000 on owner turn");
        AssertEqual(2000, new HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.Permanent(ctx, opp).DP, "opponent's Digimon unaffected");

        ctx.TurnController.EndTurn();
        AssertEqual(2000, new HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.Permanent(ctx, mine).DP, "no buff on opponent turn");
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

        // (re-aim) The top carries a base dp so the +1000 buff reads over a real base (2000 -> 3000), the same
        // ST2_01 SelfStack convention.
        var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["sourceIds"] = sourceIds, ["dp"] = 2000 };
        ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(Top, new HeadlessEntityId("TOPDEF"), P1, Metadata: meta));
        await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, Top, ChoiceZone.None, ChoiceZone.BattleArea));

        // (re-aim) Owner is the turn player and the game is past setup (phase != None), so the continuous scan's
        // CanTrigger DoneStartGame gate is open — the [Your Turn] modifiers become live.
        ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
        ctx.TurnController.SetPhase(HeadlessPhase.Main);

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
