using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// M-4 (G9-055): Decoy un-seal. DecoySelfEffect grants the Decoy KEYWORD, but the redirect mechanism
// (DeletionReplacementGate.FindDecoyRedirect) previously only recognised a HasDecoy METADATA flag that is
// never set in production — so Decoy was inert. Now the redirect also recognises the live keyword.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Decoy-keyword ally is found as a redirect for an enemy-caused deletion", KeywordRecognised),
    ("Without the effectRegistry (no keyword lookup) the holder is NOT found (control = old behaviour)", NoRegistryControl),
    ("An ally WITHOUT Decoy is not a redirect (control)", NoDecoyControl),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task KeywordRecognised()
{
    EngineContext ctx = Ctx();
    var target = await Place(ctx, P1, "TARGET");
    var holder = await Place(ctx, P1, "HOLDER");
    var enemyDeleter = await Place(ctx, P2, "ENEMY");
    GrantDecoy(ctx, holder);

    var targetRec = Rec(ctx, target);
    var redirect = DeletionReplacementGate.FindDecoyRedirect((ICardInstanceRepository)ctx.CardInstanceRepository, (IZoneStateReader)ctx.ZoneMover, targetRec, enemyDeleter, effectRegistry: ctx.EffectRegistry);
    AssertTrue(redirect == holder, "the Decoy-keyword holder is found as the redirect");
}

async Task NoRegistryControl()
{
    EngineContext ctx = Ctx();
    var target = await Place(ctx, P1, "TARGET");
    var holder = await Place(ctx, P1, "HOLDER");
    var enemyDeleter = await Place(ctx, P2, "ENEMY");
    GrantDecoy(ctx, holder);

    var targetRec = Rec(ctx, target);
    var redirect = DeletionReplacementGate.FindDecoyRedirect((ICardInstanceRepository)ctx.CardInstanceRepository, (IZoneStateReader)ctx.ZoneMover, targetRec, enemyDeleter);
    AssertTrue(redirect is null, "no registry -> keyword not recognised -> no redirect (old sealed behaviour)");
}

async Task NoDecoyControl()
{
    EngineContext ctx = Ctx();
    var target = await Place(ctx, P1, "TARGET");
    await Place(ctx, P1, "PLAIN");
    var enemyDeleter = await Place(ctx, P2, "ENEMY");

    var targetRec = Rec(ctx, target);
    var redirect = DeletionReplacementGate.FindDecoyRedirect((ICardInstanceRepository)ctx.CardInstanceRepository, (IZoneStateReader)ctx.ZoneMover, targetRec, enemyDeleter, effectRegistry: ctx.EffectRegistry);
    AssertTrue(redirect is null, "no Decoy ally -> no redirect");
}

// --- Helpers ---

void GrantDecoy(EngineContext ctx, HeadlessEntityId holder) =>
    ctx.EffectRegistry.Register(CardEffectFactory.DecoySelfEffect(false, new CardSource(ctx, holder, P1), null).ToBinding($"decoy:{holder.Value}"));

CardInstanceRecord Rec(EngineContext ctx, HeadlessEntityId id) =>
    ctx.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? r) && r is not null ? r : throw new InvalidOperationException("no record");

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 955);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    return ctx;
}

async Task<HeadlessEntityId> Place(EngineContext ctx, HeadlessPlayerId owner, string tag)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = false }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
