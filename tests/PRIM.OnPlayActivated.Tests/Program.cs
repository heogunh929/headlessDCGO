using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// PRIM [On Play]: PlayCardAction resolves the just-played card's own OnEnterFieldAnyone activated effects
// through the activation flow. Verified END-TO-END with BT1_029 ([On Play] <Draw 1>): playing it draws a
// card from the deck. This is the resolution point that was previously missing (activated [On Play] effects
// are not auto-registered and the trigger bridge excludes OnEnterFieldAnyone).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("BT1_029: playing it triggers [On Play] <Draw 1> (deck -> hand)", OnPlayDraw),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failures.Add(test.Name); Console.Error.WriteLine($"FAIL {test.Name}: {ex.Message}"); }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task OnPlayDraw()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 29);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    context.TurnController.SetPhase(HeadlessPhase.Main);
    context.MemoryController.Set(8);

    // BT1_029 in hand (playable), and a 3-card deck to draw from.
    var bt1029 = await PlaceHand(context, P1, "BT1_029", playCost: 3);
    for (int i = 0; i < 3; i++)
    {
        await PlaceInZone(context, P1, $"deck{i}", ChoiceZone.Library);
    }

    int deckBefore = Count(context, P1, ChoiceZone.Library);
    AssertEqual(3, deckBefore, "deck starts at 3");

    ActionProcessResult result = await new PlayCardAction()
        .ProcessAsync(HeadlessActionFactory.PlayCard(P1, bt1029, 3), context);
    AssertTrue(result.IsSuccess, $"BT1_029 played ({result.Message})");

    // [On Play] <Draw 1> resolved during the play: exactly one card moved deck -> hand.
    AssertEqual(2, Count(context, P1, ChoiceZone.Library), "deck 3 - 1 drawn = 2 (the [On Play] draw fired)");
    AssertEqual(1, Count(context, P1, ChoiceZone.Hand), "1 card drawn into hand (BT1_029 itself left hand for the battle area)");
}

// --- Helpers ---

async Task<HeadlessEntityId> PlaceHand(EngineContext context, HeadlessPlayerId owner, string cardNumber, int playCost)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId(cardNumber);
    cards.Upsert(new CardRecord(defId, cardNumber, cardNumber,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 4, ["playCost"] = playCost },
        CardType: "Digimon", PlayCost: playCost));
    var id = new HeadlessEntityId($"{owner.Value}:hand:{cardNumber}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["playCost"] = playCost }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.Hand));
    return id;
}

async Task PlaceInZone(EngineContext context, HeadlessPlayerId owner, string tag, ChoiceZone zone)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(defId, defId.Value, tag, new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:{zone}:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone));
}

int Count(EngineContext context, HeadlessPlayerId owner, ChoiceZone zone) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(owner, zone).Count;

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
