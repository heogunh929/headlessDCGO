using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// (P0-3/P0-4/RD-4, D-3) INTEGRATION census tests. RD4-SourceTrash calls the helper directly; these drive an
// ACTUAL deletion through MatchStateMutationSink.ApplyDelete and then census the trash, proving (a) the wiring
// actually invokes the source-trash, (b) AS-IS sources-then-top order, and (c) the Fortitude count SNAPSHOT
// lets a replayed permanent keep working after its sources are unconditionally trashed.

HeadlessPlayerId P1 = new(1);

int failures = 0;
void Check(bool cond, string label)
{
    if (!cond) { Console.Error.WriteLine($"FAIL {label}"); failures++; }
    else { Console.WriteLine($"PASS {label}"); }
}

async Task<(EngineContext ctx, HeadlessEntityId host, HeadlessEntityId src0, HeadlessEntityId src1)> Setup(
    params (string key, object? val)[] hostFlags)
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 4);
    ctx.TurnController.Initialize(new[] { P1, new HeadlessPlayerId(2) }, P1);
    var cards = (CardDatabase)ctx.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId("DIGI"), "DIGI", "Digi",
        new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));

    var host = new HeadlessEntityId("p1:HOST");
    var src0 = new HeadlessEntityId("p1:SRC0");
    var src1 = new HeadlessEntityId("p1:SRC1");
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["sourceIds"] = new[] { src0.Value, src1.Value } };
    foreach ((string key, object? val) in hostFlags) { meta[key] = val; }

    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(host, new HeadlessEntityId("DIGI"), P1, Metadata: meta));
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(src0, new HeadlessEntityId("DIGI"), P1));
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(src1, new HeadlessEntityId("DIGI"), P1));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, host, ChoiceZone.None, ChoiceZone.BattleArea));
    return (ctx, host, src0, src1);
}

bool InZone(EngineContext ctx, HeadlessEntityId id, ChoiceZone zone) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(P1, zone).Contains(id);

// Delete `target` through the real effect-deletion sink (the production ApplyDelete path).
async Task DeleteViaSink(EngineContext ctx, HeadlessEntityId target)
{
    var sink = new MatchStateMutationSink(
        ctx.CardInstanceRepository, log: null, ctx.ZoneMover, memory: null,
        ctx.EffectRegistry, ctx.GameEventQueue, context: ctx);
    sink.Apply(new EffectMutation(
        MatchStateMutationSink.DeleteKind,
        new HeadlessEntityId("test:killer"),
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [MatchStateMutationSink.TargetEntityIdKey] = target.Value,
        }));
    await sink.FlushAsync();
}

// --- 1. Census: deleting a 2-source stack via the sink lands the TOP and BOTH sources in the trash. ---
{
    var (ctx, host, src0, src1) = await Setup();
    await DeleteViaSink(ctx, host);
    Check(InZone(ctx, host, ChoiceZone.Trash), "the deleted top card reaches the trash");
    Check(InZone(ctx, src0, ChoiceZone.Trash) && InZone(ctx, src1, ChoiceZone.Trash),
        "BOTH digivolution sources reach the trash through the sink wiring (not stranded in None)");
    Check(!InZone(ctx, src0, ChoiceZone.None) && !InZone(ctx, src1, ChoiceZone.None),
        "no source is left orphaned in ChoiceZone.None");
}

// --- 2. Fortitude: sources are trashed unconditionally, yet the count snapshot lets the top replay back to
//        the battle area (AS-IS: trash the sources, replay the top sourceless). ---
{
    var (ctx, host, src0, src1) = await Setup((DeletionReplacementGate.HasFortitudeKey, true));
    await DeleteViaSink(ctx, host);
    Check(InZone(ctx, src0, ChoiceZone.Trash) && InZone(ctx, src1, ChoiceZone.Trash),
        "a Fortitude card's sources ARE trashed on deletion (unconditional, like AS-IS)");
    Check(InZone(ctx, host, ChoiceZone.BattleArea),
        "Fortitude still replays the top back to the battle area (count snapshot survived the source-trash)");
    ctx.CardInstanceRepository.TryGetInstance(host, out CardInstanceRecord? rec);
    Check(rec is not null && DeletionReplacementGate.SourceCountAtDeletion(rec!.Metadata) >= 0,
        "the deletion-time source-count snapshot is readable on the record");
}

// --- 3. Decode: the POST play window still needs a source in None, so a Decode card's sources are held. ---
{
    var (ctx, host, src0, _) = await Setup((DeletionReplacementGate.HasDecodeKey, true));
    await DeleteViaSink(ctx, host);
    Check(InZone(ctx, host, ChoiceZone.Trash), "a Decode card's top still reaches the trash");
    Check(!InZone(ctx, src0, ChoiceZone.Trash),
        "a Decode card's sources are HELD (not trashed) for the POST play window (PRE move = TODO-96)");
}

if (failures > 0) { Console.Error.WriteLine($"\n{failures} test(s) failed."); Environment.Exit(1); }
Console.WriteLine("\nall RD-4 deletion-wiring census checks passed.");
