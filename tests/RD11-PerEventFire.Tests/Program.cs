using HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// (RD-11) AS-IS stacks a SEPARATE SkillInfo per driving event (AutoProcessing.cs:984-989): a "when an
// opponent's Digimon is deleted, gain 1 memory" (UNCAPPED) fires once PER deletion. The headless collector
// previously deduped by EffectId across the WHOLE pass, so two deletions in one pass fired the effect only
// once (+1). With per-(EffectId, event) dedup it fires per deletion (+2).

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

// --- Uncapped: TWO opponent 0-DP deletes in one pass -> gain memory TWICE (once per deletion). ---
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

    Check(context.MemoryController.Current.Current == 2,
        $"an uncapped 'on opponent delete: +1 memory' fires ONCE PER deletion (2 deletes -> +2, got {context.MemoryController.Current.Current})");
}

// --- Control: ONE deletion -> +1 (per-event dedup still collapses a single event's duplicate matches). ---
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

if (failures > 0) { Console.Error.WriteLine($"\n{failures} test(s) failed."); Environment.Exit(1); }
Console.WriteLine("\nall RD-11 per-event-fire checks passed.");
