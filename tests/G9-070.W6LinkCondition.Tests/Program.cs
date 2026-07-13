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

    // MIGRATION-NOTE (P7 test-fix): AddSelfLinkConditionStaticEffect returns AddLinkConditionClass
    // (Script/CardEffects/AddLinkConditionClass.cs), a new-model kind-class with no ToBinding/EffectRegistry
    // bridge (stage-B RED, docs/audit/rebuild_p6_stageA_notes.md). The gate this test checks
    // (CardSource.LinkConditionOf, which scans CardSource.EffectList -> cEntity_EffectController.GetCardEffects
    // -> the card's dispatched CEntity_Effect) has no test-facing hook to attach a synthetic ICardEffect
    // instance either (CEntity_Effect is populated only from CardEffectDispatch.TryCreateForCard, i.e. a real
    // ported card class) — so there is no buildable way to make this declaration observable yet. Assertions
    // below are UNCHANGED and EXPECTED TO FAIL until stage B lands — tracked, not silently weakened.
    CardEffectFactory.AddSelfLinkConditionStaticEffect(
        permanentCondition: p => p.TopCard.EqualsCardName("APPHOST"), linkCost: 1, card: view);

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

    // MIGRATION-NOTE (P7 test-fix): AddSelfLinkConditionStaticEffect is a new-model kind-class with no
    // ToBinding/EffectRegistry bridge (stage-B RED, docs/audit/rebuild_p6_stageA_notes.md). See
    // DeclarationReadable() above for the full rationale. Assertions below are UNCHANGED and EXPECTED TO FAIL
    // until stage B lands — tracked, not silently weakened.
    CardEffectFactory.AddSelfLinkConditionStaticEffect(
        permanentCondition: p => p.TopCard.EqualsCardName("APPHOST"), linkCost: 1, card: view);

    var provider = (ScriptedChoiceProvider)ctx.ChoiceProvider;
    provider.Enqueue(ChoiceResult.Select(goodHost));

    // MIGRATION-NOTE (P7 test-fix): CardEffectFactory.LinkEffect (KeyWordEffects/Link.cs) was re-pointed by the
    // kind-class rebuild to construct/return the NEW-model ActivateClass (whose ActivateCoroutine throws
    // NotSupportedException — design item RD-P6C2-7, docs/audit/rebuild_p6_cluster2_notes.md: "AS-IS ILinkCard
    // has no mirror"), so it can no longer be cast to the OLD-model action class LinkSelfEffect
    // (CardEffectCommons/ActivatedEffects.cs) this test exercises. LinkSelfEffect itself is still fully
    // functional (host-filter via CardSource.LinkConditionOf + link-cost payment) and has no other production
    // call site since the flip, so it is constructed directly here, reading the declared cost the same way the
    // orphaned factory used to. Because LinkConditionOf() depends on the same un-bindable AddLinkConditionClass
    // grant (see above), it resolves to null for this fixture card, so LinkCost below is 0, not the declared 1 —
    // the "reads the DECLARED cost" assertion is UNCHANGED and EXPECTED TO FAIL until stage B lands.
    var effect = new LinkSelfEffect(view, view.LinkConditionOf()?.cost ?? 0, "Link");
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
    // MIGRATION-NOTE (P7 test-fix): AddSelfLinkConditionStaticEffect / CardEffectFactory.LinkEffect — see
    // DeclarationReadable() / LinkFlow() above for the full rationale (un-bindable kind-class grant + orphaned
    // factory constructing the broken new-model stub instead of LinkSelfEffect).
    CardEffectFactory.AddSelfLinkConditionStaticEffect(
        permanentCondition: p => p.TopCard.EqualsCardName("NOSUCH"), linkCost: 1, card: view2);
    var effect2 = new LinkSelfEffect(view2, view2.LinkConditionOf()?.cost ?? 0, "Link");
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
