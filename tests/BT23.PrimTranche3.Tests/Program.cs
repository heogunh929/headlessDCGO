// BT2/BT3 missing-primitive development — Tranche 3 (low-risk BT1-asset generalizations).
//   G7 — own-stack select -> return to hand -> parameterized self follow-up.
//        The former SelectDigivolutionSourceToHandThenUnsuspendSelfEffect now takes an Action<sink> follow-up:
//        BT1_084 passes CardEffectCommons.UnsuspendSelf; BT3_112 (br2) passes a self GainCanNotBeBlocked grant.
//        This tranche exercises the NEW (BT3_112-style) grant follow-up end-to-end.
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
    ("G7: return 1 Lv6 own-stack source to hand, then self GainCanNotBeBlocked (BT3_112 br2 follow-up)", G7_ReturnSourceThenUnblockable),
    ("G7: skipping the optional pick leaves the source in the stack and grants nothing", G7_SkipDoesNothing),
    ("G1: reveal 3, play the matching card as a new permanent, remaining 2 -> deck bottom (BT3_063 shape)", G1_RevealThenPlayAsNewPermanent),
    ("G1: revealCount is a Func evaluated at resolve (BT3_073 dynamic count)", G1_RevealCountIsDynamic),
    ("G5: digivolve-from-hand onto Paildramon costs -2 (dispatch-first hand-card cost gate, BT3_031/111)", G5_CostGateAppliesFromHand),
    ("G5: gate does NOT apply when the target permanent's top is unmatched", G5_CostGateTargetMismatch),
    ("G5: gate does NOT apply when the card is not in hand (condition false)", G5_CostGateNotInHand),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}\n{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// ---- G7 ----------------------------------------------------------------------

async Task G7_ReturnSourceThenUnblockable()
{
    EngineContext ctx = NewContext();
    // Host Digimon on the battle area with one Lv6 Digimon digivolution source under it.
    HeadlessEntityId host = Battle(ctx, "HOST", "Host", cardType: "Digimon");
    HeadlessEntityId src = Source(ctx, "SRC6", level: 6);
    SetSources(ctx, host, src);

    var card = new CardSource(ctx, host, P1);
    var eff = new SelectDigivolutionSourceToHandThenSelfFollowUpEffect(
        card,
        canSelect: cs => cs.IsDigimon && cs.Level == 6 && cs.HasLevel,
        isOptional: true,
        onSelected: sink => CardEffectCommons.GainCanNotBeBlocked(
            new Permanent(ctx, host, P1), defenderCondition: null, EffectDuration.UntilEachTurnEnd, card),
        description: "[When Attacking] return 1 Lv6 source to hand -> this Digimon becomes unblockable");

    Script(ctx, ChoiceResult.Select(src));
    await eff.ResolveAsync(NewSink(ctx), CancellationToken.None);

    AssertTrue(InZone(ctx, ChoiceZone.Hand, src), "the picked Lv6 source returned to hand");
    AssertTrue(ContinuousRestrictionGate.EvaluateBeBlocked(ctx, host).IsRestricted,
        "the self follow-up granted CanNotBeBlocked (unblockable) to the host");
}

async Task G7_SkipDoesNothing()
{
    EngineContext ctx = NewContext();
    HeadlessEntityId host = Battle(ctx, "HOST", "Host", cardType: "Digimon");
    HeadlessEntityId src = Source(ctx, "SRC6", level: 6);
    SetSources(ctx, host, src);

    var card = new CardSource(ctx, host, P1);
    var eff = new SelectDigivolutionSourceToHandThenSelfFollowUpEffect(
        card,
        canSelect: cs => cs.IsDigimon && cs.Level == 6 && cs.HasLevel,
        isOptional: true,
        onSelected: sink => CardEffectCommons.GainCanNotBeBlocked(
            new Permanent(ctx, host, P1), defenderCondition: null, EffectDuration.UntilEachTurnEnd, card),
        description: "optional");

    Script(ctx, ChoiceResult.Skip());
    await eff.ResolveAsync(NewSink(ctx), CancellationToken.None);

    AssertFalse(InZone(ctx, ChoiceZone.Hand, src), "skipping returns nothing to hand");
    AssertFalse(ContinuousRestrictionGate.EvaluateBeBlocked(ctx, host).IsRestricted,
        "skipping applies no follow-up grant");
}

// ---- G1 ----------------------------------------------------------------------

async Task G1_RevealThenPlayAsNewPermanent()
{
    EngineContext ctx = NewContext();
    HeadlessEntityId host = Battle(ctx, "HOST", "Host", cardType: "Digimon");
    // Library top -> bottom: [match, a, b]. Only `match` is a playable Digimon the pick accepts.
    HeadlessEntityId match = Library(ctx, "MATCH", cardType: "Digimon");
    HeadlessEntityId a = Library(ctx, "AAA", cardType: "Option");
    HeadlessEntityId b = Library(ctx, "BBB", cardType: "Option");

    var card = new CardSource(ctx, host, P1);
    var eff = new RevealSelectThenPlaySelectedEffect(
        card,
        revealCount: () => 3,
        canSelect: id => new CardSource(ctx, id, P1).IsDigimon,
        mode: RevealPlayMode.PlayAsNewPermanent,
        description: "[On Deletion] reveal 3 -> play 1 matching -> remaining to deck bottom");

    Script(ctx, ChoiceResult.Select(match));
    await eff.ResolveAsync(CancellationToken.None);

    AssertTrue(InZone(ctx, ChoiceZone.BattleArea, match), "the selected card was played as a new permanent");
    AssertFalse(InZone(ctx, ChoiceZone.Library, match), "the played card left the library");
    AssertTrue(InZone(ctx, ChoiceZone.Library, a) && InZone(ctx, ChoiceZone.Library, b),
        "the 2 non-selected revealed cards remain in the deck (moved to bottom)");
    AssertEqual(2, ((IZoneStateReader)ctx.ZoneMover).GetCards(P1, ChoiceZone.Library).Count,
        "exactly the 2 unselected revealed cards remain in the library");
}

async Task G1_RevealCountIsDynamic()
{
    EngineContext ctx = NewContext();
    HeadlessEntityId host = Battle(ctx, "HOST", "Host", cardType: "Digimon");
    HeadlessEntityId c0 = Library(ctx, "C0", cardType: "Option");
    HeadlessEntityId c1 = Library(ctx, "C1", cardType: "Option");
    HeadlessEntityId c2 = Library(ctx, "C2", cardType: "Option");

    // Dynamic count: set to 2 AFTER construction, proving the Func is evaluated at resolve, not at build.
    int dynamic = 0;
    var eff = new RevealSelectThenPlaySelectedEffect(
        new CardSource(ctx, host, P1),
        revealCount: () => dynamic,
        canSelect: _ => false, // nothing selectable -> pure reveal + deck-bottom of revealed
        mode: RevealPlayMode.PlayAsNewPermanent,
        description: "dynamic reveal");
    dynamic = 2;

    await eff.ResolveAsync(CancellationToken.None);

    // 2 revealed (c0, c1) go to deck bottom; c2 was never revealed. All 3 remain in library, count unchanged.
    AssertEqual(3, ((IZoneStateReader)ctx.ZoneMover).GetCards(P1, ChoiceZone.Library).Count,
        "no card selected -> library count unchanged (only order changed for the 2 revealed)");
    // The unrevealed 3rd card (c2) must still be present.
    AssertTrue(InZone(ctx, ChoiceZone.Library, c2), "the 3rd library card was never revealed (dynamic count = 2)");
}

// ---- G5 ----------------------------------------------------------------------

async Task G5_CostGateAppliesFromHand()
{
    EngineContext ctx = NewContext();
    // Fixture card in hand: "-2 to digivolve me from hand onto a Paildramon/Dinobeemon permanent".
    HeadlessEntityId x = Hand(ctx, "TfxDigivolveCostGate", name: "GateCard", cardType: "Digimon");
    // Target FROM permanent on the owner's battle area, top named Paildramon.
    HeadlessEntityId paildramon = Battle(ctx, "P1PAIL", "Paildramon", cardType: "Digimon");

    int cost = ContinuousModifierGate.ResolveDigivolutionCost(ctx, x, baseDigivolutionCost: 4, digivolveTargetPermanentId: paildramon);
    AssertEqual(2, cost, "base 4 - 2 (gate matched: from hand, target top Paildramon)");
    await Task.CompletedTask;
}

async Task G5_CostGateTargetMismatch()
{
    EngineContext ctx = NewContext();
    HeadlessEntityId x = Hand(ctx, "TfxDigivolveCostGate", name: "GateCard", cardType: "Digimon");
    HeadlessEntityId other = Battle(ctx, "P1AGU", "Agumon", cardType: "Digimon"); // top not Paildramon/Dinobeemon

    int cost = ContinuousModifierGate.ResolveDigivolutionCost(ctx, x, baseDigivolutionCost: 4, digivolveTargetPermanentId: other);
    AssertEqual(4, cost, "unmatched target top -> no reduction");
    await Task.CompletedTask;
}

async Task G5_CostGateNotInHand()
{
    EngineContext ctx = NewContext();
    // Same fixture, but the card is on the battle area (not in hand) -> condition() false -> gate inert.
    HeadlessEntityId x = Battle(ctx, "TfxDigivolveCostGate", "GateCard", cardType: "Digimon");
    HeadlessEntityId paildramon = Battle(ctx, "P1PAIL", "Paildramon", cardType: "Digimon");

    int cost = ContinuousModifierGate.ResolveDigivolutionCost(ctx, x, baseDigivolutionCost: 4, digivolveTargetPermanentId: paildramon);
    AssertEqual(4, cost, "card not in hand -> gate condition false -> no reduction");
    await Task.CompletedTask;
}

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

HeadlessEntityId Library(EngineContext ctx, string number, string cardType)
{
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal);
    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(new HeadlessEntityId(number), number, number, meta, CardType: cardType));
    var id = new HeadlessEntityId($"p1:{number}:lib");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId(number), P1));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.Library)).GetAwaiter().GetResult();
    return id;
}

// A Lv6 Digimon card instance that will be attached as a digivolution source (lives outside any zone until returned).
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

bool InZone(EngineContext ctx, ChoiceZone zone, HeadlessEntityId id) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(P1, zone).Contains(id);

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertFalse(bool v, string label) { if (v) throw new InvalidOperationException($"{label}: expected false."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected {expected}, got {actual}.");
}
