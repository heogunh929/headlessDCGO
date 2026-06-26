using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

// G3.5-RL-B1: typed DP model (DpModifier + DpCalculator) and digivolution stack, mirroring the
// original Permanent.BaseDP accumulation: relative deltas first, then absolute "set" effects in
// activation order (last wins), clamped at zero. BattleResolver computes DP through DpCalculator.

HeadlessPlayerId Player = new(1);
HeadlessPlayerId Opponent = new(2);
HeadlessEntityId AttackerId = new("p1:main:001:P1-M01");
HeadlessEntityId TargetId = new("p2:main:001:P2-M01");

var tests = new (string Name, Func<Task> Body)[]
{
    ("No modifiers returns the base DP", () => Pure(NoModifiersReturnsBase)),
    ("Relative modifiers are summed onto the base", () => Pure(RelativeModifiersSum)),
    ("Absolute modifier overrides the base", () => Pure(AbsoluteOverridesBase)),
    ("Relative deltas apply before an absolute set", () => Pure(RelativeBeforeAbsolute)),
    ("Multiple absolute sets resolve by activation order", () => Pure(AbsoluteSetsResolveByOrder)),
    ("DP is clamped at zero", () => Pure(ClampedAtZero)),
    ("Digivolution stack exposes top card, depth and base DP", () => Pure(StackExposesTopAndBaseDp)),
    ("Digivolution stack rejects a non-top Top role", () => Pure(StackRejectsInvalidTop)),
    ("Battle uses computed DP so a modifier flips the outcome", BattleUsesComputedDp),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try
    {
        await test.Body();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        failures.Add($"{test.Name}: {ex.GetType().Name}: {ex.Message}");
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}");
    }
}

if (failures.Count > 0)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine($"{failures.Count} test(s) failed.");
    Environment.Exit(1);
}

Console.WriteLine();
Console.WriteLine($"{tests.Length} test(s) passed.");

static Task Pure(Action body)
{
    body();
    return Task.CompletedTask;
}

// --- Pure DP / stack model ----------------------------------------------

void NoModifiersReturnsBase()
{
    AssertEqual(3000, DpCalculator.ComputeDp(3000, Array.Empty<DpModifier>()), "base preserved");
}

void RelativeModifiersSum()
{
    int dp = DpCalculator.ComputeDp(3000, new[]
    {
        DpModifier.Relative(2000),
        DpModifier.Relative(-1000)
    });
    AssertEqual(4000, dp, "relative sum");
}

void AbsoluteOverridesBase()
{
    int dp = DpCalculator.ComputeDp(3000, new[] { DpModifier.Absolute(5000) });
    AssertEqual(5000, dp, "absolute override");
}

void RelativeBeforeAbsolute()
{
    // Relative deltas are applied first, then the absolute set replaces the value entirely.
    int dp = DpCalculator.ComputeDp(3000, new[]
    {
        DpModifier.Relative(2000),
        DpModifier.Absolute(5000)
    });
    AssertEqual(5000, dp, "absolute wins over earlier relative");
}

void AbsoluteSetsResolveByOrder()
{
    // Insertion order is reversed; the later ActivatedOrder must win regardless.
    int dp = DpCalculator.ComputeDp(3000, new[]
    {
        DpModifier.Absolute(7000, activatedOrder: 2),
        DpModifier.Absolute(5000, activatedOrder: 1)
    });
    AssertEqual(7000, dp, "last activated absolute wins");
}

void ClampedAtZero()
{
    int dp = DpCalculator.ComputeDp(1000, new[] { DpModifier.Relative(-5000) });
    AssertEqual(0, dp, "clamped at zero");
}

void StackExposesTopAndBaseDp()
{
    var stack = new DigivolutionStack(new[]
    {
        new StackedCard(new HeadlessEntityId("egg"), "BT1-001", StackRole.DigiEgg, 2, 0),
        new StackedCard(new HeadlessEntityId("rookie"), "BT1-010", StackRole.Digivolution, 3, 2000),
        new StackedCard(new HeadlessEntityId("champion"), "BT1-020", StackRole.Top, 4, 5000)
    });

    AssertEqual(3, stack.Depth, "depth");
    AssertEqual("BT1-020", stack.TopCard!.CardNumber, "top card");
    AssertEqual(5000, stack.BaseDp, "base dp from top card");
    AssertEqual(2, stack.UnderCards.Count, "under cards");
}

void StackRejectsInvalidTop()
{
    AssertThrows(() => new DigivolutionStack(new[]
    {
        new StackedCard(new HeadlessEntityId("a"), "X", StackRole.Top, 3, 1000),
        new StackedCard(new HeadlessEntityId("b"), "Y", StackRole.Top, 4, 2000)
    }), "only the top card may have the Top role");
}

// --- Battle integration --------------------------------------------------

async Task BattleUsesComputedDp()
{
    // Control (no modifier): attacker base 3000 < defender 5000 -> the attacker is deleted.
    DcgoMatch control = await RunBattleAsync(attackerDp: 3000, targetDp: 5000, attackerModifiers: null);
    AssertTrue(InZone(control, Player, ChoiceZone.Trash, AttackerId), "control: attacker deleted (base dp loses)");
    AssertTrue(InZone(control, Opponent, ChoiceZone.BattleArea, TargetId), "control: defender survives");

    // With a typed +3000 relative modifier: attacker effective 6000 > 5000 -> outcome flips.
    DcgoMatch boosted = await RunBattleAsync(
        attackerDp: 3000,
        targetDp: 5000,
        attackerModifiers: new[] { DpModifier.Relative(3000, source: "test") });
    AssertTrue(InZone(boosted, Opponent, ChoiceZone.Trash, TargetId), "boosted: defender deleted (modifier wins)");
    AssertTrue(InZone(boosted, Player, ChoiceZone.BattleArea, AttackerId), "boosted: attacker survives");
}

async Task<DcgoMatch> RunBattleAsync(int attackerDp, int targetDp, IReadOnlyList<DpModifier>? attackerModifiers)
{
    DcgoMatch match = await CreateBattleMatchAsync(attackerDp, targetDp);
    if (attackerModifiers is not null)
    {
        SetMetadata(match, AttackerId, new Dictionary<string, object?>
        {
            [BattleResolver.DpModifiersKey] = attackerModifiers.ToArray()
        });
    }

    await DeclareTargetAttackAsync(match); // the game loop's attack pipeline resolves the battle
    return match;
}

bool InZone(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId)
{
    return ((IZoneStateReader)match.Context.ZoneMover).GetCards(player, zone).Contains(cardId);
}

// --- Battle harness (trimmed from G2G-003) -------------------------------

async Task<DcgoMatch> CreateBattleMatchAsync(int attackerDp, int targetDp)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 73);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(CreateDefinition($"P1-M{index:D2}"));
        cards.Upsert(CreateDefinition($"P2-M{index:D2}"));
    }

    DcgoMatch match = new(context);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { BuildDeck(Player, "P1"), BuildDeck(Opponent, "P2") },
        firstPlayerId: Player);
    await match.InitializeAsync(MatchConfig.Create(new[] { Player, Opponent }, randomSeed: 73, setup: setup));
    await AdvanceToMainAsync(match);

    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Player, AttackerId, ChoiceZone.Hand, ChoiceZone.BattleArea));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Opponent, TargetId, ChoiceZone.Hand, ChoiceZone.BattleArea));

    SetMetadata(match, AttackerId, new Dictionary<string, object?> { ["isSuspended"] = false, [BattleResolver.DpKey] = attackerDp });
    SetMetadata(match, TargetId, new Dictionary<string, object?> { ["isSuspended"] = true, [BattleResolver.DpKey] = targetDp });
    return match;
}

static CardRecord CreateDefinition(string id)
{
    return new CardRecord(new HeadlessEntityId(id), id, $"{id} Card", new Dictionary<string, object?>(), CardType: "Digimon");
}

static PlayerDeckSetup BuildDeck(HeadlessPlayerId playerId, string prefix)
{
    return new PlayerDeckSetup(
        playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());
}

async Task AdvanceToMainAsync(DcgoMatch match)
{
    for (var attempt = 0; attempt < 8 && match.GetObservation().Turn.Phase != HeadlessPhase.Main; attempt++)
    {
        LegalAction advance = match.GetLegalActions(Player).Single(a => a.ActionType == HeadlessActionTypes.AdvancePhase);
        await match.ApplyActionAsync(advance);
        await match.StepAsync();
    }

    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "advance to main");
}

async Task DeclareTargetAttackAsync(DcgoMatch match)
{
    LegalAction attack = match.GetLegalActions(Player)
        .Single(a => a.ActionType == HeadlessActionTypes.DeclareAttack &&
            ReadId(a.Parameters, HeadlessActionParameterKeys.AttackTargetId) == TargetId.Value);
    await match.ApplyActionAsync(attack);
    await match.StepAsync();
}

static string? ReadId(IReadOnlyDictionary<string, object?> parameters, string key)
{
    if (!parameters.TryGetValue(key, out object? raw) || raw is null)
    {
        return null;
    }

    return raw is HeadlessEntityId entityId ? entityId.Value : raw.ToString();
}

void SetMetadata(DcgoMatch match, HeadlessEntityId cardId, IReadOnlyDictionary<string, object?> values)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
    {
        throw new InvalidOperationException($"Missing card instance '{cardId}'.");
    }

    Dictionary<string, object?> metadata = new(record.Metadata, StringComparer.Ordinal);
    foreach (KeyValuePair<string, object?> pair in values)
    {
        metadata[pair.Key] = pair.Value;
    }

    match.Context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
}

// --- Asserts -------------------------------------------------------------

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
    }
}

static void AssertTrue(bool value, string label)
{
    if (!value)
    {
        throw new InvalidOperationException($"{label}: expected true.");
    }
}


static void AssertThrows(Action body, string label)
{
    try
    {
        body();
    }
    catch
    {
        return;
    }

    throw new InvalidOperationException($"{label}: expected an exception.");
}
