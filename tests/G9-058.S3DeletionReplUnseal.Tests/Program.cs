using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// S3 (G9-058): deletion-replacement keywords un-sealed. The keyword is granted as a continuous keyword
// (EvadeSelfEffect etc.), but the option-gating (DeletionReplacementTiming.PreOptions) previously read a
// metadata flag never set in production. Now it recognises the LIVE keyword — so the replacement OPTION surfaces.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Evade granted via keyword -> EvadeOption offered (un-sealed)", () => Offered("EVADE", CardEffectFactory.EvadeSelfEffect, DeletionReplacementTiming.EvadeOption, byBattle: false)),
    ("Without the keyword -> no EvadeOption (control)", () => NotOffered("PLAIN", DeletionReplacementTiming.EvadeOption, byBattle: false)),
    ("Barrier granted via keyword (byBattle) -> BarrierOption offered", () => Offered("BARRIER", CardEffectFactory.BarrierSelfEffect, DeletionReplacementTiming.BarrierOption, byBattle: true, security: true)),
    ("(C1) Fragment <3>: the grant's trashValue gates the option (2 sources no / 3 sources yes)", FragmentTrashValueGates),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task Offered(string tag, Func<bool, CardSource, Func<bool>?, ICardEffect> factory, string option, bool byBattle, bool security = false)
{
    EngineContext ctx = Ctx();
    var id = await Place(ctx, P1, tag);
    if (security) { await PlaceSecurity(ctx, P1); }
    ctx.EffectRegistry.Register(factory(false, new CardSource(ctx, id, P1), null).ToBinding($"kw:{id.Value}"));
    var record = Rec(ctx, id);
    var options = DeletionReplacementTiming.PreOptions(ctx.CardInstanceRepository, (IZoneStateReader)ctx.ZoneMover, record, byBattle, ctx.EffectRegistry);
    AssertTrue(options.Contains(option), $"{option} offered for keyword-granted {tag} (un-sealed)");
}

async Task NotOffered(string tag, string option, bool byBattle)
{
    EngineContext ctx = Ctx();
    var id = await Place(ctx, P1, tag);
    var record = Rec(ctx, id);
    var options = DeletionReplacementTiming.PreOptions(ctx.CardInstanceRepository, (IZoneStateReader)ctx.ZoneMover, record, byBattle, ctx.EffectRegistry);
    AssertTrue(!options.Contains(option), $"{option} NOT offered without the keyword");
}

// (C1) AS-IS Fragment <X>: CanActivateFragment(p, trashValue) — the grant's X (previously dropped,
// collapsing every Fragment to 1) gates on DigivolutionCards.Count >= X.
async Task FragmentTrashValueGates()
{
    foreach ((int sourceCount, bool offered) in new[] { (2, false), (3, true) })
    {
        EngineContext ctx = Ctx();
        var id = await Place(ctx, P1, $"FRAG{sourceCount}");
        var sources = Enumerable.Range(1, sourceCount).Select(i => new HeadlessEntityId($"frag-src-{i}")).ToArray();
        foreach (HeadlessEntityId src in sources)
        {
            ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(src, new HeadlessEntityId("DEF:SRC"), P1));
        }

        var rec0 = Rec(ctx, id);
        ctx.CardInstanceRepository.Upsert(rec0 with
        {
            Metadata = new Dictionary<string, object?>(rec0.Metadata, StringComparer.Ordinal)
            {
                [DeletionReplacementGate.SourceIdsKey] = sources.Select(s => s.Value).ToArray(),
            }
        });
        ctx.EffectRegistry.Register(CardEffectFactory.FragmentSelfEffect(
            false, new CardSource(ctx, id, P1), null, trashValue: 3).ToBinding($"frag:{id.Value}"));

        var record = Rec(ctx, id);
        AssertTrue(DeletionReplacementGate.FragmentCostOf(record, ctx.EffectRegistry) == 3, "the grant's trashValue is the cost");
        var options = DeletionReplacementTiming.PreOptions(ctx.CardInstanceRepository, (IZoneStateReader)ctx.ZoneMover, record, byBattle: false, ctx.EffectRegistry);
        AssertTrue(options.Contains(DeletionReplacementTiming.FragmentOption) == offered,
            $"{sourceCount} sources with Fragment<3> -> offered={offered}");
    }
}

// --- Helpers ---

CardInstanceRecord Rec(EngineContext ctx, HeadlessEntityId id) =>
    ctx.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? r) && r is not null ? r : throw new InvalidOperationException("no record");

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 958);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    return ctx;
}

async Task<HeadlessEntityId> Place(EngineContext ctx, HeadlessPlayerId owner, string tag)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = false }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

async Task PlaceSecurity(EngineContext ctx, HeadlessPlayerId owner)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId("DEF:SEC");
    cards.Upsert(new CardRecord(defId, "SEC", "SEC", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:sec:1");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner, Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.Security));
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
