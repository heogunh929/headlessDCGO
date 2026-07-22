using System.Collections;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

// ══════════════════════════════════════════════════════════════════════════════════════════════════════
// BT3-Behavior.Witness — F2 coverage re-home (R7 repair).
//
// PROVENANCE / HONEST FINDING: campaign f1d1c835 retired tests/BT23.PrimTranche4.Tests as "white-box only",
// carrying behavioral pins for BT3_019 / BT3_030 / BT3_100 / BT3_102. The retirement's justification (recorded
// in ActivatedEffectResolver.cs:568-575: "the printed cards BT3_019 / BT3_100 / BT3_102 / BT3_107 / BT3_112 are
// ported and drive the live substrate directly") is FACTUALLY WRONG: all four cards are still unported skeletons
// (7-8 line // TODO stubs, last touched at initial skeleton generation), and the bespoke primitive carriers the
// old test drove white-box (SelectHandAttachToOwnStackThenMemoryEffect, ActivatedPlayFromUnderEffect,
// ChooseCountThenTrashDigivolutionEffect, OpponentBinaryChoiceEffect, …) were DELETED in that same campaign.
//
// Because the printed cards cannot be driven (skeletons) and the carriers no longer exist (deleted), each pin is
// re-homed onto the LIVE SUBSTRATE the deletion comment itself names as the cards' eventual driver
// (DigivolutionStackHelpers / the sink mutations / ChoiceType.ModeChoice). Every subtest below exercises a REAL,
// reachable engine primitive through its real API — the exact substrate a faithful port of each card would call —
// not a re-implementation and not a deleted wrapper. The card-LAYER predicates each card would add on top (the
// [Durandamon]/[BryweLudramon] name filter, the Lv<=4 gate, the count menu, the opponent selectPlayer) live in
// the unported card and are NOT asserted here; they are called out per-subtest as the residual card-layer gap.
// ══════════════════════════════════════════════════════════════════════════════════════════════════════

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("BT3_019 pin (G8): a hand card is attached to the TOP of the owner's own digivolution stack + memory gained (live DigivolutionStackHelpers.AddSourcesTopAsync — docstring-tagged 'G8 / BT3_019')", Bt3_019_AttachHandCardToStackTop),
    ("BT3_030 pin (G9): a designated under-card is played as a NEW cost-free battle-area permanent, leaving the host stack (live DigivolutionStackHelpers.PlaySpecificSourceAsync)", Bt3_030_PlayUnderCardAsNewPermanentCostFree),
    ("BT3_100 pin (G12): a chosen count is trashed from the BOTTOM of every opponent Digimon, capped per stack (live DigivolutionStackHelpers.TrashSourcesAsync fromBottom)", Bt3_100_TrashCountFromBottomOfAllOpponentDigimon),
    ("BT3_102 pin (G13): a scripted binary/mode selection routes to the corresponding branch (live ChoiceType.ModeChoice dispatch via the real ChoiceProvider)", Bt3_102_ModeChoiceRoutesToBranch),
};

int failures = 0;
foreach ((string name, Func<Task> body) in tests)
{
    try { await body(); Console.WriteLine($"PASS {name}"); }
    catch (Exception ex)
    {
        failures++;
        Console.Error.WriteLine($"FAIL {name}");
        Console.Error.WriteLine($"  {ex.GetType().Name}: {ex.Message}");
    }
}

if (failures > 0) { Console.Error.WriteLine($"\n{failures} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// ── Subtests ─────────────────────────────────────────────────────────────────────────────────────────

// BT3_019 [When Digivolving]: place 1 [Durandamon]/[BryweLudramon] from hand on TOP of this card's digivolution
// cards, then gain 3 memory. Live substrate = DigivolutionStackHelpers.AddSourcesTopAsync (its own docstring
// tags it "(G8 / BT3_019) AddDigivolutionCardsTop") + the AddMemory sink mutation. Residual card-layer gap: the
// [Durandamon]/[BryweLudramon] name filter and the optional-skip arm belong to the unported card.
async Task Bt3_019_AttachHandCardToStackTop()
{
    EngineContext ctx = NewContext();
    ctx.MemoryController.Set(0);
    HeadlessEntityId host = Battle(ctx, P1, "HOST19", "Host", "Digimon");
    HeadlessEntityId hand = Hand(ctx, P1, "DURAND", "Durandamon", "Digimon");

    await DigivolutionStackHelpers.AddSourcesTopAsync(
        ctx.CardInstanceRepository, ctx.ZoneMover, host, new[] { hand }, ChoiceZone.Hand,
        context: ctx, gameEventQueue: ctx.GameEventQueue);

    MatchStateMutationSink sink = NewSink(ctx);
    sink.Apply(new EffectMutation(
        MatchStateMutationSink.AddMemoryKind, host,
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.AmountKey] = 3 }));
    await sink.FlushAsync();

    List<HeadlessEntityId> under = SourceIds(ctx, host, P1);
    AssertTrue(under.Contains(hand), "the placed hand card is now a digivolution source of the host");
    AssertEqual(hand.Value, under[0].Value, "it is at the TOP of the stack (add-to-top prepends)");
    AssertFalse(InZoneP(ctx, P1, ChoiceZone.Hand, hand), "the placed card left the hand");
    AssertEqual(3, ctx.MemoryController.Current.Current, "gained 3 memory");
}

// BT3_030 [When Digivolving]: play 1 of your Digimon's Lv<=4 digivolution cards as a new Digimon, cost-free. Live
// substrate = DigivolutionStackHelpers.PlaySpecificSourceAsync (detach a source and land it as a new field
// permanent). Residual card-layer gap: the Lv<=4 candidate predicate and the cost-free rule are the unported
// card's; here we assert the raw substrate play (no memory is spent by the helper itself).
async Task Bt3_030_PlayUnderCardAsNewPermanentCostFree()
{
    EngineContext ctx = NewContext();
    ctx.MemoryController.Set(5);
    HeadlessEntityId host = Battle(ctx, P1, "HOST30", "Host", "Digimon");
    HeadlessEntityId under = SourceCard(ctx, P1, "U4", level: 4);
    SetSources(ctx, host, under);
    int memBefore = ctx.MemoryController.Current.Current;

    bool played = await DigivolutionStackHelpers.PlaySpecificSourceAsync(
        ctx.CardInstanceRepository, ctx.ZoneMover, host, under, ChoiceZone.BattleArea);

    AssertTrue(played, "the substrate reported the under-card was played");
    AssertTrue(InZoneP(ctx, P1, ChoiceZone.BattleArea, under), "the under-card is now a battle-area permanent");
    AssertFalse(SourceIds(ctx, host, P1).Contains(under), "the played under-card left the host's stack");
    AssertEqual(memBefore, ctx.MemoryController.Current.Current, "cost-free: the helper spent no memory");
}

// BT3_100 [Main]: trash the chosen number of digivolution cards from the bottom of all of your opponent's Digimon.
// Live substrate = DigivolutionStackHelpers.TrashSourcesAsync(fromBottom: true), applied per opponent host with a
// per-stack cap = min(count, available). Residual card-layer gap: the count menu (SelectCount) and the
// eligibility scan belong to the unported card; here count=2 is fixed and driven straight against the substrate.
async Task Bt3_100_TrashCountFromBottomOfAllOpponentDigimon()
{
    EngineContext ctx = NewContext();
    HeadlessEntityId t1 = Battle(ctx, P2, "T1", "Opp1", "Digimon");
    SetSources(ctx, t1, SourceCard(ctx, P2, "A", 3), SourceCard(ctx, P2, "B", 3));
    HeadlessEntityId t2 = Battle(ctx, P2, "T2", "Opp2", "Digimon");
    SetSources(ctx, t2, SourceCard(ctx, P2, "C", 3));

    int n1 = await DigivolutionStackHelpers.TrashSourcesAsync(
        ctx.CardInstanceRepository, ctx.ZoneMover, t1, count: 2, fromBottom: true,
        gameEventQueue: ctx.GameEventQueue, context: ctx);
    int n2 = await DigivolutionStackHelpers.TrashSourcesAsync(
        ctx.CardInstanceRepository, ctx.ZoneMover, t2, count: 2, fromBottom: true,
        gameEventQueue: ctx.GameEventQueue, context: ctx);

    AssertEqual(2, n1, "T1 (2 sources) trashed min(count 2, available 2) = 2");
    AssertEqual(1, n2, "T2 (1 source) trashed min(count 2, available 1) = 1 (capped, not over-trashed)");
    AssertEqual(0, SourceIds(ctx, t1, P2).Count, "T1's digivolution stack is emptied from the bottom");
    AssertEqual(0, SourceIds(ctx, t2, P2).Count, "T2's single source was trashed");
}

// BT3_102 [Main]: the opponent makes a binary decision; a "yes" runs one branch, a "no" the other. Live substrate
// = ChoiceType.ModeChoice dispatched through the real ChoiceProvider, then the selected branch's coroutine runs
// (TfxSelectMode fixture — the live re-point of the deleted ModeChoiceEffect carrier onto the AS-IS inline
// ActivateClass menu). Two runs prove selection index 0 vs 1 route to distinct, observable branches. Residual
// card-layer gap: routing the prompt to the OPPONENT seat (selectPlayer) and the specific yes/no effect bodies
// are the unported BT3_102's; the witnessed capability is the substrate's "scripted binary pick -> that branch".
async Task Bt3_102_ModeChoiceRoutesToBranch()
{
    // Branch 0 = "Draw 1", branch 1 = "Draw 3" (TfxSelectMode). Distinct draw counts make the routed branch
    // observable via the library delta.
    int drawnWhenPickingBranch0 = await RunModeChoice(pickIndex: 0);
    int drawnWhenPickingBranch1 = await RunModeChoice(pickIndex: 1);

    AssertEqual(1, drawnWhenPickingBranch0, "picking mode branch 0 ran the 'Draw 1' branch (library -1)");
    AssertEqual(3, drawnWhenPickingBranch1, "picking mode branch 1 ran the 'Draw 3' branch (library -3)");
    AssertTrue(drawnWhenPickingBranch0 != drawnWhenPickingBranch1,
        "a different binary/mode selection routes to a different branch (the BT3_102 yes/no capability)");
}

async Task<int> RunModeChoice(int pickIndex)
{
    EngineContext ctx = NewContext();
    HeadlessEntityId owner = Battle(ctx, P1, "TfxSelectMode", "ModeFixture", "Digimon");
    for (int i = 0; i < 6; i++) { Library(ctx, P1, $"LIB{i}"); }
    int libraryBefore = ((IZoneStateReader)ctx.ZoneMover).GetCards(P1, ChoiceZone.Library).Count;

    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(ctx);
    var card = new CardSource(ctx, owner, P1);
    List<ICardEffect> effects = new TfxSelectMode().CardEffects(EffectTiming.OptionSkill, card);
    var menu = (HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects.ActivateClass)effects[0];

    var provider = (ScriptedChoiceProvider)ctx.ChoiceProvider;
    provider.Clear();
    provider.Enqueue(ChoiceResult.Select(new HeadlessEntityId($"{owner.Value}#mode#{pickIndex}")));

    await menu.Activate(new Hashtable());

    int libraryAfter = ((IZoneStateReader)ctx.ZoneMover).GetCards(P1, ChoiceZone.Library).Count;
    return libraryBefore - libraryAfter;
}

// ── Harness ──────────────────────────────────────────────────────────────────────────────────────────

EngineContext NewContext()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 9);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.TurnController.SetPhase(HeadlessPhase.Main);
    return ctx;
}

MatchStateMutationSink NewSink(EngineContext ctx) =>
    new MatchStateMutationSink(ctx.CardInstanceRepository, ctx.LogSink, ctx.ZoneMover, ctx.MemoryController, ctx.GameEventQueue, context: ctx);

HeadlessEntityId Battle(EngineContext ctx, HeadlessPlayerId owner, string number, string name, string cardType)
{
    var defId = new HeadlessEntityId($"DEF:{number}:{owner.Value}");
    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(defId, number, name, new Dictionary<string, object?>(StringComparer.Ordinal), CardType: cardType));
    var id = new HeadlessEntityId($"{owner.Value}:{number}:battle");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner, Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["isSuspended"] = false }));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
    return id;
}

HeadlessEntityId Hand(EngineContext ctx, HeadlessPlayerId owner, string number, string name, string cardType)
{
    var defId = new HeadlessEntityId($"DEF:{number}:{owner.Value}");
    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(defId, number, name, new Dictionary<string, object?>(StringComparer.Ordinal), CardType: cardType));
    var id = new HeadlessEntityId($"{owner.Value}:{number}:hand");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner, Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.Hand)).GetAwaiter().GetResult();
    return id;
}

HeadlessEntityId Library(EngineContext ctx, HeadlessPlayerId owner, string number)
{
    var defId = new HeadlessEntityId($"DEF:{number}:{owner.Value}");
    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(defId, number, number, new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:{number}:lib");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner, Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.Library)).GetAwaiter().GetResult();
    return id;
}

HeadlessEntityId SourceCard(EngineContext ctx, HeadlessPlayerId owner, string number, int level)
{
    var defId = new HeadlessEntityId($"DEF:{number}:{owner.Value}");
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["level"] = level };
    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(defId, number, number, meta, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:{number}:src");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner, Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["level"] = level }));
    return id;
}

void SetSources(EngineContext ctx, HeadlessEntityId host, params HeadlessEntityId[] sources)
{
    ctx.CardInstanceRepository.TryGetInstance(host, out CardInstanceRecord? rec);
    var meta = new Dictionary<string, object?>(rec!.Metadata, StringComparer.Ordinal)
    {
        [DigivolutionStackReader.SourceIdsKey] = sources.Select(s => s.Value).ToArray(),
    };
    ctx.CardInstanceRepository.Upsert(rec with { Metadata = meta });
}

List<HeadlessEntityId> SourceIds(EngineContext ctx, HeadlessEntityId host, HeadlessPlayerId owner) =>
    new Permanent(ctx, host, owner).DigivolutionCards.Select(c => c.InstanceId).ToList();

bool InZoneP(EngineContext ctx, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId id) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(player, zone).Contains(id);

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"expected true: {label}"); }
static void AssertFalse(bool v, string label) { if (v) throw new InvalidOperationException($"expected false: {label}"); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
}
