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
        ctx.GameEventQueue, context: ctx);
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
    ctx.CardInstanceRepository.TryGetInstance(host, out CardInstanceRecord? rec1);
    Check(rec1 is not null && DeletionReplacementGate.SourceCountAtDeletion(rec1!.Metadata) == 2,
        "the deletion-time source-count snapshot recorded the actual count (2), not a fallback");
}

// --- 2. Sources are trashed unconditionally on deletion. (C-Del 3a RETIRED) A BARE hasFortitude metadata
//        marker — no printed FortitudeEffect — no longer replays: the invented gate replay
//        (TryFortitudeReplayAsync) is retired, and AS-IS [Fortitude] now fires through the OnDestroyedAnyone
//        window from a printed/granted effect (witnessed live in tests/C-Del-POST). So the source-trash still
//        runs, but the top stays trashed. ---
{
    var (ctx, host, src0, src1) = await Setup((DeletionReplacementGate.HasFortitudeKey, true));
    await DeleteViaSink(ctx, host);
    Check(InZone(ctx, src0, ChoiceZone.Trash) && InZone(ctx, src1, ChoiceZone.Trash),
        "a Fortitude card's sources ARE trashed on deletion (unconditional, like AS-IS)");
    Check(InZone(ctx, host, ChoiceZone.Trash) && !InZone(ctx, host, ChoiceZone.BattleArea),
        "a bare hasFortitude marker no longer replays via the gate (C-Del 3a retired) — the top stays trashed");
    // The deletion-time count snapshot is still recorded (the source-trash wiring stamps it); nothing cleared it
    // since no replay occurred.
    ctx.CardInstanceRepository.TryGetInstance(host, out CardInstanceRecord? rec);
    Check(rec is not null && DeletionReplacementGate.SourceCountAtDeletion(rec!.Metadata) == 2,
        "the deletion-time source-count snapshot is recorded (2); no gate replay cleared it");
}

// --- 3. (C-Del 3c-2b) The invented gate no longer detects/defers the PRE replacement keywords. A BARE
//        hasDecode metadata marker — no printed DecodeSelfEffect — no longer opens a PRE (would-be-deleted)
//        defer: HasPreOption is false for it, so the deletion is IMMEDIATE (top + sources trashed), exactly
//        like the retired Fortitude marker in block 2. The real Decode/Partition keyword fires ONLY through the
//        AS-IS PRE cut-in window from its printed ActivateClass (routing witnessed by C-Del-3C2B-Witness;
//        window collection by C-Del-PRE). The bare-marker path here is now a wiring census: the sink trashes the
//        top and every source with no gate defer. ---
{
    var (ctx, host, src0, src1) = await Setup((DeletionReplacementGate.HasDecodeKey, true));
    await DeleteViaSink(ctx, host);
    Check(InZone(ctx, host, ChoiceZone.Trash) && !InZone(ctx, host, ChoiceZone.BattleArea),
        "a bare hasDecode marker no longer defers via the gate (C-Del 3c-2b retired) — the top is trashed immediately");
    Check(InZone(ctx, src0, ChoiceZone.Trash) && InZone(ctx, src1, ChoiceZone.Trash),
        "both sources are trashed with the stack (no PRE defer held them back)");
    ctx.CardInstanceRepository.TryGetInstance(host, out CardInstanceRecord? rec3);
    Check(rec3 is not null && !(rec3!.Metadata.TryGetValue(GameFlowProcessor.PendingDeletionKey, out object? pd) && pd is true),
        "the bare-marker Decode card is NOT flagged pendingDeletion (gate firing retired — no PRE defer)");
    Check(rec3 is not null && DeletionReplacementGate.SourceCountAtDeletion(rec3!.Metadata) == 2,
        "the deletion-time source-count snapshot recorded the actual count (2)");
}

// --- 4. (P0-4) The PRE-declined deferred-deletion FINISHER path (GameFlowProcessor.RuleProcessAsync) — a
//        different code path from the sink — must also trash the sources, not just the top. A card marked
//        pending-deletion (no PRE window awaiting) is finished by RuleProcessAsync on the next RunToStable. ---
{
    var (ctx, host, src0, src1) = await Setup();
    // Mark the host pending-deletion WITHOUT any PRE replacement window (so IsPreAwaiting is false and the
    // finisher runs) — mirrors a would-be-deleted window that was declined and now completes.
    ctx.CardInstanceRepository.TryGetInstance(host, out CardInstanceRecord? pre);
    ctx.CardInstanceRepository.Upsert(pre! with
    {
        Metadata = new Dictionary<string, object?>(pre!.Metadata, StringComparer.Ordinal)
        {
            [GameFlowProcessor.PendingDeletionKey] = true,
        }
    });

    await new GameFlowProcessor().RunToStableAsync(ctx);

    Check(InZone(ctx, host, ChoiceZone.Trash), "the finisher path moves the top card to the trash");
    Check(InZone(ctx, src0, ChoiceZone.Trash) && InZone(ctx, src1, ChoiceZone.Trash),
        "the RuleProcessAsync finisher also trashes the sources (P0-4 wiring, not sink-only)");
}

if (failures > 0) { Console.Error.WriteLine($"\n{failures} test(s) failed."); Environment.Exit(1); }
Console.WriteLine("\nall RD-4 deletion-wiring census checks passed.");
