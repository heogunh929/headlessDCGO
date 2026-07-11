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
    ("BT1_044: candidates = own-stack under-cards with Level <= 4 ONLY (Lv5 / other-stack excluded)", Bt1044Candidates),
    ("BT1_044: canActivate false when the own stack has no Lv<=4 under-card", Bt1044NoCandidateGate),
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
    var eff = (ActivatedSelectAndPlayEffect)((ActivatedEffect)CardEffectFactory.SelectAndPlayFromZoneEffect(
        new CardSource(ctx, src, P1), zone, _ => true, 1, false, $"play from {zone}")).Body;
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
    var eff = (ActivatedSelectAndPlayEffect)((ActivatedEffect)CardEffectFactory.SelectAndPlayFromZoneEffect(
        new CardSource(ctx, src, P1), ChoiceZone.Trash, id => id.Value.Contains("MATCH"), 1, false, "play match")).Body;
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

// (P1 remediation) AS-IS BT1_044.cs:27-44/55/89 — the [When Attacking] play-from-under candidates are the
// digivolution cards of THIS card's own permanent ONLY (card.PermanentOfThisCard().DigivolutionCards), each
// gated by IsDigimon + CanPlayAsNewPermanent(payCost:false) + Level <= 4 + HasLevel. A prior port wrapped a
// 2-arg default ActivatedPlayFromUnderEffect (all-owner-Digimon scope, no level filter) — these tests pin the
// corrected candidate set.
async Task Bt1044Candidates()
{
    EngineContext ctx = Ctx();
    // BT1_044's own permanent: under-cards LV3 (candidate) and LV5 (excluded by Level <= 4).
    var self = await PlaceLeveled(ctx, P1, "BT44", ChoiceZone.BattleArea, level: 6);
    var lv3 = await PlaceLeveled(ctx, P1, "LV3", ChoiceZone.None, level: 3);
    var lv5 = await PlaceLeveled(ctx, P1, "LV5", ChoiceZone.None, level: 5);
    SetSources(ctx, self, lv3, lv5);
    // ANOTHER of the owner's battle-area Digimon with its own Lv3 under-card — NOT a candidate (self-stack only).
    var other = await PlaceLeveled(ctx, P1, "OTHER", ChoiceZone.BattleArea, level: 4);
    var otherUnder = await PlaceLeveled(ctx, P1, "OU3", ChoiceZone.None, level: 3);
    SetSources(ctx, other, otherUnder);

    var uniform = (ActivatedEffect)new HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Blue.BT1_044()
        .CardEffects(EffectTiming.OnAllyAttack, new CardSource(ctx, self, P1)).Single();
    AssertTrue(uniform.CanResolveActivateHalf(), "canActivate: on battle area with a Lv<=4 own-stack under-card");

    var body = (ActivatedPlayFromUnderEffect)uniform.Body;
    var req = body.BuildRequest(new[] { P1, P2 });
    AssertTrue(req.Candidates.Count == 1, $"exactly ONE candidate (got {req.Candidates.Count})");
    AssertTrue(req.Candidates[0].Id == lv3, "the candidate is the own-stack Lv3 under-card (Lv5 and other-stack Lv3 excluded)");
    AssertTrue(req.MinCount == 1 && !req.CanSkip, "mandatory pick (AS-IS canNoSelect: () => false)");
    _ = (lv5, otherUnder);
}

async Task Bt1044NoCandidateGate()
{
    EngineContext ctx = Ctx();
    // Own stack holds only a Lv5 under-card -> AS-IS CanActivateCondition (BT1_044.cs:51-62) is false.
    var self = await PlaceLeveled(ctx, P1, "BT44", ChoiceZone.BattleArea, level: 6);
    var lv5 = await PlaceLeveled(ctx, P1, "LV5", ChoiceZone.None, level: 5);
    SetSources(ctx, self, lv5);

    var uniform = (ActivatedEffect)new HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Blue.BT1_044()
        .CardEffects(EffectTiming.OnAllyAttack, new CardSource(ctx, self, P1)).Single();
    AssertTrue(!uniform.CanResolveActivateHalf(), "canActivate rejects: no Lv<=4 own-stack under-card");

    var body = (ActivatedPlayFromUnderEffect)uniform.Body;
    AssertTrue(body.BuildRequest(new[] { P1, P2 }).Candidates.Count == 0, "and the body offers no candidate");
}

// --- Helpers -------------------------------------------------------------

async Task<HeadlessEntityId> PlaceLeveled(EngineContext ctx, HeadlessPlayerId owner, string tag, ChoiceZone zone, int level)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, defId.Value, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = level }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:{zone}:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = false }));
    if (zone != ChoiceZone.None)
    {
        await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone));
    }

    return id;
}

void SetSources(EngineContext ctx, HeadlessEntityId host, params HeadlessEntityId[] sources)
{
    ctx.CardInstanceRepository.TryGetInstance(host, out CardInstanceRecord? r);
    ctx.CardInstanceRepository.Upsert(r! with
    {
        Metadata = new Dictionary<string, object?>(r!.Metadata, StringComparer.Ordinal)
        {
            ["sourceIds"] = sources.Select(s => s.Value).ToArray(),
        },
    });
}

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
