// E-2 (L2 / RD-3) Burst re-digivolve edge: when is the temporary burst card trashed at turn end, and what
// happens if a NORMAL digivolve is stacked on top of the burst card in the same turn?
//
// AS-IS re-derivation (the original "AddTrashTopCardAtTurnEnd is undefined" premise is FALSE — that was a
// mojibake misread; the method is defined at SelectBurstDigivolutionEffect.cs:249-344):
//   * A Burst digivolve is isEvolution: CardController.cs:1528-1529 -> DigivolveCount_ThisTurn++ AND draw 1.
//   * CardController.cs:1531-1538: because _burstDigivolved==true for THIS play, it stamps the permanent
//     (IsBurstDigivolved=true) and calls selectBurstDigivolutionEffect.AddTrashTopCardAtTurnEnd(permanent).
//   * AddTrashTopCardAtTurnEnd (SelectBurstDigivolutionEffect.cs:249-344) registers an OnEndTurn ActivateClass
//     on the PERMANENT (permanent.UntilEachTurnEndEffects). Its ActivateCoroutine1 reads
//     `CardSource cardSource = permanent.TopCard;` LIVE at end of turn (:315) and trashes THAT card, then
//     RemoveDigivolveRootEffect reverts the permanent to the prior form. The gate needs
//     DigivolutionCards.Count >= 1 (:293).
//   * _burstDigivolved is a per-play flag, so a subsequent NORMAL digivolve (a separate play, _burstDigivolved
//     == false) does NOT register a second marker — there is exactly ONE registration, on the permanent.
//
//   => AS-IS RE-DIGIVOLVE EDGE: the single registered end-of-turn effect reads permanent.TopCard LIVE, so it
//      trashes whatever is on TOP at end of turn. If a normal digivolve stacked a NEW top over the burst card,
//      AS-IS trashes the NEW top (the current top), and the buried burst card SURVIVES.
//
// Headless model: SpecialPlayAction (SpecialPlayAction.cs:390-397) stamps the burst-card INSTANCE with
// GameFlowProcessor.BurstTrashAtTurnEndKey. HeadlessEndTurnCleanupFlow.PromoteBurstMarkers walks each
// battle-area permanent's STACK (top + buried sources) to promote the marker to *Due on the owner's turn —
// so a same-turn re-digivolve that buries the burst card no longer orphans it. The due sweep
// (GameFlowProcessor.SweepDueTurnEndDeletionsAsync) then locates the marker anywhere in the stack and trashes
// the permanent's LIVE top, mirroring the AS-IS permanent.TopCard read.
//
// See the AS-IS parity asserted in ReDigivolveEdge below (design item E2-01, remediated).
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("(a/d) burst digivolve -> +1 isEvolution draw; at turn end the burst TOP self-trashes and reverts", BurstTemporaryTrashAndDraw),
    ("(b) RE-DIGIVOLVE EDGE: a normal digivolve stacked on the burst top (characterizes the AS-IS divergence)", ReDigivolveEdge),
    ("(c) a plain (non-burst) digivolve carries NO burst marker -> nothing self-trashes at turn end", PlainDigivolveNoTrash),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}\n{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// ---- (a/d) simple burst temporary + draw ------------------------------------

async Task BurstTemporaryTrashAndDraw()
{
    EngineContext ctx = NewContext();
    ctx.MemoryController.Set(5);
    var baseTarget = Battle(ctx, "BASE", "Base", level: 3, colors: new[] { "Red" });
    var burst = Hand(ctx, "BURST", "Burst", level: 4, colors: new[] { "Red" });
    Deck(ctx, "d1"); Deck(ctx, "d2");                 // library so the isEvolution draw has cards
    int deckBefore = ZoneCount(ctx, ChoiceZone.Library);
    int handBefore = ZoneCount(ctx, ChoiceZone.Hand); // = 1 (burst)

    var action = SpecialPlayAction.Create(P1, burst, new[] { baseTarget }, memoryCost: 0, SpecialPlayKind.Burst);
    AssertTrue((await new SpecialPlayAction().ProcessAsync(action, ctx)).IsSuccess, "burst digivolve resolved");

    AssertTrue(InZone(ctx, ChoiceZone.BattleArea, burst), "the burst card is the battle-area top");
    AssertTrue(SourcesOf(ctx, burst).Contains(baseTarget.Value), "the burst-from target is now a source under it");
    AssertTrue(HasKey(ctx, burst, GameFlowProcessor.BurstTrashAtTurnEndKey), "the burst top carries the turn-end trash marker");
    // (d) isEvolution +1 draw.
    AssertEqual(deckBefore - 1, ZoneCount(ctx, ChoiceZone.Library), "the burst digivolve drew 1 card (deck -1)");
    AssertEqual(handBefore, ZoneCount(ctx, ChoiceZone.Hand), "hand net unchanged (burst left hand, 1 drawn in)");
    AssertEqual(1, ctx.PlayerTurnCounters.Get(P1, PlayerTurnCounterController.DigivolveCountKey), "per-turn digivolve counter incremented");

    // End of P1's turn: cleanup promotes the marker, the sweep trashes the burst top and reverts the permanent.
    new HeadlessEndTurnCleanupFlow().Cleanup(ctx, ctx.TurnController.Current);
    AssertTrue(HasKey(ctx, burst, GameFlowProcessor.BurstTrashAtTurnEndDueKey), "cleanup promoted the marker to DUE on the owner's turn");
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertTrue(InZone(ctx, ChoiceZone.Trash, burst), "the burst top was trashed at turn end");
    AssertFalse(InZone(ctx, ChoiceZone.BattleArea, burst), "the burst top left the battle area");
    AssertTrue(InZone(ctx, ChoiceZone.BattleArea, baseTarget), "the permanent reverted: the prior form is the new battle-area top");
}

// ---- (b) the re-digivolve edge ----------------------------------------------

async Task ReDigivolveEdge()
{
    EngineContext ctx = NewContext();
    ctx.MemoryController.Set(9);
    var baseTarget = Battle(ctx, "BASE", "Base", level: 3, colors: new[] { "Red" });
    var burst = Hand(ctx, "BURST", "Burst", level: 4, colors: new[] { "Red" });
    // newTop evolves FROM the burst card (Red@4) via a normal, cost-paid digivolve.
    var newTop = HandEvo(ctx, "NEWTOP", "NewTop", level: 5, evolutionCondition: "Red@4", fixedCost: 2);
    Deck(ctx, "d1"); Deck(ctx, "d2"); Deck(ctx, "d3"); Deck(ctx, "d4");   // enough for two isEvolution draws

    // 1) Burst-digivolve burst onto baseTarget (marker lands on `burst`, which becomes the top).
    AssertTrue((await new SpecialPlayAction()
        .ProcessAsync(SpecialPlayAction.Create(P1, burst, new[] { baseTarget }, memoryCost: 0, SpecialPlayKind.Burst), ctx)).IsSuccess,
        "burst digivolve resolved");
    AssertTrue(HasKey(ctx, burst, GameFlowProcessor.BurstTrashAtTurnEndKey), "burst top carries the marker");

    // 2) Same turn: a NORMAL digivolve stacks newTop over the burst card -> burst becomes a buried source.
    AssertTrue((await new DigivolveAction()
        .ProcessAsync(HeadlessActionFactory.Digivolve(P1, newTop, burst, memoryCost: 2), ctx)).IsSuccess,
        "normal digivolve onto the burst top resolved");
    AssertTrue(InZone(ctx, ChoiceZone.BattleArea, newTop), "newTop is the current battle-area top");
    AssertTrue(SourcesOf(ctx, newTop).Contains(burst.Value), "the burst card is now a BURIED digivolution source under newTop");
    AssertFalse(InZone(ctx, ChoiceZone.BattleArea, burst), "the burst card is no longer a battle-area top");
    // The normal digivolve does NOT re-stamp (its play is not a burst) and does NOT clear the buried card's marker.
    AssertTrue(HasKey(ctx, burst, GameFlowProcessor.BurstTrashAtTurnEndKey), "the buried burst card STILL carries its marker (never promoted/cleared)");
    AssertFalse(HasKey(ctx, newTop, GameFlowProcessor.BurstTrashAtTurnEndKey), "newTop carries NO burst marker (a normal digivolve is not a burst)");

    // 3) End of P1's turn.
    new HeadlessEndTurnCleanupFlow().Cleanup(ctx, ctx.TurnController.Current);
    await new GameFlowProcessor().RunToStableAsync(ctx);

    // ------------------------------------------------------------------------------------------------------
    // AS-IS PARITY (design item E2-01, remediated): the OnEndTurn effect reads permanent.TopCard LIVE
    // (SelectBurstDigivolutionEffect.cs:315), so it trashes the CURRENT top == newTop, and the buried burst
    // card survives (the permanent reverts to it). Headless anchors the marker on the burst INSTANCE, but the
    // end-turn promotion (HeadlessEndTurnCleanupFlow.PromoteBurstMarkers) now walks the permanent's stack to
    // reach the buried marker, and the due sweep (GameFlowProcessor.SweepDueTurnEndDeletionsAsync) locates the
    // marker anywhere in the stack and trashes the permanent's LIVE top -> newTop is trashed, burst reverts up.
    // ------------------------------------------------------------------------------------------------------
    AssertFalse(InZone(ctx, ChoiceZone.BattleArea, newTop),
        "AS-IS PARITY: newTop (the current live top) is trashed at turn end");
    AssertTrue(InZone(ctx, ChoiceZone.Trash, newTop), "newTop was trashed (the live TopCard read target)");
    AssertFalse(InZone(ctx, ChoiceZone.Trash, burst), "the buried burst card is NOT trashed");
    AssertTrue(InZone(ctx, ChoiceZone.BattleArea, burst), "the permanent reverted: the burst card is the new live top");
    AssertFalse(SourcesOf(ctx, burst).Contains(newTop.Value), "the reverted top (burst) does not source the trashed newTop");
}

// ---- (c) plain digivolve carries no burst marker ----------------------------

async Task PlainDigivolveNoTrash()
{
    EngineContext ctx = NewContext();
    ctx.MemoryController.Set(9);
    var baseTarget = Battle(ctx, "BASE", "Base", level: 4, colors: new[] { "Red" });
    var newTop = HandEvo(ctx, "NEWTOP", "NewTop", level: 5, evolutionCondition: "Red@4", fixedCost: 2);
    Deck(ctx, "d1"); Deck(ctx, "d2");

    AssertTrue((await new DigivolveAction()
        .ProcessAsync(HeadlessActionFactory.Digivolve(P1, newTop, baseTarget, memoryCost: 2), ctx)).IsSuccess,
        "plain digivolve resolved");
    AssertFalse(HasKey(ctx, newTop, GameFlowProcessor.BurstTrashAtTurnEndKey), "a plain digivolve stamps NO burst marker");
    AssertFalse(HasKey(ctx, baseTarget, GameFlowProcessor.BurstTrashAtTurnEndKey), "the source carries no burst marker either");

    new HeadlessEndTurnCleanupFlow().Cleanup(ctx, ctx.TurnController.Current);
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertTrue(InZone(ctx, ChoiceZone.BattleArea, newTop), "the plain digivolve's top survives the turn end (no burst self-trash)");
    AssertFalse(InZone(ctx, ChoiceZone.Trash, newTop), "nothing was trashed at turn end");
}

// ---- Harness ----------------------------------------------------------------

EngineContext NewContext()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 1531);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);   // P1's turn is ending
    return ctx;
}

CardRecord Def(EngineContext ctx, string number, string name, int? level, string[]? colors, int? evolutionCost, string? evolutionCondition)
{
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal);
    if (level is int lv) { meta["level"] = lv; }
    if (colors is not null) { meta["colors"] = colors; }
    if (evolutionCost is int fc) { meta["fixedDigivolutionCost"] = fc; }
    var def = new CardRecord(new HeadlessEntityId(number), number, name, meta, CardType: "Digimon",
        EvolutionCost: evolutionCost, EvolutionCondition: evolutionCondition);
    ((CardDatabase)ctx.CardRepository).Upsert(def);
    return def;
}

HeadlessEntityId Place(EngineContext ctx, string idSuffix, string number, string name, ChoiceZone zone,
    int? level, string[]? colors, int? evolutionCost, string? evolutionCondition)
{
    Def(ctx, number, name, level, colors, evolutionCost, evolutionCondition);
    var id = new HeadlessEntityId($"p1:{idSuffix}");
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal);
    if (evolutionCost is int fc) { meta["fixedDigivolutionCost"] = fc; }
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId(number), P1, Metadata: meta));
    if (zone != ChoiceZone.None)
    {
        ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, zone)).GetAwaiter().GetResult();
    }
    return id;
}

HeadlessEntityId Battle(EngineContext ctx, string number, string name, int? level = null, string[]? colors = null) =>
    Place(ctx, number, number, name, ChoiceZone.BattleArea, level, colors, null, null);

HeadlessEntityId Hand(EngineContext ctx, string number, string name, int? level = null, string[]? colors = null) =>
    Place(ctx, number, number, name, ChoiceZone.Hand, level, colors, null, null);

HeadlessEntityId HandEvo(EngineContext ctx, string number, string name, int level, string evolutionCondition, int fixedCost) =>
    Place(ctx, number, number, name, ChoiceZone.Hand, level, new[] { "Red" }, fixedCost, evolutionCondition);

HeadlessEntityId Deck(EngineContext ctx, string idSuffix) =>
    Place(ctx, idSuffix, idSuffix, idSuffix, ChoiceZone.Library, 3, new[] { "Red" }, null, null);

bool InZone(EngineContext ctx, ChoiceZone zone, HeadlessEntityId id) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(P1, zone).Contains(id);

int ZoneCount(EngineContext ctx, ChoiceZone zone) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(P1, zone).Count;

bool HasKey(EngineContext ctx, HeadlessEntityId id, string key) =>
    ctx.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
    && rec.Metadata.TryGetValue(key, out object? v) && v is true;

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
