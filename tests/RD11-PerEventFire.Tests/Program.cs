using HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// (RD-11 / P0-2) AS-IS stacks a SEPARATE SkillInfo per driving PROCESS, not per deleted card: a delete-PROCESS
// (DestroyPermanentsClass) packs ALL simultaneously-deleted permanents into ONE OnDestroyedAnyone stack whose
// gate is an any-match over the batch (CardController.cs:3736 / CanUseEffects/OnDeletion.cs). So a "when an
// opponent's Digimon is deleted, gain 1 memory" (UNCAPPED) fires ONCE for N simultaneous 0-DP deletions, not N
// times. The headless collector groups a pass as one batch: an OnDestroyedAnyone effect fires at most once per
// pass. (An earlier RD-11 revision over-fired per-card, +2 — corrected by P0-2.) A genuinely SEPARATE delete-
// process falls in a later pass and fires again.

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

// --- (P0-2) TWO opponent 0-DP deletes in ONE batch (same sweep/pass) -> gain memory ONCE (AS-IS any-match
//     over the delete-process batch fires the effect a single time, not per card). ---
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 1104);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    var self = await PlaceDigimon(context, P1, "SELF", dp: 4000);
    await PlaceDigimon(context, P2, "FOE1", dp: 0);
    await PlaceDigimon(context, P2, "FOE2", dp: 0);
    CardEffectRegistrar.RegisterOnEnterPlay(context, new TfxOnDeleteGainMemory(), "TfxOnDeleteGainMemory", new CardSource(context, self, P1));
    context.MemoryController.Set(0);

    await DpZeroDeletionHelpers.SweepAsync(context, new[] { P1, P2 });
    await new GameFlowProcessor().RunToStableAsync(context);

    Check(context.MemoryController.Current.Current == 1,
        $"an uncapped 'on opponent delete: +1 memory' fires ONCE for a 2-card simultaneous batch (+1, got {context.MemoryController.Current.Current})");
}

// --- Control: ONE deletion -> +1 (single-card batch). ---
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 1105);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    var self = await PlaceDigimon(context, P1, "SELF", dp: 4000);
    await PlaceDigimon(context, P2, "FOE", dp: 0);
    CardEffectRegistrar.RegisterOnEnterPlay(context, new TfxOnDeleteGainMemory(), "TfxOnDeleteGainMemory", new CardSource(context, self, P1));
    context.MemoryController.Set(0);

    await DpZeroDeletionHelpers.SweepAsync(context, new[] { P1, P2 });
    await new GameFlowProcessor().RunToStableAsync(context);

    Check(context.MemoryController.Current.Current == 1, $"a single deletion fires it exactly once (+1, got {context.MemoryController.Current.Current})");
}

// --- (P0-2) Two SEPARATE delete-processes (separate passes) fire the effect once EACH (+2 total) — the
//     once-per-pass batch dedup must NOT collapse genuinely sequential deletions across passes. ---
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 1106);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    var self = await PlaceDigimon(context, P1, "SELF", dp: 4000);
    await PlaceDigimon(context, P2, "FOE1", dp: 0);
    CardEffectRegistrar.RegisterOnEnterPlay(context, new TfxOnDeleteGainMemory(), "TfxOnDeleteGainMemory", new CardSource(context, self, P1));
    context.MemoryController.Set(0);

    await DpZeroDeletionHelpers.SweepAsync(context, new[] { P1, P2 });
    await new GameFlowProcessor().RunToStableAsync(context);   // batch/pass 1 -> +1

    await PlaceDigimon(context, P2, "FOE2", dp: 0);
    await DpZeroDeletionHelpers.SweepAsync(context, new[] { P1, P2 });
    await new GameFlowProcessor().RunToStableAsync(context);   // separate process/pass 2 -> +1

    Check(context.MemoryController.Current.Current == 2,
        $"two separate delete-processes each fire it once (+2 total, got {context.MemoryController.Current.Current})");
}

if (failures > 0) { Console.Error.WriteLine($"\n{failures} test(s) failed."); Environment.Exit(1); }
Console.WriteLine("\nall RD-11 per-event-fire checks passed.");
