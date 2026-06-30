using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G8-001: DigivolveAction validates the real-data evolution condition format ("Color@Level:Cost") against
// the target's actual color(s) and level, instead of the old id/number/type tokens that never matched the
// loader output — so real-data digivolution is no longer universally rejected.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Red@3 condition: digivolve onto a Red Lv3 target is legal", () => Attempt("Red@3:2", "Red", 3, expectLegal: true)),
    ("Red@3 condition: digivolve onto a Blue Lv3 target is illegal (wrong color)", () => Attempt("Red@3:2", "Blue", 3, expectLegal: false)),
    ("Red@3 condition: digivolve onto a Red Lv4 target is illegal (wrong level)", () => Attempt("Red@3:2", "Red", 4, expectLegal: false)),
    ("Empty condition still allows digivolution (backward compat)", () => Attempt(null, "Blue", 9, expectLegal: true)),
    ("Multi-condition (Red@3;Blue@3): a Blue Lv3 target matches the second clause", () => Attempt("Red@3:2;Blue@3:2", "Blue", 3, expectLegal: true)),
    // G11-003: per-target evolution cost (Red@3:2 / Blue@3:3) — the structured evolutionCosts pick the cost
    // matching the chosen target's color+level, and a mismatching submitted cost is rejected.
    ("Red@3:2 cost: digivolve onto Red Lv3 paying 2 is legal", () => AttemptCost("Red", 3, submitCost: 2, expectLegal: true)),
    ("Red@3:2 cost: paying 3 onto Red Lv3 is illegal (cost mismatch)", () => AttemptCost("Red", 3, submitCost: 3, expectLegal: false)),
    ("Blue@3:3 cost: digivolve onto Blue Lv3 paying 3 is legal", () => AttemptCost("Blue", 3, submitCost: 3, expectLegal: true)),
    ("Blue@3:3 cost: paying 2 onto Blue Lv3 is illegal (cost mismatch)", () => AttemptCost("Blue", 3, submitCost: 2, expectLegal: false)),
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

async Task Attempt(string? condition, string targetColor, int targetLevel, bool expectLegal)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 801);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    context.MemoryController.Set(5);
    CardDatabase cards = (CardDatabase)context.CardRepository;

    cards.Upsert(new CardRecord(new HeadlessEntityId("EVO"), "EVO", "Greymon", new Dictionary<string, object?>(), CardType: "Digimon", EvolutionCost: 2, EvolutionCondition: condition));
    var evoMeta = new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["colors"] = new[] { targetColor },
        ["level"] = targetLevel,
    };
    cards.Upsert(new CardRecord(new HeadlessEntityId("TGT"), "TGT", "Agumon", evoMeta, CardType: "Digimon"));

    var evolving = new HeadlessEntityId("p1:hand:EVO");
    var target = new HeadlessEntityId("p1:battle:TGT");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(evolving, new HeadlessEntityId("EVO"), P1));
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(target, new HeadlessEntityId("TGT"), P1));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, evolving, ChoiceZone.None, ChoiceZone.Hand));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, target, ChoiceZone.None, ChoiceZone.BattleArea));

    LegalAction action = HeadlessActionFactory.Digivolve(P1, evolving, target, memoryCost: 2);
    ActionProcessResult result = await new DigivolveAction().ProcessAsync(action, context);

    if (expectLegal)
    {
        AssertTrue(result.IsSuccess, $"expected legal digivolve ({result.Message})");
    }
    else
    {
        AssertTrue(result.IsIllegal, $"expected illegal digivolve, got success");
    }
}

// G11-003: an EVO card with per-target evolution costs Red@3 -> 2 and Blue@3 -> 3. Digivolving onto a
// target resolves the cost of the matching color+level requirement; the submitted cost must equal it.
async Task AttemptCost(string targetColor, int targetLevel, int submitCost, bool expectLegal)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 1103);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    context.MemoryController.Set(9);
    CardDatabase cards = (CardDatabase)context.CardRepository;

    var evoCardMeta = new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["evolutionCosts"] = new object[]
        {
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["color"] = "Red", ["level"] = 3, ["cost"] = 2 },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["color"] = "Blue", ["level"] = 3, ["cost"] = 3 },
        },
    };
    cards.Upsert(new CardRecord(new HeadlessEntityId("EVO"), "EVO", "Greymon", evoCardMeta, CardType: "Digimon", EvolutionCondition: "Red@3:2;Blue@3:3"));

    var evoMeta = new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["colors"] = new[] { targetColor },
        ["level"] = targetLevel,
    };
    cards.Upsert(new CardRecord(new HeadlessEntityId("TGT"), "TGT", "Agumon", evoMeta, CardType: "Digimon"));

    var evolving = new HeadlessEntityId("p1:hand:EVO");
    var target = new HeadlessEntityId("p1:battle:TGT");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(evolving, new HeadlessEntityId("EVO"), P1));
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(target, new HeadlessEntityId("TGT"), P1));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, evolving, ChoiceZone.None, ChoiceZone.Hand));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, target, ChoiceZone.None, ChoiceZone.BattleArea));

    LegalAction action = HeadlessActionFactory.Digivolve(P1, evolving, target, memoryCost: submitCost);
    ActionProcessResult result = await new DigivolveAction().ProcessAsync(action, context);

    if (expectLegal)
    {
        AssertTrue(result.IsSuccess, $"expected legal digivolve at cost {submitCost} onto {targetColor} Lv{targetLevel} ({result.Message})");
    }
    else
    {
        AssertTrue(result.IsIllegal, $"expected illegal digivolve at cost {submitCost} (cost mismatch), got success");
    }
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
