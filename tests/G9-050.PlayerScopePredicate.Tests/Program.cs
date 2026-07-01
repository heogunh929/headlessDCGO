using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// FR-P1 (G9-050): the player-scope predicate enabler. A player-scope continuous effect can carry an ARBITRARY
// per-permanent predicate (permanentCondition), and ContinuousScopeEvaluation evaluates it against each
// candidate — so "your <narrowed> Digimon get +X" targets exactly the matching set, NOT all Digimon.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Predicate (Level==4) DP buff applies to a Lv4 ally", () => Dp(level: 4, expectBuff: true)),
    ("Predicate (Level==4) DP buff does NOT apply to a Lv3 ally (not flattened to all)", () => Dp(level: 3, expectBuff: false)),
    ("Predicate keyword grant only reaches matching allies", KeywordPredicate),
    ("FR-P2 factories honor permanentCondition (RushStatic / ChangeDPStatic)", FactoryPredicate),
    ("FR-P3 SET-form CanNotBeDestroyed protects only matching allies (sink player-scope)", SetFormDelete),
    ("FR-P3 SET-form CantSuspend blocks suspend only for matching allies", SetFormSuspend),
    ("FR-P3 CanNotAttackSelf(defenderCondition) restricts only matching defenders", DefenderCondition),
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

async Task Dp(int level, bool expectBuff)
{
    EngineContext ctx = Ctx();
    var src = await Place(ctx, P1, "SRC", level: 4);
    var ally = await Place(ctx, P1, "ALLY", level: level);
    // "Your Level-4 Digimon get +1000 DP" — player-scope with an ARBITRARY predicate (not just CardType).
    var eff = new PlayerScopeModifierEffect(new CardSource(ctx, src, P1), ModifierHelpers.DpDeltaKey, 1000,
        scopeCardType: "Digimon", condition: null, scopeZone: null, scopePredicate: cs => cs.Level == 4);
    ctx.EffectRegistry.Register(eff.ToBinding($"psp:{src.Value}"));

    int dp = ContinuousDpGate.ResolveDp(ctx, ally, 4000);
    AssertTrue(dp == (expectBuff ? 5000 : 4000), $"Lv{level} ally DP == {(expectBuff ? 5000 : 4000)} (got {dp})");
}

async Task KeywordPredicate()
{
    EngineContext ctx = Ctx();
    var src = await Place(ctx, P1, "SRC", level: 4);
    var lv4 = await Place(ctx, P1, "LV4", level: 4);
    var lv3 = await Place(ctx, P1, "LV3", level: 3);
    var eff = new ContinuousPlayerScopeKeywordEffect(new CardSource(ctx, src, P1), P1, ContinuousKeywordGate.Rush,
        scopeCardType: null, isInheritedEffect: false, condition: null, scopePredicate: cs => cs.Level == 4);
    ctx.EffectRegistry.Register(eff.ToBinding($"psk:{src.Value}"));

    AssertTrue(ContinuousKeywordGate.HasKeyword(ctx, lv4, ContinuousKeywordGate.Rush), "Lv4 ally has Rush");
    AssertTrue(!ContinuousKeywordGate.HasKeyword(ctx, lv3, ContinuousKeywordGate.Rush), "Lv3 ally does NOT (predicate honored, not flattened)");
}

async Task FactoryPredicate()
{
    EngineContext ctx = Ctx();
    var src = await Place(ctx, P1, "SRC", level: 4);
    var lv4 = await Place(ctx, P1, "LV4", level: 4);
    var lv3 = await Place(ctx, P1, "LV3", level: 3);

    // RushStaticEffect with a NARROWED permanentCondition (only Lv4).
    ctx.EffectRegistry.Register(CardEffectFactory.RushStaticEffect(p => p.Level == 4, false, new CardSource(ctx, src, P1), null).ToBinding($"rush:{src.Value}"));
    AssertTrue(ContinuousKeywordGate.HasKeyword(ctx, lv4, ContinuousKeywordGate.Rush), "Rush on Lv4 ally");
    AssertTrue(!ContinuousKeywordGate.HasKeyword(ctx, lv3, ContinuousKeywordGate.Rush), "no Rush on Lv3 ally (permanentCondition honored)");

    // ChangeDPStaticEffect with a NARROWED permanentCondition (only Lv4).
    ctx.EffectRegistry.Register(CardEffectFactory.ChangeDPStaticEffect(p => p.Level == 4, 1000, false, new CardSource(ctx, src, P1), null).ToBinding($"dp:{src.Value}"));
    AssertTrue(ContinuousDpGate.ResolveDp(ctx, lv4, 4000) == 5000, "Lv4 ally +1000 DP");
    AssertTrue(ContinuousDpGate.ResolveDp(ctx, lv3, 4000) == 4000, "Lv3 ally no DP change (permanentCondition honored)");
}

async Task SetFormDelete()
{
    EngineContext ctx = Ctx();
    var src = await Place(ctx, P1, "SRC", level: 4);
    var lv3 = await Place(ctx, P1, "LV3", level: 3);
    var lv4 = await Place(ctx, P1, "LV4", level: 4);
    // "Your Level-3 Digimon cannot be deleted." SET form (permanentCondition, not self).
    ctx.EffectRegistry.Register(CardEffectFactory.CanNotBeDestroyedStaticEffect(p => p.Level == 3, false, new CardSource(ctx, src, P1), null, null).ToBinding($"cbd:{src.Value}"));

    await Delete(ctx, lv3);
    await Delete(ctx, lv4);
    var reader = (IZoneStateReader)ctx.ZoneMover;
    AssertTrue(reader.GetCards(P1, ChoiceZone.BattleArea).Contains(lv3), "Lv3 ally survives (protected set)");
    AssertTrue(!reader.GetCards(P1, ChoiceZone.BattleArea).Contains(lv4), "Lv4 ally is deleted (not in the set)");
}

async Task SetFormSuspend()
{
    EngineContext ctx = Ctx();
    var src = await Place(ctx, P1, "SRC", level: 4);
    var lv3 = await Place(ctx, P1, "LV3", level: 3);
    var lv4 = await Place(ctx, P1, "LV4", level: 4);
    ctx.EffectRegistry.Register(CardEffectFactory.CantSuspendStaticEffect(p => p.Level == 3, false, new CardSource(ctx, src, P1), null, null).ToBinding($"cs:{src.Value}"));

    await Suspend(ctx, lv3);
    await Suspend(ctx, lv4);
    AssertTrue(!ReadSuspended(ctx, lv3), "Lv3 ally NOT suspended (protected set)");
    AssertTrue(ReadSuspended(ctx, lv4), "Lv4 ally suspended (not in the set)");
}

async Task Delete(EngineContext ctx, HeadlessEntityId id)
{
    var sink = new MatchStateMutationSink(ctx.CardInstanceRepository, ctx.LogSink, ctx.ZoneMover, ctx.MemoryController, ctx.EffectRegistry, ctx.GameEventQueue, context: ctx);
    sink.Apply(new EffectMutation(MatchStateMutationSink.DeleteKind, new HeadlessEntityId("d"), new Dictionary<string, object?>(StringComparer.Ordinal) { ["targetEntityId"] = id.Value }));
    await sink.FlushAsync();
}

async Task Suspend(EngineContext ctx, HeadlessEntityId id)
{
    var sink = new MatchStateMutationSink(ctx.CardInstanceRepository, ctx.LogSink, ctx.ZoneMover, ctx.MemoryController, ctx.EffectRegistry, ctx.GameEventQueue, context: ctx);
    sink.Apply(new EffectMutation(MatchStateMutationSink.SuspendKind, new HeadlessEntityId("s"), new Dictionary<string, object?>(StringComparer.Ordinal) { ["targetEntityId"] = id.Value }));
    await sink.FlushAsync();
}

bool ReadSuspended(EngineContext ctx, HeadlessEntityId id) =>
    ctx.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? r) && r is not null
    && r.Metadata.TryGetValue("isSuspended", out object? raw) && raw is bool b && b;

async Task DefenderCondition()
{
    EngineContext ctx = Ctx();
    var attacker = await Place(ctx, P1, "ATK", level: 5);
    var lv3def = await PlaceOwner(ctx, P2, "D3", level: 3);
    var lv4def = await PlaceOwner(ctx, P2, "D4", level: 4);
    // "This Digimon cannot attack Level-3 Digimon" (defenderCondition), can still attack others.
    ctx.EffectRegistry.Register(CardEffectFactory.CanNotAttackSelfStaticEffect(p => p.Level == 3, false, new CardSource(ctx, attacker, P1), null, null).ToBinding($"cna:{attacker.Value}"));

    AssertTrue(ContinuousRestrictionGate.EvaluateAttack(ctx, attacker, lv3def).IsRestricted, "cannot attack the Lv3 defender");
    AssertTrue(!ContinuousRestrictionGate.EvaluateAttack(ctx, attacker, lv4def).IsRestricted, "CAN attack the Lv4 defender (not over-restricted)");
}

async Task<HeadlessEntityId> PlaceOwner(EngineContext ctx, HeadlessPlayerId owner, string tag, int level)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, defId.Value, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = level }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = false }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

// --- Helpers -------------------------------------------------------------

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 950);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    return ctx;
}

async Task<HeadlessEntityId> Place(EngineContext ctx, HeadlessPlayerId owner, string tag, int level)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, defId.Value, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = level }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = false }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
