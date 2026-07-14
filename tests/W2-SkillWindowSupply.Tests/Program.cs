using System.Collections;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using PermanentT = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.Permanent;

// (R3-W1b W2) SkillWindowSupply DORMANT converter + A3 minimum-batch feeder tests. Expected values are derived
// from the AS-IS StackSkillInfos inventory (docs/audit/window_supply_correspondence_2026-07-15.md) and the old
// WindowResolver.FilterToMinimumBatch semantics, NOT from the converter's own decisions.

int failures = 0;
void Check(bool cond, string label)
{
    if (!cond) { Console.Error.WriteLine($"FAIL {label}"); failures++; }
    else { Console.WriteLine($"PASS {label}"); }
}

var owner = new HeadlessPlayerId(1);
var attacker = new HeadlessEntityId("attacker");

GameEvent AllyAttackEvent(bool effectDriven = false)
{
    var queue = new GameEventQueue();
    TriggerEventEmitter.Emit(
        queue,
        TriggerTimings.OnAllyAttack,
        actor: owner,
        subject: attacker,
        extraMetadata: effectDriven
            ? new Dictionary<string, object?>(StringComparer.Ordinal) { ["attackCauseEffectId"] = "someEffect" }
            : null);
    return queue.DrainPending().Single();
}

GameEvent DeletionMove(long batchId)
{
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [MatchStateMutationSink.DeletionBatchIdKey] = batchId,
    };
    return new GameEvent(10, GameEventType.CardMoved, "delete", meta)
    {
        Actor = owner,
        Subject = new HeadlessEntityId($"dead:{batchId}"),
        ZoneFrom = ChoiceZone.BattleArea,
        ZoneTo = ChoiceZone.Trash,
    };
}

// --- 1. OnAllyAttack (plain declared attack) is HANDLED: builds OnAttackCheckHashtableOfPermanent
//        (AttackProcess.cs:98-99 → {AttackingPermanent, CardEffect}) with CardEffect=null (TurnStateMachine.cs:
//        1250 passes attackEffect=null). ---
{
    EngineContext context = EngineContext.CreateDefault();
    IReadOnlyList<SkillWindowSupplyEntry> entries = SkillWindowSupply.ConvertEvent(context, AllyAttackEvent());
    SkillWindowSupplyEntry attackEntry = entries.SingleOrDefault(e => e.Timing == EffectTiming.OnAllyAttack);

    Check(entries.Any(e => e.Timing == EffectTiming.OnAllyAttack), "OnAllyAttack is handled (one entry produced)");
    Check(attackEntry.Hashtable is not null
        && attackEntry.Hashtable.ContainsKey("AttackingPermanent")
        && attackEntry.Hashtable.ContainsKey("CardEffect"),
        "hashtable has the AS-IS OnAttackCheckHashtableOfPermanent keys {AttackingPermanent, CardEffect}");
    Check(attackEntry.Hashtable?["CardEffect"] is null,
        "CardEffect is null for a plain declared attack (AS-IS attackEffect=null)");
    Check(attackEntry.Hashtable?["AttackingPermanent"] is PermanentT p && p.InstanceId == attacker,
        "AttackingPermanent is the attacker subject");
    Check(attackEntry.BatchId is null, "an attack entry is batch-less (no cross-batch id)");
}

// --- 2. Effect-driven attack (attackCauseEffectId present) is a GAP (RDW-05): the live ICardEffect is not
//        reconstructable from the id, so the timing is UNHANDLED rather than fabricated. ---
{
    EngineContext context = EngineContext.CreateDefault();
    GameEvent ev = AllyAttackEvent(effectDriven: true);
    Check(SkillWindowSupply.ConvertEvent(context, ev).Count == 0,
        "effect-driven attack produces no entry (RDW-05 GAP, not fabricated)");
    Check(SkillWindowSupply.UnhandledTimings(context, ev).Contains(EffectTiming.OnAllyAttack),
        "OnAllyAttack is reported UNHANDLED for an effect-driven attack");
}

// --- 3. Deletion CardMoved derives OnDestroyedAnyone + OnLeaveFieldAnyone, both UNHANDLED (RDW-01: the
//        OnDeletionHashtable payload needs the pre-removal snapshot + battle/isDPZero/cardEffect). ---
{
    EngineContext context = EngineContext.CreateDefault();
    GameEvent ev = DeletionMove(7);
    IReadOnlyList<EffectTiming> unhandled = SkillWindowSupply.UnhandledTimings(context, ev);
    Check(SkillWindowSupply.ConvertEvent(context, ev).Count == 0, "deletion move produces no handled entry (RDW-01)");
    Check(unhandled.Contains(EffectTiming.OnDestroyedAnyone) && unhandled.Contains(EffectTiming.OnLeaveFieldAnyone),
        "OnDestroyedAnyone + OnLeaveFieldAnyone reported UNHANDLED (RDW-01)");
}

// --- 4. ReadSequencingBatchId: deletion timings read DeletionBatchIdKey; a batch-less timing is null; the
//        sentinel 0 is treated as batch-less (parity with WindowResolverWiring.cs:348 `!= 0` guard). ---
{
    GameEvent ev = DeletionMove(42);
    Check(SkillWindowSupply.ReadSequencingBatchId(ev, EffectTiming.OnDestroyedAnyone) == 42,
        "OnDestroyedAnyone reads the deletion batch id (42)");
    Check(SkillWindowSupply.ReadSequencingBatchId(ev, EffectTiming.OnLeaveFieldAnyone) == 42,
        "OnLeaveFieldAnyone shares the deletion batch id (42)");
    Check(SkillWindowSupply.ReadSequencingBatchId(ev, EffectTiming.OnAllyAttack) is null,
        "a batch-less timing has no sequencing id");
    Check(SkillWindowSupply.ReadSequencingBatchId(DeletionMove(0), EffectTiming.OnDestroyedAnyone) is null,
        "sentinel batch id 0 is treated as batch-less (parity with the != 0 stamp guard)");
}

// --- 5. SequenceByMinimumBatch (A3) — parity with WindowResolver.FilterToMinimumBatch: pass 0 = every
//        batch-less entry + the LOWEST batch; each later pass = the next ascending batch alone. ---
{
    SkillWindowSupplyEntry E(long? batch, string tag) =>
        new(EffectTiming.OnDestroyedAnyone, new Hashtable { ["tag"] = tag }, batch,
            new GameEvent(0, GameEventType.CardMoved, tag, new Dictionary<string, object?>()));

    // batch-less BL, batch 5 (b5a,b5b), batch 3 (b3), batch 9 (b9) — deliberately out of order.
    var entries = new List<SkillWindowSupplyEntry> { E(null, "BL"), E(5, "b5a"), E(3, "b3"), E(9, "b9"), E(5, "b5b") };
    IReadOnlyList<IReadOnlyList<SkillWindowSupplyEntry>> passes = SkillWindowSupply.SequenceByMinimumBatch(entries);

    string[] Tags(IReadOnlyList<SkillWindowSupplyEntry> p) => p.Select(e => (string)e.Hashtable["tag"]!).ToArray();

    Check(passes.Count == 3, $"three passes for batches {{3,5,9}}; got {passes.Count}");
    Check(passes.Count >= 1 && Tags(passes[0]).SequenceEqual(new[] { "BL", "b3" }),
        $"pass 0 = batch-less + lowest batch (BL,b3); got [{string.Join(",", passes.Count >= 1 ? Tags(passes[0]) : Array.Empty<string>())}]");
    Check(passes.Count >= 2 && Tags(passes[1]).SequenceEqual(new[] { "b5a", "b5b" }),
        "pass 1 = next batch (5), stable order within batch");
    Check(passes.Count >= 3 && Tags(passes[2]).SequenceEqual(new[] { "b9" }),
        "pass 2 = highest batch (9)");
}

// --- 6. SequenceByMinimumBatch with only batch-less entries → a single pass (all eligible at once). ---
{
    SkillWindowSupplyEntry E(string tag) =>
        new(EffectTiming.OnAllyAttack, new Hashtable { ["tag"] = tag }, null,
            new GameEvent(0, GameEventType.StateChanged, tag, new Dictionary<string, object?>()));
    IReadOnlyList<IReadOnlyList<SkillWindowSupplyEntry>> passes =
        SkillWindowSupply.SequenceByMinimumBatch(new List<SkillWindowSupplyEntry> { E("x"), E("y") });
    Check(passes.Count == 1 && passes[0].Count == 2, "all-batch-less entries feed in one pass");
    Check(SkillWindowSupply.SequenceByMinimumBatch(Array.Empty<SkillWindowSupplyEntry>()).Count == 0,
        "empty input yields no passes");
}

// --- 7. OnMove (Breeding→BattleArea promotion) is HANDLED (C1 W2 extension): AS-IS inline { "Permanent",
//        permanent } (CardObjectController.cs:1111), reconstructed as a live Permanent view over the subject. ---
{
    EngineContext context = EngineContext.CreateDefault();
    var mover = new HeadlessEntityId("mover");
    GameEvent ev = new GameEvent(11, GameEventType.CardMoved, "move", new Dictionary<string, object?>())
    {
        Actor = owner,
        Subject = mover,
        ZoneFrom = ChoiceZone.BreedingArea,
        ZoneTo = ChoiceZone.BattleArea,
    };
    SkillWindowSupplyEntry moveEntry = SkillWindowSupply.ConvertEvent(context, ev)
        .SingleOrDefault(e => e.Timing == EffectTiming.OnMove);

    Check(moveEntry.Hashtable is not null, "OnMove is handled (one entry produced)");
    Check(moveEntry.Hashtable is { Count: 1 } && moveEntry.Hashtable.ContainsKey("Permanent"),
        "OnMove hashtable has exactly the AS-IS key { \"Permanent\" }");
    Check(moveEntry.Hashtable?["Permanent"] is PermanentT p && p.InstanceId == mover,
        "OnMove Permanent is the promoted subject");
    Check(moveEntry.BatchId is null, "OnMove is batch-less");
    Check(!SkillWindowSupply.UnhandledTimings(context, ev).Contains(EffectTiming.OnMove),
        "OnMove is not reported UNHANDLED");
}

// --- 8. OnReturnCardsToLibraryFromTrash (Trash→Library) is HANDLED (C1 W2 extension): AS-IS inline
//        { "CardSources", cardSources } (CardObjectController.cs:800/882). One CardMoved per card → the list
//        carries this event's single returned card (N→1 window collapse is A3/loop territory). ---
{
    EngineContext context = EngineContext.CreateDefault();
    var returned = new HeadlessEntityId("returned");
    GameEvent ev = new GameEvent(12, GameEventType.CardMoved, "returnlib", new Dictionary<string, object?>())
    {
        Actor = owner,
        Subject = returned,
        ZoneFrom = ChoiceZone.Trash,
        ZoneTo = ChoiceZone.Library,
    };
    SkillWindowSupplyEntry retEntry = SkillWindowSupply.ConvertEvent(context, ev)
        .SingleOrDefault(e => e.Timing == EffectTiming.OnReturnCardsToLibraryFromTrash);

    Check(retEntry.Hashtable is not null, "OnReturnCardsToLibraryFromTrash is handled (one entry produced)");
    Check(retEntry.Hashtable is { Count: 1 } && retEntry.Hashtable.ContainsKey("CardSources"),
        "hashtable has exactly the AS-IS key { \"CardSources\" }");
    Check(retEntry.Hashtable?["CardSources"] is List<CardSource> list && list.Count == 1 && list[0].InstanceId == returned,
        "CardSources is a single-element list of this event's returned card");
    Check(retEntry.BatchId is null, "OnReturnCardsToLibraryFromTrash is batch-less");
}

Console.WriteLine(failures == 0 ? "ALL PASS" : $"{failures} FAILURE(S)");
return failures == 0 ? 0 : 1;
