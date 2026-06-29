using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G7-006: BattleResolver now opens the OnStartBattle window for each participant before the DP
// comparison. Verifies the emit (the remaining combat-detail timings follow the same one-line pattern).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Resolving a battle emits OnStartBattle for both participants", EmitsOnStartBattle),
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

async Task EmitsOnStartBattle()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 706);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);

    HeadlessEntityId attacker = await Digimon(context, P1, "A", dp: 5000);
    HeadlessEntityId defender = await Digimon(context, P2, "D", dp: 3000);

    context.AttackController.DeclareAttack(P1, attacker, P2, defender, isDirectAttack: false);
    BattleResolutionResult result = await new BattleResolver().ResolveAsync(context);
    AssertTrue(result.IsSuccess, "battle resolved");

    var events = context.GameEventQueue.DrainPending();
    bool attackerWindow = events.Any(e => Timing(e) == TriggerTimings.OnStartBattle && Subject(e) == attacker.Value);
    bool defenderWindow = events.Any(e => Timing(e) == TriggerTimings.OnStartBattle && Subject(e) == defender.Value);
    AssertTrue(attackerWindow, "OnStartBattle emitted for the attacker");
    AssertTrue(defenderWindow, "OnStartBattle emitted for the defender");
}

// --- Helpers -------------------------------------------------------------

async Task<HeadlessEntityId> Digimon(EngineContext context, HeadlessPlayerId owner, string tag, int dp)
{
    CardDatabase cards = (CardDatabase)context.CardRepository;
    var def = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(def, def.Value, tag, new Dictionary<string, object?>(), CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp };
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, owner, Metadata: meta));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

static string? Timing(GameEvent e) =>
    e.Metadata.TryGetValue(AutoProcessingTriggerCollector.TriggerTimingKey, out object? raw) ? raw?.ToString() : null;

static string? Subject(GameEvent e) =>
    e.Metadata.TryGetValue(AutoProcessingTriggerCollector.SourceEntityIdKey, out object? raw) ? raw?.ToString() : null;

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
