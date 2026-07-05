// PRIM-P0 B.O.6: CardEffectFactory.CanNotAddSecurityStaticEffect — a player-scope "cannot add security" grant
// consulted at the AddToSecurity mutation choke (AS-IS Player.CanAddSecurity). Verifies a security add is
// blocked for the restricted player, allowed without the grant, and unaffected for the opponent.
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
    ("without the grant, a card is added to P1 security", AddsWithoutGrant),
    ("with CanNotAddSecurityStaticEffect on P1, the add is blocked (card stays put)", BlockedWithGrant),
    ("the grant on P1 does NOT block P2's security add", OpponentUnaffected),
    ("with a causing-effect predicate, only matching-source adds are blocked (fidelity)", CausingPredicateHonored),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failures.Add(test.Name); Console.Error.WriteLine($"FAIL {test.Name}\n{ex.GetType().Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task AddsWithoutGrant()
{
    EngineContext context = Context();
    var card = await PlaceInTrash(context, P1, "C1");
    await AddToSecurity(context, P1, card);
    AssertTrue(InZone(context, P1, ChoiceZone.Security, card), "card moved to security");
}

async Task BlockedWithGrant()
{
    EngineContext context = Context();
    var card = await PlaceInTrash(context, P1, "C1");
    GrantCannotAddSecurity(context, P1);
    await AddToSecurity(context, P1, card);
    AssertTrue(!InZone(context, P1, ChoiceZone.Security, card), "the add was blocked (not in security)");
    AssertTrue(InZone(context, P1, ChoiceZone.Trash, card), "card stayed in the trash");
}

async Task OpponentUnaffected()
{
    EngineContext context = Context();
    var card = await PlaceInTrash(context, P2, "C2");
    GrantCannotAddSecurity(context, P1);   // restriction is on P1, not P2
    await AddToSecurity(context, P2, card);
    AssertTrue(InZone(context, P2, ChoiceZone.Security, card), "P2 can still add to security");
}

// The AS-IS CardEffectCondition: the restriction fires only when the causing effect matches. Here it fires
// only for adds caused by a source named "BLOCK"; an add caused by "ALLOW" goes through.
async Task CausingPredicateHonored()
{
    EngineContext context = Context();
    var blockSrc = new HeadlessEntityId("1:battle:BLOCK");
    var allowSrc = new HeadlessEntityId("1:battle:ALLOW");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(blockSrc, new HeadlessEntityId("DEF:BLOCK"), P1, Metadata: new Dictionary<string, object?>()));
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(allowSrc, new HeadlessEntityId("DEF:ALLOW"), P1, Metadata: new Dictionary<string, object?>()));

    var grantSrc = new HeadlessEntityId("1:battle:GRANT");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(grantSrc, new HeadlessEntityId("DEF:GRANT"), P1, Metadata: new Dictionary<string, object?>()));
    var grantCard = new CardSource(context, grantSrc, P1, P1);
    ICardEffect effect = CardEffectFactory.CanNotAddSecurityStaticEffect(P1, isInheritedEffect: false, grantCard, condition: null,
        causingEffectPredicate: cause => cause.InstanceId.Value.Contains("BLOCK", StringComparison.Ordinal));
    context.EffectRegistry.Register(effect.ToBinding("1:cannotAddSecurity:pred"));

    var c1 = await PlaceInTrash(context, P1, "X1");
    await AddToSecurityFrom(context, P1, c1, blockSrc);
    AssertTrue(!InZone(context, P1, ChoiceZone.Security, c1), "add caused by BLOCK source is restricted");

    var c2 = await PlaceInTrash(context, P1, "X2");
    await AddToSecurityFrom(context, P1, c2, allowSrc);
    AssertTrue(InZone(context, P1, ChoiceZone.Security, c2), "add caused by ALLOW source goes through");
}

async Task AddToSecurityFrom(EngineContext context, HeadlessPlayerId owner, HeadlessEntityId card, HeadlessEntityId cause)
{
    var sink = new MatchStateMutationSink(context.CardInstanceRepository, log: null, context.ZoneMover, context.MemoryController, context.EffectRegistry, context.GameEventQueue, context: context);
    sink.Apply(new EffectMutation(MatchStateMutationSink.AddToSecurityKind, cause,
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = card.Value }));
    await sink.FlushAsync();
}

// --- Harness -------------------------------------------------------------

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 7);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    return context;
}

async Task<HeadlessEntityId> PlaceInTrash(EngineContext context, HeadlessPlayerId owner, string tag)
{
    var cards = (CardDatabase)context.CardRepository;
    var def = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(def, tag, tag, new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:trash:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, owner, Metadata: new Dictionary<string, object?>()));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.Trash));
    return id;
}

async Task AddToSecurity(EngineContext context, HeadlessPlayerId owner, HeadlessEntityId card)
{
    var sink = new MatchStateMutationSink(context.CardInstanceRepository, log: null, context.ZoneMover, context.MemoryController, context.EffectRegistry, context.GameEventQueue, context: context);
    sink.Apply(new EffectMutation(MatchStateMutationSink.AddToSecurityKind, new HeadlessEntityId($"src:{owner.Value}"),
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = card.Value }));
    await sink.FlushAsync();
}

void GrantCannotAddSecurity(EngineContext context, HeadlessPlayerId player)
{
    // Grant via the real card-facing factory (source card owned by `player`).
    var srcId = new HeadlessEntityId($"{player.Value}:battle:GRANT");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(srcId, new HeadlessEntityId("DEF:GRANT"), player, Metadata: new Dictionary<string, object?>()));
    var card = new CardSource(context, srcId, player, player);
    ICardEffect effect = CardEffectFactory.CanNotAddSecurityStaticEffect(player, isInheritedEffect: false, card, condition: null);
    context.EffectRegistry.Register(effect.ToBinding($"{player.Value}:cannotAddSecurity"));
}

bool InZone(EngineContext context, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId card) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(player, zone).Contains(card);

static void AssertTrue(bool value, string label) { if (!value) throw new InvalidOperationException($"{label}: expected true."); }
