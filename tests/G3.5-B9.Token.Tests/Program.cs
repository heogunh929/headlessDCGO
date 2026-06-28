// B-9 token creation: an effect creates N token Digimon on the controller's battle area (AS-IS
// CardEffectCommons.PlayToken). Exposed as the player-scoped CreateToken mutation kind — it Upserts IsToken
// instances of the supplied token definition (summoning-sick) and moves them onto the battle area.
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);
const string TokenDef = "TKN-Digimon";

var tests = new (string Name, Func<Task> Body)[]
{
    ("CreateToken places a token on the battle area", CreateOneToken),
    ("CreateToken with count creates that many tokens", CreateMultipleTokens),
    ("CreateToken tapped enters suspended", CreateTokenTapped),
    ("CreateToken without a definition is unsupported", CreateTokenMissingDefinition),
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

async Task CreateOneToken()
{
    EngineContext context = NewContext();
    await ApplyAsync(context, Token("P1-Tok", count: 1));

    var tok = new HeadlessEntityId("P1-Tok");
    AssertTrue(InZone(context, ChoiceZone.BattleArea, tok), "the token is on the battle area");
    AssertTrue(context.CardInstanceRepository.TryGetInstance(tok, out CardInstanceRecord? r) && r is { IsToken: true }, "the instance is a token");
    AssertTrue(ReadFlag(context, tok, "enteredThisTurn"), "the token is summoning-sick");
    AssertEqual(TokenDef, r!.DefinitionId.Value, "the token uses the supplied definition");
}

async Task CreateMultipleTokens()
{
    EngineContext context = NewContext();
    await ApplyAsync(context, Token("P1-Tok", count: 3));

    foreach (string id in new[] { "P1-Tok", "P1-Tok#2", "P1-Tok#3" })
    {
        AssertTrue(InZone(context, ChoiceZone.BattleArea, new HeadlessEntityId(id)), $"{id} is on the battle area");
    }

    AssertEqual(3, ((IZoneStateReader)context.ZoneMover).GetCards(P1, ChoiceZone.BattleArea).Count, "exactly 3 tokens created");
}

async Task CreateTokenTapped()
{
    EngineContext context = NewContext();
    EffectMutation mutation = Token("P1-Tok", count: 1);
    var values = new Dictionary<string, object?>(mutation.Values, StringComparer.Ordinal) { [MatchStateMutationSink.TokenTappedKey] = true };
    await ApplyAsync(context, new EffectMutation(MatchStateMutationSink.CreateTokenKind, new HeadlessEntityId("src"), values));

    AssertTrue(ReadFlag(context, new HeadlessEntityId("P1-Tok"), "isSuspended"), "the tapped token enters suspended");
}

async Task CreateTokenMissingDefinition()
{
    EngineContext context = NewContext();
    var sink = Sink(context);
    sink.Apply(new EffectMutation(MatchStateMutationSink.CreateTokenKind, new HeadlessEntityId("src"),
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [MatchStateMutationSink.PlayerIdKey] = P1,
            [MatchStateMutationSink.TokenInstanceIdKey] = "P1-Tok",
        }));
    await sink.FlushAsync();

    AssertTrue(sink.Unsupported.Any(m => m.Kind == MatchStateMutationSink.CreateTokenKind), "missing token definition -> unsupported");
}

// --- Harness -------------------------------------------------------------

EngineContext NewContext()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 7);
    ((CardDatabase)context.CardRepository).Upsert(
        new CardRecord(new HeadlessEntityId(TokenDef), "TKN", "Token Digimon", new Dictionary<string, object?>(), CardType: "Digimon"));
    return context;
}

MatchStateMutationSink Sink(EngineContext context) =>
    new(context.CardInstanceRepository, log: null, context.ZoneMover, memory: null, context.EffectRegistry, context.GameEventQueue);

async Task ApplyAsync(EngineContext context, EffectMutation mutation)
{
    var sink = Sink(context);
    sink.Apply(mutation);
    await sink.FlushAsync();
}

EffectMutation Token(string instanceId, int count) =>
    new(MatchStateMutationSink.CreateTokenKind, new HeadlessEntityId("src"), new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [MatchStateMutationSink.PlayerIdKey] = P1,
        [MatchStateMutationSink.TokenDefinitionIdKey] = TokenDef,
        [MatchStateMutationSink.TokenInstanceIdKey] = instanceId,
        [MatchStateMutationSink.CountKey] = count,
    });

bool InZone(EngineContext context, ChoiceZone zone, HeadlessEntityId card) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(P1, zone).Contains(card);

bool ReadFlag(EngineContext context, HeadlessEntityId card, string key) =>
    context.CardInstanceRepository.TryGetInstance(card, out CardInstanceRecord? r) && r is not null
        && r.Metadata.TryGetValue(key, out object? raw) && raw is bool b && b;

static void AssertTrue(bool value, string label) { if (!value) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"{label}: expected '{expected}', actual '{actual}'.");
}
