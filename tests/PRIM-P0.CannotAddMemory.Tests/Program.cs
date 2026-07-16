// PRIM-P0 B.O.6: CardEffectFactory.CanNotAddMemoryStaticEffect — a player-scope "cannot add memory" grant
// consulted at the AddMemory mutation choke (AS-IS Player.CanAddMemory). A GAIN (positive add) by the
// restricted player is blocked; a loss (negative) and SetMemory are unaffected.
using HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("without the grant, +3 memory is gained (0 -> 3)", GainWithoutGrant),
    ("with CanNotAddMemoryStaticEffect on P1, a +3 gain is blocked (stays 0)", GainBlocked),
    ("the grant does NOT block a memory LOSS (-2 still applies -> -2)", LossUnaffected),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failures.Add(test.Name); Console.Error.WriteLine($"FAIL {test.Name}\n{ex.GetType().Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task GainWithoutGrant()
{
    EngineContext context = Context();
    context.MemoryController.Set(0);
    await AddMemory(context, P1, 3);
    AssertEqual(3, context.MemoryController.Current.Current, "gained 3");
}

async Task GainBlocked()
{
    EngineContext context = Context();
    context.MemoryController.Set(0);
    Grant(context, P1);
    await AddMemory(context, P1, 3);
    AssertEqual(0, context.MemoryController.Current.Current, "gain blocked (stays 0)");
}

async Task LossUnaffected()
{
    EngineContext context = Context();
    context.MemoryController.Set(0);
    Grant(context, P1);
    await AddMemory(context, P1, -2);
    AssertEqual(-2, context.MemoryController.Current.Current, "a loss is not a gain — still applies");
}

// --- Harness -------------------------------------------------------------

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 7);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    // (R3-W3c-4c B3) the live CannotAddMemory scan gates each effect with CanUse (→ CanTrigger, DoneStartGame),
    // so put the match past Setup.
    context.TurnController.SetPhase(HeadlessPhase.Main);
    return context;
}

async Task AddMemory(EngineContext context, HeadlessPlayerId owner, int amount)
{
    using var _ambient = AmbientMatchContext.Enter(context);
    var srcId = new HeadlessEntityId($"{owner.Value}:battle:SRC");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(srcId, new HeadlessEntityId("DEF:SRC"), owner, Metadata: new Dictionary<string, object?>()));
    var sink = new MatchStateMutationSink(context.CardInstanceRepository, log: null, context.ZoneMover, context.MemoryController, context.EffectRegistry, context.GameEventQueue, context: context);
    sink.Apply(new EffectMutation(MatchStateMutationSink.AddMemoryKind, srcId,
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.AmountKey] = amount }));
    await sink.FlushAsync();
}

void Grant(EngineContext context, HeadlessPlayerId player)
{
    // (R3-W3c-4c B3) Grant the AS-IS kind-class CannotAddMemoryClass on a FIELD permanent owned by `player`
    // (the live scan walks Players_ForTurnPlayer's field permanents' EffectList(None)); dispatched via the
    // TfxCannotAddMemory fixture by CardNumber, configured for the restricted player, no causing predicate.
    TfxCannotAddMemory.ScopePlayer = player;
    TfxCannotAddMemory.CausingPredicate = null;
    var cards = (CardDatabase)context.CardRepository;
    var def = new HeadlessEntityId("DEF:GRANT");
    cards.Upsert(new CardRecord(def, "TfxCannotAddMemory", "GRANT", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var srcId = new HeadlessEntityId($"{player.Value}:battle:GRANT");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(srcId, def, player, Metadata: new Dictionary<string, object?>()));
    context.ZoneMover.MoveAsync(new ZoneMoveRequest(player, srcId, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
}

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"{label}: expected '{expected}', actual '{actual}'.");
}
