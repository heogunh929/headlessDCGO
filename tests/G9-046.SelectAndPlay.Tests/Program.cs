using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// PRIM-W5 (G9-046): select-and-play / de-digivolve / play-from-under primitives.
// (R6-Da'-1 WHITEBOX DISPOSAL) The three SelectAndPlayFromZoneEffect cases (PlayFrom(Trash)/PlayFrom(Hand)/
// CandidateFilter) are REMOVED with the deletion of the invented helper `CardEffectFactory.
// SelectAndPlayFromZoneEffect` and its invented body `ActivatedSelectAndPlayEffect` (0 consumers after the
// R6-Da'-1 fixture inline-migration). REAL-RULE COVERAGE EVIDENCE: the play-from-zone rule surface those
// cases exercised is the AS-IS `CardEffectCommons.PlayPermanentCards(..., root)` / `SelectCardEffect`
// (Root.Trash/Hand) flow, covered live by the re-ported printed-card corpus and its suites — e.g. BT9_081
// ([On Play] play from trash: SelectCardEffect Root.Trash + PlayPermanentCards), BT2_090 (zone-card select
// from trash, SelectCardEffect Mode.AddHand/Root.Trash — CardEffect.BT2 batch tests), and the BT1_044
// play-from-under cases RETAINED below (ActivatedPlayFromUnderEffect candidates/gate). The BT1_044 cases
// remain (symbols carry over to A6).
// (R6-Da'-5 WHITEBOX DISPOSAL) The DeDigivolve case is REMOVED with the deletion of the invented body
// `ActivatedSelectAndDeDigivolveEffect` + its factory helper `CardEffectFactory.SelectAndDeDigivolveEffect`
// (census-0 producer; this test was the body's only consumer, a white-box `.Apply(sink, …)` call that never
// touched the resolver switch). REAL-RULE COVERAGE EVIDENCE: the de-digivolve mutation surface it pinned is the
// shared `MatchStateMutationSink.DeDigivolveKind` (host-stack clamp + de-digivolve immunity), covered live by
// `CardEffectCommons.DeDigivolvePermanent` (C5-Witness suite, EX8_051 ESS "<De-Digivolve 1>") and the
// SelectDeDigivolveThenConditionalDestroyEffect / MassDeDigivolveThenConditionalDestroyEffect primitives
// (BT23.PrimTranche4 — BT3_107 / BT3_112).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("AddThisCardToHandEffect -> self returns to hand", SelfReturn),
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

async Task SelfReturn()
{
    EngineContext ctx = Ctx();
    var self = await Place(ctx, P1, "SELF", ChoiceZone.BattleArea);
    // (R7 종점) Re-pointed off the retired invented `ReturnThisCardToHandEffect` / factory
    // `AddThisCardToHandEffect` onto the LIVE self-bounce coroutine it wrapped (AS-IS
    // CardEffectCommons.AddThisCardToHand — the same path ST3_13 / ST4_15 [Security] drive).
    var card = new CardSource(ctx, self, P1);
    using (AmbientMatchContext.Enter(ctx))
    {
        await CardEffectCommons.AddThisCardToHand(card, card);
    }

    var reader = (IZoneStateReader)ctx.ZoneMover;
    AssertTrue(reader.GetCards(P1, ChoiceZone.Hand).Contains(self), "self returned to hand");
    AssertTrue(!reader.GetCards(P1, ChoiceZone.BattleArea).Contains(self), "self left battle area");
}

// (R6-Da'-1 marking — A6 disposal pending) The two BT1_044 cases below are STALE RED: BT1_044 was re-ported
// to the inline AS-IS ActivateClass idiom, so the `(ActivatedEffect)` white-box cast throws
// InvalidCastException (pre-existing before R6-Da'-1; measured red at the batch baseline). Kept UNTOUCHED per
// the R6-Da'-1 rescope decision — they are disposed with the A6 test-pin disposal batch (re-target onto the
// ActivateClass surface or retire with coverage evidence there).
// (P1 remediation) AS-IS BT1_044.cs:27-44/55/89 — the [When Attacking] play-from-under candidates are the
// digivolution cards of THIS card's own permanent ONLY (card.PermanentOfThisCard().DigivolutionCards), each
// gated by IsDigimon + CanPlayAsNewPermanent(payCost:false) + Level <= 4 + HasLevel. A prior port wrapped a
// 2-arg default ActivatedPlayFromUnderEffect (all-owner-Digimon scope, no level filter) — these tests pin the
// corrected candidate set.
async Task Bt1044Candidates()
{
    EngineContext ctx = Ctx(deferred: true);
    // BT1_044's own permanent: under-cards LV3 (candidate) and LV5 (excluded by Level <= 4).
    var self = await PlaceLeveled(ctx, P1, "BT44", ChoiceZone.BattleArea, level: 6);
    var lv3 = await PlaceLeveled(ctx, P1, "LV3", ChoiceZone.None, level: 3);
    var lv5 = await PlaceLeveled(ctx, P1, "LV5", ChoiceZone.None, level: 5);
    SetSources(ctx, self, lv3, lv5);
    // ANOTHER of the owner's battle-area Digimon with its own Lv3 under-card — NOT a candidate (self-stack only).
    var other = await PlaceLeveled(ctx, P1, "OTHER", ChoiceZone.BattleArea, level: 4);
    var otherUnder = await PlaceLeveled(ctx, P1, "OU3", ChoiceZone.None, level: 3);
    SetSources(ctx, other, otherUnder);

    // (uniform-사멸 flip re-target) BT1_044 is the AS-IS inline ActivateClass — assert the per-pass gate on the
    // live ICardEffect surface and read the coroutine's SelectCardEffect request via a deferred drive.
    var eff = (HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects.ActivateClass)new HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Blue.BT1_044()
        .CardEffects(EffectTiming.OnAllyAttack, new CardSource(ctx, self, P1)).Single();
    using var scope = AmbientMatchContext.Enter(ctx);
    AssertTrue(eff.CanActivate(new System.Collections.Hashtable()), "canActivate: on battle area with a Lv<=4 own-stack under-card");

    ChoiceRequest req = await OpenUnderSelect(ctx, eff);
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

    // (uniform-사멸 flip re-target) live per-pass gate on the AS-IS ICardEffect surface.
    var eff = (HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects.ActivateClass)new HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Blue.BT1_044()
        .CardEffects(EffectTiming.OnAllyAttack, new CardSource(ctx, self, P1)).Single();
    using var scope = AmbientMatchContext.Enter(ctx);
    AssertTrue(!eff.CanActivate(new System.Collections.Hashtable()), "canActivate rejects: no Lv<=4 own-stack under-card");
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
    ctx.CardInstanceRepository, ctx.LogSink, ctx.ZoneMover, ctx.MemoryController, ctx.GameEventQueue);

EngineContext Ctx(bool deferred = false)
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 946, deferredChoice: deferred);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    return ctx;
}

// (uniform-사멸 flip) Drive BT1_044's ActivateClass coroutine with the DEFERRED provider until its
// SelectCardEffect surfaces, then return the pending request (the live candidate/min rule surface).
async Task<ChoiceRequest> OpenUnderSelect(EngineContext ctx, HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects.ActivateClass eff)
{
    try
    {
        await eff.Activate(new System.Collections.Hashtable());
    }
    catch (DeferredChoicePendingException)
    {
    }

    ChoiceRequest? pending = ctx.ChoiceController.PendingRequest;
    AssertTrue(pending is not null, "the under-card select surfaced to the agent (deferred)");
    return pending!;
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
