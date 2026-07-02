using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

// (AD1-A) Assembly special play — AS-IS: a rider on the ORDINARY play (CardController.cs:753-761, NOT a
// fusion): the card declares an AssemblyCondition (AddAssemblyConditionClass at timing None, AD1_025 shape);
// when the owner's TRASH can fill the full material set, the play costs (base - reduceCost) and, after
// entry, the materials move from trash to UNDER the permanent (AddDigivolutionCardsBottom). No dedicated
// trigger; AssemblyCount rides the OnEnterField params (no consumer, mirrored for parity).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("full material set in trash -> the discounted Assembly play variant is offered (base 10 - 6 = 4)", AssemblyOffered),
    ("a missing material -> NO Assembly variant (full set only), the normal play remains", MissingMaterialNotOffered),
    ("executing the Assembly play: reduced cost paid, materials stacked UNDER in element order, assemblyCount", AssemblyExecutes),
    ("forged materials (predicate mismatch) are rejected by validation", ForgedMaterialsRejected),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task AssemblyOffered()
{
    (EngineContext ctx, HeadlessEntityId top, _, _) = await OmnimonBoard(includeGaruru: true);
    IReadOnlyList<LegalAction> actions = new PlayCardAction().GetLegalActions(ctx, P1);

    LegalAction normal = actions.Single(a => a.Id.Value.Contains(top.Value) && !a.Id.Value.Contains("assembly"));
    LegalAction assembly = actions.Single(a => a.Id.Value.Contains("assembly"));
    AssertEqual(10, ReadCost(normal), "the normal play stays at full cost");
    AssertEqual(4, ReadCost(assembly), "the Assembly variant costs base 10 - reduce 6 = 4");
}

async Task MissingMaterialNotOffered()
{
    (EngineContext ctx, HeadlessEntityId top, _, _) = await OmnimonBoard(includeGaruru: false);
    IReadOnlyList<LegalAction> actions = new PlayCardAction().GetLegalActions(ctx, P1);
    AssertTrue(actions.Any(a => a.Id.Value.Contains(top.Value)), "the normal play is offered");
    AssertTrue(!actions.Any(a => a.Id.Value.Contains("assembly")), "no Assembly variant without the FULL set (AS-IS: no partial discount)");
}

async Task AssemblyExecutes()
{
    (EngineContext ctx, HeadlessEntityId top, HeadlessEntityId wgrey, HeadlessEntityId mgaru) = await OmnimonBoard(includeGaruru: true);
    LegalAction assembly = new PlayCardAction().GetLegalActions(ctx, P1).Single(a => a.Id.Value.Contains("assembly"));

    int memoryBefore = ctx.MemoryController.Current.Current;
    var result = await new PlayCardAction().ProcessAsync(assembly, ctx);
    AssertTrue(result.IsSuccess, $"assembly play succeeded ({result.Message})");

    var zones = (IZoneStateReader)ctx.ZoneMover;
    AssertTrue(zones.GetCards(P1, ChoiceZone.BattleArea).Contains(top), "the card entered the battle area");
    AssertEqual(memoryBefore - 4, ctx.MemoryController.Current.Current, "the REDUCED cost (4) was paid");
    AssertTrue(!zones.GetCards(P1, ChoiceZone.Trash).Contains(wgrey) && !zones.GetCards(P1, ChoiceZone.Trash).Contains(mgaru),
        "both materials left the trash");

    ctx.CardInstanceRepository.TryGetInstance(top, out CardInstanceRecord? played);
    var sources = (played!.Metadata[DigivolutionStackReader.SourceIdsKey] as IEnumerable<string>)?.ToArray()
        ?? Array.Empty<string>();
    AssertTrue(sources.Contains(wgrey.Value) && sources.Contains(mgaru.Value),
        "the materials are UNDER the permanent as digivolution cards (AS-IS AddDigivolutionCardsBottom)");
    AssertEqual(2, (int)(result.Metadata["assemblyCount"] ?? 0), "assemblyCount rides the play result (HashtableSetting mirror)");
}

async Task ForgedMaterialsRejected()
{
    (EngineContext ctx, HeadlessEntityId top, HeadlessEntityId wgrey, _) = await OmnimonBoard(includeGaruru: true);
    var junk = await PlaceTrash(ctx, P1, "JUNK", "Agumon");   // fails the MetalGarurumon predicate

    var payload = new PlayCardActionPayload(top, 4, ChoiceZone.Hand, ChoiceZone.BattleArea)
    {
        AssemblyMaterials = new[] { wgrey, junk },
    };
    LegalAction forged = HeadlessActionFactory.Create(
        HeadlessActionTypes.PlayCard, P1, $"{P1.Value}:{HeadlessActionTypes.PlayCard}:assembly:{top.Value}", payload.ToParameters());

    var result = await new PlayCardAction().ProcessAsync(forged, ctx);
    AssertTrue(!result.IsSuccess, "forged materials rejected (per-element predicate re-validated 1:1)");
}

// --- Harness ---------------------------------------------------------------

async Task<(EngineContext Ctx, HeadlessEntityId Top, HeadlessEntityId Wgrey, HeadlessEntityId Mgaru)> OmnimonBoard(bool includeGaruru)
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 965);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.MemoryController.Set(10);

    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId("DEF:OMNI");
    cards.Upsert(new CardRecord(defId, "AD1-025t", "Omnimon",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 12000, ["level"] = 6, ["playCost"] = 10 }, CardType: "Digimon", PlayCost: 10));
    var top = new HeadlessEntityId("p1:hand:OMNI");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(top, defId, P1));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, top, ChoiceZone.None, ChoiceZone.Hand));

    // AD1_025.cs:214-261 mirror: two 1-count name-matched elements, flat reduceCost 6, timing-None wrapper.
    var view = new CardSource(ctx, top, P1);
    var assembly = new AddAssemblyConditionClass();
    assembly.SetUpICardEffect("Assembly", null, view);
    assembly.SetUpAddAssemblyConditionClass(cardSource =>
        cardSource.InstanceId == top
            ? new AssemblyCondition(
                new List<AssemblyConditionElement>
                {
                    new(cs => cs is not null && cs.Owner == view.Owner && cs.IsDigimon && cs.EqualsCardName("WarGreymon"), selectMessage: "[WarGreymon]", elementCount: 1),
                    new(cs => cs is not null && cs.Owner == view.Owner && cs.IsDigimon && cs.EqualsCardName("MetalGarurumon"), selectMessage: "[MetalGarurumon]", elementCount: 1),
                },
                reduceCost: 6)
            : null);
    assembly.SetNotShowUI(true);
    ctx.EffectRegistry.Register(assembly.ToBinding($"assembly:{top.Value}"));

    var wgrey = await PlaceTrash(ctx, P1, "WGREY", "WarGreymon");
    HeadlessEntityId mgaru = default;
    if (includeGaruru)
    {
        mgaru = await PlaceTrash(ctx, P1, "MGARU", "MetalGarurumon");
    }

    return (ctx, top, wgrey, mgaru);
}

async Task<HeadlessEntityId> PlaceTrash(EngineContext ctx, HeadlessPlayerId owner, string tag, string name)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(defId, tag, name,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 5000, ["level"] = 6 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:trash:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.Trash));
    return id;
}

int ReadCost(LegalAction action) =>
    action.Parameters[HeadlessActionParameterKeys.MemoryCost] is int c ? c : -1;

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected {expected}, got {actual}.");
    }
}
