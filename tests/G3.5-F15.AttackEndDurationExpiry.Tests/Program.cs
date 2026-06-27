using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// F-1.5: the attack-end EffectDuration expiry hook is now wired into the attack pipeline. A continuous
// binding tagged UntilEndAttack survives the battle (so "+DP until end of attack" applies during it) and
// is removed once the attack completes (AttackPipeline.AdvanceEndAttack). A permanent binding survives.

HeadlessPlayerId Player = new(1);
HeadlessPlayerId Opponent = new(2);
HeadlessEntityId AttackerId = new("p1:main:001:P1-M01");
HeadlessEntityId TargetId = new("p2:main:001:P2-M01");
HeadlessEntityId Boosted = new("p1:main:boosted");
HeadlessEntityId Permanent = new("p1:main:permanent");

var tests = new (string Name, Func<Task> Body)[]
{
    ("UntilEndAttack binding expires when the attack completes; permanent survives", AttackEndExpires),
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

async Task AttackEndExpires()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 11);
    RegisterDp(context, Boosted, dpDelta: 2000, EffectDuration.UntilEndAttack);
    RegisterDp(context, Permanent, dpDelta: 1000, duration: null);

    AssertEqual(5000, ContinuousDpGate.ResolveDp(context, Boosted, baseDp: 3000), "boost applies before the attack ends");

    context.AttackController.DeclareAttack(Player, AttackerId, Opponent, TargetId, isDirectAttack: false);
    var pipeline = new AttackPipeline();
    for (int step = 0; step < 6 && context.AttackController.Current.Phase is not AttackPhase.Completed and not AttackPhase.None; step++)
    {
        await pipeline.AdvanceAsync(context);
    }

    AssertEqual(3000, ContinuousDpGate.ResolveDp(context, Boosted, baseDp: 3000), "UntilEndAttack boost gone after the attack");
    AssertEqual(4000, ContinuousDpGate.ResolveDp(context, Permanent, baseDp: 3000), "permanent boost survives");
}

void RegisterDp(EngineContext context, HeadlessEntityId cardId, int dpDelta, EffectDuration? duration)
{
    var effectId = new HeadlessEntityId($"dp:{cardId.Value}:{duration}");
    var effectContext = new EffectContext(
        Player, Player, new HeadlessEntityId($"src:{cardId.Value}"),
        triggerEntityId: null, targetEntityIds: new[] { cardId },
        values: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dpDelta"] = dpDelta });
    context.EffectRegistry.Register(new EffectBinding(
        new EffectRequest(effectId, Player, "Continuous", effectContext),
        keywords: null,
        EffectQueryRole.Continuous,
        new[] { ContinuousRestrictionGate.Scope },
        effect: null,
        duration: duration));
}

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
