using HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST3.Yellow;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G12-003: ST3_01/ST3_04 fire LIVE on a cross-card deletion. An opponent Digimon dropping to 0 DP is swept
// (DPZero), the deletion drives OnDestroyedAnyone, and — now that "Anyone" timings broadcast the subject to
// cross-card listeners — the ST3_01/04 effect bound to a DIFFERENT (own) card resolves through the real
// GameFlowProcessor, gated on opponent-Digimon + 0-DP-delete.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("ST3_01: opponent 0-DP delete -> this Digimon +1000 DP (live cross-card)", ST3_01_Live),
    ("ST3_01: OWN Digimon 0-DP delete -> no buff (gate: opponent only)", ST3_01_OwnNoFire),
    ("ST3_04: opponent 0-DP delete -> gain 1 memory (live cross-card)", ST3_04_Live),
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

// ST3_01/ST3_04 carry INHERITED (下段/継承) effects (SetIsInheritedEffect(true) AS-IS-verbatim), which the
// membership split (Permanent.EffectList_ForCard) surfaces only when the card is a NON-flipped digivolution
// SOURCE under a Digimon host — "this Digimon" then resolves to the HOST (PermanentOfThisCard). So the witness
// is tucked as a source under host X (the InheritScan geometry), and the buff/memory reads off the host.
async Task ST3_01_Live()
{
    EngineContext context = Context();
    var host = await PlaceHostWithInheritedSource(context, P1, "X", "ST3_01", hostDp: 4000);
    await PlaceDigimon(context, P2, "FOE", dp: 0);                // opponent at 0 DP -> swept by the live pipeline

    await new GameFlowProcessor().RunToStableAsync(context);

    AssertEqual(5000, new HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.Permanent(context, host).DP, "host +1000 from the opponent's 0-DP delete (inherited cross-card reactor)");
}

async Task ST3_01_OwnNoFire()
{
    EngineContext context = Context();
    var host = await PlaceHostWithInheritedSource(context, P1, "X", "ST3_01", hostDp: 4000);
    await PlaceDigimon(context, P1, "MINE", dp: 0);              // OWN Digimon at 0 DP -> swept, but not opponent

    await new GameFlowProcessor().RunToStableAsync(context);

    AssertEqual(4000, new HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.Permanent(context, host).DP, "own deletion does not trigger (no buff)");
}

async Task ST3_04_Live()
{
    EngineContext context = Context();
    await PlaceHostWithInheritedSource(context, P1, "X", "ST3_04", hostDp: 4000);
    await PlaceDigimon(context, P2, "FOE", dp: 0);
    context.MemoryController.Set(0);

    await new GameFlowProcessor().RunToStableAsync(context);

    AssertEqual(1, context.MemoryController.Current.Current, "gained 1 memory from the opponent's 0-DP delete (inherited cross-card reactor)");
}

// --- Helpers -------------------------------------------------------------

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 1203);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    context.TurnController.SetPhase(HeadlessPhase.Main); // (harness triage) DoneStartGame gate: new-model CanTrigger needs a live phase
    return context;
}

async Task<HeadlessEntityId> PlaceDigimon(EngineContext context, HeadlessPlayerId owner, string tag, int dp)
{
    CardDatabase cards = (CardDatabase)context.CardRepository;
    var def = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(def, def.Value, tag, new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, owner, Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

// Place host X (a plain Digimon top permanent) with an inherited-effect witness tucked underneath as a
// non-flipped digivolution source (dispatched by class-name = card number, so its inherited CardEffects fold
// into the host's EffectList via EffectList_ForCard). Returns the HOST id.
async Task<HeadlessEntityId> PlaceHostWithInheritedSource(
    EngineContext context, HeadlessPlayerId owner, string hostTag, string sourceCardNumber, int hostDp)
{
    CardDatabase cards = (CardDatabase)context.CardRepository;

    var srcDef = new HeadlessEntityId($"DEF:{owner.Value}:{sourceCardNumber}src");
    cards.Upsert(new CardRecord(srcDef, sourceCardNumber, $"{sourceCardNumber}src",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 2000 }, CardType: "Digimon"));
    var source = new HeadlessEntityId($"{owner.Value}:src:{sourceCardNumber}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(source, srcDef, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)));

    var hostDef = new HeadlessEntityId($"DEF:{owner.Value}:{hostTag}");
    cards.Upsert(new CardRecord(hostDef, hostTag, hostTag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = hostDp }, CardType: "Digimon"));
    var host = new HeadlessEntityId($"{owner.Value}:battle:{hostTag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(host, hostDef, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["dp"] = hostDp,
            ["sourceIds"] = new[] { source.Value },
        }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, host, ChoiceZone.None, ChoiceZone.BattleArea));
    return host;
}

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
