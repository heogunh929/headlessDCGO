// PRIM-P0-flow: the mode-choice activated-effect primitive (CardEffectFactory.SelectModeEffect / ModeChoiceEffect).
// A card's [Main] effect offers a mandatory "choose one" menu (AS-IS UserSelectionManager SetBool/IntSelection);
// the chosen branch (an existing IActivatedCardEffect) resolves through the same activation flow. The fixture
// TfxSelectMode offers Draw 1 / Draw 3, plus a conditional Draw 5 (only when "extraMode" is set). Verifies:
// (1) selecting a mode runs ONLY that branch, (2) a different selection runs a different branch,
// (3) an unavailable mode is omitted from the menu. See docs/porting/mode_choice_primitive_design.md.
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("selecting 'Draw 1' runs only that branch (hand +1)", SelectDraw1),
    ("selecting 'Draw 3' runs the other branch (hand +3)", SelectDraw3),
    ("the conditional mode is selectable when available (hand +5)", ConditionalModeSelectable),
    ("an unavailable mode is omitted from the menu; available -> present", UnavailableModeOmitted),
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

async Task SelectDraw1()
{
    (EngineContext context, HeadlessEntityId src) = await Setup(extraMode: false);
    int before = HandCount(context, P1);
    Answer(context, ModeCandidate(src, 0));   // "Draw 1"

    await ActivatedEffectResolver.ResolveAsync(context, src, P1, EffectTiming.OptionSkill);

    AssertEqual(before + 1, HandCount(context, P1), "only the Draw-1 branch ran");
}

async Task SelectDraw3()
{
    (EngineContext context, HeadlessEntityId src) = await Setup(extraMode: false);
    int before = HandCount(context, P1);
    Answer(context, ModeCandidate(src, 1));   // "Draw 3"

    await ActivatedEffectResolver.ResolveAsync(context, src, P1, EffectTiming.OptionSkill);

    AssertEqual(before + 3, HandCount(context, P1), "only the Draw-3 branch ran");
}

async Task ConditionalModeSelectable()
{
    (EngineContext context, HeadlessEntityId src) = await Setup(extraMode: true);
    int before = HandCount(context, P1);
    Answer(context, ModeCandidate(src, 2));   // "Draw 5" (conditional, available here)

    await ActivatedEffectResolver.ResolveAsync(context, src, P1, EffectTiming.OptionSkill);

    AssertEqual(before + 5, HandCount(context, P1), "the conditional Draw-5 branch ran");
}

// Unit test of the omission logic (AS-IS conditional selectionElements.Add): a mode whose IsAvailable() is
// false is dropped from AvailableModes()/BuildRequest(); a true predicate is kept.
async Task UnavailableModeOmitted()
{
    (EngineContext context, HeadlessEntityId src) = await Setup(extraMode: false);
    var card = new CardSource(context, src, P1);
    ICardEffect stub = CardEffectFactory.DrawCardsEffect(card, 1);

    var effOff = new ModeChoiceEffect(card, "menu", new[]
    {
        new ModeChoiceEffect.Mode("A", null, stub),
        new ModeChoiceEffect.Mode("B", () => false, stub),
    });
    AssertEqual(1, effOff.AvailableModes().Count, "the false-predicate mode is omitted");
    AssertEqual(1, effOff.BuildRequest(effOff.AvailableModes()).Candidates.Count, "menu shows only the available mode");

    var effOn = new ModeChoiceEffect(card, "menu", new[]
    {
        new ModeChoiceEffect.Mode("A", null, stub),
        new ModeChoiceEffect.Mode("B", () => true, stub),
    });
    AssertEqual(2, effOn.AvailableModes().Count, "a true-predicate mode is kept");
    await Task.CompletedTask;
}

// --- Harness -------------------------------------------------------------

HeadlessEntityId ModeCandidate(HeadlessEntityId src, int index) => new($"{src.Value}#mode#{index}");

void Answer(EngineContext context, HeadlessEntityId modeId) =>
    ((ScriptedChoiceProvider)context.ChoiceProvider).Enqueue(ChoiceResult.Select(modeId));

async Task<(EngineContext Context, HeadlessEntityId Src)> Setup(bool extraMode)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 917);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    var cards = (CardDatabase)context.CardRepository;

    // The effect card (dispatches to the TfxSelectMode fixture by card number).
    var defId = new HeadlessEntityId("TfxSelectMode");
    cards.Upsert(new CardRecord(defId, "TfxSelectMode", "ModeSrc", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var src = new HeadlessEntityId("1:battle:SRC");
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["isSuspended"] = false };
    if (extraMode) meta["extraMode"] = true;
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(src, defId, P1, Metadata: meta));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, src, ChoiceZone.None, ChoiceZone.BattleArea));

    // Give P1 a library to draw from (8 cards).
    for (int i = 1; i <= 8; i++)
    {
        var lib = new HeadlessEntityId($"1:lib:{i:D2}");
        cards.Upsert(new CardRecord(new HeadlessEntityId($"DEF:LIB{i}"), $"LIB{i}", $"Lib{i}", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
        context.CardInstanceRepository.Upsert(new CardInstanceRecord(lib, new HeadlessEntityId($"DEF:LIB{i}"), P1, Metadata: new Dictionary<string, object?>()));
        await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, lib, ChoiceZone.None, ChoiceZone.Library));
    }

    return (context, src);
}

int HandCount(EngineContext context, HeadlessPlayerId player) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(player, ChoiceZone.Hand).Count;

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"{label}: expected '{expected}', actual '{actual}'.");
}
