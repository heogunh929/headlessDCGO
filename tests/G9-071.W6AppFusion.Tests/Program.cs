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

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("a matching host (top=A, link=B) offers the App-Fusion digivolve; same-name pair (i=j) does not", Enumeration),
    ("executing: link material joins the fused sources; the fused card tops the host; evolution trigger fires", Execution),
    ("(W6-A2) Arts Digivolve: cost-free evolution out of the executing area onto a qualifying Digimon", Arts),
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

    ctx.EffectRegistry.Register(CardEffectFactory.AddAppfuseMethodByName(
        new List<string> { "Mediamon", "Dreammon" }, V(ctx, fused)).ToBinding($"appfuse:{fused.Value}"));

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
    ctx.EffectRegistry.Register(CardEffectFactory.AddAppfuseMethodByName(
        new List<string> { "Mediamon", "Dreammon" }, V(ctx, fused)).ToBinding($"appfuse:{fused.Value}"));

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

async Task Arts()
{
    EngineContext ctx = Ctx();
    ctx.MemoryController.Set(0);   // cost-free: memory must not move
    var option = await Put(ctx, P1, "ARTSOPT", ChoiceZone.Execution, name: "ArtsOption");
    var target = await Put(ctx, P1, "TGT", ChoiceZone.BattleArea, name: "Rookie");
    // definition must satisfy the normal digivolution requirement onto the target: reuse a permissive
    // evolution condition by matching levels (option level 5 onto level-4 target via engine cost gate).
    var cards = (CardDatabase)ctx.CardRepository;
    ctx.CardInstanceRepository.TryGetInstance(option, out CardInstanceRecord? optRec);
    cards.Upsert(new CardRecord(optRec!.DefinitionId, "ARTSOPT", "ArtsOption",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 6000, ["level"] = 5 },
        CardType: "Digimon", EvolutionCost: 0, EvolutionCondition: "level=4"));

    var provider = (ScriptedChoiceProvider)ctx.ChoiceProvider;
    provider.Enqueue(ChoiceResult.Select(target));
    var effect = (ArtsDigivolveSelfEffect)CardEffectFactory.ArtsDigivolveEffect(V(ctx, option));
    await effect.ResolveAsync(CancellationToken.None);

    var zones = (IZoneStateReader)ctx.ZoneMover;
    AssertTrue(zones.GetCards(P1, ChoiceZone.BattleArea).Contains(option), "the option digivolved onto the field");
    ctx.CardInstanceRepository.TryGetInstance(option, out CardInstanceRecord? rec);
    var sources = (rec!.Metadata[DigivolutionStackReader.SourceIdsKey] as IEnumerable<string>)?.ToArray() ?? Array.Empty<string>();
    AssertTrue(sources.Contains(target.Value), "the target folded under as a source");
    AssertTrue(ctx.MemoryController.Current.Current == 0, "cost-free (AS-IS payCost:false)");
}

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
