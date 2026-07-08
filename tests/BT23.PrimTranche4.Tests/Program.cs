// BT2/BT3 missing-primitive development — Tranche 4 (new primitives).
//   G8  — attach a hand card onto THIS card's own digivolution stack (top) + memory gain.
//   (G9/G10/G12/G13 added below as each primitive lands.)
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("G8: place 1 matching hand card on top of this card's stack, gain 3 memory (BT3_019)", G8_AttachThenMemory),
    ("G8: skipping the optional placement attaches nothing and gains no memory", G8_SkipDoesNothing),
    ("G9: play a Lv<=4 under-card of your Digimon as a new permanent, cost-free (BT3_030)", G9_PlayFromUnderPredicate),
    ("G9: an over-level under-card is not a candidate (predicate gates the flattened select)", G9_PredicateFiltersLevel),
    ("G10: select opp Digimon -> de-digivolve 1 -> destroy if new top cost <=4 (BT3_107)", G10_SelectDeDigivolveDestroy),
    ("G10: new top cost >4 -> survives (post-de-digivolve predicate false)", G10_SelectDeDigivolveSurvives),
    ("G10: mass de-digivolve all opp -> destroy those with DP<=5000 (BT3_112-WD)", G10_MassDeDigivolveDestroy),
    ("G12: choose count 2 -> trash that many (capped) from the bottom of every opp Digimon (BT3_100-A)", G12_ChooseCountTrashAll),
    ("G12: choosing 0 trashes nothing", G12_ChooseZero),
    ("G13: opponent picks 'yes' -> ifYes branch runs (BT3_102)", G13_OpponentYes),
    ("G13: opponent picks 'no' -> ifNo branch runs", G13_OpponentNo),
    ("G13: auto-no when nothing to decide -> ifNo runs with no prompt", G13_AutoNo),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}\n{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// ---- G8 ----------------------------------------------------------------------

async Task G8_AttachThenMemory()
{
    EngineContext ctx = NewContext();
    ctx.MemoryController.Set(0);
    HeadlessEntityId host = Battle(ctx, "HOST", "Host", cardType: "Digimon");
    HeadlessEntityId durand = Hand(ctx, "DURAND", "Durandamon", cardType: "Digimon");
    HeadlessEntityId other = Hand(ctx, "OTHER", "Agumon", cardType: "Digimon"); // not a legal target

    var card = new CardSource(ctx, host, P1);
    var eff = new SelectHandAttachToOwnStackThenMemoryEffect(
        card,
        canTarget: cs => cs.EqualsCardName("Durandamon") || cs.EqualsCardName("BryweLudramon"),
        memoryGain: 3,
        description: "[When Digivolving] place 1 [Durandamon]/[BryweLudramon] from hand on top of digivolution cards -> +3 memory");

    var sink = NewSink(ctx);
    Script(ctx, ChoiceResult.Select(durand));
    await eff.ResolveAsync(sink, CancellationToken.None);
    await sink.FlushAsync();

    var underCards = new Permanent(ctx, host, P1).DigivolutionCards.Select(c => c.InstanceId).ToList();
    AssertTrue(underCards.Contains(durand), "the placed hand card is now a digivolution source of the host");
    AssertFalse(InZone(ctx, ChoiceZone.Hand, durand), "the placed card left the hand");
    AssertTrue(InZone(ctx, ChoiceZone.Hand, other), "the non-target hand card is untouched");
    AssertEqual(3, ctx.MemoryController.Current.Current, "gained 3 memory after placing");
}

async Task G8_SkipDoesNothing()
{
    EngineContext ctx = NewContext();
    ctx.MemoryController.Set(0);
    HeadlessEntityId host = Battle(ctx, "HOST", "Host", cardType: "Digimon");
    HeadlessEntityId durand = Hand(ctx, "DURAND", "Durandamon", cardType: "Digimon");

    var card = new CardSource(ctx, host, P1);
    var eff = new SelectHandAttachToOwnStackThenMemoryEffect(
        card, canTarget: cs => cs.EqualsCardName("Durandamon"), memoryGain: 3, description: "optional");

    var sink = NewSink(ctx);
    Script(ctx, ChoiceResult.Skip());
    await eff.ResolveAsync(sink, CancellationToken.None);
    await sink.FlushAsync();

    AssertTrue(InZone(ctx, ChoiceZone.Hand, durand), "skipping leaves the card in hand");
    AssertEqual(0, new Permanent(ctx, host, P1).DigivolutionCards.Count, "no source attached");
    AssertEqual(0, ctx.MemoryController.Current.Current, "no memory gained when skipped");
}

// ---- G9 ----------------------------------------------------------------------

async Task G9_PlayFromUnderPredicate()
{
    EngineContext ctx = NewContext();
    ctx.MemoryController.Set(0);
    HeadlessEntityId src = Battle(ctx, "SRC", "Source", cardType: "Digimon"); // the effect's own card
    HeadlessEntityId host = Battle(ctx, "AAA", "HostDigi", cardType: "Digimon");
    HeadlessEntityId under4 = Source(ctx, "U4", level: 4);   // Lv4 Digimon under-card (playable)
    HeadlessEntityId under6 = Source(ctx, "U6", level: 6);   // Lv6 under-card (not <=4)
    SetSources(ctx, host, under4, under6);

    var card = new CardSource(ctx, src, P1);
    var eff = new ActivatedPlayFromUnderEffect(
        card, "[When Digivolving] play 1 of your Digimon's Lv<=4 digivolution cards as a new Digimon, cost-free",
        canTarget: cs => cs.IsDigimon && cs.Level <= 4 && cs.HasLevel && CardEffectCommons.CanPlayAsNewPermanent(cs, payCost: false, cardEffect: null),
        isOptional: true);

    var sink = NewSink(ctx);
    var req = eff.BuildRequest(new[] { P1, P2 });
    Script(ctx, ChoiceResult.Select(under4));
    ChoiceResult result = await ctx.ChoiceProvider.ChooseAsync(req, CancellationToken.None);
    if (!result.IsSkipped) { eff.Apply(sink, result.SelectedIds); }
    await sink.FlushAsync();

    AssertTrue(InZone(ctx, ChoiceZone.BattleArea, under4), "the Lv4 under-card was played as a new battle-area permanent");
    var hostUnder = new Permanent(ctx, host, P1).DigivolutionCards.Select(c => c.InstanceId).ToList();
    AssertFalse(hostUnder.Contains(under4), "the played under-card left the host's stack");
    AssertTrue(hostUnder.Contains(under6), "the over-level under-card is still under the host");
    AssertEqual(0, ctx.MemoryController.Current.Current, "cost-free play (no memory spent)");
}

async Task G9_PredicateFiltersLevel()
{
    EngineContext ctx = NewContext();
    HeadlessEntityId src = Battle(ctx, "SRC", "Source", cardType: "Digimon");
    HeadlessEntityId host = Battle(ctx, "AAA", "HostDigi", cardType: "Digimon");
    HeadlessEntityId under6 = Source(ctx, "U6", level: 6);   // ONLY an over-level under-card
    SetSources(ctx, host, under6);

    var eff = new ActivatedPlayFromUnderEffect(
        new CardSource(ctx, src, P1), "play Lv<=4 under-card",
        canTarget: cs => cs.IsDigimon && cs.Level <= 4 && cs.HasLevel, isOptional: true);

    var req = eff.BuildRequest(new[] { P1, P2 });
    AssertEqual(0, req.Candidates.Count, "no Lv<=4 candidate -> empty request (over-level under-card excluded)");
    await Task.CompletedTask;
}

// ---- G10 ---------------------------------------------------------------------

async Task G10_SelectDeDigivolveDestroy()
{
    EngineContext ctx = NewContext();
    HeadlessEntityId src = Battle(ctx, "SRC", "Source", cardType: "Digimon"); // effect owner (P1)
    HeadlessEntityId t = BattleP2(ctx, "T", "OppTop");                          // opponent Digimon (P2)
    HeadlessEntityId under = UnderP2(ctx, "UND", playCost: 3, dp: null);        // new top after de-digivolve, cost 3
    SetSources(ctx, t, under);

    var card = new CardSource(ctx, src, P1);
    var eff = new SelectDeDigivolveThenConditionalDestroyEffect(
        card,
        canTarget: id => CardEffectCommons.IsOpponentBattleAreaDigimon(card, id),
        count: 1,
        destroyIf: perm => perm.TopCard.HasPlayCost && perm.TopCard.GetCostItself <= 4,
        description: "[Main] de-digivolve 1 opp Digimon, then if new top cost <=4 delete it");

    var sink = NewSink(ctx);
    Script(ctx, ChoiceResult.Select(t));
    await eff.ResolveAsync(sink, CancellationToken.None);
    await sink.FlushAsync();

    AssertFalse(InZoneP(ctx, P2, ChoiceZone.BattleArea, under), "the promoted new top (cost 3) was destroyed");
    AssertTrue(InZoneP(ctx, P2, ChoiceZone.Trash, t), "the old top was trashed by the de-digivolve");
}

async Task G10_SelectDeDigivolveSurvives()
{
    EngineContext ctx = NewContext();
    HeadlessEntityId src = Battle(ctx, "SRC", "Source", cardType: "Digimon");
    HeadlessEntityId t = BattleP2(ctx, "T", "OppTop");
    HeadlessEntityId under = UnderP2(ctx, "UND", playCost: 6, dp: null);        // new top cost 6 (>4)
    SetSources(ctx, t, under);

    var card = new CardSource(ctx, src, P1);
    var eff = new SelectDeDigivolveThenConditionalDestroyEffect(
        card,
        canTarget: id => CardEffectCommons.IsOpponentBattleAreaDigimon(card, id),
        count: 1,
        destroyIf: perm => perm.TopCard.HasPlayCost && perm.TopCard.GetCostItself <= 4,
        description: "de-digivolve then conditional destroy");

    var sink = NewSink(ctx);
    Script(ctx, ChoiceResult.Select(t));
    await eff.ResolveAsync(sink, CancellationToken.None);
    await sink.FlushAsync();

    AssertTrue(InZoneP(ctx, P2, ChoiceZone.BattleArea, under), "new top cost 6 (>4) survives");
}

async Task G10_MassDeDigivolveDestroy()
{
    EngineContext ctx = NewContext();
    HeadlessEntityId src = Battle(ctx, "SRC", "Source", cardType: "Digimon");
    HeadlessEntityId t1 = BattleP2(ctx, "T1", "Opp1");
    HeadlessEntityId u1 = UnderP2(ctx, "U1", playCost: null, dp: 4000);         // new top DP 4000 (<=5000)
    SetSources(ctx, t1, u1);
    HeadlessEntityId t2 = BattleP2(ctx, "T2", "Opp2");
    HeadlessEntityId u2 = UnderP2(ctx, "U2", playCost: null, dp: 6000);         // new top DP 6000 (>5000)
    SetSources(ctx, t2, u2);

    var card = new CardSource(ctx, src, P1);
    var eff = new MassDeDigivolveThenConditionalDestroyEffect(
        card,
        target: id => CardEffectCommons.IsOpponentBattleAreaDigimon(card, id),
        count: 1,
        destroyIf: perm => perm.DP <= 5000,
        description: "[When Digivolving] de-digivolve all opp Digimon, then delete all with DP <=5000");

    var sink = NewSink(ctx);
    await eff.ResolveAsync(sink, CancellationToken.None);
    await sink.FlushAsync();

    AssertFalse(InZoneP(ctx, P2, ChoiceZone.BattleArea, u1), "DP 4000 (<=5000) promoted top destroyed");
    AssertTrue(InZoneP(ctx, P2, ChoiceZone.BattleArea, u2), "DP 6000 (>5000) promoted top survives");
}

// ---- G12 ---------------------------------------------------------------------

async Task G12_ChooseCountTrashAll()
{
    EngineContext ctx = NewContext();
    HeadlessEntityId src = Battle(ctx, "SRC", "Source", cardType: "Digimon");
    HeadlessEntityId t1 = BattleP2(ctx, "T1", "Opp1");
    SetSources(ctx, t1, UnderP2(ctx, "A", null, null), UnderP2(ctx, "B", null, null)); // 2 digivolution cards
    HeadlessEntityId t2 = BattleP2(ctx, "T2", "Opp2");
    SetSources(ctx, t2, UnderP2(ctx, "C", null, null));                                 // 1 digivolution card

    var card = new CardSource(ctx, src, P1);
    var eff = new ChooseCountThenTrashDigivolutionEffect(
        card, target: id => CardEffectCommons.IsOpponentBattleAreaDigimon(card, id), maxCount: 2, fromBottom: true,
        description: "[Main] trash up to 2 digivolution cards from the bottom of all opp Digimon");

    var sink = NewSink(ctx);
    Script(ctx, ChoiceResult.SelectCount(2));
    await eff.ResolveAsync(sink, CancellationToken.None);
    await sink.FlushAsync();

    AssertEqual(0, new Permanent(ctx, t1, P2).DigivolutionCards.Count, "T1 (2 cards) trashed min(2,2)=2 -> 0 left");
    AssertEqual(0, new Permanent(ctx, t2, P2).DigivolutionCards.Count, "T2 (1 card) trashed min(2,1)=1 -> 0 left");
}

async Task G12_ChooseZero()
{
    EngineContext ctx = NewContext();
    HeadlessEntityId src = Battle(ctx, "SRC", "Source", cardType: "Digimon");
    HeadlessEntityId t1 = BattleP2(ctx, "T1", "Opp1");
    SetSources(ctx, t1, UnderP2(ctx, "A", null, null), UnderP2(ctx, "B", null, null));

    var card = new CardSource(ctx, src, P1);
    var eff = new ChooseCountThenTrashDigivolutionEffect(
        card, target: id => CardEffectCommons.IsOpponentBattleAreaDigimon(card, id), maxCount: 2, fromBottom: true, description: "trash");

    var sink = NewSink(ctx);
    Script(ctx, ChoiceResult.SelectCount(0));
    await eff.ResolveAsync(sink, CancellationToken.None);
    await sink.FlushAsync();

    AssertEqual(2, new Permanent(ctx, t1, P2).DigivolutionCards.Count, "count 0 -> nothing trashed");
}

// ---- G13 ---------------------------------------------------------------------

async Task G13_OpponentYes()
{
    EngineContext ctx = NewContext();
    ctx.MemoryController.Set(0);
    HeadlessEntityId src = Battle(ctx, "SRC", "Source", cardType: "Digimon");
    var card = new CardSource(ctx, src, P1);
    var eff = OppBinary(card);

    var sink = NewSink(ctx);
    Script(ctx, ChoiceResult.Select(new HeadlessEntityId($"{src.Value}#mode#0"))); // opponent picks YES
    await eff.ResolveAsync(sink, CancellationToken.None);
    await sink.FlushAsync();

    AssertEqual(10, ctx.MemoryController.Current.Current, "opponent chose yes -> ifYes branch (memory +10)");
}

async Task G13_OpponentNo()
{
    EngineContext ctx = NewContext();
    ctx.MemoryController.Set(0);
    HeadlessEntityId src = Battle(ctx, "SRC", "Source", cardType: "Digimon");
    var card = new CardSource(ctx, src, P1);
    var eff = OppBinary(card);

    var sink = NewSink(ctx);
    Script(ctx, ChoiceResult.Select(new HeadlessEntityId($"{src.Value}#mode#1"))); // opponent picks NO
    await eff.ResolveAsync(sink, CancellationToken.None);
    await sink.FlushAsync();

    AssertEqual(1, ctx.MemoryController.Current.Current, "opponent chose no -> ifNo branch (memory +1)");
}

async Task G13_AutoNo()
{
    EngineContext ctx = NewContext();
    ctx.MemoryController.Set(0);
    HeadlessEntityId src = Battle(ctx, "SRC", "Source", cardType: "Digimon");
    var card = new CardSource(ctx, src, P1);
    // autoNoWhen true -> no prompt, ifNo runs directly.
    var eff = new OpponentBinaryChoiceEffect(
        card, "opp binary", "Discard", "Not Discard",
        ifYes: s => AddMemory(s, src, 10), ifNo: s => AddMemory(s, src, 1), autoNoWhen: () => true);

    var sink = NewSink(ctx);
    // No choice enqueued: if a prompt were issued the scripted fallback would fire; auto-no avoids it entirely.
    await eff.ResolveAsync(sink, CancellationToken.None);
    await sink.FlushAsync();

    AssertEqual(1, ctx.MemoryController.Current.Current, "auto-no -> ifNo runs with no prompt (memory +1)");
}

OpponentBinaryChoiceEffect OppBinary(CardSource card) =>
    new OpponentBinaryChoiceEffect(
        card, "[Main] opponent may discard their top security", "Discard", "Not Discard",
        ifYes: s => AddMemory(s, card.InstanceId, 10),
        ifNo: s => AddMemory(s, card.InstanceId, 1),
        autoNoWhen: null);

static void AddMemory(MatchStateMutationSink sink, HeadlessEntityId source, int amount) =>
    sink.Apply(new EffectMutation(
        MatchStateMutationSink.AddMemoryKind, source,
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.AmountKey] = amount }));

// ---- Harness -----------------------------------------------------------------

EngineContext NewContext()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 9);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    return ctx;
}

MatchStateMutationSink NewSink(EngineContext ctx) =>
    new MatchStateMutationSink(ctx.CardInstanceRepository, ctx.LogSink, ctx.ZoneMover, ctx.MemoryController, ctx.EffectRegistry, ctx.GameEventQueue, context: ctx);

void Script(EngineContext ctx, params ChoiceResult[] choices)
{
    var p = (ScriptedChoiceProvider)ctx.ChoiceProvider;
    p.Clear();
    foreach (var c in choices) { p.Enqueue(c); }
}

HeadlessEntityId Battle(EngineContext ctx, string number, string name, string cardType)
{
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal);
    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(new HeadlessEntityId(number), number, name, meta, CardType: cardType));
    var id = new HeadlessEntityId($"p1:{number}:battle");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId(number), P1));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
    return id;
}

HeadlessEntityId Hand(EngineContext ctx, string number, string name, string cardType)
{
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal);
    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(new HeadlessEntityId(number), number, name, meta, CardType: cardType));
    var id = new HeadlessEntityId($"p1:{number}:hand");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId(number), P1));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.Hand)).GetAwaiter().GetResult();
    return id;
}

HeadlessEntityId Source(EngineContext ctx, string number, int level)
{
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["level"] = level };
    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(new HeadlessEntityId(number), number, number, meta, CardType: "Digimon"));
    var id = new HeadlessEntityId($"p1:{number}:src");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId(number), P1));
    return id;
}

void SetSources(EngineContext ctx, HeadlessEntityId host, params HeadlessEntityId[] sources)
{
    ctx.CardInstanceRepository.TryGetInstance(host, out CardInstanceRecord? rec);
    var meta = new Dictionary<string, object?>(rec!.Metadata, StringComparer.Ordinal)
    {
        [DigivolutionStackReader.SourceIdsKey] = sources.ToArray(),
    };
    ctx.CardInstanceRepository.Upsert(rec with { Metadata = meta });
}

HeadlessEntityId BattleP2(EngineContext ctx, string number, string name)
{
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal);
    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(new HeadlessEntityId(number), number, name, meta, CardType: "Digimon"));
    var id = new HeadlessEntityId($"p2:{number}:battle");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId(number), P2));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
    return id;
}

// An off-field P2 card that will be promoted to top on de-digivolve. playCost -> def (GetCostItself); dp -> instance (Permanent.DP).
HeadlessEntityId UnderP2(EngineContext ctx, string number, int? playCost, int? dp)
{
    var defMeta = new Dictionary<string, object?>(StringComparer.Ordinal);
    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(new HeadlessEntityId(number), number, number, defMeta, CardType: "Digimon", PlayCost: playCost));
    var instMeta = new Dictionary<string, object?>(StringComparer.Ordinal);
    if (dp is int d) { instMeta["dp"] = d; }
    var id = new HeadlessEntityId($"p2:{number}:src");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId(number), P2, Metadata: instMeta));
    return id;
}

bool InZone(EngineContext ctx, ChoiceZone zone, HeadlessEntityId id) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(P1, zone).Contains(id);

bool InZoneP(EngineContext ctx, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId id) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(player, zone).Contains(id);

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertFalse(bool v, string label) { if (v) throw new InvalidOperationException($"{label}: expected false."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected {expected}, got {actual}.");
}
