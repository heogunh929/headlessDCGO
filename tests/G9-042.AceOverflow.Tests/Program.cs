using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// PRIM-W4 (G9-042): the ACE Overflow rule. An un-flipped ACE Digimon leaving the field costs its owner
// `overflowMemory` memory (turn-relative sign). Verified end-to-end through the mutation sink for the
// delete / return-to-hand / return-to-deck field-leave paths, plus the off-turn sign and the no-penalty
// controls (non-ACE, flipped, from-deck, ignoreOverflow).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("ACE deleted on owner's turn -> owner loses 3 (5 -> 2)", () => Leave(MatchStateMutationSink.DeleteKind, ChoiceZone.BattleArea, ace: true, flipped: false, ignore: false, ownerActive: true, 5, 2)),
    ("ACE returned to hand -> owner loses 3 (5 -> 2)", () => Leave(MatchStateMutationSink.ReturnToHandKind, ChoiceZone.BattleArea, true, false, false, true, 5, 2)),
    ("ACE returned to deck -> owner loses 3 (5 -> 2)", () => Leave(MatchStateMutationSink.ReturnToDeckBottomKind, ChoiceZone.BattleArea, true, false, false, true, 5, 2)),
    ("ACE from breeding area -> overflow applies (5 -> 2)", () => Leave(MatchStateMutationSink.DeleteKind, ChoiceZone.BreedingArea, true, false, false, true, 5, 2)),
    ("Off-turn owner's ACE deleted -> turn-relative +3 (5 -> 8)", () => Leave(MatchStateMutationSink.DeleteKind, ChoiceZone.BattleArea, true, false, false, ownerActive: false, 5, 8)),
    ("Non-ACE deleted -> no penalty (5)", () => Leave(MatchStateMutationSink.DeleteKind, ChoiceZone.BattleArea, ace: false, flipped: false, ignore: false, ownerActive: true, 5, 5)),
    ("Flipped ACE deleted -> no penalty (5)", () => Leave(MatchStateMutationSink.DeleteKind, ChoiceZone.BattleArea, true, flipped: true, false, true, 5, 5)),
    ("ignoreOverflow flag -> no penalty (5)", () => Leave(MatchStateMutationSink.DeleteKind, ChoiceZone.BattleArea, true, false, ignore: true, true, 5, 5)),
    ("ACE returned from DECK (not on field) -> no penalty (5)", () => Leave(MatchStateMutationSink.ReturnToHandKind, ChoiceZone.Library, true, false, false, true, 5, 5)),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failures.Add(test.Name); Console.Error.WriteLine($"FAIL {test.Name}: {ex.Message}"); }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Test body -----------------------------------------------------------

async Task Leave(string kind, ChoiceZone fromZone, bool ace, bool flipped, bool ignore, bool ownerActive, int startMemory, int expectedMemory)
{
    EngineContext context = Context();
    context.MemoryController.Set(startMemory);
    HeadlessPlayerId owner = ownerActive ? P1 : P2; // P1 is the turn player
    var id = await Place(context, owner, "ACE", fromZone, ace, flipped);

    var sink = new MatchStateMutationSink(
        context.CardInstanceRepository, context.LogSink, context.ZoneMover, context.MemoryController,
        context.EffectRegistry, context.GameEventQueue, currentTurnPlayer: () => P1);

    var values = new Dictionary<string, object?>(StringComparer.Ordinal) { ["targetEntityId"] = id.Value };
    if (ignore) { values[AceOverflowGate.IgnoreOverflowKey] = true; }
    sink.Apply(new EffectMutation(kind, new HeadlessEntityId("src"), values));
    await sink.FlushAsync();

    AssertEqual(expectedMemory, context.MemoryController.Current.Current, $"{kind} ace={ace} flipped={flipped} ignore={ignore} ownerActive={ownerActive}");
}

// --- Helpers -------------------------------------------------------------

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 942);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    return context;
}

async Task<HeadlessEntityId> Place(EngineContext context, HeadlessPlayerId owner, string tag, ChoiceZone zone, bool ace, bool flipped)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, defId.Value, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 }, CardType: "Digimon"));
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["dp"] = 4000,
        ["isSuspended"] = false,
        [AceOverflowGate.IsAceKey] = ace,
        [AceOverflowGate.OverflowMemoryKey] = 3,
        [AceOverflowGate.IsFlippedKey] = flipped,
    };
    var id = new HeadlessEntityId($"{owner.Value}:{zone}:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner, Metadata: meta));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone));
    return id;
}

static void AssertEqual<T>(T e, T a, string label) { if (!EqualityComparer<T>.Default.Equals(e, a)) throw new InvalidOperationException($"{label}: expected '{e}', got '{a}'."); }
