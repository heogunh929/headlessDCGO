using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

// (W6-F) App Fusion — AS-IS AddAppfuseMethodByName (AddAppfusionMethod.cs) declares
// AppFusionCondition{digimonCondition, linkedCondition, cost}: fuse onto an owner Digimon whose TOP matches
// one named material and one of whose LINK cards matches a DIFFERENT material (i != j); executed as an
// EVOLUTION with the chosen link card consumed into the fused sources (CardController.cs:400/786).
//
// (수리-9 DISPOSITION — STALE FIXTURE, not an engine gap) The 2 red cases (Enumeration/Execution) use the
// SYNTHETIC CardEffectFactory.AddAppfuseMethodByName registration, which the rebuilt engine cannot observe:
// the gate (CardSource.AppFusionConditionOf) scans only a DISPATCHED CEntity_Effect, populated solely by
// CardEffectDispatch.TryCreateForCard for a REAL corpus card — never by a synthetic factory call (the
// MIGRATION-NOTEs below). 7b/option-A did NOT change this observability gap. The real AppFusion cards
// (AD1_025/BT21_059/BT22_035/BT24_062) exist but their interactive fusion play is itself STOP-ported, so
// re-targeting to a dispatched real card is a separate porting+witness task (witness-selection-card-level),
// NOT a marking. Engine consumption of fusion select is proven for the analogous DigiXros pump path
// (tests/RD-BATCH7B.Witness, BT18_065). Classification: FIXTURE OBSOLESCENCE, retain red + tracked.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

// (이연③-b) The `Arts` case was RETIRED with the invented `ArtsDigivolveSelfEffect` (an orphaned duplicate with
// no production call-site). The AS-IS canonical Arts surface is CardEffectFactory.ArtsDigivolveEffect →
// OptionResolutionClass → PlayCardClass (RD-P6C2-10 resolved; live on BT9_109 / BT25_104 / BT25_092 / BT25_089),
// and the cost-free digivolve RULE (attach on top, target folds as a source, no cost, WhenDigivolving) is covered
// GREEN by G3.5-D6.FreeDigivolve (4/4). The two App-Fusion cases below remain: they are the pre-existing STALE
// FIXTURE reds (fixture obsolescence, not an engine gap — see the header), tracked in the fail-set unchanged.
var tests = new (string Name, Func<Task> Body)[]
{
    ("a matching host (top=A, link=B) offers the App-Fusion digivolve; same-name pair (i=j) does not", Enumeration),
    ("executing: link material joins the fused sources; the fused card tops the host; evolution trigger fires", Execution),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task Enumeration()
{
    EngineContext ctx = Ctx();
    ctx.MemoryController.Set(5);
    var fused = await Put(ctx, P1, "FUSED", ChoiceZone.Hand, name: "Globemon");
    var host = await Put(ctx, P1, "HOST", ChoiceZone.BattleArea, name: "Mediamon");
    SetMeta(ctx, host, LinkHelpers.LinkedMaxKey, 2);   // room for both links (default max would trim the oldest)
    var link = await Put(ctx, P1, "LINK", ChoiceZone.Hand, name: "Dreammon");
    var sameLink = await Put(ctx, P1, "SAMELINK", ChoiceZone.Hand, name: "Mediamon");   // i=j — must NOT count
    await LinkHelpers.AddLinkCardAsync(ctx.CardInstanceRepository, ctx.ZoneMover, host, link, ChoiceZone.Hand);
    await LinkHelpers.AddLinkCardAsync(ctx.CardInstanceRepository, ctx.ZoneMover, host, sameLink, ChoiceZone.Hand);

    // MIGRATION-NOTE (P7 test-fix): AddAppfuseMethodByName returns AddAppFusionConditionClass
    // (Script/CardEffects/AddAppFusionConditionClass.cs), a new-model kind-class with no ToBinding/EffectRegistry
    // bridge (stage-B RED, docs/audit/rebuild_p6_stageA_notes.md). The gate this test checks
    // (CardSource.AppFusionConditionOf, which scans CardSource.EffectList -> cEntity_EffectController.GetCardEffects
    // -> the card's dispatched CEntity_Effect) has no test-facing hook to attach a synthetic ICardEffect
    // instance either (CEntity_Effect is populated only from CardEffectDispatch.TryCreateForCard, i.e. a real
    // ported card class) — so there is no buildable way to make this declaration observable yet. Assertions
    // below are UNCHANGED and EXPECTED TO FAIL until stage B lands — tracked, not silently weakened.
    CardEffectFactory.AddAppfuseMethodByName(
        new List<string> { "Mediamon", "Dreammon" }, V(ctx, fused));

    IReadOnlyList<LegalAction> actions = new DigivolveAction().GetLegalActions(ctx, P1);
    LegalAction[] fusions = actions.Where(a => a.Id.Value.Contains("appfusion")).ToArray();
    AssertTrue(fusions.Length == 1, $"exactly one App-Fusion offer (got {fusions.Length})");
    AssertTrue(fusions[0].Parameters[DigivolveActionPayload.AppFusionLinkCardKey]?.ToString() == link.Value,
        "the DIFFERENT-name link (Dreammon) is the material — a same-name link (i=j) is not");
}

async Task Execution()
{
    EngineContext ctx = Ctx();
    ctx.MemoryController.Set(5);
    var fused = await Put(ctx, P1, "FUSED", ChoiceZone.Hand, name: "Globemon");
    var host = await Put(ctx, P1, "HOST", ChoiceZone.BattleArea, name: "Mediamon");
    var link = await Put(ctx, P1, "LINK", ChoiceZone.Hand, name: "Dreammon");
    await LinkHelpers.AddLinkCardAsync(ctx.CardInstanceRepository, ctx.ZoneMover, host, link, ChoiceZone.Hand);
    // MIGRATION-NOTE (P7 test-fix): AddAppfuseMethodByName is a new-model kind-class with no
    // ToBinding/EffectRegistry bridge (stage-B RED, docs/audit/rebuild_p6_stageA_notes.md). See Enumeration()
    // above for the full rationale. Assertions below are UNCHANGED and EXPECTED TO FAIL until stage B lands —
    // tracked, not silently weakened.
    CardEffectFactory.AddAppfuseMethodByName(
        new List<string> { "Mediamon", "Dreammon" }, V(ctx, fused));

    LegalAction fusion = new DigivolveAction().GetLegalActions(ctx, P1).Single(a => a.Id.Value.Contains("appfusion"));
    var result = await new DigivolveAction().ProcessAsync(fusion, ctx);
    AssertTrue(result.IsSuccess, $"fusion executed ({result.Message})");

    var zones = (IZoneStateReader)ctx.ZoneMover;
    AssertTrue(zones.GetCards(P1, ChoiceZone.BattleArea).Contains(fused), "the fused card tops the host's spot");
    ctx.CardInstanceRepository.TryGetInstance(fused, out CardInstanceRecord? rec);
    var sources = (rec!.Metadata[DigivolutionStackReader.SourceIdsKey] as IEnumerable<string>)?.ToArray() ?? Array.Empty<string>();
    AssertTrue(sources.Contains(host.Value), "the host went under as a source");
    AssertTrue(sources.Contains(link.Value), "the LINK material was consumed into the fused sources (AS-IS AddToSources)");
    ctx.CardInstanceRepository.TryGetInstance(fused, out _);
    ctx.CardInstanceRepository.TryGetInstance(host, out CardInstanceRecord? hostRec);
    AssertTrue(LinkHelpers.ReadLinkedCardIds(rec.Metadata).Count == 0 && LinkHelpers.ReadLinkedCardIds(hostRec!.Metadata).Count == 0,
        "no dangling link entries");
}

// (이연③-b RETIRED) `Arts()` removed with the invented `ArtsDigivolveSelfEffect` — see the tests-array comment
// above; the cost-free digivolve rule is covered green by G3.5-D6.FreeDigivolve and the canonical
// CardEffectFactory.ArtsDigivolveEffect path is live on BT9_109 / BT25_104 / BT25_092 / BT25_089.

// --- Harness ---

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 971);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    return ctx;
}

async Task<HeadlessEntityId> Put(EngineContext ctx, HeadlessPlayerId owner, string tag, ChoiceZone zone, string name)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, tag, name,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 5000, ["level"] = 5 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 5000 }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone));
    return id;
}

CardSource V(EngineContext ctx, HeadlessEntityId id) => new(ctx, id, OwnerOf(ctx, id), OwnerOf(ctx, id));

void SetMeta(EngineContext ctx, HeadlessEntityId id, string key, object? value)
{
    ctx.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? r);
    ctx.CardInstanceRepository.Upsert(r! with
    {
        Metadata = new Dictionary<string, object?>(r!.Metadata, StringComparer.Ordinal) { [key] = value }
    });
}
HeadlessPlayerId OwnerOf(EngineContext ctx, HeadlessEntityId id) =>
    ctx.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? r) && r is not null ? r.OwnerId : default;

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
