namespace ST1RedTests;

using HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST1.Red;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

// Option [Main] select-and-delete effects: ST1_16 (delete 1) and ST1_15 (delete up to 2 with DP <= 4000).
internal static class ActivatedTests
{
    private static readonly HeadlessPlayerId P1 = new(1);
    private static readonly HeadlessPlayerId P2 = new(2);
    private static readonly HeadlessPlayerId[] Both = { new(1), new(2) };

    public static (string Name, Func<Task> Body)[] Cases => new (string, Func<Task>)[]
    {
        ("ST1_16: [Main] deletes the chosen opponent Digimon, leaves the rest", ST1_16_Delete),
        ("ST1_15: [Main] only offers opponent Digimon with DP <= 4000", ST1_15_Candidates),
        ("ST1_15: [Main] deletes up to 2 chosen low-DP Digimon", ST1_15_Delete),
        ("ST1_12: [Security] plays this Tamer onto the battle area", ST1_12_SecurityPlay),
        ("ST1_15: [Main] delete threshold is raise-able (MaxDP_DeleteEffect)", ST1_15_DynamicThreshold),
    };

    private static async Task ST1_15_DynamicThreshold()
    {
        // Base 4000 -> the 5000-DP Digimon is NOT a candidate (covered by ST1_15_Candidates). With a +2000
        // delete-threshold raise active, the same 5000-DP Digimon becomes deletable (4000 + 2000 = 6000).
        (EngineContext context, _, _, _) = await ThreeOpponents();
        var raise = new EffectBinding(
            new EffectRequest(new HeadlessEntityId("raise:delthreshold"), P1, "Continuous",
                new EffectContext(P1, P1, new HeadlessEntityId("raise:src"), triggerEntityId: null,
                    targetEntityIds: System.Array.Empty<HeadlessEntityId>(),
                    values: new Dictionary<string, object?>(StringComparer.Ordinal) { ["maxDpDeleteDelta"] = 2000 })),
            keywords: null, EffectQueryRole.Continuous, new[] { "DeleteThreshold" }, effect: null, duration: null);
        context.EffectRegistry.Register(raise);

        var effect = (ActivatedSelectEffect)Main(new ST1_15(), context);
        ChoiceRequest request = effect.BuildRequest(Both);
        AssertEqual(3, request.Candidates.Count, "with +2000 threshold, the 5000-DP Digimon is now a candidate");
    }

    private static async Task ST1_12_SecurityPlay()
    {
        EngineContext context = EngineContext.CreateDefault(randomSeed: 112);
        CardDatabase cards = (CardDatabase)context.CardRepository;
        cards.Upsert(new CardRecord(new HeadlessEntityId("ST1_12def"), "ST1_12", "Tamer", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Tamer"));
        var revealed = new HeadlessEntityId("p1:trash:ST1_12T");
        context.CardInstanceRepository.Upsert(new CardInstanceRecord(revealed, new HeadlessEntityId("ST1_12def"), P1));
        await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, revealed, ChoiceZone.None, ChoiceZone.Trash));

        var play = (PlayThisCardToBattleEffect)new ST1_12().CardEffects(EffectTiming.SecuritySkill, new CardSource(context, revealed, P1)).Single();
        MatchStateMutationSink sink = Sink(context);
        play.Apply(sink);
        await sink.FlushAsync();

        AssertTrue(InZone(context, P1, ChoiceZone.BattleArea, revealed), "[Security] played the Tamer onto the battle area");
        AssertTrue(!InZone(context, P1, ChoiceZone.Trash, revealed), "Tamer no longer in the trash");
    }

    private static async Task ST1_16_Delete()
    {
        EngineContext context = EngineContext.CreateDefault(randomSeed: 16);
        var b1 = new HeadlessEntityId("p2:battle:B1");
        var b2 = new HeadlessEntityId("p2:battle:B2");
        await Place(context, P2, b1, dp: 3000);
        await Place(context, P2, b2, dp: 3000);

        var effect = (ActivatedSelectEffect)Main(new ST1_16(), context);
        ChoiceRequest request = effect.BuildRequest(Both);
        AssertEqual(2, request.Candidates.Count, "both opponent Digimon are candidates");

        var sink = Sink(context);
        var provider = new ScriptedChoiceProvider();
        provider.Enqueue(ChoiceResult.Select(b1));
        ChoiceResult result = await provider.ChooseAsync(request);
        effect.Apply(sink, result.SelectedIds);
        await sink.FlushAsync();

        AssertTrue(InZone(context, P2, ChoiceZone.Trash, b1), "B1 trashed");
        AssertTrue(InZone(context, P2, ChoiceZone.BattleArea, b2), "B2 untouched");
    }

    private static async Task ST1_15_Candidates()
    {
        (EngineContext context, _, _, _) = await ThreeOpponents();
        var effect = (ActivatedSelectEffect)Main(new ST1_15(), context);
        ChoiceRequest request = effect.BuildRequest(Both);
        AssertEqual(2, request.Candidates.Count, "only the two <=4000 DP Digimon are candidates");
        AssertEqual(2, request.MaxCount, "up to 2");
        AssertEqual(1, request.MinCount, "canEndNotMax -> min 1");
    }

    private static async Task ST1_15_Delete()
    {
        (EngineContext context, HeadlessEntityId low1, HeadlessEntityId high, HeadlessEntityId low2) = await ThreeOpponents();
        var effect = (ActivatedSelectEffect)Main(new ST1_15(), context);
        ChoiceRequest request = effect.BuildRequest(Both);

        var sink = Sink(context);
        var provider = new ScriptedChoiceProvider();
        provider.Enqueue(ChoiceResult.Select(low1, low2));
        ChoiceResult result = await provider.ChooseAsync(request);
        effect.Apply(sink, result.SelectedIds);
        await sink.FlushAsync();

        AssertTrue(InZone(context, P2, ChoiceZone.Trash, low1), "low1 trashed");
        AssertTrue(InZone(context, P2, ChoiceZone.Trash, low2), "low2 trashed");
        AssertTrue(InZone(context, P2, ChoiceZone.BattleArea, high), "5000 DP Digimon untouched");
    }

    private static async Task<(EngineContext, HeadlessEntityId, HeadlessEntityId, HeadlessEntityId)> ThreeOpponents()
    {
        EngineContext context = EngineContext.CreateDefault(randomSeed: 15);
        var low1 = new HeadlessEntityId("p2:battle:LOW1");
        var high = new HeadlessEntityId("p2:battle:HIGH");
        var low2 = new HeadlessEntityId("p2:battle:LOW2");
        await Place(context, P2, low1, dp: 3000);
        await Place(context, P2, high, dp: 5000);
        await Place(context, P2, low2, dp: 4000);
        return (context, low1, high, low2);
    }

    private static ICardEffect Main(CEntity_Effect card, EngineContext context)
    {
        var source = new CardSource(context, new HeadlessEntityId("p1:trash:OPT"), P1);
        return card.CardEffects(EffectTiming.OptionSkill, source).Single();
    }

    private static async Task Place(EngineContext context, HeadlessPlayerId owner, HeadlessEntityId id, int dp)
    {
        CardDatabase cards = (CardDatabase)context.CardRepository;
        var defId = new HeadlessEntityId($"DEF:{id.Value}");
        cards.Upsert(new CardRecord(defId, defId.Value, id.Value, new Dictionary<string, object?>(), CardType: "Digimon"));
        var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp };
        context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner, Metadata: meta));
        await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    }

    private static MatchStateMutationSink Sink(EngineContext context) =>
        new(context.CardInstanceRepository, log: null, context.ZoneMover, memory: null, context.EffectRegistry);

    private static bool InZone(EngineContext context, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId) =>
        ((IZoneStateReader)context.ZoneMover).GetCards(player, zone).Contains(cardId);

    private static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
    private static void AssertEqual<T>(T expected, T actual, string label)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
    }
}
