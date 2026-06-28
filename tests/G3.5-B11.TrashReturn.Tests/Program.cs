// B-11 trash -> hand/deck return. The card-targeted ReturnToHand / ReturnToDeckTop / ReturnToDeckBottom
// mutation kinds are source-zone agnostic (MoveCardToSingleZone), so returning a TRASH card already works;
// the timing map derives OnReturnCardsToHand/LibraryFromTrash for trash-origin moves. This suite verifies
// the trash-origin behaviour end to end through the mutation sink.
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);

var tests = new (string Name, Func<Task> Body)[]
{
    ("A trash card returns to the hand", TrashToHand),
    ("A trash card returns to the deck top", TrashToDeckTop),
    ("A trash card returns to the deck bottom", TrashToDeckBottom),
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

async Task TrashToHand()
{
    (EngineContext context, HeadlessEntityId card) = await TrashedCard();
    await ApplyAsync(context, Return(MatchStateMutationSink.ReturnToHandKind, card));

    AssertTrue(InZone(context, ChoiceZone.Hand, card), "returned to hand");
    AssertFalse(InZone(context, ChoiceZone.Trash, card), "no longer in trash");
}

async Task TrashToDeckTop()
{
    (EngineContext context, HeadlessEntityId card) = await TrashedCard();
    await ApplyAsync(context, Return(MatchStateMutationSink.ReturnToDeckTopKind, card));

    AssertTrue(InZone(context, ChoiceZone.Library, card), "returned to the deck");
    AssertFalse(InZone(context, ChoiceZone.Trash, card), "no longer in trash");
}

async Task TrashToDeckBottom()
{
    (EngineContext context, HeadlessEntityId card) = await TrashedCard();
    await ApplyAsync(context, Return(MatchStateMutationSink.ReturnToDeckBottomKind, card));

    AssertTrue(InZone(context, ChoiceZone.Library, card), "returned to the deck");
    AssertFalse(InZone(context, ChoiceZone.Trash, card), "no longer in trash");
}

// --- Harness -------------------------------------------------------------

async Task<(EngineContext, HeadlessEntityId)> TrashedCard()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 7);
    var card = new HeadlessEntityId("P1-Trashed");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(card, new HeadlessEntityId("def"), P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, card, ChoiceZone.None, ChoiceZone.Trash, FaceUp: true));
    AssertTrue(InZone(context, ChoiceZone.Trash, card), "card starts in the trash");
    return (context, card);

    bool InZone(EngineContext ctx, ChoiceZone zone, HeadlessEntityId id) =>
        ((IZoneStateReader)ctx.ZoneMover).GetCards(P1, zone).Contains(id);
}

async Task ApplyAsync(EngineContext context, EffectMutation mutation)
{
    var sink = new MatchStateMutationSink(context.CardInstanceRepository, log: null, context.ZoneMover, memory: null, context.EffectRegistry, context.GameEventQueue);
    sink.Apply(mutation);
    await sink.FlushAsync();
}

EffectMutation Return(string kind, HeadlessEntityId card) =>
    new(kind, new HeadlessEntityId("src"), new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [MatchStateMutationSink.TargetEntityIdKey] = card.Value,
    });

bool InZone(EngineContext context, ChoiceZone zone, HeadlessEntityId card) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(P1, zone).Contains(card);

static void AssertTrue(bool value, string label) { if (!value) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertFalse(bool value, string label) { if (value) throw new InvalidOperationException($"{label}: expected false."); }
