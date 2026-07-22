using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

// CV-A1: the EffectDuration system.
// (③-B) The invented EffectRegistry duration-SWEEP surface (EffectDurationExpiry.ExpireTurnEnd / ExpireBattleEnd /
// ExpireAttackEnd / ExpireUnsuspend, sweeping registry EffectBinding.Duration) is RETIRED — the continuous-binding
// producer reached 0, so every sweep was a dead write against a permanently-empty store. The EffectDuration ENUM
// survives as the BUCKET key (AddEffectToPermanent / AddEffectToPlayer switch on it); duration expiry is now the
// AS-IS bucket reset at each choke (HeadlessEndTurnCleanupFlow / BattleResolver UntilEndBattle / AttackProcess
// UntilEndAttack / TSM:256-259). Per-duration expiry behaviour is witnessed live in J2-UnsuspendRevival (unsuspend
// blocks), G9-073 / G3.5-B2 (turn-end resets), BT1.StopRemainder (UntilEachTurnEnd player bucket).
//
// This suite now witnesses (1) the enum still carries the 8 durations, and (2) an UntilOpponentTurnEnd bucket grant
// resets through the REAL HeadlessEndTurnCleanupFlow (the AS-IS bucket-reset that replaces the retired registry sweep).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("EffectDuration enum has the 8 original durations", () => Pure(EnumHasEightDurations)),
    ("UntilOpponentTurnEnd bucket grant resets through the REAL HeadlessEndTurnCleanupFlow (registry sweep retired)", BucketResetExpires),
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

void EnumHasEightDurations()
{
    AssertEqual(8, Enum.GetValues<EffectDuration>().Length, "duration count");
    foreach (string name in new[] { "UntilEachTurnEnd", "UntilOwnerTurnEnd", "UntilOpponentTurnEnd",
        "UntilEndAttack", "UntilEndBattle", "UntilOwnerActivePhase", "UntilNextUntap", "UntilCalculateFixedCost" })
    {
        AssertTrue(Enum.IsDefined(typeof(EffectDuration), name) || Enum.GetNames<EffectDuration>().Contains(name), $"has {name}");
    }
}

async Task BucketResetExpires()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 5);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.TurnController.SetPhase(HeadlessPhase.Main);
    using AmbientMatchContext.Scope _scope = AmbientMatchContext.Enter(ctx);

    HeadlessEntityId src = await Put(ctx, P1, "SRC", ChoiceZone.BattleArea);
    HeadlessEntityId target = await Put(ctx, P1, "TGT", ChoiceZone.BattleArea);

    // Grant any ICardEffect into the target's UntilOpponentTurnEnd BUCKET (AddEffectToPermanent; src/target both P1
    // -> IsOwnerPermanent -> UntilOpponentTurnEndEffects). Witness the duration carrier directly (the store the
    // AS-IS reset clears), so the assertion does not depend on any downstream continuous-scan reader.
    ICardEffect blocker = CardEffectFactory.BlockerSelfStaticEffect(false, V(ctx, src), null);
    CardEffectCommons.AddEffectToPermanent(
        Perm(ctx, target), EffectDuration.UntilOpponentTurnEnd, V(ctx, src), blocker, EffectTiming.None);
    AssertEqual(1, Perm(ctx, target).UntilOpponentTurnEndEffects.Count, "grant lands in the UntilOpponentTurnEnd bucket");

    // End the OPPONENT (P2)'s turn through the REAL cleanup flow — nonTurnPlayer = P1 drops UntilOpponentTurnEndEffects.
    new HeadlessEndTurnCleanupFlow().Cleanup(ctx, new HeadlessTurnState(
        TurnNumber: 2, TurnPlayerId: P2, NonTurnPlayerId: P1,
        Phase: HeadlessPhase.End, StepCursor: TurnStepCursor.PhaseStart, IsFirstTurn: false, PlayerOrder: new[] { P1, P2 }));
    AssertEqual(0, Perm(ctx, target).UntilOpponentTurnEndEffects.Count,
        "bucket reset by HeadlessEndTurnCleanupFlow (AS-IS bucket expiry replaces the retired registry sweep)");
}

// --- Helpers -------------------------------------------------------------

async Task<HeadlessEntityId> Put(EngineContext ctx, HeadlessPlayerId owner, string tag, ChoiceZone zone)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 5000, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 5000 }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone));
    return id;
}

CardSource V(EngineContext ctx, HeadlessEntityId id) => new(ctx, id, OwnerOf(ctx, id), OwnerOf(ctx, id));
Permanent Perm(EngineContext ctx, HeadlessEntityId id) => new(ctx, id, OwnerOf(ctx, id));
HeadlessPlayerId OwnerOf(EngineContext ctx, HeadlessEntityId id) =>
    ctx.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? r) && r is not null ? r.OwnerId : default;

static Task Pure(Action body) { body(); return Task.CompletedTask; }

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
