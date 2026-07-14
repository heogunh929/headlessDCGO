using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// PRIM-W1-6/9 (G9-024): CardEffectFactory.AddDigivolutionRequirementStaticEffect — grants a card an ADDITIONAL
// "from Color@Level" digivolution path. The printed requirement stays in JSON (W1-1 subsumed); this adds an
// alternative that DigivolveAction consults when the printed condition fails. Card printed = Green@4; add
// Red@4 -> it can now digivolve from a Red Lv4 target.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Control: printed Green@4 cannot digivolve from a Red Lv4 target", ControlIllegal),
    ("Added Red@4 requirement -> digivolve from the Red Lv4 target is now legal", AddedLegal),
    ("A false condition on the added requirement -> illegal again", ConditionGates),
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

async Task ControlIllegal()
{
    EngineContext context = Context();
    context.MemoryController.Set(5);
    var target = await PlaceBase(context, "REDBASE");
    var evo = await PlaceEvolve(context, "EVO", printed: "Green@4");

    ActionProcessResult result = await new DigivolveAction()
        .ProcessAsync(HeadlessActionFactory.Digivolve(P1, evo, target, memoryCost: 2), context);

    AssertTrue(!result.IsSuccess, "printed Green@4 does not match a Red Lv4 target -> illegal");
}

async Task AddedLegal()
{
    EngineContext context = Context();
    context.MemoryController.Set(5);
    var target = await PlaceBase(context, "REDBASE");
    var evo = await PlaceEvolve(context, "EVO", printed: "Green@4");

    var evoCard = new CardSource(context, evo, P1);
    // SEAM (RD-P6B-15): AddSelfDigivolutionRequirementStaticEffect returns AddDigivolutionRequirementClass
    // (IAddDigivolutionRequirementEffect — GetEvoCost(Permanent, CardSource, IgnoreRequirement, bool)), the
    // REAL AS-IS mechanism. It registers no binding, so the mirror observes it via the AS-IS live interface
    // scan CardSource.EvoCosts/CostList (mirror CardSource.AddedDigivolutionCosts, DCGO CardSource.cs:534-627),
    // which DigivolveAction now UNIONs into its added-requirement consumer (MatchesAddedDigivolutionRequirement
    // / TryGetAddedDigivolutionCost). Attach the built effect to the evolving card's controller (AS-IS EvoCosts'
    // "effects of itself" region scans EffectList(None) while the card is not yet a permanent — a hand card).
    ICardEffect addedReq = CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(
        permanentCondition: null,
        digivolutionCost: 2,
        ignoreDigivolutionRequirement: false,
        card: evoCard,
        condition: null,
        cardColor: CardColor.Red,
        level: 4);
    evoCard.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(addedReq);

    ActionProcessResult result = await new DigivolveAction()
        .ProcessAsync(HeadlessActionFactory.Digivolve(P1, evo, target, memoryCost: 2), context);

    AssertTrue(result.IsSuccess, $"added Red@4 path makes the Red Lv4 digivolve legal ({result.Message})");
    AssertTrue(InZone(context, P1, ChoiceZone.BattleArea, evo), "the evolving card entered play");
}

async Task ConditionGates()
{
    EngineContext context = Context();
    context.MemoryController.Set(5);
    var target = await PlaceBase(context, "REDBASE");
    var evo = await PlaceEvolve(context, "EVO", printed: "Green@4");

    var evoCard = new CardSource(context, evo, P1);
    // SEAM (RD-P6B-15): now that the added requirement is observed end-to-end, this test genuinely exercises
    // the `condition` gate — a false condition makes AddDigivolutionRequirementClass.CanUse(null) return false
    // (CanUseCondition), so the added path is NOT collected by CardSource.AddedDigivolutionCosts and the
    // digivolve stays illegal for the RIGHT reason.
    ICardEffect addedReq = CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(
        permanentCondition: null,
        digivolutionCost: 2,
        ignoreDigivolutionRequirement: false,
        card: evoCard,
        condition: () => false,
        cardColor: CardColor.Red,
        level: 4);
    evoCard.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(addedReq);

    ActionProcessResult result = await new DigivolveAction()
        .ProcessAsync(HeadlessActionFactory.Digivolve(P1, evo, target, memoryCost: 2), context);

    AssertTrue(!result.IsSuccess, "false condition -> added requirement inactive -> illegal");
}

// --- Helpers -------------------------------------------------------------

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 924);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    // (RD-P6B-15 seam) AS-IS ICardEffect.CanUse gates on TurnStateMachine.DoneStartGame (phase past None/Setup).
    // The new-model added-requirement scan honours it exactly like every continuous member — a real board is in
    // Main, so set the live phase (the legacy registry path this suite's controls exercise is unaffected).
    context.TurnController.SetPhase(HeadlessPhase.Main);
    return context;
}

async Task<HeadlessEntityId> PlaceBase(EngineContext context, string tag)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId(tag);
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["colors"] = new[] { "Red" }, ["level"] = 4, ["dp"] = 3000 },
        CardType: "Digimon"));
    var id = new HeadlessEntityId($"p1:battle:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["isSuspended"] = false }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

async Task<HeadlessEntityId> PlaceEvolve(EngineContext context, string tag, string printed)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId(tag);
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 6000, ["level"] = 5, ["fixedDigivolutionCost"] = 2 },
        CardType: "Digimon", EvolutionCondition: printed));
    var id = new HeadlessEntityId($"p1:hand:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["fixedDigivolutionCost"] = 2 }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.Hand));
    return id;
}

bool InZone(EngineContext context, HeadlessPlayerId p, ChoiceZone zone, HeadlessEntityId id) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(p, zone).Contains(id);

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }

// Minimal AS-IS-shaped CEntity_Effect: the same seam every ported card definition class uses to surface its
// printed effect list to CardSource.EffectList → CEntity_Effect.CardEffects.
sealed class TestCardEntityEffect : CEntity_Effect
{
    private readonly ICardEffect _effect;
    public TestCardEntityEffect(ICardEffect effect) { _effect = effect; }
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource cardSource) => new() { _effect };
}
