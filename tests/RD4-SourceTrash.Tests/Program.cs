using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// (RD-4) AS-IS Permanent.DiscardEvoRoots (CardController.cs:3846, before the top's AddTrashCard at :3852): a
// deleted permanent's digivolution sources are trashed alongside its top card — not just the top. The headless
// port trashed only the top and left the sources dead in ChoiceZone.None. DeletionSourceTrash mirrors the
// source-trash, but is SKIPPED when a source-consuming replacement/replay keyword (Save/Decode/Partition/
// Fortitude) is pending, since the headless POST window / Fortitude replay reads those sources after deletion.

HeadlessPlayerId P1 = new(1);

int failures = 0;
void Check(bool cond, string label)
{
    if (!cond) { Console.Error.WriteLine($"FAIL {label}"); failures++; }
    else { Console.WriteLine($"PASS {label}"); }
}

// Host permanent with two digivolution sources (src0 = bottom/DigiEgg end … src1) sitting in ChoiceZone.None.
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

    var meta = new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["sourceIds"] = new[] { src0.Value, src1.Value },
    };
    foreach ((string key, object? val) in hostFlags)
    {
        meta[key] = val;
    }

    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(host, new HeadlessEntityId("DIGI"), P1, Metadata: meta));
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(src0, new HeadlessEntityId("DIGI"), P1));
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(src1, new HeadlessEntityId("DIGI"), P1));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, host, ChoiceZone.None, ChoiceZone.BattleArea));
    // Sources live in ChoiceZone.None under the host (the digivolution-stack representation).
    return (ctx, host, src0, src1);
}

bool InTrash(EngineContext ctx, HeadlessEntityId id) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(P1, ChoiceZone.Trash).Contains(id);

// Empty stack = 0 sources whether WithSources kept an empty list or dropped the key.
int HostSourceCount(EngineContext ctx, HeadlessEntityId host) =>
    ctx.CardInstanceRepository.TryGetInstance(host, out CardInstanceRecord? r) && r is not null
    && r.Metadata.TryGetValue("sourceIds", out object? raw) && raw is IEnumerable<string> ids ? ids.Count() : 0;

// --- 1. Plain deletion (no source-consuming keyword): all sources go to trash, host stack emptied. ---
{
    var (ctx, host, src0, src1) = await Setup();
    await DeletionSourceTrash.TrashEvoSourcesAsync(ctx.CardInstanceRepository, ctx.ZoneMover, host);
    Check(InTrash(ctx, src0) && InTrash(ctx, src1), "a plain-deleted permanent's evo sources are trashed");
    Check(HostSourceCount(ctx, host) == 0, "the host's source stack is emptied after the trash");
}

// --- 2. Decode pending: sources are STILL held back — the headless POST window plays one from
//        ChoiceZone.None (full AS-IS parity = PRE move, TODO-96). ---
{
    var (ctx, host, src0, src1) = await Setup((DeletionReplacementGate.HasDecodeKey, true));
    await DeletionSourceTrash.TrashEvoSourcesAsync(ctx.CardInstanceRepository, ctx.ZoneMover, host);
    Check(!InTrash(ctx, src0) && !InTrash(ctx, src1), "a Decode card's sources are held for the POST play window");
    Check(HostSourceCount(ctx, host) == 2, "the Decode host keeps both sources");
}

// --- 3. Partition pending: still held (same POST reason as Decode). ---
{
    var (ctx, host, src0, _) = await Setup((DeletionReplacementGate.HasPartitionKey, true));
    await DeletionSourceTrash.TrashEvoSourcesAsync(ctx.CardInstanceRepository, ctx.ZoneMover, host);
    Check(!InTrash(ctx, src0), "a Partition card's sources are held for the POST play window");
}

// --- 4. (P0-3) Save NO LONGER holds its sources back — AS-IS DiscardEvoRoots trashes them unconditionally;
//        Save only relocates the TOP card under a Tamer. ---
{
    var (ctx, host, src0, src1) = await Setup((DeletionReplacementGate.HasSaveKey, true));
    await DeletionSourceTrash.TrashEvoSourcesAsync(ctx.CardInstanceRepository, ctx.ZoneMover, host);
    Check(InTrash(ctx, src0) && InTrash(ctx, src1), "a Save card's sources ARE trashed (Save moves only the top)");
}

// --- 5. (P0-3) Fortitude NO LONGER holds its sources back — eligibility reads the deletion-time count
//        SNAPSHOT, so the sources can be trashed exactly like AS-IS. ---
{
    var (ctx, host, src0, _) = await Setup((DeletionReplacementGate.HasFortitudeKey, true));
    await DeletionSourceTrash.TrashEvoSourcesAsync(ctx.CardInstanceRepository, ctx.ZoneMover, host);
    Check(InTrash(ctx, src0), "a Fortitude card's sources ARE trashed (replay reads the count snapshot, not live)");
}

// --- 6. No sources: a no-op (no throw, nothing added to trash). ---
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 4);
    ctx.TurnController.Initialize(new[] { P1, new HeadlessPlayerId(2) }, P1);
    var cards = (CardDatabase)ctx.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId("DIGI"), "DIGI", "Digi",
        new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var lone = new HeadlessEntityId("p1:LONE");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(lone, new HeadlessEntityId("DIGI"), P1));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, lone, ChoiceZone.None, ChoiceZone.BattleArea));
    await DeletionSourceTrash.TrashEvoSourcesAsync(ctx.CardInstanceRepository, ctx.ZoneMover, lone);
    Check(((IZoneStateReader)ctx.ZoneMover).GetCards(P1, ChoiceZone.Trash).Count == 0, "a sourceless permanent trashes nothing");
}

if (failures > 0) { Console.Error.WriteLine($"\n{failures} test(s) failed."); Environment.Exit(1); }
Console.WriteLine("\nall RD-4 source-trash checks passed.");
