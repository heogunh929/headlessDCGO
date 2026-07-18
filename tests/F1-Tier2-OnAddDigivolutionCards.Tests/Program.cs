// F1-Tier2: OnAddDigivolutionCards activated-bridge expansion (third F-1 Tier2 unit — the emit-INVERSION fix).
//
// AS-IS fires OnAddDigivolutionCards ONLY when an EFFECT places >=1 card under a permanent
// (Permanent.AddDigivolutionCardsTop/Bottom); NATURAL digivolution does NOT (plain AddCardSource). Payload
// {Permanent=receiving host, CardEffect=cause (mandatory != null), CardSources=the added batch, ...}. Self=38 /
// owner-anyone=10 / opponent=2 by permanentCondition. Batch = ONE emit per place-under (list payload), not per-card.
//
// The headless emit was INVERTED (fired on natural digivolve, silent on effect place-under). Fixed by moving the emit
// from DigivolveAction into DigivolutionStackHelpers (the AS-IS AddDigivolutionCards* port), hardening the gate to
// require a non-empty cause, and registering OnAddDigivolutionCards in ActivatedBridgeTimings.EventBroadcast. These
// tests drive the effect place-under path directly (DigivolutionStackHelpers.AddSourcesBottomAsync + cause) and drain.
//
// Witnesses (REAL cards): BT22_044 (top self, [CS]-trait, +1 memory) and EX6_001 (INHERITED self, [Legend-Arms],
// +1 memory) — both memory reactors (the "memory latent gap"), ported as uniform ActivatedEffect + MemoryBody.
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);   // controls the receiving host / witness
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    // --- BT22_044 (top self, CS trait, memory) ---
    ("BT22_044 effect place-under fires: an effect placing a [CS] Digimon under this Digimon gains 1 memory", Bt22044Fires),
    ("BT22_044 non-CS no-fire: placing a NON-[CS] card under this Digimon does NOT gain memory (cardCondition)", Bt22044NonCsNoFire),
    ("BT22_044 no-cause no-fire: a place-under with NO causing effect does NOT fire (AS-IS CardEffect != null)", Bt22044NoCauseNoFire),
    ("BT22_044 non-host no-fire: an effect placing a [CS] card under a DIFFERENT permanent does NOT fire this one", Bt22044NonHostNoFire),
    ("BT22_044 once-per-turn cap: a SECOND [CS] place-under the same turn does NOT re-gain (OPT cap)", Bt22044OncePerTurnCap),
    ("BT22_044 batch any-match: ONE place-under of 2 cards (only 1 is [CS]) fires OnAddDigivolutionCards ONCE (+1)", Bt22044BatchAnyMatchFiresOnce),
    // --- EX6_001 (inherited self, Legend-Arms) ---
    ("EX6_001 inherited fires: an inherited reactor under the host gains memory when a [Legend-Arms] card is placed under", Ex6001InheritedFires),
    ("EX6_001 top-permanent no-fire: EX6_001 as a TOP permanent does NOT fire its INHERITED effect (membership)", Ex6001TopPermanentNoFire),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}\n{ex}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

async Task Bt22044Fires()
{
    EngineContext ctx = Setup(seed: 100);
    ctx.MemoryController.Set(0);
    var host = await Place(ctx, P1, "BT22_044", "HOST", "Digimon", traits: null);
    var cs = await Source(ctx, P1, "CS", traits: new[] { "CS" });   // a [CS] Digimon source in trash

    await PlaceUnder(ctx, host, cs, cause: host);

    AssertEqual(1, ctx.MemoryController.Current.Current,
        "an effect placing a [CS] Digimon under BT22_044 gained exactly +1 memory");
}

async Task Bt22044NonCsNoFire()
{
    EngineContext ctx = Setup(seed: 101);
    ctx.MemoryController.Set(0);
    var host = await Place(ctx, P1, "BT22_044", "HOST", "Digimon", traits: null);
    var plain = await Source(ctx, P1, "PLAIN", traits: null);       // no [CS] trait

    await PlaceUnder(ctx, host, plain, cause: host);

    AssertEqual(0, ctx.MemoryController.Current.Current,
        "placing a non-[CS] card under BT22_044 did NOT gain memory (cardCondition rejected it)");
}

async Task Bt22044NoCauseNoFire()
{
    EngineContext ctx = Setup(seed: 102);
    ctx.MemoryController.Set(0);
    var host = await Place(ctx, P1, "BT22_044", "HOST", "Digimon", traits: null);
    var cs = await Source(ctx, P1, "CS", traits: new[] { "CS" });

    // Place-under WITH a queue but NO cause (causeSourceId default/empty) — the event IS emitted, but the gate's
    // mandatory-cause check (AS-IS OnAddDigivolutionCards.cs:24 `CardEffect != null`) rejects it. This targets the
    // GATE (not just the no-queue emit guard): removing the gate's cause-required branch makes this test fire (+1).
    await DigivolutionStackHelpers.AddSourcesBottomAsync(
        ctx.CardInstanceRepository, ctx.ZoneMover, host, new[] { cs }, ChoiceZone.Trash,
        gameEventQueue: ctx.GameEventQueue);   // causeSourceId defaults to empty → gate rejects
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertEqual(0, ctx.MemoryController.Current.Current,
        "a cause-less place-under did NOT fire OnAddDigivolutionCards (gate requires a non-empty cause = AS-IS CardEffect != null)");
}

async Task Bt22044NonHostNoFire()
{
    EngineContext ctx = Setup(seed: 103);
    ctx.MemoryController.Set(0);
    var host = await Place(ctx, P1, "BT22_044", "HOST", "Digimon", traits: null);       // the reactor
    var other = await Place(ctx, P1, "OTHER", "OTHER", "Digimon", traits: null);        // receives the source
    var cs = await Source(ctx, P1, "CS", traits: new[] { "CS" });

    await PlaceUnder(ctx, other, cs, cause: other);   // placed under a DIFFERENT permanent

    AssertEqual(0, ctx.MemoryController.Current.Current,
        "BT22_044 did NOT fire when the [CS] card went under a different permanent (host self-gate)");
}

async Task Bt22044OncePerTurnCap()
{
    EngineContext ctx = Setup(seed: 104);
    ctx.MemoryController.Set(0);
    var host = await Place(ctx, P1, "BT22_044", "HOST", "Digimon", traits: null);
    var cs1 = await Source(ctx, P1, "CS1", traits: new[] { "CS" });
    var cs2 = await Source(ctx, P1, "CS2", traits: new[] { "CS" });

    await PlaceUnder(ctx, host, cs1, cause: host);
    AssertEqual(1, ctx.MemoryController.Current.Current, "first [CS] place-under gained +1");

    await PlaceUnder(ctx, host, cs2, cause: host);
    AssertEqual(1, ctx.MemoryController.Current.Current,
        "second [CS] place-under the same turn did NOT re-gain (Once Per Turn cap) — still +1");
}

async Task Bt22044BatchAnyMatchFiresOnce()
{
    EngineContext ctx = Setup(seed: 107);
    ctx.MemoryController.Set(0);
    var host = await Place(ctx, P1, "BT22_044", "HOST", "Digimon", traits: null);
    var cs = await Source(ctx, P1, "CS", traits: new[] { "CS" });
    var plain = await Source(ctx, P1, "PLAIN", traits: null);

    // ONE place-under of TWO cards (a CS one + a non-CS one) — AS-IS fires ONE OnAddDigivolutionCards with the whole
    // CardSources list; the gate any-matches (a [CS] card is present), so BT22_044 fires exactly ONCE (+1), not +2
    // (per-card) and not +0 (the non-CS card must not veto the any-match).
    await DigivolutionStackHelpers.AddSourcesBottomAsync(
        ctx.CardInstanceRepository, ctx.ZoneMover, host, new[] { cs, plain }, ChoiceZone.Trash,
        gameEventQueue: ctx.GameEventQueue, causeSourceId: host);
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertEqual(1, ctx.MemoryController.Current.Current,
        "one place-under of a 2-card batch (1 [CS] any-match) fired OnAddDigivolutionCards once (+1)");
}

async Task Ex6001InheritedFires()
{
    EngineContext ctx = Setup(seed: 105);
    ctx.MemoryController.Set(0);
    var (host, _) = await Tuck(ctx, srcNumber: "EX6_001", hostDispatch: "HOSTA", hostIsDigimon: true);
    var la = await Source(ctx, P1, "LA", traits: new[] { "Legend-Arms" });

    await PlaceUnder(ctx, host, la, cause: host);

    AssertEqual(1, ctx.MemoryController.Current.Current,
        "the inherited EX6_001 (under the host) gained +1 memory when a [Legend-Arms] card was placed under");
}

async Task Ex6001TopPermanentNoFire()
{
    EngineContext ctx = Setup(seed: 106);
    ctx.MemoryController.Set(0);
    var host = await Place(ctx, P1, "EX6_001", "EX6TOP", "Digimon", traits: null);   // EX6_001 as its OWN top permanent
    var la = await Source(ctx, P1, "LA", traits: new[] { "Legend-Arms" });

    await PlaceUnder(ctx, host, la, cause: host);

    AssertEqual(0, ctx.MemoryController.Current.Current,
        "EX6_001 as a TOP permanent did NOT fire its inherited OnAddDigivolutionCards (membership)");
}

// --- Helpers -------------------------------------------------------------

EngineContext Setup(int seed)
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: seed);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.TurnController.SetPhase(HeadlessPhase.Main); // (harness triage) DoneStartGame gate: new-model CanTrigger needs a live phase (mirrors F1-Tier2-WhenLinked Setup)
    return ctx;
}

// Drive an EFFECT place-under: move `src` (from trash) under `host` with a causing effect source, then drain.
async Task PlaceUnder(EngineContext ctx, HeadlessEntityId host, HeadlessEntityId src, HeadlessEntityId cause)
{
    await DigivolutionStackHelpers.AddSourcesBottomAsync(
        ctx.CardInstanceRepository, ctx.ZoneMover, host, new[] { src }, ChoiceZone.Trash,
        gameEventQueue: ctx.GameEventQueue, causeSourceId: cause);
    await new GameFlowProcessor().RunToStableAsync(ctx);
}

async Task<HeadlessEntityId> Place(EngineContext ctx, HeadlessPlayerId owner, string cardNumber, string tag, string cardType, string[]? traits)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 4 };
    if (traits is not null) meta["traits"] = traits;
    var def = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(def, cardNumber, tag, meta, CardType: cardType));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    var instMeta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 4, ["isSuspended"] = false };
    if (traits is not null) instMeta["traits"] = traits;
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, owner, Metadata: instMeta));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

// A card sitting in the trash (to be placed under a host as a digivolution source), with optional traits.
async Task<HeadlessEntityId> Source(EngineContext ctx, HeadlessPlayerId owner, string tag, string[]? traits)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 2000, ["level"] = 3 };
    if (traits is not null) meta["traits"] = traits;
    var def = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(def, tag, tag, meta, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:trash:{tag}");
    var instMeta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 2000, ["level"] = 3 };
    if (traits is not null) instMeta["traits"] = traits;
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, owner, Metadata: instMeta));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.Trash));
    return id;
}

// A HOST top permanent (generic) with srcNumber tucked underneath as an active digivolution source.
async Task<(HeadlessEntityId Host, HeadlessEntityId Source)> Tuck(
    EngineContext ctx, string srcNumber, string hostDispatch, bool hostIsDigimon)
{
    var cards = (CardDatabase)ctx.CardRepository;

    var srcDef = new HeadlessEntityId($"DEF:{P1.Value}:{srcNumber}src");
    cards.Upsert(new CardRecord(srcDef, srcNumber, $"{srcNumber}src",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 2000, ["level"] = 3 }, CardType: "Digimon"));
    var source = new HeadlessEntityId($"{P1.Value}:src:{srcNumber}src");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(source, srcDef, P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)));

    var hostDef = new HeadlessEntityId($"DEF:{P1.Value}:{hostDispatch}");
    cards.Upsert(new CardRecord(hostDef, hostDispatch, hostDispatch,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 5000, ["level"] = 4 },
        CardType: hostIsDigimon ? "Digimon" : "Tamer"));
    var host = new HeadlessEntityId($"{P1.Value}:battle:{hostDispatch}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(host, hostDef, P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["dp"] = 5000,
            ["level"] = 4,
            ["isSuspended"] = false,
            ["sourceIds"] = new[] { source.Value },
        }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, host, ChoiceZone.None, ChoiceZone.BattleArea));
    return (host, source);
}

void AssertEqual(int expected, int actual, string what)
{
    if (expected != actual) throw new Exception($"{what}: expected {expected}, got {actual}");
}
