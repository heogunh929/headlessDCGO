using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G8-003: OnStartBattle is a SYNCHRONOUS window — a battle-start effect that changes a participant's DP is
// resolved and the DP recomputed BEFORE the comparison, so it affects the outcome. Here a +3000 OnStart
// effect flips a would-be loss (3000 vs 5000) into a win (6000 vs 5000).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
HeadlessEntityId Attacker = new("p1:battle:A");
HeadlessEntityId Defender = new("p2:battle:D");

var tests = new (string Name, Func<Task> Body)[]
{
    ("Without an OnStartBattle effect the attacker keeps its base DP (3000 < 5000)", () => Battle(withStartEffect: false, expectedAttackerDp: 3000)),
    ("An OnStartBattle +3000 effect is applied before the DP comparison (6000 > 5000)", () => Battle(withStartEffect: true, expectedAttackerDp: 6000)),
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

async Task Battle(bool withStartEffect, int expectedAttackerDp)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 803);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    await Digimon(context, P1, Attacker, dp: 3000);
    await Digimon(context, P2, Defender, dp: 5000);

    if (withStartEffect)
    {
        var effect = new StartBattleDpEffect(Attacker, 3000);
        var request = new EffectRequest(effect.Definition.EffectId, P1, TriggerTimings.OnStartBattle,
            new EffectContext(P1, Attacker, new Dictionary<string, object?>(StringComparer.Ordinal)));
        context.EffectRegistry.Register(new EffectBinding(request, keywords: null, EffectQueryRole.None, Array.Empty<string>(), effect, duration: null));
    }

    context.AttackController.DeclareAttack(P1, Attacker, P2, Defender, isDirectAttack: false);
    BattleResolutionResult result = await new BattleResolver().ResolveAsync(context);

    AssertTrue(result.IsSuccess, "battle resolved");
    AssertEqual(expectedAttackerDp, result.AttackerDp, "attacker DP used in the comparison");
    AssertEqual(5000, result.DefenderDp, "defender DP");
}

// --- Helpers -------------------------------------------------------------

async Task Digimon(EngineContext context, HeadlessPlayerId owner, HeadlessEntityId id, int dp)
{
    CardDatabase cards = (CardDatabase)context.CardRepository;
    var def = new HeadlessEntityId($"DEF:{id.Value}");
    cards.Upsert(new CardRecord(def, def.Value, id.Value, new Dictionary<string, object?>(), CardType: "Digimon"));
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp };
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, owner, Metadata: meta));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}

// --- Minimal OnStartBattle effect that grants its source +DP -------------

sealed class StartBattleDpEffect : IHeadlessCardEffect
{
    private readonly HeadlessEntityId _target;
    private readonly int _amount;

    public StartBattleDpEffect(HeadlessEntityId target, int amount)
    {
        _target = target;
        _amount = amount;
        Definition = new CardEffectDefinition(new HeadlessEntityId($"{target.Value}:onstartbattle"), target, "OnStartBattleDp", TriggerTimings.OnStartBattle);
    }

    public CardEffectDefinition Definition { get; }

    public CardEffectCanResolveResult CanResolve(CardEffectResolveContext context) => CardEffectCanResolveResult.Success();

    public ValueTask<EffectResult> ResolveAsync(CardEffectResolveContext context, IEffectMutationSink mutations, CancellationToken cancellationToken = default)
    {
        mutations.Apply(new EffectMutation(
            MatchStateMutationSink.AddDpModifierKind,
            _target,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [MatchStateMutationSink.DpValueKey] = _amount,
                [MatchStateMutationSink.DpAbsoluteKey] = false,
                [MatchStateMutationSink.DpActivatedOrderKey] = 0L,
                [MatchStateMutationSink.TargetEntityIdKey] = _target.Value,
            }));
        return ValueTask.FromResult(EffectResult.Success("dp+"));
    }
}
