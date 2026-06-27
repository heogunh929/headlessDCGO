using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// F-5: player-scope continuous effects. An effect can apply to EVERY one of a player's permanents that
// meets a condition (not an individually-targeted card). The continuous gates (DP / restriction) fold
// these in via PlayerScopeContinuousHelpers: "your Digimon get +1000 DP", "your opponent cannot attack".

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
HeadlessEntityId Dig1 = new("p1:main:DIG1");   // P1 Digimon
HeadlessEntityId Tam1 = new("p1:main:TAM1");   // P1 Tamer
HeadlessEntityId Dig2 = new("p2:main:DIG2");   // P2 Digimon

var tests = new (string Name, Func<Task> Body)[]
{
    ("Player-scope +DP applies to the owner's permanents only", () => Pure(PlayerScopeDpScopedToOwner)),
    ("Player-scope +DP with a CardType condition applies only to matching cards", () => Pure(PlayerScopeDpCardTypeCondition)),
    ("Player-scope cannot-attack restriction reaches the owner's permanents", () => Pure(PlayerScopeRestriction)),
    ("ConditionMatches honours CardType and metadata conditions", () => Pure(ConditionMatchesUnit)),
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

static Task Pure(Action body) { body(); return Task.CompletedTask; }

// --- Tests ---------------------------------------------------------------

void PlayerScopeDpScopedToOwner()
{
    EngineContext context = Board();
    RegisterPlayerScopeDp(context, scopePlayer: P1, dpDelta: 1000, cardType: null);

    AssertEqual(4000, ContinuousDpGate.ResolveDp(context, Dig1, baseDp: 3000), "P1 Digimon boosted");
    AssertEqual(4000, ContinuousDpGate.ResolveDp(context, Tam1, baseDp: 3000), "P1 Tamer boosted");
    AssertEqual(3000, ContinuousDpGate.ResolveDp(context, Dig2, baseDp: 3000), "P2 card NOT boosted (different owner)");
}

void PlayerScopeDpCardTypeCondition()
{
    EngineContext context = Board();
    RegisterPlayerScopeDp(context, scopePlayer: P1, dpDelta: 2000, cardType: "Digimon");

    AssertEqual(5000, ContinuousDpGate.ResolveDp(context, Dig1, baseDp: 3000), "P1 Digimon matched the condition");
    AssertEqual(3000, ContinuousDpGate.ResolveDp(context, Tam1, baseDp: 3000), "P1 Tamer did not match Digimon condition");
    AssertEqual(3000, ContinuousDpGate.ResolveDp(context, Dig2, baseDp: 3000), "P2 Digimon wrong owner");
}

void PlayerScopeRestriction()
{
    EngineContext context = Board();
    RegisterPlayerScopeCannotAttack(context, scopePlayer: P2);

    AssertTrue(ContinuousRestrictionGate.EvaluateAttack(context, Dig2).IsRestricted, "P2's Digimon cannot attack");
    AssertFalse(ContinuousRestrictionGate.EvaluateAttack(context, Dig1).IsRestricted, "P1's Digimon unaffected");
}

void ConditionMatchesUnit()
{
    CardRecord digimon = Def("DIG1", "Digimon", trait: "Dragon");
    var typeMatch = new Dictionary<string, object?>(StringComparer.Ordinal) { [PlayerScopeContinuousHelpers.ScopeCardTypeKey] = "Digimon" };
    var typeMiss = new Dictionary<string, object?>(StringComparer.Ordinal) { [PlayerScopeContinuousHelpers.ScopeCardTypeKey] = "Tamer" };
    AssertTrue(PlayerScopeContinuousHelpers.ConditionMatches(typeMatch, digimon), "CardType match");
    AssertFalse(PlayerScopeContinuousHelpers.ConditionMatches(typeMiss, digimon), "CardType mismatch");

    var metaMatch = new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [PlayerScopeContinuousHelpers.ScopeMetaKeyKey] = "trait",
        [PlayerScopeContinuousHelpers.ScopeMetaValueKey] = "Dragon",
    };
    var metaMiss = new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [PlayerScopeContinuousHelpers.ScopeMetaKeyKey] = "trait",
        [PlayerScopeContinuousHelpers.ScopeMetaValueKey] = "Beast",
    };
    AssertTrue(PlayerScopeContinuousHelpers.ConditionMatches(metaMatch, digimon), "metadata match");
    AssertFalse(PlayerScopeContinuousHelpers.ConditionMatches(metaMiss, digimon), "metadata mismatch");

    AssertTrue(PlayerScopeContinuousHelpers.ConditionMatches(new Dictionary<string, object?>(StringComparer.Ordinal), digimon), "no condition matches all");
}

// --- Setup helpers -------------------------------------------------------

EngineContext Board()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 9);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    cards.Upsert(Def("DIG1", "Digimon", trait: "Dragon"));
    cards.Upsert(Def("TAM1", "Tamer", trait: null));
    cards.Upsert(Def("DIG2", "Digimon", trait: "Beast"));
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(Dig1, new HeadlessEntityId("DIG1"), P1));
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(Tam1, new HeadlessEntityId("TAM1"), P1));
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(Dig2, new HeadlessEntityId("DIG2"), P2));
    return context;
}

static CardRecord Def(string id, string cardType, string? trait)
{
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal);
    if (trait is not null)
    {
        meta["trait"] = trait;
    }

    return new CardRecord(new HeadlessEntityId(id), id, $"{id} Card", meta, CardType: cardType);
}

void RegisterPlayerScopeDp(EngineContext context, HeadlessPlayerId scopePlayer, int dpDelta, string? cardType)
{
    var values = new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [PlayerScopeContinuousHelpers.PlayerScopeKey] = true,
        [PlayerScopeContinuousHelpers.ScopePlayerIdKey] = scopePlayer.Value,
        ["dpDelta"] = dpDelta,
    };
    if (cardType is not null)
    {
        values[PlayerScopeContinuousHelpers.ScopeCardTypeKey] = cardType;
    }

    Register(context, $"pscope:dp:{scopePlayer.Value}:{dpDelta}", scopePlayer, values);
}

void RegisterPlayerScopeCannotAttack(EngineContext context, HeadlessPlayerId scopePlayer)
{
    var values = new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [PlayerScopeContinuousHelpers.PlayerScopeKey] = true,
        [PlayerScopeContinuousHelpers.ScopePlayerIdKey] = scopePlayer.Value,
        [RestrictionHelpers.CannotAttackKey] = true,
    };
    Register(context, $"pscope:cannotattack:{scopePlayer.Value}", scopePlayer, values);
}

void Register(EngineContext context, string effectId, HeadlessPlayerId owner, Dictionary<string, object?> values)
{
    var effectContext = new EffectContext(
        owner, owner, new HeadlessEntityId($"src:{effectId}"),
        triggerEntityId: null, targetEntityIds: Array.Empty<HeadlessEntityId>(), values: values);
    context.EffectRegistry.Register(new EffectBinding(
        new EffectRequest(new HeadlessEntityId(effectId), owner, "Continuous", effectContext),
        keywords: null, EffectQueryRole.Continuous, new[] { ContinuousRestrictionGate.Scope }));
}

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertFalse(bool v, string label) { if (v) throw new InvalidOperationException($"{label}: expected false."); }
