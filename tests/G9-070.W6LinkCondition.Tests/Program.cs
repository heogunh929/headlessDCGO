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

// (이연③-b) The `LinkFlow` case was RETIRED with the invented `LinkSelfEffect` (an orphaned duplicate with no
// production call-site). Its rule — hosts filtered by the declared LinkCondition, link attaches, declared cost
// paid — is covered GREEN on the AS-IS canonical path by G9-031.LinkSecurity (LinkAttaches: CardEffectFactory
// .LinkEffect → ActivateClass → ILinkCard.LinkCard(), real K:Link card EX10_029, cost 2, attach + memory 5→3).
// DeclarationReadable stays: it pins the SYNTHETIC-card LinkConditionOf observability gap (a stage-B RED,
// unrelated to the invention removal), honest-red and tracked.
var tests = new (string Name, Func<Task> Body)[]
{
    ("the declaration is readable via CardSource.LinkConditionOf (registry path)", DeclarationReadable),
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

// (이연③-b RETIRED) `LinkFlow()` removed with the invented `LinkSelfEffect` — see the tests-array comment above;
// the AS-IS canonical link-attach + declared-cost rule is covered green by G9-031.LinkSecurity (LinkAttaches).

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
