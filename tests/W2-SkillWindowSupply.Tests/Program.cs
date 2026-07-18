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

GameEvent CounterTimingEvent()
{
    var queue = new GameEventQueue();
    TriggerEventEmitter.Emit(
        queue,
        TriggerTimings.OnCounter,
        actor: owner,
        subject: attacker,
        extraMetadata: null);
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

// --- 1. (P1-2 C2r) OnAllyAttack is NO LONGER supplied here. At the flip, AttackProcess.Attack()'s inline
//        StackSkillInfos insert became the sole opener and the OnAllyAttack emit was removed, so this converter
//        never sees an OnAllyAttack attack event again — the timing is unreachable/unhandled (both plain and
//        effect-driven). OnCounterTiming stays supply-carried (RDW-06 two-pass is C2b), and TryBuildAttack still
//        builds the AS-IS OnAttackCheckHashtableOfPermanent for it. ---
{
    EngineContext context = EngineContext.CreateDefault();
    IReadOnlyList<SkillWindowSupplyEntry> allyEntries = SkillWindowSupply.ConvertEvent(context, AllyAttackEvent());
    Check(allyEntries.Count == 0, "OnAllyAttack is no longer supplied (inline insert is the sole opener; emit removed)");
    Check(SkillWindowSupply.UnhandledTimings(context, AllyAttackEvent()).Contains(EffectTiming.OnAllyAttack),
        "OnAllyAttack is reported UNHANDLED (its window opens via the inline insert, not supply conversion)");

    // OnCounterTiming remains handled: the builder path (TryBuildAttack) is intact.
    IReadOnlyList<SkillWindowSupplyEntry> counterEntries = SkillWindowSupply.ConvertEvent(context, CounterTimingEvent());
    SkillWindowSupplyEntry counterEntry = counterEntries.SingleOrDefault(e => e.Timing == EffectTiming.OnCounterTiming);
    Check(counterEntries.Any(e => e.Timing == EffectTiming.OnCounterTiming), "OnCounterTiming is still handled (one entry produced)");
    Check(counterEntry.Hashtable is not null
        && counterEntry.Hashtable.ContainsKey("AttackingPermanent")
        && counterEntry.Hashtable.ContainsKey("CardEffect"),
        "hashtable has the AS-IS OnAttackCheckHashtableOfPermanent keys {AttackingPermanent, CardEffect}");
    Check(counterEntry.Hashtable?["CardEffect"] is null,
        "CardEffect is null for a plain attack (AS-IS attackEffect=null)");
    Check(counterEntry.Hashtable?["AttackingPermanent"] is PermanentT p && p.InstanceId == attacker,
        "AttackingPermanent is the attacker subject");
    Check(counterEntry.BatchId is null, "an attack entry is batch-less (no cross-batch id)");
}

// --- 2. (P1-2 C2r) An effect-driven OnAllyAttack is likewise not supplied (the whole OnAllyAttack timing is off
//        the supply switch now — the inline insert opens the window regardless of the cause id). ---
{
    EngineContext context = EngineContext.CreateDefault();
    GameEvent ev = AllyAttackEvent(effectDriven: true);
    Check(SkillWindowSupply.ConvertEvent(context, ev).Count == 0,
        "effect-driven OnAllyAttack produces no supply entry (the window opens via the inline insert)");
    Check(SkillWindowSupply.UnhandledTimings(context, ev).Contains(EffectTiming.OnAllyAttack),
        "OnAllyAttack is reported UNHANDLED for an effect-driven attack (opened inline)");
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

// --- 9. (C1d RDW-04) OnEnterFieldAnyone for a PLAYER play: the enriched Hand→BattleArea CardMoved builds the
//        AS-IS OnEnterFieldHashtable (HashtableSetting.cs:157). Non-evolution: top isEvolution=false, one
//        hashtables entry with empty evoRoots/oldLevels; cardEffect null ⇒ NO "CardEffect" key (builder omits). ---
{
    EngineContext context = EngineContext.CreateDefault();
    var played = new HeadlessEntityId("played");
    var playMeta = new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [SkillWindowSupply.OnEnterFieldIsEvolutionKey] = false,
        [SkillWindowSupply.OnEnterFieldIsJogressKey] = false,
        [SkillWindowSupply.OnEnterFieldDigiXrosCountKey] = 0,
        [SkillWindowSupply.OnEnterFieldAssemblyCountKey] = 2,
        [SkillWindowSupply.OnEnterFieldEvoRootIdsKey] = Array.Empty<string>(),
        [SkillWindowSupply.OnEnterFieldOldLevelsKey] = Array.Empty<int>(),
        [SkillWindowSupply.OnEnterFieldIsFromDigimonDigivolutionCardsKey] = false,
    };
    GameEvent ev = new GameEvent(20, GameEventType.CardMoved, "play", playMeta)
    {
        Actor = owner, Subject = played, ZoneFrom = ChoiceZone.Hand, ZoneTo = ChoiceZone.BattleArea,
    };
    SkillWindowSupplyEntry e = SkillWindowSupply.ConvertEvent(context, ev)
        .SingleOrDefault(x => x.Timing == EffectTiming.OnEnterFieldAnyone);
    Hashtable? ht = e.Hashtable;
    Check(ht is not null, "OnEnterFieldAnyone is handled for an enriched play");
    Check(ht is not null && ht.ContainsKey("isEvolution") && ht["isEvolution"] is false
        && ht.ContainsKey("isJogress") && ht.ContainsKey("DigiXrosCount")
        && ht.ContainsKey("AssemblyCount") && (int)ht["AssemblyCount"]! == 2
        && ht.ContainsKey("isFromDigimonDigivolutionCards") && ht.ContainsKey("hashtables"),
        "OnEnterFieldHashtable has the AS-IS top-level keys (isEvolution/isJogress/DigiXrosCount/AssemblyCount/isFromDigimonDigivolutionCards/hashtables)");
    Check(ht is not null && !ht.ContainsKey("CardEffect"), "player play: no CardEffect key (AS-IS omits null cardEffect)");
    Check(ht?["hashtables"] is List<Hashtable> list && list.Count == 1
        && list[0].ContainsKey("Permanent") && list[0]["Permanent"] is PermanentT pp && pp.InstanceId == played
        && list[0].ContainsKey("evoRoots") && ((List<CardSource>)list[0]["evoRoots"]!).Count == 0
        && list[0].ContainsKey("evoRootTops") && list[0].ContainsKey("Root")
        && list[0].ContainsKey("oldLevels") && ((List<int>)list[0]["oldLevels"]!).Count == 0,
        "per-permanent entry has {Permanent(=subject), evoRoots(empty), evoRootTops, Root, oldLevels(empty)}");
    Check(e.BatchId is null, "OnEnterFieldAnyone is batch-less");
    // a BARE Hand→BattleArea move (no enrichment marker) stays UNHANDLED.
    GameEvent bare = new GameEvent(21, GameEventType.CardMoved, "bareplay", new Dictionary<string, object?>())
    { Actor = owner, Subject = played, ZoneFrom = ChoiceZone.Hand, ZoneTo = ChoiceZone.BattleArea };
    Check(SkillWindowSupply.UnhandledTimings(context, bare).Contains(EffectTiming.OnEnterFieldAnyone),
        "an unenriched Hand→BattleArea move is UNHANDLED for OnEnterFieldAnyone");
}

// --- 10. (C1d RDW-04) WhenDigivolving for a digivolve: isEvolution=true, one evoRoot (the target) + oldLevels. ---
{
    EngineContext context = EngineContext.CreateDefault();
    var evolved = new HeadlessEntityId("evolved");
    var target = new HeadlessEntityId("target");
    var queue = new GameEventQueue();
    TriggerEventEmitter.Emit(queue, TriggerTimings.WhenDigivolving, actor: owner, subject: evolved,
        extraMetadata: new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["oldLevel"] = 4,
            [SkillWindowSupply.OnEnterFieldIsEvolutionKey] = true,
            [SkillWindowSupply.OnEnterFieldIsJogressKey] = false,
            [SkillWindowSupply.OnEnterFieldDigiXrosCountKey] = 0,
            [SkillWindowSupply.OnEnterFieldAssemblyCountKey] = 0,
            [SkillWindowSupply.OnEnterFieldEvoRootIdsKey] = new[] { target.Value },
            [SkillWindowSupply.OnEnterFieldOldLevelsKey] = new[] { 4 },
            [SkillWindowSupply.OnEnterFieldIsFromDigimonDigivolutionCardsKey] = false,
        });
    GameEvent ev = queue.DrainPending().Single();
    SkillWindowSupplyEntry e = SkillWindowSupply.ConvertEvent(context, ev)
        .SingleOrDefault(x => x.Timing == EffectTiming.WhenDigivolving);
    Hashtable? ht = e.Hashtable;
    Check(ht is not null && ht["isEvolution"] is true, "WhenDigivolving builds OnEnterFieldHashtable with isEvolution=true");
    Check(ht?["hashtables"] is List<Hashtable> list && list.Count == 1
        && ((List<CardSource>)list[0]["evoRoots"]!).Count == 1 && ((List<CardSource>)list[0]["evoRoots"]!)[0].InstanceId == target
        && ((List<CardSource>)list[0]["evoRootTops"]!).Count == 1
        && ((List<int>)list[0]["oldLevels"]!).SequenceEqual(new[] { 4 }),
        "evoRoots/evoRootTops = [target], oldLevels = [4]");
    Check(ht is not null && list0Permanent(ht) == evolved, "WhenDigivolving Permanent is the digivolved subject");

    static HeadlessEntityId list0Permanent(Hashtable ht) =>
        ((List<Hashtable>)ht["hashtables"]!)[0]["Permanent"] is PermanentT p ? p.InstanceId : default;
}

// --- 11. (C1d RDW-02) OnAddDigivolutionCards inline {Permanent, CardEffect, CardSources, isFromSameDigimon,
//         isFromDigimon} (Permanent.cs:1109-1116). CardEffect rebuilt from the threaded causeSourceId
//         (RD-C1-CARDEFFECT-IDTHREAD closed for OnAddDigivolutionCards — the F1-Tier2 twin of the WhenLinked fix). ---
{
    EngineContext context = EngineContext.CreateDefault();
    var host = new HeadlessEntityId("host");
    var added = new HeadlessEntityId("added1");
    var queue = new GameEventQueue();
    TriggerEventEmitter.Emit(queue, TriggerTimings.OnAddDigivolutionCards, actor: owner, subject: host,
        extraMetadata: new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [SkillWindowSupply.AddDigivolutionAddedCardIdsKey] = added.Value,
            ["causeSourceId"] = "cause",
            [SkillWindowSupply.AddDigivolutionIsFromSameDigimonKey] = true,
            [SkillWindowSupply.AddDigivolutionIsFromDigimonKey] = false,
        });
    GameEvent ev = queue.DrainPending().Single();
    SkillWindowSupplyEntry e = SkillWindowSupply.ConvertEvent(context, ev)
        .SingleOrDefault(x => x.Timing == EffectTiming.OnAddDigivolutionCards);
    Hashtable? ht = e.Hashtable;
    Check(ht is { Count: 5 }
        && ht.ContainsKey("Permanent") && ht.ContainsKey("CardEffect")
        && ht.ContainsKey("CardSources") && ht.ContainsKey("isFromSameDigimon") && ht.ContainsKey("isFromDigimon"),
        "OnAddDigivolutionCards has exactly the 5 AS-IS keys");
    Check(ht?["Permanent"] is PermanentT p && p.InstanceId == host, "Permanent = the host subject");
    // (F1-Tier2 OnAddDigivolutionCards 상환) an effect-driven add carries a non-empty causeSourceId, so the supply
    // rebuilds a non-null BareCauseEffect that passes the AS-IS CardEffect != null gate
    // (CanUseEffects/OnAddDigivolutionCards.cs:24) — the twin of the WhenLinked re-pin below. A cause-LESS add
    // (empty causeSourceId) keeps it null; that null-path is covered by F1-Tier2-OnAddDigivolutionCards's no-cause test.
    Check(ht?["CardEffect"] is not null, "CardEffect = non-null bare cause rebuilt from the threaded causeSourceId (AS-IS 게이트 통과)");
    Check(ht?["CardSources"] is List<CardSource> cs && cs.Count == 1 && cs[0].InstanceId == added, "CardSources = [added card]");
    Check(ht?["isFromSameDigimon"] is true && ht?["isFromDigimon"] is false, "from-flags carried from the emit (pre-computed)");
}

// --- 12. (C1d RDW-02) WhenLinked inline {Permanent, CardEffect, Card, isFromDigimon} (Permanent.cs:1290). ---
{
    EngineContext context = EngineContext.CreateDefault();
    var host = new HeadlessEntityId("linkhost");
    var linkCard = new HeadlessEntityId("linkcard");
    var queue = new GameEventQueue();
    TriggerEventEmitter.Emit(queue, TriggerTimings.WhenLinked, actor: owner, subject: host,
        extraMetadata: new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [SkillWindowSupply.WhenLinkedLinkCardIdKey] = linkCard.Value,
            [SkillWindowSupply.WhenLinkedIsFromDigimonKey] = true,
        });
    GameEvent ev = queue.DrainPending().Single();
    SkillWindowSupplyEntry e = SkillWindowSupply.ConvertEvent(context, ev)
        .SingleOrDefault(x => x.Timing == EffectTiming.WhenLinked);
    Hashtable? ht = e.Hashtable;
    Check(ht is { Count: 4 } && ht.ContainsKey("Permanent") && ht.ContainsKey("CardEffect")
        && ht.ContainsKey("Card") && ht.ContainsKey("isFromDigimon"),
        "WhenLinked has exactly the 4 AS-IS keys");
    Check(ht?["Permanent"] is PermanentT p && p.InstanceId == host, "Permanent = the host subject");
    Check(ht?["Card"] is CardSource card && card.InstanceId == linkCard, "Card = the linked card");
    // (링크 기반 상환) RD-C1-CARDEFFECT-IDTHREAD 잔차 해소 재핀: AS-IS는 링크가 항상 효과-구동이라
    // 비-null cause를 적재(Permanent.cs:1284) — 공급이 BareCauseEffect를 실어 CanTriggerWhenLinked의
    // CardEffect != null 게이트(CanUseEffects/WhenLinked.cs:61)를 통과해야 reactor가 발화한다.
    Check(ht?["CardEffect"] is not null && ht?["isFromDigimon"] is true, "CardEffect non-null bare cause (AS-IS 게이트 통과); isFromDigimon carried");
}

// --- 13. (C1d RDW-07) turn/phase boundaries are HANDLED with a NULL payload (AS-IS StackSkillInfos(null, timing)).
//         The port's TriggerEventEmitter already stamps the explicit timing, so TriggerTimingMap derives them. ---
{
    EngineContext context = EngineContext.CreateDefault();
    foreach ((string timing, EffectTiming expected) in new[]
    {
        (TriggerTimings.OnStartTurn, EffectTiming.OnStartTurn),
        (TriggerTimings.OnStartMainPhase, EffectTiming.OnStartMainPhase),
        (TriggerTimings.OnEndTurn, EffectTiming.OnEndTurn),
    })
    {
        var queue = new GameEventQueue();
        TriggerEventEmitter.Emit(queue, timing, actor: owner);
        GameEvent ev = queue.DrainPending().Single();
        IReadOnlyList<SkillWindowSupplyEntry> entries = SkillWindowSupply.ConvertEvent(context, ev);
        SkillWindowSupplyEntry e = entries.SingleOrDefault(x => x.Timing == expected);
        Check(entries.Any(x => x.Timing == expected), $"{timing} is handled (one entry)");
        Check(e.Timing == expected && e.Hashtable is null, $"{timing} carries the AS-IS null payload");
        Check(!SkillWindowSupply.UnhandledTimings(context, ev).Contains(expected), $"{timing} is not reported UNHANDLED");
    }
}

Console.WriteLine(failures == 0 ? "ALL PASS" : $"{failures} FAILURE(S)");
return failures == 0 ? 0 : 1;
