using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// PRIM-W4 batch 1a (G9-038): low-frequency variants reusing existing seams, verified behavior-live:
//  CanNotBlock (self + player-scope) -> EvaluateBlock; CanNotBeDestroyed -> Delete/Prevent replacement
//  (battle + effect); ImmuneFromDPMinus -> DpReduction/Immune replacement; Alliance/Jamming -> player-scope
//  keyword; Ascension -> keyword grant.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("CanNotBlockStaticSelfEffect -> EvaluateBlock restricted", CanNotBlockSelf),
    ("CanNotBlockStaticEffect (scope P2) -> P2's Digimon cannot block", CanNotBlockScoped),
    ("CanNotBeDestroyedStaticEffect -> battle + effect deletion prevented", CanNotBeDestroyed),
    ("ImmuneFromDPMinusStaticEffect -> DP reduction ignored", ImmuneFromDpMinus),
    ("AllianceStaticEffect -> owner's ally has Alliance", () => KeywordScoped(c => CardEffectFactory.AllianceStaticEffect(null, false, c, null), ContinuousKeywordGate.Alliance)),
    ("JammingStaticEffect -> owner's ally has Jamming", () => KeywordScoped(c => CardEffectFactory.JammingStaticEffect(null, false, c, null), ContinuousKeywordGate.Jamming)),
    ("AscensionSelfEffect -> HasKeyword(Ascension)", AscensionSelf),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failures.Add(test.Name); Console.Error.WriteLine($"FAIL {test.Name}: {ex.Message}"); }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

async Task CanNotBlockSelf()
{
    EngineContext context = Context();
    var id = await Place(context, P1, "SELF");
    AssertTrue(!ContinuousRestrictionGate.EvaluateBlock(context, id).IsRestricted, "not restricted before");
    context.EffectRegistry.Register(CardEffectFactory.CanNotBlockStaticSelfEffect(false, new CardSource(context, id, P1), null).ToBinding($"cb:{id.Value}"));
    AssertTrue(ContinuousRestrictionGate.EvaluateBlock(context, id).IsRestricted, "cannot block after grant");
}

async Task CanNotBlockScoped()
{
    EngineContext context = Context();
    var src = await Place(context, P1, "SRC");
    var foe = await Place(context, P2, "FOE");
    AssertTrue(!ContinuousRestrictionGate.EvaluateBlock(context, foe).IsRestricted, "not restricted before");
    context.EffectRegistry.Register(CardEffectFactory.CanNotBlockStaticEffect(P2, false, new CardSource(context, src, P1), null).ToBinding($"cbp:{src.Value}"));
    AssertTrue(ContinuousRestrictionGate.EvaluateBlock(context, foe).IsRestricted, "P2's Digimon cannot block");
}

async Task CanNotBeDestroyed()
{
    EngineContext context = Context();
    var id = await Place(context, P1, "SELF");
    context.EffectRegistry.Register(CardEffectFactory.CanNotBeDestroyedStaticEffect(null, false, new CardSource(context, id, P1), null).ToBinding($"cbd:{id.Value}"));
    AssertTrue(BattleDeletionGate.PreventsBattleDeletion(context, id), "battle deletion prevented");
    // effect deletion
    var sink = Sink(context);
    sink.Apply(new EffectMutation(MatchStateMutationSink.DeleteKind, new HeadlessEntityId("deleter"),
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["targetEntityId"] = id.Value }));
    await sink.FlushAsync();
    AssertTrue(InBattle(context, P1, id), "effect deletion prevented (card survives)");
}

async Task ImmuneFromDpMinus()
{
    EngineContext context = Context();
    var id = await Place(context, P1, "SELF");
    // A -3000 DP reduction from another source.
    context.EffectRegistry.Register(new ContinuousSelfModifierEffect(new CardSource(context, id, P1), ModifierHelpers.DpDeltaKey, -3000, false, null).ToBinding($"dp:{id.Value}"));
    AssertEqual(1000, ContinuousDpGate.ResolveDp(context, id, 4000), "reduction applies before immunity (4000-3000)");
    context.EffectRegistry.Register(CardEffectFactory.ImmuneFromDPMinusStaticEffect(null, false, new CardSource(context, id, P1), null).ToBinding($"imm:{id.Value}"));
    AssertEqual(4000, ContinuousDpGate.ResolveDp(context, id, 4000), "DP reduction ignored under immunity");
}

async Task KeywordScoped(Func<CardSource, ICardEffect> build, string keyword)
{
    EngineContext context = Context();
    var src = await Place(context, P1, "SRC");
    var ally = await Place(context, P1, "ALLY");
    context.EffectRegistry.Register(build(new CardSource(context, src, P1)).ToBinding($"kw:{keyword}:{src.Value}"));
    AssertTrue(ContinuousKeywordGate.HasKeyword(context, ally, keyword), $"owner's ally has {keyword}");
}

async Task AscensionSelf()
{
    EngineContext context = Context();
    var id = await Place(context, P1, "SELF");
    AssertTrue(!ContinuousKeywordGate.HasKeyword(context, id, ContinuousKeywordGate.Ascension), "absent before");
    context.EffectRegistry.Register(CardEffectFactory.AscensionSelfEffect(false, new CardSource(context, id, P1), null).ToBinding($"asc:{id.Value}"));
    AssertTrue(ContinuousKeywordGate.HasKeyword(context, id, ContinuousKeywordGate.Ascension), "Ascension live after grant");
}

// --- Helpers -------------------------------------------------------------

MatchStateMutationSink Sink(EngineContext context) => new(
    context.CardInstanceRepository, context.LogSink, context.ZoneMover, context.MemoryController, context.EffectRegistry, context.GameEventQueue);

bool InBattle(EngineContext context, HeadlessPlayerId owner, HeadlessEntityId id) =>
    context.ZoneMover is IZoneStateReader r && r.GetCards(owner, ChoiceZone.BattleArea).Contains(id);

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 938);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    return context;
}

async Task<HeadlessEntityId> Place(EngineContext context, HeadlessPlayerId owner, string tag)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, defId.Value, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = false }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T e, T a, string label) { if (!EqualityComparer<T>.Default.Equals(e, a)) throw new InvalidOperationException($"{label}: expected '{e}', got '{a}'."); }
