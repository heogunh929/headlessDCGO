using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// PRIM-W2 (G9-031): the last three W2 primitives:
//  - LinkEffect -> LinkSelfEffect: choose a host + attach this card as a link card (LinkHelpers).
//  - PlaceSelfDelayOptionSecurityEffect / PlaySelfDigimonAfterBattleSecurityEffect -> play this card from
//    security to the battle area (reuse PlayThisCardToBattleEffect).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("LinkEffect: attach this card to a chosen host, pay the link cost", LinkAttaches),
    ("PlaceSelfDelayOptionSecurityEffect: plays this card from security to battle", PlaceDelayOption),
    ("PlaySelfDigimonAfterBattleSecurityEffect: plays this Digimon from security to battle", PlayAfterBattle),
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

async Task LinkAttaches()
{
    EngineContext context = Context();
    context.MemoryController.Set(5);
    var host = await Place(context, P1, "HOST", ChoiceZone.BattleArea, linkCost: 0);
    var linkCard = await Place(context, P1, "LINK", ChoiceZone.Hand, linkCost: 2);

    ((ScriptedChoiceProvider)context.ChoiceProvider).Enqueue(ChoiceResult.Select(host));
    var effect = (LinkSelfEffect)CardEffectFactory.LinkEffect(new CardSource(context, linkCard, P1));
    await effect.ResolveAsync(default);

    var linked = LinkHelpers.ReadLinkedCardIds(
        context.CardInstanceRepository.TryGetInstance(host, out CardInstanceRecord? h) && h is not null ? h.Metadata : new Dictionary<string, object?>());
    AssertTrue(linked.Contains(linkCard), "the link card is now attached to the host");
    AssertEqual(3, context.MemoryController.Current.Current, "link cost 2 paid: 5 -> 3");
}

async Task PlaceDelayOption()
{
    EngineContext context = Context();
    var card = await Place(context, P1, "OPT", ChoiceZone.Security, linkCost: 0);
    await ApplyPlaySelf(context, (PlayThisCardToBattleEffect)CardEffectFactory.PlaceSelfDelayOptionSecurityEffect(new CardSource(context, card, P1)));
    AssertTrue(InBattle(context, card), "the card was placed into the battle area from security");
}

async Task PlayAfterBattle()
{
    EngineContext context = Context();
    var card = await Place(context, P1, "DIG", ChoiceZone.Security, linkCost: 0);
    await ApplyPlaySelf(context, (PlayThisCardToBattleEffect)CardEffectFactory.PlaySelfDigimonAfterBattleSecurityEffect(new CardSource(context, card, P1)));
    AssertTrue(InBattle(context, card), "the Digimon was played into the battle area from security");
}

// --- Helpers -------------------------------------------------------------

async Task ApplyPlaySelf(EngineContext context, PlayThisCardToBattleEffect effect)
{
    var sink = new MatchStateMutationSink(
        context.CardInstanceRepository, context.LogSink, context.ZoneMover, context.MemoryController, context.EffectRegistry, context.GameEventQueue);
    effect.Apply(sink);
    await sink.FlushAsync();
}

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 931);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    return context;
}

async Task<HeadlessEntityId> Place(EngineContext context, HeadlessPlayerId owner, string tag, ChoiceZone zone, int linkCost)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId(tag);
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4, ["linkCost"] = linkCost }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:{zone}:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = false }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone));
    return id;
}

bool InBattle(EngineContext context, HeadlessEntityId id) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(P1, ChoiceZone.BattleArea).Contains(id);

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
