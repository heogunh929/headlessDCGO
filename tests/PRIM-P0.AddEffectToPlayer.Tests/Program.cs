// PRIM-P0 B.O.5 / R3-C2b-2 Stage-A witness: CardEffectCommons.AddEffectToPlayer (the AS-IS 4-arg form) now stores
// the effect into the owning Player's Until*Effects bucket (GiveEffect/GiveEffectToPermanentOrPlayer.cs), NOT a
// registry DelayedOneShot binding. Since the R3-C2 window flip, AutoProcessing.GetSkillInfos enumerates
// player.EffectList(timing), so a bucket-stored "[End of Your Turn] lose N memory" ActivateClass FIRES through the
// live window at OnEndTurn, and the per-duration bucket reset (HeadlessEndTurnCleanupFlow, AS-IS
// `player.UntilEachTurnEndEffects = new()`) removes it so it does not re-fire next turn.
//
// The window is driven the way the live engine drives it (W1b harness): GetSkillInfos collects the bucket effect,
// then MultipleSkills.ActivateMultipleSkills resolves it — all under AmbientMatchContext.Enter with the turn in
// Main so DoneStartGame is true (the F4 DoneStartGame-gate finding).
using System.Collections;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using SkillInfo = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.SkillInfo;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
HeadlessEntityId Src = new("1:battle:SRC");

var tests = new (string Name, Func<Task> Body)[]
{
    ("the bucket-stored [End of Turn] lose-2 is collected by the live window and fires (memory 5 -> 3)", FiresThroughLiveWindow),
    ("after the per-duration bucket reset, the reversal is gone and does NOT re-fire next turn end", OneShotNoRefire),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failures.Add(test.Name); Console.Error.WriteLine($"FAIL {test.Name}\n{ex.GetType().Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task FiresThroughLiveWindow()
{
    EngineContext context = Setup();
    using var scope = AmbientMatchContext.Enter(context);

    var card = new CardSource(context, Src, P1, P1);
    StoreLose2Reversal(context, card);
    context.MemoryController.Set(5);

    // Storage proof: the deferred selector is in the owner's OnEndTurn player bucket.
    AssertTrue(new Player(context, P1).EffectList(EffectTiming.OnEndTurn).Count >= 1,
        "the -2 reversal is stored in the player's UntilEachTurnEnd bucket (surfaced by EffectList(OnEndTurn))");

    // Live-window collection + fire.
    AutoProcessing ap = AutoProcessing.For(context);
    List<SkillInfo> skills = AutoProcessing.GetSkillInfos(new Hashtable(), EffectTiming.OnEndTurn);
    AssertTrue(skills.Count >= 1, "the live window (GetSkillInfos) collects the bucket-stored reversal at OnEndTurn");
    await ap.availableMultipleSkills.ActivateMultipleSkills(skills, ap, false, null);

    AssertEqual(3, context.MemoryController.Current.Current, "the -2 fired through the live window (5 -> 3)");
}

async Task OneShotNoRefire()
{
    EngineContext context = Setup();
    using var scope = AmbientMatchContext.Enter(context);

    var card = new CardSource(context, Src, P1, P1);
    StoreLose2Reversal(context, card);
    context.MemoryController.Set(5);

    AutoProcessing ap = AutoProcessing.For(context);
    await ap.availableMultipleSkills.ActivateMultipleSkills(
        AutoProcessing.GetSkillInfos(new Hashtable(), EffectTiming.OnEndTurn), ap, false, null);
    AssertEqual(3, context.MemoryController.Current.Current, "fired on the first turn end (5 -> 3)");

    // AS-IS per-duration bucket reset (HeadlessEndTurnCleanupFlow: player.UntilEachTurnEndEffects = new()).
    new Player(context, P1).UntilEachTurnEndEffects = new();

    // A second OnEndTurn window collects nothing — the one-shot did not re-arm.
    List<SkillInfo> second = AutoProcessing.GetSkillInfos(new Hashtable(), EffectTiming.OnEndTurn);
    AssertEqual(0, second.Count, "after the bucket reset the reversal is gone (no re-fire next turn end)");
    await ap.availableMultipleSkills.ActivateMultipleSkills(second, ap, false, null);
    AssertEqual(3, context.MemoryController.Current.Current, "memory unchanged on the second turn end (still 3)");
}

// --- Harness -------------------------------------------------------------

EngineContext Setup()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 11);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);   // P1 is the turn player (== owner)
    context.TurnController.SetPhase(HeadlessPhase.Main);        // past Setup -> DoneStartGame true
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(Src, new HeadlessEntityId("DEF:SRC"), P1, Metadata: new Dictionary<string, object?>()));
    return context;
}

// AS-IS BT1_090 nested "Memory -2" shape: a new-model ActivateClass whose body loses 2 memory, stored at player
// scope via the (Stage-A) 4-arg AddEffectToPlayer -> UntilEachTurnEnd bucket, selected at OnEndTurn.
void StoreLose2Reversal(EngineContext context, CardSource card)
{
    ActivateClass activateClass = new ActivateClass();
    activateClass.SetUpICardEffect("Memory -2", _ => true, card);
    activateClass.SetUpActivateClass(_ => true, async _ => await card.Owner.AddMemory(-2, activateClass), -1, false, "Lose 2 memory.");
    CardEffectCommons.AddEffectToPlayer(EffectDuration.UntilEachTurnEnd, card, activateClass, EffectTiming.OnEndTurn);
}

static void AssertTrue(bool value, string label) { if (!value) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"{label}: expected '{expected}', actual '{actual}'.");
}
