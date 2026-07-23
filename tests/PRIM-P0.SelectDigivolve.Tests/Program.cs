// PRIM-P0-flow Build Order 3 batch C: SelectAndDigivolveEffect (AS-IS DigivolveIntoHandOrTrashCard). Select a
// target Digimon + a source Digimon in hand, then digivolve — the source becomes the new top with the target
// folded under as a digivolution source. Verifies (1) free digivolve places+folds and pays nothing, (2) a
// normal-cost digivolve pays the evolution cost, (3) no candidates -> no-op. Fixture: TfxSelectDigivolve.
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
HeadlessEntityId Src = new("1:exec:SRC");     // the option card (in Execution, not a target candidate)
HeadlessEntityId Target = new("1:battle:TGT");
HeadlessEntityId Evo = new("1:hand:EVO");

var tests = new (string Name, Func<Task> Body)[]
{
    ("free digivolve: source placed onto the target, target folded under, no memory paid", FreeDigivolve),
    ("normal-cost digivolve: pays the evolution cost", NormalCostDigivolve),
    ("no source in hand: no-op", NoSourceNoOp),
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

async Task FreeDigivolve()
{
    EngineContext context = await Setup("free", withSource: true);
    context.MemoryController.Set(3);
    Answer(context, ChoiceResult.Select(Target));   // pick target
    Answer(context, ChoiceResult.Select(Evo));      // pick source

    await ActivatedEffectResolver.ResolveAsync(context, Src, P1, EffectTiming.OptionSkill);

    AssertTrue(InZone(context, P1, ChoiceZone.BattleArea, Evo), "source is now the top Digimon on the battle area");
    AssertTrue(!InZone(context, P1, ChoiceZone.BattleArea, Target), "target folded under (no longer a standalone battler)");
    AssertTrue(SourcesOf(context, Evo).Contains(Target.Value), "target is now a digivolution source under the new top");
    AssertEqual(3, context.MemoryController.Current.Current, "free digivolve paid no memory");
}

async Task NormalCostDigivolve()
{
    EngineContext context = await Setup("normal", withSource: true);
    context.MemoryController.Set(3);
    Answer(context, ChoiceResult.Select(Target));
    Answer(context, ChoiceResult.Select(Evo));

    await ActivatedEffectResolver.ResolveAsync(context, Src, P1, EffectTiming.OptionSkill);

    AssertTrue(InZone(context, P1, ChoiceZone.BattleArea, Evo), "source digivolved onto the target");
    AssertEqual(1, context.MemoryController.Current.Current, "paid the evolution cost of 2 (3 -> 1)");
}

async Task NoSourceNoOp()
{
    EngineContext context = await Setup("free", withSource: false);   // no evolution-capable card in hand
    Answer(context, ChoiceResult.Select(Target));

    await ActivatedEffectResolver.ResolveAsync(context, Src, P1, EffectTiming.OptionSkill);

    AssertTrue(InZone(context, P1, ChoiceZone.BattleArea, Target), "with no source card the target is untouched");
}

// --- Harness -------------------------------------------------------------

void Answer(EngineContext context, ChoiceResult result) =>
    ((ScriptedChoiceProvider)context.ChoiceProvider).Enqueue(result);

IReadOnlyList<string> SourcesOf(EngineContext context, HeadlessEntityId id) =>
    context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? r) && r is not null &&
    r.Metadata.TryGetValue("sourceIds", out object? raw) && raw is IEnumerable<string> s ? s.ToArray() : Array.Empty<string>();

async Task<EngineContext> Setup(string costMode, bool withSource)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 55);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    context.TurnController.SetPhase(HeadlessPhase.Main);   // past None -> DoneStartGame true (AS-IS ICardEffect.CanTrigger gate)
    var cards = (CardDatabase)context.CardRepository;

    // The option/effect card (in Execution so it is not itself a battle-area target candidate).
    var srcDef = new HeadlessEntityId("TfxSelectDigivolve");
    cards.Upsert(new CardRecord(srcDef, "TfxSelectDigivolve", "DigSrc", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(Src, srcDef, P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["digCost"] = costMode }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, Src, ChoiceZone.None, ChoiceZone.Execution));

    // The target Digimon (own battle area, a base Digimon).
    var tgtDef = new HeadlessEntityId("DEF:TGT");
    cards.Upsert(new CardRecord(tgtDef, "TGT", "Base", new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 3 }, CardType: "Digimon"));
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(Target, tgtDef, P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["isSuspended"] = false }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, Target, ChoiceZone.None, ChoiceZone.BattleArea));

    if (withSource)
    {
        // A Digimon in hand with an evolution cost (Any-target requirement, cost 2).
        var evoDef = new HeadlessEntityId("DEF:EVO");
        cards.Upsert(new CardRecord(evoDef, "EVO", "Evolver", new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 5000, ["level"] = 4 }, CardType: "Digimon", EvolutionCost: 2));
        context.CardInstanceRepository.Upsert(new CardInstanceRecord(Evo, evoDef, P1, Metadata: new Dictionary<string, object?>()));
        await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, Evo, ChoiceZone.None, ChoiceZone.Hand));
    }

    return context;
}

bool InZone(EngineContext context, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(player, zone).Contains(cardId);

static void AssertTrue(bool value, string label) { if (!value) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"{label}: expected '{expected}', actual '{actual}'.");
}
