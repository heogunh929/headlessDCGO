// E-1 (L1 / RD-1) parity: effect-driven free-digivolve reveal is a PEEK model, end to end.
//
// AS-IS re-derivation (the original "reveal moves cards to an executing zone" premise is FALSE):
//   * RevealLibrary.RevealLibrary() (RevealLibrary.cs:749-790) reads the top-N library cards but leaves them
//     PHYSICALLY on the library top — it only sets IsBeingRevealed=true (:789). No card is moved by the reveal.
//   * BT1_078 (BT1/Green) [When Attacking]: SimplifiedRevealDeckTopCardsAndSelect(revealCount:3, one Custom-mode
//     select maxCount:1 canNoSelect:true, remaining:DeckBottom), THEN — if a card was selected —
//     PlayCardClass(root:Library, payCost:false, targetPermanent: this card's own permanent): a costless
//     digivolve of the selected card straight out of the library (BT1_078.cs:74-108).
//   * The digivolve is isEvolution -> CardController.cs:1528-1529: DigivolveCount_ThisTurn++ AND draw 1. By the
//     time the draw fires, the 2 non-selected revealed cards are already at the deck BOTTOM and the selected
//     card has left the library, so the draw pulls the card BELOW the revealed batch.
//
// Headless mirror (이연③-e): BT1_078 is the literal AS-IS inline [When Attacking] ActivateClass — the
// coroutine-callable commons CardEffectCommons.SimplifiedRevealDeckTopCardsAndSelect (Custom-mode capture,
// remaining:DeckBottom) THEN new PlayCardClass(root:Library, payCost:false, targetPermanent: this card's own
// permanent).PlayCard() — driven here through the real BT1_078 card via ActivatedEffectResolver
// (window-dispatched, as the AS-IS attack window collects the skill). The reveal peeks (only reads the
// library), cards move only when the choice resolves, and the digivolve's isEvolution +1 draw flows through
// the mirrored PlayCardClass.PlayCard() isEvolution path.
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
    ("(a) PEEK: reveal opens the choice WITHOUT moving any library card (size/order unchanged)", Peek_RevealDoesNotMove),
    ("(b/c/d) select -> free digivolve from library (no cost), +1 isEvolution draw, non-selected to deck bottom", SelectFreeDigivolveDrawBottom),
    ("(e) skip: all revealed cards go to the deck bottom, no digivolve, no draw", SkipAllToBottomNoDraw),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}\n{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// ---- (a) Peek --------------------------------------------------------------

async Task Peek_RevealDoesNotMove()
{
    // Deferred provider: the reveal registers its select choice and SUSPENDS (throws
    // DeferredChoicePendingException) BEFORE any of the follow-up moves run. That suspension point is exactly
    // "after reveal, before the choice resolves" — the AS-IS IsBeingRevealed peek window.
    EngineContext ctx = NewContext(deferred: true);
    ctx.MemoryController.Set(5);
    var self = Battle(ctx, "BT1_078", "BT1_078", level: 5, colors: new[] { "Green" });
    var good = Library(ctx, "g6", "G6", level: 6, colors: new[] { "Green" }, evolutionCost: 3);
    var bad1 = Library(ctx, "b4a", "B4", level: 4, colors: new[] { "Green" });
    var bad2 = Library(ctx, "b4b", "B4", level: 4, colors: new[] { "Green" });
    var deep = Library(ctx, "deep", "Deep", level: 3, colors: new[] { "Green" });

    HeadlessEntityId[] before = LibrarySnapshot(ctx);
    AssertEqual(4, before.Length, "4 cards seeded on the library");

    // (이연③-e) drive BT1_078's real inline [When Attacking] ActivateClass and STOP at the reveal-select
    // choice. With the deferred provider and no scripted answer, the reveal's ChooseAsync suspends (throws
    // DeferredChoicePendingException, re-thrown by the resolver's cycle wrapper) BEFORE any follow-up move —
    // exactly the AS-IS IsBeingRevealed peek window. window-dispatched = the attack window already collected
    // the [When Attacking] skill (skips only the CanTrigger re-check).
    bool suspended = false;
    try { await ActivatedEffectResolver.ResolveAsync(ctx, self, P1, EffectTiming.OnAllyAttack, windowDispatched: true); }
    catch (DeferredChoicePendingException) { suspended = true; }
    AssertTrue(suspended, "the reveal opened a pending select choice (deferred), i.e. it stopped at the peek window");

    HeadlessEntityId[] after = LibrarySnapshot(ctx);
    AssertTrue(before.Select(id => id.Value).SequenceEqual(after.Select(id => id.Value)),
        "PEEK: the library is byte-for-byte unchanged (same cards, same order) at the choice point — reveal moved nothing");
    AssertTrue(after.Take(3).Select(id => id.Value).SequenceEqual(new[] { good.Value, bad1.Value, bad2.Value }),
        "the revealed 3 are still the physical top of the library");
    AssertTrue(InZone(ctx, ChoiceZone.Library, deep), "the below-batch card is still in the library");
    AssertTrue(InZone(ctx, ChoiceZone.BattleArea, self), "BT1_078 is still the standalone battle-area top (no digivolve yet)");
    AssertEqual(5, ctx.MemoryController.Current.Current, "no memory touched by a mere reveal");
}

// ---- (b/c/d) select -> digivolve + draw + remaining to bottom --------------

async Task SelectFreeDigivolveDrawBottom()
{
    EngineContext ctx = NewContext();
    ctx.MemoryController.Set(5);
    var self = Battle(ctx, "BT1_078", "BT1_078", level: 5, colors: new[] { "Green" });
    // Top 3: one selectable level-6 green (evolutionCost so TryGetEvolutionCost passes), two not.
    var good = Library(ctx, "g6", "G6", level: 6, colors: new[] { "Green" }, evolutionCost: 3);
    var bad1 = Library(ctx, "b4a", "B4", level: 4, colors: new[] { "Green" });
    var bad2 = Library(ctx, "b4b", "B4", level: 4, colors: new[] { "Green" });
    // 4th card, BELOW the revealed batch — this is the one the isEvolution +1 draw must pull.
    var deep = Library(ctx, "deep", "Deep", level: 3, colors: new[] { "Green" });
    int handBefore = ZoneCount(ctx, ChoiceZone.Hand);

    Script(ctx, ChoiceResult.Select(good));
    // (이연③-e) window-dispatched: the AS-IS attack window collected the [When Attacking] skill.
    await ActivatedEffectResolver.ResolveAsync(ctx, self, P1, EffectTiming.OnAllyAttack, windowDispatched: true);

    // (b) free digivolve straight out of the library, cost NOT paid.
    AssertTrue(InZone(ctx, ChoiceZone.BattleArea, good), "the selected level-6 is the new battle-area top (digivolved from the library)");
    AssertTrue(SourcesOf(ctx, good).Contains(self.Value), "BT1_078 became a digivolution source under the level-6");
    AssertEqual(5, ctx.MemoryController.Current.Current, "no memory paid (free digivolve)");

    // (c) isEvolution +1 draw pulls the card BELOW the revealed batch (AS-IS CardController.cs:1529), never a revealed card.
    AssertTrue(InZone(ctx, ChoiceZone.Hand, deep), "the +1 draw pulled the below-batch card into hand");
    AssertEqual(handBefore + 1, ZoneCount(ctx, ChoiceZone.Hand), "hand grew by exactly the 1 drawn card");
    AssertEqual(1, ctx.PlayerTurnCounters.Get(P1, PlayerTurnCounterController.DigivolveCountKey), "the per-turn digivolve counter incremented");

    // (d) the 2 non-selected revealed cards are at the deck bottom, in reveal order; nothing was extracted to a limbo.
    HeadlessEntityId[] lib = LibrarySnapshot(ctx);
    AssertTrue(lib.Select(id => id.Value).SequenceEqual(new[] { bad1.Value, bad2.Value }),
        "the library is exactly the 2 non-selected cards, in reveal order at the bottom");
    AssertFalse(InZone(ctx, ChoiceZone.Trash, bad1) || InZone(ctx, ChoiceZone.Trash, bad2), "no revealed card was trashed (peek never removes)");
}

// ---- (e) skip --------------------------------------------------------------

async Task SkipAllToBottomNoDraw()
{
    EngineContext ctx = NewContext();
    ctx.MemoryController.Set(5);
    var self = Battle(ctx, "BT1_078", "BT1_078", level: 5, colors: new[] { "Green" });
    var good = Library(ctx, "g6", "G6", level: 6, colors: new[] { "Green" }, evolutionCost: 3);
    var bad1 = Library(ctx, "b4a", "B4", level: 4, colors: new[] { "Green" });
    var bad2 = Library(ctx, "b4b", "B4", level: 4, colors: new[] { "Green" });
    var deep = Library(ctx, "deep", "Deep", level: 3, colors: new[] { "Green" });
    int handBefore = ZoneCount(ctx, ChoiceZone.Hand);

    Script(ctx, ChoiceResult.Skip());
    // (이연③-e) window-dispatched, as above.
    await ActivatedEffectResolver.ResolveAsync(ctx, self, P1, EffectTiming.OnAllyAttack, windowDispatched: true);

    AssertTrue(InZone(ctx, ChoiceZone.BattleArea, self), "no pick -> BT1_078 stays the top (no digivolve)");
    AssertEqual(5, ctx.MemoryController.Current.Current, "no memory paid");
    AssertEqual(handBefore, ZoneCount(ctx, ChoiceZone.Hand), "no digivolve -> no isEvolution draw");
    AssertEqual(0, ctx.PlayerTurnCounters.Get(P1, PlayerTurnCounterController.DigivolveCountKey), "no digivolve -> counter unchanged");
    foreach (HeadlessEntityId id in new[] { good, bad1, bad2, deep })
    {
        AssertTrue(InZone(ctx, ChoiceZone.Library, id), "every card remains in the library (revealed-but-unselected go to the deck bottom)");
    }
    // The 3 revealed cards were sent to the bottom in reveal order; deep (untouched) sits above them.
    HeadlessEntityId[] lib = LibrarySnapshot(ctx);
    AssertTrue(lib.Select(id => id.Value).SequenceEqual(new[] { deep.Value, good.Value, bad1.Value, bad2.Value }),
        "skip sends all 3 revealed to the deck bottom in reveal order; the below-batch card floats to the top");
}

// ---- Harness ----------------------------------------------------------------

EngineContext NewContext(bool deferred = false)
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 11, deferredChoice: deferred);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    return ctx;
}

void Script(EngineContext ctx, params ChoiceResult[] choices)
{
    var provider = (ScriptedChoiceProvider)ctx.ChoiceProvider;
    provider.Clear();
    foreach (ChoiceResult c in choices) { provider.Enqueue(c); }
}

CardRecord Def(EngineContext ctx, string number, string name, string cardType, int? level, string[]? colors, int? evolutionCost)
{
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal);
    if (level is int lv) { meta["level"] = lv; }
    if (colors is not null) { meta["colors"] = colors; }
    var def = new CardRecord(new HeadlessEntityId(number), number, name, meta, CardType: cardType, EvolutionCost: evolutionCost);
    ((CardDatabase)ctx.CardRepository).Upsert(def);
    return def;
}

HeadlessEntityId Place(EngineContext ctx, string idSuffix, string number, string name, ChoiceZone zone, string cardType, int? level, string[]? colors, int? evolutionCost)
{
    Def(ctx, number, name, cardType, level, colors, evolutionCost);
    var id = new HeadlessEntityId($"p1:{idSuffix}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId(number), P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)));
    if (zone != ChoiceZone.None)
    {
        ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, zone)).GetAwaiter().GetResult();
    }
    return id;
}

HeadlessEntityId Battle(EngineContext ctx, string number, string name, int? level = null, string[]? colors = null) =>
    Place(ctx, number, number, name, ChoiceZone.BattleArea, "Digimon", level, colors, null);

HeadlessEntityId Library(EngineContext ctx, string idSuffix, string name, int? level = null, string[]? colors = null, int? evolutionCost = null) =>
    Place(ctx, idSuffix, idSuffix, name, ChoiceZone.Library, "Digimon", level, colors, evolutionCost);

HeadlessEntityId[] LibrarySnapshot(EngineContext ctx) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(P1, ChoiceZone.Library).ToArray();

bool InZone(EngineContext ctx, ChoiceZone zone, HeadlessEntityId id) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(P1, zone).Contains(id);

int ZoneCount(EngineContext ctx, ChoiceZone zone) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(P1, zone).Count;

IReadOnlyList<string> SourcesOf(EngineContext ctx, HeadlessEntityId host)
{
    if (ctx.CardInstanceRepository.TryGetInstance(host, out CardInstanceRecord? rec) && rec is not null
        && rec.Metadata.TryGetValue("sourceIds", out object? raw) && raw is IEnumerable<string> s)
    {
        return s.ToArray();
    }
    return Array.Empty<string>();
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertFalse(bool v, string label) { if (v) throw new InvalidOperationException($"{label}: expected false."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected {expected}, got {actual}.");
    }
}
