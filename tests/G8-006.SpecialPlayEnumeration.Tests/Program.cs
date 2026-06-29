using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G8-006: SpecialPlay legal-action enumeration from a recipe, plus action-encoder coverage. With a
// registered DigiXros recipe and the named materials on the battle area, GetLegalActions offers the
// special play; FactoredActionEncoder maps it into a dedicated SpecialPlay lane.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Recipe + materials present -> GetLegalActions offers the DigiXros special play", OffersWhenSatisfied),
    ("A missing material -> no special play offered", NoneWhenMissing),
    ("FactoredActionEncoder maps SpecialPlay into its own lane", EncoderLane),
    ("SpecialPlay is inside the agent-facing legality boundary", ValidatorTreatsSpecialPlayAsAgentFacing),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex)
    {
        failures.Add(test.Name);
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}");
    }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

async Task OffersWhenSatisfied()
{
    (EngineContext context, HeadlessEntityId top, HeadlessEntityId matA, HeadlessEntityId matB) = await Board(includeBoth: true);

    IReadOnlyList<LegalAction> actions = new SpecialPlayAction().GetLegalActions(context, P1);
    AssertEqual(1, actions.Count, "one special play offered");
    AssertEqual(HeadlessActionTypes.SpecialPlay, actions[0].ActionType, "action type");
    string materials = actions[0].Parameters[SpecialPlayAction.MaterialsKey]!.ToString()!;
    AssertTrue(materials.Contains(matA.Value) && materials.Contains(matB.Value), "both named materials selected");
}

async Task NoneWhenMissing()
{
    (EngineContext context, _, _, _) = await Board(includeBoth: false);
    IReadOnlyList<LegalAction> actions = new SpecialPlayAction().GetLegalActions(context, P1);
    AssertEqual(0, actions.Count, "no special play when a material is missing");
}

async Task EncoderLane()
{
    (EngineContext context, _, _, _) = await Board(includeBoth: true);
    LegalAction action = new SpecialPlayAction().GetLegalActions(context, P1).Single();

    FactoredActionMask mask = FactoredActionEncoder.Encode(new[] { action }, FactoredPositionContext.FromContext(context));
    FactoredAction mapped = mask.Actions.Single();
    AssertEqual("SpecialPlay", mapped.Lane, "mapped into the SpecialPlay lane");
    AssertTrue(mapped.Index >= mask.Schema.SpecialPlayOffset && mapped.Index < mask.Schema.SpecialPlayOffset + mask.Schema.MaxHand, "index within the SpecialPlay lane");
    AssertEqual(0, mask.Unmapped.Count, "the action was mapped");
}

async Task ValidatorTreatsSpecialPlayAsAgentFacing()
{
    // Regression for the reported P0: NormalizedSpecialPlay must be in the agent action space, otherwise
    // the boundary defers it to per-handler validation and never checks it against the legal set.
    AssertTrue(
        LegalActionSetValidator.AgentActionTypes.Contains(HeadlessActionTypes.NormalizedSpecialPlay),
        "agent action space includes SpecialPlay");

    // Offered board: the dispatcher exposes the special play in Main, so the validator accepts it.
    (EngineContext offered, _, _, _) = await Board(includeBoth: true);
    ((InMemoryHeadlessTurnController)offered.TurnController).SetPhase(HeadlessPhase.Main);
    LegalAction specialPlay = new SpecialPlayAction().GetLegalActions(offered, P1).Single();

    var validator = new LegalActionSetValidator();
    AssertTrue(validator.Validate(specialPlay, offered).IsLegal, "offered SpecialPlay validates as legal");

    // Boundary enforcement: the SAME action submitted against a board where the recipe is unsatisfiable
    // (a material missing) is now actively rejected — proving SpecialPlay is checked, not deferred.
    (EngineContext missing, _, _, _) = await Board(includeBoth: false);
    ((InMemoryHeadlessTurnController)missing.TurnController).SetPhase(HeadlessPhase.Main);
    AssertEqual(0, new SpecialPlayAction().GetLegalActions(missing, P1).Count, "no special play when a material is missing");
    AssertTrue(!validator.Validate(specialPlay, missing).IsLegal, "unsatisfiable SpecialPlay is rejected at the boundary");
}

// --- Helpers -------------------------------------------------------------

async Task<(EngineContext, HeadlessEntityId, HeadlessEntityId, HeadlessEntityId)> Board(bool includeBoth)
{
    SpecialPlayRecipeRegistry.Clear();
    SpecialPlayRecipeRegistry.Register("XROS", new SpecialPlayRecipe(SpecialPlayKind.DigiXros, new[] { "Shoutmon X4", "Beelzemon" }, MemoryCost: 2));

    EngineContext context = EngineContext.CreateDefault(randomSeed: 806);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    context.MemoryController.Set(5);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId("XROS"), "XROS", "Shoutmon X4B", new Dictionary<string, object?>(), CardType: "Digimon"));

    var top = new HeadlessEntityId("p1:hand:XROS");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(top, new HeadlessEntityId("XROS"), P1));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, top, ChoiceZone.None, ChoiceZone.Hand));

    HeadlessEntityId matA = await Material(context, "MatA", "Shoutmon X4");
    HeadlessEntityId matB = includeBoth ? await Material(context, "MatB", "Beelzemon") : default;
    return (context, top, matA, matB);
}

async Task<HeadlessEntityId> Material(EngineContext context, string tag, string name)
{
    CardDatabase cards = (CardDatabase)context.CardRepository;
    var def = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(def, def.Value, name, new Dictionary<string, object?>(), CardType: "Digimon"));
    var id = new HeadlessEntityId($"p1:battle:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, P1));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
