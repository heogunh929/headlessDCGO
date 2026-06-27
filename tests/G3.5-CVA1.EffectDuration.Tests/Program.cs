using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// CV-A1 / F-1: EffectDuration system. A continuous EffectBinding can carry an EffectDuration; the
// EffectDurationExpiry hooks remove it at the matching expiry point (turn end, battle end, unsuspend).
// Verified here at the gate level (a duration-tagged DP modifier boosts DP until it expires) and at the
// registry level (the right durations are removed for each scope; permanent bindings survive).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
HeadlessEntityId Card = new("p1:main:001:P1-M01");

var tests = new (string Name, Func<Task> Body)[]
{
    ("EffectDuration enum has the 8 original durations", () => Pure(EnumHasEightDurations)),
    ("UntilEachTurnEnd DP modifier boosts DP, then expires at turn end", () => Pure(EachTurnEndExpires)),
    ("UntilOwnerTurnEnd expires only on the owner's turn end", () => Pure(OwnerTurnEndScope)),
    ("UntilOpponentTurnEnd expires only on the opponent's turn end", () => Pure(OpponentTurnEndScope)),
    ("UntilEndBattle / UntilEndAttack expire at their hooks; permanent survives", () => Pure(BattleAndAttendExpiry)),
    ("UntilNextUntap / UntilOwnerActivePhase expire at unsuspend", () => Pure(UnsuspendExpiry)),
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

void EnumHasEightDurations()
{
    AssertEqual(8, Enum.GetValues<EffectDuration>().Length, "duration count");
    foreach (string name in new[] { "UntilEachTurnEnd", "UntilOwnerTurnEnd", "UntilOpponentTurnEnd",
        "UntilEndAttack", "UntilEndBattle", "UntilOwnerActivePhase", "UntilNextUntap", "UntilCalculateFixedCost" })
    {
        AssertTrue(Enum.IsDefined(typeof(EffectDuration), name) || Enum.GetNames<EffectDuration>().Contains(name), $"has {name}");
    }
}

void EachTurnEndExpires()
{
    EngineContext context = EngineContext.CreateDefault();
    RegisterDp(context, Card, owner: P1, dpDelta: 3000, EffectDuration.UntilEachTurnEnd);

    AssertEqual(5000, ContinuousDpGate.ResolveDp(context, Card, baseDp: 2000), "DP boosted before expiry");
    int removed = EffectDurationExpiry.ExpireTurnEnd(context.EffectRegistry, endingTurnPlayerId: P2);
    AssertEqual(1, removed, "each-turn-end binding removed at any turn end");
    AssertEqual(2000, ContinuousDpGate.ResolveDp(context, Card, baseDp: 2000), "DP back to base after expiry");
}

void OwnerTurnEndScope()
{
    EngineContext context = EngineContext.CreateDefault();
    RegisterDp(context, Card, owner: P1, dpDelta: 1000, EffectDuration.UntilOwnerTurnEnd);

    AssertEqual(0, EffectDurationExpiry.ExpireTurnEnd(context.EffectRegistry, endingTurnPlayerId: P2), "survives opponent's turn end");
    AssertEqual(3000, ContinuousDpGate.ResolveDp(context, Card, baseDp: 2000), "still applies");
    AssertEqual(1, EffectDurationExpiry.ExpireTurnEnd(context.EffectRegistry, endingTurnPlayerId: P1), "expires on owner's turn end");
}

void OpponentTurnEndScope()
{
    EngineContext context = EngineContext.CreateDefault();
    RegisterDp(context, Card, owner: P1, dpDelta: 1000, EffectDuration.UntilOpponentTurnEnd);

    AssertEqual(0, EffectDurationExpiry.ExpireTurnEnd(context.EffectRegistry, endingTurnPlayerId: P1), "survives owner's turn end");
    AssertEqual(1, EffectDurationExpiry.ExpireTurnEnd(context.EffectRegistry, endingTurnPlayerId: P2), "expires on opponent's turn end");
}

void BattleAndAttendExpiry()
{
    EngineContext context = EngineContext.CreateDefault();
    RegisterDp(context, Card, owner: P1, dpDelta: 1000, EffectDuration.UntilEndBattle);
    RegisterDp(context, new HeadlessEntityId("atk"), owner: P1, dpDelta: 1000, EffectDuration.UntilEndAttack);
    RegisterDp(context, new HeadlessEntityId("perm"), owner: P1, dpDelta: 1000, duration: null); // permanent

    AssertEqual(1, EffectDurationExpiry.ExpireBattleEnd(context.EffectRegistry), "battle-end expired");
    AssertEqual(1, EffectDurationExpiry.ExpireAttackEnd(context.EffectRegistry), "attack-end expired");
    // permanent (null duration) survives all expiry passes
    AssertEqual(0, EffectDurationExpiry.ExpireTurnEnd(context.EffectRegistry, P1), "permanent survives turn end");
    AssertEqual(3000, ContinuousDpGate.ResolveDp(context, new HeadlessEntityId("perm"), baseDp: 2000), "permanent still applies");
}

void UnsuspendExpiry()
{
    EngineContext context = EngineContext.CreateDefault();
    RegisterDp(context, Card, owner: P1, dpDelta: 1000, EffectDuration.UntilNextUntap);
    RegisterDp(context, new HeadlessEntityId("p1b"), owner: P1, dpDelta: 1000, EffectDuration.UntilOwnerActivePhase);

    // Opponent unsuspends: only UntilNextUntap (any) expires, not the owner-active-phase one.
    AssertEqual(1, EffectDurationExpiry.ExpireUnsuspend(context.EffectRegistry, unsuspendingPlayerId: P2), "next-untap expired on any unsuspend");
    AssertEqual(1, EffectDurationExpiry.ExpireUnsuspend(context.EffectRegistry, unsuspendingPlayerId: P1), "owner-active-phase expired on owner unsuspend");
}

// --- Helpers -------------------------------------------------------------

void RegisterDp(EngineContext context, HeadlessEntityId cardId, HeadlessPlayerId owner, int dpDelta, EffectDuration? duration)
{
    var effectId = new HeadlessEntityId($"dp:{cardId.Value}:{dpDelta}:{duration}");
    var effectContext = new EffectContext(
        owner, owner, new HeadlessEntityId($"src:{cardId.Value}"),
        triggerEntityId: null, targetEntityIds: new[] { cardId },
        values: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dpDelta"] = dpDelta });
    context.EffectRegistry.Register(new EffectBinding(
        new EffectRequest(effectId, owner, "Continuous", effectContext),
        keywords: null,
        EffectQueryRole.Continuous,
        new[] { ContinuousRestrictionGate.Scope },
        effect: null,
        duration: duration));
}

static Task Pure(Action body) { body(); return Task.CompletedTask; }

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
