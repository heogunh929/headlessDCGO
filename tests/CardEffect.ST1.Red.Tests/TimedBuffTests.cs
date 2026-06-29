namespace ST1RedTests;

using HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST1.Red;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// Duration-tagged / zone-scoped activated buffs: ST1_13 ([Main] select +3000, [Security] SA +1),
// ST1_14 (Security-Digimon +7000 DP), ST1_08 ([When Digivolving] select +3000).
internal static class TimedBuffTests
{
    private static readonly HeadlessPlayerId P1 = new(1);
    private static readonly HeadlessPlayerId P2 = new(2);

    public static (string Name, Func<Task> Body)[] Cases => new (string, Func<Task>)[]
    {
        ("ST1_13 [Main]: chosen Digimon gets +3000 DP, expiring at turn end", ST1_13_MainDpBuff),
        ("ST1_13 [Security]: all your Digimon get SA +1, expiring at your turn end", ST1_13_SecuritySaBuff),
        ("ST1_14 [Main]: only your Security Digimon get +7000 DP (battle area unaffected)", ST1_14_SecurityZoneScope),
        ("ST1_14 [Security]: Security Digimon +7000 DP expires at turn end", ST1_14_Expiry),
        ("ST1_08 [When Digivolving]: select 1 of your Digimon for +3000 DP", ST1_08_DigivolveBuff),
    };

    private static async Task ST1_13_MainDpBuff()
    {
        (EngineContext context, HeadlessEntityId mine) = await OwnDigimon();
        context.TurnController.Initialize(new[] { P1, P2 }, P1);

        var effect = (ActivatedTargetBuffEffect)Effect(new ST1_13(), EffectTiming.OptionSkill, context);
        ChoiceRequest request = effect.BuildRequest(new[] { P1, P2 });
        AssertEqual(1, request.Candidates.Count, "only the owner's Digimon is a candidate");

        var provider = new ScriptedChoiceProvider();
        provider.Enqueue(ChoiceResult.Select(mine));
        ChoiceResult result = await provider.ChooseAsync(request);
        effect.ApplyBuff(result.SelectedIds);

        AssertEqual(5000, ContinuousDpGate.ResolveDp(context, mine, baseDp: 2000), "+3000 DP while active");
        EffectDurationExpiry.ExpireTurnEnd(context.EffectRegistry, endingTurnPlayerId: P1);
        AssertEqual(2000, ContinuousDpGate.ResolveDp(context, mine, baseDp: 2000), "buff expired at turn end");
    }

    private static async Task ST1_13_SecuritySaBuff()
    {
        (EngineContext context, HeadlessEntityId mine) = await OwnDigimon();
        context.TurnController.Initialize(new[] { P1, P2 }, P1);

        var effect = (ActivatedPlayerScopeBuffEffect)Effect(new ST1_13(), EffectTiming.SecuritySkill, context);
        effect.ApplyBuff();

        AssertEqual(2, ContinuousModifierGate.ResolveSecurityAttack(context, mine, baseSecurityAttack: 1), "SA +1 while active");
        EffectDurationExpiry.ExpireTurnEnd(context.EffectRegistry, endingTurnPlayerId: P1);
        AssertEqual(1, ContinuousModifierGate.ResolveSecurityAttack(context, mine, baseSecurityAttack: 1), "buff expired at owner turn end");
    }

    private static async Task ST1_14_SecurityZoneScope()
    {
        (EngineContext context, HeadlessEntityId security, HeadlessEntityId battle) = await SecurityAndBattleDigimon();
        context.TurnController.Initialize(new[] { P1, P2 }, P1);

        var effect = (ActivatedPlayerScopeBuffEffect)Effect(new ST1_14(), EffectTiming.OptionSkill, context);
        effect.ApplyBuff();

        AssertEqual(8000, ContinuousDpGate.ResolveDp(context, security, baseDp: 1000), "Security Digimon +7000");
        AssertEqual(1000, ContinuousDpGate.ResolveDp(context, battle, baseDp: 1000), "battle-area Digimon unaffected");
    }

    private static async Task ST1_14_Expiry()
    {
        (EngineContext context, HeadlessEntityId security, _) = await SecurityAndBattleDigimon();
        context.TurnController.Initialize(new[] { P1, P2 }, P1);

        var effect = (ActivatedPlayerScopeBuffEffect)Effect(new ST1_14(), EffectTiming.SecuritySkill, context);
        effect.ApplyBuff();

        AssertEqual(8000, ContinuousDpGate.ResolveDp(context, security, baseDp: 1000), "+7000 while active");
        EffectDurationExpiry.ExpireTurnEnd(context.EffectRegistry, endingTurnPlayerId: P1);
        AssertEqual(1000, ContinuousDpGate.ResolveDp(context, security, baseDp: 1000), "expired at turn end");
    }

    private static async Task ST1_08_DigivolveBuff()
    {
        (EngineContext context, HeadlessEntityId mine) = await OwnDigimon();
        context.TurnController.Initialize(new[] { P1, P2 }, P1);

        var source = new CardSource(context, new HeadlessEntityId("p1:battle:ST1_08"), P1);
        var registered = CardEffectRegistrar.RegisterOnEnterPlay(context, new ST1_08(), "ST1_08", source);
        AssertEqual(0, registered.Count, "ST1_08's WhenDigivolving select effect is not auto-registered");

        var effect = (ActivatedTargetBuffEffect)new ST1_08().CardEffects(EffectTiming.WhenDigivolving, source).Single();
        ChoiceRequest request = effect.BuildRequest(new[] { P1, P2 });
        var provider = new ScriptedChoiceProvider();
        provider.Enqueue(ChoiceResult.Select(mine));
        ChoiceResult result = await provider.ChooseAsync(request);
        effect.ApplyBuff(result.SelectedIds);

        AssertEqual(5000, ContinuousDpGate.ResolveDp(context, mine, baseDp: 2000), "+3000 DP on the chosen Digimon");
    }

    private static async Task<(EngineContext, HeadlessEntityId, HeadlessEntityId)> SecurityAndBattleDigimon()
    {
        EngineContext context = EngineContext.CreateDefault(randomSeed: 14);
        CardDatabase cards = (CardDatabase)context.CardRepository;
        cards.Upsert(new CardRecord(new HeadlessEntityId("SECD"), "SECD", "SecDigi", new Dictionary<string, object?>(), CardType: "Digimon"));
        cards.Upsert(new CardRecord(new HeadlessEntityId("BATD"), "BATD", "BatDigi", new Dictionary<string, object?>(), CardType: "Digimon"));
        var security = new HeadlessEntityId("p1:sec:S");
        var battle = new HeadlessEntityId("p1:battle:B");
        context.CardInstanceRepository.Upsert(new CardInstanceRecord(security, new HeadlessEntityId("SECD"), P1));
        context.CardInstanceRepository.Upsert(new CardInstanceRecord(battle, new HeadlessEntityId("BATD"), P1));
        await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, security, ChoiceZone.None, ChoiceZone.Security));
        await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, battle, ChoiceZone.None, ChoiceZone.BattleArea));
        return (context, security, battle);
    }

    private static async Task<(EngineContext, HeadlessEntityId)> OwnDigimon()
    {
        EngineContext context = EngineContext.CreateDefault(randomSeed: 13);
        CardDatabase cards = (CardDatabase)context.CardRepository;
        cards.Upsert(new CardRecord(new HeadlessEntityId("MINE"), "MINE", "Greymon", new Dictionary<string, object?>(), CardType: "Digimon"));
        var mine = new HeadlessEntityId("p1:battle:MINE");
        context.CardInstanceRepository.Upsert(new CardInstanceRecord(mine, new HeadlessEntityId("MINE"), P1));
        await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, mine, ChoiceZone.None, ChoiceZone.BattleArea));
        return (context, mine);
    }

    private static ICardEffect Effect(CEntity_Effect card, EffectTiming timing, EngineContext context)
    {
        var source = new CardSource(context, new HeadlessEntityId("p1:trash:OPT13"), P1);
        return card.CardEffects(timing, source).Single();
    }

    private static void AssertEqual<T>(T expected, T actual, string label)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
    }
}
