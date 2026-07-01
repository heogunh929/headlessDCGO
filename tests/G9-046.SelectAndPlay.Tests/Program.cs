using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// PRIM-W5 (G9-046): SelectAndPlayFromZoneEffect — declarative form of PlayPermanentCards(root). Select a
// card in a zone (Trash / Hand) and play it onto the battle area (cost-free).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Play a selected Trash card onto the battle area", () => PlayFrom(ChoiceZone.Trash)),
    ("Play a selected Hand card onto the battle area", () => PlayFrom(ChoiceZone.Hand)),
    ("BuildRequest offers only matching candidates in the zone", CandidateFilter),
    ("AddThisCardToHandEffect -> self returns to hand", SelfReturn),
    ("SelectAndDeDigivolveEffect -> target's top digivolution card removed", DeDigivolve),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failures.Add(test.Name); Console.Error.WriteLine($"FAIL {test.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

async Task PlayFrom(ChoiceZone zone)
{
    EngineContext ctx = Ctx();
    var src = await Place(ctx, P1, "SRC", ChoiceZone.BattleArea);
    var card = await Place(ctx, P1, "PLAYME", zone);
    var eff = (ActivatedSelectAndPlayEffect)CardEffectFactory.SelectAndPlayFromZoneEffect(
        new CardSource(ctx, src, P1), zone, _ => true, 1, false, $"play from {zone}");
    var sink = Sink(ctx);
    eff.Apply(sink, new[] { card });
    await sink.FlushAsync();
    var reader = (IZoneStateReader)ctx.ZoneMover;
    AssertTrue(reader.GetCards(P1, ChoiceZone.BattleArea).Contains(card), "played card is on the battle area");
    AssertTrue(!reader.GetCards(P1, zone).Contains(card), $"played card left {zone}");
}

async Task CandidateFilter()
{
    EngineContext ctx = Ctx();
    var src = await Place(ctx, P1, "SRC", ChoiceZone.BattleArea);
    var match = await Place(ctx, P1, "MATCH", ChoiceZone.Trash);
    var nomatch = await Place(ctx, P1, "OTHER", ChoiceZone.Trash);
    var eff = (ActivatedSelectAndPlayEffect)CardEffectFactory.SelectAndPlayFromZoneEffect(
        new CardSource(ctx, src, P1), ChoiceZone.Trash, id => id.Value.Contains("MATCH"), 1, false, "play match");
    var req = eff.BuildRequest(new[] { P1, P2 });
    // Only the MATCH trash card passes the canTarget filter (NOMATCH is excluded).
    AssertTrue(req.Candidates.Count == 1, $"exactly one candidate offered (got {req.Candidates.Count})");
    _ = (match, nomatch);
}

async Task SelfReturn()
{
    EngineContext ctx = Ctx();
    var self = await Place(ctx, P1, "SELF", ChoiceZone.BattleArea);
    var eff = (ReturnThisCardToHandEffect)CardEffectFactory.AddThisCardToHandEffect(new CardSource(ctx, self, P1));
    var sink = Sink(ctx);
    eff.Apply(sink);
    await sink.FlushAsync();
    var reader = (IZoneStateReader)ctx.ZoneMover;
    AssertTrue(reader.GetCards(P1, ChoiceZone.Hand).Contains(self), "self returned to hand");
    AssertTrue(!reader.GetCards(P1, ChoiceZone.BattleArea).Contains(self), "self left battle area");
}

async Task DeDigivolve()
{
    EngineContext ctx = Ctx();
    var src = await Place(ctx, P1, "SRC", ChoiceZone.BattleArea);
    var target = await Place(ctx, P1, "TGT", ChoiceZone.BattleArea);
    var under = await Place(ctx, P1, "UNDER", ChoiceZone.None);
    // Give the target a 1-card digivolution stack.
    ctx.CardInstanceRepository.TryGetInstance(target, out CardInstanceRecord? r);
    ctx.CardInstanceRepository.Upsert(r! with { Metadata = new Dictionary<string, object?>(r!.Metadata, StringComparer.Ordinal) { ["sourceIds"] = new[] { under.Value } } });

    var eff = (ActivatedSelectAndDeDigivolveEffect)CardEffectFactory.SelectAndDeDigivolveEffect(
        new CardSource(ctx, src, P1), id => id.Value.Contains("TGT"), 1, 1, false, "de-digivolve");
    var sink = Sink(ctx);
    eff.Apply(sink, new[] { target });
    await sink.FlushAsync();

    // The top card was removed; the target no longer battles (peeled off the stack).
    var reader = (IZoneStateReader)ctx.ZoneMover;
    bool targetPeeled = !reader.GetCards(P1, ChoiceZone.BattleArea).Contains(target);
    bool underSurfaced = reader.GetCards(P1, ChoiceZone.BattleArea).Contains(under);
    AssertTrue(targetPeeled || underSurfaced, "top card removed / under-card surfaced (de-digivolved)");
}

// --- Helpers -------------------------------------------------------------

MatchStateMutationSink Sink(EngineContext ctx) => new(
    ctx.CardInstanceRepository, ctx.LogSink, ctx.ZoneMover, ctx.MemoryController, ctx.EffectRegistry, ctx.GameEventQueue);

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 946);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    return ctx;
}

async Task<HeadlessEntityId> Place(EngineContext ctx, HeadlessPlayerId owner, string tag, ChoiceZone zone)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, defId.Value, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:{zone}:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = false }));
    if (zone != ChoiceZone.None)
    {
        await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone));
    }

    return id;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
