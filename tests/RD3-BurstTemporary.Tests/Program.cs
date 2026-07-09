using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// (RD-3) AS-IS Burst Digivolve is TEMPORARY (CardController.cs:1531-1538): at the end of the burst player's
// turn, the burst permanent's TOP card is trashed and it reverts to the prior form. This locks that: a burst
// marker on the top card is promoted to due by the end-turn cleanup (owner's turn), and the due sweep trashes
// the top and promotes the under-source (the permanent survives). Previously the port left burst permanent.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

int failures = 0;
void Check(bool cond, string label)
{
    if (!cond) { Console.Error.WriteLine($"FAIL {label}"); failures++; }
    else { Console.WriteLine($"PASS {label}"); }
}

EngineContext ctx = EngineContext.CreateDefault(randomSeed: 1531);
ctx.TurnController.Initialize(new[] { P1, P2 }, P1);   // P1's turn is ending
var zones = (IZoneStateReader)ctx.ZoneMover;
var cards = (CardDatabase)ctx.CardRepository;
cards.Upsert(new CardRecord(new HeadlessEntityId("DEF"), "DEF", "Def",
    new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));

// Burst stack: burstTop (the burst card, the current top) over underSource (the target it burst-digivolved
// from). underSource lives off-zone as a digivolution source (ChoiceZone.None); burstTop is the battle-area top.
var burstTop = new HeadlessEntityId("p1:BURST");
var underSource = new HeadlessEntityId("p1:UNDER");
ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(burstTop, new HeadlessEntityId("DEF"), P1,
    Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["sourceIds"] = new[] { underSource.Value },
        [GameFlowProcessor.BurstTrashAtTurnEndKey] = true,
    }));
ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(underSource, new HeadlessEntityId("DEF"), P1));
await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, burstTop, ChoiceZone.None, ChoiceZone.BattleArea));

// End-turn cleanup promotes the burst marker to DUE (owner P1's turn is ending).
new HeadlessEndTurnCleanupFlow().Cleanup(ctx, ctx.TurnController.Current);
ctx.CardInstanceRepository.TryGetInstance(burstTop, out CardInstanceRecord? afterCleanup);
Check(afterCleanup!.Metadata.TryGetValue(GameFlowProcessor.BurstTrashAtTurnEndDueKey, out object? due) && due is true,
    "end-turn cleanup promotes the burst marker to DUE on the owner's turn");
Check(!afterCleanup.Metadata.ContainsKey(GameFlowProcessor.BurstTrashAtTurnEndKey),
    "the base burst marker is consumed by the promotion");

// The due sweep (via the game-flow loop) trashes the top card and promotes the under-source.
await new GameFlowProcessor().RunToStableAsync(ctx);

Check(zones.GetCards(P1, ChoiceZone.Trash).Contains(burstTop), "the burst top card is trashed at turn end");
Check(!zones.GetCards(P1, ChoiceZone.BattleArea).Contains(burstTop), "the burst top card is no longer on the battle area");
Check(zones.GetCards(P1, ChoiceZone.BattleArea).Contains(underSource), "the under-source is promoted to the new battle-area top (permanent survives)");

if (failures > 0) { Console.Error.WriteLine($"\n{failures} test(s) failed."); Environment.Exit(1); }
Console.WriteLine("\nall RD-3 burst-temporary checks passed.");
