using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

// FAIL-b: ImmuneFromDeDigivolveClass was inert — the "cannot be de-digivolved" flag had a consumer but NO
// producer (nothing could grant it). ImmuneFromDeDigivolveStaticEffect now registers the continuous restriction,
// and the sink's DeDigivolve handler skips an immune target (AS-IS Permanent.ImmuneFromDeDigivolve()).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("no immunity: de-digivolve removes the top source", () => Run(immune: false, expectRemoved: true)),
    ("ImmuneFromDeDigivolve: the stack is untouched", () => Run(immune: true, expectRemoved: false)),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task Run(bool immune, bool expectRemoved)
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 920);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    // (R3-W3c-4c B5) the live getter Permanent.ImmuneFromDeDigivolve() gates each effect with CanUse (→ CanTrigger,
    // which requires DoneStartGame i.e. a live phase; → IsDisabled → GManager), so drive the sink under an ambient
    // match scope in a live phase (A1/W3c-1/FAILd-06 precedent).
    ctx.TurnController.SetPhase(HeadlessDCGO.Engine.Headless.Runtime.HeadlessPhase.Main);
    using var _ambient = AmbientMatchContext.Enter(ctx);
    var cards = (CardDatabase)ctx.CardRepository;

    // Host with level 5 (above the rookie floor) and one under-source, so a de-digivolve WOULD remove the top.
    // (R3-W3c-4c B5) The continuous immunity is now the AS-IS kind-class ImmuneFromDeDigivolveClass carried on the
    // host's LIVE EffectList (no registry binding); the immune case dispatches the host card definition to the
    // TfxImmuneDeDigivolve fixture (self-scope "Isn't affected by <De-Digivolve>") by CardNumber. The live getter
    // Permanent.ImmuneFromDeDigivolve() (via DeDigivolveHelpers.IsDeDigivolveImmune, consulted by the sink) walks
    // the field permanents' EffectList(None) and finds it.
    string hostNumber = immune ? "TfxImmuneDeDigivolve" : "H";
    cards.Upsert(new CardRecord(new HeadlessEntityId("H"), hostNumber, "Host", new Dictionary<string, object?>(StringComparer.Ordinal) { ["level"] = 5 }, CardType: "Digimon"));
    var host = new HeadlessEntityId("p1:H");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(host, new HeadlessEntityId("H"), P1));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, host, ChoiceZone.None, ChoiceZone.BattleArea));

    cards.Upsert(new CardRecord(new HeadlessEntityId("U"), "U", "Under", new Dictionary<string, object?>(StringComparer.Ordinal) { ["level"] = 4 }, CardType: "Digimon"));
    var under = new HeadlessEntityId("p1:U");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(under, new HeadlessEntityId("U"), P1));
    SetMeta(ctx, host, DigivolutionStackReader.SourceIdsKey, new[] { under.Value });

    var sink = new MatchStateMutationSink(
        ctx.CardInstanceRepository, ctx.LogSink, ctx.ZoneMover, ctx.MemoryController, ctx.GameEventQueue, context: ctx);
    sink.Apply(new EffectMutation(MatchStateMutationSink.DeDigivolveKind, new HeadlessEntityId("p2:cause"),
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [MatchStateMutationSink.TargetEntityIdKey] = host.Value,
            [MatchStateMutationSink.CountKey] = 1,
        }));
    await sink.FlushAsync();

    // De-digivolve trashes the TOP card (H) and promotes the under-source (U). So "removed" == H left the battle
    // area; when immune, H stays with U still under it.
    var zones = (IZoneStateReader)ctx.ZoneMover;
    bool topRemoved = !zones.GetCards(P1, ChoiceZone.BattleArea).Contains(host);
    AssertTrue(topRemoved == expectRemoved, $"top card de-digivolved (left battle area) == {expectRemoved}");
}

void SetMeta(EngineContext ctx, HeadlessEntityId id, string key, object? value)
{
    ctx.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? r);
    ctx.CardInstanceRepository.Upsert(r! with { Metadata = new Dictionary<string, object?>(r!.Metadata, StringComparer.Ordinal) { [key] = value } });
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
