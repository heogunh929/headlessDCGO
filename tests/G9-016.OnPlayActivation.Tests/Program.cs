using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G9-016: a played card's OWN [On Play] (OnEnterFieldAnyone) activated effect fires through the real
// PlayCardAction flow — verifying the on-play activation wiring (PlayCardAction resolves the played card's
// own OnEnterFieldAnyone activated effects on the normal play path, mirroring the AS-IS
// StackSkillInfos(OnEnterFieldAnyone) broadcast which includes the entering card). End-to-end (not a
// manual ResolveAsync).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
HeadlessEntityId Card = new("p1:hand:TfxOnPlayDraw");

var tests = new (string Name, Func<Task> Body)[]
{
    ("Playing an [On Play] Draw card draws via PlayCardAction (hand +2, library -2)", OnPlayDrawFires),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}\n{ex.GetType().Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task OnPlayDrawFires()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 916);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    var cards = (CardDatabase)context.CardRepository;

    cards.Upsert(new CardRecord(new HeadlessEntityId("TfxOnPlayDraw"), "TfxOnPlayDraw", "OnPlayDraw",
        new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon", PlayCost: 3));
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(Card, new HeadlessEntityId("TfxOnPlayDraw"), P1));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, Card, ChoiceZone.None, ChoiceZone.Hand));

    for (int i = 0; i < 5; i++)
    {
        var defId = new HeadlessEntityId($"LIB:{i}");
        cards.Upsert(new CardRecord(defId, defId.Value, $"lib{i}", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
        var id = new HeadlessEntityId($"p1:lib:{i}");
        context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, P1));
        await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.Library));
    }
    context.MemoryController.Set(5);

    AssertEqual(1, Hand(context), "hand = just the card to play");
    AssertEqual(5, Library(context), "library starts at 5");

    ActionProcessResult result = await new PlayCardAction()
        .ProcessAsync(HeadlessActionFactory.PlayCard(P1, Card, 3), context);
    AssertTrue(result.IsSuccess, $"play succeeded ({result.Message})");

    AssertTrue(((IZoneStateReader)context.ZoneMover).GetCards(P1, ChoiceZone.BattleArea).Contains(Card), "card on battle area");
    AssertEqual(2, Hand(context), "hand +2 from [On Play] Draw (was 1 card, played it, drew 2)");
    AssertEqual(3, Library(context), "library -2 after [On Play] Draw (5 -> 3)");
}

int Hand(EngineContext c) => ((IZoneStateReader)c.ZoneMover).GetCards(P1, ChoiceZone.Hand).Count;
int Library(EngineContext c) => ((IZoneStateReader)c.ZoneMover).GetCards(P1, ChoiceZone.Library).Count;

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
