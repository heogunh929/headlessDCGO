using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// C7 (dual cards): AS-IS CardKinds is a LIST — a hybrid dual card reports BOTH kinds
// (CardSource.cs:3460/3472 CardKinds.Contains). The port carries extra kinds under the definition
// metadata "cardTypes"; every type judgement goes through CardRecord.IsCardType.
// C9 (linked effects): AS-IS gates an isLinkedEffect-flagged effect LIVE on cardSource.IsLinked
// (Permanent.cs:1532 / ICardEffect.cs:403) — active only while the source card is a LINK card of a
// battle-area permanent; breaking the link stops it (no removal event).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("(C7) a dual Digimon/Option card reports BOTH kinds (view + IsDigimon chokepoint)", DualCardBothKinds),
    ("(C9) CardSource.IsLinked mirrors LinkedCards membership (live)", IsLinkedLive),
    ("(C9) an isLinkedEffect grant is active ONLY while linked; breaking the link stops it", LinkedEffectGate),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task DualCardBothKinds()
{
    EngineContext ctx = Ctx();
    var dual = await Place(ctx, P1, "DUAL", cardType: "Option", extraTypes: new[] { "Digimon" });
    var plainOption = await Place(ctx, P1, "OPT", cardType: "Option");

    var view = new CardSource(ctx, dual, P1);
    AssertTrue(view.IsOption, "the dual card is an Option (printed kind)");
    AssertTrue(view.IsDigimon, "the dual card is ALSO a Digimon (additional kind)");
    AssertTrue(ContinuousKeywordGate.IsDigimon(ctx, dual), "the K4 chokepoint sees the additional kind");

    var plainView = new CardSource(ctx, plainOption, P1);
    AssertTrue(plainView.IsOption && !plainView.IsDigimon, "a plain Option stays single-kind");
}

async Task IsLinkedLive()
{
    EngineContext ctx = Ctx();
    var host = await Place(ctx, P1, "HOST");
    var link = await OffField(ctx, P1, "LINK");

    var linkView = new CardSource(ctx, link, P1);
    AssertTrue(!linkView.IsLinked, "not linked before AddLinkCard");

    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, link, ChoiceZone.None, ChoiceZone.Hand));
    AssertTrue(await LinkHelpers.AddLinkCardAsync(ctx.CardInstanceRepository, ctx.ZoneMover, host, link, ChoiceZone.Hand),
        "link attached");
    AssertTrue(new CardSource(ctx, link, P1).IsLinked, "linked after AddLinkCard");

    AssertTrue(await LinkHelpers.RemoveLinkCardAsync(ctx.CardInstanceRepository, ctx.ZoneMover, host, link),
        "link removed");
    AssertTrue(!new CardSource(ctx, link, P1).IsLinked, "IsLinked flips false when the link breaks (live)");
}

async Task LinkedEffectGate()
{
    EngineContext ctx = Ctx();
    var host = await Place(ctx, P1, "HOST");
    var link = await OffField(ctx, P1, "LINK");

    // The LINK card carries "Collision" flagged isLinkedEffect — AS-IS it applies only while linked.
    ctx.EffectRegistry.Register(CardEffectFactory.CollisionSelfStaticEffect(
        false, new CardSource(ctx, link, P1), null, isLinkedEffect: true).ToBinding($"col:{link.Value}"));

    AssertTrue(!ContinuousKeywordGate.HasKeyword(ctx, link, ContinuousKeywordGate.Collision),
        "not linked -> the linked effect is inactive");

    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, link, ChoiceZone.None, ChoiceZone.Hand));
    await LinkHelpers.AddLinkCardAsync(ctx.CardInstanceRepository, ctx.ZoneMover, host, link, ChoiceZone.Hand);
    AssertTrue(ContinuousKeywordGate.HasKeyword(ctx, link, ContinuousKeywordGate.Collision),
        "linked -> the effect is active (AS-IS Permanent.cs:1532)");

    await LinkHelpers.RemoveLinkCardAsync(ctx.CardInstanceRepository, ctx.ZoneMover, host, link);
    AssertTrue(!ContinuousKeywordGate.HasKeyword(ctx, link, ContinuousKeywordGate.Collision),
        "link broken -> the effect stops (live gate, no removal event needed)");
}

// --- Helpers ---

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 963);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    return ctx;
}

async Task<HeadlessEntityId> Place(EngineContext ctx, HeadlessPlayerId owner, string tag, string cardType = "Digimon", string[]? extraTypes = null)
{
    HeadlessEntityId id = await OffField(ctx, owner, tag, cardType, extraTypes);
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

Task<HeadlessEntityId> OffField(EngineContext ctx, HeadlessPlayerId owner, string tag, string cardType = "Digimon", string[]? extraTypes = null)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 4 };
    if (extraTypes is not null)
    {
        meta[CardRecord.AdditionalCardTypesKey] = extraTypes;
    }

    cards.Upsert(new CardRecord(defId, tag, tag, meta, CardType: cardType));
    var id = new HeadlessEntityId($"{owner.Value}:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["isSuspended"] = false }));
    return Task.FromResult(id);
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
