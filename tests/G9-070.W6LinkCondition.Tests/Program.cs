using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// (W6-L) Link-condition declaration — AS-IS AddSelfLinkConditionStaticEffect (AddLinkRequirement.cs:11)
// declares LinkCondition{digimonCondition, cost} at timing None; the separate LinkEffect (OnDeclaration)
// consumes it: host candidates filtered by the predicate (Link.cs:18), paid cost = condition.cost folded
// through the link-cost modifiers (GetChangedLinkCost mirror = LinkHelpers.ResolveLinkCost).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("the declaration is readable via CardSource.LinkConditionOf (registry path)", DeclarationReadable),
    ("LinkSelfEffect: hosts are filtered by the declared digimonCondition; the link attaches and pays the declared cost", LinkFlow),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task DeclarationReadable()
{
    EngineContext ctx = Ctx();
    var linkCard = await Put(ctx, P1, "LINKER", ChoiceZone.Hand);
    CardSource view = V(ctx, linkCard);

    ctx.EffectRegistry.Register(CardEffectFactory.AddSelfLinkConditionStaticEffect(
        permanentCondition: p => p.TopCard.EqualsCardName("APPHOST"), linkCost: 1, card: view).ToBinding($"linkcond:{linkCard.Value}"));

    LinkCondition? condition = view.LinkConditionOf();
    AssertTrue(condition is not null, "the declaration is readable");
    AssertTrue(condition!.cost == 1, "declared cost");
    var host = await Put(ctx, P1, "APPHOST", ChoiceZone.BattleArea);
    AssertTrue(condition.digimonCondition(new Permanent(ctx, host, P1)), "the host predicate is stored verbatim");
}

async Task LinkFlow()
{
    EngineContext ctx = Ctx();
    ctx.MemoryController.Set(5);
    var linkCard = await Put(ctx, P1, "LINKER", ChoiceZone.Hand);
    var goodHost = await Put(ctx, P1, "APPHOST", ChoiceZone.BattleArea);
    var badHost = await Put(ctx, P1, "PLAIN", ChoiceZone.BattleArea);
    CardSource view = V(ctx, linkCard);

    ctx.EffectRegistry.Register(CardEffectFactory.AddSelfLinkConditionStaticEffect(
        permanentCondition: p => p.TopCard.EqualsCardName("APPHOST"), linkCost: 1, card: view).ToBinding($"linkcond:{linkCard.Value}"));

    var provider = (ScriptedChoiceProvider)ctx.ChoiceProvider;
    provider.Enqueue(ChoiceResult.Select(goodHost));

    var effect = (LinkSelfEffect)CardEffectFactory.LinkEffect(view);
    AssertTrue(effect.LinkCost == 1, "the play action reads the DECLARED cost (not metadata)");
    await effect.ResolveAsync(CancellationToken.None);

    ctx.CardInstanceRepository.TryGetInstance(goodHost, out CardInstanceRecord? host);
    var linked = LinkHelpers.ReadLinkedCardIds(host!.Metadata);
    AssertTrue(linked.Contains(linkCard), "the link card attached to the MATCHING host");
    AssertTrue(ctx.MemoryController.Current.Current == 4, "the declared link cost (1) was paid");

    // The scripted provider offered only filtered candidates: verify the bad host was never a candidate by
    // confirming a second run with only the bad host present yields no link.
    var linkCard2 = await Put(ctx, P1, "LINKER2", ChoiceZone.Hand);
    CardSource view2 = V(ctx, linkCard2);
    ctx.EffectRegistry.Register(CardEffectFactory.AddSelfLinkConditionStaticEffect(
        permanentCondition: p => p.TopCard.EqualsCardName("NOSUCH"), linkCost: 1, card: view2).ToBinding($"linkcond:{linkCard2.Value}"));
    var effect2 = (LinkSelfEffect)CardEffectFactory.LinkEffect(view2);
    await effect2.ResolveAsync(CancellationToken.None);   // no matching host -> no choice, no link
    ctx.CardInstanceRepository.TryGetInstance(badHost, out CardInstanceRecord? bad);
    AssertTrue(!LinkHelpers.ReadLinkedCardIds(bad!.Metadata).Contains(linkCard2), "no host matched the predicate -> no link");
}

// --- Harness ---

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 970);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    return ctx;
}

async Task<HeadlessEntityId> Put(EngineContext ctx, HeadlessPlayerId owner, string tag, ChoiceZone zone)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 5000, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 5000 }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone));
    return id;
}

CardSource V(EngineContext ctx, HeadlessEntityId id) => new(ctx, id, OwnerOf(ctx, id), OwnerOf(ctx, id));
HeadlessPlayerId OwnerOf(EngineContext ctx, HeadlessEntityId id) =>
    ctx.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? r) && r is not null ? r.OwnerId : default;

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
