using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// BT-PRE-A2 (G9-016): SimplifiedRevealAndSelectEffect + SimplifiedSelectCardConditionClass — mirror of the
// original SimplifiedRevealDeckTopCardsAndSelect ("reveal top N, add a condition-matching card to hand, rest
// to deck bottom"). TfxRevealSelect reveals 3, lets you add 1 Tamer to hand. Resolved through the activation
// flow; selected/remaining moves are staged on the sink. Verified against a scripted choice provider.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Reveal 3, select the Tamer -> Tamer to hand, other 2 to deck bottom", SelectMatchToHand),
    ("Reveal 3, skip the selection -> all 3 to deck bottom (nothing to hand)", SkipAllToBottom),
    ("Reveal 3 with no matching Tamer -> condition auto-skips, all 3 to deck bottom", NoMatchAllToBottom),
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

async Task SelectMatchToHand()
{
    EngineContext context = Context();
    var src = await PlaceFixture(context, P1, "TfxRevealSelect");
    var tamer = await PlaceLibrary(context, P1, "TAMER", "Tamer");
    await PlaceLibrary(context, P1, "DIGI1", "Digimon");
    await PlaceLibrary(context, P1, "DIGI2", "Digimon");

    ((ScriptedChoiceProvider)context.ChoiceProvider).Enqueue(ChoiceResult.Select(tamer));
    await ActivatedEffectResolver.ResolveAsync(context, src, P1, EffectTiming.OptionSkill);

    AssertTrue(InZone(context, P1, ChoiceZone.Hand, tamer), "selected Tamer went to hand");
    AssertEqual(1, HandCount(context, P1), "exactly 1 card added to hand");
    AssertEqual(2, LibraryCount(context, P1), "the 2 unselected revealed cards remain in the library (deck bottom)");
}

async Task SkipAllToBottom()
{
    EngineContext context = Context();
    var src = await PlaceFixture(context, P1, "TfxRevealSelect");
    var tamer = await PlaceLibrary(context, P1, "TAMER", "Tamer");
    await PlaceLibrary(context, P1, "DIGI1", "Digimon");
    await PlaceLibrary(context, P1, "DIGI2", "Digimon");

    ((ScriptedChoiceProvider)context.ChoiceProvider).Enqueue(ChoiceResult.Skip());
    await ActivatedEffectResolver.ResolveAsync(context, src, P1, EffectTiming.OptionSkill);

    AssertEqual(0, HandCount(context, P1), "nothing added to hand when skipped");
    AssertEqual(3, LibraryCount(context, P1), "all 3 revealed cards returned to the deck bottom");
    AssertTrue(!InZone(context, P1, ChoiceZone.Hand, tamer), "the Tamer was NOT added");
}

async Task NoMatchAllToBottom()
{
    EngineContext context = Context();
    var src = await PlaceFixture(context, P1, "TfxRevealSelect");
    await PlaceLibrary(context, P1, "DIGI1", "Digimon");
    await PlaceLibrary(context, P1, "DIGI2", "Digimon");
    await PlaceLibrary(context, P1, "DIGI3", "Digimon");

    // No Tamer in the top 3 -> the condition has no candidates -> no choice is requested.
    await ActivatedEffectResolver.ResolveAsync(context, src, P1, EffectTiming.OptionSkill);

    AssertEqual(0, HandCount(context, P1), "nothing added (no matching card)");
    AssertEqual(3, LibraryCount(context, P1), "all 3 revealed cards returned to the deck bottom");
}

// --- Helpers -------------------------------------------------------------

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 916);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    return context;
}

async Task<HeadlessEntityId> PlaceFixture(EngineContext context, HeadlessPlayerId owner, string cardNumber)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId(cardNumber);
    cards.Upsert(new CardRecord(defId, cardNumber, cardNumber, new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:src:{cardNumber}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

async Task<HeadlessEntityId> PlaceLibrary(EngineContext context, HeadlessPlayerId owner, string tag, string cardType)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(defId, defId.Value, tag, new Dictionary<string, object?>(StringComparer.Ordinal), CardType: cardType));
    var id = new HeadlessEntityId($"{owner.Value}:lib:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.Library));
    return id;
}

bool InZone(EngineContext context, HeadlessPlayerId p, ChoiceZone zone, HeadlessEntityId id) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(p, zone).Contains(id);

int HandCount(EngineContext context, HeadlessPlayerId p) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(p, ChoiceZone.Hand).Count;

int LibraryCount(EngineContext context, HeadlessPlayerId p) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(p, ChoiceZone.Library).Count;

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
