using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// (RD-11 / P0-2) AS-IS stacks a SEPARATE SkillInfo per driving PROCESS, not per deleted card: a delete-PROCESS
// (DestroyPermanentsClass) packs ALL simultaneously-deleted permanents into ONE OnDestroyedAnyone stack whose
// gate is an any-match over the batch (CardController.cs:3736 / CanUseEffects/OnDeletion.cs). So a "when an
// opponent's Digimon is deleted, gain 1 memory" (UNCAPPED) fires ONCE for N simultaneous 0-DP deletions, not N
// times. The headless collector groups a pass as one batch: an OnDestroyedAnyone effect fires at most once per
// pass. (An earlier RD-11 revision over-fired per-card, +2 — corrected by P0-2.) A genuinely SEPARATE delete-
// process falls in a later pass and fires again.
//
// (harness triage, R3-C2b-2 window cutover) The original probe (TfxOnDeleteGainMemory -> TfxTriggeredMemoryEffect)
// was an OLD-MODEL registry-bound effect: its ONLY collection path was AutoProcessingTriggerCollector reading an
// EffectRegistry binding. Post-cutover the registrar stopped lowering window-trigger timings into the registry
// (creg the window path is a live SkillInfo scan), so that probe can never be collected again — the suite's
// ASSERTIONS (the AS-IS per-BATCH expectation, already corrected by P0-2) are unchanged; what changed is the
// COLLECTION MECHANISM. Likewise the driving helper (DpZeroDeletionHelpers.SweepAsync) does a RAW ZoneMover move
// stamping only the deletion-batch marker — the live OnDestroyedAnyone window is opened by
// MatchStateMutationSink's DeleteKind flush (COLLECT-BEFORE-REMOVAL StackSkillInfos, MatchStateMutationSink.cs
// :1170-1181), which the raw move bypasses (RDW-01: a marker-only post-move CardMoved cannot rebuild the AS-IS
// per-permanent OnDeletionHashtable). D1-BatchId.Tests already established the live equivalent (DeleteViaSink
// using the sink's DeleteKind mutation) for OnLeaveFieldAnyone; this file re-attaches the SAME assertions via
// that live mechanism instead, adding the IsDpZeroKey marker so the AS-IS DPZero-only hashtable (IsByBattle=false/
// IsByEffect=false/IsDPZeroDelete=true) is reproduced, and swaps the old-model probe for a new-model ActivateClass
// reactor (mirrors TfxOnLeaveFieldCounter's shape) gated the same way the deleted TfxOnDeleteGainMemory was:
// opponent-owned + DP-zero, uncapped.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

int failures = 0;
void Check(bool cond, string label)
{
    if (!cond) { Console.Error.WriteLine($"FAIL {label}"); failures++; }
    else { Console.WriteLine($"PASS {label}"); }
}

async Task<HeadlessEntityId> PlaceDigimon(EngineContext context, HeadlessPlayerId owner, string tag, int dp)
{
    var cards = (CardDatabase)context.CardRepository;
    var def = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(def, def.Value, tag, new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, owner, Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

EngineContext NewContext(int seed)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: seed);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    context.TurnController.SetPhase(HeadlessPhase.Main); // (harness triage) DoneStartGame gate
    return context;
}

// (harness triage) The live DP-zero delete opener: one MatchStateMutationSink DeleteKind flush == one AS-IS
// DestroyPermanentsClass(LackPowerPermanents).Destroy() batch == one collect-before-removal StackSkillInfos pair.
// IsDpZeroKey marks every target DP-zero (matches AS-IS's DPZero-only hashtable, no live IBattle/ICardEffect).
async Task DeleteViaSink(EngineContext ctx, params HeadlessEntityId[] targets)
{
    var sink = new MatchStateMutationSink(
        ctx.CardInstanceRepository, log: null, ctx.ZoneMover, ctx.MemoryController,
        ctx.GameEventQueue, context: ctx);
    foreach (HeadlessEntityId target in targets)
    {
        sink.Apply(new EffectMutation(
            MatchStateMutationSink.DeleteKind,
            new HeadlessEntityId("rule:dp-zero"),
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [MatchStateMutationSink.TargetEntityIdKey] = target.Value,
                [MatchStateMutationSink.IsDpZeroKey] = true,
            }));
    }

    await sink.FlushAsync();
}

// --- (P0-2) TWO opponent 0-DP deletes in ONE batch (same sweep/pass) -> gain memory ONCE (AS-IS any-match
//     over the delete-process batch fires the effect a single time, not per card). ---
{
    EngineContext context = NewContext(1104);
    var self = await PlaceDigimon(context, P1, "SELF", dp: 4000);
    var foe1 = await PlaceDigimon(context, P2, "FOE1", dp: 0);
    var foe2 = await PlaceDigimon(context, P2, "FOE2", dp: 0);
    CardEffectRegistrar.RegisterOnEnterPlay(context, new TfxOnOpponentDpZeroDeleteMemory(), "TfxOnOpponentDpZeroDeleteMemory", new CardSource(context, self, P1));
    context.MemoryController.Set(0);

    await DeleteViaSink(context, foe1, foe2);
    await new GameFlowProcessor().RunToStableAsync(context);

    Check(context.MemoryController.Current.Current == 1,
        $"an uncapped 'on opponent delete: +1 memory' fires ONCE for a 2-card simultaneous batch (+1, got {context.MemoryController.Current.Current})");
}

// --- Control: ONE deletion -> +1 (single-card batch). ---
{
    EngineContext context = NewContext(1105);
    var self = await PlaceDigimon(context, P1, "SELF", dp: 4000);
    var foe = await PlaceDigimon(context, P2, "FOE", dp: 0);
    CardEffectRegistrar.RegisterOnEnterPlay(context, new TfxOnOpponentDpZeroDeleteMemory(), "TfxOnOpponentDpZeroDeleteMemory", new CardSource(context, self, P1));
    context.MemoryController.Set(0);

    await DeleteViaSink(context, foe);
    await new GameFlowProcessor().RunToStableAsync(context);

    Check(context.MemoryController.Current.Current == 1, $"a single deletion fires it exactly once (+1, got {context.MemoryController.Current.Current})");
}

// --- (P0-2) Two SEPARATE delete-processes (separate passes) fire the effect once EACH (+2 total) — the
//     once-per-pass batch dedup must NOT collapse genuinely sequential deletions across passes. ---
{
    EngineContext context = NewContext(1106);
    var self = await PlaceDigimon(context, P1, "SELF", dp: 4000);
    var foe1 = await PlaceDigimon(context, P2, "FOE1", dp: 0);
    CardEffectRegistrar.RegisterOnEnterPlay(context, new TfxOnOpponentDpZeroDeleteMemory(), "TfxOnOpponentDpZeroDeleteMemory", new CardSource(context, self, P1));
    context.MemoryController.Set(0);

    await DeleteViaSink(context, foe1);
    await new GameFlowProcessor().RunToStableAsync(context);   // batch/pass 1 -> +1

    var foe2 = await PlaceDigimon(context, P2, "FOE2", dp: 0);
    await DeleteViaSink(context, foe2);
    await new GameFlowProcessor().RunToStableAsync(context);   // separate process/pass 2 -> +1

    Check(context.MemoryController.Current.Current == 2,
        $"two separate delete-processes each fire it once (+2 total, got {context.MemoryController.Current.Current})");
}

if (failures > 0) { Console.Error.WriteLine($"\n{failures} test(s) failed."); Environment.Exit(1); }
Console.WriteLine("\nall RD-11 per-event-fire checks passed.");

// (harness triage) TEST-LOCAL new-model replacement for the deleted old-model TfxOnDeleteGainMemory /
// TfxTriggeredMemoryEffect: an UNCAPPED live ActivateClass reactor — "when an opponent's Digimon is deleted by
// DP-zero, gain 1 memory" — collected via the live SkillInfo scan (CEntity_Effect / cEntity_EffectController),
// exactly like TfxOnLeaveFieldCounter (D1-BatchId.Tests) but scoped to OnDestroyedAnyone + opponent + DP-zero
// (mirrors the deleted probe's CanTriggerOnPermanentDeleted(opponent) + IsDPZeroDelete gate).
public sealed class TfxOnOpponentDpZeroDeleteMemory : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Memory +1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            effects.Add(activateClass);

            string EffectDiscription() =>
                "[All Turns] When an opponent's Digimon is deleted by dropping to 0 DP, gain 1 memory (uncapped).";

            bool CanUseCondition(System.Collections.Hashtable hashtable) =>
                CardEffectCommons.IsExistOnBattleArea(card)
                && CardEffectCommons.CanTriggerOnPermanentDeleted(hashtable, permanent => permanent.OwnerId != card.Owner)
                && CardEffectCommons.IsDPZeroDelete(hashtable);

            bool CanActivateCondition(System.Collections.Hashtable hashtable) =>
                CardEffectCommons.IsExistOnBattleArea(card);

            async Task ActivateCoroutine(System.Collections.Hashtable _hashtable) =>
                await card.Owner.AddMemory(1, activateClass);
        }

        return effects;
    }
}
