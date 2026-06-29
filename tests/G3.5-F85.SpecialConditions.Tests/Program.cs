using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// F-8.5: special condition predicates (SpecialConditionHelpers) + their producers — FusionDigivolve tags
// WhenDigivolving with isJogress/isDigiXros; DpZeroDeletionHelpers stamps DPZero on DP<=0 deletions;
// IsTopCardInTrash reads the trash order.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Predicates read context flags (IsJogress / IsDigiXros / IsDpZeroDelete)", () => Pure(PredicatesReadFlags)),
    ("DNA fusion tags WhenDigivolving with isJogress", DnaFusionTagsJogress),
    ("DigiXros fusion tags WhenDigivolving with isDigiXros", XrosFusionTagsDigiXros),
    ("DP-zero sweep deletes a DP<=0 Digimon and stamps DPZero", DpZeroSweep),
    ("Healthy Digimon survives the DP-zero sweep", DpZeroSweepKeepsHealthy),
    ("IsTopCardInTrash identifies the most recently trashed card", () => Pure(TopOfTrash)),
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

static Task Pure(Action body) { body(); return Task.CompletedTask; }

// --- Tests ---------------------------------------------------------------

void PredicatesReadFlags()
{
    var jogress = new Dictionary<string, object?>(StringComparer.Ordinal) { [SpecialConditionHelpers.IsJogressKey] = true };
    var xros = new Dictionary<string, object?>(StringComparer.Ordinal) { [SpecialConditionHelpers.IsDigiXrosKey] = true };
    var dpzero = new Dictionary<string, object?>(StringComparer.Ordinal) { [SpecialConditionHelpers.DpZeroKey] = true };
    var empty = new Dictionary<string, object?>(StringComparer.Ordinal);

    AssertTrue(SpecialConditionHelpers.IsJogress(jogress), "isJogress true");
    AssertFalse(SpecialConditionHelpers.IsJogress(empty), "isJogress false by default");
    AssertTrue(SpecialConditionHelpers.IsDigiXros(xros), "isDigiXros true");
    AssertTrue(SpecialConditionHelpers.IsDpZeroDelete(dpzero), "DPZero true");
    AssertFalse(SpecialConditionHelpers.IsDpZeroDelete(empty), "DPZero false by default");
}

async Task DnaFusionTagsJogress()
{
    EngineContext context = await FusionBoard();
    await FusionDigivolveHelpers.FuseAsync(context.CardInstanceRepository, context.ZoneMover,
        new("p1:hand:DNA"), ChoiceZone.Hand, new HeadlessEntityId[] { new("p1:main:A"), new("p1:main:B") },
        ChoiceZone.BattleArea, context.GameEventQueue, kind: FusionKind.DnaDigivolve);

    GameEvent? evt = FindWhenDigivolving(context);
    AssertTrue(evt is not null, "WhenDigivolving emitted");
    AssertTrue(SpecialConditionHelpers.IsJogress(evt!.Metadata), "event tagged isJogress");
    AssertFalse(SpecialConditionHelpers.IsDigiXros(evt.Metadata), "not tagged isDigiXros");
}

async Task XrosFusionTagsDigiXros()
{
    EngineContext context = await FusionBoard();
    await FusionDigivolveHelpers.FuseAsync(context.CardInstanceRepository, context.ZoneMover,
        new("p1:hand:DNA"), ChoiceZone.Hand, new HeadlessEntityId[] { new("p1:main:A"), new("p1:main:B") },
        ChoiceZone.BattleArea, context.GameEventQueue, kind: FusionKind.DigiXros);

    GameEvent? evt = FindWhenDigivolving(context);
    AssertTrue(evt is not null && SpecialConditionHelpers.IsDigiXros(evt!.Metadata), "event tagged isDigiXros");
}

async Task DpZeroSweep()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 33);
    HeadlessEntityId weak = new("p1:main:WEAK");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(weak, new HeadlessEntityId("W"), P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 2000 }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, weak, ChoiceZone.None, ChoiceZone.BattleArea));
    // Continuous -3000 DP makes resolved DP (2000-3000) <= 0.
    RegisterDp(context, weak, -3000);

    IReadOnlyList<HeadlessEntityId> deleted = await DpZeroDeletionHelpers.SweepAsync(context, new[] { P1, P2 });

    AssertTrue(deleted.Contains(weak), "DP<=0 Digimon deleted");
    AssertTrue(InZone(context, P1, ChoiceZone.Trash, weak), "moved to trash");
    CardInstanceRecord r = Instance(context, weak);
    AssertTrue(SpecialConditionHelpers.IsDpZeroDelete(r.Metadata), "DPZero stamped on the deleted card");
}

async Task DpZeroSweepKeepsHealthy()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 34);
    HeadlessEntityId healthy = new("p1:main:OK");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(healthy, new HeadlessEntityId("OK"), P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 5000 }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, healthy, ChoiceZone.None, ChoiceZone.BattleArea));

    IReadOnlyList<HeadlessEntityId> deleted = await DpZeroDeletionHelpers.SweepAsync(context, new[] { P1, P2 });

    AssertFalse(deleted.Contains(healthy), "healthy Digimon not deleted");
    AssertTrue(InZone(context, P1, ChoiceZone.BattleArea, healthy), "stays on the battle area");
}

void TopOfTrash()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 35);
    HeadlessEntityId a = new("t:A");
    HeadlessEntityId b = new("t:B");
    Place(context, a, ChoiceZone.Trash);
    Place(context, b, ChoiceZone.Trash); // b trashed last => top
    var zones = (IZoneStateReader)context.ZoneMover;
    AssertTrue(SpecialConditionHelpers.IsTopCardInTrash(zones, P1, b), "b is the top of trash");
    AssertFalse(SpecialConditionHelpers.IsTopCardInTrash(zones, P1, a), "a is not the top");
}

// --- Helpers -------------------------------------------------------------

async Task<EngineContext> FusionBoard()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 36);
    foreach (var (id, zone) in new[] { ("p1:hand:DNA", ChoiceZone.Hand), ("p1:main:A", ChoiceZone.BattleArea), ("p1:main:B", ChoiceZone.BattleArea) })
    {
        context.CardInstanceRepository.Upsert(new CardInstanceRecord(new(id), new HeadlessEntityId(id), P1));
        await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, new(id), ChoiceZone.None, zone));
    }

    context.GameEventQueue.DrainPending();
    return context;
}

void Place(EngineContext context, HeadlessEntityId id, ChoiceZone zone)
{
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId(id.Value), P1));
    context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, zone)).GetAwaiter().GetResult();
}

void RegisterDp(EngineContext context, HeadlessEntityId target, int delta)
{
    var values = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dpDelta"] = delta };
    var effectContext = new EffectContext(P1, P1, new HeadlessEntityId("dp-src"),
        triggerEntityId: null, targetEntityIds: new[] { target }, values: values);
    context.EffectRegistry.Register(new EffectBinding(
        new EffectRequest(new HeadlessEntityId($"dp:{target.Value}"), P1, "Continuous", effectContext),
        keywords: null, EffectQueryRole.Continuous, new[] { ContinuousDpGate.Scope }));
}

GameEvent? FindWhenDigivolving(EngineContext context) =>
    context.GameEventQueue.DrainPending().FirstOrDefault(e => TriggerTimingMap.Derive(e).Contains(TriggerTimings.WhenDigivolving));

CardInstanceRecord Instance(EngineContext context, HeadlessEntityId id) =>
    context.CardInstanceRepository.TryGetInstance(id, out var r) && r is not null ? r : throw new InvalidOperationException($"missing {id}");

bool InZone(EngineContext context, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(player, zone).Contains(cardId);

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertFalse(bool v, string label) { if (v) throw new InvalidOperationException($"{label}: expected false."); }
