namespace ST1RedTests;

using HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST1.Red;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// Option [Main] select-and-delete effects: ST1_16 (delete 1) and ST1_15 (delete up to 2 with DP <= 4000).
//
// (deferred-queue re-aim, ST2.Blue toolbox) The [Main] arms were already re-ported to the live AS-IS
// ActivateClass + SelectPermanentEffect(Mode.Destroy) coroutine, but the contexts were never initialised with a
// seat order, so Player.Enemy resolved null -> IsOpponentBattleAreaDigimon returned false -> the target pool was
// empty and the coroutine returned WITHOUT opening a select (hence "expected true" for a deferred pending and
// "B1 trashed" never happening). Re-aimed by initialising the turn (P1 first, P2 opponent) and opening the
// DoneStartGame gate. ST1_12's [Security] arm is the shared AS-IS PlaySelfTamerSecurityEffect factory (the
// retired invented PlayThisCardToBattleEffect cast died) — re-aimed onto the factory-identity surface, the same
// as ST2_12.
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
        ("ST1_12: [Security] arm is the shared PlaySelfTamerSecurityEffect factory", ST1_12_SecurityPlay),
        ("ST1_15: [Main] delete threshold is raise-able (MaxDP_DeleteEffect)", ST1_15_DynamicThreshold),
    };

    private static async Task ST1_15_DynamicThreshold()
    {
        // Base 4000 -> the 5000-DP Digimon is NOT a candidate (covered by ST1_15_Candidates). With a +2000
        // delete-threshold raise active, the same 5000-DP Digimon becomes deletable (4000 + 2000 = 6000).
        (EngineContext context, _, _, _) = await ThreeOpponents(deferred: true);
        var raise = new EffectBinding(
            new EffectRequest(new HeadlessEntityId("raise:delthreshold"), P1, "Continuous",
                new EffectContext(P1, P1, new HeadlessEntityId("raise:src"), triggerEntityId: null,
                    targetEntityIds: System.Array.Empty<HeadlessEntityId>(),
                    values: new Dictionary<string, object?>(StringComparer.Ordinal) { ["maxDpDeleteDelta"] = 2000 })),
            keywords: null, EffectQueryRole.Continuous, new[] { "DeleteThreshold" }, effect: null, duration: null);
        context.EffectRegistry.Register(raise);

        // (re-aim) drive the AS-IS ActivateClass coroutine with a DEFERRED provider and read the surfaced
        // SelectPermanentEffect request (the live rule surface — MaxDpDeleteThreshold is a live EffectRegistry
        // continuous query, so the raise is honoured). The 5000-DP Digimon now qualifies -> all 3 are candidates.
        ChoiceRequest request = await OpenSelect(context, Main(new ST1_15(), context));
        AssertEqual(3, request.Candidates.Count, "with +2000 threshold, the 5000-DP Digimon is now a candidate");
    }

    // (re-aim) The [Security] block returns CardEffectFactory.PlaySelfTamerSecurityEffect(card) — a shared AS-IS
    // ActivateClass (IsSecurityEffect, "[Security] Play this card without paying its memory cost."), not the
    // retired invented PlayThisCardToBattleEffect. Its play behaviour is covered wherever the factory is
    // exercised (BT2_084/085/087/090, EX4_062 witnesses); the retired white-box Apply cast is re-aimed onto the
    // factory-identity surface, the same as ST2_12.
    private static Task ST1_12_SecurityPlay()
    {
        EngineContext context = EngineContext.CreateDefault(randomSeed: 112);
        CardDatabase cards = (CardDatabase)context.CardRepository;
        cards.Upsert(new CardRecord(new HeadlessEntityId("ST1_12def"), "ST1_12", "Tamer", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Tamer"));
        var revealed = new HeadlessEntityId("p1:trash:ST1_12T");
        context.CardInstanceRepository.Upsert(new CardInstanceRecord(revealed, new HeadlessEntityId("ST1_12def"), P1));

        var security = (ActivateClass)new ST1_12().CardEffects(EffectTiming.SecuritySkill, new CardSource(context, revealed, P1)).Single();
        AssertTrue(security.IsSecurityEffect, "[Security] arm is a security effect");
        AssertTrue(security.EffectDiscription.Contains("Play this card", StringComparison.Ordinal),
            "[Security] routes to the AS-IS PlaySelfTamerSecurityEffect (\"Play this card without paying its memory cost\")");
        return Task.CompletedTask;
    }

    private static async Task ST1_16_Delete()
    {
        EngineContext context = EngineContext.CreateDefault(randomSeed: 16);
        context.TurnController.Initialize(Both, P1);
        context.TurnController.SetPhase(HeadlessPhase.Main);
        var b1 = new HeadlessEntityId("p2:battle:B1");
        var b2 = new HeadlessEntityId("p2:battle:B2");
        await Place(context, P2, b1, dp: 3000);
        await Place(context, P2, b2, dp: 3000);

        // (re-aim) live surface: the AS-IS ActivateClass coroutine drives SelectPermanentEffect(Mode.Destroy)
        // through the context's scripted provider.
        ((ScriptedChoiceProvider)context.ChoiceProvider).Enqueue(ChoiceResult.Select(b1));
        await RunMain(context, Main(new ST1_16(), context));

        AssertTrue(InZone(context, P2, ChoiceZone.Trash, b1), "B1 trashed");
        AssertTrue(InZone(context, P2, ChoiceZone.BattleArea, b2), "B2 untouched");
    }

    private static async Task ST1_15_Candidates()
    {
        (EngineContext context, HeadlessEntityId low1, _, _) = await ThreeOpponents(deferred: true);
        // (re-aim) the surfaced SelectPermanentEffect request is the live rule surface.
        ChoiceRequest request = await OpenSelect(context, Main(new ST1_15(), context));
        AssertEqual(2, request.Candidates.Count, "only the two <=4000 DP Digimon are candidates");
        AssertEqual(2, request.MaxCount, "up to 2");
        // (re-aim) The live Activate coroutine builds the batch request through RunAsIsSelectionAsync, whose
        // panel-level minimum for canEndNotMax is 0 (CanEndSelectAsIs allows ending at any count ≤ max — the
        // AS-IS termination rule). The card's "must delete at least 1" floor is NOT the request MinCount; it
        // rides on the CanEndSelectCondition, carried as the request's SelectionValidator (rejects an empty set,
        // permits below-max). The old MinCount==1 assert probed the RETIRED legacy BuildRequest path.
        AssertEqual(0, request.MinCount, "canEndNotMax -> panel minimum 0 (floor enforced by the SelectionValidator)");
        AssertTrue(request.SelectionValidator is not null, "the request carries the CanEndSelectCondition gate");
        AssertTrue(!request.SelectionValidator!(System.Array.Empty<HeadlessEntityId>()), "empty selection is rejected (must delete at least 1)");
        AssertTrue(request.SelectionValidator!(new[] { low1 }), "a single low-DP pick is a legal end (canEndNotMax)");
    }

    private static async Task ST1_15_Delete()
    {
        (EngineContext context, HeadlessEntityId low1, HeadlessEntityId high, HeadlessEntityId low2) = await ThreeOpponents();
        // (re-aim) live drive through the scripted provider.
        ((ScriptedChoiceProvider)context.ChoiceProvider).Enqueue(ChoiceResult.Select(low1, low2));
        await RunMain(context, Main(new ST1_15(), context));

        AssertTrue(InZone(context, P2, ChoiceZone.Trash, low1), "low1 trashed");
        AssertTrue(InZone(context, P2, ChoiceZone.Trash, low2), "low2 trashed");
        AssertTrue(InZone(context, P2, ChoiceZone.BattleArea, high), "5000 DP Digimon untouched");
    }

    private static async Task<(EngineContext, HeadlessEntityId, HeadlessEntityId, HeadlessEntityId)> ThreeOpponents(bool deferred = false)
    {
        EngineContext context = EngineContext.CreateDefault(randomSeed: 15, deferredChoice: deferred);
        // (re-aim) seat order so Player.Enemy resolves (opponent-target predicate), game past setup.
        context.TurnController.Initialize(Both, P1);
        context.TurnController.SetPhase(HeadlessPhase.Main);
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

    /// <summary>(re-aim) Run the [Main] ActivateClass coroutine under the ambient match scope (the AS-IS GManager
    /// component flow) with the context's own choice provider.</summary>
    private static async Task RunMain(EngineContext context, ICardEffect effect)
    {
        using var scope = AmbientMatchContext.Enter(context);
        await ((HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects.ActivateClass)effect).Activate(
            CardEffectCommons.OptionMainCheckHashtable(effect.EffectSourceCard));
    }

    /// <summary>(re-aim) Drive the coroutine with a DEFERRED provider until its permanent-select surfaces, and
    /// return the pending ChoiceRequest (the live candidates/min/max rule surface).</summary>
    private static async Task<ChoiceRequest> OpenSelect(EngineContext context, ICardEffect effect)
    {
        try
        {
            await RunMain(context, effect);
        }
        catch (HeadlessDCGO.Engine.Headless.Runtime.DeferredChoicePendingException)
        {
        }

        ChoiceRequest? pending = context.ChoiceController.PendingRequest;
        AssertTrue(pending is not null, "the coroutine surfaced its permanent select (deferred)");
        return pending!;
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

    private static bool InZone(EngineContext context, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId) =>
        ((IZoneStateReader)context.ZoneMover).GetCards(player, zone).Contains(cardId);

    private static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
    private static void AssertEqual<T>(T expected, T actual, string label)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
    }
}
