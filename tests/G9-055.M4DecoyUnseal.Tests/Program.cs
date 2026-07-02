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
    ("(D1) permanentCondition: MATCHING protected target -> redirect found", PredicateMatchRedirects),
    ("(D1) permanentCondition: NON-matching protected target -> no redirect (predicate honored, not flattened)", PredicateMismatchNoRedirect),
    ("(D1) permanentCondition without context (sink defer superset) -> still eligible", PredicateWithoutContextSuperset),
    ("(D1) AS-IS protects DIGIMON only: a Tamer target is not redirected (with context)", TamerTargetNotProtected),
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

// (D1) AS-IS Decoy.cs:51 — permanentCondition narrows the PROTECTED permanent (e.g. "Decoy ([Bagra
// Army])"), evaluated live at redirect time. Modeled here with a Level predicate (Level==4).

async Task PredicateMatchRedirects()
{
    EngineContext ctx = Ctx();
    var target = await Place(ctx, P1, "TARGET", level: 4);
    var holder = await Place(ctx, P1, "HOLDER", level: 3);
    var enemyDeleter = await Place(ctx, P2, "ENEMY");
    GrantDecoy(ctx, holder, p => p.Level == 4);

    var redirect = DeletionReplacementGate.FindDecoyRedirect(
        (ICardInstanceRepository)ctx.CardInstanceRepository, (IZoneStateReader)ctx.ZoneMover, Rec(ctx, target), enemyDeleter,
        effectRegistry: ctx.EffectRegistry, context: ctx);
    AssertTrue(redirect == holder, "matching protected target -> the predicate Decoy redirects");
}

async Task PredicateMismatchNoRedirect()
{
    EngineContext ctx = Ctx();
    var target = await Place(ctx, P1, "TARGET", level: 3);
    var holder = await Place(ctx, P1, "HOLDER", level: 3);
    var enemyDeleter = await Place(ctx, P2, "ENEMY");
    GrantDecoy(ctx, holder, p => p.Level == 4);

    var redirect = DeletionReplacementGate.FindDecoyRedirect(
        (ICardInstanceRepository)ctx.CardInstanceRepository, (IZoneStateReader)ctx.ZoneMover, Rec(ctx, target), enemyDeleter,
        effectRegistry: ctx.EffectRegistry, context: ctx);
    AssertTrue(redirect is null, "non-matching protected target -> the predicate Decoy does NOT redirect");
}

async Task PredicateWithoutContextSuperset()
{
    EngineContext ctx = Ctx();
    var target = await Place(ctx, P1, "TARGET", level: 3);
    var holder = await Place(ctx, P1, "HOLDER", level: 3);
    var enemyDeleter = await Place(ctx, P2, "ENEMY");
    GrantDecoy(ctx, holder, p => p.Level == 4);

    // Context-less = the sink's defer decision (documented safe superset): a stored predicate passes;
    // the context-aware choice paths re-evaluate strictly (previous test).
    var redirect = DeletionReplacementGate.FindDecoyRedirect(
        (ICardInstanceRepository)ctx.CardInstanceRepository, (IZoneStateReader)ctx.ZoneMover, Rec(ctx, target), enemyDeleter,
        effectRegistry: ctx.EffectRegistry);
    AssertTrue(redirect == holder, "no context -> predicate treated as passing (superset defer)");
}

async Task TamerTargetNotProtected()
{
    EngineContext ctx = Ctx();
    var target = await Place(ctx, P1, "TARGET", cardType: "Tamer");
    var holder = await Place(ctx, P1, "HOLDER");
    var enemyDeleter = await Place(ctx, P2, "ENEMY");
    GrantDecoy(ctx, holder);

    var redirect = DeletionReplacementGate.FindDecoyRedirect(
        (ICardInstanceRepository)ctx.CardInstanceRepository, (IZoneStateReader)ctx.ZoneMover, Rec(ctx, target), enemyDeleter,
        effectRegistry: ctx.EffectRegistry, context: ctx);
    AssertTrue(redirect is null, "a Tamer target is not a Decoy-protected permanent (AS-IS Digimon-only)");
}

// --- Helpers ---

void GrantDecoy(EngineContext ctx, HeadlessEntityId holder, Func<Permanent, bool>? permanentCondition = null) =>
    ctx.EffectRegistry.Register(CardEffectFactory.DecoySelfEffect(false, new CardSource(ctx, holder, P1), null, permanentCondition).ToBinding($"decoy:{holder.Value}"));

CardInstanceRecord Rec(EngineContext ctx, HeadlessEntityId id) =>
    ctx.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? r) && r is not null ? r : throw new InvalidOperationException("no record");

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 955);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    return ctx;
}

async Task<HeadlessEntityId> Place(EngineContext ctx, HeadlessPlayerId owner, string tag, int level = 4, string cardType = "Digimon")
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = level }, CardType: cardType));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = false, ["level"] = level }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
