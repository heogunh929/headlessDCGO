using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// M-1 (G9-052): ChangeSecurityDigimonCardDPStaticEffect honours cardCondition 1:1 — the predicate decides the
// affected set INCLUDING which player. "Your opponent's Security Digimon get -2000 DP" must hit the ENEMY's
// Security only, not the owner's (the previous port hardcoded owner-scope = wrong player).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Opponent-targeting cardCondition hits enemy Security only", EnemySecurity),
    ("Self-targeting cardCondition hits own Security only", OwnSecurity),
    ("Security-zone scope: enemy battle-area Digimon unaffected", ZoneScope),
    ("UseRequirements: ignore-color active only with a matching permanent", UseReq),
    ("AddSelfDigivolution cardCondition: added requirement reaches matching cards only", AddSelfCardCond),
    ("M-5 ChangeBaseDPGlobal applies to BOTH players' matching Digimon (not owner-only)", BaseDpGlobal),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task EnemySecurity()
{
    EngineContext ctx = Ctx();
    var src = await Place(ctx, P1, "SRC", ChoiceZone.BattleArea);
    var ownSec = await Place(ctx, P1, "OWNSEC", ChoiceZone.Security);
    var enemySec = await Place(ctx, P2, "ENEMYSEC", ChoiceZone.Security);
    // "Your opponent's Security Digimon get -2000 DP" — predicate selects the enemy (source owner is P1).
    ctx.EffectRegistry.Register(CardEffectFactory.ChangeSecurityDigimonCardDPStaticEffect(
        cs => cs.Owner == P2, -2000, false, new CardSource(ctx, src, P1), null).ToBinding($"sd:{src.Value}"));

    AssertTrue(ContinuousDpGate.ResolveDp(ctx, enemySec, 5000) == 3000, "enemy Security -2000");
    AssertTrue(ContinuousDpGate.ResolveDp(ctx, ownSec, 5000) == 5000, "own Security unchanged (right player)");
}

async Task OwnSecurity()
{
    EngineContext ctx = Ctx();
    var src = await Place(ctx, P1, "SRC", ChoiceZone.BattleArea);
    var ownSec = await Place(ctx, P1, "OWNSEC", ChoiceZone.Security);
    var enemySec = await Place(ctx, P2, "ENEMYSEC", ChoiceZone.Security);
    ctx.EffectRegistry.Register(CardEffectFactory.ChangeSecurityDigimonCardDPStaticEffect(
        cs => cs.Owner == P1, 2000, false, new CardSource(ctx, src, P1), null).ToBinding($"sd:{src.Value}"));

    AssertTrue(ContinuousDpGate.ResolveDp(ctx, ownSec, 5000) == 7000, "own Security +2000");
    AssertTrue(ContinuousDpGate.ResolveDp(ctx, enemySec, 5000) == 5000, "enemy Security unchanged");
}

async Task ZoneScope()
{
    EngineContext ctx = Ctx();
    var src = await Place(ctx, P1, "SRC", ChoiceZone.BattleArea);
    var enemyBattle = await Place(ctx, P2, "ENEMYBATTLE", ChoiceZone.BattleArea);
    ctx.EffectRegistry.Register(CardEffectFactory.ChangeSecurityDigimonCardDPStaticEffect(
        cs => cs.Owner == P2, -2000, false, new CardSource(ctx, src, P1), null).ToBinding($"sd:{src.Value}"));

    AssertTrue(ContinuousDpGate.ResolveDp(ctx, enemyBattle, 5000) == 5000, "enemy BATTLE-area Digimon unaffected (Security-zone scope)");
}

async Task UseReq()
{
    // ignore-color is gated on the owner controlling a Level-5 Digimon (cardCondition via TopCard).
    foreach (bool hasMatch in new[] { false, true })
    {
        EngineContext ctx = Ctx();
        var src = await Place(ctx, P1, "SRC", ChoiceZone.BattleArea);
        if (hasMatch)
        {
            await PlaceLevel(ctx, P1, "MATCH", ChoiceZone.BattleArea, level: 5);
        }

        ctx.EffectRegistry.Register(CardEffectFactory.UseRequirements(new CardSource(ctx, src, P1), cs => cs.Level == 5).ToBinding($"ur:{src.Value}"));

        bool active = ContinuousScopeEvaluation
            .ApplicableEffects(ctx, ContinuousRestrictionGate.Scope, src)
            .Any(e => e.Context.Values.TryGetValue(DigivolveAction.IgnoreColorRequirementKey, out object? v) && v is true);
        AssertTrue(active == hasMatch, $"ignore-color active == {hasMatch} (cardCondition gate honored)");
    }
}

async Task BaseDpGlobal()
{
    EngineContext ctx = Ctx();
    var src = await PlaceLevel(ctx, P1, "SRC", ChoiceZone.BattleArea, level: 4);
    var ownLv5 = await PlaceLevel(ctx, P1, "OWN5", ChoiceZone.BattleArea, level: 5);
    var enemyLv5 = await PlaceLevel(ctx, P2, "ENE5", ChoiceZone.BattleArea, level: 5);
    var enemyLv4 = await PlaceLevel(ctx, P2, "ENE4", ChoiceZone.BattleArea, level: 4);
    // "All Level-5 Digimon get +1000 base DP" — global (both players), predicate = Level==5.
    ctx.EffectRegistry.Register(CardEffectFactory.ChangeBaseDPGlobalEffect(p => p.Level == 5, 1000, false, new CardSource(ctx, src, P1), null).ToBinding($"bdp:{src.Value}"));

    AssertTrue(ContinuousDpGate.ResolveDp(ctx, ownLv5, 5000) == 6000, "own Lv5 +1000 base DP");
    AssertTrue(ContinuousDpGate.ResolveDp(ctx, enemyLv5, 5000) == 6000, "ENEMY Lv5 +1000 too (global, not owner-only)");
    AssertTrue(ContinuousDpGate.ResolveDp(ctx, enemyLv4, 5000) == 5000, "enemy Lv4 unchanged (predicate)");
}

async Task AddSelfCardCond()
{
    EngineContext ctx = Ctx();
    var src = await PlaceNamed(ctx, P1, "SRC", "Sourcemon", ChoiceZone.BattleArea);
    var match = await PlaceNamed(ctx, P1, "MATCH", "UlforceVeedramon", ChoiceZone.Hand);
    var nonMatch = await PlaceNamed(ctx, P1, "OTHER", "Agumon", ChoiceZone.Hand);
    // "Your UlforceVeedramon cards can digivolve from <permanentCondition>" — cardCondition targets other cards.
    ctx.EffectRegistry.Register(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(
        permanentCondition: _ => true, digivolutionCost: 0, ignoreDigivolutionRequirement: false,
        card: new CardSource(ctx, src, P1), condition: null, cardCondition: cs => cs.EqualsCardName("UlforceVeedramon")).ToBinding($"asd:{src.Value}"));

    AssertTrue(HasAddedReq(ctx, match), "added requirement reaches the matching (UlforceVeedramon) card");
    AssertTrue(!HasAddedReq(ctx, nonMatch), "added requirement does NOT reach a non-matching card (cardCondition honored)");
}

bool HasAddedReq(EngineContext ctx, HeadlessEntityId cardId) =>
    ContinuousScopeEvaluation.ApplicableEffects(ctx, ContinuousRestrictionGate.Scope, cardId)
        .Any(e => e.Context.Values.ContainsKey(DigivolveAction.AddedEvolutionPredicateKey));

async Task<HeadlessEntityId> PlaceNamed(EngineContext ctx, HeadlessPlayerId owner, string tag, string name, ChoiceZone zone)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, name, name,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 5000, ["level"] = 6 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:{zone}:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 5000, ["isSuspended"] = false }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone));
    return id;
}

async Task<HeadlessEntityId> PlaceLevel(EngineContext ctx, HeadlessPlayerId owner, string tag, ChoiceZone zone, int level)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 5000, ["level"] = level }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:{zone}:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 5000, ["isSuspended"] = false }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone));
    return id;
}

// --- Helpers ---

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 952);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    return ctx;
}

async Task<HeadlessEntityId> Place(EngineContext ctx, HeadlessPlayerId owner, string tag, ChoiceZone zone)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 5000, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:{zone}:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 5000, ["isSuspended"] = false }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone));
    return id;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
